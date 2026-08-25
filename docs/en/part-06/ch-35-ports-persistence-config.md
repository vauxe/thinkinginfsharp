---
title: "Chapter 35: Ports, Persistence, Configuration, and Stubs"
description: "Keep F# domain values behind explicit DTO mappings, persist one bounded local snapshot safely, and assemble deterministic adapters with clear ownership."
translationKey: part-06/ch-35-ports-persistence-config
---

# Chapter 35: Ports, Persistence, Configuration, and Stubs {#overview}

Chapter 34 ended at an accepted fact. This chapter crosses the effect boundary without letting JSON, paths, or test-service behavior become domain rules. The result is deliberately small: one versioned DTO contract, one bounded local snapshot, deterministic payment and notification stubs, and one composition object that owns them.

The central question is authority. The domain decides whether a command is legal. A mapper decides whether an external representation can become protected data. A file adapter decides how bytes are replaced. A composition root decides which implementations supply capabilities and who disposes them. Keeping those decisions separate makes failures both honest and testable.

## What you will be able to do {#outcomes}

By the end of this chapter, you should be able to:

- read a port as a capability required by the application rather than an implementation choice;
- keep transport and persistence DTOs separate from private F# records and discriminated unions;
- design bidirectional mapping that either succeeds completely or returns a precise error;
- choose and lock a stable JSON representation for a union;
- use a schema version as a compatibility decision point, not decoration;
- reject unknown, wrongly cased, oversized, malformed, and semantically impossible snapshots;
- distinguish a same-directory replacement from a database transaction or distributed guarantee;
- load a configurable path without treating ordinary configuration as a secret;
- build deterministic stubs for success, refusal, fault, and cancellation;
- propagate the caller's cancellation token through every asynchronous port;
- put resource ownership at the composition boundary and make disposal idempotent;
- state exactly what this local adapter still cannot guarantee.

## Follow dependency direction {#dependency-direction}

The project dependencies point inward:

```text
Booking.Api (next chapter)
        |
        v
Booking.Infrastructure ---> Booking.Contracts ---> Booking.Domain
        |                                             ^
        +---------------------------------------------+
```

`Booking.Domain` names commands, facts, protected values, decisions, and required ports. It knows nothing about JSON or files. `Booking.Contracts` references the domain only to perform explicit conversion. `Booking.Infrastructure` implements effects using both layers. A future API may reference all three at its composition boundary, but the domain never points back outward.

This is not ceremonial layering. If the domain referenced `JsonPropertyNameAttribute`, a file path, or a payment stub, changing an outer mechanism could force changes to business types. The dependency graph prevents that accidental authority transfer.

## Keep wire shape separate from domain shape {#separate-shapes}

The snapshot DTO is intentionally ordinary .NET data:

```fsharp:line-numbers [Dtos.fs]
[<CLIMutable>]
type BookingDto =
    { [<JsonPropertyName("schemaVersion")>]
      SchemaVersion: int
      [<JsonPropertyName("requestId")>]
      RequestId: string | null
      [<JsonPropertyName("eventId")>]
      EventId: string | null
      [<JsonPropertyName("seats")>]
      Seats: Nullable<int>
      [<JsonPropertyName("status")>]
      Status: string | null
      [<JsonPropertyName("confirmationCode")>]
      ConfirmationCode: string | null
      [<JsonPropertyName("cancellationReason")>]
      CancellationReason: string | null }
```
`[<CLIMutable>]` adds a parameterless constructor and property setters for CLI-oriented consumers. It does not make this record a domain entity. `[<JsonPropertyName>]` fixes the wire names in both serialization directions, independently of future F# field renames.

The DTO admits states the domain forbids: null identifiers, a missing seat count, an unknown status string, or two status payloads at once. That is correct at an untrusted boundary. If its type pretended those values were impossible, deserialization failure would merely move into reflection or exceptions without giving the application an explicit mapping policy.

