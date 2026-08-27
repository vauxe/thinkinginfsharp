---
title: "Appendix E: Common Compiler Diagnostic Index"
description: "Diagnose frequent F# 10 compiler messages from observed evidence, root-cause questions, and the smallest semantically correct repair."
translationKey: appendices/e-compiler-errors
---

# Appendix E: Common Compiler Diagnostic Index {#overview}

A compiler code identifies a diagnostic category, not a unique root cause. `FS0039` can mean a typo, a scope error, a missing reference, or the wrong F# file order. `FS0001` can appear where two type constraints finally contradict each other even though an earlier expression introduced the decisive constraint.

This index is a triage aid for F# 10 under .NET SDK 10.0.301, not a replacement for the complete message. Every listed code was produced by that locked compiler on 2026-08-25. Diagnostic wording, locations, severity defaults, and occasionally codes can evolve; reproduce a problem with the project's selected SDK before searching or changing code.

## Read a diagnostic in thirty seconds {#thirty-seconds}

1. Preserve the **first relevant** diagnostic, including path, line, column, severity, code, and full message.
2. Re-run the narrowest real command: FSI for an isolated expression, or the actual project build for files, references, generated code, and compiler settings.
3. Inspect the inferred or declared types on both sides of the reported point. The highlighted token is where the compiler proved a conflict, not necessarily where the mistaken assumption began.
4. Fix one root cause, then rebuild. Later messages may be cascades from a missing type, delimiter, or provider file.
5. When reducing the reproduction, preserve the SDK, language and nullability settings, references, file order, and diagnostic code.
6. Turn a useful negative case into an expected-error fixture that requires failure with the intended code; never make it pass by suppressing the lesson.

A typical line has this anatomy:

```text
path/File.fs(12,9): error FS0039: The value or constructor 'name' is not defined.
```

Warnings retain their `FS` code when `--warnaserror+` or `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` promotes them to build failures. A formatter can normalize valid syntax; it cannot decide the intended program or repair invalid syntax.

## Fast index {#fast-index}

| Code | Immediate evidence | First root-cause question | Usual repair direction |
|---|---|---|---|
| `FS0001` | two types or constraints do not unify | Where was either side first fixed to that type? | correct the model/value, application shape, branch, or intentional annotation |
| `FS0010` | unexpected token in the current grammar context | Is a delimiter, keyword, operator, or preceding expression incomplete? | repair the smallest syntactic construct, then reparse |
| `FS0025` | pattern match is not exhaustive | Which valid input state is unhandled? | add its real rule; use `_` only when all remaining states deliberately agree |
| `FS0027` | `<-` targets a non-mutable value | Is mutation actually the intended state transition? | derive a new value, or mark narrowly owned state `mutable` deliberately |
| `FS0030` | a value retains a weak generic variable | Is this one stored value, a generic transformation, or a fresh-value factory? | choose a concrete type, data parameter, or `unit ->` factory by lifetime |
| `FS0039` | a name, type, namespace, or module is unavailable | Is it misspelled, out of scope, unreferenced, or compiled later? | qualify/open correctly, add the reference, or reorder provider before consumer |
| `FS0041` | overload resolution has multiple candidates | What argument or result type does the domain require? | annotate the argument/result or call through a typed function |
| `FS0058` | a token is offside or indentation is structurally invalid | Which enclosing construct establishes the indentation context? | complete and consistently indent that construct |
| `FS0072` | member lookup occurs before the receiver type is known | What is the receiver contract at this function boundary? | annotate the receiver or expose an operation instead of an unknown member |
| `FS0748` | `return` appears outside a computation expression | Is this an ordinary function or a builder-controlled workflow? | use the final expression, or enter the intended computation expression |
| `FS0764` | record construction omits a required field | Is this a new complete value or an update of an existing one? | supply all fields, or use record copy-and-update intentionally |
| `FS0800` | code tries to construct a value through a hidden representation | Does an `.fsi` or access modifier hide the representation? | call the public constructor/smart constructor; do not pierce abstraction |
| `FS3261` | nullable analysis rejects null for a non-null type | Is null truly permitted at this boundary? | declare `| null` and narrow/guard, or remove the invalid null |

