namespace AvaloniaPlayground.Editor

open System
open AvaloniaEdit.Document
open TreeSitter

/// A capture on one line, in chars from the line's start. End can run past the line's end.
type Span = { Start: int; End: int; Capture: string }

/// The spans of every line of a text, each line's in the order to paint them. Version is the
/// TextDocument version the text was sent with, echoed back untouched.
type Styling = { Version: ITextSourceVersion; Lines: Span list array }

type SnippetMsg =
    | Style of version: ITextSourceVersion * text: string
    | Stop

/// A text's parser, as an agent: it gets the whole text on every change and publishes Styled with
/// the spans of all its lines. It styles one text at a time, and while waiting holds no thread.
type Snippet(grammar: string) =
    let styled = Event<Styling>()

    // Sorted so that painting in order leaves the innermost capture, then the last matching
    // pattern, on top: the precedence the highlight query is written for.
    let style (parser: Parser) (highlights: Query) (text: string) =
        use tree = parser.Parse text
        use cursor = highlights.Execute tree.RootNode
        let lines = Array.init (text.AsSpan().Count '\n' + 1) (fun _ -> ResizeArray<Span>())

        let captures =
            [
                for capture in cursor.Captures -> capture.Node, capture.PatternIndex, capture.Name
            ]
            |> List.sortBy (fun (node, pattern, _) -> node.StartIndex, -node.EndIndex, pattern)

        for node, _, name in captures do
            let first, last = node.StartPosition, node.EndPosition

            for row in first.Row .. last.Row do
                lines[row].Add {
                    Start = if row = first.Row then first.Column else 0
                    End = if row = last.Row then last.Column else Int32.MaxValue
                    Capture = name
                }

        lines |> Array.map List.ofSeq

    let rec loop (inbox: MailboxProcessor<SnippetMsg>) parser highlights =
        async {
            match! inbox.Receive() with
            // a newer text is already queued, so this one's styling would only be dropped
            | Style _ when inbox.CurrentQueueLength > 0 -> return! loop inbox parser highlights
            | Style(version, text) ->
                styled.Trigger { Version = version; Lines = style parser highlights text }
                return! loop inbox parser highlights
            | Stop -> parser.Dispose()
        }

    let agent =
        new MailboxProcessor<SnippetMsg>(fun inbox ->
            // awaited, not blocked on: agents starting while the grammar loads hold no thread,
            // and their texts wait in the mailbox
            async {
                let! loaded = Grammars.grammarFor grammar |> Async.AwaitTask
                return! loop inbox (new Parser(loaded.Language)) loaded.Highlights
            })

    do
        // an exception ends the agent and is reported only here; subscribed before it starts
        agent.Error.Add(fun error -> eprintfn $"Snippet ({grammar}) stopped: {error}")
        agent.Start()

    member _.Post message = agent.Post message

    /// Raised on the agent's thread.
    [<CLIEvent>]
    member _.Styled = styled.Publish
