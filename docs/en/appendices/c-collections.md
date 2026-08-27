---
title: "Appendix C: Collection Choice and Complexity"
description: "Choose F# and .NET collections by evaluation, update, lookup, ordering, key rules, and documented complexity bounds rather than by familiar names."
translationKey: appendices/c-collections
---

# Appendix C: Collection Choice and Complexity {#overview}

Choosing a collection means choosing its behavior, not merely its type spelling. Ask when it produces elements, whether anyone may update its storage, how lookup works, what order callers can observe, and how it compares keys. Then use complexity to distinguish the remaining candidates.

The bounds below describe the implementations and official API contracts checked on 2026-08-25. `n` is collection size; `k` is a traversed prefix or index; `m` is another input size. Big O hides allocation, locality, comparer cost, element size, JIT behavior, and I/O. “Expected” and “amortized” are deliberately not “worst case.”

## Start with the decision table {#decision-table}

| Need | First candidate | Why | Reconsider when… |
|---|---|---|---|
| small/medium immutable ordered data, head-first transformation | `'T list` | persistent singly linked shape; pattern matching and prepend are natural | random indexing, repeated append, or cache locality dominates |
| fixed-size indexed mutable buffer or dense transformation | `'T array` | contiguous runtime array; O(1) index and length | size changes repeatedly or earlier versions must remain usable |
| general enumerable/deferred pipeline | `seq<'T>` | adapts `IEnumerable<'T>` and can produce on demand | repeatability, lifetime, one-shot sources, or materialization must be explicit |
| growable ordered mutable buffer | `ResizeArray<'T>` / `List<T>` | O(1) index and amortized O(1) append | shared mutation or persistent snapshots are required |
| immutable sorted unique values | `Set<'T>` | persistent binary tree, comparison order | equality-only keys or hash lookup better fits |
| immutable sorted key/value bindings | `Map<'Key, 'Value>` | persistent binary tree, comparison order | hot mutable updates or equality-only keys dominate |
| mutable key/value lookup | `Dictionary<'Key, 'Value>` | hash table with expected near-O(1) lookup | deterministic sorted order or persistent versions matter |
| mutable unique membership | `HashSet<'T>` | hash-based set operations | sorted iteration, duplicates, or persistent versions matter |

These are starting points. A ten-element array can be clearer than a list; a map can be preferable to a dictionary when avoiding shared mutation matters more than constant factors. Measure a representative operation only after the behavior fits.

## Answer five questions separately {#five-contracts}

For any collection, record:

1. **Production:** eager value, deferred enumerable, remote query, or one-shot stream?
2. **Update:** new value with structural sharing, new full copy, or in-place mutation?
3. **Access:** head, index, scan, comparison tree, or hash lookup?
4. **Order:** insertion/source order, comparison order, unspecified, or sorted only after an operation?
5. **Keys:** no key rule, structural equality, generic comparison, or an explicit comparer?

`seq<'T>` answers only “can produce an `IEnumerator<'T>`.” It does not answer whether enumeration is cheap, repeatable, finite, thread-safe, pure, or independent of an open resource.

## Evaluation and update summary {#evaluation-update}

| Collection | Evaluation/storage | Update behavior | Older value remains usable? |
|---|---|---|---|
| list | eager immutable linked nodes | prepend shares tail; transformations allocate a result spine | yes |
| array | eager fixed-size contiguous storage | element set mutates; size change needs another array | no for element mutation |
| sequence | producer-defined; transforms are often deferred | no common storage to update | source-defined |
| `ResizeArray` | eager resizable array-backed storage | methods mutate and may resize/copy capacity | no |
| `Map` / `Set` | eager immutable comparison tree | add/remove return a new tree and share unaffected structure | yes |
| `Dictionary` / `HashSet` | eager mutable hash buckets/entries | methods mutate and may resize/re-hash | no |

“Persistent” here means previous collection values remain valid after an update. It does not mean the data survives process failure or is stored durably.

Arrays are mutable, but `Array.map`, `Array.filter`, and `Array.sort` return arrays according to each function's behavior; explicitly named functions such as `Array.sortInPlace` mutate. Check the exact operation instead of applying one rule to the whole module.

## Lists: head-oriented persistent chains {#lists}

An F# list is an immutable singly linked list. Its useful cost model follows directly from that shape:

