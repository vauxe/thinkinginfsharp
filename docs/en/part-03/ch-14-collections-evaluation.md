---
title: "Chapter 14: Choosing Collections and Evaluation Models"
description: "Choose list, array, sequence, map, set, or a .NET hash collection by representation, evaluation timing, lookup semantics, and conversion cost."
translationKey: part-03/ch-14-collections-evaluation
---

# Chapter 14: Choosing Collections and Evaluation Models {#overview}

A collection type is not merely different punctuation around the same elements. It determines the data representation, evaluation timing, available updates, key-matching rule, and expected costs. Using `seq` everywhere discards useful guarantees; using `list` everywhere can hide indexing or lookup work.

We will choose from F# `list`, array, `seq`, `Map`, and `Set` by starting with the required operations. We will also use .NET `Dictionary` and `HashSet` when the requirement is equality-based hashing.

## Begin with the dominant operation {#decision-first}

Ask what the consumer will do most often:

1. decompose a small immutable series from the front;
2. index or update a fixed-size block;
3. request only part of a potentially large producer;
4. look up immutable values by ordered keys;
5. maintain unique ordered elements;
6. perform mutable equality-based lookup with a custom comparer.

Those answers point to different representations. “It contains several values” is not enough information.

## Five core collection forms {#five-shapes}

### List: immutable structure from the front {#list}

An F# list is an immutable singly linked structure. `head :: tail` is constant-time construction and matches the recursive structure taught in Chapters 4–6. `List.map` and `List.filter` eagerly traverse the input and allocate a result list.

A list is a strong default for a modest, already available batch that is transformed sequentially or decomposed from the front. It is poor for repeated indexing: reaching item *i* walks through preceding nodes. Repeated end-appends also fight its design; prefer prepending then reversing, a fold, or another builder.

### Array: fixed extent and indexed storage {#array}

An array is a fixed-size, zero-based .NET array whose elements occupy consecutive storage. Element lookup and replacement by index are constant-time. The binding can remain immutable while the object it references is changed:

```fsharp
let seats = [| false; false; false |]
seats[1] <- true
```

Use an array for indexed algorithms, dense fixed snapshots, numeric work, or APIs that naturally exchange arrays. `Array.map` returns a new array eagerly; it does not mutate its input. Slices create copies. Copying an array is shallow, so reference-type elements still refer to the same underlying objects.

The shared script places list and array behavior side by side:

```fsharp:line-numbers [ch14-collections-evaluation.fsx]
let source = [ 1; 2; 3 ]
let doubledList = source |> List.map ((*) 2)
let doubledArray = source |> List.toArray |> Array.map ((*) 2)
doubledArray[0] <- 20

ensureEqual "list stays immutable" [ 2; 4; 6 ] doubledList
ensureEqual "array element changes" [| 20; 4; 6 |] doubledArray
ensureEqual "source stays unchanged" [ 1; 2; 3 ] source
printfn "Eager: list=%A array=%A source=%A" doubledList doubledArray source
```
Neither representation is universally “faster.” The dominant access pattern, allocation profile, element type, and measured workload decide.

### Sequence: an enumeration contract, not stored data {#sequence}

`seq<'T>` abbreviates `System.Collections.Generic.IEnumerable<'T>`. It describes how a consumer requests elements. It guarantees neither stored data, purity, cheap enumeration, nor the same external observations on repeated traversal.

Many values can be viewed as sequences: lists, arrays, maps, sets, and most .NET enumerable collections. Accepting `seq<'T>` therefore makes a read-only consumer broadly usable. It also provides fewer guarantees than accepting an array or list: there is no general constant-time count, index, snapshot, or restartability promise.

Use a sequence when on-demand production matters, a consumer may stop early, or an API naturally receives `IEnumerable<'T>`. Do not use it merely to maximize abstraction; require the guarantees that the implementation and callers actually need.

### Map and Set: immutable lookup in comparison order {#map-and-set}

