module AvaloniaPlayground.Core.AlEvaluation

open AvaloniaPlayground.Core.AlValues

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
