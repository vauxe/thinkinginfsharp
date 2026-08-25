---
title: "Chapter 17: Signatures, Access Control, and F#-Facing APIs"
description: "Use `.fsi` files as checked public contracts, hide implementation representation, and design a small idiomatic surface for F# consumers."
translationKey: part-03/ch-17-signatures-encapsulation
kind: chapter
part: 3
chapter: 17
status: complete
verifiedWith:
  fsharp: "10"
  dotnetSdk: "10.0.301"
exampleIds:
  - ch17-signature-library
  - ch17-hidden-representation
exerciseIds:
  - ch17-exercise-01
  - ch17-exercise-02
  - ch17-exercise-03
termIds:
  - abstract-representation
  - public-api-surface
sources:
  - id: microsoft-signature-files
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/signature-files
    checked: "2026-08-24"
  - id: microsoft-access-control
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/access-control
    checked: "2026-08-24"
  - id: microsoft-modules
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/modules
    checked: "2026-08-24"
  - id: microsoft-component-design
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/style-guide/component-design-guidelines
    checked: "2026-08-24"
---

# Chapter 17: Signatures, Access Control, and F#-Facing APIs {#overview}

An implementation contains everything needed to make a component work. A consumer should usually depend on less: stable domain names, safe construction paths, useful observations, and operations whose types explain their outcomes. An F# signature file turns that smaller view into a compiler-checked contract.

`Library.fs` answers “how does this work?” Its matching `Library.fsi` answers “what may code outside this implementation file know?” The compiler checks that the implementation fulfills the signature and hides declarations the signature omits. This is more than generated documentation and more precise than a naming convention.

## What you will be able to do {#outcomes}

By the end of this chapter, you should be able to:

- read a paired `.fsi` signature and `.fs` implementation;
- place the signature immediately before its implementation in project order;
- declare values and functions with `val` rather than implementation bodies;
- expose a type name while hiding its union cases or record fields;
- deliberately expose error union cases that consumers should match;
- distinguish signature omission from `private`, `internal`, and `public`;
- predict common signature/implementation mismatches;
- design a small F#-facing API with modules, curried functions, and typed outcomes;
- test a library from a separate consumer assembly without coupling to representation;
- decide when signature-file maintenance is valuable and when it is premature.

## The signature is the consumer's view {#signature-as-view}

The chapter library uses this compilation shape:

```text
Library.fsi  ── constrains ──▶  Library.fs
     │                              │
     └──── visible contract ────────┴──▶ later files and assemblies
```

The signature contains namespaces, modules, type declarations, and value signatures, but no function bodies. The implementation contains representations and executable code. Every declaration exposed in the signature must be supplied compatibly by the implementation; extra implementation declarations remain hidden from code outside that implementation file.

This is an information boundary, not a runtime call layer. Calling `Capacity.value` does not dispatch through the `.fsi` file. The signature has already influenced compilation and emitted visibility; the runtime executes the implementation.

## A paired file occupies one compilation boundary {#paired-files}

The project records the pair explicitly:

<<< @/../examples/chapters/ch17/Ch17.fsproj{xml:line-numbers} [Ch17.fsproj]

The signature has the same base name as the implementation and appears immediately before it: `Library.fsi`, then `Library.fs`. Reversing them makes the compiler process an implementation before its contract; separating the pair with dependent code gives that code the wrong boundary.

One implementation file may have at most its matching signature in this form. A signature is not a header that several unrelated `.fs` files append to, and a later file cannot reopen the implementation to reach declarations that were omitted.

Chapter 16's provider-before-consumer rule still applies. The pair acts as one provider: the signature states its visible shape, the implementation satisfies it, and later files consume only that shape.

## Read a signature from the outside in {#read-signature}

Here is the complete public contract used by the tests:

<<< @/../examples/chapters/ch17/Library.fsi{fsharp:line-numbers} [Library.fsi]

Read it in layers:

1. `namespace ThinkingInFSharp.Ch17` gives the stable outer path.
2. `module SeatAllocation` groups the F#-facing vocabulary.
3. Error unions expose named cases and their payloads.
4. `type Capacity`, `type SeatCount`, and `type Allocation` expose names but no representation.
5. Same-named modules publish safe construction and observation functions.
6. `allocate` publishes the workflow with capacity first and the flowing request last.

