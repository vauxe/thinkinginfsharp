---
title: "Chapter 25: Defining Objects in F#"
description: "Choose functions, records, unions, classes, interfaces, object expressions, extensions, and structs from semantics rather than ceremony."
translationKey: part-05/ch-25-objects-interfaces
kind: chapter
part: 5
chapter: 25
status: complete
verifiedWith:
  fsharp: "10"
  dotnetSdk: "10.0.301"
exampleIds:
  - ch25-objects-interfaces
exerciseIds:
  - ch25-exercise-01
  - ch25-exercise-02
  - ch25-exercise-03
termIds:
  - invariant
  - record
  - reference-identity
  - smart-constructor
sources:
  - id: microsoft-fsharp-classes
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/classes
    checked: "2026-08-24"
  - id: microsoft-fsharp-constructors
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/members/constructors
    checked: "2026-08-24"
  - id: microsoft-fsharp-interfaces
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/interfaces
    checked: "2026-08-24"
  - id: microsoft-fsharp-object-expressions
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/object-expressions
    checked: "2026-08-24"
  - id: microsoft-fsharp-type-extensions
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/type-extensions
    checked: "2026-08-24"
  - id: microsoft-fsharp-structs
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/structs
    checked: "2026-08-24"
---

# Chapter 25: Defining Objects in F# {#overview}

F# is a .NET language, so classes, members, interfaces, inheritance, and value types are native parts of the language—not foreign escape hatches. They are also not a maturity layer that every functional model must eventually acquire. A function can express behavior, a record can name data, and a discriminated union can close a set of states without a class wrapper.

This chapter starts from the meaning a type must preserve. It introduces an object only when reference identity, hidden state, construction work, a lifetime, subtype dispatch, or a .NET contract makes that representation useful. The goal is not “functional versus object-oriented”; it is the smallest honest boundary for the problem.

## What you will be able to do {#outcomes}

By the end of this chapter, you should be able to:

- choose among a function, record, discriminated union, class, interface, and struct;
- define a class with a primary constructor, initialization, an additional constructor, properties, and methods;
- separate invalid object configuration from expected domain rejection;
- call an explicitly implemented interface through an interface view;
- use an object expression for a small local implementation;
- add derived behavior with a type extension without pretending to add stored data;
- explain reference identity and value-copy semantics;
- identify zero-initialization as a struct invariant boundary;
- prefer composition unless an actual base-type contract requires inheritance;
- treat resource ownership and disposal as part of an object's public contract.

## Choose a representation before choosing syntax {#representation-first}

Ask what callers must know and what the runtime must preserve:

| Need | Usually start with | Why |
|---|---|---|
| One transformation or policy | Function | The input-output contract is already the abstraction |
| Named immutable product data | Record | Fields and structural operations describe the value directly |
| One of a closed set of shapes or states | Discriminated union | Cases and exhaustive matching expose the alternatives |
| Related dependencies carried together | Record of functions | Callers can assemble a small explicit capability set |
| Identity, hidden evolving state, or an owned lifetime | Class | One reference can encapsulate state and resource protocol |
| Several implementations behind a .NET member contract | Interface | Runtime dispatch and ecosystem consumption are intentional |
| One local implementation of an existing object contract | Object expression | No reusable named implementation is needed |
| Small measured value with copy semantics or required interop layout | Struct | Value-type representation is part of the requirement |

These are starting points, not syntax bans. Records and unions can have members and implement interfaces. Classes can be immutable. A member does not prove that a class is needed, and a class does not by itself provide better architecture.

Two quick tests expose ceremonial wrappers:

1. If replacing `service.Execute x` with `execute x` loses no identity, lifetime, dispatch, or contract, a function may be the clearer API.
2. If a class only stores public constructor arguments and exposes them unchanged, an immutable record may state the same model with less machinery.

## A class is a .NET reference type {#classes}

The verified chapter example models quote calculation. `Quote` remains a private record representation, errors remain a discriminated union, and only the calculator behavior is represented by a class:

<<< @/../examples/chapters/ch25/Types.fs{fsharp:line-numbers} [Types.fs]

In `PriceCalculator(taxRate, discountPolicy)`, the parameter list declares the primary constructor. The leading `do` binding is part of that constructor and runs for every instance. The `new(discountPolicy)` member is an additional constructor; it must delegate to the primary constructor, here supplying a zero tax rate.

Constructor parameters are in scope throughout the class. A leading `let` can keep a field or helper private, while `member` exposes a method or property in .NET metadata. Use `_` when the member does not need its current instance; give the self identifier a name only when it does.

### Put each failure in the right channel {#constructor-invariants}

A negative `taxRate` means the calculator itself was configured incorrectly, so construction throws `ArgumentException` through `invalidArg`. A non-positive seat request is an expected input outcome, so `Calculate` returns `Error (NonPositiveSeats actual)`. The syntax does not dictate this split; ownership and recoverability do.

Avoid constructors that perform remote I/O, start unowned background work, or publish `this` before initialization completes. Such work makes creation hard to cancel, retry, test, and dispose. Prefer a small validated constructor plus an explicit factory or start method when acquisition is asynchronous or fallible.

### Identity is not domain equality {#class-identity}

A class instance is a reference. Two separately constructed calculators may hold equivalent configuration yet be different references. Unless a class deliberately supplies equality semantics, do not assume it behaves like an F# record's generated structural equality.

Use `obj.ReferenceEquals` only when identity itself matters. For domain equality, compare an explicit stable identifier or model the value as a record/union. Overriding `Equals` also requires a consistent hash code and a clear policy for mutation and inheritance; it should not be added merely to make a test convenient.

