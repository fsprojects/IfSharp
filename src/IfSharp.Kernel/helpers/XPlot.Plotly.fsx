#nowarn "211"

#r "IfSharp.Kernel.dll"
#I "./packages/XPlot.Plotly/lib/net45/"
#r "XPlot.Plotly.dll"

open XPlot.Plotly
open IfSharp.Kernel
open IfSharp.Kernel.Globals

do
    Printers.addDisplayPrinter(fun (plot: PlotlyChart) ->
        { ContentType = "text/html"; Data = plot.GetInlineHtml() })

    { Html = @"<script src=""https://cdn.plot.ly/plotly-latest.min.js""></script>" }
        |> Display