`Map<'Key,'Value>` stores one value per key; `Set<'T>` stores unique elements. Both are immutable tree-based collections. Adding or removing returns a new collection and leaves the old value usable.

Their defining constraint is ordering:

```fsharp
Map<'Key, 'Value>  // 'Key : comparison
Set<'T>            // 'T : comparison
```

Lookup, insertion, and membership are logarithmic in collection size for these tree implementations. Enumeration follows F# generic comparison order, not insertion order. A later binding for a key replaces the earlier map binding; values that compare as the same set element collapse to one element.

Choose `Map` when immutable lookup and deterministic key-ordered traversal are both useful. Choose `Set` for immutable membership, deduplication, and set algebra when elements have a meaningful, stable comparison.

## Evaluation timing is observable {#evaluation}

Creating and transforming lists and arrays normally performs the traversal immediately. Many `Seq` producers and transformations defer work until enumeration. “Lazy” therefore determines not only performance, but also when exceptions, state reads, I/O, and other side effects occur.

### A sequence expression defines a producer {#sequence-expression}

A sequence expression uses `seq { ... }` to describe how to yield elements:

```fsharp
let candidateSeatCounts maximum =
    seq {
        for seats in 1..maximum do
            if seats % 2 = 1 then
                yield seats
    }
```

Calling `candidateSeatCounts 1_000_000` creates a sequence value; it does not immediately build one million candidates. A consumer such as `Seq.truncate 3 >> Seq.toList` can request only a prefix. `yield!` can contribute every element from an inner sequence.

The body is executable code, not inert data. Its side effects run when elements are requested.

### Re-enumeration can repeat production {#repeated-enumeration}

The shared example makes evaluation visible with a counter:

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
The counter remains zero after constructing `delayedSquares`. The first `Seq.toList` pulls three elements; the second starts a new enumeration of this sequence expression and runs the body three more times.

That observation does not mean every `IEnumerable<'T>` is safely restartable. A concrete source controls its enumerators: it may query changing state, wrap a resource, be single-use by convention, or throw on another traversal. The `seq<'T>` type alone promises none of those behaviors.

### The operator determines how much is demanded {#operation-timing}

Many `Seq` transformations—such as `map`, `filter`, and `choose`—produce another deferred sequence. Consumers such as `toList`, `toArray`, `fold`, and iteration demand elements. Search and prefix operations can stop early. Sorting and grouping must inspect enough input to organize the result and usually buffer data before yielding useful output.

Do not infer evaluation from the `Seq.` prefix alone. Read the operation documentation and identify the terminal consumer. An unbounded sequence can be safe with `Seq.truncate 10`, yet impossible to pass to `Seq.toList` or a full sort.

### Cache replay, or materialize a snapshot {#cache-or-materialize}

`Seq.cache` computes elements as demanded and remembers them for later enumeration:

```fsharp:line-numbers [ch14-collections-evaluation.fsx]
let mutable cachedPulls = 0

let cachedSquares =
    seq {
        for value in 1..3 do
            cachedPulls <- cachedPulls + 1
            yield value * value
    }
    |> Seq.cache

let cachedFirst = cachedSquares |> Seq.toList
let cachedSecond = cachedSquares |> Seq.toList

ensureEqual "cached values" cachedFirst cachedSecond
ensureEqual "cached production count" 3 cachedPulls
printfn "Cached enumerations: first=%A second=%A pulls=%d" cachedFirst cachedSecond cachedPulls
```
Caching is appropriate when one deferred calculation must be replayed and retaining its produced elements is acceptable. It is not a universal optimization: the cache consumes memory, preserves earlier observations instead of fetching fresh ones, and can grow without bound for a long or infinite source.

When the program means “capture all values now,” say so with `Seq.toList` or `Seq.toArray`. A materialized snapshot has a clear completion point and predictable replay, at the cost of one full traversal and storage for all elements.

## Conversions change behavior and cost {#conversions}

A conversion may allocate, enumerate, copy references, change update rules, discard duplicates, or impose ordering. Name it in reasoning and place it deliberately:

- `List.toArray` allocates indexed storage and copies the element values;
- `Array.toList` allocates list nodes and captures the array's current element values;
- `Seq.toList` and `Seq.toArray` enumerate now and materialize all produced elements;
- viewing a list or array as `seq` does not create an independent immutable snapshot;
- `Set.ofSeq` enumerates and removes comparison-equal duplicates;
- `Map.ofSeq` enumerates key-value pairs and retains one binding per comparison-equal key.

These copies are shallow. If an element is a reference to a mutable object, both collections can still point to that same object. The shared array-to-list conversion proves independence of collection slots, not deep cloning:

```fsharp:line-numbers [ch14-collections-evaluation.fsx]
let mutableArray = [| 1; 2; 3 |]
let listSnapshot = mutableArray |> Array.toList
mutableArray[0] <- 99

ensureEqual "list is an independent snapshot" [ 1; 2; 3 ] listSnapshot
printfn "Conversion snapshot: array=%A list=%A" mutableArray listSnapshot
```
Avoid chains that repeatedly bounce among `list`, array, and `seq` just to call a familiar module function. Keep the representation suited to the workflow, or convert once at a deliberate point.

## Ordered keys and hash keys answer different questions {#lookup-semantics}

The ordered collections in the script expose comparison order directly:

```fsharp:line-numbers [ch14-collections-evaluation.fsx]
let uniqueSeats = [ 3; 1; 3; 2 ] |> Set.ofList

let bookingByCode =
    [ "B2", "first"; "A1", "only"; "B2", "replacement" ] |> Map.ofList

ensureEqual "set removes duplicates and orders" [ 1; 2; 3 ] (Set.toList uniqueSeats)
ensureEqual "later map binding replaces earlier" "replacement" bookingByCode["B2"]

printfn "Ordered collections: set=%A map=%A" (Set.toList uniqueSeats) (Map.toList bookingByCode)
```
`Map` and `Set` need a total ordering to navigate their trees. That is why their type parameters carry `comparison`, not merely `equality`. The order must also be stable while a value acts as a key or element.

### Hash collections need equality and a compatible hash code {#hash-collections}

.NET `Dictionary<'Key,'Value>` and `HashSet<'T>` organize values by an `IEqualityComparer`, not a total ordering. Equal values must produce the same hash code; unequal values may collide, after which equality distinguishes them. Mutable state that affects equality or hashing must not change while a value is stored as a key.

The script defines an equality-only key with `[<NoComparison>]`:

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
The dictionary accepts the key because its equality and hash semantics suffice. Attempting `Map<EmailAddress,string>` produces FS0001: the type explicitly does not support the `comparison` constraint. This is a real capability difference, not merely a performance choice.

For context-specific rules such as case-insensitive strings, supplying an `IEqualityComparer` to `Dictionary` or `HashSet` is often better than changing the domain type's global equality. The script embeds the rule only to make the equality-only constraint visible.

### Choose semantics before complexity {#hash-or-tree}

F# `Map` and `Set` are immutable and ordered, with logarithmic tree operations. .NET `Dictionary` and `HashSet` are mutable and provide hash-based lookup that is close to constant time when hashing is well distributed. The latter do not guarantee sorted enumeration.

Choose based on required semantics first:

- require immutable updates and key-ordered traversal: `Map` or `Set`;
- require custom equality, no ordering, and controlled local mutation: `Dictionary` or `HashSet`;
- require an occasional sorted report from a hash collection: sort a projection explicitly when producing the output;
- require persistent data with custom equality: consider an immutable hash collection from the .NET ecosystem, and make its comparer part of the design.

Only then benchmark a representative workload. Big-O notation does not account for collection size, allocation, cache locality, comparer cost, or concurrency.

## Compact decision table {#decision-table}

