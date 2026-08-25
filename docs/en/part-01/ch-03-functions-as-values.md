---
title: "Chapter 3: Functions Are Values"
description: "Start from function values to master application, arrow types, lambdas, higher-order functions, currying, tupled parameters, partial application, and automatic generalization."
translationKey: part-01/ch-03-functions-as-values
---

# Chapter 3: Functions Are Values {#overview}

F# does not confine functions to special code sections that can only be declared and called. A function is a value: `let` can bind it to a name, another function can receive it as an argument, and a function can return it as a result. **Function application** is itself an expression that produces a value.

This idea connects the first two chapters. Literals produce data values, function values describe computations from inputs to results, and higher-order functions turn behavior itself into composable data. Reading arrow types accurately before learning pipelines prevents `|>` from becoming decorative syntax.

## What you will be able to do {#outcomes}

By the end of this chapter, you should be able to:

- define, bind, and apply named and anonymous functions;
- distinguish parameters in a definition from arguments at a call site;
- read right-associated arrow types correctly;
- distinguish a curried function from a function that accepts a tuple;
- use partial application to obtain a function that awaits remaining arguments;
- write higher-order functions that receive or return functions;
- read the automatic-generalization intuition represented by type variables such as `'a`.

This chapter uses only simple arithmetic, strings, and minimal tuples. Collection functions such as `map` and `filter`, along with pipelines, arrive in Chapter 5. Chapter 11 handles the value restriction and explicit constraints on generalization.

## A function binding is still a binding {#function-binding}

Start with a function that calculates the amount for a booking line:

```fsharp:line-numbers [ch03-functions-as-values.fsx]
let lineTotal unitPrice seats = unitPrice * decimal seats
let standardLineTotal = lineTotal 19.50m
let totalForThree = standardLineTotal 3

printfn "Curried total: %M" totalForThree
```
`let lineTotal unitPrice seats = ...` establishes a binding between the name `lineTotal` and a function value. `unitPrice` and `seats` are **parameters**: they stand for inputs the function will later receive. In the call `lineTotal 19.50m 3`, `19.50m` and `3` are **arguments**: the values actually supplied by the caller.

Defining a function does not immediately evaluate its body. Only after the function receives enough arguments does the body `unitPrice * decimal seats` run. The value of the body's final expression is the function result; an ordinary F# function needs no `return` here.

### Apply functions with spaces {#application}

F# primarily expresses function application with spaces: the function value comes first, followed by its arguments. `standardLineTotal 3` supplies the integer `3` to the function value `standardLineTotal`.

Application associates to the left and binds more tightly than most infix operators. Parentheses control grouping; they are not required around every call. Read `transform (transform value)` in two steps:

1. compute the inner `transform value`;
2. supply that result to the outer `transform`.

Putting comma-separated values in parentheses constructs a tuple and may change the meaning entirely. `lineTotal (19.50m, 3)` is not another spelling of a call to the curried `lineTotal`; it supplies one tuple argument and therefore does not type-check.

### A function body produces a result {#body-result}

A function body may contain local `let` bindings and effects, but the final expression still determines the result type. If the last expression is `printfn`, the result is `unit`; if it is the amount calculation, the result is `decimal`.

Being a function value does not make a function pure. Its body can still read a clock, write a file, or mutate controlled state. Purity comes from a design that depends only on explicit inputs and causes no observable effects, not from a property automatically granted by `let`.

## An arrow denotes a function type {#function-types}

FSI infers this signature for `lineTotal`:

```text
val lineTotal: decimal -> int -> decimal
```

The arrow `->` separates input from result. It associates to the right, so the signature is equivalent to:

```text
decimal -> (int -> decimal)
```

After receiving a `decimal`, the result is not the final amount but an `int -> decimal` function. Supplying the `int` then produces a `decimal`. That is why `standardLineTotal` has this signature:

```text
val standardLineTotal: int -> decimal
```

When a type signature omits parameter names, it expresses only the data shape. Good source names still matter: `decimal -> int -> decimal` alone cannot tell a reader whether the first number is a unit price or a discount rate.

### Currying and successive application {#currying}

F# `let`-bound functions normally use **curried** form: multiple visible parameters are represented semantically as successive single-parameter functions. `lineTotal 19.50m 3` associates to the left as:

```text
(lineTotal 19.50m) 3
```

This is not a promise that an expensive intermediate object is allocated every time. It is the semantic model of function types and application. The compiler may optimize the concrete representation; callers should depend on type behavior, not guessed allocation.

### A tupled parameter has another shape {#tupled-parameters}

The same calculation can be written as a function receiving one pair:

```fsharp:line-numbers [ch03-functions-as-values.fsx]
let lineTotalTupled (unitPrice, seats) = unitPrice * decimal seats
let tupledTotal = lineTotalTupled (19.50m, 3)

printfn "Tupled total: %M" tupledTotal
```
Here `(unitPrice, seats)` is a tuple pattern that separates two positions from one argument. The signature is:

```text
decimal * int -> decimal
```

The `*` in a type denotes tuple composition, not multiplication. The curried version receives two successive arguments; the tupled version receives one argument containing two components. They happen to compute the same result, but they do not have the same function type.

Idiomatic `let`-bound functions usually favor currying because it supports partial application and higher-order composition. A tuple can be clear when the domain naturally treats the input as one grouped value. .NET method calls often contain parentheses and commas, but their CLR calling semantics should not be reduced to an ordinary tupled function; the interoperation chapter treats that boundary separately.

## Partial application preserves remaining work {#partial-application}

Supplying fewer than all arguments to a curried function produces a new function waiting for the rest. This is **partial application**, not an erroneous “incomplete call.”

In the first example, `lineTotal 19.50m` produces an `int -> decimal` function bound as `standardLineTotal`. The unit price is fixed, so future callers provide only a seat count. The service-fee example uses the same idea:

```fsharp:line-numbers [ch03-functions-as-values.fsx]
let addFee fee subtotal = subtotal + fee
let addServiceFee = addFee 2.00m
let finalTotal = addServiceFee totalForThree

printfn "With service fee: %M" finalTotal
```
`addFee` has type `decimal -> decimal -> decimal`. `addFee 2.00m` returns a `decimal -> decimal` function that can still use the supplied `2.00m` later. A function value together with surrounding values it retains forms the semantics of a **closure**. The runtime may optimize its representation, but the captured-value behavior remains.

Parameter order is therefore part of API design. Stable configuration that callers may fix in advance often belongs first, while frequently changing data that flows through a computation often belongs last. Chapter 13 treats pipeline-oriented argument order systematically; partial application gives the first evidence here.

## Anonymous functions create function values directly {#anonymous-functions}

A short function used only nearby may not need a name first. A `fun` expression creates an anonymous function directly:

```fsharp:line-numbers [ch03-functions-as-values.fsx]
let increment seats = seats + 1
let incrementAnonymous = fun seats -> seats + 1

printfn "Named and anonymous: %d, %d" (increment 3) (incrementAnonymous 3)
```
Read `fun seats -> seats + 1` as “receive seats and produce seats plus one.” The parameter pattern is left of the arrow, and the body expression is right of it. Both `increment` and `incrementAnonymous` infer the type `int -> int` and produce the same call result.

A name records intent and improves diagnostics, so brevity is not a reason to turn every function into an anonymous one. Anonymous functions fit local behavior, especially as an argument to another function. When the same logic appears in several places or names a domain concept, a named function is usually clearer.

## Higher-order functions compose behavior {#higher-order-functions}

A **higher-order function** does at least one of two things: it accepts a function value or returns one. Partial application already returned functions; this example accepts one:

```fsharp:line-numbers [ch03-functions-as-values.fsx]
let applyTwice transform value = transform (transform value)
let incrementedTwice = applyTwice increment 3

printfn "Applied twice: %d" incrementedTwice
```
`applyTwice` does not know the business meaning of `transform`. It requires only that the first transformation's output can be supplied to the same transformation again, so FSI infers:

```text
val applyTwice: ('a -> 'a) -> 'a -> 'a
```

The parentheses matter. The first argument itself has the function type `'a -> 'a`; the next argument is an `'a` value; and the result is still `'a`. Without parentheses, right association would describe a different shape.

Higher-order does not automatically mean better abstraction. If behavior does not genuinely vary, a direct call to a named function is often clearer. A higher-order function earns its place when it fixes stable structure and makes a changing policy an explicit input, not merely when it removes lines.

## Generic functions do not depend on one concrete type {#generic-functions}

Consider a function that neither inspects, changes, nor constructs its input:

```fsharp:line-numbers [ch03-functions-as-values.fsx]
let identity value = value
let unchangedNumber = identity 42
let unchangedText = identity "F#"

printfn "Identity values: %d, %s" unchangedNumber unchangedText
```
`identity` simply returns the value it receives, and its body imposes no concrete type. The compiler **automatically generalizes** its type to:

```text
val identity: 'a -> 'a
```

`'a` is a type variable. A call may replace it with a concrete type, but the input and output of that one call must have the same type. The same binding can therefore be used first with an `int` and then with a `string`; each call remains fully statically typed.

The three occurrences of the same `'a` in `applyTwice` likewise express a consistency constraint: the transformation's input and output and the value being transformed must align. Different letters such as `'a` and `'b` denote positions that need not have the same type.

