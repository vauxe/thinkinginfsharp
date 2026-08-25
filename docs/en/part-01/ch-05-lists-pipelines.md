---
title: "Chapter 5: Lists, Pipelines, and Data Flow"
description: "Express list transformations with map, filter, choose, and pipelines, then compare them with for, while, and local mutable bindings."
translationKey: part-01/ch-05-lists-pipelines
---

# Chapter 5: Lists, Pipelines, and Data Flow {#overview}

When processing a group of values, the best first question is not “what should the loop variable be?” but “what shape does each stage transform into what other shape?” The F# List module expresses frequent stages as higher-order functions: `filter` selects elements, `map` transforms them, and `choose` selects and transforms together. The pipeline `|>` then places stages in data-flow order.

F# also supports `for`, `while`, and `let mutable`. It is not a pure functional language, and this chapter does not treat loops as failure. We will compare three implementations of one problem, seeing when immutable transformations are clearer, when imperative iteration is more direct, and which traversal and allocation costs must be counted honestly before optimization.

## What you will be able to do {#outcomes}

By the end of this chapter, you should be able to:

- construct lists and understand the basic costs of `[]`, `::`, and `@`;
- read the type signatures of `List.map`, `List.filter`, and `List.choose`;
- reduce `x |> f` to `f x` instead of treating a pipeline as magic;
- trace a pipeline's input, output, order, and eager evaluation stage by stage;
- understand the minimal `Some`/`None` selection protocol used by `choose`;
- write `for` and `while` loops whose bodies return `unit`;
- compare a local mutable implementation with immutable list transformations by clarity and cost.

The lists here are small and already in memory. Arrays, lazy `seq`, `Map`, `Set`, and repeated enumeration wait until Chapter 14. Recursion and `fold` arrive in the next chapter.

## A list is a persistent immutable structure {#list-foundations}

The shared script's input is a list of pairs:

```text
(string * int) list
```

Each element contains a guest name and requested seat count, and every element has the same element type. A list literal uses square brackets. Items on one line use semicolons; semicolons can be omitted when indentation separates items across lines.

An F# list is an immutable singly linked structure. `item :: list` creates a new front node whose tail shares the old list and is normally constant time. `left @ right`, or `List.append left right`, must rebuild the left spine, so its cost is proportional to the length of `left`. Repeated tail append inside a loop can therefore become quadratic; accumulating at the front and calling `List.rev` once is a common alternative.

Immutability still describes the structure boundary. List nodes are not changed in place, but if elements are objects with mutable internals, lists sharing those objects can still observe object changes. Chapter 14 separates collection choice from element semantics.

## Three core transformations {#core-transformations}

First, inspect only the type shapes:

```text
List.map    : ('a -> 'b)        -> 'a list -> 'b list
List.filter : ('a -> bool)      -> 'a list -> 'a list
List.choose : ('a -> 'b option) -> 'a list -> 'b list
```

Each accepts a function value before a list, so each works well with partial application and pipelines. All preserve input order, but their responsibilities differ.

### `map`: exactly one output per item {#map}

`List.map transform source` calls `transform` on every input and collects results in original order. Output length equals input length, while element type may change from `'a` to `'b`.

For example, `(string * int) -> string` function `formatRequest` turns every booking pair into a label. Partial application makes `List.map formatRequest` a function `(string * int) list -> string list`. `map` itself has no meaning for skipping an item.

### `filter`: decide whether to retain the original item {#filter}

`List.filter predicate source` requires `predicate` to return `bool`. The result contains only original elements for which it returned `true`, still in relative order, and its type remains `'a list`.

`filter` fits when selection and later transformation are two clear conceptual stages. It returns a result list without removing elements from the source, and retained element values can be referenced by both lists. Do not depend on whether any internal spine is reused; that is an implementation detail.

### `choose`: zero or one output per item {#choose}

`List.choose chooser source` asks each input to produce `Some value` or `None`. A `Some value` contributes the transformed value, while `None` contributes no result item, expressing selection and transformation together.

For now, treat `option` as a minimal protocol: `Some x` means a value is present, and `None` means no value. Chapter 9 covers absence modeling, composition, and the `Some null` boundary. Do not treat `None` as an ordinary empty string or as error information.

When filtering and mapping share one decision and the intermediate list has no independent meaning, `choose` can express “each input yields at most one output” more directly. When each stage has a domain name or must be observed separately, keeping `filter` and `map` can be clearer.

## A pipeline puts the final argument back into data flow {#pipelines}

The pipe operator has a small core equivalence:

```text
value |> functionValue

equivalent to / 等价于

functionValue value
```

Therefore, `requests |> List.filter isValidRequest` is `List.filter isValidRequest requests`. The parameter order from Chapter 3 puts the list last, allowing the predicate to be partially applied before the pipeline supplies data.

### Read one stage at a time {#pipeline-stages}

The shared pipeline is:

```fsharp:line-numbers [ch05-lists-pipelines.fsx]
let pipelineLabels =
    requests |> List.filter isValidRequest |> List.map formatRequest

printfn "Pipeline labels: %A" pipelineLabels
```
Record types and values from top to bottom:

| Stage | Type | Value summary |
| --- | --- | --- |
| `requests` | `(string * int) list` | Four requests |
| After `List.filter isValidRequest` | `(string * int) list` | Lin and Sam |
| After `List.map formatRequest` | `string list` | `"Lin:3"` and `"Sam:2"` |

The pipeline adds no control flow. It supplies the preceding stage's value as the following function's final argument. Aligned lines make transformation order scannable and avoid nested call parentheses.

### List pipelines evaluate eagerly {#eager-pipelines}

Each `List` operation here traverses its input and completes a result list when called. `filter` first produces an intermediate list, and `map` traverses it next. The pipe operator neither fuses the calls automatically nor turns a list into a lazy sequence.

That does not make a two-stage pipeline inherently “slow.” The intermediate result may improve clarity and data may be small. Write clearly and measure a real bottleneck first. When one traversal matters and the logic naturally belongs together, use `choose` or a `fold` from Chapter 6.

### A pipeline can also reduce clarity {#pipeline-boundaries}

A direct call such as `List.isEmpty values` may be clearer than a one-stage pipeline written only to use the operator. A pipeline can also hide information when parameters are not designed for final-argument flow, stages contain many effects, or lambdas become long.

Extract complex stages into named functions and keep each output type easy to state. A pipeline is notation for data flow, not a style rule requiring every expression to be vertical.

## Use `choose` to merge related stages {#choose-pipeline}

The shared script combines validity and formatting into one chooser:

```fsharp:line-numbers [ch05-lists-pipelines.fsx]
let tryFormatRequest request =
    if isValidRequest request then
        Some(formatRequest request)
    else
        None

let chosenLabels = requests |> List.choose tryFormatRequest

printfn "Chosen labels: %A" chosenLabels
```
`tryFormatRequest` has type `(string * int) -> string option`. A valid request produces `Some label`, an invalid one produces `None`, and `List.choose` extracts the text inside each `Some` in order, again yielding `string list`.

A `try` prefix often signals in F#/.NET code that an operation may not produce a normal value, but the exact failure representation still comes from the type. This type distinguishes only presence from absence and carries no reason. Validation that must explain failure should not hide that reason in `None`; later it should use `Result` or accumulating validation.

## Use `iter` or `for` for effects {#iteration-for-effects}

Transformation functions answer “what is the new data?” When the goal is to perform an effect such as output for each item without collecting results, `List.iter action` or `for item in source do ...` is a better fit. Their action or loop body returns `unit`, and the whole iteration returns `unit` too.

The shared example uses `for` to demonstrate label order:

```fsharp:line-numbers [ch05-lists-pipelines.fsx]
printf "Iteration order:"

for label in pipelineLabels do
    printf " %s" label

printfn ""
```
A `for` loop enumerates its input and may use a pattern in the loop-variable position. It suits logging, writing to an existing buffer, or calling an imperative API. If the goal is a new list, a loop must manage accumulation separately, while `map` and `filter` already encode that intent in their return type.

Do not put effects in `map` merely because it visits every item. That also creates a result list that may be ignored and mixes “produce data” with “perform an effect.” `iter` or `for` makes the `unit` intent explicit.

## A mutable binding is an explicit tool {#mutable-bindings}

`let mutable name = initial` creates a mutable storage location, and `name <- next` updates it. `=` remains binding or equality syntax; it does not perform updates.

Mutable state adds time order: understanding `name` at one line requires knowing which earlier paths executed `<-`. Keeping state inside one small function and not exposing a reference to it can contain that reasoning cost. Both imperative implementations in the shared script follow that boundary.

### The `for` version: the language manages enumeration {#for-version}

```fsharp:line-numbers [ch05-lists-pipelines.fsx]
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

```fsharp:line-numbers [ch05-lists-pipelines.fsx]
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
A `while` repeats its `unit` body while the condition is `true`. This code must maintain `remaining` manually and update it to `tail` on each nonempty match. Forgetting that update causes an infinite loop. The empty-list rule remains because the compiler does not erase a possible type shape based on the outer loop condition.

The version works and mutates only two local bindings, but it exposes more mechanical state than `for` or `choose`. A `while` is more appropriate when whether to continue genuinely depends on changing state and no existing collection traversal expresses the problem, such as some low-level API interactions.

## How to choose among the three {#choosing-style}

The shared script uses structural equality to show that all three implementations produce the same labels in the same order. The choice should not follow “functional is always good” or “loops are always fast,” but the problem:

