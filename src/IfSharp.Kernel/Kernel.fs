namespace IfSharp.Kernel

open System
open System.Collections.Generic
open System.Diagnostics
open System.IO
open System.Reflection
open System.Text
open System.Threading

open FSharp.Charting

open Newtonsoft.Json
open fszmq
open fszmq.Socket

type IfSharpKernel(connectionInformation : ConnectionInformation, ioSocket : Socket, shellSocket : Socket, hbSocket : Socket, controlSocket : Socket, stdinSocket : Socket) = 

    let data = new List<BinaryOutput>()
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
        
        File.AppendAllLines(fileName, messages)

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

    /// Constructs an 'envelope' from the specified socket
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

    /// Convenience method for creating a header
    let createHeader (messageType) (sourceEnvelope) =
        {
            msg_type = messageType;
            msg_id = Guid.NewGuid().ToString();
            session = sourceEnvelope.Header.session;
            username = sourceEnvelope.Header.username;
        }

    /// Convenience method for sending a message
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
                protocol_version = [| 4; 0 |]; 
                ipython_version = Some [| 1; 0; 0 |];
                language_version = [| 1; 0; 0 |];
                language = "fsharp";
            }

        sendMessage shellSocket msg "kernel_info_reply" content

    /// Sends display data information immediately
    let sendDisplayData (contentType) (displayItem) (messageType) =
        data.Add( { ContentType = contentType; Data = displayItem } )
        
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
    
    /// Handles an 'execute_request' message
    let executeRequest(msg : KernelMessage) (content : ExecuteRequest) = 
        
        // clear some state
        sbOut.Clear() |> ignore
        sbErr.Clear() |> ignore
        data.Clear()
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
                if data.Count = 0 then
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

        // TODO: actually shutdown        
        let reply = { restart = true; }

        sendMessage shellSocket msg "shutdown_reply" reply

    /// Handles a 'history_request' message
    let historyRequest (msg : KernelMessage) (content : HistoryRequest) =

        // TODO: actually handle this
        sendMessage shellSocket msg "history_reply" { history = [] }

    /// Handles a 'object_info_request' message
    let objectInfoRequest (msg : KernelMessage) (content : ObjectInfoRequest) =
        // TODO: actually handle this
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
                | _                      -> logMessage (String.Format("Unknown content type. msg_type is `{0}`", msg.Header.msg_type))
            with 
            | ex -> handleException ex
   
    /// Loops repeating message from the client
    let doHeartbeat() =

        try
            while true do
                let bytes = recv hbSocket
                let str = decode bytes
                send hbSocket bytes
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
        
        Async.Start (async { doHeartbeat() } )
        Async.Start (async { doShell() } )
