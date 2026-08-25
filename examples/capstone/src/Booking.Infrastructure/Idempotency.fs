namespace Booking.Infrastructure

open System
open System.Globalization
open System.Security.Cryptography
open System.Text
open System.Threading
open System.Threading.Tasks
open Booking.Domain
open Booking.Domain.Ports
open Booking.Domain.Validation

[<RequireQualifiedAccess>]
type BookingConsistencyError =
    | DecisionRejected of BookingDecisionError
    | AggregateCapacityExceeded of requested: int * remaining: int
    | IdempotencyConflict
    | PreviousOperationIncomplete
    | PaymentDeclined
    | PaymentOutcomeUnknown
    | DependencyUnavailable
    | StorageUnavailable of BookingStoreError

type private PreparedCommand =
    { Identity: AtomicCommandIdentity
      Command: BookingCommand
      Payment: PaymentRequest option }

module private CommandIdentity =
    let private fingerprint (fields: string list) =
        let canonical = StringBuilder()

        for field in fields do
            canonical.Append(field.Length).Append(':').Append(field).Append('|') |> ignore

        canonical.ToString()
        |> Encoding.UTF8.GetBytes
        |> SHA256.HashData
        |> Convert.ToHexString

    let private identity (kind: AtomicOperationKind) (requestId: RequestId) (payload: string) : AtomicCommandIdentity =
        let kindName = AtomicStoreImplementation.kindText kind
        let normalizedRequestId = RequestId.value requestId

        { Key = AtomicStoreImplementation.operationKey kind requestId
          RequestId = requestId
          Kind = kind
          Fingerprint = fingerprint [ kindName; normalizedRequestId; payload ] }

    let prepare (command: BookingCommand) : Result<PreparedCommand, BookingConsistencyError> =
        match command with
        | BookingCommand.Place raw ->
            Validation.validatePlaceBooking raw
            |> Result.map (fun valid ->
                let requestId = ValidPlaceBooking.requestId valid
                let seats = ValidPlaceBooking.seats valid
                let rawSeats = seats |> SeatCount.value |> int

                { Identity =
                    identity AtomicOperationKind.Place requestId (rawSeats.ToString(CultureInfo.InvariantCulture))
                  Command = BookingCommand.Place(Commands.place (RequestId.value requestId) rawSeats)
                  Payment = Some { RequestId = requestId; Seats = seats } })
        | BookingCommand.Confirm raw ->
            Validation.validateConfirmBooking raw
            |> Result.map (fun valid ->
                let requestId = ValidConfirmBooking.requestId valid
                let confirmationCode = ValidConfirmBooking.confirmationCode valid
                let rawCode = ConfirmationCode.value confirmationCode

                { Identity = identity AtomicOperationKind.Confirm requestId rawCode
                  Command = BookingCommand.Confirm(Commands.confirm (RequestId.value requestId) rawCode)
                  Payment = None })
        | BookingCommand.Cancel raw ->
            Validation.validateCancelBooking raw
            |> Result.map (fun valid ->
                let requestId = ValidCancelBooking.requestId valid
                let reason = ValidCancelBooking.reason valid
                let rawReason = CancellationReason.value reason

                { Identity = identity AtomicOperationKind.Cancel requestId rawReason
                  Command = BookingCommand.Cancel(Commands.cancel (RequestId.value requestId) rawReason)
                  Payment = None })
        |> Result.mapError (BookingDecisionError.InvalidCommand >> BookingConsistencyError.DecisionRejected)

