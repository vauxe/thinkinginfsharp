---
title: "Chapter 11: Generics, Constraints, and Units"
description: "Understand automatic generalization, the value restriction, equality and comparison constraints, conditional structural capabilities, and units of measure."
translationKey: part-02/ch-11-generics-constraints
---

# Chapter 11: Generics, Constraints, and Units {#overview}

`mapTree` from Chapter 10 never inspects a leaf's concrete type. The compiler therefore inferred one implementation that works for `BookingTree<int>`, `BookingTree<string>`, and many other instantiations. By contrast, sorting leaves needs an ordering operation, and adding quantities needs compatible numeric dimensions. “Generic” does not mean “all operations are available”; it means the definition states exactly which type facts it requires.

We will trace those requirements through unconstrained functions, the value restriction, structural equality and comparison, and units of measure. The aim is to explain why a type variable is general or constrained—and why the compiler sometimes refuses to generalize a binding.

## Generality is inferred from independence {#automatic-generalization}

The shared function uses its input only to place two copies in a list:

```fsharp:line-numbers
let duplicate value = [ value; value ]

let integerCopies = duplicate 3
let attendeeCopies = duplicate "Lin"

printfn "Generalized function: ints=%A strings=%A" integerCopies attendeeCopies

let genericEmpty = []
let oneInteger = 1 :: genericEmpty
let oneAttendee = "Ada" :: genericEmpty

printfn "Simple generic value: ints=%A strings=%A" oneInteger oneAttendee
```
F# infers:

```fsharp
duplicate : 'T -> 'T list
```

`'T` is a generic type parameter. It does not mean “a dynamically typed value.” At each call, the argument and both result elements have one consistent concrete type. One call instantiates `'T` as `int`; another instantiates it as `string`. Both calls remain statically checked.

This is **automatic generalization**. When a complete function definition with explicit parameters does not depend on a particular parameter type, the compiler can quantify that type variable. Explicit syntax exists:

```fsharp
let duplicateExplicit<'T> (value: 'T) : 'T list =
    [ value; value ]
```

The explicit version adds no information here and is usually noisier. Let inference expose the most general safe signature. Add annotations when they clarify a public API or resolve real ambiguity.

Some simple immutable data terms can also be generalized safely. The empty list contains no value and no mutable element slot, so `genericEmpty` can be instantiated as both `int list` and `string list`. This exception is not permission to assume every expression with an unknown type is a reusable generic value.

### Generic types carry the same relationship {#generic-types}

The script's record declares its parameter explicitly because a type definition must name the varying field type:

```fsharp
type Envelope<'T> =
    { Label: string
      Payload: 'T }
```

`Envelope<int>` and `Envelope<string>` are distinct constructed types made from one definition. Within either value, `Payload` has exactly the supplied type. `BookingTree<'T>`, `'T option`, and `'T list` use the same idea.

Generality is useful only when the implementation really ignores the difference. If a function converts its input with `string`, its result can still be generic in the input; if it adds `1`, the chosen literal and addition operation introduce numeric requirements. Read operations, not parameter names, to find constraints.

## The value restriction protects one value from incompatible uses {#value-restriction}

This diagnostic-only definition is deliberately absent from the valid example:

```fsharp
let ambiguousBuckets = Array.create 2 []
```

F# 10 reports FS0030 and shows an inferred weak type such as `'_a list array`. The expression contains an unresolved element type, but the binding is neither a complete explicit-argument function nor a simple immutable data term eligible for safe generalization.

The restriction matters because an array has mutable element slots. If one array value could be viewed first as `int list array` and then as `string list array`, a write through one view would invalidate the other. The compiler must not turn one storage location into unrelated constructed types.

The rule is intentionally conservative and also catches some pure-looking partial applications:

```fsharp
let alwaysKeep = List.filter (fun _ -> true) // FS0030
```

Although the result is a function value, the right side is an application and the binding has no explicit argument. Automatic generalization follows safe binding forms. A complete function definition with an explicit argument provides such a form, while a partial application may need an annotation or eta expansion.

### Repair intent, not just the diagnostic {#value-restriction-fixes}

There are three common intents:

1. **One value of one concrete type:** add the missing annotation.

   ```fsharp
   let integerBuckets: int list array = Array.create 2 []
   ```

2. **A generic function:** expose the argument instead of storing an unresolved partial application.

   ```fsharp
   let alwaysKeep values = List.filter (fun _ -> true) values
   ```

