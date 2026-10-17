namespace Aguil.Editor

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

module AlStyler =
    let private nameChar c =
        c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9' || c = '_'

    let private digit c = c >= '0' && c <= '9'

    /// AL's lexical forms, using the same quote, comment, name and number boundaries as AL.Syntax.
    let style (text: string) =
        let rows = text.Split '\n'
        let mutable quote = None

        [|
            for row in rows do
                let spans = ResizeArray<Span>()
                let mutable i = 0

                let add first last capture =
                    if first < last then
                        spans.Add { Start = first; End = last; Capture = capture }

                let quoted first delimiter =
                    let escapes = ResizeArray<Span>()
                    let mutable doneQuote = false

                    while i < row.Length && not doneQuote do
                        if row[i] = '\\' && i + 1 < row.Length then
                            escapes.Add { Start = i; End = i + 2; Capture = "string.escape" }
                            i <- i + 2
                        elif row[i] = delimiter then
                            i <- i + 1
                            doneQuote <- true
                        else
                            i <- i + 1

                    add
                        first
                        i
                        (if delimiter = '"' then
                             "string"
                         else
                             "string.special.symbol")

                    spans.AddRange escapes
                    quote <- if doneQuote then None else Some delimiter

                while i < row.Length do
                    let first = i

                    match quote with
                    | Some delimiter -> quoted first delimiter
                    | None ->
                        match row[i] with
                        | '#' when i + 1 < row.Length && row[i + 1] = '{' ->
                            i <- i + 2
                            add first i "operator"
                        | '#' ->
                            i <- row.Length
                            add first i "comment"
                        | '"'
                        | '\'' as delimiter ->
                            i <- i + 1
                            quoted first delimiter
                        | c when digit c ->
                            i <- i + 1

                            while i < row.Length && (digit row[i] || row[i] = '_') do
                                i <- i + 1

                            if i + 1 < row.Length && row[i] = '.' && digit row[i + 1] then
                                i <- i + 2

                                while i < row.Length
                                      && (digit row[i] || row[i] = '_' || row[i] = 'e' || row[i] = 'E') do
                                    i <- i + 1

                            add first i "number"
                        | c when nameChar c ->
                            i <- i + 1

                            while i < row.Length && nameChar row[i] do
                                i <- i + 1

                            add
                                first
                                i
                                (if c >= 'A' && c <= 'Z' || c = '_' then
                                     "variable"
                                 else
                                     "constant")
                        | c when "=<>+-*/".Contains c ->
                            i <- i + 1

                            if
                                i < row.Length
                                && List.contains (string c + string row[i]) [
                                    ">>"
                                    "->"
                                    "=>"
                                    "=="
                                    "<="
                                    ">="
                                    "**"
                                ]
                            then
                                i <- i + 1

                            add first i "operator"
                        | _ -> i <- i + 1

                yield List.ofSeq spans
        |]

/// An agent that styles a text: gets the whole text on each change and publishes Styled.
/// Handles one text at a time and holds no thread while idle.
type Snippet(grammar: string) =
    let styled = Event<Styling>()

    let rec loop (inbox: MailboxProcessor<SnippetMsg>) style =
        async {
            match! inbox.Receive() with
            // We redraw whole, so skip
            | Style _ when inbox.CurrentQueueLength > 0 -> return! loop inbox style
            | Style(version, text) ->
                styled.Trigger { Version = version; Lines = style text }
                return! loop inbox style
            | Stop -> ()
        }

    let agent =
        new MailboxProcessor<SnippetMsg>(fun inbox ->
            async {
                if grammar = "al" then
                    return! loop inbox AlStyler.style
                else
                    // await rather than block, so agents started during the load hold no thread
                    let! loaded = Grammars.grammarFor grammar |> Async.AwaitTask
                    use parser = new Parser(loaded.Language)
                    return! loop inbox (Styler.style parser loaded.Highlights)
            })

    do
        // an exception ends the agent and only shows up here; subscribe before Start
        agent.Error.Add(fun error -> eprintfn $"Snippet ({grammar}) stopped: {error}")
        agent.Start()

    member _.Post message = agent.Post message

    /// Raised on the agent's thread.
    [<CLIEvent>]
    member _.Styled = styled.Publish
