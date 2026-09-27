namespace AvaloniaPlayground.Core

open System
open System.Net.Http
open System.Net.Http.Json
open System.Text.Json

type AlException(message: string) =
    inherit Exception(message)

type Binding = {
    Symbol: string
    Value: string
} with

    override b.ToString() = $"{b.Symbol} = {b.Value}"

type EvaluationContext = {
    Bindings: Binding list
} with

    override c.ToString() =
        c.Bindings |> List.map string |> String.concat ", " |> sprintf "EvaluationContext { %s }"

type AlMcpClient(url: string) =
    let http = new HttpClient()
    let mutable currentId = 0

    let callTool (toolName: string) (arguments: obj) =
        task {
            let request = {|
                jsonrpc = "2.0"
                id = currentId
                method = "tools/call"
                ``params`` = {| name = toolName; arguments = arguments |}
            |}

            currentId <- currentId + 1

            let! response = http.PostAsJsonAsync(url, request)
            response.EnsureSuccessStatusCode() |> ignore
            let! json = response.Content.ReadFromJsonAsync<JsonElement>()

            let result = json.GetProperty "result"

            if result.GetProperty("isError").GetBoolean() then
                raise (AlException(result.GetProperty("content").[0].GetProperty("text").GetString()))
            // non structured content is for the LLMs
            return result.GetProperty "structuredContent"
        }

    new() = AlMcpClient("http://127.0.0.1:3031/mcp")

    member _.QueryAl(source: string, branch: string) =
        task {
            let! content =
                callTool "queryAL" (box {| source = source; branch = Option.ofObj branch |})

            let bindings =
                content.GetProperty("bindings").EnumerateArray()
                |> Seq.map (fun b -> {
                    Symbol = b.GetProperty("variable").GetProperty("name").GetString()
                    Value = b.GetProperty("value").GetProperty("value").GetString()
                })
                |> List.ofSeq

            return { Bindings = bindings }
        }