Start with the code and full message, then use the sections below. Do not add a cast, wildcard, `mutable`, `#nowarn`, or nullability opt-out merely because it silences the line.

## Syntax and indentation: FS0010 and FS0058 {#syntax-indentation}

`FS0010` means the parser encountered a symbol or keyword that cannot continue the current construct. The offending token may only expose a missing `)`, `]`, `}`, `then`, `with`, `=`, or expression immediately before it. Match delimiters and inspect the smallest enclosing binding before changing indentation globally.

`FS0058` is tied to F#'s offside rule. In the locked probe, an unfinished `let answer =` reached end of input and produced `FS0058`, while `let answer = )` produced `FS0010`. Similar-looking broken source can therefore receive different codes depending on the first parser fact available.

Repair the structure, run Fantomas after it parses, and keep strict indentation. Disabling strict indentation to preserve ambiguous layout changes the language contract and is not a general fix.

## Type equations: FS0001 {#fs0001}

F# inference collects equations such as “this branch returns `int`” and “that argument must be `string`.” `FS0001` means the equations cannot all hold. The current compiler reproduced it from a value annotated `int` but initialized by a string.

Common sources include:

- `if` or `match` branches returning incompatible types;
- a tupled call supplied as curried arguments, or the reverse;
- partial application returning a function where a value was expected;
- an earlier numeric/string operation that fixed a previously generic parameter;
- equality, comparison, units-of-measure, or other constraints unavailable on a type;
- confusing `unit` with a calculated result.

Read the expected and actual types literally. Follow repeated type variables through the whole signature, and inspect the earliest use that constrained them. If the expected type is the real contract, fix the value; if the value is correct, fix the contract. A conversion is appropriate only when conversion is part of the domain meaning.

## Overloads and member lookup: FS0041 and FS0072 {#overloads-members}

`FS0041` says the compiler knows several applicable overloads but lacks enough type information to choose one. Binding `System.Math.Abs` without an argument reproduced candidates for `int`, `float`, `decimal`, and other numeric types. Add the domain's argument or result type near the call; do not pick an arbitrary overload just to compile.

`FS0072` appeared when a parameter of unknown type was immediately used through `.Length`. F# generally infers left to right and cannot invent a structural “anything with Length” contract. Annotate the parameter as `string`, an array, a collection interface, or the actual domain type. If several unrelated types need the operation, accept a projection function or a meaningful interface instead of relying on member-name coincidence.

## Binding lifetime and mutation: FS0027 and FS0030 {#bindings}

`FS0027` is direct evidence that `<-` targets an immutable binding. First ask whether the intended operation is transformation: `let updated = ...` and record copy-and-update often express it better. If a loop, buffer, cache, or interop API genuinely owns changing state, make only that narrow binding mutable.

`FS0030` is subtler. This minimal example creates one mutable array value whose element type remains weakly generic:

```fsharp:line-numbers [ch11-value-restriction.fsx — expected error]
let ambiguousBuckets = Array.create 2 []
```
Choose among three repairs by semantics:

| Intent | Repair | Consequence |
|---|---|---|
| one shared value of one element type | add a concrete annotation or constraining use | all readers share that storage and type |
| one generic transformation | expose the data as a normal parameter | no ambiguous stored generic value exists |
| fresh storage per request | add a `unit` parameter and construct inside | each call allocates a distinct value |

Adding `()` is not punctuation; it changes lifetime and allocation. [Chapter 11](../part-02/ch-11-generics-constraints) develops this distinction.

## Patterns and records: FS0025 and FS0764 {#patterns-records}

`FS0025` reports a valid value not covered by a pattern match. With warnings treated as errors, adding a union case makes incomplete decision code fail before it silently chooses behavior. Add an explicit branch and decide its rule. A wildcard is appropriate only when the remaining cases truly share one stable policy; otherwise it discards future compiler help.

Active patterns and guarded patterns can make coverage analysis conservative. Distinguish “the compiler cannot prove coverage” from “the code forgot a domain case.” Restructure a complex match or add an explicit final policy when proof is impractical.

`FS0764` says construction of a named record omitted a field; the locked probe omitted `Age` from `Person`. A record value is complete. Supply every field for a new value, or start from a known value with `{ existing with Field = value }` when the meaning is an update. Do not add meaningless defaults merely to satisfy construction.

