namespace IfSharp.Widgets

open System
open System.Collections.Generic
open IfSharp.Kernel
open Newtonsoft.Json

module Internals = 
    
    /// Creates a function that caches the results when called
    let internal memoize f =
        let cache = Dictionary<_, _>()
        fun x ->
            if cache.ContainsKey(x) then
                cache.[x]
            else
                let res = f x
                cache.[x] <- res
                res

    /// A memoized function that takes in a Type and looks for all properties that 
    /// return an IWidget that is decorated by an JsonConverterAttribute that uses
    /// the WidgetSerializer
    let internal lookupSerializationProperties<'Serializer> = memoize (fun (t: Type) -> 
        t.GetProperties()
        |> Array.choose (fun prop -> 
            let isIWidget = typeof<IWidget>.IsAssignableFrom(prop.PropertyType)
            match isIWidget with
            | true ->
                match prop.GetCustomAttributes(typeof<JsonConverterAttribute>, false) with
                | [| jc |] -> 
                    let jc = jc :?> JsonConverterAttribute
                    match jc.ConverterType = typeof<'Serializer> with
                    | true -> Some prop
                    | _ -> None
                | _ -> None
            | _ -> None
        )
    )

open Internals 

/// A serializer that only supports writing instances of IWidget such that the notebook
/// can link the values together in the UI
type WidgetSerializer() =
    inherit JsonConverter()

    override __.WriteJson(writer, value, _serializer) = 
        let isIWidget = typeof<IWidget>.IsAssignableFrom(value.GetType())
        let isIWidgetArray = typeof<IWidget[]>.IsAssignableFrom(value.GetType())
        if isIWidget then
            let w = value :?> IWidget
            writer.WriteValue(w.Key |> string |> sprintf "IPY_MODEL_%s")
        elif isIWidgetArray then
            let w = value :?> IWidget[]
            writer.WriteStartArray()
            w |> Array.iter (fun w -> writer.WriteValue(w.Key |> string |> sprintf "IPY_MODEL_%s"))
            writer.WriteEndArray()
        else
            value.GetType().FullName |> failwithf "Unsupported type: %s. Expected: IWidget"

    override __.ReadJson(_reader, _objectType, _existingValue, _serializer) = raise(NotSupportedException())
    override __.CanConvert(_objectType) = false

type ButtonStyle =
    | Primary
    | Success
    | Info
    | Warning
    | Danger
    | Custom of string
    | NotSet
    static member All() =
        [|
            Primary
            Success
            Info
            Warning
            Danger
            NotSet
        |]

type ButtonStyleSerializer() = 
    inherit JsonConverter()

    override __.WriteJson(writer, value, _serializer) = 
        match value with
        | :? ButtonStyle as w -> 
            let styleString = 
                match w with
                | Primary    -> "primary"
                | Success    -> "success"
                | Info       -> "info"
                | Warning    -> "warning"
                | Danger     -> "danger"
                | NotSet     -> ""
                | Custom str -> str
            writer.WriteValue(styleString)
        | _ -> 
            value.GetType().FullName |> failwithf "Unsupported type: %s. Expected: ButtonStyle"

    override __.ReadJson(_reader, _objectType, existingValue, _serializer) = 
        match (existingValue |> string).ToLowerInvariant() with
        | "primary" -> Primary
        | "success" -> Success
        | "info"    -> Info
        | "warning" -> Warning
        | "danger"  -> Danger
        | ""        -> NotSet
        | other     -> Custom other
        |> box

    override __.CanConvert(_objectType) = false

