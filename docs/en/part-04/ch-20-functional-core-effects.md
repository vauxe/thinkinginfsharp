---
title: "Chapter 20: Functional Core and Effect Boundaries"
description: "Turn time, randomness, and environment access into explicit values or function dependencies, keeping domain decisions replayable and effect policy visible."
translationKey: part-04/ch-20-functional-core-effects
---

# Chapter 20: Functional Core and Effect Boundaries {#overview}

A function can have no visible parameter and still depend on the world. `DateTimeOffset.UtcNow`, a random draw, and an environment lookup each obtain information absent from the function's arguments. Calling them deep inside a pricing or booking rule can make apparently identical inputs produce different results and force tests to control global process state.

F# functions are values, so the repair can be small. Perform a side effect once and pass its result as data, or pass a narrow function that performs it. The domain core then receives every fact through ordinary arguments. Objects and interfaces remain useful when related operations form one component or require a lifecycle.

The main-line code in this chapter forms one `.fsx` script in reading order: each type is defined once, and later blocks continue to use the preceding `Campaign`, `Candidate`, `RuntimeEffects`, and functions. The first block opens `System`, giving the later time, randomness, environment, and test-collection names an explicit source.

## Hidden input is still input {#hidden-input}

Consider a function whose visible parameter is a request but whose body reads the current time, chooses a random number, and reads `BOOKING_REGION`. Its real input is closer to:

```text
(request, current time, random source state, process environment)
```

Omitting three items from the type does not remove them. It only makes their acquisition time, failure behavior, and test control implicit.

An effect is observable behavior not described by the returned value alone. Reading time or the environment may not mutate your code's data, but another evaluation can observe a different result. Random generation also advances source state. Console output, I/O, and shared mutation are more obvious examples handled in later chapters.

The goal is not “effects are forbidden.” A useful program must interact with its runtime. The goal is to make the point of interaction small enough that the pure decision can be understood and replayed independently.

## First make the decision a value transformation {#pure-core}

The example models only facts needed by a campaign decision:

```fsharp:line-numbers
open System

type Campaign =
    { OpensAt: DateTimeOffset
      ClosesAt: DateTimeOffset
      CodePrefix: string
      DefaultRegion: string }

type Candidate =
    { SubmittedAt: DateTimeOffset
      Draw: int
      Region: string }

type Decision =
    | NotOpen
    | Closed
    | Accepted of code: string
```
`Campaign` contains policy. `Candidate` contains observations already captured for one attempt. `Decision` names every pure outcome. The decision function is correspondingly direct:

```fsharp:line-numbers
let decide campaign candidate =
    if candidate.SubmittedAt < campaign.OpensAt then
        NotOpen
    elif candidate.SubmittedAt >= campaign.ClosesAt then
        Closed
    else
        let suffix = candidate.Draw.ToString("D4")
        Accepted $"{campaign.CodePrefix}-{candidate.Region}-{suffix}"
```
`decide` does not ask what time it is. It compares `Candidate.SubmittedAt` with the supplied window. It does not generate a suffix or discover a region; those values are already present. Given the same two records, it returns the same union case and performs no external work.

Passing time as data also states snapshot semantics. Every comparison in this decision uses one captured instant. Calling `UtcNow` several times could cross a window boundary halfway through one logical decision.

Purity is a property of this implementation and its dependencies, not of the `let` keyword or a function-shaped type. A supplied function can still perform I/O or mutate state.

## Capture side effects in a thin orchestration step {#capture-effects}

The orchestrator receives a record of three named function values:

```fsharp:line-numbers
type RuntimeEffects =
    { UtcNow: unit -> DateTimeOffset
      NextInt: int -> int
      ReadSetting: string -> string option }

let private normalizedRegion (fallback: string) (value: string option) =
    value
    |> Option.map (fun text -> text.Trim())
    |> Option.filter (String.IsNullOrWhiteSpace >> not)
    |> Option.defaultValue fallback

let captureCandidate campaign effects =
    let submittedAt = effects.UtcNow()
    let draw = effects.NextInt 10_000

    if draw < 0 || draw >= 10_000 then
        invalidArg (nameof effects) "NextInt returned a value outside its requested range."

    let region =
        effects.ReadSetting "BOOKING_REGION" |> normalizedRegion campaign.DefaultRegion

    { SubmittedAt = submittedAt
      Draw = draw
      Region = region }
```
The data flow is:

```text
UtcNow ───────────────┐
NextInt ──────────────┼──▶ captureCandidate ──▶ Candidate ──▶ decide
ReadSetting ──────────┘          effects             data        pure
```

`captureCandidate` invokes each dependency in a visible order, validates the random provider's promised range, normalizes the optional setting, and constructs data for the core. Passing side-effecting functions as parameters does not make this function pure; this is where orchestration performs the side effects.

The provider must honor the `10_000` bound. A production `Random.Next(10_000)` does so. A broken substitute returning `10_000` is rejected immediately instead of producing a malformed code. This guard checks the adapter, not an end-user domain rejection.

The record is useful here because one small internal orchestrator needs three independent capabilities together. Do not pass it into every domain function. If the bundle grows whenever any code needs a new service, it has become a service locator with an unclear dependency graph; split it by workflow.

## Keep real runtime access at the composition edge {#system-adapter}

The system adapter is small:

```fsharp:line-numbers
let systemEffects (random: Random) =
    { UtcNow = fun () -> DateTimeOffset.UtcNow
      NextInt = fun upperExclusive -> random.Next upperExclusive
      ReadSetting = fun name -> Environment.GetEnvironmentVariable name |> Option.ofObj }
```
Constructing this record does not read time or environment. Each closure performs its operation when `captureCandidate` invokes it:

- `DateTimeOffset.UtcNow` reads the current UTC instant;
- the supplied `Random` instance holds random-source state and implements the bounded draw;
- `Environment.GetEnvironmentVariable` reads the current process environment and may return null, converted immediately with `Option.ofObj` as established in Chapter 19.

The caller creates and owns the `Random` instance. Creating a fresh seeded generator inside every draw would accidentally change statistical behavior; sharing mutable random state introduces concurrency questions deferred to Chapter 24. `System.Random` is also not a source for security-sensitive tokens; use a cryptographic random-number API for that requirement. This chapter's boundary makes ownership and algorithm choice visible without claiming one lifetime is universally correct.

Only application composition should know `systemEffects`. The domain file should not open `System.Environment` or read a clock. Moving a global call into a helper named `Clock.now` without passing that helper still leaves a hidden dependency.

## Closures make configured functions {#closures}

A closure is a function value together with values captured from its definition scope. The deterministic providers are tiny closures:

```fsharp:line-numbers
let fixedClock instant = fun () -> instant

let fixedDraw draw =
    fun upperExclusive ->
        if draw < 0 || draw >= upperExclusive then
            invalidArg (nameof draw) "Fixed draw is outside the requested range."

        draw

let settingsFrom values = fun name -> Map.tryFind name values
```
`fixedClock instant` returns a `unit -> DateTimeOffset` function that remembers `instant`. `fixedDraw draw` remembers the chosen draw but still verifies the caller's requested range. `settingsFrom values` remembers an immutable map.

These closures are pure because their captured values are immutable and their bodies perform no effects. A closure can instead capture a mutable counter, database client, or random generator; then invoking it is effectful. “Closure” describes how context is retained, not a purity guarantee.

Closures are especially useful for partial configuration:

```fsharp
let campaignSettings = settingsFrom (Map [ ("BOOKING_REGION", "eu-west") ])
```

The result has only the operation the consumer needs. It does not expose the map or require a new nominal type.

## Choose the smallest accurate dependency form {#dependency-shapes}

Several representations are valid. Choose by the consumer's need, ownership, and audience:

| Form | Prefer it when | Watch for |
|---|---|---|
| Plain data such as `DateTimeOffset` | One observation should remain consistent throughout a decision | Capture it at the correct moment |
| One function parameter such as `unit -> DateTimeOffset` | One local operation is the whole capability | The function may still throw or perform side effects |
| A closure | A function should retain configuration, a client, or deliberately private state | Captured lifetime and mutation remain real |
| A small record of functions | An internal F# orchestration step needs several named capabilities together | Avoid an ever-growing service locator |
| An interface | Operations form one coherent stable component, need object lifecycle/state, framework DI, or a .NET-friendly public API | Do not create a broad interface merely for mocking |

