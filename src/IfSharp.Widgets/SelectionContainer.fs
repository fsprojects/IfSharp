namespace IfSharp.Widgets

open System.Collections.Generic

/// Base class used to display multiple child widgets.
type SelectionContainer(modelName, viewName, ?children) as this =
    inherit Box(modelName, viewName)

    let initialChildren, initialTitles = 
        let c = defaultArg children Seq.empty
        let titles = c |> Seq.map fst |> Seq.mapi (fun i x -> i, x) |> dict
        let children = c |> Seq.map snd |> Seq.toArray
        children, Dictionary<int, string>(titles)

    do this.children <- initialChildren

    member val _titles        = initialTitles
    member val selected_index = 0 with get,set

    member this.SetTitle(index, title) = this._titles.[index] <- title
    member this.GetTitle(index) = this._titles.[index]

/// Displays children each on a separate accordion page.
type Accordion(?sections) =
    inherit SelectionContainer("AccordionModel", "AccordionView", defaultArg sections Seq.empty)

/// Displays children each on a separate accordion tab.
type Tab(?tabs) =
    inherit SelectionContainer("TabModel", "TabView", defaultArg tabs Seq.empty)
