---
title: "Chapter 27: Designing F# APIs for C#"
description: "Derive a stable .NET public surface from C# call sites while keeping unions, options, pure functions, and domain invariants inside F#."
translationKey: part-05/ch-27-fsharp-api-for-csharp
---

# Chapter 27: Designing F# APIs for C# {#overview}

F# and C# share the CLR, assemblies, and most foundational types. Yet idiomatic F# types look different when exposed through an assembly API:

| F# source form | What C# sees |
|---|---|
| `Result<_,_>` | `FSharpResult` |
| discriminated union | union-case types and helper members |
| `option` | `FSharpOption` |
| curried function | `FSharpFunc` |
| `Async<_>` | `FSharpAsync` |

These are valid CLR representations. Once published, however, callers depend on them and they become part of version compatibility.

A stable library therefore maintains two clear vocabularies: expressive F# types inside the domain and a conventional .NET API for external callers. The adapter between them stays small and testable.

## Design from the call site {#consumer-first}

Write a minimal C# client first. It reveals whether the API requires F# knowledge and lets the compiler check named arguments, nullability, construction, and result types:

```csharp:line-numbers [Program.cs]
var accepted = BookingApi.Evaluate(
    capacity: 5,
    request: new BookingRequest(requestId: "REQ-27", attendee: "Lin", seats: 2));

Require(accepted.Outcome == BookingOutcome.Accepted, "accepted outcome");
Require(default(BookingOutcome) == BookingOutcome.None, "valid enum zero value");
Require(accepted.IsAccepted, "accepted flag");
Require(accepted.ConfirmationCode == "CONF-REQ-27", "confirmation code");
Require(accepted.RemainingSeats == 3, "remaining seats");
Require(accepted.ErrorMessage is null, "accepted error must be null");
Require(accepted.SuggestedSeats is null, "accepted suggestion must be null");

Console.WriteLine(
    $"Accepted: outcome={accepted.Outcome} code={accepted.ConfirmationCode} remaining={accepted.RemainingSeats}");
```
This call uses only a namespace, enum, sealed classes, constructor, static method, properties, `string?`, and `int?`. The C# caller need not know that a union and `option` exist internally. Named arguments also show that names such as `capacity`, `request`, and `requestId` can become source dependencies, not mere implementation details.

“C# can call it” is only the minimum. Also ask: Does IntelliSense feel natural? Is nullable analysis accurate? Can callers branch on expected errors? Will an old binary still run after an upgrade?

## One meaning, three deliberately designed representations {#three-surfaces}

The same booking request can use three representations, but the business rules should exist only once:

| Layer | Optimized for | Suitable representation | Must not define |
|---|---|---|---|
| F# domain core | Domain reasoning and exhaustive matching | Private unions, records, `option`, `Result`, pure functions | C# convenience, serializer construction rules |
| .NET public API | C#, VB, and reflection tools | Namespaces, classes, enums, members, nullable annotations, tasks, delegates | JSON field names, ORM layout |
| Wire/storage DTO | JSON, messages, or database adapters | Explicit fields, versions, serialization attributes | The only implementation of domain invariants |

Only the domain core decides business rules. Public APIs and DTOs decode input, call the core, and translate the result. They may use different representations and version schedules because assembly signatures, JSON schemas, and database schemas have different compatibility rules.

### Keep the union in the core {#internal-union}

The sample uses a closed union for two domain outcomes; suggested seats exist only for a rejection:

```fsharp:line-numbers [Library.fs]
type internal Decision =
    | Accepted of confirmationCode: string * remainingSeats: int
    | Rejected of message: string * suggestedSeats: int option

module internal Decision =
    let evaluate capacity (request: BookingRequest) =
        if String.IsNullOrWhiteSpace request.RequestId then
            Rejected("request id must not be blank", None)
        elif String.IsNullOrWhiteSpace request.Attendee then
            Rejected("attendee must not be blank", None)
        elif request.Seats <= 0 then
            Rejected("seat count must be positive", None)
        elif request.Seats > capacity then
            let suggestion = if capacity > 0 then Some capacity else None

            Rejected($"requested {request.Seats} exceeds available {capacity}", suggestion)
        else
            let normalizedRequestId = request.RequestId.Trim().ToUpperInvariant()
            Accepted($"CONF-{normalizedRequestId}", capacity - request.Seats)
```
Pattern matching remains exhaustive and invalid combinations do not enter the core. `internal` prevents C# or another assembly from depending on the compiled case representation, while leaving the library free to add an internal case or change its payload.

### Translate once in the public API {#boundary-projection}

