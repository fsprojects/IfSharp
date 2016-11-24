(*** hide ***)
// This block of code is omitted in the generated HTML documentation. Use 
// it to define helpers that you do not want to show in the documentation.
#I "../../bin"
#r "Newtonsoft.Json.dll"
#r "IFSharp.Kernel"
#r "System.Data.dll"
#r "System.Windows.Forms.DataVisualization.dll"
#r "FSharp.Data.TypeProviders.dll"
#r "FSharp.Charting.dll"
#r "NetMQ.dll"

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

Startup script
--------------
[Include.fsx](https://github.com/BayardRock/IfSharp/blob/master/src/IfSharp.Kernel/Include.fsx) is a script that
is executed on startup. The script automatically references the following assemblies:

* IfSharp.Kernel.dll
* NetMQ.dll

And automatically opens the following namespaces:

* IfSharp.Kernel
* IfSharp.Kernel.Global

Paket support
------------------------
To download [Paket](https://fsprojects.github.io/Paket/) packages, start with:
*)

#load "Paket.fsx"

(**
This will allow you to specify Paket dependencies with a declarative syntax.
*)

Paket.Package ["Newtonsoft.Json"]

(**
You will need to load any assemblies you require from those packages using `#r` directives.

If you need to specify a version, you can use an alternate form:
*)

Paket.Version ["Newtonsoft.Json", "~> 9.0.1"]

(**
Some helper scripts are provided to handle Paket package installation, assembly dependency, and custom `Display` printers for commonly used packages. Each helper script is divided into a script that installs Paket dependencies, and another that loads assemblies and sets up extension functions and `Display` printers.
*)

#load "Angara.Charting.Paket.fsx"
#load "Angara.Charting.fsx"

#load "XPlot.Plotly.Paket.fsx"
#load "XPlot.Plotly.fsx"

#load "FSharp.Charting.Paket.fsx"
#load "FSharp.Charting.fsx"

(**
[`Angara.Charting`](http://predictionmachines.github.io/Angara.Chart/) and [`XPlot.Plotly`](https://tahahachana.github.io/XPlot/plotly.html) are both feature-rich, cross-platform charting packages. [`FSharp.Charting`](https://fslab.org/FSharp.Charting/) is a Windows-only charting package.

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