// #region idempotent-booking-service
/// Replays acknowledged commands, preserves uncertain payment state for reconciliation,
/// and retries a committed notification with at-least-once delivery semantics.
type IdempotentBookingService
    (
        activity: Event,
        store: AtomicBookingStore,
        charge: PaymentRequest -> CancellationToken -> Task<PaymentOutcome>,
        notify: NotificationRequest -> CancellationToken -> Task<unit>
    ) =

    do ArgumentNullException.ThrowIfNull(store, nameof store)

    let storage (result: Result<'value, BookingStoreError>) =
        result |> Result.mapError BookingConsistencyError.StorageUnavailable

    let tryExternal
        (cancellationToken: CancellationToken)
        (start: unit -> Task<'value>)
        : Task<Result<'value, BookingConsistencyError>> =
        task {
            try
                let! value = start ()
                return Ok value
            with
            | :? OperationCanceledException as error when cancellationToken.IsCancellationRequested ->
                return raise error
            | :? OperationCanceledException -> return Error BookingConsistencyError.DependencyUnavailable
            | :? DependencyUnavailableException -> return Error BookingConsistencyError.DependencyUnavailable
        }

    let notificationFor (token: AtomicOperationToken) : NotificationRequest =
        let message =
            match token.Identity.Kind with
            | AtomicOperationKind.Place -> "booking placed"
            | AtomicOperationKind.Confirm -> "booking confirmed"
            | AtomicOperationKind.Cancel -> "booking cancelled"

        { RequestId = token.Identity.RequestId
          Message = message }

    // #region effect-progress
    let sendNotification
        (token: AtomicOperationToken)
        (cancellationToken: CancellationToken)
        : Task<Result<Booking, BookingConsistencyError>> =
        task {
            let! delivered = tryExternal cancellationToken (fun () -> notify (notificationFor token) cancellationToken)

            match delivered with
            | Error error -> return Error error
            | Ok() ->
                let! completed = store.CompleteNotification(activity, token, cancellationToken)

                return completed |> storage |> Result.map (fun () -> token.Candidate)
        }

    let chargeAndCommit
        (token: AtomicOperationToken)
        (payment: PaymentRequest)
        (cancellationToken: CancellationToken)
        : Task<Result<Booking, BookingConsistencyError>> =
        task {
            let! marked = store.MarkPaymentStarted(activity, token, cancellationToken)

            match storage marked with
            | Error error -> return Error error
            | Ok() ->
                let! paymentResult = tryExternal cancellationToken (fun () -> charge payment cancellationToken)

                match paymentResult with
                | Error error -> return Error error
                | Ok(PaymentOutcome.Declined _) ->
                    let! recorded = store.RecordPaymentDeclined(activity, token, cancellationToken)

                    return
                        match storage recorded with
                        | Error error -> Error error
                        | Ok() -> Error BookingConsistencyError.PaymentDeclined
                | Ok(PaymentOutcome.Authorized _) ->
                    let! committed = store.CommitAuthorizedBooking(activity, token, cancellationToken)

                    match storage committed with
                    | Error error -> return Error error
                    | Ok() -> return! sendNotification token cancellationToken
        }

    let executePrepared
        (prepared: PreparedCommand)
        (cancellationToken: CancellationToken)
        : Task<Result<Booking, BookingConsistencyError>> =
        task {
            let! begun = store.Begin(activity, prepared.Identity, prepared.Command, cancellationToken)

            match storage begun with
            | Error error -> return Error error
            | Ok(AtomicBeginResult.Replay booking) -> return Ok booking
            | Ok(AtomicBeginResult.DecisionRejected error) ->
                return Error(BookingConsistencyError.DecisionRejected error)
            | Ok(AtomicBeginResult.AggregateCapacityExceeded(requested, remaining)) ->
                return Error(BookingConsistencyError.AggregateCapacityExceeded(requested, remaining))
            | Ok AtomicBeginResult.IdempotencyConflict -> return Error BookingConsistencyError.IdempotencyConflict
            | Ok AtomicBeginResult.PreviousOperationIncomplete ->
                return Error BookingConsistencyError.PreviousOperationIncomplete
            | Ok AtomicBeginResult.PaymentDeclined -> return Error BookingConsistencyError.PaymentDeclined
            | Ok AtomicBeginResult.PaymentOutcomeUnknown -> return Error BookingConsistencyError.PaymentOutcomeUnknown
            | Ok(AtomicBeginResult.SendNotification token) -> return! sendNotification token cancellationToken
            | Ok(AtomicBeginResult.StartPayment token) ->
                match prepared.Payment with
                | Some payment -> return! chargeAndCommit token payment cancellationToken
                | None ->
                    return
                        Error(
                            BookingConsistencyError.StorageUnavailable(
                                BookingStoreError.CorruptSnapshot SnapshotCorruption.InconsistentData
                            )
                        )
        }
    // #endregion effect-progress

    member _.Execute(command: BookingCommand, cancellationToken: CancellationToken) =
        task {
            cancellationToken.ThrowIfCancellationRequested()

            match CommandIdentity.prepare command with
            | Error error -> return Error error
            | Ok prepared ->
                do! store.WorkflowGate.WaitAsync cancellationToken

                try
                    return! executePrepared prepared cancellationToken
                finally
                    store.WorkflowGate.Release() |> ignore
        }

    member _.Load(requestId: RequestId, cancellationToken: CancellationToken) =
        store.Load(activity, requestId, cancellationToken)
// #endregion idempotent-booking-service
