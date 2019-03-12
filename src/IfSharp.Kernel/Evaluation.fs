namespace IfSharp.Kernel

open System
open System.IO
open System.Text
open Microsoft.FSharp.Compiler.SourceCodeServices
open Microsoft.FSharp.Compiler.Interactive.Shell

[<AutoOpen>]
module Evaluation = 
    open Microsoft.FSharp.Compiler

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

        type Microsoft.FSharp.Compiler.Interactive.Shell.FsiEvaluationSession with
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
    
        

        let fsiObj = Microsoft.FSharp.Compiler.Interactive.Shell.Settings.fsi
        // The following is a workaround for IfSharp github issue #143
        // Mono fails to handle tailcall in Fsi printing code, thus constraining the length of print on Mono
        // https://github.com/mono/mono/issues/8975
        if Type.GetType("Mono.Runtime") <> null then
            // Default PrintLength value of 100 triggers the issue when printing the array of size 12x100
            // Value of 50 "postpones" the issue. E.g. Issue is triggered on printing larger arrays of size 50x100
            // Value of 10 seems to work, arrays 1000x1000 are printed without the error, although truncated with ellipsis as expected
            // After the value is set to 10, effectively the sequences are truncated to 100 elements during printing . Maybe F# interactive issue
            fsiObj.PrintLength <- 10
            fsiObj.PrintDepth <- 10
            fsiObj.PrintWidth <- 10
            fsiObj.PrintSize <- 10
        let fsiConfig = FsiEvaluationSession.GetDefaultConfiguration(fsiObj, false)
        let args = [|"--noninteractive"; "--define:HAS_FSI_ADDHTMLPRINTER"; "--define:IFSHARP"; "--define:JUPYTER" |]
        let fsiSession = FsiEvaluationSession.Create(fsiConfig, args, inStream, outStream, errStream)

        // Load the `fsi` object from the right location of the `FSharp.Compiler.Interactive.Settings.dll`
        // assembly and add the `fsi.AddHtmlPrinter` extension method; then clean it from FSI output
        let origLength = sbOut.Length
        let fsiLocation = typeof<Microsoft.FSharp.Compiler.Interactive.Shell.Settings.InteractiveSettings>.Assembly.Location    
        let _, errors1 = fsiSession.EvalInteractionNonThrowing("#r @\"" + fsiLocation + "\"")
        let _, errors2 = fsiSession.EvalInteractionNonThrowing(addHtmlPrinter)
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
            //We could display more of these errors but the errors may be confusing. Consider.
            try 
                let result, errors = fsiEval.EvalExpressionNonThrowing("it")
                match result with
                | Choice1Of2 (Some value) -> Some value
                | Choice1Of2 None -> None
                | Choice2Of2 (exn:exn) -> None

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

                    //https://github.com/fsharp/FSharp.Compiler.Service/issues/835
                    //Particularly suggestion it should be folded into GetDeclarationListInfo, and perhaps we should move to the F# AST as well
                    let partialName = QuickParse.GetPartialLongNameEx(line, charIndex-1) 

                    let decls =
                        checkFileResults.GetDeclarationListInfo(Some parseFileResults, lineNumber, line, partialName)
                        |> Async.RunSynchronously

                    // get declarations for a location
                    (*let decls = 
                        checkFileResults.GetDeclarationListInfo(Some(parseFileResults), lineNumber, charIndex, line, names, filterString, (fun _ -> []))
                        |> Async.RunSynchronously*)

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
