---
title: "Chapter 9: Absence and Expected Failure"
description: "Derive option from meaningful absence and Result from expected failure, then compose both without losing error context."
translationKey: part-02/ch-09-option-result
---

# Chapter 9: Absence and Expected Failure {#overview}

A search may legitimately find no booking. A booking request may instead be found but fail because it asks for too many seats. Both computations lack a normal value, but they do not communicate the same thing. In the first, absence is the whole answer. In the second, a caller needs the reason.

F# gives those two meanings different types. `'T option` says “there may be a `'T`.” `Result<'T, 'TError>` says “there is either a successful `'T` or a modeled `'TError`.” Both are discriminated unions, so callers must handle their cases rather than rely on a sentinel value or undocumented exception.

## What you will be able to do {#outcomes}

By the end of this chapter, you should be able to:

- choose `option` when absence needs no further explanation;
- choose `Result` when an expected failure reason matters;
- consume both types safely with pattern matching and defaults;
- distinguish `map` from `bind` by the return type of the next function;
- compose validation steps while preserving the first error;
- attach context with `Result.mapError` without changing success values;
- explain why `Some null` is possible and why it should usually be normalized at a boundary.

The chapter stays synchronous and uses explicit functions. Computation expressions arrive in Chapter 18, nullable-reference interoperation in Chapter 19, and exception boundaries in Chapter 21.

## Absence should be a case, not a secret value {#absence-as-data}

Imagine a function that returns `""` when an attendee is not found. A caller cannot tell whether the empty string means “missing” or is stored data. Returning `null` moves the ambiguity into a runtime convention, and throwing makes an ordinary search outcome look exceptional.

An option has exactly two cases:

```fsharp
type Option<'T> =
    | Some of 'T
    | None
```

The definition shown is conceptual—the type already exists in FSharp.Core. `Some value` proves a value is present; `None` states that it is absent. The return type records the possibility before the code runs.

The shared script uses the standard `try` naming convention for an operation that may not produce a value:

```fsharp:line-numbers [ch09-option-result.fsx]
let attendees = [ "B-101", "Lin"; "B-102", "Ada" ]

let tryFindAttendee bookingId =
    attendees |> List.tryFind (fun (id, _) -> id = bookingId) |> Option.map snd

let knownAttendee = tryFindAttendee "B-101" |> Option.defaultValue "none"

let missingAttendee = tryFindAttendee "B-999" |> Option.defaultValue "none"

printfn "Lookup: known=%s missing=%s" knownAttendee missingAttendee
```
`List.tryFind` returns an option. `Option.map snd` transforms the tuple only when it exists: `Some (id, name)` becomes `Some name`, while `None` remains `None`. The function never invents a placeholder attendee.

### Consume absence deliberately {#consuming-option}

Pattern matching retains both cases when they lead to different behavior:

```fsharp
let lookupMessage bookingId =
    match tryFindAttendee bookingId with
    | Some attendee -> $"attendee:{attendee}"
    | None -> "booking not found"
```

When a genuine fallback is enough, `Option.defaultValue fallback option` collapses the two cases at that boundary. `Option.defaultWith` delays computing an expensive fallback until it is needed.

Avoid treating `.Value` or `Option.get` as the normal way to unwrap an option. Both throw for `None`, discarding the safety expressed by the type. They are reasonable only when the proof of `Some` is local and obvious; a match usually records that proof more clearly.

## `map` changes a present value; `bind` continues a search {#option-composition}

Suppose finding a row and accepting its seat count can each return no value. Mapping the second function would create `int option option`:

```fsharp
// Produces a nested option because tryPositiveSeats already returns an option.
rowOption |> Option.map tryPositiveSeats
```

`Option.bind` joins those two absence-producing steps:

```fsharp:line-numbers [ch09-option-result.fsx]
let requestedSeats = [ "B-101", 3; "B-102", 0 ]

let tryPositiveSeats seats = if seats > 0 then Some seats else None

let tryRequestedSeats bookingId =
    requestedSeats
    |> List.tryFind (fun (id, _) -> id = bookingId)
    |> Option.map snd
    |> Option.bind tryPositiveSeats

let positiveSeats =
    tryRequestedSeats "B-101" |> Option.map string |> Option.defaultValue "none"

let nonPositiveSeats =
    tryRequestedSeats "B-102" |> Option.map string |> Option.defaultValue "none"

printfn "Option bind: positive=%s nonPositive=%s" positiveSeats nonPositiveSeats
```
Read the choice from the next function's return type:

