module Aguil.Core.AlViews

open Aguil.Core.AlEvaluation
open Aguil.Core.AlValues

let descriptions (result: EvaluationContext) =
    match (result.Bindings.Values |> List.find (fun b -> b.Symbol = "maps")).Value with
    | AlList views ->
        views
        |> List.map (function
            | AlMap fields -> fields
            | _ -> invalidOp "Expected a view map")
    | _ -> invalidOp "Expected a list of views"

let raw binding =
    Map.ofList [
        AlAtom "view", AlText "text"
        AlAtom "title", AlText "Raw"
        AlAtom "priority", AlInteger 100I
        AlAtom "text", AlText(Binding.pretty " = " 80 binding)
        AlAtom "grammar", AlText "elixir"
        AlAtom "target", binding.Value
    ]

let forBinding binding result =
    descriptions result @ [ raw binding ]
    |> List.sortBy (fun fields ->
        match fields[AlAtom "priority"] with
        | AlInteger priority -> priority
        | _ -> invalidOp "Expected an integer view priority")
