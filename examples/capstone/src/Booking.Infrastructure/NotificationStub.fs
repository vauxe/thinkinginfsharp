namespace Booking.Infrastructure

open System
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open Booking.Domain.Ports

[<RequireQualifiedAccess>]
type NotificationStubBehavior =
    | Deliver
    | Fail of message: string

// #region notification-stub
type NotificationStub(behavior: NotificationStubBehavior) =
    let syncRoot = obj ()
    let calls = ResizeArray<NotificationRequest>()
    let mutable disposed = false

    let ensureActive () =
        if disposed then
            raise (ObjectDisposedException(nameof NotificationStub))

    member _.Calls: IReadOnlyList<NotificationRequest> =
        lock syncRoot (fun () -> calls.ToArray())

    member _.IsDisposed = lock syncRoot (fun () -> disposed)

    member _.Invoke (request: NotificationRequest) (cancellationToken: CancellationToken) : Task<unit> =
        task {
            cancellationToken.ThrowIfCancellationRequested()

            lock syncRoot (fun () ->
                ensureActive ()
                calls.Add request)

            match behavior with
            | NotificationStubBehavior.Deliver -> return ()
            | NotificationStubBehavior.Fail message -> return raise (InvalidOperationException message)
        }

    interface IDisposable with
        member _.Dispose() =
            lock syncRoot (fun () -> disposed <- true)
// #endregion notification-stub