type Widget(modelName: string, viewName: string, ?modelModule, ?modelModuleVersion, ?viewModule, ?viewModuleVersion) as this =

    let domClasses = ResizeArray<_>()
    let key = WidgetManager.Register(this)

    member val comm_id = key
    member val _dom_classes          = ResizeArray<_>()                                   with get, set
    member val _model_module         = defaultArg modelModule "@jupyter-widgets/controls" with get, set
    member val _model_module_version = defaultArg modelModuleVersion "1.4.0"              with get, set
    member val _model_name           = modelName                                          with get, set
    member val _view_count           = null                                               with get, set
    member val _view_module          = defaultArg viewModule "@jupyter-widgets/base"      with get, set
    member val _view_module_version  = defaultArg viewModuleVersion "1.1.0"               with get, set
    member val _view_name            = viewName                                           with get, set

    member __.AddClass(className) = 
        match domClasses.Contains className with
        | true -> ()
        | false -> domClasses.Add className

    member __.RemoveClass(className) = domClasses.Remove className

    interface IWidget with

        member __.Key = key

        member this.GetParents() =
            this.GetType()
            |> lookupSerializationProperties<WidgetSerializer>
            |> Seq.map (fun prop -> prop.GetValue this)
            |> Seq.cast<IWidget>
            |> Seq.toArray

/// The WidgetManager contains an in-memory dictionary of all instances of Widget that
/// have been creates in order to keep track of UI element in the notebook
and WidgetManager() =
    
    /// All registered widgets
    static member RegisteredWidgets = Dictionary<Guid, Widget>()
    
    /// Adds a widget to the registration (and provides the registration id back)
    static member Register widget = 
        let key = Guid.NewGuid()
        WidgetManager.RegisteredWidgets.Add(key, widget)
        key

    /// Removes a widget from the registration
    static member DeRegister key = 
        WidgetManager.RegisteredWidgets.Remove key

