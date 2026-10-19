namespace Aguil.Core

open System
open System.Collections.Concurrent
open System.Collections.Generic
open System.Threading.Tasks
open Aguil.Core.AlValues
open Aguil.Core.MCP

/// Shared objects on one binding synchronization context; controls belong to their views.
type ObservationCache(client: AlMcpClient) =
    let objects = ConcurrentDictionary<struct (string * string), WeakReference<AlMutableObject>>()

    let find key =
        match objects.TryGetValue key with
        | true, weak ->
            match weak.TryGetTarget() with
            | true, value -> Some value
            | _ -> None
        | _ -> None

    let head branch =
        if isNull branch then
            client.HeadBranch()
        else
            Task.FromResult branch

    let rec openObject (objectId: string) (branch: string) : Task<AlMutableObject> =
        task {
            let key = struct (branch, objectId)

            match find key with
            | Some reference -> return reference
            | None ->
                let weak = WeakReference<AlMutableObject>(Unchecked.defaultof<_>)
                let retire () = objects.TryRemove(KeyValuePair(key, weak)) |> ignore

                let resolve value =
                    task {
                        match value with
                        | AlAtom id when id <> "true" && id <> "false" ->
                            let! reference = openObject id branch
                            return box reference
                        | _ -> return toNet value
                    }

                let! snapshot = client.ReadObject(objectId, branch)

                let created =
                    if List.contains "observable_list" snapshot.Classes then
                        AlObservableList(client, objectId, branch, resolve, retire) :> AlMutableObject
                    else
                        AlMutableObject(client, objectId, branch, resolve, retire)

                created.Apply snapshot
                // Another opener may have completed while we fetched the snapshot.
                match find key with
                | Some reference -> return reference
                | None ->
                    weak.SetTarget created
                    objects[key] <- weak
                    created.Follow()
                    do! created.Refresh()
                    return created
        }

    member _.Open(objectId: string, branch: string) =
        task {
            let! branch = head branch
            return! openObject objectId branch
        }

    member _.Open(objectId: string, path: string, branch: string) =
        task {
            let! branch = head branch
            let! owner = openObject objectId branch
            let slot = owner.Slot path
            do! slot.Pending
            return slot
        }
