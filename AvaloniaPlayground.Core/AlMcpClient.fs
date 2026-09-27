namespace AvaloniaPlayground.Core

open System
open System.Net.Http
open System.Net.Http.Json
open System.Text.Json
open AvaloniaPlayground.Core.AlValues

type AlException(message: string) =
    inherit Exception(message)

type Binding = {
    Symbol: string
    Value: AlValue option
} with
    // slopped out
    member b.Pretty(width: int) =
        let prefix = $"{b.Symbol} = "

        let value =
            b.Value
            |> Option.map (fun v -> AlValue.Pretty(width, prefix.Length, 0, v))
            |> Option.defaultValue "<Decoding Error>"

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
    // refactor to return an either Result inside the Task, rather than throwing
    let sequence (xs: 'a option list) : 'a list option =
        List.foldBack
            (fun x acc ->
                match x, acc with
                | Some v, Some vs -> Some(v :: vs)
                | _ -> None)
            xs
            (Some [])

    let traverse f xs = xs |> List.map f |> sequence

    let rec mcp_decode_items (json: JsonElement) =
        json.GetProperty("items").EnumerateArray() |> List.ofSeq |> traverse mcp_decode_to_term
    // Make into an either, with noting what failed where
    and mcp_decode_to_term (json: JsonElement) : AlValue option =
        let str (name: string) = json.GetProperty(name).GetString()

        match str "type" with
        | "variable" -> str "name" |> AlVar |> Some
        | "atom" -> str "name" |> AlAtom |> Some
        | "integer" -> str "value" |> bigint.Parse |> AlInteger |> Some
        | "float" -> str "value" |> float |> AlFloat |> Some
        // TODO :: cover the base64 encoding, match on encoding
        | "binary" when str "encoding" = "base64" ->
            str "value" |> System.Convert.FromBase64String |> AlBinary |> Some
        | "binary" -> str "value" |> AlText |> Some
        | "list" ->
            let items = mcp_decode_items json

            match json.TryGetProperty "tail" with
            | true, tail ->
                Option.bind
                    (fun t -> Option.map (fun i -> AlImproperList(i, t)) items)
                    (mcp_decode_to_term tail)
            | false, _ -> Option.map AlList items
        | "tuple" -> Option.map AlTuple (mcp_decode_items json)
        | "map" ->
            json.GetProperty("entries").EnumerateArray()
            |> List.ofSeq
            |> traverse (fun jtuple ->
                let key = mcp_decode_to_term (jtuple.GetProperty("key"))
                let value = mcp_decode_to_term (jtuple.GetProperty("value"))

                Option.bind (fun k -> Option.map (fun v -> k, v) value) key)
            |> Option.map (fun tup -> AlMap(Map.ofSeq tup))
        | _ -> None

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
