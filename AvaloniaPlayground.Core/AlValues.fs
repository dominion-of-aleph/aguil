module AvaloniaPlayground.Core.AlValues

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

    // Slopped out AL Printer

    /// Single-line rendering in AL (Elixir-embedded) syntax.
    static member Flat(v: AlValue) : string =
        let join xs = xs |> List.map AlValue.Flat |> String.concat ", "

        match v with
        | AlAtom a -> $":{a}"
        | AlVar name -> name
        | AlInteger i -> string i
        | AlFloat f ->
            let s = f.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
            if s.Contains '.' || s.Contains 'E' then s else s + ".0"
        | AlText s -> $"\"{s}\""
        | AlBinary b -> $"<<{b.Length} bytes>>"
        | AlList xs -> $"[{join xs}]"
        | AlImproperList(xs, tail) -> $"[{join xs} | {AlValue.Flat tail}]"
        | AlTuple xs -> $"{{{join xs}}}"
        | AlMap m ->
            m
            |> Map.toList
            |> List.map (fun (k, value) -> AlValue.MapKey k + AlValue.Flat value)
            |> String.concat ", "
            |> sprintf "%%{%s}"

    static member private MapKey(k: AlValue) : string =
        match k with
        | AlAtom a -> $"{a}: "
        | _ -> $"{AlValue.Flat k} => "


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
            | AlTuple xs -> block "{" "}" (items xs)
            | AlImproperList(xs, tail) ->
                let body = items xs |> List.map (fun l -> pad inner + l) |> String.concat ",\n"
                let tailLine = pad inner + "| " + AlValue.Pretty(width, inner + 2, inner, tail)
                $"[\n{body}\n{tailLine}\n{pad indent}]"
            | AlMap m ->
                m
                |> Map.toList
                |> List.map (fun (k, value) ->
                    let key = AlValue.MapKey k
                    key + AlValue.Pretty(width, inner + key.Length, inner, value))
                |> block "%{" "}"
            | _ -> flat

    static member Pretty(width: int, v: AlValue) = AlValue.Pretty(width, 0, 0, v)

    override v.ToString() = AlValue.Pretty(80, v)