### Function parameter or closure {#function-or-closure}

A function parameter describes what one caller needs:

```fsharp
let captureInstant (utcNow: unit -> DateTimeOffset) =
    utcNow ()
```

A closure supplies a configured implementation of that function. The two ideas are complementary: the parameter states the function the consumer needs, and the closure is one possible provider.

Prefer the plain value instead when no later read is needed. `decide campaign candidate` is stronger than passing a clock into `decide`, because its type proves the core cannot decide to read time twice.

### Interface when operations belong together {#interface-choice}

An interface is useful when multiple operations belong to one abstraction or an object must carry identity, state, or lifecycle:

```fsharp
type IClock =
    abstract UtcNow: unit -> DateTimeOffset
```

A one-member interface may be warranted by a host's dependency-injection conventions or a cross-language public API. Inside a small F#-only algorithm, `unit -> DateTimeOffset` is usually less ceremony. For a serializer with related operations or a disposable client, an interface can describe one coherent component better than unrelated function arguments.

Neither form decides failure policy. A function or interface member can return a value, `option`, `Result`, or `Task`, or it can throw. Choose the return type and exception policy separately.

## Deterministic tests observe calls, not elapsed time {#deterministic-tests}

The script uses fixed dependencies plus a mutable `ResizeArray` only as test instrumentation:

```fsharp:line-numbers
let instant = DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero)

let campaign =
    { OpensAt = instant.AddHours(-1.0)
      ClosesAt = instant.AddHours(1.0)
      CodePrefix = "BOOK"
      DefaultRegion = "global" }

let calls = ResizeArray<string>()

let observedEffects =
    { UtcNow =
        fun () ->
            calls.Add "clock"
            instant
      NextInt =
        fun upperExclusive ->
            calls.Add $"random:{upperExclusive}"
            7
      ReadSetting =
        fun name ->
            calls.Add $"environment:{name}"
            Some " eu-west " }

let candidate = captureCandidate campaign observedEffects
let firstDecision = decide campaign candidate
let replayedDecision = decide campaign candidate

let expectedCalls = [ "clock"; "random:10000"; "environment:BOOKING_REGION" ]

assert (candidate.SubmittedAt = instant)
assert (candidate.Draw = 7)
assert (candidate.Region = "eu-west")
assert (firstDecision = Accepted "BOOK-eu-west-0007")
assert (replayedDecision = firstDecision)
assert (List.ofSeq calls = expectedCalls)

let fallbackEffects =
    { UtcNow = fixedClock instant
      NextInt = fixedDraw 42
      ReadSetting = settingsFrom Map.empty }

let fallbackDecision =
    fallbackEffects |> captureCandidate campaign |> decide campaign

assert (fallbackDecision = Accepted "BOOK-global-0042")

let earlyDecision =
    decide
        campaign
        { candidate with
            SubmittedAt = campaign.OpensAt.AddTicks(-1L) }

let closedDecision =
    decide
        campaign
        { candidate with
            SubmittedAt = campaign.ClosesAt }

assert (earlyDecision = NotOpen)
assert (closedDecision = Closed)

printfn
    "decision=%A fallback=%A calls=%A"
    firstDecision
    fallbackDecision
    (List.ofSeq calls)
```
The assertions verify:

- the captured instant, draw, and trimmed region are exactly the supplied values;
- the three dependency functions are called once in documented order;
- replaying the pure core on captured data produces the same decision without more dependency calls;
- a missing setting uses the campaign's configured fallback;
- the opening instant is included and the closing instant is excluded according to the code's comparisons.

Save the chapter's main-line fragments in reading order as `effects.fsx`, then run `dotnet fsi effects.fsx`. After every assertion passes, it prints:

```text
decision=Accepted "BOOK-eu-west-0007" fallback=Accepted "BOOK-global-0042" calls=["clock"; "random:10000"; "environment:BOOKING_REGION"]
```

That line exposes the pure decision, the fallback-region path, and the actual call order of the three external dependencies.

No test sleeps, changes the process environment, or guesses what `Random` will return for a seed. A seed can make one implementation reproducible, but asserting the framework's exact sequence couples a domain test to an algorithm the domain does not define. The fixed function states the actual requirement: return this in-range draw.

## Side-effecting dependencies still need failure policies {#failure-contracts}

