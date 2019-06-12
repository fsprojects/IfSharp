namespace IfSharp.Kernel

open System
open System.Collections.Generic
open System.IO
open System.Reflection
open System.Text
open System.Security.Cryptography


open Newtonsoft.Json
open NetMQ
open NetMQ.Sockets
open System.Threading.Tasks
open Microsoft.FSharp.Control
open System.Threading

/// A function that by it's side effect sends the received dict as a comm_message
type SendCommMessage = Dictionary<string,obj> -> unit

type CommOpenCallback = SendCommMessage -> CommOpen -> unit
type CommMessageCallback = SendCommMessage -> CommMessage -> unit
type CommCloseCallback = CommTearDown  -> unit

type CommId = string
type CommTargetName = string

/// The set of callbacks which define comm registration at the kernel side
type CommCallbacks = {
    /// called upon comm creation
    onOpen : CommOpenCallback
    /// called to handle every received message while the come is opened
    onMessage : CommMessageCallback
    /// called upon comm close
    onClose: CommCloseCallback
    }

type IfSharpKernel(connectionInformation : ConnectionInformation, runtime : Config.Runtime) = 
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
    let nuGetManager = NuGetManager(FileInfo(".").FullName)
    let mutable executionCount = 0
    let mutable lastMessage : Option<KernelMessage> = None    

    /// Registered comm definitions (can be activated from Frontend side by comm_open message containing registered comm_target name)
    let mutable registeredComms : Map<CommTargetName,CommCallbacks> = Map.empty;
    /// Comms that are in the open state
    let mutable activeComms : Map<CommId,CommTargetName> = Map.empty;

    /// Gets the header code to prepend to all items
    let headerCode =

        let includeTemplate2 = """// include directory, this will be replaced by the kernel
#I "{0}"

// load base dlls
#r "IfSharp.Kernel.dll"

// open the global functions and methods
open IfSharp.Kernel
open IfSharp.Kernel.Globals"""

        let includeTemplate =
            match runtime with
            
            | Config.NetFramework ->
                """#r "netstandard"
""" 
                + includeTemplate2
            | Config.NetCore ->
                //Should we be speculatively referencing netstandard? It's convenient but maybe people don't want it?
                """#r "netstandard"
                """ 
                + includeTemplate2


        let file = FileInfo(Assembly.GetEntryAssembly().Location)
        let dir = file.Directory.FullName
        //let includeFile = Path.Combine(dir, "Include.fsx")
        //let code = File.ReadAllText(includeFile)
        String.Format(includeTemplate, dir.Replace("\\", "\\\\"))

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
        //let metaDataDict     = deserializeDict (metadata) //We don't currently need metadata and it's changing between notebooks and labs
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
        // lock on socket prevents simultaneous sends to the same socket from different threads
        // e.g. when Async results are ready and they are sent to be displayed
        lock socket (fun () ->
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
            socket.SendMultipartMessage(msg))

        
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
                protocol_version = "5.1.0";
                implementation = "ifsharp";
                implementation_version = "5.1.0";
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
    let sendDisplayData (contentType) (displayItem) (messageType) (display_id) = 
        if lastMessage.IsSome then

            let d = Dictionary<string,obj>()
            d.Add(contentType, displayItem)

            let d2 = Dictionary<string,obj>()
            d2.Add("display_id",display_id)

            let reply:DisplayData = { data = d; metadata = Dictionary<string,obj>(); transient = d2 }
            sendMessage ioSocket lastMessage.Value messageType reply
    
    /// pyout renamed to execute_result in current version of protocol
    let sendExecutionResult message additionalRepresentations display_id = 
        if lastMessage.IsSome then            
            // A plain text representation should always be provided in the text/plain mime-type. (https://jupyter-client.readthedocs.io/en/latest/messaging.html)
            let d = Dictionary<string,obj>()
            d.Add("text/plain", message)            
            
            // Results can have multiple simultaneous formats depending on its configuration. (https://jupyter-client.readthedocs.io/en/latest/messaging.html)            
            let addPresentation presentation = 
                let mime,data = presentation
                d.Add(mime,data)
            List.iter addPresentation additionalRepresentations

            let d2 = Dictionary<string,obj>()
            d2.Add("display_id", display_id)

            let reply:ExecutionResult = { data = d; metadata = Dictionary<string,obj>(); transient = d2;  execution_count = executionCount; }
            sendMessage ioSocket lastMessage.Value "execute_result" reply    

    let nugetErrors (nugetLines:string[]) =
        if nugetLines.Length > 0 then 
            nugetLines
                |> (Seq.map nuGetManager.ParseNugetLine >> Seq.map (fun (name, version, pre) -> "\"" + name + "\""))
                |> (String.concat "; ")
                |> sprintf """Instead of #N please get NuGets by Paket (https://fsprojects.github.io/Paket/) e.g. %s%s#load "Paket.fsx"%sPaket.Package [%s]"""
                    Environment.NewLine
                    Environment.NewLine
                    Environment.NewLine
        else
            null

    /// Preprocesses the code and evaluates it
    let preprocessCode(code) = 

        logMessage code

        // preprocess
        let preprocessing = nuGetManager.Preprocess(code)
        let newCode = String.Join("\n", preprocessing.FilteredLines)

        if not (Seq.isEmpty preprocessing.HelpLines) then
            fsiEval.EvalInteraction("#help")
            let ifsharpHelp =
                """  IF# notebook directives:
    #fsioutput ["on"|"off"];;   Toggle output display on/off
    """
            let fsiHelp = sbOut.ToString()
            sendExecutionResult (ifsharpHelp + fsiHelp) [] (Guid.NewGuid().ToString())
            sbOut.Clear() |> ignore

        //This is a persistent toggle, just respect the last one
        if not (Seq.isEmpty preprocessing.FsiOutputLines) then
            let lastFsiOutput = Seq.last preprocessing.FsiOutputLines
            if lastFsiOutput.ToLower().Contains("on") then
                fsiout := true
            else if lastFsiOutput.ToLower().Contains("off") then
                fsiout := false
            else                
                sendExecutionResult (sprintf "Unrecognised fsioutput setting: %s" lastFsiOutput) [] (Guid.NewGuid().ToString())

        let nugetErrors = nugetErrors preprocessing.NuGetLines
            
        newCode, nugetErrors

    /// Sends the "value" to the frontend presented in a proper  way
    let produceOutput value isExecutionResult =
        match Printers.tryFindAsyncPrinter value with
        | Some(asyncPrinter) -> asyncPrinter.Print value isExecutionResult sendExecutionResult sendDisplayData
        | None ->
            // Regular immediate printing of returned object
            let display_id = Guid.NewGuid().ToString()
            let printer = Printers.findDisplayPrinter (value.GetType())
            let (_, callback) = printer
            let callbackValue = callback(value)

            if isExecutionResult then
                if callbackValue.ContentType = "text/plain" then                                    
                    sendExecutionResult callbackValue.Data [] display_id
                else
                    // printer returned non plain text while plain text is required by the protocol
                    // thus generating compulsory value
                    let plainText = sprintf "%A" value
                    // adding originally returned value as optional
                    sendExecutionResult plainText [callbackValue.ContentType,callbackValue.Data] display_id                    
            else
                    sendDisplayData callbackValue.ContentType callbackValue.Data "display_data" display_id                

    /// Handles an 'execute_request' message
    let executeRequest(msg : KernelMessage) (content : ExecuteRequest) = 
        
        // Send error local function
        let sendError err =
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
                logMessage err

        // clear some state from previous runs
        sbOut.Clear() |> ignore
        sbErr.Clear() |> ignore
        sbPrint.Clear() |> ignore
        payload.Clear()

        // only increment if we are not silent
        if not content.silent then executionCount <- executionCount + 1
        
        // send busy
        sendStateBusy msg
        sendMessage ioSocket msg "pyin" { code = content.code; execution_count = executionCount  }

        // preprocess
        let newCode, err = preprocessCode content.code

        if not (String.IsNullOrEmpty err) then
            sendError err

        // evaluate
        if not (String.IsNullOrEmpty newCode) then
            try 
                let value, errors = fsiEval.EvalInteractionNonThrowing newCode

                if errors |> Array.length > 0 then
                    let err = errors |> Seq.map (fun error -> error.Message) |> (String.concat Environment.NewLine)
                    sendError err

                match value with
                | Choice1Of2 _ ->
                    () //Success!

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
                    if not content.silent then
                        let lastExpression = GetLastExpression()
                        match lastExpression with
                        | ActualValue(fsiValue) -> 
                            if fsiValue.ReflectionType <> typeof<unit> then
                                produceOutput fsiValue.ReflectionValue true
                        | BackupString str ->
                            let display_id = Guid.NewGuid()
                            sendDisplayData "text/plain" str "display_data" (display_id.ToString())
                        | Empty -> ()


                | Choice2Of2 exn ->
                    //Usually this is redundant but if we haven't shown any errors, try showing this
                    if errors.Length = 0 then
                        let err = "Expression evaluation failed: " + exn.Message + Environment.NewLine + exn.CompleteStackTrace()
                        sendError err
            with exn ->
                let err = "Expression evaluation failed: " + exn.Message + Environment.NewLine + exn.CompleteStackTrace()
                sendError err

        if sbPrint.Length > 0 then
            let display_id = Guid.NewGuid()
            sendDisplayData "text/plain" (sbPrint.ToString()) "display_data" (display_id.ToString())

        // we are now idle
        sendStateIdle msg       

    /// Handles a 'complete_request' message
    let completeRequest (msg : KernelMessage) (content : CompleteRequest) =
        // Don't respond. Use "intellisense_request" instead.
        ()

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

        let (decls, tcr, filterStartIndex, filterString) = GetDeclarations runtime (codeString, realLineNumber, position.ch)
        
        let matches = 
            decls
            |> Seq.map (fun x -> { glyph = x.Glyph; name = x.Name; documentation = x.Documentation; value = x.Value })
            |> Seq.toList

        let newContent = 
            {
                matched_text = filterString
                filter_start_index = filterStartIndex
                matches = matches
                status = "ok"
            }

        // send back errors
        let errors = 
            [|
                yield! tcr.Errors |> Seq.map CustomErrorInfo.From
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
            
        sendDisplayData "errors" newErrors "display_data" (Guid.NewGuid().ToString())
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

    let sendCommData sourceEnvelope commId (data:Dictionary<string,obj>) =
        let message : CommMessage = {comm_id=commId; data = data}
        sendMessage ioSocket sourceEnvelope "comm_msg" message

    let commOpen (msg : KernelMessage) (content : CommOpen) =
        if String.IsNullOrEmpty(content.target_name) then
            // as defined in protocol
            let reply: CommTearDown = {comm_id = content.comm_id; data = Dictionary<string,obj>();}
            sendMessage ioSocket msg "comm_close" reply
        match Map.tryFind content.target_name registeredComms with
        |   Some callbacks ->
            // executing open callback
            let onOpen = callbacks.onOpen
            let sendOnjectWithComm = sendCommData msg content.comm_id 
            onOpen sendOnjectWithComm content
            // saving comm_id for created instance 
            activeComms <- Map.add content.comm_id content.target_name activeComms
            logMessage (sprintf "comm opened id=%s target_name=%s" content.comm_id content.target_name)
        |   None ->            
            logMessage (sprintf "received comOpen request for the unknown com target_name \"%s\". Please register comm with this target_name first." content.target_name)
            let reply: CommTearDown = {comm_id = content.comm_id; data = Dictionary<string,obj>();}
            sendMessage ioSocket msg "comm_close" reply
    
    let commMessage (msg : KernelMessage) (content : CommMessage) =
        match Map.tryFind content.comm_id activeComms with
        |   Some comm_target ->
            // finding corresponding callback
            let callbacks = Map.find comm_target registeredComms
            // and executing it
            let onMessage = callbacks.onMessage
            let sendOnjectWithComm = sendCommData msg content.comm_id 
            onMessage sendOnjectWithComm content
            logMessage (sprintf "comm message handled id=%s target_name=%s" content.comm_id comm_target)
        |   None -> logMessage (sprintf "Got comm message (comm_id=%s), but there is nor opened comms with such comm_id. Ignoring" content.comm_id)

    let commClose (msg : KernelMessage) (content : CommTearDown) =        
        match Map.tryFind content.comm_id activeComms with
        |   Some target_name ->
            // executing close callback
            let callbacks = Map.find target_name registeredComms
            callbacks.onClose content
            // removing comm from opened comms
            activeComms <- Map.remove content.comm_id activeComms
            logMessage (sprintf "comm closed id=%s target_name=%s" content.comm_id target_name)
        |   None -> logMessage (sprintf "Got comm close request (comm_id=%s), but there is nor opened comms with such comm_id" content.comm_id)
    
    let commInfoRequest (msg : KernelMessage) (content : CommInfoRequest) =
        // returning all open comms
        let pairToDict pair =
            let comm_id,target_name = pair
            let dict = new Dictionary<string,string>();
            dict.Add("target_name",target_name)
            comm_id,dict        
        let openedCommsDict  = Dictionary<string,Dictionary<string,string>>()
        activeComms |> Map.toSeq |> Seq.map pairToDict |> Seq.iter (fun entry -> let key,value = entry in openedCommsDict.Add(key,value))
        let reply = { comms = openedCommsDict}
        sendMessage shellSocket msg "comm_info_reply" reply
        logMessage (sprintf "Reporting %d opened comms" openedCommsDict.Count)


    /// Loops forever receiving messages from the client and processing them
    let doShell() =

        let _, errors = fsiEval.EvalInteractionNonThrowing headerCode

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
                | CommOpen(r)            -> commOpen msg r
                | CommMessage(r)         -> commMessage msg r
                | CommTearDown(r)        -> commClose msg r
                | CommInfoRequest(r)     -> commInfoRequest msg r
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
    
    /// Registers a comm with specified target_name and callbacks
    member __.RegisterComm(target_name, onOpen,onMessage,onClose) =
        if Map.containsKey target_name registeredComms then
            logMessage (sprintf "Warning! The comm with target_name \"%s\" is already registered. Overriding previous registration" target_name)
        let callbacks :CommCallbacks = 
            {
                onOpen = onOpen
                onMessage = onMessage
                onClose = onClose
            }
        registeredComms <- Map.add target_name callbacks registeredComms
    
    /// Removes comm registration by specified comm target_name
    member __.UnregisterComm(target_name) =
        registeredComms <- Map.remove target_name registeredComms
       

    /// Clears the display
    member __.ClearDisplay () =
        if lastMessage.IsSome then
            sendMessage (ioSocket) (lastMessage.Value) ("clear_output") { wait = false; stderr = true; stdout = true; other = true; }

    /// Sends auto complete information to the client
    member __.AddPayload (text) =
        payload.Add( { html = ""; source = "page"; start_line_number = 1; text = text })    
    
    /// Shows the value in a frontend
    member __.DisplayValue(value) = 
        produceOutput value false

    /// Sends plain text execution results as well as other optional representations
    /// Return display_id of generated cell
    member __.SendExecuteResult(text,additionalRepresentations) =
        let display_id = Guid.NewGuid().ToString()
        sendExecutionResult text additionalRepresentations display_id
        display_id

    /// Adds display data to the list of display data to send to the client
    /// Return display_id of generated cell
    member __.SendDisplayData (contentType, displayItem) =
        let display_id = Guid.NewGuid().ToString()
        sendDisplayData contentType displayItem "display_data" (display_id.ToString())
        display_id    

    /// Updates the display data for the particular display_id
    /// Return display_id of updated cell
    member __.UpdateDisplayData (contentType, displayItem,display_id) =        
        sendDisplayData contentType displayItem "update_display_data" display_id
        display_id    
    
    /// Starts the kernel asynchronously
    member __.StartAsync() = 
        
        //Async.Start (async { doHeartbeat() } )
        Async.Start (async { doShell() } )
        Async.Start (async { doControl() } )
