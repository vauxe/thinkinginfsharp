---
title: "Chapter 25: Defining Objects in F#"
description: "Choose functions, records, unions, classes, interfaces, object expressions, extensions, and structs from semantics rather than ceremony."
translationKey: part-05/ch-25-objects-interfaces
---

# Chapter 25: Defining Objects in F# {#overview}

F# is a .NET language, so classes, members, interfaces, inheritance, and value types are native tools. Functions express behavior, records name data, and discriminated unions close a set of states. Classes become useful when the model needs their specific runtime semantics.

Start with the meaning a type must preserve. Use an object when reference identity, hidden state, constructor logic, resource lifetime, runtime dispatch, or a .NET member API makes it useful. Choose the simplest accurate representation, whether functional or object-oriented.

## Choose a representation before choosing syntax {#representation-first}

Ask what callers must know and what the runtime must preserve:

| Need | Usually start with | Why |
|---|---|---|
| One transformation or policy | Function | Input-output behavior already defines the abstraction |
| Immutable data with named fields | Record | Fields and structural operations describe the value directly |
| One of a closed set of alternatives or states | Discriminated union | Cases and exhaustive matching expose every alternative |
| Related dependencies carried together | Record of functions | Callers can assemble a small, clear set of operations |
| Identity, hidden evolving state, or a managed lifetime | Class | One reference can encapsulate state and resource handling |
| Several implementations behind a .NET member API | Interface | Runtime dispatch and .NET consumption are intentional |
| One local implementation of an existing interface | Object expression | No reusable named implementation is needed |
| Small measured value with copy semantics or required interop layout | Struct | Value-type representation is part of the requirement |

Treat these as starting points. Records and unions can have members and implement interfaces, while classes can be immutable. Choose a class when its identity, state, lifetime, or dispatch behavior improves the model; having a member is not by itself a reason.

Two quick tests expose ceremonial wrappers:

1. If replacing `service.Execute x` with `execute x` loses no identity, lifetime, dispatch, or API behavior, a function may be clearer.
2. If a class only stores public constructor arguments and exposes them unchanged, an immutable record may state the same model with less machinery.

## A class is a .NET reference type {#classes}

The example models quote calculation. `Quote` remains a private record, errors remain a discriminated union, and only the calculator behavior uses a class. The following is the complete type definition from `examples/chapters/ch25/Types.fs`; later fragments use these names:

```fsharp:line-numbers [Types.fs]
namespace ThinkingInFSharp.Ch25

type QuoteRequest = { Seats: int; UnitPrice: decimal }

type QuoteError =
    | NonPositiveSeats of actual: int
    | NegativeUnitPrice of actual: decimal
    | InvalidDiscountRate of actual: decimal

type Quote =
    private
        { Seats: int
          Subtotal: decimal
          Discount: decimal
          Tax: decimal
          Total: decimal }

module Quote =
    let seats quote = quote.Seats
    let subtotal quote = quote.Subtotal
    let discount quote = quote.Discount
    let tax quote = quote.Tax
    let total quote = quote.Total

type IDiscountPolicy =
    abstract Rate: QuoteRequest -> decimal

type IQuoteService =
    abstract Quote: QuoteRequest -> Result<Quote, QuoteError>

type PriceCalculator(taxRate: decimal, discountPolicy: IDiscountPolicy) =
    do
        if taxRate < 0M then
            invalidArg (nameof taxRate) "Tax rate cannot be negative."

    new(discountPolicy: IDiscountPolicy) = PriceCalculator(0M, discountPolicy)

    member _.TaxRate = taxRate

    member _.Calculate(request: QuoteRequest) =
        if request.Seats <= 0 then
            Error(NonPositiveSeats request.Seats)
        elif request.UnitPrice < 0M then
            Error(NegativeUnitPrice request.UnitPrice)
        else
            let discountRate = discountPolicy.Rate request

            if discountRate < 0M || discountRate > 1M then
                Error(InvalidDiscountRate discountRate)
            else
                let subtotal = decimal request.Seats * request.UnitPrice
                let discount = subtotal * discountRate
                let taxable = subtotal - discount
                let tax = taxable * taxRate

                Ok
                    { Seats = request.Seats
                      Subtotal = subtotal
                      Discount = discount
                      Tax = tax
                      Total = taxable + tax }

    interface IQuoteService with
        member this.Quote request = this.Calculate request

[<AutoOpen>]
module QuoteExtensions =
    type Quote with
        member this.IsDiscounted = Quote.discount this > 0M
        member this.TotalAmount = Quote.total this

[<Struct>]
type QuoteRevision = private QuoteRevision of int

module QuoteRevision =
    let create raw =
        if raw > 0 then Ok(QuoteRevision raw) else Error raw

    let value (QuoteRevision revision) = revision
```
In `PriceCalculator(taxRate, discountPolicy)`, the parameter list declares the primary constructor. The leading `do` binding is part of that constructor and runs for every instance. The `new(discountPolicy)` member is an additional constructor; it must delegate to the primary constructor, here supplying a zero tax rate.

