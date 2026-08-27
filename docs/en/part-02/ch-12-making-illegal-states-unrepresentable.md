---
title: "Chapter 12: Making Illegal States Unrepresentable"
description: "Protect domain invariants with private representations, companion modules, smart constructors, and explicit file-level API boundaries."
translationKey: part-02/ch-12-making-illegal-states-unrepresentable
---

# Chapter 12: Making Illegal States Unrepresentable {#overview}

A function named `validateCapacity` does not protect a plain `int`. Any caller can skip it, store `0`, and pass that value into code that assumes capacity is positive. The validated result is indistinguishable from an unchecked integer.

F# can enforce that rule through the API. Expose a `Capacity` type, hide its representation, and make the only public constructor return `Result<Capacity, CapacityError>`. Once a caller obtains `Capacity`, downstream code can rely on its invariant instead of checking the same integer repeatedly.

“Impossible” here means impossible through the supported public API under the stated assumptions. Corrupted storage, hostile reflection, unsafe primitives, null interoperation, and concurrency races still exist.

## Validation without a protected result is bypassable {#bypassable-validation}

This type abbreviation creates no new runtime or compile-time distinction:

```fsharp
type Capacity = int<seat>

let validateCapacity capacity =
    if capacity > 0<seat> then Ok capacity
    else Error "capacity must be positive"
```

Even if one path calls `validateCapacity`, another can write `let capacity: Capacity = 0<seat>`. A type abbreviation is only another name for the same type; it cannot control construction.

A public record has the same weakness when callers can fill its fields directly. Validation that returns the unchanged public representation is useful when accepting input, but later code still cannot distinguish validated from unchecked data.

The repair needs two parts:

1. a distinct type whose representation callers cannot construct;
2. a function that checks raw input before it returns that type.

Either part alone is incomplete. A private wrapper with a public unchecked constructor still permits invalid values; a validator returning raw `int` gives later code no additional guarantee.

## Expose the type and hide the constructor {#private-representation}

The example defines the domain inside an explicit module:

```fsharp:line-numbers
type CapacityError = NonPositiveCapacity of actual: int

type Capacity = private Capacity of int<seat>

module Capacity =
    let create raw =
        if raw > 0 then
            raw |> LanguagePrimitives.Int32WithMeasure<seat> |> Capacity |> Ok
        else
            Error(NonPositiveCapacity raw)

    let value (Capacity capacity) = capacity
```
Notice the modifier position:

```fsharp
type Capacity = private Capacity of int<seat>
```

The type `Capacity` is visible, while its union representation is private to the enclosing `BookingDomain` module. By contrast, `type private Capacity = ...` would hide the type itself, making it unusable in a public signature.

The outer `Capacity` name denotes the type; the inner case constructs or matches its representation. Code outside `BookingDomain` can pass and store a `Capacity` or call public functions over it, but cannot invoke that case.

This diagnostic-only bypass was verified with F# 10:

```fsharp
let invalid = BookingDomain.Capacity 0<BookingDomain.seat>
// FS1093: the union cases or fields of Capacity are not accessible here.
```

F# union cases are not individually less accessible than their union representation. Hiding the representation hides all construction/deconstruction cases together. Private record representation similarly hides direct record construction and field-pattern access from consumers.

## The companion module groups construction and observation {#companion-module}

F# allows a type and module to share a name. This produces a focused API:

```fsharp
Capacity.create : int -> Result<Capacity, CapacityError>
Capacity.value : Capacity -> int<seat>
```

The module is inside the same enclosing `BookingDomain` module, so it can construct and pattern-match the private case. Callers use qualified names and never need the representation.

`create` is a **smart constructor**. It accepts raw data, checks positivity, attaches the `seat` measure, and returns either a protected value or a typed expected error. It does not throw for an expected rejection.

`value` is a deliberate observation function. Returning the measured integer lets adapters display or persist it, but does not let them turn an arbitrary integer back into `Capacity` without calling `create` again.

Keep the trusted code small. Every function that can invoke the private `Capacity` case is responsible for preserving the invariant. `private` blocks outside callers; it does not prove the code inside the module correct.

## Smart construction may validate and normalize {#validation-and-normalization}

The other protected components show two policies:

```fsharp:line-numbers
type EventIdError = | BlankEventId

type EventId = private EventId of string

module EventId =
    let create raw =
        if String.IsNullOrWhiteSpace raw then
            Error BlankEventId
        else
            raw.Trim() |> EventId |> Ok

    let value (EventId eventId) = eventId

type SeatCountError = NonPositiveSeatCount of actual: int

type SeatCount = private SeatCount of int<seat>

module SeatCount =
    let create raw =
        if raw > 0 then
            raw |> LanguagePrimitives.Int32WithMeasure<seat> |> SeatCount |> Ok
        else
            Error(NonPositiveSeatCount raw)

    let value (SeatCount seats) = seats
```
`EventId.create` rejects blank input and trims surrounding whitespace. `SeatCount.create` rejects non-positive counts and restores the compile-time measure. Once constructed:

- an `EventId` is nonblank and normalized according to the chosen trim rule;
- a `SeatCount` is positive and dimensioned as seats.

Normalization is domain policy, not harmless cleanup. Trimming is appropriate here, but silently changing case might be wrong if external identifiers are case-sensitive. State each normalization rule and test it alongside rejection.

Error types retain the rejected fact: `NonPositiveSeatCount actual` is more useful than `Error "invalid"`. Formatting and localization remain outside the constructor.

Do not publish an unchecked escape hatch merely for convenience. If trusted migration code needs one, keep it private or narrowly `internal` and test that exceptional path directly.

## Compose protected values into larger states {#composing-invariants}

The request model combines the two protected component types and also hides its record representation:

```fsharp:line-numbers
type BookingRequestError =
    | InvalidEventId of EventIdError
    | InvalidSeatCount of SeatCountError

type BookingRequest =
    private
        { EventId: EventId
          Seats: SeatCount }

module BookingRequest =
    let create rawEventId rawSeats =
        rawEventId
        |> EventId.create
        |> Result.mapError InvalidEventId
        |> Result.bind (fun eventId ->
            rawSeats
            |> SeatCount.create
            |> Result.mapError InvalidSeatCount
            |> Result.map (fun seats -> { EventId = eventId; Seats = seats }))

    let eventId request = request.EventId |> EventId.value

    let seats request = request.Seats |> SeatCount.value
```
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

## `private`, `internal`, and signatures protect different scopes {#access-boundaries}

F# access control is lexical and assembly-aware:

| Mechanism | Visible from | Appropriate use |
| --- | --- | --- |
| `private` | enclosing type or module | protect representation from sibling modules and later files |
| `internal` | any code in the same assembly | assembly implementation detail, not a strong invariant barrier inside that assembly |
| `public` or omitted default | all callers allowed by the containing API | intended consumer surface |
| `.fsi` signature | only declarations exposed by the signature are visible outside its implementation file | stable cross-file/component abstraction |

Every F# file is implicitly a module when no explicit top-level namespace/module changes the organization. A top-level module is contained in one file. Therefore a private representation and its companion module can share one file-level module, while another file cannot reopen that module to reach the private case.

If both type and companion module sit inside an explicit `BookingDomain` module, code after that module is outside the private boundary even in the same physical `.fsx` file. Scope is determined by the enclosing module, not merely by the filename.

### A signature file defines the cross-file API {#signature-file}

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

## Revalidate data that enters from outside {#boundary-limits}

Private representation constrains ordinary compiled callers. Data arriving from JSON, a database, environment variables, or another service is raw again and must pass through validation. Units of measure are erased, so persisted numbers carry no measure information.

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

Start with the smallest type that removes real risk. Protect `EventId` if nonblank identity matters everywhere. Do not wrap every display label merely to make the type list longer.

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

## Part II checkpoint {#part-checkpoint}

Test the constructors above with a valid request, a blank event ID, non-positive capacity, and non-positive seat count. The valid request must be constructed; each invalid value must fail at its own boundary. Later chapters add state transitions and external adapters.

[Continue to Chapter 13](../part-03/ch-13-composition-pipeline-api), which begins composing these typed operations.

## Sources {#sources}

- [Microsoft Learn: Access control](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/access-control)
- [Microsoft Learn: Signature files](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/signature-files)
- [Microsoft Learn: Discriminated unions](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/discriminated-unions)
- [Microsoft Learn: Modules](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/modules)
- [Microsoft Learn: F# component design guidelines](https://learn.microsoft.com/en-us/dotnet/fsharp/style-guide/component-design-guidelines)
