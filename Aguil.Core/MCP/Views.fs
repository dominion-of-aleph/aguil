module Aguil.Core.MCP.Views

open Aguil.Core.MCP.Evaluation
open Aguil.Core.AlValues

let fromQuery output (result: EvaluationContext) = binding output result |> maps

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

let rawFields result =
    Map.ofList [
        AlAtom "title", AlText "Raw"
        AlAtom "priority", AlInteger 100I
        AlAtom "show_headers", AlAtom "false"
        AlAtom "columns", AlList [ AlMap(Map.ofList [ AlAtom "cell", AlAtom "brick_editor_cell" ]) ]
        AlAtom "items",
        AlList(rows " = " result.Bindings @ rows " : " result.Constraints @ rows " => " result.Store)
    ]

let inspector branch binding =
    Map.ofList [
        AlAtom "view", AlText "inspect"
        AlAtom "title", AlText binding.Symbol
        AlAtom "priority", AlInteger 200I
        AlAtom "target", binding.Value
        AlAtom "branch", AlText branch
    ]
