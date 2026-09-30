/// Colours for tree-sitter highlight captures, taken from VS Code's Light+ and Dark+ themes.
/// A capture without its own colour takes its parent's: string.special.symbol, then
/// string.special, then string. Captures with no colour at all keep the editor's text colour.
module AvaloniaPlayground.Editor.HighlightTheme

open System.Collections.Generic
open Avalonia.Media
open Avalonia.Media.Immutable
open Avalonia.Styling

// capture -> (Light+, Dark+)
let private colours =
    dict [
        "comment", ("#008000", "#6A9955")
        "keyword", ("#AF00DB", "#C586C0")
        "string", ("#A31515", "#CE9178")
        "string.escape", ("#EE0000", "#D7BA7D")
        "string.regex", ("#811F3F", "#D16969")
        "string.special.symbol", ("#0000FF", "#569CD6")
        "number", ("#098658", "#B5CEA8")
        "constant", ("#0000FF", "#569CD6")
        "function", ("#795E26", "#DCDCAA")
        "module", ("#267F99", "#4EC9B0")
        "variable", ("#001080", "#9CDCFE")
        "property", ("#001080", "#9CDCFE")
        "attribute", ("#0070C1", "#4FC1FF")
    ]

let rec private colourOf (capture: string) =
    match colours.TryGetValue capture with
    | true, pair -> Some pair
    | _ ->
        match capture.LastIndexOf '.' with
        | dot when dot > 0 -> colourOf capture[.. dot - 1]
        | _ -> None

// each capture is resolved once per variant, the first time it is drawn; UI thread only
let private resolved = Dictionary<struct (string * bool), IBrush option>()

let brushFor (capture: string) (variant: ThemeVariant) =
    let dark = variant = ThemeVariant.Dark

    match resolved.TryGetValue(struct (capture, dark)) with
    | true, brush -> brush
    | _ ->
        let brush =
            colourOf capture
            |> Option.map (fun (light, darkColour) ->
                ImmutableSolidColorBrush(Color.Parse(if dark then darkColour else light)) :> IBrush)

        resolved[struct (capture, dark)] <- brush
        brush
