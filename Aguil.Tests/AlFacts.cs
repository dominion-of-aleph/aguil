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
        var message = await AlExamples.FailureMessage("label(y)");
        output.WriteLine(message);
        Assert.Equal("label(:\"$y\") has nothing to enumerate: no finite bounds, domain, or class.", message);
    }

    [Fact]
    public async Task SyntaxError()
    {
        var message = await AlExamples.FailureMessage("x = = 1");
        output.WriteLine(message);
        Assert.Equal("syntax error before: '='", message);
    }
}