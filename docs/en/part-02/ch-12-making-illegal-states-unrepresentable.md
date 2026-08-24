---
title: "Chapter 12: Making Illegal States Unrepresentable"
description: "Protect domain invariants with private representations, companion modules, smart constructors, and explicit file-level API boundaries."
translationKey: part-02/ch-12-making-illegal-states-unrepresentable
kind: chapter
part: 2
chapter: 12
status: complete
verifiedWith:
  fsharp: "10"
  dotnetSdk: "10.0.301"
exampleIds:
  - ch12-making-illegal-states-unrepresentable
exerciseIds:
  - ch12-exercise-01
  - ch12-exercise-02
  - ch12-exercise-03
termIds:
  - access-control
  - invariant
  - private-representation
  - result
  - signature-file
  - smart-constructor
  - unit-of-measure
sources:
  - id: microsoft-access-control
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/access-control
    checked: "2026-08-24"
  - id: microsoft-signature-files
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/signature-files
    checked: "2026-08-24"
  - id: microsoft-discriminated-unions
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/discriminated-unions
    checked: "2026-08-24"
  - id: microsoft-modules
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/modules
    checked: "2026-08-24"
  - id: microsoft-component-design
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/style-guide/component-design-guidelines
    checked: "2026-08-24"
---

# Chapter 12: Making Illegal States Unrepresentable {#overview}

A function named `validateCapacity` does not protect a plain `int`. Any caller can skip it, store `0`, and pass that value into code that assumes capacity is positive. Validation has happened somewhere, but the result carries no proof.

F# can turn that convention into an API boundary: expose a `Capacity` type, hide its representation, and make the only public construction path return `Result<Capacity, CapacityError>`. After a caller obtains `Capacity`, downstream code may rely on the invariant established by the constructor instead of checking the same integer repeatedly.

“Impossible” here means impossible through the supported public API under its stated boundary assumptions. It does not mean corrupted storage, hostile reflection, unsafe primitives, null interoperation, or a concurrency race have ceased to exist.

## What you will be able to do {#outcomes}

By the end of this chapter, you should be able to:

- distinguish a type abbreviation from a protected domain type;
- expose a type while hiding its union or record representation;
- place smart constructors and accessors in a same-named companion module;
- return typed rejection reasons for expected invalid input;
- compose protected component types into a larger valid model;
- explain the scopes of `private`, `internal`, and `public`;
- use a `.fsi` signature to hide representation across files;
- preserve an invariant in every transformation that can create a new value;
- decide when representation hiding earns its cost and when it is overdesign.

## Validation without a protected result is bypassable {#bypassable-validation}

This type abbreviation creates no new runtime or compile-time distinction:

```fsharp
type Capacity = int<seat>

let validateCapacity capacity =
    if capacity > 0<seat> then Ok capacity
    else Error "capacity must be positive"
```

Even if one path calls `validateCapacity`, another path can still write `let capacity: Capacity = 0<seat>`. A type abbreviation is another name for the same type; it does not own construction.

A public record has the same weakness when callers can fill its fields directly. Validation returning the unchanged public representation is useful at an input boundary, but it does not make later code distinguish validated from unvalidated data.

The repair needs two parts:

1. a distinct type whose representation callers cannot construct;
2. a function that checks raw input before it returns that type.

Either part alone is incomplete. A private wrapper with a public unchecked constructor still permits invalid values; a validator returning raw `int` still carries no proof.

## Expose the type and hide the constructor {#private-representation}

The shared script defines the domain inside an explicit module:

<<< @/../examples/scripts/ch12-making-illegal-states-unrepresentable.fsx#private-capacity{fsharp:line-numbers} [ch12-making-illegal-states-unrepresentable.fsx]

Notice the modifier position:

```fsharp
type Capacity = private Capacity of int<seat>
```

The type `Capacity` is visible, while its union representation is private to the enclosing `BookingDomain` module. By contrast, `type private Capacity = ...` would hide the type itself, making it unusable in a public signature.

The outer `Capacity` name denotes the type; the inner case also constructs or patterns on its representation. Code outside the access boundary can pass a `Capacity`, store it, and call public functions over it, but cannot invoke that case.

This diagnostic-only bypass was verified with F# 10:

```fsharp
let invalid = BookingDomain.Capacity 0<BookingDomain.seat>
// FS1093: the union cases or fields of Capacity are not accessible here.
```

F# union cases are not individually less accessible than their union representation. Hiding the representation hides all construction/deconstruction cases together. Private record representation similarly hides direct record construction and field-pattern access from consumers.

## The companion module owns construction and observation {#companion-module}

F# allows a type and module to share a name. This produces a focused API:

```fsharp
Capacity.create : int -> Result<Capacity, CapacityError>
Capacity.value : Capacity -> int<seat>
```

