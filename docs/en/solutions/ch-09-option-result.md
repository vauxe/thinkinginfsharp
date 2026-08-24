---
title: "Chapter 9 Solutions"
description: "Reasoning about option, Result, composition, short-circuiting, and structured error context."
translationKey: solutions/ch-09-option-result
kind: solution
part: 2
chapter: 9
status: complete
verifiedWith:
  fsharp: "10"
  dotnetSdk: "10.0.301"
exampleIds:
  - ch09-option-result
exerciseIds:
  - ch09-exercise-01
  - ch09-exercise-02
  - ch09-exercise-03
termIds: []
sources:
  - id: microsoft-options
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/options
    checked: "2026-08-24"
  - id: microsoft-results
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/results
    checked: "2026-08-24"
---

# Chapter 9 Solutions {#overview}

A good answer preserves exactly the alternatives a caller must distinguish. A shorter type is not better if it erases a useful failure reason; a richer type is not better when its extra case carries no information.

[Return to Chapter 9](../part-02/ch-09-option-result).

## Exercise 1: choose a return type {#exercise-01}

1. **Find by a valid identifier:** `Booking option`. No match is an ordinary answer, and the premise says identifier validation is already complete. If storage access itself can fail, that is a separate dimension and the type may become `Result<Booking option, StorageError>`.
2. **Parse a seat count:** `Result<int, SeatCountError>`. Text may be malformed, outside the `int` range, or numerically unacceptable. A typed error lets the caller explain or respond to the distinction. `int option` can be sufficient only when every failure is intentionally equivalent to “not parsed.”
3. **Calculate initials:** `string`. The premise promises a validated non-empty name. Advertising absence or failure would make every caller handle a case the contract says cannot occur. If the premise cannot actually be trusted, repair the input type or validate at the boundary.
4. **Query the service:** `Result<Booking option, ServiceError>`. `Error` means the query did not complete successfully. `Ok None` means it completed but found nothing. `Ok (Some booking)` means it completed and found a value. Flattening either layer would merge distinct facts.

The type follows meaning, not the implementation's convenience.

## Exercise 2: compose optional data {#exercise-02}

The direct definition is:

```fsharp
let tryFindConfirmedCode bookingId =
    bookingId
    |> tryFindBooking
    |> Option.bind tryConfirmedCode
```

`tryConfirmedCode` already returns `string option`. `Option.map tryConfirmedCode` would wrap that returned option again and produce `string option option`. `Option.bind` applies the function to `Some booking` and returns its option directly; it preserves `None` without calling the function.

An explicit match has the same behavior:

```fsharp
let tryFindConfirmedCodeExplicit bookingId =
    match tryFindBooking bookingId with
    | Some booking -> tryConfirmedCode booking
    | None -> None
```

The first version is not more correct; it is a compact expression of the same case analysis.

## Exercise 3: preserve validation context {#exercise-03}

Because union cases form a closed set, revise the original definition rather than trying to extend it elsewhere:

```fsharp
type BookingError =
    | EmptyAttendee
    | NonPositiveSeats of actual: int
    | TooManySeats of requested: int * maximum: int
    | EventClosed

type ValidationFailure =
    { RequestId: string
      EventId: string
      Cause: BookingError }

let validateOpen isOpen request =
    if isOpen then Ok request else Error EventClosed

let validateBooking maximum isOpen request =
    request
    |> validateAttendee
    |> Result.bind (validateSeats maximum)
    |> Result.bind (validateOpen isOpen)

let addContext requestId eventId result =
    result
    |> Result.mapError (fun cause ->
        { RequestId = requestId
          EventId = eventId
          Cause = cause })

let checkRequest request =
    request
    |> validateBooking 4 false
    |> addContext "R-9" "E-2"
```

With this order, `checkRequest` applied to an empty attendee and a closed event produces `EmptyAttendee`; `Result.bind` does not run the later seat or open checks after the first `Error`. A request that passes the first two checks but targets a closed event produces `EventClosed`. `addContext` then wraps whichever domain error survives, without changing an `Ok` value.

If the UI must report all three independent violations, this pipeline is the wrong combining rule. Each validation must run and its errors must be accumulated deliberately; changing the order of `bind` calls cannot create accumulation.

## What to notice {#what-to-notice}

- **Absence and operational failure are independent:** nesting may be the most truthful representation.
- **`map` and `bind` differ by the next return type:** plain value versus already wrapped value.
- **Validation order is policy:** first-error short-circuiting makes the earlier check observable.
- **Structured context remains data:** request and event identifiers can be logged or translated without parsing a message.
- **Types should not advertise impossible branches:** a guaranteed calculation returns a plain value.
