#r "packages/FSharp.Control.AsyncSeq/lib/net45/FSharp.Control.AsyncSeq.dll"
#r "IfSharp.Kernel.dll"

open System
open FSharp.Control
open System.Reflection
open System.Threading.Tasks
open System.Threading
open System.Text
open FSharp.Control

/// Returns the AsyncSeq<SpecificT'> if the passed object is AsyncSeq<T'>, None otherwise
let getAsyncSeqType (t:Type) =
    if t.IsGenericType then
        let targetIface = typedefof<AsyncSeq<_>>
        let isAsyncSeqIface (t:Type) =
            if t.IsGenericType && (t.GetGenericTypeDefinition() = targetIface) then Some t else None                                        
        match t.GetInterfaces() |> Seq.tryPick isAsyncSeqIface with
        | Some iface ->
            Some iface
        | None -> None
    else
        None

/// Adapts the IAsyncEnumerator<T'> to IAsyncEnumerator<obj>
/// o is IAsyncEnumerator<T'> to adapt
/// moveNext is a MethodInfo of o.MoveNext()
/// dispose is o.Dispose()
type AsyncSeqGenEnumerator(o:obj, moveNext:MethodInfo,dispose:unit -> unit) =
    interface IAsyncEnumerator<obj> with
        member __.MoveNext(): Async<obj option> =
            async {
                let value = moveNext.Invoke(o,[||]) //returns Async<T' option>
                let t = value.GetType()
                // the actual "pooling" of the next value is done in a threadpool thread
                // as the thread is temporary blocked by "Task<argT option>.Result" call
                do! Async.SwitchToThreadPool()                                                           

                // We are dealing with Async<argT option>
                let argToption = t.GenericTypeArguments.[0]                

                let argT = argToption.GenericTypeArguments.[0]

                // Extracting Async<argT option>.StartAsTask with reflection
                let asyncT = typedefof<Async>
                let methodInfo = asyncT.GetMethod("StartAsTask")
                let methodInfo2 = methodInfo.MakeGenericMethod([|argToption|])

                // And then invoking it
                let noneTaskCreationOptions: TaskCreationOptions option = None
                let noneCancelationToken: CancellationToken option = None
                
                let resultTaskObj = methodInfo2.Invoke(null,[|value; noneTaskCreationOptions; noneCancelationToken|]) //Task<T' option> is returned                

                // Extracting Task<argT option>.Result property accessor
                let taskT = typedefof<Task<_>>
                let taskT2 = taskT.MakeGenericType([|argToption|])
                let resultExtractor = taskT2.GetProperty("Result")
                // And invoking it
                let extractedResult = resultExtractor.GetValue(resultTaskObj) //T' option     
                
                if extractedResult = null then
                    return None
                else
                    let resultType = extractedResult.GetType()

                    let optionT = typedefof<Option<_>>
                    let optionTyped = optionT.MakeGenericType([|argT|])

                    let isSomeMI = optionTyped.GetProperty("IsSome")                

                    let isSomeRes = isSomeMI.GetValue(null,[|extractedResult|]) :?> bool // bool is returned
                    if isSomeRes then
                        let valueExtractor = resultType.GetProperty("Value")
                        let value = valueExtractor.GetValue(extractedResult) // T' is returned
                        return Some value
                    else
                        return None                        
            }
        member __.Dispose() =
            dispose()

/// Adapts IAsyncEnumerable<T'> to IAsyncEnumerable<obj>
type AsyncSeqGenEnumerable(asyncSeqObj:obj) =
    // t is supposed to be IAsyncEnumerable<'t>
    interface IAsyncEnumerable<obj> with
        member __.GetEnumerator() =
            let t = asyncSeqObj.GetType()
            match getAsyncSeqType(t) with
            |   Some(iAsyncEnumGen) ->
                let enumeratorExtractorGen = iAsyncEnumGen.GetMethod("GetEnumerator")
                let enumerator = enumeratorExtractorGen.Invoke(asyncSeqObj,[||])

                let asyncEnumerator = enumerator.GetType()
                let iAsyncEnumerator = asyncEnumerator.GetInterface("IAsyncEnumerator`1")
                let moveNextInfo = iAsyncEnumerator.GetMethod("MoveNext")            

                let iDisposable = asyncEnumerator.GetInterface("IDisposable")
                let disposeInfo = iDisposable.GetMethod("Dispose")
                new AsyncSeqGenEnumerator(enumerator,moveNextInfo, fun () -> disposeInfo.Invoke(enumerator,[||]) |> ignore) :> IAsyncEnumerator<obj>
            |   None ->
                failwith "asyncSeqObj is supposed to be AsyncSeq<_>"

