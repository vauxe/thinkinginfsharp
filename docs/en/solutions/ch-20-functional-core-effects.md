---
title: "Chapter 20 Solutions"
description: "Expose hidden runtime inputs, select the smallest honest dependency shape, and preserve expected boundary failures without flattening contract violations."
translationKey: solutions/ch-20-functional-core-effects
kind: solution
part: 4
chapter: 20
status: complete
verifiedWith:
  fsharp: "10"
  dotnetSdk: "10.0.301"
exampleIds:
  - ch20-functional-core-effects
exerciseIds:
  - ch20-exercise-01
  - ch20-exercise-02
  - ch20-exercise-03
termIds: []
sources:
  - id: microsoft-functions
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/functions/
    checked: "2026-08-24"
  - id: microsoft-component-design
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/style-guide/component-design-guidelines
    checked: "2026-08-24"
  - id: dotnet-environment-variable
    url: https://learn.microsoft.com/en-us/dotnet/api/system.environment.getenvironmentvariable?view=net-10.0
    checked: "2026-08-24"
---

# Chapter 20 Solutions {#overview}

Expose the smallest capability a consumer needs, and prefer captured data when the consumer does not need the power to observe again. Injection makes acquisition visible; the pure core begins only after acquisition has finished.

[Return to Chapter 20](../part-04/ch-20-functional-core-effects).

## Exercise 1: expose three hidden inputs {#exercise-01}

### Separate captured facts from the decision {#exercise-01-core}

```fsharp
open System

type OfferPolicy =
    { EndsAt: DateTimeOffset
      WinningDrawExclusive: int }

type OfferFacts =
    { ObservedAt: DateTimeOffset
      Draw: int
      Region: string }

type OfferDecision =
    | Expired
    | NotSelected
    | Selected of region: string

let decideOffer policy facts =
    if facts.ObservedAt >= policy.EndsAt then
        Expired
    elif facts.Draw >= policy.WinningDrawExclusive then
        NotSelected
    else
        Selected facts.Region
```

The function has no way to reread time, advance a random source, or inspect the process environment. Its two arguments describe its complete decision input.

### Put acquisition in one orchestration function {#exercise-01-boundary}

```fsharp
type OfferEffects =
    { UtcNow: unit -> DateTimeOffset
      NextInt: int -> int
      ReadSetting: string -> string option }

let captureOffer effects =
    { ObservedAt = effects.UtcNow()
      Draw = effects.NextInt 100
      Region =
        effects.ReadSetting "OFFER_REGION"
        |> Option.defaultValue "global" }

let fixedInstant = DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero)
let mutable calls = 0

let fixedEffects =
    { UtcNow = fun () -> calls <- calls + 1; fixedInstant
      NextInt = fun upper -> calls <- calls + 1; assert (upper = 100); 7
      ReadSetting = fun name -> calls <- calls + 1; assert (name = "OFFER_REGION"); Some "eu" }

let policy =
    { EndsAt = fixedInstant.AddHours(1.0)
      WinningDrawExclusive = 10 }

let facts = captureOffer fixedEffects
let first = decideOffer policy facts
let replay = decideOffer policy facts

assert (first = Selected "eu")
assert (replay = first)
assert (calls = 3)
```

The mutable counter is test instrumentation. Both decisions use the same facts and leave it unchanged. A production adapter can replace the three function fields without changing `decideOffer`.

## Exercise 2: choose data, function, closure, or interface {#exercise-02}

### Match power to need {#exercise-02-choices}

| Case | Choice | Reason |
|---|---|---|
| One expiration comparison uses one instant | Capture `DateTimeOffset` data | The consumer should not have power to reread time |
| Retry policy requests a new delay after each failure | Function such as `int -> TimeSpan` | Each attempt intentionally asks for another value |
| Formatter retains immutable culture and prefix | Closure | Configuration is captured once behind one formatting operation |
| Cross-language storage client owns disposable connection and related read/write operations | Interface extending or exposing disposal policy | Operations, identity, and lifecycle form one component contract |
| One internal workflow needs clock, draw, and setting lookup | Small workflow-specific record of functions | Named local capabilities travel together; domain functions receive only captured data |

The retry function's lifetime must cover the retry operation and its failure contract must say whether producing a delay can fail. The formatter closure is pure only if formatting and captured values are pure. The storage interface does not make I/O pure; it gives the effectful component a stable boundary and lifecycle.

Do not pass the whole workflow dependency record to the expiration comparison. That would grant unnecessary capability and make its real dependency less obvious.

## Exercise 3: make boundary failure explicit {#exercise-03}

### Return expected setting absence {#exercise-03-result}

Using the chapter's `Campaign`, `Candidate`, and `RuntimeEffects` types:

```fsharp
type CaptureError =
    | MissingRequiredSetting of name: string

let captureCandidateRequired campaign effects =
    let submittedAt = effects.UtcNow()
    let draw = effects.NextInt 10_000

    if draw < 0 || draw >= 10_000 then
        invalidArg (nameof effects) "NextInt returned a value outside its requested range."

    match effects.ReadSetting "BOOKING_REGION" with
    | None -> Error(MissingRequiredSetting "BOOKING_REGION")
    | Some raw when String.IsNullOrWhiteSpace raw ->
        Error(MissingRequiredSetting "BOOKING_REGION")
    | Some raw ->
        Ok
            { SubmittedAt = submittedAt
              Draw = draw
              Region = raw.Trim() }
```

Tests should cover both branches without reading the process environment:

```fsharp
let missingEffects =
    { UtcNow = fixedClock instant
      NextInt = fixedDraw 42
      ReadSetting = fun _ -> None }

assert (
    captureCandidateRequired campaign missingEffects =
        Error(MissingRequiredSetting "BOOKING_REGION")
)
```

Missing required configuration is an expected startup or request-boundary fact that a caller can report. An out-of-range result violates the `NextInt` function's contract; this solution keeps it as `ArgumentException` because ordinary business recovery cannot make the provider correct. If the provider is untrusted input and the caller can select another provider, a distinct `InvalidDrawProvider` error case could instead be honest.

Do not merge both conditions into `Error "capture failed"`. One identifies absent configuration; the other identifies a broken dependency contract and needs different diagnostics and ownership.

## What to notice {#what-to-notice}

- Captured data can express a stronger snapshot guarantee than an injected capability.
- An orchestration function may be effectful even though every dependency is explicit.
- Closures are implementations of function contracts, not a competing form of parameter.
- Interfaces earn their cost through coherent operations, lifecycle, tooling, or public-boundary needs.
- Expected absence and provider contract violations should remain distinguishable.
