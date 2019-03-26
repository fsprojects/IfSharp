namespace IfSharp.Kernel

open System
open System.Text
open System.Reflection
open System.IO

module IfSharpResources = 
    let private executingAssembly = Assembly.GetExecutingAssembly()
    
    let resources () = 
        executingAssembly.GetManifestResourceNames()
    let streamFor resource = 
        let resourceName = sprintf "IfSharp.Kernel.%s" resource
        let s = executingAssembly.GetManifestResourceStream(resourceName)
        if isNull s 
        then failwithf "could not find stream for %s. Available names are:\n%s" resourceName (resources() |> String.concat "\n\t")
        else s
    
    let getString (stream: Stream) =
        use s = stream
        use reader = new StreamReader(s)
        reader.ReadToEnd()

    let kernel_js() = streamFor "kernel.js"
    let fsharp_css() = streamFor "static.custom.fsharp.css"
    let webintellisense_js() = streamFor "static.custom.webintellisense.js"
    let webintellisense_codemirror_js() = streamFor "static.custom.webintellisense-codemirror.js"
    let ifsharp_64logo() = streamFor "logo-32x32.png"
    let ifsharp_32logo() = streamFor "logo-64x64.png"