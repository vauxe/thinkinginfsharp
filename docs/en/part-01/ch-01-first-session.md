---
title: "Chapter 1: A First F# Session"
description: "Choose among F# Interactive, scripts, and projects while building an accurate first model of expressions, values, and unit."
translationKey: part-01/ch-01-first-session
---

# Chapter 1: A First F# Session {#overview}

The fastest way into a language is to form a model that is accurate enough to test immediately. Syntax then has a context and a purpose. This chapter answers one question: **how does a piece of F# code become a result you can observe?**

We will move among three ways of running code. An interactive session answers one small question, a script preserves an experiment, and a project organizes code that must be compiled, tested, and shipped. One F# idea runs through all three: code is made of expressions, and an expression that completes normally produces a value.

Functions, pattern matching, and collections arrive in later chapters. For now, read `let` as “give this value a name.” Chapter 2 will state precisely what a binding is and how type inference works.

## Before you start {#before-you-start}

The examples were reviewed with F# 10 and .NET 10. Check your installed SDK in a terminal:

```console
dotnet --version
```

This chapter uses only tools and libraries supplied with the .NET SDK; no additional package is required. An editor or IDE is optional. When a command example includes a shell prompt, enter only the text after the prompt.

## Choose the shortest feedback loop {#feedback-loop}

The three entry points suit different kinds of feedback.

| Entry point | Best suited to | What you retain |
| --- | --- | --- |
| FSI session | Test one expression and inspect its inferred type | Ephemeral session state |
| `.fsx` script | Repeatable experiments, automation, small tools | One or more script files |
| `.fsproj` project | Multiple files, tests, packages, application publishing | A project file and ordered source files |

### F# Interactive {#fsi}

Run:

```console
dotnet fsi
```

FSI is a read-evaluate-print loop, or REPL. At its prompt, enter `20 + 22;;`; the double semicolon ends this submission. FSI does more than print `42`: it reports that the result has the static type `int` and temporarily binds an unnamed result to `it`.

There are two early clues here. First, `20 + 22` is an expression that produces the value `42`. Second, the compiler checks its type before execution. FSI combines interactive feedback with static typing.

The `;;` sequence terminates an interactive submission. Ordinary F# source files use their file boundary and syntax instead. Use the interactive window as a workbench, then preserve durable code in a script or project.

### F# script {#script}

An F# script has the `.fsx` extension. This command runs a script and tells FSI to exit after it finishes:

```console
dotnet fsi --exec ch01-first-session.fsx
```

A script preserves the order, names, and output of an experiment. It can therefore live in version control and run again in automated checks. You normally omit `;;` because the file and its syntax already tell the compiler what to process.

The script is still executed by FSI. A project adds multi-file compilation order, a test entry point, publishing settings, and a reusable assembly. Move to a project when code acquires those responsibilities.

### F# project {#project}

The .NET SDK can create a minimal F# console project:

```console
dotnet new console -lang "F#" -o HelloFSharp
dotnet run --project HelloFSharp
```

The project file records the target framework, source-file order, package dependencies, and build settings. That structure has a small cost, but it lets `dotnet build`, `dotnet test`, and publishing tools operate consistently. This book moves gradually from scripts to projects: use FSI for a small arithmetic experiment, and use a project once an application needs tests or deployment.

## Read the first program as expressions {#expressions}

Save the following self-contained block as `ch01-first-session.fsx`, then run the script command shown earlier. Observe the output order first; later sections explain each unfamiliar symbol.

```fsharp:line-numbers
let eventName = "Functional Foundations"
let capacity = 40
let booked = 18
let remaining = capacity - booked
let hasSeats = remaining > 0
let summary = $"{eventName}: {remaining} seats remaining"

let printResult = printfn "%s" summary
printfn "Seats available: %b" hasSeats
printfn "Printing returned: %A" printResult
```

The output is:

```text
Functional Foundations: 22 seats remaining
Seats available: true
Printing returned: ()
```

### Literals produce values {#literals-and-values}

`"Functional Foundations"`, `40`, `18`, and `0` are **literals**: they represent values directly in source code. `let eventName = ...` gives a name to the value computed on the right. Explicit mutable storage uses a different construct introduced later.

Later expressions use those values:

- `capacity - booked` computes the integer `22`;
- `remaining > 0` computes the Boolean value `true`;
- `$"{eventName}: {remaining} seats remaining"` inserts existing values into a string.

The compiler infers a set of static types from these uses:

| Name | Inferred type | Evidence |
| --- | --- | --- |
| `eventName` | `string` | The right side is a string literal |
| `capacity`, `booked`, `remaining` | `int` | In this context, unsuffixed integer literals default to `int`, so the subtraction is also an `int` operation |
| `hasSeats` | `bool` | A `>` comparison produces true or false |
| `summary` | `string` | String interpolation produces text |

