---
title: "Chapter 5: Lists, Pipelines, and Data Flow"
description: "Express list transformations with map, filter, choose, and pipelines, then compare them with for, while, and local mutable bindings."
translationKey: part-01/ch-05-lists-pipelines
---

# Chapter 5: Lists, Pipelines, and Data Flow {#overview}

Before chaining collection operations, write the input and output type of each stage. The F# `List` module provides common stages as higher-order functions: `filter` selects elements, `map` transforms them, and `choose` selects and transforms together. The pipeline operator `|>` places those stages in data-flow order.

F# supports both immutable transformations and the imperative tools `for`, `while`, and `let mutable`. We will compare three implementations of one problem, see where each style communicates intent most directly, and identify what to measure before optimizing.

The lists here are small and already in memory. Arrays, lazy `seq`, `Map`, `Set`, and repeated enumeration wait until Chapter 14. Recursion and `fold` arrive in the next chapter.

## A list is immutable and preserves earlier versions {#list-foundations}

The example's input is a list of pairs:

```text
(string * int) list
```

Each element contains a guest name and requested seat count, and every element has the same element type. A list literal uses square brackets. Items on one line use semicolons; semicolons can be omitted when indentation separates items across lines.

Save the following complete starting point as `ch05-lists-pipelines.fsx`. Later code blocks in this chapter continue from these definitions in reading order:

```fsharp:line-numbers
let requests =
    [ ("Lin", 3)
      ("Ada", 0)
      ("Sam", 2)
      ("Mina", -1) ]

let isValidRequest (_, seats) = seats > 0

let formatRequest (guest, seats) =
    $"{guest}:{seats}"
```

`requests` is the data to process, `isValidRequest` decides whether a seat count is valid, and `formatRequest` turns one request into a display label. Naming those roles first makes the later pipeline traceable.

An F# list is an immutable singly linked structure. `item :: list` creates a new front node whose tail shares the old list and is normally constant time. `left @ right`, or `List.append left right`, must rebuild the left spine, so its cost is proportional to the length of `left`. Repeated tail append inside a loop can therefore become quadratic; accumulating at the front and calling `List.rev` once is a common alternative.

The list nodes themselves are immutable, but their elements may not be. If two lists contain the same mutable object, both can observe changes inside that object. Chapter 14 separates collection behavior from element behavior.

## Three core transformations {#core-transformations}

First, inspect only the function signatures:

```text
List.map    : ('a -> 'b)        -> 'a list -> 'b list
List.filter : ('a -> bool)      -> 'a list -> 'a list
List.choose : ('a -> 'b option) -> 'a list -> 'b list
```

Each accepts a function value before a list, so each works well with partial application and pipelines. All preserve input order, but their responsibilities differ.

### `map`: exactly one output per item {#map}

`List.map transform source` calls `transform` on every input and collects results in original order. Output length equals input length, while element type may change from `'a` to `'b`.

For example, `(string * int) -> string` function `formatRequest` turns every booking pair into a label. Partial application makes `List.map formatRequest` a function `(string * int) list -> string list`. Selection belongs to `filter` or `choose`; `map` preserves one output position for every input.

### `filter`: decide whether to retain the original item {#filter}

`List.filter predicate source` requires `predicate` to return `bool`. The result contains only original elements for which it returned `true`, still in relative order, and its type remains `'a list`.

`filter` fits when selection and later transformation are two clear conceptual stages. It returns a new result list while the source remains available, and retained element values can be referenced by both lists. Treat internal spine reuse as an implementation detail.

### `choose`: zero or one output per item {#choose}

`List.choose chooser source` asks each input to produce `Some value` or `None`. A `Some value` contributes the transformed value, while `None` contributes no result item, expressing selection and transformation together.

For now, read `Some x` as “a value is present” and `None` as “no value.” Chapter 9 covers absence modeling, composition, and the `Some null` case. Use `Result` later when the caller needs an error reason.

When filtering and mapping share one decision and the intermediate list has no independent meaning, `choose` can express “each input yields at most one output” more directly. When each stage has a domain name or must be observed separately, keeping `filter` and `map` can be clearer.

## A pipeline passes data as the final argument {#pipelines}

The pipe operator has a small core equivalence:

```text
value |> functionValue

equivalent to

functionValue value
```

For example:

```text
requests |> List.filter isValidRequest

equivalent to

List.filter isValidRequest requests
```

The parameter order from Chapter 3 puts the list last. The predicate can therefore be partially applied before the pipeline supplies the data.

### Read one stage at a time {#pipeline-stages}

The shared pipeline is:

```fsharp:line-numbers
let pipelineLabels =
    requests |> List.filter isValidRequest |> List.map formatRequest

printfn "Pipeline labels: %A" pipelineLabels
```
This block continues from the three definitions at the start of the chapter and prints:

```text
Pipeline labels: ["Lin:3"; "Sam:2"]
```

Record types and values from top to bottom:

| Stage | Type | Value summary |
| --- | --- | --- |
| `requests` | `(string * int) list` | Four requests |
| After `List.filter isValidRequest` | `(string * int) list` | Lin and Sam |
| After `List.map formatRequest` | `string list` | `"Lin:3"` and `"Sam:2"` |

The pipeline supplies the preceding stage's value as the following function's final argument. Its control flow remains ordinary function application. Aligned lines make transformation order scannable and reduce nested call parentheses.

### List pipelines evaluate eagerly {#eager-pipelines}

Each `List` operation here traverses its input and completes a result list when called. `filter` first produces an intermediate list, and `map` traverses it next. The pipe operator preserves those two eager calls; fusion and lazy evaluation require different operations or data structures.

A two-stage pipeline can be the right choice when the intermediate result improves clarity or the data is small. Write clearly and measure a real bottleneck first. When one traversal matters and the logic naturally belongs together, use `choose` or a `fold` from Chapter 6.

### A pipeline can also reduce clarity {#pipeline-boundaries}

A direct call such as `List.isEmpty values` often communicates a single operation more clearly. Pipelines work best with final-argument flow, short stages, and visible effects.

Extract complex stages into named functions and keep each output type easy to state. Use pipelines as notation for clear data flow, and use ordinary application where it reads more directly.

## Use `choose` to merge related stages {#choose-pipeline}

The example combines validity and formatting into one chooser:

```fsharp:line-numbers
let tryFormatRequest request =
    if isValidRequest request then
        Some(formatRequest request)
    else
        None

let chosenLabels = requests |> List.choose tryFormatRequest

printfn "Chosen labels: %A" chosenLabels
```
This block continues to use `requests`, `isValidRequest`, and `formatRequest` from the starting point. It prints:

```text
Chosen labels: ["Lin:3"; "Sam:2"]
```

`tryFormatRequest` has type `(string * int) -> string option`. A valid request produces `Some label`, an invalid one produces `None`, and `List.choose` extracts the text inside each `Some` in order, again yielding `string list`.

A `try` prefix often signals in F#/.NET code that an operation may return an alternative to its normal value, while the type defines that alternative precisely. Here, `option` distinguishes presence from absence. Validation that must explain failure uses `Result` or accumulating validation later in the book.

## Use `iter` or `for` for effects {#iteration-for-effects}

Transformation functions answer “what is the new data?” When the goal is to perform an effect such as output for each item without collecting results, `List.iter action` or `for item in source do ...` is a better fit. Their action or loop body returns `unit`, and the whole iteration returns `unit` too.

The following block continues from `pipelineLabels` and uses `for` to demonstrate label order:

```fsharp:line-numbers
printf "Iteration order:"

for label in pipelineLabels do
    printf " %s" label

printfn ""
```
It prints:

```text
Iteration order: Lin:3 Sam:2
```

A `for` loop enumerates its input and may use a pattern in the loop-variable position. It suits logging, writing to an existing buffer, or calling an imperative API. If the goal is a new list, a loop must manage accumulation separately, while `map` and `filter` already encode that intent in their return type.

Use `map` to produce data. Use `iter` or `for` to perform an effect for every item; their `unit` result makes that intent explicit and avoids an unused result list.

## A mutable binding is an explicit tool {#mutable-bindings}

`let mutable name = initial` creates a mutable storage location, and `name <- next` updates it. `=` remains binding or equality syntax; it does not perform updates.

