namespace Aguil.Editor

open System
open System.Collections.Generic
open Avalonia.Threading
open AvaloniaEdit.Document
open AvaloniaEdit.Rendering

/// Colors a TextView: sends its text to a Snippet on each change and paints lines from the latest styling.
type SnippetColorizer(view: TextView, grammar: string) as this =
    inherit DocumentColorizingTransformer()

    let document = view.Document
    let snippet = Snippet(grammar)

    // keyed by line
    let mutable spans: IDictionary<DocumentLine, Span list> = dict []

    let send () = snippet.Post(Style(document.Version, document.Text))

    let changed = EventHandler(fun _ _ -> send ())

    let subscription =
        snippet.Styled.Subscribe(fun styling ->
            Dispatcher.UIThread.Post(fun () ->
                // a stale reply; the one for the current text is coming
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
            // later spans paint over earlier ones; clamp, since an edited line can be shorter
            for span in lineSpans do
                let from = line.Offset + min span.Start line.Length
                let until = line.Offset + min span.End line.Length

                match HighlightTheme.brushFor span.Capture view.ActualThemeVariant with
                | Some brush when from < until ->
                    let ele (element: VisualLineElement) =
                        element.TextRunProperties.SetForegroundBrush brush

                    this.ChangeLinePart(from, until, ele)
                | _ -> ()
        | _ -> ()

    interface IDisposable with
        member this.Dispose() = this.Dispose()