The module is inside the same enclosing `BookingDomain` module, so it can construct and pattern-match the private case. Callers use qualified names and never need the representation.

`create` is a **smart constructor**. It accepts boundary-friendly raw data, checks positivity, attaches the `seat` measure, and returns either a protected value or a typed expected error. It does not throw for ordinary rejection.

`value` is a deliberate observation function. Returning the measured integer lets adapters display or persist it, but does not let them turn an arbitrary integer back into `Capacity` without calling `create` again.

Keep the trusted surface small. Every function inside the enclosing module that can invoke `Capacity` directly is part of the invariant's implementation boundary. `private` stops outside callers; it does not prove that inside code is correct.

## Smart construction may validate and normalize {#validation-and-normalization}

The other protected components show two policies:

<<< @/../examples/scripts/ch12-making-illegal-states-unrepresentable.fsx#protected-components{fsharp:line-numbers} [ch12-making-illegal-states-unrepresentable.fsx]

`EventId.create` rejects blank input and trims surrounding whitespace. `SeatCount.create` rejects non-positive counts and restores the compile-time measure. Once constructed:

- an `EventId` is nonblank and normalized according to the chosen trim rule;
- a `SeatCount` is positive and dimensioned as seats.

Normalization is domain policy, not harmless cleanup. Trimming is appropriate here, but silently changing case might be wrong if external identifiers are case-sensitive. State each normalization rule and test it alongside rejection.

Error types retain the rejected fact: `NonPositiveSeatCount actual` is more useful than `Error "invalid"`. Formatting and localization remain outside the constructor.

Do not publish an unchecked escape hatch merely for convenience. If trusted migration code needs one, keep it private or narrowly internal and test that boundary explicitly.

## Valid components can make a larger state valid {#composing-invariants}

The request model combines the two component proofs and also hides its record representation:

<<< @/../examples/scripts/ch12-making-illegal-states-unrepresentable.fsx#private-request{fsharp:line-numbers} [ch12-making-illegal-states-unrepresentable.fsx]

`BookingRequest.create` first creates an `EventId`, then a `SeatCount`, mapping each component error into request context. Only after both succeed does it construct the private record. The resulting value cannot contain a blank identifier or non-positive seat count through this API.

This result pipeline preserves the first error, as Chapter 9 explained. If a UI must accumulate independent errors, use an accumulating validator later; changing the representation does not decide error-combination policy.

The private request record is a design choice, not a universal requirement. A public record containing only already-protected `EventId` and `SeatCount` fields would still preserve those two component invariants and offer callers convenient pattern matching. Hide the outer record when it has cross-field rules, must control construction, or likely needs representation evolution. Leave it public when transparent data composition is the intended API.

## Every producer must preserve the invariant {#invariant-preservation}

A constructor is not the only function that can create a value. Updates, arithmetic, parsing, database reads, and deserialization are construction paths too.

For immutable protected values, a transformation can either:

- return an existing value unchanged;
- calculate raw candidate data and call the smart constructor again;
- prove the transformation preserves the invariant and construct inside the trusted module.

For example, subtracting reserved seats from capacity can reach zero. Whether zero means “sold out but valid capacity,” “remaining seats rather than capacity,” or an invalid value is a modeling decision. Do not reuse `Capacity` for a quantity with different invariants merely because both are `int<seat>` underneath.

Avoid getters that expose mutable internal objects. The wrappers here contain immutable strings and numbers. If a protected type contains an array or mutable .NET object, returning it directly lets callers mutate state behind the proof; return a copy, read-only view, or operations that preserve the invariant.

## `private`, `internal`, and signatures protect different boundaries {#access-boundaries}

F# access control is lexical and assembly-aware:

| Mechanism | Visible from | Appropriate use |
| --- | --- | --- |
| `private` | enclosing type or module | protect representation from sibling modules and later files |
| `internal` | any code in the same assembly | assembly implementation detail, not a strong invariant barrier inside that assembly |
| `public` or omitted default | all callers allowed by the containing API | intended consumer surface |
| `.fsi` signature | only declarations exposed by the signature are visible outside its implementation file | stable cross-file/component abstraction |

Every F# file is implicitly a module when no explicit top-level namespace/module changes the organization. A top-level module is contained in one file. Therefore a private representation and its companion module can share one file-level module, while another file cannot reopen that module to reach the private case.

In the shared script, both type and companion module sit inside `BookingDomain`; code after that module is outside the private boundary even though it is in the same physical `.fsx` file. Scope is determined by the enclosing module, not merely by the filename.

### A signature file makes the cross-file contract explicit {#signature-file}

For a stable library API, `BookingDomain.fsi` can expose an abstract type:

```fsharp
namespace Booking.Domain

[<Measure>]
type seat

type CapacityError =
    | NonPositiveCapacity of actual: int

type Capacity

module Capacity =
    val create: raw: int -> Result<Capacity, CapacityError>
    val value: capacity: Capacity -> int<seat>
```

