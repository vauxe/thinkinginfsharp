---
title: "Chapter 10: Recursive Types and Structural Recursion"
description: "Model trees with recursive discriminated unions, then derive traversals, map, and fold directly from the type's cases."
translationKey: part-02/ch-10-recursive-types
---

# Chapter 10: Recursive Types and Structural Recursion {#overview}

A venue may divide into sections, each section into smaller sections, until the leaves hold bookable groups. The depth is not fixed in advance. A flat record with fields such as `Left`, `LeftLeft`, and `LeftRight` cannot express this open-ended hierarchy, while a list loses the branching relationship.

A recursive type solves the representation problem by referring to itself. Structural recursion solves the processing problem by following the same cases and recurring only into the smaller values stored there. The type definition is therefore not merely storage syntax: it is a plan for the functions that consume it.

We use finite in-memory values here and do not consider mutation, cyclic lazy values, or alternative stack-management techniques.

## A recursive case stores smaller values of the same type {#recursive-type}

The shared type represents an empty tree, one leaf value, or a branch with two subtrees:

```fsharp:line-numbers
type BookingTree<'T> =
    | Empty
    | Leaf of 'T
    | Branch of left: BookingTree<'T> * right: BookingTree<'T>

let emptyTree: BookingTree<int> = Empty
let leafTree = Leaf 2

let branchTree = Branch(Leaf 2, Branch(Leaf 3, Leaf 4))
```
Save this complete starting point as `ch10-recursive-types.fsx`. Except for the independent mutual-type syntax example, later blocks continue from these definitions in reading order.

`BookingTree<'T>` appears inside its own `Branch` case. That self-reference makes the type recursive. The type parameter says every leaf in one tree carries the same payload type, while the branching structure is independent of that type.

Read the cases as construction rules:

```text
a BookingTree<'T> is
  Empty
  or Leaf containing one 'T
  or Branch containing two BookingTree<'T> values
```

`Branch(Leaf 2, Branch(Leaf 3, Leaf 4))` is finite even though the type permits arbitrary depth. Each branch value contains already constructed subtrees. The declaration allows more nesting; it does not create an infinite value by itself.

The chosen cases are domain policy. If an empty tree is meaningless, omit `Empty` and define a non-empty tree. If branch nodes also need labels, carry a value in `Branch`. Decide which structures are legal before copying this particular definition.

## The type determines the traversal {#structural-traversal}

To consume every `BookingTree`, cover every case. To process a `Branch`, recursively process its two subtree fields:

```fsharp:line-numbers
let rec countLeaves tree =
    match tree with
    | Empty -> 0
    | Leaf _ -> 1
    | Branch(left, right) -> countLeaves left + countLeaves right

let rec totalSeats tree =
    match tree with
    | Empty -> 0
    | Leaf seats -> seats
    | Branch(left, right) -> totalSeats left + totalSeats right

printfn "Counts: empty=%d leaf=%d branch=%d" (countLeaves emptyTree) (countLeaves leafTree) (countLeaves branchTree)

printfn "Totals: empty=%d leaf=%d branch=%d" (totalSeats emptyTree) (totalSeats leafTree) (totalSeats branchTree)
```
This block continues from the three example trees and prints:

```text
Counts: empty=0 leaf=1 branch=3
Totals: empty=0 leaf=2 branch=9
```

`let rec` makes the function name available inside its own body. Both functions have the same structural skeleton:

- `Empty` is a base case and makes no recursive call;
- `Leaf` handles its payload and makes no recursive call;
- `Branch` calls the function on `left` and `right`, then combines the two results.

This is **structural recursion**. Each recursive call receives a direct component of the matched value, so for an ordinary finite tree it progresses toward `Empty` or `Leaf`. Termination is visible in the relationship between the type and the function rather than hidden in a numeric counter.

The result rule still depends on the question. `countLeaves` gives every leaf `1`; `totalSeats` gives it its payload. Both give `Empty` the identity `0` and combine branch results with addition.

