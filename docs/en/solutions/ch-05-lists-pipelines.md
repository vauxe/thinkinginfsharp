---
title: "Chapter 5 Solutions"
description: "Reasoning about list transformations, pipelines, choose, for, while, and local mutable state."
translationKey: solutions/ch-05-lists-pipelines
kind: solution
part: 1
chapter: 5
status: complete
verifiedWith:
  fsharp: "10"
  dotnetSdk: "10.0.301"
exampleIds:
  - ch05-lists-pipelines
exerciseIds:
  - ch05-exercise-01
  - ch05-exercise-02
  - ch05-exercise-03
termIds: []
sources:
  - id: microsoft-lists
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/lists
    checked: "2026-08-24"
  - id: microsoft-values
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/values/
    checked: "2026-08-24"
---

# Chapter 5 Solutions {#overview}

Check every stage's shape and order first. Equal final output can still hide excess traversals, uncontained state, or repeated effects.

[Return to Chapter 5](../part-01/ch-05-lists-pipelines).

## Exercise 1: trace a pipeline stage by stage {#exercise-01}

The shared pipeline is:

<<< @/../examples/scripts/ch05-lists-pipelines.fsx#filter-map-pipeline{fsharp:line-numbers} [ch05-lists-pipelines.fsx]

Both `requests` and the filtered result have type `(string * int) list`. The former contains Lin 3, Ada 0, Sam 2, and Mina -1 in order; the latter retains only Lin 3 and Sam 2. The mapped result has type `string list` and order `[ "Lin:3"; "Sam:2" ]`.

Without pipelines, first evaluate `List.filter isValidRequest requests`, then supply that result as the final argument to `List.map formatRequest`. Nested, it is `List.map formatRequest (List.filter isValidRequest requests)`. The source list does not change.

This runs two eager list stages: filtering traverses four elements and produces an intermediate list, then mapping traverses two elements and produces the final list. The number of calls and the number of visited elements are not identical concepts, but there are two list operations.

## Exercise 2: merge selection and transformation with `choose` {#exercise-02}

The answer region is:

<<< @/../examples/scripts/ch05-lists-pipelines.fsx#choose-pipeline{fsharp:line-numbers} [ch05-lists-pipelines.fsx]

`tryFormatRequest` has full type `(string * int) -> string option`. In order it produces `Some "Lin:3"`, `None`, `Some "Sam:2"`, and `None`. `List.choose` extracts only the values inside the two `Some` cases while preserving order, producing the same `string list` as filter then map.

If the valid-request list must be logged, tested, or passed to another step independently, separate `filter` and `map` stages are clearer. If an output can only be constructed for a valid item and the intermediate list has no domain meaning, `choose` is more exact. Here `None` discards why a request was invalid and the original request itself; use an error-carrying model when consumers need that reason.

## Exercise 3: compare loop state {#exercise-03}

The `for` and `while` versions are:

<<< @/../examples/scripts/ch05-lists-pipelines.fsx#for-loop{fsharp:line-numbers} [ch05-lists-pipelines.fsx]

<<< @/../examples/scripts/ch05-lists-pipelines.fsx#while-loop{fsharp:line-numbers} [ch05-lists-pipelines.fsx]

Both versions change `reversedLabels` in the same way. After Lin it is `[ "Lin:3" ]`. Ada produces `None`, so it stays unchanged. Prepending Sam produces `[ "Sam:2"; "Lin:3" ]`. Mina produces `None`, so it remains unchanged. `List.rev` restores relative input order; without it, valid items would be reversed.

The `while` version must also move `remaining` from the full list through each successive `tail` until it reaches `[]`. Forgetting the update on any nonempty path leaves the condition true and repeats the same element forever.

For “print every label,” prefer `for` or `List.iter` because the goal is a `unit` effect. For “produce a new label list,” prefer `choose` because its result type expresses the output. If profiling later proves a hot path needs a custom one-pass implementation, compare a local mutable loop rather than assuming first.

## What to notice {#what-to-notice}

- **A pipeline does not change evaluation:** list stages still complete eagerly and each produces a result.
- **Order is part of the contract:** front cons is efficient but reverses order, so restoration must be explicit.
- **Local mutation can be contained:** callers see ordinary input and an immutable result, but the implementation still requires tracing every update.
- **`option` expresses only presence:** `None` lacks information when a consumer needs a failure reason.

Appending with `@ [ label ]` on every iteration may produce the same result, but each append traverses an increasingly long left list. After correctness, check this kind of asymptotic cost too.
