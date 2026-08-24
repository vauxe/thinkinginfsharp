namespace Booking.Infrastructure

open System
open System.Threading
open System.Threading.Tasks
open Booking.Domain
open Booking.Domain.Ports
open Booking.Domain.Workflow

[<Sealed>]
type BookingStoreAdapterException(storeError: BookingStoreError) =
    inherit Exception("The booking snapshot operation failed.")

    member _.StoreError = storeError

// #region infrastructure-composition
type InfrastructureComposition
    internal
    (
        configuration: BookingStoreConfiguration,
        paymentBehavior: PaymentStubBehavior,
        notificationBehavior: NotificationStubBehavior,
        getUtcNow: CancellationToken -> Task<DateTimeOffset>
    ) =

    let syncRoot = obj ()
    let store = FileBookingStore configuration
    let payment = new PaymentStub(paymentBehavior)
    let notification = new NotificationStub(notificationBehavior)
    let mutable disposed = false

    let ensureActive (cancellationToken: CancellationToken) =
        cancellationToken.ThrowIfCancellationRequested()

        lock syncRoot (fun () ->
            if disposed then
                raise (ObjectDisposedException(nameof InfrastructureComposition)))

    let unwrapStoreResult result =
        match result with
        | Ok value -> value
        | Error error -> raise (BookingStoreAdapterException error)

    let ports: AsyncPorts =
        { LoadBooking =
            fun requestId cancellationToken ->
                task {
                    ensureActive cancellationToken
                    let! stored = store.Load cancellationToken

                    return
                        match unwrapStoreResult stored with
                        | Some booking when Booking.requestId booking = requestId -> Booked booking
                        | Some _
                        | None -> NotBooked
                }
          AppendEvent =
            fun requestId bookingEvent cancellationToken ->
                task {
                    ensureActive cancellationToken
                    let booking = BookingEvent.booking bookingEvent

                    if Booking.requestId booking <> requestId then
                        invalidArg (nameof requestId) "The event request ID must match the storage key."

                    let! saved = store.Save(booking, cancellationToken)
                    return unwrapStoreResult saved
                }
          Charge =
            fun request cancellationToken ->
                ensureActive cancellationToken
                payment.Invoke request cancellationToken
          Notify =
            fun request cancellationToken ->
                ensureActive cancellationToken
                notification.Invoke request cancellationToken
          GetUtcNow =
            fun cancellationToken ->
                ensureActive cancellationToken
                getUtcNow cancellationToken }

    member _.Ports = ports
    member _.PaymentStub = payment
    member _.NotificationStub = notification
    member _.IsDisposed = lock syncRoot (fun () -> disposed)

    interface IDisposable with
        member _.Dispose() =
            let shouldDispose =
                lock syncRoot (fun () ->
                    if disposed then
                        false
                    else
                        disposed <- true
                        true)

            if shouldDispose then
                (notification :> IDisposable).Dispose()
                (payment :> IDisposable).Dispose()

[<RequireQualifiedAccess>]
module Composition =
    // The returned composition creates and owns both stubs; dispose it at the application boundary.
    let start configuration paymentBehavior notificationBehavior getUtcNow =
        new InfrastructureComposition(configuration, paymentBehavior, notificationBehavior, getUtcNow)
// #endregion infrastructure-composition