Making a side-effecting dependency visible does not say what happens when it fails. `Environment.GetEnvironmentVariable` can return null and has documented exceptions. A clock provider or remote client can throw. A test substitute can violate the required range.

Use the smallest accurate return type. Optional configuration may return `option`; required configuration may return `Result`; an unexpected runtime failure may remain an exception until a layer can add context. Do not turn every capability into `unit -> Result<_, string>` without identifying expected failures. Chapter 21 develops exception and resource policy.

Likewise, do not invoke an effect repeatedly merely because it is injected. Decide whether one workflow needs a snapshot, a fresh read per step, or a stream of changing observations, and encode that choice in where the function is called.

## Exercises {#exercises}

### Exercise 1: expose three hidden inputs {#exercise-01}

Refactor a function that reads `DateTimeOffset.UtcNow`, calls `Random.Next(100)`, and reads `OFFER_REGION` inside its body. Split it into a pure `decideOffer` taking a record of captured facts and an orchestration function taking narrow dependencies.

Write a deterministic test that supplies a fixed instant, draw, and region. Prove that calling the pure function twice does not invoke any dependency again.


::: details Answer

#### Separate captured facts from the decision {#exercise-01-core}

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

#### Put acquisition in one orchestration function {#exercise-01-boundary}

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

:::

### Exercise 2: choose data, function, closure, or interface {#exercise-02}

Choose a dependency form for each case and justify it:

1. one expiration comparison must use the same instant throughout;
2. a local retry policy needs to request a new delay after each failure;
3. a configured formatter needs an immutable culture and prefix;
4. a cross-language storage client has related read/write operations and manages a disposable connection;
5. one internal workflow needs clock, random draw, and setting lookup, while no domain function needs all three.

State the lifetime and failure behavior even when the chosen type is a function.


::: details Answer

#### Match power to need {#exercise-02-choices}

| Case | Choice | Reason |
|---|---|---|
| One expiration comparison uses one instant | Capture `DateTimeOffset` data | The consumer should not be able to reread time |
| Retry policy requests a new delay after each failure | Function such as `int -> TimeSpan` | Each attempt intentionally asks for another value |
| Formatter retains immutable culture and prefix | Closure | Configuration is captured once behind one formatting operation |
| Cross-language storage client manages a disposable connection and related read/write operations | Interface extending or exposing disposal policy | Operations, identity, and lifecycle form one component contract |
| One internal workflow needs clock, draw, and setting lookup | Small workflow-specific record of functions | Named local capabilities travel together; domain functions receive only captured data |

The retry function's lifetime must cover the retry operation and its failure contract must say whether producing a delay can fail. The formatter closure is pure only if formatting and captured values are pure. The storage interface does not make I/O pure; it gives the effectful component a stable boundary and lifecycle.

Do not pass the whole workflow dependency record to the expiration comparison. That would grant unnecessary capability and make its real dependency less obvious.

:::

### Exercise 3: make adapter failures visible {#exercise-03}

Change setting lookup so a missing `BOOKING_REGION` is an error instead of using a fallback. Define a specific error union and make the capture step return `Result<Candidate, CaptureError>`.

Ensure that a missing setting is distinguishable from a random provider returning an out-of-range value. Decide whether the latter should remain an exception or become an error case, and justify the choice based on who can recover.


::: details Answer

#### Return expected setting absence {#exercise-03-result}

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

:::


The next chapter adds exceptions, disposable resources, and file I/O to this orchestration layer while preserving the same functional core.

## Sources {#sources}

- [Microsoft Learn: F# functions](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/functions/)
- [Microsoft Learn: F# component design guidelines](https://learn.microsoft.com/en-us/dotnet/fsharp/style-guide/component-design-guidelines)
- [Microsoft Learn: `DateTimeOffset.UtcNow`](https://learn.microsoft.com/en-us/dotnet/api/system.datetimeoffset.utcnow?view=net-10.0)
- [Microsoft Learn: `Random.Next`](https://learn.microsoft.com/en-us/dotnet/api/system.random.next?view=net-10.0)
- [Microsoft Learn: `Environment.GetEnvironmentVariable`](https://learn.microsoft.com/en-us/dotnet/api/system.environment.getenvironmentvariable?view=net-10.0)
