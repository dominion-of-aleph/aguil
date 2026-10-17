using Aguil.Core.MCP;
using Xunit.Abstractions;

namespace Aguil.Tests;

public static class AlExamples
{
    // what AL says when a query fails while running; needs the AL server up
    public static async Task<string> FailureMessage(string source)
    {
        try
        {
            await new AlMcpClient().QueryAl(source, null);
            return "no failure";
        }
        catch (AlException e)
        {
            return e.Message;
        }
    }
}

public class AlFacts(ITestOutputHelper output)
{
    [Fact]
    public async Task UnboundLabel()
    {
        var message = await AlExamples.FailureMessage("label Y.");
        output.WriteLine(message);
        Assert.Contains("nothing to enumerate", message);
    }

    [Fact]
    public async Task SyntaxError()
    {
        var message = await AlExamples.FailureMessage("new point #{x => 1} Point");
        output.WriteLine(message);
        Assert.Equal("missing a full stop", message);
    }

    [Fact]
    public async Task PreviousInputsOnlyIncludeReplRuns()
    {
        var al = new AlMcpClient();
        var marker = Guid.NewGuid().ToString("N");
        var replSource = $"= Visible \"{marker}\".";
        var internalSource = $"= Internal \"{marker}\".";
        var repl = await al.QueryAlTagged(replSource, null, "aguil/repl");
        var internalResult = await al.QueryAl(internalSource, null);

        var inputs = await al.PreviousInputs();
        Assert.Contains(replSource, inputs);
        Assert.DoesNotContain(internalSource, inputs);

        await al.ReleaseContext(repl.Context);
        await al.ReleaseContext(internalResult.Context);
    }
}