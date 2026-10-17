module Aguil.Core.MCP.History

open Aguil.Core.AlValues
open Aguil.Core.MCP.Evaluation

let tag = "aguil/repl"
let query = "findall [Tx, Text, Origin] Inputs {vm_transaction_source Tx Text Origin}."

/// Keep evaluated inputs in source order, excluding our own history query.
let fromQuery (result: EvaluationContext) =
    match result.Bindings.Values |> List.tryFind (fun b -> b.Symbol = "Inputs") with
    | Some { Value = AlList rows } ->
        rows
        |> List.choose (function
            | AlList [ _; AlText text; AlMap origin ] when
                origin.TryFind(AlAtom "kind") = Some(AlAtom "al_text")
                && origin.TryFind(AlAtom "tag") = Some(AlText tag)
                ->
                Some text
            | _ -> None)
    | _ -> []
