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

    use wc = new System.Net.WebClient()
    sprintf
        """
<script type="text/javascript">
var require_save = require;
var requirejs_save = requirejs;
var define_save = define;
require = requirejs = define = undefined;
%s
require = require_save;
requirejs = requirejs_save;
define = define_save;
function ifsharpMakeImage(gd) {
    var fmt =
        (document.documentMode || /Edge/.test(navigator.userAgent)) ?
            'svg' : 'png'
    return Plotly.toImage(gd, {format: fmt})
        .then(function(url) {
            var img = document.createElement('img');
            img.setAttribute('src', url);
            var div = document.createElement('div');
            div.appendChild(img);
            gd.parentNode.replaceChild(div, gd);
        });
}
</script>
"""
        (wc.DownloadString("https://cdn.plot.ly/plotly-latest.min.js"))
        |> Util.Html
        |> Display

type XPlot.Plotly.PlotlyChart with

    member __.GetPngHtml() =
        let html = __.GetInlineHtml()
        html
            .Replace("Plotly.newPlot(", "Plotly.plot(")
            .Replace(
                "data, layout);",
                "data, layout).then(ifsharpMakeImage);")

type XPlot.Plotly.Chart with

    static member Image (chart: PlotlyChart) =
        { ContentType = "text/html"
          Data = chart.GetPngHtml()
        }
