---
title: "Chapter 17 Solutions"
description: "Specify an abstract email type, narrow an inconsistent allocation surface, and align function arity and helper accessibility across a signature pair."
translationKey: solutions/ch-17-signatures-encapsulation
---

# Chapter 17 Solutions {#overview}

Begin with what a caller must construct, observe, and decide. The signature should expose that complete vocabulary and nothing that merely makes the current implementation convenient.

[Return to Chapter 17](../part-03/ch-17-signatures-encapsulation).

## Exercise 1: design an email-address pair {#exercise-01}

### Public signature {#exercise-01-signature}

`EmailAddress.fsi` can publish actionable errors and an abstract successful value:

```fsharp
namespace Contacts

type EmailAddressError =
    | Blank
    | MissingAtSign of normalized: string

type EmailAddress

module EmailAddress =
    val create: raw: string -> Result<EmailAddress, EmailAddressError>
    val value: address: EmailAddress -> string
```

Consumers can match `Blank` and `MissingAtSign`, but there is no public union case for `EmailAddress`. They can obtain that type only through published functions such as `create`.

### Matching implementation {#exercise-01-implementation}

`EmailAddress.fs` supplies the representation and keeps its normalization helper out of the signature:

```fsharp
namespace Contacts

open System

type EmailAddressError =
    | Blank
    | MissingAtSign of normalized: string

type EmailAddress = EmailAddress of string

module NormalizedText =
    let create (raw: string) = raw.Trim()

module EmailAddress =
    let create raw =
        if String.IsNullOrWhiteSpace raw then
            Error Blank
        else
            let normalized = NormalizedText.create raw

            if normalized.Contains('@') then
                Ok(EmailAddress normalized)
            else
                Error(MissingAtSign normalized)

    let value (EmailAddress address) = address
```

`NormalizedText` is an ordinary implementation declaration, but omission from the matching signature makes it unavailable outside this implementation file. It could additionally be declared `private`; the signature omission is already sufficient for later consumers.

The project order is `EmailAddress.fsi`, `EmailAddress.fs`, then any consuming file. Later files see the error cases, abstract type, `create`, and `value`. They do not see `NormalizedText` or the `EmailAddress` union case.

This example only checks blank text and the presence of `@`; it is not a claim to implement the full email-address syntax. The public error names state the intentionally small policy.

## Exercise 2: narrow an overexposed allocation API {#exercise-02}

### Replace construction with a workflow {#exercise-02-redesign}

Assume `Capacity` and `SeatCount` are already protected types. The allocation surface can be:

```fsharp
type AllocationError =
    | InsufficientCapacity of requested: int * available: int

type Allocation

module Allocation =
    val capacity: allocation: Allocation -> Capacity
    val requested: allocation: Allocation -> SeatCount
    val remaining: allocation: Allocation -> int

val allocate:
    capacity: Capacity ->
    requested: SeatCount ->
    Result<Allocation, AllocationError>
```

There is no `unsafeCreate`. `allocate` is the only published producer, so the implementation can establish `remaining = capacity - requested` and refuse requests above capacity. Returning the protected component types from two accessors retains their existing proofs; returning an `int` for remaining seats is honest because zero is allowed.

### Keep the useful error transparent {#exercise-02-error}

`AllocationError` should remain transparent because callers need to distinguish insufficient capacity and can use both numbers in a UI or API response. Hiding the error representation would require replacement predicates or formatting functions and would make normal control flow less direct.

A transparent `Allocation` record is appropriate if it is intentionally a data-transfer or reporting snapshot, every combination permitted by its field types is legal, and direct construction/copy-and-update is part of the consumer contract. It is not appropriate while the three integers claim a relationship that callers can violate.

Opacity should preserve a real rule, not merely prevent convenient record syntax. The published observations must still let callers perform every supported task.

## Exercise 3: repair arity and choose a helper boundary {#exercise-03}

### Match the curried signature {#exercise-03-arity}

The signature describes two applications:

```fsharp
apply policy request
```

The tupled implementation accepts one pair, so its arity differs. Repair it by removing the tuple pattern:

```fsharp
let apply policy request =
    // compute Result<Decision, DecisionError>
    // ...
```

Changing the signature to `val apply: policy: Policy * request: Request -> ...` would also make the pair consistent, but it would publish a different calling convention. Keep the curried form when partial application with one policy is a representative use.

### Choose the smallest helper scope {#exercise-03-helper}

If tracing is used only in the implementation file, omit it from the signature and make the local intent explicit:

```fsharp
let private traceDecision decision =
    // ...
```

If a later file in the same assembly genuinely needs it, the signature must expose an assembly-only value:

```fsharp
val internal traceDecision: decision: Decision -> string
```

The implementation must match:

```fsharp
let internal traceDecision decision =
    // ...
```

Now later files in the assembly can call it, while external assemblies cannot. Merely writing `internal` in `Library.fs` but omitting the value from `Library.fsi` leaves it hidden outside the implementation file, because the signature is the visible inventory.

Do not broaden the helper just to make a white-box test easy. Prefer testing `apply` through its published decisions; widen visibility only when another real implementation consumer owns that dependency.

## What to notice {#what-to-notice}

- Transparent error cases and abstract success types can coexist in one deliberate API.
- Omission from a signature hides a helper even if the implementation would otherwise infer it as public.
- A hidden record needs sufficient observation functions, not an unsafe escape hatch.
- Curried and tupled functions differ in public arity even when both mention the same input types.
- An `internal` value needed across files must appear with matching accessibility in both files.
- Signature design starts from supported consumer work, not from every name currently present in the implementation.
