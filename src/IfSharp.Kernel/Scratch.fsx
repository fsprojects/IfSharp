#r "bin/debug/FSharp.Compiler.Service.dll"

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

let line = "Seq.map (fun x -> x)"
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

let charIndex = "Seq.map (fun x -> x".Length
line.Substring(charIndex)
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
|> Array.rev

let ``the string`` =
    tokens
    |> Array.map (fun x -> x.LeftColumn, x.FullMatchedLength + x.LeftColumn, line.Substring(x.LeftColumn, x.FullMatchedLength))