These are common cross-language mappings, not mechanical one-for-one replacements:

| Internal F# representation | Common .NET public representation | Decision criterion |
|---|---|---|
| Private DU / `Result<'T,'E>` | Closed response class plus status enum, or expected value plus exception | How callers branch and whether failure is expected |
| `'T option` return | Nullable reference, `Nullable<T>`, or `TryX(..., out T)` | Meaning of absence and value/reference category |
| `'T option` parameter | Clear overloads, occasionally a nullable parameter with explicit null semantics | Avoid requiring C# to construct `FSharpOption<T>` |
| `'T -> 'U` | `Func<T,U>`, `Action<T>`, or a named delegate | C# lambdas and tooling |
| `Async<'T>` | `Task<T>`, usually accepting `CancellationToken` | .NET async conventions |
| F# `list`/`Map`/`Set` | A .NET collection interface matching the behavior | Enumeration, indexing, lookup, and mutation semantics |
| Tuple with domain meaning | Named result type | Stable member meaning and an evolution point |

The public request uses an ordinary constructor and read-only properties:

```fsharp:line-numbers [Library.fs]
/// <summary>Identifies whether a booking request was accepted or rejected.</summary>
type BookingOutcome =
    /// <summary>No booking outcome has been assigned.</summary>
    | None = 0
    /// <summary>The booking was accepted and has a confirmation code.</summary>
    | Accepted = 1
    /// <summary>The booking was rejected and has an error message.</summary>
    | Rejected = 2

/// <summary>Input supplied by a .NET caller when evaluating a booking.</summary>
/// <param name="requestId">A non-null request identifier. Blank identifiers are rejected by <c>Evaluate</c>.</param>
/// <param name="attendee">A non-null attendee name. Blank names are rejected by <c>Evaluate</c>.</param>
/// <param name="seats">The number of seats requested.</param>
/// <exception cref="System.ArgumentNullException"><paramref name="requestId"/> or <paramref name="attendee"/> is <see langword="null"/>.</exception>
[<Sealed>]
type BookingRequest(requestId: string, attendee: string, seats: int) =
    do
        ArgumentNullException.ThrowIfNull(requestId, nameof requestId)
        ArgumentNullException.ThrowIfNull(attendee, nameof attendee)

    /// <summary>Gets the request identifier exactly as supplied.</summary>
    member _.RequestId = requestId

    /// <summary>Gets the attendee name exactly as supplied.</summary>
    member _.Attendee = attendee

    /// <summary>Gets the requested seat count.</summary>
    member _.Seats = seats
```
The public response maps an absent reference to nullable `string` and an absent value to `Nullable<int>`. Its constructor is assembly-internal, so callers cannot construct an “accepted but missing confirmation code” response:

```fsharp:line-numbers [Library.fs]
/// <summary>A C#-friendly projection of the internal F# booking decision.</summary>
/// <remarks>
/// Accepted responses have a confirmation code and remaining-seat count.
/// Rejected responses have an error message and may have a suggested seat count.
/// </remarks>
[<Sealed>]
type BookingResponse
    internal
    (
        outcome: BookingOutcome,
        confirmationCode: string | null,
        remainingSeats: Nullable<int>,
        errorMessage: string | null,
        suggestedSeats: Nullable<int>
    ) =
    /// <summary>Gets the accepted or rejected outcome.</summary>
    member _.Outcome = outcome

    /// <summary>Gets whether this response represents an accepted booking.</summary>
    member _.IsAccepted = outcome = BookingOutcome.Accepted

    /// <summary>Gets the confirmation code, or <see langword="null"/> when rejected.</summary>
    member _.ConfirmationCode = confirmationCode

    /// <summary>Gets remaining capacity, or <see langword="null"/> when rejected.</summary>
    member _.RemainingSeats = remainingSeats

    /// <summary>Gets the rejection message, or <see langword="null"/> when accepted.</summary>
    member _.ErrorMessage = errorMessage

    /// <summary>Gets a capacity-based suggestion when available; otherwise <see langword="null"/>.</summary>
    member _.SuggestedSeats = suggestedSeats
```
The adapter is the only place that understands both representations:

```fsharp:line-numbers [Library.fs]
module internal ResponseAdapter =
    let fromDecision decision =
        match decision with
        | Accepted(confirmationCode, remainingSeats) ->
            BookingResponse(BookingOutcome.Accepted, confirmationCode, Nullable remainingSeats, null, Nullable<int>())
        | Rejected(message, suggestedSeats) ->
            let suggestion =
                match suggestedSeats with
                | Some seats -> Nullable seats
                | None -> Nullable<int>()

            BookingResponse(BookingOutcome.Rejected, null, Nullable<int>(), message, suggestion)
```
Even when public signatures contain no `Microsoft.FSharp.*`, an assembly compiled from F# normally still depends on `FSharp.Core` at runtime. The goal is to spare callers from knowing F# representations, not to hide the implementation language. Normal project or NuGet dependency resolution carries that dependency transitively.

