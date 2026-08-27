---
title: "Chapter 3 Solutions"
description: "Reasoning about function types, lambdas, higher-order functions, currying, tupled parameters, and partial application."
translationKey: solutions/ch-03-functions-as-values
---

# Chapter 3 Solutions {#overview}

Check the type structure before checking the number. Equal output from two calls does not mean they receive arguments in the same way.

[Return to Chapter 3](../part-01/ch-03-functions-as-values).

## Exercise 1: decode the arrows {#exercise-01}

| Name | Parenthesized type | Reading |
| --- | --- | --- |
| `lineTotal` | `decimal -> (int -> decimal)` | Receives a unit price and returns a function that receives seats and produces an amount |
| `standardLineTotal` | `int -> decimal` | Receives seats and produces an amount whose unit price is already fixed |
| `applyTwice` | `('a -> 'a) -> ('a -> 'a)` | Receives a type-preserving function and returns another function from `'a` to `'a` |
| `identity` | `'a -> 'a` | Receives a value of any one type and returns a value of that same type |

You can also read `applyTwice` position by position as `('a -> 'a) -> 'a -> 'a`: give it the transformation, then the value, then obtain the result. Right association makes the last two positions the returned `'a -> 'a` function. Repeated `'a` requires all positions in one instantiation to agree; it does not mean that each position independently accepts any type.

The first argument to `lineTotal` must be `decimal`, and the second must be `int`. Supplying the first produces a function shaped like `standardLineTotal`, not an amount.

## Exercise 2: pass behavior {#exercise-02}

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

## Exercise 3: choose a parameter form {#exercise-03}

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

## What to notice {#what-to-notice}

- **Arrow direction is not an evaluation-order diagram:** read a type with right association, then a concrete application with left association.
- **Visible parameter count does not determine the call form:** two names may be successive parameters or components in one tuple pattern.
- **Partial application returns a function:** the final body result does not exist until the remaining arguments arrive.
- **Generic still means consistent:** `'a` can be instantiated with many concrete types, but positions carrying the same letter must align.

A wrapper can fix one component for the tupled form, but needing the wrapper demonstrates precisely that it cannot be partially applied as directly as the curried form. Choose from the intended calls rather than declaring one form universally superior.
