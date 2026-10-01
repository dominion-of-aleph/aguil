using Aguil.Core;
using Xunit.Abstractions;

namespace Aguil.Tests;

public static class HistoryExamples
{
    // inputs evaluated in earlier sessions, oldest first; needs the AL server up
    public static async Task<List<string>> PreviousInputs() => [.. await new AlMcpClient().PreviousInputs()];
}

public class HistoryFacts(ITestOutputHelper output)
{
    [Fact]
    public async Task PreviousInputs()
    {
        var inputs = await HistoryExamples.PreviousInputs();
        output.WriteLine($"{inputs.Count} inputs, newest: " + string.Join(" | ", inputs.TakeLast(4)));
        Assert.Contains("label(y)", inputs);
        Assert.DoesNotContain(inputs, input => input.Contains("vm_transaction_source"));
    }
}
