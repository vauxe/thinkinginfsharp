---
title: "Chapter 25 Solutions"
description: "Replace a ceremonial class, compare function and interface policy boundaries, and redesign a struct so its default representation is valid."
translationKey: solutions/ch-25-objects-interfaces
---

# Chapter 25 Solutions {#overview}

These solutions choose representation from observable semantics. They do not count lines or declare every class wrong: a class stays only when removing it would erase a real identity, lifetime, encapsulation, dispatch, or ecosystem contract.

[Return to Chapter 25](../part-05/ch-25-objects-interfaces).

## Exercise 1: remove a ceremonial class {#exercise-01}

### Let data and validation say exactly what they are {#exercise-01-record}

```fsharp
open System

type SeatRequest =
    { RequestId: string
      Seats: int }

type ValidationError =
    | EmptyRequestId
    | NonPositiveSeats of actual: int

module SeatRequest =
    let create requestId seats =
        if String.IsNullOrWhiteSpace requestId then
            Error EmptyRequestId
        elif seats <= 0 then
            Error(NonPositiveSeats seats)
        else
            Ok
                { RequestId = requestId.Trim()
                  Seats = seats }

let accepted = SeatRequest.create "  REQ-25  " 2
let rejected = SeatRequest.create "REQ-25" 0

assert (accepted = Ok { RequestId = "REQ-25"; Seats = 2 })
assert (rejected = Error(NonPositiveSeats 0))
```

The record exposes immutable product data and receives generated structural equality, which is exactly what this request needs. The module owns normalization and expected validation without turning construction into exception control flow.

A class would become justified if each request had reference identity, protected evolving state, an owned disposable resource, required virtual/interface dispatch, or a framework base-class contract. Merely preferring property-call syntax would not be enough.

## Exercise 2: choose a policy boundary {#exercise-02}

### Compare the same rule without changing its meaning {#exercise-02-policies}

```fsharp
type QuoteRequest =
    { Seats: int
      UnitPrice: decimal }

type DiscountPolicy = QuoteRequest -> decimal

type IDiscountPolicy =
    abstract Rate: QuoteRequest -> decimal

let groupRate request =
    if request.Seats >= 5 then 0.10M else 0M

let totalWith (rate: DiscountPolicy) request =
    let subtotal = decimal request.Seats * request.UnitPrice
    subtotal * (1M - rate request)

let objectPolicy =
    { new IDiscountPolicy with
        member _.Rate request = groupRate request }

let request = { Seats = 5; UnitPrice = 10M }
let functionTotal = totalWith groupRate request
let interfaceTotal = totalWith objectPolicy.Rate request

assert (functionTotal = 45M)
assert (interfaceTotal = functionTotal)
```

For an F#-only library with one stateless operation, `DiscountPolicy` is the smaller public boundary and composes directly. The interface becomes reasonable when an existing .NET framework expects it, other .NET languages need a member-shaped contract, several related operations belong together, or implementations own identity/lifetime and must be selected by runtime dispatch.

The object expression is local and contains one forwarding member. If the policy acquired collaborators, caches, disposal, or substantial rules, a named implementation would make those responsibilities visible.

## Exercise 3: audit a struct invariant {#exercise-03}

### Prove the unsafe default, then make default a modeled state {#exercise-03-default}

```fsharp
[<Struct>]
type PositiveRevision = private | PositiveRevision of int

module PositiveRevision =
    let create raw =
        if raw > 0 then Ok(PositiveRevision raw) else Error raw

    let value (PositiveRevision raw) = raw

let positive =
    PositiveRevision.create 3
    |> Result.defaultWith (fun error -> failwithf "unexpected: %d" error)
let copied = positive
let invalidDefault = Unchecked.defaultof<PositiveRevision>

assert (PositiveRevision.value copied = 3)
assert (not (obj.ReferenceEquals(box positive, box copied)))
assert (PositiveRevision.value invalidDefault = 0)

[<Struct>]
type Revision =
    private
    | Unassigned
    | Assigned of value: int

module Revision =
    let assign raw =
        if raw > 0 then Ok(Assigned raw) else Error raw

    let describe revision =
        match revision with
        | Unassigned -> "unassigned"
        | Assigned value -> $"assigned:{value}"

let initial = Unchecked.defaultof<Revision>
let assigned =
    Revision.assign 3
    |> Result.defaultWith (fun error -> failwithf "unexpected: %d" error)

assert (Revision.describe initial = "unassigned")
assert (Revision.describe assigned = "assigned:3")
```

The private case protects ordinary construction but cannot prevent the CLR's zero initialization. The redesign makes tag zero, the first case, mean `Unassigned`; every representable default is now a defined domain state, while `Assigned` still goes through validation for ordinary callers.

Do this only if a struct is independently justified. If “unassigned” is not meaningful, prefer a non-struct domain model and keep default-producing interop outside it, or reject zero immediately at every such boundary. `Unchecked.defaultof` can manufacture problematic values for reference representations too; it is an unsafe escape hatch, not normal construction.

## Solution review {#solution-review}

- Remove a wrapper only after identifying whether it carries real object semantics.
- Records plus module functions keep immutable data and expected validation explicit.
- A single F# policy is often just a function; an interface is useful at a genuine object/.NET boundary.
- Object expressions are compact local implementations, not a way to hide a subsystem.
- A private struct constructor cannot remove zero initialization.
- If a value type must survive default construction, model the all-zero representation as valid or reject it at every entry boundary.
