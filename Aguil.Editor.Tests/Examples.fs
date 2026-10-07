/// Composable editor examples. Facts.fs prints their results and asserts on them.
module Aguil.Editor.Examples

open System
open System.Threading
open System.Windows.Input
open Aguil.Core
open Aguil.Core.MCP.Evaluation
open Aguil.Core.AlValues
open Aguil.Views
open Aguil.ViewModels
open Aguil.Phlow
open Avalonia
open Avalonia.Controls
open Avalonia.Headless
open Avalonia.Input
open Avalonia.Markup.Xaml.Styling
open Avalonia.Themes.Fluent
open Avalonia.Threading
open Avalonia.VisualTree
open AvaloniaEdit
open TreeSitter

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
        AppBuilder.Configure<HeadlessApp>().UseHeadless(AvaloniaHeadlessPlatformOptions())

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

/// An evaluation assembled from bindings, without contacting AL.
let evaluation bindings =
    let empty = { Values = []; Failures = [] }

    {
        Bindings = {
            empty with
                Values = [ for name, value in bindings -> { Symbol = name; Value = value } ]
        }
        Constraints = empty
        Store = empty
        Context = "example"
        HasPotentialSolution = false
    }

/// Ask AL for the views of supplied bindings; the keys remain their display names.
let viewsFor bindings = MCP.AlMcpClient().AlViewsFromQuery(evaluation bindings, null).Result

/// A map containing two references to the same unbound variable.
let partialMap name = AlMap(Map.ofList [ AlAtom "slot", AlList [ AlVar name; AlVar name ] ])

/// A retained source row in the history query's wire format.
let historyRow tx kind source =
    AlList [
        AlInteger tx
        AlText source
        AlMap(Map.ofList [ AlAtom "kind", AlAtom kind ])
    ]

/// Authored inputs interleaved with an internal run and our own history query.
let historyRows () = [
    historyRow 1I "eval_source" "x = 1"
    historyRow 2I "transaction_program" "x = 2"
    historyRow 3I "eval_source" MCP.History.query
    historyRow 4I "eval_source" "vm_transaction_source(tx, text, origin)"
    historyRow 5I "eval_source" "y = 2"
    historyRow 6I "eval_source" "x = 1"
]

/// Filter a history result without contacting AL or depending on its stored inputs.
let historyInputs () = evaluation [ "inputs", AlList(historyRows ()) ] |> MCP.History.fromQuery

/// An answer owns its inspection identity independently of its position in a result list.
let answer bindings =
    Answer(
        {
            Result = evaluation bindings
            Views = Map.ofList [ for name, _ in bindings -> name, [] ]
        }
    )

/// An ordinary control framed as a pane; callers can replace its content.
let pane title = Pane(Header = title, Content = TextBox(Text = title))

/// A column owns its panes and can be populated before joining a workspace.
let paneColumn titles =
    let column = PaneColumn()

    for title in titles do
        column.Panes.Add(pane title)

    column

/// A window to show and extend on the UI thread, with one full-width pane.
let paneWindow () =
    let host = PaneHost()
    host.Columns.Add(paneColumn [ "A" ])
    Window(Content = host, Width = 1000, Height = 600)

/// Open another ordinary control to the right of its source.
let openPane (host: PaneHost) source title =
    let child = pane title
    host.Open(child, source)
    child

/// Two openings from A, each with another pane opened from it.
let paneFork () =
    let window = paneWindow ()
    let host = window.Content :?> PaneHost
    let a = Seq.head host.Panes
    let b = openPane host a "B"
    let x = openPane host a "X"
    openPane host b "C" |> ignore
    openPane host x "Y" |> ignore
    window

/// A pane's visible rectangle, independent of how the host arranges it.
let paneRect (window: Window) (pane: Pane) =
    Dispatcher.UIThread.RunJobs()
    Rect(pane.TranslatePoint(Avalonia.Point(), window).Value, pane.Bounds.Size)

/// Exercise a control through the window's normal pointer route.
let click (window: Window) (control: Control) button =
    Dispatcher.UIThread.RunJobs()

    let at =
        control
            .TranslatePoint(
                Avalonia.Point(control.Bounds.Width / 2.0, control.Bounds.Height / 2.0),
                window
            )
            .Value

    window.MouseDown(at, button, RawInputModifiers.None)
    window.MouseUp(at, button, RawInputModifiers.None)
    Dispatcher.UIThread.RunJobs()

/// Click an action by its visible tooltip, independent of its position or button type.
let clickAction window (control: Control) tooltip =
    let button =
        control.GetVisualDescendants()
        |> Seq.choose (function
            | :? Button as b -> Some b
            | _ -> None)
        |> Seq.find (fun b -> ToolTip.GetTip(b) = box tooltip)

    click window button MouseButton.Left

/// A REPL with a supplied answer; no evaluation is needed to explore its inline inspector.
let replAnswer bindings =
    let window = Repl(Width = 1000, Height = 600)
    let model = window.DataContext :?> ReplViewModel

    model.History.Add(ReplSuccess("example", answer bindings, MCP.AlMcpClient()))
    window

/// Middle-click a displayed target through the same inherited marker as the REPL.
let inspectTarget (window: Window) value =
    Dispatcher.UIThread.RunJobs()

    let control =
        window.GetVisualDescendants()
        |> Seq.choose (function
            | :? Control as c -> Some c
            | _ -> None)
        |> Seq.find (fun c ->
            c.IsEffectivelyVisible
            && c.Bounds.Width > 0
            && c.Bounds.Height > 0
            && InspectionTarget.GetTarget(c) = box value)

    click window control MouseButton.Middle
