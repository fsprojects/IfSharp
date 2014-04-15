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
IfSharp is an F# implementation for [iPython](http://ipython.org).
It works with iPython Notebook 1.x and 2.x.

![Intellisense Example #3 With Chart](img/intellisense-3.png "Intellisense Example #3 With Chart")

Download
--------
[View releases](https://github.com/BayardRock/IfSharp/releases).
For more information about manual installation visit the [installation](installation.html) page.

API Documentation
-----------------
* [Globals](globals.html) (functions that can be accessed throughout the notebook)
* [Utils](utils.html) (common utility methods used mostly for displaying)

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

Sin chart wave example
----------------------
This example shows you how to perform charting using FSharp.Charting
*)

open System

// start at -PI and finish at PI
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
<img src="img/chart-1.png" alt="sin function" />
*)