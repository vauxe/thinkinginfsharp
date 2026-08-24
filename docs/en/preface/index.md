---
title: "Preface: How to Use This Book"
description: "Choose a learning route, read F# types, run the evidence, and understand the F# 10 and .NET 10 scope of the book."
translationKey: preface/index
kind: preface
status: complete
exampleIds: []
exerciseIds: []
termIds: []
sources:
  - id: microsoft-fsharp-get-started
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/get-started/
    checked: "2026-08-25"
  - id: microsoft-fsharp-10
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/whats-new/fsharp-10
    checked: "2026-08-25"
  - id: microsoft-dotnet-10-download
    url: https://dotnet.microsoft.com/en-us/download/dotnet/10.0
    checked: "2026-08-25"
  - id: microsoft-global-json
    url: https://learn.microsoft.com/en-us/dotnet/core/tools/global-json
    checked: "2026-08-25"
---

# Preface: How to Use This Book {#overview}

This is a book about learning to think with F#, not translating another language statement by statement. It starts from expressions, types, functions, and data transformation, then follows those ideas into effects, .NET boundaries, testing, architecture, and ecosystem choices.

The Chinese and English editions have the same content contract, examples, exercises, evidence, and stable anchors. This English edition is self-contained; knowing Chinese provides no missing prerequisite.

## Who this book is for—and who it is not for {#audience}

This book fits you if you can already write small programs in any language and want to do at least one of these things:

- learn functional programming without treating F# as mathematical decoration;
- move from C# or another .NET language while preserving useful platform knowledge;
- model domain rules, failures, effects, and concurrency explicitly;
- judge F# realistically for web, data, cloud, desktop, automation, or Unity work;
- progress from scripts to a tested, diagnosable application.

It is not optimized as a first-ever introduction to variables, loops, files, HTTP, or testing. It is also not an exhaustive language specification, package catalog, framework cookbook, compiler-internals course, or promise that one tool fits every product. Use the linked official references when exact syntax or a changing platform contract matters.

## What “thinking in F#” means {#fsharp-first}

F# is not “C# with fewer braces.” The recurring questions in this book are:

1. What value does this expression produce?
2. What does the inferred or public type permit?
3. Which cases does the data model make explicit?
4. Which transformation is pure, and where is an effect owned?
5. What observable evidence would distinguish a correct design from a plausible wrong one?

This viewpoint does not forbid objects, mutation, exceptions, tasks, or .NET APIs. It asks you to use each at a deliberate boundary. Mastery here means being able to explain that boundary and its tradeoff, not merely remembering syntax.

## Choose one of three reading routes {#reading-routes}

| Route | Read in this order | Move on when |
|---|---|---|
| quick start | [setup](../appendices/a-setup), Chapters [1](../part-01/ch-01-first-session)–[6](../part-01/ch-06-recursion-folds), then the Part I booking script | you can predict a small script from its types and explain its pipeline or fold |
| systematic study | Chapters 1–38 in order; then Chapters 39–45 by platform interest; use appendices as references | you can model and test the booking workflow, then justify an ecosystem boundary |
| C#/.NET transition | scan the [migration map](../appendices/d-csharp-migration), run Chapter 1, test yourself through Chapters 2–18, study Chapters 19–32 closely, then build Chapters 33–38 | you stop translating syntax and can expose an intentional API to both F# and C# callers |

“Scan” does not mean assume. Read the type signatures, attempt the exercises, and stay when your prediction differs from the compiler or test. The later parts depend on the modeling vocabulary of Chapters 7–18 even when the syntax looks familiar.

## Use a chapter as a feedback loop {#chapter-loop}

For each chapter:

1. read the outcome and inspect important type signatures;
2. predict output, failure, ordering, or ownership before running anything;
3. run the smallest referenced example or test;
4. explain the result without copying the prose;
5. solve all three exercises before opening the solution;
6. compare contracts and evidence, then revise your answer.

