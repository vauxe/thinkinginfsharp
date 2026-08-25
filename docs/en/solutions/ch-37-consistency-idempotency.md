---
title: "Chapter 37 Solutions"
description: "Move capacity control across processes, reconcile ambiguous payments, and design an outbox without claiming exactly-once delivery."
translationKey: solutions/ch-37-consistency-idempotency
kind: solution
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
  - id: microsoft-retry-pattern
    url: https://learn.microsoft.com/en-us/azure/architecture/patterns/retry
    checked: "2026-08-25"
  - id: microsoft-transactional-outbox
    url: https://learn.microsoft.com/en-us/azure/architecture/databases/guide/transactional-out-box-cosmos
    checked: "2026-08-25"
  - id: microsoft-minimize-coordination
    url: https://learn.microsoft.com/en-us/azure/architecture/guide/design-principles/minimize-coordination
    checked: "2026-08-25"
---

# Chapter 37 Solutions {#overview}

These are designs, not drop-in production recipes. Each answer first names the invariant and durable evidence, then chooses coordination. Provider, database, and broker contracts still need to be verified against the products actually deployed.

[Return to Chapter 37](../part-06/ch-37-consistency-idempotency).

## Exercise 1: cross the process boundary {#exercise-01}

### Make the activity the concurrency key {#exercise-01-key}

Use the normalized event ID as the aggregate or partition key. One durable aggregate contains:

- the event ID, capacity, and a monotonically changing version or ETag;
- all booking states that contribute to occupancy;
- all unfinished reservations that contribute to occupancy;
- the operation key, payload fingerprint, phase, and replay result for each command.

This is the same information needed by the current consistency design, but the storage engine—not a process-local semaphore—must reject a stale commit. A relational design might lock one activity row and update its dependent rows in one transaction. A key-value or document design might conditionally replace one activity document only when its ETag matches.

Partitioning by event ID means one hot event can contend with itself without blocking unrelated events. That benefit holds only if no global table lock, singleton worker, or shared transaction serializes every partition. A booking that spans multiple events would cross this boundary and require a different model.

### Re-decide after a version conflict {#exercise-01-loop}

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

Payment must not run before the capacity reservation wins its conditional commit. The external-effect phases and provider key from the chapter remain necessary after replacing the local store.

### Force both writers to use one version {#exercise-01-test}

An integration test should start two independent service hosts against the same real storage partition. A barrier in a test storage hook pauses both after reading version 12. Release both conditional writes together and assert:

- exactly one write using expected version 12 commits;
- the loser reads version 13 and re-decides;
- capacity is never exceeded in committed state;
- only the winner starts payment when no remaining capacity exists;
- cancellation of one existing booking permits the loser on its next decision, if the policy allows it;
- simultaneous commands for two different event IDs can both progress.

Do not replace the storage engine's concurrency test with two objects in one process. That can accidentally share a lock and never exercise the production conflict primitive.

## Exercise 2: reconcile an unknown payment {#exercise-02}

### Model provider evidence separately {#exercise-02-model}

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

### Let lookup results drive transitions {#exercise-02-transitions}

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

## Exercise 3: turn pending notification into an outbox {#exercise-03}

### Commit business state and intent together {#exercise-03-commit}

In the same database transaction that commits the booking transition, insert an outbox row such as:

```text
messageId, eventId, requestId, operationKind,
messageType, schemaVersion, payload,
status, attemptCount, nextAttemptAt,
leaseOwner, leaseUntil, createdAt, completedAt
```

`messageId` is stable across every publish attempt. The payload is the versioned integration contract, not a serialized private F# domain object. The unique operation/message constraint prevents the request transaction from inserting two logical notifications.

If the transaction rolls back, neither booking change nor delivery intent exists. If it commits, both exist. This is the outbox's central guarantee: no lost local intent between those two records.

### Lease, send, and acknowledge {#exercise-03-relay}

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

### Deduplicate at the consumer boundary {#exercise-03-consumer}

Each consumer stores processed `messageId` values in the same local transaction as its own state change. On receipt:

- if the ID is new, apply the handler and record the ID atomically;
- if the ID already exists, acknowledge without applying the state change again;
- if either local write fails, do not acknowledge, so redelivery can retry both together.

Retention must cover the maximum broker redelivery and replay horizon. The handler itself should also favor naturally idempotent state assignments over additive effects. An email send, third-party webhook, or physical action introduces another independent boundary and needs its own key, lookup, or reconciliation policy.

The resulting claims are deliberately different:

| Claim | What the design establishes |
|---|---|
| no lost local intent | booking change and outbox row commit or roll back together |
| at least once publish attempt | while storage and the relay remain available, an unfinished row stays recoverable and retryable |
| same observable consumer outcome | a consumer atomically deduplicates by stable message ID |
| exactly once in every external system | not established |

Monitor pending age, attempt count, expired leases, dead-letter volume, end-to-end latency, and duplicate rate. Define ordering per aggregate if consumers require it, archive completed rows after the replay horizon, and test worker death at every row in the crash table.

Microsoft's [transactional outbox guidance](https://learn.microsoft.com/en-us/azure/architecture/databases/guide/transactional-out-box-cosmos) likewise separates the local transaction from later publication and calls out duplicate handling. The exact schema and lease mechanism remain database-specific.

## Solution review {#solution-review}

- Put every capacity-relevant fact under one aggregate version or transaction.
- A conditional-write loser reloads and re-decides; it never commits a stale decision.
- Partitioning by event avoids global coordination only when the storage path preserves that partitioning.
- Payment runs only after a durable reservation wins.
- An ambiguous provider call remains reserved until lookup supplies trustworthy terminal evidence.
- `NotFound` means only what the provider contract explicitly promises.
- Reconciliation preserves the original operation key and payload fingerprint.
- A transactional outbox prevents a gap between local state and local delivery intent.
- The relay can still publish twice if it crashes after sending and before acknowledging locally.
- Consumer-side atomic deduplication can make repeated delivery produce one local outcome.
- Stable IDs, bounded retries, leases, dead-letter handling, retention, and monitoring are protocol parts—not optional polish.
- None of these mechanisms alone creates exactly-once effects across every system.

## Sources {#sources}

- [Microsoft Learn: Retry pattern](https://learn.microsoft.com/en-us/azure/architecture/patterns/retry)
- [Microsoft Learn: Transactional Outbox pattern with Azure Cosmos DB](https://learn.microsoft.com/en-us/azure/architecture/databases/guide/transactional-out-box-cosmos)
- [Microsoft Learn: Minimize coordination](https://learn.microsoft.com/en-us/azure/architecture/guide/design-principles/minimize-coordination)
