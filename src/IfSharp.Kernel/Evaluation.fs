namespace IfSharp.Kernel

open System
open System.IO
open System.Text
open Microsoft.FSharp.Compiler.Interactive.Shell

[<AutoOpen>]
module Evaluation = 

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
        let inStream = new StringReader("")
        let outStream = new StringWriter(sbOut)
        let errStream = new StringWriter(sbErr)
    
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
        sbErr, sbOut, extraPrinters, fsiSession

    let internal fsiout = ref false
    let internal sbErr, sbOut, extraPrinters, fsiEval = startSession ()

    /// Gets `it` only if `it` was printed to the console
    let GetLastExpression() =

        let lines = 
            sbOut.ToString().Split('\r', '\n')
            |> Seq.filter (fun x -> x <> "")
            |> Seq.toArray

        let index = lines |> Seq.tryFindIndex (fun x -> x.StartsWith("val it : "))
        if index.IsSome then
            try 
                fsiEval.EvalExpression("it")
            with _ -> None
        else 
            None

    /// New way of getting the declarations
    let GetDeclarations(source, lineIndex, charIndex) = 
        
        let (parse, tcr, _) = fsiEval.ParseAndCheckInteraction(source)
        let lines = source.Split([| '\n' |])
        let line = lines.[lineIndex]
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
                    tcr.GetDeclarationListInfo(Some(parse), lineIndex + 1, charIndex, line, names, filterString)
                    |> Async.RunSynchronously

                let items = 
                    decls.Items
                    |> Array.filter (fun x -> x.Name.StartsWith(filterString, StringComparison.OrdinalIgnoreCase))
                    |> Array.map (fun x -> { Documentation = formatTip(x.DescriptionText, None); Glyph = x.Glyph; Name = x.Name; Value = getValue x.Name })

                (items, startIdx, filterString)
            | None -> 
                ([||], charIndex, "")
        | Some(x) -> 

            let items = 
                x.Matches
                |> Array.map (fun x -> { Documentation = matchToDocumentation x; Glyph = matchToGlyph x.MatchType; Name = x.Name; Value = x.Name })
            
            (items, x.FilterStartIndex, "")
