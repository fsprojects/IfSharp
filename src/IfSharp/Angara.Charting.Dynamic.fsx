#load ".paket/load/Itis.Angara.Html.fsx"

open System
open IfSharp.Kernel
open IfSharp.Kernel.Globals
open FSharp.Control
open Angara.Charting
open Angara.Serialization
open Newtonsoft.Json

type DynamicChartPrinter private () =    
    static let instance = DynamicChartPrinter()
    static member Instance : IfSharp.Kernel.IAsyncPrinter = upcast instance
    interface IfSharp.Kernel.IAsyncPrinter with
        member __.CanPrint value =
            let t = value.GetType()            
            match AsyncDisplay.getAsyncSeqType(t) with
            | Some iface ->                
                iface.GetGenericArguments().[0] = typedefof<Chart>
            | None -> false            
        member __.Print value isExecutionResult sendExecutionResult sendDisplayData =
            let chart_display_id = Guid.NewGuid().ToString()
            let data_display_id = Guid.NewGuid().ToString()
            let chart_id = Guid.NewGuid().ToString("N")
            let aSeq: FSharp.Control.AsyncSeq<Angara.Charting.Chart> = value :?> FSharp.Control.AsyncSeq<Angara.Charting.Chart>            
            
            let displayDiv = @"
            <section>
            <div id='chart"+chart_id+"' style='height:auto'></div>
            <script src='https://cdnjs.cloudflare.com/ajax/libs/require.js/2.2.0/require.min.js'  type='text/javascript'></script>
            <script type='text/javascript'>
            require.config({                        
            paths: {
                'idd': 'https://cdn.jsdelivr.net/gh/predictionmachines/InteractiveDataDisplay@v1.5.13/dist/idd',
                'idd.umd': 'https://cdn.jsdelivr.net/gh/predictionmachines/InteractiveDataDisplay@v1.5.13/dist/idd.umd',
                'idd-css': 'https://cdn.jsdelivr.net/gh/predictionmachines/InteractiveDataDisplay@v1.5.13/dist/idd.umd',
                'jquery': 'https://cdnjs.cloudflare.com/ajax/libs/jquery/2.2.2/jquery.min',
                'jquery-ui': 'https://cdnjs.cloudflare.com/ajax/libs/jqueryui/1.11.4/jquery-ui',
                'css': 'https://cdnjs.cloudflare.com/ajax/libs/require-css/0.1.8/css.min',
                'rx': 'https://cdnjs.cloudflare.com/ajax/libs/rxjs/4.1.0/rx.lite.min',
                'svg': 'https://cdnjs.cloudflare.com/ajax/libs/svg.js/2.4.0/svg.min',
                'filesaver': 'https://cdnjs.cloudflare.com/ajax/libs/FileSaver.js/1.3.3/FileSaver.min',
                'jquery-mousewheel': 'https://cdnjs.cloudflare.com/ajax/libs/jquery-mousewheel/3.1.13/jquery.mousewheel.min',
                'angara-serialization': 'https://cdn.jsdelivr.net/gh/predictionmachines/Angara.Serialization@v0.3.0/dist/Angara.Serialization.umd'
            }
        });
        require(['angara-serialization','idd.umd'], function (Serialization,Charting) {
            //console.log('callback called');

            var viewerControl"+chart_id+" = Charting.InteractiveDataDisplay.show(document.getElementById('chart"+chart_id+"'), {});      

            window.UpdateAngaraChart"+chart_id+" = function(encodedData) {
                var decodedData = atob(encodedData);
                //console.log(decodedData);
                var parsed = JSON.parse(decodedData)
                //console.log(parsed);
                var infoset = Serialization.InfoSet.Unmarshal(parsed);
                //console.log(infoset);
                var chartInfo = Serialization.InfoSet.Deserialize(infoset);
                //console.log(chartInfo);
                
                var plotMap = [];
                for (var i = 0; i < chartInfo.plots.length; i++) {
                    var pi = chartInfo.plots[i];
                    var props = $.extend(true, {}, pi.properties);
                    props['kind'] = pi.kind;
                    props['displayName'] = pi.displayName;
                    props['titles'] = pi.titles;
                    plotMap[i] = props;
                }

                viewerControl"+chart_id+".update(plotMap);
            }

            
            });
            
            </script>
            </section>"

            sendDisplayData "text/html" displayDiv "display_data" chart_display_id

                        
            let resolver = SerializerCompositeResolver([CoreSerializerResolver.Instance; Angara.Html.Serializers])                        
            
            let sendUpdatedData (chart:Angara.Charting.Chart) =
                let dataJToken = Angara.Serialization.Json.FromObject(resolver,chart)
                let dataString = dataJToken.ToString(Formatting.Indented)
                let bytes = System.Text.Encoding.UTF8.GetBytes(dataString)
                let base64 = System.Convert.ToBase64String(bytes)
                // printfn "%s" dataSerialized
                let updateDiv = @"<div style='visibility=hidden'>                    
                    <script type='text/javascript'>
                    //console.log('probe');
                    if(window.UpdateAngaraChart"+chart_id+") {
                        window.UpdateAngaraChart"+chart_id+"('"+base64+"');
                    }
                    </script>
                    </div>"
                sendDisplayData "text/html" updateDiv "update_display_data" data_display_id
            sendDisplayData "text/html" "<div style='visibility=hidden'></div>" "display_data" data_display_id
            AsyncSeq.iter sendUpdatedData aSeq |> Async.StartImmediate

IfSharp.Kernel.Printers.addAsyncDisplayPrinter(DynamicChartPrinter.Instance, 20)
