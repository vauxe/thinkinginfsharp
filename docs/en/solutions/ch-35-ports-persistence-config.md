---
title: "Chapter 35 Solutions"
description: "Evolve a versioned snapshot, audit interruption points in replacement, and redesign composition for borrowed production clients."
translationKey: solutions/ch-35-ports-persistence-config
---

# Chapter 35 Solutions {#overview}

These solutions treat compatibility, file replacement, and resource lifetime as separate policies. A migration is not merely “add a nullable field,” flushing is not a transaction, and dependency injection does not decide who disposes a borrowed client.

[Return to Chapter 35](../part-06/ch-35-ports-persistence-config).

## Exercise 1: evolve the snapshot contract {#exercise-01}

### Dispatch before strict version-specific parsing {#exercise-01-dispatch}

Keep the current `BookingDto` as the exact version 1 representation. Introduce a separate `BookingDtoV2` with the same existing fields plus `[<JsonPropertyName("customerNote")>] CustomerNote: string | null`. Do not mutate the meaning of `schemaVersion = 1` after files already exist.

Reading now proceeds in six steps:

1. Apply the existing byte bound, strict UTF-8 decoding, and JSON depth limit.
2. Read only the top-level `schemaVersion` with `JsonDocument` or a minimal envelope.
3. Dispatch `1` to the exact version 1 deserializer and mapper.
4. Dispatch `2` to an independently configured strict version 2 deserializer and mapper.
5. Return `UnsupportedSchemaVersion actual` for every other integer.
6. Reject a missing or non-integer version as corrupt representation.

Version dispatch must happen before strict full-object deserialization. Otherwise a version 2 `customerNote` appears to the version 1 options as an unknown member and fails for the wrong reason.

Use this compatibility table:

| Stored document | Result in memory | Rewrite policy |
|---|---|---|
| Exact version 1 | Protected booking plus `CustomerNote = None` | Do not write during load |
| Version 2 without `customerNote` | Protected booking plus `None` | Save later as canonical version 2 |
| Version 2 with string note | Protected booking plus `Some note` | Preserve on the next successful save |
| Version 2 with unknown member | Corrupt/unsupported representation under the strict policy | Do not modify the file |
| Version 3 | `UnsupportedSchemaVersion 3` | Do not guess or downgrade |

Upgrade in memory and rewrite only on the next successful business save. A read should not unexpectedly become a write, acquire new failure modes, or change timestamps. If eager migration is operationally required, make it a separate command with backup, audit, and rollback behavior.

The note's domain policy still needs a requirement. “Optional” answers presence, not maximum length, whitespace normalization, privacy, or whether it belongs in a booking at all. Add a protected type only after those rules are stated.

Contract tests should retain every version 1 snapshot test and add the exact serialized forms for version 2. Verify that both versions map to the intended validated value. Also verify that version 3 and unknown members fail during the documented phase.

## Exercise 2: audit every save interruption {#exercise-02}

### Separate target visibility from durability {#exercise-02-table}

Assume an old complete target exists:

| Interruption point | Target visible to a reader | Temporary file | Honest conclusion |
|---|---|---|---|
| Before temporary creation | Old complete target | None | No write began |
| During temporary write | Old complete target | Possibly partial | Normal unwinding deletes it; process crash may leave residue |
| After `Flush(true)`, before move | Old complete target | Complete staged bytes | Cancellation deletes it; crash may leave a complete orphan |
| During same-volume replacement | Filesystem-specific old-or-new replacement behavior | Being renamed | No in-place partial target was written |
| After successful move | New complete target | Normally absent | Commit occurred; a later cancellation cannot truthfully roll it back |

The implementation observes cancellation before writing and again immediately before `File.Move`. There is no cancellable await after the move. If the token is cancelled concurrently after commit, returning `Ok` is safer than reporting cancellation for work that became visible.

Three claims must remain distinct:

- Process-visible replacement: the target is replaced through a same-directory move instead of overwritten byte by byte.
- Buffer flushing: `Flush(true)` requests that intermediate file buffers be written before the move.
- Power-loss durability: persistence of file data, rename metadata, and directory entries depends on the operating system, filesystem, mount options, and device behavior; this sample does not prove it.

Cleanup in `finally` is best effort. A startup maintenance routine may remove old files matching the private temporary-name pattern after verifying they are not the configured target. It must never delete a broad directory or accept an unvalidated path.

If the requirement is “the booking and payment authorization commit together,” no rearrangement of these seven file steps can provide it. That requires a wider consistency protocol, not stronger wording around `File.Move`.

## Exercise 3: change ownership without ambiguity {#exercise-03}

### Separate borrowed capabilities from resources managed here {#exercise-03-borrowed}

Refactor composition to depend on capabilities rather than concrete stubs:

```fsharp
type ExternalServicePorts =
    { Charge: PaymentRequest -> CancellationToken -> Task<PaymentOutcome>
      Notify: NotificationRequest -> CancellationToken -> Task<unit> }

type BorrowedInfrastructureComposition =
    inherit IDisposable
    abstract member Ports: AsyncPorts

val startBorrowed:
    configuration: BookingStoreConfiguration ->
    externalServices: ExternalServicePorts ->
    getUtcNow: (CancellationToken -> Task<DateTimeOffset>) ->
    BorrowedInfrastructureComposition
```

This `.fsi`-style declaration states an interface contract, not code already implemented by the chapter's adapter. A production implementation would move the existing port-record construction behind `startBorrowed` and expose no concrete `PaymentStub` or `NotificationStub` properties from the borrowed composition.

The host creates long-lived clients, registers them, starts one or more borrowed compositions, and disposes the compositions before disposing the shared clients. If clients implement `IAsyncDisposable`, the host awaits them at its own shutdown boundary. A borrowed composition must never call either disposal interface.

Avoid one ambiguous `ownsClients: bool` flag. Use separate constructors or types, such as `startOwnedStubs` for the demo and `startBorrowed` for host clients. The call site then states who must dispose the clients.

Prevent use after disposal at two levels:

1. Each composition still marks itself closed and rejects new port calls.
2. The host prevents shared clients from being disposed while any borrowing composition remains active.

Tests can remain deterministic by creating `PaymentStub` and `NotificationStub` inside the test. Pass their `Invoke` functions as borrowed capabilities, then dispose the stubs in the test's outer `use` scope. Check authorization, decline, the specified fault, and pre-cancellation without a recorded call. Also check that disposing the composition does not dispose borrowed stubs.

The caller token must still pass through unchanged. Borrowing changes disposal responsibility; it does not let the composition replace cancellation, retry, or error policy.

## Solution review {#solution-review}

- Preserve the exact meaning of every already-written schema version.
- Inspect the version before applying strict version-specific unknown-member rules.
- Upgrade on read in memory; make eager rewrites an explicit migration operation.
- An optional field still needs a domain and privacy policy.
- Before the move, the old target remains authoritative; after the move, commit happened.
- Temporary cleanup and crash recovery are separate concerns.
- Flush requests buffer persistence but does not prove universal power-loss durability.
- File replacement cannot atomically include a remote payment operation.
- Borrowed capabilities and owned resources need different construction APIs.
- The creator/host disposes shared clients after every borrower is closed.
- Deterministic stubs remain useful when passed as borrowed functions in tests.
- Changing disposal responsibility does not change cancellation or failure semantics.
