---
title: "Chapter 33 Solutions"
description: "Classify booking values by role, design a seat-change command and event within the domain boundary, and choose persistence from stated guarantees."
translationKey: solutions/ch-33-domain-language-model
---

# Chapter 33 Solutions {#overview}

These solutions classify values by when they are meaningful and who has authority to create them. The suggested seat-change language is a design proposal, not code already supported by the capstone. Persistence is chosen from required guarantees rather than from the presence of an event union.

[Return to Chapter 33](../part-06/ch-33-domain-language-model).

## Exercise 1: classify values by role {#exercise-01}

### Ask what the value claims {#exercise-01-classification}

| Value | Role | Normal creator | Does construction mean the requested booking happened? |
|---|---|---|---|
| `PlaceBooking` | Raw command | Boundary mapper or `Commands.place` | No; its primitives may be invalid and the domain may refuse it |
| `ValidPlaceBooking` | Validated command | `validatePlaceBooking` after independent field checks | No; duplicate identity or capacity can still refuse it |
| `BookingPlaced booking` | Domain event | Normally the placement decider after `Booking.create` succeeds | Yes, by the value's meaning; the current public case can still rewrap an existing valid booking |
| `Booked booking` | Domain state | `evolve` or a trusted state-rehydration boundary | It says a booking presently exists; it does not claim that a new request just succeeded |
| `PlaceBookingRequestDto` | Boundary DTO | JSON serializer/client or HTTP adapter | No; it remains untrusted representation |
| `AppendEvent` | Port capability | Composition root supplies an adapter function | No; possessing or calling the capability is not a domain decision, and the call can fail |
| `RequestedSeatsExceedCapacity` | Domain error | `Booking.create` when a protected seat count exceeds protected capacity | No; it says the attempted placement was refused |

The decisive distinction is grammatical and temporal. A command points toward possible future work. A validated command narrows malformed input but still asks. An event speaks in the past tense. State says what is true now. A DTO says only what crossed a representation boundary.

The `BookingPlaced` row needs one qualification. On the normal production path, `Booking.create` ensures that the payload is valid. The union case remains public, however, so any code holding a valid `Booking` can wrap it. If provenance becomes an invariant, hide event construction behind a signature or internal boundary; the current type alone cannot verify where the event came from.

`AppendEvent` is a record field whose value is a function. The port promises a capability and its operational contract; it does not grant the adapter authority to decide whether placement is legal. The application calls it only after a pure accepted result.

## Exercise 2: extend the language before the code {#exercise-02}

### State the policy choices first {#exercise-02-policy}

One coherent vocabulary is:

| Role | Proposed name | Data or meaning |
|---|---|---|
| Command | `ChangeBookingSeats` | Raw `RequestId: string` and `Seats: int` |
| Validated command | `ValidChangeBookingSeats` | Protected `RequestId` and `SeatCount` |
| Accepted event | `BookingSeatCountChanged` | The resulting protected `Booking` |
| Field errors | `BlankRequestId`, `NonPositiveSeatCount` | Independent malformed fields |
| State errors | `BookingDoesNotExist`, `RequestIdDoesNotMatch`, `CannotChangeSeatsFrom`, `InsufficientCapacity`, `SeatCountUnchanged` | Refusals requiring booking or activity-wide state |

The command uses an imperative action; the event uses a past-tense fact. Neither name mentions JSON, a database column, or an HTTP verb. Carrying the resulting booking matches the current event/evolution design, though a future durable event schema might instead carry a stable request ID and old/new counts.

Validate request ID and seat count independently. Those checks need no current state and can accumulate both errors. Do not check capacity by comparing raw integers at this stage: the decision should use a protected `SeatCount` and the protected activity `Capacity`.

The state-dependent decision can proceed in this order:

1. Refuse `NotBooked` with `BookingDoesNotExist`.
2. For `Booked booking`, require the command request ID to equal `Booking.requestId booking`.
3. Require `Booking.status booking` to be `Pending`; refuse `Confirmed` and `Cancelled` with `CannotChangeSeatsFrom current`.
4. Use activity-wide reservation state to require the replacement count to fit available capacity. `Event.capacity` and one booking alone cannot prevent aggregate overselling.
5. Choose and document the equal-count policy. This solution returns `SeatCountUnchanged` rather than emitting a false “changed” fact.
6. Call one protected `Booking.changeSeats` transition and return `BookingSeatCountChanged updated`.