Closed exercises may have a narrow observable result. Diagnostic and design exercises can have several sound answers. The [solutions and review guide](../appendices/g-solutions-guide) explains the rubric and links to every answer; a published solution is feedback, not automatic proof or the only acceptable design.

## Run only as much as you need {#running-examples}

The static book can be read without installing a toolchain. To execute the core F# examples, install the SDK described in [Appendix A](../appendices/a-setup), clone the repository, and run commands from its root.

Check which SDK the repository selected:

```console
dotnet --version
```

Run one script without opening an interactive prompt:

```console
dotnet fsi --exec examples/scripts/ch01-first-session.fsx
```

Run the first integrated booking slice:

```console
dotnet fsi --exec examples/capstone/part-01/BookingBasics.fsx
```

Maintainers can run the complete example gate after the repository's frozen Node installation:

```console
pnpm check:examples
```

Do not run files under `examples/expected-errors/` as if they were successful programs. The harness compiles them expecting a specific diagnostic. Some ecosystem passages are explicitly marked as source review, proposals, or manual platform checks; they are not silently promoted to executed evidence.

## Read the type before the implementation {#reading-signatures}

Start at the outer shape and read arrows from left to right while remembering that `->` groups to the right.

| Signature | Read it as |
|---|---|
| `string -> int` | a function from one string to one integer |
| `int -> int -> int` | an integer in, then another integer in, then an integer out; it can be partially applied |
| `(int * int) -> int` | one tuple containing two integers in, then one integer out |
| `'T list -> 'T option` | a list of any one element type in; either one element of that same type or no value out |
| `Request -> Result<Reservation, BookingError>` | a request in; either a reservation or an expected booking error out |
| `unit -> Task<'T>` | an explicit start signal in; an asynchronous .NET task producing `'T` out |

For example, read `values: 'T list -> 'T option` as: the parameter is named `values`; all list elements share some type `'T`; success returns that same type; absence is represented explicitly. Names help, but the type is the contract the compiler can check.

When a signature feels dense, name each input and intermediate result instead of guessing from punctuation. [Appendix B](../appendices/b-syntax-reference) is the compact reference; the [glossary](../glossary) defines the vocabulary in English and shows its Chinese counterpart without requiring Chinese.

## Understand the version and evidence boundary {#version-scope}

The repository sets `<LangVersion>10.0</LangVersion>`, normally targets `net10.0`, and records SDK `10.0.301` as its reproducibility baseline. Its `global.json` uses `latestPatch`, so SDK selection may move only to an installed patch in the same `10.0.3xx` feature band. That baseline establishes the compiler behavior reported by the book; it is not a claim that `10.0.301` remains the newest security-servicing release.

Use a currently supported patched SDK and runtime for deployed software, and rerun the evidence after upgrading. Most examples target .NET 10; the Unity library deliberately targets `netstandard2.1` for that host boundary, and editor/player results remain separately recorded manual evidence.

F# 10 is the language scope, not a reason to showcase every new feature. The book prefers durable fundamentals and introduces version-specific behavior only where it changes a real decision. Package, browser, cloud, mobile, and Unity facts are dated because their contracts can change faster than the language core.

## When you get stuck {#recovery}

- If a tool is missing or the wrong SDK is selected, use [Appendix A](../appendices/a-setup).
- If punctuation or precedence is blocking reading, use [Appendix B](../appendices/b-syntax-reference).
- If the compiler reports an unfamiliar `FS` number, use [Appendix E](../appendices/e-compiler-errors) and fix the first relevant error.
- If a term is unclear, use the [bilingual glossary](../glossary).
- If an advanced feature appears in library code, use [Appendix H](../appendices/h-advanced-index) to decide whether to learn, wrap, or defer it.

Then begin with [Chapter 1](../part-01/ch-01-first-session). Keep your predictions visible: the gap between a prediction and compiler evidence is where the book does its best teaching.
