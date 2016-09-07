#nowarn "211"
namespace IfSharp

#r "IfSharp.Kernel.dll"
#I "./packages/XPlot.Plotly/lib/net45/"
#r "XPlot.Plotly.dll"

open XPlot.Plotly

open System.IO
open IfSharp.Kernel
open IfSharp.Kernel.Globals

module Plotly =
    let plotly_url = "https://cdn.plot.ly/plotly-latest.min.js"
    let plotly_reference = """<script src="[URL]"></script>""".Replace("[URL]", plotly_url)

    let react_save = """var require_save = require; var requirejs_save = requirejs; var define_save = define; require=requirejs=define=undefined; """
    let react_restore = """require = require_save; requirejs = requirejs_save; define = define_save;"""

    let plotly_include = """
<script type="text/javascript">
    [PLOTLY_JS]
</script>"""

    let script_template =
        """Plotly.plot("[ID]", [DATA], [LAYOUT], [CONFIG]).then(function() {
    $(".[ID].loading").remove()
})"""

    let InitialiseNotebook () =
        let wc = new System.Net.WebClient()
        let plotlyjs = react_save + wc.DownloadString(plotly_url) + react_restore
        plotly_include.Replace("[PLOTLY_JS]", plotlyjs) |> Util.Html

    let Show (plot:XPlot.Plotly.PlotlyChart) = 
        plot.GetInlineHtml () |> Util.Html
      
