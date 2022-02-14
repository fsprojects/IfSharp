#r "IfSharp.Kernel.dll"
#r "Plotly.NET.dll"
#r "Plotly.NET.ImageExport.dll"
#r "DynamicObj.dll"
#r "Newtonsoft.Json.dll"

open Plotly.NET

open GenericChart
open IfSharp.Kernel
open IfSharp.Kernel.Globals

open DynamicObj
open Newtonsoft.Json


// The following code is modified from Plotly.NET:
(* Copyright 2020 Timo MÃ¼hlhaus
 
Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*)

// The code is modified in the way that plotly and requirejs are not loaded from the cdn.
// Plotly is inlined, requirejs is already available from jupyter notebooks
let chart =
    let newScript = new System.Text.StringBuilder()
    newScript.AppendLine("""<div id="[ID]"><!-- Plotly chart will be drawn inside this DIV --></div>""") |> ignore
    newScript.AppendLine("<script type=\"text/javascript\">") |> ignore

    newScript.AppendLine(
        @"
        var renderPlotly_[SCRIPTID] = function() {
            var data = [DATA];
            var layout = [LAYOUT];
            var config = [CONFIG];
            Plotly.newPlot('[ID]', data, layout, config);
        }
        renderPlotly_[SCRIPTID]();") |> ignore

    newScript.AppendLine("</script>") |> ignore
    newScript.ToString()

let toChartHTML gChart =

       
       let jsonConfig = JsonSerializerSettings()
       jsonConfig.ReferenceLoopHandling <- ReferenceLoopHandling.Serialize

       let guid = System.Guid.NewGuid().ToString()

       let tracesJson =
           let traces = GenericChart.getTraces gChart
           JsonConvert.SerializeObject(traces, jsonConfig)

       let layoutJson =
           let layout = GenericChart.getLayout gChart
           JsonConvert.SerializeObject(layout, jsonConfig)

       let configJson =
           let config = GenericChart.getConfig gChart
           JsonConvert.SerializeObject(config, jsonConfig)

       let displayOpts = GenericChart.getDisplayOptions gChart

       let dims =  GenericChart.tryGetLayoutSize gChart

       let width, height =
           let w, h =  GenericChart.tryGetLayoutSize gChart
           w |> Option.defaultValue 600, h |> Option.defaultValue 600



       chart.Replace("[WIDTH]", string width)
            .Replace("[HEIGHT]", string height)
            .Replace("[ID]", guid)
            .Replace("[SCRIPTID]", guid.Replace("-", ""))
            .Replace("[DATA]", tracesJson)
            .Replace("[LAYOUT]", layoutJson)
            .Replace("[CONFIG]", configJson)
        |> DisplayOptions.replaceHtmlPlaceholders displayOpts


do 
    Printers.addDisplayPrinter(fun (plot: GenericChart.GenericChart) ->
        let contentType = "text/html"
        let generatedHTML = toChartHTML plot
        { ContentType = "text/html"; Data = generatedHTML}
    )

    
    // Require plotly inlined to avoid calls to cdn
    sprintf
        """
<script type="text/javascript">
var require_save = require;
var requirejs_save = requirejs;
var define_save = define;
var MathJax_save = MathJax;
MathJax = require = requirejs = define = undefined;
%s
require = require_save;
requirejs = requirejs_save;
define = define_save;
MathJax = MathJax_save;
</script>
"""
        (System.IO.File.ReadAllText(__SOURCE_DIRECTORY__ + "/plotly-latest.min.js"))
        |> Util.Html
        |> Display
