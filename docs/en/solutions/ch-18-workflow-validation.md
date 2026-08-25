---
title: "Chapter 18 Solutions"
description: "Separate pure, dependent, and effectful checks; implement ordered accumulation; and replace an unspecified computation expression with explicit semantics."
translationKey: solutions/ch-18-workflow-validation
---

# Chapter 18 Solutions {#overview}

Validation design is dependency design. Make raw facts valid together, then use the resulting typed values in dependent domain and effectful phases.

[Return to Chapter 18](../part-03/ch-18-workflow-validation).

## Exercise 1: draw two validation phases {#exercise-01}

### Classify the checks {#exercise-01-classification}

Given the raw command in memory:

| Check | Classification | Reason |
|---|---|---|
| Request ID is nonblank/well formed | Independent pure input validation | Uses only raw request-ID text |
| Attendee name is nonblank | Independent pure input validation | Uses only raw attendee text |
| Seat text parses to a positive count | Independent from the other fields; internally dependent | Parse must succeed before positivity, but no other field is needed |
| Requested seats fit a supplied `Capacity` | Dependent pure domain validation | Requires a valid `SeatCount` and `Capacity` |
| Request ID is unique in the database | Effectful boundary work | Requires a valid ID and an external, potentially stale query |

If current capacity must itself be loaded rather than supplied as a domain value, acquisition is also effectful. The comparison can remain a pure function after loading.

### Put the phases in order {#exercise-01-order}

```text
accumulate request-ID + attendee + seat-text errors
                       ↓ only on success
compare valid SeatCount with supplied/current Capacity
                       ↓ only on success
query uniqueness for the valid RequestId
                       ↓ only on success
commit with atomic capacity and uniqueness enforcement
```

The first phase runs all three useful input checks. The capacity comparison short-circuits when seat parsing failed because it lacks a `SeatCount`. The uniqueness query waits until cheap validation and the domain decision pass, avoiding unnecessary I/O.

A database uniqueness query is advisory until the write. Another request may claim the same identifier after the query, so the commit boundary must enforce uniqueness atomically. The same reasoning applies to live capacity: a pre-check cannot prevent a later race.

The exact order of the capacity check and uniqueness query may change when measured cost or product policy differs, but both remain after their typed prerequisites. That is an explicit workflow decision, not a property of `Result`.

## Exercise 2: implement ordered accumulation {#exercise-02}

### A small reusable apply function {#exercise-02-apply}

```fsharp
let applyValidation valueResult functionResult =
    match functionResult, valueResult with
    | Ok mapping, Ok value -> Ok(mapping value)
    | Error earlier, Error later -> Error(earlier @ later)
    | Error errors, Ok _
    | Ok _, Error errors -> Error errors
```

The existing accumulated function is the left side, so two failures append `earlier @ later`. This fixes error order as the order in which value results enter the pipeline.

### Validate three fields {#exercise-02-fields}

```fsharp
open System

type FormError =
    | MissingName
    | InvalidEmail of raw: string
    | InvalidSeats of raw: string

type ValidForm =
    { Name: string
      Email: string
      Seats: int }

let validateName (raw: string) =
    if String.IsNullOrWhiteSpace raw then Error [ MissingName ]
    else Ok(raw.Trim())

let validateEmail (raw: string) =
    if raw.Contains('@') then Ok raw
    else Error [ InvalidEmail raw ]

let validateSeats (raw: string) =
    match Int32.TryParse raw with
    | true, seats when seats > 0 -> Ok seats
    | _ -> Error [ InvalidSeats raw ]

let createForm name email seats =
    { Name = name
      Email = email
      Seats = seats }

let validateForm name email seats =
    Ok createForm
    |> applyValidation (validateName name)
    |> applyValidation (validateEmail email)
    |> applyValidation (validateSeats seats)
```

The required checks are:

```fsharp
assert (
    validateForm "" "wrong" "zero" =
        Error [ MissingName; InvalidEmail "wrong"; InvalidSeats "zero" ]
)

assert (
    validateForm " Lin " "lin@example.test" "3" =
        Ok { Name = "Lin"; Email = "lin@example.test"; Seats = 3 }
)
```

`Error []` would say that validation failed but provide no failure, contradicting this API's intended evidence. A list cannot prevent that state. If callers or custom combinators might manufacture errors directly, define a non-empty list type and use it as the error parameter; if only these small trusted functions construct results, a tested convention may be sufficient.

## Exercise 3: audit computation-expression claims {#exercise-03}

### Identify the missing contract {#exercise-03-builder}

`result` must be a value whose type supplies computation-expression members. FSharp.Core defines `Result` and its module functions, but no built-in value named `result` that establishes this builder. The snippet may compile after importing a library or defining a builder; without that context, it is incomplete.

`let!` primarily uses the builder's `Bind`. `and!` primarily uses `MergeSources`, with optional `MergeSourcesN`, `BindN`, or `BindNReturn` optimizations. The request-ID and seat computations in one `let!`/`and!` group must not refer to each other's bound values.

Even when it compiles, accumulation depends on how that builder merges two `Error` values. Syntax alone does not supply list append.

### Rewrite accumulation explicitly {#exercise-03-rewrite}

For validators returning error lists, the complete two-check rule is:

```fsharp
let validatePair raw =
    let requestIdResult = validateRequestId raw.RequestId
    let seatsResult = validateSeats raw.Seats

    match requestIdResult, seatsResult with
    | Ok requestId, Ok seats -> Ok(requestId, seats)
    | Error requestErrors, Error seatErrors -> Error(requestErrors @ seatErrors)
    | Error errors, Ok _
    | Ok _, Error errors -> Error errors
```

Both validator calls occur before the match, both failures are preserved, and request-ID errors come first. If seat validation instead required a value produced by request-ID validation, this combination would be dishonest; use `Result.bind` and short-circuit that dependency.

A custom validation builder can later encode this exact rule and be tested against the ordinary function. The rewrite remains useful documentation of what `MergeSources` must mean.

## What to notice {#what-to-notice}

- Independent raw fields, dependent domain decisions, and external queries belong to different phases.
- Error ordering comes from the combining function and call order.
- A typed pre-check does not replace atomic enforcement at a concurrent storage boundary.
- A reusable apply function can remove repetition without hiding its four cases.
- Plain `list` permits an empty error collection; decide whether that matters at the API boundary.
- Computation-expression keywords are interpreted by a builder, so name the builder before claiming semantics.
