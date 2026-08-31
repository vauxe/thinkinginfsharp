---
title: "Chapter 19: .NET APIs and Null Boundaries"
description: "Call ordinary .NET constructors, members, overloads, and interfaces from F#, then convert nullable references and values into honest domain types at one boundary."
translationKey: part-04/ch-19-dotnet-null-boundaries
---

# Chapter 19: .NET APIs and Null Boundaries {#overview}

F# does not sit beside .NET; it is a .NET language. A `Uri`, `String.Join`, or `IReadOnlyCollection<'T>` call is ordinary typed F# code. The important question is not how to escape from functional programming, but where a foreign API's construction rules, overloads, exceptions, and absence conventions should stop influencing the rest of the program.

We establish that conversion point before performing substantial I/O. First we call constructors, members, overloaded methods, and interfaces. Then we distinguish three meanings often collapsed into “nullable”: a nullable reference `T | null`, a nullable value `Nullable<T>`, and a domain choice `T option`.

## Read a .NET call as a typed expression {#dotnet-calls}

The example begins without I/O:

```fsharp:line-numbers [NullBoundaries.fs]
let createAbsoluteUri (raw: string) : Uri = Uri(raw, UriKind.Absolute)

let uriHost (uri: Uri) : string = uri.Host

let joinLabels (labels: string array) : string = String.Join(" / ", labels)

let countItems (items: IReadOnlyCollection<'T>) : int = items.Count
```
Each definition is an ordinary function whose result is a value. The capitalized type and member names follow .NET conventions; pipelines, local bindings, pattern matching, and domain types remain available around them.

### Construction and member access {#construction-members}

`Uri(raw, UriKind.Absolute)` invokes a constructor. The keyword `new` is optional for class construction, so `new Uri(...)` means the same thing here. Constructor arguments appear in parentheses and are comma-separated, matching .NET method-call syntax.

`uri.Host` reads an instance property. A parameterless .NET method uses parentheses, as in `uri.ToString()`. Properties and methods can both perform work; syntax alone does not promise purity or low cost. Read the API documentation.

The `Uri` constructor can reject malformed input by throwing. This small wrapper intentionally preserves that behavior. If malformed URI text is an expected domain outcome, validate with `Uri.TryCreate` or translate the exception in a dedicated adapter. Chapter 21 covers exception policy; we do not silently turn every exception into `None`.

### Overload selection follows available types {#overloads}

`String.Join` has multiple overloads. In `joinLabels`, the annotation `labels: string array` and the string separator select the overload accepting a string separator and string array. The compiler does not choose an overload from a return type you hope to receive; it uses the statically available argument types and context.

When overload selection is unclear, add the smallest accurate annotation at the call site:

```fsharp
let joinLabels (labels: string array) : string =
    String.Join(" / ", labels)
```

Do not add arbitrary casts until something compiles. A cast can select a different API and hide a modeling error. First inspect the overload signatures, then state the type of the argument you actually have.

### Program to the required interface {#interfaces}

`countItems` needs only `IReadOnlyCollection<'T>.Count`, so its parameter names that interface rather than an array or concrete list:

```fsharp
let countItems (items: IReadOnlyCollection<'T>) : int =
    items.Count
```

An array can be supplied as that interface. When the context does not perform the upcast automatically, write it explicitly:

```fsharp
let items = [| 1; 2; 3 |] :> IReadOnlyCollection<int>
countItems items
```

The static upcast operator `:>` is checked by the compiler and is safe at runtime. The downcast operator `:?>` serves a different purpose and performs a runtime check. Accepting an interface narrows the required capabilities; purity, immutability, and thread safety remain separate properties.

## “Missing” has three distinct representations {#three-representations}

The following types answer different questions:

| Representation | What it represents | Runtime form | Typical use |
|---|---|---|---|
| `T | null` | A reference may be the null reference | The reference itself may be null | Nullable .NET annotations and interop |
| `Nullable<T>` | A value type may have no value | `System.Nullable<T>` with `HasValue` and `Value` | .NET APIs using nullable structs |
| `T option` | The program models absence as `None` or presence as `Some value` | An F# discriminated union | F# domain and workflow APIs |