The protected `Booking` record remains private, and `BookingStatus` remains a useful F# union. Neither is serialized directly. Domain representation can therefore evolve without silently redefining stored JSON.

## Represent a union deliberately {#union-representation}

Version 1 projects `BookingStatus` to one exact tag plus at most one payload:

| Domain value | `status` | Required payload | Forbidden payload |
|---|---|---|---|
| `Pending` | `"pending"` | none | confirmation code and cancellation reason |
| `Confirmed code` | `"confirmed"` | `confirmationCode` | cancellation reason |
| `Cancelled reason` | `"cancelled"` | `cancellationReason` | confirmation code |

A raw string is preferable to a CLR enum here. The domain value is not an enum: two cases carry different protected data. A string tag also lets mapping return `UnknownStatus actual` instead of allowing serializer defaults to invent a numeric convention.

Omitted null payloads make each successful shape smaller, but omission is not ambiguity. The tag says which payload must exist. Contract tests assert the exact property set for every case so a serializer-option change cannot quietly add both null fields.

## Make reverse mapping explicit {#explicit-mapping}

The mapping error union names representation failures without flattening them to text:

```fsharp:line-numbers [Mapping.fs]
[<RequireQualifiedAccess>]
type DtoMappingError =
    | MissingBody
    | UnsupportedSchemaVersion of actual: int
    | MissingRequestId
    | MissingEventId
    | MissingSeats
    | MissingStatus
    | MissingConfirmationCode
    | MissingCancellationReason
    | InvalidRequestId of RequestIdError
    | InvalidEventId of EventIdError
    | InvalidSeatCount of SeatCountError
    | InvalidConfirmationCode of ConfirmationCodeError
    | InvalidCancellationReason of CancellationReasonError
    | UnknownStatus of actual: string
    | UnexpectedConfirmationCode of status: string
    | UnexpectedCancellationReason of status: string
```
Reverse snapshot mapping proceeds in a stated order:

```fsharp:line-numbers [Mapping.fs]
module BookingMapping =
    let ofDomain (booking: Booking) : BookingDto =
        let nullableText (value: string) : string | null = value
        let noText: string | null = null

        let status, confirmationCode, cancellationReason =
            match Booking.status booking with
            | Pending -> "pending", noText, noText
            | Confirmed code -> "confirmed", code |> ConfirmationCode.value |> nullableText, noText
            | Cancelled reason -> "cancelled", noText, reason |> CancellationReason.value |> nullableText

        { SchemaVersion = BookingContract.CurrentSchemaVersion
          RequestId = booking |> Booking.requestId |> RequestId.value
          EventId = booking |> Booking.eventId |> EventId.value
          Seats = booking |> Booking.seats |> SeatCount.value |> int |> Nullable
          Status = status
          ConfirmationCode = confirmationCode
          CancellationReason = cancellationReason }

    let toDomain (dto: BookingDto | null) =
        match dto with
        | null -> Error DtoMappingError.MissingBody
        | value when value.SchemaVersion <> BookingContract.CurrentSchemaVersion ->
            Error(DtoMappingError.UnsupportedSchemaVersion value.SchemaVersion)
        | value ->
            MappingInternals.requestId value.RequestId
            |> Result.bind (fun requestId ->
                MappingInternals.eventId value.EventId
                |> Result.map (fun eventId -> requestId, eventId))
            |> Result.bind (fun (requestId, eventId) ->
                MappingInternals.seats value.Seats
                |> Result.map (fun seats -> requestId, eventId, seats))
            |> Result.bind (fun (requestId, eventId, seats) ->
                MappingInternals.status value
                |> Result.map (Booking.restore requestId eventId seats))
```
The schema version is checked first. A version 2 document is incompatible even if its remaining fields happen to resemble version 1, so the mapper returns `UnsupportedSchemaVersion 2` before interpreting that payload.

Next, identifier and seat primitives pass through their existing smart constructors. Status mapping then checks both the exact tag and its legal payload combination. Only after every value is protected does `Booking.restore` rebuild the private record. That function accepts protected values; it does not accept raw JSON strings or integers.

