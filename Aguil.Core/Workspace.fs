module Aguil.Core.Workspace

open Aguil.Core.AlValues

type Placement = {
    Id: AlValue
    Content: AlValue
    X: float
    Y: float
    Width: float
    Height: float
}

type Update = {
    Reset: bool
    Changed: Placement list
    Removed: AlValue list
    Width: float
    Height: float
    Xaml: string
}

let private windows =
    function
    | AlList items -> items
    | _ -> invalidOp "Expected windows."

let private placement window =
    let position = field "position" window
    let extent = field "extent" window

    {
        Id = field "id" window
        Content = field "content" window
        X = field "x" position |> number
        Y = field "y" position |> number
        Width = field "x" extent |> number
        Height = field "y" extent |> number
    }

let private update reset changed removed extent xaml = {
    Reset = reset
    Changed = windows changed |> List.map placement
    Removed = windows removed |> List.map (field "id")
    Width = field "x" extent |> number
    Height = field "y" extent |> number
    Xaml =
        match xaml with
        | AlText text -> text
        | _ -> invalidOp "Expected workspace XAML."
}

let snapshot state =
    update
        true
        (field "windows" (field "layout" state))
        (AlList [])
        (field "total_extent" state)
        (field "xaml" state)

/// Snapshots rebuild current membership; ordinary writes retain every published delta.
let changes (change: ObjectChange) =
    if change.Writes.IsEmpty then
        [ snapshot change.Value ]
    else
        change.Writes
        |> List.choose (Map.tryFind (AlAtom "changes"))
        |> List.map (fun delta ->
            update
                false
                (field "changed" delta)
                (field "removed" delta)
                (field "extent" delta)
                (field "xaml" delta))
