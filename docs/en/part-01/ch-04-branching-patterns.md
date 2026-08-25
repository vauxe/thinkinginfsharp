---
title: "Chapter 4: Branching and Basic Patterns"
description: "Read if and match as value-producing expressions, then build safe branches with literal, variable, wildcard, tuple, and list patterns."
translationKey: part-01/ch-04-branching-patterns
---

# Chapter 4: Branching and Basic Patterns {#overview}

Imperative languages often describe branching as “choose which statement executes next.” A more useful starting point in F# is that both `if` and `match` are expressions, so the selected branch must produce the result of the whole expression. A branch changes control flow and is also responsible for a result type.

An `if` selects between two results from a Boolean condition. A `match` compares one value with an ordered series of **patterns**. A pattern can recognize the shape of input and bind names to its components in the successful branch. Type checking and exhaustiveness checking can therefore participate in branch design.

## What you will be able to do {#outcomes}

By the end of this chapter, you should be able to:

- read `if...then...else` as an expression with one unified result type;
- write Boolean conditions without implicit truthiness conversions;
- trace a `match` in top-to-bottom rule order;
- use literal, variable, wildcard, tuple, and list patterns;
- use a `when` guard for a Boolean constraint beyond pattern shape;
- understand the value of basic exhaustiveness and redundant-rule diagnostics;
- choose `if` or `match` based on a simple Boolean choice versus structural decomposition.

This chapter uses tuples and lists only to build pattern intuition. Records and discriminated unions make exhaustiveness a domain-modeling tool in Chapters 7 and 8. List transformations wait until the next chapter.

## An `if` selects one result {#if-expression}

The shared script first maps remaining capacity to text:

```fsharp:line-numbers [ch04-branching-patterns.fsx]
let availability remaining =
    if remaining > 0 then "available" else "full"

printfn "Availability: %s" (availability 3)
```
Evaluation first computes `remaining > 0`. When it is `true`, only the `then` branch runs; otherwise, only the `else` branch runs. The unselected branch is not evaluated. The value of the whole `if` is the selected branch's value, so `availability` returns `string`.

### The condition must actually be `bool` {#boolean-only}

F# has no general rule that automatically interprets an integer, string, list, or object as true or false. `0`, `""`, and an empty list cannot stand directly after `if`. The condition must have type `bool`, such as `remaining > 0`, `name = "Lin"`, or a Boolean function call learned later.

This restriction writes intent into the source. Testing whether a person count is positive, whether a string is empty, and whether a value is missing are different questions; an implicit truthiness convention should not blur them together.

### Both branches must unify to one type {#branch-types}

Because an `if` is one expression, the results of `then` and `else` must unify to one type. If one branch returns `string` and the other returns `int`, a caller cannot receive one static result type, so the compiler rejects the code.

Effects in a branch do not remove this rule. If one side only calls `printfn` and returns `unit` while the other returns text, the result types still conflict. Have both branches produce data and print once outside when possible; the decision then becomes easier to test as well.

### Omitting `else` is only for `unit` {#else-unit}

F# permits an omitted `else`, but then the whole expression must have type `unit`, and the `then` branch must also return `unit`. This fits an effect-only action such as recording a message when a condition holds. It is not a way to give a data branch a hidden default.

When the business operation needs a result, write every branch explicitly. If no sensible alternative result exists, the model may need `option` or `Result` rather than an implicit `()`; those types arrive in Chapter 9.

## A `match` inspects the shape of one value {#match-expression}

`match input with` evaluates `input` once, then tries rules from top to bottom. Each rule contains a pattern, an optional guard, and a result expression:

```text
match input with
| pattern when condition -> result
| pattern -> result
```

After finding the first rule whose pattern matches and whose guard is true, only that rule's right side is evaluated. Its result is the value of the entire `match`, so all reachable rule results must unify to one type.

### Rule order is semantics {#rule-order}

Patterns are not an unordered query. An early broad rule intercepts later specific rules. Put wildcard `_` first and it matches every input, making later rules meaningless; the compiler normally reports an unreachable-rule warning.

Ordering “special cases before general cases” is often clear, but business priority expressed by guards can decide the order too. An invalid request must be checked before “requested seats do not exceed remaining seats,” or `(0, 0)` could be accepted accidentally.

### Patterns both test and bind {#patterns-bind}

Basic patterns play different roles:

| Pattern | What it matches | Binds a name? |
| --- | --- | --- |
| `0`, `1`, `"Lin"` | A value equal to the literal | No |
| `value` | Any value compatible with the contextual type | Yes, as `value` |
| `_` | Any value | No |
| `(guest, seats)` | A pair, decomposing both positions | Yes |
| `[]` | An empty list | No |
| `[ only ]` | A list of exactly one element | Yes |
| `head :: tail` | The head and remaining list of a nonempty list | Yes |

