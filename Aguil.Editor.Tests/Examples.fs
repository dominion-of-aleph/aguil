/// Composable editor examples. Facts.fs prints their results and asserts on them.
module Aguil.Editor.Examples

open System
open System.Threading
open System.Threading.Tasks
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

/// Columns are UTF-16 chars: U+00E9 is 1, U+20000 is 2.
let nonAscii () = style "x = \"\u00E9\U00020000\" <> y"

/// Styles AL source without changing the Elixir examples.
let alStyle text = text, AlStyler.style text

let alAssignment () = alStyle "= X 1."
let alMapAndComment () = alStyle "= X #{x => 1}. # note"
let alStringAcrossLines () = alStyle "= X \"a\nb\"."
let alQuotedAtom () = alStyle "= X 'with \\' quote'."

let alMethodClause () =
    alStyle "list >> fold_left\n| [H . T] Func Acc Result |\nrun Func [Acc, H, Next]."

let alNonAscii () = alStyle "= X \"\u00E9\U00020000\"."

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

/// Runs an asynchronous example without blocking its UI continuations.
let onUiAsync (f: unit -> Task<'a>) =
    session.Value.Dispatch<'a>(Func<Task<'a>> f, CancellationToken.None)

let alClient () = MCP.AlMcpClient()

type CommandRequest = {
    Branch: string
    Cursor: int64
    Cancellation: CancellationToken
    Reply: TaskCompletionSource<CommandBatch>
}

/// A controllable command-log reader, sharing a plain broker with any other producers.
let commandListener () =
    let requests = System.Threading.Channels.Channel.CreateUnbounded<CommandRequest>()
    let broker = Event<CommandBatch>()

    let listener =
        CommandLogListener(
            (fun branch cursor cancellation ->
                let reply =
                    TaskCompletionSource<CommandBatch>(
                        TaskCreationOptions.RunContinuationsAsynchronously
                    )

                requests.Writer.TryWrite {
                    Branch = branch
                    Cursor = cursor
                    Cancellation = cancellation
                    Reply = reply
                }
                |> ignore

                reply.Task.WaitAsync cancellation),
            broker.Trigger
        )

    let next () = requests.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds 5.)
    broker.Publish, listener, next

type BindingAddress = { ObjectId: string; Branch: string; Path: string }

/// A durable object with editable text and a constrained choice.
let bindingObject (al: MCP.AlMcpClient) : Task<BindingAddress> =
    task {
        let! result =
            al.QueryAl(
                """
                @aguil_xaml_example
                #{super => object,
                  ivars => [#{name => text, type => string},
                            #{name => choice, domain => ["foo", "bar"]}]}.
                new aguil_xaml_example #{text => "foo", choice => "foo"} Object.
                """,
                null
            )

        do! al.ReleaseContext(result.Context)

        let id =
            match binding "Object" result with
            | AlAtom id -> id
            | value -> failwithf "Expected a durable object, got %A" value

        let address: BindingAddress = { ObjectId = id; Branch = result.Branch; Path = "text" }
        return address
    }

type ObservationExample = {
    Client: MCP.AlMcpClient
    ObservationCache: ObservationCache
    Address: BindingAddress
}

/// A connection, durable object and shared slot collection to build examples on.
let observationExample () =
    task {
        let al = alClient ()
        let! address = bindingObject al
        return { Client = al; ObservationCache = ObservationCache(al); Address = address }
    }

/// A binding notification can open another slot on the same object.
let openSlotDuringNotification () =
    onUiAsync (fun () ->
        task {
            let! source = observationExample ()
            let address = source.Address
            let! slot = source.ObservationCache.Open(address.ObjectId, address.Path, address.Branch)

            use subscription =
                (slot :> System.ComponentModel.INotifyPropertyChanged)
                    .PropertyChanged.Subscribe(fun _ -> slot.Owner.Slot "choice" |> ignore)

            do! slot.Owner.Refresh()
            return slot.Owner.Slot "choice"
        })

/// Explicit collection for exploring weakly held objects without blocking the UI thread.
let collectGarbage () =
    task {
        GC.Collect()
        do! Task.Run(Action(GC.WaitForPendingFinalizers)).WaitAsync(TimeSpan.FromSeconds 5.)
        GC.Collect()
    }

