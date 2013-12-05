namespace IfSharp.Kernel

open System
open System.Collections.Generic
open System.IO
open System.Text
open System.Threading

open FSharp.Charting
open Newtonsoft.Json
open fszmq
open fszmq.Socket

module App = 

    let Black        = "\u001B[0;30m"
    let Blue         = "\u001B[0;34m"
    let Green        = "\u001B[0;32m"
    let Cyan         = "\u001B[0;36m"
    let Red          = "\u001B[0;31m"
    let Purple       = "\u001B[0;35m"
    let Brown        = "\u001B[0;33m"
    let Gray         = "\u001B[0;37m"
    let DarkGray     = "\u001B[1;30m"
    let LightBlue    = "\u001B[1;34m"
    let LightGreen   = "\u001B[1;32m"
    let LightCyan    = "\u001B[1;36m"
    let LightRed     = "\u001B[1;31m"
    let LightPurple  = "\u001B[1;35m"
    let Yellow       = "\u001B[1;33m"
    let White        = "\u001B[1;37m"
    let Reset        = "\u001B[0m"

    let mutable kernel:Option<IfSharpKernel> = None

    (** Convenience method for encoding a string within HTML *)
    let htmlEncode(str) =
        System.Web.HttpUtility.HtmlEncode(str)

    (** Displays a generic HTML string *)
    let internal displayHtml (x:HtmlOutput) =
        kernel.Value.SendDisplayData("text/html", x.Html)

    (** Constructs an HTML table from the specified TableOutput *)
    let internal displayTable(x:TableOutput) =
        let sb = StringBuilder()
        sb.Append("<table>") |> ignore

        // output header
        sb.Append("<thead>") |> ignore
        sb.Append("<tr>") |> ignore
        for col in x.Columns do
            sb.Append("<th>") |> ignore
            sb.Append(htmlEncode col) |> ignore
            sb.Append("</th>") |> ignore
        sb.Append("</tr>") |> ignore
        sb.Append("</thead>") |> ignore

        // output body
        sb.Append("<tbody>") |> ignore
        for row in x.Rows do
            sb.Append("<tr>") |> ignore
            for cell in row do
                sb.Append("<td>") |> ignore
                sb.Append(htmlEncode cell) |> ignore
                sb.Append("</td>") |> ignore
                    
            sb.Append("</tr>") |> ignore
        sb.Append("<tbody>") |> ignore
        sb.Append("</tbody>") |> ignore
        sb.Append("</table>") |> ignore

        displayHtml { Html = sb.ToString() }

    (** Displays a string *)
    let displayString (x : string) =
        kernel.Value.SendDisplayData("text/plain", x)

    (** Displays a generic object using sprintf "%A" *)
    let displayGeneric (x : obj) =
        displayString (sprintf "%A" x)

    (** Displays a generic chart by converting it to a png *)
    let displayGenericChart (x : ChartTypes.GenericChart) =
        kernel.Value.SendDisplayData("image/png", x.ToPng())

    (** Displays a generic chart with size by converting it to a png with the size *)
    let displayChartWithSize (x : GenericChartWithSize) =
        kernel.Value.SendDisplayData("image/png", x.Chart.ToPng(x.Size))

    (** Displays a generic chart with size by converting it to a png with the size *)
    let displayLatex (x : LatexOutput) =
        kernel.Value.SendDisplayData("text/latex", x.Latex)

    (** Displays a generic object *)
    let displayBinaryOutput (x : BinaryOutput) =
        kernel.Value.SendDisplayData(x.ContentType, x.Data)

    let Clear () = 
        kernel.Value.ClearDisplay()

    (** Global display function *)
    let Display (value : obj) =
        match value with
        | :? ChartTypes.GenericChart as chart -> displayGenericChart chart
        | :? GenericChartWithSize    as chart -> displayChartWithSize chart
        | :? TableOutput             as table -> displayTable table
        | :? HtmlOutput              as html  -> displayHtml html
        | :? LatexOutput             as math  -> displayLatex math
        | :? BinaryOutput            as raw   -> displayBinaryOutput raw
        | _                                   -> displayGeneric value

    (** Displays help about an object *)
    let Help (value : obj) = 
        let text = StringBuilder()
        let props = value.GetType().GetProperties() |> Seq.map (fun x -> x.Name) |> Seq.distinct |> Seq.toArray
        let meths = value.GetType().GetMethods() |> Seq.map (fun x -> x.Name) |> Seq.distinct |> Seq.toArray

        // type information
        text.Append(Blue)
            .Append("Type: ")
            .AppendLine(value.GetType().FullName)
            .Append(Reset) |> ignore

        // output properties
        text.AppendLine() |> ignore
        text.Append(Red).AppendLine("Properties").Append(Reset) |> ignore
        props |> Seq.iter (fun x -> text.AppendLine(x) |> ignore)

        // output methods
        text.AppendLine() |> ignore
        text.Append(Red).AppendLine("Methods").Append(Reset) |> ignore
        meths |> Seq.iter (fun x -> text.AppendLine(x) |> ignore)

        // add to the payload
        kernel.Value.AddPayload(text.ToString())

    (** First argument must be an ipython connection file, blocks forever *)
    let Start (args : array<string>) = 

        // validate
        if args.Length < 1 then failwith "First argument must be a connection file for ipython"

        // get connection information
        let fileName = args.[0]
        let json = File.ReadAllText(fileName)
        let connectionInformation = JsonConvert.DeserializeObject<ConnectionInformation>(json)

        // startup 0mq stuff
        use context = new Context()

        // heartbeat
        use hbSocket = Context.rep context
        Socket.bind (hbSocket) (String.Format("{0}://{1}:{2}", connectionInformation.transport, connectionInformation.ip, connectionInformation.hb_port))
        
        // shell
        use shellSocket = Context.route context
        Socket.bind (shellSocket) (String.Format("{0}://{1}:{2}", connectionInformation.transport, connectionInformation.ip, connectionInformation.shell_port))
        
        // control
        use controlSocket = Context.route context
        Socket.bind (controlSocket) (String.Format("{0}://{1}:{2}", connectionInformation.transport, connectionInformation.ip, connectionInformation.control_port))

        // stdin
        use stdinSocket = Context.route context
        Socket.bind (stdinSocket) (String.Format("{0}://{1}:{2}", connectionInformation.transport, connectionInformation.ip, connectionInformation.stdin_port))

        // iopub
        use iopubSocket = Context.pub context
        Socket.bind (iopubSocket) (String.Format("{0}://{1}:{2}", connectionInformation.transport, connectionInformation.ip, connectionInformation.iopub_port))

        // start the kernel
        kernel <- Some (IfSharpKernel(connectionInformation, iopubSocket, shellSocket, hbSocket, controlSocket, stdinSocket))
        kernel.Value.StartAsync()

        // block forever
        Thread.Sleep(Timeout.Infinite)
