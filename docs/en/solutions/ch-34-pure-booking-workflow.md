---
title: "Chapter 34 Solutions"
description: "Trace booking error precedence, extend independent validation to three fields, and compare cancellation precedence policies."
translationKey: solutions/ch-34-pure-booking-workflow
kind: solution
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
---

# Chapter 34 Solutions {#overview}

These solutions follow the current workflow's observable order rather than collecting every imaginable complaint. Field validation is complete within its independent phase; state and lifecycle decisions then stop as soon as a prerequisite fails.

[Return to Chapter 34](../part-06/ch-34-pure-booking-workflow).

## Exercise 1: trace exact precedence {#exercise-01}

### Follow the branch that can actually run {#exercise-01-traces}

| Input | Exact result | Rules not evaluated |
|---|---|---|
| Blank ID and zero seats, `NotBooked` | `InvalidCommand [InvalidRequestId BlankRequestId; InvalidSeatCount (NonPositiveSeatCount 0)]` | State occupancy and capacity |
| Valid five seats, capacity four, `NotBooked` | `BookingCreationFailed (RequestedSeatsExceedCapacity (5<seat>, 4<seat>))` | Nothing after creation refusal |
| Same valid command, `Booked existing` | `BookingAlreadyExists (Booking.requestId existing)` | `Booking.create`, so capacity is not rechecked |
| Blank ID and blank confirmation code, `NotBooked` | `InvalidCommand [InvalidRequestId BlankRequestId; InvalidConfirmationCode BlankConfirmationCode]` | Booking lookup and status transition |
| Valid confirmation, already `Confirmed currentCode` | `BookingTransitionFailed (CannotConfirmFrom (Confirmed currentCode))` | Event wrapping and evolution |

Case (a) runs both pure field validators even though the first failed. It never inspects `NotBooked`; changing the state to `Booked existing` would produce the same validation list.

Case (b) reaches creation because both fields are valid and state is empty. `Booking.create` owns the capacity comparison, so the workflow wraps that exact error rather than calculating another integer comparison.

Case (c) demonstrates business short-circuiting. The new request's five seats are individually valid, but occupied state means no creation attempt exists to diagnose. Reporting both duplicate and capacity would pretend the rejected creation ran.

Case (d) establishes that this decider validates independent lifecycle fields before lookup. That is a public precedence choice, not a universal security policy. Exercise 3 considers an alternative.

Case (e) validates the new command, finds the matching booking, and calls `Booking.confirm`. The error carries the current booking's existing confirmation code, not the proposed new one, because it describes the state that refused transition.

## Exercise 2: add a third independent field {#exercise-02}

### Extend the constructor one argument at a time {#exercise-02-validation}

The following self-contained extension uses the same shape. It introduces different type names so it does not claim the capstone already has email policy:

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

## Exercise 3: specify cancellation precedence {#exercise-03}

### Current policy {#exercise-03-current}

Assume the state contains request `REQ-7` with `Cancelled oldReason`:

1. Blank ID plus blank reason returns `InvalidCommand [InvalidRequestId BlankRequestId; InvalidCancellationReason BlankCancellationReason]`. State is not inspected.
2. A valid different ID plus valid reason returns `BookingDoesNotExist`. The cancelled status is not inspected because the target does not match.
3. The correct ID plus a valid new reason returns `BookingTransitionFailed (CannotCancelFrom (Cancelled oldReason))`. The new reason is valid but never replaces the final status.

This order gives callers complete field feedback before domain lookup. It is simple and deterministic, and it matches placement and confirmation validation order. It may reveal validation details for a target that does not exist, which some public boundaries prefer not to do.

### One defensible alternative {#exercise-03-alternative}

A privacy-sensitive API could validate only the request ID, perform authorization and lookup, then validate the reason only for an authorized existing target. A valid missing ID would always return one indistinguishable not-found result, regardless of whether the reason was blank. This can reduce account or resource probing and avoids spending further validation work for concealed targets.

That is an application-boundary policy, not a silent edit to the pure function. It would require a separate authenticated lookup phase, a documented error contract, endpoint tests proving indistinguishable missing/unauthorized responses, and revised decider inputs—perhaps a protected authorized booking plus a reason command.

It also gives up full accumulation across ID and reason. That trade-off is acceptable only when the security or privacy requirement outweighs immediate field feedback. Keeping the current internal decider and projecting a coarser external error can often preserve both concerns without duplicating transition rules.

Whichever policy is chosen, tests should state the exact precedence. Vague claims such as “all errors are handled” do not tell a caller which error wins or which checks ran.

## Solution review {#solution-review}

- Trace only rules whose prerequisites produced values.
- Field accumulation and business short-circuiting occupy different phases.
- Capacity comes from `Booking.create`; duplicate state prevents creation from running.
- A transition error describes the current state that refused the command.
- Adding a field extends the validated constructor and one independent pipeline step.
- Error order is part of observable behavior when a list is returned.
- Activity-wide availability is a stateful rule, not an email or integer field check.
- Email policy should come from requirements, not a convenient regular expression.
- The current policy validates all command fields before state lookup.
- A public security boundary may deliberately conceal lookup and validation details.
- Changing precedence requires new types or orchestration, documentation, and tests.
- No precedence policy substitutes for an atomic load-decide-commit boundary.
