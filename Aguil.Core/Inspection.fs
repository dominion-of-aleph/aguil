namespace Aguil.Core

/// An opening and its source, independent of where either is displayed.
type Inspection(target: obj, source: Inspection option, branch: string) =
    member _.Target = target
    member _.Source = source
    member _.Branch = branch
