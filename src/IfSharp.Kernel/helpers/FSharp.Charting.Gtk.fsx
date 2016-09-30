[<AutoOpen>]
module FSharp.Charting

#I "packages/FSharp.Charting.Gtk/lib/net40"

#r "FSharp.Compiler.Service.dll"
#r "System.Windows.Forms.DataVisualization.dll"
#r "/usr/lib/cli/gtk-sharp-3.0/gtk-sharp.dll"
#r "/usr/lib/cli/gdk-sharp-3.0/gdk-sharp.dll"
#r "/usr/lib/cli/atk-sharp-3.0/atk-sharp.dll"
#r "/usr/lib/cli/glib-sharp-3.0/glib-sharp.dll"
#r "/usr/lib/mono/4.0/Mono.Cairo.dll"
#r "OxyPlot.dll"
#r "OxyPlot.GtkSharp.dll"
#r "FSharp.Charting.Gtk.dll"
#r "IfSharp.Kernel.dll"

// Current status
// Modified FSCharting so that it exposes "CopyAsBitmap"
// Build FSCharting in my home dir
// Then install it in Ifsharp
// FSharp.Charting$ cp bin/FSharp.Charting.Gtk.* ../ifsharp/IfSharp_git/bin/packages/FSharp.Charting.Gtk/lib/net40/
// however, testing in feature notebook 4.
//#I "/home/nmurphy/coding/ifsharp/IfSharp_git/bin" //?why need this?
//#load "FSCharting.Gtk.fsx"
//IfSharp.FSCharting.Initialize()
// FSharp.Charting.Chart.Bar(data) //|> Display
// We get a broken image.

open System.IO

open IfSharp.Kernel
open System.Drawing
open System.Drawing.Imaging
open OxyPlot
open OxyPlot.Series
//open GTKSharp
open FSharp.Charting

type GenericChartWithSize =
    {
        Chart: ChartTypes.GenericChart;
        Size: int * int;
    }
type GenericChartsWithSize =
    {
        Charts: ChartTypes.GenericChart list;
        Size: int * int;
        Columns: int;
    }

//static member
let MultipleCharts (charts: ChartTypes.GenericChart list) (size:int*int) (cols:int) =
    { Charts = charts; Size = size; Columns = cols }


type ChartTypes.GenericChart with
    /// Wraps a GenericChartWithSize around the GenericChart
    member self.WithSize(x:int, y:int) =
        {
            Chart = self;
            Size = (x, y);
        }

    /// Converts the GenericChart to a PNG, in order to do this, we must show a form with ChartControl on it, save the bmp, then write the png to memory
    member self.ToPng(?size) =
        // get the size
        let (width, height) = if size.IsNone then (320, 240) else size.Value

        // create a new ChartControl in order to get the underlying Chart
        //let ctl = new ChartTypes.ChartControl(self)

        // save
        use ms = new MemoryStream()
        //let plot = new OxyPlot.GtkSharp.PlotView(Model = self.Model )
        //let bm = self.CopyAsBitmap()
        let pngExporter = new OxyPlot.GtkSharp.PngExporter()
        pngExporter.Width <- width
        pngExporter.Height <- height
        pngExporter.Background <- OxyPlot.OxyColors.White
        // write to a temporary file
        //let tmp = sprintf "%s.png" (System.Guid.NewGuid().ToString())
        pngExporter.Export(self.Model, ms) //tmp, width, height, OxyPlot.OxyColors.White)
        //let bytes = File.ReadAllBytes(tmp);
        // write to the stream
        //stream.Write(bytes, 0, bytes.Length);
        // delete the temporary file
        //File.Delete(tmp);

        //Export(self.Model, ms, width, height, e, 96)

        //self.Chart.SaveImage(ms, ChartImageFormat.Png |> int |> enum)

        ms.Seek(0L, System.IO.SeekOrigin.Begin) |> ignore
        ms.ToArray()
        //Bitmap.FromStream ms
        //let actualChart = ctl.Controls.[0] :?> System.Windows.Forms.DataVisualization.Charting.Chart
        //actualChart.Dock <- DockStyle.None
        //actualChart.Size <- Size(width, height)
        //actualChart.SaveImage(ms, ImageFormat.Png)
        //ms.ToArray()

    member self.ToData(?size) =
        let bytes = match size with Some size -> self.ToPng(size) | _ -> self.ToPng()
        let base64 = System.Convert.ToBase64String(bytes)
        let data = "data:image/png;base64,"+base64
        data

type FSharp.Charting.Chart with

    /// Wraps a GenericChartWithSize around the GenericChart
    static member WithSize(x:int, y:int) =

        fun (ch : #ChartTypes.GenericChart) ->
            ch.WithSize(x, y)

do
    Printers.addDisplayPrinter(fun (x:ChartTypes.GenericChart) ->
        { ContentType = "image/png"; Data = x.ToPng() })

    // add chart printer
    Printers.addDisplayPrinter(fun (x:GenericChartWithSize) ->
        { ContentType = "image/png"; Data = x.Chart.ToPng(x.Size) })

    // add generic chart printer
    Printers.addDisplayPrinter(fun (x:ChartTypes.GenericChart) ->
        { ContentType = "image/png"; Data = x.ToPng() })

    // add chart printer
    Printers.addDisplayPrinter(fun (x:GenericChartWithSize) ->
                { ContentType = "image/png"; Data = x.Chart.ToPng(x.Size) })


    Printers.addDisplayPrinter(fun (x:GenericChartsWithSize) ->
        let count = x.Charts.Length
        let (width, height) = x.Size
        let totalWidth = if count = 1 then width else width * x.Columns
        let numRows = int (System.Math.Ceiling (float count / float x.Columns))
        let totalHeight = numRows * height
        let finalBitmap = new Bitmap(totalWidth, totalHeight)
        let finalGraphics = Graphics.FromImage(finalBitmap)
        let copy i (chart:ChartTypes.GenericChart) =
            let img = chart.ToPng(x.Size)
            let bitmap = new Bitmap(new MemoryStream(img))
            finalGraphics.DrawImage(bitmap, i % x.Columns * width, i / x.Columns * height)
        List.iteri copy x.Charts;
        finalGraphics.Dispose();
        let ms = new MemoryStream()
        //TODO finalBitmap.Save(ms, ImageFormat.Png);
        { ContentType = "image/png"; Data = ms.ToArray() }
    )

    Printers.addDisplayPrinter(fun (x:GenericChartsWithSize) ->
                let count = x.Charts.Length
                let (width, height) = x.Size
                let totalWidth = if count = 1 then width else width * x.Columns
                let numRows = int (System.Math.Ceiling (float count / float x.Columns))
                let totalHeight = numRows * height
                let finalBitmap = new Bitmap(totalWidth, totalHeight)
                let finalGraphics = Graphics.FromImage(finalBitmap)
                let copy i (chart:ChartTypes.GenericChart) =
                    let img = chart.ToPng(x.Size)
                    let bitmap = new Bitmap(new MemoryStream(img))
                    finalGraphics.DrawImage(bitmap, i % x.Columns * width, i / x.Columns * height)
                List.iteri copy x.Charts;
                finalGraphics.Dispose();
                let ms = new MemoryStream()
                //TODO finalBitmap.Save(ms, ImageFormat.Png);
                { ContentType = "image/png"; Data = ms.ToArray() }
            )
