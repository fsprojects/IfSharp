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

    /// Installs the ifsharp files if they do not exist
    let Install forceInstall = 

        let thisExecutable = Assembly.GetEntryAssembly().Location
        let kernelDir = Config.KernelDir
        let staticDir = Config.StaticDir
        let tempDir = Config.TempDir
        let customDir = Path.Combine(staticDir, "custom")
            
        let createDir(str) =
            if Directory.Exists(str) = false then
                Directory.CreateDirectory(str) |> ignore

        createDir kernelDir
        createDir staticDir
        createDir tempDir
        createDir customDir

        let allFiles = new System.Collections.Generic.List<string>()
        let addFile fn = allFiles.Add(fn); fn
        let configFile = Path.Combine(kernelDir, "ipython_config.py") |> addFile
        let configqtFile = Path.Combine(kernelDir, "ipython_qtconsole_config.py") |> addFile
        let kernelFile = Path.Combine(kernelDir, "kernel.json") |> addFile
        let logoFile = Path.Combine(customDir, "ifsharp_logo.png") |> addFile
        let kjsFile = Path.Combine(kernelDir, "kernel.js") |> addFile
        let fjsFile = Path.Combine(customDir, "fsharp.js") |> addFile
        let wjsFile = Path.Combine(customDir, "webintellisense.js") |> addFile
        let wcjsFile = Path.Combine(customDir, "webintellisense-codemirror.js") |> addFile
        let logo64File = Path.Combine(kernelDir, "logo-64x64.png") |> addFile
        let logo32File = Path.Combine(kernelDir, "logo-32x32.png") |> addFile
        let versionFile = Path.Combine(kernelDir, "version.txt") |> addFile
        let missingFiles = Seq.exists (fun fn -> File.Exists(fn) = false) allFiles
        
        let differentVersion = File.Exists(versionFile) && File.ReadAllText(versionFile) <> Config.Version

        if forceInstall then printfn "Force install required, performing install..."
        else if missingFiles then printfn "One or more files are missing, performing install..."
        else if differentVersion then printfn "Different version found, performing install..."

        if forceInstall || missingFiles || differentVersion then
            
            // write the version file
            File.WriteAllText(versionFile, Config.Version);

            // write the startup script
            let codeTemplate = IfSharpResources.ipython_config()
            let code = 
              match Environment.OSVersion.Platform with
                | PlatformID.Win32Windows | PlatformID.Win32NT -> codeTemplate.Replace("\"mono\",", "")
                | _ -> codeTemplate
            let code = code.Replace("%kexe", thisExecutable)
            let code = code.Replace("%kstatic", staticDir)
            printfn "Saving custom config file [%s]" configFile
            File.WriteAllText(configFile, code)

            let codeqt = IfSharpResources.ipython_qt_config()
            printfn "Saving custom qt config file [%s]" codeqt
            File.WriteAllText(configqtFile, codeqt)

            // write custom logo file
            printfn "Saving custom logo [%s]" logoFile
            IfSharpResources.ifsharp_logo().Save(logoFile)

            // write fsharp css file
            let cssFile = Path.Combine(customDir, "fsharp.css")
            printfn "Saving fsharp css [%s]" cssFile
            File.WriteAllText(cssFile, IfSharpResources.fsharp_css())

            // write kernel js file
            printfn "Saving kernel js [%s]" kjsFile
            File.WriteAllText(kjsFile, IfSharpResources.kernel_js())

            // write fsharp js file
            printfn "Saving fsharp js [%s]" fjsFile
            File.WriteAllText(fjsFile, IfSharpResources.fsharp_js())

            // write webintellisense js file
            printfn "Saving webintellisense js [%s]" wjsFile
            File.WriteAllText(wjsFile, IfSharpResources.webintellisense_js())

            // write webintellisense-codemirror js file
            printfn "Saving webintellisense-codemirror js [%s]" wcjsFile
            File.WriteAllText(wcjsFile, IfSharpResources.webintellisense_codemirror_js())

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
            
            printfn "Saving kernel icon [%s]" logo64File
            IfSharpResources.ifsharp_64logo().Save(logo64File)
            
            printfn "Saving kernel icon [%s]" logo32File
            IfSharpResources.ifsharp_32logo().Save(logo32File)

            printfn "Installing dependencies via Paket"
            let dependencies = Paket.Dependencies.Locate(System.IO.Path.GetDirectoryName(thisExecutable))
            dependencies.Install(false)

    /// Starts jupyter in the user's home directory
    let StartJupyter () =

        let userDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        printfn "Starting ipython..."
        let p = new Process()
        p.StartInfo.FileName <- "jupyter"
        p.StartInfo.Arguments <- "notebook"
        p.StartInfo.WorkingDirectory <- userDir

        // tell the user something bad happened
        if p.Start() = false then printfn "Unable to start jupyter, please install jupyter first"

    /// First argument must be an ipython connection file, blocks forever
    let Start (args : array<string>) = 

        if args.Length = 0 then
            Install true
            StartJupyter()

        else if args.[0] = "--install" then
            Install true

        else
            // Verify kernel installation status
            Install false

            // Clear the temporary folder
            try
              if Directory.Exists(Config.TempDir) then Directory.Delete(Config.TempDir, true)
            with exc -> Console.Out.Write(exc.ToString())

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
