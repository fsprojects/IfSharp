namespace IfSharp.Kernel

open System
open System.IO
open System.Text
open Microsoft.FSharp.Compiler.SourceCodeServices
open Microsoft.FSharp.Compiler.Interactive.Shell

[<AutoOpen>]
module Evaluation = 

    type SimpleDeclaration =
        {
            Documentation: string
            Glyph: FSharpGlyph
            Name: string
            Value: string
        }

    /// Extend the `fsi` object with `fsi.AddHtmlPrinter` 
    let addHtmlPrinter = """
        module FsInteractiveService = 
            let mutable htmlPrinters = new ResizeArray<System.Type * (obj -> seq<string * string> * string)>()
            let htmlPrinterParams = System.Collections.Generic.Dictionary<string, obj>()
            do htmlPrinterParams.["html-standalone-output"] <- false

        type Microsoft.FSharp.Compiler.Interactive.InteractiveSession with
            member x.HtmlPrinterParameters = FsInteractiveService.htmlPrinterParams
            member x.AddHtmlPrinter<'T>(f:'T -> seq<string * string> * string) = 
                FsInteractiveService.htmlPrinters.Add(typeof<'T>, fun (value:obj) ->
                    f (value :?> 'T))"""

    /// Start the F# interactive session with HAS_FSI_ADDHTMLPRINTER symbol defined
    let internal startSession () =
        let sbOut = new StringBuilder()
        let sbErr = new StringBuilder()
        let sbPrint = new StringBuilder()
        let inStream = new StringReader("")
        let outStream = new StringWriter(sbOut)
        let errStream = new StringWriter(sbErr)
        let printStream = new StringWriter(sbPrint)
    
        let fsiObj = Microsoft.FSharp.Compiler.Interactive.Settings.fsi
        let fsiConfig = FsiEvaluationSession.GetDefaultConfiguration(fsiObj, false)
        let args = [|"--noninteractive"; "--define:HAS_FSI_ADDHTMLPRINTER" |]
        let fsiSession = FsiEvaluationSession.Create(fsiConfig, args, inStream, outStream, errStream)

        // Load the `fsi` object from the right location of the `FSharp.Compiler.Interactive.Settings.dll`
        // assembly and add the `fsi.AddHtmlPrinter` extension method; then clean it from FSI output
        let origLength = sbOut.Length
        let fsiLocation = typeof<Microsoft.FSharp.Compiler.Interactive.InteractiveSession>.Assembly.Location    
        fsiSession.EvalInteraction("#r @\"" + fsiLocation + "\"")
        fsiSession.EvalInteraction(addHtmlPrinter)
        sbOut.Remove(origLength, sbOut.Length-origLength) |> ignore
        
        // Get reference to the extra HTML printers registered inside the FSI session
        let extraPrinters = 
          unbox<ResizeArray<System.Type * (obj -> seq<string * string> * string)>>
            (fsiSession.EvalExpression("FsInteractiveService.htmlPrinters").Value.ReflectionValue)

        Console.SetOut(printStream)
        sbErr, sbOut, sbPrint, extraPrinters, fsiSession

    let internal fsiout = ref false
    let internal sbErr, sbOut, sbPrint, extraPrinters, fsiEval = startSession ()

    /// Gets `it` only if `it` was printed to the console
    let GetLastExpression() =

        let lines = 
            sbOut.ToString().Split('\r', '\n')
            |> Seq.filter (fun x -> x <> "")
            |> Seq.toArray

        let index = lines |> Seq.tryFindIndex (fun x -> x.StartsWith("val it :"))
        if index.IsSome then
            try 
                fsiEval.EvalExpression("it")
            with _ -> None
        else 
            None

    /// New way of getting the declarations
    let GetDeclarations(source, lineNumber, charIndex) = 

        let scriptFileName = Path.Combine(Environment.CurrentDirectory, "script.fsx")
        let options, errors =
            fsiEval.InteractiveChecker.GetProjectOptionsFromScript(
                scriptFileName, source)
            |> Async.RunSynchronously

        let (parseFileResults, checkFileAnswer) =
            fsiEval.InteractiveChecker.ParseAndCheckFileInProject(scriptFileName, 0, source, options)
            |> Async.RunSynchronously

        let checkFileResults =
            match checkFileAnswer with
            | FSharpCheckFileAnswer.Aborted -> failwith "unexpected"
            | FSharpCheckFileAnswer.Succeeded x -> x

        try
            let lines = source.Split([| '\n' |])
            let line = lines.[lineNumber - 1]
            let preprocess = getPreprocessorIntellisense "." charIndex line
            match preprocess with
            | None ->
                match extractNames(line, charIndex) with
                | Some (names, startIdx) ->
                    let filterString = line.Substring(startIdx, charIndex - startIdx)
                    let getValue(str:string) =
                        if str.Contains(" ") then "``" + str + "``" else str

                    // get declarations for a location
                    let decls = 
                        checkFileResults.GetDeclarationListInfo(Some(parseFileResults), lineNumber, charIndex, line, names, filterString, (fun _ -> []))
                        |> Async.RunSynchronously

                    let items = 
                        decls.Items
                        |> Array.filter (fun x -> x.Name.StartsWith(filterString, StringComparison.OrdinalIgnoreCase))
                        |> Array.map (fun x -> { Documentation = formatTip(x.DescriptionText, None); Glyph = x.Glyph; Name = x.Name; Value = getValue x.Name })

                    (items, checkFileResults, startIdx, filterString)
                | None -> 
                    ([||], checkFileResults, charIndex, "")
            | Some(x) -> 

                let items = 
                    x.Matches
                    |> Array.map (fun x -> { Documentation = matchToDocumentation x; Glyph = matchToGlyph x.MatchType; Name = x.Name; Value = x.Name })
                
                (items, checkFileResults, x.FilterStartIndex, "")
        with _ ->
            ([||], checkFileResults, 0, "")
