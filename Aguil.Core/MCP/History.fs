module Aguil.Core.MCP.History

open Aguil.Core.AlValues
open Aguil.Core.MCP.Evaluation

let query = "findall([tx, text, origin], inputs) do\n  vm_transaction_source(tx, text, origin)\nend"

/// Keep evaluated inputs in source order, excluding our own history query.
let fromQuery (result: EvaluationContext) =
    match result.Bindings.Values |> List.tryFind (fun b -> b.Symbol = "inputs") with
    | Some { Value = AlList rows } ->
        rows
        |> List.choose (function
            | AlList [ _; AlText text; AlMap origin ] when
                origin.TryFind(AlAtom "kind") = Some(AlAtom "eval_source") && text <> query
                ->
                Some text
            | _ -> None)
    | _ -> []
