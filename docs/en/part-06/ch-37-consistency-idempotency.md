---
title: "Chapter 37: Consistency, Idempotency, Retries, and Partial Failure"
description: "Protect aggregate booking capacity, make command retries explicit, persist effect progress, and state the exact limits of a local F# consistency boundary."
translationKey: part-06/ch-37-consistency-idempotency
---

# Chapter 37: Consistency, Idempotency, Retries, and Partial Failure {#overview}

Chapter 36 made a dangerous interval observable: two requests can both read old capacity, a payment can succeed before local state changes, and a notification can fail after the booking is committed. Catching exceptions does not close any of those intervals. This chapter gives each one a state model and a deliberately narrow consistency boundary.

The in-page reference design adds `AtomicBookingStore` and `IdempotentBookingService` here. The first stores the whole activity aggregate and command progress; the second coordinates payment and notification against that progress. When implementing the design, focused verification should call this service directly; Chapter 38 then connects it to HTTP. That staging prevents evidence below HTTP from being misreported as deployed endpoint behavior.

The current repository still has no buildable project containing this design. In the proposed `Booking.Infrastructure` compile order, `AtomicBookingStore.fs` follows `FileStore.fs`. `Idempotency.fs` follows the payment/notification stubs and precedes `Composition.fs`.

Both code blocks are excerpts from larger files. The first requires `System.Collections.Concurrent`, `System.Threading`, and snapshot/phase types defined earlier in its file. The second requires the previously defined `PreparedCommand`, store operations, and external-service ports.

“Aggregate,” “idempotency,” and “transactional outbox” are domain-design and distributed-systems terms, not F#-specific vocabulary. F# contributes discriminated unions for phases, pattern matching for transitions, and `Result` plus `task` to separate expected outcomes from asynchronous work.

## Find the invariant's real owner {#aggregate-invariant}

The pure decider from Chapter 34 sees one `BookingState`. It can prove that a request for five seats does not fit an activity whose total capacity is four. It cannot prove that two separate two-seat bookings fit a capacity of three, because neither booking state contains the other.

That is not a defect in `Booking.create`. It reveals two different invariants:

| Invariant | Required state | Current owner |
|---|---|---|
| one request has a positive seat count no greater than total capacity | activity plus one command | domain constructor and decider |
| all live bookings and in-flight reservations fit together | every booking and reservation for the activity | aggregate consistency boundary |

Putting the second check into the single-booking decider without supplying aggregate state would only make the function look authoritative. A rule can be enforced only where all facts used by that rule are read and committed consistently.

## Reproduce overselling before fixing it {#overselling-race}

Suppose capacity is three and two callers each request two seats:

| Step | Request A | Request B | Stored occupied seats |
|---|---|---|---|
| 1 | reads `0` | | `0` |
| 2 | | reads `0` | `0` |
| 3 | computes `0 + 2 <= 3` | | `0` |
| 4 | | computes `0 + 2 <= 3` | `0` |
| 5 | writes two seats | | `2` |
| 6 | | writes two seats | `4` |

Both checks are locally correct. Their composition is wrong because “read occupied seats, decide, write accepted state” was not one serialized or conditional operation.

A thread-safe dictionary would not repair this sequence. Safe individual `get` and `set` calls can still surround a stale decision. A lock only around file writing would also be too late: both callers already accepted from the same old state.

The minimum atomic region contains:

```text
load all capacity-relevant state
  -> calculate occupied and reserved seats
  -> run the pure command decision
  -> reject, or persist the accepted reservation/state transition
```

“Atomic” here means that another cooperating workflow cannot observe the middle and commit a competing decision. It does not mean every later external effect joins the same transaction.

## Define seat accounting before choosing a primitive {#seat-accounting}

The consistency boundary uses one explicit policy:

```text
occupied = seats in Pending bookings + seats in Confirmed bookings
reserved = seats in placement operations at Reserved or PaymentStarted
required invariant: occupied + reserved <= activity capacity
```

`Cancelled` bookings occupy zero seats. Confirmation changes status but not occupancy. A placement reservation occupies seats before payment so a slow payment cannot let another request claim the same capacity. A recorded decline stops occupying seats.

This is a business choice, not a universal ticketing rule. Another system might hold seats for a deadline, release them when payment expires, separate wheelchair inventory, or require confirmation before occupancy. Such a change belongs in the stated policy and its tests, not in a different semaphore API.