## Interfaces are contracts, not dependency-injection decoration {#interfaces}

An interface declares related abstract members and stores no data. `IQuoteService` is useful when consumers need a .NET-shaped service contract or several implementations must be selected by runtime dispatch. `IDiscountPolicy` is deliberately narrow, but for an F#-only caller its single member could also be a function of type `QuoteRequest -> decimal`.

| Boundary | Prefer a function or function record when | Prefer an interface when |
|---|---|---|
| Shape | One operation or a small capability bundle is enough | Named related members form one stable object contract |
| Consumption | Callers are primarily F# and composition is lexical | Frameworks or other .NET languages expect members/runtime dispatch |
| State/lifetime | Dependencies are plain values | Implementations own identity, state, or disposal behavior |
| Evolution | The capability is local and easy to replace together | The public member contract and compatibility policy are deliberate |

F# interface implementations are normally explicit. `PriceCalculator.Calculate` is a class member, but `IQuoteService.Quote` is callable through an interface view:

```fsharp
let calculator = PriceCalculator(policy)
let service = calculator :> IQuoteService
let result = service.Quote request
```

The upcast is useful evidence: callers using the concrete class see its concrete API, while callers using the interface see only the contract. Keep interfaces cohesive and small. Splitting every function into an `IThing`/`Thing` pair adds names and indirection without necessarily adding a boundary.

## Object expressions implement a small contract locally {#object-expressions}

An object expression creates an instance of a compiler-generated anonymous object type based on an interface or base class. The example supplies a group-discount policy at the composition root:

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

An intrinsic extension is declared in the same file and namespace/module as the type, is compiled as part of that type, and appears through reflection. An optional extension must be in a module; callers bring that module into scope—automatically here because of `[<AutoOpen>]`—and C# or Visual Basic consumers cannot call it. Type extensions cannot add virtual/abstract members or overrides, and a non-extension member wins an ambiguous call.

Use an extension when member-call discoverability improves a stable derived operation, especially around a type you cannot edit. Prefer an ordinary module function when explicit dependencies, pipeline order, cross-language visibility, or avoiding scope-dependent discovery matters more.

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

If an object owns an `IDisposable` or `IAsyncDisposable` resource, its API must say who disposes it and when methods stop being valid. Chapter 21 and Chapter 23's `use`/`use!` rules still apply. Hiding a handle inside a class does not remove its lifetime; it transfers the responsibility to that class and its caller contract.

Classes with mutable state also need a concurrency policy. “Private” prevents direct field access, not simultaneous member calls. Publish immutability, serialize ownership, or synchronize the whole invariant using Chapter 24's rules.

## A compact public-API review {#api-review}

Before publishing an object-shaped API, ask:

- Would a function, record, or union state the same semantics more directly?
- Does reference identity matter, and is equality behavior explicit?
- Which constructor arguments are configuration errors, and which method inputs produce typed domain errors?
- Is every interface member one cohesive contract rather than speculative extensibility?
- Does an object expression remain local and small?
- Is a type extension discoverable in the scopes and languages that need it?
- Is a struct's zero value valid, and has copying/boxing been measured?
- Who owns disposal, mutation, cancellation, and thread safety?
- Does the public representation suit both current callers and future compatibility constraints?

Chapter 27 will revisit the last question from a C# consumer's view. Chapter 31 will supply measurement before representation-level optimization.

## Run the verified example {#run-example}

From the repository root:

```console
dotnet run --project examples/chapters/ch25/Ch25.fsproj --configuration Release
dotnet test ThinkingInFSharp.slnx --configuration Release --filter FullyQualifiedName~Ch25Object
```

The program prints class, interface, extension, and struct observations. Focused tests cover both constructors, member validation, the explicit interface view, an object-expression substitute, derived extension members, value copying, distinct boxes, and the invalid zero-initialized revision.

## Exercises {#exercises}

### Exercise 1: remove a ceremonial class {#exercise-01}

A `SeatRequest` class only stores an identifier and seat count through read-only properties. Replace it with an immutable record. Put expected validation in a module function returning `Result`, and explain what would have justified keeping a class.

### Exercise 2: choose a policy boundary {#exercise-02}

Implement the same discount rule once as a function and once as an `IDiscountPolicy` object expression. Use both from a calculation, then state which public boundary you would keep for an F#-only library and which condition could justify the interface.

### Exercise 3: audit a struct invariant {#exercise-03}

Create a positive revision struct through a smart constructor, copy it, box both copies, and observe its default value. Then redesign the type so that zero initialization represents an explicit valid state, or document and test rejection at every default-producing boundary.

[Read the chapter solutions](../solutions/ch-25-objects-interfaces).

## Model review {#model-review}

- F# object features are native tools, not a required destination for every model.
- Functions express behavior; records express products; unions express alternatives; classes add reference identity and object protocols.
- Constructor failure and expected method rejection need different policies.
- Explicit interface implementations are consumed through an interface view.
- Object expressions suit small local implementations; size and lifecycle justify a named type.
- Extensions add callable behavior but no stored state or representation change.
- Structs copy by value and always have a zero-initialized default representation.
- Composition is the default; inherit only for a real base-type contract.
- Encapsulation transfers lifecycle and concurrency responsibility—it does not erase it.

## Sources {#sources}

- [Microsoft Learn: classes in F#](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/classes)
- [Microsoft Learn: constructors in F#](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/members/constructors)
- [Microsoft Learn: interfaces in F#](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/interfaces)
- [Microsoft Learn: object expressions in F#](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/object-expressions)
- [Microsoft Learn: type extensions in F#](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/type-extensions)
- [Microsoft Learn: structs in F#](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/structs)