/// Returns a weak handle to an unused reference, leaving the cache reusable.
let collectedReference (example: ObservationExample) =
    task {
        let address = example.Address
        // Keep the temporary slot out of the caller's async state.
        let! key =
            Task.Run(fun () ->
                let opened =
                    example.ObservationCache.Open(address.ObjectId, address.Path, address.Branch)

                let slot = opened.GetAwaiter().GetResult()
                slot.Pending.GetAwaiter().GetResult()

                WeakReference<AlMutableObjectSlot>(slot), WeakReference<AlMutableObject>(slot.Owner))

        do! collectGarbage ()
        return key
    }

/// An ordinary textbox using the same compiled path as al:Bind.
let boundTextBox (slot: AlMutableObjectSlot) =
    let textbox = TextBox()
    let source = Aguil.Bindings.ReferenceSource(Reference = slot)

    textbox.Bind(
        TextBox.TextProperty,
        Avalonia.Data.CompiledBinding.Create<Aguil.Bindings.ReferenceSource, obj>(
            (fun s -> s.Reference.Value),
            source
        )
    )
    |> ignore

    textbox

let waitFor condition =
    task {
        let deadline = DateTime.UtcNow.AddSeconds 10

        while not (condition ()) do
            if DateTime.UtcNow >= deadline then
                failwith "Binding did not reach the expected value."

            do! Task.Delay 10
    }

let alQuery (client: MCP.AlMcpClient) branch source bindings =
    task {
        let! result = client.QueryAlWith(source, branch, Map.ofList bindings)
        do! client.ReleaseContext(result.Context)
        return result
    }

/// Times the first visible REPL and the cost of a redundant viewport request.
let startupTimings () =
    onUiAsync (fun () ->
        task {
            let elapsed = Diagnostics.Stopwatch.StartNew()
            let timings = ResizeArray<string * TimeSpan>()

            let mark phase =
                timings.Add(phase, elapsed.Elapsed)
                elapsed.Restart()

            let client = alClient ()
            let size = Size(1100., 780.)
            use! workspace = WindowManager.Create(client, size)
            mark "Create workspace"
            let repl = Repl()
            mark "Construct REPL"

            let window =
                Window(Content = workspace.Content, Width = size.Width, Height = size.Height)

            window.Show()

            try
                do! workspace.Spawn(CSharpForward(repl, Title = "REPL"))
                do! waitFor (fun () -> repl.IsLoaded)
                mark "Spawn and display REPL"
                do! workspace.ViewPortChanged size
                do! workspace.Pending
                mark "Repeat viewport"
                let! inputs = client.PreviousInputs()
                mark $"Read {inputs.Length} prior inputs"
                return List.ofSeq timings
            finally
                window.Close()
        })

let observedList (client: MCP.AlMcpClient) (cache: ObservationCache) branch items =
    task {
        let! result =
            alQuery client branch "new observable_list Items List." [ "Items", AlList items ]

        let! reference =
            match binding "List" result with
            | AlAtom id -> cache.Open(id, result.Branch)
            | value -> failwithf "Expected a list object, got %A" value

        return reference :?> AlObservableList
    }

let changeList (list: AlObservableList) source =
    alQuery list.Client list.Branch source [ "List", list.Identifier ]

/// Another ordinary ListBox observing the same AL list, with its own item controls.
let listBinding (source: AlObservableList) =
    task {
        let! result =
            alQuery source.Client source.Branch "new brick_list Items Brick." [
                "Items", source.Identifier
            ]

        let! xaml = source.Client.Xaml(binding "Brick" result, source.Branch)
        let content = XamlView.Load(xaml) :?> ContentControl
        let window = Window(Content = content, Width = 400., Height = 300.)
        Inspector.SetInspection(window, Inspection(source.Identifier, None, source.Branch))
        window.Show()
        return window, content.Content :?> ListBox
    }

let listTextBoxes (view: ListBox) =
    view.GetVisualDescendants()
    |> Seq.choose (function
        | :? TextBox as text -> Some text
        | _ -> None)
    |> Seq.toList

type TextBinding = {
    Source: ObservationExample
    Window: Window
    TextBox: TextBox
} with

    interface IDisposable with
        member self.Dispose() = self.Window.Close()