Constructor parameters are in scope throughout the class. A leading `let` can keep a field or helper private, while `member` exposes a method or property in .NET metadata. Use `_` when the member does not need its current instance; give the self identifier a name only when it does.

### Put each failure in the right channel {#constructor-invariants}

A negative `taxRate` means the calculator was configured incorrectly, so construction throws `ArgumentException` through `invalidArg`. A non-positive seat request is an expected input outcome, so `Calculate` returns `Error (NonPositiveSeats actual)`. Syntax does not dictate this split; responsibility and recoverability do.

Avoid constructors that perform remote I/O, start background work with no responsible owner, or publish `this` before initialization completes. Such work makes creation hard to cancel, retry, test, and dispose. When acquisition is asynchronous or fallible, prefer a small validated constructor plus a factory or start method.

### Identity is not domain equality {#class-identity}

A class instance is a reference. Two separately constructed calculators may hold equivalent configuration yet be different references. Unless a class deliberately supplies equality semantics, do not assume it behaves like an F# record's generated structural equality.

Use `obj.ReferenceEquals` only when identity itself matters. For domain equality, compare an explicit stable identifier or model the value as a record/union. Overriding `Equals` also requires a consistent hash code and a clear policy for mutation and inheritance; it should not be added merely to make a test convenient.

## Interfaces define member-based APIs {#interfaces}

An interface declares related abstract members and stores no data. `IQuoteService` is useful when consumers need a member-based .NET API or runtime dispatch must select among implementations. `IDiscountPolicy` is deliberately narrow, but an F#-only caller could use a `QuoteRequest -> decimal` function instead.

| Consideration | Prefer a function or function record when | Prefer an interface when |
|---|---|---|
| Operations | One operation or a small bundle is enough | Related named members form one stable object API |
| Consumption | Callers are primarily F# and composition is lexical | Frameworks or other .NET languages expect members/runtime dispatch |
| State/lifetime | Dependencies are plain values | Implementations manage identity, state, or disposal |
| Evolution | The operation set is local and easy to replace together | The public member API has a deliberate compatibility policy |

F# interface implementations are normally explicit. `PriceCalculator.Calculate` is a class member, but `IQuoteService.Quote` is callable through an interface view. The following creates a no-discount policy and a complete request first, so the block can run directly after `Types.fs`:

```fsharp
let noDiscount =
    { new IDiscountPolicy with
        member _.Rate _ = 0M }

let request = { Seats = 2; UnitPrice = 10M }
let calculator = PriceCalculator(noDiscount)
let service = calculator :> IQuoteService
let result = service.Quote request
```

The upcast makes the distinction visible: callers using the concrete class see its full API, while callers using the interface see only its members. Keep interfaces cohesive and small. Splitting every function into an `IThing`/`Thing` pair adds names and indirection without necessarily creating a useful abstraction.

## Object expressions implement a small interface locally {#object-expressions}

An object expression creates an instance of a compiler-generated anonymous object type based on an interface or base class. The example supplies a group-discount policy where the application's dependencies are assembled:

```fsharp
let groupDiscount =
    { new IDiscountPolicy with
        member _.Rate request =
            if request.Seats >= 5 then 0.10M else 0M }
```

This is excellent for a one-off adapter, a tiny test substitute, or a policy assembled from local values. It still creates an object and can capture mutable state; it is not automatically pure or thread-safe.

Give the implementation a name when it has independent invariants, several collaborators, substantial behavior, reuse, lifecycle, or diagnostics. A hundred-line object expression merely hides a type name while keeping all the complexity.

## Type extensions add a view, not representation {#type-extensions}

The example adds `Quote.IsDiscounted` and `Quote.TotalAmount` as derived members inside the auto-open `QuoteExtensions` module. These are optional F# extensions: no field is added to existing values, their runtime representation does not change, and reflection does not report them as properties on `Quote`.

An intrinsic extension is declared in the same file and namespace or module as the type. It is compiled as part of that type and appears through reflection. An optional extension must live in a module that callers bring into scope; `[<AutoOpen>]` does that automatically here. C# and Visual Basic callers cannot use optional F# extensions. Extensions cannot add virtual or abstract members or overrides, and a real member wins an ambiguous call.

