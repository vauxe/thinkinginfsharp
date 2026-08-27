---
title: "Appendix D: C# to F# Migration and Interop"
description: "Move C# systems toward F# by redesigning values, domain models, failures, asynchronous work, collections, and public boundaries—not by transliterating syntax."
translationKey: appendices/d-csharp-migration
---

# Appendix D: C# to F# Migration and Interop {#overview}

A successful migration preserves behavior while improving the model. It does not replace every C# token with an F# token. Both languages target .NET, so existing assemblies, tests, protocols, and deployment can remain useful while you migrate one seam at a time.

Use this appendix as a decision guide. First identify what callers depend on and who controls the state or resources; then choose an F# representation. Keep an adapter when callers need a different API shape. If a direct translation hides invalid states, effects, or evaluation timing, stop and redesign that small interface.

## The non-goal: line-by-line translation {#non-goal}

These pairs resemble one another, but none is a universal replacement:

| Familiar C# form | Possible F# form | Question that decides |
|---|---|---|
| local variable | `let` binding | Is rebinding, mutation, or a new derived value intended? |
| `void` method | function returning `unit` | Is the operation an effect, or should it return useful data? |
| class or C# record | F# record, union, class, or interface | Is the type a product, a choice, mutable identity, or behavior contract? |
| nullable reference | non-null value, `'T option`, or `'T | null` | Is absence part of the domain or only a boundary condition? |
| exception | exception, `Result`, or domain union | Can the caller recover as an expected branch? |
| `IEnumerable<T>` | `seq<'T>` | Is deferred, repeatable enumeration really the contract? |
| `Task<T>` | `Task<'T>` or `Async<'T>` | Must work integrate with .NET tasks, and when should it start? |
| LINQ chain | collection module pipeline | Which collection, evaluation, ordering, and allocation are required? |

Migration is complete when the resulting model tells the truth, not when the new file has the same number of lines.

## Values and expressions {#values-expressions}

C# code often communicates through statements that update locals. F# code usually communicates by naming intermediate values and returning the final expression:

```fsharp
let subtotal = lines |> List.sumBy (fun line -> line.Price * decimal line.Quantity)
let discount = if subtotal >= 100M then subtotal * 0.10M else 0M
subtotal - discount
```

`let` introduces an immutable binding by default. A later `let subtotal = ...` in a nested scope is shadowing: it creates another binding; it does not update the earlier value. Use `let mutable` and `<-` only when local mutation makes an algorithm or interop boundary clearer.

Translate intent, not control-flow shape:

| C# intent | F# starting point | Do not assume |
|---|---|---|
| derive one value | `let`, `if`, `match`, pipeline | every intermediate needs mutation |
| transform all elements | `List.map`, `Array.map`, `Seq.map` | the three collection modules have identical evaluation |
| aggregate | `fold`, `sumBy`, a small loop | recursion is automatically clearer or faster |
| early validation | guard at boundary, then return/match | every guard should throw |
| stateful hot loop | scoped mutation or a mutable .NET collection | functional style forbids controlled mutation |
| compose behavior | functions and partial application | every method must become a curried public function |

F# `if`, `match`, `try`, loops, and computation expressions all produce values. Branches therefore need compatible result types. `unit` is a real type with one value, `()`, not the absence of a return type. A function ending in an effect can return `unit`; a calculation should normally return its result.

Type inference removes repetition, not the type system. Put annotations at public boundaries, recursive definitions, overloaded .NET calls, and places where a domain type makes intent easier to review. Read function signatures from the outside inward before changing implementation.

## Model products, alternatives, and identity explicitly {#data-modeling}

Start from the valid states rather than the old declaration keyword:

| Domain structure | F# default candidate | Keep a class/interface when… |
|---|---|---|
| all named fields exist together | record | identity, inheritance, encapsulated mutation, or framework construction dominates |
| exactly one of several cases | discriminated union | a broad .NET public consumer cannot reasonably consume the compiled union representation |
| one primitive with an invariant | private single-case union plus smart constructor | the wrapper adds no invariant or semantic distinction |
| optional domain value | `'T option` | the boundary contract uses CLR null or `Nullable<T>` |
| replaceable capability | function or small interface | lifetime, multiple members, mocking tools, or DI registration favor an object contract |
| mutable entity with lifecycle | class or private mutable state behind functions | value semantics describe it more accurately |

A C# model with booleans such as `IsAccepted`, `IsRejected`, and nullable payload fields may admit contradictory states. An internal union can make each outcome carry exactly its valid data. Do not add an `Unknown` case merely to imitate a default enum unless unknown is a real domain state.

