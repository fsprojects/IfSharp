namespace IfSharp.Kernel

open System
open System.Text
open System.Web
//open System.Drawing
//open System.Drawing.Imaging
//open System.IO

module Printers =

    let mutable internal displayPrinters : list<Type * (obj -> BinaryOutput)> = []

    /// Convenience method for encoding a string within HTML
    let internal htmlEncode(str) = HttpUtility.HtmlEncode(str)

    /// Adds a custom display printer for extensibility
    let addDisplayPrinter(printer : 'T -> BinaryOutput) =
        displayPrinters <- (typeof<'T>, (fun (x:obj) -> printer (unbox x))) :: displayPrinters

    /// Default display printer
    let defaultDisplayPrinter(x) =
        { ContentType = "text/plain"; Data = sprintf "%A" x }

    let rec possiblyAFuncAsString(p) =
        if FSharp.Reflection.FSharpType.IsFunction p then
             let fromType, toType = FSharp.Reflection.FSharpType.GetFunctionElements p
             sprintf "(%s -> %s)" (possiblyAFuncAsString fromType) (possiblyAFuncAsString toType)
        else
            p.Name

    let functionPrinter(func:obj) =
        let funcArguments = possiblyAFuncAsString (func.GetType())
        { ContentType = "text/plain"; Data = sprintf "%A : %s" func funcArguments }

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