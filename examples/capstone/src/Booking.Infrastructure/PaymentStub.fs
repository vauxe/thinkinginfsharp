namespace Booking.Infrastructure

open System
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open Booking.Domain.Ports

[<RequireQualifiedAccess>]
type PaymentStubBehavior =
    | Authorize of transactionId: string
    | Decline of reason: string
    | Fail of message: string

// #region payment-stub
type PaymentStub(behavior: PaymentStubBehavior) =
    let syncRoot = obj ()
    let calls = ResizeArray<PaymentRequest>()
    let mutable disposed = false

    let ensureActive () =
        if disposed then
            raise (ObjectDisposedException(nameof PaymentStub))

    member _.Calls: IReadOnlyList<PaymentRequest> =
        lock syncRoot (fun () -> calls.ToArray())

    member _.IsDisposed = lock syncRoot (fun () -> disposed)

    member _.Invoke (request: PaymentRequest) (cancellationToken: CancellationToken) : Task<PaymentOutcome> =
        task {
            cancellationToken.ThrowIfCancellationRequested()

            lock syncRoot (fun () ->
                ensureActive ()
                calls.Add request)

            match behavior with
            | PaymentStubBehavior.Authorize transactionId -> return Authorized transactionId
            | PaymentStubBehavior.Decline reason -> return Declined reason
            | PaymentStubBehavior.Fail message -> return raise (InvalidOperationException message)
        }

    interface IDisposable with
        member _.Dispose() =
            lock syncRoot (fun () -> disposed <- true)
// #endregion payment-stub
