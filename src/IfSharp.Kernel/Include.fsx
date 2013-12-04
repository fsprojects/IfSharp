#r "IfSharp.Kernel.dll";
#r "System.Data.dll";
#r "System.Windows.Forms.DataVisualization.dll";
#r "FSharp.Data.TypeProviders.dll";
#r "FSharp.Charting.dll"

open System
open System.Data
open Microsoft.FSharp.Data.TypeProviders
open IfSharp.Kernel
open FSharp.Charting

let Display = IfSharp.Kernel.App.Display

let x = [| 1; 2; 3; |]
let start = 1
x.[start + 1..x.Length - 1]