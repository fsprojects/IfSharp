namespace IfSharp.Kernel

open System
open System.Drawing
open System.IO
open System.Net
open System.Text
open System.Drawing.Imaging
open System.Windows.Forms
open System.Xml
open System.Xml.Linq
open System.Xml.XPath

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

    static member Base64 (bytes:seq<byte>, contentType:string) =
        let base64 = Convert.ToBase64String(Array.ofSeq bytes)
        let data = "data:"+contentType+";base64,"+base64
        data

    /// Loads a local image from disk and wraps a BinaryOutput around the image data.
    static member Image (fileName:string) =
        Util.Image (File.ReadAllBytes(fileName))


    static member CreatePublicFile (name:string) (content:byte[]) =
        try
            if Directory.Exists(Config.TempDir) = false then
                Directory.CreateDirectory(Config.TempDir) |> ignore;
            let path = Path.Combine(Config.TempDir,name)
            File.WriteAllBytes(path, content)
            "/static/temp/"+name
        with exc ->
            exc.ToString()

    static member MoveSvg (svg:string) (delta:float*float) =
        let (dx,dy) = delta
        let doc = XElement.Parse(svg)
        let width = match Seq.tryFind (fun (xa:XAttribute) -> xa.Name.LocalName="width") (doc.Attributes()) with None -> 0. | Some xa -> float xa.Value
        let height = match Seq.tryFind (fun (xa:XAttribute) -> xa.Name.LocalName="height") (doc.Attributes()) with None -> 0. | Some xa -> float xa.Value
        let width = Math.Max(width, width + dx)
        let height = Math.Max(height, height + dy)
        doc.SetAttributeValue(XName.Get("width"), width)
        doc.SetAttributeValue(XName.Get("height"), height)
        let gnode = new XElement(XName.Get("g"))
        gnode.SetAttributeValue(XName.Get("transform"), "translate("+(string dx)+","+(string dy)+")")
        let objects = doc.Elements() |> Array.ofSeq;
        doc.RemoveNodes()
        gnode.Add(objects)
        doc.Add(gnode)
        let svg = doc.ToString()
        svg

    static member MergeSvg (svg1:string) (svg2:string) =
        let doc1 = XElement.Parse(svg1)
        let doc2 = XElement.Parse(svg2)
        let width1 = match Seq.tryFind (fun (xa:XAttribute) -> xa.Name.LocalName="width") (doc1.Attributes()) with None -> 0. | Some xa -> float xa.Value
        let height1 = match Seq.tryFind (fun (xa:XAttribute) -> xa.Name.LocalName="height") (doc1.Attributes()) with None -> 0. | Some xa -> float xa.Value
        let width2 = match Seq.tryFind (fun (xa:XAttribute) -> xa.Name.LocalName="width") (doc2.Attributes()) with None -> 0. | Some xa -> float xa.Value
        let height2 = match Seq.tryFind (fun (xa:XAttribute) -> xa.Name.LocalName="height") (doc2.Attributes()) with None -> 0. | Some xa -> float xa.Value
        let width = Math.Max(width1, width2)
        let height = Math.Max(height1, height2)
        doc1.SetAttributeValue(XName.Get("width"), width)
        doc1.SetAttributeValue(XName.Get("height"), height)
        let doc2Objects = doc2.Elements() |> Array.ofSeq;
        doc2.RemoveNodes()
        doc1.Add(doc2Objects)
        let svg = doc1.ToString()
        svg