| Next function | Operation | Result for `Some x` | Result for `None` |
| --- | --- | --- | --- |
| `'T -> 'U` | `Option.map` | `Some (f x)` | `None` |
| `'T -> 'U option` | `Option.bind` | `f x` | `None` |

`bind` **short-circuits**: once a step returns `None`, later dependent functions do not run. This is ordinary data flow encoded by the cases, not hidden control flow.

An option intentionally does not say *why* it is absent. If callers need to distinguish “unknown booking,” “invalid identifier,” and “directory unavailable,” `None` has erased information they need. That is the point where `Result` becomes the better model.

## Expected failures deserve an error type {#result-model}

`Result<'T, 'TError>` also has two cases:

```fsharp
// Conceptual shape; Result is already provided by FSharp.Core.
type Result<'T, 'TError> =
    | Ok of 'T
    | Error of 'TError
```

`Ok value` carries the successful value. `Error error` carries a reason chosen by the domain. A discriminated union is usually better than a bare error string because each failure shape remains machine-readable:

```fsharp:line-numbers [ch09-option-result.fsx]
let validateAttendee request =
    if String.IsNullOrWhiteSpace request.Attendee then
        Error EmptyAttendee
    else
        Ok request

let validateSeats maximum request =
    if request.Seats <= 0 then
        Error(NonPositiveSeats request.Seats)
    elif request.Seats > maximum then
        Error(TooManySeats(request.Seats, maximum))
    else
        Ok request

let validate maximum request =
    request |> validateAttendee |> Result.bind (validateSeats maximum)

let describeError error =
    match error with
    | EmptyAttendee -> "attendee is empty"
    | NonPositiveSeats actual -> $"seat count {actual} is not positive"
    | TooManySeats(requested, maximum) -> $"requested {requested} exceeds maximum {maximum}"

let describeResult result =
    match result with
    | Ok request -> $"ok:{request.Attendee}:{request.Seats}"
    | Error error -> $"error:{describeError error}"

let validRequest = { Attendee = "Lin"; Seats = 2 }

let emptyAttendeeRequest = { Attendee = ""; Seats = 2 }

printfn
    "Validation: success=%s failure=%s"
    (validate 4 validRequest |> describeResult)
    (validate 4 emptyAttendeeRequest |> describeResult)
```
`BookingError` distinguishes an empty attendee, a non-positive count with its actual value, and a request above a known maximum. Formatting is kept in `describeError`, so validation policy is not coupled to English UI text.

`validateAttendee` and `validateSeats` return `Result<BookingRequest, BookingError>`. The `validate` pipeline uses `Result.bind` because the second validation itself returns a result. If attendee validation returns `Error`, seat validation is skipped and that same error is preserved.

### Transform success and error independently {#result-transformations}

The module operations keep the two tracks explicit:

- `Result.map` transforms only the value inside `Ok`;
- `Result.mapError` transforms only the value inside `Error`;
- `Result.bind` runs the next result-producing function only for `Ok`;
- `Result.defaultValue` deliberately replaces an error with a fallback value.

As with option, choose `map` when the next function returns a plain value and `bind` when it already returns the same context. For example, `Result.map bookingLabel` retains errors, while `Result.bind reserveSeats` can produce a new error.

## Error context should grow toward the boundary {#error-context}

A low-level validation error may not identify which request caused it. Replace a string concatenation convention with structured context:

```fsharp:line-numbers [ch09-option-result.fsx]
type RequestFailure =
    { RequestId: string
      Cause: BookingError }

let addRequestContext requestId result =
    result
    |> Result.mapError (fun error -> { RequestId = requestId; Cause = error })

let oversizedRequest = { Attendee = "Ada"; Seats = 6 }

let contextualFailure = oversizedRequest |> validate 4 |> addRequestContext "R-9"

match contextualFailure with
| Ok _ -> printfn "Context: unexpected success"
| Error failure -> printfn "Context: %s -> %s" failure.RequestId (describeError failure.Cause)
```
`addRequestContext` changes only the error type. An `Ok request` passes through untouched; an `Error BookingError` becomes `Error RequestFailure`. Code farther out can log `RequestId`, translate `Cause`, or map the domain failure into an HTTP response without parsing text.

Do not attach every possible detail at the deepest function. Give each layer the error facts it owns, then add request, file, or endpoint context as the value moves outward. This keeps core domain functions reusable and avoids losing diagnostic identity in strings.

## `bind` preserves the first failure, not every failure {#short-circuiting}

The shared request violates two rules, but the pipeline returns the attendee error:

