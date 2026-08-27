---
title: "Chapter 1 Solutions"
description: "Reasoning, a migration example, and execution-entry choices for the first F# session."
translationKey: solutions/ch-01-first-session
---

# Chapter 1 Solutions {#overview}

Complete your own reasoning before comparing it with the process below. Matching the output is not enough: you should be able to trace every value and say when each line was printed.

[Return to Chapter 1](../part-01/ch-01-first-session).

## Exercise 1: explain the run {#exercise-01}

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

## Exercise 2: migrate a small program {#exercise-02}

One direct answer is in the separate solution script:

```fsharp:line-numbers
let guest = "Lin"
let requestedSeats = 3
let confirmation = $"{guest} booked {requestedSeats} seats."

printfn "%s" confirmation
```
The three `let` bindings state data dependencies rather than declaring three storage slots that must later be rewritten. `confirmation` depends only on the two values already named. The final `printfn` writes the text to standard output and returns `()`.

Type annotations add no value here: the string literal, integer `3`, interpolation, and `printfn` already give the compiler enough constraints. Nor is there a reason to invent an operator or abstraction to make the answer look “more functional.” Clear intermediate values are the purpose of the exercise.

## Exercise 3: choose an entry point {#exercise-03}

| Job | Suitable entry point | Reason |
| --- | --- | --- |
| Inspect `17 * 23` | FSI | The question is one expression; immediate value and type feedback is most useful |
| Produce a local report every week | Script | The code must be saved, reviewed, and repeated, but may not need an application publishing boundary |
| Build and deploy an HTTP service | Project | Multiple modules, tests, dependencies, configuration, and publishing need an explicit build boundary |

These are not inviolable rules. A growing script can migrate to a project, and a small expression from a project can still be explored in FSI. Choose the shortest reliable feedback loop for the current problem, not a status hierarchy of file extensions.

## What to notice {#what-to-notice}

- **A value and an effect are different:** the effect of `printfn` is output; its value is `()`.
- **Dependencies reveal evaluation order:** the right-side expression must finish before a `let` binding can be established.
- **Inference is still static typing:** fewer annotations do not permit arbitrary runtime type changes.
- **Tools should scale with the problem:** FSI, scripts, and projects each preserve a useful workflow.

If your answer differs but runs, ask whether the difference is permitted by the task or merely happens to print the same text. Putting everything directly into one `printfn` would produce the same line, but it would not meet the learning goal of expressing data dependencies with bindings.
