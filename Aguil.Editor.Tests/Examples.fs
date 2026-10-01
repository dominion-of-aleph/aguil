/// Composable editor examples. Facts.fs prints their results and asserts on them.
module Aguil.Editor.Examples

open System
open System.Threading
open System.Windows.Input
open Avalonia
open Avalonia.Controls
open Avalonia.Headless
open Avalonia.Input
open Avalonia.Markup.Xaml.Styling
open Avalonia.Themes.Fluent
open Avalonia.Threading
open AvaloniaEdit
open TreeSitter
open Aguil.Core.AlEvaluation
open Aguil.Core.AlValues
open Avalonia.Input.Platform
open Avalonia.Styling
open AvaloniaEdit.Rendering

/// The grammar the name "elixir" stands for.
let elixirGrammar () = (Grammars.grammarFor "elixir").Result

/// A parser for Elixir. Not thread-safe, so each agent has its own.
let elixirParser () = new Parser((elixirGrammar ()).Language)

/// Parses "x = 1". The tree stays valid after the parser is disposed.
let assignmentTree () =
    use parser = elixirParser ()
    parser.Parse "x = 1"

/// The captures in assignmentTree, as (name, text) pairs.
let assignmentCaptures () =
    use tree = assignmentTree ()
    use cursor = (elixirGrammar ()).Highlights.Execute tree.RootNode
    [ for capture in cursor.Captures -> capture.Name, capture.Node.Text ]

/// Styles a text the way the agent does. Returns the text and its spans per line.
let style text =
    use parser = elixirParser ()
    text, Styler.style parser (elixirGrammar ()).Highlights text

/// Formats each line followed by its spans.
let describe (text: string, lines: Span list array) =
    let rows = text.Split '\n'

    [
        for row, spans in Array.indexed lines do
            sprintf "line %d  %A" (row + 1) rows[row]

            for span in spans do
                sprintf
                    "    %2d..%-2d %-26s %A"
                    span.Start
                    span.End
                    span.Capture
                    rows[row].[span.Start .. span.End - 1]
    ]
    |> String.concat "\n"

let assignment () = style "x = 1"

/// A string over two lines: one span per line.
let stringAcrossLines () = style "x = \"a\nb\""

/// An escape in a string: the escape's span comes after the string's, so it wins.
let escapeInString () = style "\"tab\\there\""

/// @doc makes the heredoc comment.doc.
let docHeredoc () = style "@doc \"\"\"\nhello\n\"\"\""

/// @dog: same tree shape, but no comment.doc.
let notADocHeredoc () = style "@dog \"\"\"\nhello\n\"\"\""

/// A stray quote: the string runs to the next quote, then the parser recovers.
let strayQuote () =
    style "\"defclass :switch,\n  ivars: [%{domain: [\"on\", \"off\"], default: \"off\"}] do\nend"

/// Columns are UTF-16 chars: é is 1, 🙂 is 2.
let nonAscii () = style "x = \"é🙂\" <> y"

/// A headless Avalonia app with AvaloniaEdit's theme, so editors get their parts and can take focus.
type HeadlessApp() =
    inherit Application()

    override this.Initialize() =
        this.Styles.Add(FluentTheme())

        this.Styles.Add(
            StyleInclude(
                Uri "avares://AvaloniaEdit",
                Source = Uri "avares://AvaloniaEdit/Themes/Fluent/AvaloniaEdit.xaml"
            )
        )

    static member BuildAvaloniaApp() =
        AppBuilder
            .Configure<HeadlessApp>()
            .UseSkia()
            .UseHeadless(AvaloniaHeadlessPlatformOptions(UseHeadlessDrawing = false))

let private session = lazy (HeadlessUnitTestSession.StartNew typeof<HeadlessApp>)

/// Runs f on the headless app's UI thread, starting the app on first use.
let onUi (f: unit -> 'a) = session.Value.Dispatch(Func<'a> f, CancellationToken.None).Result

/// The AvaloniaEdit commands a keymap can name.
let editorCommandNames () = onUi (fun () -> Keymaps.editorCommands.Keys |> Seq.sort |> List.ofSeq)

