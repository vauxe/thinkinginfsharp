---
title: "Chapter 37: Consistency, Idempotency, Retries, and Partial Failure"
description: "Protect aggregate booking capacity, make command retries explicit, persist effect progress, and state the exact limits of a local F# consistency boundary."
translationKey: part-06/ch-37-consistency-idempotency
kind: chapter
part: 6
chapter: 37
status: complete
verifiedWith:
  fsharp: "10"
  dotnetSdk: "10.0.301"
exampleIds:
  - capstone-booking-domain
  - capstone-booking-contracts
  - capstone-booking-infrastructure
  - foundation-contract-tests
exerciseIds:
  - ch37-exercise-01
  - ch37-exercise-02
  - ch37-exercise-03
termIds: []
sources:
  - id: microsoft-semaphore-slim
    url: https://learn.microsoft.com/en-us/dotnet/api/system.threading.semaphoreslim?view=net-10.0
    checked: "2026-08-25"
  - id: microsoft-file-move
    url: https://learn.microsoft.com/en-us/dotnet/api/system.io.file.move?view=net-10.0
    checked: "2026-08-25"
  - id: microsoft-file-flush
    url: https://learn.microsoft.com/en-us/dotnet/api/system.io.filestream.flush?view=net-10.0
    checked: "2026-08-25"
  - id: microsoft-retry-pattern
    url: https://learn.microsoft.com/en-us/azure/architecture/patterns/retry
    checked: "2026-08-25"
  - id: microsoft-transactional-outbox
    url: https://learn.microsoft.com/en-us/azure/architecture/databases/guide/transactional-out-box-cosmos
    checked: "2026-08-25"
  - id: microsoft-minimize-coordination
    url: https://learn.microsoft.com/en-us/azure/architecture/guide/design-principles/minimize-coordination
    checked: "2026-08-25"
  - id: ietf-http-semantics
    url: https://www.rfc-editor.org/rfc/rfc9110.html#section-9.2.2
    checked: "2026-08-25"
---

# Chapter 37: Consistency, Idempotency, Retries, and Partial Failure {#overview}

Chapter 36 made a dangerous interval observable: two requests can both read old capacity, a payment can succeed before local state changes, and a notification can fail after the booking is committed. Catching exceptions does not close any of those intervals. This chapter gives each one a state model and a deliberately narrow consistency boundary.

This chapter adds `AtomicBookingStore` and `IdempotentBookingService`. The first stores the whole activity aggregate and command progress; the second coordinates payment and notification against that progress. The focused tests call this service directly. The Chapter 36 HTTP endpoint still uses its earlier `AsyncPorts` workflow; Chapter 38 will connect the consistent service to the final API. Keeping that staging explicit prevents test evidence below HTTP from being misreported as deployed endpoint behavior.

## What you will be able to do {#outcomes}

By the end of this chapter, you should be able to:

- distinguish a booking-local invariant from aggregate capacity;
- exhibit the read-check-write race that causes overselling;
- choose one atomic boundary that contains every value used by a capacity decision;
- define which lifecycle states occupy and release seats;
- distinguish request identity, operation identity, and a normalized payload fingerprint;
- state when an exact retry replays, resumes, conflicts, or requires reconciliation;
- persist effect progress without pretending a local file controls a payment provider;
- explain why notification delivery is at least once rather than exactly once;
- separate a transient failure from an ambiguous outcome;
- test concurrency with signals instead of timing guesses;
- prove recovery with a genuinely separate process;
- state the single-process, single-path limits of the local adapter;
- choose a production upgrade only when deployment requirements demand it.

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

Persistence-only CLR DTOs remain separate from protected domain types. On load, strict JSON is mapped back through `BookingMapping`, then checked for duplicate keys, event mismatches, impossible phase/kind combinations, oversized seat counts, broken operation links, multiple unfinished operations for one request, and aggregate overselling.

The persisted event ID and capacity must match the activity supplied by the process. A different restart configuration produces `SnapshotActivityMismatch`; silently interpreting old bookings under a new capacity would make recovery look successful while changing the invariant.

The snapshot is limited to 1 MiB and read as strict UTF-8. This is ample for the teaching workload, not an unbounded production database. Retention, archival, migration beyond schema 1, backup, encryption, and tamper protection remain explicit omissions.

## Put the gate around the decision {#process-local-gate}

Every `AtomicBookingStore` constructed for the same normalized path retrieves shared state and workflow gates. Case-only path variants conservatively share a gate. The workflow gate surrounds the complete application command; the state gate protects each snapshot read or replacement.

<<< @/../examples/capstone/src/Booking.Infrastructure/AtomicBookingStore.fs#process-local-gates{fsharp:line-numbers} [AtomicBookingStore.fs]