Forward mapping cannot fail for a valid `Booking`: every union case has one declared projection. Reverse mapping is allowed to fail because an external representation carries no domain guarantee. This asymmetry is useful information, not an API defect.

### Keep command mapping at the right trust level {#command-mapping}

Command DTOs perform a narrower job:

```fsharp:line-numbers [Mapping.fs]
module PlaceBookingMapping =
    let ofDomain (command: PlaceBooking) : PlaceBookingDto =
        { RequestId = command.RequestId
          Seats = Nullable command.Seats }

    let toDomain (dto: PlaceBookingDto | null) =
        match dto with
        | null -> Error DtoMappingError.MissingBody
        | value ->
            match value.RequestId with
            | null -> Error DtoMappingError.MissingRequestId
            | requestId -> MappingInternals.rawSeats value.Seats |> Result.map (Commands.place requestId)

module ConfirmBookingMapping =
    let ofDomain (command: ConfirmBooking) : ConfirmBookingDto =
        { RequestId = command.RequestId
          ConfirmationCode = command.ConfirmationCode }

    let toDomain (dto: ConfirmBookingDto | null) =
        match dto with
        | null -> Error DtoMappingError.MissingBody
        | value ->
            match value.RequestId, value.ConfirmationCode with
            | null, _ -> Error DtoMappingError.MissingRequestId
            | _, null -> Error DtoMappingError.MissingConfirmationCode
            | requestId, confirmationCode -> Ok(Commands.confirm requestId confirmationCode)

module CancelBookingMapping =
    let ofDomain (command: CancelBooking) : CancelBookingDto =
        { RequestId = command.RequestId
          Reason = command.Reason }

    let toDomain (dto: CancelBookingDto | null) =
        match dto with
        | null -> Error DtoMappingError.MissingBody
        | value ->
            match value.RequestId, value.Reason with
            | null, _ -> Error DtoMappingError.MissingRequestId
            | _, null -> Error DtoMappingError.MissingCancellationReason
            | requestId, reason -> Ok(Commands.cancel requestId reason)
```
They reject transport absence such as a missing body, request ID, seat property, code, or reason. They deliberately preserve blank strings and zero seats in raw domain commands. Chapter 34's validators own those rules and can accumulate their errors; repeating them in DTO mapping would create competing authorities and different precedence.

Thus “mapping succeeded” means the transport supplied the fields needed to express an intent. It does not mean the intent passed domain validation or business decision.

## Lock serializer policy once {#json-policy}

The JSON helper configures one private options object before use:

```fsharp:line-numbers [Dtos.fs]
module BookingJson =
    // Wire names: https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/customize-properties
    // Unmapped data: https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/missing-members
    let configure (options: JsonSerializerOptions) =
        ArgumentNullException.ThrowIfNull(options, nameof options)
        options.PropertyNamingPolicy <- JsonNamingPolicy.CamelCase
        options.PropertyNameCaseInsensitive <- false
        options.UnmappedMemberHandling <- JsonUnmappedMemberHandling.Disallow
        options.DefaultIgnoreCondition <- JsonIgnoreCondition.WhenWritingNull
        options.MaxDepth <- 8

    let private options =
        let settings = JsonSerializerOptions()
        configure settings
        settings

    let serializeBooking (dto: BookingDto) =
        ArgumentNullException.ThrowIfNull(dto, nameof dto)
        JsonSerializer.Serialize(dto, options)

    let deserializeBooking (json: string) : BookingDto | null =
        ArgumentNullException.ThrowIfNull(json, nameof json)
        JsonSerializer.Deserialize<BookingDto>(json, options)

    let deserializePlaceBooking (json: string) : PlaceBookingDto | null =
        ArgumentNullException.ThrowIfNull(json, nameof json)
        JsonSerializer.Deserialize<PlaceBookingDto>(json, options)

    let deserializeConfirmBooking (json: string) : ConfirmBookingDto | null =
        ArgumentNullException.ThrowIfNull(json, nameof json)
        JsonSerializer.Deserialize<ConfirmBookingDto>(json, options)

    let deserializeCancelBooking (json: string) : CancelBookingDto | null =
        ArgumentNullException.ThrowIfNull(json, nameof json)
        JsonSerializer.Deserialize<CancelBookingDto>(json, options)
```
The choices are part of the boundary contract:

