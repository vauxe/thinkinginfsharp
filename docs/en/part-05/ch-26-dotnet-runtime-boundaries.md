---
title: "Chapter 26: Deeper .NET Boundaries"
description: "Cross runtime type, delegate, event, collection, identity, equality, and hashing boundaries without losing F# domain semantics."
translationKey: part-05/ch-26-dotnet-runtime-boundaries
---

# Chapter 26: Deeper .NET Boundaries {#overview}

F# values run in the .NET type system. A record can be boxed as `obj`, a function can be adapted to a delegate, an F# event can be published as a CLI event, and a `seq<'T>` is the F# name for `IEnumerable<'T>`. Interoperability is therefore close—but “same runtime” does not mean “same semantics.”

A boundary must answer what static information was erased, who owns subscriptions and mutable collections, and which equality relation a hash table uses. This chapter makes those choices explicit, then converts back to ordinary F# values as early as possible.

## What you will be able to do {#outcomes}

By the end of this chapter, you should be able to:

- distinguish a compile-time type from an exact runtime `System.Type`;
- use `typeof<'T>`, `GetType`, boxing, type-test patterns, upcasts, and downcasts accurately;
- treat uncertain runtime values and nullability as decoding inputs;
- adapt F# functions to .NET delegates at the boundary;
- expose and consume CLI events with explicit subscription lifetime;
- distinguish a live `IEnumerable<'T>` view from a materialized snapshot;
- choose mutable .NET collections only when their protocol is wanted;
- specify a dictionary's equality comparer deliberately;
- keep reference identity, value equality, ordering, and hashing separate;
- preserve the equality/hash contract and avoid mutable hash keys.

## Static type and runtime type answer different questions {#static-runtime-types}

The compiler assigns every expression a static type. That type determines which operations compile and carries most of the program's guarantees. The runtime also associates each object instance with an exact `System.Type`, which reflection and object-oriented dispatch can inspect.

The script compares both forms:

```fsharp:line-numbers [ch26-dotnet-runtime-boundaries.fsx]
let request = { RequestId = "R-26"; Seats = 3 }

let declaredType = typeof<BookingRequest>
let boxedRequest: objnull = box request

let actualType =
    match boxedRequest with
    | null -> failwith "boxing a non-null record unexpectedly produced null"
    | value -> value.GetType()

ensureEqual "runtime type" declaredType actualType
printfn "Runtime type: declared=%s actual=%s" declaredType.Name actualType.Name
```
`typeof<BookingRequest>` obtains metadata for a type known statically. `value.GetType()` obtains the exact type of a non-null runtime instance, even when the reference's static type is a base class or `obj`. Because `obj`-shaped .NET input may be null under nullable checking, the example first matches `null` and calls `GetType` only in the non-null branch.

Reflection is appropriate for framework discovery, serialization infrastructure, plugin loading, diagnostics, and truly dynamic protocols. It should not replace a discriminated union when the set of domain alternatives is known. A union preserves cases at compile time; `System.Type` inspection moves missing-case errors to runtime.

### Boxing erases a static view {#boxing}

`box value` converts a value to `objnull`. A reference value is viewed through `System.Object`; a value type is boxed into a new object containing a copy. `unbox<'T>` or a downcast must recover the compatible runtime type.

Boxing is sometimes required by reflection, nongeneric legacy APIs, formatting, or object-based protocols. Do not use `obj` as a convenient universal domain container. It discards static information, introduces runtime checks and null handling, and can allocate for value types.

Generic .NET APIs normally preserve the type argument, so prefer `IEnumerable<BookingRequest>` over nongeneric `IEnumerable`, and `Dictionary<RequestId, Booking>` over object-valued storage.

## Cast only when the relationship is real {#casts}

Three operations have different guarantees:

| Operation | Check | Failure model | Suitable use |
|---|---|---|---|
| `derived :> Base` / `upcast derived` | Compile time | A compiled valid upcast cannot fail at runtime | Widen to a known base class or interface |
| `value :?> Derived` / `downcast value` | Runtime | `InvalidCastException` for an incompatible non-null value | A boundary contract already guarantees the runtime type |
| `:? Derived as value` in `match` | Runtime branch | Non-match chooses another branch | Runtime type is genuinely uncertain |

