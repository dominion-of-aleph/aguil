module Aguil.Core.MCP.Evaluation

open Aguil.Core.AlValues

type DecodeError =
    | UnknownType of string
    | UnknownEncoding of string

type Binding<'a> = { Symbol: string; Value: 'a }

/// Evaluated Terms, preserving failures
type Decoded = { Values: Binding<AlValue> list; Failures: Binding<DecodeError> list }

let binding_to_query ({ Symbol = name; Value = v }: Binding<AlValue>) =
    "new(:view_builder, %{}, builder); "
    + $"{name} = {v}; findall(map, maps) do; view({name}, builder, phlow); to_map(phlow, map); end"


[<RequireQualifiedAccess>]
module Binding =
    let partition (xs: (string * Result<AlValue, DecodeError>) list) =
        let failures, values =
            List.foldBack
                (fun (sym, r) (errs, oks) ->
                    match r with
                    | Ok v -> errs, { Symbol = sym; Value = v } :: oks
                    | Error e -> { Symbol = sym; Value = e } :: errs, oks)
                xs
                ([], [])

        { Values = values; Failures = failures }

    // slopped out
    let pretty (sep: string) (width: int) (b: Binding<AlValue>) =
        let prefix = b.Symbol + sep
        prefix + AlValue.Pretty(width, prefix.Length, 0, b.Value)

    let prettyFailure (sep: string) (b: Binding<DecodeError>) =
        $"{b.Symbol}{sep}<decode error: %A{b.Value}>"

type EvaluationContext = {
    Bindings: Decoded
    Constraints: Decoded
    Store: Decoded
    // Perhaps change later it's a reference really
    Context: string
    // If any choicepoints are left, indicating potential solutions
    HasPotentialSolution: bool
} with
    // slopped out
    member c.Pretty(width: int) =
        let section sep (d: Decoded) =
            (d.Values |> List.map (Binding.pretty sep width))
            @ (d.Failures |> List.map (Binding.prettyFailure sep))

        section " = " c.Bindings @ section " : " c.Constraints @ section " => " c.Store
        |> String.concat "\n"
        |> fun body -> body + "\n" + c.Context

    override c.ToString() = c.Pretty 80

type Solution = { Result: EvaluationContext; Views: Map<string, Map<AlValue, AlValue> list> }
