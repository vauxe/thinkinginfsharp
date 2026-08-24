---
title: "Chapter 11: Generics, Constraints, and Units"
description: "Understand automatic generalization, the value restriction, equality and comparison constraints, conditional structural capabilities, and units of measure."
translationKey: part-02/ch-11-generics-constraints
kind: chapter
part: 2
chapter: 11
status: complete
verifiedWith:
  fsharp: "10"
  dotnetSdk: "10.0.301"
exampleIds:
  - ch11-generics-constraints
exerciseIds:
  - ch11-exercise-01
  - ch11-exercise-02
  - ch11-exercise-03
termIds:
  - automatic-generalization
  - comparison-constraint
  - equality-constraint
  - generic-type-parameter
  - statically-resolved-type-parameter
  - type-inference
  - unit-of-measure
  - value-restriction
sources:
  - id: microsoft-generics
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/generics/
    checked: "2026-08-24"
  - id: microsoft-automatic-generalization
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/generics/automatic-generalization
    checked: "2026-08-24"
  - id: microsoft-generic-constraints
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/generics/constraints
    checked: "2026-08-24"
  - id: microsoft-units-of-measure
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/units-of-measure
    checked: "2026-08-24"
  - id: fsharp-core-language-primitives
    url: https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-languageprimitives.html
    checked: "2026-08-24"
---

# Chapter 11: Generics, Constraints, and Units {#overview}

`mapTree` from Chapter 10 never inspects a leaf's concrete type. The compiler therefore inferred one implementation that works for `BookingTree<int>`, `BookingTree<string>`, and many other instantiations. By contrast, sorting leaves needs an ordering operation, and adding quantities needs compatible numeric dimensions. “Generic” does not mean “all operations are available”; it means the definition states exactly which type facts it requires.

This chapter follows those requirements from unconstrained functions, through the value restriction and structural equality/comparison constraints, to units of measure. The goal is not to decorate every signature. It is to read why a type variable is general, why it is constrained, and why the compiler sometimes refuses to generalize a binding.

## What you will be able to do {#outcomes}

By the end of this chapter, you should be able to:

- read repeated type variables as consistency requirements;
- explain when F# automatically generalizes a definition;
- diagnose FS0030 value-restriction errors from binding shape and intent;
- choose among an annotation, an explicit parameter, and a unit-taking factory;
- read and write `'T : equality` and `'T : comparison` constraints;
- explain how record, tuple, list, and union capabilities depend on component types;
- distinguish ordinary `'T` generics from statically resolved `^T` parameters;
- use units of measure to reject dimensionally invalid arithmetic at compile time;
- state what units do not validate and what happens to them at runtime.

## Generality is inferred from independence {#automatic-generalization}

The shared function uses its input only to place two copies in a list:

<<< @/../examples/scripts/ch11-generics-constraints.fsx#automatic-generalization{fsharp:line-numbers} [ch11-generics-constraints.fsx]

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

The explicit version adds no information here and is usually noisier. Let inference expose the most general safe signature, then add annotations when they communicate a public contract or resolve real ambiguity.

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

This diagnostic-only definition is deliberately absent from the valid shared script:

```fsharp
let ambiguousBuckets = Array.create 2 []
```

F# 10 reports FS0030 and shows an inferred weak type such as `'_a list array`. The expression contains an unresolved element type, but the binding is neither a complete explicit-argument function nor a simple immutable data term eligible for safe generalization.

The restriction matters because an array has mutable element slots. If one array value could be viewed first as `int list array` and then as `string list array`, a write through one view would invalidate the other. The compiler must not turn one storage location into unrelated constructed types.

The rule is intentionally conservative and also catches some pure-looking partial applications:

```fsharp
let alwaysKeep = List.filter (fun _ -> true) // FS0030
```

Although the result is a function value, the right side is an application without an explicit argument on the binding. Do not conclude that “functions are never generic values”; conclude that automatic generalization uses safe binding forms rather than attempting a whole-program proof of purity and captured state.

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

The shared script demonstrates the third form:

<<< @/../examples/scripts/ch11-generics-constraints.fsx#value-restriction-fixes{fsharp:line-numbers} [ch11-generics-constraints.fsx]

Adding `()` changes semantics: each call allocates a new array. That is correct for a factory, not for a singleton cache callers are meant to share. Eta-expanding `alwaysKeep` exposes its data argument but retains its pure transformation meaning. An annotation instead commits a value to one type. Select the remedy from ownership and lifetime, not from whichever edit makes FS0030 disappear.

Explicit generic value syntax exists for rare cases, but it is not the default repair. A clear ordinary function is easier to call and makes evaluation timing visible.

## Generic operations introduce capability constraints {#generic-constraints}

An unconstrained `'T` offers no promise of ordering, arithmetic, members, or even F# generic equality. The operation used in a definition adds the smallest required capability:

<<< @/../examples/scripts/ch11-generics-constraints.fsx#equality-comparison{fsharp:line-numbers} [ch11-generics-constraints.fsx]

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

