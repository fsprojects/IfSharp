namespace IfSharp.Widgets

open IfSharp.Kernel
open Newtonsoft.Json

///  Displays multiple widgets in a group.
///   The widgets are laid out horizontally.
type Box(?modelName, ?viewName) =
    inherit DOMWidget(modelName = defaultArg modelName "BoxModel", viewName = defaultArg viewName "BoxView")
    [<JsonConverter(typeof<WidgetSerializer>)>]
    member val children : IWidget[] = [||]   with get,set
    [<JsonConverter(typeof<ButtonStyleSerializer>)>]
    member val box_style            = NotSet with get,set
    interface IWidgetCollection with
        member this.GetChildren() = this.children

/// Displays multiple widgets vertically using the flexible box model.
type VBox() =
    inherit Box(modelName = "VBoxModel", viewName = "VBoxView")

/// Displays multiple widgets horizontally using the flexible box model.
type HBox() =
    inherit Box(modelName = "HBoxModel", viewName = "HBoxView")

/// Displays multiple widgets horizontally using the flexible box model.
type GridBox() =
    inherit Box(modelName = "GridBoxModel", viewName = "GridBoxView")