A lowercase variable pattern such as `value` matches any value and creates a new binding. It does not automatically compare input with an outer value of the same name. Use a guard to compare with an existing runtime value. Mistaking a variable pattern for a constant comparison is one of the subtler early `match` errors.

## A guard adds a Boolean constraint {#guards}

A numeric range is not a simple literal shape. Bind the value with a variable pattern, then check it in a `when` guard:

```fsharp:line-numbers [ch04-branching-patterns.fsx]
let capacityBand remaining =
    match remaining with
    | value when value <= 0 -> "full"
    | 1 -> "last seat"
    | value when value <= 5 -> "limited"
    | _ -> "available"

printfn "Capacity bands: %s, %s, %s, %s" (capacityBand 0) (capacityBand 1) (capacityBand 4) (capacityBand 8)
```
For input `4`, the first variable pattern initially matches, but `4 <= 0` is false, so matching continues. Literal `1` does not match. The third variable pattern matches and its guard is true, producing `"limited"`.

A guard runs only after its associated pattern matches. A false guard does not fail the whole `match`; it continues to the next rule. Keep a guard easy to understand and preferably effect-free. Depending on guard side effects makes rule order harder to reason about.

Guards also explain why `| value when value = target -> ...` compares with a runtime parameter named `target`. Writing `| target -> ...` directly would create a new local binding that covers every input instead of reading the outer `target`.

## Tuple patterns inspect several positions together {#tuple-patterns}

Chapter 3 used a tuple as one composite argument. A pattern can also decompose it in a function parameter or `match`:

```fsharp:line-numbers [ch04-branching-patterns.fsx]
let bookingSummary (guest, seats) =
    let noun = if seats = 1 then "seat" else "seats"
    $"{guest} requested {seats} {noun}"

printfn "Booking: %s" (bookingSummary ("Lin", 3))
```
`(guest, seats)` requires a pair and establishes two local names in the function body. Tuple patterns work positionally, and both arity and component types must agree with the input.

A pattern handles shape and binding; a value test such as `seats = 1` remains a Boolean expression. This example uses `if` for singular versus plural because the question is one direct Boolean choice. There is no need to turn every Boolean into `match` merely to display syntax.

In `match remaining, requested with`, the comma first forms a pair as the match input; the rule pattern decomposes it. F# often omits unnecessary outer parentheses around rule patterns, so `| remaining, requested ->` still denotes a tuple pattern.

## List patterns distinguish structure {#list-patterns}

An F# list is an ordered, immutable, singly linked collection of elements of one type. This chapter needs only enough syntax to recognize shape: `[]` is empty, `[ a; b ]` contains exactly two elements, and `head :: tail` decomposes a nonempty list into its first element and remaining list.

The shared example covers empty, one-element, and at-least-two-element shapes:

```fsharp:line-numbers [ch04-branching-patterns.fsx]
let describeQueue queue =
    match queue with
    | [] -> "empty"
    | [ only ] -> $"one: {only}"
    | first :: second :: _ -> $"next: {first}, then {second}"

printfn "Queues: %s | %s | %s" (describeQueue []) (describeQueue [ "Lin" ]) (describeQueue [ "Lin"; "Ada"; "Sam" ])
```
`[ only ]` matches only a list of length one. `first :: second :: _` associates to the right as `first :: (second :: _)`: take the first item, then the second, while wildcard `_` accepts the remaining list without binding it. It therefore matches any list with at least two elements.

Do not use `[ first; second ]` to mean “the first two”; it matches exactly two elements. The next chapter covers construction and transformation, and Chapter 6 uses `head :: tail` for structural recursion.

## Exhaustiveness makes omissions visible {#exhaustiveness}

A set of rules is **exhaustive** when every possible shape of the input type can reach at least one rule. Otherwise, a runtime input may cause match failure. The compiler warns about clearly incomplete matches; the book projects treat warnings as errors so omissions must be addressed.

### What the compiler can prove {#compiler-checks}

The compiler can analyze finite structural cases such as a list being empty or `head :: tail`. It cannot generally prove that arbitrary guards cover every integer. Even when two guards seem to cover a range to a human, an unguarded final rule is normally still required.

A guard is not proof of exhaustiveness. `| value when value > 0 -> ...` covers only the path where the guard is true; zero and negative values still need handling. An explicit fallback result is more reliable than assuming input can never reach that point.

### A wildcard is useful and can hide information {#wildcard-tradeoff}

For open value spaces such as `int` or `string`, a final `_` is often a reasonable fallback. It matches every remaining value without binding a name. Use a named variable pattern instead if the result needs the original value.

A wildcard can also be too broad. When a later union type has a finite set of named cases, listing each case lets a new state trigger a compiler reminder; an early `_` swallows that feedback. Chapter 8 compares these choices with real domain states.

