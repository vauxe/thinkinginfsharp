---
title: "Chapter 26: Deeper .NET Interoperability"
description: "Use runtime types, delegates, events, collections, identity, equality, and hashing without losing F# domain semantics."
translationKey: part-05/ch-26-dotnet-runtime-boundaries
---

# Chapter 26: Deeper .NET Interoperability {#overview}

F# values run in the .NET type system. A record can be boxed as `obj`, a function can be adapted to a delegate, an F# event can be published as a CLI event, and a `seq<'T>` is the F# name for `IEnumerable<'T>`. Interoperability is therefore close—but “same runtime” does not mean “same semantics.”

At each interop point, determine which static information was erased, who manages subscriptions and mutable collections, and which equality rule a hash table uses. Then convert back to normal F# values as early as possible.

## Static type and runtime type answer different questions {#static-runtime-types}

The compiler assigns every expression a static type. That type determines which operations compile and carries most of the program's guarantees. The runtime also associates each object instance with an exact `System.Type`, which reflection and object-oriented dispatch can inspect.

The script compares both forms:

```fsharp:line-numbers
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
`typeof<BookingRequest>` obtains metadata for a type known statically. `value.GetType()` obtains the exact type of a non-null runtime instance, even when the reference's static type is a base class or `obj`. Because `obj`-typed .NET input may be null under nullable checking, the example first matches `null` and calls `GetType` only in the non-null branch.

Reflection is appropriate for framework discovery, serialization infrastructure, plugin loading, diagnostics, and truly dynamic protocols. It should not replace a discriminated union when the set of domain alternatives is known. A union preserves cases at compile time; `System.Type` inspection moves missing-case errors to runtime.

### Boxing erases static type information {#boxing}

`box value` converts a value to `objnull`. A reference value is viewed through `System.Object`; a value type is boxed into a new object containing a copy. `unbox<'T>` or a downcast must recover the compatible runtime type.

Boxing is sometimes required by reflection, nongeneric legacy APIs, formatting, or object-based protocols. Do not use `obj` as a convenient universal domain container. It discards static information, introduces runtime checks and null handling, and can allocate for value types.

Generic .NET APIs normally preserve the type argument, so prefer `IEnumerable<BookingRequest>` over nongeneric `IEnumerable`, and `Dictionary<RequestId, Booking>` over object-valued storage.

## Cast only between related runtime types {#casts}

Three operations have different guarantees:

| Operation | Check | Failure model | Suitable use |
|---|---|---|---|
| `derived :> Base` / `upcast derived` | Compile time | A compiled valid upcast cannot fail at runtime | Widen to a known base class or interface |
| `value :?> Derived` / `downcast value` | Runtime | `InvalidCastException` for an incompatible non-null value | The caller already guarantees the runtime type |
| `:? Derived as value` in `match` | Runtime branch | Non-match chooses another branch | Runtime type is genuinely uncertain |

The shared decoder uses type-test patterns:

```fsharp:line-numbers
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

Numeric functions such as `int64`, `decimal`, and checked operators convert representations and may apply overflow or rounding rules. Upcasts and downcasts only change how compatible object types are viewed. Text parsing is a third operation and belongs in `Result`- or `TryParse`-style validation.

Decode dynamic input once as it enters the application. Return a typed union or `Result`, then keep reflection and casts out of domain logic. Repeated `:?` checks in business code usually reveal missing input modeling.

## A delegate is a callable .NET object {#delegates}

An F# function value and a .NET delegate both represent callable behavior, but they have different runtime types and calling conventions. Inside F# code, prefer function values for currying, partial application, and composition. Use delegates when a .NET API or public cross-language API requires one.

The script constructs `Func<int,int,int>` and `Converter<int,string>` explicitly:

```fsharp:line-numbers
let add = Func<int, int, int>(fun left right -> left + right)

let labels =
    Array.ConvertAll([| 1; 2; 3 |], Converter<int, string>(fun number -> string (number * 2)))