The shared decoder uses type-test patterns:

```fsharp:line-numbers [ch26-dotnet-runtime-boundaries.fsx]
let describeObject (value: objnull) =
    match value with
    | null -> "null"
    | :? string as text -> $"text:{text.ToUpperInvariant()}"
    | :? BookingRequest as booking -> $"request:{booking.RequestId}/{booking.Seats}"
    | :? int as number -> $"int:{number}"
    | _ -> "other"

let descriptions = [ box "lin"; box request; box 42 ] |> List.map describeObject

ensureEqual "pattern casts" [ "text:LIN"; "request:R-26/3"; "int:42" ] descriptions

printfn "Pattern casts: %A" descriptions

let failedDowncast =
    try
        let _: string | null = (box 42 :?> (string | null))
        "no-error"
    with :? InvalidCastException as error ->
        error.GetType().Name

ensureEqual "failed downcast" "InvalidCastException" failedDowncast
printfn "Failed downcast: %s" failedDowncast
```
It handles null first, even though the fixed inputs happen to be non-null. Each successful `:?` branch narrows the value and binds the typed payload. The deliberately wrong `:?> string` proves that a downcast is not a conversion service: boxed integer `42` does not become text; it raises `InvalidCastException`.

Numeric conversion functions such as `int64`, `decimal`, or checked operators convert representations and may have overflow/rounding policy. Upcasting/downcasting instead navigate compatible object types. Parsing text is another operation again and belongs in `Result`/`TryParse`-style validation.

Decode dynamic input once. Return a typed union or `Result`, then keep reflection and casts out of the domain core. Repeated `:?` checks throughout business logic usually reveal an unmodeled boundary.

## A delegate is a callable .NET object {#delegates}

An F# function value and a .NET delegate both represent callable behavior, but they have different runtime types and consumption conventions. Inside F# code, prefer function values for currying, partial application, and composition. Use delegates where a .NET API or public cross-language contract asks for one.

The script constructs `Func<int,int,int>` and `Converter<int,string>` explicitly:

```fsharp:line-numbers [ch26-dotnet-runtime-boundaries.fsx]
let add = Func<int, int, int>(fun left right -> left + right)

let labels =
    Array.ConvertAll([| 1; 2; 3 |], Converter<int, string>(fun number -> string (number * 2)))

ensureEqual "delegate invocation" 7 (add.Invoke(3, 4))
ensureEqual "delegate conversion" [| "2"; "4"; "6" |] labels
printfn "Delegates: add=%d labels=%A" (add.Invoke(3, 4)) labels
```
The delegate's `Invoke` method performs the call. F# can often adapt a compatible lambda at a known delegate parameter, but an explicit constructor is useful when overload resolution is ambiguous, the delegate must be stored/removed later, or the public type matters.

Do not expose `FSharpFunc<_,_>` accidentally to languages that expect `Func<_,_>`/`Action<_>` or a named delegate; Chapter 27 designs that public surface. Conversely, do not replace every internal function with a delegate merely because the application runs on .NET. Adapt at one edge.

## An event is a subscription protocol {#events}

An event separates a publisher that may trigger notifications from observers that subscribe. The chapter publisher stores a private `Event<EventHandler<SeatsChangedEventArgs>, SeatsChangedEventArgs>` and exposes only `Publish` through a `[<CLIEvent>]` member:

```fsharp:line-numbers [ch26-dotnet-runtime-boundaries.fsx]
type SeatsChangedEventArgs(previous: int, current: int) =
    inherit EventArgs()

    member _.Previous = previous
    member _.Current = current

type CapacityPublisher(initial: int) =
    let changed = Event<EventHandler<SeatsChangedEventArgs>, SeatsChangedEventArgs>()
    let mutable current = initial

    [<CLIEvent>]
    member _.SeatsChanged = changed.Publish

    member this.SetSeats(next: int) =
        let previous = current
        current <- next
        changed.Trigger(this, SeatsChangedEventArgs(previous, next))

let publisher = CapacityPublisher(4)
let observations = ResizeArray<string>()

let handler =
    EventHandler<SeatsChangedEventArgs>(fun sender args ->
        assert (obj.ReferenceEquals(sender, publisher))
        observations.Add($"{args.Previous}->{args.Current}"))

publisher.SeatsChanged.AddHandler handler
publisher.SetSeats 2
publisher.SeatsChanged.RemoveHandler handler
publisher.SetSeats 1

let observedChanges = observations |> Seq.toList
ensureEqual "removed handler" [ "4->2" ] observedChanges
printfn "Event: observed=%A after-remove=%d" observedChanges observations.Count
```
`AddHandler` and `RemoveHandler` use the same stored delegate instance. The first update is observed; after removal, the second is not. This is a lifetime assertion, not just an event-value assertion.

For an F#-shaped event, `.Subscribe` returns `IDisposable`; bind that subscription with `use` or transfer ownership explicitly. Convenience `.Add` installs a handler but does not return a removal token, so do not use it when lifetime must end before the event source dies.

Subscriptions commonly let the publisher retain references to handlers and captured objects. Forgetting to unsubscribe from a longer-lived publisher can therefore retain an otherwise dead subscriber. UI teardown, test cleanup, and application shutdown need a defined owner.

An in-process event is not a durable message bus, transaction, replay log, backpressure mechanism, or error-isolation boundary. Handler code runs as part of notification; keep it small and make ordering/error policy explicit if correctness depends on multiple handlers.

## .NET collections expose mutable protocols {#dotnet-collections}

Chapter 14 chose collections by lookup, update, order, and evaluation needs. The same questions apply when .NET types enter the picture:

| Type/view | Important semantics | Do not infer |
|---|---|---|
| F# `list<'T>` | Immutable linked sequence; structural equality/comparison when elements support them | Cheap indexed access or in-place growth |
| `ResizeArray<'T>` / `List<T>` | Mutable growable indexed collection | Immutability, thread safety, or a stable snapshot |
| `seq<'T>` / `IEnumerable<T>` | Enumeration protocol; source decides when/how work runs | Repeatability, finiteness, cheapness, or ownership |
| `IReadOnlyList<T>` / read-only wrapper | No mutation through that view | Underlying storage cannot change |
| `Dictionary<TKey,TValue>` | Mutable hash lookup using one `IEqualityComparer<TKey>` | Sorted order, domain equality, or compound thread safety |
| F# `Map<'K,'V>` | Immutable ordered map using F# comparison | Hash-only keys or constant-time update |

The script makes a live view and a snapshot observable:

```fsharp:line-numbers [ch26-dotnet-runtime-boundaries.fsx]
let mutableNumbers = ResizeArray<int>([ 1; 2 ])
let liveView: IEnumerable<int> = mutableNumbers
let snapshot = liveView |> Seq.toList
mutableNumbers.Add 3
let liveValues = liveView |> Seq.toList

ensureEqual "live enumerable" [ 1; 2; 3 ] liveValues
ensureEqual "list snapshot" [ 1; 2 ] snapshot
printfn ".NET list: live=%A snapshot=%A" liveValues snapshot

let bookingByEmail = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
bookingByEmail["lin@example.com"] <- "first"
bookingByEmail["LIN@EXAMPLE.COM"] <- "second"
let found, emailValue = bookingByEmail.TryGetValue "Lin@Example.com"

ensureEqual "case-insensitive key count" 1 bookingByEmail.Count
ensureEqual "case-insensitive lookup" (true, "second") (found, emailValue)

printfn "String comparer: count=%d found=%b value=%s" bookingByEmail.Count found emailValue
```
`liveView` is the same mutable `List<T>` seen through `IEnumerable<T>`; enumerating after `Add` sees the new element. `Seq.toList` materializes an independent F# list at that moment. A read-only interface would restrict available operations but could still expose later mutations made through another alias.