## Computation expressions: FS0748 {#computation-expressions}

`return`, `return!`, `let!`, `do!`, and `yield` receive meaning from a computation-expression builder. The probe `let invalid = return 1` produced `FS0748`. An ordinary function returns its last expression, so write `let valid = 1`. If asynchronous, task, sequence, result, or another builder semantics are intended, put the operation inside that builder and use only syntax it implements.

Moving `return` into `task {}` merely to silence the error changes the return type and execution semantics. Choose the workflow first.

## Names, references, and file order: FS0039 {#names-file-order}

Check `FS0039` in this order:

1. spelling and capitalization;
2. lexical scope and whether a local value was defined before use;
3. qualification or the correct `open` declaration;
4. project/package/assembly reference and target framework;
5. whether generated source actually ran;
6. F# `<Compile>` order—provider `.fsi`/`.fs` files must precede consumers.

This wrong-order project intentionally lists `Workflow.fs` before `Domain.fs`:

```xml:line-numbers [Ch16WrongOrder.fsproj — expected error]
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <Compile Include="../../chapters/ch16/Workflow.fs" Link="Workflow.fs" />
    <Compile Include="../../chapters/ch16/Domain.fs" Link="Domain.fs" />
  </ItemGroup>
</Project>
```
The first missing namespace causes further unknown types and values. Repair the first dependency, not every cascade. An `open` only shortens available names; it does not add an assembly reference or make a later file available. See [Chapter 16](../part-03/ch-16-modules-namespaces-projects).

## Hidden representation: FS0800 {#hidden-representation}

The separate Chapter 17 consumer attempts to construct a union case omitted by the library's `.fsi` file:

```fsharp:line-numbers [Consumer.fs — expected error]
namespace ThinkingInFSharp.Ch17.InvalidConsumer

open ThinkingInFSharp.Ch17.SeatAllocation

module Consumer =
    let invalidCapacity = Capacity 0
```
F# 10 reports the terse `FS0800: Invalid use of a type name`. The surrounding signature supplies the missing context: `Capacity` is public as an abstract type, but its representation is not a public constructor. Use `Capacity.create` and handle its validation result. Changing access or deleting the signature defeats the invariant rather than repairing the consumer.

## Nullable boundaries: FS3261 {#nullable-boundaries}

With nullable checking enabled, passing `null` to a parameter inferred as non-null `string` reproduced `FS3261`. Decide the boundary contract:

- if null is invalid, retain `string`, reject untrusted callers at the public edge, and remove the invalid internal call;
- if null is a real CLR input possibility, declare `string | null`, then pattern-match or guard before using it as `string`;
- if absence is a domain value after validation, convert once to `option` or a domain union.

Do not disable nullable checking for a whole project because one interop boundary is honest about null. Make that boundary precise. See Chapters 16, 19, and 27 for compiled examples.

## What was actually run {#verification}

On 2026-08-25, these diagnostics were observed with .NET SDK 10.0.301 and F# 10. The small probes ran with `--warnaserror+ --checknulls+`; each exited nonzero and emitted its listed code. Reproduce one minimal example at a time: a compiler update may change the wording, while the error code and type relationship are the durable evidence.

## Before asking for help {#before-help}

Provide the smallest source that retains the failure, the full first diagnostic, `dotnet --version`, the exact command, project properties affecting language/nullability/warnings, and relevant file/reference order. Say what type or behavior you intended. A screenshot of only the underlined token removes most of the evidence needed to distinguish inference, scope, project, and tooling failures.

## Sources {#sources}

- [Microsoft Learn: F# compiler messages](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/compiler-messages/)
- [Microsoft Learn: compiler error FS0001](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/compiler-messages/fs0001)
- [Microsoft Learn: compiler warning FS0025](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/compiler-messages/fs0025)
- [Microsoft Learn: F# compiler options](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/compiler-options)
- [Microsoft Learn: F# formatting guidelines](https://learn.microsoft.com/en-us/dotnet/fsharp/style-guide/formatting)
- [Microsoft Learn: automatic generalization](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/generics/automatic-generalization)
- [Microsoft Learn: F# signature files](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/signature-files)
- [Microsoft Learn: null values and nullable checking in F#](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/values/null-values)
