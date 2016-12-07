namespace IfSharp.Kernel

open System
open System.Collections.Generic
open System.Diagnostics
open System.Linq
open System.IO
open System.Text
open System.Reflection

open Microsoft.FSharp.Compiler
open Microsoft.FSharp.Compiler.Ast
open Microsoft.FSharp.Compiler.SourceCodeServices
open Microsoft.FSharp.Compiler.SimpleSourceCodeServices

type DynamicAssembly = 
    {
        Errors : CustomErrorInfo[]
        ExitCode : int
        Assembly : Option<Assembly>
        AdditionalReferences : string[]
    }

type TooltipInfo = 
    {
        LineIndex: int;
        StartIndex: int;
        EndIndex: int;
        Tooltip: string;
    }

type SimpleDeclaration =
    {
        Documentation: string
        Glyph: int
        Name: string
        Value: string
    }

type TypeCheckResults = 
    {
        Parse : FSharpParseFileResults
        Check : FSharpCheckFileResults
        Preprocess : PreprocessResults
    }

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
        | File -> 1000
        | Folder -> 1001

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
        | FSharpToolTipElement.Single(it, comment) ->
            sb.AppendLine(it) |> buildFormatComment xmlCommentRetriever comment
        | FSharpToolTipElement.SingleParameter(it, comment, _) ->
            sb.AppendLine(it) |> buildFormatComment xmlCommentRetriever comment
        | FSharpToolTipElement.Group(items) ->
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
        
        let sourceTok = SourceTokenizer([], Some "/home/test.fsx")
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

        let tokens = gatherTokens tokenizer 0L |> Seq.toArray
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

