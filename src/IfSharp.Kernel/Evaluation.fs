namespace IfSharp.Kernel

open System
open System.IO
open System.Text
open Microsoft.FSharp.Compiler.Interactive.Shell

[<AutoOpen>]
module Evaluation = 

    let internal fsiout = ref false
    let internal sbOut = new StringBuilder()
    let internal sbErr = new StringBuilder()
    let internal inStream = new StringReader("")
    let internal outStream = new StringWriter(sbOut)
    let internal errStream = new StringWriter(sbErr)
    let internal fsiConfig = FsiEvaluationSession.GetDefaultConfiguration()
    let internal fsiEval = FsiEvaluationSession.Create(fsiConfig, [|"--noninteractive"|], inStream, outStream, errStream)
    
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
