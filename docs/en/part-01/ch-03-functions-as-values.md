---
title: "Chapter 3: Functions Are Values"
description: "Start from function values to master application, arrow types, lambdas, higher-order functions, currying, tupled parameters, partial application, and automatic generalization."
translationKey: part-01/ch-03-functions-as-values
---

# Chapter 3: Functions Are Values {#overview}

A function is an ordinary F# value: `let` can bind it to a name, another function can receive it as an argument, and a function can return it as a result. **Function application** is itself an expression that produces a value.

This idea connects the first two chapters. Literals produce data values, function values describe computations from inputs to results, and higher-order functions turn behavior itself into composable data. Reading arrow types accurately before learning pipelines prevents `|>` from becoming decorative syntax.

This chapter uses only simple arithmetic, strings, and minimal tuples. Collection functions such as `map` and `filter`, along with pipelines, arrive in Chapter 5. Chapter 11 handles the value restriction and explicit constraints on generalization.

## A function binding is still a binding {#function-binding}

Start with a self-contained booking-total example. The unit price is a `decimal`, the seat count is an `int`, and the code fixes the price before calculating the amount for three seats:

```fsharp:line-numbers
let lineTotal unitPrice seats = unitPrice * decimal seats
let standardLineTotal = lineTotal 19.50m
let totalForThree = standardLineTotal 3

printfn "Curried total: %M" totalForThree
```

It prints `Curried total: 58.50`.

`let lineTotal unitPrice seats = ...` establishes a binding between the name `lineTotal` and a function value. `unitPrice` and `seats` are **parameters**: they stand for inputs the function will later receive. In the call `lineTotal 19.50m 3`, `19.50m` and `3` are **arguments**: the values actually supplied by the caller.

Defining a function creates its function value. The body `unitPrice * decimal seats` runs after the function receives enough arguments. Its final expression supplies the result directly, so an ordinary F# function uses that value in place of a `return` statement.

### Apply functions with spaces {#application}

F# primarily expresses function application with spaces: the function value comes first, followed by its arguments. `standardLineTotal 3` supplies the integer `3` to the function value `standardLineTotal`.

Application associates to the left and binds more tightly than most infix operators. Parentheses control grouping; they are not required around every call. Read `transform (transform value)` in two steps:

1. compute the inner `transform value`;
2. supply that result to the outer `transform`.

Putting comma-separated values in parentheses constructs a tuple and changes the argument form. The curried `lineTotal` expects two successive arguments, while `lineTotal (19.50m, 3)` supplies one tuple argument and therefore produces a type error.

### A function body produces a result {#body-result}

A function body may contain local `let` bindings and effects, but the final expression still determines the result type. If the last expression is `printfn`, the result is `unit`; if it is the amount calculation, the result is `decimal`.

A function value may be pure or effectful. Its body can read a clock, write a file, or mutate controlled state. A pure function depends only on its inputs and reports its result through the returned value; `let` itself only establishes the binding.

## An arrow denotes a function type {#function-types}

FSI infers this signature for `lineTotal`:

```text
val lineTotal: decimal -> int -> decimal
```

The arrow `->` separates input from result. It associates to the right, so the signature is equivalent to:

```text
decimal -> (int -> decimal)
```

After receiving a `decimal`, the function returns an `int -> decimal` function. Supplying the `int` then produces the final `decimal`. That is why `standardLineTotal` has this signature:

```text
val standardLineTotal: int -> decimal
```

When a type signature omits parameter names, it expresses only the data shape. Good source names still matter: `decimal -> int -> decimal` alone cannot tell a reader whether the first number is a unit price or a discount rate.

### Currying and successive application {#currying}

F# `let`-bound functions normally use **curried** form: multiple visible parameters are represented semantically as successive single-parameter functions. `lineTotal 19.50m 3` associates to the left as:

```text
(lineTotal 19.50m) 3
```

This describes the semantic model of function types and application. The compiler may optimize the concrete representation, including intermediate allocations; callers should rely on type behavior and use measurements for allocation claims.

### A tupled parameter has another shape {#tupled-parameters}

The same calculation can be written as a function receiving one pair:

```fsharp:line-numbers
let lineTotalTupled (unitPrice, seats) = unitPrice * decimal seats
let tupledTotal = lineTotalTupled (19.50m, 3)

printfn "Tupled total: %M" tupledTotal
```

This block also runs on its own and produces the same amount, `58.50`.

Here `(unitPrice, seats)` is a tuple pattern that separates two positions from one argument. The signature is:

```text
decimal * int -> decimal
```

The `*` in a type denotes tuple composition. The curried version receives two successive arguments; the tupled version receives one argument containing two components. They compute the same result through different function types.