| Operation | Typical bound | Why/condition |
|---|---|---|
| `head :: tail` | O(1) | allocates one node and shares `tail` |
| `List.head` / `List.tail` | O(1) | reads first node; empty input fails |
| enumerate/map/fold/length | O(n) | visits every node |
| `List.item k` | O(k), therefore O(n) worst case | walks from the head |
| `left @ right` / `List.append left right` | O(length left) | copies the left spine and reuses `right` |
| reverse | O(n) | constructs a reversed spine |

Repeatedly appending one item to the end of a growing list produces quadratic total traversal. Prepend into an accumulator and reverse once, use a fold that constructs the desired order, or choose a growable buffer.

Structural sharing is not zero allocation: prepending allocates a node; mapping allocates a new spine; captured values can keep a large shared tail alive.

## Arrays and growable buffers {#arrays-resizearray}

| Operation | Array | `ResizeArray<'T>` / `List<T>` |
|---|---|---|
| index get/set | O(1) | O(1) |
| length/count | O(1) | O(1) |
| full scan/map | O(n) | O(n) scan |
| append one item | needs a new/copying operation, O(n) | amortized O(1), worst O(n) on resize |
| insert/remove in middle | copy/shift in a new or explicit mutable scheme, O(n) | shifts suffix, O(n) |
| snapshot copy | O(n) | O(n) |

Amortized append means that occasional capacity growth copies existing elements; it does not guarantee that every call is O(1). Set an initial capacity when a trustworthy upper estimate will avoid repeated resizing, but never allocate without a limit from an untrusted claimed size.

Dense contiguous storage often improves locality and interop. It also makes aliasing important: two names can refer to the same mutable array or list object. A type exposed as `seq<'T>` may still be backed by a mutable array that changes between enumerations.

## Sequences: an enumeration contract {#sequences}

Many `Seq` transformations return a deferred enumerable. Creating the pipeline can be O(1), while each later enumeration performs the work. Terminal operations such as `Seq.fold`, `Seq.toList`, or a complete `Seq.length` consume elements.

```fsharp:line-numbers [ch14-collections-evaluation.fsx]
let mutable pulls = 0

let delayedSquares =
    seq {
        for value in 1..3 do
            pulls <- pulls + 1
            yield value * value
    }

ensureEqual "deferred before enumeration" 0 pulls
printfn "Deferred before enumeration: pulls=%d" pulls

let firstPass = delayedSquares |> Seq.toList
ensureEqual "first values" [ 1; 4; 9 ] firstPass
ensureEqual "first pass count" 3 pulls
printfn "First enumeration: values=%A pulls=%d" firstPass pulls

let secondPass = delayedSquares |> Seq.toList
ensureEqual "second values" firstPass secondPass
ensureEqual "second pass repeats production" 6 pulls
printfn "Second enumeration: values=%A pulls=%d" secondPass pulls
```
For a finite sequence producing `n` elements:

- a complete scan is O(n) plus producer and callback cost;
- reaching item `k` is O(k) unless the concrete source exposes a separately used indexer;
- `Seq.take k` is deferred, but consuming it still asks for up to `k` values;
- sorting, grouping, reversing, and many set-like operations must buffer data;
- a source may be infinite, throw midway, read live state, perform I/O, or permit only one enumeration.

`Seq.cache` memoizes values as they are requested and avoids reproducing cached prefixes. It also retains both the cached values and the source state, so it is not a universal performance switch. Materialize once with `Seq.toList` or `Seq.toArray` when you actually need a bounded snapshot.

Do not call `Seq.length` and then enumerate merely to test emptiness. Use a one-pass decision such as `Seq.isEmpty`, or materialize when both count and contents are required and bounded.

## Map and Set: comparison-ordered persistent trees {#map-set}

FSharp.Core documents `Map` and `Set` as immutable binary-tree collections ordered by F# generic comparison. Their types have a `comparison` constraint.

