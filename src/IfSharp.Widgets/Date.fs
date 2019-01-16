namespace IfSharp.Widgets

open System

type DatePicker() =
    inherit DOMWidget(modelName = "DatePickerModel", viewName = "DatePickerView")
    member val value    = DateTime.Now with get,set // Date(None, allow_none=True).tag(sync=True, **date_serialization)
    member val disabled = false        with get,set // Bool(False, help="Enable or disable user changes.").tag(sync=True)