Use `option` for modeled absence inside F#. At a public .NET boundary, reference absence may be `string | null` and value absence may be `Nullable<int>`. Nullable annotations help callers' static analysis; runtime argument guards are still required because reflection, older assemblies, and null-forgiving operators can bypass analysis.

`[<CLIMutable>]` creates a default constructor and setters for an F# record. Use it deliberately for a serializer or framework DTO that requires that construction pattern, then validate and convert to a domain type. It is not a shortcut for making every record C#-friendly.

## Give failures a caller action {#failure-modeling}

Classify failure by what the caller can do:

| Failure | Typical representation | Caller action |
|---|---|---|
| expected business alternative | `Result` or a domain union internally; explicit response at a broad .NET boundary | branch, display, retry with changed input |
| programmer violated an API contract | `ArgumentNullException`, `ArgumentException`, or `ArgumentOutOfRangeException` | fix the call |
| unavailable external resource or corrupted state | exception, possibly mapped at an application boundary | retry, degrade, log, or abort by policy |
| cancellation | preserve cancellation token and cancellation exception/task state | stop work without reporting an ordinary failure |

Choose the representation by how callers can recover. Allocation failure and a broken invariant normally propagate as exceptions; an expected rejected booking belongs in `Result` or a domain union. When callers routinely branch on an outcome, make that branch visible in the return model.

When adapting a C# API that already uses exceptions, first characterize which exception types are contractual. Catch only exceptions you can interpret, retain the original cause where relevant, and never use a blanket catch to convert cancellation into a generic error.

## Choose asynchronous semantics before syntax {#asynchrony}

`Async<'T>` and `Task<'T>` are both useful, but they do not mean the same thing. An F# async workflow is cold until explicitly started; a task expression produces a task and begins executing immediately through its synchronous prefix. Existing .NET APIs, C# callers, ASP.NET Core, and most framework extension points naturally use `Task`.

| Situation | Prefer initially | Reason |
|---|---|---|
| public API for general .NET callers | `Task` / `Task<'T>` | native C# `await` and ordinary .NET conventions |
| composing task-based .NET APIs | `task { ... }` | avoids unnecessary representation conversion |
| internal F# workflow needing cold/composable start | `async { ... }` | start and parallelism remain explicit |
| synchronous CPU work | synchronous function, then measure | wrapping work in a task does not make it non-blocking |
| high-frequency result where `ValueTask` may help | measure first | reuse and consumption rules increase API complexity |

Accept a `CancellationToken` where callers need cancellation and pass it to operations that support it. Do not block with `.Result`, `.Wait()`, or `Async.RunSynchronously` inside an asynchronous request path. Preserve exception and cancellation behavior when bridging `Async` and `Task`, and test whether work begins at construction or await/start time.

## Collections are behavioral contracts {#collections}

`seq<'T>` is the F# abbreviation for `IEnumerable<T>`, but that fact alone does not make it the right public type. Specify whether callers receive a snapshot or live view, whether enumeration is deferred or repeatable, whether order is stable, and who may mutate storage.

| Requirement | Internal F# candidate | Cross-language boundary candidate |
|---|---|---|
| immutable head-oriented processing | list | project to an agreed read-only view or array; do not leak `FSharpList<T>` accidentally |
| fixed indexed snapshot | array | array only if mutation/ownership is clear; otherwise a read-only abstraction or dedicated result |
| forward-only/deferred enumeration | `seq<'T>` | `IEnumerable<T>` with lifetime and repeatability documented |
| growable owned buffer | `ResizeArray<'T>` | keep private; expose only required collection operations or a snapshot |
| immutable sorted lookup | `Map` / `Set` | project when callers should not inherit F# comparison types and compiled representations |
| mutable hash lookup | `Dictionary` / `HashSet` | an interface or domain collection when mutation and future implementation must remain controlled |

Do not replace every LINQ query with `Seq`: list and array functions are eager, many sequence functions are deferred, and repeated enumeration can repeat I/O or computation. See [Appendix C](./c-collections) for complexity, ordering, and key contracts.

For a public API, choose the least specialized input that still expresses the operation, but do not erase requirements. If callers need indexing, a stable count, or a source that may be traversed only once, `IEnumerable<T>` is too weak. Never return `null` for “no elements”; return an empty collection of the documented type.

## Give each side an idiomatic API {#api-boundary}

An F#-only API can expose records, unions, options, curried functions, and `Async`. A library intended for C#, VB, reflection-heavy frameworks, or mixed teams should normally expose familiar CLR types while its internals remain idiomatic F#.

