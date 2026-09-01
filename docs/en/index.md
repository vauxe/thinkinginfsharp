---
title: Thinking in F#
description: Learn functional modeling and production .NET engineering from F# itself.
translationKey: index
outline: false
aside: false
---

# Thinking in F# {#overview}

This book starts with F# expressions, values, types, and functions, then applies them to effects, testing, .NET interoperability, and complete applications.

::: tip Start here
If F# is new to you, read the [preface](./preface/) and then [start Chapter 1](./part-01/ch-01-first-session). Continue through Parts I–III in order; use the later parts when you need their topics.
:::

## Executable single-file tour {#single-file-tour}

Readers who want a compact, Chinese-commented companion can run `examples/learn-fsharp.fsx`. In one fresh F# Interactive process, it checks the included examples while surveying the core language, type modeling, .NET interoperability, asynchrony, and project boundaries. Use it for review after the ordered chapters, not as a replacement for their explanations and exercises.

```console
dotnet fsi --nologo --warnaserror+ --checknulls+ --exec examples/learn-fsharp.fsx
```

## Part I · Expressions and functions {#part-1}

- [Chapter 1: A First F# Session](./part-01/ch-01-first-session)
- [Chapter 2: Values, Bindings, and Expressions](./part-01/ch-02-values-bindings-expressions)
- [Chapter 3: Functions Are Values](./part-01/ch-03-functions-as-values)
- [Chapter 4: Branching and Basic Patterns](./part-01/ch-04-branching-patterns)
- [Chapter 5: Lists, Pipelines, and Data Flow](./part-01/ch-05-lists-pipelines)
- [Chapter 6: Recursion, Tail Calls, and Folds](./part-01/ch-06-recursion-folds)

## Part II · Modeling with types {#part-2}

- [Chapter 7: Records, Updates, Equality, and Comparison](./part-02/ch-07-records-equality)
- [Chapter 8: Discriminated Unions and State Modeling](./part-02/ch-08-discriminated-unions)
- [Chapter 9: Absence and Expected Failure](./part-02/ch-09-option-result)
- [Chapter 10: Recursive Types and Structural Recursion](./part-02/ch-10-recursive-types)
- [Chapter 11: Generics, Constraints, and Units](./part-02/ch-11-generics-constraints)
- [Chapter 12: Making Illegal States Unrepresentable](./part-02/ch-12-making-illegal-states-unrepresentable)

## Part III · Composition and program structure {#part-3}

- [Chapter 13: Composition, Argument Order, and Pipeline APIs](./part-03/ch-13-composition-pipeline-api)
- [Chapter 14: Choosing Collections and Evaluation Models](./part-03/ch-14-collections-evaluation)
- [Chapter 15: Active Patterns and Domain Matching Boundaries](./part-03/ch-15-active-patterns)
- [Chapter 16: Modules, Namespaces, Projects, and Compiler Settings](./part-03/ch-16-modules-namespaces-projects)
- [Chapter 17: Signatures, Access Control, and F#-Facing APIs](./part-03/ch-17-signatures-encapsulation)
- [Chapter 18: Explicit Workflow Composition and Validation Accumulation](./part-03/ch-18-workflow-validation)

## Part IV · Effects, asynchrony, and concurrency {#part-4}

- [Chapter 19: .NET APIs and Null Boundaries](./part-04/ch-19-dotnet-null-boundaries)
- [Chapter 20: Functional Core and Effect Boundaries](./part-04/ch-20-functional-core-effects)
- [Chapter 21: Exceptions, Resources, and I/O](./part-04/ch-21-exceptions-resources-io)
- [Chapter 22: Async<'T> and Task<'T>](./part-04/ch-22-async-task)
- [Chapter 23: Cancellation, Timeouts, Faults, and Disposal](./part-04/ch-23-cancellation-timeouts)
- [Chapter 24: Parallelism, Concurrency, Agents, and Controlled Mutation](./part-04/ch-24-concurrency-agents-state)

## Part V · .NET interop and engineering quality {#part-5}

- [Chapter 25: Defining Objects in F#](./part-05/ch-25-objects-interfaces)
- [Chapter 26: Deeper .NET Interoperability](./part-05/ch-26-dotnet-runtime-boundaries)
- [Chapter 27: Designing F# APIs for C#](./part-05/ch-27-fsharp-api-for-csharp)
- [Chapter 28: Example Tests, Test Doubles, and Contract Tests](./part-05/ch-28-testing-boundaries)
- [Chapter 29: Property Testing with FsCheck](./part-05/ch-29-property-testing)
- [Chapter 30: Diagnostics, Debugging, Formatting, and Builds](./part-05/ch-30-diagnostics-tooling-builds)
- [Chapter 31: Measure Before Optimizing](./part-05/ch-31-measure-before-optimizing)
- [Chapter 32: From Functions to Applications](./part-05/ch-32-functions-to-applications)

## Part VI · A complete booking workflow {#part-6}

- [Chapter 33: Business Language, Commands, Events, and Model](./part-06/ch-33-domain-language-model)
- [Chapter 34: The Pure Booking Workflow and Validation](./part-06/ch-34-pure-booking-workflow)
- [Chapter 35: Ports, Persistence, Configuration, and Stubs](./part-06/ch-35-ports-persistence-config)
- [Chapter 36: Web API, JSON, and Input Boundaries](./part-06/ch-36-web-api-boundaries)
- [Chapter 37: Consistency, Idempotency, Retries, and Partial Failure](./part-06/ch-37-consistency-idempotency)
- [Chapter 38: Integration, Diagnostics, C# Client, and Release Verification](./part-06/ch-38-integration-diagnostics-release)

## Part VII · Where F# fits {#part-7}

- [Chapter 39: ASP.NET Core and the F# Web Ecosystem](./part-07/ch-39-web-ecosystem)
- [Chapter 40: Data, Type Providers, Analytics, and Machine Learning](./part-07/ch-40-data-analytics)
- [Chapter 41: Fable, Elmish, and Browser Applications](./part-07/ch-41-fable-elmish)
- [Chapter 42: Cloud, Containers, Serverless, and .NET Aspire](./part-07/ch-42-cloud-containers-aspire)
- [Chapter 43: Avalonia, Desktop, and Mobile](./part-07/ch-43-avalonia-desktop-mobile)
- [Chapter 44: Unity 6.3 LTS and F#](./part-07/ch-44-unity)
- [Chapter 45: Scripting, Automation, Packages, and What Comes Next](./part-07/ch-45-scripting-packages-next)

## Reference {#reference}

- [Appendix A: Cross-Platform Setup](./appendices/a-setup)
- [Appendix B: Syntax and Operator Quick Reference](./appendices/b-syntax-reference)
- [Appendix C: Collection Choice and Complexity](./appendices/c-collections)
- [Appendix D: C# to F# Migration and Interop](./appendices/d-csharp-migration)
- [Appendix E: Common Compiler Diagnostic Index](./appendices/e-compiler-errors)
- [Appendix F: F# Glossary](./glossary)
- [Appendix G: Working with Exercises and Answers](./appendices/g-solutions-guide)
- [Appendix H: Advanced Feature Recognition Index](./appendices/h-advanced-index)