Mutable state adds a timeline: to know the value of `name`, you must know which earlier paths executed `<-`. Keep that state inside one small function and do not expose a reference to it; the two imperative implementations here follow this rule.

### The `for` version: the language manages enumeration {#for-version}

This version continues from the earlier `tryFormatRequest` definition:

```fsharp:line-numbers
let labelsWithFor source =
    let mutable reversedLabels = []

    for request in source do
        match tryFormatRequest request with
        | Some label -> reversedLabels <- label :: reversedLabels
        | None -> ()

    List.rev reversedLabels
```
The loop calls `tryFormatRequest` in input order. A valid label is added to the front of `reversedLabels` with constant-time `::`, temporarily reversing order. One `List.rev` after the loop restores the original order.

Both `match` branches in the `for` body return `unit`: update `<-` produces `()`, and the `None` branch explicitly returns `()`. The function's final expression, `List.rev reversedLabels`, produces the `string list` result.

### The `while` version: code manages condition and progress {#while-version}

This version also continues from `tryFormatRequest`, but it explicitly tracks the unprocessed list:

```fsharp:line-numbers
let labelsWithWhile source =
    let mutable remaining = source
    let mutable reversedLabels = []

    while not (List.isEmpty remaining) do
        match remaining with
        | request :: tail ->
            remaining <- tail

            match tryFormatRequest request with
            | Some label -> reversedLabels <- label :: reversedLabels
            | None -> ()
        | [] -> ()

    List.rev reversedLabels
```
A `while` repeats its `unit` body while the condition is `true`. This code must maintain `remaining` manually and update it to `tail` on each nonempty match. Forgetting that update causes an infinite loop. Even though the loop condition checks for a nonempty list, `remaining` still has a list type, so the inner match must handle the empty case.

The version works and mutates only two local bindings, but it exposes more mechanical state than `for` or `choose`. A `while` is more appropriate when whether to continue genuinely depends on changing state and no existing collection traversal expresses the problem, such as some low-level API interactions.

## How to choose among the three {#choosing-style}

First run this continuation, which uses structural equality to confirm that all four forms produce the same labels in the same order:

```fsharp:line-numbers
let forLabels = labelsWithFor requests
let whileLabels = labelsWithWhile requests

let sameLabels =
    pipelineLabels = chosenLabels
    && pipelineLabels = forLabels
    && pipelineLabels = whileLabels

printfn "All implementations agree: %b" sameLabels
```

It prints `All implementations agree: true`. After confirming equivalent behavior, choose according to the required result and measured cost:

| Goal | Usually consider first | Reason |
| --- | --- | --- |
| Produce a new collection from a collection | `map`, `filter`, `choose`, later `fold` | The result type directly expresses transformation |
| Perform an effect for every item | `List.iter` or `for` | The `unit` intent is explicit and no unused result is built |
| Continue according to explicit changing state | Small `while` plus local `mutable` | A state machine may be clearer than a distorted transformation |
| Reduce traversals or allocations on a hot path | Measure, then merge stages or choose another collection | A clear baseline and measurements beat guesses |

Either style can carry avoidable costs: a functional version may allocate too many intermediate lists, while an imperative version may become quadratic through mistaken tail appends. First establish the same results, ordering, and externally visible behavior; then benchmark or profile the real costs.

## Exercises {#exercises}

Write stage types and intermediate values before running each exercise. An equal final list is not enough; compare source data, ordering, and effects too.

### Exercise 1: trace a pipeline stage by stage {#exercise-01}

For `pipelineLabels` in “Read one stage at a time”:

1. write the types of `requests`, the filtered list, and the mapped list;
2. write the exact element order in both intermediate lists;
3. expand both uses of `|>` into an equivalent call without pipelines;
4. state whether the source changes and how many list stages the pipeline traverses.


::: details Answer

The shared pipeline is:

```fsharp:line-numbers
let pipelineLabels =
    requests |> List.filter isValidRequest |> List.map formatRequest

printfn "Pipeline labels: %A" pipelineLabels
```
Both `requests` and the filtered result have type `(string * int) list`. The former contains Lin 3, Ada 0, Sam 2, and Mina -1 in order; the latter retains only Lin 3 and Sam 2. The mapped result has type `string list` and order `[ "Lin:3"; "Sam:2" ]`.