/// The Compiler class contains methods and for compiling F# code and other tasks
type FsCompiler (executingDirectory : string) =

    let baseReferences = [||]
    let scs = SimpleSourceCodeServices()
    let checker = FSharpChecker.Create()
    let nuGetManager = NuGetManager(executingDirectory)
    let optionsCache = Dictionary<string, FSharpProjectOptions>()
    let mutable buildingLibraries = false

    let lastIndexOfAll(str : string) (find : string) (startIndex) =
        seq {
            for c in find do
                let idx =  str.LastIndexOf(c, startIndex)
                if idx <> -1 then yield idx
            yield -1
        }

    let indexOfAll(str : string) (find : string) (startIndex) =
        seq {
            for c in find do
                let idx =  str.IndexOf(c, startIndex)
                if idx <> -1 then yield idx
            yield str.Length
        }

    let maxIndexOfAny(str) (find) (startIndex) =
        lastIndexOfAll str find startIndex |> Seq.max

    let minIndexOfAny(str) (find) (startIndex) =
        indexOfAll str find startIndex |> Seq.min

    /// The NuGet manager
    member this.NuGetManager = nuGetManager

    /// The simple source code services object that is being used
    member this.CodeServices = scs

    /// The interactive checker object being used
    member this.Checker = checker

    /// Formats a comment into a string
    member this.BuildFormatComment (xmlCommentRetriever: string * string -> string) cmt (sb: StringBuilder) =
        match cmt with
        | FSharpXmlDoc.Text(s) -> sb.AppendLine(s) |> ignore
        | FSharpXmlDoc.XmlDocFileSignature(file, signature) ->
            let comment = xmlCommentRetriever (file, signature)
            if (not (comment.Equals(null))) && comment.Length > 0 then sb.AppendLine(comment) |> ignore
        | FSharpXmlDoc.None -> ()

    /// Converts a ToolTipElement into a string
    member this.BuildFormatElement isSingle el (sb: StringBuilder) xmlCommentRetriever =
        
        match el with
        | FSharpToolTipElement.None -> ()
        | FSharpToolTipElement.Single(it, comment) ->
            sb.AppendLine(it) |> this.BuildFormatComment xmlCommentRetriever comment
        | FSharpToolTipElement.SingleParameter(it, comment, _) ->
            sb.AppendLine(it) |> this.BuildFormatComment xmlCommentRetriever comment
        | FSharpToolTipElement.Group(items) ->
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
        | FSharpToolTipElement.CompositionError(err) ->
            sb.Append("Composition error: " + err) |> ignore
            
    /// Formats a DataTipText into a string
    member this.FormatTip (tip, xmlCommentRetriever) =
        
        let commentRetriever = defaultArg xmlCommentRetriever (fun _ -> "")
        let sb = new StringBuilder()
        match tip with
        | FSharpToolTipText([single]) -> this.BuildFormatElement true single sb commentRetriever
        | FSharpToolTipText(its) -> for item in its do this.BuildFormatElement false item sb commentRetriever
        sb.ToString().Trim('\n', '\r')

    /// Tries to figure out the names to pass to GetDeclarations or GetMethods.
    member this.ExtractNames (line, charIndex) =
        
        let sourceTok = SourceTokenizer([], Some "/home/test.fsx")
        let tokenizer = sourceTok.CreateLineTokenizer(line)
        let rec gatherTokens (tokenizer:FSharpLineTokenizer) state =
            seq {
                match tokenizer.ScanToken(state) with
                | Some tok, state ->
                    yield tok
                    yield! gatherTokens tokenizer state
                | None, state -> ()
            }

        let tokens = gatherTokens tokenizer 0L |> Seq.toArray
        let idx = tokens |> Array.tryFindIndex(fun x -> charIndex >= x.LeftColumn && charIndex <= x.LeftColumn + x.FullMatchedLength)

        match idx with
        | Some(endIndex) ->
    
            let token = tokens.[endIndex]
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
            (lst, filterStartIndex)

        | None -> (List.empty, 0)

    /// Tries to figure out the names to pass to GetToolTip
    member this.ExtractTooltipName (line : string) (charIndex : int) =
        
        let startIdx = (maxIndexOfAny line " \t\r()<>|," (Math.Max(charIndex, 1) - 1)) + 1
        let endIdx = (minIndexOfAny line " \t\r.()<>|," charIndex)

        let splits = line.Substring(startIdx, endIdx - startIdx).Trim().Split('.')
        let names = splits |> Seq.toList

        // recalculate the start index
        let startIdx =
            if names.Length > 0 then
                endIdx - names.[names.Length - 1].Length
            else
                startIdx

        (startIdx, endIdx, names)

    /// Compiles the specified source code and returns the results
    member this.TypeCheck (source : string, fileName : string) =

        // preprocess the code
        let preprocessResults = nuGetManager.Preprocess(source)

        // TODO: get more creative about caching options
        let getOptionsTimeFromFile (f : string) =
            DateTime.Now.Date

        // build the arguments
        let arguments = 
            [|
                yield "-I:" + executingDirectory
                for r in baseReferences do yield "-r:" + r
                for p in preprocessResults.Packages do yield! p.Assemblies |> Seq.map (fun x -> "-r:" + nuGetManager.GetFullAssemblyPath(p, x))
            |]

        // get the options and parse
        let options = checker.GetProjectOptionsFromScript(fileName, source, getOptionsTimeFromFile(fileName), arguments, true) |> Async.RunSynchronously
        let recent = checker.TryGetRecentCheckResultsForFile(fileName, options, source)
        let (parse, check) = 
            if recent.IsSome then
                Debug.WriteLine("Using cached results for file: {0}", fileName)
                
                let (parse, check, _) = recent.Value
                (parse, check)
            else 
                Debug.WriteLine("Compiling file: {0}", fileName)

                let parse = checker.ParseFileInProject(fileName, source, options) |> Async.RunSynchronously
                let answer = checker.CheckFileInProject(parse, fileName, 0, source, options, IsResultObsolete(fun () -> false), null) |> Async.RunSynchronously
        
                match answer with
                | FSharpCheckFileAnswer.Succeeded(check) -> (parse, check)
                | FSharpCheckFileAnswer.Aborted -> failwithf "Parsing did not finish... (%A)" answer

        { Check = check; Parse = parse; Preprocess = preprocessResults }

    /// Convenience method for getting the methods from a piece of source code
    member this.GetMethods (source, lineNumber : int, charIndex : int) = 
        
        let fileName = "/home/Test.fsx"
        let tcr = this.TypeCheck(source, fileName)
        let line = tcr.Preprocess.OriginalLines.[lineNumber - 1]
        let names, _ = this.ExtractNames(line, charIndex)

        // get declarations for a location
        let methods = tcr.Check.GetMethodsAlternate(lineNumber, charIndex, line, Some(names))
        (names, methods)

    /// Convenience method for getting the declarations from a piece of source code
    member this.GetDeclarations (source, lineNumber : int, charIndex : int) =

        let fileName = "/home/Test.fsx"
        let tcr = this.TypeCheck(source, fileName)
        let line = tcr.Preprocess.OriginalLines.[lineNumber - 1]
        let preprocess = FsCompilerInternals.getPreprocessorIntellisense "." charIndex line

        match preprocess with
        | None ->

            let getValue(str:string) =
                if str.Contains(" ") then "``" + str + "``" else str

            // get declarations for a location
            let names, filterStartIndex = this.ExtractNames(line, charIndex)
            let decls = 
                tcr.Check.GetDeclarationListInfo(Some(tcr.Parse), lineNumber, charIndex, line, names, "")
                |> Async.RunSynchronously

            let items = 
                decls.Items
                |> Seq.map (fun x -> { Documentation = this.FormatTip(x.DescriptionText, None); Glyph = x.Glyph; Name = x.Name; Value = getValue x.Name })
                |> Seq.toArray

            (names, items, tcr, filterStartIndex)

        | Some(x) -> 
            
            let items = 
                x.Matches
                |> Array.map (fun x -> { Documentation = matchToDocumentation x; Glyph = matchToGlyph x.MatchType; Name = x.Name; Value = x.Name })
            
            (List.empty, items, tcr, x.FilterStartIndex)


    /// *** member this.GetToolTipText uses Parser which is internal to the F# Compiler service. Investigate if we need this.

    /// Gets tooltip information for the specified information
    (*member this.GetToolTipText (source, lineNumber : int, charIndex : int) =

        let fileName = "/home/Test.fsx"
        let tcr = this.TypeCheck(source, fileName)
        let lines = tcr.Preprocess.OriginalLines
        let line = tcr.Preprocess.OriginalLines.[lineNumber - 1]
        let (startIndex, endIndex, names) = this.ExtractTooltipName line charIndex
        let identToken = Parser.tagOfToken(Parser.token.IDENT("")) 
        let toolTip = tcr.Check.GetToolTipTextAlternate(lineNumber, charIndex, line, names, identToken) |> Async.RunSynchronously

        (startIndex, endIndex, this.FormatTip(toolTip, None))
        *)