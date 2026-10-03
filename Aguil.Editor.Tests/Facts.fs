namespace Aguil.Editor

open System.Linq
open Aguil.ALViews
open Aguil.Core
open Aguil.Core.AlValues
open Aguil.Inspection
open Aguil.ViewModels
open Avalonia
open Avalonia.Controls
open Avalonia.Headless
open Avalonia.Input
open Avalonia.Threading
open Avalonia.VisualTree
open AvaloniaEdit
open AvaloniaEdit.Rendering
open Xunit
open Xunit.Abstractions
open Aguil.Editor.Examples

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

    let withWindow (create: unit -> #Window) check =
        onUi (fun () ->
            let window = create ()
            window.Show()

            try
                Dispatcher.UIThread.RunJobs()
                check window
            finally
                window.Close())

    let press (window: Window) key modifiers =
        window.KeyPress(key, modifiers, PhysicalKey.None, null)
        Dispatcher.UIThread.RunJobs()

    let editor (window: Window) = window.GetVisualDescendants().OfType<TextEditor>().Single()

    let tabs (window: Window) entry =
        window.GetVisualDescendants().OfType<TabControl>()
        |> Seq.find (fun t -> t.DataContext = entry && t.Classes.Contains "result-tabs")

    [<Fact>]
    member _.TextViews() =
        onUi (fun () ->
            for editor, grammar in [ plainEditor (), null; sourceEditor (), "elixir" ] do
                Assert.Equal("x = 1", see editor.Text)
                Assert.Equal(grammar, see (Code.GetGrammar editor))
                Assert.Null(InspectionTarget.GetTarget editor))

    [<Fact>]
    member _.ItemTargets() =
        withWindow (fun () -> Window(Content = targetList ())) (fun window ->
            let list = window.Content :?> ListBox
            Assert.Null(InspectionTarget.GetTarget list)

            Assert.Equal<obj list>(
                [ box "one"; box "two" ],
                see [
                    for label in list.GetVisualDescendants().OfType<TextBlock>() ->
                        InspectionTarget.GetTarget label
                ]
            ))

    [<Fact>]
    member _.RawTargets() =
        onUi (fun () ->
            let solution = viewSolution ()

            for binding in solution.Result.Bindings.Values do
                let editor = AlViews.raw binding |> PhlowBuilder.Render
                Assert.Equal<obj>(binding.Value, see (InspectionTarget.GetTarget editor)))

    [<Fact>]
    member _.PreviewScroll() =
        withWindow (fun () -> previewWindow (longEditor ())) (fun window ->
            let editor = editor window
            editor.TextArea.Focus() |> ignore
            press window Key.End RawInputModifiers.Control
            Assert.True(see editor.VerticalOffset > 0.))

    [<Fact>]
    member _.PreviewScrollStopsAtDocument() =
        withWindow (fun () -> previewWindow (longEditor ())) (fun window ->
            let editor = editor window
            window.MouseWheel(Point(100., 100.), Vector(0., -10000.), RawInputModifiers.None)
            Dispatcher.UIThread.RunJobs()

            Assert.Equal(
                editor.TextArea.TextView.DocumentHeight,
                see (editor.VerticalOffset + editor.ViewportHeight),
                3
            ))

    [<Fact>]
    member _.RawScroll() =
        let create () =
            evaluation [ "long", AlText(String.replicate 100 "a line\n") ] |> rawView |> previewWindow

        withWindow create (fun window ->
            let editor = editor window
            editor.TextArea.Focus() |> ignore
            press window Key.End RawInputModifiers.Control
            let view = editor.TextArea.TextView

            let caret =
                view.GetVisualPosition(editor.TextArea.Caret.Position, VisualYPosition.LineBottom)

            let point = view.TranslatePoint(caret - view.ScrollOffset, window).Value
            Assert.InRange(see point.Y, 0., window.Bounds.Height))

    [<Fact>]
    member _.ViewPriority() =
        let solution = viewSolution ()
        let binding = solution.Result.Bindings.Values.Head
        let views = AlViews.forBinding binding solution.Views[binding.Symbol]

        Assert.Equal<AlValue list>(
            [ AlInteger 10I; AlInteger 100I; AlInteger 200I ],
            see (views |> List.map (Map.find (AlAtom "priority")))
        )

        Assert.Equal("Raw", PhlowBuilder.Text(views[1], "title"))

    [<Fact>]
    member _.ViewError() =
        onUi (fun () ->
            let binding = (viewSolution ()).Result.Bindings.Values.Head
            let views = evaluation [ "maps", AlList [ AlMap(invalidTextView ()) ] ]
            let error = Record.Exception(fun () -> SolutionView.BindingTab(binding, views) |> ignore)
            Assert.Null(see error))

    [<Fact>]
    member _.ViewTabs() =
        withWindow viewRepl (fun window ->
            let vm = window.DataContext :?> ReplViewModel
            let outer = tabs window vm.Selected

            let headers (t: TabControl) = [
                for item in t.Items -> (item :?> TabItem).Header :?> string
            ]

            Assert.Equal<string list>([ "Raw"; "first"; "second" ], see (headers outer))
            Assert.Equal(0, outer.SelectedIndex)
            outer.SelectedIndex <- 1
            Dispatcher.UIThread.RunJobs()
            let inner = outer.GetVisualDescendants().OfType<TabControl>().Single()
            Assert.Equal<string list>([ "Earlier"; "Raw"; "Later" ], see (headers inner))
            Assert.Equal(0, inner.SelectedIndex))

    [<Theory>]
    [<InlineData(1)>]
    [<InlineData(2)>]
    [<InlineData(3)>]
    member _.OuterTabKey(number: int) =
        withWindow viewRepl (fun window ->
            let vm = window.DataContext :?> ReplViewModel
            let outer = tabs window vm.Selected
            outer.SelectedIndex <- 2
            let input = window.FindControl<TextEditor>("Input")
            input.TextArea.Focus() |> ignore
            press window (enum<Key>(int Key.D1 + number - 1)) RawInputModifiers.Alt
            Assert.Equal(number - 1, see outer.SelectedIndex)
            Assert.Equal(0, (tabs window vm.History[1]).SelectedIndex)
            Assert.True(input.TextArea.IsFocused))

    [<Theory>]
    [<InlineData(1)>]
    [<InlineData(2)>]
    [<InlineData(3)>]
    member _.InnerTabKey(number: int) =
        withWindow viewRepl (fun window ->
            let vm = window.DataContext :?> ReplViewModel
            let outer = tabs window vm.Selected
            outer.SelectedIndex <- 1
            Dispatcher.UIThread.RunJobs()
            let inner = outer.GetVisualDescendants().OfType<TabControl>().Single()
            inner.SelectedIndex <- 2
            let input = window.FindControl<TextEditor>("Input")
            input.TextArea.Focus() |> ignore

            press
                window
                (enum<Key>(int Key.D1 + number - 1))
                (RawInputModifiers.Alt ||| RawInputModifiers.Control)

            Assert.Equal(number - 1, see inner.SelectedIndex)
            Assert.Equal(1, outer.SelectedIndex)
            Assert.True(input.TextArea.IsFocused))

    [<Fact>]
    member _.ViewAnswers() =
        task {
            let! entry = answerEntry ()

            let check expected =
                let answer = entry.Result.Bindings.Values |> List.find (fun b -> b.Symbol = "answer")
                let description = (AlViews.descriptions entry.Solution.Views["output"])[0]
                Assert.Equal(AlText expected, see answer.Value)
                Assert.Equal(expected, PhlowBuilder.Text(description, "text"))

            check "one"
            do! entry.NextCommand.ExecuteAsync null
            check "two"
        }

    [<Fact>]
    member _.AnswerNavigation() =
        task {
            let! entry = answerEntry ()
            let first = entry.Solution
            do! entry.NextCommand.ExecuteAsync null
            let second = entry.Solution
            entry.PrevCommand.Execute null
            Assert.Equal(first, entry.Solution)
            Assert.Equal("1/2", see entry.Position)
            do! entry.NextCommand.ExecuteAsync null
            Assert.Equal(second, entry.Solution)
            Assert.Equal("2/2", see entry.Position)
        }

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