Without pipelines, first evaluate `List.filter isValidRequest requests`, then supply that result as the final argument to `List.map formatRequest`. Nested, it is `List.map formatRequest (List.filter isValidRequest requests)`. The source list does not change.

This runs two eager list stages: filtering traverses four elements and produces an intermediate list, then mapping traverses two elements and produces the final list. The number of calls and the number of visited elements are not identical concepts, but there are two list operations.

:::

### Exercise 2: merge selection and transformation with `choose` {#exercise-02}

State what `tryFormatRequest` returns for each of the four requests and write its full type. Then explain how `List.choose` obtains the same result as `filter` followed by `map`.

Compare the forms: when is retaining a separate filtered result clearer? When is `choose` more exact? What information does `None` discard in this example?


::: details Answer

Using the shared definitions from the start of the chapter, the core code is:

```fsharp:line-numbers
let tryFormatRequest request =
    if isValidRequest request then
        Some(formatRequest request)
    else
        None

let chosenLabels = requests |> List.choose tryFormatRequest

printfn "Chosen labels: %A" chosenLabels
```
`tryFormatRequest` has full type `(string * int) -> string option`. In order it produces `Some "Lin:3"`, `None`, `Some "Sam:2"`, and `None`. `List.choose` extracts only the values inside the two `Some` cases while preserving order, producing the same `string list` as filter then map.

If the valid-request list must be logged, tested, or passed to another step independently, separate `filter` and `map` stages are clearer. If an output can only be constructed for a valid item and the intermediate list has no domain meaning, `choose` is more exact. Here `None` discards why a request was invalid and the original request itself; use an error-carrying model when consumers need that reason.

:::

### Exercise 3: compare loop state {#exercise-03}

For `labelsWithFor` and `labelsWithWhile`:

1. write `reversedLabels` after processing Lin, Ada, Sam, and Mina;
2. explain why the final `List.rev` is required;
3. identify the state the `while` must advance on every iteration and what happens if it does not;
4. choose a preferred form for “print every label” and for “produce a new label list,” explaining each choice.


::: details Answer

The `for` and `while` versions are:

```fsharp:line-numbers
let labelsWithFor source =
    let mutable reversedLabels = []

    for request in source do
        match tryFormatRequest request with
        | Some label -> reversedLabels <- label :: reversedLabels
        | None -> ()

    List.rev reversedLabels
```
```fsharp:line-numbers
let labelsWithWhile source =
    let mutable remaining = source
    let mutable reversedLabels = []

    while not (List.isEmpty remaining) do
        match remaining with
        | request :: tail ->
            remaining <- tail

            match tryFormatRequest request with
            | Some label -> reversedLabels <- label :: reversedLabels
            | None -> ()
        | [] -> ()

    List.rev reversedLabels
```
Both versions change `reversedLabels` in the same way. After Lin it is `[ "Lin:3" ]`. Ada produces `None`, so it stays unchanged. Prepending Sam produces `[ "Sam:2"; "Lin:3" ]`. Mina produces `None`, so it remains unchanged. `List.rev` restores relative input order; without it, valid items would be reversed.

The `while` version must also move `remaining` from the full list through each successive `tail` until it reaches `[]`. Forgetting the update on any nonempty path leaves the condition true and repeats the same element forever.

For “print every label,” prefer `for` or `List.iter` because the goal is a `unit` effect. For “produce a new label list,” prefer `choose` because its result type expresses the output. If profiling later proves a hot path needs a custom one-pass implementation, compare a local mutable loop rather than assuming first.

:::


The next chapter generalizes “accumulate at the front and reverse” into recursion and accumulators, then rewrites a class of explicit recursion with `fold` while describing tail-call boundaries accurately.

## Sources {#sources}

- [Microsoft Learn: Lists and `map`/`filter`/`choose`](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/lists)
- [FSharp.Core: List module reference](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-collections-listmodule.html)
- [Microsoft Learn: Functions and pipelines](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/functions/)
- [Microsoft Learn: Values and mutable bindings](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/values/)
- [Microsoft Learn: `for...in`](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/loops-for-in-expression)
- [Microsoft Learn: `while...do`](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/loops-while-do-expression)
