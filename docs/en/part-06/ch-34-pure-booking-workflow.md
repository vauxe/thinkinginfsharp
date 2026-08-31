---
title: "Chapter 34: The Pure Booking Workflow and Validation"
description: "Derive one pure booking decider, accumulate independent field errors, short-circuit state-dependent rules, and evolve only accepted facts."
translationKey: part-06/ch-34-pure-booking-workflow
---

# Chapter 34: The Pure Booking Workflow and Validation {#overview}

Chapter 33 defined the vocabulary. This chapter connects it with one function. The workflow accepts a protected activity, current booking state, and raw command; it returns either an accepted fact or a classified refusal. All required facts arrive through arguments. Application adapters remain responsible for databases, clocks, transport failures, and notifications.

The key design choice is where each kind of failure belongs. Independent malformed fields are useful together, so validation accumulates them. A state-dependent decision stops when a prerequisite fails because later rules no longer have meaningful inputs. Applying one combination policy to both categories would either lose useful errors or invent misleading ones.

## Read the contract from left to right {#contract}

The public decision contract is:

```fsharp
Event
    -> BookingState
    -> BookingCommand
    -> Result<BookingEvent, BookingDecisionError>
```

Each position carries a different trust level:

| Position | Meaning | Validity on entry |
|---|---|---|
| `Event` | The scheduled activity and its protected capacity | Already constructed from `EventId` and `Capacity` |
| `BookingState` | The current booking view for this decision | Already a valid union value containing zero or one protected booking |
| `BookingCommand` | Placement, confirmation, or cancellation intent | The case is known, but record fields are still raw strings and integers |
| `Ok BookingEvent` | A fact accepted by domain rules | Safe to evolve and then commit according to application policy |
| `Error BookingDecisionError` | A classified expected refusal | No accepted fact was produced |

Currying allows partial application of a stable activity or state. More importantly, the argument order tells the story: context first, changing command last.

“Pure” means the same inputs produce the same result and evaluation has no observable external effect. Command acceptance, computational cost, exceptional behavior elsewhere in .NET, and concurrent commit safety are separate properties.

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

```fsharp:line-numbers [Validation.fs]
type ValidConfirmBooking =
    private
        { RequestId: RequestId
          ConfirmationCode: ConfirmationCode }

module ValidConfirmBooking =
    let requestId (command: ValidConfirmBooking) = command.RequestId
    let confirmationCode (command: ValidConfirmBooking) = command.ConfirmationCode

type ValidCancelBooking =
    private
        { RequestId: RequestId
          Reason: CancellationReason }

module ValidCancelBooking =
    let requestId (command: ValidCancelBooking) = command.RequestId
    let reason (command: ValidCancelBooking) = command.Reason
```
The raw records use `string` and `int` because a caller or future DTO begins with representation data. The validated records contain `RequestId`, `SeatCount`, `ConfirmationCode`, or `CancellationReason`. Their record constructors are private; callers obtain them only through validators and observe them through module functions.

This split avoids two bad extremes. Making the raw command constructor private would force an untrusted boundary to pretend its fields were already valid. Leaving the validated record public would let callers bypass normalization and invariants. Separate types preserve both honest input and protected internal data.

A validated command guarantees only that its fields are valid. `ValidConfirmBooking` has a nonblank, normalized identifier and code; it does not guarantee that a booking exists or is pending. Those facts depend on state and belong to the decision phase.

## Accumulate independent errors deliberately {#accumulation}

The project uses a small local combinator rather than hiding the policy in a validation framework:

```fsharp:line-numbers [Validation.fs]
let private applyValidation valueResult functionResult =
    match functionResult, valueResult with
    | Ok mapping, Ok value -> Ok(mapping value)
    | Error earlier, Error later -> Error(earlier @ later)
    | Error errors, Ok _
    | Ok _, Error errors -> Error errors

let private validateRequestId (raw: string) =
    RequestId.create raw
    |> Result.mapError (fun error -> [ InvalidRequestId error ])

let private validateSeatCount (raw: int) =
    SeatCount.create raw
    |> Result.mapError (fun error -> [ InvalidSeatCount error ])

let private validateConfirmationCode raw =
    ConfirmationCode.create raw
    |> Result.mapError (fun error -> [ InvalidConfirmationCode error ])

let private validateCancellationReason raw =
    CancellationReason.create raw
    |> Result.mapError (fun error -> [ InvalidCancellationReason error ])

let private createValidCommand (requestId: RequestId) (seats: SeatCount) : ValidPlaceBooking =
    { RequestId = requestId; Seats = seats }

let validatePlaceBooking (command: PlaceBookingCommand) =
    Ok createValidCommand
    |> applyValidation (validateRequestId command.RequestId)
    |> applyValidation (validateSeatCount command.Seats)
```
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

