namespace Aguil.Core

open System
open System.Net.Http
open System.Net.Http.Json
open System.Text.Json
open Aguil.Core.AlEvaluation
open Aguil.Core.AlValues

type AlException(message: string) =
    inherit Exception(message)

type AlMcpClient(url: string) =
    // AL keeps every source it evaluated, with where it came from
    static let previousInputsQuery =
        "findall([tx, text, origin], inputs) do\n  vm_transaction_source(tx, text, origin)\nend"

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
                let key = mcp_decode_to_term (jtuple.GetProperty "key")
                let value = mcp_decode_to_term (jtuple.GetProperty "value")

                Result.bind (fun k -> Result.map (fun v -> k, v) value) key)
            |> Result.map (fun tup -> AlMap(Map.ofSeq tup))
        | other -> Error(UnknownType other)

    // a failed run carries AL's reason, so show its message; compile errors are only text
    let failureMessage (result: JsonElement) =
        let text () = result.GetProperty("content").[0].GetProperty("text").GetString()

        match result.GetProperty("structuredContent").TryGetProperty "reason" with
        | true, reason ->
            match mcp_decode_to_term reason with
            | Ok(AlMap fields) ->
                match fields.TryFind(AlAtom "message") with
                | Some(AlText message) -> message
                | _ -> AlValue.Pretty(80, AlMap fields)
            | Ok other -> AlValue.Pretty(80, other)
            | Error _ -> text ()
        | _ -> text ()

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
                raise (AlException(failureMessage result))
            // non structured content is for the LLMs
            return result.GetProperty "structuredContent"
        }

    let grab (content: JsonElement) =
        content.EnumerateArray()
        |> Seq.map (fun b ->
            b.GetProperty("variable").GetProperty("name").GetString(),
            mcp_decode_to_term (b.GetProperty "value"))
        |> List.ofSeq
        |> Binding.partition

    let grab_store (content: JsonElement) =
        content.EnumerateArray()
        |> Seq.map (fun e ->
            let key = e.GetProperty "key"

            match mcp_decode_to_term key with
            | Ok k -> AlValue.Flat k, mcp_decode_to_term (e.GetProperty "value")
            | Error err -> key.GetRawText(), Error err)
        |> List.ofSeq
        |> Binding.partition

    let grab_context (content: JsonElement) = {
        Bindings = grab (content.GetProperty "bindings")
        Constraints = grab (content.GetProperty "constraints")
        Store = grab_store (content.GetProperty "store")
        Context = content.GetProperty("context").GetString()
        HasPotentialSolution = content.GetProperty("hasPotentialSolution").GetBoolean()
    }

    new() = AlMcpClient("http://127.0.0.1:3031/mcp")

    member _.QueryAl(source: string, branch: string) =
        task {
            let! content =
                callTool "queryAL" (box {| source = source; branch = Option.ofObj branch |})

            return grab_context content
        }

    member _.NextSolution(context: string) =
        task {
            let! content = callTool "nextSolution" (box {| context = context |})
            return grab_context content
        }

    /// Every input evaluated before, oldest first: AL's own runs and this query are left out.
    member this.PreviousInputs() =
        task {
            let! result = this.QueryAl(previousInputsQuery, null)

            let input row =
                match row with
                | AlList [ _; AlText text; AlMap origin ] when
                    origin.TryFind(AlAtom "kind") = Some(AlAtom "eval_source")
                    && text <> previousInputsQuery
                    ->
                    Some text
                | _ -> None

            return
                match result.Bindings.Values |> List.tryFind (fun b -> b.Symbol = "inputs") with
                | Some { Value = AlList rows } -> List.choose input rows
                | _ -> []
        }

    member _.DebugGrab(source: string, branch: string) =
        callTool "queryAL" (box {| source = source; branch = Option.ofObj branch |})