The `val` keyword introduces the type of a value or function. Parameter labels such as `raw`, `capacity`, and `requested` appear in tooling and public metadata, so they should describe meaning rather than merely mirror local variable names.

No caller needs to know whether `Capacity` is implemented as a union, record, class, or something else. Callers can store it, pass it to `allocate`, and observe it through `Capacity.value`. That is an abstract representation: the type remains usable while its shape does not become a dependency.

## Hide proof-carrying values; expose actionable alternatives {#selective-exposure}

The signature makes different choices for different types:

```fsharp
type CapacityError =
    | NonPositiveCapacity of actual: int

type Capacity
```

`CapacityError` is transparent because a caller should match the rejection and use `actual`. `Capacity` is abstract because callers must not bypass `Capacity.create` or depend on its storage. “Public type” does not mean “public representation.”

Likewise, `AllocationError` exposes `InsufficientCapacity(requested, available)` because those facts guide a response. `Allocation` hides its record fields because only `allocate` may establish the relationship:

```text
0 < requested ≤ capacity
remaining = capacity − requested
```

Its observation functions reveal exactly what consumers need without offering record construction or copy-and-update. This connects to Chapter 12's smart-constructor pattern, but the protection is now explicit across files and assemblies.

Do not hide every union or record. A public union is ideal when its complete set of cases is the domain vocabulary consumers should construct and exhaustively match. A public record is ideal when transparent data composition is the intended API. Hide a representation when construction carries proof, fields must remain synchronized, or representation evolution should not rewrite consumer code.

## The implementation may be richer than the contract {#implementation}

The implementation supplies the hidden cases, record fields, and function bodies:

<<< @/../examples/chapters/ch17/Library.fs{fsharp:line-numbers} [Library.fs]

Inside `Library.fs`, the `Capacity` and `SeatCount` union cases are available, and `Allocation` can be constructed as a record. Outside the implementation file, the matching `.fsi` removes those shapes from the visible API even though the `.fs` declarations themselves do not use `private` representation modifiers.

This separation permits a later implementation to store a different numeric type, cache a derived value, or replace the allocation record, provided the published types and behavior remain compatible. The signature does not prove behavioral equivalence; tests still protect invariants and semantics.

An implementation may also contain helpers absent from the signature. Omission is often clearer than decorating every helper with an access modifier, but the signature should not become a dumping ground generated once and then ignored. Review every exposed line as a support commitment.

## Signature and implementation must agree {#matching-rules}

The compiler checks more than names. Important agreement includes:

- namespaces, modules, and types exposed by the signature must exist in the implementation;
- function input/output types, generic parameters, and constraints must match;
- curried versus tupled parameter structure—arity—must match;
- relevant accessibility, `inline`, and `mutable` modifiers must match;
- literal attributes and values must match;
- a record or discriminated union exposes either all fields/cases or none through an abstract declaration;
- exposed declaration order must remain compatible with implementation order.

For example, these are different APIs:

```fsharp
// Signature: two curried arguments
val allocate: capacity: Capacity -> requested: SeatCount -> Result<Allocation, AllocationError>

// Implementation: one tupled argument — does not satisfy that signature
let allocate (capacity, requested) =
    // ...
```

The compiler rejects the pair before a consumer can use it. It also uses parameter names from the signature as the public names; keeping signature and implementation labels aligned avoids misleading debugging and profiling information. Warning 3218 can be enabled when a project wants the compiler to report parameter-name mismatches.

The compiler can generate an initial signature view, and F# Interactive prints inferred signatures for entered definitions. Treat generated output as inventory, not design: remove helpers, choose abstraction deliberately, improve parameter names, add documentation, then let the compiler keep both files synchronized.

## Access control has several distinct boundaries {#access-control}

Signatures complement ordinary access modifiers:

| Mechanism | Who can use the declaration? | Typical purpose |
|---|---|---|
| `private` | Code in the enclosing type or module | Local representation or helper |
| `internal` | Code anywhere in the same assembly | Cross-file implementation facility |
| `public` or common omitted default | All consumers allowed by the containing API | Supported public surface |
| Omitted from a matching `.fsi` | Code in the implementation file only | Hide an otherwise inferred declaration from later code |
| Abstract `type T` in `.fsi` | Consumers can use `T`, but not its omitted cases/fields | Preserve construction proof and representation freedom |

F# does not use a `protected` keyword for declarations authored in F#. Also remember the placement distinction from Chapter 12: `type private T = ...` hides the type, while `type T = private ...` exposes the type but hides its representation.

An `internal` smart-constructor bypass is available to every file in the assembly, so it is not a strong invariant barrier within that assembly. If only `Library.fs` should use a helper, omit it from the signature or make it private to the enclosing module. If another implementation file genuinely needs it, expose it as `internal` in both signature and implementation and accept the wider trust boundary.

Accessibility cannot be inconsistent. A public function cannot reveal a less-accessible parameter or return type: consumers would see an API they could not name. Start from intended consumers and make every type in a published signature at least as accessible as the value that exposes it.

## Design the public surface from representative uses {#fsharp-facing-api}

The example is intentionally F#-facing. Its surface uses:

- PascalCase domain types and union cases;
- camelCase functions in focused modules;
- same-named type modules such as `Capacity.create` and `Capacity.value`;
- curried functions that support partial application and pipelines;
- `Result` plus transparent error unions for expected rejection;
- abstract representations for values whose constructors carry proof.

These choices let consumer code stay direct:

```fsharp
let tryAllocate capacity requested =
    requested |> allocate capacity
```

Do not contort an F# API around hypothetical C# callers. F# unions, options, curried functions, and modules are appropriate when F# is the audience. Chapter 27 introduces a separately designed C# boundary, where .NET naming, members, delegates, and representation choices may differ.

Before freezing a signature, write representative successful, failure, pipeline, and pattern-match call sites. A compact surface is not automatically usable: hiding every observation forces consumers toward reflection or duplicated work, while exposing every helper prevents implementation change. Publish the smallest complete vocabulary for real tasks.

## Test through the same boundary consumers see {#consumer-tests}

The chapter tests live in another project and reference the library assembly. They can construct values only through `Capacity.create` and `SeatCount.create`, allocate through the public function, and observe results through the published modules. They cover both smart constructors, successful allocation, and insufficient capacity.

That positive suite proves the surface is sufficient. A separate expected-error consumer proves it is restrictive:

<<< @/../examples/expected-errors/ch17-hidden-representation/Consumer.fs{fsharp:line-numbers} [Consumer.fs — expected error]

`Capacity 0` attempts to use the implementation union case. The public signature contains only the abstract type name, so F# 10 rejects the expression with `FS0800`. The test does not use reflection to inspect private layout because layout is precisely what a consumer contract should ignore.

Compile-time opacity and behavioral tests answer different questions:

| Evidence | Proves |
|---|---|
| `.fsi`/`.fs` pair builds | Implementation satisfies the declared API |
| External consumer builds | Public surface is usable without hidden names |
| Invalid consumer fails | Hidden representation cannot be used by ordinary compiled callers |
| Behavioral tests pass | Published operations preserve the stated outcomes and invariants |

None of these claims protection against hostile reflection, unsafe code, corrupted persistence, or bugs inside the trusted implementation. State the boundary honestly.

## Treat signature edits as API edits {#evolution}

Changing only hidden implementation details can leave consumer source unchanged. Changing a line in the signature changes the contract:

- renaming parameter labels affects metadata and tooling; reordering changes call meaning and may change inferred types;
- changing curried/tupled shape or a type breaks calls;
- exposing representation lets consumers acquire dependencies that are difficult to retract;
- adding a case to a public union changes the set consumers must handle;
- removing or narrowing a value breaks consumers directly.

An added function is usually source-compatible, but it still expands the supported surface and can introduce name collisions for code that broadly opens modules. Compatibility is a property to evaluate, not something the `.fsi` extension provides automatically.

Place XML documentation on the public declarations consumers see. A signature becomes a concise review page for naming, parameter order, error shape, and missing observations. The implementation remains the place to explain algorithms and local decisions.

