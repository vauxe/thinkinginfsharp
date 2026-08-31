---
title: "Chapter 13: Composition, Argument Order, and Pipeline APIs"
description: "Derive pipelines and function composition from nested calls, then design argument order around real calling forms without forcing every expression through a pipe."
translationKey: part-03/ch-13-composition-pipeline-api
---

# Chapter 13: Composition, Argument Order, and Pipeline APIs {#overview}

Small functions become useful when their outputs fit the next function's inputs. You can write that connection as nested calls, apply one value through a pipeline, or compose a new reusable function. These are not three programming models; they are three views of the same applications.

The form of those calls also influences API design. A curried function whose primary data parameter comes last is easy to partially apply and pipe. But “always pipe” and “data must always be last” are poor substitutes for reading the call site. Some predicates, constructors, symmetric operations, and .NET methods are clearer as direct calls.

## Repeated nesting reveals a data path {#repeated-nesting}

First save this complete starting point as `ch13-composition.fsx`. Later valid blocks continue from these definitions in reading order:

```fsharp:line-numbers
type BookingDraft =
    { Attendee: string
      RequestedSeats: int
      Channel: string option }

let rawDraft =
    { Attendee = "  Lin  "
      RequestedSeats = 6
      Channel = None }

let trimAttendee draft =
    { draft with Attendee = draft.Attendee.Trim() }

let capSeats maximum draft =
    { draft with RequestedSeats = min maximum draft.RequestedSeats }

let addChannel channel draft =
    { draft with Channel = Some channel }

let toLabel draft =
    let channel = draft.Channel |> Option.defaultValue "unknown"
    $"{draft.Attendee}:{draft.RequestedSeats}:{channel}"
```

`rawDraft` supplies observable unnormalized input. The four functions trim whitespace, cap seats, set a channel, and create a label. The example now begins with ordinary application:

```fsharp:line-numbers
let nestedLabel = toLabel (addChannel "web" (capSeats 4 (trimAttendee rawDraft)))

printfn "Nested: %s" nestedLabel
```
This block prints `Nested: Lin:4:web`.

Read from the innermost parentheses outward:

1. trim the attendee;
2. cap the requested seats at four;
3. add the `web` channel;
4. produce a label.

The code is correct, but the reading direction repeatedly reverses: source text begins with `toLabel`, while execution begins with `trimAttendee`. More nested parentheses also make it harder to pause and inspect an intermediate value.

Do not introduce a pipeline merely because one call is nested. The signal is a genuine chain in which each whole result becomes the primary input of the next operation.

## A pipeline applies one value now {#pipeline}

The forward pipe has the essential behavior:

```text
value |> functionValue
// means
functionValue value
```

Rewriting the shared chain gives:

```fsharp:line-numbers
let pipedLabel =
    rawDraft |> trimAttendee |> capSeats 4 |> addChannel "web" |> toLabel

printfn "Pipeline matches nested: %b" (pipedLabel = nestedLabel)
```
This continuation prints `Pipeline matches nested: true`.

Now source order follows data flow. Each line transforms the value from the preceding line, and the final result is computed immediately.

The type between stages must fit. If `trimAttendee : BookingDraft -> BookingDraft` and `toLabel : BookingDraft -> string`, then `trimAttendee` can precede `toLabel`. A function requiring some unrelated input cannot be inserted merely by adding `|>`.

Pipelines are ordinary application, not an effect system or automatic error propagation. Piping a `Result` into `Result.bind next` works because that function's final parameter is a result; `|>` itself knows nothing about `Ok` or `Error`.

### A multi-line pipeline should expose its stages {#pipeline-formatting}

For more than a very short expression, begin with the value and put one `|>` stage on each line, as the example does. Name a stage when its lambda becomes long or you need to inspect it in a debugger. A pipeline is readable because its transformations are visible, not because it minimizes characters.

## Composition creates a function for later {#composition}

Forward composition connects two functions without supplying the input yet:

```text
(>>) : ('A -> 'B) -> ('B -> 'C) -> ('A -> 'C)

let composed = first >> second
let result = composed input
// equivalent to: second (first input)
```

This is the composition type and equivalence, not a standalone script. `first`, `second`, and `input` are placeholders.

The example composes all four stages:

```fsharp:line-numbers
let prepareLabel = trimAttendee >> capSeats 4 >> addChannel "web" >> toLabel

let prepareLabelBackward = toLabel << addChannel "web" << capSeats 4 << trimAttendee

printfn "Forward composition: %s" (prepareLabel rawDraft)
printfn "Backward composition: %s" (prepareLabelBackward rawDraft)
```
This continuation prints:

```text
Forward composition: Lin:4:web
Backward composition: Lin:4:web
```

`prepareLabel` is a function value that can be stored, passed, tested, and applied to many drafts. In contrast, the earlier pipeline computes one label from `rawDraft` immediately.