Idiomatic `let`-bound functions usually favor currying because it supports partial application and higher-order composition. A tuple can be clear when the problem naturally treats several values as one grouped input. .NET method calls also use parentheses and commas, but that notation does not make every method an ordinary tupled F# function; the interoperation chapter explains the distinction.

## Partial application preserves remaining work {#partial-application}

Supplying fewer than all arguments to a curried function produces a new function waiting for the rest. This is **partial application**, not an erroneous “incomplete call.”

In the first example, `lineTotal 19.50m` produces an `int -> decimal` function bound as `standardLineTotal`. The unit price is fixed, so later calls provide only a seat count. The next block continues from that example's `totalForThree` value and adds a fixed service fee:

```fsharp:line-numbers
let addFee fee subtotal = subtotal + fee
let addServiceFee = addFee 2.00m
let finalTotal = addServiceFee totalForThree

printfn "With service fee: %M" finalTotal
```

Run it after the first block and the final line prints `With service fee: 60.50`.

`addFee` has type `decimal -> decimal -> decimal`. `addFee 2.00m` returns a `decimal -> decimal` function that can still use the supplied `2.00m` later. A function value together with the surrounding values it retains is a **closure**. The runtime may optimize its representation, but the captured-value behavior remains.

Parameter order is therefore part of API design. Stable configuration that callers may fix in advance often belongs first, while frequently changing data that flows through a computation often belongs last. Chapter 13 treats pipeline-oriented argument order systematically; this partial-application example shows why.

## Anonymous functions create function values directly {#anonymous-functions}

A short function used only nearby may not need a name first. A `fun` expression creates an anonymous function directly:

```fsharp:line-numbers
let increment seats = seats + 1
let incrementAnonymous = fun seats -> seats + 1

printfn "Named and anonymous: %d, %d" (increment 3) (incrementAnonymous 3)
```

This block runs on its own; both calls produce `4`.

Read `fun seats -> seats + 1` as “receive seats and produce seats plus one.” The parameter pattern is left of the arrow, and the body expression is right of it. Both `increment` and `incrementAnonymous` infer the type `int -> int` and produce the same call result.

A name records intent and improves diagnostics, so brevity is not a reason to turn every function into an anonymous one. Anonymous functions fit local behavior, especially as an argument to another function. When the same logic appears in several places or names a domain concept, a named function is usually clearer.

## Higher-order functions compose behavior {#higher-order-functions}

A **higher-order function** does at least one of two things: it accepts a function value or returns one. Partial application already returned functions. The next block continues from the preceding definition of `increment` and demonstrates accepting a function:

```fsharp:line-numbers
let applyTwice transform value = transform (transform value)
let incrementedTwice = applyTwice increment 3

printfn "Applied twice: %d" incrementedTwice
```

Run after the preceding block, it prints `Applied twice: 5`.

`applyTwice` does not know the business meaning of `transform`. It requires only that the first transformation's output can be supplied to the same transformation again, so FSI infers:

```text
val applyTwice: ('a -> 'a) -> 'a -> 'a
```

The parentheses matter. The first argument itself has the function type `'a -> 'a`; the next argument is an `'a` value; and the result is still `'a`. Without parentheses, right association would describe a different shape.

Use a higher-order function when behavior genuinely varies and the function can keep stable structure while accepting that policy as explicit input. For fixed behavior, a direct call to a named function is often clearer. This criterion ties the abstraction to meaning rather than line count.

## Generic functions do not depend on one concrete type {#generic-functions}

Consider a function that neither inspects, changes, nor constructs its input:

```fsharp:line-numbers
let identity value = value
let unchangedNumber = identity 42
let unchangedText = identity "F#"

printfn "Identity values: %d, %s" unchangedNumber unchangedText
```

This self-contained block prints `Identity values: 42, F#`.

`identity` simply returns the value it receives, and its body imposes no concrete type. The compiler **automatically generalizes** its type to:

```text
val identity: 'a -> 'a
```

`'a` is a type variable. A call may replace it with a concrete type, but the input and output of that one call must have the same type. The same binding can therefore be used first with an `int` and then with a `string`; each call remains fully statically typed.

The three occurrences of the same `'a` in `applyTwice` likewise express a consistency constraint: the transformation's input and output and the value being transformed must align. Different letters such as `'a` and `'b` denote positions that need not have the same type.

Complete function definitions with explicit parameters can usually be generalized when it is safe. Mutable state, partial applications, and complex values may instead expose the **value restriction**. When that diagnostic appears, Chapter 11 provides the precise rule and an explicit repair.

## Exercises {#exercises}

Write the signature before calculating output. Use arrows and tuple types to determine whether a function is curried.

### Exercise 1: decode the arrows {#exercise-01}

