(*** hide ***)
// This block of code is omitted in the generated HTML documentation. Use 
// it to define helpers that you do not want to show in the documentation.
#I "../../bin"
#r "IFSharp.Kernel"
// #r "System.Data.dll"
// #r "System.Windows.Forms.DataVisualization.dll"
// #r "FSharp.Data.TypeProviders.dll"
// #r "FSharp.Charting.dll"
#r "NetMQ.dll"

open IfSharp.Kernel
open IfSharp.Kernel.Globals

(**
Custom display printers
=======================
Custom display printers can be added during runtime based off of type.
Example for providing a custom display printer for `float` to display a pink div:
*)

App.AddDisplayPrinter(fun (x:float) ->
    {
        ContentType = "text/html"
        Data = "<div style=\"background-color: pink\">" + x.ToString() + "</div>"
    }
)

(** 
To invoke the printer:
*)
1.0 |> Display

(**
Will render <div style="background-color: pink">1</div>

Custom fsi printers
===================
Similarly, FSI printers can be added which can provide display a string for a type.
However, a more complex implementation could send display data directly to the client
as a side-effect, which will skip the need for calling the `Display` function directly
from the notebook. Example:
*)
App.AddFsiPrinter(fun (x:float) ->
    App.Kernel.Value.SendDisplayData("text/html", "<div style=\"background-color: pink\">" + x.ToString() + "</div>")
    x.ToString()
)

(**
Now, executing a cell with the following code:
*)
1.0

(** Will render as: <div style="background-color: pink">1</div> *)

(**
However, executing a cell with the following code:
*)
[1.0; 2.0; 3.0;]

(**
Will render as:
<div style="background-color: pink">1</div>
<div style="background-color: pink">2</div>
<div style="background-color: pink">3</div>
*)