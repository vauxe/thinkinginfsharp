---
title: "Chapter 34: The Pure Booking Workflow and Validation"
description: "Derive one pure booking decider, accumulate independent field errors, short-circuit state-dependent rules, and evolve only accepted facts."
translationKey: part-06/ch-34-pure-booking-workflow
kind: chapter
part: 6
chapter: 34
status: complete
verifiedWith:
  fsharp: "10"
  dotnetSdk: "10.0.301"
exampleIds:
  - capstone-booking-domain
exerciseIds:
  - ch34-exercise-01
  - ch34-exercise-02
  - ch34-exercise-03
termIds: []
sources:
  - id: microsoft-fsharp-results
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/results
    checked: "2026-08-25"
  - id: fsharp-core-result-module
    url: https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-resultmodule.html
    checked: "2026-08-25"
  - id: microsoft-fsharp-match-expressions
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/match-expressions
    checked: "2026-08-25"
  - id: microsoft-fsharp-discriminated-unions
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/discriminated-unions
    checked: "2026-08-25"
  - id: microsoft-fsharp-records
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/records
    checked: "2026-08-25"
---

# Chapter 34: The Pure Booking Workflow and Validation {#overview}

Chapter 33 fixed the words. This chapter connects them with one function. The resulting workflow accepts a protected activity, current booking state, and one raw command; it returns either an accepted fact or an explicit decision error. It reads no database, calls no clock, catches no transport exception, and performs no notification.

The interesting design choice is not the absence of I/O. It is the placement of failure boundaries. Independent malformed fields are all useful at once, so validation accumulates them. A state-dependent decision cannot meaningfully evaluate every later rule after an earlier prerequisite fails, so it short-circuits. Treating both categories with one indiscriminate combinator would either lose useful errors or invent misleading ones.

## What you will be able to do {#outcomes}

By the end of this chapter, you should be able to:

- read a workflow signature as an executable statement of inputs, facts, and refusals;
- distinguish raw commands from protected validated commands;
- accumulate errors from independent pure field validators in stable order;
- use `Result.bind` when a later rule requires an earlier successful value;
- explain why validation accumulation and business short-circuiting are complementary;
- route a closed command union with an exhaustive match;
- reuse specialized decisions and domain transitions instead of copying rules;
- map detailed errors into one workflow error vocabulary without erasing meaning;
- keep `decide` separate from `evolve`;
- test failure precedence directly without mocks or external services;
- state what purity proves and what persistence must still guarantee.

## Read the contract from left to right {#contract}

The public decision contract is:

```fsharp
Event
    -> BookingState
    -> BookingCommand
    -> Result<BookingEvent, BookingDecisionError>
```

Each position carries a different trust level:

| Position | Meaning | Trust at entry |
|---|---|---|
| `Event` | The scheduled activity and its protected capacity | Already constructed from `EventId` and `Capacity` |
| `BookingState` | The current booking view for this decision | Already a valid union value containing zero or one protected booking |
| `BookingCommand` | Placement, confirmation, or cancellation intent | The case is known, but record fields are still raw strings and integers |
| `Ok BookingEvent` | A fact accepted by domain rules | Safe to evolve and then commit according to application policy |
| `Error BookingDecisionError` | A classified expected refusal | No accepted fact was produced |

Currying lets an application partially apply a stable activity or state if that improves a local pipeline, but the argument order primarily tells the story: supply context first and the changing command last.

“Pure” means the same inputs produce the same result and evaluation has no observable external effect. It does not mean every command succeeds, computation is free, exceptions are impossible everywhere in .NET, or two callers can safely commit the same result concurrently.

## Separate two kinds of failure {#two-failure-kinds}

Ask whether two checks can be judged independently from the same raw value set:

| Failure kind | Example | Combination policy | Reason |
|---|---|---|---|
| Independent field validation | Blank request ID and blank confirmation code | Accumulate both | Repairing one does not determine the other |
| Representation-to-domain conversion | Non-positive seat count | Accumulate with other independent field errors | No state is needed |
| State lookup | No booking matches the validated request ID | Stop | A transition cannot run without a booking |
| Lifecycle rule | Confirming an already confirmed booking | Stop | Later success depends on an allowed current status |
| Capacity rule | A valid placement exceeds protected capacity | Stop | It requires validated seats and the activity |
| Duplicate rule | A booking already occupies the provided state | Stop before creation | Creating another booking would be meaningless |

Accumulation is not “more functional” than short-circuiting. They answer different questions. Accumulate facts that can be evaluated independently and reported truthfully together. Bind sequential decisions whose later inputs do not exist, or whose meaning is undefined, after an earlier failure.

