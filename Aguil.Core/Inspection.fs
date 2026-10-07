namespace Aguil.Core

/// An opening and its source, independent of where either is displayed.
type Inspection(target: obj, source: Inspection option) =
    member _.Target = target
    member _.Source = source