## Add signatures at the right time {#when-to-use}

Explicit signature files are valuable when:

- a library or component has external consumers, even elsewhere in the same repository;
- representation must remain hidden across files;
- reviewers need a concise, compiler-enforced public API inventory;
- the surface is stable enough that deliberate change friction is useful;
- implementation work should proceed without accidentally exporting helpers.

They may be premature for a short experiment, rapidly changing private application code, or a file whose ordinary access modifiers already express the needed boundary. Maintaining both files costs attention, and frequent harmless implementation edits can become noisy if the surface has not stabilized.

Generate or write a signature after representative call sites reveal the right API—not before exploration has named the problem. Once adopted, keep the pair adjacent, build with warnings treated seriously, and review its diff as a public contract.

## Build and verify the example {#build-test}

From the repository root:

```console
dotnet build examples/chapters/ch17/Ch17.fsproj -c Release --locked-mode
dotnet test tests/ExampleTests/ExampleTests.fsproj -c Release --no-restore --filter FullyQualifiedName~Ch17SignatureTests
```

The focused suite passes. This command is intentionally expected to fail and is checked separately:

```console
dotnet build examples/expected-errors/ch17-hidden-representation/Ch17HiddenRepresentation.fsproj -c Release
```

Its required `FS0800` diagnostic guards the representation-hiding claim. A successful build of that invalid consumer would be a regression, not a passing example.

## Exercises {#exercises}

### Exercise 1: design an email-address pair {#exercise-01}

Design `EmailAddress.fsi` and `EmailAddress.fs`. Public callers need an abstract `EmailAddress`, a transparent `EmailAddressError` with `Blank` and `MissingAtSign` cases, `EmailAddress.create`, and `EmailAddress.value`. The implementation also needs a normalization helper that callers must not see.

Write the public signature, sketch the implementation, and state the project order. Explain which declarations may be used by a later file.

### Exercise 2: narrow an overexposed allocation API {#exercise-02}

Review this proposed public signature:

```fsharp
type Allocation =
    { Capacity: int
      Requested: int
      Remaining: int }

val unsafeCreate: capacity: int -> requested: int -> remaining: int -> Allocation
```

Redesign it so consumers cannot create inconsistent fields. Include the minimum construction/workflow and observation functions, and decide whether the insufficient-capacity error cases should remain visible. State one requirement that would instead justify a transparent record.

### Exercise 3: repair arity and choose a helper boundary {#exercise-03}

A signature declares:

```fsharp
val apply: policy: Policy -> request: Request -> Result<Decision, DecisionError>
```

The implementation defines `let apply (policy, request) = ...` and a `traceDecision` helper. Explain why `apply` does not match, then repair it. Show how to keep `traceDecision` usable only inside the implementation file, and how the declarations must change if one later file in the same assembly genuinely needs that helper.

[Read the chapter solutions](../solutions/ch-17-signatures-encapsulation).

## Model review {#model-review}

- A `.fsi` file is a compiler-checked consumer view of its matching `.fs` implementation.
- The signature precedes the implementation and publishes declarations, not bodies.
- An abstract type name lets consumers use values without constructing or deconstructing representation.
- Transparent error unions are useful when callers should match actionable alternatives.
- Signature/implementation types, arity, constraints, modifiers, and exposed order must agree.
- `private`, `internal`, signature omission, and abstract representation protect different scopes.
- A good F#-facing surface is small but complete: safe construction, meaningful operations, necessary observations, and typed outcomes.
- External positive tests prove usability; a compiler-failing consumer can prove opacity.
- Signature edits are API edits, so add explicit signatures when that deliberate stability is worth their maintenance cost.

Chapter 18 uses this bounded public vocabulary to compose larger workflows, contrasting first-error `Result` sequencing with accumulation of independent validation errors.

## Sources {#sources}

- [Microsoft Learn: F# signature files](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/signature-files)
- [Microsoft Learn: F# access control](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/access-control)
- [Microsoft Learn: F# modules](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/modules)
- [Microsoft Learn: F# component design guidelines](https://learn.microsoft.com/en-us/dotnet/fsharp/style-guide/component-design-guidelines)
