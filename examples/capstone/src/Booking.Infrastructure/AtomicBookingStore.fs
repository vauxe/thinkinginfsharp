namespace Booking.Infrastructure

open System
open System.Collections.Concurrent
open System.IO
open System.Text.Json
open System.Text.Json.Serialization
open System.Threading
open System.Threading.Tasks
open Booking.Contracts
open Booking.Domain
open Booking.Domain.Workflow

[<RequireQualifiedAccess>]
type internal AtomicOperationKind =
    | Place
    | Confirm
    | Cancel

type internal AtomicCommandIdentity =
    { Key: string
      RequestId: RequestId
      Kind: AtomicOperationKind
      Fingerprint: string }

type internal AtomicOperationPhase =
    | Reserved
    | PaymentStarted
    | PaymentDeclined
    | NotificationPending
    | Completed

type internal StoredOperation =
    { Identity: AtomicCommandIdentity
      Phase: AtomicOperationPhase
      Candidate: Booking }

type internal AtomicState =
    { EventId: EventId
      Capacity: Capacity
      Bookings: Map<string, Booking>
      Operations: Map<string, StoredOperation> }

type internal AtomicOperationToken =
    { Identity: AtomicCommandIdentity
      Candidate: Booking }

[<RequireQualifiedAccess>]
type internal AtomicBeginResult =
    | StartPayment of AtomicOperationToken
    | SendNotification of AtomicOperationToken
    | Replay of Booking
    | DecisionRejected of BookingDecisionError
    | AggregateCapacityExceeded of requested: int * remaining: int
    | IdempotencyConflict
    | PreviousOperationIncomplete
    | PaymentDeclined
    | PaymentOutcomeUnknown

// Persistence-only DTOs keep the JSON schema separate from the protected domain representation.
/// Mutable CLR shape for one persisted command-progress entry; not a domain or HTTP contract.
[<Sealed; AllowNullLiteral>]
type AtomicOperationDto() =
    [<JsonPropertyName("kind")>]
    member val Kind: string | null = null with get, set

    [<JsonPropertyName("requestId")>]
    member val RequestId: string | null = null with get, set

    [<JsonPropertyName("fingerprint")>]
    member val Fingerprint: string | null = null with get, set

    [<JsonPropertyName("phase")>]
    member val Phase: string | null = null with get, set

    [<JsonPropertyName("candidate")>]
    member val Candidate: BookingDto | null = null with get, set

/// Mutable CLR shape for the versioned local aggregate snapshot; not a domain or HTTP contract.
[<Sealed; AllowNullLiteral>]
type AtomicSnapshotDto() =
    [<JsonPropertyName("schemaVersion")>]
    member val SchemaVersion = 0 with get, set

    [<JsonPropertyName("eventId")>]
    member val EventId: string | null = null with get, set

    [<JsonPropertyName("capacity")>]
    member val Capacity = Nullable<int>() with get, set

    [<JsonPropertyName("bookings")>]
    member val Bookings: (BookingDto | null) array | null = null with get, set

    [<JsonPropertyName("operations")>]
    member val Operations: AtomicOperationDto array | null = null with get, set