## Choose `if` or `match` {#choosing-branching}

Use the form that exposes the basis of the decision:

- for one direct Boolean condition choosing between two results, `if` is usually shortest;
- when branching by a literal, tuple, or list shape and binding components, `match` is natural;
- several mutually exclusive structural rules fit `match`;
- a long `match` consisting only of numeric ranges can still be readable, but its order must be clear;
- do not rewrite a clear `if` as `match true with` merely to look “more functional.”

Both forms produce values. The real criterion is which one makes the input space, priority, and omissions easiest to see.

## Run the shared example {#run-example}

From the directory containing the example, run:

```console
dotnet fsi --exec ch04-branching-patterns.fsx
```

You should see:

```text
Availability: available
Capacity bands: full, last seat, limited, available
Booking: Lin requested 3 seats
Queues: empty | one: Lin | next: Lin, then Ada
```

Compare each output line in order. Each branch function returns data and output is kept outside the function, so decision and display can be verified separately.

## Debugging: simulate each rule {#debugging}

When a result enters the wrong branch, record one input and execute mechanically:

1. what value and type does the match input expression produce?
2. does the first pattern match?
3. only if it matches, is the guard true?
4. if it fails, how does the next rule handle the same original input?
5. what type and value does the first successful right side produce?

When a type error points to a right side, compare every branch result type rather than only the tested input. When an exhaustiveness warning appears, look for the missing shape first. Do not immediately silence it with `_ -> failwith ...`; that merely postpones the omission until runtime.

If you cannot tell where a variable came from, check whether a pattern established it as a new local binding. Domain names such as `requested` are usually easier to trace than `x`.

## Exercises {#exercises}

Write the first successful rule for each input before running the script. An answer should explain why every skipped rule failed.

### Exercise 1: unify the result of `if` {#exercise-01}

For `availability`:

1. evaluate `availability 3` and `availability 0`;
2. write the types of the condition, both branches, and the whole function;
3. explain why `then` cannot return `"available"` while `else` only calls `printfn`;
4. state when omitting `else` is legal.

### Exercise 2: trace rules and guards {#exercise-02}

For `capacityBand -2`, `capacityBand 0`, `capacityBand 1`, `capacityBand 5`, and `capacityBand 6`, write the first successful rule and result.

Then answer: what happens if `_ -> "available"` moves to the first rule? If only two guarded variable rules remain, why can the compiler not treat them as reliably exhaustive?

### Exercise 3: decompose composite input {#exercise-03}

Write a `classifyRequest` function that examines remaining and requested seats together. It should return `"invalid"` for a nonpositive request, `"accepted"` when a positive request fits, and `"too large"` otherwise. Then:

1. explain the results for `(5, 0)`, `(5, 3)`, and `(2, 3)`;
2. explain why the invalid-request rule must precede requested-not-greater-than-remaining;
3. write the full function type;
4. for the queue `[ "Lin"; "Ada" ]` and a four-item queue, state which `describeQueue` pattern matches and what `_` denotes.

[Read the chapter solutions](../solutions/ch-04-branching-patterns).

## Summary {#summary}

- Both `if` and `match` are expressions. The selected branch becomes the result, and branch types must unify.
- An `if` condition must be `bool`; there is no general truthiness conversion, and omitting `else` is only for `unit`.
- A `match` chooses the first rule whose pattern matches and guard is true, so rule order is semantic.
- A variable pattern matches and binds any compatible value; it does not compare against an outer value of the same name.
- Tuple patterns decompose positions, while list patterns distinguish empty, fixed-length, and head-tail structure.
- A guard adds a runtime Boolean constraint but cannot replace unguarded patterns as evidence of exhaustiveness.
- A wildcard is useful for an open value space but can hide future cases in a finite domain model.

The next chapter moves from list shape to list transformation. `map`, `filter`, `choose`, and pipelines will compose branch functions into readable data flow and be compared honestly with loops and mutable state.

## Vocabulary {#vocabulary}

- **pattern:** a rule that tests input shape, decomposes components, and establishes local bindings.
- **pattern matching:** checking a value against ordered patterns and choosing the first successful branch.
- **guard:** a `when` Boolean condition evaluated only after its pattern matches.
- **exhaustiveness:** the property that rules cover every possible shape of the input type.
- **wildcard pattern:** `_`, which matches any input without binding a name.
- **tuple:** one value that combines several fixed positions.
- **list:** an ordered immutable singly linked collection of elements of one type.

## Sources {#sources}

- [Microsoft Learn: `if...then...else` conditional expressions](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/conditional-expressions-if-then-else)
- [Microsoft Learn: `match` expressions and guards](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/match-expressions)
- [Microsoft Learn: Pattern matching](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/pattern-matching)
- [Microsoft Learn: Lists](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/lists)
