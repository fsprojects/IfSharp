#r "IfSharp.Kernel.dll"
#r "packages/Angara.Base/lib/net452/Angara.Base.dll"
#r "packages/Angara.Html/lib/net452/Angara.Html.dll"
#r "packages/Angara.Chart/lib/net452/Angara.Chart.dll"
#r "packages/Angara.Table/lib/net452/Angara.Table.dll"
#r "packages/Angara.Reinstate/lib/net452/Angara.Reinstate.dll"
#r "packages/Angara.Serialization/lib/net452/Angara.Serialization.dll"
#r "packages/Angara.Serialization.Json/lib/net452/Angara.Serialization.Json.dll"
#r "packages/Suave/lib/net40/Suave.dll"

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
