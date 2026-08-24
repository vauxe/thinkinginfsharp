---
title: "Chapter 14 Solutions"
description: "Select collections by workload, calculate deferred demand precisely, and separate ordered keys from equality-based hash keys."
translationKey: solutions/ch-14-collections-evaluation
kind: solution
part: 3
chapter: 14
status: complete
verifiedWith:
  fsharp: "10"
  dotnetSdk: "10.0.301"
exampleIds:
  - ch14-collections-evaluation
exerciseIds:
  - ch14-exercise-01
  - ch14-exercise-02
  - ch14-exercise-03
termIds: []
sources:
  - id: microsoft-collection-types
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/fsharp-collection-types
    checked: "2026-08-24"
  - id: microsoft-sequences
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/sequences
    checked: "2026-08-24"
  - id: fsharp-core-map
    url: https://fsharp.github.io/fsharp-core-docs/reference/fsharp-collections-mapmodule.html
    checked: "2026-08-24"
  - id: fsharp-core-set
    url: https://fsharp.github.io/fsharp-core-docs/reference/fsharp-collections-setmodule.html
    checked: "2026-08-24"
  - id: dotnet-dictionary
    url: https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2?view=net-10.0
    checked: "2026-08-24"
  - id: dotnet-hashset
    url: https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.hashset-1?view=net-10.0
    checked: "2026-08-24"
---

# Chapter 14 Solutions {#overview}

State the dominant operation, the required semantics, and the conversion boundary. A type name without that reasoning is only a guess.

[Return to Chapter 14](../part-03/ch-14-collections-evaluation).

## Exercise 1: select by workload {#exercise-01}

### 1. Immutable head/tail command processing {#exercise-01-list}

Start with `Command list`. The batch is modest, already available, immutable, and consumed according to the list's recursive shape. Prepending and head/tail matching fit the representation.

If the input arrives as `seq<Command>`, materialize once after validating any size limit:

```fsharp
let commands = incoming |> Seq.toList
```

This declares that processing uses a stable snapshot. It would be wasteful to convert back to `seq` between every list operation.

### 2. Indexed seat occupancy {#exercise-01-array}

Start with `bool array` or a richer status array. The seat domain is fixed for the event, and indexed reads and local updates dominate:

```fsharp
let occupied = Array.create capacity false
occupied[seatIndex] <- true
```

Keep this mutation inside a narrow owner. At an outward boundary, return a copied array, an immutable summary, or domain events according to the API contract; exposing the working array would let callers mutate internal state.

### 3. First ten candidate allocations {#exercise-01-sequence}

Start with `seq<Allocation>` because production may be large and the consumer intentionally stops early:

```fsharp
let selected =
    generateCandidates request
    |> Seq.filter isValid
    |> Seq.truncate 10
    |> Seq.toList
```

The final list is the small accepted snapshot. `Seq.truncate` is preferable to `Seq.take` when fewer than ten candidates is a normal result, because `take` fails when the source is too short.

### 4. Immutable lookup and ordered report {#exercise-01-map}

Start with `Map<ConfirmationCode, Booking>` if `ConfirmationCode` has stable comparison semantics. `Map.tryFind` provides immutable lookup, and `Map.toList` produces key-comparison order without a separate sort.

If business order differs from the type's generic comparison—for example, chronological confirmation time—store that information explicitly and sort or index by the business key. Do not pretend generic structural order has domain meaning it lacks.

### 5. Case-insensitive mutable email membership {#exercise-01-hashset}

Start with a .NET `HashSet<string>` and provide the equality rule explicitly:

```fsharp
open System
open System.Collections.Generic

let attendees = HashSet<string>(StringComparer.OrdinalIgnoreCase)
attendees.Add("Lin@example.com") |> ignore
```

No total ordering is required. If an alphabetic report is needed only at output, project to strings and sort there. If the set crosses an ownership boundary, copy it or expose a read-only result rather than sharing mutable state.

## Exercise 2: predict demand and caching {#exercise-02}

Immediately after defining `values`, `reads` is `0`: no consumer has requested an element.

Without caching:

1. `Seq.take 2 |> Seq.toList` requests two elements, so `reads = 2` and `firstTwo = [ 2; 4 ]`;
2. `values |> Seq.toList` starts another enumeration and requests all three, so `reads = 5` and `all = [ 2; 4; 6 ]`.

The second traversal does not resume after the earlier two. It asks this sequence expression for a new enumerator and production begins again.

With a fresh counter and a cached sequence:

```fsharp
let cached = values |> Seq.cache
let firstTwo = cached |> Seq.take 2 |> Seq.toList
let all = cached |> Seq.toList
```

After `firstTwo`, `reads = 2`. During `all`, the first two values come from the cache and only the third is newly produced, so the final count is `3`.

Choose the exposed meaning deliberately:

- **fresh enumeration:** retain the uncached sequence when the source is cheap, pure, restartable, and current observations are wanted;
- **cached replay:** use `Seq.cache` when deferred prefix consumption and replay of the same produced values are both required;
- **complete snapshot:** use `Seq.toList` or `Seq.toArray` at the boundary when all work should complete once and later traversal must be predictable.

For an effectful or resource-backed source, “fresh enumeration” requires an explicit source contract. The type `seq<'T>` alone is insufficient evidence.

## Exercise 3: order versus equality {#exercise-03}

`Map<'Key,'Value>` and `Set<'T>` navigate ordered trees, so their keys or elements must satisfy F# `comparison`. `[<NoComparison>]` explicitly prevents that constraint, and the compiler rejects the ordered collection.

`Dictionary<'Key,'Value>` and `HashSet<'T>` instead use an `IEqualityComparer`—or the type's default equality and hash implementation. They need to determine a bucket and then whether a candidate is equal; they do not need to decide which value is less than another.

For a context-specific case-insensitive email set, prefer a comparer at the collection boundary:

```fsharp
let emails =
    System.Collections.Generic.HashSet<string>(
        System.StringComparer.OrdinalIgnoreCase
    )
```

An occasional sorted report can project a sortable representation and order it explicitly:

```fsharp
let report =
    emails
    |> Seq.sortWith (fun left right ->
        System.StringComparer.OrdinalIgnoreCase.Compare(left, right))
    |> Seq.toList
```

This sort materializes ordering work for the report; it does not turn the hash set into an ordered collection. If the domain key is a protected equality-only type, project its normalized display value before sorting instead of adding a meaningless comparison merely to satisfy `Map`.

The mandatory hash rule is one-way:

```text
if comparer.Equals(left, right), then comparer.GetHashCode(left) = comparer.GetHashCode(right)
```

Unequal values may share a hash code. A collision can reduce performance, but equality still distinguishes the values. Never use a hash code itself as proof of identity or as a sorting key.

## What to notice {#what-to-notice}

- **Representation follows repeated operations:** one unusual call should not dictate the whole data shape.
- **Materialization communicates meaning:** it is often a useful snapshot boundary, not an automatic failure of functional style.
- **Caching is incremental:** an already produced prefix is replayed and a later traversal can continue producing the remainder.
- **Comparison and hashing are different contracts:** an ordered tree needs a stable total order; a hash table needs compatible equality and hash codes.
- **Mutation needs ownership:** arrays and .NET hash collections are effective local tools when callers cannot mutate them unexpectedly.
