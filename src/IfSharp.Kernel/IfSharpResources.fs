namespace IfSharp.Kernel

open System
open System.Text
open System.Reflection
open System.Resources
open System.IO

module IfSharpResources = 
    let private thisAssy = Assembly.GetExecutingAssembly()
    
    let resources () = 
        thisAssy.GetManifestResourceNames()
    let streamFor resource = 
        let resourceName = sprintf "IfSharp.Kernel.%s" resource
        let s = thisAssy.GetManifestResourceStream(resourceName)
        if isNull s 
        then failwithf "could not find stream for %s. Available names are:\n%s" resourceName (resources() |> String.concat "\n\t")
        else s
    
    let getString (stream: Stream) =
        use s = stream
        use reader = new StreamReader(s)
        reader.ReadToEnd()

    let ifsharp_logo() = streamFor "static.custom.ifsharp_logo.png" 
    let kernel_js() = streamFor "kernel.js"
    let fsharp_css() = streamFor "static.custom.fsharp.css"
    let webintellisense_js() = streamFor "static.custom.webintellisense.js"
    let webintellisense_codemirror_js() = streamFor "static.custom.webintellisense-codemirror.js"
    let ipython_config() = streamFor "ipython_config.py" |> getString
    let ipython_qt_config() = streamFor "ipython_qtconsole_config.py"
    let ifsharp_kernel_json() = streamFor "kernel.json" |> getString
    let ifsharp_64logo() = streamFor "logo-32x32.png"
    let ifsharp_32logo() = streamFor "logo-64x64.png"
