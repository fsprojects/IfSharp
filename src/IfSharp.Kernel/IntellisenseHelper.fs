namespace IfSharp.Kernel

open System
open System.Text
open Microsoft.FSharp.Compiler
open Microsoft.FSharp.Compiler.Ast
open Microsoft.FSharp.Compiler.SourceCodeServices

type TooltipInfo = 
    {
        LineIndex: int;
        StartIndex: int;
        EndIndex: int;
        Tooltip: string;
    }

type SimpleDeclaration =
    {
        ToolTip: string;
        Glyph: int;
        Name: string;
    }

type IntellisenseHelper (additionalArgs : array<string>) = 

    // Create an interactive checker instance (ignore notifications)
    let file = "/home/Temp.fsx"
    let checker = InteractiveChecker.Create()
    let mutable lastResults : CheckFileResults option = None

    member this.LastResults
        with get () = lastResults
        and set (value) = lastResults <- value

    member this.ParseWithTypeInfo (input) = 
    
        // We first need to get the untyped info
        let checkOptions = checker.GetProjectOptionsFromScript(file, input, DateTime.Now, additionalArgs)
        let untypedRes = checker.ParseFileInProject(file, input, checkOptions)
    
        // This might need some time - wait until all DLLs are loaded etc.
        let rec waitForTypeCheck(n) = async {
            let typedRes = checker.CheckFileInProjectIfReady(untypedRes, file, 0, input, checkOptions, IsResultObsolete(fun _ -> false), null)
            match typedRes with
            | Some(CheckFileAnswer.Succeeded(res)) -> return untypedRes, res
            | res when n > 100 -> return failwithf "Parsing did not finish... (%A)" res
            | _ -> 
                do! Async.Sleep(100)
                return! waitForTypeCheck(n + 1)
        }

        waitForTypeCheck 0 |> Async.RunSynchronously

    member this.BuildFormatComment (xmlCommentRetriever: string * string -> string) cmt (sb: StringBuilder) =
        match cmt with
        | XmlCommentText(s) -> sb.AppendLine(s) |> ignore
        | XmlCommentSignature(file, signature) ->
            let comment = xmlCommentRetriever (file, signature)
            if (not (comment.Equals(null))) && comment.Length > 0 then sb.AppendLine(comment) |> ignore
        | XmlCommentNone -> ()

    member this.BuildFormatElement isSingle el (sb: StringBuilder) xmlCommentRetriever =
        
        match el with
        | ToolTipElementNone -> ()
        | ToolTipElement(it, comment) ->
            sb.AppendLine(it) |> this.BuildFormatComment xmlCommentRetriever comment
        | ToolTipElementGroup(items) ->
            let items, msg =
                if items.Length > 10 then
                    (items |> Seq.take 10 |> List.ofSeq),
                    sprintf "   (+%d other overloads)" (items.Length - 10)
                else items, null
            if isSingle && items.Length > 1 then
                sb.AppendLine("Multiple overloads") |> ignore
            for (it, comment) in items do
                sb.AppendLine(it) |> this.BuildFormatComment xmlCommentRetriever comment
            if msg <> null then sb.AppendFormat(msg) |> ignore
        | ToolTipElementCompositionError(err) ->
            sb.Append("Composition error: " + err) |> ignore

    // Convert DataTipText to string
    member this.FormatTip (tip, xmlCommentRetriever) =
        let commentRetriever = defaultArg xmlCommentRetriever (fun _ -> "")
        let sb = new StringBuilder()
        match tip with
        | ToolTipText([single]) -> this.BuildFormatElement true single sb commentRetriever
        | ToolTipText(its) -> for item in its do this.BuildFormatElement false item sb commentRetriever
        sb.ToString().Trim('\n', '\r')

    member this.ExtractNames (line : string) (charIndex : int) =
        let find = line.LastIndexOfAny([| ' '; '\t'; '\r'; |], Math.Max(charIndex, 1) - 1)
        let start = find + 1
        let len = charIndex - start
        let splits = line.Substring(start, len).Split('.')
        splits |> Seq.take (splits.Length - 1) |> Seq.toList

    member this.ExtractTooltipName (line : string) (charIndex : int) =
        let find1 = line.LastIndexOfAny([| ' '; '\t'; '\r'; '('; ')'; |], Math.Max(charIndex, 1) - 1)
        let find2 = line.IndexOfAny([| ' '; '\t'; '\r'; '.'; '('; ')'; |], charIndex)
        let startIdx = if find1 = -1 then 0 else find1
        let endIdx = if find2 = -1 then line.Length else find2

        let splits = line.Substring(startIdx, endIdx - startIdx).Trim().Split('.')
        let names = splits |> Seq.toList

        (startIdx, endIdx, names)

    member this.GetToolTip (source:string) (lineIndex, charIndex) =

        let inputLines = source.Split('\n')

        // Get untyped & typed information and get code for the IDENT token (needed later)
        let identToken = Parser.tagOfToken(Parser.token.IDENT("")) 
        let untyped, parsed = this.ParseWithTypeInfo(source)
        let line = inputLines.[lineIndex]
        let (startIdx, endIdx, names) = this.ExtractTooltipName line charIndex

        // Get tool tip at the specified location
        let tip = parsed.GetToolTipText(lineIndex, charIndex, line, names, identToken)
        let str = this.FormatTip(tip, None)

        { LineIndex = lineIndex; StartIndex = startIdx; EndIndex = endIdx; Tooltip = str; }

    member this.GetDeclarations (source:string) (lineIndex, charIndex) =

        let inputLines = source.Split('\n')
        let line = inputLines.[lineIndex]
        let names = this.ExtractNames line charIndex

        // parse
        let untyped, parsed = this.ParseWithTypeInfo(source)

        // get declarations for a location
        let decls = 
            parsed.GetDeclarations(Some untyped, lineIndex, charIndex, source, names, "", fun _ -> false)
            //parsed.GetDeclarations(Some untyped, (lineIndex, charIndex), source, (names, ""), fun _ -> false)
            |> Async.RunSynchronously
    
        let items = 
            decls.Items
            |> Seq.map (fun x -> { ToolTip = this.FormatTip(x.DescriptionText, None); Glyph = x.Glyph; Name = x.Name })

        (names, items)