## Present public members as a .NET API {#dotnet-shape}

An API for other .NET languages favors namespaces, types, and members; implementation functions can stay in non-public modules. The sample defines an abstract, sealed type with a private constructor to group static operations:

```fsharp:line-numbers [Library.fs]
/// <summary>Provides the stable .NET entry point for booking decisions.</summary>
[<AbstractClass; Sealed>]
type BookingApi private () =
    /// <summary>Evaluates one request against the supplied available capacity.</summary>
    /// <param name="capacity">Available seats. Negative capacity is invalid configuration.</param>
    /// <param name="request">A non-null request to evaluate.</param>
    /// <returns>A response projected into ordinary .NET enum, class, string, and nullable-value members.</returns>
    /// <exception cref="System.ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    /// <exception cref="System.ArgumentOutOfRangeException"><paramref name="capacity"/> is negative.</exception>
    static member Evaluate(capacity: int, request: BookingRequest) =
        ArgumentNullException.ThrowIfNull(request, nameof request)

        if capacity < 0 then
            raise (ArgumentOutOfRangeException(nameof capacity, capacity, "Capacity cannot be negative."))

        request |> Decision.evaluate capacity |> ResponseAdapter.fromDecision
```
This does not require turning every F# module into a class. Only the cross-language public API needs the caller's vocabulary; an F#-facing API can still expose modules, functions, and unions naturally.

### Names are part of source compatibility {#names}

Use `PascalCase` for public namespaces, types, methods, and properties, and `camelCase` for parameters; use affirmative Boolean properties such as `IsAccepted` or `CanRetry`. Avoid distinctions based only on casing, and do not export internal abbreviations to every caller.

F# members declared with parenthesized tuple-style parameters compile as normal multi-parameter CLI methods. C# therefore sees `Evaluate(int capacity, BookingRequest request)`. Choose parameter names carefully: C# named arguments embed them in source, so renaming one breaks that source even though the binary signature remains unchanged.

`[<CompiledName>]` can give a compiled value or function another name when F# and CLI callers genuinely need different idiomatic names. It is not a general repair for incoherent naming. First make the public vocabulary consistent, then inspect the resulting API with the C# compiler.

### Properties, methods, and overloads make different promises {#members-overloads}

Properties suit cheap, stable values that resemble state observations. Work that takes arguments, can be expensive, or has conspicuous failure should be a method. Do not hide network or disk I/O behind something that looks like field access.

Overloads often express optional behavior, such as `Find(requestId)` and `Find(requestId, attendee)`, both delegating to one implementation. Varying argument count is usually clearer than overloading on similar types. Do not prebuild every combination for hypothetical future options; when options grow together, move them into a named options type.

Public callbacks use `Func`, `Action`, or a domain delegate. Public asynchronous methods return `Task` or `Task<T>` and accept `CancellationToken` when callers may cancel. Internally, the adapter can immediately convert a delegate to an F# function and map task work back to domain operations.

### Collections and tuples must preserve behavior {#collections-tuples}

Do not merely replace `list<'T>` with `IEnumerable<T>` and call the design complete. `IEnumerable<T>` suits sequential enumeration, `IReadOnlyList<T>` can promise stable indexing and count, and a dictionary interface promises keyed lookup. Also document whether callers receive a live view or a snapshot; a read-only interface does not prove that the backing storage is immutable.

A pair of short-lived, unrelated results can occasionally be a tuple. If `Item1` and `Item2` make C# callers guess, or members may be added later, return a named type. A little adapter code buys a much clearer API.

## Express null, absence, and failure accurately {#absence-failure}

Public API design must separate three things: invalid caller arguments, expected business rejection, and infrastructure failure. Encoding all three as `null`, throwing all three, or putting all three in one string discards information.

### Nullable annotations do not replace runtime checks {#null-contract}

With nullable checking enabled in F# 9 and later, `string` and `string | null` have different static meanings. For non-null public inputs, the sample emits `NotNull` metadata and calls `ArgumentNullException.ThrowIfNull` at entry. C# code without nullable analysis, reflection, and other runtime callers can still pass null.

