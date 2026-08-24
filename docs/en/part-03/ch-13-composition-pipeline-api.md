---
title: "Chapter 13: Composition, Argument Order, and Pipeline APIs"
description: "Derive pipelines and function composition from nested calls, then design argument order around real calling forms without forcing every expression through a pipe."
translationKey: part-03/ch-13-composition-pipeline-api
kind: chapter
part: 3
chapter: 13
status: complete
verifiedWith:
  fsharp: "10"
  dotnetSdk: "10.0.301"
exampleIds:
  - ch13-composition-pipeline-api
exerciseIds:
  - ch13-exercise-01
  - ch13-exercise-02
  - ch13-exercise-03
termIds:
  - function-composition
  - parameter
  - partial-application
  - pipeline
sources:
  - id: microsoft-functions
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/functions/
    checked: "2026-08-24"
  - id: microsoft-parameters-arguments
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/parameters-and-arguments
    checked: "2026-08-24"
  - id: microsoft-formatting
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/style-guide/formatting
    checked: "2026-08-24"
  - id: microsoft-component-design
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/style-guide/component-design-guidelines
    checked: "2026-08-24"
---

# Chapter 13: Composition, Argument Order, and Pipeline APIs {#overview}

Small functions become useful when their outputs fit the next function's inputs. You can write that connection as nested calls, apply one value through a pipeline, or compose a new reusable function. These are not three programming models; they are three views of the same applications.

The shape of those calls also influences API design. A curried function whose primary data parameter comes last is easy to partially apply and pipe. But “always pipe” and “data must always be last” are poor substitutes for reading the call site. Some predicates, constructors, symmetric operations, and .NET methods are clearer as direct calls.

## What you will be able to do {#outcomes}

By the end of this chapter, you should be able to:

- expand `|>` into ordinary application and `>>`/`<<` into nested calls;
- distinguish applying a value now from creating a function for later;
- derive a pipeline from repeated inner-to-outer calls;
- derive forward and backward composition when intermediate types align;
- order configuration and transformation parameters to support partial application;
- identify APIs that require a lambda only because the flowing value is in the wrong position;
- choose a direct call when a pipeline adds motion but no clarity;
- avoid custom operators and point-free expressions that hide domain intent.

## Repeated nesting reveals a data path {#repeated-nesting}

The shared script begins with ordinary application:

<<< @/../examples/scripts/ch13-composition-pipeline-api.fsx#repeated-nesting{fsharp:line-numbers} [ch13-composition-pipeline-api.fsx]

Read from the innermost parentheses outward:

1. trim the attendee;
2. cap the requested seats at four;
3. add the `web` channel;
4. produce a label.

The code is correct, but the reading direction repeatedly reverses: source text begins with `toLabel`, while execution begins with `trimAttendee`. More nested parentheses also make it harder to pause and inspect an intermediate value.

Do not introduce a pipeline merely because one call is nested. The signal is a genuine chain in which each whole result becomes the primary input of the next operation.

## A pipeline applies one value now {#pipeline}

The forward pipe has the essential behavior:

```fsharp
value |> functionValue
// means
functionValue value
```

Rewriting the shared chain gives:

<<< @/../examples/scripts/ch13-composition-pipeline-api.fsx#pipeline{fsharp:line-numbers} [ch13-composition-pipeline-api.fsx]

Now source order follows data flow. Each line transforms the value from the preceding line, and the final result is computed immediately.

The type between stages must fit. If `trimAttendee : BookingDraft -> BookingDraft` and `toLabel : BookingDraft -> string`, then `trimAttendee` can precede `toLabel`. A function requiring some unrelated input cannot be inserted merely by adding `|>`.

Pipelines are ordinary application, not an effect system or automatic error propagation. Piping a `Result` into `Result.bind next` works because that function's final parameter is a result; `|>` itself knows nothing about `Ok` or `Error`.

### Multi-line shape should expose stages {#pipeline-formatting}

For more than a very short expression, begin with the value and align one `|>` stage per line, as the shared script does. Name a stage when its lambda becomes long or when a debugger needs a meaningful boundary. A pipeline is readable because its transformations are visible, not because it minimizes characters.

## Composition creates a function for later {#composition}

Forward composition connects two functions without supplying the input yet:

```fsharp
(>>) : ('A -> 'B) -> ('B -> 'C) -> ('A -> 'C)

let composed = first >> second
let result = composed input
// equivalent to: second (first input)
```

The shared script composes all four stages:

<<< @/../examples/scripts/ch13-composition-pipeline-api.fsx#composition{fsharp:line-numbers} [ch13-composition-pipeline-api.fsx]

`prepareLabel` is a function value that can be stored, passed, tested, and applied to many drafts. In contrast, the earlier pipeline computes one label from `rawDraft` immediately.

Backward composition reverses how the functions are written:

```fsharp
second << first
// also means: first runs, then second
```

Thus `toLabel << addChannel "web" << capSeats 4 << trimAttendee` executes from right to left. It mirrors nested-call order and can read naturally when you start from the final operation. Forward `>>` usually matches this book's left-to-right data narration better. Select the direction that makes execution order obvious to the reader.

Composition is sometimes called “point-free” when the input parameter is omitted. That omission is useful only while the resulting function remains recognizable. A named `let prepare draft = ...` pipeline is better when intermediate domain names, annotations, logging, or breakpoints matter.

## Argument order makes partial application useful {#parameter-order}

Consider the shared signatures:

```fsharp
capSeats : int -> BookingDraft -> BookingDraft
addChannel : string -> BookingDraft -> BookingDraft
```