## Turn raw records into protected commands {#validated-commands}

The place command already had a private validated form. This chapter adds corresponding forms for confirmation and cancellation:

<<< @/../examples/capstone/src/Booking.Domain/Validation.fs#validated-lifecycle-commands{fsharp:line-numbers} [Validation.fs]

The raw records use `string` and `int` because a caller or future DTO begins with representation data. The validated records contain `RequestId`, `SeatCount`, `ConfirmationCode`, or `CancellationReason`. Their record constructors are private; callers obtain them only through validators and observe them through module functions.

This split avoids two bad extremes. Making the raw command constructor private would force an untrusted boundary to pretend its fields were already valid. Leaving the validated record public would let callers bypass normalization and invariants. Separate types preserve both honest input and protected internal data.

A validated command guarantees only its field-level claims. `ValidConfirmBooking` says its identifier and code are nonblank and normalized. It does not say a booking exists or is pending. Those facts depend on state and belong to decision.

## Accumulate independent errors deliberately {#accumulation}

The project uses a small local combinator rather than hiding the policy in a validation framework:

<<< @/../examples/capstone/src/Booking.Domain/Validation.fs#validation-accumulation{fsharp:line-numbers} [Validation.fs]

`applyValidation` combines a `Result` containing a constructor function with one validated field. Its four cases are exhaustive:

- two successes apply the function to the value;
- two failures concatenate the earlier error list with the later list;
- one failure preserves that error list;
- no protected command is constructed until every field succeeds.

For `({ RequestId = " "; Seats = 0 } : PlaceBooking)`, evaluation proceeds as follows:

```text
Ok createValidCommand
  + Error [InvalidRequestId BlankRequestId]
  = Error [InvalidRequestId BlankRequestId]
  + Error [InvalidSeatCount (NonPositiveSeatCount 0)]
  = Error [InvalidRequestId ...; InvalidSeatCount ...]
```

The order is stable because validators are applied in field order and the combinator uses `earlier @ later`. Stable order makes tests and user-facing mapping predictable; it is not a claim that one invalid field is more important.

These validators are pure and cheap. F# evaluates the next validator expression even when the accumulated function result is already `Error`, which is exactly what collection requires. Do not reuse this shape around database calls, rate-limited services, or destructive operations: evaluating all of those after failure could be expensive or wrong.

### Extend the same policy to lifecycle commands {#lifecycle-validation}

Confirmation and cancellation each validate two independent fields in the same order:

<<< @/../examples/capstone/src/Booking.Domain/Validation.fs#lifecycle-validation{fsharp:line-numbers} [Validation.fs]

The shared `CommandValidationError` union makes the unified decider return one error list while retaining exact cases. A confirmation validator cannot actually produce a seat-count error; the broader union is the cost of one command-level error vocabulary. Tests fix the cases each validator may emit.

Do not add “booking exists” to these functions. Doing so would require state, change a pure field conversion into a business decision, and make independent error accumulation ambiguous. The validated command is the boundary between those phases.

## Give the workflow one error vocabulary {#decision-errors}

The decider exposes expected refusal categories explicitly:

<<< @/../examples/capstone/src/Booking.Domain/Decider.fs#decision-contract{fsharp:line-numbers} [Decider.fs]

The cases retain their source:

| Case | Source | Meaning |
|---|---|---|
| `InvalidCommand errors` | Validation | One or more raw fields were malformed |
| `BookingAlreadyExists id` | Placement state check | The provided state already contains a booking |
| `BookingDoesNotExist` | Confirmation/cancellation lookup | No booking in the provided state matches the validated target |
| `BookingCreationFailed error` | `Booking.create` | Valid placement intent violated a creation rule such as capacity |
| `BookingTransitionFailed error` | `Booking.confirm` or `Booking.cancel` | The protected booking rejected a lifecycle transition |

One union makes the outer application handle every expected outcome exhaustively. It does not flatten every problem to a string. Detailed domain errors remain available for a later HTTP, logging, or public-API projection.

These cases are expected values, not exceptions. A database timeout, cancellation token, serializer defect, or programmer invariant violation belongs to a different boundary. The pure function has no reason to catch them because it does not perform those operations.

## Route the closed command set {#routing}

The unified function is an exhaustive match over `BookingCommand`:

<<< @/../examples/capstone/src/Booking.Domain/Decider.fs#decide{fsharp:line-numbers} [Decider.fs]

The branch structure is intentionally plain:

- placement delegates to the already tested `decidePlaceBooking` and maps its error union;
- confirmation validates raw fields, maps validation failure, then binds the state decision;
- cancellation follows the same phase boundary with its own validated type;
- every successful branch returns exactly one `BookingEvent`.

