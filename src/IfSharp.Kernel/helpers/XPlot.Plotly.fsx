#r "IfSharp.Kernel.dll"
#I "packages/XPlot.Plotly/lib/net45/"
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
function ifsharpMakeImage(gd, fmt) {
    return Plotly.toImage(gd, {format: fmt})
        .then(function(url) {
            var img = document.createElement('img');
            img.setAttribute('src', url);
            var div = document.createElement('div');
            div.appendChild(img);
            gd.parentNode.replaceChild(div, gd);
        });
}
function ifsharpMakePng(gd) {
    var fmt =
        (document.documentMode || /Edge/.test(navigator.userAgent)) ?
            'svg' : 'png';
    return ifsharpMakeImage(gd, fmt);
}
function ifsharpMakeSvg(gd) {
    return ifsharpMakeImage(gd, 'svg');
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
                "data, layout).then(ifsharpMakePng);")

    member __.GetSvgHtml() =
        let html = __.GetInlineHtml()
        html
            .Replace("Plotly.newPlot(", "Plotly.plot(")
            .Replace(
                "data, layout);",
                "data, layout).then(ifsharpMakeSvg);")

type XPlot.Plotly.Chart with

    static member Png (chart: PlotlyChart) =
        { ContentType = "text/html"
          Data = chart.GetPngHtml()
        }

    static member Svg (chart: PlotlyChart) =
        { ContentType = "text/html"
          Data = chart.GetSvgHtml()
        }
