using AvaloniaPlayground.Core;
using Xunit.Abstractions;

namespace AvaloniaPlayground.Tests;

public class UnitTest1(ITestOutputHelper output)
{
    // Sadly tests can't return so we relegate everything to non tests
    public string BasicBind()
    {
        return "x = y + 3; y = 2 * 5 + 20";
    }

    [Fact]
    public async Task BasicResolution()
    {
        var r = await new AlMcpClient().QueryAl(BasicBind(), null);
        output.WriteLine(r.ToString());
    }

    [Fact]
    public void Test1()
    {
    }
}