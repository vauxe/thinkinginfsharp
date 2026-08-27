---
title: "Appendix H: Advanced Feature Recognition Index"
description: "Recognize quotations, statically resolved type parameters, flexible types, and byref-like code, then decide whether deeper study belongs to the problem at hand."
translationKey: appendices/h-advanced-index
---

# Appendix H: Advanced Feature Recognition Index {#overview}

This appendix is a recognition guide. It helps you identify four F# feature families in unfamiliar code, ask the first useful question, and find an authoritative reference. None of them is a prerequisite for everyday F#.

The foundation remains functions, types, patterns, modules, collections, controlled effects, async/task, .NET interoperability, and tests. Study an advanced feature deeply only when an API, profiling result, library, or necessary abstraction requires it.

## Quick recognition table {#quick-map}

| Feature | Recognition clues | Core idea | First question |
|---|---|---|---|
| quotations | `<@ ... @>`, `<@@ ... @@>`, `Expr<'T>`, quotation patterns | represent an F# expression as data | is the code constructing, transforming, translating, or executing an expression tree? |
| SRTP | `inline` with static/member constraints; `'T` in current simplified signatures or `^T` in older/complex forms | resolve required members and specialize at compile time | why can ordinary generics, an interface, or concrete overloads not express this clearly? |
| flexible types | `#BaseType` inside a type expression | accept any subtype or interface implementation in a nested/higher-order position | would a normal parameter upcast already work, and is the more general signature worth exposing? |
| byref/Span | `&value`, `byref`, `inref`, `outref`, `Span`, `ReadOnlySpan` | use stack-constrained managed references and contiguous-memory views | which measured copying cost or interop API justifies the lifetime restrictions? |

Do not infer meaning from punctuation alone. The same symbols occur elsewhere: `#` begins FSI directives, `^` appears in older SRTP syntax and in operators, and `&` also appears in member constraints and Boolean operators. `<@` has this meaning only when it begins quotation syntax.

## Quotations: code represented as expression data {#quotations}

A typed quotation uses `<@ expression @>` and has type `Expr<'T>`. An untyped quotation uses `<@@ expression @@>` and has type `Expr`. The compiler builds an object tree that represents the quoted expression instead of executing that expression at this location.

Recognition signals include:

- `open Microsoft.FSharp.Quotations`;
- `Expr<'T>` or `Expr` values;
- patterns from `Microsoft.FSharp.Quotations.Patterns`, `DerivedPatterns`, or `ExprShape`;
- splice operators `%` and `%%` inside another quotation;
- a library API that accepts an expression tree for a query, DSL, translation, analysis, or generator.

The first distinction is representation versus execution. A quotation does not execute itself. An evaluator, translator, provider, or other consumer gives the tree meaning, and that consumer may support only a subset of possible expressions.

Use quotations when a caller genuinely needs to inspect code as data—for example, when a typed query translator reads property access and comparisons. Do not wrap ordinary callbacks in quotations merely because expression trees look more powerful. A function is simpler when the caller only needs to invoke the behavior.

Review:

- which expression nodes the consumer accepts and how unsupported nodes fail;
- whether captured values are embedded, parameterized, serialized, or rejected;
- whether evaluation happens locally, remotely, through generated code, or not at all;
- whether quotations cross version, process, trust, trimming, or AOT boundaries;
- whether diagnostics point back to useful source locations.

Chapter 15's active-pattern mindset helps when traversing expression shapes. [Chapter 40](../part-07/ch-40-data-analytics) shows why data/query tools may expose typed expression surfaces without requiring every user to author quotation processors.

## SRTP: compile-time member constraints {#srtp}

A statically resolved type parameter lets an inline function require members that ordinary .NET generic constraints cannot express. F# resolves the required members at compile time and specializes inline uses.

Current F# simplified syntax commonly prints apostrophe-prefixed parameters such as `'T` even for SRTP constraints. Documentation and complex explicit dispatch code may still use caret-prefixed forms such as `^T`. Therefore identify SRTP by the combination of:

- an `inline` function or member;
- a static or instance member constraint;
- operator/member resolution at the call site;
- specialization rather than one ordinary generic method body.

Common entry points are numeric operators and small member-based abstractions. Many FSharp.Core operators already expose SRTP; calling them does not mean you should author a custom abstraction.

Before introducing SRTP, compare:

- a concrete type that expresses the domain more directly;
- an ordinary generic function with no member requirement;
- an interface, delegate, or record of operations passed explicitly;
- a small set of named overloads;
- current .NET generic-math interfaces when interoperating with that API family is the real goal.

