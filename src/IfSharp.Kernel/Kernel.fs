namespace IfSharp.Kernel

open System
open System.Collections.Generic
open System.IO
open System.Reflection
open System.Text
open System.Threading
open System.Security.Cryptography


open Newtonsoft.Json
open NetMQ
open NetMQ.Sockets

type IfSharpKernel(connectionInformation : ConnectionInformation) = 

    // heartbeat
    let hbSocket = new RouterSocket()
    let hbSocketURL = String.Format("{0}://{1}:{2}", connectionInformation.transport, connectionInformation.ip, connectionInformation.hb_port) 
    do hbSocket.Bind(hbSocketURL)
        
    // control
    let controlSocket = new RouterSocket()
    let controlSocketURL = String.Format("{0}://{1}:{2}", connectionInformation.transport, connectionInformation.ip, connectionInformation.control_port)
    do controlSocket.Bind(controlSocketURL)

    // stdin
    let stdinSocket = new RouterSocket()
    let stdinSocketURL = String.Format("{0}://{1}:{2}", connectionInformation.transport, connectionInformation.ip, connectionInformation.stdin_port)
    do stdinSocket.Bind(stdinSocketURL)

    // iopub
    let ioSocket = new PublisherSocket()
    let ioSocketURL = String.Format("{0}://{1}:{2}", connectionInformation.transport, connectionInformation.ip, connectionInformation.iopub_port)
    do ioSocket.Bind(ioSocketURL)

    // shell
    let shellSocket = new RouterSocket()
    let shellSocketURL =String.Format("{0}://{1}:{2}", connectionInformation.transport, connectionInformation.ip, connectionInformation.shell_port)
    do shellSocket.Bind(shellSocketURL)

    let payload = new List<Payload>()
    let compiler = FsCompiler(FileInfo(".").FullName)
    let mutable executionCount = 0
    let mutable lastMessage : Option<KernelMessage> = None

    /// Gets the header code to prepend to all items
    let headerCode = 
        let file = FileInfo(Assembly.GetEntryAssembly().Location)
        let dir = file.Directory.FullName
        let includeFile = Path.Combine(dir, "Include.fsx")
        let code = File.ReadAllText(includeFile)
        String.Format(code, dir.Replace("\\", "\\\\"))

    /// Splits the message up into lines and writes the lines to shell.log
    let logMessage (msg : string) =
        let fileName = "shell.log"
        let messages = 
            msg.Split('\r', '\n')
            |> Seq.filter (fun x -> x <> "")
            |> Seq.map (fun x -> String.Format("{0:yyyy-MM-dd HH:mm:ss} - {1}", DateTime.Now, x))
            |> Seq.toArray
        try
            File.AppendAllLines(fileName, messages)
        with _ -> ()

    /// Logs the exception to the specified file name
    let handleException (ex : exn) = 
        let message = ex.CompleteStackTrace()
        logMessage message

    /// Decodes byte array into a string using UTF8
    let decode (bytes) =
        Encoding.UTF8.GetString(bytes)

    /// Encodes a string into a byte array using UTF8
    let encode (str : string) =
        Encoding.UTF8.GetBytes(str)

    /// Deserializes a dictionary from a JSON string
    let deserializeDict (str) =
        JsonConvert.DeserializeObject<Dictionary<string, string>>(str)

    /// Serializes any object into JSON
    let serialize (obj) =
        let ser = JsonSerializer()
        let sw = new StringWriter()
        ser.Serialize(sw, obj)
        sw.ToString()

    /// Sign a set of strings.
    let hmac = new HMACSHA256(Encoding.UTF8.GetBytes(connectionInformation.key))
    let sign (parts:string list) : string =
        if connectionInformation.key = "" then "" else
          ignore (hmac.Initialize())
          List.iter (fun (s:string) -> let bytes = Encoding.UTF8.GetBytes(s) in ignore(hmac.TransformBlock(bytes, 0, bytes.Length, null, 0))) parts
          ignore (hmac.TransformFinalBlock(Array.zeroCreate 0, 0, 0))
          BitConverter.ToString(hmac.Hash).Replace("-", "").ToLower()

    let recvAll (socket: NetMQSocket) = socket.ReceiveMultipartBytes()
    
    /// Constructs an 'envelope' from the specified socket
    let recvMessage (socket: NetMQSocket) = 
        
        // receive all parts of the message
        let message = (recvAll (socket)) |> Array.ofSeq
        let asStrings = message |> Array.map decode

        // find the delimiter between IDS and MSG
        let idx = Array.IndexOf(asStrings, "<IDS|MSG>")

        let idents = message.[0..idx - 1]
        let messageList = asStrings.[idx + 1..message.Length - 1]

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

        let calculated_signature = sign [headerJson; parentHeaderJson; metadata; contentJson]
        if calculated_signature <> hmac then failwith("Wrong message signature")

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

    /// Convenience method for creating a header
    let createHeader (messageType) (sourceEnvelope) =
        {
            msg_type = messageType;
            msg_id = Guid.NewGuid().ToString();
            session = sourceEnvelope.Header.session;
            username = sourceEnvelope.Header.username;
        }

    /// Convenience method for sending a message
    let sendMessage (socket: NetMQSocket) (envelope) (messageType) (content) =

        let header = createHeader messageType envelope
        let msg = NetMQMessage()

        for ident in envelope.Identifiers do
            msg.Append(ident)

        let header = serialize header
        let parent_header = serialize envelope.Header
        let meta = "{}"
        let content = serialize content
        let signature = sign [header; parent_header; meta; content]

        msg.Append(encode "<IDS|MSG>")
        msg.Append(encode signature)
        msg.Append(encode header)
        msg.Append(encode parent_header)
        msg.Append(encode "{}")
        msg.Append(encode content)
        socket.SendMultipartMessage(msg)

        
    /// Convenience method for sending the state of the kernel
    let sendState (envelope) (state) =
        sendMessage ioSocket envelope "status" { execution_state = state } 

    /// Convenience method for sending the state of 'busy' to the kernel
    let sendStateBusy (envelope) =
        sendState envelope "busy"

    /// Convenience method for sending the state of 'idle' to the kernel
    let sendStateIdle (envelope) =
        sendState envelope "idle"

    /// Handles a 'kernel_info_request' message
    let kernelInfoRequest(msg : KernelMessage) (content : KernelRequest) = 
        let content = 
            {
                protocol_version = "4.0.0";
                implementation = "ifsharp";
                implementation_version = "4.0.0";
                banner = "";
                help_links = [||];
                language = "fsharp";
                language_info =
                {
                    name = "fsharp";
                    version = "4.3.1.0";
                    mimetype = "text/x-fsharp";
                    file_extension = ".fs";
                    pygments_lexer = "";
                    codemirror_mode = "";
                    nbconvert_exporter = "";
                };
            }

        sendStateBusy msg
        sendMessage shellSocket msg "kernel_info_reply" content

    /// Sends display data information immediately
    let sendDisplayData (contentType) (displayItem) (messageType) =        
        if lastMessage.IsSome then

            let d = Dictionary<string,obj>()
            d.Add(contentType, displayItem)

            let reply = { execution_count = executionCount; data = d; metadata = Dictionary<string,obj>() }
            sendMessage ioSocket lastMessage.Value messageType reply

    /// Sends a message to pyout
    let pyout (message) = sendDisplayData "text/plain" message "pyout"

    /// Preprocesses the code and evaluates it
    let preprocessAndEval(code) = 

        logMessage code

        // preprocess
        let results = compiler.NuGetManager.Preprocess(code)
        let newCode = String.Join("\n", results.FilteredLines)

        if not (Seq.isEmpty results.HelpLines) then
            fsiEval.EvalInteraction("#help")
            let ifsharpHelp =
                """  IF# notebook directives:

    #fsioutput ["on"|"off"];;   Toggle output display on/off
    """
            let fsiHelp = sbOut.ToString()
            pyout (ifsharpHelp + fsiHelp)
            sbOut.Clear() |> ignore

        //This is a persistent toggle, just respect the last one
        if not (Seq.isEmpty results.FsiOutputLines) then
            let lastFsiOutput = Seq.last results.FsiOutputLines
            if lastFsiOutput.ToLower().Contains("on") then
                fsiout := true
            else if lastFsiOutput.ToLower().Contains("off") then
                fsiout := false
            else
                pyout (sprintf "Unreocognised fsioutput setting: %s" lastFsiOutput)

        // do nuget stuff
        for package in results.Packages do
            if not (String.IsNullOrWhiteSpace(package.Error)) then
                pyout ("NuGet error: " + package.Error)
            else
                pyout ("NuGet package: " + package.Package.Value.Id)
                for frameworkAssembly in package.FrameworkAssemblies do
                    pyout ("Referenced Framework: " + frameworkAssembly.AssemblyName)
                    let code = String.Format(@"#r @""{0}""", frameworkAssembly.AssemblyName)
                    fsiEval.EvalInteraction(code)

                for assembly in package.Assemblies do
                    let fullAssembly = compiler.NuGetManager.GetFullAssemblyPath(package, assembly)
                    pyout ("Referenced: " + fullAssembly)

                    let code = String.Format(@"#r @""{0}""", fullAssembly)
                    fsiEval.EvalInteraction(code)

        if not <| String.IsNullOrEmpty(newCode) then
            fsiEval.EvalInteraction(newCode)

        if fsiout.Value then
            pyout (sbOut.ToString())
    
    /// Handles an 'execute_request' message
    let executeRequest(msg : KernelMessage) (content : ExecuteRequest) = 
        
        // clear some state
        sbOut.Clear() |> ignore
        sbErr.Clear() |> ignore
        payload.Clear()

        // only increment if we are not silent
        if content.silent = false then executionCount <- executionCount + 1
        
        // send busy
        sendStateBusy msg
        sendMessage ioSocket msg "pyin" { code = content.code; execution_count = executionCount  }

        // evaluate
        let ex = 
            try
                // preprocess
                preprocessAndEval (content.code)
                None
            with
            | exn -> 
                handleException exn
                Some exn

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
                    user_variables = Dictionary<string,obj>();
                    user_expressions = Dictionary<string,obj>()
                }

            sendMessage shellSocket msg "execute_reply" executeReply

            // send all the data
            if not <| content.silent then
                let lastExpression = GetLastExpression()
                match lastExpression with
                | Some(it) -> 
                        
                    let printer = Printers.findDisplayPrinter(it.ReflectionType)
                    let (_, callback) = printer
                    let callbackValue = callback(it.ReflectionValue)
                    sendDisplayData callbackValue.ContentType callbackValue.Data "pyout"

                | None -> ()

        // we are now idle
        sendStateIdle msg

    /// Handles a 'complete_request' message
    let completeRequest (msg : KernelMessage) (content : CompleteRequest) = 
        let decls, pos, filterString = GetDeclarations(content.line, 0, content.cursor_pos)
        let items = decls |> Array.map (fun x -> x.Value)
        let newContent = 
            {
                matched_text = filterString
                filter_start_index = pos
                matches = items
                status = "ok"
            }

        sendMessage (shellSocket) (msg) ("complete_reply") (newContent)

    /// Handles a 'intellisense_request' message
    let intellisenseRequest (msg : KernelMessage) (content : IntellisenseRequest) = 

        // in our custom UI we put all cells in content.text and more information in content.block
        // the position is contains the selected index and the relative character and line number
        let cells = JsonConvert.DeserializeObject<array<string>>(content.text)
        let codes = cells |> Seq.append [headerCode]
        let position = JsonConvert.DeserializeObject<BlockType>(content.block)

        // calculate absolute line number
        let lineOffset = 
            codes
            |> Seq.take (position.selectedIndex + 1)
            |> Seq.map (fun x -> x.Split('\n').Length)
            |> Seq.sum

        let realLineNumber = position.line + lineOffset + 1
        let codeString = String.Join("\n", codes)
        let (_, decls, tcr, filterStartIndex) = compiler.GetDeclarations(codeString, realLineNumber, position.ch)
        
        let matches = 
            decls
            |> Seq.map (fun x -> { glyph = x.Glyph; name = x.Name; documentation = x.Documentation; value = x.Value })
            |> Seq.toList

        let newContent = 
            {
                matched_text = ""
                filter_start_index = filterStartIndex
                matches = matches
                status = "ok"
            }
        
        // send back errors
        let errors = 
            [|
                yield! tcr.Check.Errors |> Seq.map CustomErrorInfo.From
                yield! tcr.Preprocess.Errors
            |]

        // create an array of tuples <cellNumber>, <line>
        let allLines = 
            [|
                for index, cell in cells |> Seq.mapi (fun i x -> i, x) do
                    for cellLineNumber, line in cell.Split('\n') |> Seq.mapi (fun i x -> i, x) do
                        yield index, cellLineNumber, line
            |]

        let newErrors = 
            [|
                let headerLines = headerCode.Split('\n')
                for e in errors do
                    let realLineNumber = 
                        let x = e.StartLine - headerLines.Length - 1
                        max x 0

                    let cellNumber, cellLineNumber, _ = allLines.[realLineNumber]
                    yield { e with CellNumber = cellNumber; StartLine = cellLineNumber; EndLine = cellLineNumber; }
            |]
            |> Array.filter (fun x -> x.Subcategory <> "parse")
            
        sendDisplayData "errors" newErrors "display_data"
        sendMessage (shellSocket) (msg) ("complete_reply") (newContent)

    /// Handles a 'connect_request' message
    let connectRequest (msg : KernelMessage) (content : ConnectRequest) = 

        let reply =
            {
                hb_port = connectionInformation.hb_port;
                iopub_port = connectionInformation.iopub_port;
                shell_port = connectionInformation.shell_port;
                stdin_port = connectionInformation.stdin_port; 
            }

        logMessage "connectRequest()"
        sendMessage shellSocket msg "connect_reply" reply

    /// Handles a 'shutdown_request' message
    let shutdownRequest (msg : KernelMessage) (content : ShutdownRequest) =
        logMessage "shutdown request"
        // TODO: actually shutdown        
        let reply = { restart = true; }

        sendMessage shellSocket msg "shutdown_reply" reply;
        System.Environment.Exit(0)

    /// Handles a 'history_request' message
    let historyRequest (msg : KernelMessage) (content : HistoryRequest) =

        // TODO: actually handle this
        sendMessage shellSocket msg "history_reply" { history = [] }

    /// Handles a 'object_info_request' message
    let objectInfoRequest (msg : KernelMessage) (content : ObjectInfoRequest) =
        // TODO: actually handle this
        ()

    let inspectRequest (msg : KernelMessage) (content : InspectRequest) =
        // TODO: actually handle this
        let reply = { status = "ok"; found = false; data = Dictionary<string,obj>(); metadata = Dictionary<string,obj>() }
        sendMessage shellSocket msg "inspect_reply" reply
        ()

    /// Loops forever receiving messages from the client and processing them
    let doShell() =

        try
            preprocessAndEval headerCode
        with
        | exn -> handleException exn

        logMessage (sbErr.ToString())
        logMessage (sbOut.ToString())

        while true do
            let msg = recvMessage (shellSocket)

            try
                match msg.Content with
                | KernelRequest(r)       -> kernelInfoRequest msg r
                | ExecuteRequest(r)      -> executeRequest msg r
                | CompleteRequest(r)     -> completeRequest msg r
                | IntellisenseRequest(r) -> intellisenseRequest msg r
                | ConnectRequest(r)      -> connectRequest msg r
                | ShutdownRequest(r)     -> shutdownRequest msg r
                | HistoryRequest(r)      -> historyRequest msg r
                | ObjectInfoRequest(r)   -> objectInfoRequest msg r
                | InspectRequest(r)      -> inspectRequest msg r
                | _                      -> logMessage (String.Format("Unknown content type on shell. msg_type is `{0}`", msg.Header.msg_type))
            with 
            | ex -> handleException ex
   
    let doControl() =
        while true do
            let msg = recvMessage (controlSocket)
            try
                match msg.Content with
                | ShutdownRequest(r)     -> shutdownRequest msg r
                | _                      -> logMessage (String.Format("Unexpected content type on control. msg_type is `{0}`", msg.Header.msg_type))
            with 
            | ex -> handleException ex

    /// Loops repeating message from the client
    let doHeartbeat() =

        try
            while true do
                let hb = hbSocket.ReceiveMultipartBytes() in
                hbSocket.SendMultipartBytes hb
        with
        | ex -> handleException ex

    /// Clears the display
    member __.ClearDisplay () =
        if lastMessage.IsSome then
            sendMessage (ioSocket) (lastMessage.Value) ("clear_output") { wait = false; stderr = true; stdout = true; other = true; }

    /// Sends auto complete information to the client
    member __.AddPayload (text) =
        payload.Add( { html = ""; source = "page"; start_line_number = 1; text = text })

    /// Adds display data to the list of display data to send to the client
    member __.SendDisplayData (contentType, displayItem) =
        sendDisplayData contentType displayItem "display_data"

    /// Starts the kernel asynchronously
    member __.StartAsync() = 
        
        //Async.Start (async { doHeartbeat() } )
        Async.Start (async { doShell() } )
        Async.Start (async { doControl() } )