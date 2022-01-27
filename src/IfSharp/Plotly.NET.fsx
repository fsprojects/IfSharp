#r "IfSharp.Kernel.dll"

#load @".paket/load/main.group.fsx"

open Plotly.NET
open GenericChart
open IfSharp.Kernel
open IfSharp.Kernel.Globals

do 
    Printers.addDisplayPrinter(fun (plot: GenericChart.GenericChart) ->
        { ContentType = "text/html"; Data = GenericChart.toChartHTML plot}
    )