ensureEqual "delegate invocation" 7 (add.Invoke(3, 4))
ensureEqual "delegate conversion" [| "2"; "4"; "6" |] labels
printfn "Delegates: add=%d labels=%A" (add.Invoke(3, 4)) labels
```
The delegate's `Invoke` method performs the call. F# can often adapt a compatible lambda when the parameter's delegate type is known. Construct the delegate explicitly when overload resolution is ambiguous, when it must later be stored or removed, or when its public type matters.

Do not accidentally expose `FSharpFunc<_,_>` to languages that expect `Func<_,_>`, `Action<_>`, or a named delegate; Chapter 27 designs that public API. Conversely, do not replace every internal function with a delegate merely because the application runs on .NET. Adapt once where the APIs meet.

## An event manages subscription lifetime {#events}

An event separates a publisher that triggers notifications from observers that subscribe. The example publisher stores a private `Event<EventHandler<SeatsChangedEventArgs>, SeatsChangedEventArgs>` and exposes only `Publish` through a `[<CLIEvent>]` member:

```fsharp:line-numbers
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
`AddHandler` and `RemoveHandler` use the same stored delegate instance. The first update is observed; after removal, the second is not. The assertion tests subscription lifetime as well as the delivered value.

For an F#-style event, `.Subscribe` returns `IDisposable`; bind that subscription with `use` or clearly transfer cleanup responsibility. The convenient `.Add` method installs a handler but returns no removal token, so avoid it when the subscription must end before the event source.

Subscriptions commonly let the publisher retain references to handlers and captured objects. Forgetting to unsubscribe from a longer-lived publisher can therefore retain a subscriber that should have been collected. UI teardown, test cleanup, and application shutdown need clearly responsible code.

An in-process event is not a durable message bus, transaction, replay log, backpressure mechanism, or error-isolation tool. Handler code runs as part of notification; keep it small, and define ordering and error handling if correctness depends on multiple handlers.

## .NET collections expose mutable behavior {#dotnet-collections}

Chapter 14 chose collections by lookup, update, order, and evaluation needs. The same questions apply when .NET types enter the picture:

| Type/view | Important behavior | Do not assume |
|---|---|---|
| F# `list<'T>` | Immutable linked sequence; structural equality/comparison when elements support them | Cheap indexed access or in-place growth |
| `ResizeArray<'T>` / `List<T>` | Mutable growable indexed collection | Immutability, thread safety, or a stable snapshot |
| `seq<'T>` / `IEnumerable<T>` | Enumeration protocol; source decides when and how work runs | Repeatability, finiteness, low cost, or lifecycle |
| `IReadOnlyList<T>` / read-only wrapper | No mutation through that view | Underlying storage cannot change |
| `Dictionary<TKey,TValue>` | Mutable hash lookup using one `IEqualityComparer<TKey>` | Sorted order, domain equality, or compound thread safety |
| F# `Map<'K,'V>` | Immutable ordered map using F# comparison | Hash-only keys or constant-time update |

The script directly compares a live view with a snapshot:

```fsharp:line-numbers
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

Do not modify an ordinary `List<T>` or `Dictionary<TKey,TValue>` while enumerating it, and do not assume concurrent writers are supported. Snapshot external mutable input when domain logic needs stability. If updates must remain visible, return an API that clearly says whether its view is read-only or live.

F# adapts the .NET `bool TryGetValue(key, out value)` pattern to a tuple return. Thus `let found, value = dictionary.TryGetValue key` performs one lookup. If `found` is false, treat the output as unspecified or default data, not as a meaningful domain value.

## Hash tables need an equality policy {#hash-equality}

A `Dictionary<TKey,TValue>` uses the comparer supplied to its constructor; without one it uses `EqualityComparer<TKey>.Default`. The comparer answers both “are these keys equal?” and “which hash bucket should be searched?”

The email dictionary uses `StringComparer.OrdinalIgnoreCase`, so three case variants name one entry. This policy is defined for that dictionary and is culture-independent; it does not globally change string equality.

Four related concepts must stay separate:

| Concept | Question | Representative operation |
|---|---|---|
| Reference identity | Are these the same object instance? | `obj.ReferenceEquals(a, b)` |
| Value/domain equality | Do these values mean the same thing? | F# `=`, `Equals`, or comparer `Equals` |
| Hashing | Which candidate bucket should contain this key? | `hash`, `GetHashCode`, comparer `GetHashCode` |
| Ordering | Which value sorts before another? | F# `compare` or `IComparer<T>` |

Reference identity is fixed. For value types, calling `ReferenceEquals` boxes each argument, so it does not test value equality. String interning also makes it unreliable for comparing text values. Use it only for genuine object identity.

### Default class keys versus domain keys {#class-keys}

The script creates two `Customer` instances carrying the same ID. The class does not override equality, so the default dictionary treats the two references as separate keys. A second dictionary receives an explicit comparer built with `HashIdentity.FromFunctions`:

```fsharp:line-numbers
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
Both comparer functions project the same immutable `CustomerId`. The second insertion therefore replaces the first, leaving one domain key. Choosing the comparer at construction makes the collection's meaning visible without changing equality for every use of `Customer`.

