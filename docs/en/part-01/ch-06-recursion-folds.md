---
title: "Chapter 6: Recursion, Tail Calls, and Folds"
description: "Derive recursion from list structure, distinguish ordinary and tail recursion, and rewrite linear accumulation with accumulators and List.fold."
translationKey: part-01/ch-06-recursion-folds
---

# Chapter 6: Recursion, Tail Calls, and Folds {#overview}

A list has only two structures: empty `[]`, or nonempty `head :: tail`. Structural recursion lets a definition descend through those cases: the empty structure supplies a base result, while the nonempty structure handles the head and gives the smaller tail to the same rule.

That correspondence determines the function's branches, but it does not prove termination, efficiency, or stack safety. We will separate “structurally smaller,” “tail position,” and “compiler optimization,” then express a common linear accumulation pattern with `List.fold`.

Here we follow one recursive path through a singly linked list. Chapter 10 covers trees and branching recursion. Async and task recursion use different execution models, so their stack behavior needs separate analysis.

## `rec` makes the name visible in its body {#rec-binding}

An ordinary non-recursive `let` name enters the following scope only after its right side has been evaluated. `let rec` makes a function name visible within its own body, allowing it to call itself:

```fsharp:line-numbers [ch06-recursion-folds.fsx]
let rec sumRecursive values =
    match values with
    | [] -> 0
    | head :: tail -> head + sumRecursive tail
```
`rec` changes binding visibility only. The programmer still supplies a base case and a decreasing step. Passing the original list back unchanged can recurse forever, while omitting `[]` makes the match non-exhaustive.

Functions that call one another can be defined together with `let rec ... and ...`. Reserve that form for real mutual dependence; separate groups keep inference and comprehension scope smaller.

## Derive branches from data structure {#structural-recursion}

`sumRecursive` is **structural recursion**: its match branches correspond to the list constructors.

- `[]` has no elements, so its sum uses additive identity `0`;
- `head :: tail` combines the current head with the sum of the smaller tail;
- every recursive call receives `tail`, whose length is strictly one less.

The termination argument has two pieces: a finite list eventually reaches `[]`, and the recursive branch really uses structurally smaller `tail`. The type system confirms that both branches return `int`; the decreasing argument comes from inspecting the recursive input.

A base result expresses the empty case's meaning. Product uses identity `1`; copying a list may start from `[]`; searching uses an explicit absence value. A wrong base value first breaks empty input and then propagates through every recursive input.

### Expand one call {#expansion}

For `[ 3; 0; 4 ]`, direct recursion means:

```text
3 + sumRecursive [0; 4]
3 + (0 + sumRecursive [4])
3 + (0 + (4 + sumRecursive []))
3 + (0 + (4 + 0))
```

The expansion reveals two facts: progression moves from list head toward tail, and each level still performs an addition after the recursive result returns. The second fact makes it non-tail-recursive.

## Tail position leaves no pending work {#tail-position}

A **tail call** is the last operation before a branch returns. The current function does not process the call's result further; it returns that result directly. Tail recursion requires self-calls on recursive paths to be in tail position.

In `head + sumRecursive tail`, addition with `head` still happens after the recursive call returns, so work remains after the call. Tail position requires representing that pending work in an accumulator; parentheses and line breaks preserve the original execution order.

### An accumulator carries completed work {#accumulator}

Pass the sum completed so far into the next step as an extra parameter:

```fsharp:line-numbers [ch06-recursion-folds.fsx]
[<TailCall>]
let rec sumLoop accumulator values =
    match values with
    | [] -> accumulator
    | head :: tail -> sumLoop (accumulator + head) tail

let sumTailRecursive values = sumLoop 0 values
```
Each `sumLoop` step computes the new `accumulator + head` before calling itself with that value and `tail`. No addition or construction remains after the call. The branch result is exactly the call result, so it is in tail position.

Understand an accumulator by writing its invariant: at every step, `accumulator + sum values` equals the original input sum. Initially the accumulator is `0`. Moving one `head` into it preserves the equation. When `values` is empty, the accumulator is the complete answer.

Outer `sumTailRecursive` hides the initial accumulator and leaves callers a clear `int list -> int` interface. Asking callers for arbitrary initial state leaks an implementation detail and makes the invariant easier to violate.

### `[<TailCall>]` checks intent {#tailcall-attribute}

Starting with F# 8, `[<TailCall>]` can be placed on a module function or method. The compiler warns when it finds recursive calls in that function that are not in tail position. Put this deliberately invalid `.fs` example in a minimal project with warnings as errors to observe `FS3569`:

```fsharp:line-numbers [NonTailRecursion.fs]
[<TailCall>]
let rec fibonacci n =
    match n with
    | 0
    | 1 -> n
    | value -> fibonacci (value - 1) + fibonacci (value - 2)
```
The standard script check also runs FSI with `--warnaserror+`. The compiled negative fixture demonstrates the `TailCall` diagnostic. Source position and the bounded run below check the shared loops from two other angles.

The attribute checks call position; it neither changes the algorithm nor proves termination. Actual stack use can also depend on cross-function calls, runtime behavior, debug settings, computation expressions, and other execution models. The narrow conclusion here is that this synchronous self-call is in tail position.

The shared script also runs this implementation against a 100,000-item list:

```fsharp:line-numbers [ch06-recursion-folds.fsx]
[<TailCall>]
let rec countLoop accumulator values =
    match values with
    | [] -> accumulator
    | _ :: tail -> countLoop (accumulator + 1) tail

let countTailRecursive values = countLoop 0 values
let largeCount = countTailRecursive [ 1..100_000 ]

printfn "Tail-recursive count: %d" largeCount
```
Compare implementations through code position, compiler diagnostics, and bounded tests. Deliberately exhausting the process stack risks terminating the process because .NET treats stack overflow as a fatal condition in ordinary application code.

## Tail recursion addresses stack usage {#tail-recursion-limits}

Tail recursion primarily changes stack usage. Time complexity, numeric overflow, side-effect order, and allocation cost remain separate properties. An exponential algorithm that recomputes the same subproblem stays exponential even when one call sits in tail position.

An accumulator may also change result order. Chapter 5 built a reversed list with `::` and needed a final `List.rev`. Forget that reversal and a function may be stack-safe but semantically wrong. Multiple recursive branches, exception handling, and construction after a call all need separate analysis.

Review recursion with at least four questions: does the problem shrink, can the base case be reached, is the call in tail position, and does the algorithm still repeat or accumulate expensive work?

## `fold` extracts linear accumulation {#fold}

Tail-recursive summation has a general skeleton: begin with state, combine each element into that state in order, and return the final state. `List.fold` keeps the traversal in the library and asks only for an update function and initial state:

```fsharp:line-numbers [ch06-recursion-folds.fsx]
let sumWithFold values =
    values |> List.fold (fun accumulator value -> accumulator + value) 0
```
Its core type is:

```text
List.fold : ('State -> 'T -> 'State) -> 'State -> 'T list -> 'State
```

The first argument receives the current accumulator and element and produces the next state. The second is initial state, and the third is the list. This order makes `values |> List.fold folder initial` a natural pipeline.

For `[ a; b; c ]`, a left fold expands as:

```text
folder (folder (folder initial a) b) c
```

In `sumWithFold`, both `'State` and `'T` become `int`, but they need not be the same. A booking pair list could be folded into a different state type containing text, count, and amount.

### `foldBack` changes direction and argument order {#foldback}

`List.foldBack folder [ a; b; c ] initial` combines from the right, with semantic expansion:

```text
folder a (folder b (folder c initial))
```

Its folder receives the element before state, the reverse of `List.fold`'s state-first order. The shared script makes the difference visible with subtraction:

```fsharp:line-numbers [ch06-recursion-folds.fsx]
let leftAssociated = List.fold (fun state value -> state - value) 0 [ 1; 2; 3 ]
let rightAssociated = List.foldBack (fun value state -> value - state) [ 1; 2; 3 ] 0

printfn "Fold order: left=%d right=%d" leftAssociated rightAssociated
```
The left fold computes `((0 - 1) - 2) - 3 = -6`; the right fold computes `1 - (2 - (3 - 0)) = 2`. Addition within this example's integer range hides the direction, while subtraction exposes it. Always identify the association when order matters.

Use semantic expansion to understand combination order and result meaning. Use the current FSharp.Core implementation and measurements to establish stack or allocation behavior. For order-sensitive behavior, state the mathematical association first.

## Choose direct recursion or a fold {#choosing-recursion}

| Problem structure | Usually consider first | Reason |
| --- | --- | --- |
| Linear traversal carrying one state | `List.fold` or a specialized library function | Common traversal is encapsulated and state type is explicit |
| Sum, length, or another existing operation | `List.sum`, `List.length` | Domain intent is clearer than a custom folder |
| Output structure mirrors input structure | Clear structural recursion or `map` | Construction follows directly from patterns |
| Need to stop early | `tryFind`, `exists`, or careful recursion | A fold normally traverses the whole input |
| Tree or multiple recursive branches | Recursion aligned with the type structure | One linear accumulator may be insufficient |

