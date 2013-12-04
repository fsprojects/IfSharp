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

    (** Splits the message up into lines and writes the lines to the specified file name *)
    let internal logMessage fileName (msg:string) =
        let messages = 
            msg.Split('\r', '\n')
            |> Seq.filter (fun x -> x <> "")
            |> Seq.map (fun x -> String.Format("{0:yyyy-MM-dd HH:mm:ss} - {1}", DateTime.Now, x))
            |> Seq.toArray
        
        File.AppendAllLines(fileName, messages)

    (** Logs the exception to the specified file name *)
    let internal handleException (fileName) (ex:exn) = 
        let message = ex.CompleteStackTrace()
        logMessage fileName message

    (** Decodes byte array into a string using UTF8 *)
    let internal decode (bytes) =
        Encoding.UTF8.GetString(bytes)

    (** Encodes a string into a byte array using UTF8 *)
    let internal encode (str:string) =
        Encoding.UTF8.GetBytes(str)

    (** Deserializes a dictionary from a JSON string *)
    let internal deserializeDict (str) =
        JsonConvert.DeserializeObject<Dictionary<string, string>>(str)

    (** Serializes any object into JSON *)
    let internal serialize (obj) =
        let ser = JsonSerializer()
        let sw = new StringWriter()
        ser.Serialize(sw, obj)
        sw.ToString()

    (** Constructs an 'envelope' from the specified socket *)
    let internal recvEnvelope (socket) = 
        
        // receive all parts of the message
        let message =
            recvAll (socket)
            |> Seq.map decode
            |> Seq.toArray

        // find the delimiter between IDS and MSG
        let idx = Array.IndexOf(message, "<IDS|MSG>")
        let idents = message.[0..idx - 1]
        let messageList = message.[idx + 1..message.Length - 1]

        // detect a malformed message
        if messageList.Length < 4 then failwith ("Malformed message")

        // assemble the 'envelope'
        let hmac             = messageList.[0]
        let headerJson       = messageList.[1]
        let parentHeaderJson = messageList.[2]
        let metadata         = messageList.[3]
        let contentJson      = messageList.[4]
        
        let header           = JsonConvert.DeserializeObject<Header>(headerJson)
        let parentHeader     = JsonConvert.DeserializeObject<Header>(parentHeaderJson)
        let metaDataDict     = deserializeDict (metadata)
        let content          = ShellMessages.Deserialize (header.msg_type) (contentJson)
        let envelope         = 
            {
                Identifiers = idents |> Seq.toList;
                HmacSignature = hmac;
                Header = header;
                ParentHeader = parentHeader;
                Metadata = metadata;
                Content = content;
            }

        envelope

    (** Convenience method for creating a header *)
    let internal createHeader (messageType) (sourceEnvelope) =
        {
            msg_type = messageType;
            msg_id = Guid.NewGuid().ToString();
            session = sourceEnvelope.Header.session;
            username = sourceEnvelope.Header.username;
        }

    (** Convenience method for sending a message *)
    let internal sendMessage (socket) (envelope) (messageType) (content) =

        let header = createHeader messageType envelope

        for ident in envelope.Identifiers do
            socket <~| (encode ident) |> ignore

        socket
            <~| (encode "<IDS|MSG>")
            <~| (encode "")
            <~| (encode (serialize header))
            <~| (encode (serialize envelope.Header))
            <~| (encode "{}")
            <<| (encode (serialize content))
        
    (** Convenience method for sending the state of the kernel *)
    let internal sendState (socket) (envelope) (state) =
        sendMessage socket envelope "status" { execution_state = state } 
        
    (** The display data to send to the user *)
    let internal data = new List<BinaryOutput>()

    (** Convenience method for encoding a string within HTML *)
    let internal htmlEncode(str) =
        System.Web.HttpUtility.HtmlEncode(str)

    (** Displays a generic HTML string *)
    let internal displayHtml (x:HtmlOutput) =
        data.Add( { ContentType = "text/html"; Data = x.Html })

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
    let internal displayString (x:string) =
        data.Add( { ContentType = "text/plain"; Data = x })

    (** Displays a generic object using sprintf "%A" *)
    let internal displayGeneric (x:obj) =
        displayString (sprintf "%A" x)

    (** Displays a generic chart by converting it to a png *)
    let internal displayGenericChart (x:ChartTypes.GenericChart) =
        data.Add( { ContentType = "image/png"; Data = x.ToPng() } )

    (** Displays a generic chart with size by converting it to a png with the size *)
    let internal displayChartWithSize (x:GenericChartWithSize) =
        data.Add( { ContentType = "image/png"; Data = x.Chart.ToPng(x.Size) } )

    (** Global display function *)
    let Display (value : obj) =
        match value with
        | :? BinaryOutput            as bo    -> data.Add(bo)
        | :? ChartTypes.GenericChart as chart -> displayGenericChart chart
        | :? GenericChartWithSize    as chart -> displayChartWithSize chart
        | :? TableOutput             as table -> displayTable table
        | :? HtmlOutput              as html  -> displayHtml html
        | _                                   -> displayGeneric value

        value

    // performs shell operations
    let mutable internal executionCount = 0

    (** Loops forever receiving messages from the client and processing them *)
    let internal doShell (shellSocket) (iopubSocket) =

        // start up FSI in-process
        let sbOut = new StringBuilder()
        let sbErr = new StringBuilder()
        let inStream = new StringReader("")
        let outStream = new StringWriter(sbOut)
        let errStream = new StringWriter(sbErr)
        let fsiEval = FsiEvaluationSession([|"--noninteractive"|], inStream, outStream, errStream)
        
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
                    
                    let content = 
                        {
                            protocol_version = [| 4; 0 |]; 
                            ipython_version = None;
                            language_version = [| 1; 0; 0 |];
                            language = "fsharp";
                        }

                    sendMessage shellSocket envelope "kernel_info_reply" content

                else if envelope.Header.msg_type = "execute_request" then
                    
                    let content = 
                        match envelope.Content with
                        | ExecuteRequest x -> x
                        | _ -> failwith ("system error")

                    // clear some state
                    sbOut.Clear() |> ignore
                    sbErr.Clear() |> ignore
                    data.Clear()
                    
                    if content.silent = false then executionCount <- executionCount + 1
                    sendState iopubSocket envelope "busy"
                    sendMessage iopubSocket envelope "pyin" { code = content.code; execution_count = executionCount  }

                    // evaluate
                    let mutable exMessage = ""
                    try
                        fsiEval.EvalInteraction(content.code)
                    with 
                    | exn -> exMessage <- exn.CompleteStackTrace()


                    if sbErr.Length > 0 then
                        let executeReply =
                            {
                                status = "error";
                                execution_count = executionCount;
                                ename = "generic";
                                evalue = sbErr.ToString();
                                traceback = [||]
                            }

                        sendMessage shellSocket envelope "execute_reply" executeReply
                        sendMessage iopubSocket envelope "pyerr" executeReply
                        
                        let d = dict()
                        d.Add("text/plain", sbErr.ToString())
                        let pyoutReply = { execution_count = executionCount; data = d; metadata = dict() }
                        sendMessage iopubSocket envelope "pyout" pyoutReply
                    else
                        let executeReply =
                            {
                                status = "ok";
                                execution_count = executionCount;
                                payload = [];
                                user_variables = dict();
                                user_expressions = dict()
                            }

                        sendMessage shellSocket envelope "execute_reply" executeReply

                        // send all the data
                        if content.silent = false then

                            if data.Count = 0 then data.Add({ ContentType = "text/plain"; Data = sbOut.ToString(); })

                            for datum in data do
                        
                                let d = dict()
                                d.Add(datum.ContentType, datum.Data)

                                let pyoutReply = { execution_count = executionCount; data = d; metadata = dict() }
                                sendMessage iopubSocket envelope "pyout" pyoutReply

                    // we are now idle
                    sendState iopubSocket envelope "idle"

                else

                    logMessage "shell.log" (String.Format("msg_type not implemented {0}", envelope.Header.msg_type))

                ()
        with 
        | exn -> handleException "shell.log" exn
   
    (** Loops repeating message from the client *)
    let internal doHeartbeat (hbSocket) =

        try
            while true do
                let bytes = Socket.recv hbSocket
                let str = decode bytes
                Socket.send hbSocket bytes
        with
        | exn -> handleException "heartbeat.log" exn
 
    (** First argument must be an ipython connection file, blocks forever *)
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
