// Prints each example in Examples.fs. Build first (dotnet build Aguil.slnx), then
//   dotnet fsi Aguil.Editor.Tests/Examples.fsx           prints every example
//   dotnet fsi --use:Aguil.Editor.Tests/Examples.fsx     the same, then a prompt:  show (docHeredoc ());;
// Examples.fs is loaded as source, so edits to it need no rebuild.

#r "bin/Debug/net10.0/Avalonia.Base.dll"
#r "bin/Debug/net10.0/AvaloniaEdit.dll"
#r "bin/Debug/net10.0/TreeSitter.dll"
#r "bin/Debug/net10.0/Avalonia.Controls.dll"
#r "bin/Debug/net10.0/Avalonia.Headless.dll"
#r "bin/Debug/net10.0/Avalonia.Markup.Xaml.dll"
#r "bin/Debug/net10.0/Avalonia.Themes.Fluent.dll"
#r "bin/Debug/net10.0/HarfBuzzSharp.dll"
#r "bin/Debug/net10.0/Aguil.Editor.dll"
#load "Examples.fs"

open System
open System.IO
open System.Runtime.InteropServices

// dotnet fsi doesn't read the test project's dependency file, so point HarfBuzz at its native copy
let private native =
    let os =
        if OperatingSystem.IsWindows() then "win"
        elif OperatingSystem.IsMacOS() then "osx"
        else "linux"

    Path.Combine(
        __SOURCE_DIRECTORY__,
        "bin/Debug/net10.0/runtimes",
        $"{os}-{RuntimeInformation.OSArchitecture}".ToLowerInvariant(),
        "native"
    )

NativeLibrary.SetDllImportResolver(
    typeof<HarfBuzzSharp.Buffer>.Assembly,
    fun name _ _ ->
        Directory.GetFiles(native, name + ".*")
        |> Array.tryHead
        |> Option.map NativeLibrary.Load
        |> Option.defaultValue 0n
)

open Aguil.Editor
open Aguil.Editor.Examples

let show example = printfn "%s" (describe example)

printfn "elixirGrammar\n  %d highlight patterns" (elixirGrammar ()).Highlights.Patterns.Count
printfn "assignmentTree\n  %s" (assignmentTree ()).RootNode.Expression
printfn "assignmentCaptures\n  %A" (assignmentCaptures ())
printfn "editorCommandNames\n  %A" (editorCommandNames ())
printfn "lineKeys\n  %A" (lineKeys ())
printfn "contextCommand\n  %A" (contextCommand ())

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
