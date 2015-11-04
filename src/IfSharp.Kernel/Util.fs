namespace IfSharp.Kernel

open System
open System.Drawing
open System.IO
open System.Net
open System.Text
open System.Drawing.Imaging
open System.Windows.Forms
open FSharp.Charting

type BinaryOutput =
    { 
        ContentType: string;
        Data: obj
    }

type TableOutput = 
    {
        Columns: array<string>;
        Rows: array<array<string>>;
    }

type LatexOutput =
    {
        Latex: string;
    }

type HtmlOutput =
    {
        Html: string;
    }

type SvgOutput =
    {
        Svg: string;
    }

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

module InternalUtil =
  let KernelDir = 
    let thisExecutable = System.Reflection.Assembly.GetEntryAssembly().Location
    let userDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    let appData =  
      match Environment.OSVersion.Platform with
        | PlatformID.Win32Windows | PlatformID.Win32NT -> Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
        | PlatformID.MacOSX -> Path.Combine(userDir, "Library")
        | _ -> Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) // PlatformID.Unix
    let jupyterDir = 
      match Environment.OSVersion.Platform with 
        | PlatformID.Unix -> Path.Combine(appData, "jupyter")
        | _ -> Path.Combine(appData, "Jupyter")
    let kernelsDir = Path.Combine(jupyterDir, "kernels")
    let kernelDir = Path.Combine(kernelsDir, "ifsharp")
    kernelDir

  let TempDir = Path.Combine(KernelDir, "temp");

[<AutoOpen>]
module ExtensionMethods =

    type Exception with
        
        /// Convenience method for getting the full stack trace by going down the inner exceptions
        member self.CompleteStackTrace() = 
            
            let mutable ex = self
            let sb = StringBuilder()
            while ex <> null do
                sb.Append(ex.GetType().Name)
                  .AppendLine(ex.Message)
                  .AppendLine(ex.StackTrace) |> ignore

                ex <- ex.InnerException

            sb.ToString()

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
            let ctl = new ChartTypes.ChartControl(self)

            // save
            use ms = new MemoryStream()
            let actualChart = ctl.Controls.[0] :?> System.Windows.Forms.DataVisualization.Charting.Chart
            actualChart.Dock <- DockStyle.None
            actualChart.Size <- Size(width, height)
            actualChart.SaveImage(ms, ImageFormat.Png)
            ms.ToArray()

    type FSharp.Charting.Chart with
    
        /// Wraps a GenericChartWithSize around the GenericChart
        static member WithSize(x:int, y:int) = 

            fun (ch : #ChartTypes.GenericChart) ->
                ch.WithSize(x, y)

type Util = 

    /// Wraps a LatexOutput around a string in order to send to the UI.
    static member Latex (str) =
        { Latex = str}

    static member Svg (str) =
        { Svg = str}

    /// Wraps a LatexOutput around a string in order to send to the UI.
    static member Math (str) =
        { Latex = "$$" + str + "$$" }

    /// Wraps a HtmlOutput around a string in order to send to the UI.
    static member Html (str) =
        { Html = str }

    ///  Creates an array of strings with the specified properties and the item to get the values out of.
    static member Row (columns:seq<Reflection.PropertyInfo>) (item:'A) =
        columns
        |> Seq.map (fun p -> p.GetValue(item))
        |> Seq.map (fun x -> Convert.ToString(x))
        |> Seq.toArray

    /// Creates a TableOutput out of a sequence of items and a list of property names.
    static member Table (items:seq<'A>, ?propertyNames:seq<string>) =

        // get the properties
        let properties =
            if propertyNames.IsSome then
                typeof<'A>.GetProperties()
                |> Seq.filter (fun x -> (propertyNames.Value |> Seq.exists (fun y -> x.Name = y)))
                |> Seq.toArray
            else
                typeof<'A>.GetProperties()

        {
            Columns = properties |> Array.map (fun x -> x.Name);
            Rows = items |> Seq.map (Util.Row properties) |> Seq.toArray;
        }

    /// Downloads the specified url and wraps a BinaryOutput around the results.
    static member Url (url:string) =
        let req = WebRequest.Create(url)
        let res = req.GetResponse()
        use stream = res.GetResponseStream()
        use mstream = new MemoryStream()
        stream.CopyTo(mstream)
        { ContentType = res.ContentType; Data =  mstream.ToArray() }

    /// Wraps a BinaryOutput around image bytes with the specified content-type
    static member Image (bytes:seq<byte>, ?contentType:string) =
        {
            ContentType = if contentType.IsSome then contentType.Value else "image/jpeg";
            Data = bytes;
        }

    /// Loads a local image from disk and wraps a BinaryOutput around the image data.
    static member Image (fileName:string) =
        Util.Image (File.ReadAllBytes(fileName))

    static member MultipleCharts (charts: ChartTypes.GenericChart list) (size:int*int) (cols:int) =
        { Charts = charts; Size = size; Columns = cols }

    static member CreatePublicFile (name:string) (content:byte[]) =
        try
            let path = Path.Combine(InternalUtil.TempDir,name)
            File.WriteAllBytes(path, content)
            "/static/temp/"+name
        with exc ->
            exc.ToString()