---
title: "Chapter 6 Solutions"
description: "Reasoning about structural recursion, accumulator invariants, tail calls, and left and right folds."
translationKey: solutions/ch-06-recursion-folds
---

# Chapter 6 Solutions {#overview}

A recursion answer must explain decrease, invariants, and work after return. Writing only the final integer does not verify the algorithm's structure.

[Return to Chapter 6](../part-01/ch-06-recursion-folds).

## Exercise 1: expand structural recursion {#exercise-01}

The shared definition is:

```fsharp:line-numbers
let rec sumRecursive values =
    match values with
    | [] -> 0
    | head :: tail -> head + sumRecursive tail
```
The full expansion is `3 + sumRecursive [0; 4]`, then `3 + (0 + sumRecursive [4])`, then `3 + (0 + (4 + sumRecursive []))`. The base rule supplies `0`, producing `7`.

The three nonempty calls have `(head, tail)` values `(3, [0; 4])`, `(0, [4])`, and `(4, [])`. Tail length falls by one each time, so finite input eventually reaches the empty list and terminates.

Every level must wait for the recursive result and then perform `head + result`. Those three additions are pending work, so the self-call is not in tail position and call depth grows with list length.

## Exercise 2: prove the accumulator meaning {#exercise-02}

The tail-recursive definition is:

```fsharp:line-numbers
[<TailCall>]
let rec sumLoop accumulator values =
    match values with
    | [] -> accumulator
    | head :: tail -> sumLoop (accumulator + head) tail

let sumTailRecursive values = sumLoop 0 values
```
The states are `(0, [3; 0; 4])`, `(3, [0; 4])`, `(3, [4])`, and `(7, [])`. At every step, accumulator plus remaining-list sum is `7`. At the end, the remaining sum is zero and the accumulator is the answer.

If the function recurses first and adds `head` afterward, work remains after return. `[<TailCall>]` should report a non-tail recursive call, and the book's warnings-as-errors setting should reject the build. Even a passing attribute check proves neither finite input, decreasing arguments, arithmetic range safety, nor domain correctness. Reasoning, type choice, and tests verify those separately.

## Exercise 3: expand folds and choose an abstraction {#exercise-03}

The order example is:

```fsharp:line-numbers
let leftAssociated = List.fold (fun state value -> state - value) 0 [ 1; 2; 3 ]
let rightAssociated = List.foldBack (fun value state -> value - state) [ 1; 2; 3 ] 0

printfn "Fold order: left=%d right=%d" leftAssociated rightAssociated
```
The left-fold parentheses are `((0 - 1) - 2) - 3`, producing `-6`. The right-fold parentheses are `1 - (2 - (3 - 0))`, producing `2`. Direction and folder parameter order both differ.

To count with `List.fold`, initial state is `0`. The folder receives `count` and an ignored element and returns `count + 1`, with abstract type `int -> 'a -> int`. The complete operation folds an `'a list` into `int`.

Prefer `List.sum` for ordinary summation because its name states intent. Prefer `List.tryFind` for the first match because it can stop early; ordinary `fold` normally visits the whole input. Use direct recursion aligned with leaf and branch structure for a binary tree, then extract a tree fold in Chapter 10.

## What to notice {#what-to-notice}

- **Structural decrease and tail position are separate checks:** a function can terminate but be non-tail-recursive, or make tail calls without approaching a base case.
- **An accumulator needs an invariant:** an extra parameter is not a proof until its meaning at every step is stated.
- **Fold order affects results:** direction can be hidden only for operations with the relevant associativity properties.
- **Prefer a specialized operation first:** when `List.sum` or `List.tryFind` fits, a handwritten fold may be less clear.

A length folder using tuple state or an extra index can also reach the right answer, but it adds invariants this exercise does not need. Minimal state is usually easiest to verify.
