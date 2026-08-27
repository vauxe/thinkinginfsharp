---
title: "Chapter 15 Solutions"
description: "Build total domain views, preserve parsing errors, and move database work outside active-pattern matching."
translationKey: solutions/ch-15-active-patterns
---

# Chapter 15 Solutions {#overview}

An active pattern should make the match's domain view clearer without making evaluation behavior less visible. Expand the pattern back to its recognizer function whenever cost or failure is uncertain.

[Return to Chapter 15](../part-03/ch-15-active-patterns).

## Exercise 1: design two total views {#exercise-01}

### Complete workflow partition {#exercise-01-complete}

```fsharp
type BookingStatus =
    | Pending
    | Confirmed of confirmationCode: string
    | Cancelled of reason: string

let (|Open|Closed|) status =
    match status with
    | Pending -> Open "pending"
    | Confirmed code -> Open $"confirmed:{code}"
    | Cancelled reason -> Closed reason

let canChange status =
    match status with
    | Open _ -> true
    | Closed _ -> false
```

The two active cases cover every declared status. The payload also preserves which open situation was observed, but callers that only need `canChange` can ignore it.

### Complete single-case projection {#exercise-01-single}

```fsharp
let (|StatusLabel|) status =
    match status with
    | Pending -> "pending"
    | Confirmed code -> $"confirmed:{code}"
    | Cancelled reason -> $"cancelled:{reason}"

let renderStatus (StatusLabel label) = label
```

`StatusLabel` always returns one string, so this single-case view cannot fail. An ordinary `statusLabel` function returning `string` would be equally valid and arguably simpler if no match-oriented call site benefits from the pattern spelling.

Neither recognizer changes the domain's possible values. Each consumes an existing `BookingStatus` and computes a view. Construction, legal transitions, and invariants remain properties of the original type and its module.

## Exercise 2: preserve the useful failure {#exercise-02}

### Explicit parser {#exercise-02-parser}

```fsharp
open System

type SeatCountError =
    | NotAnInteger of raw: string
    | NotPositive of actual: int

let parseSeatCount (raw: string) =
    match Int32.TryParse raw with
    | true, value when value > 0 -> Ok value
    | true, value -> Error(NotPositive value)
    | false, _ -> Error(NotAnInteger raw)
```

If the real domain uses a protected `SeatCount`, the successful branch should call its smart constructor and return that type rather than a bare `int`. The exercise keeps `int` only to isolate the failure-shape question.

### Optional match view {#exercise-02-partial}

```fsharp
let (|SeatCount|_|) raw =
    match parseSeatCount raw with
    | Ok seats -> Some seats
    | Error _ -> None
```

This pattern fits a multi-format recognizer: a non-match simply tries the next token format. It does not fit HTTP, form, or command validation that must explain how to repair the input. Those paths should call `parseSeatCount` and retain `Error`.

The conversion loses both the error case and its payload:

- `"oops"` loses `NotAnInteger "oops"`;
- `"0"` loses `NotPositive 0`;
- `"-3"` loses `NotPositive -3`.

All three become `None`. The successful value remains, but the reason and offending detail are irrecoverable without parsing again.

## Exercise 3: move I/O out of matching {#exercise-03}

### What the original hides {#exercise-03-problems}

The syntax `ExistingBooking booking` looks like inexpensive decomposition, yet it performs a database query. That creates several problems:

- latency and resource use are absent from the call-site shape;
- two occurrences can execute two queries for one match input;
- `None` cannot distinguish “not found” from any infrastructure failure that was incorrectly collapsed;
- a thrown database exception appears during matching and bypasses the wildcard fallback;
- the recognizer is difficult to test without repository state;
- clause reordering can change external work.

### Explicit acquisition, pure view {#exercise-03-rewrite}

Prefer a repository contract that distinguishes infrastructure failure from absence:

```fsharp
type BookingLookupError =
    | StorageFailure of message: string

type BookingDecisionError =
    | NotFound
    | LookupFailed of BookingLookupError
    | BookingClosed of reason: string

let decide tryLoad bookingId =
    match tryLoad bookingId with
    | Error lookupError -> Error(LookupFailed lookupError)
    | Ok None -> Error NotFound
    | Ok(Some booking) ->
        match Booking.status booking with
        | Open detail -> Ok $"change:{detail}"
        | Closed reason -> Error(BookingClosed reason)
```

Here `tryLoad` has a conceptual type like:

```fsharp
BookingId -> Result<Booking option, BookingLookupError>
```

The one explicit call performs acquisition. Only the already loaded status reaches the pure `Open | Closed` view. Lookup failure, absence, and closed-domain state remain distinct, while loading and domain matching are structurally separate.

If the existing repository really returns only `Booking option`, bind its result once before matching so at least the query is neither hidden nor repeated, then improve the boundary to model infrastructure errors.

### Repeated occurrences multiply work {#exercise-03-repetition}

This form contains two recognizer occurrences:

```fsharp
match bookingId with
| ExistingBooking booking when canChange booking -> "change"
| ExistingBooking _ -> "closed"
| _ -> "missing"
```

If the first occurrence matches but its guard is false, matching proceeds and the second occurrence can invoke the recognizer again. The same issue appears with `AtLeast 5` followed by `AtLeast 2`: two specializations are two calls when the first fails. With a cheap pure comparison that is fine; with a database query it is a correctness and latency problem.

## What to notice {#what-to-notice}

- **Views do not create states:** complete active cases repartition or project existing values.
- **Option is an intentional information reduction:** use it only when non-match is the entire needed result.
- **Effects precede matching:** acquire once, preserve acquisition failure, then classify the obtained value.
- **Occurrences are work:** compact pattern syntax does not memoize a recognizer.
- **Ordinary functions remain first-class choices:** use an active pattern only when its match vocabulary improves repeated call sites.