Capacity rejection itself is not persisted as a terminal idempotent result. If another booking is cancelled, the same waiting placement may be tried again and can now succeed. In contrast, a provider-declined payment is persisted as terminal for that placement identity. The difference is intentional: available capacity can change; this sample has no new payment-method input with which to reconsider a decline.

## Store the activity aggregate, not one booking {#aggregate-snapshot}

The earlier `FileBookingStore` saved one `BookingDto`; saving another request replaced it. This chapter introduces a separate versioned snapshot containing:

- the event ID and configured capacity;
- every current booking keyed by normalized request ID;
- every command's kind, request ID, payload fingerprint, progress phase, and candidate booking.

Persistence-only CLR DTOs remain separate from protected domain types. On load, `BookingMapping` converts strict JSON back to domain values. Further checks reject duplicate keys, event mismatches, impossible phase/kind combinations, oversized seat counts, broken operation links, multiple unfinished operations for one request, and aggregate overselling.

The persisted event ID and capacity must match the activity supplied by the process. A different restart configuration produces `SnapshotActivityMismatch`; silently interpreting old bookings under a new capacity would make recovery look successful while changing the invariant.

The snapshot is limited to 1 MiB and read as strict UTF-8. This is ample for the teaching workload, not an unbounded production database. Retention, archival, migration beyond schema 1, backup, encryption, and tamper protection remain explicit omissions.

## Put the gate around the decision {#process-local-gate}

Every `AtomicBookingStore` constructed for the same normalized path retrieves shared state and workflow gates. Case-only path variants conservatively share a gate. The workflow gate surrounds the complete application command; the state gate protects each snapshot read or replacement.

