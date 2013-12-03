namespace IfSharp.Kernel

open System
open System.Collections.Generic
open System.Diagnostics
open System.IO
open System.Reflection
open System.Text
open System.Threading

open FSharp.Charting

open Microsoft.FSharp.Compiler.Interactive.Shell
open Newtonsoft.Json
open fszmq
open fszmq.Socket

module App = 

    // logs a message to the log
    let internal logMessage fileName (msg:string) =
        let messages = 
            msg.Split('\r', '\n')
            |> Seq.map (fun x -> String.Format("{0:yyyy-MM-dd HH:mm:ss} - {1}", DateTime.Now, x))
            |> Seq.filter (fun x -> x <> "")
            |> Seq.toArray
        
        File.AppendAllLines(fileName, messages)

    // logs the exception and returns -1
    let internal handleException (fileName) (ex:exn) = 
        let message = ex.CompleteStackTrace()
        logMessage fileName message

    // decodes bytes into a string
    let internal decode (bytes) =
        Encoding.UTF8.GetString(bytes)

    // encodes a string into bytes
    let internal encode (str:string) =
        Encoding.UTF8.GetBytes(str)

    // receives a string from a socket
    let internal recvString (socket:Socket) = 
        let bytes = Socket.recv socket
        decode bytes

    // sends a string to a socket
    let internal sendString (socket:Socket) (str) = 
        let bytes = encode str
        Socket.send socket bytes

    // deserializes a dictionary from a string
    let internal deserializeDict (str) =
        JsonConvert.DeserializeObject<Dictionary<string, string>>(str)

    // serializes an object to a string
    let internal serialize (obj) =
        let ser = JsonSerializer()
        let sw = new StringWriter()
        ser.Serialize(sw, obj)
        sw.ToString()

    // sends a message
    let internal sendMessage (socket) (obj) =
        let json = serialize obj
        sendString (socket) (json)
        json

    // receives until a delimiter
    let internal recvUntil (socket) (delimiter) =
        let results = List<string>()
        let mutable msg = recvString socket
        
        while msg <> delimiter do
            results.Add(msg)
            msg <- recvString socket

        results |> Seq.toList

    // receives an envelope
    let internal recvEnvelope (socket) = 

        let uuids            = recvUntil socket "<IDS|MSG>"
        let hmac             = recvString socket
        let headerJson       = recvString socket
        let parentHeaderJson = recvString socket
        let metadata         = recvString socket
        let contentJson      = recvString socket
        
        let header           = JsonConvert.DeserializeObject<Header>(headerJson)
        let parentHeader     = JsonConvert.DeserializeObject<Header>(parentHeaderJson)
        let metaDataDict     = deserializeDict (metadata)
        let content          = ShellMessages.Deserialize (header.msg_type) (contentJson)
        let envelope         = 
            {
                Identifiers = uuids;
                HmacSignature = hmac;
                Header = header;
                ParentHeader = parentHeader;
                Metadata = metadata;
                Content = content;
            }

        envelope

    // sends an envelope
    let internal sendEnvelope (socket) (e: MessageEnvelope) =
        
        // send identifiers
        for id in e.Identifiers do
            socket <~| (encode id) |> ignore

        // send everything else
        socket
            <~| (encode "<IDS|MSG>")
            <~| (encode e.HmacSignature)
            <~| (encode (serialize e.Header))
            <~| (encode (serialize e.ParentHeader))
            <~| (encode e.Metadata)
            <<| (encode (serialize e.Content))

    // heartbeat is just echo
    let internal doHeartbeat (hbSocket) =

        try
            while true do
                let bytes = Socket.recv hbSocket
                let str = decode bytes
                Socket.send hbSocket bytes
        with
        | exn -> handleException "heartbeat.log" exn

    let internal createHeader (messageType) (sourceEnvelope) =
        {
            msg_type = messageType;
            msg_id = Guid.NewGuid().ToString();
            session = sourceEnvelope.Header.session;
            username = sourceEnvelope.Header.username;
        }

    let internal sendState (socket) (envelope) (state) =

        let ident = ""
        let delim = "<IDS|MSG>"
        let signature = ""
        let header = createHeader "status" envelope
        let metadata = "{}"
        let content = { execution_state = state } 

        socket
            <~| (encode ident)
            <~| (encode delim)
            <~| (encode signature)
            <~| (encode (serialize header))
            <~| (encode (serialize envelope.Header))
            <~| (encode metadata)
            <<| (encode (serialize content))
        
    let mutable internal executionCount = 0

    // performs shell operations
    let internal doShell (shellSocket) (iopubSocket) =

        // start up FSI in-process
        let sbOut = new StringBuilder()
        let sbErr = new StringBuilder()
        let inStream = new StringReader("")
        let outStream = new StringWriter(sbOut)
        let errStream = new StringWriter(sbErr)
        let fsiEval = FsiEvaluationSession([|"--noninteractive"|], inStream, outStream, errStream)
        let data = dict()
        
        // add our custom printers
        fsi.AddPrinter(fun (ch:ChartTypes.GenericChart) ->
            data.["image/png"] <- ch.ToPng()
            "(GenericChart)"
        ) 

        fsi.AddPrinter(fun (ch:GenericChartWithSize) ->
            data.["image/png"] <- ch.Chart.ToPng(ch.Size)
            "(GenericChartWithSize)"
        ) 

        fsi.AddPrinter(fun (x:BinaryOutput) ->
            data.[x.ContentType] <- x.Data
            "(BinaryOutput)"
        )

        fsi.AddPrinter(fun (x:LatexOutput) ->
            data.["text/latex"] <- x.Latex
            "(LatexOutput)"
        )
        
        fsi.AddPrinter(fun (x:HtmlOutput) ->
            data.["text/html"] <- x.Html
            "(LatexOutput)"
        )

        fsi.AddPrinter(fun (x:TableOutput) ->

            let sb = StringBuilder()
            sb.Append("<table>") |> ignore

            // output header
            sb.Append("<thead>") |> ignore
            sb.Append("<tr>") |> ignore
            for col in x.Columns do
                sb.Append("<th>") |> ignore
                sb.Append(col) |> ignore
                sb.Append("</th>") |> ignore
            sb.Append("</tr>") |> ignore
            sb.Append("</thead>") |> ignore

            // output body
            sb.Append("<tbody>") |> ignore
            for row in x.Rows do
                sb.Append("<tr>") |> ignore
                for cell in row do
                    sb.Append("<td>") |> ignore
                    sb.Append(cell) |> ignore
                    sb.Append("</td>") |> ignore
                    
                sb.Append("</tr>") |> ignore
            sb.Append("<tbody>") |> ignore
            sb.Append("</tbody>") |> ignore
            sb.Append("</table>") |> ignore

            data.["text/html"] <- sb.ToString()
            "(Table)"
        )

        try

            let file = FileInfo(Assembly.GetEntryAssembly().Location)
            let dir = file.Directory.FullName
            let includeFile = Path.Combine(dir, "Include.fsx")
            let code = File.ReadAllText(includeFile)
            fsiEval.EvalInteraction(code)

        with
        | exn -> handleException "shell.log" exn

        logMessage ("shell.log") (sbErr.ToString())
        logMessage ("shell.log") (sbOut.ToString())

        try

            while true do

                let envelope = recvEnvelope (shellSocket)

                if envelope.Header.msg_type = "kernel_info_request" then
                    
                    let ident = ""
                    let delim = "<IDS|MSG>"
                    let signature = ""
                    let header = createHeader "kernel_info_reply" envelope
                    let metadata = "{}"
                    let content = { protocol_version = [| 4; 0 |];  ipython_version = [| 1; 1; 0; ""|]; language_version = [| 1; 0; 0 |]; language = "fsharp"; } 

                    // send messages
                    shellSocket
                        <~| (encode ident)
                        <~| (encode delim)
                        <~| (encode signature)
                        <~| (encode (serialize header))
                        <~| (encode (serialize envelope.Header))
                        <~| (encode metadata)
                        <<| (encode (serialize content))

                else if envelope.Header.msg_type = "execute_request" then
                    
                    let content = 
                        match envelope.Content with
                        | ExecuteRequest x -> x
                        | _ -> failwith ("system error")

                    // clear stdout / stderr
                    sbOut.Clear() |> ignore
                    sbErr.Clear() |> ignore

                    // evaluate
                    sendState iopubSocket envelope "busy"
                    let mutable exMessage = ""
                    data.Clear()

                    try
                        fsiEval.EvalInteraction(content.code)
                    with 
                    | exn -> exMessage <- exn.CompleteStackTrace()

                    // send reply
                    if content.silent = false then executionCount <- executionCount + 1

                    // build data
                    data.Add("text/plain", (sbOut.ToString() + sbErr.ToString()).Trim())

                    // build message
                    let ident = ""
                    let delim = "<IDS|MSG>"
                    let signature = ""
                    let headerPub = { msg_type = "pyout"; msg_id = Guid.NewGuid().ToString(); session = envelope.Header.session; username = envelope.Header.username; }
                    let headerShell =  { headerPub with msg_type = "execute_reply"; }
                    let metadata = "{}"
                    let pyOutContent = { execution_count = executionCount; data = data; metadata = dict() }
                    let shellContent = { status = "ok"; execution_count = executionCount; payload = []; user_variables = dict(); user_expressions = dict() }

                    // send messages
                    shellSocket
                        <~| (encode ident)
                        <~| (encode delim)
                        <~| (encode signature)
                        <~| (encode (serialize headerShell))
                        <~| (encode (serialize envelope.Header))
                        <~| (encode metadata)
                        <<| (encode (serialize shellContent))
                         
                    iopubSocket
                        <~| (encode ident)
                        <~| (encode delim)
                        <~| (encode signature)
                        <~| (encode (serialize headerPub))
                        <~| (encode (serialize envelope.Header))
                        <~| (encode metadata)
                        <<| (encode (serialize pyOutContent))

                    sendState iopubSocket envelope "idle"

                else

                    logMessage "shell.log" (String.Format("msg_type not implemented {0}", envelope.Header.msg_type))

                ()
        with 
        | exn -> handleException "shell.log" exn
    
    // first argument must be an ipython connection file, blocks forever
    let Start(args:array<string>) = 

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

        // start listening and responding
        Async.Start (async { doHeartbeat hbSocket |> ignore } )
        Async.Start (async { doShell shellSocket iopubSocket |> ignore } )

        // block forever
        Thread.Sleep(Timeout.Infinite)
