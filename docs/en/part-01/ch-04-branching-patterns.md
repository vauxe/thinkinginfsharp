---
title: "Chapter 4: Branching and Basic Patterns"
description: "Read if and match as value-producing expressions, then build safe branches with literal, variable, wildcard, tuple, and list patterns."
translationKey: part-01/ch-04-branching-patterns
---

# Chapter 4: Branching and Basic Patterns {#overview}

Imperative languages often describe branching as “choose which statement executes next.” In F#, both `if` and `match` are expressions: the selected branch supplies the result of the whole expression. Their branch results must therefore have compatible types.

An `if` selects between two results from a Boolean condition. A `match` compares one value with an ordered series of **patterns**. A pattern can recognize the shape of input and bind names to its components in the successful branch. Type checking and exhaustiveness checking can therefore participate in branch design.

This chapter uses tuples and lists only to build pattern intuition. Records and discriminated unions make exhaustiveness a domain-modeling tool in Chapters 7 and 8. List transformations wait until the next chapter.

## An `if` selects one result {#if-expression}

The shared script first maps remaining capacity to text:

```fsharp:line-numbers [ch04-branching-patterns.fsx]
let availability remaining =
    if remaining > 0 then "available" else "full"

printfn "Availability: %s" (availability 3)
```
Evaluation first computes `remaining > 0`. When it is `true`, the `then` branch runs; otherwise, the `else` branch runs. Exactly one branch supplies the value of the whole `if`, so `availability` returns `string`.

### The condition must actually be `bool` {#boolean-only}

F# requires an `if` condition to have type `bool`, such as `remaining > 0`, `name = "Lin"`, or a Boolean function call learned later. Integers, strings, lists, and objects use explicit predicates to express the intended question.

This rule writes intent into the source. A positive person count, an empty string, and a missing value are different questions, each represented by its own predicate.

### Both branches must unify to one type {#branch-types}

Because an `if` is one expression, the results of `then` and `else` must unify to one type. If one branch returns `string` and the other returns `int`, a caller cannot receive one static result type, so the compiler rejects the code.

The same rule covers effectful branches. A branch that calls `printfn` returns `unit`, so its peer must also return `unit`. When the decision produces data, let both branches return that data and print once outside; the decision then becomes easier to test.

### Omitting `else` is only for `unit` {#else-unit}

F# permits an omitted `else` for a `unit` expression, and the `then` branch must also return `unit`. This fits an effect-only action such as recording a message when a condition holds. Data-producing decisions use explicit results for both outcomes.

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

Patterns form an ordered decision list. An early broad rule intercepts later specific rules. A leading wildcard `_` matches every input, so the compiler normally reports later rules as unreachable.

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

A lowercase variable pattern such as `value` matches any value and creates a new binding. A guard compares input with an existing runtime value. Reading lowercase names as binders prevents one of the subtler early `match` errors.

## A guard adds a Boolean constraint {#guards}

A numeric range is not one literal pattern. Bind the value with a variable pattern, then check it in a `when` guard:

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

A guard runs only after its associated pattern matches. A true guard selects that rule; a false guard continues to the next one. Keep guards easy to understand and preferably effect-free so rule order remains easy to reason about.

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

A pattern handles shape and binding; a value test such as `seats = 1` remains a Boolean expression. This example uses `if` for singular versus plural because the question is one direct Boolean choice. Reserve `match` for decisions that benefit from pattern structure.

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
`[ only ]` matches only a list of length one. `first :: second :: _` associates to the right as `first :: (second :: _)`. It takes the first item and then the second; the wildcard `_` accepts the rest without binding it. The pattern therefore matches any list with at least two elements.

Use `[ first; second ]` for a list of exactly two elements. Use `first :: second :: _` for the first two elements of a longer-or-equal list. The next chapter covers construction and transformation, and Chapter 6 uses `head :: tail` for structural recursion.

## Exhaustiveness makes omissions visible {#exhaustiveness}

A set of rules is **exhaustive** when every possible shape of the input type can reach at least one rule. Otherwise, a runtime input may cause match failure. The compiler warns about clearly incomplete matches; the book projects treat warnings as errors so omissions must be addressed.

### What the compiler can prove {#compiler-checks}

The compiler can analyze finite structural cases such as a list being empty or `head :: tail`. Arbitrary guards over integers lie outside that structural proof, so guarded ranges normally finish with an unguarded rule.

A guard contributes a runtime condition rather than an exhaustiveness proof. `| value when value > 0 -> ...` covers the positive path; an explicit fallback handles zero and negative values.

### A wildcard is useful and can hide information {#wildcard-tradeoff}

For open value spaces such as `int` or `string`, a final `_` is often a reasonable fallback. It matches every remaining value without binding a name. Use a named variable pattern instead if the result needs the original value.

A wildcard can also be too broad. When a later union type has a finite set of named cases, listing each case lets a new state trigger a compiler reminder; an early `_` swallows that feedback. Chapter 8 compares these choices with real domain states.

## Choose `if` or `match` {#choosing-branching}

Use the form that exposes the basis of the decision:

- for one direct Boolean condition choosing between two results, `if` is usually shortest;
- when branching by literal, tuple, or list structure and binding components, `match` is natural;
- several mutually exclusive structural rules fit `match`;
- a long `match` consisting only of numeric ranges can still be readable, but its order must be clear;
- keep a clear `if` when the decision is one Boolean choice.

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

## Simulate each rule {#debugging}

When a result enters the wrong branch, record one input and execute mechanically:

1. what value and type does the match input expression produce?
2. does the first pattern match?
3. only if it matches, is the guard true?
4. if it fails, how does the next rule handle the same original input?
5. what type and value does the first successful right side produce?

When a type error points to a right side, compare every branch result type. When an exhaustiveness warning appears, identify the missing case and give it an explicit result. A blanket `_ -> failwith ...` merely moves the omission to runtime.

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

## Key takeaways {#summary}

- Both `if` and `match` are expressions. The selected branch becomes the result, and branch types must unify.
- An `if` condition has type `bool`; a two-branch form returns a unified result type, while the one-branch form returns `unit`.
- A `match` chooses the first rule whose pattern matches and guard is true, so rule order is semantic.
- A variable pattern matches any compatible value and creates a branch-local binding, even when an outer value shares its name.
- Tuple patterns decompose positions, while list patterns distinguish empty, fixed-length, and head-tail structure.
- A guard adds a runtime Boolean constraint; unguarded patterns provide the compiler's structural evidence of exhaustiveness.
- A wildcard is useful for an open value space but can hide future cases in a finite domain model.

The next chapter moves from list shape to list transformation. `map`, `filter`, `choose`, and pipelines will compose branch functions into readable data flow and be compared honestly with loops and mutable state.

## Sources {#sources}

- [Microsoft Learn: `if...then...else` conditional expressions](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/conditional-expressions-if-then-else)
- [Microsoft Learn: `match` expressions and guards](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/match-expressions)
- [Microsoft Learn: Pattern matching](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/pattern-matching)
- [Microsoft Learn: Lists](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/lists)
