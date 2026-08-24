---
title: "Chapter 6: Recursion, Tail Calls, and Folds"
description: "Derive recursion from list structure, distinguish ordinary and tail recursion, and rewrite linear accumulation with accumulators and List.fold."
translationKey: part-01/ch-06-recursion-folds
kind: chapter
part: 1
chapter: 6
status: complete
verifiedWith:
  fsharp: "10"
  dotnetSdk: "10.0.301"
exampleIds:
  - ch06-recursion-folds
exerciseIds:
  - ch06-exercise-01
  - ch06-exercise-02
  - ch06-exercise-03
termIds:
  - accumulator
  - fold
  - list
  - pattern-matching
  - recursion
  - structural-recursion
  - tail-call
  - tail-recursion
sources:
  - id: microsoft-recursive-functions
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/functions/recursive-functions-the-rec-keyword
    checked: "2026-08-24"
  - id: microsoft-functions
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/functions/
    checked: "2026-08-24"
  - id: microsoft-lists
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/lists
    checked: "2026-08-24"
  - id: fsharp-core-list
    url: https://fsharp.github.io/fsharp-core-docs/reference/fsharp-collections-listmodule.html
    checked: "2026-08-24"
---

# Chapter 6: Recursion, Tail Calls, and Folds {#overview}

A list has only two structures: empty `[]`, or nonempty `head :: tail`. Recursion is not an arbitrary request for a function to “call itself again.” It lets a definition descend along the data structure: the empty structure supplies a base result, while the nonempty structure handles the head and gives the smaller tail to the same rule.

That correspondence is explanatory, but it does not automatically guarantee termination, efficiency, or stack safety. This chapter separates “structurally smaller,” “tail position,” and “compiler optimization,” then hands a common linear accumulation pattern to `List.fold`. The goal is not to hand-write recursion everywhere, but to read, verify, and choose it.

## What you will be able to do {#outcomes}

By the end of this chapter, you should be able to:

- define a self-referential function with `let rec` and explain why ordinary `let` cannot refer to itself;
- derive base and recursive branches from `[] | head :: tail`;
- explain how a recursive argument becomes structurally smaller and what termination depends on;
- decide whether work remains after a recursive call;
- rewrite one linear aggregation as tail recursion with an accumulator;
- use `[<TailCall>]` to check tail-call intent while understanding that it is not a universal stack-safety guarantee;
- expand the order of `List.fold` and `List.foldBack` and choose an appropriate abstraction.

This chapter handles one recursive path through a singly linked list. Trees and branching structural recursion arrive in Chapter 10. Async and task recursion must follow their own execution models and cannot inherit the synchronous tail-call conclusion directly.

## `rec` makes the name visible in its body {#rec-binding}

An ordinary non-recursive `let` name enters the following scope only after its right side has been evaluated. `let rec` makes a function name visible within its own body, allowing it to call itself:

<<< @/../examples/scripts/ch06-recursion-folds.fsx#direct-recursion{fsharp:line-numbers} [ch06-recursion-folds.fsx]

`rec` changes binding visibility only. It does not add a base case or prove that calls approach termination. Passing the original list back unchanged can recurse forever, while omitting `[]` makes the match non-exhaustive.

Functions that call one another can be defined together with `let rec ... and ...`, but only real mutual dependence needs it. Putting unrelated functions in one recursive group expands inference and comprehension scope, so this chapter does not use that form.

## Derive branches from data structure {#structural-recursion}

`sumRecursive` is **structural recursion**: its match branches correspond to the list constructors.

- `[]` has no elements, so its sum uses additive identity `0`;
- `head :: tail` combines the current head with the sum of the smaller tail;
- every recursive call receives `tail`, whose length is strictly one less.

The termination argument has two pieces: a finite list eventually reaches `[]`, and the recursive branch really uses structurally smaller `tail`. The type system helps confirm that both branches return `int`, but it does not generally prove this decreasing argument.

A base result is not arbitrary filler. Product needs identity `1`; copying a list may start from `[]`; searching needs a representation of “not found.” A wrong base value first breaks empty input and then propagates through every recursive input.

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

In `head + sumRecursive tail`, addition with `head` still happens after the recursive call returns, so the call is not in tail position. Parentheses and line breaks cannot change that fact. The representation of pending work must change.

### An accumulator carries completed work {#accumulator}

Pass the sum completed so far into the next step as an extra parameter:

<<< @/../examples/scripts/ch06-recursion-folds.fsx#tail-recursion{fsharp:line-numbers} [ch06-recursion-folds.fsx]

