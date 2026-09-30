/// Tree-sitter grammars by name. Each is compiled into the native "tree-sitter" library
/// (Native/tree-sitter.targets) and reached through its exported symbol rather than a file path,
/// so the same code works where native code is linked statically (browser, iOS).
module AvaloniaPlayground.Editor.Grammars

open System
open System.IO
open System.Runtime.InteropServices
open System.Threading.Tasks
open TreeSitter

[<DllImport("tree-sitter")>]
extern nativeint private tree_sitter_elixir()

/// A grammar's parse tables and compiled highlight query. Both are read-only once built, so one
/// serves every agent at once.
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

/// The first request starts loading on a pool thread (compiling the highlight query is ~65 ms).
/// Every caller awaits the same task, so agents starting meanwhile don't each hold a thread.
let grammarFor (name: string) = byName[name].Value
