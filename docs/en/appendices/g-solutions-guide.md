---
title: "Appendix G: Solutions and Open-Exercise Review Guide"
description: "Reach all 45 solution pages and review closed, diagnostic, and open design exercises without pretending that engineering has one canonical answer."
translationKey: appendices/g-solutions-guide
---

# Appendix G: Solutions and Open-Exercise Review Guide {#overview}

A solution is feedback, not a substitute for attempting the exercise. Compare contracts, types, effects, and evidence before comparing surface syntax. Producing the same printed line can still miss a modeling, ownership, failure, or interoperability goal.

Some exercises have a narrow observable result; others ask for a diagnosis or an engineering design. The solution pages therefore show reasoning, constraints, and representative implementations. They do not claim that every open question has one canonical answer.

Each chapter links to its own solution page. Attempt the exercise first, then use the solution to compare reasoning and tradeoffs.

## Before opening a solution {#before-opening}

1. Restate the required behavior and every explicit constraint in your own words.
2. Predict the important type signatures, output, failure, and effect order before running code.
3. Run the narrowest relevant command and retain unexpected evidence instead of editing toward the book output blindly.
4. Explain why your answer satisfies the task; then inspect the solution and compare decisions, not line counts.

## Three exercise kinds {#exercise-kinds}

| Kind | What can be checked directly | Where variation remains |
|---|---|---|
| closed behavior | required value, type, output order, diagnostic, or test | names and implementation may differ while preserving the whole contract |
| diagnosis | reproduction command, first relevant evidence, root cause, and repair | several repairs may compile, but only those preserving intended semantics qualify |
| open design | constraints, invariants, boundary, failure policy, and verification plan | representation, library, architecture, and rollout may differ with explicit tradeoffs |

## Rubric for open design exercises {#open-design-rubric}

| Dimension | Meets the exercise | Strong evidence |
|---|---|---|
| contract | covers every stated input, output, failure, and non-goal | identifies ambiguity and records a bounded assumption |
| model | types represent required states without needless ceremony | invalid states are excluded or rejected at one clear boundary |
| effects and ownership | names where I/O, time, mutation, resources, and cancellation live | lifetime and partial-failure behavior are testable and locally controlled |
| API and interop | callers can use the surface in their own language/tooling | compiled call sites, nullability, compatibility, and representation leakage are checked |
| evidence | supplies a reproducible build, test, probe, or explicitly marked review | tests counterexamples and separates executed, reviewed, and unverified claims |
| clarity and scope | solves the requested problem without hiding key decisions | compares a plausible alternative and explains the stop condition |

## Acceptable variation and hard failures {#acceptable-variation}

Recursion, a fold, or a small loop may all be sound when stack use, order, and ownership match. A record or class, function or interface, list or array, `Result` or domain union, and `Async` or `Task` are likewise decisions made from the boundary—not style points awarded in isolation.

A different answer is acceptable when it preserves explicit constraints, makes new assumptions visible, and supplies proportional evidence. Improve the published solution when your alternative is simpler and at least as well proven.

Reject an answer that suppresses a relevant warning, hides a new union case behind a wildcard, uses timing sleeps as concurrency proof, leaks secrets or accidental representation, reports an unrun platform check as passing, changes the public contract silently, or offers output without explaining the causal model.

## Match the claim to the evidence {#evidence}

| Claim | Minimum suitable evidence |
|---|---|
| a type relationship or diagnostic | locked compiler invocation and exact relevant signature/code |
| pure behavior or invariant | focused example/property tests including a counterexample or boundary |
| resource, async, concurrency, or interop behavior | real boundary test with deterministic coordination and cleanup |
| framework/platform adoption | compiled minimal slice plus explicit untested platform/deployment limits |
| proposed architecture or package choice | written constraints, official-source review, spike plan, rollback/removal condition |

Not every prose design in a solution page is executable evidence. Distinguish code you ran, compiler evidence, official-source review, and proposed work. Do not promote a proposal to “verified” merely because it appears under Solutions.

## All chapter answers {#answer-index}

Every exercise link below targets its exact answer heading. “Review focus” comes from the corresponding solution page and summarizes what comparison should teach.

