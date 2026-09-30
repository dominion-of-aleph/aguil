namespace AvaloniaPlayground.Editor

open System.Collections.Generic
open Xunit
open Xunit.Abstractions
open AvaloniaPlayground.Editor.Examples

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
    member _.SharedGrammar() =
        let grammar = elixirGrammar ()
        output.WriteLine $"{grammar.Highlights.Patterns.Count} highlight patterns"
        Assert.Same(grammar, elixirGrammar ())

    [<Fact>]
    member _.AssignmentTree() =
        use tree = assignmentTree ()

        Assert.Equal(
            "(source (binary_operator left: (identifier) right: (integer)))",
            see tree.RootNode.Expression
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
        Assert.Throws<KeyNotFoundException>(fun () -> Grammars.grammarFor "elixr" |> ignore) |> ignore