SRTP can enlarge inferred signatures, duplicate specialized code, increase compile time, make public APIs harder to consume, and interact subtly with inference. Keep it local, inspect the inferred type, test every intended instantiation, and avoid exposing incidental constraints.

[Chapter 11](../part-02/ch-11-generics-constraints) establishes the distinction between ordinary generics, equality/comparison constraints, and SRTP. Its current wording follows the F# 7+ simplified syntax rather than treating `^T` punctuation as the definition.

## Flexible types: subtype compatibility in a type expression {#flexible-types}

`#SomeType` is a flexible type annotation. Conceptually it is equivalent to a fresh generic type constrained with `:> SomeType`. It is especially useful when the compatible type appears inside a higher-order or nested type position where automatic upcasting does not occur.

For example, a signature may accept a function returning `#seq<'T>` so that callers can return a list, array, or another sequence implementation without writing an explicit upcast.

Recognition questions are:

- is `#` inside a type annotation rather than at the start of an FSI directive?
- which base class or interface establishes compatibility?
- is the flexible type nested inside a function, collection, or other generic position?
- does the more general input help callers, or merely make the inferred signature harder to explain?

Prefer direct argument types when ordinary automatic conversion already works. Use an explicit named generic constraint when it makes a public API or implementation easier to read. Flexible syntax is a compact signature tool, not a different runtime representation.

## Byref and Span: constrained lifetimes for interop and buffers {#byref-span}

`byref<'T>`, `inref<'T>`, and `outref<'T>` are managed-reference types for read/write, read-oriented, and write-oriented boundaries. `Span<'T>` and `ReadOnlySpan<'T>` are byref-like views over contiguous memory. The compiler enforces escape and capture restrictions so these values cannot outlive the storage they reference.

Recognition signals include:

- `&value` when passing or preserving a managed reference;
- parameters or returns containing `byref`, `inref`, or `outref`;
- `Span`, `ReadOnlySpan`, or a type marked `IsByRefLike`;
- code that cannot capture a value in a closure, object field, or asynchronous workflow;
- direct adaptation to a .NET API that already uses spans or references.

Use these types in synchronous code when measurement shows that avoiding a slice or copy matters, or when an existing API requires them. They are not replacements for arrays, lists, records, or `Memory<'T>` in long-lived and asynchronous code.

Review ownership of the underlying storage, mutation and aliasing, empty/default values, bounds, escape lifetime, and behavior after the call. `inref` restricts what that reference holder may do; it does not prove that no other alias mutates the value.

F# restrictions and supported interop evolve. Check the language version and current official page rather than copying an old workaround. [Chapter 31](../part-05/ch-31-measure-before-optimizing) supplies the profiling and representation decision; this appendix only helps recognize the syntax.

## Features deliberately outside this edition's teaching scope {#scope-boundary}

This edition does not teach:

- authoring type providers;
- building tools on FSharp.Compiler.Service;
- writing a general quotation evaluator or compiler;
- advanced SRTP dispatch frameworks;
- authoring custom byref-like data structures;
- using low-level features without a measured or interoperability requirement.

You can still consume a type provider, compiler-backed tool, query library, numeric abstraction, or Span-based .NET API by understanding its public contract. “Not taught here” means the implementation subject needs its own versioned, source-driven guide—not that the ecosystem capability is invalid.

## A safe reading sequence {#reading-sequence}

When unfamiliar advanced code blocks progress:

1. capture the smallest inferred public signature;
2. identify whether the feature represents code, resolves members, broadens subtype input, or constrains memory lifetime;
3. read the linked official reference for the exact language version;
4. isolate one executable example with one failure or rejection;
5. find where the feature enters and leaves the larger system;
6. decide whether to learn the implementation, wrap it, or keep it behind a library adapter;
7. record target, version, performance, trimming/AOT, and interoperability evidence when relevant.

Return to [Chapter 45](../part-07/ch-45-scripting-packages-next) for the broader learning and package-selection map. Advanced knowledge is most durable when it answers a system question you can test.

## Official entry points {#official-entry-points}

- [Microsoft Learn: code quotations](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/code-quotations)
- [FSharp.Core quotation API](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-quotations.html)
- [Microsoft Learn: statically resolved type parameters](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/generics/statically-resolved-type-parameters)
- [Microsoft Learn: flexible types](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/flexible-types)
- [Microsoft Learn: byrefs and byref-like structs](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/byrefs)
- [Microsoft Learn: Memory and Span usage guidelines](https://learn.microsoft.com/en-us/dotnet/standard/memory-and-spans/memory-t-usage-guidelines)