```fsharp:line-numbers [AtomicBookingStore.fs]
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
```
`WaitAsync cancellationToken` lets a cancelled caller leave while waiting. `finally` pairs every successful entry with `Release`. Microsoft documents `SemaphoreSlim` as a local semaphore for synchronization within one application and explicitly says it does not support named system semaphores. That is exactly the implemented scope, not a hidden distributed lock. See the [.NET 10 `SemaphoreSlim` documentation](https://learn.microsoft.com/en-us/dotnet/api/system.threading.semaphoreslim?view=net-10.0).

Holding one workflow gate while a payment or notification runs is conservative. It prevents two service instances in this process from launching the same effect concurrently, and keeps the example understandable. It also means one slow dependency blocks unrelated bookings for this activity. `SemaphoreSlim` does not promise FIFO fairness.

For a small local application this tradeoff is honest. For high throughput, partition by activity, use conditional database updates, and move delivery work out of the request path. Do not remove coordination first and hope tests still pass.

The aggregate decision and first persisted phase happen under the state gate:

```fsharp:line-numbers [AtomicBookingStore.fs]
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
```
The domain decider still decides legal lifecycle transitions. The store adds only the aggregate fact the one-booking state cannot know. Accepted placement first records a reservation; confirmation and cancellation update the booking and record pending notification together.

## Do not confuse safe replacement with a database transaction {#file-replacement}

The writer serializes the complete DTO, checks the bound, creates a random temporary file in the destination directory, writes with `WriteThrough`, calls `Flush(true)`, and then uses `File.Move(temp, destination, true)`.

Microsoft documents that [`Flush(true)` clears intermediate file buffers](https://learn.microsoft.com/en-us/dotnet/api/system.io.filestream.flush?view=net-10.0) and that [`File.Move` with `overwrite = true` replaces an existing destination](https://learn.microsoft.com/en-us/dotnet/api/system.io.file.move?view=net-10.0). Keeping the temporary file in the same directory also avoids the documented cross-volume copy behavior.

Those API facts do not establish ACID durability under every filesystem, power loss, directory-metadata failure, network share, antivirus hook, or hardware cache. After implementation, file tests can establish complete replacement and orderly restart only on the tested environment; they do not simulate power removal unless explicit fault injection is added. A production durability claim needs the actual filesystem, mount, storage, backup, and recovery evidence.

Within this process, cooperating readers use the same state gate, so they do not parse the temporary file or read during replacement. A second OS process does not share that gate and can race. The class XML documentation states this limit directly.

## Give an operation a stable identity {#idempotency-identity}

A request ID identifies a booking, but one booking legitimately receives placement, confirmation, and cancellation commands. Therefore the consistency design defines an operation key from:

```text
operation kind + normalized request ID
```

It separately hashes a length-prefixed canonical sequence containing the kind, normalized ID, and normalized command payload. Trimming happens through domain validation before this fingerprint is computed. `" REQ-7 "` and `"REQ-7"` therefore identify the same placement; one seat and two seats do not.

The SHA-256 fingerprint is a compact equality token, not a password hash, signature, or authorization decision. The snapshot is not trusted merely because the fingerprint looks valid. All persisted fields are still parsed and checked.

The resulting policy is precise:

| Incoming command | Stored evidence | Result |
|---|---|---|
| same kind, normalized ID, and payload; phase `Completed` | exact acknowledged result | replay stored candidate; no payment or notification |
| same kind and ID, different payload | key exists, fingerprint differs | `IdempotencyConflict`; no effect |
| different kind for the same booking after completion | separate operation key | run the next legal lifecycle decision |
| different kind while an earlier operation is unfinished | unfinished progress exists | `PreviousOperationIncomplete` |
| invalid command | no protected identity can be formed | domain validation error; no store access |
| aggregate capacity currently unavailable | no terminal record | reject now; a later retry may re-evaluate |

A safe idempotency identity combines two parts. The stable operation key gives the server a durable address for progress, while the payload fingerprint detects a reused key carrying a different seat count. Require both before replaying an earlier result.

## Separate HTTP method semantics from application idempotency {#http-idempotency}

RFC 9110 defines an idempotent HTTP method by the intended server effect of repeated identical requests. It identifies safe methods, `PUT`, and `DELETE` as idempotent; `POST` is not inherently so. It also says a client should not automatically retry a non-idempotent request unless it knows the request semantics are idempotent or knows the original was not applied. See [HTTP Semantics, section 9.2.2](https://www.rfc-editor.org/rfc/rfc9110.html#section-9.2.2).

The stored identity rules make one application command retry-safe under its exact operation key and payload fingerprint. HTTP method semantics and intermediary retry authority remain separate policies. The HTTP boundary must expose reused-key conflicts and ambiguous-payment outcomes so a client can act deliberately.

The Microsoft [Retry pattern](https://learn.microsoft.com/en-us/azure/architecture/patterns/retry) makes the same distinction operationally: classify faults, bound attempts, and ask whether an operation is idempotent before repeating it. “Received `503`” is not enough context to decide that a charge is safe to repeat.

## Persist progress around external effects {#effect-progress}

An exact command moves through durable phases:

```text
Place:
  Reserved -> PaymentStarted -> NotificationPending -> Completed
                              \-> PaymentDeclined

Confirm / Cancel:
  NotificationPending -> Completed
```

The phase names describe evidence, not optimism:

- `Reserved`: capacity is held; payment has definitely not been started by this workflow;
- `PaymentStarted`: the provider may have acted, so the result is unknown after interruption;
- `PaymentDeclined`: a provider refusal was recorded and will be replayed without charging;
- `NotificationPending`: the booking state is committed and notification remains deliverable;
- `Completed`: the modeled effects returned successfully and completion was persisted.

The service orders storage and effects accordingly:

```fsharp:line-numbers [Idempotency.fs]
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
```
Before calling payment, the service changes `Reserved` to `PaymentStarted`. A crash after that write but before the provider call creates a conservative false positive: recovery says “unknown” even if no charge occurred. The alternative—calling first and recording later—creates a window in which a completed charge looks absent and is blindly repeated. For money, stopping for reconciliation is the safer sample policy.

After authorization returns, the booking and `NotificationPending` phase are saved in one aggregate replacement. A notification failure therefore cannot erase the booking. Retrying the same command skips payment and attempts only the pending notification.

After notification returns, the service persists `Completed`. If delivery succeeded but the acknowledgment was lost, the process crashed, or that final write failed, the snapshot can still say pending. Retrying may notify again. That is at-least-once delivery.

## Read a retry result from durable evidence {#retry-matrix}

The word “retry” hides several different transitions:

| Last durable evidence | First observed failure | Exact retry behavior | Side-effect policy |
|---|---|---|---|
| no operation | validation or domain refusal | decide again | no effect occurred |
| no operation | aggregate capacity refusal | re-evaluate current capacity | may succeed after cancellation |
| `Reserved` | cancellation before payment start | start payment once | reservation remains safe to resume |
| `PaymentStarted` | payment call fault, cancellation, or crash | `PaymentOutcomeUnknown` | do not charge automatically |
| `PaymentDeclined` | expected decline | replay decline | do not charge again |
| `NotificationPending` | notification fault after commit | retry notification only | payment and booking are not repeated |
| `Completed` | response lost after completion | replay stored booking | no modeled effect is repeated |

The first payment exception returns `DependencyUnavailable` because that call failed. The next exact attempt sees `PaymentStarted` and returns `PaymentOutcomeUnknown`. These two observations differ because durable knowledge changed before the call.

An operator or provider lookup must resolve an unknown payment. A real workflow could record `Authorized`, `Declined`, or `Released` after querying by a provider idempotency key. This teaching slice deliberately refuses to invent an answer or automatically free seats.

## Understand why exactly once is not local {#exactly-once}

The snapshot file and a payment provider are two independent systems. No local `SemaphoreSlim`, hash, or `File.Move` can commit both as one transaction. The same is true for the file and an email or message service.

There are only honest ways to narrow the gap:

- give the provider a stable idempotency key and let it durably deduplicate requests;
- query the provider by that key before deciding whether an ambiguous operation may continue;
- save business state and an outbox entry in one real storage transaction;
- let a separate relay retry the outbox entry;
- give every consumer a stable message ID and an idempotent handler;
- define compensation when an effect cannot be made safely repeatable.

Microsoft's [transactional outbox guidance](https://learn.microsoft.com/en-us/azure/architecture/databases/guide/transactional-out-box-cosmos) stores the business object and event in the same database transaction, then has another process publish pending events. It also discusses duplicate publication during replay and downstream duplicate detection. The transaction prevents a lost local intent; it does not turn the remote send into the same transaction.

Persisting `NotificationPending` with the booking resembles a tiny inline outbox. It is not a full outbox: there is no independent worker, lease, backoff, dead-letter policy, ordering policy, or retention cleanup. Calling it one would overstate the implementation.

## Design orderly-restart verification {#restart-recovery}

The design goal is for every decision-relevant value to survive in the aggregate snapshot. A new process should reconstruct domain bookings, command phases, capacity accounting, and exact replay results without relying on an in-memory cache.

After implementing the reference design, a restart test should do more than instantiate a second object. It can complete a placement, read the JSON to confirm schema 1 and absence of the payment transaction text, then launch a separate `dotnet fsi` process. The child loads the built assemblies and the same snapshot, receives payment and notification functions that fail if invoked, and repeats the placement.

The expected child output is:

```text
restored|REQ-RESTART|2|pending
```

Zero exit status, the output above, and exactly one call in each parent-process stub together show that persisted completion replayed without invoking payment or notification again.

Such a test is orderly-restart evidence only. It does not prove simultaneous multi-process writing, recovery from every instruction-level crash, or survival after disk loss. Those require different storage and fault-injection tests.

## State the guarantee as a table {#guarantee-table}

| Question | Reference-design claim (still verify after implementation) |
|---|---|
| Can two controlled commands in one process oversell one activity? | no, when they use `IdempotentBookingService` and the same configured path |
| Do pending and confirmed bookings consume seats? | yes |
| Does cancellation release seats? | yes, when its booking transition is committed |
| Does an exact completed retry charge or notify again? | no |
| Does the same operation key accept different content? | no; it conflicts |
| Is an uncertain payment automatically retried? | no; it requires reconciliation |
| Can a failed notification be retried? | yes, without repeating payment or booking commit |
| Can notification be delivered more than once after an ambiguous acknowledgment? | yes |
| Does state survive a new process with matching activity configuration? | the design intends yes; verify it with the separate-process test above |
| Can two OS processes or containers safely write the file concurrently? | no |
| Is the snapshot an ACID, replicated, encrypted, backed-up database? | no |
| Do the Chapter 36 HTTP endpoints already use this service? | no; final integration is Chapter 38 |

The narrow wording is part of correctness. “Thread safe,” “atomic,” “durable,” and “idempotent” are incomplete claims unless they name scope, state, failures, and observers.

## Design race tests with causal control {#deterministic-tests}

After implementation, each competition test should create both tasks, have each signal readiness, and hold them behind a `TaskCompletionSource`. Only after both are ready should the test release them. No assertion should depend on which request wins.

For capacity three and two requests of two seats, the required outcome is:

- exactly one `Ok booking`;
- exactly one `AggregateCapacityExceeded(2, 1)`;
- one payment and one notification call;
- total persisted occupied seats equal two.

The duplicate test should release two normalized forms of the same command together. Both should receive success, while the counters remain one payment and one notification. Reusing the same operation key for a different seat count should yield `IdempotencyConflict` without changing either counter.

Add cases showing that notification failure commits the booking and a retry sends only notification; that an unknown payment does not cause a second charge; that cancellation releases capacity; and that a separate process replays completed work.

These focused tests should use a controlled happens-before structure instead of timing sleeps. That does not prove every possible schedule, but causal control is stronger evidence than `Task.Delay(50)` followed by an assertion that merely tends to win.

## Choose a production boundary from requirements {#production-upgrades}

The local design is intentionally serialized and easy to inspect. Upgrade the mechanism when a requirement crosses its boundary:

| Requirement | Candidate mechanism | Evidence still required |
|---|---|---|
| several API processes write one activity | database transaction with row/key-range locking, or optimistic version/ETag plus retry | conflicting writes cannot both commit |
| many independent activities | partition/lock by activity ID | hot-key behavior and cross-activity isolation |
| reliable deferred notification | transactional outbox plus worker | relay recovery, duplicate handling, ordering, retention |
| payment retry after timeout | provider-supported idempotency key and lookup | exact provider semantics and retention window |
| no indefinite reservation | expiry command driven by stored time and reconciliation policy | clock, races, payment lookup, and release tests |
| regional durability | replicated managed store and tested backup/restore | stated consistency level, RPO, RTO, failover exercise |

Optimistic concurrency is often preferable to a process-wide lock when collisions are rare. A conditional write says “commit only if the version I read is still current”; a loser reloads and decides again. The invariant still needs all capacity-relevant state in one transactional or conditional boundary.

Microsoft's [minimize coordination guidance](https://learn.microsoft.com/en-us/azure/architecture/guide/design-principles/minimize-coordination) recommends idempotent operations and optimistic concurrency where possible, while also recognizing that some invariants require coordination. “Minimize” does not mean “delete.” Measure contention before choosing a more elaborate design.

## Notice what F# contributes—and what it cannot {#fsharp-role}

F# makes progress and failure states explicit. `AtomicOperationPhase` prevents a magic string inside orchestration; `BookingConsistencyError` forces callers to distinguish capacity, conflict, decline, unknown payment, dependency failure, and storage failure. Pattern matches show the policy's branches, while protected domain constructors remain the rehydration authority.

`Result` separates declared outcomes from faults. `task` composes file and dependency work while preserving cancellation. Records make an immutable candidate easy to carry between phases. None of these language features supplies a transaction across two systems.

The useful F# lesson is not “functional programming solves concurrency.” It is that precise data types let the code name knowledge, and pure decisions reduce the region that needs coordination. The remaining coordination must still match the deployment topology.

## Exercises {#exercises}

### Exercise 1: cross the process boundary {#exercise-01}

The API must run as three replicas and each event has independent capacity. Replace the local gate and snapshot with a storage design that prevents overselling. Specify the aggregate key, persisted version, conditional or transactional write, conflict retry loop, and how cancellation affects a retry. State whether one hot event can block unrelated events, and name a test that forces two replicas to use the same version.


::: details Answer

#### Make the activity the concurrency key {#exercise-01-key}

Use the normalized event ID as the aggregate or partition key. One durable aggregate contains:

- the event ID, capacity, and a monotonically changing version or ETag;
- all booking states that contribute to occupancy;
- all unfinished reservations that contribute to occupancy;
- the operation key, payload fingerprint, phase, and replay result for each command.

This is the same information needed by the current consistency design, but the storage engine—not a process-local semaphore—must reject a stale commit. A relational design might lock one activity row and update its dependent rows in one transaction. A key-value or document design might conditionally replace one activity document only when its ETag matches.

Partitioning by event ID means one hot event can contend with itself without blocking unrelated events. That benefit holds only if no global table lock, singleton worker, or shared transaction serializes every partition. A booking that spans multiple events would cross this boundary and require a different model.

#### Re-decide after a version conflict {#exercise-01-loop}

The conceptual loop is:

```fsharp
let rec execute remaining cancellationToken = task {
    cancellationToken.ThrowIfCancellationRequested()
    let! snapshot, version = store.Load(eventId, cancellationToken)
    let decision = decideAgainstAggregate command snapshot

    match decision with
    | Error error -> return Error error
    | Ok nextSnapshot ->
        match! store.TryReplace(eventId, version, nextSnapshot, cancellationToken) with
        | Written -> return Ok nextSnapshot
        | VersionConflict when remaining > 0 ->
            return! execute (remaining - 1) cancellationToken
        | VersionConflict -> return Error ContentionLimitExceeded
}
```

The retry reruns the pure decision against freshly loaded state. It must not merely attempt the same stale write. If the competing command consumed the last seat, the second decision returns capacity rejection. If it cancelled an occupied booking, the second decision may now accept the waiting placement.

Bound attempts, observe cancellation, and add jittered backoff only when repeated collisions justify it. Storage timeouts, authentication failures, corrupt data, and domain rejections are not version conflicts. The [Retry pattern](https://learn.microsoft.com/en-us/azure/architecture/patterns/retry) recommends classifying faults rather than repeating every failure.

Payment must not run before the capacity reservation wins its conditional commit. The external-operation phases and provider key from the chapter remain necessary after replacing the local store.

#### Force both writers to use one version {#exercise-01-test}

An integration test should start two independent service hosts against the same real storage partition. A barrier in a test storage hook pauses both after reading version 12. Release both conditional writes together and assert:

- exactly one write using expected version 12 commits;
- the loser reads version 13 and re-decides;
- capacity is never exceeded in committed state;
- only the winner starts payment when no remaining capacity exists;
- cancellation of one existing booking permits the loser on its next decision, if the policy allows it;
- simultaneous commands for two different event IDs can both progress.

Do not replace the storage engine's concurrency test with two objects in one process. That can accidentally share a lock and never exercise the production conflict primitive.

:::

### Exercise 2: reconcile an unknown payment {#exercise-02}

Extend the progress model without writing code. Add the provider key and the minimum states needed for an operator or background job to query an ambiguous payment. Define transitions for provider reports `Authorized`, `Declined`, and `NotFound`. Decide when seats remain reserved, when they are released, and which transitions may send notification. Include a conflict rule for changed command payload.


::: details Answer

#### Model provider status separately {#exercise-02-model}

Add a stable provider idempotency key derived from the operation identity and persisted before any provider call. A useful minimum model is:

| Local phase | Durable knowledge | Seat policy |
|---|---|---|
| `Reserved` | no provider attempt has begun | reserved |
| `PaymentStarted` | a request may have reached the provider | reserved |
| `PaymentUnknown` | the call ended ambiguously and needs lookup | reserved |
| `Authorized` | provider confirms authorization | occupied as booking |
| `Declined` | provider confirms refusal | released |
| `Released` | policy or operator completed a safe release | released |
| `NotificationPending` | booking and local delivery intent are committed | occupied as booking |
| `Completed` | modeled delivery acknowledged and result stored | occupied as booking |

`PaymentStarted` can become `PaymentUnknown` when the call faults, times out, is cancelled, or recovery finds it unfinished. Keeping both states is useful if the first is a short-lived execution marker and the second schedules reconciliation; they may be one state if no behavior differs.

#### Let lookup results drive transitions {#exercise-02-transitions}

Query the provider by its stable key. The response does not directly authorize arbitrary mutation; it is input to a checked transition:

| Provider report | Allowed local action | Notification |
|---|---|---|
| `Authorized(providerTransactionId)` | persist the provider reference, commit the reserved candidate as the booking, and save `NotificationPending` atomically | may run only after that commit |
| `Declined(reasonCode)` | persist terminal decline and release the reservation | none |
| `NotFound` before the provider's documented finality window | keep `PaymentUnknown`; schedule another lookup | none |
| `NotFound` after a documented finality/retention threshold | follow an explicit release or manual-review policy | none before a safe terminal decision |
| transport or provider failure | retain `PaymentUnknown`; retry lookup with a bounded policy | none |

`NotFound` is not automatically equivalent to “never charged.” The request may still be in flight, indexed later, visible in a different API, or outside the provider's lookup retention. The integration must use the provider's documented semantics, not a convenient guess.

Every reconciliation command carries the original operation key and stored fingerprint. A changed request ID, event ID, seat count, or other decision-relevant payload produces `IdempotencyConflict`; reconciliation never edits the original command to make a lookup fit.

Only one checked transition may convert the reservation to a booking. Repeated `Authorized` callbacks or operator retries replay that state. A late contradictory provider report is an operational inconsistency for review, not permission to run both transitions.

:::

### Exercise 3: turn pending notification into an outbox {#exercise-03}

Design a real outbox for booking notifications. Specify:

- what is saved with the booking;
- how a worker claims work;
- how retries and backoff are recorded;
- how a stable message ID reaches consumers;
- how consumers deduplicate;
- what happens after the retry limit.

Distinguish “no lost local intent,” “at-least-once publication,” and “the same observable consumer outcome.”


::: details Answer

#### Commit business state and intent together {#exercise-03-commit}

In the same database transaction that commits the booking transition, insert an outbox row such as:

```text
messageId, eventId, requestId, operationKind,
messageType, schemaVersion, payload,
status, attemptCount, nextAttemptAt,
leaseOwner, leaseUntil, createdAt, completedAt
```

`messageId` is stable across every publish attempt. The payload is the versioned integration contract, not a serialized private F# domain object. The unique operation/message constraint prevents the request transaction from inserting two logical notifications.

If the transaction rolls back, neither booking change nor delivery intent exists. If it commits, both exist. This is the outbox's central guarantee: no lost local intent between those two records.

#### Lease, send, and acknowledge {#exercise-03-relay}

A relay performs a recoverable protocol:

1. Select eligible pending rows whose `nextAttemptAt` has arrived.
2. Claim each row with a conditional update and a bounded lease.
3. Publish the versioned payload with the stable `messageId`.
4. On acknowledged success, conditionally mark the row completed.
5. On a classified transient failure, increment `attemptCount`, record sanitized diagnostics, and schedule bounded exponential backoff with jitter.
6. On a permanent failure or exhausted policy, move to a dead-letter/review state and alert an owner.
7. Reclaim an expired lease after a worker crash.

Keep the database transaction short; do not hold it open across the broker call. That choice creates an unavoidable crash window:

| Crash point | Durable row | Recovery result |
|---|---|---|
| before claim commits | pending | another worker may claim |
| after claim, before publish | leased | lease expires, then publish is retried |
| after publish, before completion update | leased or pending later | the same message may be published again |
| after completion update | completed | normal scanning skips it |

The third row is why the relay is at least once. A broker-side deduplication feature can reduce duplicates, but its key scope and retention window must be verified; it does not justify an unqualified exactly-once claim.

#### Deduplicate at the consumer boundary {#exercise-03-consumer}

Each consumer stores processed `messageId` values in the same local transaction as its own state change. On receipt:

- if the ID is new, apply the handler and record the ID atomically;
- if the ID already exists, acknowledge without applying the state change again;
- if either local write fails, do not acknowledge, so redelivery can retry both together.

Retention must cover the maximum broker redelivery and replay horizon. The handler itself should also favor naturally idempotent state assignments over additive effects. An email send, third-party webhook, or physical action introduces another independent boundary and needs its own key, lookup, or reconciliation policy.

The resulting claims are deliberately different:

| Claim | What the design guarantees |
|---|---|
| no lost local intent | booking change and outbox row commit or roll back together |
| at least once publish attempt | while storage and the relay remain available, an unfinished row stays recoverable and retryable |
| same observable consumer outcome | a consumer atomically deduplicates by stable message ID |
| exactly once in every external system | not established |

Monitor pending age, attempt count, expired leases, dead-letter volume, end-to-end latency, and duplicate rate. Define ordering per aggregate if consumers require it, archive completed rows after the replay horizon, and test worker death at every row in the crash table.

Microsoft's [transactional outbox guidance](https://learn.microsoft.com/en-us/azure/architecture/databases/guide/transactional-out-box-cosmos) likewise separates the local transaction from later publication and calls out duplicate handling. The exact schema and lease mechanism remain database-specific.

:::


Chapter 38 will wire this service through HTTP, add a C# contract client and end-to-end tests, then finish diagnostics and release evidence.