- property names are camel-case and explicitly attributed;
- reading is case-sensitive, so `RequestId` is not an alias for `requestId`;
- unmapped members are rejected rather than silently ignored;
- null properties are omitted when writing;
- nesting depth is capped;
- a JSON `null` body remains representable and maps to `MissingBody`.

Strict unknown-member rejection is a fail-closed compatibility policy. It catches misspellings and unexpected producer changes, but it also means additive fields require an intentional version or policy change. That trade-off must be documented for clients; “JSON is flexible” is not a compatibility contract.

JSON contract tests pin tags, property sets, casing, unknown fields, version precedence, all protected-status round trips, missing values, impossible payload combinations, and raw command preservation.

## Treat path as validated configuration {#configuration}

The file adapter receives a protected configuration value:

```fsharp:line-numbers [Configuration.fs]
[<RequireQualifiedAccess>]
module BookingStoreConfiguration =
    [<Literal>]
    let PathEnvironmentVariable = "BOOKING_STORE_PATH"

    let create (configuredPath: string | null) =
        match configuredPath with
        | null -> Error BookingStoreConfigurationError.MissingSnapshotPath
        | raw when String.IsNullOrWhiteSpace raw -> Error BookingStoreConfigurationError.MissingSnapshotPath
        | raw ->
            try
                let fullPath = raw.Trim() |> Path.GetFullPath
                let fileName = Path.GetFileName fullPath
                let directory = Path.GetDirectoryName fullPath

                match directory with
                | null -> Error BookingStoreConfigurationError.InvalidSnapshotPath
                | value when String.IsNullOrWhiteSpace fileName || Directory.Exists fullPath ->
                    Error BookingStoreConfigurationError.InvalidSnapshotPath
                | value ->
                    Ok
                        { SnapshotPath = fullPath
                          DirectoryPath = value }
            with
            | :? ArgumentException
            | :? NotSupportedException
            | :? PathTooLongException -> Error BookingStoreConfigurationError.InvalidSnapshotPath

    // Environment variables override file settings in the default .NET configuration stack:
    // https://learn.microsoft.com/dotnet/core/extensions/configuration-providers#environment-variable-configuration-provider
    let fromEnvironment () =
        Environment.GetEnvironmentVariable PathEnvironmentVariable |> create

    let snapshotPath configuration = configuration.SnapshotPath

    let internal directoryPath configuration = configuration.DirectoryPath
```
`create` distinguishes a missing value from an invalid file path, normalizes to an absolute path, and rejects a path that already names a directory. The adapter therefore does not repeatedly reinterpret raw configuration.

`BOOKING_STORE_PATH` can come from an environment-variable provider, while tests call `create` with a path under the operating system's temporary directory. A storage path is configuration, not a secret. Credentials, API keys, and certificates would require a secret provider and must not be committed merely because environment variables also carry configuration.

The path is controlled by deployment configuration, never derived from a request ID. This avoids turning user input into path traversal or an unbounded set of files.

## Persist one bounded snapshot {#bounded-snapshot}

`FileBookingStore` exposes asynchronous `Load` and `Save` operations over a protected `Booking`:

```fsharp:line-numbers [FileStore.fs]
type FileBookingStore(configuration: BookingStoreConfiguration) =
    let snapshotPath = BookingStoreConfiguration.snapshotPath configuration
    let directoryPath = BookingStoreConfiguration.directoryPath configuration

    static member MaxSnapshotBytes = FileStoreImplementation.MaxSnapshotBytes

    member _.Load(cancellationToken: CancellationToken) : Task<Result<Booking option, BookingStoreError>> =
        task {
            cancellationToken.ThrowIfCancellationRequested()

            let! bytesResult =
                FileStoreImplementation.readBounded
                    FileStoreImplementation.MaxSnapshotBytes
                    snapshotPath
                    cancellationToken

            match bytesResult with
            | Error error -> return Error error
            | Ok None -> return Ok None
            | Ok(Some bytes) ->
                match FileStoreImplementation.decode bytes with
                | Error error -> return Error error
                | Ok json ->
                    try
                        return
                            BookingJson.deserializeBooking json
                            |> BookingMapping.toDomain
                            |> Result.map Some
                            |> Result.mapError (
                                SnapshotCorruption.InvalidDomainData >> BookingStoreError.CorruptSnapshot
                            )
                    with :? JsonException ->
                        return Error(BookingStoreError.CorruptSnapshot SnapshotCorruption.InvalidJson)
        }

    member _.Save(booking: Booking, cancellationToken: CancellationToken) : Task<Result<unit, BookingStoreError>> =
        task {
            cancellationToken.ThrowIfCancellationRequested()

            let bytes =
                booking
                |> BookingMapping.ofDomain
                |> BookingJson.serializeBooking
                |> Encoding.UTF8.GetBytes

            if bytes.Length > FileStoreImplementation.MaxSnapshotBytes then
                return Error(BookingStoreError.SnapshotTooLarge FileStoreImplementation.MaxSnapshotBytes)
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
```
Internally, save maps the booking to `BookingDto`, serializes UTF-8 without a byte-order mark, and rejects output larger than 64 KiB. Load reads at most 64 KiB plus one sentinel byte before deciding whether parsing is allowed. It accepts an optional UTF-8 BOM but rejects invalid byte sequences.

The fixed bound prevents a damaged or replaced local file from causing unbounded allocation. Sixty-four KiB is a sample-specific limit for one small snapshot, not a universal JSON limit. A collection store would need a limit derived from its real cardinality and streaming policy.

Missing file or directory means `Ok None`; it is not corruption. Read or permission failure has a separate operational case. Syntactically invalid JSON, invalid UTF-8, and valid JSON that cannot become a protected booking are three distinct corruption categories.

## Replace through the same directory {#replacement}

Save follows this sequence:

1. Serialize and size-check before touching the target.
2. Create the configured parent directory if necessary.
3. Create a uniquely named temporary file in that same directory.
4. Write the complete bytes and call `Flush(true)`.
5. Observe cancellation once more before commit.
6. Move the temporary file over the destination with overwrite enabled.
7. Best-effort delete any remaining temporary file in `finally`.

Placing both files in one directory keeps the move on one volume. It avoids the documented cross-volume behavior where moving may become copy-then-delete, and it avoids the visibly partial destination produced by writing directly to the target.

The guarantee is narrow. Readers see the old complete file or the newly moved complete file under the local filesystem's same-volume replacement semantics. `Flush(true)` asks .NET and the operating system to flush intermediate buffers, but this example does not claim protection against every device, filesystem, kernel, or sudden-power-loss behavior.

It is also not a transaction around load, domain decision, payment, notification, and save. Two callers can still read the same old state and race. Chapter 37 will add an atomic state boundary and test concurrent capacity explicitly.

## Assemble capabilities at one edge {#composition}

The infrastructure composition object supplies the domain's `AsyncPorts` record:

```fsharp:line-numbers [Composition.fs]
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
```
Each function keeps the caller's `CancellationToken`. Store errors become `BookingStoreAdapterException`, retaining a typed internal category while giving a later HTTP layer one place to map a safe response. The exception message contains neither file contents nor a configured path.

`LoadBooking` honors the requested key and returns `NotBooked` for a different stored request. `AppendEvent` rejects a mismatch between its key and the event's protected request ID before saving the event's resulting booking.

The current adapter stores only one snapshot. Therefore a different request can replace the prior snapshot after a later successful append. This is an acknowledged teaching-stage limitation, not a multi-booking repository. Chapter 37 replaces this read/write model before the API is called consistency-safe.

