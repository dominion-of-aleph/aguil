namespace AvaloniaPlayground.Editor

open System
open System.Collections.Generic
open Avalonia.Threading
open AvaloniaEdit.Document
open AvaloniaEdit.Rendering

/// Colours a TextView's document: sends the text to a Snippet on every change, and paints each
/// line with the spans of the latest styling.
type SnippetColorizer(view: TextView, grammar: string) as this =
    inherit DocumentColorizingTransformer()

    let document = view.Document
    let snippet = Snippet(grammar)

    // keyed by line, so a line keeps its spans while an edit elsewhere waits for its styling
    let mutable spans: IDictionary<DocumentLine, Span list> = dict []

    let send () = snippet.Post(Style(document.Version, document.Text))

    let changed = EventHandler(fun _ _ -> send ())

    let subscription =
        snippet.Styled.Subscribe(fun styling ->
            Dispatcher.UIThread.Post(fun () ->
                // an older text's styling is dropped: the current text's is on its way
                if styling.Version.CompareAge document.Version = 0 then
                    spans <- dict (Seq.zip document.Lines styling.Lines)
                    view.Redraw()))

    do
        document.TextChanged.AddHandler changed
        view.LineTransformers.Add this
        send ()

    member _.Dispose() =
        document.TextChanged.RemoveHandler changed
        view.LineTransformers.Remove this |> ignore
        subscription.Dispose()
        snippet.Post Stop

    override this.ColorizeLine(line: DocumentLine) =
        match spans.TryGetValue line with
        | true, lineSpans ->
            // in order, each over the last; a line edited since it was styled can be shorter
            for span in lineSpans do
                let from = line.Offset + min span.Start line.Length
                let until = line.Offset + min span.End line.Length

                match HighlightTheme.brushFor span.Capture view.ActualThemeVariant with
                | Some brush when from < until ->
                    this.ChangeLinePart(
                        from,
                        until,
                        fun element -> element.TextRunProperties.SetForegroundBrush brush
                    )
                | _ -> ()
        | _ -> ()

    interface IDisposable with
        member this.Dispose() = this.Dispose()