/// Layout specification
/// Defines a layout that can be expressed using CSS.  Supports a subset of
/// https://developer.mozilla.org/en-US/docs/Web/CSS/Reference
/// When a property is also accessible via a shorthand property, we only
/// expose the shorthand.
/// For example:
/// - ``flex-grow``, ``flex-shrink`` and ``flex-basis`` are bound to ``flex``.
/// - ``flex-wrap`` and ``flex-direction`` are bound to ``flex-flow``.
/// - ``margin-[top/bottom/left/right]`` values are bound to ``margin``, etc.
type Layout() =
    inherit Widget
        (
            modelName = "LayoutModel",
            viewName = "LayoutView",
            modelModule = "@jupyter-widgets/base",
            modelModuleVersion = "1.1.0",
            viewModule = "@jupyter-widgets/base",
            viewModuleVersion = "1.1.0"
        )

    /// The align-content CSS attribute.
    member val align_content         : string = null with get, set// CaselessStrEnum(['flex-start', 'flex-end', 'center', 'space-between', 'space-around', 'space-evenly', 'stretch'] + CSS_PROPERTIES, allow_none=True, help="The align-content CSS attribute.").tag(sync=True)

    /// The align-items CSS attribute.
    member val align_items           : string = null with get, set// CaselessStrEnum(['flex-start', 'flex-end', 'center','baseline', 'stretch'] + CSS_PROPERTIES, allow_none=True, help="The align-items CSS attribute.").tag(sync=True)

    /// The align-self CSS attribute.
    member val align_self            : string = null with get, set// CaselessStrEnum(['auto', 'flex-start', 'flex-end','center', 'baseline', 'stretch'] + CSS_PROPERTIES, allow_none=True, help="The align-self CSS attribute.").tag(sync=True)
    member val bottom                : string = null with get, set// Unicode(None, allow_none=True, help="The bottom CSS attribute.").tag(sync=True)
    member val border                : string = null with get, set// Unicode(None, allow_none=True, help="The border CSS attribute.").tag(sync=True)
    member val display               : string = null with get, set// Unicode(None, allow_none=True, help="The display CSS attribute.").tag(sync=True)
    member val flex                  : string = null with get, set// Unicode(None, allow_none=True, help="The flex CSS attribute.").tag(sync=True)
    member val flex_flow             : string = null with get, set// Unicode(None, allow_none=True, help="The flex-flow CSS attribute.").tag(sync=True)
    member val height                : string = null with get, set// Unicode(None, allow_none=True, help="The height CSS attribute.").tag(sync=True)
    member val justify_content       : string = null with get, set// CaselessStrEnum(['flex-start', 'flex-end', 'center','space-between', 'space-around'] + CSS_PROPERTIES, allow_none=True, help="The justify-content CSS attribute.").tag(sync=True)
    member val left                  : string = null with get, set// Unicode(None, allow_none=True, help="The left CSS attribute.").tag(sync=True)
    member val margin                : string = null with get, set// Unicode(None, allow_none=True, help="The margin CSS attribute.").tag(sync=True)
    member val max_height            : string = null with get, set// Unicode(None, allow_none=True, help="The max-height CSS attribute.").tag(sync=True)
    member val max_width             : string = null with get, set// Unicode(None, allow_none=True, help="The max-width CSS attribute.").tag(sync=True)
    member val min_height            : string = null with get, set// Unicode(None, allow_none=True, help="The min-height CSS attribute.").tag(sync=True)
    member val min_width             : string = null with get, set// Unicode(None, allow_none=True, help="The min-width CSS attribute.").tag(sync=True)
    member val overflow              : string = null with get, set// CaselessStrEnum(['visible', 'hidden', 'scroll', 'auto'] + CSS_PROPERTIES, allow_none=True, help="The overflow CSS attribute.").tag(sync=True)
    member val overflow_x            : string = null with get, set// CaselessStrEnum(['visible', 'hidden', 'scroll', 'auto'] + CSS_PROPERTIES, allow_none=True, help="The overflow-x CSS attribute.").tag(sync=True)
    member val overflow_y            : string = null with get, set// CaselessStrEnum(['visible', 'hidden', 'scroll', 'auto'] + CSS_PROPERTIES, allow_none=True, help="The overflow-y CSS attribute.").tag(sync=True)
    member val order                 : string = null with get, set// Unicode(None, allow_none=True, help="The order CSS attribute.").tag(sync=True)
    member val padding               : string = null with get, set// Unicode(None, allow_none=True, help="The padding CSS attribute.").tag(sync=True)
    member val right                 : string = null with get, set// Unicode(None, allow_none=True, help="The right CSS attribute.").tag(sync=True)
    member val top                   : string = null with get, set// Unicode(None, allow_none=True, help="The top CSS attribute.").tag(sync=True)
    member val visibility            : string = null with get, set// CaselessStrEnum(['visible', 'hidden']+CSS_PROPERTIES, allow_none=True, help="The visibility CSS attribute.").tag(sync=True)
    member val width                 : string = null with get, set// Unicode(None, allow_none=True, help="The width CSS attribute.").tag(sync=True)
                                                     
    member val grid_auto_columns     : string = null with get, set// Unicode(None, allow_none=True, help="The grid-auto-columns CSS attribute.").tag(sync=True)
    member val grid_auto_flow        : string = null with get, set// CaselessStrEnum(['column','row','row dense','column dense']+ CSS_PROPERTIES, allow_none=True, help="The grid-auto-flow CSS attribute.").tag(sync=True)
    member val grid_auto_rows        : string = null with get, set// Unicode(None, allow_none=True, help="The grid-auto-rows CSS attribute.").tag(sync=True)
    member val grid_gap              : string = null with get, set// Unicode(None, allow_none=True, help="The grid-gap CSS attribute.").tag(sync=True)
    member val grid_template_rows    : string = null with get, set// Unicode(None, allow_none=True, help="The grid-template-rows CSS attribute.").tag(sync=True)
    member val grid_template_columns : string = null with get, set// Unicode(None, allow_none=True, help="The grid-template-columns CSS attribute.").tag(sync=True)
    member val grid_template_areas   : string = null with get, set// Unicode(None, allow_none=True, help="The grid-template-areas CSS attribute.").tag(sync=True)
    member val grid_row              : string = null with get, set// Unicode(None, allow_none=True, help="The grid-row CSS attribute.").tag(sync=True)
    member val grid_column           : string = null with get, set// Unicode(None, allow_none=True, help="The grid-column CSS attribute.").tag(sync=True)
    member val grid_area             : string = null with get, set// Unicode(None, allow_none=True, help="The grid-area CSS attribute.").tag(sync=True)