## `map` changes payloads and preserves structure {#tree-map}

A tree map handles every case but changes only leaves:

```fsharp:line-numbers
let rec mapTree mapping tree =
    match tree with
    | Empty -> Empty
    | Leaf value -> Leaf(mapping value)
    | Branch(left, right) -> Branch(mapTree mapping left, mapTree mapping right)

let rec renderTree formatValue tree =
    match tree with
    | Empty -> "Empty"
    | Leaf value -> $"Leaf({formatValue value})"
    | Branch(left, right) -> $"Branch({renderTree formatValue left},{renderTree formatValue right})"

let labeledTree = branchTree |> mapTree (fun seats -> $"{seats} seats")

printfn "Mapped: %s" (renderTree id labeledTree)
```
This continuation prints:

```text
Mapped: Branch(Leaf(2 seats),Branch(Leaf(3 seats),Leaf(4 seats)))
```

Its inferred type is:

```text
mapTree : ('T -> 'U) -> BookingTree<'T> -> BookingTree<'U>
```

That is the type FSI displays, not a declaration to paste into the script.

`Empty` remains `Empty`; `Leaf value` becomes `Leaf (mapping value)`; a `Branch` is rebuilt from mapped subtrees in the same positions. The mapping function knows nothing about branches, and the traversal knows nothing about the payload conversion.

Two useful laws state what “structure-preserving” means:

```text
mapTree id tree = tree
mapTree (f >> g) tree = mapTree f tree |> mapTree g
```

The second law assumes ordinary pure functions and equality-capable values when tested. These laws are design checks, not special compiler behavior. Reordering or dropping a branch would violate them.

`renderTree` is another structural traversal. It does not preserve the tree structure; it converts the same cases into text. Repeated traversal code suggests that the case handling can be factored out.

## `fold` names one rule for each case {#tree-fold}

The shared fold captures the recursive mechanics:

```fsharp:line-numbers
let rec foldTree onEmpty onLeaf onBranch tree =
    match tree with
    | Empty -> onEmpty
    | Leaf value -> onLeaf value
    | Branch(left, right) ->
        let leftResult = foldTree onEmpty onLeaf onBranch left
        let rightResult = foldTree onEmpty onLeaf onBranch right
        onBranch leftResult rightResult

let countWithFold = foldTree 0 (fun _ -> 1) (+)

let totalWithFold = foldTree 0 id (+)

printfn
    "Fold agrees: count=%b total=%b"
    (countWithFold branchTree = countLeaves branchTree)
    (totalWithFold branchTree = totalSeats branchTree)
```
This continuation prints `Fold agrees: count=true total=true`.

Read its arguments from the type:

```text
foldTree :
    onEmpty:'State ->
    onLeaf:('T -> 'State) ->
    onBranch:('State -> 'State -> 'State) ->
    tree:BookingTree<'T> ->
    'State
```

This is also an inferred signature for reading, not a standalone code block.

The fold replaces each constructor with a caller-supplied rule. `Empty` becomes `onEmpty`. A leaf becomes `onLeaf value`. A branch first folds both subtrees, then combines their results with `onBranch`.

This removes explicit recursion from each caller, not the traversal work. Every node is still visited. `countWithFold` and `totalWithFold` differ only in their three rules, and the script checks that they agree with the direct definitions.

A fold can produce more than numbers. Choose `'State` as a record to compute count, total, and maximum together; choose it as another tree to reconstruct structure; choose it as a function when building a more specialized traversal. The type signature tells you every rule must return the same state type.

### Derive `map` from `fold` {#map-from-fold}

Once the earlier `foldTree` is trusted, map can be expressed without writing `let rec` again:

```fsharp
let mapTreeWithFold mapping =
    foldTree
        Empty
        (mapping >> Leaf)
        (fun left right -> Branch(left, right))
```

The three arguments are exactly the rules that preserve the three constructors. The explicit recursive version reveals how the function is derived; once those rules are familiar, the fold version centralizes traversal.