These validators are pure and cheap. F# evaluates the next validator expression even when the accumulated function result is already `Error`, which is exactly what collection requires. Do not reuse this accumulation pattern around database calls, rate-limited services, or destructive operations: evaluating all of those after failure could be expensive or wrong.

### Extend the same policy to lifecycle commands {#lifecycle-validation}

Confirmation and cancellation each validate two independent fields in the same order:

```fsharp:line-numbers [Validation.fs]
let private createValidConfirmCommand requestId confirmationCode : ValidConfirmBooking =
    { RequestId = requestId
      ConfirmationCode = confirmationCode }

let validateConfirmBooking (command: ConfirmBooking) =
    Ok createValidConfirmCommand
    |> applyValidation (validateRequestId command.RequestId)
    |> applyValidation (validateConfirmationCode command.ConfirmationCode)

let private createValidCancelCommand requestId reason : ValidCancelBooking =
    { RequestId = requestId
      Reason = reason }

let validateCancelBooking (command: CancelBooking) =
    Ok createValidCancelCommand
    |> applyValidation (validateRequestId command.RequestId)
    |> applyValidation (validateCancellationReason command.Reason)
```
The shared `CommandValidationError` union makes the unified decider return one error list while retaining exact cases. A confirmation validator cannot actually produce a seat-count error; the broader union is the cost of one command-level error vocabulary. Tests fix the cases each validator may emit.

Do not add “booking exists” to these functions. Doing so would require state, change a pure field conversion into a business decision, and make independent error accumulation ambiguous. The validated command is the boundary between those phases.

## Give the workflow one error vocabulary {#decision-errors}

The decider exposes expected refusal categories explicitly:

```fsharp:line-numbers [Decider.fs]
[<RequireQualifiedAccess>]
type BookingDecisionError =
    | InvalidCommand of CommandValidationError list
    | BookingAlreadyExists of existingRequestId: RequestId
    | BookingDoesNotExist
    | BookingCreationFailed of BookingCreationError
    | BookingTransitionFailed of BookingTransitionError
```
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

```fsharp:line-numbers [Decider.fs]
let decide
    (activity: Event)
    (state: BookingState)
    (command: BookingCommand)
    : Result<BookingEvent, BookingDecisionError> =
    match command with
    | BookingCommand.Place placeCommand ->
        decidePlaceBooking activity state placeCommand |> Result.mapError mapPlaceError
    | BookingCommand.Confirm confirmCommand ->
        validateConfirmBooking confirmCommand
        |> Result.mapError BookingDecisionError.InvalidCommand
        |> Result.bind (decideConfirm state)
    | BookingCommand.Cancel cancelCommand ->
        validateCancelBooking cancelCommand
        |> Result.mapError BookingDecisionError.InvalidCommand
        |> Result.bind (decideCancel state)
```
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

```fsharp:line-numbers [Decider.fs]
let private requireBooking requestId state =
    match state with
    | NotBooked -> Error BookingDecisionError.BookingDoesNotExist
    | Booked booking when Booking.requestId booking = requestId -> Ok booking
    | Booked _ -> Error BookingDecisionError.BookingDoesNotExist

let private decideConfirm state command =
    requireBooking (ValidConfirmBooking.requestId command) state
    |> Result.bind (fun booking ->
        Booking.confirm (ValidConfirmBooking.confirmationCode command) booking
        |> Result.map BookingConfirmed
        |> Result.mapError BookingDecisionError.BookingTransitionFailed)

let private decideCancel state command =
    requireBooking (ValidCancelBooking.requestId command) state
    |> Result.bind (fun booking ->
        Booking.cancel (ValidCancelBooking.reason command) booking
        |> Result.map BookingCancelled
        |> Result.mapError BookingDecisionError.BookingTransitionFailed)
```
`requireBooking` must succeed before a transition has an input. This sample returns `BookingDoesNotExist` both when state is `NotBooked` and when the contained request ID differs. Only a matching protected booking flows onward.

`Result.bind` then has the exact required semantics: an `Error` passes through without evaluating the binder; an `Ok booking` invokes the next function. Confirmation calls `Booking.confirm`; cancellation calls `Booking.cancel`. Those domain functions remain the only authorities for allowed lifecycle transitions.

`Result.map` wraps a successful updated booking in a past-tense fact. `Result.mapError` preserves a domain transition error under the workflow-level `BookingTransitionFailed` case. Neither combinator reruns the transition or alters the booking.

The scheduled `Event` input is needed by placement because creation checks capacity. Confirmation and cancellation need only the protected booking in state. Keeping one uniform decider signature simplifies routing; it does not justify adding fake activity checks to branches whose rule does not use them.