## Part 1 · Expressions and functions {#part-1}

### Chapter 1: A First F# Session {#chapter-01}

[Chapter](../part-01/ch-01-first-session#overview) · [Solution page](../solutions/ch-01-first-session#overview)

**Answers:** [Exercise 1](../solutions/ch-01-first-session#exercise-01) · [Exercise 2](../solutions/ch-01-first-session#exercise-02) · [Exercise 3](../solutions/ch-01-first-session#exercise-03)

**Review focus:** Reasoning, a migration example, and execution-entry choices for the first F# session.

### Chapter 2: Values, Bindings, and Expressions {#chapter-02}

[Chapter](../part-01/ch-02-values-bindings-expressions#overview) · [Solution page](../solutions/ch-02-values-bindings-expressions#overview)

**Answers:** [Exercise 1](../solutions/ch-02-values-bindings-expressions#exercise-01) · [Exercise 2](../solutions/ch-02-values-bindings-expressions#exercise-02) · [Exercise 3](../solutions/ch-02-values-bindings-expressions#exercise-03)

**Review focus:** Reasoning about values, bindings, basic types, explicit conversion, and local shadowing.

### Chapter 3: Functions Are Values {#chapter-03}

[Chapter](../part-01/ch-03-functions-as-values#overview) · [Solution page](../solutions/ch-03-functions-as-values#overview)

**Answers:** [Exercise 1](../solutions/ch-03-functions-as-values#exercise-01) · [Exercise 2](../solutions/ch-03-functions-as-values#exercise-02) · [Exercise 3](../solutions/ch-03-functions-as-values#exercise-03)

**Review focus:** Reasoning about function types, lambdas, higher-order functions, currying, tupled parameters, and partial application.

### Chapter 4: Branching and Basic Patterns {#chapter-04}

[Chapter](../part-01/ch-04-branching-patterns#overview) · [Solution page](../solutions/ch-04-branching-patterns#overview)

**Answers:** [Exercise 1](../solutions/ch-04-branching-patterns#exercise-01) · [Exercise 2](../solutions/ch-04-branching-patterns#exercise-02) · [Exercise 3](../solutions/ch-04-branching-patterns#exercise-03)

**Review focus:** Reasoning about conditional results, match order, guards, and tuple and list patterns.

### Chapter 5: Lists, Pipelines, and Data Flow {#chapter-05}

[Chapter](../part-01/ch-05-lists-pipelines#overview) · [Solution page](../solutions/ch-05-lists-pipelines#overview)

**Answers:** [Exercise 1](../solutions/ch-05-lists-pipelines#exercise-01) · [Exercise 2](../solutions/ch-05-lists-pipelines#exercise-02) · [Exercise 3](../solutions/ch-05-lists-pipelines#exercise-03)

**Review focus:** Reasoning about list transformations, pipelines, choose, for, while, and local mutable state.

### Chapter 6: Recursion, Tail Calls, and Folds {#chapter-06}

[Chapter](../part-01/ch-06-recursion-folds#overview) · [Solution page](../solutions/ch-06-recursion-folds#overview)

**Answers:** [Exercise 1](../solutions/ch-06-recursion-folds#exercise-01) · [Exercise 2](../solutions/ch-06-recursion-folds#exercise-02) · [Exercise 3](../solutions/ch-06-recursion-folds#exercise-03)

**Review focus:** Reasoning about structural recursion, accumulator invariants, tail calls, and left and right folds.

## Part 2 · Modeling with types {#part-2}

### Chapter 7: Records, Updates, Equality, and Comparison {#chapter-07}

[Chapter](../part-02/ch-07-records-equality#overview) · [Solution page](../solutions/ch-07-records-equality#overview)

**Answers:** [Exercise 1](../solutions/ch-07-records-equality#exercise-01) · [Exercise 2](../solutions/ch-07-records-equality#exercise-02) · [Exercise 3](../solutions/ch-07-records-equality#exercise-03)

**Review focus:** Reasoning about tuple migration, immutable updates, structural equality, reference identity, hash contracts, and business ordering.

### Chapter 8: Discriminated Unions and State Modeling {#chapter-08}

[Chapter](../part-02/ch-08-discriminated-unions#overview) · [Solution page](../solutions/ch-08-discriminated-unions#overview)

**Answers:** [Exercise 1](../solutions/ch-08-discriminated-unions#exercise-01) · [Exercise 2](../solutions/ch-08-discriminated-unions#exercise-02) · [Exercise 3](../solutions/ch-08-discriminated-unions#exercise-03)

**Review focus:** Reasoning about flag combinations, union cases, exhaustiveness, and state-transition policy.

### Chapter 9: Absence and Expected Failure {#chapter-09}

[Chapter](../part-02/ch-09-option-result#overview) · [Solution page](../solutions/ch-09-option-result#overview)

**Answers:** [Exercise 1](../solutions/ch-09-option-result#exercise-01) · [Exercise 2](../solutions/ch-09-option-result#exercise-02) · [Exercise 3](../solutions/ch-09-option-result#exercise-03)

**Review focus:** Reasoning about option, Result, composition, short-circuiting, and structured error context.

### Chapter 10: Recursive Types and Structural Recursion {#chapter-10}

[Chapter](../part-02/ch-10-recursive-types#overview) · [Solution page](../solutions/ch-10-recursive-types#overview)

**Answers:** [Exercise 1](../solutions/ch-10-recursive-types#exercise-01) · [Exercise 2](../solutions/ch-10-recursive-types#exercise-02) · [Exercise 3](../solutions/ch-10-recursive-types#exercise-03)

**Review focus:** Derive a short-circuiting query, map laws, and a one-pass tree summary from recursive cases.

### Chapter 11: Generics, Constraints, and Units {#chapter-11}

[Chapter](../part-02/ch-11-generics-constraints#overview) · [Solution page](../solutions/ch-11-generics-constraints#overview)

**Answers:** [Exercise 1](../solutions/ch-11-generics-constraints#exercise-01) · [Exercise 2](../solutions/ch-11-generics-constraints#exercise-02) · [Exercise 3](../solutions/ch-11-generics-constraints#exercise-03)

**Review focus:** Infer generic signatures, repair value restrictions by intent, and preserve measured dimensions at boundaries.

### Chapter 12: Making Illegal States Unrepresentable {#chapter-12}

[Chapter](../part-02/ch-12-making-illegal-states-unrepresentable#overview) · [Solution page](../solutions/ch-12-making-illegal-states-unrepresentable#overview)

**Answers:** [Exercise 1](../solutions/ch-12-making-illegal-states-unrepresentable#exercise-01) · [Exercise 2](../solutions/ch-12-making-illegal-states-unrepresentable#exercise-02) · [Exercise 3](../solutions/ch-12-making-illegal-states-unrepresentable#exercise-03)

**Review focus:** Protect a bounded value, choose an outer-record boundary, and correct a cross-file capacity API whose types blur capacity with availability.

## Part 3 · Composition and program structure {#part-3}

### Chapter 13: Composition, Argument Order, and Pipeline APIs {#chapter-13}

[Chapter](../part-03/ch-13-composition-pipeline-api#overview) · [Solution page](../solutions/ch-13-composition-pipeline-api#overview)

**Answers:** [Exercise 1](../solutions/ch-13-composition-pipeline-api#exercise-01) · [Exercise 2](../solutions/ch-13-composition-pipeline-api#exercise-02) · [Exercise 3](../solutions/ch-13-composition-pipeline-api#exercise-03)

**Review focus:** Translate calls among pipelines and composition, order representative F# APIs, and simplify a decorative pipeline.

### Chapter 14: Choosing Collections and Evaluation Models {#chapter-14}

[Chapter](../part-03/ch-14-collections-evaluation#overview) · [Solution page](../solutions/ch-14-collections-evaluation#overview)

**Answers:** [Exercise 1](../solutions/ch-14-collections-evaluation#exercise-01) · [Exercise 2](../solutions/ch-14-collections-evaluation#exercise-02) · [Exercise 3](../solutions/ch-14-collections-evaluation#exercise-03)

**Review focus:** Select collections by workload, calculate deferred demand precisely, and separate ordered keys from equality-based hash keys.

### Chapter 15: Active Patterns and Domain Matching Boundaries {#chapter-15}

[Chapter](../part-03/ch-15-active-patterns#overview) · [Solution page](../solutions/ch-15-active-patterns#overview)

**Answers:** [Exercise 1](../solutions/ch-15-active-patterns#exercise-01) · [Exercise 2](../solutions/ch-15-active-patterns#exercise-02) · [Exercise 3](../solutions/ch-15-active-patterns#exercise-03)

**Review focus:** Build total domain views, preserve parsing errors, and move database work outside active-pattern matching.

### Chapter 16: Modules, Namespaces, Projects, and Compiler Settings {#chapter-16}

[Chapter](../part-03/ch-16-modules-namespaces-projects#overview) · [Solution page](../solutions/ch-16-modules-namespaces-projects#overview)

**Answers:** [Exercise 1](../solutions/ch-16-modules-namespaces-projects#exercise-01) · [Exercise 2](../solutions/ch-16-modules-namespaces-projects#exercise-02) · [Exercise 3](../solutions/ch-16-modules-namespaces-projects#exercise-03)

**Review focus:** Order a multi-file project, repair a namespace-level binding, and propagate an explicit nullable-reference contract through a wrapper.

### Chapter 17: Signatures, Access Control, and F#-Facing APIs {#chapter-17}

[Chapter](../part-03/ch-17-signatures-encapsulation#overview) · [Solution page](../solutions/ch-17-signatures-encapsulation#overview)

**Answers:** [Exercise 1](../solutions/ch-17-signatures-encapsulation#exercise-01) · [Exercise 2](../solutions/ch-17-signatures-encapsulation#exercise-02) · [Exercise 3](../solutions/ch-17-signatures-encapsulation#exercise-03)

**Review focus:** Specify an abstract email type, narrow an inconsistent allocation surface, and align function arity and helper accessibility across a signature pair.

### Chapter 18: Explicit Workflow Composition and Validation Accumulation {#chapter-18}

[Chapter](../part-03/ch-18-workflow-validation#overview) · [Solution page](../solutions/ch-18-workflow-validation#overview)

**Answers:** [Exercise 1](../solutions/ch-18-workflow-validation#exercise-01) · [Exercise 2](../solutions/ch-18-workflow-validation#exercise-02) · [Exercise 3](../solutions/ch-18-workflow-validation#exercise-03)

**Review focus:** Separate pure, dependent, and effectful checks; implement ordered accumulation; and replace an unspecified computation expression with explicit semantics.

## Part 4 · Effects, asynchrony, and concurrency {#part-4}

### Chapter 19: .NET APIs and Null Boundaries {#chapter-19}

[Chapter](../part-04/ch-19-dotnet-null-boundaries#overview) · [Solution page](../solutions/ch-19-dotnet-null-boundaries#overview)

**Answers:** [Exercise 1](../solutions/ch-19-dotnet-null-boundaries#exercise-01) · [Exercise 2](../solutions/ch-19-dotnet-null-boundaries#exercise-02) · [Exercise 3](../solutions/ch-19-dotnet-null-boundaries#exercise-03)

**Review focus:** Classify nullable boundaries, wrap a real nullable .NET return without erasing failures, and prove why an option payload can still be null.

### Chapter 20: Functional Core and Effect Boundaries {#chapter-20}

[Chapter](../part-04/ch-20-functional-core-effects#overview) · [Solution page](../solutions/ch-20-functional-core-effects#overview)

**Answers:** [Exercise 1](../solutions/ch-20-functional-core-effects#exercise-01) · [Exercise 2](../solutions/ch-20-functional-core-effects#exercise-02) · [Exercise 3](../solutions/ch-20-functional-core-effects#exercise-03)

**Review focus:** Expose hidden runtime inputs, select the smallest honest dependency shape, and preserve expected boundary failures without flattening contract violations.

### Chapter 21: Exceptions, Resources, and I/O {#chapter-21}

[Chapter](../part-04/ch-21-exceptions-resources-io#overview) · [Solution page](../solutions/ch-21-exceptions-resources-io#overview)

**Answers:** [Exercise 1](../solutions/ch-21-exceptions-resources-io#exercise-01) · [Exercise 2](../solutions/ch-21-exceptions-resources-io#exercise-02) · [Exercise 3](../solutions/ch-21-exceptions-resources-io#exercise-03)

**Review focus:** Compose resource-safe reading with pure parsing, replace catch-all strings with structured policy, and verify two-reader disposal on success and failure.

### Chapter 22: Async<'T> and Task<'T> {#chapter-22}

[Chapter](../part-04/ch-22-async-task#overview) · [Solution page](../solutions/ch-22-async-task#overview)

**Answers:** [Exercise 1](../solutions/ch-22-async-task#exercise-01) · [Exercise 2](../solutions/ch-22-async-task#exercise-02) · [Exercise 3](../solutions/ch-22-async-task#exercise-03)

**Review focus:** Prove async and task start semantics with gates, compose a task API with an Async validator, and make single-execution ownership explicit.

### Chapter 23: Cancellation, Timeouts, Faults, and Disposal {#chapter-23}

[Chapter](../part-04/ch-23-cancellation-timeouts#overview) · [Solution page](../solutions/ch-23-cancellation-timeouts#overview)

**Answers:** [Exercise 1](../solutions/ch-23-cancellation-timeouts#exercise-01) · [Exercise 2](../solutions/ch-23-cancellation-timeouts#exercise-02) · [Exercise 3](../solutions/ch-23-cancellation-timeouts#exercise-03)

**Review focus:** Verify token propagation, implement abandon-wait and cancel-work timeout policies with signals, and test compiled asynchronous disposal.

### Chapter 24: Parallelism, Concurrency, Agents, and Controlled Mutation {#chapter-24}

[Chapter](../part-04/ch-24-concurrency-agents-state#overview) · [Solution page](../solutions/ch-24-concurrency-agents-state#overview)

**Answers:** [Exercise 1](../solutions/ch-24-concurrency-agents-state#exercise-01) · [Exercise 2](../solutions/ch-24-concurrency-agents-state#exercise-02) · [Exercise 3](../solutions/ch-24-concurrency-agents-state#exercise-03)

**Review focus:** Choose coordination from invariants, extend a reservation agent without assuming message order, and make cache invalidation and duplicate-work policy executable.

## Part 5 · .NET interop and engineering quality {#part-5}

### Chapter 25: Defining Objects in F# {#chapter-25}

[Chapter](../part-05/ch-25-objects-interfaces#overview) · [Solution page](../solutions/ch-25-objects-interfaces#overview)

**Answers:** [Exercise 1](../solutions/ch-25-objects-interfaces#exercise-01) · [Exercise 2](../solutions/ch-25-objects-interfaces#exercise-02) · [Exercise 3](../solutions/ch-25-objects-interfaces#exercise-03)

**Review focus:** Replace a ceremonial class, compare function and interface policy boundaries, and redesign a struct so its default representation is valid.

### Chapter 26: Deeper .NET Boundaries {#chapter-26}

[Chapter](../part-05/ch-26-dotnet-runtime-boundaries#overview) · [Solution page](../solutions/ch-26-dotnet-runtime-boundaries#overview)

**Answers:** [Exercise 1](../solutions/ch-26-dotnet-runtime-boundaries#exercise-01) · [Exercise 2](../solutions/ch-26-dotnet-runtime-boundaries#exercise-02) · [Exercise 3](../solutions/ch-26-dotnet-runtime-boundaries#exercise-03)

**Review focus:** Decode object input once, own an event subscription, and prove a custom dictionary comparer obeys its equality and hash contract.

### Chapter 27: Designing F# APIs for C# {#chapter-27}

[Chapter](../part-05/ch-27-fsharp-api-for-csharp#overview) · [Solution page](../solutions/ch-27-fsharp-api-for-csharp#overview)

**Answers:** [Exercise 1](../solutions/ch-27-fsharp-api-for-csharp#exercise-01) · [Exercise 2](../solutions/ch-27-fsharp-api-for-csharp#exercise-02) · [Exercise 3](../solutions/ch-27-fsharp-api-for-csharp#exercise-03)

**Review focus:** Project a leaking F# result into a controlled .NET response, evolve a query with overloads, and isolate serializer requirements in a dedicated DTO.

### Chapter 28: Example Tests, Test Doubles, and Boundary Tests {#chapter-28}

[Chapter](../part-05/ch-28-testing-boundaries#overview) · [Solution page](../solutions/ch-28-testing-boundaries#overview)

**Answers:** [Exercise 1](../solutions/ch-28-testing-boundaries#exercise-01) · [Exercise 2](../solutions/ch-28-testing-boundaries#exercise-02) · [Exercise 3](../solutions/ch-28-testing-boundaries#exercise-03)

**Review focus:** Select the smallest test level by risk, hand-write a double for a missing-product path, and design compatible evolution for an optional JSON field.

### Chapter 29: Property Testing with FsCheck {#chapter-29}

[Chapter](../part-05/ch-29-property-testing#overview) · [Solution page](../solutions/ch-29-property-testing#overview)

**Answers:** [Exercise 1](../solutions/ch-29-property-testing#exercise-01) · [Exercise 2](../solutions/ch-29-property-testing#exercise-02) · [Exercise 3](../solutions/ch-29-property-testing#exercise-03)

**Review focus:** Derive an independent streaming property, design a valid identifier generator and shrinker, and turn an order-sensitive counterexample into a durable regression example.

### Chapter 30: Diagnostics, Debugging, Formatting, and Builds {#chapter-30}

[Chapter](../part-05/ch-30-diagnostics-tooling-builds#overview) · [Solution page](../solutions/ch-30-diagnostics-tooling-builds#overview)

**Answers:** [Exercise 1](../solutions/ch-30-diagnostics-tooling-builds#exercise-01) · [Exercise 2](../solutions/ch-30-diagnostics-tooling-builds#exercise-02) · [Exercise 3](../solutions/ch-30-diagnostics-tooling-builds#exercise-03)

**Review focus:** Repair an F# file-order cascade, assign distinct questions to FSI, tests, and a debugger, and audit an intentionally changed locked dependency graph.

### Chapter 31: Measure Before Optimizing {#chapter-31}

[Chapter](../part-05/ch-31-measure-before-optimizing#overview) · [Solution page](../solutions/ch-31-measure-before-optimizing#overview)

**Answers:** [Exercise 1](../solutions/ch-31-measure-before-optimizing#exercise-01) · [Exercise 2](../solutions/ch-31-measure-before-optimizing#exercise-02) · [Exercise 3](../solutions/ch-31-measure-before-optimizing#exercise-03)

**Review focus:** Bound conclusions to the captured benchmark, design a behavior-preserving option versus voption allocation experiment, and choose evidence for three different system symptoms.

### Chapter 32: From Functions to Applications {#chapter-32}

[Chapter](../part-05/ch-32-functions-to-applications#overview) · [Solution page](../solutions/ch-32-functions-to-applications#overview)

**Answers:** [Exercise 1](../solutions/ch-32-functions-to-applications#exercise-01) · [Exercise 2](../solutions/ch-32-functions-to-applications#exercise-02) · [Exercise 3](../solutions/ch-32-functions-to-applications#exercise-03)

**Review focus:** Derive narrow dispatch ports and ownership, design bounded observable signals, and choose an application host from concrete lifecycle requirements.

## Part 6 · The booking system {#part-6}

### Chapter 33: Business Language, Commands, Events, and Model {#chapter-33}

[Chapter](../part-06/ch-33-domain-language-model#overview) · [Solution page](../solutions/ch-33-domain-language-model#overview)

**Answers:** [Exercise 1](../solutions/ch-33-domain-language-model#exercise-01) · [Exercise 2](../solutions/ch-33-domain-language-model#exercise-02) · [Exercise 3](../solutions/ch-33-domain-language-model#exercise-03)

**Review focus:** Classify booking values by role, design a seat-change command and fact without crossing boundaries, and choose persistence from stated guarantees.

### Chapter 34: The Pure Booking Workflow and Validation {#chapter-34}

[Chapter](../part-06/ch-34-pure-booking-workflow#overview) · [Solution page](../solutions/ch-34-pure-booking-workflow#overview)

**Answers:** [Exercise 1](../solutions/ch-34-pure-booking-workflow#exercise-01) · [Exercise 2](../solutions/ch-34-pure-booking-workflow#exercise-02) · [Exercise 3](../solutions/ch-34-pure-booking-workflow#exercise-03)

**Review focus:** Trace booking error precedence, extend independent validation to three fields, and compare cancellation precedence policies.

### Chapter 35: Ports, Persistence, Configuration, and Stubs {#chapter-35}

[Chapter](../part-06/ch-35-ports-persistence-config#overview) · [Solution page](../solutions/ch-35-ports-persistence-config#overview)

**Answers:** [Exercise 1](../solutions/ch-35-ports-persistence-config#exercise-01) · [Exercise 2](../solutions/ch-35-ports-persistence-config#exercise-02) · [Exercise 3](../solutions/ch-35-ports-persistence-config#exercise-03)

**Review focus:** Evolve a versioned snapshot, audit interruption points in replacement, and redesign composition for borrowed production clients.

### Chapter 36: Web API, JSON, and Input Boundaries {#chapter-36}

[Chapter](../part-06/ch-36-web-api-boundaries#overview) · [Solution page](../solutions/ch-36-web-api-boundaries#overview)

**Answers:** [Exercise 1](../solutions/ch-36-web-api-boundaries#exercise-01) · [Exercise 2](../solutions/ch-36-web-api-boundaries#exercise-02) · [Exercise 3](../solutions/ch-36-web-api-boundaries#exercise-03)

**Review focus:** Preserve an HTTP contract under automatic binding, reason about ambiguous effects, and assign security controls across deployment topologies.

### Chapter 37: Consistency, Idempotency, Retries, and Partial Failure {#chapter-37}

[Chapter](../part-06/ch-37-consistency-idempotency#overview) · [Solution page](../solutions/ch-37-consistency-idempotency#overview)

**Answers:** [Exercise 1](../solutions/ch-37-consistency-idempotency#exercise-01) · [Exercise 2](../solutions/ch-37-consistency-idempotency#exercise-02) · [Exercise 3](../solutions/ch-37-consistency-idempotency#exercise-03)

**Review focus:** Move capacity control across processes, reconcile ambiguous payments, and design an outbox without claiming exactly-once delivery.

### Chapter 38: Integration, Diagnostics, C# Client, and Release Evidence {#chapter-38}

[Chapter](../part-06/ch-38-integration-diagnostics-release#overview) · [Solution page](../solutions/ch-38-integration-diagnostics-release#overview)

**Answers:** [Exercise 1](../solutions/ch-38-integration-diagnostics-release#exercise-01) · [Exercise 2](../solutions/ch-38-integration-diagnostics-release#exercise-02) · [Exercise 3](../solutions/ch-38-integration-diagnostics-release#exercise-03)

**Review focus:** Audit inflated guarantees, design bounded telemetry collection, and turn the local booking check into a concrete release plan.

## Part 7 · The ecosystem map {#part-7}

### Chapter 39: ASP.NET Core and the F# Web Ecosystem {#chapter-39}

[Chapter](../part-07/ch-39-web-ecosystem#overview) · [Solution page](../solutions/ch-39-web-ecosystem#overview)

**Answers:** [Exercise 1](../solutions/ch-39-web-ecosystem#exercise-01) · [Exercise 2](../solutions/ch-39-web-ecosystem#exercise-02) · [Exercise 3](../solutions/ch-39-web-ecosystem#exercise-03)

**Review focus:** Choose web surfaces for concrete teams, design a contract-preserving Falco spike, and migrate framework-bound endpoints reversibly.

### Chapter 40: Data, Type Providers, Analytics, and Machine Learning {#chapter-40}

[Chapter](../part-07/ch-40-data-analytics#overview) · [Solution page](../solutions/ch-40-data-analytics#overview)

**Answers:** [Exercise 1](../solutions/ch-40-data-analytics#exercise-01) · [Exercise 2](../solutions/ch-40-data-analytics#exercise-02) · [Exercise 3](../solutions/ch-40-data-analytics#exercise-03)

**Review focus:** Choose bounded data tools, absorb CSV schema drift explicitly, and turn an exploratory classifier into reproducible training and inference systems.

### Chapter 41: Fable, Elmish, and Browser Applications {#chapter-41}

[Chapter](../part-07/ch-41-fable-elmish#overview) · [Solution page](../solutions/ch-41-fable-elmish#overview)

**Answers:** [Exercise 1](../solutions/ch-41-fable-elmish#exercise-01) · [Exercise 2](../solutions/ch-41-fable-elmish#exercise-02) · [Exercise 3](../solutions/ch-41-fable-elmish#exercise-03)

**Review focus:** Choose proportional browser architectures, reject stale asynchronous results, and split a shared pricing library across honest runtime boundaries.

### Chapter 42: Cloud, Containers, Serverless, and .NET Aspire {#chapter-42}

[Chapter](../part-07/ch-42-cloud-containers-aspire#overview) · [Solution page](../solutions/ch-42-cloud-containers-aspire#overview)

**Answers:** [Exercise 1](../solutions/ch-42-cloud-containers-aspire#exercise-01) · [Exercise 2](../solutions/ch-42-cloud-containers-aspire#exercise-02) · [Exercise 3](../solutions/ch-42-cloud-containers-aspire#exercise-03)

**Review focus:** Choose proportional compute models, turn the local cloud sample into a release proposal, and design an idempotent event consumer with honest unknown outcomes.

### Chapter 43: Avalonia, Desktop, and Mobile {#chapter-43}

[Chapter](../part-07/ch-43-avalonia-desktop-mobile#overview) · [Solution page](../solutions/ch-43-avalonia-desktop-mobile#overview)

**Answers:** [Exercise 1](../solutions/ch-43-avalonia-desktop-mobile#exercise-01) · [Exercise 2](../solutions/ch-43-avalonia-desktop-mobile#exercise-02) · [Exercise 3](../solutions/ch-43-avalonia-desktop-mobile#exercise-03)

**Review focus:** Choose proportional UI boundaries, turn the verified Avalonia slice into a desktop release plan, and design an honest mobile project and evidence graph.

### Chapter 44: Unity 6.3 LTS and F# {#chapter-44}

[Chapter](../part-07/ch-44-unity#overview) · [Solution page](../solutions/ch-44-unity#overview)

**Answers:** [Exercise 1](../solutions/ch-44-unity#exercise-01) · [Exercise 2](../solutions/ch-44-unity#exercise-02) · [Exercise 3](../solutions/ch-44-unity#exercise-03)

**Review focus:** Choose proportional F#/C# Unity boundaries, promote the managed plug-in sample through an honest IL2CPP evidence plan, and design versioned quest data without hiding AOT risk.

### Chapter 45: Scripting, Automation, Packages, and What Comes Next {#chapter-45}

[Chapter](../part-07/ch-45-scripting-packages-next#overview) · [Solution page](../solutions/ch-45-scripting-packages-next#overview)

**Answers:** [Exercise 1](../solutions/ch-45-scripting-packages-next#exercise-01) · [Exercise 2](../solutions/ch-45-scripting-packages-next#exercise-02) · [Exercise 3](../solutions/ch-45-scripting-packages-next#exercise-03)

**Review focus:** Extend deterministic artifact automation, evaluate current command-line packages without overstating evidence, and turn the book into a twelve-week F# delivery loop.

## Final self-review {#final-review}

- Can you explain the inferred or public types without relying on the solution text?
- Did you preserve ordering, evaluation, ownership, failure, cancellation, and compatibility requirements?
- Which evidence actually ran, and which claim is only a reviewed or proposed boundary?
- What counterexample would distinguish your design from a superficially similar but incorrect one?
- If your answer differs, can a reviewer see the tradeoff and the condition under which you would choose the book’s version instead?