```fsharp:line-numbers [ch09-option-result.fsx]
let doublyInvalidRequest = { Attendee = ""; Seats = 0 }

printfn "Short circuit: %s" (validate 4 doublyInvalidRequest |> describeResult)
```
That behavior is correct for dependent steps: seat validation may make sense only after earlier data is valid. It is not an accumulating validator. If a form should display all independent errors at once, collect those results explicitly or use an applicative validation design; Chapter 18 returns to that distinction.

An `Error` should describe a failure the caller can reasonably inspect or handle. Do not catch every exception and turn it into a vague `Error "failed"`; that destroys stack and cause information. Bugs, cancellation, resource failure, and domain rejection have different boundaries. Chapter 21 develops that policy.

## Choosing the smallest truthful type {#choosing-a-type}

Use the question a caller must answer:

| Situation | Usually choose | Caller learns |
| --- | --- | --- |
| A lookup may have no match, and no-match is enough | `'T option` | present or absent |
| Parsing or validation may fail for useful known reasons | `Result<'T, 'Error>` | success or a modeled reason |
| A value is guaranteed by the function contract | `'T` | a value, with no advertised alternate case |
| A failure is unexpected or cannot be handled locally | not automatically `Result` | preserve the appropriate exception/cancellation boundary |

Do not return `Result<'T, unit>` merely to imitate option; use option when the error carries no information. Conversely, do not compress meaningful errors into `None` merely to make a signature shorter.

Nested shapes can be honest. `Result<'T option, 'Error>` can mean “the operation itself may fail; on success, a value may still be absent.” The two dimensions should be flattened only when the domain says they are the same fact.

## `Some null` is still possible {#some-null}

An option wraps a value; it does not sanitize that value. A nullable reference can therefore be wrapped in `Some`:

```fsharp:line-numbers [ch09-option-result.fsx]
let riskyPayload: (string | null) option = Some null

let payloadIsNull =
    match riskyPayload with
    | Some value -> isNull value
    | None -> false

printfn "Some null: isSome=%b payloadIsNull=%b" riskyPayload.IsSome payloadIsNull
```
This produces three representable states: `None`, `Some null`, and `Some "Lin"`. That is usually accidental complexity. At a .NET boundary, normalize a nullable result into `None` or reject it before core code receives the value.

The annotation `(string | null) option` makes the nullable payload explicit under F# nullness checking. This chapter needs only the warning: `Some` does not prove a reference payload is non-null. Chapter 19 explains `T | null`, `Nullable<T>`, legacy .NET annotations, and boundary conversion in full.

## Run the shared example {#run-example}

From the directory containing the example:

```console
dotnet fsi --exec ch09-option-result.fsx
```

The six deterministic lines cover a successful lookup, absence, option composition, validation success and failure, added error context, first-error short-circuiting, and the `Some null` edge case. Compare the exact output.

## Exercises {#exercises}

### Exercise 1: choose a return type {#exercise-01}

Choose `'T`, `'T option`, or `Result<'T, 'Error>` for each function and justify the information preserved:

1. find a booking by an otherwise valid identifier;
2. parse a seat count supplied as text;
3. calculate the attendee's initials from an already validated non-empty name;
4. query an external service that can fail, where a successful query may still find no booking.

### Exercise 2: compose optional data {#exercise-02}

Start with these functions:

```fsharp
tryFindBooking : string -> Booking option
tryConfirmedCode : Booking -> string option
```

Define `tryFindConfirmedCode : string -> string option`. Keep the result flat and express the composition directly, without pattern matching. Then explain why `bind` fits this composition and how `map` would change the result type.

### Exercise 3: preserve validation context {#exercise-03}

Complete four steps:

1. Add an `EventClosed` case to `BookingError`.
2. Write `validateOpen : bool -> BookingRequest -> Result<BookingRequest, BookingError>`.
3. Compose it after the existing validations.
4. Use `Result.mapError` to attach both a request identifier and an event identifier.

Finally, state which error a doubly invalid request returns and explain the precedence.

[Read the chapter solutions](../solutions/ch-09-option-result).

## Model review {#model-review}

- `option` models meaningful absence, not unexplained failure.
- `Result` preserves a successful value or a typed expected failure.
- `map` transforms a wrapped value; `bind` continues with a function that already returns the wrapper.
- `Option.bind` and `Result.bind` stop at the first `None` or `Error`.
- `Result.mapError` enriches failure context without disturbing success.
- An option can contain `null`; normalize nullable .NET values at a deliberate boundary.

Chapter 10 generalizes the same case-driven reasoning from two-case containers to recursive trees.

## Sources {#sources}

- [Microsoft Learn: Options](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/options)
- [Microsoft Learn: Results](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/results)
- [FSharp.Core: Option module](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-optionmodule.html)
- [FSharp.Core: Result module](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-resultmodule.html)
