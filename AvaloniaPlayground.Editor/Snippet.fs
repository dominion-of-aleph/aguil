namespace AvaloniaPlayground.Editor

open AvaloniaEdit.Document
open TreeSitter

/// A capture on one line, in chars from the line's start.
type Span = { Start: int; End: int; Capture: string }

/// Spans for each line of a text, in paint order. Version is echoed back from the request.
type Styling = { Version: ITextSourceVersion; Lines: Span list array }

type SnippetMsg =
    | Style of version: ITextSourceVersion * text: string
    | Stop

module Styler =
    /// The spans of each line, in tree-sitter's capture order: by start, then by pattern.
    let style (parser: Parser) (highlights: Query) (text: string) =
        use tree = parser.Parse text
        use cursor = highlights.Execute tree.RootNode
        let rows = text.Split '\n'
        let lines = Array.init rows.Length (fun _ -> ResizeArray<Span>())
        // Order matters here, last one wins, the elixir styler does General -> specific
        // So it ends up working out here.
        for capture in cursor.Captures do
            let first, last = capture.Node.StartPosition, capture.Node.EndPosition

            for row in first.Row .. last.Row do
                lines[row].Add {
                    Start = if row = first.Row then first.Column else 0
                    End = if row = last.Row then last.Column else rows[row].Length
                    Capture = capture.Name
                }

        lines |> Array.map List.ofSeq

/// An agent that styles a text: gets the whole text on each change and publishes Styled.
/// Handles one text at a time and holds no thread while idle.
type Snippet(grammar: string) =
    let styled = Event<Styling>()

    let rec loop (inbox: MailboxProcessor<SnippetMsg>) parser highlights =
        async {
            match! inbox.Receive() with
            // We redraw whole, so skip
            | Style _ when inbox.CurrentQueueLength > 0 -> return! loop inbox parser highlights
            | Style(version, text) ->
                styled.Trigger { Version = version; Lines = Styler.style parser highlights text }
                return! loop inbox parser highlights
            | Stop -> parser.Dispose()
        }

    let agent =
        new MailboxProcessor<SnippetMsg>(fun inbox ->
            // await rather than block, so agents started during the load hold no thread
            async {
                let! loaded = Grammars.grammarFor grammar |> Async.AwaitTask
                return! loop inbox (new Parser(loaded.Language)) loaded.Highlights
            })

    do
        // an exception ends the agent and only shows up here; subscribe before Start
        agent.Error.Add(fun error -> eprintfn $"Snippet ({grammar}) stopped: {error}")
        agent.Start()

    member _.Post message = agent.Post message

    /// Raised on the agent's thread.
    [<CLIEvent>]
    member _.Styled = styled.Publish
