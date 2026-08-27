---
title: "Chapter 8: Discriminated Unions and State Modeling"
description: "Derive discriminated unions from contradictory Boolean flags, then express exclusive states and transitions with case-specific data and exhaustive matching."
translationKey: part-02/ch-08-discriminated-unions
---

# Chapter 8: Discriminated Unions and State Modeling {#overview}

Suppose booking status is represented by three Boolean fields: `IsPending`, `IsConfirmed`, and `IsCancelled`. Three independent switches have eight combinations, while the business may allow only three: pending, confirmed, or cancelled. What does `true, true, false` mean? The type already permits a question callers should not be able to ask.

A discriminated union says that a value is exactly one of several named cases. Each case carries only the data that state needs: a confirmation code exists only in `Confirmed`, and a cancellation reason only in `Cancelled`. Pattern matching then requires the code to consider every case.

This chapter handles a closed, synchronous, in-memory state representation. Chapter 9 adds `Result` for failed transitions, Chapter 12 protects construction invariants, and later capstone slices address persistence and concurrency.

## Independent flags create a Cartesian product {#flag-problem}

The example first retains an intentionally weak record:

```fsharp:line-numbers
type BookingFlags =
    { IsPending: bool
      IsConfirmed: bool
      IsCancelled: bool }

let contradictoryFlags =
    { IsPending = true
      IsConfirmed = true
      IsCancelled = false }

printfn
    "Flag model contradiction: pending=%b confirmed=%b cancelled=%b"
    contradictoryFlags.IsPending
    contradictoryFlags.IsConfirmed
    contradictoryFlags.IsCancelled
```
The record ensures that all three fields exist and are `bool`, but cannot say “exactly one is true.” Each added switch doubles the combination space. A validation function must reject contradictions after every construction and update.

Worse, case-specific data often gets flattened into nullable or optional fields. Why does `ConfirmationCode` exist when a booking is unconfirmed? What should `CancellationReason` contain for a confirmed booking? The data relationship survives only as convention.

This does not make Boolean values bad. `HasDietaryRequirements` and `NeedsWheelchairAccess` may be independent facts that can both be true. A union repairs the model only when several flags are really imitating “exactly one state.”

## A union type expresses a finite choice {#union-definition}

List every legal state in one type:

```fsharp:line-numbers
type BookingStatus =
    | Pending
    | Confirmed of confirmationCode: string
    | Cancelled of reason: string
```
`BookingStatus` is a discriminated union. `Pending`, `Confirmed`, and `Cancelled` are its three **union cases**. A value is constructed by exactly one case at a time, so no value can be both pending and confirmed.

`Pending` carries no data. `Confirmed` carries a `string` field named `confirmationCode`, while `Cancelled` carries a `reason`. These are not optional properties shared by all three cases; each field is part of its particular case.

A union is a sum type: its legal value set is the sum of each case's value set. The previous chapter's record is a product type: one value has all its fields simultaneously. They often compose—a record can store stable booking fields and one `BookingStatus` field.

## A case name constructs and deconstructs {#construction}

Applying a case name to its data constructs a union value:

```fsharp
let pending = Pending
let confirmed = Confirmed "C-42"
let cancelled = Cancelled "duplicate"
```

When type context is insufficient or several unions reuse a case name, qualify it as `BookingStatus.Confirmed "C-42"`. Large domains also sometimes apply `[<RequireQualifiedAccess>]`. We will keep the definition minimal and focus on cases and their data.

In a pattern, the same name identifies a case and binds its data. `Confirmed code` is not a call there; it means “if the input is `Confirmed`, bind its field as `code`.” Construction and deconstruction share one vocabulary, so no separate runtime tag needs to stay synchronized.

## Exhaustive matches cover every case {#exhaustive-match}

The shared function covers all three cases:

```fsharp:line-numbers
let describeStatus status =
    match status with
    | Pending -> "pending"
    | Confirmed confirmationCode -> $"confirmed:{confirmationCode}"
    | Cancelled reason -> $"cancelled:{reason}"

let statuses = [ Pending; Confirmed "C-42"; Cancelled "duplicate" ]

let descriptions = statuses |> List.map describeStatus

printfn "Statuses: %A" descriptions
```
Every branch returns `string`, so the whole `match` is a `string` expression. The compiler knows the closed set of `BookingStatus` cases and checks whether the patterns cover all of them.

If the type later gains `Waitlisted of position: int`, every explicit match that omits it produces a diagnostic. Model evolution becomes a compiler-located change list instead of an omission discovered on a rare runtime path.

### The non-exhaustive version is diagnostic-only {#non-exhaustive-diagnostic}

The following deliberately omits `Cancelled` and is not part of the valid example:

```fsharp
let incomplete status =
    match status with
    | Pending -> "pending"
    | Confirmed code -> $"confirmed:{code}"
```

The F# compiler reports warning FS0025 for an incomplete pattern match and gives an uncovered value as an example. A project that treats warnings as errors will reject this code at build time.

Do not mechanically add `| _ -> "other"` to silence it. When states have distinct business meaning, a wildcard lets a future case fall silently into old behavior. A wildcard is appropriate only when remaining cases truly share one rule and you intentionally accept future cases under that rule too.

## Match a case before reading its data {#case-data}

To read a confirmation code, first prove the state is `Confirmed`:

```fsharp:line-numbers
let confirmationCode status =
    match status with
    | Confirmed code -> Some code
    | Pending
    | Cancelled _ -> None

printfn "Confirmed case carries code: %s" (confirmationCode (Confirmed "C-42") |> Option.defaultValue "none")
```
`confirmationCode` returns `string option`: confirmed state yields `Some code`, and other states yield `None`. This reuses the minimal `option` intuition established for `List.choose` in Chapter 5. The next chapter treats missing-value composition systematically.

Code cannot read a nonexistent confirmation code from `Pending`. A successful `Confirmed code` pattern establishes both the case and the availability of `code`.

Several cases can share one branch:

```fsharp
| Pending
| Cancelled _ -> None
```

Alternatives in an OR pattern must bind compatible names and types. Neither alternative needs carried data here, so combining them is safe.

## A state transition is a value-to-value function {#transitions}

The example writes confirmation as a pure function:

```fsharp:line-numbers
let confirm code status =
    match status with
    | Pending -> Confirmed code
    | Confirmed _
    | Cancelled _ -> status

let transitioned = Pending |> confirm "C-99"

printfn "Transition: pending -> %s" (describeStatus transitioned)
printfn "All descriptions: %d" (List.length descriptions)
```
`confirm` constructs `Confirmed code` from `Pending`; for already confirmed or cancelled state, it returns the original value. The function does not mutate its input, and its output remains inside the legal `BookingStatus` cases.

“Keep the original state on an invalid transition” is a teaching choice, not a general booking rule. Repeated confirmation might be an idempotent success or a conflict; confirmation after cancellation usually deserves a contextual failure. Chapter 9 uses `Result` to put that decision in the return type.

The union guarantees that output is a legal state, not that every transition policy is correct. Types shrink the problem space; functions still implement business policy.

## What records and unions each carry {#records-and-unions}

A record expresses “has all of these”: a booking has an ID, attendee, and status at once. A union expresses “exactly one of these”: status is pending, confirmed, or cancelled.

```fsharp
type Booking =
    { BookingId: string
      Attendee: string
      Status: BookingStatus }
```

Do not replace every record with a union or flatten case data back into one giant record. A common domain model uses a record for stable fields and a union-valued field for the changing state. Each union case may in turn carry named fields or a small record.

Like records, ordinary unions automatically receive structural equality and comparison when their component data supports them. `Confirmed "C-42" = Confirmed "C-42"` is true; different cases are unequal. Whether that equality means business-entity equality still comes from requirements.

## `.IsCase` properties and patterns {#case-tests}

Starting with F# 9, union values expose generated case-test properties such as `.IsConfirmed`. This is useful when code needs only a Boolean test, but it does not extract the confirmation code. Use `match` when you need case data, several branches, or exhaustiveness.

Do not use a sequence of `.IsPending` and `.IsConfirmed` checks to turn a union back into flag-oriented code. A union ties each case to its data and lets the compiler understand the complete case set.

## Design checklist {#design-checklist}

When considering a discriminated union, ask:

1. are these cases mutually exclusive and collectively a closed set?
2. does each case carry different data?
3. do case names come from domain language rather than implementation machinery?
4. which matches should the compiler flag when a case is added?
5. are independent facts being forced incorrectly into exclusive cases?

If a union has many cases differing only by a few independent switches, the model may need “a record plus several small unions or Booleans,” not one huge union enumerating a Cartesian product. A type should compress illegal combinations, not enumerate every accidental one.

## Inspect the case payload {#debugging}

When a new case causes FS0025, decide the new case's real rule in each match instead of adding a wildcard first. If branches repeat, confirm that their semantics truly agree before joining them with an OR pattern.

When case construction causes a type error, inspect the data after `of`. `Confirmed of string` receives one string; `Case of string * int` receives the stated pair. Construction and patterns must agree with that declaration.

Code that first tests a case and then matches again to extract data can usually become one `match`. If a state transition returns its input but callers cannot tell whether anything happened, its return type probably needs Chapter 9's `Result`.

## Exercises {#exercises}

### Exercise 1: remove flag combinations {#exercise-01}

A notification request has `IsEmail`, `IsSms`, and `IsDisabled` flags, while the rule requires exactly one of email, SMS, or disabled. Count all flag combinations and the three legal ones, then define a union carrying an email address, phone number, or disable reason. Explain which illegal states can no longer be constructed.

### Exercise 2: prove exhaustiveness {#exercise-02}

Write an exhaustive short-label function for `BookingStatus`. Then add `Waitlisted of position: int` on paper and identify which match the compiler should flag. Compare the maintenance information from an explicit new branch with what happens if the old function already had `_`.

### Exercise 3: design a transition policy {#exercise-03}

Write a pure `cancel reason status` function: `Pending` and `Confirmed _` become `Cancelled reason`, while cancelled state remains unchanged. List the information lost by returning only `BookingStatus`. Then propose a return type that could distinguish success from a forbidden transition; you need not compose `Result` yet.

[Read the chapter solutions](../solutions/ch-08-discriminated-unions).

The next chapter compares two common return types: `option` represents possible absence, while `Result` represents an expected failure with context.

## Sources {#sources}

- [Microsoft Learn: Discriminated unions](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/discriminated-unions)
- [Microsoft Learn: `match` expressions and guards](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/match-expressions)
- [Microsoft Learn: Pattern matching](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/pattern-matching)
