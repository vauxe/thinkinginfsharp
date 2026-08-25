---
title: "Chapter 13 Solutions"
description: "Translate calls among pipelines and composition, order representative F# APIs, and simplify a decorative pipeline."
translationKey: solutions/ch-13-composition-pipeline-api
---

# Chapter 13 Solutions {#overview}

Verify every rewrite by expanding it back to ordinary application. Visual left-to-right flow is useful, but type alignment and actual application order are the proof.

[Return to Chapter 13](../part-03/ch-13-composition-pipeline-api).

## Exercise 1: derive both compositions {#exercise-01}

The immediate pipeline is:

```fsharp
let result =
    text
    |> parse
    |> normalize
    |> label
```

The two reusable functions are:

```fsharp
let forward = parse >> normalize >> label
let backward = label << normalize << parse
```

Both have type `string -> string`. In both, `parse` runs first, then `normalize`, then `label`. `>>` lists execution order from left to right; `<<` lists it from final operation back toward the input.

Expanding either application proves the nesting:

```fsharp
// forward text expands to:
label (normalize (parse text))

// backward text expands to the same expression:
label (normalize (parse text))
```

In executable code, compare the resulting values with `forward text = backward text`.

## Exercise 2: order an F#-facing API {#exercise-02}

For a status reused across many collections, place status first and bookings last:

```fsharp
let filterByStatus status bookings =
    bookings
    |> List.filter (fun booking -> Booking.status booking = status)

let pendingOnly = filterByStatus Pending
let pending = allBookings |> pendingOnly
```

For one formatter reused across many collections, use the same selector-first convention as `List.map`:

```fsharp
let renderMany formatter bookings =
    bookings |> List.map formatter

let renderForConsole = renderMany renderBookingForConsole
let labels = allBookings |> renderForConsole
```

For capacity and requested seats, either order can be defensible. If one event capacity is reused, capacity-first supports partial application:

```fsharp
let fitsWithin capacity requested =
    SeatCount.value requested <= Capacity.value capacity

let fitsEvent = fitsWithin eventCapacity
let accepted = requestedSeats |> fitsEvent
```

For a single check, `fitsWithin eventCapacity requestedSeats` reads more directly as a two-value relation. The protected types make reversal a compile-time error even though both contain measured integers. That safety matters more than whether the final call contains `|>`.

## Exercise 3: remove decorative piping {#exercise-03}

The given exercise uses a representation-level `fitsWithin : int<seat> -> int<seat> -> bool`: both protected values have already been unwrapped. That is deliberately different from the protected-type API designed in Exercise 2.

A direct version names both quantities and leaves the final proposition direct:

```fsharp
let canAccept capacity request =
    let availableSeats = Capacity.value capacity
    let requestedSeats = request |> Booking.seats |> SeatCount.value
    fitsWithin availableSeats requestedSeats
```

A pipeline-oriented version can still preserve the important intermediate name:

```fsharp
let canAcceptPiped capacity request =
    let requestedSeats =
        request
        |> Booking.seats
        |> SeatCount.value

    requestedSeats
    |> fitsWithin (Capacity.value capacity)
```

I would choose the first version here. The extraction is short, both quantities appear next to the final relation, and a debugger can inspect each named value. The second version is correct and may fit a surrounding pipeline, but its final pipe adds no transformation stage; it only rotates a binary predicate.

If `fitsWithin` instead accepts protected `Capacity` and `SeatCount` directly, the best implementation is smaller still:

```fsharp
let canAccept capacity request =
    fitsWithin capacity (Booking.seats request)
```

Keeping measured unwrapping inside the domain predicate also reduces repeated representation knowledge at call sites.

## What to notice {#what-to-notice}

- **Composition direction changes spelling, not execution:** expand both operators to nested calls.
- **Reusable fixed arguments belong early in curried F# functions:** partial application then awaits the flowing data.
- **Selector-first APIs align with FSharp.Core:** familiar order reduces adapter lambdas.
- **Binary relations often read directly:** piping is optional, not a style requirement.
- **Domain types prevent same-primitive reversal:** parameter order alone cannot provide that safety.