## Use deterministic substitutes, not pretend integrations {#deterministic-stubs}

The payment substitute fixes its behavior at construction:

```fsharp:line-numbers [PaymentStub.fs]
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
            | PaymentStubBehavior.Fail message ->
                return
                    raise (
                        DependencyUnavailableException(
                            "Payment dependency is unavailable.",
                            InvalidOperationException message
                        )
                    )
        }

    interface IDisposable with
        member _.Dispose() =
            lock syncRoot (fun () -> disposed <- true)
```
It authorizes with a supplied transaction ID, returns a supplied decline reason, or raises `DependencyUnavailableException` whose `InnerException` carries the supplied failure detail. The notification substitute similarly delivers or raises the same typed availability signal:

```fsharp:line-numbers [NotificationStub.fs]
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
            | NotificationStubBehavior.Fail message ->
                return
                    raise (
                        DependencyUnavailableException(
                            "Notification dependency is unavailable.",
                            InvalidOperationException message
                        )
                    )
        }

    interface IDisposable with
        member _.Dispose() =
            lock syncRoot (fun () -> disposed <- true)
```
Both check cancellation before recording a call. Neither uses HTTP, clocks, randomness, sleeps, credentials, or environment state. Their call lists are synchronized snapshots, which makes assertions deterministic without a mocking library.

These are substitutes for learning and integration control. They do not model payment authorization protocols, retries, webhook delivery, message durability, fraud checks, or provider idempotency. Naming them `Stub` prevents a reader from mistaking deterministic behavior for a production integration.

## Put ownership beside construction {#ownership}

`Composition.start` constructs both stubs and returns the object that owns them. The application should bind that object with `use` at its outer lifetime boundary. Disposal marks the composition closed, disposes notification then payment, and is safe to call repeatedly.

Ports reject calls after disposal. A pre-cancelled call observes cancellation before checking disposal; this ordering is fixed by `ensureActive`. The file adapter retains no open stream between calls, so each `use stream` owns and releases its handle inside one operation.

Ownership would be ambiguous if a composition accepted arbitrary externally owned `IDisposable` values without saying whether it borrowed or adopted them. Constructing the owned values in `start` makes the policy visible.

## Keep failure categories separate {#failure-categories}

| Boundary | Expected representation | Handling |
|---|---|---|
| Missing transport field | `DtoMappingError` | Return a value; do not invoke domain validation yet |
| Invalid domain primitive or union payload | `DtoMappingError` | Return a value and reject rehydration |
| Unknown schema version | `UnsupportedSchemaVersion` | Stop before interpreting version-specific payload |
| Corrupt or oversized snapshot | `BookingStoreError` | Keep a typed storage classification |
| I/O or replacement failure | `BookingStoreError` | Surface as an operational adapter failure |
| Payment refusal | `PaymentOutcome.Declined` | Expected service outcome, not an exception |
| Stubbed provider outage | `DependencyUnavailableException` | Fault the asynchronous operation and retain the stub cause as `InnerException` |
| Caller cancellation | cancelled `Task` / `OperationCanceledException` | Propagate the caller token; do not record new stub work |
| Domain refusal | `BookingDecisionError` | Remains in the pure workflow, not in the adapter |

One generic `Error of string` would erase which layer has authority to recover or report. Conversely, inventing a separate exception class for every domain refusal would turn ordinary business outcomes into control-flow surprises.

## Verify effects with real boundaries {#testing}

File-store contract tests write only to unique system temporary directories. They prove real JSON round trips, replacement without temporary residue, missing-file behavior, strict encoding, corruption categories, the size cap, path validation, and cancellation before save preserving the prior complete snapshot.

Adapter tests run the real file adapter and deterministic substitutes. They cover authorization, decline, delivery, exact faults, cancellation without recorded side effects, token propagation to the clock, persistence through the composed ports, typed corruption errors, repeated disposal, and use-after-disposal rejection.

