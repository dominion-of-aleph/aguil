using Aguil.ViewModels;
using Xunit.Abstractions;

namespace Aguil.Tests;

public static class ReplExamples
{
    // what the input shows after each M-p/M-n, from a draft over three earlier inputs
    public static List<string> RecallWalk()
    {
        var repl = new ReplViewModel();
        foreach (var source in new[] { "a = 1", "b = 2", "c = 3" })
            repl.History.Add(new ReplFailure(source, ""));
        repl.Source = "draft";

        var previous = repl.PreviousInputCommand;
        var next = repl.NextInputCommand;
        var shown = new List<string>();
        foreach (var key in new[] { previous, previous, previous, previous, next, next, next })
        {
            key.Execute(null);
            shown.Add(repl.Source);
        }

        return shown;
    }
}

public class ReplFacts(ITestOutputHelper output)
{
    [Fact]
    public void RecallWalk()
    {
        var shown = ReplExamples.RecallWalk();
        output.WriteLine(string.Join(" | ", shown));
        Assert.Equal(["c = 3", "b = 2", "a = 1", "a = 1", "b = 2", "c = 3", "draft"], shown);
    }
}