Operator-general code can instead reveal a signature involving `inline`, `^T`, and a static-member constraint:

```fsharp
let inline add left right = left + right
// Caret-prefixed operand types are resolved through a static (+) member constraint.
```

The exact inferred signature may use separate caret-prefixed types for the two operands and another result type, according to the available `+` member. A parameter such as `^T` is a **statically resolved type parameter** (SRTP), part of a separate F# mechanism resolved at inline call sites. It is useful for selected generic numeric and member-based abstractions, but it is not a prerequisite for ordinary generic programming. Do not add `inline` and caret-prefixed parameters to `map`, equality checks, or domain functions merely because their signatures contain type variables.

The shared measured addition deliberately fixes the representation as `int` and varies only the measure. It needs no custom SRTP machinery. Appendix H provides recognition rules and the advanced official entry point; concrete numeric types are usually clearer for domain APIs.

## Units of measure constrain numeric dimensions {#units-of-measure}

Seat counts and elapsed minutes may both be represented by numbers, but adding them is meaningless. F# can attach compile-time measures to supported numeric types:

<<< @/../examples/scripts/ch11-generics-constraints.fsx#units-of-measure{fsharp:line-numbers} [ch11-generics-constraints.fsx]

`[<Measure>] type seat` declares a measure, not a runtime record or wrapper. `int<seat>` is a seat-count quantity. Addition and subtraction require compatible measures; multiplication and division combine them, so `bookingRate` has type `float<seat/minute>`.

The measure variable in `addMeasured` allows any one measure but requires both arguments to share it:

```fsharp
addMeasured : int<'Measure> -> int<'Measure> -> int<'Measure>
```

This diagnostic-only expression fails because its dimensions disagree:

```fsharp
let invalid = 2<seat> + 3<minute> // FS0001
```

Units are erased at runtime. They do not change the underlying numeric representation, cannot be discovered with runtime reflection, and are not automatically carried by serialization or a non-F# boundary. That is why the example output prints plain numbers even though the compiler checked the measures.

At an input boundary, parse and validate the raw number, then restore the trusted measure explicitly:

```fsharp
let seatsFromInt raw : int<seat> =
    LanguagePrimitives.Int32WithMeasure raw
```

`Int32WithMeasure` attaches a compile-time measure; it does not check positivity or capacity. `-3<seat>` is dimensionally a seat count but still violates this domain's likely invariant. Chapter 12 combines measured representations with private construction and validation.

Do not confuse the `unit` type, whose sole value is `()`, with a unit **of measure** such as `seat`. The words overlap; their roles do not.

## Run the shared example {#run-example}

From the repository root:

```console
dotnet fsi --exec examples/scripts/ch11-generics-constraints.fsx
```

The five deterministic lines demonstrate one generalized function at two types, a safe simple generic value, a fresh-value factory that avoids the value restriction, inferred equality/comparison constraints through a generic record, and dimension-checked arithmetic.

The invalid FS0030 and FS0001 examples remain diagnostic-only so the shared script stays warning-free. Appendix E collects compiler-diagnostic labs separately.

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

### Exercise 2: repair two value restrictions {#exercise-02}

These diagnostic-only bindings both report FS0030:

```fsharp
let buckets = Array.create 2 []
let keepAll = List.filter (fun _ -> true)
```

Provide three intentional repairs: one shared `BookingRequest list array`, one fresh generic array per call, and one generic `keepAll` function. For each, state whether construction happens once or per call.

### Exercise 3: preserve dimensions across a boundary {#exercise-03}

Define `seat` and `minute`, then write:

- `throughput : float<seat> -> float<minute> -> float<seat/minute>`;
- a boundary function that converts a validated plain `int` into `int<seat>`;
- an expression that should fail because it adds seats to minutes.

Explain what measure information remains after serialization and name one booking invariant that measures alone cannot enforce.

[Read the chapter solutions](../solutions/ch-11-generics-constraints).

## Model review {#model-review}

- Automatic generalization quantifies type variables only when the definition safely ignores a concrete type.
- The value restriction prevents one nongeneralizable value from being used as incompatible constructed types.
- An annotation specializes one value; an explicit parameter exposes a generic function; `()` can make a fresh-value factory.
- Equality and comparison constraints arise from operations and compose through structural fields.
- Ordinary `'T` generics do not require SRTP; `^T` with `inline` is a separate advanced mechanism.
- Units of measure reject dimensional mistakes at compile time, are erased at runtime, and do not enforce value-range invariants.

Chapter 12 now uses these type capabilities deliberately: private representations and smart constructors will prevent callers from constructing invalid domain values.

## Sources {#sources}

- [Microsoft Learn: Generics](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/generics/)
- [Microsoft Learn: Automatic generalization](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/generics/automatic-generalization)
- [Microsoft Learn: Generic constraints](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/generics/constraints)
- [Microsoft Learn: Units of measure](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/units-of-measure)
- [FSharp.Core: LanguagePrimitives](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-languageprimitives.html)