module internal AtomicStoreImplementation =
    [<Literal>]
    let SchemaVersion = 1

    [<Literal>]
    let MaxSnapshotBytes = 1024 * 1024

    let private jsonOptions =
        let options = JsonSerializerOptions()
        BookingJson.configure options
        options.MaxDepth <- 16
        options

    let private corruption detail : Result<'value, BookingStoreError> =
        BookingStoreError.CorruptSnapshot detail |> Error

    let kindText (kind: AtomicOperationKind) =
        match kind with
        | AtomicOperationKind.Place -> "place"
        | AtomicOperationKind.Confirm -> "confirm"
        | AtomicOperationKind.Cancel -> "cancel"

    let private parseKind (raw: string | null) : Result<AtomicOperationKind, BookingStoreError> =
        match raw with
        | "place" -> Ok AtomicOperationKind.Place
        | "confirm" -> Ok AtomicOperationKind.Confirm
        | "cancel" -> Ok AtomicOperationKind.Cancel
        | _ -> corruption SnapshotCorruption.InconsistentData

    let private phaseText (phase: AtomicOperationPhase) =
        match phase with
        | Reserved -> "reserved"
        | PaymentStarted -> "paymentStarted"
        | PaymentDeclined -> "paymentDeclined"
        | NotificationPending -> "notificationPending"
        | Completed -> "completed"

    let private parsePhase (raw: string | null) : Result<AtomicOperationPhase, BookingStoreError> =
        match raw with
        | "reserved" -> Ok Reserved
        | "paymentStarted" -> Ok PaymentStarted
        | "paymentDeclined" -> Ok PaymentDeclined
        | "notificationPending" -> Ok NotificationPending
        | "completed" -> Ok Completed
        | _ -> corruption SnapshotCorruption.InconsistentData

    let operationKey (kind: AtomicOperationKind) (requestId: RequestId) =
        $"{kindText kind}:{RequestId.value requestId}"

    let private isCanonicalFingerprint (value: string) =
        value.Length = 64
        && value = value.ToUpperInvariant()
        && (value |> Seq.forall Uri.IsHexDigit)

    let private activeSeats (booking: Booking) =
        match Booking.status booking with
        | BookingStatus.Cancelled _ -> 0L
        | BookingStatus.Pending
        | BookingStatus.Confirmed _ -> booking |> Booking.seats |> SeatCount.value |> int64

    let private isReservation (operation: StoredOperation) =
        match operation.Phase with
        | Reserved
        | PaymentStarted -> true
        | PaymentDeclined
        | NotificationPending
        | Completed -> false

    let private isIncomplete (operation: StoredOperation) =
        match operation.Phase with
        | Reserved
        | PaymentStarted
        | NotificationPending -> true
        | PaymentDeclined
        | Completed -> false

    let private validateCandidateKind (kind: AtomicOperationKind) (booking: Booking) =
        match kind, Booking.status booking with
        | AtomicOperationKind.Place, BookingStatus.Pending
        | AtomicOperationKind.Confirm, BookingStatus.Confirmed _
        | AtomicOperationKind.Cancel, BookingStatus.Cancelled _ -> true
        | _ -> false

    let private validatePhase (kind: AtomicOperationKind) (phase: AtomicOperationPhase) =
        match kind, phase with
        | AtomicOperationKind.Place, _ -> true
        | (AtomicOperationKind.Confirm | AtomicOperationKind.Cancel), (NotificationPending | Completed) -> true
        | _ -> false

    let private addBooking
        (eventId: EventId)
        (capacity: Capacity)
        (result: Result<Map<string, Booking>, BookingStoreError>)
        (dto: BookingDto | null)
        =
        result
        |> Result.bind (fun bookings ->
            BookingMapping.toDomain dto
            |> Result.mapError (SnapshotCorruption.InvalidDomainData >> BookingStoreError.CorruptSnapshot)
            |> Result.bind (fun booking ->
                let key = booking |> Booking.requestId |> RequestId.value
                let sameEvent = Booking.eventId booking = eventId

                let seatsFit =
                    Booking.seats booking
                    |> SeatCount.value
                    |> fun seats -> seats <= Capacity.value capacity

                if not sameEvent || not seatsFit || Map.containsKey key bookings then
                    corruption SnapshotCorruption.InconsistentData
                else
                    Ok(Map.add key booking bookings)))

    let private addOperation
        (eventId: EventId)
        (capacity: Capacity)
        (result: Result<Map<string, StoredOperation>, BookingStoreError>)
        (dto: AtomicOperationDto)
        =
        result
        |> Result.bind (fun operations ->
            match dto with
            | null -> corruption SnapshotCorruption.InconsistentData
            | value ->
                parseKind value.Kind
                |> Result.bind (fun kind -> parsePhase value.Phase |> Result.map (fun phase -> kind, phase))
                |> Result.bind (fun (kind, phase) ->
                    match value.RequestId, value.Fingerprint with
                    | null, _
                    | _, null -> corruption SnapshotCorruption.InconsistentData
                    | rawRequestId, fingerprint ->
                        RequestId.create rawRequestId
                        |> Result.mapError (fun _ ->
                            BookingStoreError.CorruptSnapshot SnapshotCorruption.InconsistentData)
                        |> Result.map (fun requestId -> kind, phase, requestId, fingerprint))
                |> Result.bind (fun (kind, phase, requestId, fingerprint) ->
                    BookingMapping.toDomain value.Candidate
                    |> Result.mapError (SnapshotCorruption.InvalidDomainData >> BookingStoreError.CorruptSnapshot)
                    |> Result.map (fun candidate -> kind, phase, requestId, fingerprint, candidate))
                |> Result.bind (fun (kind, phase, requestId, fingerprint, candidate) ->
                    let key = operationKey kind requestId
                    let sameRequest = Booking.requestId candidate = requestId
                    let sameEvent = Booking.eventId candidate = eventId

                    let seatsFit =
                        Booking.seats candidate
                        |> SeatCount.value
                        |> fun seats -> seats <= Capacity.value capacity

                    if
                        Map.containsKey key operations
                        || not (isCanonicalFingerprint fingerprint)
                        || not sameRequest
                        || not sameEvent
                        || not seatsFit
                        || not (validateCandidateKind kind candidate)
                        || not (validatePhase kind phase)
                    then
                        corruption SnapshotCorruption.InconsistentData
                    else
                        Ok(
                            Map.add
                                key
                                { Identity =
                                    { Key = key
                                      RequestId = requestId
                                      Kind = kind
                                      Fingerprint = fingerprint }
                                  Phase = phase
                                  Candidate = candidate }
                                operations
                        )))

    let private validateState (state: AtomicState) =
        let unfinishedPerRequestIsUnique =
            state.Operations
            |> Map.values
            |> Seq.filter isIncomplete
            |> Seq.countBy (fun operation -> RequestId.value operation.Identity.RequestId)
            |> Seq.forall (fun (_, count) -> count = 1)

        let operationLinksAreValid =
            state.Operations
            |> Map.values
            |> Seq.forall (fun operation ->
                let requestKey = RequestId.value operation.Identity.RequestId
                let bookingExists = Map.containsKey requestKey state.Bookings

                match operation.Phase with
                | Reserved
                | PaymentStarted
                | PaymentDeclined -> not bookingExists
                | NotificationPending
                | Completed -> bookingExists)

        let occupied = state.Bookings |> Map.values |> Seq.sumBy activeSeats

        let reserved =
            state.Operations
            |> Map.values
            |> Seq.filter isReservation
            |> Seq.sumBy (fun operation -> operation.Candidate |> Booking.seats |> SeatCount.value |> int64)

        let capacity = state.Capacity |> Capacity.value |> int64

        if
            unfinishedPerRequestIsUnique
            && operationLinksAreValid
            && occupied + reserved <= capacity
        then
            Ok state
        else
            corruption SnapshotCorruption.InconsistentData

    let empty (activity: Event) : AtomicState =
        { EventId = Event.id activity
          Capacity = Event.capacity activity
          Bookings = Map.empty
          Operations = Map.empty }

    let private fromDto (activity: Event) (dto: AtomicSnapshotDto) : Result<AtomicState, BookingStoreError> =
        match dto with
        | null -> corruption SnapshotCorruption.InconsistentData
        | value when value.SchemaVersion <> SchemaVersion ->
            corruption (SnapshotCorruption.UnsupportedSchemaVersion value.SchemaVersion)
        | value ->
            match value.EventId, value.Capacity.HasValue, value.Bookings, value.Operations with
            | null, _, _, _
            | _, false, _, _
            | _, _, null, _
            | _, _, _, null -> corruption SnapshotCorruption.InconsistentData
            | rawEventId, true, bookingDtos, operationDtos ->
                EventId.create rawEventId
                |> Result.mapError (fun _ -> BookingStoreError.CorruptSnapshot SnapshotCorruption.InconsistentData)
                |> Result.bind (fun eventId ->
                    Capacity.create value.Capacity.Value
                    |> Result.mapError (fun _ -> BookingStoreError.CorruptSnapshot SnapshotCorruption.InconsistentData)
                    |> Result.map (fun capacity -> eventId, capacity))
                |> Result.bind (fun (eventId, capacity) ->
                    if eventId <> Event.id activity || capacity <> Event.capacity activity then
                        Error BookingStoreError.SnapshotActivityMismatch
                    else
                        Array.fold (addBooking eventId capacity) (Ok Map.empty) bookingDtos
                        |> Result.bind (fun bookings ->
                            Array.fold (addOperation eventId capacity) (Ok Map.empty) operationDtos
                            |> Result.map (fun operations ->
                                { EventId = eventId
                                  Capacity = capacity
                                  Bookings = bookings
                                  Operations = operations })))
                |> Result.bind validateState

    let private operationToDto (operation: StoredOperation) : AtomicOperationDto =
        let dto = AtomicOperationDto()
        dto.Kind <- kindText operation.Identity.Kind
        dto.RequestId <- RequestId.value operation.Identity.RequestId
        dto.Fingerprint <- operation.Identity.Fingerprint
        dto.Phase <- phaseText operation.Phase
        dto.Candidate <- BookingMapping.ofDomain operation.Candidate
        dto

    let private toDto (state: AtomicState) : AtomicSnapshotDto =
        let dto = AtomicSnapshotDto()
        dto.SchemaVersion <- SchemaVersion
        dto.EventId <- EventId.value state.EventId
        dto.Capacity <- Nullable(state.Capacity |> Capacity.value |> int)

        dto.Bookings <- state.Bookings |> Map.toArray |> Array.map (snd >> BookingMapping.ofDomain)

        dto.Operations <- state.Operations |> Map.toArray |> Array.map (snd >> operationToDto)

        dto

    let readState
        (snapshotPath: string)
        (activity: Event)
        (cancellationToken: CancellationToken)
        : Task<Result<AtomicState, BookingStoreError>> =
        task {
            let! bytesResult = FileStoreImplementation.readBounded MaxSnapshotBytes snapshotPath cancellationToken

            match bytesResult with
            | Error error -> return Error error
            | Ok None -> return Ok(empty activity)
            | Ok(Some bytes) ->
                match FileStoreImplementation.decode bytes with
                | Error error -> return Error error
                | Ok json ->
                    try
                        return
                            JsonSerializer.Deserialize<AtomicSnapshotDto>(json, jsonOptions)
                            |> fromDto activity
                    with :? JsonException ->
                        return corruption SnapshotCorruption.InvalidJson
        }

    let writeState
        (directoryPath: string)
        (snapshotPath: string)
        (state: AtomicState)
        (cancellationToken: CancellationToken)
        : Task<Result<unit, BookingStoreError>> =
        task {
            cancellationToken.ThrowIfCancellationRequested()
            let bytes = JsonSerializer.SerializeToUtf8Bytes(toDto state, jsonOptions)

            if bytes.Length > MaxSnapshotBytes then
                return Error(BookingStoreError.SnapshotTooLarge MaxSnapshotBytes)
            else
                let temporaryPath =
                    Path.Combine(directoryPath, $".{Path.GetFileName(snapshotPath)}.{Guid.NewGuid():N}.tmp")

                try
                    let directoryResult =
                        try
                            Directory.CreateDirectory directoryPath |> ignore
                            Ok()
                        with
                        | :? IOException
                        | :? UnauthorizedAccessException -> Error BookingStoreError.CannotWriteTemporarySnapshot

                    match directoryResult with
                    | Error error -> return Error error
                    | Ok() ->
                        let! writeResult = FileStoreImplementation.writeTemporary temporaryPath bytes cancellationToken

                        match writeResult with
                        | Error error -> return Error error
                        | Ok() ->
                            cancellationToken.ThrowIfCancellationRequested()
                            return FileStoreImplementation.replace temporaryPath snapshotPath
                finally
                    FileStoreImplementation.cleanup temporaryPath
        }

    let occupiedSeats (state: AtomicState) =
        state.Bookings |> Map.values |> Seq.sumBy activeSeats

    let reservedSeats (state: AtomicState) =
        state.Operations
        |> Map.values
        |> Seq.filter isReservation
        |> Seq.sumBy (fun operation -> operation.Candidate |> Booking.seats |> SeatCount.value |> int64)

    let hasIncompleteOperationFor (requestId: RequestId) (state: AtomicState) =
        state.Operations
        |> Map.values
        |> Seq.exists (fun operation -> operation.Identity.RequestId = requestId && isIncomplete operation)