/// The editor's text with the caret as | and the selection in [ ].
let marked (editor: TextEditor) =
    let start, finish = editor.SelectionStart, editor.SelectionStart + editor.SelectionLength

    let mark i =
        (if i = finish && start < finish then "]" else "")
        + (if i = editor.CaretOffset then "|" else "")
        + (if i = start && start < finish then "[" else "")

    String.concat "" [
        for i in 0 .. editor.Text.Length -> mark i + (editor.Text + " ").Substring(i, 1)
    ]
    |> _.TrimEnd(' ')

/// Presses keys in a focused editor holding text, caret at 3, with the keymap installed.
/// Returns each key with the editor after it, marked.
let pressKeys text keymap (context: obj) keys =
    onUi (fun () ->
        let editor = TextEditor(Text = text, DataContext = context)
        let window = Window(Content = editor)
        window.Show()
        Dispatcher.UIThread.RunJobs()
        editor.TextArea.Focus() |> ignore
        use _ = Keymaps.install editor.TextArea (readOnlyDict keymap)
        editor.CaretOffset <- 3

        let after = [
            for key: Key, modifiers: RawInputModifiers in keys do
                window.KeyPress(key, modifiers, PhysicalKey.None, null)
                Dispatcher.UIThread.RunJobs()
                $"{modifiers}+{key}", marked editor
        ]

        window.Close()
        after)

/// Ctrl+E and Ctrl+A bound to line end and start; Ctrl+A is Select All without the keymap.
let lineKeys () =
    pressKeys "hello world" [ "Ctrl+E", "MoveToLineEnd"; "Ctrl+A", "MoveToLineStart" ] null [
        Key.E, RawInputModifiers.Control
        Key.A, RawInputModifiers.Control
    ]

/// A data context whose PreviousInput and Cancel commands log their runs.
type CommandLog() =
    member val Runs: string list = [] with get, set

    member private this.Logging name = {
        new ICommand with
            member _.CanExecute _ = true
            member _.Execute _ = this.Runs <- this.Runs @ [ name ]

            [<CLIEvent>]
            member _.CanExecuteChanged = Event<EventHandler, EventArgs>().Publish
    }

    member this.PreviousInputCommand = this.Logging "PreviousInput"
    member this.CancelCommand = this.Logging "Cancel"

/// Alt+P twice then C-g under the default keymap: actions not in AvaloniaEdit run the context's commands.
let contextCommand () =
    let log = CommandLog()

    pressKeys "hello world" [ for KeyValue(key, action) in Keymaps.defaults -> key, action ] log [
        Key.P, RawInputModifiers.Alt
        Key.P, RawInputModifiers.Alt
        Key.G, RawInputModifiers.Control
    ]
    |> ignore

    log.Runs

/// The default keymap's movement over "hello world" and "second line", from line 1 column 3.
let defaultMovement () =
    pressKeys
        "hello world\nsecond line"
        [ for KeyValue(key, action) in Keymaps.defaults -> key, action ]
        null
        [
            Key.N, RawInputModifiers.Control
            Key.P, RawInputModifiers.Control
            Key.E, RawInputModifiers.Control
            Key.A, RawInputModifiers.Control
            Key.F, RawInputModifiers.Control
            Key.F, RawInputModifiers.Alt
            Key.B, RawInputModifiers.Alt
            Key.B, RawInputModifiers.Control
        ]

/// The default keymap's deletes in "hello world" from column 3: a character, then up to the next word.
let defaultDeletion () =
    pressKeys "hello world" [ for KeyValue(key, action) in Keymaps.defaults -> key, action ] null [
        Key.D, RawInputModifiers.Control
        Key.D, RawInputModifiers.Alt
    ]

/// C-SPC then moves select from column 3; C-SPC again clears, so C-f moves; C-g and an arrow also end it.
let markSelection () =
    pressKeys
        "hello world, again"
        [ for KeyValue(key, action) in Keymaps.defaults -> key, action ]
        null
        [
            Key.Space, RawInputModifiers.Control
            Key.F, RawInputModifiers.Control
            Key.F, RawInputModifiers.Alt
            Key.Space, RawInputModifiers.Control
            Key.F, RawInputModifiers.Control
            Key.Space, RawInputModifiers.Control
            Key.F, RawInputModifiers.Control
            Key.G, RawInputModifiers.Control
            Key.F, RawInputModifiers.Control
            Key.Space, RawInputModifiers.Control
            Key.F, RawInputModifiers.Control
            Key.Right, RawInputModifiers.None
            Key.F, RawInputModifiers.Control
        ]