An optionally absent reference output uses `string | null`; an optionally absent value output uses `Nullable<int>`. `Nullable<T>` applies only to value types. F# constructs `Nullable<T>()` for no value; C# sees it as `T?` and `is null` is true.

The C# test client uses reflection to check these promises and ensure that public signatures expose no F#-specific types:

```csharp:line-numbers [Program.cs]
var publicTypes = typeof(BookingApi).Assembly.GetExportedTypes();

var publicTypeNames = publicTypes
    .Select(type => type.Name)
    .OrderBy(name => name, StringComparer.Ordinal)
    .ToArray();

var expectedPublicTypes = new[]
{
    nameof(BookingApi),
    nameof(BookingOutcome),
    nameof(BookingRequest),
    nameof(BookingResponse)
};

Require(publicTypeNames.SequenceEqual(expectedPublicTypes), "minimal public type surface");
Require(typeof(BookingResponse).GetConstructors().Length == 0, "response construction is controlled");
Require(
    !publicTypes.SelectMany(GetPublicSignatureTypes).Any(ContainsFSharpSpecificType),
    "no F#-specific type leaks through public signatures");
Console.WriteLine($"Public types: {string.Join(",", publicTypeNames)}");

var nullability = new NullabilityInfoContext();
var requestIdParameter = typeof(BookingRequest).GetConstructors().Single().GetParameters()[0];
var confirmationProperty = typeof(BookingResponse).GetProperty(nameof(BookingResponse.ConfirmationCode))!;
var requestIdState = nullability.Create(requestIdParameter).ReadState;
var confirmationState = nullability.Create(confirmationProperty).ReadState;

Require(requestIdState == NullabilityState.NotNull, "requestId nullable metadata");
Require(confirmationState == NullabilityState.Nullable, "confirmation nullable metadata");
Console.WriteLine(
    $"Nullability: request-id={requestIdState} confirmation={confirmationState}");

var documentationPath = Path.ChangeExtension(typeof(BookingApi).Assembly.Location, ".xml");
Require(File.Exists(documentationPath), "XML documentation sidecar");
var documentation = File.ReadAllText(documentationPath);
Require(documentation.Contains("BookingApi.Evaluate", StringComparison.Ordinal), "Evaluate XML documentation");
Console.WriteLine("XML docs: evaluate=true");
```
Reflection tests validate metadata but do not replace real calls. The sample also compiles and runs accepted, rejected, invalid-value, null-input, and range-error paths.

### Enums need a valid zero and a rule for unknown values {#enum-contract}

The default CLR enum value is zero, and any underlying integer can be cast to an enum. `BookingOutcome.None = 0` therefore gives the default a name; the library itself produces only `Accepted` or `Rejected` through controlled construction. If an enum arrives from untrusted input, still validate defined values or retain a default `switch` branch—do not confuse the type declaration with a runtime closed set.

Enums suit stable, payload-free coarse labels; they are not discriminated unions. When cases carry different data, let the enum guide interpretation of one response rather than creating several contradictory public Boolean flags.

### Expected rejection is data; invalid API use is an exception {#error-policy}

The sample returns insufficient seats and invalid business fields as `BookingResponse`, because callers are expected to display or handle them. A null request and negative capacity are invalid API or configuration inputs, so they throw `ArgumentNullException` or `ArgumentOutOfRangeException`. Unexpected I/O, cancellation, and programming failures follow the relevant .NET exception and task conventions.

The category is not universal for every application. Classify each error by what the caller can do. Let the F# core use `Result` or a union for expected branches, then map them to a clear, stable .NET outcome in the public adapter.

## XML documentation ships with the public API {#xml-documentation}

Every public type, constructor, property, and method should have concise XML documentation; argument guards should also document their exception conditions. `<summary>`, `<remarks>`, `<param>`, `<returns>`, and `<exception>` flow into IDE and documentation tooling.

The sample enables `GenerateDocumentationFile` and adds F# warning 3390 to the build to catch malformed XML and incorrect parameter names:

```xml:line-numbers [FSharpApi.fsproj]
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <AssemblyName>ThinkingInFSharp.Ch27.FSharpApi</AssemblyName>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <WarnOn>$(WarnOn);3390</WarnOn>
  </PropertyGroup>

  <ItemGroup>
    <Compile Include="Library.fs" />
  </ItemGroup>
</Project>
```
The client also asserts that the XML sidecar exists and contains `BookingApi.Evaluate`. This cannot judge prose quality, but it catches comments that never ship with the assembly. Once the API stabilizes, an `.fsi` file can collect public signatures and documentation in one reviewable list.

## Do not let JSON or a database design the domain backwards {#wire-boundary}

