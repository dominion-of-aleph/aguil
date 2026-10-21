namespace Aguil.Core

open System
open System.Threading
open System.Threading.Tasks
open Aguil.Core.AlValues

type CommandBatch = { Branch: string; From: int64; Cursor: int64; Commands: AlValue list }

/// One reader per active branch; joining observers never rewind it.
type CommandLogListener
    (read: string -> int64 -> CancellationToken -> Task<CommandBatch>, publish: CommandBatch -> unit)
    =
    let gate = obj ()
    let mutable branches = Map.empty<string, int * CancellationTokenSource>

    let follow branch cursor (cancellation: CancellationTokenSource) =
        task {
            use cancellation = cancellation
            let mutable cursor = cursor

            while not cancellation.IsCancellationRequested do
                try
                    let! batch = read branch cursor cancellation.Token
                    cancellation.Token.ThrowIfCancellationRequested()
                    publish batch
                    cursor <- batch.Cursor
                with
                | :? OperationCanceledException when cancellation.IsCancellationRequested -> ()
                | _ -> do! Task.Delay 1000
        }

    member _.Watch(branch, cursor) =
        lock gate (fun () ->
            match branches.TryFind branch with
            | Some(count, cancellation) ->
                branches <- branches.Add(branch, (count + 1, cancellation))
            | None ->
                let cancellation = new CancellationTokenSource()
                branches <- branches.Add(branch, (1, cancellation))
                Task.Run(fun () -> follow branch cursor cancellation :> Task) |> ignore)

        let mutable disposed = false

        { new IDisposable with
            member _.Dispose() =
                lock gate (fun () ->
                    if not disposed then
                        disposed <- true
                        let count, cancellation = branches[branch]

                        if count = 1 then
                            branches <- branches.Remove branch
                            cancellation.Cancel()
                        else
                            branches <- branches.Add(branch, (count - 1, cancellation)))
        }