/// Description style widget.
type DescriptionStyle() =
    inherit Widget
        (
            modelName = "DescriptionStyleModel",
            viewName = "StyleView",
            modelModule = "@jupyter-widgets/controls",
            modelModuleVersion = "1.4.0"
        )

    member val description_width: string = null with get, set // Unicode(help="Width of the description to the side of the control.").tag(sync=True)

/// Widget that can be inserted into the DOM
type DOMWidget(modelName, viewName, ?modelModule, ?modelModuleVersion, ?viewModule, ?viewModuleVersion) =
    inherit Widget
        (
            modelName = modelName,
            viewName = viewName,
            modelModule = defaultArg modelModule "@jupyter-widgets/controls",
            modelModuleVersion = defaultArg modelModuleVersion "1.4.0",
            viewModule = defaultArg viewModule "@jupyter-widgets/controls",
            viewModuleVersion = defaultArg viewModuleVersion "1.4.0"
        )

    member val description         = "" with get, set
    member val description_tooltip = "" with get, set
    member val placeholder         = "" with get, set

    [<JsonConverter(typeof<WidgetSerializer>)>]
    member val layout              = Layout() with get, set
    [<JsonConverter(typeof<WidgetSerializer>)>]
    member val style               = DescriptionStyle() with get, set

type Html(?value) =
    inherit DOMWidget(modelName = "HTMLModel", viewName = "HTMLView")
    member val value = defaultArg value "" with get,set

type IntSlider() =
    inherit DOMWidget(modelName = "IntSliderModel", viewName = "IntSliderView")
    member val value             = 7            with get,set
    member val min               = 0            with get,set
    member val max               = 10           with get,set
    member val step              = 1            with get,set
    member val disabled          = false        with get,set
    member val continuous_update = false        with get,set
    member val orientation       = "horizontal" with get,set
    member val readout           = true         with get,set
    member val readout_format    = "d"          with get,set

/// Displays a boolean `value` in the form of a checkbox.
//    Parameters
//    ----------
//    value : {True,False}
//        value of the checkbox: True-checked, False-unchecked
//    description : str
//	    description displayed next to the checkbox
//    indent : {True,False}
//        indent the control to align with other controls with a description. The style.description_width attribute controls this width for consistence with other controls.
type Checkbox() =
    inherit DOMWidget(modelName = "CheckboxModel", viewName = "CheckboxView")
    member val value    = false with get,set // Bool(False, help="Bool value").tag(sync=True)
    member val disabled = false with get,set // Bool(False, help="Enable or disable user changes.").tag(sync=True)
    member val indent   = false with get,set // Bool(True, help="Indent the control to align with other controls with a description.").tag(sync=True)

/// Displays a boolean `value` in the form of a toggle button.
///     Parameters
///     ----------
///     value : {True,False}
///         value of the toggle button: True-pressed, False-unpressed
///     description : str
///       description displayed next to the button
///     tooltip: str
///         tooltip caption of the toggle button
///     icon: str
///         font-awesome icon name
type ToggleButton() =
    inherit DOMWidget(modelName = "ToggleButtonModel", viewName = "ToggleButtonView")
    
    member val value        = false  with get,set
    member val tooltip      = ""     with get,set
    member val icon         = ""     with get,set
    [<JsonConverter(typeof<ButtonStyleSerializer>)>]
    member val button_style = NotSet with get,set

/// Displays a boolean `value` in the form of a green check (True / valid)
//    or a red cross (False / invalid).
//    Parameters
//    ----------
//    value: {True,False}
//        value of the Valid widget
type Valid() =
    inherit DOMWidget(modelName = "ValidModel", viewName = "ValidView")
    member val value    = false with get,set // Bool(False, help="Bool value").tag(sync=True)
    member val disabled = false with get,set // Bool(False, help="Enable or disable user changes.").tag(sync=True)
    member val readout  = ""    with get,set // Unicode('Invalid', help="Message displayed when the value is False").tag(sync=True)