Use an extension when member-call syntax makes a stable derived operation easier to find, especially for a type you cannot edit. Prefer a module function when dependency visibility, pipeline order, cross-language access, or independence from module scope matters more.

## Structs change semantics, not just allocation {#structs}

A struct is a .NET value type. Assignment, argument passing, and return normally copy its value; boxing creates an object containing a copy. Storage depends on context—locals are not guaranteed simply to “live on the stack,” and a boxed struct lives on the managed heap—so do not choose one from a stack-versus-heap slogan.

The example's `[<Struct>]` single-case union `QuoteRevision` has a private case and a smart constructor that accepts only positive integers. Ordinary callers cannot construct `QuoteRevision 0`, yet `Unchecked.defaultof<QuoteRevision>` still produces its zero-initialized representation. Every struct has a default value; constructor privacy cannot remove it.

That fact creates design obligations:

- make the all-zero representation valid, or reject it at every boundary that can produce defaults;
- be cautious with arrays, serializers, interop, and generic APIs that may zero-initialize values;
- avoid mutable structs unless copy behavior is explicitly required and understood;
- consider boxing and interface-call costs as well as allocation savings;
- use measurement and interop requirements, not aesthetics, to justify a struct.

Small immutable struct records or unions can be valuable in a proven hot path. A reference record is the safer default for ordinary domain data because its value semantics are clearer and it does not silently acquire a zero representation.

## Inheritance, composition, and lifetime {#inheritance-lifetime}

F# classes can inherit one direct base class and implement multiple interfaces. Use inheritance when the framework contract is genuinely a base class—for example, overriding a UI or hosting type—not merely to share a helper. Modules, function composition, records of capabilities, and contained objects usually make dependencies more visible and avoid fragile base-class state.

If an object manages an `IDisposable` or `IAsyncDisposable` resource, its API must say who disposes it and when its methods stop being valid. The `use` and `use!` rules from Chapters 21 and 23 still apply. Hiding a handle inside a class does not remove its lifetime; it transfers responsibility to that class and its callers.

Classes with mutable state also need a concurrency policy. “Private” prevents direct field access, not simultaneous member calls. Publish immutable snapshots, serialize access, or synchronize the whole invariant using Chapter 24's rules.

## Review an object-based public API {#api-review}

Before publishing an object-based API, ask:

- Would a function, record, or union state the same semantics more directly?
- Does reference identity matter, and is equality behavior explicit?
- Which constructor arguments are configuration errors, and which method inputs produce typed domain errors?
- Does every interface member belong to one cohesive API rather than speculative future extensibility?
- Does an object expression remain local and small?
- Is a type extension discoverable in the scopes and languages that need it?
- Is a struct's zero value valid, and has copying/boxing been measured?
- Who handles disposal, mutation, cancellation, and thread safety?
- Does the public representation suit both current callers and future compatibility constraints?

Chapter 27 will revisit the last question from a C# consumer's view. Chapter 31 will supply measurement before representation-level optimization.

The complete consumer is `examples/chapters/ch25/Program.fs`. From the repository root, run:

```console
dotnet run --project examples/chapters/ch25/Ch25.fsproj --configuration Release
```

The program prints four deterministic lines covering classes, interfaces, extensions, and structs:

```text
Class: tax-rate=0.20 total=54.00
Interface: total=12.00
Extension: discounted=true
Struct: value=2 copy=2 default=0
```

The final line also exposes struct copying and zero initialization directly. The repository check compares all four results exactly.

## Exercises {#exercises}

### Exercise 1: remove a ceremonial class {#exercise-01}

A `SeatRequest` class only stores an identifier and seat count through read-only properties. Replace it with an immutable record. Put expected validation in a module function returning `Result`, and explain what would have justified keeping a class.


::: details Answer

#### Let data and validation say exactly what they are {#exercise-01-record}

```fsharp
open System

type SeatRequest =
    { RequestId: string
      Seats: int }

type ValidationError =
    | EmptyRequestId
    | NonPositiveSeats of actual: int

module SeatRequest =
    let create requestId seats =
        if String.IsNullOrWhiteSpace requestId then
            Error EmptyRequestId
        elif seats <= 0 then
            Error(NonPositiveSeats seats)
        else
            Ok
                { RequestId = requestId.Trim()
                  Seats = seats }

let accepted = SeatRequest.create "  REQ-25  " 2
let rejected = SeatRequest.create "REQ-25" 0

assert (accepted = Ok { RequestId = "REQ-25"; Seats = 2 })
assert (rejected = Error(NonPositiveSeats 0))
```

