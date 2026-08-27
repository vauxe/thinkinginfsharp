---
title: "Chapter 2: Values, Bindings, and Expressions"
description: "Distinguish values, let bindings, local shadowing, and expressions while learning to read F# basic types and inference results."
translationKey: part-01/ch-02-values-bindings-expressions
---

# Chapter 2: Values, Bindings, and Expressions {#overview}

Chapter 1 temporarily read `let eventName = ...` as “give a value a name.” We can now be precise: F# first evaluates the expression on the right, then the pattern on the left establishes a **binding**. A binding associates a name with a value. Repeated assignment requires an explicit mutable storage location.

This distinction changes how you read a program. Because ordinary bindings are not reassigned, you can trace data dependencies more directly through the source. Type inference combines those uses into compile-time constraints.

Functions become an important kind of value in the next chapter. Here, treat calls such as `decimal requestedSeats` as available operations; Chapter 3 will explain application syntax and function types.

## From values to bindings {#from-value-to-binding}

A **value** is the result of an expression that completes normally. The integer `40` is a value, the result of concatenating strings is a value, and functions will later be values too. Every value has a definite static type.

An **expression** is code evaluated to produce a result, such as `20 + 4`. Evaluation can also cause an observable effect. `printfn` writes output, yet it still returns the `unit` value `()`.

A **binding** is an association between a name and a value. Start with a group of bindings from the shared script:

```fsharp:line-numbers [ch02-values-bindings-expressions.fsx]
let eventName = "Functional Foundations"
let capacity = 40
let fillRatio = 0.45
let ticketPrice = 19.50m
let eventCode = 'F'
let registrationOpen = true
let noFurtherResult = ()

printfn "%s (%c): capacity=%d, fill=%.2f, open=%b" eventName eventCode capacity fillRatio registrationOpen
```
### How to read `let` {#read-let}

Read `let capacity = 40` in three steps:

1. `40` is an integer literal expression;
2. the compiler determines its type, and evaluation produces the value;
3. the pattern `capacity` gives that value a name.

The `=` here separates the left and right sides of a binding. In an ordinary expression, `=` tests structural equality. Updating an explicitly mutable location uses `<-`. Reading each symbol by its role keeps binding, comparison, and mutation distinct.

At the top of a module or script, `let` introduces a declaration. In a local scope, a sequence of `let` bindings and its following body form an expression. Both positions preserve the same core order: evaluate the right side, then make the new name visible in the following scope. An ordinary non-recursive name cannot be used before its definition; recursive bindings wait until Chapter 6.

### What immutable by default means {#immutability}

An ordinary `let` binding is immutable by default: its name remains associated with the value established at creation. Readers do not need to track later assignments to that name, and concurrent code has one fewer source of shared change.

Keep two ideas separate: **whether a binding can be reassigned is independent of whether a referenced object can change internally**. A name can stay bound to the same .NET object while that object's mutable fields change. Later chapters discuss collections and controlled mutation separately.

F# also supports `let mutable`, because local counters, array updates, and some interoperation genuinely need changing storage. Choose it deliberately and keep its scope small. For now, recognize the syntax; Chapter 5 will compare transformation and iteration on the same problem.

## Scope and shadowing {#scope-shadowing}

Scope determines where a name is visible. F# uses indentation for much of its local structure. The right side of `normalizedCapacity` below contains two local bindings:

```fsharp:line-numbers [ch02-values-bindings-expressions.fsx]
let normalizedCapacity =
    let capacity = 20
    let capacity = capacity + 4
    capacity

printfn "Normalized capacity: %d; outer capacity: %d" normalizedCapacity capacity
```
The second local `capacity` **shadows** the first local binding with that name. It uses the earlier value to compute `24`, then establishes a new binding. For the rest of that local scope, the name `capacity` resolves to the new value.

When the local expression ends, both local bindings leave scope, while the script-level `capacity` is still `40`. The output therefore demonstrates both that `normalizedCapacity` is `24` and that the outer `capacity` remains `40`.