/// Opens either XAML spelling against an existing object; returns the live view.
let textBinding (source: ObservationExample) propertyElement =
    task {
        let address = source.Address

        let content =
            if propertyElement then
                $"""<TextBox><TextBox.Text><al:BindExtension Path="{address.Path}" ObjectId="{address.ObjectId}"/></TextBox.Text><TextBox.PlaceholderText>hi</TextBox.PlaceholderText><TextBox.IsReadOnly>false</TextBox.IsReadOnly></TextBox>"""
            else
                $"""<TextBox PlaceholderText="hi" IsReadOnly="False" Text="{{al:Bind {address.Path}, ObjectId={address.ObjectId}}}"/>"""

        let view =
            PhlowBuilder.Build(
                Map.ofList [
                    AlAtom "view", AlText "xaml"
                    AlAtom "title", AlText "Binding example"
                    AlAtom "priority", AlInteger 10I
                    AlAtom "content", AlText content
                ]
            )

        let window = Window(Content = view.Content)
        let textbox = (view.Content :?> ContentControl).Content :?> TextBox
        Inspector.SetInspection(window, Inspection(AlAtom address.ObjectId, None, address.Branch))
        window.Show()

        try
            let! stored = source.Client.ReadSlot(address.ObjectId, address.Path, address.Branch)
            do! waitFor (fun () -> box textbox.Text = toNet stored.Value)
            return { Source = source; Window = window; TextBox = textbox }
        with error ->
            window.Close()
            return raise error
    }

/// Edits a live textbox and returns the value observed back in AL.
let editText (example: TextBinding) text =
    task {
        example.TextBox.Text <- text
        let address = example.Source.Address
        let read () = example.Source.Client.ReadSlot(address.ObjectId, address.Path, address.Branch)
        let! initial = read ()
        let mutable stored = initial
        let deadline = DateTime.UtcNow.AddSeconds 10

        while stored.Value <> AlText text do
            if DateTime.UtcNow >= deadline then
                failwith "Textbox edit did not reach AL."

            do! Task.Delay 10
            let! next = read ()
            stored <- next

        return stored
    }

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

    member private this.Logging name =
        { new ICommand with
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
        Branch = "main"
        Context = "example"
        HasPotentialSolution = false
    }

/// Parse a printed value through the same AL endpoint used by inspection.
let roundTrip value =
    let result =
        (alClient ()).QueryAlWith("= Value Input.", null, Map.ofList [ "Input", value ]).Result

    (result.Bindings.Values |> List.find (fun binding -> binding.Symbol = "Value")).Value

let printedRoundTrip value =
    let source = "= Value " + AlValue.Flat value + "."
    let result = (alClient ()).QueryAl(source, null).Result
    (result.Bindings.Values |> List.find (fun binding -> binding.Symbol = "Value")).Value

/// Atom names that require quoting, alone and as map keys.
let quotedValues () =
    let names = [
        "map"
        "#20"
        ""
        "with space"
        "a:b"
        "a\"b"
        "a\\b"
        "#{1 + 1}"
        "a\nb"
        "λ"
    ]

    [
        for name in names do
            AlAtom name
            AlMap(Map.ofList [ AlAtom name, AlText name ])
    ]

/// A retained source row in the history query's wire format.
let historyRow tx kind tag source =
    let origin =
        [ AlAtom "kind", AlAtom kind ]
        @ [ for value in Option.toList tag -> AlAtom "tag", AlText value ]

    AlList [ AlInteger tx; AlText source; AlMap(Map.ofList origin) ]

/// Authored inputs interleaved with an internal run and our own history query.
let historyRows () = [
    historyRow 1I "al_text" (Some MCP.History.tag) "= X 1."
    historyRow 2I "transaction_program" None "= X 2."
    historyRow 3I "al_text" None MCP.History.query
    historyRow 4I "al_text" None "vm_transaction_source Tx Text Origin."
    historyRow 5I "al_text" (Some MCP.History.tag) "= Y 2."
    historyRow 6I "al_text" (Some MCP.History.tag) "= X 1."
]

/// Filter a history result without contacting AL or depending on its stored inputs.
let historyInputs () = evaluation [ "Inputs", AlList(historyRows ()) ] |> MCP.History.fromQuery

/// An answer owns its inspection identity independently of its position in a result list.
let answer bindings = Answer({ Result = evaluation bindings; Views = [] })
