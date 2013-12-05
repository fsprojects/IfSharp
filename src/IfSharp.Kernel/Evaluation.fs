namespace IfSharp.Kernel

open System
open System.IO
open System.Text
open Microsoft.FSharp.Compiler.Interactive.Shell

[<AutoOpen>]
module Evaluation = 

    let internal sbOut = new StringBuilder()
    let internal sbErr = new StringBuilder()
    let internal inStream = new StringReader("")
    let internal outStream = new StringWriter(sbOut)
    let internal errStream = new StringWriter(sbErr)
    let internal fsiEval = new FsiEvaluationSession([|"--noninteractive"|], inStream, outStream, errStream)
    
    let GetLastExpression() =

        let lines = 
            sbOut.ToString().Split('\r', '\n')
            |> Seq.filter (fun x -> x <> "")
            |> Seq.toArray

        let index = lines |> Seq.tryFindIndex (fun x -> x.StartsWith("val it : "))
        if index.IsSome then
            let newLines =  [| for i in [index.Value..lines.Length - 1] do yield lines.[i] |]
            String.Join("\r\n", newLines)
        else 
            ""
