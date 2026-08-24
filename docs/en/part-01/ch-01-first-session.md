---
title: "Chapter 1: A First F# Session"
description: "Choose among F# Interactive, scripts, and projects while building an accurate first model of expressions, values, and unit."
translationKey: part-01/ch-01-first-session
kind: chapter
part: 1
chapter: 1
status: review
verifiedWith:
  fsharp: "10"
  dotnetSdk: "10.0.301"
exampleIds:
  - ch01-first-session
exerciseIds:
  - ch01-exercise-01
  - ch01-exercise-02
  - ch01-exercise-03
termIds:
  - expression
  - fsharp-interactive
  - fsharp-script
  - literal
  - unit
  - value
sources:
  - id: microsoft-fsi
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/tools/fsharp-interactive/
    checked: "2026-08-24"
  - id: microsoft-fsi-options
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/fsharp-interactive-options
    checked: "2026-08-24"
  - id: microsoft-fsharp-cli
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/get-started/get-started-command-line
    checked: "2026-08-24"
  - id: microsoft-fsharp-unit
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/unit-type
    checked: "2026-08-24"
  - id: microsoft-fsharp-literals
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/literals
    checked: "2026-08-24"
---

# Chapter 1: A First F# Session {#overview}

The fastest way into a language is not to memorize its syntax first. It is to form a model that is accurate enough to test immediately. This chapter answers one question: **how does a piece of F# code become a result you can observe?**

We will move among three ways of running code. An interactive session answers one small question, a script preserves an experiment, and a project gives a boundary to code that must be compiled, tested, and shipped. One F# idea runs through all three: code is made of expressions, and an expression that completes normally produces a value.

## What you will be able to do {#outcomes}

By the end of this chapter, you should be able to:

- start F# Interactive (FSI) and explain the types and values it prints;
- run an `.fsx` script and explain why a script is not identical to interactive input;
- decide when an experiment should become an `.fsproj` project;
- read strings, integers, Booleans, string interpolation, and simple arithmetic;
- explain why `printfn` produces output yet returns the `unit` value `()`.

You do not need functions, pattern matching, or collections yet. For now, read `let` as “give this value a name.” Chapter 2 will state precisely what a binding is and how type inference works.

## Before you start {#before-you-start}

The examples are verified with F# 10 and .NET SDK `10.0.301`. The repository's `global.json` selects that SDK feature band. Check your environment in a terminal:

```console
dotnet --version
```

If you are using the book repository, run the commands below from its root. A shell prompt shown before a command is not part of the command. This chapter needs only the .NET SDK; an editor, an IDE, and extra NuGet packages are not prerequisites.

## Choose the shortest feedback loop {#feedback-loop}

The three entry points do not replace one another. Each serves a different length of feedback loop.

| Entry point | Best suited to | What you retain |
| --- | --- | --- |
| FSI session | Test one expression and inspect its inferred type | Nothing, if you choose |
| `.fsx` script | Repeatable experiments, automation, small tools | One or more script files |
| `.fsproj` project | Multiple files, tests, packages, application publishing | A project file and ordered source files |

### F# Interactive {#fsi}

Run:

```console
dotnet fsi
```

FSI is a read-evaluate-print loop, or REPL. At its prompt, enter `20 + 22;;`; the double semicolon ends this submission. FSI does more than print `42`: it reports that the result has the static type `int` and temporarily binds an unnamed result to `it`.

There are two early clues here. First, `20 + 22` is not merely a command that “does something”; it is an expression that produces the value `42`. Second, the compiler checks its type before execution. Interactive feedback does not mean dynamic typing.

The `;;` sequence terminates an interactive submission. It is not punctuation that belongs at the end of every line in an ordinary F# source file. Treat the interactive window as a workbench, not as the permanent home of a program.

### F# script {#script}

An F# script has the `.fsx` extension. This stable command runs a script and tells FSI to exit after it finishes:

```console
dotnet fsi --exec examples/scripts/ch01-first-session.fsx
```

A script preserves the order, names, and output of an experiment. It can therefore live in version control and be rerun by a quality gate. You normally omit `;;` in a script because the file boundary and its syntax already tell the compiler what to process.

The script is still executed by FSI. It does not naturally supply an application project's multi-file compilation order, test entry point, publishing settings, or reusable assembly. When code acquires those responsibilities, use a project.

### F# project {#project}

The .NET SDK can create a minimal F# console project:

```console
dotnet new console -lang "F#" -o HelloFSharp
dotnet run --project HelloFSharp
```

The project file records the target framework, source-file order, package dependencies, and build settings. That structure has a small cost, but it gives `dotnet build`, `dotnet test`, and publishing tools a clear boundary. This book moves gradually from scripts to projects. Do not create a project for one arithmetic experiment, and do not leave an application that needs tests and deployment in one script forever.

## Read the first program as expressions {#expressions}

Read the shared example first; there is no need to memorize every symbol.

<<< @/../examples/scripts/ch01-first-session.fsx#first-session{fsharp:line-numbers} [ch01-first-session.fsx]

### Literals produce values {#literals-and-values}

`"Functional Foundations"`, `40`, `18`, and `0` are **literals**: they represent values directly in source code. `let eventName = ...` gives a name to the value computed on the right. It does not create an empty box and assign into it later.

Later expressions use those values:

- `capacity - booked` computes the integer `22`;
- `remaining > 0` computes the Boolean value `true`;
- `$"{eventName}: {remaining} seats remaining"` inserts existing values into a string.

The compiler infers a set of static types from these uses:

| Name | Inferred type | Evidence |
| --- | --- | --- |
| `eventName` | `string` | The right side is a string literal |
| `capacity`, `booked`, `remaining` | `int` | Unsuffixed integers form the `int` arithmetic required here |
| `hasSeats` | `bool` | A `>` comparison produces true or false |
| `summary` | `string` | String interpolation produces text |

Inference removes repetitive annotations; it does not remove types. Trying to subtract a string from an integer fails during compilation rather than waiting for a rare runtime path to guess a conversion rule.

### Printing also returns a value {#unit}

`printfn` writes text to standard output, which is its observable effect. It is still an F# expression, so it must also have a result. Its result type here is `unit`, a type with exactly one value: `()`.

The example first evaluates `printfn "%s" summary`, so the summary appears on screen. The name `printResult` is then bound to the returned value `()`. A later line prints that value. Comparing `unit` with C#'s `void` gives useful intuition, but they are not identical: `void` denotes the absence of an available result, whereas `unit` is an ordinary F# type with one value.

This distinction matters later. A signature ending in `unit` usually warns you that the meaningful result lies in an effect, such as writing a file, sending a response, or recording a log. It does not prove that the effect occurred, and it is not evidence that error handling succeeded.

## Run the shared example {#run-example}

From the repository root, run:

```console
dotnet fsi --exec examples/scripts/ch01-first-session.fsx
```

You should see:

```text
Functional Foundations: 22 seats remaining
Seats available: true
Printing returned: ()
Lin booked 3 seats.
```

In interactive mode, FSI proactively displays the values and types of submissions. Under `--exec`, every line above comes from an explicit `printfn` call in the script. The repository's example manifest also asserts key lines, so the prose and executable behavior share one source of evidence.

## Debugging: identify the execution boundary first {#debugging}

Problems in a first session usually occur at the execution boundary rather than in business logic.

- **FSI keeps waiting:** the interactive submission may lack its closing `;;`, parenthesis, or quotation mark.
- **The script path does not exist:** inspect the current directory. Paths in this book are relative to the repository root.
- **The script does not display a value:** unlike the prompt, a script does not echo every binding; call `printfn` explicitly or take a small expression back to FSI.
- **An integer and a string do not combine:** F# does not casually guess conversions. Read the expected and actual types in the diagnostic, then decide what the data should mean.
- **The output is right but the design is unclear:** one output proves only one run. Later chapters add types, failure paths, and testable boundaries.

A productive rhythm is to send the smallest expression to FSI, understand its type, put it back into the script, and rerun the whole script. This keeps fast feedback without validating only a hand-picked line.

## Exercises {#exercises}

Write down each prediction before running or editing a local copy. The answer is useful when it lets you compare reasoning, not merely final text.

### Exercise 1: predict, then run {#exercise-01}

Without executing the code, answer these questions:

1. What are the types of `remaining`, `hasSeats`, `summary`, and `printResult`?
2. In what order do the four output lines appear? Why does the summary appear before `printResult` is printed?
3. If `booked` changes to `40`, which values change, and how should the output change?

Then copy the script to a temporary location and test the prediction. Do not edit the repository's shared answer.

### Exercise 2: migrate a small program {#exercise-02}

Imagine an imperative program that creates rewritable variables named `guest`, `requestedSeats`, and `confirmation`, then prints “Lin booked 3 seats.” Rewrite it in F# using only constructs from this chapter:

1. express the data dependencies with three `let` bindings;
2. build the confirmation with string interpolation;
3. print it with `printfn`;
4. state the final call's return value, not only what appears on screen.

### Exercise 3: choose an entry point {#exercise-03}

Choose FSI, a script, or a project for each job, and give one reason:

1. inspect the result and type of `17 * 23`;
2. run a version-controlled utility each week to produce a local report;
3. build an HTTP service with multiple modules, automated tests, and deployment.

[Read the chapter solutions](../solutions/ch-01-first-session).

## Summary {#summary}

- FSI gives the shortest feedback loop and displays both values and inferred types.
- An `.fsx` file preserves an experiment as a repeatable script; `--exec` exits when it finishes.
- A project gives boundaries to multi-file compilation, dependencies, tests, and publishing.
- The basic reading unit in F# is an expression; an expression that completes normally produces a value.
- Output is an effect, while the return value of `printfn` is the sole `unit` value, `()`.

The next chapter tightens the temporary language used here: what `let` binds, why names are non-rewritable by default, and how the compiler infers types from constraints.

## Vocabulary {#vocabulary}

- **expression:** code that is evaluated and produces a value when it completes normally.
- **value:** a result of evaluation that another expression can use; functions will also count as values.
- **literal:** a value representation written directly in source, such as `40` or `"hello"`.
- **F# Interactive:** the F# REPL in the .NET SDK, also capable of executing `.fsx` scripts.
- **F# script:** an `.fsx` source file normally run directly by FSI.
- **unit:** a type with one value, `()`, often returned when only an effect is of interest.

## Sources {#sources}

- [Microsoft Learn: F# Interactive](https://learn.microsoft.com/en-us/dotnet/fsharp/tools/fsharp-interactive/)
- [Microsoft Learn: F# Interactive options](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/fsharp-interactive-options)
- [Microsoft Learn: Get started with F# and the .NET CLI](https://learn.microsoft.com/en-us/dotnet/fsharp/get-started/get-started-command-line)
- [Microsoft Learn: Unit type](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/unit-type)
- [Microsoft Learn: Literals](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/literals)
