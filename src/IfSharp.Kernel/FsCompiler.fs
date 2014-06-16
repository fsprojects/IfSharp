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
        Documentation: string;
        Glyph: int;
        Name: string;
    }

type TypeCheckResults = 
    {
        Parse : ParseFileResults
        Check : CheckFileResults
        Preprocess : PreprocessResults
    }

/// The Compiler class contains methods and for compiling F# code and other tasks
type FsCompiler (executingDirectory : string) =

    let baseReferences = [||]
    let scs = SimpleSourceCodeServices()
    let checker = InteractiveChecker.Create()
    let nuGetManager = NuGetManager(executingDirectory)
    let optionsCache = Dictionary<string, ProjectOptions>()
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
        | XmlCommentText(s) -> sb.AppendLine(s) |> ignore
        | XmlCommentSignature(file, signature) ->
            let comment = xmlCommentRetriever (file, signature)
            if (not (comment.Equals(null))) && comment.Length > 0 then sb.AppendLine(comment) |> ignore
        | XmlCommentNone -> ()

    /// Converts a ToolTipElement into a string
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
            
    /// Formats a DataTipText into a string
    member this.FormatTip (tip, xmlCommentRetriever) =
        
        let commentRetriever = defaultArg xmlCommentRetriever (fun _ -> "")
        let sb = new StringBuilder()
        match tip with
        | ToolTipText([single]) -> this.BuildFormatElement true single sb commentRetriever
        | ToolTipText(its) -> for item in its do this.BuildFormatElement false item sb commentRetriever
        sb.ToString().Trim('\n', '\r')

    /// Tries to figure out the names to pass to GetDeclarations or GetMethods.
    member this.ExtractNames (line : string, charIndex : int) =
        
        let sourceTok = SourceTokenizer([], "/home/test.fsx")
        let tokenizer = sourceTok.CreateLineTokenizer(line)
        let rec gatherTokens (tokenizer:LineTokenizer) state =
            seq {
                match tokenizer.ScanToken(state) with
                | Some tok, state ->
                    yield tok
                    yield! gatherTokens tokenizer state
                | None, state -> ()
            }

        let tokens = gatherTokens tokenizer 0L |> Seq.toArray |> Array.rev

        let startIndex = 
            match tokens |> Array.tryFindIndex (fun x -> charIndex > x.LeftColumn && charIndex <= x.LeftColumn + x.FullMatchedLength) with
            | Some x -> x
            | None -> 0

        let endIndex = 
            let idx = 
                tokens
                |> Seq.mapi (fun i x -> i, x)
                |> Seq.tryFindIndex (fun (i, x) -> x.TokenName <> "DOT" && x.TokenName <> "IDENT" && i > startIndex)

            match idx with
            | Some 0 -> startIndex
            | Some x -> x - 1
            | None -> tokens.Length - 1

        tokens.[startIndex..endIndex]
        |> Array.filter (fun x -> x.TokenName <> "DOT")
        |> Array.map (fun x -> line.Substring(x.LeftColumn, x.FullMatchedLength))
        |> Array.map (fun x -> x.Trim([|'`'|]))
        |> Array.rev
        |> Array.toList

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
        let recent = checker.TryGetRecentTypeCheckResultsForFile(fileName, options, source)
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
                | CheckFileAnswer.Succeeded(check) -> (parse, check)
                | CheckFileAnswer.Aborted -> failwithf "Parsing did not finish... (%A)" answer

        { Check = check; Parse = parse; Preprocess = preprocessResults }

    /// Convenience method for getting the methods from a piece of source code
    member this.GetMethods (source, lineNumber : int, charIndex : int) = 
        
        let fileName = "/home/Test.fsx"
        let tcr = this.TypeCheck(source, fileName)
        let line = tcr.Preprocess.OriginalLines.[lineNumber - 1]
        let names = this.ExtractNames(line, charIndex)

        // get declarations for a location
        let methods = tcr.Check.GetMethodsAlternate(lineNumber, charIndex, line, Some(names))
        (names, methods)

    /// Convenience method for getting the declarations from a piece of source code
    member this.GetDeclarations (source, lineNumber : int, charIndex : int) =

        let fileName = "/home/Test.fsx"
        let tcr = this.TypeCheck(source, fileName)
        let lines = tcr.Preprocess.OriginalLines
        let line = tcr.Preprocess.OriginalLines.[lineNumber - 1]
        let names = this.ExtractNames(line, charIndex)

        // get declarations for a location
        let decls = 
            tcr.Check.GetDeclarationsAlternate(Some(tcr.Parse), lineNumber, charIndex, line, names, "")
            |> Async.RunSynchronously

        let items = 
            decls.Items
            |> Seq.map (fun x -> { Documentation = this.FormatTip(x.DescriptionText, None); Glyph = x.Glyph; Name = x.Name })
            |> Seq.toArray

        (names, items, tcr)

    /// Gets tooltip information for the specified information
    member this.GetToolTipText (source, lineNumber : int, charIndex : int) =

        let fileName = "/home/Test.fsx"
        let tcr = this.TypeCheck(source, fileName)
        let lines = tcr.Preprocess.OriginalLines
        let line = tcr.Preprocess.OriginalLines.[lineNumber - 1]
        let (startIndex, endIndex, names) = this.ExtractTooltipName line charIndex
        let identToken = Parser.tagOfToken(Parser.token.IDENT("")) 
        let toolTip = tcr.Check.GetToolTipTextAlternate(lineNumber, charIndex, line, names, identToken) |> Async.RunSynchronously

        (startIndex, endIndex, this.FormatTip(toolTip, None))