| Internal/F#-facing form | Broad .NET-facing option | Decision note |
|---|---|---|
| curried function | class/module method with tupled arguments | parameter names become part of named-argument source compatibility |
| F# function value | `Func<...>` / `Action<...>` or an interface | choose a delegate for one operation, interface for a richer lifecycle |
| `'T option` | nullable reference, `Nullable<T>`, `Try...`, overload, or response object | distinguish absent, invalid, and failed |
| `Result<'T,'Error>` or union | response class/enum, exception, or documented hierarchy | preserve every meaningful case without requiring F# helpers |
| tuple | named record/class/struct | names survive tooling and future review better than `Item1` |
| `Async<'T>` | `Task<T>` | use the consumer's asynchronous convention |
| F# list/map/set | ordinary .NET abstraction, array, or dedicated collection | preserve order, ownership, equality, and update semantics |
| module function | PascalCase static-style member or ordinary type member | inspect the emitted C# call site, not just F# source |

Avoid leaking `Microsoft.FSharp.*` types unless consumers explicitly opt into an F#-specific API. Put public types in a namespace, follow .NET naming, document public members with XML comments, annotate nullability accurately, and validate untrusted public inputs at runtime. Once stable, an `.fsi` signature file can make the exported F# surface deliberate.

Binary, source, behavioral, and wire compatibility are separate. Renaming a parameter can break C# named arguments; adding an overload can make old source ambiguous; replacing a returned rejection with an exception changes behavior; changing a DTO field changes persisted or network data. Prefer an additive bridge and an obsolete migration path over silently changing a published member.

## Read the executable interop pair {#executable-pair}

Chapter 27 keeps the domain choice inside F#:

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
One adapter converts that closed union and its `option` payload into four ordinary CLR public types:

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
The C# consumer sees a normal static call, enum, properties, nullable reference, and nullable value:

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
The same client uses reflection to assert that exactly `BookingApi`, `BookingOutcome`, `BookingRequest`, and `BookingResponse` are exported; no public signature contains `Microsoft.FSharp.*`; nullability metadata is correct; and XML documentation ships beside the assembly. These assertions test the compiled API rather than assuming how F# source will appear to C#.

Run the pair from the directory containing the example:

```console
dotnet build CSharpClient.csproj --configuration Release --no-restore
dotnet run --project CSharpClient.csproj --configuration Release --no-build
```

## Migrate by seams, not by folders {#migration-workflow}

1. **Inventory contracts.** Record public signatures, serialization, database formats, exceptions, timing, ordering, null behavior, and deployment constraints.
2. **Freeze representative behavior.** Add consumer-level tests around valuable paths and known edge cases before changing language.
3. **Choose one seam.** A pure rule, parser, calculation, or adapter with narrow dependencies is a better first slice than an entire architectural layer.
4. **Model the core in F#.** Make invalid states harder to represent; isolate time, I/O, randomness, and mutation behind explicit inputs or capabilities.
5. **Preserve the old call contract.** Keep a thin adapter so existing C# code can compile and behave as before while the core changes behind it.
6. **Compile real consumers.** Test from C# and inspect metadata, nullability, documentation, exceptions, and collection behavior—not only F# unit tests.
7. **Expand only after verification.** Move the next seam when the interface is simpler and tests remain stable; stop if translation merely moves complexity.
8. **Retire bridges deliberately.** Deprecate, version, and document migration before removing a public compatibility layer.

A mixed F#/C# solution is a valid destination, not an unfinished migration. Keep each language where its model and ecosystem fit, and keep the shared API clear.

## Review checklist {#review-checklist}

- Can each important state be constructed, and can any contradictory state still be constructed?
- Are absence, rejection, invalid input, infrastructure failure, and cancellation distinct?
- Are evaluation time, repeatability, ordering, ownership, and mutation of collections explicit?
- Does asynchronous work start and cancel when the caller expects?
- Does a C# call site read naturally without knowledge of FSharp.Core representation types?
- Are nullability annotations backed by runtime guards at public entry points?
- Are parameter names, exceptions, XML documentation, and emitted public types tested as contracts?
- Is compatibility evaluated at source, binary, behavior, and wire levels?
- Is the adapter small enough that domain rules exist in only one place?
- Did the migration improve the model, or only change its syntax?

## Sources {#sources}

- [Microsoft Learn: F# component design guidelines](https://learn.microsoft.com/en-us/dotnet/fsharp/style-guide/component-design-guidelines)
- [Microsoft Learn: null values and nullable checking in F#](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/values/null-values)
- [Microsoft Learn: nullable value types in F#](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/nullable-value-types)
- [Microsoft Learn: async programming in F#](https://learn.microsoft.com/en-us/dotnet/fsharp/tutorials/async)
- [Microsoft Learn: F# task expressions](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/task-expressions)
- [Microsoft Learn: guidelines for collections](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/guidelines-for-collections)
- [Microsoft Learn: breaking changes and .NET libraries](https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/breaking-changes)
