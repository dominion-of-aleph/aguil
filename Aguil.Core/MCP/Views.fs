module Aguil.Core.MCP.Views

open Aguil.Core.MCP.Evaluation
open Aguil.Core.AlValues

let fromQuery (result: EvaluationContext) =
    (result.Bindings.Values |> List.find (fun b -> b.Symbol = "maps")).Value |> maps

let rows separator section = [
    for binding in section.Values ->
        AlMap(
            Map.ofList [
                AlAtom "text", AlText(Binding.pretty separator 80 binding)
                AlAtom "target", binding.Value
            ]
        )
    for failure in section.Failures ->
        AlMap(Map.ofList [ AlAtom "text", AlText(Binding.prettyFailure separator failure) ])
]

let raw result =
    Map.ofList [
        AlAtom "view", AlText "columned_list"
        AlAtom "title", AlText "Raw"
        AlAtom "priority", AlInteger 100I
        AlAtom "columns",
        AlList [
            AlMap(Map.ofList [ AlAtom "element", AlText "editor"; AlAtom "grammar", AlText "elixir" ])
        ]
        AlAtom "items",
        AlList(rows " = " result.Bindings @ rows " : " result.Constraints @ rows " => " result.Store)
    ]

let inspector binding views =
    Map.ofList [
        AlAtom "view", AlText "inspector"
        AlAtom "title", AlText binding.Symbol
        AlAtom "priority", AlInteger 200I
        AlAtom "target", binding.Value
        AlAtom "views", AlList(List.map AlMap views)
    ]

/// Describe the MCP envelope using the same view maps AL returns.
let fromSolution (solution: Solution) = [
    yield raw solution.Result
    for binding in solution.Result.Bindings.Values do
        yield inspector binding solution.Views[binding.Symbol]
]
