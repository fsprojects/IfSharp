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

type IfSharpKernel(connectionInformation : ConnectionInformation, ioSocket : Socket, shellSocket : Socket, hbSocket : Socket, controlSocket : Socket, stdinSocket : Socket) = 

    let data = new List<BinaryOutput>()
    let payload = new List<Payload>()
    let mutable executionCount = 0
    let mutable lastMessage : Option<Message> = None

    (** Splits the message up into lines and writes the lines to the specified file name *)
    let logMessage (msg : string) =
        let fileName = "shell.log"
        let messages = 
            msg.Split('\r', '\n')
            |> Seq.filter (fun x -> x <> "")
            |> Seq.map (fun x -> String.Format("{0:yyyy-MM-dd HH:mm:ss} - {1}", DateTime.Now, x))
            |> Seq.toArray
        
        File.AppendAllLines(fileName, messages)

    (** Logs the exception to the specified file name *)
    let handleException (ex : exn) = 
        let message = ex.CompleteStackTrace()
        logMessage message

    (** Decodes byte array into a string using UTF8 *)
    let decode (bytes) =
        Encoding.UTF8.GetString(bytes)

    (** Encodes a string into a byte array using UTF8 *)
    let encode (str : string) =
        Encoding.UTF8.GetBytes(str)

    (** Deserializes a dictionary from a JSON string *)
    let deserializeDict (str) =
        JsonConvert.DeserializeObject<Dictionary<string, string>>(str)

    (** Serializes any object into JSON *)
    let serialize (obj) =
        let ser = JsonSerializer()
        let sw = new StringWriter()
        ser.Serialize(sw, obj)
        sw.ToString()

    (** Constructs an 'envelope' from the specified socket *)
    let recvMessage (socket) = 
        
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

        lastMessage <- Some
            {
                Identifiers = idents |> Seq.toList;
                HmacSignature = hmac;
                Header = header;
                ParentHeader = parentHeader;
                Metadata = metadata;
                Content = content;
            }

        lastMessage.Value

    (** Convenience method for creating a header *)
    let createHeader (messageType) (sourceEnvelope) =
        {
            msg_type = messageType;
            msg_id = Guid.NewGuid().ToString();
            session = sourceEnvelope.Header.session;
            username = sourceEnvelope.Header.username;
        }

    (** Convenience method for sending a message *)
    let sendMessage (socket) (envelope) (messageType) (content) =

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
    let sendState (envelope) (state) =
        sendMessage ioSocket envelope "status" { execution_state = state } 

    (** Handles a 'kernel_info_request' message *)
    let kernelInfoRequest(msg : Message) = 
        let content = 
            {
                protocol_version = [| 4; 0 |]; 
                ipython_version = None;
                language_version = [| 1; 0; 0 |];
                language = "fsharp";
            }

        sendMessage shellSocket msg "kernel_info_reply" content

    (** Sends display data information immediately *)
    let sendDisplayData (contentType) (displayItem) (messageType) =
        data.Add( { ContentType = contentType; Data = displayItem } )
        
        if lastMessage.IsSome then

            let d = dict()
            d.Add(contentType, displayItem)

            let reply = { execution_count = executionCount; data = d; metadata = dict() }
            sendMessage ioSocket lastMessage.Value messageType reply

    (** Handles an 'execute_request' message *)
    let executeRequest(msg : Message) = 
        
        // extract the contents
        let content = match msg.Content with ExecuteRequest x -> x | _ -> failwith ("system error") 
        
        // clear some state
        sbOut.Clear() |> ignore
        sbErr.Clear() |> ignore
        data.Clear()
        payload.Clear()

        // only increment if we are not silent
        if content.silent = false then executionCount <- executionCount + 1
        
        // send busy
        sendState msg "busy"
        sendMessage ioSocket msg "pyin" { code = content.code; execution_count = executionCount  }

        // evaluate
        let ex = 
            try
                fsiEval.EvalInteraction(content.code)
                None
            with
            | exn -> Some exn

        if sbErr.Length > 0 then
            let err = sbErr.ToString().Trim()
            let executeReply =
                {
                    status = "error";
                    execution_count = executionCount;
                    ename = "generic";
                    evalue = err;
                    traceback = [||]
                }

            sendMessage shellSocket msg "execute_reply" executeReply
            sendMessage ioSocket msg "stream" { name = "stderr"; data = err; }
        else
            let executeReply =
                {
                    status = "ok";
                    execution_count = executionCount;
                    payload = payload |> Seq.toList;
                    user_variables = dict();
                    user_expressions = dict()
                }

            sendMessage shellSocket msg "execute_reply" executeReply

            // send all the data
            if content.silent = false then
                if data.Count = 0 then
                    let lastExpression = GetLastExpression()
                    if lastExpression <> "" then
                        sendDisplayData "text/plain" lastExpression "pyout"

        // we are now idle
        sendState msg "idle"

    (** Handles a 'complete_request' message *)
    let completeRequest (msg : Message) = 

        // extract the contents
        let content = match msg.Content with CompleteRequest x -> x | _ -> failwith ("system error")

        // in our custom UI we put all cells in content.text and more information in content.block
        // the position is contains the selected index and the relative character and line number
        let codes = JsonConvert.DeserializeObject<array<string>>(content.text)
        let position = JsonConvert.DeserializeObject<BlockType>(content.block)

        // calculate absolute line number
        let lineOffset = 
            codes
            |> Seq.take (position.selectedIndex)
            |> Seq.map (fun x -> x.Split('\n').Length)
            |> Seq.sum

        let realLineNumber = position.line + lineOffset
        let codeString = String.Join("\n", codes)
        let decls = IntellisenseHelper.GetDeclarations(codeString) (realLineNumber, position.ch)
        let matches = decls |> Seq.map (fun x -> { glyph = x.Glyph; name = x.Name; documentation = IntellisenseHelper.formatTip x.DescriptionText None})
        let newContent = 
            {
                matched_text = "";
                matches = matches;
                status = "ok";
            }
        
        sendMessage (shellSocket) (msg) ("complete_reply") (newContent)

    (** Handles a 'connect_request' message *)
    let connectRequest (msg : Message) = 

        let content = match msg.Content with ConnectRequest x -> x | _ -> failwith ("system error")
        let reply =
            {
                hb_port = connectionInformation.hb_port;
                iopub_port = connectionInformation.iopub_port;
                shell_port = connectionInformation.shell_port;
                stdin_port = connectionInformation.stdin_port; 
            }

        sendMessage shellSocket msg "connect_reply" reply

    (** Handles a 'shutdown_request' message *)
    let shutdownRequest (msg : Message) =

        // TODO: actually shutdown        
        let content = match msg.Content with ShutdownRequest x -> x | _ -> failwith ("system error")
        let reply = { restart = true; }

        sendMessage shellSocket msg "shutdown_reply" reply

    (** Handles a 'history_request' message *)
    let historyRequest (msg : Message) =

        let content = match msg.Content with HistoryRequest x -> x | _ -> failwith ("system error")
        // TODO: actually handle this
        sendMessage shellSocket msg "history_reply" { history = [] }

    (** Handles a 'object_info_request' message *)
    let objectInfoRequest (msg : Message) =
        // TODO: actually handle this
        ()

    (** Loops forever receiving messages from the client and processing them *)
    let doShell() =

        try
            let file = FileInfo(Assembly.GetEntryAssembly().Location)
            let dir = file.Directory.FullName
            let includeFile = Path.Combine(dir, "Include.fsx")
            let code = File.ReadAllText(includeFile)
            fsiEval.EvalInteraction(code)
        with
        | exn -> handleException exn

        logMessage (sbErr.ToString())
        logMessage (sbOut.ToString())

        while true do

            let msg = recvMessage (shellSocket)

            try
                match msg.Header.msg_type  with
                | "kernel_info_request" -> kernelInfoRequest (msg) 
                | "execute_request"     -> executeRequest (msg) 
                | "complete_request"    -> completeRequest (msg) 
                | "connect_request"     -> connectRequest (msg)
                | "shutdown_request"    -> shutdownRequest (msg)
                | "history_request"     -> historyRequest (msg)
                | "object_info_request" -> objectInfoRequest (msg)
                | _                     -> logMessage (String.Format("msg_type not implemented {0}", msg.Header.msg_type))
            with 
            | ex -> handleException ex
   
    (** Loops repeating message from the client *)
    let doHeartbeat() =

        try
            while true do
                let bytes = recv hbSocket
                let str = decode bytes
                send hbSocket bytes
        with
        | ex -> handleException ex

    (** Clears the display *)
    member self.ClearDisplay () =
        if lastMessage.IsSome then
            sendMessage (ioSocket) (lastMessage.Value) ("clear_output") { wait = false; stderr = true; stdout = true; other = true; }

    (** Sends auto complete information to the client *)
    member self.AddPayload (text) =
        payload.Add( { html = ""; source = "page"; start_line_number = 1; text = text })

    (** Adds display data to the list of display data to send to the client *)
    member self.SendDisplayData (contentType, displayItem) =
        sendDisplayData contentType displayItem "display_data"

    (** Starts the kernel asynchronously *)
    member self.StartAsync() = 
        Async.Start (async { doHeartbeat() } )
        Async.Start (async { doShell() } )

