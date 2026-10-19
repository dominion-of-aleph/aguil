namespace Aguil.Core

open System
open System.Collections
open System.Collections.Generic
open System.Collections.ObjectModel
open System.ComponentModel
open System.Threading
open System.Threading.Tasks
open Aguil.Core.AlValues
open Aguil.Core.MCP

/// One observed object; it owns the slots its views bind to.
type AlMutableObject
    internal
    (
        client: AlMcpClient,
        objectId: string,
        branch: string,
        resolve: AlValue -> Task<obj>,
        retire: unit -> unit
    ) =
    let mutable current = Map.empty<AlValue, AlValue>
    let mutable cursor = -1L
    let mutable cleanup = ignore
    let slots = Dictionary<AlValue, AlMutableObjectSlot>()

    member _.Identifier = AlAtom objectId
    member _.ObjectId = objectId
    member _.Branch = branch
    member _.Client = client
    member _.Current = AlMap current
    member internal _.Resolve = resolve

    abstract Changed: Map<AlValue, AlValue> list -> unit
    default _.Changed _ = ()

    member self.Slot(path: string) =
        match slots.TryGetValue(AlAtom path) with
        | true, slot -> slot
        | _ ->
            let slot = AlMutableObjectSlot(self, path)
            slots[AlAtom path] <- slot
            slot.Apply()
            slot

    member private self.Publish(fields, writes) =
        current <- fields
        self.Changed writes
        let written = if List.isEmpty writes then [ fields ] else writes

        for KeyValue(key, slot) in Seq.toArray slots do
            if written |> List.exists (Map.containsKey key) then
                slot.Apply()

    member self.Apply(snapshot: ObjectSnapshot) =
        if snapshot.Cursor >= cursor then
            match snapshot.Value with
            | AlMap fields -> self.Publish(fields, [])
            | _ -> invalidOp "Expected object slots."

            cursor <- snapshot.Cursor

    member self.Refresh() =
        task {
            let! snapshot = client.ReadObject(objectId, branch)
            self.Apply snapshot
        }

    member private self.Update(batch: CommandBatch) =
        task {
            if batch.Cursor > cursor then
                if batch.From > cursor then
                    do! self.Refresh()
                else
                    let writes =
                        batch.Commands
                        |> List.choose (function
                            | AlTuple [ AlAtom "command"; AlInteger position; _; operation ] when
                                int64 position >= cursor
                                ->
                                match operation with
                                | AlTuple [ AlAtom "set_slot"
                                            AlTuple [ AlAtom owner; slot; value; _ ] ] when
                                    owner = objectId
                                    ->
                                    Some(Map.ofList [ slot, value ])
                                | AlTuple [ AlAtom "set_slots"
                                            AlTuple [ AlAtom owner; AlMap fields; _ ] ] when
                                    owner = objectId
                                    ->
                                    Some fields
                                | _ -> None
                            | _ -> None)

                    if not writes.IsEmpty then
                        let fields =
                            List.fold
                                (Map.fold (fun fields key value -> Map.add key value fields))
                                current
                                writes

                        self.Publish(fields, writes)

                    cursor <- batch.Cursor
        }

    member internal self.Follow() =
        let target = WeakReference<AlMutableObject>(self)
        let context = SynchronizationContext.Current
        // A local copy, so the long-lived subscription does not capture this object.
        let branch = branch

        let receive batch =
            match target.TryGetTarget() with
            | true, reference -> reference.Update batch |> ignore
            | _ -> ()

        let subscription =
            client.Commands.Publish
            |> Observable.filter (fun batch -> batch.Branch = branch)
            |> Observable.subscribe (fun batch ->
                if isNull context then
                    receive batch
                else
                    context.Post((fun _ -> receive batch), null))

        let watch = client.CommandLog.Watch(branch, cursor)

        cleanup <-
            fun () ->
                subscription.Dispose()
                watch.Dispose()

    override _.Finalize() =
        cleanup ()
        retire ()

and AlObservableList internal (client, objectId, branch, resolve, retire) =
    inherit AlMutableObject(client, objectId, branch, resolve, retire)
    let items = ObservableCollection<AlValue>()

    let reset values =
        items.Clear()

        for item in values do
            items.Add item

    member _.Items = items

    override self.Changed(writes) =
        try
            for changes in writes |> List.choose (Map.tryFind (AlAtom "changes")) do
                match changes with
                | AlList changes ->
                    for change in changes do
                        match change with
                        | AlList [ AlAtom "add"; AlInteger index; item ] ->
                            items.Insert(int index, item)
                        | AlList [ AlAtom "remove"; AlInteger index ] -> items.RemoveAt(int index)
                        | AlList [ AlAtom "reset"; AlList values ] -> reset values
                        | _ -> invalidOp $"Unknown list change: {change}"
                | _ -> invalidOp "Expected list changes."
        with :? ArgumentOutOfRangeException ->
            ()

        match field "contents" self.Current with
        | AlList expected ->
            if List.ofSeq items <> expected then
                reset expected
        | _ -> invalidOp "Expected list contents."

/// One bound slot, shared by every view of it; edits and confirmations run in order.
and AlMutableObjectSlot internal (owner: AlMutableObject, path: string) as self =
    let changed = Event<PropertyChangedEventHandler, PropertyChangedEventArgs>()
    let errors = Event<EventHandler<DataErrorsChangedEventArgs>, DataErrorsChangedEventArgs>()
    let mutable error: exn option = None
    let mutable pending = Task.CompletedTask
    let mutable value: obj = null
    let mutable confirmed: obj = null
    let mutable revision = 0L
    let read () = field path owner.Current

    let enqueue (action: unit -> Task) =
        let previous = pending
        let version = revision

        pending <-
            task {
                do! previous

                try
                    try
                        do! action ()
                        let! observed = owner.Resolve(read ())
                        confirmed <- observed
                    finally
                        // Newer edits win over older confirmations.
                        if version = revision then
                            value <- confirmed
                            changed.Trigger(self, PropertyChangedEventArgs("Value"))

                    error <- None
                with failure ->
                    error <- Some failure

                errors.Trigger(self, DataErrorsChangedEventArgs("Value"))
            }

    member _.Owner = owner
    member _.Path = path
    member _.Pending = pending
    member _.HasErrors = error.IsSome

    member internal _.Apply() = enqueue (fun () -> Task.CompletedTask)

    member _.Value
        with get () =
            match value with
            | :? AlObservableList as list -> box list.Items
            | _ -> value
        and set input =
            if not (Object.Equals(value, input)) || error.IsSome then
                value <- input
                revision <- revision + 1L

                enqueue (fun () ->
                    task {
                        let! _ =
                            owner.Client.WriteSlot(
                                owner.ObjectId,
                                path,
                                owner.Branch,
                                fromNet input (read ())
                            )

                        do! owner.Refresh()
                    })

    interface INotifyPropertyChanged with
        [<CLIEvent>]
        member _.PropertyChanged = changed.Publish

    interface INotifyDataErrorInfo with
        member _.HasErrors = error.IsSome
        member _.GetErrors _ = Option.toArray error :> IEnumerable

        [<CLIEvent>]
        member _.ErrorsChanged = errors.Publish
