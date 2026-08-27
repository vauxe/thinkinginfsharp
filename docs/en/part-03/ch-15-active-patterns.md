---
title: "Chapter 15: Active Patterns and Domain Views"
description: "Use complete, partial, and parameterized active patterns as explicit domain views while keeping I/O, expensive work, and detailed failure visible."
translationKey: part-03/ch-15-active-patterns
---

# Chapter 15: Active Patterns and Domain Views {#overview}

Ordinary patterns match the cases and fields declared by a type. Active patterns let a function provide another named view for matching. A booking status can therefore be viewed as `Open` or `Closed` without changing its union; text can be recognized as a positive seat count without duplicating parsing code at every caller.

That convenience has a cost: an active pattern is executable code invoked during matching. If it opens a file, calls a database, builds an expensive parser, reads the clock, or discards a useful error, the match looks simpler while its behavior becomes obscure. Good active patterns provide a cheap, deterministic view of values already in hand.

## Use ordinary patterns first {#ordinary-patterns-first}

If a visible discriminated union already expresses the exact cases consumers need, match it directly. If one call site needs a calculation, a named function returning `option` or `Result` is usually easier to call, test, and compose. Active patterns earn their syntax when a recurring alternate view makes several matches read in domain language.

They are especially useful when:

- the declared representation should stay private but F# consumers need a matchable view;
- one type has several legitimate decompositions;
- a cheap recognizer gives a recurring subset a good name;
- parameters specialize a simple recognition rule at the match site.

They do not construct valid domain values. Keep smart constructors for validation and invariant enforcement; use active patterns to view values or recognize a subset.

## An active pattern is a recognizer function {#recognizer-model}

The “banana clip” syntax names cases supplied by a function:

```fsharp
let (|CaseA|CaseB|) input =
    // return CaseA payload or CaseB payload

let (|View|) input =
    // return the payload of an always-successful view

let (|Recognized|_|) input =
    // return Some payload or None
```

At a match site, these names occupy pattern position, but their definitions execute like functions. Inputs and returned payloads are statically typed. The compiler does not make the recognizer pure, cheap, or exception-free.

The three principal forms differ in one question:

| Form | Definition syntax | Match behavior |
|---|---|---|
| Complete multi-case | `(|A|B|)` | Every input becomes exactly one named partition |
| Complete single-case | `(|View|)` | Every input decomposes successfully into the returned payload |
| Partial single-case | `(|Case|_|)` | Some inputs match; others continue to later clauses |

## Complete active patterns partition every input {#complete-active-patterns}

The shared script groups three declared statuses into a two-case workflow view:

```fsharp:line-numbers [ch15-active-patterns.fsx]
let (|Open|Closed|) status =
    match status with
    | Pending -> Open "pending"
    | Confirmed code -> Open $"confirmed:{code}"
    | Cancelled reason -> Closed reason

let describeStatus status =
    match status with
    | Open detail -> $"open:{detail}"
    | Closed reason -> $"closed:{reason}"

printfn "Complete: pending=%s" (describeStatus Pending)
printfn "Complete: confirmed=%s" (describeStatus (Confirmed "C-42"))
printfn "Complete: cancelled=%s" (describeStatus (Cancelled "duplicate"))
```
`Open` and `Closed` cover every `BookingStatus`. The recognizer must return one of those active cases for every input, and a match containing both cases is exhaustive. Each case can carry a payload chosen for the consumer's view.

This does not add new states to `BookingStatus`. `Pending` and `Confirmed` remain different domain states even though this workflow views both as open. The alternate partition is useful precisely because it does not rewrite the source model.

A complete active pattern can declare up to seven cases. That is a language limit, not a target: a large list of computed cases is often harder to understand than a direct union or an explicit classifier result.

### A single complete case only decomposes {#single-case-complete}

Sometimes every input has one useful projection:

```fsharp
let (|BookingSummary|) booking =
    Booking.requestId booking,
    Booking.seats booking,
    Booking.status booking

let render (BookingSummary(requestId, seats, status)) =
    // use the projected values
    ()
```

`BookingSummary` cannot fail, so it is an irrefutable view. Single-case complete active patterns can also appear in function parameters and `let` patterns. Use them when the named decomposition communicates more than an ordinary helper returning a tuple; otherwise the helper is simpler.

## Partial active patterns recognize a subset {#partial-active-patterns}

A wildcard inside the name list marks a partial pattern:

```fsharp
let (|Positive|_|) value =
    if value > 0 then Some value else None
```

