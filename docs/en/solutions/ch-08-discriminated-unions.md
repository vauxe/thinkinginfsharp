---
title: "Chapter 8 Solutions"
description: "Reasoning about flag combinations, union cases, exhaustiveness, and state-transition policy."
translationKey: solutions/ch-08-discriminated-unions
---

# Chapter 8 Solutions {#overview}

A union-modeling answer should do more than compile. It should identify which combinations disappear and which transition rules remain in functions.

[Return to Chapter 8](../part-02/ch-08-discriminated-unions).

## Exercise 1: remove flag combinations {#exercise-01}

Three independent Booleans produce `2³ = 8` combinations. If email, SMS, and disabled are the only mutually exclusive states, only `(true,false,false)`, `(false,true,false)`, and `(false,false,true)` are legal. Five others need rejection.

The union states the legal set directly:

```fsharp
type NotificationTarget =
    | Email of address: string
    | Sms of phoneNumber: string
    | Disabled of reason: string
```

Every constructed value now selects exactly one target: an email address, an SMS number, or a disabled reason. Smart constructors or validation can add string-format guarantees without changing these three cases.

## Exercise 2: prove exhaustiveness {#exercise-02}

An exhaustive function is:

```fsharp
let shortLabel status =
    match status with
    | Pending -> "P"
    | Confirmed _ -> "C"
    | Cancelled _ -> "X"
```

Adding `Waitlisted of position: int` should make this match report FS0025, forcing the maintainer to choose a label such as `"W"`. If the old function ended with `_ -> "?"`, the new case would silently receive `"?"`; the compiler cannot distinguish deliberate compatibility from omission.

A wildcard is not always wrong. If a function asks only “is this Pending?” and every current and future non-Pending case truly behaves the same, `| _ -> false` may state that remainder exactly. The question is whether future case policy really is shared.

## Exercise 3: design a transition policy {#exercise-03}

The minimal function is:

```fsharp
let cancel reason status =
    match status with
    | Pending
    | Confirmed _ -> Cancelled reason
    | Cancelled _ -> status
```

Returning only `BookingStatus` prevents callers from distinguishing “cancelled now” from “already cancelled.” They also do not receive the previous reason and cannot decide whether a repeated request agrees. If some transitions are forbidden, an interface could return `Result<BookingStatus, string>`, carrying new state on success and a reason on failure. The next chapter replaces bare strings with domain errors and composes those results.

## What to notice {#what-to-notice}

- **A union shrinks representation space:** five illegal flag combinations no longer require repeated validation.
- **Case data still needs its own invariants:** `Email ""` remains constructible here and needs later protection.
- **Exhaustiveness diagnostics support evolution:** a new case forces explicit matches to revisit policy.
- **A wildcard denotes the remaining set:** use it only when that set genuinely shares one rule.
- **Legal state is not legal transition:** unions limit representable states; return types such as `Result` express transition failure.