Each `sumLoop` step computes the new `accumulator + head` before calling itself with that value and `tail`. No addition or construction remains after the call. The branch result is exactly the call result, so it is in tail position.

Understand an accumulator by writing its invariant: at every step, `accumulator + sum values` equals the original input sum. Initially the accumulator is `0`. Moving one `head` into it preserves the equation. When `values` is empty, the accumulator is the complete answer.

Outer `sumTailRecursive` hides the initial accumulator and leaves callers a clear `int list -> int` interface. Asking callers for arbitrary initial state leaks an implementation detail and makes the invariant easier to violate.

### `[<TailCall>]` checks intent {#tailcall-attribute}

Starting with F# 8, `[<TailCall>]` can be placed on a module function or method. The compiler warns when it finds recursive calls in that function that are not in tail position. The book uses F# 10 and treats warnings as errors, so the shared loops have an automated check on their tail-call intent.

The attribute does not magically rewrite non-tail recursion and does not prove termination. It checks relevant call positions. Tail position is an important prerequisite for eliminating recursive stack growth, but cross-function calls, runtimes, debug settings, computation expressions, and other execution models can impose different limits. Do not infer “all recursion is stack-safe” from one synchronous self-recursive example.

The shared script counts a 100,000-item list with tail recursion as runtime evidence for this concrete implementation:

<<< @/../examples/scripts/ch06-recursion-folds.fsx#tail-count{fsharp:line-numbers} [ch06-recursion-folds.fsx]

Do not compare by deliberately exhausting the process stack with non-tail recursion. A .NET stack overflow is not an ordinary error from which an application can reliably recover. Use code position, compiler diagnostics, and bounded tests.

## Tail recursion does not repair every algorithm {#tail-recursion-limits}

Tail recursion primarily changes stack usage. It does not automatically change time complexity, numeric overflow, effect order, or allocation cost. An exponential algorithm that recomputes the same subproblem does not become linear merely because one call sits in tail position.

An accumulator may also change result order. Chapter 5 built a reversed list with `::` and needed a final `List.rev`. Forget that reversal and a function may be stack-safe but semantically wrong. Multiple recursive branches, exception handling, and construction after a call all need separate analysis.

Review recursion with at least four questions: does the problem shrink, can the base case be reached, is the call in tail position, and does the algorithm still repeat or accumulate expensive work?

## `fold` extracts linear accumulation {#fold}

Tail-recursive summation has a general skeleton: begin with state, combine each element into that state in order, and return the final state. `List.fold` keeps the traversal in the library and asks only for an update function and initial state:

<<< @/../examples/scripts/ch06-recursion-folds.fsx#fold-sum{fsharp:line-numbers} [ch06-recursion-folds.fsx]

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

<<< @/../examples/scripts/ch06-recursion-folds.fsx#fold-order{fsharp:line-numbers} [ch06-recursion-folds.fsx]

The left fold computes `((0 - 1) - 2) - 3 = -6`; the right fold computes `1 - (2 - (3 - 0)) = 2`. Addition within this example's integer range does not reveal direction, but that is no reason to assume all fold orders are equivalent.

Do not infer a library function's stack or allocation implementation from semantic expansion alone. The API guarantees combination order and result semantics; consult the current FSharp.Core implementation and measure for performance. For order-sensitive behavior, state the mathematical association first.

## Choose direct recursion or a fold {#choosing-recursion}

| Problem shape | Usually consider first | Reason |
| --- | --- | --- |
| Linear traversal carrying one state | `List.fold` or a specialized library function | Common traversal is encapsulated and state type is explicit |
| Sum, length, or another existing operation | `List.sum`, `List.length` | Domain intent is clearer than a custom folder |
| Output structure mirrors input structure | Clear structural recursion or `map` | Construction follows directly from patterns |
| Need to stop early | `tryFind`, `exists`, or careful recursion | A fold normally traverses the whole input |
| Tree or multiple recursive branches | Recursion aligned with the type structure | One linear accumulator may be insufficient |

`fold` is not a badge of being “more functional.” Packing a simple business rule into a huge, opaque accumulator can hide state meaning and update order. Choose the form that makes invariants, termination, and result order most visible.

## Costs and boundaries {#costs}

For a finite list of length `n`, all three summation versions perform a linear amount of element work when they complete normally. They differ mainly in where control state lives:

| Version | Time | Recursive-stack intuition | Other state |
| --- | --- | --- | --- |
| Direct recursion | `O(n)` | Non-tail calls grow depth with `n` | Addition completes while returning through frames |
| Accumulator recursion | `O(n)` | Simple self-tail calls can have growth removed by the compiler; the attribute checks intent here | One accumulator |
| `List.fold` | `O(n)` | Traversal is managed by the FSharp.Core implementation | One accumulated state |

