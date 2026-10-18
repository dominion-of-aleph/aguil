module Aguil.Core.AlValues

open System
open System.Globalization
open System.Numerics

type AlValue =
    | AlAtom of string
    | AlInteger of bigint
    | AlFloat of float
    | AlText of string
    | AlBinary of byte[]
    | AlList of AlValue list
    | AlImproperList of AlValue list * AlValue
    | AlTuple of AlValue list
    | AlMap of Map<AlValue, AlValue>
    // because MCP is vibe coded
    | AlVar of string

    static member Tagged(v: AlValue) : obj =
        let items values = values |> List.map AlValue.Tagged |> List.toArray

        match v with
        | AlAtom name -> box {| ``type`` = "atom"; name = name |}
        | AlVar name -> box {| ``type`` = "variable"; name = name |}
        | AlInteger value -> box {| ``type`` = "integer"; value = string value |}
        | AlFloat value ->
            box {|
                ``type`` = "float"
                value = value.ToString("R", Globalization.CultureInfo.InvariantCulture)
            |}
        | AlText value -> box {| ``type`` = "binary"; encoding = "utf8"; value = value |}
        | AlBinary value ->
            box {|
                ``type`` = "binary"
                encoding = "base64"
                value = Convert.ToBase64String value
            |}
        | AlList values -> box {| ``type`` = "list"; items = items values |}
        | AlImproperList(values, tail) ->
            box {| ``type`` = "list"; items = items values; tail = AlValue.Tagged tail |}
        | AlTuple values -> box {| ``type`` = "tuple"; items = items values |}
        | AlMap fields ->
            let entries =
                fields
                |> Map.toArray
                |> Array.map (fun (key, value) -> {|
                    key = AlValue.Tagged key
                    value = AlValue.Tagged value
                |})

            box {| ``type`` = "map"; entries = entries |}

    // Slopped out AL Printer

    /// Single-line rendering of AL surface values.
    static member Flat(v: AlValue) : string =
        let join xs = xs |> List.map AlValue.Flat |> String.concat ", "

        match v with
        | AlAtom a -> AlValue.AtomName a
        | AlVar name -> name
        | AlInteger i -> string i
        | AlFloat f ->
            let s = f.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
            if s.Contains '.' || s.Contains 'E' then s else s + ".0"
        | AlText s -> AlValue.Quote s
        | AlBinary b -> $"<binary: {b.Length} bytes>"
        | AlList xs -> $"[{join xs}]"
        | AlImproperList(xs, tail) -> $"[{join xs} . {AlValue.Flat tail}]"
        | AlTuple xs -> $"<tuple: {join xs}>"
        | AlMap m ->
            m
            |> Map.toList
            |> List.map (fun (k, value) -> AlValue.Flat k + " => " + AlValue.Flat value)
            |> String.concat ", "
            |> sprintf "#{%s}"

    static member private Quote(s: string) : string =
        let escape =
            function
            | '\\' -> "\\\\"
            | '"' -> "\\\""
            | c when System.Char.IsControl c -> sprintf "\\u%04X" (int c)
            | c -> string c

        "\"" + (s |> Seq.map escape |> String.concat "").Replace("#{", "\\#{") + "\""

    static member private AtomName(a: string) : string =
        if System.Text.RegularExpressions.Regex.IsMatch(a, @"\A[a-z][a-zA-Z_0-9]*\z") then
            a
        else
            "'" + a.Replace("\\", "\\\\").Replace("'", "\\'") + "'"

    static member Pretty(width: int, column: int, indent: int, v: AlValue) : string =
        let flat = AlValue.Flat v

        if column + flat.Length <= width then
            flat
        else
            let inner = indent + 2
            let pad n = String.replicate n " "

            let block (openB: string) (closeB: string) (lines: string list) =
                let body = lines |> List.map (fun l -> pad inner + l) |> String.concat ",\n"
                $"{openB}\n{body}\n{pad indent}{closeB}"

            let items xs = xs |> List.map (fun x -> AlValue.Pretty(width, inner, inner, x))

            match v with
            | AlList xs -> block "[" "]" (items xs)
            | AlTuple _ -> flat
            | AlImproperList(xs, tail) ->
                let body = items xs |> List.map (fun l -> pad inner + l) |> String.concat ",\n"
                let tailLine = pad inner + ". " + AlValue.Pretty(width, inner + 2, inner, tail)
                $"[\n{body}\n{tailLine}\n{pad indent}]"
            | AlMap m ->
                m
                |> Map.toList
                |> List.map (fun (k, value) ->
                    let key = AlValue.Flat k + " => "
                    key + AlValue.Pretty(width, inner + key.Length, inner, value))
                |> block "#{" "}"
            | _ -> flat

    static member Pretty(width: int, v: AlValue) = AlValue.Pretty(width, 0, 0, v)

    override v.ToString() = AlValue.Pretty(80, v)

/// Converts a supported AL scalar to its .NET representation.
let toNet =
    function
    | AlText text -> box text
    | AlAtom "true" -> box true
    | AlAtom "false" -> box false
    | AlInteger number -> box number
    | AlFloat number -> box number
    | value -> raise (NotSupportedException($"Unsupported AL value: {value}"))

/// Converts a .NET value using an existing AL value as the type hint.
let fromNet (value: obj) =
    function
    | AlText _ -> AlText(if isNull value then "" else value :?> string)
    | AlAtom("true" | "false") ->
        AlAtom(
            if Convert.ToBoolean(value, CultureInfo.InvariantCulture) then
                "true"
            else
                "false"
        )
    | AlInteger _ ->
        AlInteger(
            BigInteger.Parse(
                Convert.ToString(value, CultureInfo.InvariantCulture),
                CultureInfo.InvariantCulture
            )
        )
    | AlFloat _ -> AlFloat(Convert.ToDouble(value, CultureInfo.InvariantCulture))
    | original -> raise (NotSupportedException($"Unsupported AL value: {original}"))

let maps value =
    match value with
    | AlList items ->
        items
        |> List.map (function
            | AlMap fields -> fields
            | _ -> invalidOp "Expected a map")
    | _ -> invalidOp "Expected a list of maps"

let text key fields =
    match Map.tryFind (AlAtom key) fields with
    | Some(AlText value) -> value
    | _ -> invalidArg key $"Field '{key}' must be a string."

let field key value =
    match value with
    | AlMap fields -> fields[AlAtom key]
    | _ -> invalidOp "Expected a map"

let number value =
    match value with
    | AlInteger integer -> float integer
    | AlFloat number -> number
    | _ -> invalidOp "Expected a number"
