namespace Aguil.Editor

open Xunit
open Xunit.Abstractions
open Aguil.Editor.Examples
open Aguil.Views
open Aguil.ViewModels
open Aguil.Core.AlValues
open Aguil.Core
open System.Collections.Specialized
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
    member _.OpenSlotDuringNotification() =
        task {
            let! slot = openSlotDuringNotification ()
            do! slot.Pending
            Assert.Equal(box "foo", see slot.Value)
        }

    [<Fact>]
    member _.SharedCommandListener() =
        task {
            let _, listener, next = commandListener ()
            use first = listener.Watch("main", 10L)
            let! request = next ()
            use second = listener.Watch("main", 10L)
            request.Reply.SetResult { Branch = "main"; From = 10L; Cursor = 12L; Commands = [] }
            let! waiting = next ()
            use joining = listener.Watch("main", 11L)
            Assert.False(waiting.Cancellation.IsCancellationRequested)
            Assert.Equal(12L, see waiting.Cursor)
            waiting.Reply.SetException(System.IO.IOException("offline"))
            let! retry = next ()
            Assert.Equal(waiting.Cursor, retry.Cursor)
            first.Dispose()
            second.Dispose()
            Assert.False(retry.Cancellation.IsCancellationRequested)
            joining.Dispose()
            Assert.True(retry.Cancellation.IsCancellationRequested)
            use reopened = listener.Watch("main", 20L)
            let! restarted = next ()
            Assert.Equal(20L, see restarted.Cursor)
        }

    [<Fact>]
    member _.CommandListenerSeparatesBranches() =
        task {
            let _, listener, next = commandListener ()
            use first = listener.Watch("main", 10L)
            let! main = next ()
            use second = listener.Watch("fork", 20L)
            let! fork = next ()
            Assert.Equal("main", see main.Branch)
            Assert.Equal("fork", see fork.Branch)
            Assert.False(main.Cancellation.IsCancellationRequested)
            second.Dispose()
            Assert.True(fork.Cancellation.IsCancellationRequested)
            Assert.False(main.Cancellation.IsCancellationRequested)
        }

    [<Fact>]
    member _.UnknownViewIsAnError() =
        onUi (fun () ->
            Assert.Throws<System.NotSupportedException>(fun () ->
                Aguil.Phlow.PhlowBuilder.Build(
                    Map.ofList [
                        AlAtom "view", AlText "missing"
                        AlAtom "title", AlText "Local"
                        AlAtom "priority", AlInteger 9I
                    ]
                )
                |> ignore)
            |> see
            |> ignore)

    [<Fact>]
    member _.StartupTimings() =
        task {
            let! timings = startupTimings ()
            see timings |> ignore
        }

    [<Fact>]
    member _.XamlTextBindings() =
        onUiAsync (fun () ->
            task {
                let! source = observationExample ()
                use! first = textBinding source false
                use! second = textBinding source true

                for view in [ first; second ] do
                    Assert.Equal("foo", see view.TextBox.Text)
                    Assert.Equal("hi", view.TextBox.PlaceholderText)
                    Assert.False(view.TextBox.IsReadOnly)

                let! stored = editText first "edited <&\" #{literal}"
                do! waitFor (fun () -> second.TextBox.Text = "edited <&\" #{literal}")
                Assert.Equal(AlText second.TextBox.Text, see stored.Value)
                let! stored = editText second "from second"
                do! waitFor (fun () -> first.TextBox.Text = "from second")
                Assert.Equal(AlText first.TextBox.Text, see stored.Value)

                let address = source.Address

                let! _ =
                    source.Client.WriteSlot(
                        address.ObjectId,
                        address.Path,
                        address.Branch,
                        AlText "external"
                    )

                do!
                    waitFor (fun () ->
                        first.TextBox.Text = "external" && second.TextBox.Text = "external")

                first.Window.Close()
                second.Window.Close()
                use! reopened = textBinding source false
                Assert.Equal("external", see reopened.TextBox.Text)
            })

    [<Fact>]
    member _.ObservableListChanges() =
        onUiAsync (fun () ->
            task {
                let client = alClient ()
                let cache = ObservationCache(client)
                let! list = observedList client cache null []
                let! same = cache.Open(list.ObjectId, list.Branch)
                Assert.Same(list, same)
                let changes = ResizeArray<NotifyCollectionChangedAction>()
                list.Items.CollectionChanged.Add(fun change -> changes.Add change.Action)
                let! _ = changeList list "insert List 0 7, insert List 0 7."
                do! waitFor (fun () -> list.Items.Count = 2)

                Assert.Equal<AlValue list>(
                    [ AlInteger 7I; AlInteger 7I ],
                    see (List.ofSeq list.Items)
                )

                Assert.Equal<NotifyCollectionChangedAction list>(
                    [ NotifyCollectionChangedAction.Add; NotifyCollectionChangedAction.Add ],
                    see (List.ofSeq changes)
                )

                list.Items.Clear()

                client.Commands.Trigger(
                    {
                        Branch = list.Branch
                        From = System.Int64.MaxValue - 1L
                        Cursor = System.Int64.MaxValue
                        Commands = []
                    }
                )

                do! waitFor (fun () -> List.ofSeq list.Items = [ AlInteger 7I; AlInteger 7I ])

                let! _ =
                    changeList
                        list
                        "remove List 0, replace List [2, 3], lambda [X, Y] Double {= Y (* X 2)}, map List Double."

                do! waitFor (fun () -> List.ofSeq list.Items = [ AlInteger 4I; AlInteger 6I ])
                let stable = list.Items
                let! _ = changeList list "set_slot List contents [9]."
                do! waitFor (fun () -> List.ofSeq list.Items = [ AlInteger 9I ])
                do! list.Refresh()
                Assert.Same(stable, list.Items)
                let! _ = changeList list "set_slot List contents [8, 9], insert List 2 10."

                do!
                    waitFor (fun () ->
                        List.ofSeq list.Items = [ AlInteger 8I; AlInteger 9I; AlInteger 10I ])

                let! rebuilt = ObservationCache(client).Open(list.ObjectId, list.Branch)

                Assert.Equal<AlValue list>(
                    List.ofSeq list.Items,
                    see (List.ofSeq (rebuilt :?> AlObservableList).Items)
                )
            })

    [<Fact>]
    member _.XamlListBindings() =
        onUiAsync (fun () ->
            task {
                let client = alClient ()

                let! created =
                    alQuery
                        client
                        null
                        "new brick_textbox #{text => \"hello\", is_read_only => false} Text."
                        []

                let item = Aguil.Core.MCP.Evaluation.binding "Text" created
                let! list = observedList client (ObservationCache(client)) created.Branch [ item ]
                let! firstWindow, first = listBinding list
                let! secondWindow, second = listBinding list

                try
                    do!
                        waitFor (fun () ->
                            (listTextBoxes first |> List.exists (fun t -> t.Text = "hello"))
                            && (listTextBoxes second |> List.exists (fun t -> t.Text = "hello")))

                    let firstText = List.exactlyOne (listTextBoxes first)
                    let secondText = List.exactlyOne (listTextBoxes second)
                    Assert.NotSame(firstText, secondText)
                    firstText.Text <- "edited"
                    do! waitFor (fun () -> secondText.Text = "edited")
                    let! _ = changeList list "get List contents [Item], insert List 0 Item."

                    do!
                        waitFor (fun () ->
                            first.ItemCount = 2
                            && second.ItemCount = 2
                            && (listTextBoxes first).Length = 2
                            && (listTextBoxes second).Length = 2)

                    Assert.Contains(firstText, listTextBoxes first)
                    let! _ = changeList list "remove List 0."
                    do! waitFor (fun () -> first.ItemCount = 1 && second.ItemCount = 1)
                    Assert.Equal("edited", see secondText.Text)
                finally
                    firstWindow.Close()
                    secondWindow.Close()
            })

    [<Fact>]
    member _.QueuedSlotWrites() =
        onUiAsync (fun () ->
            task {
                let! source = observationExample ()
                let address = source.Address

                let! first =
                    source.ObservationCache.Open(address.ObjectId, address.Path, address.Branch)

                let! second = source.ObservationCache.Open(address.ObjectId, address.Path, null)
                Assert.Same(first, second)
                first.Value <- "first"
                second.Value <- "last"
                do! second.Pending
                let! stored = source.Client.ReadSlot(address.ObjectId, address.Path, address.Branch)
                Assert.Equal(box "last", see first.Value)
                Assert.Equal(AlText "last", see stored.Value)
                Assert.False(first.HasErrors)
            })

    [<Fact>]
    member _.RetiredReferenceIsReleased() =
        task {
            let! source = observationExample ()
            let! key, owner = collectedReference source
            let address = source.Address

            let! replacement =
                source.ObservationCache.Open(address.ObjectId, address.Path, address.Branch)

            do! replacement.Pending
            do! collectGarbage ()
            Assert.False(see (fst (key.TryGetTarget())))
            Assert.False(see (fst (owner.TryGetTarget())))

            let! reopened =
                source.ObservationCache.Open(address.ObjectId, address.Path, address.Branch)

            Assert.Same(replacement, reopened)
            do! reopened.Pending
        }

    [<Fact>]
    member _.MissingSlotBinding() =
        onUiAsync (fun () ->
            task {
                let! source = observationExample ()

                let! slot =
                    source.ObservationCache.Open(
                        source.Address.ObjectId,
                        "missing",
                        source.Address.Branch
                    )

                do! slot.Pending
                Assert.True(see slot.HasErrors)
            })

    [<Fact>]
    member _.RejectedSlotWrite() =
        onUiAsync (fun () ->
            task {
                let! source = observationExample ()
                let address = source.Address
                let! slot = source.ObservationCache.Open(address.ObjectId, "choice", address.Branch)
                do! slot.Pending
                let textbox = boundTextBox slot
                textbox.Text <- "rejected"
                do! slot.Pending
                do! waitFor (fun () -> DataValidationErrors.GetHasErrors textbox)
                Assert.True(see slot.HasErrors)
                Assert.Equal("foo", see textbox.Text)
                let! unchanged = source.Client.ReadSlot(address.ObjectId, "choice", address.Branch)
                Assert.Equal(AlText "foo", see unchanged.Value)
                textbox.Text <- "bar"
                do! slot.Pending
                do! waitFor (fun () -> not (DataValidationErrors.GetHasErrors textbox))
                let! accepted = source.Client.ReadSlot(address.ObjectId, "choice", address.Branch)
                Assert.Equal(AlText "bar", see accepted.Value)
            })

    [<Fact>]
    member _.QuotedValuesRoundTrip() =
        for value in quotedValues () do
            see (string value) |> ignore
            Assert.Equal(value, roundTrip value |> see)

    [<Fact>]
    member _.PrintedValuesUseAlSyntax() =
        for value in
            [
                AlAtom "point"
                AlAtom "with space"
                AlText "hello"
                AlList [ AlInteger 1I; AlInteger 2I ]
                AlMap(Map.ofList [ AlAtom "x", AlInteger 3I ])
            ] do
            Assert.Equal(value, printedRoundTrip value |> see)

    [<Fact>]
    member _.InspectionsFollowAnswers() =
        let first = answer [ "x", AlInteger 1I ]
        let second = answer [ "x", AlInteger 2I ]
        let entry = ReplSuccess("example", first, alClient ())

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
    member _.PreviousInputs() =
        Assert.Equal<string list>([ "= X 1."; "= Y 2."; "= X 1." ], see (historyInputs ()))

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
    member _.AlAssignment() =
        Assert.Equal<Span list>(
            [ span 0 1 "operator"; span 2 3 "variable"; span 4 5 "number" ],
            (look (alAssignment ()))[0]
        )

    [<Fact>]
    member _.AlMapAndComment() =
        let line = (look (alMapAndComment ()))[0]
        Assert.Contains<Span>(span 4 6 "operator", line)
        Assert.Contains<Span>(span 15 21 "comment", line)
        Assert.Single(line |> List.filter (fun part -> part.Capture = "comment")) |> ignore

    [<Fact>]
    member _.AlStringsAndAtoms() =
        let lines = look (alStringAcrossLines ())
        Assert.Contains<Span>(span 4 6 "string", lines[0])
        Assert.Equal<Span list>([ span 0 2 "string" ], lines[1])
        let atom = (look (alQuotedAtom ()))[0]
        Assert.Contains<string>("string.special.symbol", atom |> List.map _.Capture)
        Assert.Contains<string>("string.escape", atom |> List.map _.Capture)

    [<Fact>]
    member _.AlMethodClause() =
        let lines = look (alMethodClause ())
        Assert.Contains<Span>(span 5 7 "operator", lines[0])
        Assert.Contains<Span>(span 3 4 "variable", lines[1])
        Assert.Contains<Span>(span 0 3 "constant", lines[2])

    [<Fact>]
    member _.AlUsesUtf16Offsets() =
        Assert.Contains<Span>(span 4 9 "string", (look (alNonAscii ()))[0])

    [<Fact>]
    member _.ReplInputUsesAlHighlighting() =
        onUi (fun () ->
            let repl = Repl()
            let input = repl.FindControl<AvaloniaEdit.TextEditor>("Input")
            Assert.Equal("al", Aguil.Editor.Code.GetGrammar(input)))

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
