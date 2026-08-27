---
title: "Chapter 5 Solutions"
description: "Reasoning about list transformations, pipelines, choose, for, while, and local mutable state."
translationKey: solutions/ch-05-lists-pipelines
---

# Chapter 5 Solutions {#overview}

Check every stage's type and order first. Equal final output can still hide excess traversals, escaping mutable state, or repeated side effects.

[Return to Chapter 5](../part-01/ch-05-lists-pipelines).

## Exercise 1: trace a pipeline stage by stage {#exercise-01}

The shared pipeline is:

```fsharp:line-numbers
let pipelineLabels =
    requests |> List.filter isValidRequest |> List.map formatRequest

printfn "Pipeline labels: %A" pipelineLabels
```
Both `requests` and the filtered result have type `(string * int) list`. The former contains Lin 3, Ada 0, Sam 2, and Mina -1 in order; the latter retains only Lin 3 and Sam 2. The mapped result has type `string list` and order `[ "Lin:3"; "Sam:2" ]`.

Without pipelines, first evaluate `List.filter isValidRequest requests`, then supply that result as the final argument to `List.map formatRequest`. Nested, it is `List.map formatRequest (List.filter isValidRequest requests)`. The source list does not change.

This runs two eager list stages: filtering traverses four elements and produces an intermediate list, then mapping traverses two elements and produces the final list. The number of calls and the number of visited elements are not identical concepts, but there are two list operations.

## Exercise 2: merge selection and transformation with `choose` {#exercise-02}

The answer region is:

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

## Exercise 3: compare loop state {#exercise-03}

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

## What to notice {#what-to-notice}

- **A pipeline does not change evaluation:** list stages still complete eagerly and each produces a result.
- **Order is part of the contract:** front cons is efficient but reverses order, so restoration must be explicit.
- **Local mutation can be contained:** callers see ordinary input and an immutable result, but the implementation still requires tracing every update.
- **`option` expresses only presence:** `None` lacks information when a consumer needs a failure reason.

Appending with `@ [ label ]` on every iteration may produce the same result, but each append traverses an increasingly long left list. After correctness, check this kind of asymptotic cost too.