3. **A fresh generic value on demand:** make construction a function of `unit`.

   ```fsharp
   let makeEmptyBuckets () = Array.create 2 []
   ```

The example demonstrates the third form:

```fsharp:line-numbers
let makeEmptyBuckets () = Array.create 2 []

let integerBuckets: int list array = makeEmptyBuckets ()
let attendeeBuckets: string list array = makeEmptyBuckets ()

let anotherIntegerBuckets: int list array = makeEmptyBuckets ()

printfn
    "Value restriction fixes: ints=%d strings=%d fresh=%b"
    integerBuckets.Length
    attendeeBuckets.Length
    (not (LanguagePrimitives.PhysicalEquality integerBuckets anotherIntegerBuckets))
```
Adding `()` changes semantics: each call allocates a new array. That is correct for a factory, not for a shared singleton cache. Eta-expanding `alwaysKeep` exposes its data argument but retains its pure transformation meaning. An annotation instead commits one value to one type. Choose the remedy from the intended sharing and lifetime, not merely to silence FS0030.

Explicit generic value syntax exists for rare cases, but it is not the default repair. A clear ordinary function is easier to call and makes evaluation timing visible.

## Generic operations introduce capability constraints {#generic-constraints}

An unconstrained `'T` offers no promise of ordering, arithmetic, members, or even F# generic equality. The operation used in a definition adds the smallest required capability:

```fsharp:line-numbers
type Envelope<'T> = { Label: string; Payload: 'T }

let same left right = left = right
let comesBefore left right = compare left right < 0

let first = { Label = "A"; Payload = 2 }

let firstAgain = { Label = "A"; Payload = 2 }

let second = { Label = "B"; Payload = 1 }

let sortedLabels =
    [ second; first ] |> List.sort |> List.map (fun envelope -> envelope.Label)

printfn "Constraints: equal=%b ordered=%b sorted=%A" (same first firstAgain) (comesBefore first second) sortedLabels
```
The important inferred signatures are conceptually:

```fsharp
same : 'T -> 'T -> bool when 'T : equality
comesBefore : 'T -> 'T -> bool when 'T : comparison
```

`=` introduces the **equality constraint**. `compare`, relational operators, and ordered operations such as `List.sort` introduce the **comparison constraint**. Explicit declarations are possible when a public signature needs them:

```fsharp
let sameExplicit<'T when 'T : equality>
    (left: 'T)
    (right: 'T) =
    left = right
```

Inference is preferable when the body already states the requirement. Writing constraints that the implementation does not need unnecessarily rejects useful callers.

Equality and comparison are different capabilities. A type may permit equality while opting out of comparison, and function types satisfy neither constraint. A compiler-supported order also does not prove that the order is meaningful for the domain. Decide whether booking priority should follow record-field order before using generic sorting as business policy.

## Structural capabilities depend on every component {#component-constraints}

`Envelope<'T>` can be constructed for any payload type. Its generated structural equality is usable only when `'T` satisfies equality; its generated structural comparison is usable only when `'T` satisfies comparison. This is a conditional capability, not an unconditional constraint on constructing the record.

The following value is legal:

```fsharp
let functionEnvelope =
    { Label = "f"
      Payload = (fun value -> value) }
```

But this diagnostic-only expression fails with FS0001 because a function payload does not support equality:

```fsharp
let invalid = functionEnvelope = functionEnvelope
```

The same composition rule applies to tuples, lists, options, records, and discriminated unions: the outer structural operation recursively needs the corresponding capability from relevant components. A type may also explicitly customize or suppress generated equality/comparison, so do not infer support merely from surface syntax.

This connects directly to Chapter 7. Equal records get compatible hashes because all participating field equality and hash semantics compose. Chapter 14 will use the comparison constraint for ordered `Map` and `Set` keys and distinguish it from hash-based collection requirements.

## Ordinary generics are not SRTP {#ordinary-generics-vs-srtp}

Most generic F# code in this book uses ordinary parameters written `'T`: `duplicate`, `mapTree`, `same`, and `comesBefore`. Equality and comparison are special F# constraints that work with these ordinary generic signatures.

Operator-general code can instead reveal a signature involving `inline` and a static-member constraint:

```fsharp
let inline add left right = left + right
// The inferred signature is inline and carries a static (+) member constraint.
```

