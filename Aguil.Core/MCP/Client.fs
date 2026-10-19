namespace Aguil.Core.MCP

open System
open System.Collections.Generic
open System.Net.Http
open System.Net.Http.Json
open System.Text.Json
open System.Threading
open System.Threading.Tasks
open Aguil.Core.MCP.Evaluation
open Aguil.Core.AlValues
open Aguil.Core

type AlException(message: string) =
    inherit Exception(message)

type SlotSnapshot = { Branch: string; Value: AlValue }
type ObjectSnapshot = { Cursor: int64; Classes: string list; Value: AlValue }

type AlMcpClient(url: string) as self =
    let http = new HttpClient()
    let mutable currentId = 0
    let commands = Event<CommandBatch>()

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

        let reason =
            match result.TryGetProperty "structuredContent" with
            | true, content when content.ValueKind = JsonValueKind.Object ->
                content.TryGetProperty "reason"
            | _ -> false, Unchecked.defaultof<JsonElement>

        match reason with
        | true, reason ->
            match mcp_decode_to_term reason with
            | Ok(AlMap fields) ->
                match fields.TryFind(AlAtom "message") with
                | Some(AlText message) -> message
                | _ -> AlValue.Pretty(80, AlMap fields)
            | Ok other -> AlValue.Pretty(80, other)
            | Error _ -> text ()
        | _ -> text ()

    let callToolWithCancellation
        (toolName: string)
        (arguments: obj)
        (cancellation: CancellationToken)
        =
        task {
            let request = {|
                jsonrpc = "2.0"
                id = Interlocked.Increment(&currentId)
                method = "tools/call"
                ``params`` = {| name = toolName; arguments = arguments |}
            |}

            use! response = http.PostAsJsonAsync(url, request, cancellation)
            response.EnsureSuccessStatusCode() |> ignore
            let! json = response.Content.ReadFromJsonAsync<JsonElement>(cancellation)

            match json.TryGetProperty "error" with
            | true, error -> raise (AlException(error.ToString()))
            | _ -> ()

            let result = json.GetProperty "result"

            if result.GetProperty("isError").GetBoolean() then
                raise (AlException(failureMessage result))
            // non structured content is for the LLMs
            return result.GetProperty "structuredContent"
        }

    let callTool name arguments = callToolWithCancellation name arguments CancellationToken.None

    let decode value =
        match mcp_decode_to_term value with
        | Ok value -> value
        | Error error -> invalidOp (string error)

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
        Branch = content.GetProperty("branch").GetString()
        Context = content.GetProperty("context").GetString()
        HasPotentialSolution = content.GetProperty("hasPotentialSolution").GetBoolean()
    }

    new() = AlMcpClient("http://127.0.0.1:3031/mcp")

    member _.Commands = commands

    member val CommandLog =
        CommandLogListener(
            (fun branch cursor cancellation -> self.ReadCommands(branch, cursor, cancellation)),
            commands.Trigger
        )

    member _.ReadObject(objectId: string, branch: string) =
        task {
            let! content = callTool "readObject" {| ``object`` = objectId; branch = branch |}

            return {
                Cursor = content.GetProperty("cursor").GetInt64()
                Classes =
                    content.GetProperty("classes").EnumerateArray()
                    |> Seq.map _.GetString()
                    |> List.ofSeq
                Value = decode (content.GetProperty "value")
            }
        }

    member _.ReadCommands(branch: string, cursor: int64, cancellation: CancellationToken) =
        task {
            let! content =
                callToolWithCancellation
                    "observeCommands"
                    {| cursors = dict [ branch, cursor ] |}
                    cancellation

            return
                decode (content.GetProperty "batches")
                |> maps
                |> List.map (fun batch ->
                    match
                        batch[AlAtom "branch"], batch[AlAtom "cursor"], batch[AlAtom "commands"]
                    with
                    | AlText branch, AlInteger next, AlList commands -> {
                        Branch = branch
                        From = cursor
                        Cursor = int64 next
                        Commands = commands
                      }
                    | _ -> invalidOp "Expected a command batch.")
                |> List.exactlyOne
        }

    member self.Xaml(value: AlValue, branch: string) =
        task {
            let! result =
                self.QueryAlWith("to_xaml Object Text.", branch, Map.ofList [ "Object", value ])

            do! self.ReleaseContext(result.Context)

            match binding "Text" result with
            | AlText text -> return text
            | value -> return invalidOp $"Expected XAML text, got {value}"
        }

    member _.HeadBranch() =
        task {
            let! content = callTool "listBranches" (box (Dictionary<string, obj>()))

            return
                content.GetProperty("branches").EnumerateArray()
                |> Seq.find _.GetProperty("head").GetBoolean()
                |> _.GetProperty("id").GetString()
        }

    member _.QueryAl(source: string, branch: string) =
        task {
            let! content =
                callTool "queryAL" (box {| source = source; branch = Option.ofObj branch |})

            return grab_context content
        }

    member _.QueryAlTagged(source: string, branch: string, sourceTag: string) =
        task {
            let! content =
                callTool
                    "queryAL"
                    (box {| source = source; branch = Option.ofObj branch; sourceTag = sourceTag |})

            return grab_context content
        }

    member _.QueryAlWith
        (source: string, branch: string, bindings: IReadOnlyDictionary<string, AlValue>)
        =
        task {
            let encoded =
                bindings |> Seq.map (fun entry -> entry.Key, AlValue.Tagged entry.Value) |> dict

            let! content =
                callTool
                    "queryAL"
                    (box {| source = source; branch = Option.ofObj branch; bindings = encoded |})

            return grab_context content
        }

    member self.QueryAlViews(source: string, branch: string) =
        task {
            let! results = self.QueryAlTagged(source, branch, History.tag)
            let! views = self.AlViewsFromQuery(results, results.Branch)
            return results, views
        }

    member private self.QuerySlot(source, objectId, path, branch, value) =
        task {
            let arguments = Map.ofList [ "Object", AlAtom objectId; "Slot", AlAtom path ]

            let arguments =
                match value with
                | Some value -> arguments.Add("Input", value)
                | None -> arguments

            let! result = self.QueryAlWith(source, branch, arguments)
            do! self.ReleaseContext(result.Context)

            return { Branch = result.Branch; Value = binding "Value" result }
        }

    member self.ReadSlot(objectId: string, path: string, branch: string) =
        self.QuerySlot("get Object Slot Value.", objectId, path, branch, None)

    member self.WriteSlot(objectId: string, path: string, branch: string, value: AlValue) =
        self.QuerySlot(
            "set_slot Object Slot Input, get Object Slot Value.",
            objectId,
            path,
            branch,
            Some value
        )

    member self.RunSlot(objectId: string, path: string, branch: string) =
        task {
            let! result =
                self.QueryAlWith(
                    "get Object Slot Action, run Action [].",
                    branch,
                    Map.ofList [ "Object", AlAtom objectId; "Slot", AlAtom path ]
                )

            do! self.ReleaseContext(result.Context)
        }

    member self.Inspect(value: AlValue, branch: string) =
        task {
            let query = view_query
            let! result = self.QueryAlWith(query.Source, branch, Map.ofList [ "Target", value ])
            let views = Views.fromQuery query.Output result
            do! self.ReleaseContext(result.Context)
            return views
        }

    member self.AlViewsFromQuery(results: EvaluationContext, branch: string) =
        task {
            let branch = if isNull branch then results.Branch else branch

            let! views =
                results.Bindings.Values
                |> Seq.map (fun b ->
                    task {
                        let! views = self.Inspect(b.Value, branch)
                        return b.Symbol, views
                    })
                |> Task.WhenAll

            return Map.ofArray views
        }

    member _.NextSolution(context: string) =
        task {
            let! content = callTool "nextSolution" (box {| context = context |})
            return grab_context content
        }

    member _.ReleaseContext(context: string) =
        task {
            if not (String.IsNullOrEmpty context) then
                let! _ = callTool "releaseContext" (box {| context = context |})
                ()
        }

    /// Every input evaluated before, oldest first: AL's own runs and this query are left out.
    member this.PreviousInputs() =
        task {
            let! result = this.QueryAl(History.query, null)
            let inputs = History.fromQuery result
            do! this.ReleaseContext(result.Context)
            return inputs
        }

    member _.DebugGrab(source: string, branch: string) =
        callTool "queryAL" (box {| source = source; branch = Option.ofObj branch |})
