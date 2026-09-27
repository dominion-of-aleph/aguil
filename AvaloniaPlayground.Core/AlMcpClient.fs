namespace AvaloniaPlayground.Core

open System
open System.Net.Http
open System.Net.Http.Json
open System.Text.Json
open AvaloniaPlayground.Core.AlValues

type AlException(message: string) =
    inherit Exception(message)

type DecodeError =
    | UnknownType of string
    | UnknownEncoding of string

type Binding = {
    Symbol: string
    Value: Result<AlValue, DecodeError>
} with
    // slopped out
    member b.Pretty(width: int) =
        let prefix = $"{b.Symbol} = "

        let value =
            match b.Value with
            | Ok v -> AlValue.Pretty(width, prefix.Length, 0, v)
            | Error e -> $"<decode error: %A{e}>"

        prefix + value

    override b.ToString() = b.Pretty 80

type EvaluationContext = {
    Bindings: Binding list
} with
    // slopped out
    member c.Pretty(width: int) =
        c.Bindings |> List.map (fun b -> b.Pretty width) |> String.concat "\n"

    override c.ToString() = c.Pretty 80

type AlMcpClient(url: string) =
    let http = new HttpClient()
    let mutable currentId = 0

    let sequence (xs: Result<'a, 'e> list) : Result<'a list, 'e> =
        List.foldBack
            (fun x acc ->
                match x, acc with
                | Ok v, Ok vs -> Ok(v :: vs)
                | Error e, _ -> Error e
                | _, Error e -> Error e)
            xs
            (Ok [])

    let traverse f xs = xs |> List.map f |> sequence

    let rec mcp_decode_items (json: JsonElement) =
        json.GetProperty("items").EnumerateArray() |> List.ofSeq |> traverse mcp_decode_to_term

    and mcp_decode_to_term (json: JsonElement) : Result<AlValue, DecodeError> =
        let str (name: string) = json.GetProperty(name).GetString()

        match str "type" with
        | "variable" -> str "name" |> AlVar |> Ok
        | "atom" -> str "name" |> AlAtom |> Ok
        | "integer" -> str "value" |> bigint.Parse |> AlInteger |> Ok
        | "float" -> str "value" |> float |> AlFloat |> Ok
        | "binary" ->
            match str "encoding" with
            | "utf8" -> str "value" |> AlText |> Ok
            | "base64" -> str "value" |> Convert.FromBase64String |> AlBinary |> Ok
            | other -> Error(UnknownEncoding other)
        | "list" ->
            let items = mcp_decode_items json

            match json.TryGetProperty "tail" with
            | true, tail ->
                Result.bind
                    (fun t -> Result.map (fun i -> AlImproperList(i, t)) items)
                    (mcp_decode_to_term tail)
            | false, _ -> Result.map AlList items
        | "tuple" -> mcp_decode_items json |> Result.map AlTuple
        | "map" ->
            json.GetProperty("entries").EnumerateArray()
            |> List.ofSeq
            |> traverse (fun jtuple ->
                let key = mcp_decode_to_term (jtuple.GetProperty("key"))
                let value = mcp_decode_to_term (jtuple.GetProperty("value"))

                Result.bind (fun k -> Result.map (fun v -> k, v) value) key)
            |> Result.map (fun tup -> AlMap(Map.ofSeq tup))
        | other -> Error(UnknownType other)

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
                    Value = mcp_decode_to_term (b.GetProperty("value"))
                })
                |> List.ofSeq

            return { Bindings = bindings }
        }