In current F#, simplified **statically resolved type parameter** (SRTP) syntax commonly uses apostrophe-prefixed names such as `'T`; older material and some complex dispatch forms use `^T`. Identify SRTP by the combination of an `inline` definition, compile-time specialization, and a member constraint such as `static member (+)`. Reserve it for generic numeric or member-based abstractions. Functions such as `map`, equality checks, and domain rules usually need only ordinary type parameters.

The shared measured addition deliberately fixes the representation as `int` and varies only the measure. It needs no custom SRTP machinery. Appendix H provides recognition rules and the advanced official entry point; concrete numeric types are usually clearer for domain APIs.

## Units of measure constrain numeric dimensions {#units-of-measure}

Seat counts and elapsed minutes may both be represented by numbers, but adding them is meaningless. F# can attach compile-time measures to supported numeric types:

```fsharp:line-numbers
[<Measure>]
type seat

[<Measure>]
type minute

let addMeasured (left: int<'Measure>) (right: int<'Measure>) = left + right

let capacity = 40<seat>
let requested = addMeasured 2<seat> 3<seat>
let remaining = capacity - requested
let bookingRate = 12.0<seat> / 3.0<minute>

printfn "Measures: requested=%d remaining=%d rate=%.1f" requested remaining bookingRate
```
`[<Measure>] type seat` declares a measure, not a runtime record or wrapper. `int<seat>` is a seat-count quantity. Addition and subtraction require compatible measures; multiplication and division combine them, so `bookingRate` has type `float<seat/minute>`.

The measure variable in `addMeasured` allows any one measure but requires both arguments to share it:

```fsharp
addMeasured : int<'Measure> -> int<'Measure> -> int<'Measure>
```

This diagnostic-only expression fails because its dimensions disagree:

```fsharp
let invalid = 2<seat> + 3<minute> // FS0001
```

Units are compile-time information and are erased at runtime. The numeric representation and reflected value stay unchanged, while serialization and non-F# interfaces carry the plain number. The example therefore prints ordinary numbers even though the compiler checked their measures.

At an input boundary, parse and validate the raw number, then restore the trusted measure explicitly:

```fsharp
let seatsFromInt raw : int<seat> =
    LanguagePrimitives.Int32WithMeasure raw
```

`Int32WithMeasure` attaches a compile-time measure; it does not check positivity or capacity. `-3<seat>` is dimensionally a seat count but still violates this domain's likely invariant. Chapter 12 combines measured representations with private construction and validation.

Do not confuse the `unit` type, whose sole value is `()`, with a unit **of measure** such as `seat`. The words overlap; their roles do not.

## Exercises {#exercises}

### Exercise 1: infer generality and constraints {#exercise-01}

Write the most general signature for each definition and explain which operation introduces any constraint:

```fsharp
let pair left right = left, right
let contains value values = List.contains value values
let orderedPair left right = if left <= right then left, right else right, left
let wrap value = { Label = "value"; Payload = value }
```

Then decide which definitions can accept a function value as an argument.


::: details Answer

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

:::

### Exercise 2: repair two value restrictions {#exercise-02}

These diagnostic-only bindings both report FS0030:

```fsharp
let buckets = Array.create 2 []
let keepAll = List.filter (fun _ -> true)
```

Provide three intentional repairs: one shared `BookingRequest list array`, one fresh generic array per call, and one generic `keepAll` function. For each, state whether construction happens once or per call.


::: details Answer

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

:::

### Exercise 3: preserve dimensions across a boundary {#exercise-03}

Define `seat` and `minute`, then write:

- `throughput : float<seat> -> float<minute> -> float<seat/minute>`;
- a boundary function that converts a validated plain `int` into `int<seat>`;
- an expression that should fail because it adds seats to minutes.

Explain what measure information remains after serialization and name one booking invariant that measures alone cannot enforce.


::: details Answer

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

:::


Chapter 12 now uses these type capabilities deliberately: private representations and smart constructors will prevent callers from constructing invalid domain values.

## Sources {#sources}

- [Microsoft Learn: Generics](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/generics/)
- [Microsoft Learn: Automatic generalization](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/generics/automatic-generalization)
- [Microsoft Learn: Generic constraints](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/generics/constraints)
- [Microsoft Learn: Units of measure](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/units-of-measure)
- [FSharp.Core: LanguagePrimitives](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-languageprimitives.html)