A serializer may prefer a public parameterless constructor, writable properties, particular field names, or attributes. `[<CLIMutable>]` gives an F# record a default constructor and property getters and setters. That is appropriate for an integration DTO that genuinely requires it, but it also permits null, zero, and partially initialized states.

Do not casually add `[<CLIMutable>]` to a domain record for one serializer. Create a dedicated DTO, treat nullable/default values as unvalidated input, then convert through a smart constructor or decoder into domain types. Centralize the reverse projection in an adapter too. A JSON field rename, compatibility version, or ORM requirement can then change without forcing domain unions and invariants to change with it.

A C# public type should not automatically become the JSON schema either. In-process callers, network consumers, and persisted data evolve on different schedules. Reuse one representation only after confirming that they truly share the same compatibility requirements.

## Compatibility means more than “it still compiles” {#compatibility}

A published public surface has at least four compatibility dimensions:

| Category | Failure time | Example |
|---|---|---|
| Source compatibility | When the caller recompiles | Renaming a parameter breaks named arguments; a new overload makes resolution ambiguous |
| Binary compatibility | When an old binary loads or calls | Adding a parameter to an existing method, removing a member, or changing a signature causes `MissingMethodException` and related failures |
| Behavioral compatibility | While the program runs | Changing rejection from a return value to an exception; changing comparison, ordering, or defaults |
| Wire-format compatibility | While reading messages or stored data | Changing a JSON field name, enum encoding, or required field |

“Additive” does not automatically mean safe. A new overload can make an existing method group or `null` call ambiguous. Adding an interface member breaks existing implementers. A new public union case makes a formerly exhaustive F# match incomplete. Changed nullable annotations can add warnings or errors when callers recompile.

Prefer adding a member or overload while keeping the old member as a forwarding bridge. During migration, `[<Obsolete("Use Evaluate(...)")>]` can name the replacement and timeline. Do not change a signature in place merely to “simplify” the API. For a major version, document behavioral and wire-format migrations instead of relying only on semantic versioning.

Put the C# test client in CI and retain a released assembly or package as an API baseline. NuGet packages can enable package validation and a baseline version; `Microsoft.DotNet.ApiCompat.Tool` can also compare assemblies. These tools detect many signature differences, but behavior and serialization compatibility still need focused tests.

## Run the shared API sample {#run-example}

Build and run the real C# caller from the repository root:

```console
dotnet build examples/chapters/ch27/CSharpClient/CSharpClient.csproj --configuration Release
dotnet run --project examples/chapters/ch27/CSharpClient/CSharpClient.csproj --configuration Release --no-build
```

The client asserts business outcomes, argument checks, four exported types, public signatures, nullable metadata, and XML documentation instead of merely printing a demonstration. After changing a public API, recompile this consumer first, then run existing-binary compatibility and behavioral tests.

## Exercises {#exercises}

### Exercise 1: hide leaked F# representation types {#exercise-01}

A library publishes `decide : int -> BookingRequest -> Result<(string * int), string * int option>`. Design public types and a method for C# while retaining that function as the internal core. Map success, rejection, and an absent suggestion, and identify which construction must be controlled.

### Exercise 2: add optional filtering without breaking callers {#exercise-02}

You have `BookingSearch.Find(string requestId)`. Add filtering by attendee without exposing `string option`. Give the F# member declarations and two C# calls; explain parameter naming, overload ambiguity, and the migration strategy if options continue to grow.

### Exercise 3: separate a JSON DTO from the domain request {#exercise-03}

Assume the serializer requires parameterless construction and writable properties. Design a DTO that can hold unvalidated input, a domain conversion returning structured errors, and the location of reverse projection. Classify the compatibility affected by renaming a JSON field and explain why DTO rules must not enter the domain type.

[Read the chapter solutions](../solutions/ch-27-fsharp-api-for-csharp).

## Sources {#sources}

- [Microsoft Learn: F# component design guidelines](https://learn.microsoft.com/en-us/dotnet/fsharp/style-guide/component-design-guidelines)
- [Microsoft Learn: XML documentation in F#](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/xml-documentation)
- [Microsoft Learn: null values and nullable checking in F#](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/values/null-values)
- [Microsoft Learn: nullable value types in F#](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/nullable-value-types)
- [Microsoft Learn: breaking changes and .NET libraries](https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/breaking-changes)
- [Microsoft Learn: NuGet package compatibility rules](https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/nuget-package-compatibility-rules)
- [Microsoft Learn: CA1008, enums should have a zero value](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1008)
- [FSharp.Core reference: `CLIMutableAttribute`](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-climutableattribute.html)
