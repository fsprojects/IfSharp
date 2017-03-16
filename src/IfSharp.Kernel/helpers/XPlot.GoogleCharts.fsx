#I "packages/XPlot.GoogleCharts/lib/net45/"
#I "packages/Google.DataTable.Net.Wrapper/lib/"
#r "XPlot.Googlecharts.dll"
#r "Google.DataTable.Net.Wrapper.dll"
#r "IfSharp.Kernel.dll"

open IfSharp.Kernel
open IfSharp.Kernel.App
open IfSharp.Kernel.Globals

@"<script src=""https://www.google.com/jsapi""></script>" |> Util.Html |> Display

type XPlot.GoogleCharts.GoogleChart with
  member __.GetContentHtml() =
    let html = __.GetInlineHtml()
    html
      .Replace ("google.setOnLoadCallback(drawChart);", "if (typeof google != 'undefined') { google.load('visualization', '1.0', { packages: ['corechart'], callback: drawChart }); }")

type XPlot.GoogleCharts.Chart with
  static member Content (chart : XPlot.GoogleCharts.GoogleChart) =
    { ContentType = "text/html"; Data = chart.GetContentHtml() }

AddDisplayPrinter (fun (plot: XPlot.GoogleCharts.GoogleChart) -> { ContentType = "text/html"; Data = plot.GetContentHtml() })