```fsharp:line-numbers [ch14-collections-evaluation.fsx]
let uniqueSeats = [ 3; 1; 3; 2 ] |> Set.ofList

let bookingByCode =
    [ "B2", "first"; "A1", "only"; "B2", "replacement" ] |> Map.ofList

ensureEqual "set removes duplicates and orders" [ 1; 2; 3 ] (Set.toList uniqueSeats)
ensureEqual "later map binding replaces earlier" "replacement" bookingByCode["B2"]

printfn "Ordered collections: set=%A map=%A" (Set.toList uniqueSeats) (Map.toList bookingByCode)
```
| Operation | `Map` / `Set` documented bound | Condition |
|---|---|---|
| find/tryFind/contains | O(log n) | tree comparison path |
| add/remove/change | O(log n) | returns a new collection |
| enumerate/map values | O(n) | comparison order; mapping keys/rebuilding differs |
| `Map.count` / `Set.count` | O(n) in current FSharp.Core documentation | do not assume a cached count |
| build via ordinary `ofList`/`ofArray` | O(n log n) documented | repeated tree insertion |
| filter | O(n log n) documented | result tree is rebuilt |

Enumeration follows key or element comparison order, not insertion order. Changing the comparison rule—or a representation used by structural comparison—can change both the visible order and which keys compare as equal.

The `comparison` constraint is stronger than equality. Functions, types marked `NoComparison`, and equality-only domain keys cannot be used directly. If ordering is not part of the domain, a hash collection with an explicit equality comparer may express the requirement better.

## Dictionary and HashSet: mutable hash identity {#hash-collections}

.NET documents dictionary key retrieval as very fast, close to O(1), with speed dependent on hash quality. The .NET collection complexity table distinguishes amortized/expected O(1) from O(n) worst cases for hash insertion and lookup.

```fsharp:line-numbers [ch14-collections-evaluation.fsx]
[<CustomEquality; NoComparison>]
type EmailAddress =
    { Value: string }

    override this.Equals(other: obj) =
        match other with
        | :? EmailAddress as candidate -> StringComparer.OrdinalIgnoreCase.Equals(this.Value, candidate.Value)
        | _ -> false

    override this.GetHashCode() =
        StringComparer.OrdinalIgnoreCase.GetHashCode(this.Value)

let recipients = Dictionary<EmailAddress, string>()
recipients[{ Value = "lin@example.com" }] <- "first"
recipients[{ Value = "LIN@example.com" }] <- "second"

ensureEqual "hash equality replaces value" 1 recipients.Count
ensureEqual "case-insensitive lookup" "second" recipients[{ Value = "Lin@Example.com" }]
printfn "Hash dictionary: count=%d lookup=%s" recipients.Count recipients[{ Value = "Lin@Example.com" }]
```
| Operation | Expected/amortized | Worst case or caveat |
|---|---|---|
| dictionary lookup/add | O(1) | O(n) with collisions or resize path |
| hash-set membership/add | O(1) | O(n) with collisions or resize path |
| count | O(1) | says nothing about traversal cost |
| enumerate | O(n) | order is not a portable semantic contract |
| contains value in dictionary | O(n) | values are not the hash key |

Every key must remain stable under the collection's `IEqualityComparer<'T>` while it is stored. If equality says two keys are equal, their hash codes must agree. Mutating a field that affects equality or hashing while the key is stored can make an entry unreachable or make lookup results incorrect.

There is no F# `comparison` constraint on `Dictionary` or `HashSet`; their CLR APIs use a supplied comparer or `EqualityComparer<'T>.Default`. This flexibility moves correctness from a compile-time constraint to the comparer and key design.

`HashSet` explicitly has no particular order. Do not expose the current `Dictionary` enumeration behavior as a stable sorted or insertion-order API unless another documented layer creates and tests that order. Sort before returning data when callers depend on its order.

Ordinary mutable collections are not automatically safe for concurrent writes. Confine a collection to one owner, synchronize access, use immutable snapshots, or choose a concurrent collection whose atomic methods can enforce the whole rule. A “thread-safe method” does not make a multi-step check-then-act sequence atomic.

## Equality, comparison, and keys {#key-contracts}

| Collection/operation | Required equality or ordering rule |
|---|---|
| list/array/sequence traversal | none merely to store/enumerate |
| `List.contains`, `Array.distinct`, grouping, etc. | equality/hash as required by the exact operation |
| `Map<'K, 'V>` / `Set<'T>` | F# generic comparison |
| `Dictionary<'K, 'V>` / `HashSet<'T>` | equality comparer plus compatible stable hash |
| sorting | comparison or supplied comparer/function |

