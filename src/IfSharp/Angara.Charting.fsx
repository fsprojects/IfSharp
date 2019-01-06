#r "IfSharp.Kernel.dll"

#load ".paket/load/Itis.Angara.Base.fsx"
#load ".paket/load/Itis.Angara.Html.fsx"
#load ".paket/load/Angara.Chart.fsx"
#load ".paket/load/Itis.Angara.Table.fsx"
#load ".paket/load/Itis.Angara.Reinstate.fsx"
#load ".paket/load/Angara.Serialization.fsx"
#load ".paket/load/Angara.Serialization.Json.fsx"

open IfSharp.Kernel
open IfSharp.Kernel.Globals
open Angara.Charting

Angara.Base.Init()

Printers.addDisplayPrinter(fun (chart: Chart) ->
  { ContentType = "text/html"
    Data = Angara.Html.MakeEmbeddable "auto" chart })

type Angara.Charting.Chart with

  static member WithHeight (height: int) (chart: Chart) =
    { ContentType = "text/html"
      Data = Angara.Html.MakeEmbeddable (sprintf "%dpx" height) chart }
