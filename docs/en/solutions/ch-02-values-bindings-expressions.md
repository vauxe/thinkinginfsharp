---
title: "Chapter 2 Solutions"
description: "Reasoning about values, bindings, basic types, explicit conversion, and local shadowing."
translationKey: solutions/ch-02-values-bindings-expressions
---

# Chapter 2 Solutions {#overview}

Compare the reasoning process before comparing final output. If one successful run cannot tell you where the type constraints came from, the exercise is not finished.

[Return to Chapter 2](../part-01/ch-02-values-bindings-expressions).

## Exercise 1: read types instead of guessing {#exercise-01}

The seven bindings have these types:

| Name | Type | Main constraint |
| --- | --- | --- |
| `eventName` | `string` | Double-quoted string literal |
| `capacity` | `int` | Integer `40` with no other context |
| `fillRatio` | `float` | Unsuffixed fractional literal `0.45` |
| `ticketPrice` | `decimal` | The `m` suffix |
| `eventCode` | `char` | Single-quoted character literal |
| `registrationOpen` | `bool` | `true` |
| `noFurtherResult` | `unit` | The sole value `()` |

`float` uses binary floating-point representation, while `decimal` is a distinct decimal numeric type; the `m` suffix explicitly selects the latter. A `char` is one UTF-16 code unit, while a `string` is a sequence of UTF-16 code units. Single and double quotes express those respective types.

The compiler still determines all these types at compile time without annotations. FSI may display `19.50m` as `19.50M`, so a displayed value need not reproduce its source literal character for character.

## Exercise 2: repair a representation boundary {#exercise-02}

One direct answer is in the separate solution script:

```fsharp:line-numbers
let rawAttendeeCount = "24"
let attendeeCount = int rawAttendeeCount
let nextAttendeeCount = attendeeCount + 1

printfn "Next attendee count: %d" nextAttendeeCount
```
`rawAttendeeCount` is a `string`, while the other side of integer addition is an `int`; F# will not implicitly interpret arbitrary text as an integer. `int rawAttendeeCount` explicitly produces an `int`, so both `attendeeCount` and `nextAttendeeCount` are `int`. The final output is `Next attendee count: 25`.

One risk is deliberately left here: if the text is not a valid integer, the `int` conversion throws an exception. This exercise assumes a valid-input boundary. At a real input boundary, later chapters will express that branch with an explicit failure type or controlled exception conversion. Do not read this example as “all parsing should call `int` directly.”

## Exercise 3: trace shadowing {#exercise-03}

Look at the same region again:

```fsharp:line-numbers
let normalizedCapacity =
    let capacity = 20
    let capacity = capacity + 4
    capacity

printfn "Normalized capacity: %d; outer capacity: %d" normalizedCapacity capacity
```
The first local right side directly produces `20`. While evaluating the second right side, `capacity + 4`, the name still denotes the first local binding, so the result is `24`; the new binding then shadows it. The final body sees the newest local binding, so `normalizedCapacity` is `24`.

After leaving that local scope, the script-level `capacity` is visible again and remains `40`. The region establishes three bindings: two local bindings named `capacity` and the top-level `normalizedCapacity`. It mutates no existing value.

## What to notice {#what-to-notice}

- **Types come from constraints, not names:** renaming `ticketPrice` to `x` does not remove the type information supplied by `m`.
- **An annotation is not a conversion:** `: decimal` requires a right side that already has that type; `decimal value` produces a new representation.
- **Shadowing is not assignment:** two same-named bindings remain two bindings, and the outer value does not change over time.
- **A boundary failure must re-enter the model later:** the valid-input example simplifies this chapter, but the solution records the unhandled path explicitly.

An implementation with different names can still be correct if it preserves the same type boundary. Leaving all input as strings and concatenating something that looks like `25` may produce similar text, but it evades the numeric model this exercise is meant to establish.
