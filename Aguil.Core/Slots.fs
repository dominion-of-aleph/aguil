namespace Aguil.Core

open System
open System.Collections
open System.Collections.Concurrent
open System.Collections.Generic
open System.ComponentModel
open System.Threading.Tasks
open Aguil.Core.AlValues
open Aguil.Core.MCP

type SlotAddress = { Branch: string; ObjectId: string; Path: string }

/// Shared projection and pending edits, used on the binding's synchronization context.
type Slot internal (client: AlMcpClient, address: SlotAddress, retire: unit -> unit) as self =
    let changed = Event<PropertyChangedEventHandler, PropertyChangedEventArgs>()
    let errors = Event<EventHandler<DataErrorsChangedEventArgs>, DataErrorsChangedEventArgs>()
    let mutable value: obj = null
    let mutable original: AlValue option = None
    let mutable error: exn option = None
    let mutable revision = 0L
    let mutable pending = Task.CompletedTask

    let enqueue (action: unit -> Task<SlotSnapshot>) =
        let previous = pending
        let version = revision

        pending <-
            task {
                do! previous

                try
                    let! snapshot = action ()
                    let confirmed = toNet snapshot.Value
                    original <- Some snapshot.Value
                    // Editing bumps revisions, so we only update to newer values
                    if version = revision then
                        value <- confirmed
                        changed.Trigger(self, PropertyChangedEventArgs("Value"))

                    error <- None
                with failure ->
                    error <- Some failure

                    if version = revision then
                        original
                        |> Option.iter (fun confirmed ->
                            value <- toNet confirmed
                            changed.Trigger(self, PropertyChangedEventArgs("Value")))

                errors.Trigger(self, DataErrorsChangedEventArgs("Value"))
            }

    let read () = client.ReadSlot(address.ObjectId, address.Path, address.Branch)
    do enqueue read

    member _.Address = address
    member _.Pending = pending
    member _.HasErrors = error.IsSome
    member _.Refresh() = enqueue read

    override _.Finalize() = retire ()

    member _.Value
        with get () = value
        and set input =
            if not (Object.Equals(value, input)) || error.IsSome then
                value <- input
                revision <- revision + 1L

                enqueue (fun () ->
                    match original with
                    | Some original ->
                        client.WriteSlot(
                            address.ObjectId,
                            address.Path,
                            address.Branch,
                            fromNet input original
                        )
                    | None -> invalidOp "Cannot write a slot whose initial read failed.")

    interface INotifyPropertyChanged with
        [<CLIEvent>]
        member _.PropertyChanged = changed.Publish

    interface INotifyDataErrorInfo with
        member _.HasErrors = self.HasErrors
        member _.GetErrors _ = Option.toArray error :> IEnumerable

        [<CLIEvent>]
        member _.ErrorsChanged = errors.Publish

/// One slot projection per connection, branch, object and slot; controls do not own it.
type Slots(client: AlMcpClient) =
    let slots = Dictionary<SlotAddress, WeakReference<Slot>>()
    let retired = ConcurrentQueue<KeyValuePair<SlotAddress, WeakReference<Slot>>>()

    member _.Open(objectId: string, path: string, branch: string) =
        task {
            let! branch =
                if isNull branch then
                    client.HeadBranch()
                else
                    Task.FromResult branch

            let address = { Branch = branch; ObjectId = objectId; Path = path }

            let mutable entry = Unchecked.defaultof<KeyValuePair<SlotAddress, WeakReference<Slot>>>

            while retired.TryDequeue(&entry) do
                match slots.TryGetValue entry.Key with
                | true, current when Object.ReferenceEquals(current, entry.Value) ->
                    slots.Remove entry.Key |> ignore
                | _ -> ()

            let existing =
                match slots.TryGetValue address with
                | true, reference ->
                    match reference.TryGetTarget() with
                    | true, slot -> Some slot
                    | _ -> None
                | _ -> None

            match existing with
            | Some slot ->
                if slot.Pending.IsCompleted then
                    slot.Refresh()

                return slot
            | None ->
                let reference = WeakReference<Slot>(Unchecked.defaultof<Slot>)
                let entry = KeyValuePair(address, reference)
                let queue = retired
                let slot = Slot(client, address, fun () -> queue.Enqueue entry)
                reference.SetTarget slot
                slots[address] <- reference
                return slot
        }