Backward composition reverses how the functions are written:

```text
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

These are the signatures FSI displays.

Configuration comes first; the primary flowing value comes last. Partial application turns `capSeats 4` and `addChannel "desk"` into `BookingDraft -> BookingDraft`, exactly the function type a pipeline or composition needs:

```fsharp:line-numbers
let deskLabel =
    { Attendee = "  Mira "
      RequestedSeats = 2
      Channel = None }
    |> trimAttendee
    |> capSeats 4
    |> addChannel "desk"
    |> toLabel

printfn "Configured pipeline: %s" deskLabel
```
This block depends only on the starting types and functions. It prints `Configured pipeline: Mira:2:desk`.

A common F#-facing function order is:

1. dependencies or policy values that remain fixed across calls;
2. transformation functions or selectors;
3. the collection or domain value being transformed.

FSharp.Core illustrates it: `List.map mapping list`, `List.filter predicate list`, and `Option.defaultValue fallback option`. Supplying the early argument creates a function awaiting the data.

If the draft came first—`capSeatsDataFirst draft maximum`—a pipeline would need `draft |> fun value -> capSeatsDataFirst value 4`. One lambda is harmless. Repeated adapter lambdas at every call site, however, suggest an inconvenient argument order for F# callers.

### Parameter order is not a universal law {#parameter-order-limits}

Keep established conventions and meaning ahead of pipe convenience:

- symmetric operands such as `max left right` have no privileged flowing value;
- constructors often read best as direct required arguments;
- predicates may compare two equally important domain values;
- .NET methods conventionally use parenthesized, tupled parameters for cross-language consumption;
- changing a mature public function's order is a breaking API change.

If two parameters share a primitive type, accidental reversal may still compile. Protected domain types such as `EventId` and `RequestId` reduce that risk more effectively than clever piping.

## A direct call can be the clearest call {#direct-call}

The final example leaves a small predicate direct:

```fsharp:line-numbers
let fitsWithin capacity requested = requested <= capacity

let requested = 3
let capacity = 4
let fits = fitsWithin capacity requested

printfn "Direct predicate: requested=%d capacity=%d fits=%b" requested capacity fits
```
This block runs by itself and prints `Direct predicate: requested=3 capacity=4 fits=true`.

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

If the signature makes the common call concise and the unusual call merely possible, it is probably well ordered. If every call needs flips, tuple adapters, or anonymous functions, revise the API before consumers depend on it.

Do not invent a custom symbolic operator for an operation that has a good domain name. `Booking.confirm code booking` is easier to search, document, and understand than an unexplained operator. The standard `|>`, `>>`, and `<<` already express application order.

## Exercises {#exercises}

### Exercise 1: derive both compositions {#exercise-01}

Given `parse : string -> Draft`, `normalize : Draft -> Draft`, and `label : Draft -> string`, rewrite `label (normalize (parse text))` as:

1. a pipeline that consumes `text` now;
2. a forward-composed function;
3. a backward-composed function.

Write the type of each composed function and state which function runs first.


::: details Answer

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

Expanding either application shows the nesting:

```fsharp
// forward text expands to:
label (normalize (parse text))

// backward text expands to the same expression:
label (normalize (parse text))
```

In executable code, compare the resulting values with `forward text = backward text`.

:::

### Exercise 2: order an F#-facing API {#exercise-02}

Design parameter order for these functions and show one partial application plus one pipeline call for each:

- filter bookings by one fixed `BookingStatus`;
- render many bookings with one fixed culture-specific formatter;
- check whether a requested `SeatCount` fits a `Capacity`.

Identify any function for which a direct call is more readable than the pipeline.


::: details Answer

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

:::

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


::: details Answer

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

I would choose the first version here. The extraction is short, both quantities appear next to the final relation, and a debugger can inspect each named value. The second version is correct and may fit a surrounding pipeline, but its final pipe adds no transformation stage; it only reorders a binary predicate's arguments.

If `fitsWithin` instead accepts protected `Capacity` and `SeatCount` directly, the best implementation is smaller still:

```fsharp
let canAccept capacity request =
    fitsWithin capacity (Booking.seats request)
```

Keeping measured unwrapping inside the domain predicate also reduces repeated representation knowledge at call sites.

:::


Chapter 14 applies this API reasoning to collections, where the chosen representation also determines evaluation timing, lookup rules, and conversion cost.

## Sources {#sources}

- [Microsoft Learn: Functions, pipelines, and composition](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/functions/)
- [Microsoft Learn: Parameters and arguments](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/parameters-and-arguments)
- [Microsoft Learn: F# formatting guidelines](https://learn.microsoft.com/en-us/dotnet/fsharp/style-guide/formatting)
- [Microsoft Learn: F# component design guidelines](https://learn.microsoft.com/en-us/dotnet/fsharp/style-guide/component-design-guidelines)