Complete function definitions with explicit parameters can usually be generalized when it is safe, but not every expression containing a function can be generalized arbitrarily. Mutable state, partial applications, and complex values can expose the **value restriction**. Do not use tricks to silence that diagnostic yet; Chapter 11 gives the precise rules and remedies.

## Run the shared example {#run-example}

From the directory containing the example, run:

```console
dotnet fsi --exec ch03-functions-as-values.fsx
```

You should see:

```text
Curried total: 58.50
Tupled total: 58.50
Named and anonymous: 4, 4
Applied twice: 5
With service fee: 60.50
Identity values: 42, F#
```

Matching output does not prove matching function types. The curried and tupled versions both produce `58.50`, yet their signatures and partial-application abilities differ. Inspect both output and types when verifying a function.

## Debugging: parenthesize the application first {#debugging}

Function diagnostics often arise at an application boundary. Check them in this order:

1. inspect the function value's full signature in FSI;
2. mentally parenthesize arrow types by right association;
3. mentally parenthesize function application by left association;
4. distinguish successive application `a b` from one tuple `(a, b)`;
5. check whether partial application produced a function or already produced the final value.

If a diagnostic says a value was expected but a function was supplied, an argument is often still missing. If it says a value is not a function and cannot be applied, an earlier step may already have produced the final result that you then tried to call again.

When a long anonymous function produces a confusing diagnostic, bind it to a name temporarily and let FSI display its signature alone. Fix the type first, then decide whether to inline it. This is more effective than guessing inside nested parentheses.

## Exercises {#exercises}

Write the signature before calculating output. Do not decide that a function is curried merely from the visible number of parameter names; use arrows and tuple types.

### Exercise 1: decode the arrows {#exercise-01}

Explain these four signatures and add association parentheses to every arrow:

1. `lineTotal: decimal -> int -> decimal`;
2. `standardLineTotal: int -> decimal`;
3. `applyTwice: ('a -> 'a) -> 'a -> 'a`;
4. `identity: 'a -> 'a`.

For each signature, state what it receives in sequence, what it produces, and what repeated occurrences of `'a` constrain.

### Exercise 2: pass behavior {#exercise-02}

Call `applyTwice` twice: once with the named function `increment`, and once with an equivalent anonymous function written at the call site. Start both calls from `3`.

Write the anonymous function, the two results, and the relevant types. Then explain why `applyTwice` cannot directly accept a function that converts an `int` to a `string`.

### Exercise 3: choose a parameter shape {#exercise-03}

Compare `lineTotal` with `lineTotalTupled`:

1. write each full type and a valid call;
2. when fixing only the unit price `19.50m`, which version can be partially applied directly?
3. what value does `addServiceFee` retain, and what is its remaining input type?
4. if unit price and seat count always travel as one indivisible coordinate-like pair in the domain, why might the tupled version be clearer?

[Read the chapter solutions](../solutions/ch-03-functions-as-values).

## Summary {#summary}

- A function is a value. Defining one establishes a binding; applying one evaluates its body and produces a result.
- F# applies ordinary functions with spaces; application associates left, while arrow types associate right.
- A curried function represents successive single-parameter functions, and partial application returns a function awaiting the rest.
- A tupled function receives one composite value; `decimal * int -> decimal` is not `decimal -> int -> decimal`.
- An anonymous function uses `fun parameter -> body` to create a function value directly.
- A higher-order function receives or returns a function; a closure lets a returned function retain values from its definition environment.
- Automatic generalization uses type variables such as `'a` for definitions independent of a concrete type while preserving consistency among positions.

The next chapter lets function bodies choose. Both `if` and `match` are expressions that produce values, while patterns combine input shape with branch-local bindings.

## Vocabulary {#vocabulary}

- **function:** a value that accepts input and computes a result.
- **function application:** supplying an argument to a function value and evaluating its body.
- **parameter:** a name or pattern in a definition that receives input.
- **argument:** a value or expression actually supplied at a call site.
- **currying:** representing a multi-parameter computation as successive single-parameter functions.
- **tuple:** one value combining a fixed number of positional values, with component types joined by `*`.
- **partial application:** supplying only some arguments to obtain a function awaiting the rest.
- **anonymous function:** an unnamed function value created directly with `fun ... -> ...`.
- **higher-order function:** a function that receives or returns a function.
- **closure:** a function value and the definition-environment values retained for later calls.
- **automatic generalization:** safely promoting unknown types to instantiable type parameters.

## Sources {#sources}

- [Microsoft Learn: Functions](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/functions/)
- [Microsoft Learn: Lambda expressions and `fun`](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/functions/lambda-expressions-the-fun-keyword)
- [Microsoft Learn: Parameters and arguments](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/parameters-and-arguments)
- [Microsoft Learn: Type inference](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/type-inference)
- [Microsoft Learn: Automatic generalization and the value restriction](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/generics/automatic-generalization)