None automatically prevents `int` range overflow or validates the business meaning of input. Stack safety, arithmetic safety, and domain correctness are different properties; testing one does not replace the others.

## Run the shared example {#run-example}

From the repository root, run:

```console
dotnet fsi --exec examples/scripts/ch06-recursion-folds.fsx
```

You should see:

```text
Sums: recursive=9 tail=9 fold=9
Empty sums: 0, 0, 0
Singleton sums: 5, 5, 5
Tail-recursive count: 100000
Fold order: left=-6 right=2
```

Empty, singleton, ordinary, and large lists separately verify base behavior, equal semantics, and bounded runtime behavior of this tail-recursive implementation. The manifest checks all five lines in order.

## Debugging: check decrease before tail position {#debugging}

When recursion fails, ask in order:

1. is there a rule for every input constructor?
2. does the base rule return the correct identity or terminating result?
3. is the recursive argument strictly smaller or closer to termination?
4. does any operation, construction, or effect remain after the recursive call?
5. does the accumulator invariant hold at initialization, advance, and completion?

Reversed output usually comes from front accumulation without a final reversal. A `fold` type error often comes from swapping accumulator and element parameters; label `'State` and `'T` from the full signature first.

When runtime grows unexpectedly, do not inspect tail position alone. Draw the call tree for a small input and see whether the same subproblem is computed repeatedly. A tail call solves retained frames, not repeated work.

## Exercises {#exercises}

Expand small inputs by hand rather than using stack overflow as an experiment. For every exercise, state the base case, decreasing argument, and result order.

### Exercise 1: expand structural recursion {#exercise-01}

For `sumRecursive [ 3; 0; 4 ]`:

1. write the full expansion down to `[]`;
2. label `head` and `tail` at every call;
3. explain why the function terminates;
4. circle the work still pending after each recursive return and explain why the function is not tail-recursive.

### Exercise 2: prove the accumulator meaning {#exercise-02}

For `sumLoop 0 [ 3; 0; 4 ]`, list every `(accumulator, values)` pair. Check each one against “accumulator plus the sum of the remaining list equals the original list sum.”

Then imagine changing the recursive branch to recurse first and add `head` afterward. Explain what `[<TailCall>]` should report, and which termination or numeric properties remain unproved even when a tail-call check succeeds.

### Exercise 3: expand folds and choose an abstraction {#exercise-03}

1. parenthesize the subtraction performed by `List.fold` over `[ 1; 2; 3 ]`;
2. parenthesize and evaluate the corresponding `List.foldBack`;
3. write the initial state and folder type for counting list length with `List.fold`;
4. choose a specialized function, fold, or direct recursion for ordinary summation, finding the first match early, and traversing a binary tree, explaining why.

[Read the chapter solutions](../solutions/ch-06-recursion-folds).

## Summary {#summary}

- `let rec` makes a function name visible in its own body; it does not supply a base case or termination proof.
- Structural recursion aligns patterns with data constructors and gives a structurally smaller component to the recursive call.
- A recursive call is not a tail call when work remains afterward; line breaks and parentheses do not change that.
- An accumulator carries completed work so a simple linear self-call can move into tail position.
- `[<TailCall>]` checks tail-call intent; it proves neither termination nor stack safety for every execution model.
- `List.fold` threads state from the left; `foldBack` combines from the right and reverses folder argument order.
- Tail recursion, time complexity, arithmetic safety, and domain correctness require separate verification.

The language foundations of Part I now close: values, bindings, functions, branches, list data flow, and recursion. The capstone slice next combines them into a pure booking script before Part II turns implicit constraints into a domain model with records and unions.

## Vocabulary {#vocabulary}

- **recursion:** a function calling itself directly or indirectly to process a smaller problem.
- **structural recursion:** branching on data constructors and recursively processing a structurally smaller component.
- **tail call:** a call made as the final operation whose result needs no further processing.
- **tail recursion:** recursion that places self-calls on recursive paths in tail position.
- **accumulator:** state carrying completed work from one step to the next.
- **fold:** a higher-order operation that combines elements into accumulated state in a defined order.

## Sources {#sources}

- [Microsoft Learn: Recursive functions, `rec`, tail recursion, and `TailCall`](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/functions/recursive-functions-the-rec-keyword)
- [Microsoft Learn: Functions](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/functions/)
- [Microsoft Learn: List recursion and folds](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/lists)
- [FSharp.Core: List module reference](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-collections-listmodule.html)