`Some payload` means the named case matched and binds the payload. `None` means this pattern did not match, so the match expression tries a later clause. Partial patterns do not need to be mutually exclusive; top-to-bottom clause order resolves overlap.

The shared script recognizes positive integer text as a seat count:

```fsharp:line-numbers [ch15-active-patterns.fsx]
let (|SeatCount|_|) raw =
    match parseSeatCount raw with
    | Ok value -> Some value
    | Error _ -> None

let describeRawSeatCount raw =
    match raw with
    | SeatCount value -> $"matched:{value}"
    | _ -> "not-matched"

printfn
    "Partial: three=%s zero=%s text=%s"
    (describeRawSeatCount "3")
    (describeRawSeatCount "0")
    (describeRawSeatCount "oops")
```
Both `"0"` and `"oops"` become “not matched.” That is appropriate only when the caller needs a yes/no classification.

### Non-match is smaller than a modeled error {#non-match-versus-error}

The underlying `parseSeatCount` returns two distinct errors. Converting that `Result` to `Some`/`None` deliberately erases the reason. The script prints the explicit errors separately to make the loss visible.

Use a partial active pattern when:

- failure means only “try the next pattern”;
- diagnostics are irrelevant at this decision point;
- the recognizer does not need to validate and return a protected value with rich errors.

Keep `Result` visible when a UI, API, log, retry policy, or test must know why recognition failed. Never catch an arbitrary exception inside a partial pattern and translate it to `None`; that turns a defect or infrastructure failure into a misleading non-match. Exceptions thrown by a recognizer otherwise propagate normally—`match` does not suppress them.

## Parameters specialize one recognizer {#parameterized-active-patterns}

Extra arguments precede the final value being matched:

```fsharp
let (|AtLeast|_|) minimum value =
    if value >= minimum then Some value else None

match seats with
| AtLeast 5 actual -> $"large:{actual}"
| AtLeast 2 actual -> $"group:{actual}"
| actual -> $"single:{actual}"
```

`minimum` is supplied by the pattern occurrence; `value` comes from the match input. Only single-case active patterns—complete or partial—can be parameterized. A multi-case active pattern cannot take these extra specialization arguments.

The instrumented shared example records recognizer calls:

```fsharp:line-numbers [ch15-active-patterns.fsx]
let mutable thresholdChecks = 0

let (|AtLeast|_|) minimum value =
    thresholdChecks <- thresholdChecks + 1

    if value >= minimum then Some value else None

let classifyParty seats =
    match seats with
    | AtLeast 5 actual -> $"large:{actual}"
    | AtLeast 2 actual -> $"group:{actual}"
    | actual -> $"single:{actual}"

let classifyWithCount seats =
    thresholdChecks <- 0
    let label = classifyParty seats
    label, thresholdChecks

let largeLabel, largeChecks = classifyWithCount 6
let groupLabel, groupChecks = classifyWithCount 3
let singleLabel, singleChecks = classifyWithCount 1

printfn
    "Parameterized: six=%s/%d three=%s/%d one=%s/%d"
    largeLabel
    largeChecks
    groupLabel
    groupChecks
    singleLabel
    singleChecks
```
Six seats satisfy the first clause after one check. Three seats fail `AtLeast 5`, then satisfy `AtLeast 2`, so the recognizer runs twice. One seat also checks both parameterized occurrences before reaching the fallback.

The counter demonstrates evaluation frequency; it is not a recommended design. Each pattern occurrence performs executable work, and refactoring clauses can change how often it runs. Correctness must not depend on a hidden mutable call count.

## Keep matching cheap, deterministic, and local {#effect-boundary}

An active pattern should normally inspect the value already being matched. Good work includes field projection, arithmetic classification, a bounded string check, or adapting a private representation. Suspicious work includes:

- database, HTTP, filesystem, or other I/O;
- reading current time, randomness, environment variables, or mutable global state;
- unbounded traversal or repeated enumeration of a deferred source;
- compiling a regular expression or building a large index on every pattern attempt;
- swallowing exceptions or detailed domain errors;
- mutation that changes later clauses' meaning.

Make acquisition explicit, then match the acquired value:

```fsharp
let decide loadBooking bookingId =
    match loadBooking bookingId with
    | Error loadError -> Error loadError
    | Ok booking ->
        match Booking.status booking with
        | Open detail -> Ok $"change:{detail}"
        | Closed reason -> Error $"closed:{reason}"
```

The function call shows where loading happens and preserves its error. The inner active pattern now performs only an in-memory status view. Later chapters make the loading step asynchronous without moving I/O into the pattern.

