namespace IfSharp.Kernel

open System
open System.Collections.Generic
open System.Diagnostics
open System.Linq
open System.IO
open System.Text
open System.Reflection

open FSharp.Compiler
open FSharp.Compiler.Ast
open FSharp.Compiler.SourceCodeServices

[<AutoOpen>]
module FsCompilerInternals = 
    
    type PreprocessorMatchType = 
        | Folder
        | File

    type PreprocessorMatch = 
        {
            MatchType: PreprocessorMatchType
            Name: string
        }

    type Directive =
        | Load
        | Reference

    type PreprocessorMatches =
        {
            Directive: Directive
            Directory: string
            Filter: string
            Matches: PreprocessorMatch[]
            FilterStartIndex: int
        }

    type String with 
        member this.Substring2(startIndex, endIndex) =
           this.Substring(startIndex, endIndex - startIndex)

    let matchToDocumentation (m:PreprocessorMatch) =
        match m.MatchType with
        | File -> "File: " + m.Name
        | Folder -> "Folder: " + m.Name

    let directiveToFileFilter d = 
        match d with 
        | Reference -> "*.dll"
        | Load -> "*.fsx"

    let matchToGlyph m = 
        match m with
        | File -> FSharpGlyph.Variable //1000
        | Folder -> FSharpGlyph.Variable //1001

    /// Formats a comment into a string
    let buildFormatComment (xmlCommentRetriever: string * string -> string) cmt (sb: StringBuilder) =
        match cmt with
        | FSharpXmlDoc.Text(s) -> sb.AppendLine(s) |> ignore
        | FSharpXmlDoc.XmlDocFileSignature(file, signature) ->
            let comment = xmlCommentRetriever (file, signature)
            if (not (comment.Equals(null))) && comment.Length > 0 then sb.AppendLine(comment) |> ignore
        | FSharpXmlDoc.None -> ()

    /// Converts a ToolTipElement into a string
    let buildFormatElement isSingle el (sb: StringBuilder) xmlCommentRetriever =

        match el with
        | FSharpToolTipElement.None -> ()
        (*| FSharpToolTipElement.Single(it, comment) ->
            sb.AppendLine(it) |> buildFormatComment xmlCommentRetriever comment
        | FSharpToolTipElement.SingleParameter(it, comment, _) ->
            sb.AppendLine(it) |> buildFormatComment xmlCommentRetriever comment*)
        | FSharpToolTipElement.Group(items) ->
            let items, msg =
                if items.Length > 10 then
                    (items |> Seq.take 10 |> List.ofSeq),
                    sprintf "   (+%d other overloads)" (items.Length - 10)
                else items, null
            if isSingle && items.Length > 1 then
                sb.AppendLine("Multiple overloads") |> ignore
            //for (it, comment) in items do
            for elementData in items do
                //sb.AppendLine elementData.MainDescription |> ignore
                sb.AppendLine(elementData.MainDescription) |> buildFormatComment xmlCommentRetriever elementData.XmlDoc
            if msg <> null then sb.AppendFormat(msg) |> ignore
        | FSharpToolTipElement.CompositionError(err) ->
            sb.Append("Composition error: " + err) |> ignore

    /// Formats a DataTipText into a string
    let formatTip (tip, xmlCommentRetriever) =
        let commentRetriever = defaultArg xmlCommentRetriever (fun _ -> "")
        let sb = new StringBuilder()
        match tip with
        | FSharpToolTipText.FSharpToolTipText([single]) -> buildFormatElement true single sb commentRetriever
        | FSharpToolTipText.FSharpToolTipText(its) -> for item in its do buildFormatElement false item sb commentRetriever
        sb.ToString().Trim('\n', '\r')

    /// Tries to figure out the names to pass to GetDeclarations or GetMethods.
    let extractNames (line, charIndex) =
        
        let sourceTok = FSharpSourceTokenizer([], Some "/home/test.fsx")
        let tokenizer = sourceTok.CreateLineTokenizer(line)
        let rec gatherTokens (tokenizer:FSharpLineTokenizer) state =
            seq {
                match tokenizer.ScanToken(state) with
                | Some tok, state ->
                    yield tok
                    yield! gatherTokens tokenizer state
                | None, state -> ()
            }

        let invalidTokens = 
            [|
                "IEEE64"
                "INT32_DOT_DOT"
                "INFIX_BAR_OP"
                "STRING_TEXT"
                "RARROW"
            |]
        
        
        let tokens = gatherTokens tokenizer FSharpTokenizerLexState.Initial |> Seq.toArray
        let idx = tokens |> Array.tryFindIndex(fun x -> charIndex >= x.LeftColumn && charIndex <= x.LeftColumn + x.FullMatchedLength)

        match idx with
        | Some(endIndex) ->
    
            let token = tokens.[endIndex]
            if invalidTokens.Contains(token.TokenName) then
                None
            else
                let idx = 
                    tokens.[0..endIndex]
                    |> Array.rev
                    |> Array.tryFindIndex (fun x -> x.TokenName <> "IDENT" && x.TokenName <> "DOT")
    
                let startIndex = 
                    match idx with
                    | Some(x) -> endIndex - x
                    | None -> 0

                let finalIndex = 
                    if token.TokenName = "IDENT" then
                        endIndex - 1
                    else
                        endIndex

                let relevantTokens = 
                    tokens.[startIndex..finalIndex]
                    |> Array.filter (fun x -> x.TokenName = "IDENT")
                    |> Array.map (fun x -> line.Substring(x.LeftColumn, x.FullMatchedLength))
                    |> Array.map (fun x -> x.Trim([|'`'|]))

                let filterStartIndex = if finalIndex = -1 then tokens.[0].LeftColumn else tokens.[finalIndex].RightColumn + 1
                let lst = relevantTokens |> Seq.toList
                Some <| (lst, filterStartIndex)

        | None -> 
            Some <| ([], 0)

    let getPreprocessorIntellisense baseDirectory charIndex (line:string) = 
    
        let directive = 
            if line.StartsWith "#load" then Some Load
            elif line.StartsWith "#r" then Some Reference
            else None

        match directive with
        | Some d ->
            let nextQuote = line.IndexOf('"')
            let firstQuote = line.LastIndexOf('"', charIndex - 1)
    
            // make sure we are inside quotes
            if firstQuote <> -1 && nextQuote <> -1 && firstQuote = nextQuote then

                let previousSlash = line.LastIndexOfAny([| '/'; '\\' |], charIndex - 1)

                let directory, filter, startIndex = 
                    if previousSlash <> -1 && previousSlash > firstQuote then
                        let directory = line.Substring2(firstQuote + 1, previousSlash + 1)
                        let filter = line.Substring2(previousSlash + 1, charIndex)
                        if Path.IsPathRooted(directory) then
                            directory, filter, previousSlash + 1
                        else
                            Path.Combine(baseDirectory, directory), filter, previousSlash + 1
                    else 
                        baseDirectory, line.Substring2(firstQuote + 1, charIndex), firstQuote + 1

                let files = 
                    Directory.GetFiles(directory, directiveToFileFilter d)
                    |> Array.map Path.GetFileName
                    |> Array.map (fun x -> { MatchType = PreprocessorMatchType.File; Name = x })

                let dirs = 
                    DirectoryInfo(directory).GetDirectories()
                    |> Array.map (fun x -> x.Name)
                    |> Array.map (fun x -> { MatchType = PreprocessorMatchType.Folder; Name = x })

                {
                    Matches = Array.append dirs files
                    Directory = directory
                    Filter = filter
                    FilterStartIndex = startIndex
                    Directive = d
                } |> Some
        
            else

                None

        | None -> None