Shadowing can express a short, linear refinement and is common across successive FSI experiments. When same-named stages are far apart, distinct stage names make the current stage easier to identify. Treat shadowing as a scope rule and choose it for local clarity.

## Types remain static {#types-are-static}

Every value and expression has a compile-time type, including those whose source omits annotations. Source runs after type checking succeeds. Inference supplies information that context already determines reliably.

### Common basic types {#basic-types}

Learn the frequent types first instead of memorizing every numeric width:

| F# type | Representative literal | Meaning |
| --- | --- | --- |
| `int` | `40` | 32-bit signed integer; the common default for an integer with no other context |
| `int64` | `40L` | 64-bit signed integer; the `L` suffix distinguishes it |
| `float` | `0.45` | 64-bit binary floating point, .NET `System.Double` |
| `decimal` | `19.50m` | A base-10 number with finite precision and scale, often used for decimal business quantities; suffix `m` or `M` |
| `bool` | `true` | A truth value, either `true` or `false` |
| `char` | `'F'` | One UTF-16 code unit, written with single quotes |
| `string` | `"F#"` | A .NET string, written with double quotes |
| `unit` | `()` | A type whose only value is `()` |

F# also has other signed and unsigned integer widths, `float32`, and `bigint`. Use them when an external protocol, a required range, or measured performance calls for them.

A `float` covers a wide range, but it approximates most decimal fractions in binary. A `decimal` exactly represents many everyday base-10 fractions whose coefficient and scale fit its finite representation, such as `19.50`, so it is often suitable for monetary rules. Their distinct representations support different numeric requirements.

### Inference comes from constraints {#inference-constraints}

The compiler combines several kinds of constraints:

- literals and their suffixes provide candidate types;
- operators require compatible operand types;
- parameters and results of known operations constrain surrounding expressions;
- annotations add explicit constraints;
- later uses can make an earlier unknown type definite.

For example, an unsuffixed `40` defaults to `int` in this context; `0.45` is inferred as `float`; and the suffix on `19.50m` makes `ticketPrice` a `decimal`. When two uses require incompatible types, the compiler reports the conflicting constraints and leaves conversion policy to the programmer.

### Annotations and conversions solve different problems {#annotations-conversions}

The next region shows both:

```fsharp:line-numbers [ch02-values-bindings-expressions.fsx]
let requestedSeats: int = 3
let pricePerSeat: decimal = 19.50m
let totalPrice = decimal requestedSeats * pricePerSeat

printfn "Ticket total: %M" totalPrice
```
`requestedSeats: int` and `pricePerSeat: decimal` are **type annotations**. They constrain existing expressions to have the written types; an annotation does not change a value at runtime.

`decimal requestedSeats` is an **explicit conversion**: it produces a new `decimal` value from the `int` value. Both sides of the multiplication are then `decimal`. F# does not automatically widen these existing values in ordinary numeric arithmetic. Writing the conversion explicitly keeps decisions about sign, range, precision, and rounding visible.

Write annotations where they communicate intent, stabilize a public boundary, or give the compiler missing context. Repeating an already obvious type on every local value adds noise. Put conversions where the representation genuinely changes.

### Reading type signatures {#read-signatures}

Submitting the preceding bindings individually in FSI produces output like this:

```text
val capacity: int = 40
val fillRatio: float = 0.45
val ticketPrice: decimal = 19.50M
val eventCode: char = 'F'
val noFurtherResult: unit = ()
```

Read the colon as “has type”: `capacity` has type `int`. The part to the right of the equals sign is FSI's display of the current value, not part of the type. FSI may display a `decimal` with uppercase `M`; it has the same suffix meaning as lowercase `m` in the source.

Compiler diagnostics also describe incompatible constraints as an expected type and an actual type. Find the colon, the type names, and the conflicting expression first, then decide what the model should be. Add a conversion only where the model intentionally changes representation.