The decider does not repeat placement capacity or duplicate rules. Delegation preserves `Workflow.decidePlaceBooking` as their authority while offering one command entry point. Likewise, the two lifecycle branches do not inspect status themselves.

Because the command is a closed discriminated union, adding another case makes this match incomplete. With warnings treated as errors, the build forces a deliberate routing choice. A wildcard would discard that maintenance signal and should be reserved for a genuinely open or intentionally ignored input space.

`[<RequireQualifiedAccess>]` on `BookingCommand` also makes each call site say `BookingCommand.Place`, `.Confirm`, or `.Cancel`. The qualification helps when several unions contain ordinary words such as `Cancel` or `Confirm`.

## Short-circuit state-dependent decisions {#business-short-circuit}

After field validation, confirmation and cancellation use sequential `Result` composition:

<<< @/../examples/capstone/src/Booking.Domain/Decider.fs#lifecycle-decisions{fsharp:line-numbers} [Decider.fs]

`requireBooking` must succeed before a transition has an input. This sample returns `BookingDoesNotExist` both when state is `NotBooked` and when the contained request ID differs. Only a matching protected booking flows onward.

`Result.bind` then has the exact required semantics: an `Error` passes through without evaluating the binder; an `Ok booking` invokes the next function. Confirmation calls `Booking.confirm`; cancellation calls `Booking.cancel`. Those domain functions remain the only authorities for allowed lifecycle transitions.

`Result.map` wraps a successful updated booking in a past-tense fact. `Result.mapError` preserves a domain transition error under the workflow-level `BookingTransitionFailed` case. Neither combinator reruns the transition or alters the booking.

The scheduled `Event` input is needed by placement because creation checks capacity. Confirmation and cancellation need only the protected booking in state. Keeping one uniform decider signature simplifies routing; it does not justify adding fake activity checks to branches whose rule does not use them.

## Preserve placement precedence {#placement-precedence}

The earlier specialized decision remains visible and authoritative:

<<< @/../examples/capstone/src/Booking.Domain/Workflow.fs#place-decision{fsharp:line-numbers} [Workflow.fs]

Its nesting defines observable precedence:

1. Validate request ID and seat count, accumulating both independent failures.
2. If fields are valid, inspect state.
3. If a booking already exists, return `BookingAlreadyExists` without attempting creation.
4. Only for `NotBooked`, call `Booking.create` and enforce capacity.
5. Wrap a created booking in `BookingPlaced`.

Therefore an invalid oversized command against existing state reports field errors first; a valid oversized command against existing state reports the duplicate first; the same valid oversized command against `NotBooked` reports capacity. These are not incidental implementation details. They are a decision policy fixed by tests and should change only with a stated business reason.

Trying to “collect all business errors” here would produce dubious output. Once a booking exists, there is no proposed new booking whose creation should also be diagnosed. Once the request ID is invalid, looking up that target is not meaningful. Sequential structure prevents such fictional combinations.

## Evolve only an accepted fact {#evolution}

Decision and evolution remain separate functions:

<<< @/../examples/capstone/src/Booking.Domain/Workflow.fs#evolve{fsharp:line-numbers} [Workflow.fs]

`decide` answers whether a command may produce a fact. `evolve` answers what state follows a fact that has already been accepted. The application uses them in that order:

```fsharp
match Decider.decide activity state command with
| Error error -> Error error
| Ok bookingEvent -> Ok(Workflow.evolve state bookingEvent)
```

The actual application usually commits the accepted event or next-state DTO before reporting success. That effect belongs outside this expression. If commit fails, the domain decision did not become invalid; the application has an operational failure and must follow its atomicity, retry, and idempotency contract.

The current events carry the complete resulting booking, so `evolve` is mechanical. That design does not require event sourcing, nor does it prove that replayed arbitrary public event values came from the decider. It gives the pure workflow an explicit accepted-fact boundary.

## Keep one authority for each rule {#rule-ownership}

Rule ownership is easier to review when written down:

| Rule | Authority | Reused by |
|---|---|---|
| Normalize and reject blank identifiers, codes, and reasons | Their smart-constructor modules | All command validators and public projections |
| Accumulate independent command fields | `Validation.applyValidation` and each validator's declared order | Place, confirm, cancel validation |
| Reject non-positive seats | `SeatCount.create` | Placement validation |
| Reject a placement larger than activity capacity | `Booking.create` | Specialized and unified placement paths |
| Reject placement into occupied state | `Workflow.decidePlaceBooking` | Unified decider and earlier public workflow |
| Require a matching booking target | `Decider.requireBooking` | Confirm and cancel branches |
| Permit confirmation or cancellation from a status | `Booking.confirm` and `Booking.cancel` | Decider and public API |
| Project an accepted booking fact to state | `Workflow.evolve` | Tests and application orchestration |

