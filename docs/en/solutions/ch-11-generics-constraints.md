---
title: "Chapter 11 Solutions"
description: "Infer generic signatures, repair value restrictions by intent, and preserve measured dimensions at boundaries."
translationKey: solutions/ch-11-generics-constraints
---

# Chapter 11 Solutions {#overview}

Do not guess whether a definition is generic from how abstract its name sounds. Trace each operation, then retain only the consistency and capability requirements that operation creates.

[Return to Chapter 11](../part-02/ch-11-generics-constraints).

## Exercise 1: infer generality and constraints {#exercise-01}

The most general signatures are:

```fsharp
pair : 'Left -> 'Right -> 'Left * 'Right

contains : 'T -> 'T list -> bool
    when 'T : equality

orderedPair : 'T -> 'T -> 'T * 'T
    when 'T : comparison

wrap : 'T -> Envelope<'T>
```

`pair` places values into distinct tuple positions, so the two types need not match and no capability is used. `contains` must compare a candidate with list elements using F# equality, which makes the element type consistent and adds `'T : equality`. `orderedPair` uses `<=`, so both inputs share a comparison-capable type. `wrap` only stores its value; construction does not require equality or comparison.

`pair` can receive a function in either position, and `wrap` can construct `Envelope<('A -> 'B)>`. `contains` cannot search a function list with F# generic equality, and `orderedPair` cannot order functions. The fact that an envelope *can contain* a function does not mean envelopes with that payload can later use generated structural equality.

## Exercise 2: repair two value restrictions {#exercise-02}

For one shared array with one intended element type, specialize the binding:

```fsharp
let bookingBuckets: BookingRequest list array =
    Array.create 2 []
```

The right side runs once when the binding is initialized. Every caller sees the same two-slot array, so ownership and synchronization must match that shared lifetime.

For a fresh array whose element type is inferred separately at each call, make the construction a function:

```fsharp
let makeBuckets () =
    Array.create 2 []

let bookingBuckets: BookingRequest list array = makeBuckets ()
let labelBuckets: string list array = makeBuckets ()
```

The body runs on every call, so the arrays are distinct. The `unit` argument is not meaningless decoration; it exposes the requested creation event.

For the generic transformation, expose its data parameter:

```fsharp
let keepAll values =
    List.filter (fun _ -> true) values
```

The function definition is initialized once, while filtering is performed for each invocation. Its inferred signature is `'T list -> 'T list`. `let keepAll = id` would also satisfy the observable “retain all values” result for immutable lists, but it would evade rather than demonstrate the partial-application repair and could have different sharing/allocation behavior.

These are not interchangeable compiler tricks: annotation chooses one shared typed value; `()` chooses repeated construction; an explicit data parameter defines a reusable transformation.

## Exercise 3: preserve dimensions across a boundary {#exercise-03}

A direct measured implementation is:

```fsharp
[<Measure>]
type seat

[<Measure>]
type minute

let throughput
    (processed: float<seat>)
    (elapsed: float<minute>)
    : float<seat/minute> =
    processed / elapsed

let seatsFromValidatedInt raw : int<seat> =
    LanguagePrimitives.Int32WithMeasure raw

// Diagnostic-only: FS0001, because the measures differ.
let invalid = 2<seat> + 3<minute>
```

The return annotation on `throughput` is optional because division infers it, but it documents the boundary. `seatsFromValidatedInt` assumes validation already happened; giving it a more honest name prevents readers from mistaking measure attachment for validation.

At runtime and after ordinary serialization, only the underlying `float` or `int` remains. A receiving F# boundary must validate the contract and reattach the intended measure. A JSON number cannot prove whether its producer meant seats or minutes.

Measures also cannot enforce that elapsed time is nonzero, seat count is positive, or requested seats fit remaining capacity. Those are value-level invariants. Division by zero and negative measured literals remain possible unless validation or a protected domain type rejects them.

## What to notice {#what-to-notice}

- **Storage alone adds no structural constraint:** the operation performed later determines equality or comparison needs.
- **Binding lifetime is part of an FS0030 repair:** one annotated array and a unit-taking factory have different semantics.
- **Function values expose the component rule:** they can be stored generically but not passed to generic equality or comparison.
- **Measure inference follows arithmetic:** division constructs a quotient measure without SRTP boilerplate.
- **Erasure creates a boundary duty:** deserialize a number, validate its meaning, then restore the measure.
