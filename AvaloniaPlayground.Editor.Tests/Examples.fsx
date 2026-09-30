// Prints each example in Examples.fs. Build first (dotnet build AvaloniaPlayground.slnx), then
//   dotnet fsi AvaloniaPlayground.Editor.Tests/Examples.fsx           prints every example
//   dotnet fsi --use:AvaloniaPlayground.Editor.Tests/Examples.fsx     the same, then a prompt:  show (docHeredoc ());;
// Examples.fs is loaded as source, so edits to it need no rebuild.

#r "bin/Debug/net10.0/Avalonia.Base.dll"
#r "bin/Debug/net10.0/AvaloniaEdit.dll"
#r "bin/Debug/net10.0/TreeSitter.dll"
#r "bin/Debug/net10.0/AvaloniaPlayground.Editor.dll"
#load "Examples.fs"

open AvaloniaPlayground.Editor
open AvaloniaPlayground.Editor.Examples

let show example = printfn "%s" (describe example)

printfn "elixirGrammar\n  %d highlight patterns" (elixirGrammar ()).Highlights.Patterns.Count
printfn "assignmentTree\n  %s" (assignmentTree ()).RootNode.Expression
printfn "assignmentCaptures\n  %A" (assignmentCaptures ())

for name, example in
    [
        "assignment", assignment
        "stringAcrossLines", stringAcrossLines
        "escapeInString", escapeInString
        "docHeredoc", docHeredoc
        "notADocHeredoc", notADocHeredoc
        "strayQuote", strayQuote
        "nonAscii", nonAscii
    ] do
    printfn "%s" name
    show (example ())
