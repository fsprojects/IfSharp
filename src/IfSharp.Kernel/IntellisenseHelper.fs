namespace IfSharp.Kernel

module IntellisenseHelper = 

    open System
    open System.Text
    open Microsoft.FSharp.Compiler
    open Microsoft.FSharp.Compiler.Ast
    open Microsoft.FSharp.Compiler.SourceCodeServices

    // Create an interactive checker instance (ignore notifications)
    let checker = InteractiveChecker.Create(NotifyFileTypeCheckStateIsDirty ignore)

    let parseWithTypeInfo (file, input) = 
    
        // We first need to get the untyped info
        let checkOptions = checker.GetCheckOptionsFromScriptRoot(file, input, DateTime.Now, [| |])
        let untypedRes = checker.UntypedParse(file, input, checkOptions)
    
        // This might need some time - wait until all DLLs are loaded etc.
        let rec waitForTypeCheck(n) = async {
            let typedRes = checker.TypeCheckSource(untypedRes, file, 0, input, checkOptions, IsResultObsolete(fun _ -> false), null)
            match typedRes with
            | TypeCheckAnswer.TypeCheckSucceeded(res) -> return untypedRes, res
            | res when n > 100 -> return failwithf "Parsing did not finish... (%A)" res
            | _ -> 
                do! Async.Sleep(100)
                return! waitForTypeCheck(n + 1)
        }

        waitForTypeCheck 0 |> Async.RunSynchronously

    let extractIdentifier (line : string) (charIndex : int) =
        if (charIndex > 2) then
            let find = line.LastIndexOfAny([| ' '; '\t'; '\r'; |], (charIndex - 2))
            let start = find + 1
            let len = charIndex - start - 1
            if start + len > line.Length || len <= 0 then
                ""
            else
                line.Substring(start, len)
        else
            ""

    let extractNames (line : string) (charIndex : int) =
        let ident = extractIdentifier (line) (charIndex)
        ident.Split('.') |> Seq.toList

    let buildFormatComment (xmlCommentRetriever: string * string -> string) cmt (sb: StringBuilder) =
        match cmt with
        | XmlCommentText(s) -> sb.AppendLine(s) |> ignore
        | XmlCommentSignature(file, signature) ->
            let comment = xmlCommentRetriever (file, signature)
            if (not (comment.Equals(null))) && comment.Length > 0 then sb.AppendLine(comment) |> ignore
        | XmlCommentNone -> ()

    let buildFormatElement isSingle el (sb: StringBuilder) xmlCommentRetriever =
        match el with
        | DataTipElementNone -> ()
        | DataTipElement(it, comment) ->
            sb.AppendLine(it) |> buildFormatComment xmlCommentRetriever comment
        | DataTipElementGroup(items) ->
            let items, msg =
                if items.Length > 10 then
                    (items |> Seq.take 10 |> List.ofSeq),
                    sprintf "   (+%d other overloads)" (items.Length - 10)
                else items, null
            if isSingle && items.Length > 1 then
                sb.AppendLine("Multiple overloads") |> ignore
            for (it, comment) in items do
                sb.AppendLine(it) |> buildFormatComment xmlCommentRetriever comment
            if msg <> null then sb.AppendFormat(msg) |> ignore
        | DataTipElementCompositionError(err) ->
            sb.Append("Composition error: " + err) |> ignore

    // Convert DataTipText to string
    let formatTip tip xmlCommentRetriever =
        let commentRetriever = defaultArg xmlCommentRetriever (fun _ -> "")
        let sb = new StringBuilder()
        match tip with
        | DataTipText([single]) -> buildFormatElement true single sb commentRetriever
        | DataTipText(its) -> for item in its do buildFormatElement false item sb commentRetriever
        sb.ToString().Trim('\n', '\r')

    let GetDeclarations (source:string) (lineIndex, charIndex) =

        let inputLines = source.Split('\n')
        let file = "/home/user/Test.fsx"
        let line = inputLines.[lineIndex]
        let names = extractNames line charIndex

        // parse
        let untyped, parsed = parseWithTypeInfo(file, source)

        // get declarations for a location
        let decls = 
            parsed.GetDeclarations(Some untyped, (lineIndex, charIndex), source, (names, ""), fun _ -> false)
            |> Async.RunSynchronously
    
        decls.Items

    let GetMethods (source:string) (lineIndex, charIndex) =

        let inputLines = source.Split('\n')
        let line = inputLines.[lineIndex]
        let file = "/home/user/Test.fsx"

        // Get untyped & typed information and get code for the IDENT token (needed later)
        let identToken = Parser.tagOfToken(Parser.token.IDENT("")) 
        let untyped, parsed = parseWithTypeInfo(file, source)

        // Get methods for the location
        let names = extractNames (line) (charIndex)
        let methods = parsed.GetMethods((lineIndex, charIndex), inputLines.[lineIndex], Some names)
    
        methods.Methods
        |> Seq.map (fun x -> formatTip x.Description None)
        |> Seq.toArray

    let GetTree (source:string) =

        let file = "/home/user/Test.fsx"
        let untyped, parsed = parseWithTypeInfo(file, source)
        untyped.ParseTree.Value

    let GetToolTip (source:string) (lineIndex, charIndex) =

        let inputLines = source.Split('\n')
        let file = "/home/user/Test.fsx"

        // Get untyped & typed information and get code for the IDENT token (needed later)
        let identToken = Parser.tagOfToken(Parser.token.IDENT("")) 
        let untyped, parsed = parseWithTypeInfo(file, source)
        let line = inputLines.[lineIndex]
        let names = extractNames line charIndex

        // Get tool tip at the specified location
        parsed.GetDataTipText((lineIndex, charIndex), line, names, identToken)