## Height predicts the direct traversal's stack need {#depth-and-stack}

The example defines height with the same skeleton:

```fsharp:line-numbers
let rec height tree =
    match tree with
    | Empty -> 0
    | Leaf _ -> 1
    | Branch(left, right) -> 1 + max (height left) (height right)

printfn "Heights: empty=%d leaf=%d branch=%d" (height emptyTree) (height leafTree) (height branchTree)

printfn "Shape preserved: before=%d after=%d" (countLeaves branchTree) (countLeaves labeledTree)
```
This continuation prints:

```text
Heights: empty=0 leaf=1 branch=3
Shape preserved: before=3 after=3
```

With the convention `Empty = 0` and `Leaf = 1`, a branch is one plus the greater subtree height. The example branch has three leaves and height three. Leaf count and height measure different facts: a balanced tree can hold many leaves with modest height, while a one-sided tree can have height proportional to its node count.

For `countLeaves`, `mapTree`, and `foldTree`:

- running time is `O(n)` because each of the `n` nodes is visited once;
- direct call-stack use is `O(h)`, where `h` is maximum height;
- rebuilding in `mapTree` also allocates `O(n)` output nodes.

These branch traversals are not tail-recursive: after a child call returns, the function still has another child or a combination to process. Adding an accumulator mechanically does not remove that pending work.

For ordinary bounded domain trees, the direct definition is often clearest. If input can be adversarial or extremely deep, height needs an explicit limit, measurement, or an iterative traversal with an explicit work stack. Treat that as a requirement backed by expected input, not as a reason to obscure every recursive definition preemptively.

## Mutually recursive types use `and` {#mutual-recursion}

Sometimes two types contain each other. F# joins their declarations with `and`:

```fsharp
type Expression =
    | Literal of int
    | Let of Binding * Expression
and Binding =
    { Name: string
      Value: Expression }
```

This is an independent syntax example that compiles without `BookingTree`. Use mutual recursion only when the domain genuinely has two distinct concepts. A single recursive union is easier to traverse and should not be split merely to demonstrate the syntax. Mutually recursive functions use the corresponding `let rec ... and ...` form.

## Exercises {#exercises}

### Exercise 1: derive a query from the cases {#exercise-01}

Write `exists : ('T -> bool) -> BookingTree<'T> -> bool` by structural recursion. State the rule for each case before writing code. Should the branch implementation always visit the right subtree? Explain the consequence of Boolean short-circuiting.


::: details Answer

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

:::

### Exercise 2: test the map laws {#exercise-02}

Implement `mapTreeWithFold`, then check the identity and composition laws for `emptyTree`, `leafTree`, and `branchTree`. Explain why checking three examples increases confidence but does not prove the law for every tree.


::: details Answer

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

Both values are `true` for the three examples. That detects several likely implementation mistakes, but the recursive type allows trees of arbitrary size and structure; three values cannot enumerate them all.

A proof follows the same structure. The laws hold directly for `Empty` and `Leaf`. For `Branch`, assume they hold for each smaller subtree, then show reconstruction preserves the combined result. This structural induction is the reasoning counterpart of structural recursion.

:::

### Exercise 3: compute one summary in one fold {#exercise-03}

Define a summary record containing `LeafCount`, `TotalSeats`, and `MaximumSeats : int option`. Compute it with one `foldTree` traversal. Give the correct summary for `Empty`, `Leaf 2`, and the shared branch tree, then state the time and direct stack bounds.


::: details Answer

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

:::


Chapter 11 examines how generic functions such as `mapTree` are inferred, where generalization stops, and which type constraints an operation introduces.

## Sources {#sources}

- [Microsoft Learn: Discriminated unions](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/discriminated-unions)
- [Microsoft Learn: Recursive functions and `rec`](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/functions/recursive-functions-the-rec-keyword)
- [Microsoft Learn: Functions](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/functions/)