The proposed `Booking.changeSeats` function should be the only place that performs the booking-local status transition and constructs the updated booking. The aggregate decider checks activity-wide capacity after excluding this booking's old count, then calls the transition. Each rule still has one implementation.

On acceptance, `evolve state (BookingSeatCountChanged updated)` should return `Booked updated`. It should not revalidate status or capacity. Tests need exact examples for missing, mismatched, confirmed, cancelled, unchanged, insufficient-capacity, smaller, and larger pending requests, plus a property that total reserved seats never exceed activity capacity.

This pure language still does not solve two simultaneous seat changes: both can decide from the same activity-wide total. The persistence/application layer must condition commit on the version read, or decide and commit inside one atomic transaction. A clean event name is not a concurrency guarantee.

Before implementation, confirm two business ambiguities: whether a confirmed booking may ever shrink, and whether changing capacity reservation requires payment adjustment. Different answers create different commands, events, and side effects; they should not be guessed from existing field names.

## Exercise 3: decide whether history is the truth {#exercise-03}

### Separate shared requirements from optional architecture {#exercise-03-decision}

Both persistence designs need:

- durable writes and a tested restart path;
- an atomic capacity decision or optimistic concurrency check so competitors cannot oversell;
- stable request identity and idempotency rules for retried calls;
- schema/version migration and corruption handling;
- authorization, privacy, retention, backup, and restore policy;
- integration tests that exercise the actual persistence boundary.

Current-state persistence can save the latest booking and activity capacity in a versioned DTO. A conditional update rejects stale writers. A separate append-only audit table can retain the small set of required facts for 90 days, preferably committed transactionally with the state or through an outbox whose lag and retry semantics are explicit. Reading current status remains direct.

Event sourcing stores the ordered event stream as the source of truth. Restart recovery replays that stream; a current-status projection makes reads fast. This adds stream-version concurrency, deterministic replay, immutable event schema evolution or upcasting, idempotent projection handlers, projection rebuilds, snapshot policy when streams grow, and operational tools for inspecting and repairing streams.

The 90-day audit requirement does not by itself require event sourcing. It asks for retained evidence, while event sourcing normally makes events the authoritative route to all current state. Deleting or redacting old authoritative events after 90 days can itself complicate replay and privacy policy.

From only the stated facts, choose versioned current-state persistence plus a transactional audit record. It directly serves the primary query, can recover after restart, and can enforce capacity without introducing replay and projection operations. This is a provisional choice, not a universal claim that event sourcing is unsuitable for bookings.

Revisit the choice if the business needs any of the following:

- reconstruct state at arbitrary historical instants;
- explain every decision from the inputs available at that time;
- build many projections independently;
- correct past facts without destructive updates; or
- treat the complete lifecycle history as the legal record.

Before committing, also measure stream size, rebuild time, storage cost, the team's operational experience, and recovery drills.

CQRS remains independent. The current-state design can use separate read projections, and an event-sourced design can still expose a simple query surface. Adopt read/write separation only when their workload, security, or representation needs actually diverge enough to pay for synchronization complexity.

## Solution review {#solution-review}

- Classify a value by its authority, time, validity, and audience—not merely its record fields.
- Raw and validated commands both express intent; validation does not guarantee business acceptance.
- An event's past-tense meaning is stronger than a notification mechanism.
- Public event cases do not prove provenance when callers already hold a valid payload.
- State describes the current decision context; a DTO describes an external representation.
- New language should settle policy ambiguities before introducing fields or functions.
- Independent field errors can accumulate; state-dependent decisions use protected values.
- One function implements each rule, while `evolve` only applies accepted events.
- Concurrency control belongs to an atomic or versioned commit boundary.
- A finite audit trail can accompany current-state persistence without becoming its source of truth.
- Event sourcing adds replay, event evolution, projection, and operational obligations.
- Choose architecture from required guarantees and measured costs, then revisit it when evidence changes.
