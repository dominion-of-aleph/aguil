namespace Aguil.Editor

open Xunit
open Xunit.Abstractions
open Aguil.Editor.Examples
open Aguil.Views
open Aguil.ViewModels
open Aguil.Core.AlValues
open Avalonia.Controls

type Facts(output: ITestOutputHelper) =
    let span start stop capture = { Start = start; End = stop; Capture = capture }

    /// Prints a value to the test output and returns it.
    let see value =
        output.WriteLine(sprintf "%A" value)
        value

    /// Prints a styled example to the test output and returns its lines.
    let look example =
        output.WriteLine(describe example)
        snd example

    [<Fact>]
    member _.QuotedValuesRoundTrip() =
        for value in quotedValues () do
            see (string value) |> ignore
            Assert.Equal(value, roundTrip value |> see)

    [<Fact>]
    member _.MapMethodViews() =
        let result = mapMethod () |> see
        let bindings = result.Bindings.Values |> List.map (fun b -> b.Symbol, b.Value)
        let views = viewsFor bindings |> see
        Assert.Equal(bindings.Length, views.Count)

    [<Fact>]
    member _.NamesDoNotChangeViews() =
        let names = [ "value"; "phlow"; "builder"; "map"; "maps"; "aguil0_maps" ]

        for value in [ (fun _ -> AlInteger 42I); partialMap ] do
            let views = viewsFor [ for name in names -> name, value name ] |> see
            Assert.NotEmpty(views["value"])

            for KeyValue(_, found) in views do
                Assert.Equal<Map<AlValue, AlValue> list>(views["value"], found)

    [<Fact>]
    member _.OpeningScrollsOnlyItsColumn() =
        onUi (fun () ->
            let window = paneWindow ()

            try
                window.Show()
                let host = window.Content :?> PaneHost
                let source = Seq.head host.Panes
                openPane host source "B" |> ignore
                let before = paneRect window source |> see
                let next = openPane host source "X"
                let after = paneRect window source |> see
                let opened = paneRect window next |> see
                Assert.Equal(before, after)
                Assert.True(opened.Top >= 0 && opened.Bottom <= window.ClientSize.Height)
            finally
                window.Close())

    [<Fact>]
    member _.ClosingKeepsOtherPanesInPlace() =
        onUi (fun () ->
            let window = paneFork ()

            try
                window.Show()
                let host = window.Content :?> PaneHost

                let panes = host.Panes |> Seq.map (fun p -> string p.Header, p) |> Map.ofSeq

                let before = panes |> Map.map (fun _ pane -> paneRect window pane) |> see

                for name in [ "B"; "X"; "C" ] do
                    host.Close(panes[name])

                    let remaining =
                        host.Panes
                        |> Seq.map (fun p -> string p.Header, paneRect window p)
                        |> Map.ofSeq
                        |> see

                    for KeyValue(name, bounds) in remaining do
                        Assert.Equal(before[name], bounds)
            finally
                window.Close())

    [<Fact>]
    member _.ContainersRejectWrongChildren() =
        onUi (fun () ->
            let host = PaneHost()
            let column = paneColumn [ "A" ]
            host.Columns.Add(column)
            let selected = host.Selected

            for children, invalid in
                [ column.Panes, TextBlock() :> Control; host.Columns, pane "B" :> Control ] do
                let original = Assert.Single(children)

                Assert.Throws<System.ArgumentException>(fun () -> children.Add(invalid))
                |> see
                |> ignore

                Assert.Throws<System.ArgumentException>(fun () -> children[0] <- invalid)
                |> see
                |> ignore

                Assert.Same(original, Assert.Single(children))

            Assert.Same(selected, host.Selected))

    [<Fact>]
    member _.InspectionsFollowAnswers() =
        let first = answer [ "x", AlInteger 1I ]
        let second = answer [ "x", AlInteger 2I ]
        let entry = ReplSuccess("example", first, Aguil.Core.MCP.AlMcpClient())

        for change in
            [
                (fun () -> entry.Solutions.Insert(0, second))
                (fun () -> entry.Solutions.Move(1, 0))
                (fun () -> entry.Solutions.RemoveAt(0))
            ] do
            change ()
            see entry.Result.Bindings.Values |> ignore
            Assert.Same(entry.Solution, entry.Inspection.Target)

        entry.Solutions.Clear()
        see entry.Position |> ignore
        Assert.Null(entry.Inspection)
        Assert.Empty(entry.Views)
        Assert.False(entry.NextCommand.CanExecute(null))

    [<Fact>]
    member _.PaneCollectionKeepsSelectionValid() =
        onUi (fun () ->
            let host = PaneHost()
            let column = paneColumn [ "A"; "B" ]
            let a, b = column.Panes[0] :?> Pane, column.Panes[1] :?> Pane
            host.Columns.Add(column)
            column.Panes.Move(1, 0)
            see host.Selected.Header |> ignore
            Assert.Same(b, host.Selected)
            column.Panes.Remove(b) |> ignore
            see host.Selected.Header |> ignore
            Assert.Same(a, host.Selected)
            column.Panes.Add(b)
            Assert.Same(b, host.Selected)
            let x = pane "X"
            column.Panes[column.Panes.IndexOf(b)] <- x
            see host.Selected.Header |> ignore
            Assert.Same(x, host.Selected)
            host.Columns.Clear()
            see host.Selected |> ignore
            Assert.Null(host.Selected)
            Assert.Empty(host.Panes)
            host.Columns.Add(column)
            Assert.Same(x, host.Selected)
            column.Panes.Clear()
            see host.Selected |> ignore
            Assert.Null(host.Selected))

    [<Fact>]
    member _.InspectionSourceSurvivesClosing() =
        onUi (fun () ->
            let value = AlInteger 42I
            let window = replAnswer [ "x", value ]

            try
                window.Show()
                let model = window.DataContext :?> ReplViewModel
                let source = model.History[0] :?> ReplSuccess
                let first = source.Solution
                let input = window.FindControl<AvaloniaEdit.TextEditor>("Input")
                input.TextArea.Focus() |> ignore
                inspectTarget window value
                let opened = Assert.Single(model.Inspections)
                Assert.Equal(box value, see opened.Target)
                let host = window.FindControl<PaneHost>("Flow")
                let child = host.Selected
                Assert.True(input.TextArea.IsFocused)
                host.Move(child, 1, 2)
                Assert.Same(opened, Inspector.GetInspection(child))

                source.Solutions.Add(
                    Answer({ first with Result = evaluation [ "x", AlInteger 43I ] })
                )

                source.Index <- 1
                see (paneRect window child) |> ignore
                clickAction window child "Close pane"
                Assert.DoesNotContain<Pane>(child, host.Panes)
                Assert.Same(opened, Assert.Single(model.Inspections))
                Assert.Same(first, opened.Source.Value.Target)
            finally
                window.Close())

    [<Fact>]
    member _.EitherPaneCanRestoreColumnWidth() =
        onUi (fun () ->
            let window = paneWindow ()

            try
                window.Show()
                let host = window.Content :?> PaneHost
                let a = Seq.head host.Panes
                let b = openPane host a "B"
                let half = paneRect window b |> see
                clickAction window b "Expand / restore column width"
                let full = paneRect window b |> see
                Assert.True(full.Width > half.Width)
                Assert.Equal(window.ClientSize.Width, full.Width)
                let x = openPane host a "X"
                Assert.Equal(full.Width, (paneRect window x |> see).Width)
                clickAction window x "Expand / restore column width"
                Assert.Equal(half.Width, (paneRect window x |> see).Width)
                Assert.Equal(half.Width, (paneRect window b |> see).Width)
                host.Move(x, 0, 2)
                clickAction window x "Expand / restore column width"
                Assert.Equal(full.Width, (paneRect window x |> see).Width)
                Assert.Equal(half.Width, (paneRect window b |> see).Width)
            finally
                window.Close())

    [<Fact>]
    member _.PreviousInputs() =
        Assert.Equal<string list>(
            [ "x = 1"; "vm_transaction_source(tx, text, origin)"; "y = 2"; "x = 1" ],
            see (historyInputs ())
        )

    [<Fact>]
    member _.AssignmentCaptures() =
        Assert.Equal<(string * string) list>(
            [ "variable", "x"; "operator", "="; "number", "1" ],
            see (assignmentCaptures ())
        )

    [<Fact>]
    member _.AssignmentSpans() =
        Assert.Equal<Span list>(
            [ span 0 1 "variable"; span 2 3 "operator"; span 4 5 "number" ],
            (look (assignment ()))[0]
        )

    [<Fact>]
    member _.StringAcrossLines() =
        let lines = look (stringAcrossLines ())
        Assert.Equal<Span>(span 4 6 "string", List.last lines[0])
        Assert.Equal<Span list>([ span 0 2 "string" ], lines[1])

    [<Fact>]
    member _.EscapeInString() =
        Assert.Equal<string list>(
            [ "string"; "string.escape" ],
            (look (escapeInString ()))[0] |> List.map _.Capture
        )

    [<Fact>]
    member _.DocHeredoc() = Assert.Contains<Span>(span 0 5 "comment.doc", (look (docHeredoc ()))[1])

    [<Fact>]
    member _.NotADocHeredoc() =
        Assert.DoesNotContain<string>(
            "comment.doc",
            look (notADocHeredoc ()) |> Seq.concat |> Seq.map _.Capture
        )

    [<Fact>]
    member _.StrayQuote() =
        let lines = look (strayQuote ())
        Assert.Equal<Span list>([ span 0 18 "string" ], lines[0])
        Assert.Equal<Span list>([ span 0 3 "keyword" ], lines[2])

    [<Fact>]
    member _.NonAscii() = Assert.Contains<Span>(span 4 9 "string", (look (nonAscii ()))[0])

    [<Fact>]
    member _.UnknownGrammar() =
        Assert.ThrowsAny<System.Exception>(fun () ->
            (Grammars.grammarFor "elixr").GetAwaiter().GetResult() |> ignore)
        |> ignore

    [<Fact>]
    member _.EditorCommandNames() =
        let names = see (editorCommandNames ())
        Assert.Contains<string>("MoveToLineEnd", names)
        Assert.Contains<string>("Paste", names)

    [<Fact>]
    member _.LineKeys() =
        Assert.Equal<string list>(
            [ "hello world|"; "|hello world" ],
            see (lineKeys ()) |> List.map snd
        )

    [<Fact>]
    member _.ContextCommand() =
        Assert.Equal<string list>(
            [ "PreviousInput"; "PreviousInput"; "Cancel" ],
            see (contextCommand ())
        )

    [<Fact>]
    member _.DefaultMovement() =
        Assert.Equal<string list>(
            [
                "hello world\nsec|ond line"
                "hel|lo world\nsecond line"
                "hello world|\nsecond line"
                "|hello world\nsecond line"
                "h|ello world\nsecond line"
                "hello |world\nsecond line"
                "|hello world\nsecond line"
                "|hello world\nsecond line"
            ],
            see (defaultMovement ()) |> List.map snd
        )

    [<Fact>]
    member _.DefaultDeletion() =
        Assert.Equal<string list>(
            [ "hel|o world"; "hel|world" ],
            see (defaultDeletion ()) |> List.map snd
        )

    [<Fact>]
    member _.MarkSelection() =
        Assert.Equal<string list>(
            [
                "hel|lo world, again"
                "hel[l]|o world, again"
                "hel[lo ]|world, again"
                "hello |world, again"
                "hello w|orld, again"
                "hello w|orld, again"
                "hello w[o]|rld, again"
                "hello wo|rld, again"
                "hello wor|ld, again"
                "hello wor|ld, again"
                "hello wor[l]|d, again"
                "hello world|, again"
                "hello world,| again"
            ],
            see (markSelection ()) |> List.map snd
        )
