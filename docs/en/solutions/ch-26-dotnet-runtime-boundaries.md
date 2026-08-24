---
title: "Chapter 26 Solutions"
description: "Decode object input once, own an event subscription, and prove a custom dictionary comparer obeys its equality and hash contract."
translationKey: solutions/ch-26-dotnet-runtime-boundaries
kind: solution
part: 5
chapter: 26
status: complete
verifiedWith:
  fsharp: "10"
  dotnetSdk: "10.0.301"
exampleIds:
  - ch26-dotnet-runtime-boundaries
exerciseIds:
  - ch26-exercise-01
  - ch26-exercise-02
  - ch26-exercise-03
termIds: []
sources:
  - id: microsoft-fsharp-casting
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/casting-and-conversions
    checked: "2026-08-24"
  - id: microsoft-fsharp-events
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/members/events
    checked: "2026-08-24"
  - id: dotnet-dictionary-comparer
    url: https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2.comparer?view=net-10.0
    checked: "2026-08-24"
  - id: fsharp-hash-identity
    url: https://fsharp.github.io/fsharp-core-docs/reference/fsharp-collections-hashidentity.html
    checked: "2026-08-24"
---

# Chapter 26 Solutions {#overview}

Each solution keeps runtime protocol at one edge. The resulting domain code sees a union, an explicitly owned subscription, or a dictionary whose key policy is fixed at construction.

[Return to Chapter 26](../part-05/ch-26-dotnet-runtime-boundaries).

## Exercise 1: decode an object boundary once {#exercise-01}

### Turn runtime alternatives into a closed union {#exercise-01-decoder}

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

## Exercise 2: own an event subscription {#exercise-02}

### Make disposal observable {#exercise-02-subscription}

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

The composition scope that creates `subscription` owns it. In an application that scope should bind it with `use`, store it in an owning component that implements disposal, or transfer the responsibility explicitly. The test disposes in the middle only to prove the lifetime boundary.

The publisher owns event triggering and its current capacity, but it does not own arbitrary subscriber lifetimes. A longer-lived publisher retaining an unremoved handler is the leak risk.

## Exercise 3: define dictionary key meaning {#exercise-03}

### Use one immutable projection for equality and hashing {#exercise-03-comparer}

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

## Solution review {#solution-review}

- Decode runtime alternatives once and return a closed typed result.
- Keep `System.Type` for diagnostics, not repeated domain dispatch.
- Treat event subscription disposal as an ownership assertion.
- The creator of a subscription must dispose it or transfer that obligation.
- Build dictionary equality and hashing from the same immutable projection and comparison rule.
- Equal keys require equal hashes; hash collisions do not imply equality.
- Never mutate the equality/hash projection while a key is stored.
