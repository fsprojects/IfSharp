namespace IfSharp.Widgets

open IfSharp.Kernel
open Newtonsoft.Json

type ColorPicker() =
    inherit DOMWidget(modelName = "ColorPickerModel", viewName = "ColorPickerView")
    member val value    = ""    with get,set // Color('black', help="The color value.").tag(sync=True)
    member val concise  = false with get,set // Bool(help="Display short version with just a color selector.").tag(sync=True)
    member val disabled = false with get,set // Bool(False, help="Enable or disable user changes.").tag(sync=True)