If a recognizer needs a precompiled parser or policy, pass that cheap prepared value as a parameter or close over it in a module-scoped definition. Do not let compact syntax hide setup cost.

## Public active patterns are public contracts {#public-contract}

An active pattern can let F# callers match a type whose union cases or fields remain private. This supports representation changes, but it does not make the pattern free to change. Case names, case count, input type, payload types, and complete-versus-partial behavior all affect consumer source code.

Expose a small stable view and document its semantics. Do not mirror every private field through an active pattern, because that recreates the private representation as public API. Chapter 17 places such views in a signature file and tests what consumers can actually observe.

## Return-form refinements belong after measurement {#return-forms}

The ordinary partial form returns `option`. When a pattern carries no payload, F# 9 and later also permit `bool`:

```fsharp
let (|Even|_|) value =
    value % 2 = 0
```

For a measured hot path that must return a payload without allocating `Some`, a partial pattern can explicitly return `voption`:

```fsharp
[<return: Struct>]
let (|Integer|_|) (raw: string) =
    match System.Int32.TryParse raw with
    | true, value -> ValueSome value
    | false, _ -> ValueNone
```

The return attribute is required; changing only the expression to `ValueSome`/`ValueNone` is not enough. Start with `option`, measure a real allocation problem, and optimize only the affected hot path. A struct return does not make expensive recognition cheap.

F# also supplies `Null`/`NonNull` active patterns for nullable references. Chapter 19 treats them with the complete .NET null model rather than mixing that concern into domain recognition here.

## A small selection rule {#selection-rule}

| Need | Prefer |
|---|---|
| Match the declared public union cases | Direct pattern matching |
| Compute once and retain a detailed failure | Function returning `Result` |
| Construct a value while enforcing invariants | Smart constructor |
| Reuse an alternate total view in matches | Complete active pattern |
| Reuse a yes/no subset view in matches | Partial active pattern |
| Specialize a cheap single-case view at each match | Parameterized active pattern |
| Load or query external state | Explicit effectful function, then match its result |

Readable pattern syntax is the result, not the goal. If the recognizer's name hides more than its cases reveal, return to an ordinary function.

## Run the shared example {#run-example}

From the directory containing the example:

```console
dotnet fsi --exec ch15-active-patterns.fsx
```

Six deterministic lines cover every complete partition, successful and failed partial recognition, preserved error details, and parameterized recognizer call counts.

## Exercises {#exercises}

### Exercise 1: design two total views {#exercise-01}

Given `Pending`, `Confirmed of code`, and `Cancelled of reason`, define:

1. a complete `Open | Closed` active pattern, where pending and confirmed are open;
2. a single-case `StatusLabel` active pattern that returns display text for every status.

Use both in match expressions. Explain why neither pattern adds or removes a domain state.

### Exercise 2: preserve the useful failure {#exercise-02}

A partial `SeatCount` pattern turns both nonnumeric text and nonpositive integers into non-match. Write an explicit `parseSeatCount : string -> Result<int, SeatCountError>` with separate errors, then state one call site suited to the partial pattern and one that must use the result.

Show precisely what information is lost when the result becomes an option.

### Exercise 3: move I/O out of matching {#exercise-03}

Review this recognizer:

```fsharp
let (|ExistingBooking|_|) bookingId =
    repository.tryLoad bookingId
```

Assume `tryLoad` queries a database and returns `Booking option`. Explain the cost and failure problems hidden by the pattern. Rewrite the workflow so loading is explicit and a pure active pattern is applied only after a booking has been obtained. State how parameterized pattern clauses can multiply work.

[Read the chapter solutions](../solutions/ch-15-active-patterns).

## Model review {#model-review}

- Active patterns are recognizer functions used through pattern syntax.
- Complete patterns classify every input; partial patterns may continue to later clauses.
- Only single-case active patterns can take specialization parameters.
- A partial non-match carries less information than a modeled error.
- Each pattern occurrence can execute recognizer work, so clause order and repeated attempts matter.
- Keep I/O, changing external state, expensive setup, and exception handling explicit outside the match.
- Public case names and payload types are part of the API, even when the underlying representation stays private.

Chapter 16 moves from expression-level views to program structure: modules, namespaces, file order, projects, and compiler settings determine which definitions are available at all.

## Sources {#sources}

- [Microsoft Learn: active patterns](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/active-patterns)
- [Microsoft Learn: pattern matching](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/pattern-matching)
- [Microsoft Learn: F# component design guidelines](https://learn.microsoft.com/en-us/dotnet/fsharp/style-guide/component-design-guidelines)
- [FSharp.Core: ValueOption module](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-valueoption.html)