None is a universal replacement for the others. `T | null` applies to reference types under null checking. `Nullable<T>` requires a value type. `option` works across value and reference payloads and makes callers pattern-match on domain absence, but its payload type still matters.

## Nullable references are compile-time contracts {#nullable-references}

### Opt in and annotate the actual input {#nullable-opt-in}

F# nullable-reference checking is opt-in. The chapter project states:

```xml
<Nullable>enable</Nullable>
```

With checking enabled, `string` means the compiler expects a non-null string, while `string | null` explicitly admits null. The annotation does not wrap the value at runtime. It also cannot prove that reflection, older metadata, unchecked code, deserialization, or another language will always obey its annotation.

Use the narrowest accurate type. Mark an input `T | null` only when its producer can supply null; do not make every internal reference nullable “just in case.” After conversion, keep the core non-null by construction.

### Narrow once with `Null` and `NonNull` {#null-narrowing}

The adapter and its error type are executable shared code:

```fsharp:line-numbers [NullBoundaries.fs]
type BoundaryTextError =
    | MissingText
    | BlankText
```
```fsharp:line-numbers [NullBoundaries.fs]
let requireText (raw: string | null) : Result<string, BoundaryTextError> =
    match raw with
    | Null -> Error MissingText
    | NonNull value when String.IsNullOrWhiteSpace value -> Error BlankText
    | NonNull value -> Ok(value.Trim())
```
`Null` handles the null reference. In each `NonNull value` branch, analysis narrows `value` to non-null `string`, so the declared type permits `Trim()`. Whitespace is a different invalid condition and receives a different error.

The literal `null` pattern also works. `Null`/`NonNull` is useful when the narrowed non-null value should be named. `NonNullQuick` instead throws `NullReferenceException` on null; use it only when throwing is intended, not to avoid handling null deliberately.

### Convert nullable returns immediately when the domain wants option {#nullable-return}

`Type.GetType(name, throwOnError = false)` is a real .NET API whose return may be null when the type is not found. The adapter converts that convention exactly once:

```fsharp:line-numbers [NullBoundaries.fs]
let tryResolveType (typeName: string) : Type option =
    Type.GetType(typeName, throwOnError = false) |> Option.ofObj
```
`Option.ofObj` maps null to `None` and a non-null reference to `Some value`. Downstream F# code now sees `Type option`, not a nullable reference that must be rechecked everywhere.

The argument `throwOnError = false` means “return null when lookup does not find the type”; the .NET documentation lists other conditions that may still throw. Use `option` for ordinary absence and preserve exceptions that carry other failure causes.

## `Nullable<T>` is a nullable value type {#nullable-values}

### Inspect presence before reading `Value` {#nullable-inspection}

`Nullable<int>` is `System.Nullable<int>`, a struct that can represent an absent or present integer. It is not written `int | null`, because the latter syntax concerns nullable references. Its fundamental members are:

```fsharp
let absent = Nullable<int>()
let present = Nullable 4

absent.HasValue   // false
present.HasValue  // true
present.Value     // 4
```

Reading `Value` while `HasValue` is false throws `InvalidOperationException`. Check first, use `GetValueOrDefault` only when that default truly means what the caller intends, or convert to an F# representation.

### Convert at the .NET interface, not throughout the core {#nullable-value-conversion}

FSharp.Core supplies named conversions:

```fsharp:line-numbers [NullBoundaries.fs]
let nullableIntToOption (value: Nullable<int>) : int option = Option.ofNullable value

let optionToNullableInt (value: int option) : Nullable<int> = Option.toNullable value
```
`Option.ofNullable` maps an absent nullable value to `None`; `Option.toNullable` maps `None` back to an empty `Nullable<T>`. For a present value, both preserve the payload. These functions require an appropriate value type.

Keep `Nullable<T>` where an external member requires or returns it. Prefer `option` once absence enters the F# model. Repeated conversion inside the core signals that interoperation concerns have leaked too far inward.

## `option` is a domain choice, not a null-proof wrapper {#option-boundary}

### Reference conversions have different functions {#reference-conversion}

