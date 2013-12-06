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

    let internal Black        = "\u001B[0;30m"
    let internal Blue         = "\u001B[0;34m"
    let internal Green        = "\u001B[0;32m"
    let internal Cyan         = "\u001B[0;36m"
    let internal Red          = "\u001B[0;31m"
    let internal Purple       = "\u001B[0;35m"
    let internal Brown        = "\u001B[0;33m"
    let internal Gray         = "\u001B[0;37m"
    let internal DarkGray     = "\u001B[1;30m"
    let internal LightBlue    = "\u001B[1;34m"
    let internal LightGreen   = "\u001B[1;32m"
    let internal LightCyan    = "\u001B[1;36m"
    let internal LightRed     = "\u001B[1;31m"
    let internal LightPurple  = "\u001B[1;35m"
    let internal Yellow       = "\u001B[1;33m"
    let internal White        = "\u001B[1;37m"
    let internal Reset        = "\u001B[0m"

    let mutable internal kernel : Option<IfSharpKernel> = None
    let mutable internal displayPrinters : list<Type * (obj -> BinaryOutput)> = []

    (** Convenience method for encoding a string within HTML *)
    let internal htmlEncode(str) =
        System.Web.HttpUtility.HtmlEncode(str)

    (** Adds a custom display printer for extensibility *)
    let internal addDisplayPrinter(printer : 'T -> BinaryOutput) =
        displayPrinters <- (typeof<'T>, (fun (x:obj) -> printer (unbox x))) :: displayPrinters

    (** Default display printer *)
    let internal defaultDisplayPrinter(x) =
        { ContentType = "text/plain"; Data = sprintf "%A" x }

    (** Finds a display printer based off of the type *)
    let internal findDisplayPrinter(findType) = 
        let printers = 
            displayPrinters
            |> Seq.filter (fun (t, _) -> t.IsAssignableFrom(findType))
            |> Seq.toList

        if printers.Length > 0 then
            printers.Head
        else
            (typeof<obj>, defaultDisplayPrinter)

    (** Adds default display printers *)
    let internal addDefaultDisplayPrinters() = 
        
        // add generic chart printer
        addDisplayPrinter(fun (x:ChartTypes.GenericChart) ->
            { ContentType = "image/png"; Data = x.ToPng() }
        )

        // add chart printer
        addDisplayPrinter(fun (x:GenericChartWithSize) ->
            { ContentType = "image/png"; Data = x.Chart.ToPng(x.Size) }
        )
        
        // add table printer
        addDisplayPrinter(fun (x:TableOutput) -> 
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

            { ContentType = "text/html"; Data = sb.ToString() } 
        )

        // add html printer
        addDisplayPrinter(fun (x:HtmlOutput) ->
            { ContentType = "text/html"; Data = x.Html }
        )
        
        // add latex printer
        addDisplayPrinter(fun (x:LatexOutput) ->
            { ContentType = "text/latex"; Data = x.Latex }
        )

    (** Global clear display function *)
    let Clear () = 
        kernel.Value.ClearDisplay()

    (** Global display function *)
    let Display (value : obj) =

        if value <> null then
            let printer = findDisplayPrinter(value.GetType())
            let (_, callback) = printer
            let callbackValue = callback(value)
            kernel.Value.SendDisplayData(callbackValue.ContentType, callbackValue.Data)

    (** Global help function *)
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

        // adds the default display printers
        addDefaultDisplayPrinters()

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