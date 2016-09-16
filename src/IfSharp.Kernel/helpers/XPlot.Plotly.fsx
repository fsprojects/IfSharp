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

type XPlot.Plotly.PlotlyChart with

    member __.GetPngHtml() =
        let html = __.GetInlineHtml()
        html
            .Replace("Plotly.newPlot(", "Plotly.plot(")
            .Replace(
                "data, layout);",
                """data, layout)
                .then(function(gd) {
                    return Plotly.toImage(gd, {format: 'png'})
                        .then(function(url) {
                            gd.innerHTML = '<img src=' + url + ' />'
                        });
                });
                """)

type XPlot.Plotly.Chart with

    static member Image (chart: PlotlyChart) =
        { ContentType = "text/html"
          Data = chart.GetPngHtml()
        }
