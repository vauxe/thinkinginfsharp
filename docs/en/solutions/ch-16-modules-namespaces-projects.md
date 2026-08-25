---
title: "Chapter 16 Solutions"
description: "Order a multi-file project, repair a namespace-level binding, and propagate an explicit nullable-reference contract through a wrapper."
translationKey: solutions/ch-16-modules-namespaces-projects
---

# Chapter 16 Solutions {#overview}

Solve structure problems by writing the dependency graph before editing the project. Then let declarations express names and let compiler settings express boundary contracts.

[Return to Chapter 16](../part-03/ch-16-modules-namespaces-projects).

## Exercise 1: write the dependency order {#exercise-01}

### Valid project order {#exercise-01-order}

The dependencies are:

```text
Domain.fs  ──▶  Pricing.fs  ──▶  Program.fs
     └──────────────────────────▶
```

Therefore the project items are:

```xml
<ItemGroup>
  <Compile Include="Domain.fs" />
  <Compile Include="Pricing.fs" />
  <Compile Include="Program.fs" />
</ItemGroup>
```

`Domain.fs` provides the independent vocabulary. `Pricing.fs` may use it because the compiler has already processed it. `Program.fs` comes last because it consumes both providers.

More than one topological order can be valid when files are independent. Here the stated dependencies force all three positions. Do not alphabetize the items unless alphabetical order also happens to respect the graph.

### Diagnose the reversed order {#exercise-01-diagnostic}

This order is invalid:

```xml
<ItemGroup>
  <Compile Include="Pricing.fs" />
  <Compile Include="Domain.fs" />
  <Compile Include="Program.fs" />
</ItemGroup>
```

Compilation reaches `Pricing.fs` before `Domain.fs`. `FS0039` appears at its `open` declaration or first qualified use of the missing `Domain` module or one of its types. The exact location depends on which unavailable name is encountered first; the cause is the same forward reference.

Folders do not participate in F# name resolution or compiler input order. Moving `Domain.fs` into a `Core` folder changes only its path until you also update the project item; it does not make the compiler see the file earlier and does not add `Core` to the namespace. Source declarations establish names, and `<Compile>` items establish order.

## Exercise 2: repair scope and choose qualification {#exercise-02}

### Put the value in a module {#exercise-02-fix}

The requested public name is obtained by placing `Text` under the `Booking` namespace:

```fsharp
namespace Booking

module Text =
    let normalize (raw: string) = raw.Trim()
```

The namespace may contain the module, and the module may contain the `let`-bound function. Moving only the `let` one indentation level has no meaning without the `module Text =` declaration.

### Qualified and opened calls {#exercise-02-open}

A consumer can retain the whole owner at the call site:

```fsharp
module Booking.Consumer

let normalizeQualified raw =
    Booking.Text.normalize raw
```

Or it can open the module before the following use:

```fsharp
module Booking.Consumer

open Booking.Text

let normalizeOpened raw =
    normalize raw
```

`open Booking.Text` makes accessible members available by their short names in the following scope. It does not rename `normalize`, load or reference an assembly, change file order, copy the function, or make a private function public. Without the defining file earlier in this project—or the defining assembly referenced from another project—both versions fail.

Qualification is the better default when a short name is ambiguous or only used once. A focused `open` is reasonable when the consumer repeatedly speaks that module's vocabulary.

## Exercise 3: propagate one nullable boundary {#exercise-03}

### State the wrapper's real contract {#exercise-03-contract}

This compact model makes both the inner and outer parameter explicit:

```fsharp
open System

type BookingIdError =
    | MissingBookingId

type BookingId = private BookingId of string

module BookingId =
    let create (raw: string | null) =
        match raw with
        | null -> Error MissingBookingId
        | value when String.IsNullOrWhiteSpace value -> Error MissingBookingId
        | value -> Ok(BookingId(value.Trim()))

type BookingRequestError =
    | InvalidBookingId of BookingIdError

type BookingRequest =
    private
        { Id: BookingId
          Seats: int }

module BookingRequest =
    let create (rawId: string | null) seats =
        match BookingId.create rawId with
        | Error error -> Error(InvalidBookingId error)
        | Ok bookingId -> Ok { Id = bookingId; Seats = seats }
```

`BookingRequest.create` promises that callers may supply `null`, then immediately delegates validation and preserves the error context. The production chapter example additionally validates `SeatCount`; that separate invariant does not change the nullable-reference reasoning.

### Test both sides of the boundary {#exercise-03-tests}

```fsharp
match BookingRequest.create null 2 with
| Error(InvalidBookingId MissingBookingId) -> ()
| other -> failwithf "Unexpected nullable result: %A" other

match BookingRequest.create "REQ-16" 2 with
| Ok _ -> ()
| other -> failwithf "Unexpected valid result: %A" other
```

Without `(rawId: string | null)`, inference makes the wrapper accept non-null `string`, even though the called function accepts a wider input. A test that passes `null` then conflicts with the wrapper's inferred contract. Annotating the wrapper records what its callers can actually provide.

`string | null` models a CLR reference boundary that can contain null. It should be checked and normalized at that boundary. `option<string>` is an explicit F# domain value with `Some` and `None`, pattern matching, and composition functions. One does not silently substitute for the other; convert deliberately when crossing the boundary.

## What to notice {#what-to-notice}

- Write source files in provider-before-consumer order.
- Treat an `FS0039` caused by forward reference as dependency evidence, not a warning-policy problem.
- A namespace gives the path; a module owns values and functions.
- `open` changes how later references are spelled, not what code exists or is accessible.
- A wrapper's parameter type is its own public contract, even when it immediately delegates.
- Nullable reference annotations belong at real nullable boundaries; validated domain values remain non-null.