| Dominant need | Start with | Important check |
|---|---|---|
| Immutable front-first processing and pattern matching | `list<'T>` | Repeated indexing or end append suggests another representation |
| Fixed-size indexed data or array-based .NET interop | `'T array` | Elements are mutable; copies are shallow |
| On-demand production or early termination | `seq<'T>` | Enumeration timing, repeatability, lifetime, and buffering |
| Immutable key lookup with deterministic sorted traversal | `Map<'K,'V>` | Keys require stable F# comparison |
| Immutable uniqueness and ordered set algebra | `Set<'T>` | Elements require stable F# comparison |
| Mutable lookup with custom equality | `Dictionary` / `HashSet` | Equality and hash code must agree; sorted order is not guaranteed |

The table is a starting point, not a ban on conversion. A program can receive a `seq`, validate and materialize it once as an array, then expose an immutable result. Every conversion should have a reason.

## Run the shared example {#run-example}

From the directory containing the example:

```console
dotnet fsi --exec ch14-collections-evaluation.fsx
```

Eight deterministic lines and executable assertions cover eager list/array behavior, deferred and repeated sequence enumeration, caching, ordered `Map`/`Set` behavior, an equality-only dictionary key, and a conversion snapshot.

## Exercises {#exercises}

### Exercise 1: select by workload {#exercise-01}

Choose a starting collection and justify it for each case:

1. a modest immutable command batch processed by head/tail recursion;
2. a fixed seat-occupancy table updated and read repeatedly by numeric index;
3. generated candidate allocations from which only the first ten valid values are needed;
4. an immutable booking lookup that must also produce reports in confirmation-code order;
5. a mutable set of attendee emails with case-insensitive membership and no ordering requirement.

State any conversion you would place at an input or output boundary.

### Exercise 2: predict demand and caching {#exercise-02}

Without running this code, predict `reads` after each materialization:

```fsharp
let mutable reads = 0

let values =
    seq {
        for value in 1..3 do
            reads <- reads + 1
            yield value * 2
    }

let firstTwo = values |> Seq.take 2 |> Seq.toList
let all = values |> Seq.toList
```

Then insert `let cached = values |> Seq.cache`, consume `cached` instead, and predict again. Explain which meaning—fresh enumeration, cached replay, or complete snapshot—the calling code should expose.

### Exercise 3: order versus equality {#exercise-03}

A domain key deliberately supports case-insensitive equality and hashing but has `[<NoComparison>]`. Explain why it can be used in `Dictionary` or `HashSet` but not `Map` or `Set`. Design an occasional alphabetic report without weakening the key's type contract, and state the rule that its equality and hash code must obey.

[Read the chapter solutions](../solutions/ch-14-collections-evaluation).

## Model review {#model-review}

- A collection chooses data representation, evaluation timing, update rules, and lookup semantics.
- Lists and arrays are eager stored collections; arrays additionally offer mutable indexed slots.
- A sequence is an enumeration contract, not a promise of storage, purity, replay, or cheap work.
- Re-enumerating a deferred producer may repeat work and side effects; cache or materialize only when that is the intended meaning.
- Conversions have traversal, allocation, mutability, ordering, and duplicate-handling consequences.
- `Map` and `Set` use generic comparison and ordered trees; hash collections use equality plus compatible hash codes.
- Required semantics select the family; measurements refine the choice.

Chapter 15 introduces active patterns. A matching abstraction should expose domain categories without hiding expensive evaluation or failures.

## Sources {#sources}

- [Microsoft Learn: F# collection types](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/fsharp-collection-types)
- [Microsoft Learn: lists](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/lists)
- [Microsoft Learn: arrays](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/arrays)
- [Microsoft Learn: sequences](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/sequences)
- [FSharp.Core: collection namespace](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-collections.html)
- [FSharp.Core: Map module](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-collections-mapmodule.html)
- [FSharp.Core: Set module](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-collections-setmodule.html)
- [Microsoft Learn: `Dictionary<TKey,TValue>`](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2?view=net-10.0)
- [Microsoft Learn: `HashSet<T>`](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.hashset-1?view=net-10.0)