The corresponding `BookingDomain.fs` contains the private union representation and implementations. In the project, the `.fsi` file must precede its matching `.fs` file. Later files see `type Capacity` and the declared functions, but no union case. Items omitted from the signature are private to the implementation file.

Signature files add maintenance cost because public changes must agree in two files. They are valuable once an API is stable or representation hiding across a component boundary matters; they need not be added mechanically to every exploratory file. Chapter 16 returns to project order and signatures as part of multi-file design.

## State the guarantee honestly at external boundaries {#boundary-limits}

Private representation secures ordinary compiled callers. Data arriving from JSON, a database, environment variables, or another service is raw again and must pass through validation. Units of measure are erased, so persistence cannot carry their proof either.

Reflection-based serializers, unsafe code, `Unchecked.defaultof`, legacy nulls, or corrupted persisted bytes may bypass normal construction assumptions. Configure adapters to serialize an explicit DTO and rebuild the domain value through smart constructors. Chapter 19 handles null boundaries; later capstone slices handle persistence and concurrency.

A valid `Capacity` also does not prevent two concurrent requests from overselling it. The type protects a local value invariant, not an atomic storage transition. Guarantees should name their scope.

## Use the pattern where it removes real risk {#avoiding-overdesign}

Representation hiding earns its cost when:

- a stable invariant is easy to violate with a primitive or public record;
- values cross several layers or have many producers;
- repeated defensive checks already appear;
- invalid data would cause costly or security-relevant behavior;
- representation evolution should not break consumers.

It is likely overdesign when a value is a short-lived local, its components already enforce every rule, or the wrapper exposes unchecked construction and therefore proves nothing. A public discriminated union is often better when all cases are legal and callers benefit from exhaustive matching.

Start with the smallest truthful barrier. Protect `EventId` if nonblank identity matters everywhere. Do not wrap every display label merely to make the type list longer.

## Run the shared example {#run-example}

From the repository root:

```console
dotnet fsi --exec examples/scripts/ch12-making-illegal-states-unrepresentable.fsx
```

The five deterministic lines cover accepted capacity, rejected capacity, identifier normalization, valid request construction, and both request rejection paths:

<<< @/../examples/scripts/ch12-making-illegal-states-unrepresentable.fsx#smart-constructor-results{fsharp:line-numbers} [ch12-making-illegal-states-unrepresentable.fsx]

## Exercises {#exercises}

### Exercise 1: protect a percentage {#exercise-01}

Replace `type FillRate = decimal` with a private representation whose valid values are from `0m` through `1m`, inclusive. Define a typed error carrying the rejected value, `FillRate.create`, and `FillRate.value`. Explain why a type abbreviation plus validator is insufficient.

### Exercise 2: choose a transparent or private outer record {#exercise-02}

Suppose `EventId` and `SeatCount` are already protected. Compare these designs:

```fsharp
type BookingRequest = { EventId: EventId; Seats: SeatCount }
type BookingRequest = private { EventId: EventId; Seats: SeatCount }
```

Give one requirement that favors each design. If the private design is selected, list the minimum constructor and observation functions callers need.

### Exercise 3: expose a cross-file capacity API {#exercise-03}

Write the public portion of a `.fsi` signature for `Capacity` plus a `tryReserve : SeatCount -> Capacity -> Result<Capacity, ReservationError>` operation. State the file order, where the union case may be used, and how the operation preserves the positive-capacity invariant when a reservation exactly fills the event.

[Read the chapter solutions](../solutions/ch-12-making-illegal-states-unrepresentable).

## Model review {#model-review}

- A distinct private representation plus a checked constructor carries proof that a raw validator cannot.
- A same-named module groups creation and observation while retaining representation access inside one trusted scope.
- Component invariants compose, but outer cross-field rules may still require private construction.
- `private`, `internal`, and `.fsi` signatures guard different lexical or assembly boundaries.
- Every producer and external adapter must preserve or re-establish the invariant.
- “Unrepresentable” is relative to the supported API; it does not solve corruption, null interop, or concurrency.
- Protect values whose invariants matter broadly; transparent types remain better when exhaustive construction is the feature.

The second-part capstone now turns these patterns into a compiled booking domain library with tests proving that its public API rejects invalid capacity, identifiers, and states.

## Sources {#sources}

- [Microsoft Learn: Access control](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/access-control)
- [Microsoft Learn: Signature files](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/signature-files)
- [Microsoft Learn: Discriminated unions](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/discriminated-unions)
- [Microsoft Learn: Modules](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/modules)
- [Microsoft Learn: F# component design guidelines](https://learn.microsoft.com/en-us/dotnet/fsharp/style-guide/component-design-guidelines)
