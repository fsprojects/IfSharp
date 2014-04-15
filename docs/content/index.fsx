(*** hide ***)
// This block of code is omitted in the generated HTML documentation. Use 
// it to define helpers that you do not want to show in the documentation.
#I "../../bin"
#r "IFSharp.Kernel"
#r "System.Data.dll"
#r "System.Windows.Forms.DataVisualization.dll"
#r "FSharp.Data.TypeProviders.dll"
#r "FSharp.Charting.dll"
#r "fszmq.dll"

open FSharp.Charting
open IfSharp.Kernel
open IfSharp.Kernel.Globals

(**
IfSharp
=======

Sin chart wave example
----------------------
This example shows you how to perform charting using FSharp.Charting
*)

open System

// start and -PI and and finish PI
let startX = -Math.PI
let endX = Math.PI

// the data
let data = 
    [| startX..0.1..endX |]
    |> Array.map (fun x -> (x, sin(x)))

// display a line graph
Chart.Line(data)
|> Chart.WithXAxis(true, "", 3.2, -3.2)
|> Chart.WithYAxis(true, "", 1.0, -1.0)
|> Display

// display the latex
Util.Math("f(x) = sin(x)") |> Display

(**
Sin chart wave example results
------------------------------
<img src="img/chart-1.png" alt="sin function" />
*)

(**
The Display function
--------------------
The `Display` function is a 'global' function that can be accessed anywhere throughout
the notebook. It will take any object and attempt to display it to the user. Built-in
supported types are:

* FSharp.Charting.ChartTypes.GenericChart
* IfSharp.Kernel.GenericChartWithSize
* IfSharp.Kernel.TableOutput
* IfSharp.Kernel.HtmlOutput
* IfSharp.Kernel.LatexOutput

Automatic references
--------------------
IfSharp automatically references the following assemblies:

* IfSharp.Kernel.dll
* System.Data.dll
* System.Windows.Forms.DataVisualization.dll
* FSharp.Data.TypeProviders.dll
* FSharp.Charting.dll
* fszmq.dll

Automatic namespaces
--------------------
IfSharp automatically opens the following namespaces:

* FSharp.Charting
* IfSharp.Kernel
* IfSharp.Kernel.Global

*)