Do not derive a key comparer from display formatting, current culture, unstable timestamps, mutable fields, or a lossy normalization. For strings, decide ordinal, ordinal-ignore-case, culture-aware, or domain normalization explicitly. Persisted or cross-process keys also need a versioned representation independent of in-memory hash codes.

Equality can be valid even when ordering is unavailable. Chapter 14's `EmailAddress` intentionally uses case-insensitive equality and `NoComparison`: a dictionary fits, while the compiler prevents an unsuitable `Map`.

## Order is part of an API only when stated {#ordering}

| Source | Observable order |
|---|---|
| list/array/`ResizeArray` | index/source order until explicit mutation/reorder |
| sequence | whatever each enumeration's producer emits |
| `Map`/`Set` | F# generic comparison order |
| `Dictionary` | do not rely on enumeration order as portable domain semantics |
| `HashSet` | no particular order |
| sort operation | comparer order, including tie behavior documented by that operation |

Deterministic output often needs a final explicit sort even when internal lookup uses hashing. Stable sorting is a separate promise: equal-key elements preserve input order only when the chosen function documents stability.

## Conversion usually traverses and allocates {#conversion}

`List.toArray`, `Array.toList`, `Seq.toList`, `Set.ofSeq`, and similar functions allocate/materialize according to the destination. Typical list/array snapshot conversion is O(n); ordinary map/set construction is documented as O(n log n).

A conversion can:

- force a deferred or one-shot source;
- snapshot mutable state;
- remove duplicates;
- reorder by comparison;
- replace earlier duplicate key bindings;
- allocate another full representation;
- adopt the destination's equality or comparison rules.

Convert once when data enters another layer or API, not repeatedly inside a loop or property. State why the destination collection is needed.

## Read complexity claims precisely {#complexity-rules}

1. Name the operation, collection type, runtime/FSharp.Core version, and `n`.
2. State expected, amortized, average, or worst-case explicitly.
3. Include callback, comparer, hash, producer, allocation, and I/O cost when material.
4. Distinguish pipeline construction from enumeration.
5. State whether an update mutates, copies fully, or structurally shares.
6. Include order and duplicate behavior; speed alone does not preserve meaning.
7. Benchmark a representative size and access pattern only after the collection's behavior fits the requirement.

“Dictionary lookup is O(1)” is incomplete. “Expected near-O(1) lookup under a stable, well-distributed equality/hash comparer; O(n) worst case; no sorted-order promise” is actionable.

## Common selection failures {#common-failures}

- Using a list as an indexed table or append-only buffer.
- Choosing `seq` to appear memory-efficient while enumerating it several times.
- Caching an unbounded sequence and retaining every produced value.
- Returning a mutable array as `seq` and assuming the type makes a snapshot.
- Choosing `Map` only for immutability when the key has no meaningful comparison.
- Choosing `Dictionary` for speed while exposing accidental enumeration order.
- Mutating fields that participate in a stored hash key.
- Converting collections repeatedly to reach a familiar module function.
- Treating persistent in-memory structures as durable storage.
- Optimizing asymptotic cost before measuring the actual data size and hot operation.

Return to [Chapter 14](../part-03/ch-14-collections-evaluation) for executable evaluation examples, [Chapter 24](../part-04/ch-24-concurrency-agents-state) for collection ownership under concurrency, and [Chapter 31](../part-05/ch-31-measure-before-optimizing) for measurement discipline.

## Official entry points {#official-entry-points}

- [Microsoft Learn: F# collection types](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/fsharp-collection-types)
- [Microsoft Learn: F# lists](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/lists)
- [Microsoft Learn: F# sequences](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/sequences)
- [FSharp.Core collections namespace](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-collections.html)
- [FSharp.Core List module](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-collections-listmodule.html)
- [FSharp.Core Seq module](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-collections-seqmodule.html)
- [FSharp.Core Map module](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-collections-mapmodule.html)
- [FSharp.Core Set module](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-collections-setmodule.html)
- [.NET collections and algorithmic complexity](https://learn.microsoft.com/en-us/dotnet/standard/collections/)
- [.NET `List<T>`](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.list-1?view=net-10.0)
- [.NET `Dictionary<TKey,TValue>`](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2?view=net-10.0)
- [.NET `HashSet<T>`](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.hashset-1?view=net-10.0)