The record exposes immutable product data and receives generated structural equality, which is exactly what this request needs. The module handles normalization and expected validation without turning construction into exception control flow.

A class would become justified if each request had reference identity, protected evolving state, an owned disposable resource, required virtual/interface dispatch, or a framework base-class contract. Merely preferring property-call syntax would not be enough.

:::

### Exercise 2: choose a policy representation {#exercise-02}

Implement the same discount rule once as a function and once as an `IDiscountPolicy` object expression. Use both in a calculation, then state which public API you would keep for an F#-only library and what requirement could justify the interface.


::: details Answer

#### Compare the same rule without changing its meaning {#exercise-02-policies}

```fsharp
type QuoteRequest =
    { Seats: int
      UnitPrice: decimal }

type DiscountPolicy = QuoteRequest -> decimal

type IDiscountPolicy =
    abstract Rate: QuoteRequest -> decimal

let groupRate request =
    if request.Seats >= 5 then 0.10M else 0M

let totalWith (rate: DiscountPolicy) request =
    let subtotal = decimal request.Seats * request.UnitPrice
    subtotal * (1M - rate request)

let objectPolicy =
    { new IDiscountPolicy with
        member _.Rate request = groupRate request }

let request = { Seats = 5; UnitPrice = 10M }
let functionTotal = totalWith groupRate request
let interfaceTotal = totalWith objectPolicy.Rate request

assert (functionTotal = 45M)
assert (interfaceTotal = functionTotal)
```

For an F#-only library with one stateless operation, `DiscountPolicy` is the smaller public boundary and composes directly. Use an interface when a .NET framework expects one or other languages need a member-based contract. It also fits related operations or stateful implementations selected through runtime dispatch.

The object expression is local and contains one forwarding member. If the policy acquired collaborators, caches, disposal, or substantial rules, a named implementation would make those responsibilities visible.

:::

### Exercise 3: audit a struct invariant {#exercise-03}

Create a positive revision struct through a smart constructor, copy it, box both copies, and observe its default value. Then redesign the type so zero initialization represents a valid named state, or document and test rejection at every source of default values.


::: details Answer

#### Prove the unsafe default, then make default a modeled state {#exercise-03-default}

```fsharp
[<Struct>]
type PositiveRevision = private | PositiveRevision of int

module PositiveRevision =
    let create raw =
        if raw > 0 then Ok(PositiveRevision raw) else Error raw

    let value (PositiveRevision raw) = raw

let positive =
    PositiveRevision.create 3
    |> Result.defaultWith (fun error -> failwithf "unexpected: %d" error)
let copied = positive
let invalidDefault = Unchecked.defaultof<PositiveRevision>

assert (PositiveRevision.value copied = 3)
assert (not (obj.ReferenceEquals(box positive, box copied)))
assert (PositiveRevision.value invalidDefault = 0)

[<Struct>]
type Revision =
    private
    | Unassigned
    | Assigned of value: int

module Revision =
    let assign raw =
        if raw > 0 then Ok(Assigned raw) else Error raw

    let describe revision =
        match revision with
        | Unassigned -> "unassigned"
        | Assigned value -> $"assigned:{value}"

let initial = Unchecked.defaultof<Revision>
let assigned =
    Revision.assign 3
    |> Result.defaultWith (fun error -> failwithf "unexpected: %d" error)

assert (Revision.describe initial = "unassigned")
assert (Revision.describe assigned = "assigned:3")
```

The private case protects ordinary construction but cannot prevent the CLR's zero initialization. The redesign makes tag zero, the first case, mean `Unassigned`; every representable default is now a defined domain state, while `Assigned` still goes through validation for ordinary callers.

Do this only if a struct is independently justified. If “unassigned” is not meaningful, prefer a non-struct domain model and keep default-producing interop outside it, or reject zero immediately at every such boundary. `Unchecked.defaultof` can manufacture problematic values for reference representations too; it is an unsafe escape hatch, not normal construction.

:::


## Sources {#sources}

- [Microsoft Learn: classes in F#](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/classes)
- [Microsoft Learn: constructors in F#](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/members/constructors)
- [Microsoft Learn: interfaces in F#](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/interfaces)
- [Microsoft Learn: object expressions in F#](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/object-expressions)
- [Microsoft Learn: type extensions in F#](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/type-extensions)
- [Microsoft Learn: structs in F#](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/structs)
