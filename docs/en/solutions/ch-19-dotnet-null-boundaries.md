---
title: "Chapter 19 Solutions"
description: "Classify nullable boundaries, wrap a real nullable .NET return without erasing failures, and prove why an option payload can still be null."
translationKey: solutions/ch-19-dotnet-null-boundaries
---

# Chapter 19 Solutions {#overview}

Use the external producer's real representation at the adapter, then convert once into the meaning needed by the F# core. A null annotation, domain absence, invalid input, and an exceptional failure are not synonyms.

[Return to Chapter 19](../part-04/ch-19-dotnet-null-boundaries).

## Exercise 1: classify boundary representations {#exercise-01}

### Choose from both sides of the boundary {#exercise-01-classification}

| Input | Adapter representation | Core representation | Reason |
|---|---|---|---|
| C# `DateTimeOffset? LastSeen` | `Nullable<DateTimeOffset>` | `DateTimeOffset option` when domain absence is intended | `DateTimeOffset` is a value type; C# nullable value syntax compiles to `Nullable<T>` |
| `Customer? Find(string id)` with normal “not found” | `Customer | null` | `Customer option` | The API uses a nullable reference; the core wants ordinary absence |
| Optional middle name created inside F# | No foreign nullable form is needed | `MiddleName option` | Absence belongs directly to the domain model |
| Required attendee text arriving as null or blank | `string | null` | `Result<AttendeeName, AttendeeNameError>` | Missing and blank are invalid construction facts, not valid optional states |
| Null for missing type, exceptions for malformed input | `Type | null` plus the documented exception behavior | `Type option` for ordinary absence while preserving or deliberately translating other failures | Returning null and throwing communicate different outcomes |

The core type may become even stronger than the table shows. For example, `AttendeeName` can have a private representation so only validated nonblank text can construct it.

### Place each conversion once {#exercise-01-flow}

```text
C# DateTimeOffset? ── Option.ofNullable ──▶ DateTimeOffset option
Customer? return ─── Option.ofObj ────────▶ Customer option
raw required text ── Null/NonNull + checks ▶ Result<AttendeeName, Error>
Type.GetType return ─ Option.ofObj ────────▶ Type option
```

If an outbound .NET call later requires `DateTimeOffset?` or a nullable `Customer`, convert back with `Option.toNullable` or `Option.toObj` immediately before that call. Do not make every intermediate function bilingual in both representations.

The malformed-type-name exception must not become “not found.” Either let it propagate to an exception boundary or translate only the documented exception cases into a distinct error union. The caller can then tell absence from invalid input or infrastructure failure.

## Exercise 2: wrap one real nullable API {#exercise-02}

### Preserve ordinary absence as option {#exercise-02-option}

The smallest wrapper is the one used by the chapter project:

```fsharp
open System

let tryResolveType (typeName: string) : Type option =
    Type.GetType(typeName, throwOnError = false)
    |> Option.ofObj

assert (tryResolveType "System.String" = Some typeof<string>)
assert (tryResolveType "Example.TypeThatDoesNotExist" = None)
```

`Option.ofObj` expresses only the null/non-null return split. It does not catch exceptions. That is an advantage: an unexpected loader or resolver failure is not mislabeled as ordinary absence.

### Preserve the requested name in an error {#exercise-02-result}

When callers need an explanation for a missing type, change the domain contract explicitly:

```fsharp
open System

type ResolveTypeError =
    | TypeNotFound of requestedName: string

let resolveType (typeName: string) : Result<Type, ResolveTypeError> =
    match Type.GetType(typeName, throwOnError = false) with
    | Null -> Error(TypeNotFound typeName)
    | NonNull resolved -> Ok resolved

assert (resolveType "System.String" = Ok typeof<string>)

assert (
    resolveType "Example.TypeThatDoesNotExist" =
        Error(TypeNotFound "Example.TypeThatDoesNotExist")
)
```

This still does not catch every exception. If one application has a policy for a documented `ArgumentException` or loader failure, add a specific error case and catch only that condition at the adapter. A blanket `with _ -> TypeNotFound typeName` would destroy stack, exception kind, and operational cause while making the returned error factually false.

## Exercise 3: audit an option invariant {#exercise-03}

### Prove the counterexample {#exercise-03-counterexample}

```fsharp
let suspicious : (string | null) option = Some null

let isSome, payloadIsNull =
    match suspicious with
    | None -> false, false
    | Some payload ->
        match payload with
        | Null -> true, true
        | NonNull _ -> true, false

assert isSome
assert payloadIsNull
```

The outer union case records presence as `Some`; the payload type independently admits null. Checking only `Option.isSome` therefore cannot establish a non-null payload for this type.

### Give ordinary absence and invalid input different APIs {#exercise-03-boundaries}

```fsharp
open System

type RequiredTextError =
    | MissingText
    | BlankText

let optionalText (raw: string | null) : string option =
    Option.ofObj raw

let requiredText (raw: string | null) : Result<string, RequiredTextError> =
    match raw with
    | Null -> Error MissingText
    | NonNull value when String.IsNullOrWhiteSpace value -> Error BlankText
    | NonNull value -> Ok(value.Trim())

assert (optionalText null = None)
assert (optionalText "" = Some "")
assert (requiredText null = Error MissingText)
assert (requiredText "" = Error BlankText)
assert (requiredText " F# " = Ok "F#")
```

`optionalText` says null is ordinary absence; it deliberately preserves an empty string as a present payload. `requiredText` says a usable value is required and distinguishes two failure causes. Neither API uses an unchecked conversion or catches unrelated exceptions.

At a larger domain boundary, return a protected `RequiredText` type rather than a plain string. The same conversion policy remains: normalize foreign null first, then construct the domain value only after validation.

## What to notice {#what-to-notice}

- A nullable reference and a nullable value require different FSharp.Core conversion pairs.
- Convert according to the producer representation before choosing the core's domain representation.
- An option-returning wrapper should not erase exceptions that mean something other than absence.
- `Some` describes the option case, not an invariant independently enforced on every payload.
- A small boundary function can keep null out of the core without inventing a large adapter framework.