Do not modify ordinary `List<T>`/`Dictionary<TKey,TValue>` while enumerating them, and do not assume they support concurrent writers. Convert once at ownership boundaries: snapshot external mutable input when the core needs stability, or return an explicit read-only/live contract when updates must remain visible.

F# adapts the .NET `bool TryGetValue(key, out value)` pattern to a tuple-shaped call, so `let found, value = dictionary.TryGetValue key` keeps lookup single-pass. If `found` is false, treat the out value as unspecified/default rather than meaningful domain data.

## Hash tables need an equality policy {#hash-equality}

A `Dictionary<TKey,TValue>` uses the comparer supplied to its constructor; without one it uses `EqualityComparer<TKey>.Default`. The comparer answers both “are these keys equal?” and “which hash bucket should be searched?”

The email dictionary uses `StringComparer.OrdinalIgnoreCase`, so three case variants name one entry. This policy is explicit, culture-independent, and local to the dictionary. It does not globally change string equality.

Four related concepts must stay separate:

| Concept | Question | Representative operation |
|---|---|---|
| Reference identity | Are these the same object instance? | `obj.ReferenceEquals(a, b)` |
| Value/domain equality | Do these values mean the same thing? | F# `=`, `Equals`, or comparer `Equals` |
| Hashing | Which candidate bucket should contain this key? | `hash`, `GetHashCode`, comparer `GetHashCode` |
| Ordering | Which value sorts before another? | F# `compare` or `IComparer<T>` |

Reference identity cannot be overridden. For value types, calling `ReferenceEquals` boxes each argument, so it is not a value-equality operation. String interning can also make it an unreliable way to ask whether text values are equal. Use it only for genuine object identity.

### Default class keys versus domain keys {#class-keys}

The script creates two `Customer` instances carrying the same ID. The class does not override equality, so the default dictionary treats the two references as separate keys. A second dictionary receives an explicit comparer built with `HashIdentity.FromFunctions`:

```fsharp:line-numbers [ch26-dotnet-runtime-boundaries.fsx]
type Customer(customerId: string) =
    member _.CustomerId = customerId

let customerIdComparer: IEqualityComparer<Customer> =
    HashIdentity.FromFunctions
        (fun customer -> StringComparer.Ordinal.GetHashCode(customer.CustomerId))
        (fun left right -> StringComparer.Ordinal.Equals(left.CustomerId, right.CustomerId))

let firstCustomer = Customer("C-26")
let secondCustomer = Customer("C-26")
let sameReference = obj.ReferenceEquals(firstCustomer, secondCustomer)

let defaultKeys = Dictionary<Customer, string>()
defaultKeys[firstCustomer] <- "first"
defaultKeys[secondCustomer] <- "second"

let domainKeys = Dictionary<Customer, string>(customerIdComparer)
domainKeys[firstCustomer] <- "first"
domainKeys[secondCustomer] <- "second"

ensureEqual "separate references" false sameReference
ensureEqual "default class keys" 2 defaultKeys.Count
ensureEqual "domain class keys" 1 domainKeys.Count
ensureEqual "domain lookup" "second" domainKeys[firstCustomer]

printfn
    "Class keys: same-reference=%b default=%d domain=%d value=%s"
    sameReference
    defaultKeys.Count
    domainKeys.Count
    domainKeys[firstCustomer]
```
Both comparer functions project the same immutable `CustomerId`. The second insertion therefore replaces the first and the dictionary has one domain key. Choosing the comparer at construction makes the collection's meaning reviewable without changing equality for every `Customer` use.

Every equality comparer must obey these laws:

- equality is reflexive, symmetric, and transitive;
- if `Equals(a, b)` is true, both values must produce the same hash code;
- unequal values may share a hash code—collisions are permitted and then resolved by equality;
- fields used by equality and hashing must not change while a key is stored;
- hash codes are process implementation details, not persistent IDs, signatures, or stable ordering.

