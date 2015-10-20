namespace IfSharp.Kernel

open System
open System.Diagnostics
open System.IO
open System.Reflection
open System.Text
open System.Threading

open Newtonsoft.Json
open NetMQ

open Microsoft.FSharp.Reflection

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

    let mutable Kernel : Option<IfSharpKernel> = None

    /// Public API for addDisplayPrinter
    let AddDisplayPrinter = Printers.addDisplayPrinter

    /// Convenience method for adding an fsi printer
    let AddFsiPrinter = Microsoft.FSharp.Compiler.Interactive.Shell.Settings.fsi.AddPrinter

    /// Global clear display function
    let Clear () = Kernel.Value.ClearDisplay()

    /// Global display function
    let Display (value : obj) =

        if value <> null then
            let printer = Printers.findDisplayPrinter(value.GetType())
            let (_, callback) = printer
            let callbackValue = callback(value)
            Kernel.Value.SendDisplayData(callbackValue.ContentType, callbackValue.Data)

    /// Global help function
    let Help (value : obj) = 

        let text = StringBuilder()

        let rec getTypeText (t : Type) =
            let text = StringBuilder(Blue)
    
            if FSharpType.IsTuple(t) then
                let args = FSharpType.GetTupleElements(t)
                let str:array<string> = [| for a in args do yield getTypeText(a) |]
                text.Append(String.Join(" * ", str)) |> ignore

            else if t.IsGenericType then
                let args = t.GetGenericArguments()
                let str:array<string> = [| for a in args do yield getTypeText(a) |]
                text.Append(t.Name) 
                    .Append("<")
                    .Append(String.Join(" ", str))
                    .Append(">")
                    |> ignore
            else
                text.Append(t.Name) |> ignore

            text.Append(Reset).ToString()

        let getPropertyText (p : PropertyInfo) =
            let text = StringBuilder()
            text.Append(p.Name)
                .Append(" -> ")
                .Append(getTypeText(p.PropertyType))
                |> ignore

            text.ToString()

        let getParameterInfoText (p : ParameterInfo) =
            let sb = StringBuilder()
            if p.IsOptional then sb.Append("? ") |> ignore
            if p.IsOut then sb.Append("out ") |> ignore
            sb.Append(p.Name).Append(": ").Append(getTypeText(p.ParameterType)).Append(" ") |> ignore
            if p.HasDefaultValue then sb.Append("= ").Append(p.DefaultValue).Append(" ") |> ignore
            sb.ToString().Trim()

        let getMethodText (m : MethodInfo) =
            let sb = StringBuilder()
            sb.Append(m.Name).Append("(") |> ignore

            let pametersString = String.Join(", ", m.GetParameters() |> Seq.map(fun x -> getParameterInfoText(x)))
            sb.Append(pametersString) |> ignore

            sb.Append(") -> ").Append(getTypeText(m.ReturnType)) |> ignore
            sb.ToString()

        let props = 
            value.GetType().GetProperties()
            |> Seq.sortBy (fun x -> x.Name)
            |> Seq.toArray
        
        let meths =
            value.GetType().GetMethods()
            |> Seq.filter (fun x -> x.Name.StartsWith("get_") = false)
            |> Seq.filter (fun x -> x.Name.StartsWith("set_") = false)
            |> Seq.sortBy (fun x -> x.Name)
            |> Seq.toArray

        // type information
        text.Append(Blue)
            .Append("Type: ")
            .AppendLine(value.GetType().FullName)
            .Append(Reset) |> ignore

        // output properties
        text.AppendLine() |> ignore
        text.Append(Red)
            .AppendLine("Properties")
            .Append(Reset) |> ignore

        props |> Seq.iter (fun x -> text.AppendLine(getPropertyText(x)) |> ignore)

        // output methods
        text.AppendLine() |> ignore
        text.Append(Red)
            .AppendLine("Methods")
            .Append(Reset) |> ignore

        meths |> Seq.iter (fun x -> text.AppendLine(getMethodText(x)) |> ignore)

        // add to the payload
        Kernel.Value.AddPayload(text.ToString())

    /// Installs the ifsharp files if they do not exist, then starts ipython with the ifsharp profile
    let InstallAndStart(forceInstall) = 

        let thisExecutable = Assembly.GetEntryAssembly().Location
        let appData = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        let ipythonDir = Path.Combine(appData, ".ipython")
        let profileDir = Path.Combine(ipythonDir, "profile_ifsharp")
        let kernelDir = Path.Combine([| ipythonDir; "kernels" ; "ifsharp" |] )
        let staticDir = Path.Combine(profileDir, "static")
        let customDir = Path.Combine(staticDir, "custom")
            
        let createDir(str) =
            if Directory.Exists(str) = false then
                Directory.CreateDirectory(str) |> ignore

        createDir appData
        createDir ipythonDir
        createDir profileDir
        createDir staticDir
        createDir customDir
        createDir kernelDir

        let configFile = Path.Combine(profileDir, "ipython_config.py")
        let configqtFile = Path.Combine(profileDir, "ipython_qtconsole_config.py")
        let kernelFile = Path.Combine(kernelDir, "kernel.json")
        if forceInstall || (File.Exists(configFile) = false) then
            
            printfn "Config file does not exist, performing install..."

            // write the startup script
            let codeTemplate = IfSharpResources.ipython_config()
            let code = 
              match Environment.OSVersion.Platform with
                | PlatformID.Win32Windows -> codeTemplate.Replace("\"mono\",", "")
                | PlatformID.Win32NT -> codeTemplate.Replace("\"mono\",", "")
                | _ -> codeTemplate
            let code = code.Replace("%s", thisExecutable)
            printfn "Saving custom config file [%s]" configFile
            File.WriteAllText(configFile, code)

            let codeqt = IfSharpResources.ipython_qt_config()
            printfn "Saving custom qt config file [%s]" codeqt
            File.WriteAllText(configqtFile, codeqt)

            // write custom logo file
            let logoFile = Path.Combine(customDir, "ifsharp_logo.png")
            printfn "Saving custom logo [%s]" logoFile
            IfSharpResources.ifsharp_logo().Save(logoFile)

            // write custom css file
            let cssFile = Path.Combine(customDir, "custom.css")
            printfn "Saving custom css [%s]" cssFile
            File.WriteAllText(cssFile, IfSharpResources.custom_css())

            // write custom js file
            let jsFile = Path.Combine(customDir, "custom.js")
            printfn "Saving custom js [%s]" jsFile
            File.WriteAllText(jsFile, IfSharpResources.custom_js())

            // write fsharp js file
            let jsFile = Path.Combine(customDir, "fsharp.js")
            printfn "Saving fsharp js [%s]" jsFile
            File.WriteAllText(jsFile, IfSharpResources.fsharp_js())

            // write fsharp js file
            let jsFile = Path.Combine(customDir, "webintellisense.js")
            printfn "Saving webintellisense js [%s]" jsFile
            File.WriteAllText(jsFile, IfSharpResources.webintellisense_js())

            // write fsharp js file
            let jsFile = Path.Combine(customDir, "webintellisense-codemirror.js")
            printfn "Saving webintellisense-codemirror js [%s]" jsFile
            File.WriteAllText(jsFile, IfSharpResources.webintellisense_codemirror_js())

            // Make the Kernel info folder 
            let jsonTemplate = IfSharpResources.ifsharp_kernel_json()
            let code = 
              match Environment.OSVersion.Platform with
                | PlatformID.Win32Windows -> jsonTemplate.Replace("\"mono\",", "")
                | PlatformID.Win32NT -> jsonTemplate.Replace("\"mono\",", "")
                | _ -> jsonTemplate
            let code = code.Replace("%s", thisExecutable.Replace("\\","\/"))
            printfn "Saving custom kernel.json file [%s]" kernelFile
            File.WriteAllText(kernelFile, code)
            
            let logo64File = Path.Combine(kernelDir, "logo-64x64.png")
            printfn "Saving kernel icon [%s]" logo64File
            IfSharpResources.ifsharp_64logo().Save(logo64File)
            
            let logo32File = Path.Combine(kernelDir, "logo-32x32.png")
            printfn "Saving kernel icon [%s]" logo32File
            IfSharpResources.ifsharp_32logo().Save(logo32File)

        printfn "Starting ipython..."
        let p = new Process()
        p.StartInfo.FileName <- "ipython"
        p.StartInfo.Arguments <- "notebook --profile ifsharp"
        p.StartInfo.WorkingDirectory <- appData

        // tell the user something bad happened
        if p.Start() = false then printfn "Unable to start ipython, please install ipython first"

    /// First argument must be an ipython connection file, blocks forever
    let Start (args : array<string>) = 

        if args.Length = 0 then
        
            InstallAndStart(true)

        else

            // adds the default display printers
            Printers.addDefaultDisplayPrinters()


            // get connection information
            let fileName = args.[0]
            let json = File.ReadAllText(fileName)
            let connectionInformation = JsonConvert.DeserializeObject<ConnectionInformation>(json)

            // start the kernel
            Kernel <- Some (IfSharpKernel(connectionInformation))
            Kernel.Value.StartAsync()

            // block forever
            Thread.Sleep(Timeout.Infinite)
