---
title: "Chapter 10 Solutions"
description: "Derive a short-circuiting query, map laws, and a one-pass tree summary from recursive cases."
translationKey: solutions/ch-10-recursive-types
kind: solution
part: 2
chapter: 10
status: complete
verifiedWith:
  fsharp: "10"
  dotnetSdk: "10.0.301"
exampleIds:
  - ch10-recursive-types
exerciseIds:
  - ch10-exercise-01
  - ch10-exercise-02
  - ch10-exercise-03
termIds: []
sources:
  - id: microsoft-discriminated-unions
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/discriminated-unions
    checked: "2026-08-24"
  - id: microsoft-recursive-functions
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/functions/recursive-functions-the-rec-keyword
    checked: "2026-08-24"
---

# Chapter 10 Solutions {#overview}

Each solution starts with the recursive type's cases. That keeps base behavior, recursive progress, and combination policy visible before syntax is written.

[Return to Chapter 10](../part-02/ch-10-recursive-types).

## Exercise 1: derive a query from the cases {#exercise-01}

The rules are:

- `Empty` contains no matching value, so return `false`;
- `Leaf value` returns `predicate value`;
- `Branch(left, right)` succeeds when either subtree succeeds.

The direct translation is:

```fsharp
let rec exists predicate tree =
    match tree with
    | Empty -> false
    | Leaf value -> predicate value
    | Branch(left, right) ->
        exists predicate left || exists predicate right
```

F# Boolean `||` short-circuits. When the left call returns `true`, the right call does not run. This is desirable for a pure existence query and may avoid most of the tree. In the worst case—no match, or a match in the final visited leaf—the function still visits every node and takes `O(n)` time.

Do not hide required effects inside `predicate` and then rely on every leaf being visited. The function promises an existence answer, not iteration. A separate traversal should perform work that must occur for every value.

## Exercise 2: test the map laws {#exercise-02}

The fold replaces each constructor with the corresponding reconstructed constructor:

```fsharp
let mapTreeWithFold mapping =
    foldTree
        Empty
        (mapping >> Leaf)
        (fun left right -> Branch(left, right))

let examples = [ emptyTree; leafTree; branchTree ]
let increment seats = seats + 1
let double seats = seats * 2

let identityHolds =
    examples
    |> List.forall (fun tree -> mapTreeWithFold id tree = tree)

let compositionHolds =
    examples
    |> List.forall (fun tree ->
        mapTreeWithFold (increment >> double) tree
        = (tree |> mapTreeWithFold increment |> mapTreeWithFold double))
```

Both values are `true` for the three examples. That detects several likely implementation mistakes, but the recursive type allows trees of unbounded size and shape; three values cannot enumerate them all.

A proof follows the same structure. The laws hold directly for `Empty` and `Leaf`. For `Branch`, assume they hold for each smaller subtree, then show reconstruction preserves the combined result. This structural induction is the reasoning counterpart of structural recursion.

## Exercise 3: compute one summary in one fold {#exercise-03}

The empty rule has no maximum. A leaf initializes all three fields. The branch rule combines already completed summaries:

```fsharp
type TreeSummary =
    { LeafCount: int
      TotalSeats: int
      MaximumSeats: int option }

let emptySummary =
    { LeafCount = 0
      TotalSeats = 0
      MaximumSeats = None }

let summarizeLeaf seats =
    { LeafCount = 1
      TotalSeats = seats
      MaximumSeats = Some seats }

let combineSummaries left right =
    let maximum =
        match left.MaximumSeats, right.MaximumSeats with
        | None, other
        | other, None -> other
        | Some leftMax, Some rightMax -> Some(max leftMax rightMax)

    { LeafCount = left.LeafCount + right.LeafCount
      TotalSeats = left.TotalSeats + right.TotalSeats
      MaximumSeats = maximum }

let summarize tree =
    tree
    |> foldTree emptySummary summarizeLeaf combineSummaries
```

The expected values are:

| Tree | `LeafCount` | `TotalSeats` | `MaximumSeats` |
| --- | ---: | ---: | --- |
| `Empty` | 0 | 0 | `None` |
| `Leaf 2` | 1 | 2 | `Some 2` |
| shared branch | 3 | 9 | `Some 4` |

The fold visits each node once, so time is `O(n)`. The direct recursive implementation retains at most a root-to-current-node chain plus pending branch work, so call-stack depth is `O(h)`. It computes all three fields in one traversal; writing three separate folds would remain `O(n)` asymptotically but visit the tree three times.

## What to notice {#what-to-notice}

- **Case rules precede code:** they expose missing base cases and combination policy.
- **Short-circuiting changes visitation:** `exists` may intentionally skip an entire subtree.
- **Examples are checks, not proofs:** the recursive structure supplies the induction argument.
- **`None` is the honest empty maximum:** a sentinel such as `0` would be wrong for negative leaves.
- **One compound fold shares traversal:** one state can carry several related aggregates.
