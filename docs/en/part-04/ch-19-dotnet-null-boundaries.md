---
title: "Chapter 19: .NET APIs and Null Boundaries"
description: "Call ordinary .NET constructors, members, overloads, and interfaces from F#, then convert nullable references and values into honest domain types at one boundary."
translationKey: part-04/ch-19-dotnet-null-boundaries
---

# Chapter 19: .NET APIs and Null Boundaries {#overview}

F# does not sit beside .NET; it is a .NET language. A `Uri`, `String.Join`, or `IReadOnlyCollection<'T>` call is ordinary typed F# code. The important question is not how to escape from functional programming, but where a foreign API's construction rules, overloads, exceptions, and absence conventions should stop influencing the rest of the program.

This chapter builds that boundary before performing substantial I/O. We first call constructors, members, overloaded methods, and interfaces. We then distinguish three representations that are often collapsed into the word “nullable”: a nullable reference `T | null`, a nullable value `Nullable<T>`, and an F# domain choice `T option`.

## What you will be able to do {#outcomes}

By the end of this chapter, you should be able to:

- construct .NET objects and call instance or static members with ordinary F# expressions;
- use argument types to make overload selection clear;
- accept an interface when the operation needs only that interface's contract;
- enable and read F# nullable-reference analysis;
- narrow `T | null` with `Null` and `NonNull` patterns;
- convert a nullable .NET return into `option` at the adapter boundary;
- distinguish `Nullable<T>` from a nullable reference;
- convert `Nullable<T>` and nullable references to and from `option` deliberately;
- explain why `option` does not make every possible payload non-null;
- choose a representation from the producer's contract and the domain meaning of absence.

## Read a .NET call as a typed expression {#dotnet-calls}

The shared example begins without I/O:

```fsharp:line-numbers [NullBoundaries.fs]
let createAbsoluteUri (raw: string) : Uri = Uri(raw, UriKind.Absolute)

let uriHost (uri: Uri) : string = uri.Host

let joinLabels (labels: string array) : string = String.Join(" / ", labels)

let countItems (items: IReadOnlyCollection<'T>) : int = items.Count
```
Each definition is an ordinary function whose result is a value. The capitalized type and member names follow .NET conventions; pipelines, local bindings, pattern matching, and domain types remain available around them.

### Construction and member access {#construction-members}

`Uri(raw, UriKind.Absolute)` invokes a constructor. The keyword `new` is optional for class construction, so `new Uri(...)` means the same thing here. Constructor arguments appear in parentheses and are comma-separated, matching .NET method-call shape.

`uri.Host` reads an instance property. A parameterless .NET method would use parentheses, as in `uri.ToString()`. A property and a method can both compute work; syntax alone does not promise purity or low cost. Read the API contract.

The `Uri` constructor can reject malformed input by throwing. The small wrapper intentionally preserves that contract. If malformed URI text is an expected domain outcome, validate with `Uri.TryCreate` or translate the exception at a deliberate boundary. Chapter 21 treats exception policy; this chapter does not silently turn every exception into `None`.

### Overload selection follows available types {#overloads}

`String.Join` has multiple overloads. In `joinLabels`, the annotation `labels: string array` and the string separator select the overload accepting a string separator and string array. The compiler does not choose an overload from a return type you hope to receive; it uses the statically available argument types and context.

When selection is unclear, add the smallest truthful annotation at the boundary:

```fsharp
let joinLabels (labels: string array) : string =
    String.Join(" / ", labels)
```

Do not add arbitrary casts until something compiles. A cast can change the chosen API and hide a modeling error. First inspect the overload signatures, then state which argument representation the caller actually owns.

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

The static upcast operator `:>` is checked by the compiler and cannot fail at runtime. The downcast operator `:?>` is a different, runtime-checked operation and does not belong here. Accepting an interface reduces assumptions, but it does not by itself make an operation pure, immutable, or thread-safe.

## “Missing” has three distinct representations {#three-representations}

The following types answer different questions:

| Representation | What it represents | Runtime shape | Typical boundary |
|---|---|---|---|
| `T | null` | A reference may be the null reference | The reference itself may be null | Nullable .NET annotations and interop |
| `Nullable<T>` | A value type may have no value | `System.Nullable<T>` with `HasValue` and `Value` | .NET APIs using nullable structs |
| `T option` | The program models absence as `None` or presence as `Some value` | An F# discriminated union | F# domain and workflow APIs |

None is a universal replacement for the others. `T | null` applies to reference types under null checking. `Nullable<T>` requires a value type. `option` works across value and reference payloads and makes callers pattern-match on domain absence, but its payload type still matters.

## Nullable references are compile-time contracts {#nullable-references}

### Opt in and annotate the real boundary {#nullable-opt-in}

F# nullable-reference checking is opt-in. The chapter project states:

```xml
<Nullable>enable</Nullable>
```

With checking enabled, `string` means the compiler expects a non-null string, while `string | null` explicitly admits null. The annotation does not wrap the value at runtime. It also cannot prove that reflection, older metadata, unchecked code, deserialization, or another language will always obey its annotation.

Use the narrowest honest contract. Mark an input `T | null` when its producer can actually supply null; do not make every internal reference nullable “just in case.” After conversion, keep the core non-null by construction.

### Narrow once with `Null` and `NonNull` {#null-narrowing}

The boundary error and conversion are executable shared code:

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
`Null` handles the null reference. In each `NonNull value` branch, analysis narrows `value` to non-null `string`, so `Trim()` is safe under the stated contract. Whitespace is a different invalid fact and receives a different error.

The literal `null` pattern also works. `Null`/`NonNull` is useful when the narrowed non-null value should be named. `NonNullQuick` instead throws `NullReferenceException` on null; use that only when throwing is the intended contract, not as a shortcut around boundary design.

### Convert nullable returns immediately when the domain wants option {#nullable-return}

`Type.GetType(name, throwOnError = false)` is a real .NET API whose return may be null when the type is not found. The adapter converts that convention exactly once:

```fsharp:line-numbers [NullBoundaries.fs]
let tryResolveType (typeName: string) : Type option =
    Type.GetType(typeName, throwOnError = false) |> Option.ofObj
```
`Option.ofObj` maps null to `None` and a non-null reference to `Some value`. Downstream F# code now sees `Type option`, not a nullable reference that must be rechecked everywhere.

The argument `throwOnError = false` means “return null when lookup does not find the type”; the .NET contract documents other conditions that may still throw. `option` describes absence, not arbitrary failure. Do not catch every exception and erase its cause merely to obtain a tidy type.

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

### Convert at the edge, not throughout the core {#nullable-value-conversion}

FSharp.Core supplies named conversions:

```fsharp:line-numbers [NullBoundaries.fs]
let nullableIntToOption (value: Nullable<int>) : int option = Option.ofNullable value

let optionToNullableInt (value: int option) : Nullable<int> = Option.toNullable value
```
`Option.ofNullable` maps an absent nullable value to `None`; `Option.toNullable` maps `None` back to an empty `Nullable<T>`. For a present value, both preserve the payload. These functions require an appropriate value type.

Keep `Nullable<T>` when an external member explicitly requires or returns it. Prefer `option` after the boundary when absence is part of the F# model. Converting repeatedly inside the core is a sign that the boundary has not been placed clearly.

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

Do not use `defaultArg optionValue null` as a vague substitute. Under null checking it often weakens the type, and its intent is less precise than the conversion named for this boundary.

### `Some null` is a real counterexample {#some-null}

An option says whether its case is `None` or `Some`; it does not independently validate the payload. With a payload type that explicitly admits null, this is legal:

```fsharp:line-numbers [NullBoundaries.fs]
let someNullText: (string | null) option = Some null
```
The value is `Some`, and its payload is null. Older or unchecked .NET code can also violate assumptions. Therefore the accurate rule is:

> Use `None` for domain absence, keep ordinary option payload types non-null, and normalize foreign null at the boundary.

Do not claim that the runtime representation of `option` makes null impossible. `string option` under null checking gives a useful non-null payload contract; `(string | null) option` deliberately admits the counterexample. Types make the intended distinction reviewable, while boundary tests defend it against foreign inputs.

## Put one conversion membrane around the core {#boundary-placement}

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