Explain these four signatures and add association parentheses to every arrow:

1. `lineTotal: decimal -> int -> decimal`;
2. `standardLineTotal: int -> decimal`;
3. `applyTwice: ('a -> 'a) -> 'a -> 'a`;
4. `identity: 'a -> 'a`.

For each signature, state what it receives in sequence, what it produces, and what repeated occurrences of `'a` constrain.


::: details Answer

| Name | Parenthesized type | Reading |
| --- | --- | --- |
| `lineTotal` | `decimal -> (int -> decimal)` | Receives a unit price and returns a function that receives seats and produces an amount |
| `standardLineTotal` | `int -> decimal` | Receives seats and produces an amount whose unit price is already fixed |
| `applyTwice` | `('a -> 'a) -> ('a -> 'a)` | Receives a type-preserving function and returns another function from `'a` to `'a` |
| `identity` | `'a -> 'a` | Receives a value of any one type and returns a value of that same type |

You can also read `applyTwice` position by position as `('a -> 'a) -> 'a -> 'a`: give it the transformation, then the value, then obtain the result. Right association makes the last two positions the returned `'a -> 'a` function. Repeated `'a` requires all positions in one instantiation to agree; it does not mean that each position independently accepts any type.

The first argument to `lineTotal` must be `decimal`, and the second must be `int`. Supplying the first produces a function shaped like `standardLineTotal`, not an amount.

:::

### Exercise 2: pass behavior {#exercise-02}

Call `applyTwice` twice: once with the named function `increment`, and once with an equivalent anonymous function written at the call site. Start both calls from `3`.

Write the anonymous function, the two results, and the relevant types. Then explain why `applyTwice` cannot directly accept a function that converts an `int` to a `string`.


::: details Answer

The named and anonymous functions are:

```fsharp:line-numbers
let increment seats = seats + 1
let incrementAnonymous = fun seats -> seats + 1

printfn "Named and anonymous: %d, %d" (increment 3) (incrementAnonymous 3)
```
The named call in the example is:

```fsharp:line-numbers
let applyTwice transform value = transform (transform value)
let incrementedTwice = applyTwice increment 3

printfn "Applied twice: %d" incrementedTwice
```
Read the equivalent anonymous call as `applyTwice (fun seats -> seats + 1) 3`; it also produces `5`. The anonymous function and `increment` both have type `int -> int`, so this call instantiates `'a` in `applyTwice` as `int`.

An `int -> string` function cannot be used directly because the first transformation would produce a `string`, while the second call would still require an `int`. `applyTwice` requires `'a -> 'a`, not `'a -> 'b`. If the domain needs two different transformations in sequence, define a function whose type describes those stages rather than weakening this consistency.

:::

### Exercise 3: choose a parameter form {#exercise-03}

Compare `lineTotal` with `lineTotalTupled`:

1. write each full type and a valid call;
2. when fixing only the unit price `19.50m`, which version can be partially applied directly?
3. what value does `addServiceFee` retain, and what is its remaining input type?
4. if unit price and seat count always travel as one indivisible input group, why might the tupled version be clearer?


::: details Answer

The two runnable definitions are:

```fsharp:line-numbers
let lineTotal unitPrice seats = unitPrice * decimal seats
let standardLineTotal = lineTotal 19.50m
let totalForThree = standardLineTotal 3

printfn "Curried total: %M" totalForThree
```
```fsharp:line-numbers
let lineTotalTupled (unitPrice, seats) = unitPrice * decimal seats
let tupledTotal = lineTotalTupled (19.50m, 3)

printfn "Tupled total: %M" tupledTotal
```
The curried version has type `decimal -> int -> decimal` and is called as `lineTotal 19.50m 3`. The tupled version has type `decimal * int -> decimal` and is called as `lineTotalTupled (19.50m, 3)`. Only the former can directly fix the price with `lineTotal 19.50m`, producing `int -> decimal`.

`addServiceFee` retains `2.00m`; its remaining input is a subtotal, so its type is `decimal -> decimal`. Semantically, the function forms a closure. If unit price and seat count form one domain value, the tupled input states “accept only a complete pair.” Partial application is not the only design criterion.

:::


The next chapter lets function bodies choose. Both `if` and `match` are expressions that produce values, while patterns combine input structure with branch-local bindings.

## Sources {#sources}

- [Microsoft Learn: Functions](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/functions/)
- [Microsoft Learn: Lambda expressions and `fun`](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/functions/lambda-expressions-the-fun-keyword)
- [Microsoft Learn: Parameters and arguments](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/parameters-and-arguments)
- [Microsoft Learn: Type inference](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/type-inference)
- [Microsoft Learn: Automatic generalization and the value restriction](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/generics/automatic-generalization)