Every equality comparer must obey these laws:

- equality is reflexive, symmetric, and transitive;
- if `Equals(a, b)` is true, both values must produce the same hash code;
- unequal values may share a hash code—collisions are permitted and then resolved by equality;
- fields used by equality and hashing must not change while a key is stored;
- hash codes are process implementation details, not persistent IDs, signatures, or stable ordering.

Chapter 7 established structural equality and matching hashes for immutable records. Chapter 11 explained equality and comparison constraints. Chapter 14 separated ordered `Map` and `Set` from hash collections. When calling a .NET API, state which of those rules it uses.

## Keep an interoperability workflow typed {#boundary-workflow}

Use this sequence when integrating an object-based API:

1. Read its exact nullable annotations, overloads, delegate types, collection interfaces, and comparer rules.
2. Decode `objnull`, runtime variants, exceptions, and `Try` patterns in a thin adapter.
3. Copy mutable/live data if the core requires a stable snapshot.
4. Convert callbacks and events into a function, task, message, or disposable subscription with a defined lifetime.
5. Choose identity/equality/hash policy at collection construction; keep key projections immutable.
6. Return records, unions, `option`, `Result`, and functions to the F# core.
7. Test both values and integration lifetimes: handler removal, enumeration timing, comparer behavior, and failure types.

This does not isolate F# from .NET. The adapter narrows a broad runtime protocol to the few types and operations that domain logic actually needs.

## Exercises {#exercises}

### Exercise 1: decode dynamic object input once {#exercise-01}

Write `decode : objnull -> Result<BoundaryValue, DecodeError>` for `string`, `int`, and `BookingRequest`. Clearly handle null and unsupported runtime types. The rest of the program must pattern-match only on `BoundaryValue`, with no further casts.


::: details Answer

#### Turn runtime alternatives into a closed union {#exercise-01-decoder}

```fsharp
open System

type BookingRequest =
    { RequestId: string
      Seats: int }

type BoundaryValue =
    | Text of string
    | Count of int
    | Request of BookingRequest

type DecodeError =
    | NullValue
    | UnsupportedType of Type

let decode (input: objnull) =
    match input with
    | null -> Error NullValue
    | :? string as text -> Ok(Text text)
    | :? int as count -> Ok(Count count)
    | :? BookingRequest as request -> Ok(Request request)
    | value -> Error(UnsupportedType(value.GetType()))

let request = { RequestId = "R-26"; Seats = 2 }

let decoded =
    [ box "Lin"; box 3; box request ]
    |> List.map decode

assert (decoded = [ Ok(Text "Lin"); Ok(Count 3); Ok(Request request) ])
assert (decode null = Error NullValue)

match decode (box 1.5M) with
| Error(UnsupportedType runtimeType) -> assert (runtimeType = typeof<decimal>)
| outcome -> failwithf "unexpected outcome: %A" outcome
```

Only `decode` knows about `objnull`, `:?`, and `GetType`. Downstream functions can exhaustively match `Text`, `Count`, and `Request`; an unsupported runtime type cannot leak as an unchecked cast.

Whether null and unsupported types are one or two error cases is domain policy. Keeping `System.Type` in boundary diagnostics is useful; letting reflection decide business behavior after this adapter is not.

:::