/// A result with values, a decode failure, a constraint and a store entry.
let resultContext () =
    let empty = { Values = []; Failures = [] }

    {
        Bindings = {
            Values = [
                { Symbol = "x"; Value = AlInteger 42I }
                { Symbol = "message"; Value = AlText "é🙂" }
                { Symbol = "items"; Value = AlList [ for n in 1..30 -> AlInteger(bigint n) ] }
            ]
            Failures = [ { Symbol = "unknown"; Value = UnknownType "record" } ]
        }
        Constraints = {
            empty with
                Values = [ { Symbol = "y"; Value = AlTuple [ AlInteger 1I; AlInteger 5I ] } ]
        }
        Store = { empty with Values = [ { Symbol = ":saved"; Value = AlText "value" } ] }
        Context = "context-1"
        HasPotentialSolution = false
    }

type ResultSelection = {
    Rows: string list
    BindingRetained: bool
    Dragged: string
    Copied: string
    Keys: string list
    ButtonCopy: string
    Preview: Avalonia.Media.Imaging.Bitmap
    Updated: string list
    UpdatedKey: string
}

/// Selects and copies text, uses the REPL keymap, copies the result, and replaces its boxes.
let resultSelection () =
    onUi (fun () ->
        let context = resultContext ()
        let output = StackPanel()
        ResultView.SetResult(output, context)
        let scroll = ScrollViewer(Content = output)
        let window = Window(Content = scroll, Width = 650., Height = 400.)
        let style = Style(fun selector -> selector.OfType<TextEditor>())
        style.Setters.Add(Setter(Code.KeymapProperty, Keymaps.defaults))
        window.Styles.Add style
        window.Show()
        Dispatcher.UIThread.RunJobs()

        let editors () =
            output.Children
            |> Seq.choose (function
                | :? Border as box -> Some(box.Child :?> TextEditor)
                | _ -> None)
            |> List.ofSeq

        let rows = editors ()
        let first = rows.Head
        let bindingRetained = Object.ReferenceEquals(context.Bindings.Values.Head, first.DataContext)
        let view = first.TextArea.TextView

        let point offset =
            let position = TextViewPosition(first.Document.GetLocation offset)
            let p = view.GetVisualPosition(position, VisualYPosition.LineMiddle)
            view.TranslatePoint(p, window).Value

        window.MouseDown(point 4, MouseButton.Left)
        window.MouseMove(point 6)
        window.MouseUp(point 6, MouseButton.Left)
        let dragged = first.SelectedText

        let key key =
            window.KeyPress(key, RawInputModifiers.Control, PhysicalKey.None, null)
            Dispatcher.UIThread.RunJobs()

        let clipboard () = window.Clipboard.TryGetTextAsync().GetAwaiter().GetResult()
        key Key.C
        let copied = clipboard ()

        let keys = [
            for k in [ Key.A; Key.E; Key.Space; Key.B; Key.G ] do
                key k
                yield marked first
        ]

        let copy =
            output.Children
            |> Seq.pick (function
                | :? Button as button -> Some button
                | _ -> None)

        copy.RaiseEvent(Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent))

        Dispatcher.UIThread.RunJobs()
        let buttonCopy = clipboard ()
        scroll.Offset <- Vector(0., 0.)
        Dispatcher.UIThread.RunJobs()
        let frame = window.CaptureRenderedFrame()

        ResultView.SetResult(
            output,
            {
                context with
                    Bindings = { Values = []; Failures = [] }
                    Context = "context-2"
            }
        )

        Dispatcher.UIThread.RunJobs()
        let updated = editors ()
        let next = updated.Head
        next.TextArea.Focus() |> ignore
        next.CaretOffset <- next.Text.Length
        key Key.A

        let result = {
            Rows = rows |> List.map _.Text
            BindingRetained = bindingRetained
            Dragged = dragged
            Copied = copied
            Keys = keys
            ButtonCopy = buttonCopy
            Preview = frame
            Updated = updated |> List.map _.Text
            UpdatedKey = marked next
        }

        window.Close()
        result)