/// Prints any Async<T'> by computing async in separate thread. Prints resulting 'T using registered (synchronous) printers
type AsyncPrinter() =
    interface IfSharp.Kernel.IAsyncPrinter with
        member __.CanPrint value =
            let t = value.GetType()
            t.IsGenericType && t.GetGenericTypeDefinition() = typedefof<Async<_>>
        member __.Print value isExecutionResult sendExecutionResult sendDisplayData =
            let t = value.GetType()
            let display_id = Guid.NewGuid().ToString()
            
            let deferredOutput = async {
                // This async execution will block the thread when accessing ".Result" of the Task<T> (see below).                                        
                // I do such blocking here because attaching task continuation via reflection is too complicated, so simply block the dedicated thread                
                
                // Before switching to a dedicated thread the display placeholder is produced synchronously
                // This is done to keep the visual order of "produceOutput" call outputs (in case of several async computations are initiated) in the frontend
                let deferredMessage = "(Async is being calculated. Results will appear as they are ready)"                
                if isExecutionResult then
                    sendExecutionResult deferredMessage [] display_id
                else
                    sendDisplayData "text/plain" deferredMessage "display_data" display_id

                // the rest is done in dedicated thread
                do! Async.SwitchToNewThread()
                
                // We are dealing with Async<argT>
                let argT = t.GenericTypeArguments.[0]

                // Extracting Async<argT>.StartAsTask with reflection
                let asyncT = typedefof<Async>
                let methodInfo = asyncT.GetMethod("StartAsTask")
                let methodInfo2 = methodInfo.MakeGenericMethod([|argT|])

                // And then invoking it
                let noneTaskCreationOptions: TaskCreationOptions option = None
                let noneCancelationToken: CancellationToken option = None

                try
                    let resultTaskObj = methodInfo2.Invoke(null,[|value; noneTaskCreationOptions; noneCancelationToken|])

                    // Extracting Task<argT>.Result property accessor
                    let taskT = typedefof<Task<_>>
                    let taskT2 = taskT.MakeGenericType([|argT|])
                    let resultExtractor = taskT2.GetProperty("Result")
                    // And invoking it
                    let extractedResult = resultExtractor.GetValue(resultTaskObj)

                    // updating corresponding cell content by printing resulted argT value
                    let printer = IfSharp.Kernel.Printers.findDisplayPrinter (argT)
                    let (_, callback) = printer
                    let callbackValue = callback(extractedResult)                
                    sendDisplayData callbackValue.ContentType callbackValue.Data "update_display_data" display_id
                with
                    | exc -> 
                        sendDisplayData "text/plain" (sprintf "EXCEPTION OCCURRED:\r\n%A" exc) "update_display_data" display_id
            }            
            Async.StartImmediate deferredOutput
                
/// Prints any AsyncSeq<T'> by pooling elements from it one by one. Updates the output to reflect the most recently computed element.
type AsyncSeqPrinter() =
    interface IfSharp.Kernel.IAsyncPrinter with
        member __.CanPrint value =
            let t = value.GetType()
            match getAsyncSeqType t with
            |   Some _ -> true
            |   None -> false
        member __.Print value isExecutionResult sendExecutionResult sendDisplayData =                
            let display_id = Guid.NewGuid().ToString()
            
            let deferredOutput = async {
                
                let deferredMessage = "(AsyncSeq is being calculated. Results will appear as they are ready)"                
                if isExecutionResult then
                    sendExecutionResult deferredMessage [] display_id
                else
                    sendDisplayData "text/plain" deferredMessage "display_data" display_id                                
                
                let asyncSeqObj = AsyncSeqGenEnumerable value

                let printer obj1 =
                    let printer = IfSharp.Kernel.Printers.findDisplayPrinter (obj1.GetType())
                    let (_, callback) = printer
                    let callbackValue = callback(obj1)
                    
                    sendDisplayData callbackValue.ContentType callbackValue.Data "update_display_data" display_id
                try
                    do! AsyncSeq.iter printer asyncSeqObj
                with
                    | exc -> 
                        sendDisplayData "text/plain" (sprintf "EXCEPTION OCCURRED:\r\n%A" exc) "update_display_data" display_id
            }            
            Async.StartImmediate deferredOutput                


IfSharp.Kernel.Printers.addAsyncDisplayPrinter(AsyncPrinter())
IfSharp.Kernel.Printers.addAsyncDisplayPrinter(AsyncSeqPrinter())
