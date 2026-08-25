---
title: "Appendix F: F# Glossary"
description: "A self-contained glossary with links to each term’s first teaching chapter."
translationKey: glossary
---

# Appendix F: F# Glossary {#overview}

This glossary defines the F# vocabulary used in this edition.

“First introduced” points to the earliest chapter that teaches the concept directly. Follow the link for motivation, examples, and surrounding ideas.

## How to use this glossary {#how-to-use}

Search for a term, follow a direct link, or read by part to revisit concepts in their original learning order. Definitions describe this book’s usage; the linked chapter supplies the operational detail.

## Part 1 · Foundations: values, functions, and flow {#part-1}

### expression {#expression}

A piece of code that is evaluated and, when it completes normally, produces a value.

**First introduced:** [Chapter 1: A First F# Session](./part-01/ch-01-first-session#overview)

### F# Interactive {#fsharp-interactive}

The F# interactive environment included with the .NET SDK; it runs submissions in a read-evaluate-print loop and can also execute F# scripts.

**First introduced:** [Chapter 1: A First F# Session](./part-01/ch-01-first-session#overview)

### F# script {#fsharp-script}

A source file with the .fsx extension, normally executed directly by F# Interactive for experiments, automation, and small tools.

**First introduced:** [Chapter 1: A First F# Session](./part-01/ch-01-first-session#overview)

### literal {#literal}

A representation of a value written directly in source code, such as 40, true, "hello", or 1.5m.

**First introduced:** [Chapter 1: A First F# Session](./part-01/ch-01-first-session#overview)

### unit {#unit}

A type with exactly one value, (), used when an expression has no specific result to pass to a later computation.

**First introduced:** [Chapter 1: A First F# Session](./part-01/ch-01-first-session#overview)

### value {#value}

A result produced when evaluation completes normally and available to other expressions; a function is itself a value.

**First introduced:** [Chapter 1: A First F# Session](./part-01/ch-01-first-session#overview)

### binding {#binding}

An association, introduced by a pattern such as let, between a name and a value; it is not an implicitly rewritable storage slot.

**First introduced:** [Chapter 2: Values, Bindings, and Expressions](./part-01/ch-02-values-bindings-expressions#overview)

### immutability {#immutability}

The property of retaining an established value. A binding keeps its name-to-value association, while a referenced object's internals follow their own mutability contract.

**First introduced:** [Chapter 2: Values, Bindings, and Expressions](./part-01/ch-02-values-bindings-expressions#overview)

### numeric conversion {#numeric-conversion}

Explicitly producing a value of one numeric type from another, such as using decimal to obtain a decimal from an int.

**First introduced:** [Chapter 2: Values, Bindings, and Expressions](./part-01/ch-02-values-bindings-expressions#overview)

### shadowing {#shadowing}

Introducing a new binding with the same name in an inner or later scope so that the old binding can no longer be reached by that name there; it does not mutate the old value.

**First introduced:** [Chapter 2: Values, Bindings, and Expressions](./part-01/ch-02-values-bindings-expressions#overview)

### type annotation {#type-annotation}

An explicitly written type constraint used to record intent or supply context the compiler cannot infer reliably.

**First introduced:** [Chapter 2: Values, Bindings, and Expressions](./part-01/ch-02-values-bindings-expressions#overview)

### type inference {#type-inference}

The compiler's deduction of static types from how expressions are used and from their context, without requiring annotations everywhere.

**First introduced:** [Chapter 2: Values, Bindings, and Expressions](./part-01/ch-02-values-bindings-expressions#overview)

### anonymous function {#anonymous-function}

A function value created directly with a fun parameter -> body expression without first naming the function itself.

**First introduced:** [Chapter 3: Functions Are Values](./part-01/ch-03-functions-as-values#overview)

### argument {#argument}

A value or expression actually supplied for a parameter when a function is applied.

**First introduced:** [Chapter 3: Functions Are Values](./part-01/ch-03-functions-as-values#overview)

### automatic generalization {#automatic-generalization}

The compiler's promotion of inferred unknown types to type parameters that can be instantiated with multiple types when doing so is safe and the definition does not depend on one concrete type.

**First introduced:** [Chapter 3: Functions Are Values](./part-01/ch-03-functions-as-values#overview)

### closure {#closure}

A function value together with surrounding values captured at its definition site and retained for later calls.

**First introduced:** [Chapter 3: Functions Are Values](./part-01/ch-03-functions-as-values#overview)

### currying {#currying}

Representing a multi-parameter computation as successive single-parameter functions that return the next function; this is the usual form of F# let-bound functions.

**First introduced:** [Chapter 3: Functions Are Values](./part-01/ch-03-functions-as-values#overview)

### function {#function}

A value that accepts input and computes a result; in F#, functions can be bound, passed, and returned like other values.

**First introduced:** [Chapter 3: Functions Are Values](./part-01/ch-03-functions-as-values#overview)

### function application {#function-application}

Supplying an argument to a function value and evaluating its body to produce a result.

**First introduced:** [Chapter 3: Functions Are Values](./part-01/ch-03-functions-as-values#overview)

### higher-order function {#higher-order-function}

A function that accepts at least one function value as a parameter or returns a function value as its result.

**First introduced:** [Chapter 3: Functions Are Values](./part-01/ch-03-functions-as-values#overview)

### parameter {#parameter}

A name or pattern in a function definition that receives an argument supplied by a caller.

**First introduced:** [Chapter 3: Functions Are Values](./part-01/ch-03-functions-as-values#overview)

### partial application {#partial-application}

Supplying only some arguments to a curried function to obtain a new function that awaits the remaining arguments.

**First introduced:** [Chapter 3: Functions Are Values](./part-01/ch-03-functions-as-values#overview)

### tuple {#tuple}

A type that combines a fixed number of positional values, whose component types may differ and are joined by an asterisk in a type signature.

**First introduced:** [Chapter 3: Functions Are Values](./part-01/ch-03-functions-as-values#overview)

### exhaustiveness {#exhaustiveness}

The property that a set of patterns covers every possible shape of the input type; without it, some input may have no matching branch.

**First introduced:** [Chapter 4: Branching and Basic Patterns](./part-01/ch-04-branching-patterns#overview)

### guard {#guard}

A Boolean when condition evaluated only after its pattern initially matches; when false, matching continues with later rules.

**First introduced:** [Chapter 4: Branching and Basic Patterns](./part-01/ch-04-branching-patterns#overview)

### list {#list}

An ordered immutable singly linked collection of elements of one type; [] is empty, and :: constructs or decomposes a head and tail.

**First introduced:** [Chapter 4: Branching and Basic Patterns](./part-01/ch-04-branching-patterns#overview)

### pattern {#pattern}

A shape rule used to test input structure, decompose components, and optionally bind local names to them.

**First introduced:** [Chapter 4: Branching and Basic Patterns](./part-01/ch-04-branching-patterns#overview)

### pattern matching {#pattern-matching}

Selecting a branch by the shape of a value while optionally binding names to its constituent parts.

**First introduced:** [Chapter 4: Branching and Basic Patterns](./part-01/ch-04-branching-patterns#overview)

### wildcard pattern {#wildcard-pattern}

The _ pattern, which matches any input without binding an available name to that input.

**First introduced:** [Chapter 4: Branching and Basic Patterns](./part-01/ch-04-branching-patterns#overview)

### eager evaluation {#eager-evaluation}

Computing a result when an operation is called rather than delaying work until later enumeration or demand.

**First introduced:** [Chapter 5: Lists, Pipelines, and Data Flow](./part-01/ch-05-lists-pipelines#overview)

### effect {#effect}

Observable behavior during evaluation that is not described by the return value alone, such as output, file writes, or state changes.

**First introduced:** [Chapter 5: Lists, Pipelines, and Data Flow](./part-01/ch-05-lists-pipelines#overview)

### mutable binding {#mutable-binding}

A binding introduced with let mutable whose storage location can subsequently be updated with <-.

**First introduced:** [Chapter 5: Lists, Pipelines, and Data Flow](./part-01/ch-05-lists-pipelines#overview)

### option {#option}

An F# type representing a present value as Some value and absence as None; Chapter 9 develops its full modeling rules.

**First introduced:** [Chapter 5: Lists, Pipelines, and Data Flow](./part-01/ch-05-lists-pipelines#overview)

### pipeline {#pipeline}

Using |> to pass the result on the left as the final argument of the function application on the right, expressing transformations in data-flow order.

**First introduced:** [Chapter 5: Lists, Pipelines, and Data Flow](./part-01/ch-05-lists-pipelines#overview)

### accumulator {#accumulator}

A value threaded into the next recursive or folding step that represents the result completed so far.

**First introduced:** [Chapter 6: Recursion, Tail Calls, and Folds](./part-01/ch-06-recursion-folds#overview)

### fold {#fold}

A higher-order operation that combines collection elements into an accumulator in a defined order and returns the final state.

**First introduced:** [Chapter 6: Recursion, Tail Calls, and Folds](./part-01/ch-06-recursion-folds#overview)

### recursion {#recursion}

A function calling itself directly or indirectly to continue with a smaller problem or one closer to a termination condition.

**First introduced:** [Chapter 6: Recursion, Tail Calls, and Folds](./part-01/ch-06-recursion-folds#overview)

### structural recursion {#structural-recursion}

Recursion that branches on a data type's construction shape and recursively processes a structurally smaller component.

**First introduced:** [Chapter 6: Recursion, Tail Calls, and Folds](./part-01/ch-06-recursion-folds#overview)

### tail call {#tail-call}

A call made as the last operation before a function branch returns, whose result needs no further processing by the current stack frame.

**First introduced:** [Chapter 6: Recursion, Tail Calls, and Folds](./part-01/ch-06-recursion-folds#overview)

### tail recursion {#tail-recursion}

Recursion whose recursive paths place the recursive call in tail position, allowing the compiler an opportunity to eliminate recursive stack growth.

**First introduced:** [Chapter 6: Recursion, Tail Calls, and Folds](./part-01/ch-06-recursion-folds#overview)

## Part 2 · Modeling with types {#part-2}

### anonymous record {#anonymous-record}

A record value whose exact shape is determined by its field labels and types without a separately declared type name.

**First introduced:** [Chapter 7: Records, Updates, Equality, and Comparison](./part-02/ch-07-records-equality#overview)

### hash code {#hash-code}

An integer summary derived consistently with equality to locate candidates in hash-based structures; unequal values may still share one code.

**First introduced:** [Chapter 7: Records, Updates, Equality, and Comparison](./part-02/ch-07-records-equality#overview)

### record {#record}

A product type made of named fields; an ordinary F# record is immutable by default and automatically supports structural equality and comparison when its components do.

**First introduced:** [Chapter 7: Records, Updates, Equality, and Comparison](./part-02/ch-07-records-equality#overview)

### reference identity {#reference-identity}

The relation of two references pointing to the same runtime object, separate from whether their contents are structurally equal.

**First introduced:** [Chapter 7: Records, Updates, Equality, and Comparison](./part-02/ch-07-records-equality#overview)

### structural comparison {#structural-comparison}

An ordering obtained by recursively comparing a composite value's components in a defined order, requiring each relevant component type to support comparison.

**First introduced:** [Chapter 7: Records, Updates, Equality, and Comparison](./part-02/ch-07-records-equality#overview)

### structural equality {#structural-equality}

Equality determined by recursively comparing corresponding components of composite values rather than checking whether they are the same object.

**First introduced:** [Chapter 7: Records, Updates, Equality, and Comparison](./part-02/ch-07-records-equality#overview)

### discriminated union {#discriminated-union}

A type made of named cases; each value belongs to exactly one case, and a case may carry additional data.

**First introduced:** [Chapter 8: Discriminated Unions and State Modeling](./part-02/ch-08-discriminated-unions#overview)

### union case {#union-case}

One named possible shape of a discriminated union, carrying either no data or fields meaningful only for that shape.

**First introduced:** [Chapter 8: Discriminated Unions and State Modeling](./part-02/ch-08-discriminated-unions#overview)

### Result {#result}

An F# type representing success as Ok value and an expected failure with a modeled reason as Error error.

**First introduced:** [Chapter 9: Absence and Expected Failure](./part-02/ch-09-option-result#overview)

### short-circuit {#short-circuit}

Stopping a composition at a None or Error that cannot continue, preserving that result without running later dependent steps.

**First introduced:** [Chapter 9: Absence and Expected Failure](./part-02/ch-09-option-result#overview)

### recursive type {#recursive-type}

A type that refers to itself in part of its own definition, allowing values with finite nested structure.

**First introduced:** [Chapter 10: Recursive Types and Structural Recursion](./part-02/ch-10-recursive-types#overview)

### comparison constraint {#comparison-constraint}

The 'T : comparison requirement that a type parameter support F# generic comparison and ordering operations.

**First introduced:** [Chapter 11: Generics, Constraints, and Units](./part-02/ch-11-generics-constraints#overview)

### equality constraint {#equality-constraint}

The 'T : equality requirement that a type parameter support F# generic equality operations.

**First introduced:** [Chapter 11: Generics, Constraints, and Units](./part-02/ch-11-generics-constraints#overview)

### generic type parameter {#generic-type-parameter}

A type-level parameter representing an as-yet unspecified type within a definition and replaced consistently by a type argument at each concrete use.

**First introduced:** [Chapter 11: Generics, Constraints, and Units](./part-02/ch-11-generics-constraints#overview)

### statically resolved type parameter {#statically-resolved-type-parameter}

An F# type parameter written ^T, resolved at an inline call site, and able to carry member constraints; it differs from an ordinary 'T generic parameter.

**First introduced:** [Chapter 11: Generics, Constraints, and Units](./part-02/ch-11-generics-constraints#overview)

### unit of measure {#unit-of-measure}

A compile-time type annotation attached to supported numeric types to check dimensional relationships statically and erased at runtime.

**First introduced:** [Chapter 11: Generics, Constraints, and Units](./part-02/ch-11-generics-constraints#overview)

### value restriction {#value-restriction}

The restriction of automatic generalization to safe binding shapes, rejecting nongeneralizable values with unresolved type variables so one storage location cannot be used unsafely at multiple types.

**First introduced:** [Chapter 11: Generics, Constraints, and Units](./part-02/ch-11-generics-constraints#overview)

### access control {#access-control}

The mechanism for specifying which code locations may use a program entity through public, internal, private, or a signature file.

**First introduced:** [Chapter 12: Making Illegal States Unrepresentable](./part-02/ch-12-making-illegal-states-unrepresentable#overview)

### invariant {#invariant}

A condition intended to remain true for every publicly obtainable value of a protected type.

**First introduced:** [Chapter 12: Making Illegal States Unrepresentable](./part-02/ch-12-making-illegal-states-unrepresentable#overview)

### private representation {#private-representation}

A design in which callers can use a type but cannot directly use its underlying union cases, record construction, or other representation details.

**First introduced:** [Chapter 12: Making Illegal States Unrepresentable](./part-02/ch-12-making-illegal-states-unrepresentable#overview)

### signature file {#signature-file}

An F# .fsi file placed before its corresponding .fs implementation that declares the public surface visible to other files.

**First introduced:** [Chapter 12: Making Illegal States Unrepresentable](./part-02/ch-12-making-illegal-states-unrepresentable#overview)

### smart constructor {#smart-constructor}

A function that validates or normalizes input before producing a protected domain value and reports rejection through an explicit return type.

**First introduced:** [Chapter 12: Making Illegal States Unrepresentable](./part-02/ch-12-making-illegal-states-unrepresentable#overview)

## Part 3 · Composition and program structure {#part-3}

### function composition {#function-composition}

Connecting one function's output to the next function's input to obtain a new function value from multiple function values.

**First introduced:** [Chapter 13: Composition, Argument Order, and Pipeline APIs](./part-03/ch-13-composition-pipeline-api#overview)

### array {#array}

An ordered same-type collection with fixed length, contiguous storage, and elements that can be updated in place; changing length requires a new array.

**First introduced:** [Chapter 14: Choosing Collections and Evaluation Models](./part-03/ch-14-collections-evaluation#overview)

### deferred evaluation {#deferred-evaluation}

Delaying value production or work until a consumer requests results; whether work repeats depends on the source and on caching.

**First introduced:** [Chapter 14: Choosing Collections and Evaluation Models](./part-03/ch-14-collections-evaluation#overview)

### enumeration {#enumeration}

A traversal in which a consumer requests collection elements through an enumerator; the concrete source determines the work performed by each enumeration.

**First introduced:** [Chapter 14: Choosing Collections and Evaluation Models](./part-03/ch-14-collections-evaluation#overview)

### map {#map}

An immutable tree of key-value bindings organized by F# generic comparison of keys; each key has at most one value.

**First introduced:** [Chapter 14: Choosing Collections and Evaluation Models](./part-03/ch-14-collections-evaluation#overview)

### sequence {#sequence}

`seq<'T>` is a type abbreviation for `IEnumerable<'T>` that describes how to enumerate same-type elements but does not itself guarantee caching, purity, or repeatable traversal.

**First introduced:** [Chapter 14: Choosing Collections and Evaluation Models](./part-03/ch-14-collections-evaluation#overview)

### set {#set}

An immutable tree of unique elements organized by F# generic comparison of the elements.

**First introduced:** [Chapter 14: Choosing Collections and Evaluation Models](./part-03/ch-14-collections-evaluation#overview)

### active pattern {#active-pattern}

A function-backed view of an input used as a named pattern to classify or decompose a value during matching.

**First introduced:** [Chapter 15: Active Patterns and Domain Matching Boundaries](./part-03/ch-15-active-patterns#overview)

### complete active pattern {#complete-active-pattern}

An active pattern that returns a named case for every input; its multi-case form partitions the whole input space.

**First introduced:** [Chapter 15: Active Patterns and Domain Matching Boundaries](./part-03/ch-15-active-patterns#overview)

### parameterized active pattern {#parameterized-active-pattern}

A single-case active pattern that accepts extra arguments before the final matched input to specialize recognition at the use site.

**First introduced:** [Chapter 15: Active Patterns and Domain Matching Boundaries](./part-03/ch-15-active-patterns#overview)

### partial active pattern {#partial-active-pattern}

A single-case active pattern that recognizes only part of the input space and may fail to match, with a wildcard case ending its name list.

**First introduced:** [Chapter 15: Active Patterns and Domain Matching Boundaries](./part-03/ch-15-active-patterns#overview)

### assembly {#assembly}

A .NET-compiled .dll or .exe, together with its metadata and code, used as a unit of deployment, loading, and reference.

**First introduced:** [Chapter 16: Modules, Namespaces, Projects, and Compiler Settings](./part-03/ch-16-modules-namespaces-projects#overview)

### compilation order {#compilation-order}

The sequence in which F# source files are supplied to the compiler; later files can ordinarily use earlier definitions, but not the reverse.

**First introduced:** [Chapter 16: Modules, Namespaces, Projects, and Compiler Settings](./part-03/ch-16-modules-namespaces-projects#overview)

### module {#module}

An F# construct that groups related types, values, and functions in one named scope and can itself live in a namespace or another module.

**First introduced:** [Chapter 16: Modules, Namespaces, Projects, and Compiler Settings](./part-03/ch-16-modules-namespaces-projects#overview)

### namespace {#namespace}

A named container that can organize types and modules across files and assemblies but cannot directly contain F# values or functions.

**First introduced:** [Chapter 16: Modules, Namespaces, Projects, and Compiler Settings](./part-03/ch-16-modules-namespaces-projects#overview)

### nullable reference type {#nullable-reference-type}

With F# null checking enabled, a `T | null` reference-type annotation that explicitly permits null; it is a compile-time contract, not a runtime wrapper.

**First introduced:** [Chapter 16: Modules, Namespaces, Projects, and Compiler Settings](./part-03/ch-16-modules-namespaces-projects#overview)

### open declaration {#open-declaration}

A declaration that makes names from a namespace or module available by shorter references in the following scope without loading code or changing accessibility.

**First introduced:** [Chapter 16: Modules, Namespaces, Projects, and Compiler Settings](./part-03/ch-16-modules-namespaces-projects#overview)

### project file {#project-file}

An MSBuild XML file describing target framework, compile-item order, references, and build properties; an F# project normally uses the .fsproj extension.

**First introduced:** [Chapter 16: Modules, Namespaces, Projects, and Compiler Settings](./part-03/ch-16-modules-namespaces-projects#overview)

### abstract representation {#abstract-representation}

A signature exposes a type name while omitting its union cases, record fields, or other implementation shape, so consumers can use values of the type without depending on its representation.

**First introduced:** [Chapter 17: Signatures, Access Control, and F#-Facing APIs](./part-03/ch-17-signatures-encapsulation#overview)

### public API surface {#public-api-surface}

The set of types, cases, functions, members, and signatures that a component intentionally exposes and commits to supporting for consumers.

**First introduced:** [Chapter 17: Signatures, Access Control, and F#-Facing APIs](./part-03/ch-17-signatures-encapsulation#overview)

### computation expression {#computation-expression}

F# syntax interpreted through builder members to compose computations with a particular context or control flow.

**First introduced:** [Chapter 18: Explicit Workflow Composition and Validation Accumulation](./part-03/ch-18-workflow-validation#overview)

### validation accumulation {#validation-accumulation}

A combination strategy that evaluates independent checks and merges their failures in an explicit order into one error collection.

**First introduced:** [Chapter 18: Explicit Workflow Composition and Validation Accumulation](./part-03/ch-18-workflow-validation#overview)
