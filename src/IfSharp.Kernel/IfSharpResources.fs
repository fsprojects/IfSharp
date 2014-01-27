namespace IfSharp.Kernel

open System
open System.Text
open System.Reflection
open System.Resources

module IfSharpResources = 
    let resources = new ResourceManager("IfSharpResources", Assembly.GetExecutingAssembly())
    
    let getString(name) =
         let array = resources.GetObject(name) :?> array<byte>
         Encoding.UTF8.GetString(array)

    let ifsharp_logo() = resources.GetObject("ifsharp_logo") :?> System.Drawing.Bitmap
    let custom_css() = resources.GetString("custom_css")
    let custom_js() = resources.GetString("custom_js")
    let fsharp_js() = resources.GetString("fsharp_js")
    let codemirror_intellisense_js() = resources.GetString("codemirror-intellisense")
    let ipython_config() = getString("ipython_config")