`WaitAsync cancellationToken` lets a cancelled caller leave while waiting. `finally` pairs every successful entry with `Release`. Microsoft documents `SemaphoreSlim` as a local semaphore for synchronization within one application and explicitly says it does not support named system semaphores. That is exactly the implemented scope, not a hidden distributed lock. See the [.NET 10 `SemaphoreSlim` documentation](https://learn.microsoft.com/en-us/dotnet/api/system.threading.semaphoreslim?view=net-10.0).

Holding one workflow gate while a payment or notification runs is conservative. It prevents two service instances in this process from launching the same effect concurrently, and keeps the example understandable. It also means one slow dependency blocks unrelated bookings for this activity. `SemaphoreSlim` does not promise FIFO fairness.

For a small local application this tradeoff is honest. For high throughput, partition by activity, use conditional database updates, and move delivery work out of the request path. Do not remove coordination first and hope tests still pass.

The aggregate decision and first persisted phase happen under the state gate:

<<< @/../examples/capstone/src/Booking.Infrastructure/AtomicBookingStore.fs#atomic-capacity-decision{fsharp:line-numbers} [AtomicBookingStore.fs]

The domain decider still decides legal lifecycle transitions. The store adds only the aggregate fact the one-booking state cannot know. Accepted placement first records a reservation; confirmation and cancellation update the booking and record pending notification together.

## Do not confuse safe replacement with a database transaction {#file-replacement}

The writer serializes the complete DTO, checks the bound, creates a random temporary file in the destination directory, writes with `WriteThrough`, calls `Flush(true)`, and then uses `File.Move(temp, destination, true)`.

Microsoft documents that [`Flush(true)` clears intermediate file buffers](https://learn.microsoft.com/en-us/dotnet/api/system.io.filestream.flush?view=net-10.0) and that [`File.Move` with `overwrite = true` replaces an existing destination](https://learn.microsoft.com/en-us/dotnet/api/system.io.file.move?view=net-10.0). Keeping the temporary file in the same directory also avoids the documented cross-volume copy behavior.

Those API facts do not establish ACID durability under every filesystem, power loss, directory-metadata failure, network share, antivirus hook, or hardware cache. The tests establish complete replacement and orderly restart on the tested environment. They do not simulate power removal. A production durability claim needs the actual filesystem, mount, storage, backup, and recovery evidence.

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

An idempotency key without payload comparison is dangerous: a client bug could reuse `REQ-7` for a different seat count and receive an unrelated old success. A fingerprint without a stable operation key is also insufficient: the server would have no durable address at which to find progress.

## Separate HTTP method semantics from application idempotency {#http-idempotency}

RFC 9110 defines an idempotent HTTP method by the intended server effect of repeated identical requests. It identifies safe methods, `PUT`, and `DELETE` as idempotent; `POST` is not inherently so. It also says a client should not automatically retry a non-idempotent request unless it knows the request semantics are idempotent or knows the original was not applied. See [HTTP Semantics, section 9.2.2](https://www.rfc-editor.org/rfc/rfc9110.html#section-9.2.2).

The stored identity rules make one application command retry-safe. That does not relabel every `POST` request, authorize an arbitrary proxy to retry it, or make a reused ID with different content safe. The eventual HTTP boundary must expose conflict and ambiguous-payment outcomes so a client can act deliberately.

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

<<< @/../examples/capstone/src/Booking.Infrastructure/Idempotency.fs#effect-progress{fsharp:line-numbers} [Idempotency.fs]

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

## Recover from an orderly restart {#restart-recovery}

Every decision-relevant value survives in the aggregate snapshot. A new process can reconstruct domain bookings, command phases, capacity accounting, and exact replay results without relying on an in-memory cache.

The restart test does not merely instantiate a second object. It completes a placement, reads the JSON to confirm schema 1 and absence of the payment transaction text, then launches a separate `dotnet fsi` process. That process loads the built assemblies and the same snapshot, supplies payment and notification functions that fail if invoked, and repeats the placement.

The child prints:

```text
restored|REQ-RESTART|2|pending
```

Exit code zero proves that persisted completion was replayed without either forbidden effect. The parent process then verifies its original stubs still have one payment and one notification call.

This is orderly restart evidence. It does not prove simultaneous multi-process writing, recovery from every instruction-level crash, or survival after disk loss. Those require different storage and fault-injection tests.

## State the guarantee as a table {#guarantee-table}

| Question | Current answer |
|---|---|
| Can two controlled commands in one process oversell one activity? | no, when they use `IdempotentBookingService` and the same configured path |
| Do pending and confirmed bookings consume seats? | yes |
| Does cancellation release seats? | yes, when its booking transition is committed |
| Does an exact completed retry charge or notify again? | no |
| Does the same operation key accept different content? | no; it conflicts |
| Is an uncertain payment automatically retried? | no; it requires reconciliation |
| Can a failed notification be retried? | yes, without repeating payment or booking commit |
| Can notification be delivered more than once after an ambiguous acknowledgment? | yes |
| Does state survive a new process with matching activity configuration? | yes, in the tested orderly-restart scenario |
| Can two OS processes or containers safely write the file concurrently? | no |
| Is the snapshot an ACID, replicated, encrypted, backed-up database? | no |
| Do the Chapter 36 HTTP endpoints already use this service? | no; final integration is Chapter 38 |

The narrow wording is part of correctness. “Thread safe,” “atomic,” “durable,” and “idempotent” are incomplete claims unless they name scope, state, failures, and observers.

## Test races with causality, not elapsed time {#deterministic-tests}

The two competition tests create both tasks, have each signal readiness, and hold them behind a `TaskCompletionSource`. Only after both are ready does the test release them. No assertion depends on which request wins.

For capacity three and two requests of two seats, the required outcome is:

- exactly one `Ok booking`;
- exactly one `AggregateCapacityExceeded(2, 1)`;
- one payment and one notification call;
- total persisted occupied seats equal two.

The duplicate test releases two normalized forms of the same command together. Both receive success, but the counters remain one payment and one notification. Reusing the same operation key for a different seat count yields `IdempotencyConflict` without changing either counter.

Other tests prove that notification failure commits the booking and retries only notification, payment fault becomes unknown and is not charged twice, cancellation frees capacity for a previously refused request, and a separate process replays completion.

The focused tests use a controlled happens-before structure instead of timing sleeps. This does not prove every possible schedule, but causal control is stronger evidence than `Task.Delay(50)` followed by an assertion that merely tends to win.

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

## Avoid common consistency mistakes {#common-mistakes}

- Locking only `Save` leaves the capacity decision outside the protected region.
- A concurrent collection makes operations safe individually, not a compound invariant atomic.
- Treating any exception as transient can repeat a completed payment.
- Remembering idempotency only in memory loses it on restart.
- Keying only by request ID prevents legitimate confirm and cancel operations.
- Ignoring payload mismatch lets one key silently mean two commands.
- Releasing capacity after an unknown payment can accept seats that may already be paid for.
- Calling pending notification “exactly once” ignores lost acknowledgments and final-write failure.
- Calling a `SemaphoreSlim` a distributed lock exceeds its documented application-local scope.
- Calling file replacement an ACID database ignores concurrent processes, power loss, backup, and recovery.
- Adding a broker, saga, and distributed lock before the requirements need them only moves uncertainty into more components.

## Exercises {#exercises}

### Exercise 1: cross the process boundary {#exercise-01}

The API must run as three replicas and each event has independent capacity. Replace the local gate and snapshot with a storage design that prevents overselling. Specify the aggregate key, persisted version, conditional or transactional write, conflict retry loop, and how cancellation affects a retry. State whether one hot event can block unrelated events, and name a test that forces two replicas to use the same version.

### Exercise 2: reconcile an unknown payment {#exercise-02}

Extend the progress model without writing code. Add the provider key and the minimum states needed for an operator or background job to query an ambiguous payment. Define transitions for provider reports `Authorized`, `Declined`, and `NotFound`. Decide when seats remain reserved, when they are released, and which transitions may send notification. Include a conflict rule for changed command payload.

### Exercise 3: turn pending notification into an outbox {#exercise-03}

Design a real outbox for booking notifications. Specify what is saved with the booking, how a worker claims work, how retries and backoff are recorded, how a stable message ID reaches consumers, how consumers deduplicate, and what happens after the retry limit. Distinguish “no lost local intent,” “at least once publish,” and “same observable consumer outcome.”

[Read the chapter solutions](../solutions/ch-37-consistency-idempotency).

## Chapter review {#chapter-review}

- Aggregate capacity cannot be enforced from one booking's state.
- Read, decide, reserve, and write must share one consistency boundary.
- Pending, confirmed, cancelled, and in-flight payment states need an explicit occupancy policy.
- An operation key needs a normalized payload comparison, not identity alone.
- Durable progress determines whether a retry replays, resumes, conflicts, or stops for reconciliation.
- Marking payment started before the call favors no blind duplicate charge over automatic liveness.
- Saving booking plus pending notification prevents lost local intent but still permits duplicate delivery.
- `SemaphoreSlim` proves only application-local coordination; this file adapter is not multi-process storage.
- A separate-process test proves orderly recovery more strongly than constructing another object.
- F# makes knowledge and outcomes visible; storage and provider guarantees still determine consistency.

Chapter 38 will wire this service through HTTP, add a C# contract client and end-to-end tests, then finish diagnostics and release evidence.