Choose from the producer's actual contract first, then from the consumer's domain meaning. Habit is not a type-design rule.

### Preserve causes instead of flattening them {#failure-causes}

Nullable checking prevents some accidental dereferences at compile time. It does not validate whitespace, parse text, prove a URI is acceptable, or represent an unavailable service. Those are separate contracts.

Use `option` when ordinary absence needs no explanation. Use `Result` when callers need a reason. Let unexpected exceptions retain their diagnostics until a boundary has enough context to translate them. Chapter 18 established validation semantics; Chapters 20 and 21 will add effects and exception/resource policy without changing this null model.

## Run the contract tests {#run-tests}

From the directory containing the example:

```console
dotnet test ContractTests.fsproj \
  --configuration Release \
  --filter FullyQualifiedName~Ch19NullTests
```

The contract tests compile with nullable checking and warnings as errors. They verify constructor/member/overload/interface calls, null-input narrowing, the real nullable return of `Type.GetType`, both `Nullable<int>` conversion directions, both nullable-reference conversion directions, and the `Some null` counterexample.

These are contract tests, not claims about every .NET library. Always inspect the target framework's current annotations and documented behavior for the API you actually call.

## Exercises {#exercises}

### Exercise 1: classify boundary representations {#exercise-01}

For each value below, choose `T | null`, `Nullable<T>`, `T option`, or `Result<T, Error>` at the point where your F# core consumes it. Explain both the producer contract and the domain meaning:

1. a C# property `DateTimeOffset? LastSeen`;
2. a nullable reference return `Customer? Find(string id)` where “not found” is normal;
3. an optional middle name stored in a new F# domain record;
4. required attendee text that may arrive as null or whitespace;
5. an API that returns null for “not found” but throws for malformed type names.

State where each conversion occurs and which failures must remain distinct.

### Exercise 2: wrap one real nullable API {#exercise-02}

Write `tryResolveType` around `Type.GetType(typeName, throwOnError = false)`. Its public return type must be `Type option`. Test one known core type and one missing type.

Then write the deliberately different `resolveType` returning `Result<Type, ResolveTypeError>`, where a missing type carries its requested name. Explain why catching every possible exception and returning the same error would lose information.

### Exercise 3: audit an option invariant {#exercise-03}

Given this value:

```fsharp
let suspicious : (string | null) option = Some null
```

Show that `Option.isSome suspicious` is true while the payload is null. Write a boundary function that converts `string | null` into `string option` and a second function that rejects null and blank text as distinct `Result` errors.

Explain which function is appropriate for ordinary absence and which is appropriate for required validated input. Do not use `Unchecked` or an exception-catching blanket.

[Read the chapter solutions](../solutions/ch-19-dotnet-null-boundaries).

## Model review {#model-review}

- .NET constructors, members, overloads, and interfaces are ordinary typed F# expressions.
- Argument annotations should reveal intended overload selection rather than patch over ambiguity with arbitrary casts.
- `T | null`, `Nullable<T>`, and `T option` have different syntax, runtime representation, and modeling purpose.
- Nullable-reference analysis is an opt-in compile-time contract, not runtime validation or proof about all foreign code.
- Narrow nullable references once, then keep the core non-null by construction.
- Use the conversion pair that matches the boundary: object/null, nullable value, or domain option.
- `Some null` is possible when the payload type admits null, so `option` must not be advertised as absolute null prevention.
- `option` describes ordinary absence; `Result` preserves a reason; exceptions require their own boundary policy.

The next chapter will keep this conversion membrane and make time, randomness, and environment access explicit effects rather than hidden inputs.

## Sources {#sources}

- [Microsoft Learn: F# constructors](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/members/constructors)
- [Microsoft Learn: F# methods and overloads](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/members/methods)
- [Microsoft Learn: F# interfaces](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/interfaces)
- [Microsoft Learn: F# null values and null checking](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/values/null-values)
- [Microsoft Learn: F# nullable value types](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/nullable-value-types)
- [FSharp.Core: `Option` module](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-optionmodule.html)
- [Microsoft Learn: `Type.GetType`](https://learn.microsoft.com/en-us/dotnet/api/system.type.gettype?view=net-10.0)
