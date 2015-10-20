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
    let webintellisense_js() = resources.GetString("webintellisense")
    let webintellisense_codemirror_js() = resources.GetString("webintellisense-codemirror")
    let ipython_config() = getString("ipython_config")
    let ipython_qt_config() = getString("qtconsole_config")
    let ifsharp_kernel_json() = getString("kernel_json")
    let ifsharp_64logo() = resources.GetObject("logo64File") :?> System.Drawing.Bitmap
    let ifsharp_32logo() = resources.GetObject("logo32File") :?> System.Drawing.Bitmap
