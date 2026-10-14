using Aguil.Core;
using Aguil.Core.MCP;
using Xunit.Abstractions;

namespace Aguil.Tests;

public class UnitTest1(ITestOutputHelper output)
{
    // Sadly tests can't return so we relegate everything to non tests
    public string BasicBind()
    {
        return "= Y 30. = X (+ Y 3).";
    }

    [Fact]
    public async Task BasicResolution()
    {
        var r = await new AlMcpClient().QueryAl(BasicBind(), null);
        output.WriteLine(r.ToString());
        Assert.Equal(AlValues.AlValue.NewAlInteger(33), Evaluation.binding("X", r));
    }

    [Fact]
    public void Test1()
    {
    }
}