### Exercise 2: manage an event subscription {#exercise-02}

Create a capacity publisher with a CLI event. Subscribe, trigger one change, dispose or remove the subscription, and trigger another change. Assert that only the first is observed and identify the code responsible for cleanup.


::: details Answer

#### Make disposal observable {#exercise-02-subscription}

```fsharp
open System

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

let publisher = CapacityPublisher(5)
let observed = ResizeArray<int * int>()

let subscription =
    publisher.SeatsChanged.Subscribe(fun args ->
        observed.Add(args.Previous, args.Current))

publisher.SetSeats 3
subscription.Dispose()
publisher.SetSeats 1

assert (observed |> Seq.toList = [ (5, 3) ])
```

The composition scope that creates `subscription` is responsible for disposing it. In an application, that scope should bind the subscription with `use`, store it in a component that implements disposal, or explicitly transfer the responsibility. The test disposes midway only to verify the lifetime boundary.

The publisher manages event triggering and its current capacity, not subscriber lifetimes. A longer-lived publisher that retains an unremoved handler can cause a leak.

:::

### Exercise 3: define dictionary key meaning {#exercise-03}

Store two customer objects whose IDs differ only by case. Build an ordinal case-insensitive comparer with `HashIdentity.FromFunctions`. Verify the equality and hash laws on three representative objects, then show that the second insertion replaces the first. Explain why a mutable customer ID would break dictionary lookup.


::: details Answer

#### Use one immutable projection for equality and hashing {#exercise-03-comparer}

```fsharp
open System
open System.Collections.Generic

type Customer(customerId: string, displayName: string) =
    member _.CustomerId = customerId
    member _.DisplayName = displayName

let customerIdIdentity: IEqualityComparer<Customer> =
    HashIdentity.FromFunctions
        (fun customer ->
            StringComparer.OrdinalIgnoreCase.GetHashCode(customer.CustomerId))
        (fun left right ->
            StringComparer.OrdinalIgnoreCase.Equals(
                left.CustomerId,
                right.CustomerId
            ))

let first = Customer("customer-26", "Lin")
let second = Customer("CUSTOMER-26", "Ada")
let third = Customer("Customer-26", "Mira")

let equal left right = customerIdIdentity.Equals(left, right)
let hashOf value = customerIdIdentity.GetHashCode value

assert (equal first first)
assert (equal first second = equal second first)
assert (equal first second && equal second third && equal first third)
assert (hashOf first = hashOf second && hashOf second = hashOf third)

let byCustomer = Dictionary<Customer, string>(customerIdIdentity)
byCustomer[first] <- "first"
byCustomer[second] <- "second"

assert (byCustomer.Count = 1)
assert (byCustomer[third] = "second")
```

Display names do not participate in key meaning, and all ID operations use the same ordinal case-insensitive rule. Equal IDs therefore yield equal hashes and one dictionary entry.

If `CustomerId` changed after insertion, the comparer could direct lookup to a different bucket from the one used during insertion. The entry might become unreachable or removal might fail. Keep key projections immutable; to rename a key, remove the old key and insert a new immutable value under an explicit operation.

These assertions sample the laws but cannot prove them for every string. Chapter 29 will turn laws such as symmetry and equal-hash agreement into generated properties.

:::


## Sources {#sources}

- [Microsoft Learn: casting and conversions in F#](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/casting-and-conversions)
- [Microsoft Learn: `Object.GetType`](https://learn.microsoft.com/en-us/dotnet/api/system.object.gettype?view=net-10.0)
- [Microsoft Learn: delegates in F#](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/delegates)
- [Microsoft Learn: events in F#](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/members/events)
- [Microsoft Learn: .NET collections and data structures](https://learn.microsoft.com/en-us/dotnet/standard/collections/)
- [Microsoft Learn: `Dictionary<TKey,TValue>.Comparer`](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2.comparer?view=net-10.0)
- [FSharp.Core reference: `HashIdentity`](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-collections-hashidentity.html)
- [Microsoft Learn: `Object.ReferenceEquals`](https://learn.microsoft.com/en-us/dotnet/api/system.object.referenceequals?view=net-10.0)