“One authority” does not mean one giant function. It means another layer calls the rule instead of reimplementing its condition. Mapping an error into a broader union is not duplicating the rule; checking `requested > capacity` in two modules would be.

## Test behavior without effects {#testing}

The focused workflow tests call ordinary values and functions. They need no mock framework because the decider has no ports. They establish:

- place, confirm, and cancel each accumulate their independent malformed fields in order;
- invalid lifecycle fields win before a missing-state check;
- valid placement emits `BookingPlaced` and evolves to `Booked`;
- a capacity refusal preserves the exact measured domain error;
- occupied state wins before a later placement capacity check;
- valid confirmation normalizes its code and emits `BookingConfirmed`;
- absent or mismatched target state returns `BookingDoesNotExist`;
- a second confirmation preserves `CannotConfirmFrom`;
- cancellation emits a normalized final fact;
- repeated cancellation preserves `CannotCancelFrom`.

The wider domain, workflow, property, and decider filter also passes. The full example gate restores locked dependencies, builds Release with null checking and warnings as errors, runs every test and script, and verifies expected compiler diagnostics.

This evidence proves deterministic decisions for the covered model. It does not prove that several bookings cannot consume the same activity capacity, that state was loaded consistently, or that a fact was committed exactly once. Those guarantees need an atomic persistence boundary and later integration tests.

## Avoid common false simplifications {#false-simplifications}

- Chaining independent field validators with `Result.bind` reports only the first error; use that only when fail-fast is the intended contract.
- Accumulating state-dependent refusals can report conditions that were never meaningfully evaluated.
- Rechecking status or capacity in `Decider` duplicates protected domain rules.
- Throwing exceptions for ordinary invalid input hides expected outcomes from the type.
- Reading a repository inside `decide` makes repeatability and concurrency policy implicit.
- Returning a new state without the accepted fact erases a useful application boundary in this design.
- Returning both an event and a separately calculated state risks disagreement; derive state with `evolve`.
- Calling the function pure does not make the later load-decide-commit sequence atomic.

## Exercises {#exercises}

### Exercise 1: trace exact precedence {#exercise-01}

For each input, predict the exact `BookingDecisionError` before running tests: (a) blank ID and zero seats against `NotBooked`; (b) valid five-seat placement into a four-seat activity against `NotBooked`; (c) the same valid placement against `Booked existing`; (d) blank ID and blank code against `NotBooked`; and (e) valid confirmation against an already confirmed booking. Name every rule that is skipped.

### Exercise 2: add a third independent field {#exercise-02}

Imagine placement also receives `AttendeeEmail: string`, with a protected `EmailAddress` smart constructor. Sketch `ValidPlaceBooking` and `validatePlaceBooking` for request ID, email, and seats. Preserve field-order accumulation. Explain why remaining activity capacity still does not belong in this validator.

### Exercise 3: specify cancellation precedence {#exercise-03}

Consider a cancelled booking and three cancel commands: blank ID plus blank reason, a valid different ID, and the correct ID with a valid new reason. State the result of each under the current policy. Then propose one alternative precedence policy, its user or security motivation, and the tests and public contract that would need to change.

[Read the chapter solutions](../solutions/ch-34-pure-booking-workflow).

## Model review {#model-review}

- The workflow signature exposes protected context, raw intent, accepted fact, and expected refusal.
- Independent pure field checks accumulate; dependent business decisions short-circuit.
- Private validated records mark the exact phase boundary.
- Stable error ordering follows validator order, not accidental map iteration.
- `Result.bind` invokes the next rule only for `Ok`; `map` and `mapError` preserve the other side.
- Exhaustive matching routes every command case.
- Specialized decisions and protected transitions remain rule authorities.
- Error projection preserves structure instead of flattening to text.
- `decide` accepts or refuses; `evolve` projects only accepted facts.
- Purity makes decisions deterministic and cheap to test, not commits atomic.
- The focused evidence proves single-booking workflow behavior; aggregate capacity and persistence remain later work.

## Sources {#sources}

- [Microsoft Learn: the F# `Result` type](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/results)
- [FSharp.Core reference: `Result.bind`, `map`, and `mapError`](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-resultmodule.html)
- [Microsoft Learn: match expressions, guards, and exhaustiveness](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/match-expressions)
- [Microsoft Learn: discriminated unions and named cases](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/discriminated-unions)
- [Microsoft Learn: records, immutability, and private construction](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/records)