The Release solution build passes with F# 10 null checking and warnings as errors. The complete example gate restores locked dependencies, builds every registered project, and runs tests and scripts. The capstone runtime projects add no third-party runtime package and require no service account; the test and tooling gate still restores its locked packages.

This evidence does not yet cover HTTP input, concurrent capacity, retry, restart of a multi-booking store, or a C# client. Those are the next three chapters, not hidden assumptions here.

## Avoid common boundary mistakes {#boundary-mistakes}

- Directly serializing a private domain record couples storage to compiler-oriented representation.
- Using a CLR enum for a data-carrying F# union loses its payload contract.
- Treating DTO mapping success as domain validity duplicates or bypasses Chapter 34's validation.
- Ignoring unknown JSON members can hide spelling mistakes and accidental producer drift.
- Writing straight to the target can expose a partial document after interruption.
- Calling a same-file replacement a database transaction overstates its scope.
- Deriving file names from request data introduces a path boundary this design does not need.
- Catching `OperationCanceledException` as an I/O failure destroys cancellation semantics.
- Random or delayed stubs make tests flaky without becoming more realistic.
- Constructing resources far from their disposal site makes ownership hard to review.
- Returning exception messages directly from a future API could disclose operational details.

## Exercises {#exercises}

### Exercise 1: evolve the snapshot contract {#exercise-01}

Version 2 must add an optional `customerNote`, while old version 1 files must still load. Propose DTOs and a mapping policy. State whether version 1 is upgraded in memory, rewritten immediately, or rewritten only on the next successful save. Define exact behavior for unknown fields and version 3.

### Exercise 2: audit every save interruption {#exercise-02}

For cancellation or failure (a) before temporary-file creation, (b) during write, (c) after flush but before move, and (d) after move, state what the target and temporary files may contain. Separate process-visible replacement, buffer flushing, and power-loss durability claims.

### Exercise 3: change ownership without ambiguity {#exercise-03}

Suppose production payment and notification clients are created by a host container and shared across several workflows. Redesign `Composition.start` so it borrows those clients instead of owning them. Show where disposal moves, how use-after-disposal is prevented, and how deterministic tests retain explicit success, refusal, fault, and cancellation.

[Read the chapter solutions](../solutions/ch-35-ports-persistence-config).

## Model review {#model-review}

- Ports state required capabilities; adapters choose mechanisms.
- DTOs are permissive representations, not domain entities.
- Tags, payloads, field names, casing, null omission, and versions form a JSON contract.
- Reverse mapping checks version, presence, smart constructors, and legal union shape.
- Raw command mapping preserves domain validation authority.
- A configured absolute path is distinct from a secret and from request input.
- Bounded strict decoding turns damaged files into explicit outcomes.
- Same-directory temporary write, flush, and move avoid exposing an in-place partial target.
- That replacement is not an atomic multi-operation business transaction.
- Deterministic stubs control outcomes without pretending to be network integrations.
- Cancellation is propagated before recording substitute side effects.
- The composition root constructs, exposes, and disposes what it owns.
- JSON, file-store, and adapter evidence proves this boundary; later chapters must still prove HTTP and consistency.

## Sources {#sources}

- [Microsoft Learn: customize `System.Text.Json` property names and enum representation](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/customize-properties)
- [Microsoft Learn: reject unmapped JSON members](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/missing-members)
- [FSharp.Core reference: `CLIMutableAttribute`](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-climutableattribute.html)
- [Microsoft Learn: `File.Move` overloads and cross-volume behavior](https://learn.microsoft.com/en-us/dotnet/api/system.io.file.move?view=net-10.0)
- [Microsoft Learn: `FileStream.Flush(Boolean)`](https://learn.microsoft.com/en-us/dotnet/api/system.io.filestream.flush?view=net-10.0)
- [Microsoft Learn: .NET configuration providers and environment-variable precedence](https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration-providers)
- [Microsoft Learn: `CancellationToken`](https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken?view=net-10.0)
