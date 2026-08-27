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

```fsharp:line-numbers [ch10-recursive-types.fsx]
type BookingTree<'T> =
    | Empty
    | Leaf of 'T
    | Branch of left: BookingTree<'T> * right: BookingTree<'T>

let emptyTree: BookingTree<int> = Empty
let leafTree = Leaf 2

let branchTree = Branch(Leaf 2, Branch(Leaf 3, Leaf 4))
```
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

```fsharp:line-numbers [ch10-recursive-types.fsx]
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
`let rec` makes the function name available inside its own body. Both functions have the same structural skeleton:

- `Empty` is a base case and makes no recursive call;
- `Leaf` handles its payload and makes no recursive call;
- `Branch` calls the function on `left` and `right`, then combines the two results.

This is **structural recursion**. Each recursive call receives a direct component of the matched value, so for an ordinary finite tree it progresses toward `Empty` or `Leaf`. Termination is visible in the relationship between the type and the function rather than hidden in a numeric counter.

The result rule still depends on the question. `countLeaves` gives every leaf `1`; `totalSeats` gives it its payload. Both give `Empty` the identity `0` and combine branch results with addition.

## `map` changes payloads and preserves structure {#tree-map}

A tree map handles every case but changes only leaves:

```fsharp:line-numbers [ch10-recursive-types.fsx]
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
Its inferred type is:

```fsharp
mapTree : ('T -> 'U) -> BookingTree<'T> -> BookingTree<'U>
```

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

```fsharp:line-numbers [ch10-recursive-types.fsx]
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
Read its arguments from the type:

```fsharp
foldTree :
    onEmpty:'State ->
    onLeaf:('T -> 'State) ->
    onBranch:('State -> 'State -> 'State) ->
    tree:BookingTree<'T> ->
    'State
```

The fold replaces each constructor with a caller-supplied rule. `Empty` becomes `onEmpty`. A leaf becomes `onLeaf value`. A branch first folds both subtrees, then combines their results with `onBranch`.

This removes explicit recursion from each caller, not the traversal work. Every node is still visited. `countWithFold` and `totalWithFold` differ only in their three rules, and the script checks that they agree with the direct definitions.

A fold can produce more than numbers. Choose `'State` as a record to compute count, total, and maximum together; choose it as another tree to reconstruct structure; choose it as a function when building a more specialized traversal. The type signature tells you every rule must return the same state type.

### Derive `map` from `fold` {#map-from-fold}

Once `foldTree` is trusted, map can be expressed without writing `let rec` again:

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

```fsharp:line-numbers [ch10-recursive-types.fsx]
let rec height tree =
    match tree with
    | Empty -> 0
    | Leaf _ -> 1
    | Branch(left, right) -> 1 + max (height left) (height right)

printfn "Heights: empty=%d leaf=%d branch=%d" (height emptyTree) (height leafTree) (height branchTree)

printfn "Shape preserved: before=%d after=%d" (countLeaves branchTree) (countLeaves labeledTree)
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

Use mutual recursion only when the domain genuinely has two distinct concepts. A single recursive union is easier to traverse and should not be split merely to demonstrate the syntax. Mutually recursive functions use the corresponding `let rec ... and ...` form.

## Run the shared example {#run-example}

From the directory containing the example:

```console
dotnet fsi --exec ch10-recursive-types.fsx
```

The six deterministic lines cover empty, leaf, and branch trees; direct traversals; a type-changing map; fold-derived queries; height; and preservation of leaf count.

## Exercises {#exercises}

### Exercise 1: derive a query from the cases {#exercise-01}

Write `exists : ('T -> bool) -> BookingTree<'T> -> bool` by structural recursion. State the rule for each case before writing code. Should the branch implementation always visit the right subtree? Explain the consequence of Boolean short-circuiting.

### Exercise 2: test the map laws {#exercise-02}

Implement `mapTreeWithFold`, then check the identity and composition laws for `emptyTree`, `leafTree`, and `branchTree`. Explain why checking three examples increases confidence but does not prove the law for every tree.

### Exercise 3: compute one summary in one fold {#exercise-03}

Define a summary record containing `LeafCount`, `TotalSeats`, and `MaximumSeats : int option`. Compute it with one `foldTree` traversal. Give the correct summary for `Empty`, `Leaf 2`, and the shared branch tree, then state the time and direct stack bounds.

[Read the chapter solutions](../solutions/ch-10-recursive-types).

## Model review {#model-review}

- A recursive type expresses arbitrarily nested but ordinarily finite values.
- Structural recursion mirrors the type's cases and recurs into direct recursive fields.
- `map` changes the payload type while preserving the tree structure.
- `fold` exposes one rule per constructor and centralizes recursive plumbing.
- Node count predicts traversal work; maximum height predicts direct call-stack depth.
- Enforce an input-depth limit when trees may be untrusted or extremely deep.

Chapter 11 examines how generic functions such as `mapTree` are inferred, where generalization stops, and which type constraints an operation introduces.

## Sources {#sources}

- [Microsoft Learn: Discriminated unions](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/discriminated-unions)
- [Microsoft Learn: Recursive functions and `rec`](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/functions/recursive-functions-the-rec-keyword)
- [Microsoft Learn: Functions](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/functions/)