Inference removes repetitive annotations while preserving static types. Subtracting a string from an integer therefore fails during compilation, before any runtime path can use the expression.

### Printing also returns a value {#unit}

`printfn` writes text to standard output, which is its observable effect. It is still an F# expression, so it must also have a result. Its result type here is `unit`, a type with exactly one value: `()`.

The example first evaluates `printfn "%s" summary`, so the summary appears on screen. The name `printResult` is then bound to the returned value `()`. A later line prints that value. C#'s `void` denotes the absence of an available result; F#'s `unit` is an ordinary type with one value.

This distinction matters later. A signature ending in `unit` usually means that the call matters because it performs an effect, such as writing a file, sending a response, or recording a log. The return type alone cannot say whether that effect completed or failed; tests and an explicit error model must check those outcomes.

## Exercises {#exercises}

Answer independently before running or editing a local copy. Use the solution to compare both your reasoning and your final answer.

### Exercise 1: explain the run {#exercise-01}

Use the output you just observed to answer these questions:

1. What are the types of `remaining`, `hasSeats`, `summary`, and `printResult`?
2. In what order do the output lines appear? Why does the summary appear before `printResult` is printed?
3. Before editing, predict: if `booked` changes to `40`, which values change, and how should the output change?

Then edit your local copy and test the prediction in question 3.

::: details Answer

The types are:

| Name | Type | Computed value |
| --- | --- | --- |
| `remaining` | `int` | `22` |
| `hasSeats` | `bool` | `true` |
| `summary` | `string` | `"Functional Foundations: 22 seats remaining"` |
| `printResult` | `unit` | `()` |

To evaluate the right side of `printResult`, `printfn "%s" summary` must run first, so the summary is the first output line. After that print finishes, the call returns `()`, and that value is bound to `printResult`. The next two `printfn` calls print the Boolean and `()` in order.

If `booked` changes to `40`, `remaining` changes from `22` to `0`, and `hasSeats` changes from `true` to `false`. Because `summary` depends on `remaining`, it now ends in `0 seats remaining`. The type and value of `printResult` do not change: printing different text still returns `()`.

The point is not the subtraction. It is to reason in the direction of dependency: an input changes the arithmetic expression, which changes the comparison and interpolation, which finally changes the output.

:::

### Exercise 2: migrate a small program {#exercise-02}

Imagine an imperative program that creates rewritable variables named `guest`, `requestedSeats`, and `confirmation`, then prints “Lin booked 3 seats.” Rewrite it in F# using only constructs from this chapter:

1. express the data dependencies with three `let` bindings;
2. build the confirmation with string interpolation;
3. print it with `printfn`;
4. state both the final call's return value and what appears on screen.

::: details Answer

One direct answer is:

```fsharp:line-numbers
let guest = "Lin"
let requestedSeats = 3
let confirmation = $"{guest} booked {requestedSeats} seats."

printfn "%s" confirmation
```
The three `let` bindings state data dependencies rather than declaring three storage slots that must later be rewritten. `confirmation` depends only on the two values already named. The final `printfn` writes the text to standard output and returns `()`.

Type annotations add no value here: the string literal, integer `3`, interpolation, and `printfn` already give the compiler enough constraints. Nor is there a reason to invent an operator or abstraction to make the answer look “more functional.” Clear intermediate values are the purpose of the exercise.

:::

### Exercise 3: choose an entry point {#exercise-03}

Choose FSI, a script, or a project for each job, and give one reason:

1. inspect the result and type of `17 * 23`;
2. run a version-controlled utility each week to produce a local report;
3. build an HTTP service with multiple modules, automated tests, and deployment.

::: details Answer

| Job | Suitable entry point | Reason |
| --- | --- | --- |
| Inspect `17 * 23` | FSI | The question is one expression; immediate value and type feedback is most useful |
| Produce a local report every week | Script | The code must be saved, reviewed, and repeated, but may not need an application publishing boundary |
| Build and deploy an HTTP service | Project | Multiple modules, tests, dependencies, configuration, and publishing need an explicit build boundary |

These are not inviolable rules. A growing script can migrate to a project, and a small expression from a project can still be explored in FSI. Choose the shortest reliable feedback loop for the current problem, not a status hierarchy of file extensions.

:::

The next chapter tightens the temporary language used here: what `let` binds, how immutable names work by default, and how the compiler infers types from constraints.

## Sources {#sources}

- [Microsoft Learn: F# Interactive](https://learn.microsoft.com/en-us/dotnet/fsharp/tools/fsharp-interactive/)
- [Microsoft Learn: F# Interactive options](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/fsharp-interactive-options)
- [Microsoft Learn: Get started with F# and the .NET CLI](https://learn.microsoft.com/en-us/dotnet/fsharp/get-started/get-started-command-line)
- [Microsoft Learn: Unit type](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/unit-type)
- [Microsoft Learn: Literals](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/literals)
