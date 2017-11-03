#load "Paket.fsx"
Paket.Package [ "FsLab" ]
#load "Paket.Generated.Refs.fsx"

#load "XPlot.GoogleCharts.fsx"
#load "XPlot.Plotly.fsx"

// Load the correct FSharp.Charting script
open System
open System.IO
open System.Runtime.InteropServices

let fsChartingScript = 
    match RuntimeInformation.IsOSPlatform(OSPlatform.OSX) with
    | true -> seq { yield "#load \"FSharp.Charting.Gtk.fsx\""  }
    | _    -> seq { yield "#load \"FSharp.Charting.fsx\""  }

let tempFile = Path.Combine(__SOURCE_DIRECTORY__, "__temp__.fsx")

File.WriteAllLines(tempFile, fsChartingScript)    

#load "__temp__.fsx"  

File.Delete(tempFile)