Configuration comes first; the primary flowing value comes last. Partial application turns `capSeats 4` and `addChannel "desk"` into `BookingDraft -> BookingDraft`, exactly the shape a pipeline or composition needs:

<<< @/../examples/scripts/ch13-composition-pipeline-api.fsx#parameter-order{fsharp:line-numbers} [ch13-composition-pipeline-api.fsx]

A common F#-facing function order is:

1. dependencies or policy values that remain fixed across calls;
2. transformation functions or selectors;
3. the collection or domain value being transformed.

FSharp.Core illustrates it: `List.map mapping list`, `List.filter predicate list`, and `Option.defaultValue fallback option`. Supplying the early argument creates a function awaiting the data.

If the draft came first—`capSeatsDataFirst draft maximum`—a pipeline would need `draft |> fun value -> capSeatsDataFirst value 4`. One lambda is not a disaster, but repeated adapter lambdas at every call site are evidence that an F#-facing API may have chosen an inconvenient order.

### Parameter order is not a universal law {#parameter-order-limits}

Keep established conventions and meaning ahead of pipe convenience:

- symmetric operands such as `max left right` have no privileged flowing value;
- constructors often read best as direct required arguments;
- predicates may compare two equally important domain values;
- .NET methods conventionally use parenthesized, tupled parameters for cross-language consumption;
- changing a mature public function's order is a breaking API change.

If two parameters share a primitive type, accidental reversal may still compile. Protected domain types such as `EventId` and `RequestId` reduce that risk more effectively than clever piping.

## A direct call can be the clearest call {#direct-call}

The final shared example leaves a small predicate direct:

<<< @/../examples/scripts/ch13-composition-pipeline-api.fsx#direct-call{fsharp:line-numbers} [ch13-composition-pipeline-api.fsx]

`fitsWithin capacity requested` displays the relation in one place. `requested |> fitsWithin capacity` is also valid, but it does not reveal a longer transformation path and may make a simple comparison feel procedural.

Prefer a direct call when:

- there is one operation rather than a chain;
- several arguments have equal semantic weight;
- the function name and direct arguments form a clear proposition;
- piping would require packing/unpacking a tuple or an otherwise unnecessary lambda;
- a familiar .NET method already has a clear calling convention.

Likewise, `<|` can remove parentheses—`printfn "%s" <| prepareLabel draft`—but ordinary parentheses are often more familiar. Operator knowledge should reduce noise, not create a test for the reader.

## Design from representative call sites {#api-design-workflow}

Before publishing an F# function, write three representative calls:

1. one direct call with all arguments;
2. one partially applied use reused across several values;
3. one pipeline or composition in the intended workflow.

If the signature makes the common call concise and the unusual call merely possible, it is probably well ordered. If every call needs flips, tuple adapters, or anonymous functions, revise the boundary before consumers depend on it.

Do not invent a custom symbolic operator for an operation that has a good domain name. `Booking.confirm code booking` is easier to search, document, and understand than an unexplained operator. The standard `|>`, `>>`, and `<<` already express application order.

## Run the shared example {#run-example}

From the repository root:

```console
dotnet fsi --exec examples/scripts/ch13-composition-pipeline-api.fsx
```

The six deterministic lines show nested application, an equivalent pipeline, forward and backward composition, configured partial application, and a deliberately direct predicate.

## Exercises {#exercises}

### Exercise 1: derive both compositions {#exercise-01}

Given `parse : string -> Draft`, `normalize : Draft -> Draft`, and `label : Draft -> string`, rewrite `label (normalize (parse text))` as:

1. a pipeline that consumes `text` now;
2. a forward-composed function;
3. a backward-composed function.

Write the type of each composed function and state which function runs first.

### Exercise 2: order an F#-facing API {#exercise-02}

Design parameter order for these functions and show one partial application plus one pipeline call for each:

- filter bookings by one fixed `BookingStatus`;
- render many bookings with one fixed culture-specific formatter;
- check whether a requested `SeatCount` fits a `Capacity`.

Identify any function for which a direct call is more readable than the pipeline.

### Exercise 3: remove decorative piping {#exercise-03}

Review this code:

```fsharp
let canAccept capacity request =
    request
    |> Booking.seats
    |> SeatCount.value
    |> fitsWithin (Capacity.value capacity)
```

Give a direct version and a pipeline version with one meaningful intermediate name. Choose one for production and justify the choice from readability and debugging, not from character count.

[Read the chapter solutions](../solutions/ch-13-composition-pipeline-api).

## Model review {#model-review}

- `|>` applies a value to a function now; `>>` and `<<` create a function for later.
- Nested calls, pipelines, and composition can express the same application order.
- Configuration-first and data-last often make curried F# functions easy to reuse.
- Type alignment, not operator syntax, determines whether stages compose.
- Direct calls remain preferable for simple, symmetric, constructor-like, and .NET-shaped operations.
- Representative call sites should drive argument order; custom operators should not hide domain names.

Chapter 14 applies this API reasoning to collections, where the chosen representation also determines evaluation timing, lookup rules, and conversion cost.

## Sources {#sources}

- [Microsoft Learn: Functions, pipelines, and composition](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/functions/)
- [Microsoft Learn: Parameters and arguments](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/parameters-and-arguments)
- [Microsoft Learn: F# formatting guidelines](https://learn.microsoft.com/en-us/dotnet/fsharp/style-guide/formatting)
- [Microsoft Learn: F# component design guidelines](https://learn.microsoft.com/en-us/dotnet/fsharp/style-guide/component-design-guidelines)