| Goal | Usually consider first | Reason |
| --- | --- | --- |
| Produce a new collection from a collection | `map`, `filter`, `choose`, later `fold` | The result type directly expresses transformation |
| Perform an effect for every item | `List.iter` or `for` | The `unit` intent is explicit and no unused result is built |
| Continue according to explicit changing state | Small `while` plus local `mutable` | A state machine may be clearer than a distorted transformation |
| Reduce traversals or allocations on a hot path | Measure, then merge stages or choose another collection | Evidence and a clear baseline beat guesses |

A functional version may allocate too many intermediate lists. An imperative version may become quadratic through mistaken tail appends. A syntax paradigm is not a performance proof. Establish equal results, order, and boundaries, then benchmark or profile real costs.

## Run the shared example {#run-example}

From the directory containing the example, run:

```console
dotnet fsi --exec ch05-lists-pipelines.fsx
```

You should see:

```text
Pipeline labels: ["Lin:3"; "Sam:2"]
Chosen labels: ["Lin:3"; "Sam:2"]
For/while agree: true
Iteration order: Lin:3 Sam:2
```

Compare all four lines in order, including equality across three implementations and effect iteration order. Source `requests` never changes; every result is a new list value.

## Debugging: pause at every pipeline boundary {#debugging}

Do not read a failing pipeline as one chain:

1. write the current stage's input type;
2. inspect the remaining parameter type after the right-side function is partially applied;
3. confirm that the piped value fits that final parameter exactly;
4. temporarily bind an intermediate result in FSI;
5. check whether the next stage expects a value, a list, or an `option`.

For incorrect output order, look for `::` prepending and a missing `List.rev`. For a loop that never ends, make sure every path advances state related to the condition. For a wrong result length, count `true` results from `filter` or `Some` results from `choose` separately.

When an effect happens twice, check whether validation calls an effectful mapping function more than once. Lists in this chapter are eager, so each explicit call traverses again. Lazy sequences in Chapter 14 introduce a different repeated-enumeration risk.

## Exercises {#exercises}

Write stage types and intermediate values before running each exercise. An equal final list is not the only evidence; compare source data, order, and effects too.

### Exercise 1: trace a pipeline stage by stage {#exercise-01}

For `filter-map-pipeline`:

1. write the types of `requests`, the filtered list, and the mapped list;
2. write the exact element order in both intermediate lists;
3. expand both uses of `|>` into an equivalent call without pipelines;
4. state whether the source changes and how many list stages the pipeline traverses.

### Exercise 2: merge selection and transformation with `choose` {#exercise-02}

State what `tryFormatRequest` returns for each of the four requests and write its full type. Then explain how `List.choose` obtains the same result as `filter` followed by `map`.

Compare the forms: when is retaining a separate filtered result clearer? When is `choose` more exact? What information does `None` discard in this example?

### Exercise 3: compare loop state {#exercise-03}

For `labelsWithFor` and `labelsWithWhile`:

1. write `reversedLabels` after processing Lin, Ada, Sam, and Mina;
2. explain why the final `List.rev` is required;
3. identify the state the `while` must advance on every iteration and what happens if it does not;
4. choose a preferred form for “print every label” and for “produce a new label list,” explaining each choice.

[Read the chapter solutions](../solutions/ch-05-lists-pipelines).

## Summary {#summary}

- An F# list is an ordered immutable singly linked structure. Front cons with `::` is normally constant time; append traverses the left side.
- `map` yields one item per input, `filter` retains original items, and `choose` uses `Some`/`None` for zero-or-one output.
- `x |> f` is data-flow notation for `f x`, relying on parameter order suited to partial application.
- `List` pipelines evaluate eagerly and multiple stages may create intermediate lists; a pipeline is not automatically lazy or fused.
- Use `iter` or `for` for effects instead of using `map` to create an ignored list.
- `let mutable` and `<-` explicitly denote changing storage; tight local containment controls reasoning cost.
- A `while` loop requires manual progress and fits truly state-driven problems rather than default collection traversal.

The next chapter generalizes “accumulate at the front and reverse” into recursion and accumulators, then rewrites a class of explicit recursion with `fold` while describing tail-call boundaries accurately.

## Vocabulary {#vocabulary}

- **list:** an ordered immutable singly linked collection of elements of one type.
- **pipeline:** using `|>` to supply a value as a function's final argument.
- **eager evaluation:** completing computation when an operation is called rather than delaying until enumeration.
- **option:** a type whose `Some value` means presence and whose `None` means absence.
- **effect:** observable behavior such as output or state modification not described by the return value alone.
- **mutable binding:** storage introduced with `let mutable` and updated with `<-`.

## Sources {#sources}

- [Microsoft Learn: Lists and `map`/`filter`/`choose`](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/lists)
- [FSharp.Core: List module reference](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-collections-listmodule.html)
- [Microsoft Learn: Functions and pipelines](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/functions/)
- [Microsoft Learn: Values and mutable bindings](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/values/)
- [Microsoft Learn: `for...in`](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/loops-for-in-expression)
- [Microsoft Learn: `while...do`](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/loops-while-do-expression)
