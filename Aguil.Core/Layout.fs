module Aguil.Core.Layout

/// Find a free row without moving other panes.
let vacant (occupied: seq<int>) row =
    let occupied = Set.ofSeq occupied

    Seq.initInfinite (fun offset -> row + offset)
    |> Seq.find (fun candidate -> not (Set.contains candidate occupied))