## Preserve placement precedence {#placement-precedence}

The earlier specialized decision remains visible and authoritative:

```fsharp:line-numbers [Workflow.fs]
type PlaceBookingError =
    | InvalidCommand of CommandValidationError list
    | BookingAlreadyExists of existingRequestId: RequestId
    | BookingCreationFailed of BookingCreationError

let decidePlaceBooking (event: Event) (state: BookingState) (command: PlaceBookingCommand) =
    match validatePlaceBooking command with
    | Error errors -> Error(InvalidCommand errors)
    | Ok validCommand ->
        match state with
        | Booked existing -> Error(BookingAlreadyExists(Booking.requestId existing))
        | NotBooked ->
            Booking.create event (ValidPlaceBooking.requestId validCommand) (ValidPlaceBooking.seats validCommand)
            |> Result.map BookingPlaced
            |> Result.mapError BookingCreationFailed
```
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

```fsharp:line-numbers [Workflow.fs]
let evolve (_: BookingState) (event: BookingEvent) =
    match event with
    | BookingPlaced booking
    | BookingConfirmed booking
    | BookingCancelled booking -> Booked booking
```
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

The focused workflow tests call ordinary values and functions. They need no mocking framework because the decider has no external dependencies. They cover:

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

The broader domain, workflow, property, and decider test filter also passes. The full example check restores locked dependencies, builds Release with null checking and warnings as errors, runs every test and script, and verifies expected compiler diagnostics.

These results show deterministic decisions for the covered model. They do not show that several bookings cannot consume the same activity capacity, that state was loaded consistently, or that a fact was committed exactly once. Those guarantees require atomic persistence and integration tests.

## Exercises {#exercises}

### Exercise 1: trace exact precedence {#exercise-01}

Before running tests, predict the exact `BookingDecisionError` for each input:

1. blank ID and zero seats against `NotBooked`;
2. valid five-seat placement into a four-seat activity against `NotBooked`;
3. the same valid placement against `Booked existing`;
4. blank ID and blank code against `NotBooked`;
5. valid confirmation against an already confirmed booking.

For every prediction, name the rules that evaluation skips.


::: details Answer

#### Follow the branch that can actually run {#exercise-01-traces}

| Input | Exact result | Rules not evaluated |
|---|---|---|
| Blank ID and zero seats, `NotBooked` | `InvalidCommand [InvalidRequestId BlankRequestId; InvalidSeatCount (NonPositiveSeatCount 0)]` | State occupancy and capacity |
| Valid five seats, capacity four, `NotBooked` | `BookingCreationFailed (RequestedSeatsExceedCapacity (5<seat>, 4<seat>))` | Nothing after creation refusal |
| Same valid command, `Booked existing` | `BookingAlreadyExists (Booking.requestId existing)` | `Booking.create`, so capacity is not rechecked |
| Blank ID and blank confirmation code, `NotBooked` | `InvalidCommand [InvalidRequestId BlankRequestId; InvalidConfirmationCode BlankConfirmationCode]` | Booking lookup and status transition |
| Valid confirmation, already `Confirmed currentCode` | `BookingTransitionFailed (CannotConfirmFrom (Confirmed currentCode))` | Event wrapping and evolution |

Case (a) runs both pure field validators even though the first failed. It never inspects `NotBooked`; changing the state to `Booked existing` would produce the same validation list.

Case (b) reaches creation because both fields are valid and state is empty. `Booking.create` performs the capacity comparison, so the workflow wraps its error instead of repeating the integer comparison.

Case (c) demonstrates business short-circuiting. The new request's five seats are individually valid, but occupied state means no creation attempt exists to diagnose. Reporting both duplicate and capacity would pretend the rejected creation ran.

Case (d) establishes that this decider validates independent lifecycle fields before lookup. That is a public precedence choice, not a universal security policy. Exercise 3 considers an alternative.

Case (e) validates the new command, finds the matching booking, and calls `Booking.confirm`. The error carries the current booking's existing confirmation code, not the proposed new one, because it describes the state that refused transition.

:::

### Exercise 2: add a third independent field {#exercise-02}

Imagine placement also receives `AttendeeEmail: string`, with a protected `EmailAddress` smart constructor. Sketch `ValidPlaceBooking` and `validatePlaceBooking` for request ID, email, and seats. Preserve field-order accumulation. Explain why remaining activity capacity still does not belong in this validator.


::: details Answer

#### Extend the constructor one argument at a time {#exercise-02-validation}

The following self-contained extension uses the same pattern. It introduces different type names so it does not imply that the capstone already has an email policy:

```fsharp
open System
open Booking.Domain

type EmailAddressError = BlankEmailAddress
type EmailAddress = private EmailAddress of string

module EmailAddress =
    let create raw =
        if String.IsNullOrWhiteSpace raw then
            Error BlankEmailAddress
        else
            Ok(EmailAddress(raw.Trim()))

type PlaceBookingWithEmail =
    { RequestId: string
      AttendeeEmail: string
      Seats: int }

type PlaceWithEmailError =
    | InvalidRequestId of RequestIdError
    | InvalidEmailAddress of EmailAddressError
    | InvalidSeatCount of SeatCountError

type ValidPlaceBookingWithEmail =
    private
        { RequestId: RequestId
          AttendeeEmail: EmailAddress
          Seats: SeatCount }

let applyValidation valueResult functionResult =
    match functionResult, valueResult with
    | Ok mapping, Ok value -> Ok(mapping value)
    | Error earlier, Error later -> Error(earlier @ later)
    | Error errors, Ok _
    | Ok _, Error errors -> Error errors

let createValid requestId attendeeEmail seats : ValidPlaceBookingWithEmail =
    { RequestId = requestId
      AttendeeEmail = attendeeEmail
      Seats = seats }

let validate (command: PlaceBookingWithEmail) =
    let requestId =
        RequestId.create command.RequestId
        |> Result.mapError (fun error -> [ InvalidRequestId error ])

    let email =
        EmailAddress.create command.AttendeeEmail
        |> Result.mapError (fun error -> [ InvalidEmailAddress error ])

    let seats =
        SeatCount.create command.Seats
        |> Result.mapError (fun error -> [ InvalidSeatCount error ])

    Ok createValid
    |> applyValidation requestId
    |> applyValidation email
    |> applyValidation seats
```

For blank ID, blank email, and zero seats, the result list follows declaration order: request ID, email, seats. Moving the pipeline applications changes observable error order, so choose it deliberately and fix it with a test.

The three validators depend only on their own raw fields. Remaining activity capacity depends on protected activity data and possibly activity-wide reservations. It belongs after this function in the stateful decision, where fail-fast semantics and later atomic commit can be specified.

The example checks only blank email because that is the stated rule. A production `EmailAddress` policy needs an explicit requirement before adding syntax, normalization, internationalization, or deliverability checks. Do not smuggle an arbitrary regular expression into a smart constructor.

:::

### Exercise 3: specify cancellation precedence {#exercise-03}

Consider a cancelled booking and three cancel commands: blank ID plus blank reason, a valid different ID, and the correct ID with a valid new reason. State the result of each under the current policy. Then propose one alternative precedence policy, its user or security motivation, and the tests and public contract that would need to change.


::: details Answer

#### Current policy {#exercise-03-current}

Assume the state contains request `REQ-7` with `Cancelled oldReason`:

1. Blank ID plus blank reason returns `InvalidCommand [InvalidRequestId BlankRequestId; InvalidCancellationReason BlankCancellationReason]`. State is not inspected.
2. A valid different ID plus valid reason returns `BookingDoesNotExist`. The cancelled status is not inspected because the target does not match.
3. The correct ID plus a valid new reason returns `BookingTransitionFailed (CannotCancelFrom (Cancelled oldReason))`. The new reason is valid but never replaces the final status.

This order gives callers complete field feedback before domain lookup. It is simple and deterministic, and it matches placement and confirmation validation order. It may reveal validation details for a target that does not exist, which some public boundaries prefer not to do.

#### One defensible alternative {#exercise-03-alternative}

A privacy-sensitive API could validate only the request ID, perform authorization and lookup, then validate the reason only for an authorized existing target. A valid missing ID would always return one indistinguishable not-found result, regardless of whether the reason was blank. This can reduce account or resource probing and avoids spending further validation work for concealed targets.

That is an application-boundary policy, not a silent edit to the pure function. It would require a separate authenticated lookup phase, a documented error contract, endpoint tests proving indistinguishable missing/unauthorized responses, and revised decider inputs—perhaps a protected authorized booking plus a reason command.

It also gives up full accumulation across ID and reason. That trade-off is acceptable only when the security or privacy requirement outweighs immediate field feedback. Keeping the current internal decider and projecting a coarser external error can often preserve both concerns without duplicating transition rules.

Whichever policy is chosen, tests should state the exact precedence. Vague claims such as “all errors are handled” do not tell a caller which error wins or which checks ran.

:::


## Sources {#sources}

- [Microsoft Learn: the F# `Result` type](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/results)
- [FSharp.Core reference: `Result.bind`, `map`, and `mapError`](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-resultmodule.html)
- [Microsoft Learn: match expressions, guards, and exhaustiveness](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/match-expressions)
- [Microsoft Learn: discriminated unions and named cases](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/discriminated-unions)
- [Microsoft Learn: records, immutability, and private construction](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/records)
