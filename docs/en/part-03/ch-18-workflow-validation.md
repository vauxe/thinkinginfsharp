---
title: "Chapter 18: Explicit Workflow Composition and Validation Accumulation"
description: "Choose first-error sequencing for dependent steps and explicit error accumulation for independent checks, using ordinary F# functions before builder syntax."
translationKey: part-03/ch-18-workflow-validation
---

# Chapter 18: Explicit Workflow Composition and Validation Accumulation {#overview}

“Validation failed” does not determine how failures should combine. If a later step needs a value produced by an earlier step, the workflow must stop when that value is unavailable. If three checks inspect independent fields, a form may be more useful when all three failures are reported together.

Both policies can return `Result`. The difference is not the container's name but the combining function. This chapter implements both policies with ordinary functions so evaluation and error order remain visible. Computation-expression syntax is discussed only after the behavior exists without it.

## Ask whether the next check needs the previous value {#dependency-question}

Before choosing syntax, draw the data dependencies:

```text
raw seat text ──▶ parse positive SeatCount ──▶ compare with capacity

raw request id ──▶ validate id ───────┐
raw attendee ─────▶ validate name ────┼──▶ construct ValidBooking
raw seat text ─────▶ validate seats ──┘
```

The capacity comparison depends on a parsed, positive `SeatCount`. By contrast, all three field branches can read the raw request independently. Here “independent” describes data requirements; execution remains sequential unless the program introduces parallelism.

Use the following starting rule:

| Relationship | Combination policy | Why |
|---|---|---|
| Later step requires earlier success | Short-circuit | There is no valid input for the later step after failure |
| Checks inspect independent data already in hand | Accumulate when callers need all failures | Every check can provide useful feedback |
| Check performs I/O or changes state | Keep it in a separate effectful phase | Cost, faults, cancellation, and staleness need their own policy |

Do not accumulate by reflex. A command-line tool may intentionally report only the first syntax error, and a security boundary may avoid revealing several details. Choose from the consumer requirement.

## Give each successful field a type {#model}

The shared script separates raw text from successfully checked values:

```fsharp:line-numbers [ch18-workflow-validation.fsx]
type ValidationError =
    | MissingRequestId
    | MissingAttendee
    | SeatsNotInteger of raw: string
    | NonPositiveSeats of actual: int
    | ExceedsCapacity of requested: int * available: int

type RequestId = RequestId of string
type Attendee = Attendee of string
type SeatCount = SeatCount of int

type RawBooking =
    { RequestId: string
      Attendee: string
      Seats: string }

type ValidBooking =
    { RequestId: RequestId
      Attendee: Attendee
      Seats: SeatCount }
```
`RawBooking` can contain blank or malformed text. `ValidBooking` requires `RequestId`, `Attendee`, and `SeatCount` values, so its construction is delayed until the three component checks succeed.

The error union keeps facts rather than formatted UI messages. Each field validator returns `Result<'Value, ValidationError list>`:

```fsharp:line-numbers [ch18-workflow-validation.fsx]
let validateRequestId raw =
    if String.IsNullOrWhiteSpace raw then
        Error [ MissingRequestId ]
    else
        Ok(RequestId(raw.Trim()))

let validateAttendee raw =
    if String.IsNullOrWhiteSpace raw then
        Error [ MissingAttendee ]
    else
        Ok(Attendee(raw.Trim()))

let validateSeats (raw: string) =
    match Int32.TryParse raw with
    | true, value when value > 0 -> Ok(SeatCount value)
    | true, value -> Error [ NonPositiveSeats value ]
    | false, _ -> Error [ SeatsNotInteger raw ]
```
Each individual validator currently produces either one value or a singleton error list. A list gives the combining layer a common error carrier. The type technically permits `Error []`; this implementation never produces it. If non-emptiness itself becomes a critical invariant, protect it with a non-empty error type rather than relying on convention.

Inside `validateSeats`, integer parsing must precede the positivity comparison. Those two checks are dependent: there is no integer to compare when parsing fails. Accumulation across fields does not require pretending every operation inside one field is independent.

## `Result.bind` preserves the first error {#first-error}

FSharp.Core defines the essential behavior of `Result.bind` as:

```fsharp
match input with
| Error error -> Error error
| Ok value -> binder value
```

The binder is not called in the `Error` case. The error type is preserved; `bind` has no operation that appends errors.

The first strategy nests three dependent continuations:

```fsharp:line-numbers [ch18-workflow-validation.fsx]
let validateFirstError (raw: RawBooking) =
    validateRequestId raw.RequestId
    |> Result.bind (fun requestId ->
        validateAttendee raw.Attendee
        |> Result.bind (fun attendee ->
            validateSeats raw.Seats
            |> Result.map (fun seats ->
                { RequestId = requestId
                  Attendee = attendee
                  Seats = seats })))
```
With a completely invalid request, `validateRequestId` returns `Error [MissingRequestId]`. The attendee and seat validators are inside the success continuation, so neither runs and the result contains only the first error.

This behavior is correct when each step consumes the protected output of the preceding step. It is also a valid product policy when only one message should be returned. But merely reordering `Result.bind` calls cannot make it accumulate errors; it only changes which failure becomes first.

### Order becomes observable policy {#first-error-order}

When two checks could both fail, first-error composition makes their order visible to callers. Put structural prerequisites early and document the policy. Do not rely on a convenient source-code order if the product promises a particular priority.

Short-circuiting also prevents unnecessary work, but that is a consequence rather than a license to hide effects inside validators. Chapters 20–23 make time, I/O, faults, and cancellation explicit.

## Accumulation evaluates independent results first {#accumulation}

The accumulating strategy evaluates all three field functions before deciding whether construction is possible:

```fsharp:line-numbers [ch18-workflow-validation.fsx]
let errorsOf result =
    match result with
    | Ok _ -> []
    | Error errors -> errors

let validateAccumulating (raw: RawBooking) =
    let requestIdResult = validateRequestId raw.RequestId
    let attendeeResult = validateAttendee raw.Attendee
    let seatsResult = validateSeats raw.Seats

    match requestIdResult, attendeeResult, seatsResult with
    | Ok requestId, Ok attendee, Ok seats ->
        Ok
            { RequestId = requestId
              Attendee = attendee
              Seats = seats }
    | _ ->
        [ yield! errorsOf requestIdResult
          yield! errorsOf attendeeResult
          yield! errorsOf seatsResult ]
        |> Error
```
If every result is `Ok`, the match constructs one `ValidBooking`. Otherwise, `errorsOf` contributes each failure list in field order. The invalid example therefore produces:

```text
[missing-request-id; missing-attendee; seats-not-integer:oops]
```

This is validation accumulation: evaluate independent checks and combine their failures in a stated order. The code executes sequentially in ordinary F# evaluation order, but no check is skipped because another field failed.

Keep error order deterministic. Request ID, attendee, then seats matches the input layout here. A stable ordering makes tests, UI focus, logs, and client behavior predictable. A `Set` would deduplicate and sort by comparison instead of preserving field order, which would change the observable behavior.

### Accumulation is not “keep going after anything” {#accumulation-limits}

Only combine errors that remain meaningful together. If decoding a document fails, checks over its absent fields cannot run. If an authenticated identity is unavailable, an authorization decision lacks its subject. First obtain the prerequisite, then accumulate checks that share the resulting data.

Likewise, do not construct a half-valid domain record and patch it later. Keep successful component results separate, collect errors if needed, and construct the final type in the all-`Ok` branch.

## Extract the combining rule as an ordinary function {#reusable-accumulation}

The direct three-way match is easy to audit. When the pattern repeats, factor only the combination mechanics:

```fsharp:line-numbers [ch18-workflow-validation.fsx]
let applyValidation valueResult functionResult =
    match functionResult, valueResult with
    | Ok mapping, Ok value -> Ok(mapping value)
    | Error functionErrors, Error valueErrors -> Error(functionErrors @ valueErrors)
    | Error errors, Ok _
    | Ok _, Error errors -> Error errors

let createBooking requestId attendee seats : ValidBooking =
    { RequestId = requestId
      Attendee = attendee
      Seats = seats }

let validateAccumulatingWithApply (raw: RawBooking) =
    Ok createBooking
    |> applyValidation (validateRequestId raw.RequestId)
    |> applyValidation (validateAttendee raw.Attendee)
    |> applyValidation (validateSeats raw.Seats)
```
`applyValidation` has four cases:

- apply the successful function to the successful value;
- append left-to-right errors when both failed;
- retain the one error list when only one failed.

The curried `createBooking` begins inside `Ok`. Each application supplies one independently computed component. The shared script asserts that this refactoring returns exactly the same errors, order, and successful value as the explicit match.

The function takes the next value result first so an accumulated function result can flow through `|>`. That parameter order is a local F# API choice, not a universal name for applicative application.

Repeated list append can become expensive for very large validation sets. A small fixed form has tiny lists and favors clarity. If profiling later identifies a real large-scale cost, accumulate in reverse or use an intentionally non-empty structure; do not obscure three fields with a speculative data structure.

## Dependent stages should remain short-circuiting {#dependent-workflow}

After a seat count exists, comparing it with capacity is a dependent business check:

```fsharp:line-numbers [ch18-workflow-validation.fsx]
let ensureWithin capacity (SeatCount requested as seats) =
    if requested <= capacity then
        Ok seats
    else
        Error [ ExceedsCapacity(requested, capacity) ]

let validateSeatsThenCapacity checkCapacity rawSeats =
    validateSeats rawSeats |> Result.bind checkCapacity

let observeDependentValidation rawSeats =
    let mutable capacityChecks = 0

    let observedCheck seats =
        capacityChecks <- capacityChecks + 1
        ensureWithin 4 seats

    validateSeatsThenCapacity observedCheck rawSeats, capacityChecks
```
`validateSeatsThenCapacity` uses `Result.bind`. Its injected capacity function receives only a valid `SeatCount`. The instrumented wrapper counts calls without putting mutation into either production function:

| Input | Result | Capacity checks |
|---|---|---:|
| `"oops"` | `SeatsNotInteger "oops"` | 0 |
| `"5"` with capacity 4 | `ExceedsCapacity(5, 4)` | 1 |
| `"3"` with capacity 4 | `Ok(SeatCount 3)` | 1 |

The zero directly demonstrates short-circuiting; it is not a timing-based optimization claim. The count is test instrumentation, and correctness must not depend on it.

A real booking workflow commonly uses both policies:

```text
accumulate independent raw-field errors
                  ↓ only if valid
short-circuit dependent domain decisions
                  ↓ only if accepted
perform explicit effects at the boundary
```

One label such as “validation” should not flatten these different semantics.

## Keep external checks out of pure accumulation {#effect-boundary}

An email-format check over a string in memory can join field accumulation. “Email is unique in the database” is different: it has latency, can fault, needs cancellation, and can become stale immediately after the query. Running three such checks just to collect messages may multiply cost and reveal inconsistent snapshots.

First accumulate cheap deterministic facts already in hand. Then execute necessary external decisions under a stated policy. The final write must still enforce concurrency-sensitive rules atomically; a prior validation query is not a lock.

This separation also keeps tests honest. Pure accumulation needs only input and expected values. Effectful work needs ports, controlled substitutes, and later cancellation/resource tests.

## Computation expressions do not choose semantics for you {#computation-expressions}

A computation expression has the form `builder { ... }`. Keywords such as `let!`, `return`, and `and!` are translated through methods supplied by that particular builder. The syntax does not have one universal meaning independent of the builder.

FSharp.Core supplies built-in sequence, async, task, and query computation expressions. It does not supply a built-in `result {}` or accumulating `validation {}` expression. The `Result` type and `Result.bind` function are built in; a builder using them must come from your own code or a chosen library.

A simple result builder normally defines `Bind` with first-error behavior. Writing sequential `let!` bindings would make code shorter, but would not turn it into accumulation.

### Extension: builder-specific `and!` {#and-bang-extension}

F# supports `let! ... and! ...` for bindings declared independent within one computation-expression group. The builder's `MergeSources` (or related optimized members) defines how sources combine. A particular validation builder may append error lists; an async/task builder may arrange concurrent starts; another builder may do something else.

This illustrative form is not runnable without a specifically defined or imported `validation` builder:

```fsharp
validation {
    let! requestId = validateRequestId raw.RequestId
    and! attendee = validateAttendee raw.Attendee
    and! seats = validateSeats raw.Seats
    return createBooking requestId attendee seats
}
```

The bindings cannot depend on one another within that group. Never describe `and!` alone as “accumulate errors” or “run in parallel”; state the builder and what its `MergeSources` does. Custom builders can be useful once a codebase repeats a proven workflow, but they introduce another API and debugging translation. The ordinary functions here remain the semantic baseline.

## Choose the smallest honest composition {#selection-rule}

| Need | Prefer |
|---|---|
| Next step requires the previous successful value | `Result.bind` or a direct match |
| Return one prioritized failure | Ordered first-error composition |
| Return all independent pure input errors | Direct accumulation or a small tested apply/map function |
| Perform an external check | A later effectful workflow step |
| Repeated stable syntax with agreed team/library conventions | A documented computation-expression builder |
| One unusual combination | Direct pattern matching rather than new abstraction |

Types state possible results; combining functions state evaluation policy. Review both.

## Run the shared example {#run-example}

From the directory containing the example:

```console
dotnet fsi --exec ch18-workflow-validation.fsx
```

Seven deterministic lines and assertions verify first-error output, three- and two-error accumulation, agreement on valid input, and capacity-check counts for invalid, excessive, and accepted seat text. Compare their exact order.

## Exercises {#exercises}

### Exercise 1: draw two validation phases {#exercise-01}

A booking command must check a request ID, attendee name, integer seat text, capacity, and request-ID uniqueness in a database. Classify each check as independent pure input validation, dependent domain validation, or external effectful work.

Draw an execution order that reports useful input errors together without querying the database for structurally invalid input. State where short-circuiting is required and where accumulation is useful.

### Exercise 2: implement ordered accumulation {#exercise-02}

Write an ordinary `applyValidation` for `Result<'T, 'Error list>`, then use it with a curried constructor to validate a name, email, and seat count. When all three fail, errors must appear in that field order. Assert that all-valid input constructs the final record.

Explain what `Error []` would mean and whether your API should make that state unrepresentable.

### Exercise 3: audit computation-expression claims {#exercise-03}

Review this code shown without imports or a builder definition:

```fsharp
result {
    let! requestId = validateRequestId raw.RequestId
    and! seats = validateSeats raw.Seats
    return requestId, seats
}
```

Explain why FSharp.Core alone does not establish that this compiles or accumulates errors. Name the builder operations relevant to `let!` and `and!`, state the independence restriction, then rewrite the two-check behavior with ordinary functions whose error policy is explicit.

[Read the chapter solutions](../solutions/ch-18-workflow-validation).

## Model review {#model-review}

- Dependency, not visual syntax, determines whether a later check can run.
- `Result.bind` returns the existing `Error` without calling its success continuation.
- Independent pure checks can all run and have their errors accumulated in a documented order.
- A field may contain its own dependent substeps even when fields are accumulated together.
- Construct the valid domain value only in the all-success branch.
- Keep database, network, time, and other effects outside pure input accumulation.
- FSharp.Core has `Result` and combinators, but no built-in result or validation computation-expression builder.
- `and!` expresses independent bindings; its merge behavior belongs to the selected builder.
- Ordinary functions provide a readable semantic baseline before custom syntax earns its cost.

## Part III checkpoint {#part-checkpoint}

Run the focused workflow tests from the directory containing the example:

```console
dotnet test ExampleTests.fsproj --configuration Release --filter FullyQualifiedName~BookingWorkflowTests
```

Passing tests show that independent command errors accumulate in field order, valid commands produce events, and existing state short-circuits later capacity work. They exercise ordinary functions, so the result does not depend on an unstated computation-expression builder.

[Continue to Chapter 19](../part-04/ch-19-dotnet-null-boundaries), where external .NET values enter through a dedicated adapter.

## Sources {#sources}

- [Microsoft Learn: F# results](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/results)
- [FSharp.Core: `Result` module](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-resultmodule.html)
- [Microsoft Learn: computation expressions and `and!`](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/computation-expressions)
- [Microsoft Learn: F# component design guidelines](https://learn.microsoft.com/en-us/dotnet/fsharp/style-guide/component-design-guidelines)
