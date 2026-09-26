using System;
using System.Collections.Immutable;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace AvaloniaPlayground.Models;

public class AlException(string message) : Exception(message);

public record Binding(string Symbol, object Value)
{
    public override string ToString() => $"{Symbol} = {Value}";
}

public record EvaluationContext(ImmutableList<Binding> Bindings)
{
    public override string ToString() =>
        $"EvaluationContext {{ {string.Join(", ", Bindings)} }}";
}

public class AlMcpClient(string url = "http://127.0.0.1:3031/mcp")
{
    private readonly HttpClient _httpClient = new HttpClient();
    private int _currentId = 0;

    private async Task<JsonElement> CallTool(string toolName, object arguments)
    {
        var request = new
        {
            jsonrpc = "2.0",
            id = _currentId++,
            method = "tools/call",
            @params = new { name = toolName, arguments = arguments },
        };

        var response = await _httpClient.PostAsJsonAsync(url, request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        var result = json.GetProperty("result");
        if (result.GetProperty("isError").GetBoolean())
            throw new AlException(result.GetProperty("content")[0].GetProperty("text").GetString()!);
        // non structured content is for the LLMS
        return json.GetProperty("result").GetProperty("structuredContent");
    }

    public async Task<EvaluationContext> QueryAl(string source, string? branch)
    {
        return new EvaluationContext((await CallTool("queryAL", new { source, branch }))
            .GetProperty("bindings")
            .EnumerateArray().Select(b =>
                new Binding(b.GetProperty("variable").GetProperty("name").GetString()!,
                    // We will want to match on the string return later for allocation
                    b.GetProperty("value").GetProperty("value").GetString()!
                ))
            .ToImmutableList());
    }
}