Choose `fold` when one accumulator expresses the state transition clearly. A large opaque accumulator can hide state meaning and update order, so use the form that makes invariants, termination, and result order most visible.

## Costs and limits {#costs}

For a finite list of length `n`, all three summation versions perform a linear amount of element work when they complete normally. They differ mainly in where control state lives:

| Version | Time | Recursive-stack intuition | Other state |
| --- | --- | --- | --- |
| Direct recursion | `O(n)` | Non-tail calls grow depth with `n` | Addition completes while returning through frames |
| Accumulator recursion | `O(n)` | Simple self-tail calls can have growth removed by the compiler; the attribute checks intent here | One accumulator |
| `List.fold` | `O(n)` | Traversal is managed by the FSharp.Core implementation | One accumulated state |

Each implementation still needs separate checks for `int` overflow and the business meaning of its input. Stack safety, arithmetic safety, and domain correctness must be verified separately.

## Run the shared example {#run-example}

From the repository root, run:

```console
dotnet fsi --warnaserror+ --exec examples/scripts/ch06-recursion-folds.fsx
```

You should see:

```text
Sums: recursive=9 tail=9 fold=9
Empty sums: 0, 0, 0
Singleton sums: 5, 5, 5
Tail-recursive count: 100000
Fold order: left=-6 right=2
```

Empty, singleton, ordinary, and large lists separately verify base behavior, equal semantics, and bounded runtime behavior of this tail-recursive implementation. Compare all five output lines in order.

## Check decrease before tail position {#debugging}

When recursion fails, ask in order:

1. is there a rule for every input constructor?
2. does the base rule return the correct identity or terminating result?
3. is the recursive argument strictly smaller or closer to termination?
4. does any operation, construction, or side effect remain after the recursive call?
5. does the accumulator invariant hold at initialization, advance, and completion?

Reversed output usually comes from front accumulation without a final reversal. A `fold` type error often comes from swapping accumulator and element parameters; label `'State` and `'T` from the full signature first.

When runtime grows unexpectedly, inspect both tail position and repeated work. Draw the call tree for a small input and see whether the same subproblem appears several times. A tail call addresses retained frames; memoization or a different algorithm addresses repeated work.

## Exercises {#exercises}

Expand small inputs by hand rather than using stack overflow as an experiment. For every exercise, state the base case, decreasing argument, and result order.

### Exercise 1: expand structural recursion {#exercise-01}

For `sumRecursive [ 3; 0; 4 ]`:

1. write the full expansion down to `[]`;
2. label `head` and `tail` at every call;
3. explain why the function terminates;
4. circle the work still pending after each recursive return and use it to classify the function's tail position.

### Exercise 2: prove the accumulator meaning {#exercise-02}

For `sumLoop 0 [ 3; 0; 4 ]`, list every `(accumulator, values)` pair. Check each one against “accumulator plus the sum of the remaining list equals the original list sum.”

Then imagine changing the recursive branch to recurse first and add `head` afterward. Explain what `[<TailCall>]` should report, and which termination or numeric properties remain unproved even when a tail-call check succeeds.

### Exercise 3: expand folds and choose an abstraction {#exercise-03}

1. parenthesize the subtraction performed by `List.fold` over `[ 1; 2; 3 ]`;
2. parenthesize and evaluate the corresponding `List.foldBack`;
3. write the initial state and folder type for counting list length with `List.fold`;
4. choose a specialized function, fold, or direct recursion for ordinary summation, finding the first match early, and traversing a binary tree, explaining why.

[Read the chapter solutions](../solutions/ch-06-recursion-folds).

## Part I checkpoint {#part-checkpoint}

From the repository root, run the integrated booking script:

```console
dotnet fsi --warnaserror+ --exec examples/capstone/part-01/BookingBasics.fsx
```

Its output must distinguish valid from invalid input rows, accept requests that fit, reject the over-capacity request, and finish with the correct booked and remaining capacity. This closes the Part I language path; persistence and concurrency guarantees arrive in later parts.

[Continue to Chapter 7](../part-02/ch-07-records-equality), where records and unions turn more of those implicit rules into types.

## Sources {#sources}

- [Microsoft Learn: Recursive functions, `rec`, tail recursion, and `TailCall`](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/functions/recursive-functions-the-rec-keyword)
- [Microsoft Learn: Functions](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/functions/)
- [Microsoft Learn: List recursion and folds](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/lists)
- [FSharp.Core: List module reference](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-collections-listmodule.html)