## The boundary between expressions and `unit` {#expressions-and-unit}

An expression-oriented F# file still contains declarations. Top-level `let`, type, and module forms are declarations; their right sides and bodies are made from expressions. Conditionals, matches, and local bindings all yield results that later computation can use. Later chapters will apply this property one construct at a time.

When expressions run in sequence, a non-final expression should normally return `unit`. Otherwise, ignoring a meaningful value is often a mistake, and the compiler may warn about it. `printfn` fits such a position because its meaningful behavior is output and its returned value is `()`.

This rule also explains why “expressions have values” does not conflict with “programs have side effects.” A type records the result passed to later computation. Output, file writes, and network requests are effects that happen during evaluation. You need to read both pieces of information.

## Run the shared example {#run-example}

From the repository root, run:

```console
dotnet fsi --exec examples/scripts/ch02-values-bindings-expressions.fsx
```

You should see:

```text
Functional Foundations (F): capacity=40, fill=0.45, open=true
Ticket total: 58.50
Normalized capacity: 24; outer capacity: 40
```

Compare these deterministic outputs in this order. Formatting in the script changes only the display, not the types of `fillRatio` or `totalPrice`.

## Trace the first conflicting constraint {#debugging}

When a type error appears, narrow it in this order:

1. start with the smallest expression named by the diagnostic;
2. inspect the types of its input values separately in FSI;
3. identify constraints supplied by literal suffixes, operators, and known APIs;
4. decide whether the data model really needs one common type;
5. add an annotation or explicit conversion only where the model deliberately changes type or representation.

A common mistake is to blame every error on inference “guessing wrong.” Inference has no preference of its own; it solves the constraints provided by the source. The real problem may be using a person count as text in arithmetic, choosing `float` for a monetary rule, or simply omitting `m` from a literal.

If shadowing is confusing, mark the indentation range of each name. When you cannot state in one sentence what the old and new bindings represent, distinct names are usually more effective than another comment.

## Exercises {#exercises}

Write down the types and evaluation process independently, then run a temporary copy. A good answer explains why the compiler accepts or rejects the code.

### Exercise 1: read types instead of guessing {#exercise-01}

For the `basic-values` region:

1. write the type of each of the seven bindings;
2. explain why `0.45` and `19.50m` do not have the same type;
3. explain why `eventCode` and `eventName` do not have the same type;
4. decide whether omitting every annotation makes the program dynamically typed.

Verify the answers in FSI, and compare whether FSI's display of each value has exactly the same spelling as its source literal.

### Exercise 2: repair a representation boundary {#exercise-02}

Suppose an external input supplies a person count as the string `"24"`. Explain why adding the integer `1` directly fails, then:

1. convert the text explicitly to `int` at the boundary;
2. compute the next attendee count;
3. write the types of all three names and the final output;
4. state where the risk of conversion failure has been left for now.

This chapter handles only valid input. The chapters on `option`, `Result`, and exceptions will establish complete failure models.

### Exercise 3: trace shadowing {#exercise-03}

Explain the `local-shadowing` region one line at a time:

1. which value does `capacity` denote while each right side is evaluated?
2. what is the final value of `normalizedCapacity`?
3. why is the outer `capacity` in the output still `40`?
4. how many bindings does the region create, and how many existing values does it mutate?

[Read the chapter solutions](../solutions/ch-02-values-bindings-expressions).

The next chapter brings functions into this model. Functions are values, application is an expression, and arrow types extend data dependencies into composable behavior.

## Sources {#sources}

- [Microsoft Learn: Values](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/values/)
- [Microsoft Learn: let bindings](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/functions/let-bindings)
- [Microsoft Learn: Type inference](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/type-inference)
- [Microsoft Learn: Basic types](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/basic-types)
- [Microsoft Learn: Literals](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/literals)
- [Microsoft Learn: shadowing example in A Tour of F#](https://learn.microsoft.com/en-us/dotnet/fsharp/tour)
