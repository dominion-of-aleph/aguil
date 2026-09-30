/// Tree-sitter grammars by name. Each is built into the native tree-sitter library
/// (Native/tree-sitter.targets) and found by its exported symbol, which static linking (browser, iOS) needs.
module AvaloniaPlayground.Editor.Grammars

open System
open System.IO
open System.Runtime.InteropServices
open System.Threading.Tasks
open TreeSitter

[<DllImport("tree-sitter")>]
extern nativeint private tree_sitter_elixir()

/// A grammar's parse tables and compiled highlight query. Both are read-only, so all agents share one.
type Grammar = { Language: Language; Highlights: Query }

let private load (symbol: unit -> nativeint) (highlights: string) =
    lazy
        (Task.Run(fun () ->
            let language = new Language(symbol ())

            use reader =
                new StreamReader(typeof<Grammar>.Assembly.GetManifestResourceStream highlights)

            { Language = language; Highlights = new Query(language, reader.ReadToEnd()) }))

let private byName =
    dict [
        "elixir", load (fun () -> tree_sitter_elixir ()) "tree-sitter-elixir/highlights.scm"
    ]

/// Loads on first request, on a pool thread (~65 ms to compile the query). Callers share the task,
/// so agents started meanwhile don't block threads.
let grammarFor (name: string) = byName[name].Value