// #region process-local-gates
// These gates coordinate every store instance for the same path in this process. They do not
// claim to serialize writers in different processes or machines.
module private AtomicPathGates =
    // Treat case-only variants conservatively as one path. On a case-sensitive file system this
    // may serialize unrelated files, but it cannot weaken consistency for either file.
    let private stateGates =
        ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.OrdinalIgnoreCase)

    let private workflowGates =
        ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.OrdinalIgnoreCase)

    let state (path: string) =
        stateGates.GetOrAdd(path, fun _ -> new SemaphoreSlim(1, 1))

    let workflow (path: string) =
        workflowGates.GetOrAdd(path, fun _ -> new SemaphoreSlim(1, 1))
// #endregion process-local-gates

// #region atomic-booking-store
/// Coordinates complete snapshot transitions for one configured path within one process.
/// It deliberately makes no cross-process or distributed-writer guarantee.
type AtomicBookingStore(configuration: BookingStoreConfiguration) =
    let snapshotPath = BookingStoreConfiguration.snapshotPath configuration
    let directoryPath = BookingStoreConfiguration.directoryPath configuration
    let stateGate = AtomicPathGates.state snapshotPath
    let workflowGate = AtomicPathGates.workflow snapshotPath

    let withStateGate (cancellationToken: CancellationToken) (operation: unit -> Task<'value>) : Task<'value> =
        task {
            do! stateGate.WaitAsync cancellationToken

            try
                return! operation ()
            finally
                stateGate.Release() |> ignore
        }

    let update
        (activity: Event)
        (token: AtomicOperationToken)
        (expectedPhase: AtomicOperationPhase)
        (nextPhase: AtomicOperationPhase)
        (commitBooking: bool)
        (cancellationToken: CancellationToken)
        =
        withStateGate cancellationToken (fun () ->
            task {
                let! loaded = AtomicStoreImplementation.readState snapshotPath activity cancellationToken

                match loaded with
                | Error error -> return Error error
                | Ok state ->
                    match Map.tryFind token.Identity.Key state.Operations with
                    | Some operation when
                        operation.Identity.Fingerprint = token.Identity.Fingerprint
                        && operation.Phase = expectedPhase
                        && operation.Candidate = token.Candidate
                        ->
                        let changedOperation = { operation with Phase = nextPhase }

                        let bookings =
                            if commitBooking then
                                Map.add
                                    (RequestId.value operation.Identity.RequestId)
                                    operation.Candidate
                                    state.Bookings
                            else
                                state.Bookings

                        let changed =
                            { state with
                                Bookings = bookings
                                Operations = Map.add token.Identity.Key changedOperation state.Operations }

                        return!
                            AtomicStoreImplementation.writeState directoryPath snapshotPath changed cancellationToken
                    | _ -> return Error(BookingStoreError.CorruptSnapshot SnapshotCorruption.InconsistentData)
            })

    static member MaxSnapshotBytes = AtomicStoreImplementation.MaxSnapshotBytes

    member internal _.WorkflowGate = workflowGate

    member _.Load(activity: Event, requestId: RequestId, cancellationToken: CancellationToken) =
        withStateGate cancellationToken (fun () ->
            task {
                let! loaded = AtomicStoreImplementation.readState snapshotPath activity cancellationToken

                return
                    loaded
                    |> Result.map (fun state -> Map.tryFind (RequestId.value requestId) state.Bookings)
            })

    member internal _.Begin
        (activity: Event, identity: AtomicCommandIdentity, command: BookingCommand, cancellationToken: CancellationToken) =
        withStateGate cancellationToken (fun () ->
            task {
                let! loaded = AtomicStoreImplementation.readState snapshotPath activity cancellationToken

                match loaded with
                | Error error -> return Error error
                | Ok state ->
                    match Map.tryFind identity.Key state.Operations with
                    | Some operation when operation.Identity.Fingerprint <> identity.Fingerprint ->
                        return Ok AtomicBeginResult.IdempotencyConflict
                    | Some operation ->
                        let token: AtomicOperationToken =
                            { Identity = operation.Identity
                              Candidate = operation.Candidate }

                        return
                            match operation.Phase with
                            | Reserved -> Ok(AtomicBeginResult.StartPayment token)
                            | PaymentStarted -> Ok AtomicBeginResult.PaymentOutcomeUnknown
                            | PaymentDeclined -> Ok AtomicBeginResult.PaymentDeclined
                            | NotificationPending -> Ok(AtomicBeginResult.SendNotification token)
                            | Completed -> Ok(AtomicBeginResult.Replay operation.Candidate)
                    | None when AtomicStoreImplementation.hasIncompleteOperationFor identity.RequestId state ->
                        return Ok AtomicBeginResult.PreviousOperationIncomplete
                    | None ->
                        let bookingState =
                            match Map.tryFind (RequestId.value identity.RequestId) state.Bookings with
                            | Some booking -> Booked booking
                            | None -> NotBooked

                        match Decider.decide activity bookingState command with
                        | Error error -> return Ok(AtomicBeginResult.DecisionRejected error)
                        | Ok bookingEvent ->
                            let candidate = BookingEvent.booking bookingEvent

                            let token: AtomicOperationToken =
                                { Identity = identity
                                  Candidate = candidate }

                            // #region atomic-capacity-decision
                            match identity.Kind with
                            | AtomicOperationKind.Place ->
                                let requested = candidate |> Booking.seats |> SeatCount.value |> int64

                                let remaining =
                                    (state.Capacity |> Capacity.value |> int64)
                                    - AtomicStoreImplementation.occupiedSeats state
                                    - AtomicStoreImplementation.reservedSeats state
                                    |> max 0L

                                if requested > remaining then
                                    return
                                        Ok(AtomicBeginResult.AggregateCapacityExceeded(int requested, int remaining))
                                else
                                    let operation: StoredOperation =
                                        { Identity = identity
                                          Phase = Reserved
                                          Candidate = candidate }

                                    let changed =
                                        { state with
                                            Operations = Map.add identity.Key operation state.Operations }

                                    let! saved =
                                        AtomicStoreImplementation.writeState
                                            directoryPath
                                            snapshotPath
                                            changed
                                            cancellationToken

                                    return saved |> Result.map (fun () -> AtomicBeginResult.StartPayment token)
                            | AtomicOperationKind.Confirm
                            | AtomicOperationKind.Cancel ->
                                let operation: StoredOperation =
                                    { Identity = identity
                                      Phase = NotificationPending
                                      Candidate = candidate }

                                let changed =
                                    { state with
                                        Bookings =
                                            Map.add (RequestId.value identity.RequestId) candidate state.Bookings
                                        Operations = Map.add identity.Key operation state.Operations }

                                let! saved =
                                    AtomicStoreImplementation.writeState
                                        directoryPath
                                        snapshotPath
                                        changed
                                        cancellationToken

                                return saved |> Result.map (fun () -> AtomicBeginResult.SendNotification token)
            // #endregion atomic-capacity-decision
            })

    member internal _.MarkPaymentStarted
        (activity: Event, token: AtomicOperationToken, cancellationToken: CancellationToken)
        =
        update activity token Reserved PaymentStarted false cancellationToken

    member internal _.RecordPaymentDeclined
        (activity: Event, token: AtomicOperationToken, cancellationToken: CancellationToken)
        =
        update activity token PaymentStarted PaymentDeclined false cancellationToken

    member internal _.CommitAuthorizedBooking
        (activity: Event, token: AtomicOperationToken, cancellationToken: CancellationToken)
        =
        update activity token PaymentStarted NotificationPending true cancellationToken

    member internal _.CompleteNotification
        (activity: Event, token: AtomicOperationToken, cancellationToken: CancellationToken)
        =
        update activity token NotificationPending Completed false cancellationToken
// #endregion atomic-booking-store