Chapter 7 established structural equality and hash agreement for immutable records. Chapter 11 explained equality/comparison constraints. Chapter 14 separated ordered `Map`/`Set` from hash collections. At a .NET boundary, state explicitly which of those contracts the receiving API uses.

## A boundary workflow that stays typed {#boundary-workflow}

Use this sequence when integrating an object-shaped API:

1. Read its exact nullable annotations, overloads, delegate types, collection interfaces, and comparer rules.
2. Decode `objnull`, runtime variants, exceptions, and `Try` patterns in a thin adapter.
3. Copy mutable/live data if the core requires a stable snapshot.
4. Convert callbacks and events into an owned function, task, message, or disposable subscription.
5. Choose identity/equality/hash policy at collection construction; keep key projections immutable.
6. Return records, unions, `option`, `Result`, and functions to the F# core.
7. Test both value results and boundary lifecycle: handler removal, enumeration timing, comparer behavior, and failure types.

This is not anti-.NET isolation. It is semantic compression: the adapter translates a broad runtime protocol into the smaller vocabulary the domain actually needs.

## Run the shared example {#run-example}

From the directory containing the example:

```console
dotnet fsi --checknulls+ --warnaserror+ --exec ch26-dotnet-runtime-boundaries.fsx
```

Eight deterministic lines cover exact runtime type, safe and failing casts, delegates, event removal, live versus copied collections, case-insensitive lookup, and default reference versus domain key identity. Run it once with the flags shown above, then remove `--checknulls+` if you want to compare the compiler's behavior without nullable checking.

## Exercises {#exercises}

### Exercise 1: decode an object boundary once {#exercise-01}

Write `decode : objnull -> Result<BoundaryValue, DecodeError>` for `string`, `int`, and `BookingRequest`. Handle null and unsupported runtime types explicitly. The rest of the program must pattern-match only on `BoundaryValue`, with no further casts.

### Exercise 2: own an event subscription {#exercise-02}

Create a capacity publisher with a CLI event. Subscribe, trigger one change, dispose or remove the subscription, and trigger another change. Assert that only the first is observed and identify the owner responsible for cleanup.

### Exercise 3: define dictionary key meaning {#exercise-03}

Store two customer objects whose IDs differ only by case. Build an ordinal case-insensitive comparer with `HashIdentity.FromFunctions`, verify the equality/hash laws on three representative objects, and prove that the second insertion replaces the first. Explain why a mutable customer ID would break the key protocol.

[Read the chapter solutions](../solutions/ch-26-dotnet-runtime-boundaries).

## Model review {#model-review}

- Static types prevent mistakes before execution; runtime types support genuinely dynamic protocols.
- Boxing erases a typed view and can allocate for value types.
- Upcasts are statically valid; uncertain downcasts belong in type-test branches.
- A runtime cast is not numeric conversion or parsing.
- Functions are the F# default; delegates are explicit .NET adapters.
- Event correctness includes subscription lifetime, not only delivered values.
- `IEnumerable<T>` can be a live/deferred view; materialization creates a snapshot.
- A read-only view is not proof of immutable backing storage.
- Dictionary semantics come from its comparer, not from the key's field names.
- Identity, equality, hashing, and ordering are four separate contracts.

## Sources {#sources}

- [Microsoft Learn: casting and conversions in F#](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/casting-and-conversions)
- [Microsoft Learn: `Object.GetType`](https://learn.microsoft.com/en-us/dotnet/api/system.object.gettype?view=net-10.0)
- [Microsoft Learn: delegates in F#](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/delegates)
- [Microsoft Learn: events in F#](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/members/events)
- [Microsoft Learn: .NET collections and data structures](https://learn.microsoft.com/en-us/dotnet/standard/collections/)
- [Microsoft Learn: `Dictionary<TKey,TValue>.Comparer`](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2.comparer?view=net-10.0)
- [FSharp.Core reference: `HashIdentity`](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-collections-hashidentity.html)
- [Microsoft Learn: `Object.ReferenceEquals`](https://learn.microsoft.com/en-us/dotnet/api/system.object.referenceequals?view=net-10.0)
