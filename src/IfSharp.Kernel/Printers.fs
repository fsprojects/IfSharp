namespace IfSharp.Kernel

open System
open System.Text
open System.Web

type SendExecutionResultType = string -> (string * obj) list -> string -> unit
type SendDisplayDataType = string -> obj -> string -> string -> unit

type IAsyncPrinter =
    interface
        /// Whether the printer is capable of printing the object
        abstract member CanPrint: obj -> bool
        /// Print the project in asynchronous manner (possible doing some async computations)
        /// what to print -> is the print is execution_result -> send results callback -> send dispaly callback
        abstract member Print: obj -> bool -> SendExecutionResultType -> SendDisplayDataType -> unit
    end

module Printers =

    let mutable internal displayPrinters : list<Type * (obj -> BinaryOutput)> = []
    let mutable internal asyncPrinters : list<IAsyncPrinter*int> = [] //(printer * its priority). Capable printer with higher priority is selected for printing

    /// Convenience method for encoding a string within HTML
    let internal htmlEncode(str) = HttpUtility.HtmlEncode(str)

    /// Adds a custom display printer for extensibility
    let addDisplayPrinter(printer : 'T -> BinaryOutput) =
        displayPrinters <- (typeof<'T>, (fun (x:obj) -> printer (unbox x))) :: displayPrinters
    
    /// Adds a custom async display printer for extensibility
    /// Priority affects the cases where several printers are capabale of printing the save value.
    /// The printer with higher priority is selected
    let addAsyncDisplayPrinter(printer:IAsyncPrinter, priority: int) =
        let printers = List.filter (fun entry -> let p,_ = entry in p <> printer) asyncPrinters //unregister if previously registered
        asyncPrinters <- (printer,priority) :: printers
        
    /// Removes all registered async diaplay printers
    let clearDisplayPrinters() =
        asyncPrinters <- List.empty

    /// Default display printer
    let defaultDisplayPrinter(x) =
        { ContentType = "text/plain"; Data = sprintf "%A" x }

    //https://technet.microsoft.com/en-us/ee353649(v=vs.85)
    let mapToFSharpAliases fullType name =
        //Attempt to use more standard F# types in signature
        match fullType with
        //| array<'T> -> ""
        | _ when fullType = typeof<System.Numerics.BigInteger> -> "bigint"
        | _ when fullType = typeof<System.Boolean> -> "bool"
        | _ when fullType = typeof<System.Byte> -> "byte"
        | _ when fullType = typeof<System.Char> -> "char"
        | _ when fullType = typeof<System.Decimal> -> "decimal"
        | _ when fullType = typeof<System.Double> -> "double"
        | _ when fullType = typeof<System.Exception> -> "exn"
        | _ when fullType = typeof<System.Single> -> "single"
        //| "Format<'Printer,'State,'Residue,'Result,'Tuple> Type of a formatting expression.
        //| "Format<'Printer,'State,'Residue,'Result> Type of a formatting expression.
        | _ when fullType = typeof<System.Int16> -> "int16"
        | _ when fullType = typeof<System.Int32> -> "int"
        | _ when fullType = typeof<System.Int64> -> "int64"
        | _ when fullType = typeof<System.IntPtr> -> "nativeint"
        | _ when fullType = typeof<System.Object> -> "obj"
        | _ when fullType = typeof<System.SByte> -> "sbyte"
        | _ when fullType = typeof<System.Single> -> "single"
        | _ when fullType = typeof<System.String> -> "string"
        | _ when fullType = typeof<System.UInt16> -> "uint16"
        | _ when fullType = typeof<System.UInt32> -> "uint32"
        | _ when fullType = typeof<System.UInt64> -> "uint64"
        | _ when fullType = typeof<System.Byte> -> "uint8"
        | _ when fullType = typeof<System.UIntPtr> -> "unativeint"
        //Unit
        | _ -> name

    let rec possiblyAFuncAsString(p) =
        if FSharp.Reflection.FSharpType.IsFunction p then
            let fromType, toType = FSharp.Reflection.FSharpType.GetFunctionElements p
            sprintf "(%s -> %s)" (possiblyAFuncAsString fromType) (possiblyAFuncAsString toType)
        else
            mapToFSharpAliases p p.Name

    let functionPrinter(func:obj) =
        let funcArguments = possiblyAFuncAsString (func.GetType())
        { ContentType = "text/plain"; Data = sprintf "%A : %s" func funcArguments }
    
    let tryFindAsyncPrinter(objToPrint:obj)=
        asyncPrinters
        |> Seq.filter (fun entry -> let (p:IAsyncPrinter),_ = entry in p.CanPrint objToPrint) //only capable
        |> Seq.sortByDescending (fun entry -> let _,priority = entry in priority) //sorted by priority desc
        |> Seq.map (fun entry -> let printer,_ = entry in printer) // filtering out priority data
        |> Seq.tryHead

    /// Finds a display printer based off of the type
    let findDisplayPrinter(findType) =
        // Get printers that were registered using `fsi.AddHtmlPrinter` and turn them 
        // into printers expected here (just contactenate all <script> tags with HTML)
        let extraPrinters = 
            Evaluation.extraPrinters
            |> Seq.map (fun (t, p) -> t, fun o -> 
                let extras, html = p o
                let extras = extras |> Seq.map snd |> String.concat ""
                { ContentType = "text/html"; Data = extras + html })

        let printers =
            Seq.append displayPrinters extraPrinters
            |> Seq.filter (fun (t, _) -> t.IsAssignableFrom(findType))
            |> Seq.toList

        if printers.Length > 0 then
            printers.Head
        elif FSharp.Reflection.FSharpType.IsFunction findType then
            (typeof<Type>, functionPrinter)
        else
            (typeof<obj>, defaultDisplayPrinter)

    /// Adds default display printers
    let addDefaultDisplayPrinters() =

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

        // add svg printer
        addDisplayPrinter(fun (x:SvgOutput) ->
           { ContentType = "image/svg+xml"; Data = x.Svg }
        )

        // add html printer
        addDisplayPrinter(fun (x:HtmlOutput) ->
            { ContentType = "text/html"; Data = x.Html }
        )

        // add latex printer
        addDisplayPrinter(fun (x:LatexOutput) ->
            { ContentType = "text/latex"; Data = x.Latex }
        )

        // add binaryoutput printer
        addDisplayPrinter(fun (x:BinaryOutput) ->
            x
        )