Nullable references use `Option.ofObj` and `Option.toObj`, not the `Nullable<T>` conversions:

```fsharp:line-numbers [NullBoundaries.fs]
let nullableTextToOption (value: string | null) : string option = Option.ofObj value

let optionToNullableText (value: string option) : string | null = Option.toObj value
```
The direction should be visible in adapter code:

- inbound nullable reference: `T | null -> T option` with `Option.ofObj`;
- outbound optional domain value: `T option -> T | null` with `Option.toObj` when the .NET API requires null;
- nullable value type: `Nullable<T> <-> T option` with `ofNullable` and `toNullable`.

Do not use `defaultArg optionValue null` as a vague substitute. Under null checking it often weakens the type, and its intent is less precise than the named interoperability conversion.

### `Some null` is a real counterexample {#some-null}

An option says whether its case is `None` or `Some`; it does not independently validate the payload. With a payload type that explicitly admits null, this is legal:

```fsharp:line-numbers [NullBoundaries.fs]
let someNullText: (string | null) option = Some null
```
The value is `Some`, and its payload is null. Older or unchecked .NET code can also violate assumptions. Therefore the accurate rule is:

> Use `None` for domain absence, keep ordinary option payload types non-null, and normalize foreign null in the adapter.

Do not claim that the runtime representation of `option` makes null impossible. Under null checking, `string option` requires a non-null payload; `(string | null) option` deliberately permits the counterexample. Types expose the distinction, while adapter tests check foreign inputs.

## Keep conversion outside the core {#boundary-placement}

A practical flow is:

```text
.NET constructor/member/overload/interface
                    ↓ inspect its declared contract
       T | null or Nullable<T> at the adapter
                    ↓ convert and validate once
       option / Result / protected domain type
                    ↓
             pure F# core and workflow
```

This is not a demand for a large abstraction layer. A two-line function such as `tryResolveType` can be the whole adapter. The purpose is to prevent foreign absence rules from leaking through every function signature.

### A compact conversion decision table {#decision-table}

| Situation | Keep or convert to | Reason |
|---|---|---|
| .NET reference parameter genuinely accepts null | `T | null` at that call boundary | Matches the external contract |
| .NET reference return may be null and absence is normal | Convert to `T option` | Makes downstream absence explicit |
| .NET value member uses `Nullable<T>` | Keep there; convert with `Option.ofNullable` when entering the core | Preserves the actual runtime representation at interop |
| F# domain field may be absent | `T option` | Names absence in the model |
| Missing input is itself a validation failure | `Result<T, Error>` | Preserves why construction failed |
| API uses null to signal failure but can also throw | `option` for absence; preserve/translate documented exceptions separately | Absence and failure are different facts |
| API requires null on output | Convert with `Option.toObj` at the final call | Keeps null out of intermediate domain code |

Start with the producer's documented behavior, then consider the consumer's domain meaning. Habit is not a type-design rule.

### Preserve causes instead of flattening them {#failure-causes}

Nullable checking catches some accidental dereferences at compile time. Whitespace validation, text parsing, URI acceptance, and service availability are separate concerns with their own checks and representations.

Use `option` when ordinary absence needs no explanation. Use `Result` when callers need a reason. Let unexpected exceptions retain diagnostics until a layer has enough context to translate them. Chapters 20 and 21 add side effects and exception/resource policy without changing this null model.

## Build the checked example {#run-tests}

From the repository root:

```console
dotnet build examples/chapters/ch19/Ch19.fsproj --configuration Release
```

The project compiles with null checking enabled. Its source covers ordinary constructor, member, overload, and interface calls; null-input narrowing; the nullable return of `Type.GetType`; both conversion directions for `Nullable<int>` and nullable references; and the `Some null` counterexample.

This fixture covers the APIs shown here, not every .NET library. Always inspect the current target-framework annotations and documentation for the API you call.

## Exercises {#exercises}

### Exercise 1: classify absence representations {#exercise-01}

For each value below, choose `T | null`, `Nullable<T>`, `T option`, or `Result<T, Error>` at the point where your F# core consumes it. Explain both the producer contract and the domain meaning:

1. a C# property `DateTimeOffset? LastSeen`;
2. a nullable reference return `Customer? Find(string id)` where “not found” is normal;
3. an optional middle name stored in a new F# domain record;
4. required attendee text that may arrive as null or whitespace;
5. an API that returns null for “not found” but throws for malformed type names.

State where each conversion occurs and which failures must remain distinct.


::: details Answer

#### Choose from both sides of the boundary {#exercise-01-classification}

| Input | Adapter representation | Core representation | Reason |
|---|---|---|---|
| C# `DateTimeOffset? LastSeen` | `Nullable<DateTimeOffset>` | `DateTimeOffset option` when domain absence is intended | `DateTimeOffset` is a value type; C# nullable value syntax compiles to `Nullable<T>` |
| `Customer? Find(string id)` with normal “not found” | `Customer | null` | `Customer option` | The API uses a nullable reference; the core wants ordinary absence |
| Optional middle name created inside F# | No foreign nullable form is needed | `MiddleName option` | Absence belongs directly to the domain model |
| Required attendee text arriving as null or blank | `string | null` | `Result<AttendeeName, AttendeeNameError>` | Missing and blank are invalid construction facts, not valid optional states |
| Null for missing type, exceptions for malformed input | `Type | null` plus the documented exception behavior | `Type option` for ordinary absence while preserving or deliberately translating other failures | Returning null and throwing communicate different outcomes |

The core type may become even stronger than the table shows. For example, `AttendeeName` can have a private representation so only validated nonblank text can construct it.

#### Place each conversion once {#exercise-01-flow}

```text
C# DateTimeOffset? ── Option.ofNullable ──▶ DateTimeOffset option
Customer? return ─── Option.ofObj ────────▶ Customer option
raw required text ── Null/NonNull + checks ▶ Result<AttendeeName, Error>
Type.GetType return ─ Option.ofObj ────────▶ Type option
```

If an outbound .NET call later requires `DateTimeOffset?` or a nullable `Customer`, convert back with `Option.toNullable` or `Option.toObj` immediately before that call. Do not make every intermediate function bilingual in both representations.

The malformed-type-name exception must not become “not found.” Either let it propagate to an exception boundary or translate only the documented exception cases into a distinct error union. The caller can then tell absence from invalid input or infrastructure failure.

:::

### Exercise 2: wrap one real nullable API {#exercise-02}

Write `tryResolveType` around `Type.GetType(typeName, throwOnError = false)`. Its public return type must be `Type option`. Test one known core type and one missing type.

Then write the deliberately different `resolveType` returning `Result<Type, ResolveTypeError>`, where a missing type carries its requested name. Explain why catching every possible exception and returning the same error would lose information.


::: details Answer

#### Preserve ordinary absence as option {#exercise-02-option}

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

#### Preserve the requested name in an error {#exercise-02-result}

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

:::

### Exercise 3: check an option invariant {#exercise-03}

Given this value:

```fsharp
let suspicious : (string | null) option = Some null
```

Show that `Option.isSome suspicious` is true while the payload is null. Write an adapter that converts `string | null` into `string option`, then another function that rejects null and blank text as distinct `Result` errors.

Explain which function is appropriate for ordinary absence and which is appropriate for required validated input. Do not use `Unchecked` or an exception-catching blanket.


::: details Answer

#### Prove the counterexample {#exercise-03-counterexample}

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

#### Give ordinary absence and invalid input different APIs {#exercise-03-boundaries}

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

:::


The next chapter keeps conversion outside the core and makes time, randomness, and environment access visible dependencies instead of hidden inputs.

## Sources {#sources}

- [Microsoft Learn: F# constructors](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/members/constructors)
- [Microsoft Learn: F# methods and overloads](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/members/methods)
- [Microsoft Learn: F# interfaces](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/interfaces)
- [Microsoft Learn: F# null values and null checking](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/values/null-values)
- [Microsoft Learn: F# nullable value types](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/nullable-value-types)
- [FSharp.Core: `Option` module](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-optionmodule.html)
- [Microsoft Learn: `Type.GetType`](https://learn.microsoft.com/en-us/dotnet/api/system.type.gettype?view=net-10.0)
