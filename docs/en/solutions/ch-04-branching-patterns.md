---
title: "Chapter 4 Solutions"
description: "Reasoning about conditional results, match order, guards, and tuple and list patterns."
translationKey: solutions/ch-04-branching-patterns
kind: solution
part: 1
chapter: 4
status: complete
verifiedWith:
  fsharp: "10"
  dotnetSdk: "10.0.301"
exampleIds:
  - ch04-branching-patterns
exerciseIds:
  - ch04-exercise-01
  - ch04-exercise-02
  - ch04-exercise-03
termIds: []
sources:
  - id: microsoft-conditionals
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/conditional-expressions-if-then-else
    checked: "2026-08-24"
  - id: microsoft-match
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/match-expressions
    checked: "2026-08-24"
---

# Chapter 4 Solutions {#overview}

The important part of a branch answer is not the final string but the first successful rule, why earlier rules were skipped, and the unified result type.

[Return to Chapter 4](../part-01/ch-04-branching-patterns).

## Exercise 1: unify the result of `if` {#exercise-01}

The shared definition is:

<<< @/../examples/scripts/ch04-branching-patterns.fsx#if-expression{fsharp:line-numbers} [ch04-branching-patterns.fsx]

The condition for `availability 3` is `true`, producing `"available"`. The condition for `availability 0` is `false`, producing `"full"`. The condition `remaining > 0` is `bool`, and both branches are `string`, so the whole function is `int -> string`.

If `then` returns a string while `else` only calls `printfn`, the result types are `string` and `unit` and cannot unify. An output effect does not become a string result. Omitting `else` is legal only when the whole conditional is effect-only and `then` also returns `unit`; the nonmatching path then also produces `()`.

## Exercise 2: trace rules and guards {#exercise-02}

| Input | First successful rule | Result |
| --- | --- | --- |
| `-2` | `value when value <= 0` | `"full"` |
| `0` | `value when value <= 0` | `"full"` |
| `1` | Literal `1` | `"last seat"` |
| `5` | `value when value <= 5` | `"limited"` |
| `6` | `_` | `"available"` |

Input `1` initially matches the first variable pattern too, but its guard is false, so matching continues to the literal rule. For `6`, both guards become false and the literal does not match, leaving the wildcard.

Move the wildcard first and it matches every input before the other rules, making them unreachable. Keeping only guarded variable rules is not an exhaustive set the compiler can prove: guards are arbitrary Boolean expressions that may both be false and may change later. An unguarded fallback explicitly covers the remainder.

## Exercise 3: decompose composite input {#exercise-03}

The definition is:

<<< @/../examples/scripts/ch04-branching-patterns.fsx#exercise-03{fsharp:line-numbers} [ch04-branching-patterns.fsx]

`(5, 0)` first satisfies requested-not-positive and produces `"invalid"`. `(5, 3)` skips that rule and satisfies `3 <= 5`, producing `"accepted"`. Both guards fail for `(2, 3)`, so `_` produces `"too large"`. The function type is `int * int -> string`.

Order matters: if the acceptance rule came first, `(0, 0)` would satisfy `0 <= 0` before invalidity was checked. Rule order directly expresses business priority here.

The queue definition is:

<<< @/../examples/scripts/ch04-branching-patterns.fsx#list-pattern{fsharp:line-numbers} [ch04-branching-patterns.fsx]

Both a two-item and a four-item list match `first :: second :: _`. The first two names bind the first two items, while `_` matches the remaining list: `[]` for two items and a two-item tail for four. It is not the third element and establishes no readable name.

## What to notice {#what-to-notice}

- **Branches produce values:** compare right-side types, not only whether a condition is correct.
- **Patterns and guards divide work:** patterns handle structure and binding; guards handle additional Boolean relationships.
- **The first successful rule wins:** reordering rules can change business meaning.
- **Exhaustiveness needs unconditional coverage:** a relationship humans infer among guards is not a general compiler proof.

Another implementation using nested `if` expressions may still be correct. Compare which form makes the two inputs, priority, and fallback clearest rather than counting occurrences of `match`.
