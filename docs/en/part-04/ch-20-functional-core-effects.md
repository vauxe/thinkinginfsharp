---
title: "Chapter 20: Functional Core and Effect Boundaries"
description: "Turn time, randomness, and environment access into explicit values or function dependencies, keeping domain decisions replayable and effect policy visible."
translationKey: part-04/ch-20-functional-core-effects
---

# Chapter 20: Functional Core and Effect Boundaries {#overview}

A function can have no explicit input and still depend on the world. `DateTimeOffset.UtcNow`, a random draw, and an environment lookup each obtain information not present in the function's arguments. Calling them deep inside a pricing or booking rule makes identical apparent inputs produce different results and forces tests to control global process state.

F# functions are values, so the repair can be small. Read an effect once and pass its result as data, or pass a narrow function that performs the effect. The domain core then receives every fact it needs through ordinary arguments. Objects and interfaces remain available when a coherent component contract or lifecycle calls for them.

## What you will be able to do {#outcomes}

By the end of this chapter, you should be able to:

- identify time, randomness, environment access, and mutation as observable dependencies;
- distinguish “read the world” orchestration from a pure domain decision;
- capture one observation as data when a decision needs a consistent snapshot;
- pass a function value when one operation is the whole required capability;
- bundle a few internal capabilities without creating a global service locator;
- use closures to preconfigure dependencies or retain deliberately private state;
- choose an interface for a coherent stable operation set, object lifecycle, or .NET-facing boundary;
- build a real system adapter without letting it leak into the core;
- test behavior and call order with fixed values rather than sleeps or process-global mutation;
- explain why dependency injection makes effects visible but does not make them pure.

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

```fsharp:line-numbers [ch20-functional-core-effects.fsx]
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

```fsharp:line-numbers [ch20-functional-core-effects.fsx]
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

## Capture effects in a thin orchestration step {#capture-effects}

The orchestration contract is a record of three named function values:

```fsharp:line-numbers [ch20-functional-core-effects.fsx]
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
Its shape is:

```text
UtcNow ───────────────┐
NextInt ──────────────┼──▶ captureCandidate ──▶ Candidate ──▶ decide
ReadSetting ──────────┘          effects             data        pure
```

`captureCandidate` invokes each capability in a visible order, validates the random provider's promised range, normalizes the optional setting, and constructs data for the core. The function is not pure merely because effects arrive as parameters; it is the explicit effect boundary.

The `10_000` bound is part of the call contract. A production `Random.Next(10_000)` obeys it. A broken substitute returning `10_000` is rejected immediately rather than producing a malformed code. This guard protects the adapter contract; it is not a domain rejection returned to an end user.

The record is useful here because one small internal orchestrator needs three independent capabilities together. Do not pass it into every domain function. If the bundle grows whenever any code needs a new service, it has become a service locator with an unclear dependency graph; split it by workflow.

## Keep real runtime access at the composition edge {#system-adapter}

The system adapter is small:

```fsharp:line-numbers [ch20-functional-core-effects.fsx]
let systemEffects (random: Random) =
    { UtcNow = fun () -> DateTimeOffset.UtcNow
      NextInt = fun upperExclusive -> random.Next upperExclusive
      ReadSetting = fun name -> Environment.GetEnvironmentVariable name |> Option.ofObj }
```
Constructing this record does not read time or environment. Each closure performs its operation when `captureCandidate` invokes it:

- `DateTimeOffset.UtcNow` reads the current UTC instant;
- the supplied `Random` instance owns random-source state and implements the bounded draw;
- `Environment.GetEnvironmentVariable` reads the current process environment and may return null, converted immediately with `Option.ofObj` as established in Chapter 19.

The caller creates and owns the `Random` instance. Creating a fresh seeded generator inside every draw would accidentally change statistical behavior; sharing mutable random state introduces concurrency questions deferred to Chapter 24. `System.Random` is also not a source for security-sensitive tokens; use a cryptographic random-number API for that requirement. This chapter's boundary makes ownership and algorithm choice visible without claiming one lifetime is universally correct.

Only application composition should know `systemEffects`. The domain file should not open `System.Environment` or read a clock. Moving a global call into a helper named `Clock.now` without passing that helper still leaves a hidden dependency.

## Closures make configured functions {#closures}

A closure is a function value together with values captured from its definition scope. The deterministic providers are tiny closures:

```fsharp:line-numbers [ch20-functional-core-effects.fsx]
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
let campaignSettings = settingsFrom (Map [ "BOOKING_REGION", "eu-west" ])
```

The result has only the operation the consumer needs. It does not expose the map or require a new nominal type.

## Choose the smallest dependency shape that tells the truth {#dependency-shapes}

Several representations are valid. Choose by the consumer's need, ownership, and audience:

| Shape | Prefer it when | Watch for |
|---|---|---|
| Plain data such as `DateTimeOffset` | One observation should remain consistent throughout a decision | Capture it at the correct moment |
| One function parameter such as `unit -> DateTimeOffset` | One local operation is the whole capability | The function may still throw or perform effects |
| A closure | A function should retain configuration, a client, or deliberately private state | Captured lifetime and mutation remain real |
| A small record of functions | An internal F# orchestration step needs several named capabilities together | Avoid an ever-growing service locator |
| An interface | Operations form one coherent stable component, need object lifecycle/state, framework DI, or a .NET-friendly public contract | Do not create a broad interface merely for mocking |

### Function parameter or closure {#function-or-closure}

A function parameter describes what one caller needs:

```fsharp
let captureInstant (utcNow: unit -> DateTimeOffset) =
    utcNow ()
```

A closure supplies a configured implementation of that function. The two ideas are complementary: the parameter is the consumer contract; the closure is one possible provider.

Prefer the plain value instead when no later read is needed. `decide campaign candidate` is stronger than passing a clock into `decide`, because its type proves the core cannot decide to read time twice.

### Interface when operations belong together {#interface-choice}

An interface is useful when multiple operations belong to one abstraction or an object must carry identity, state, or lifecycle:

```fsharp
type IClock =
    abstract UtcNow: unit -> DateTimeOffset
```

A one-member interface may be warranted by a host's dependency-injection conventions or a cross-language public API. Inside a small F#-only algorithm, `unit -> DateTimeOffset` is usually less ceremony. For a serializer with related serialize/deserialize operations or a client that must be disposed, an interface can state a more coherent component boundary than unrelated function arguments.

Neither shape decides failure policy. A function or interface member can return a value, `option`, `Result`, `Task`, or throw. Choose that result contract separately.

## Deterministic tests observe calls, not elapsed time {#deterministic-tests}

The script uses fixed dependencies plus a mutable `ResizeArray` only as test instrumentation:

```fsharp:line-numbers [ch20-functional-core-effects.fsx]
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
```
The assertions prove:

- the captured instant, draw, and trimmed region are exactly the supplied values;
- the three effect functions are called once in documented order;
- replaying the pure core on captured data produces the same decision without more effect calls;
- a missing setting uses the campaign's explicit fallback;
- the opening instant is included and the closing instant is excluded according to the code's comparisons.

No test sleeps, changes the process environment, or guesses what `Random` will return for a seed. A seed can make one implementation reproducible, but testing an exact framework-generated sequence can couple a domain test to an algorithm it does not own. The fixed function expresses the actual contract: return this in-range draw.

## Run the shared example {#run-example}

From the directory containing the example:

```console
dotnet fsi --exec ch20-functional-core-effects.fsx
```

Six deterministic output lines report the captured snapshot, accepted code, fallback region, window boundaries, exact effect order, and replay result. Compare their order and text.

## Effects still need failure contracts {#failure-contracts}

Making an effect explicit does not say what happens when it fails. `Environment.GetEnvironmentVariable` can return null and has documented exceptions. A clock provider or remote client can throw. A test substitute can violate the range contract.

Use the smallest accurate return type. Optional configuration may return `option`; required configuration may return `Result`; an unexpected runtime failure may remain an exception until the boundary can add context. Do not turn every capability into `unit -> Result<_, string>` without identifying expected failures. Chapter 21 develops exception and resource policy.

Likewise, do not invoke an effect repeatedly merely because it is injected. Decide whether one workflow needs a snapshot, a fresh read per step, or a stream of changing observations, and encode that choice in where the function is called.

## Exercises {#exercises}

### Exercise 1: expose three hidden inputs {#exercise-01}

Refactor a function that reads `DateTimeOffset.UtcNow`, calls `Random.Next(100)`, and reads `OFFER_REGION` inside its body. Split it into a pure `decideOffer` taking a record of captured facts and an orchestration function taking narrow dependencies.

Write a deterministic test that supplies a fixed instant, draw, and region. Prove that calling the pure function twice does not invoke any dependency again.

### Exercise 2: choose data, function, closure, or interface {#exercise-02}

Choose a dependency shape for each case and justify it:

1. one expiration comparison must use the same instant throughout;
2. a local retry policy needs to request a new delay after each failure;
3. a configured formatter needs an immutable culture and prefix;
4. a cross-language storage client has related read/write operations and owns a disposable connection;
5. one internal workflow needs clock, random draw, and setting lookup, while no domain function needs all three.

State the lifetime and failure behavior even when the chosen type is a function.

### Exercise 3: make boundary failure explicit {#exercise-03}

Change setting lookup so a missing `BOOKING_REGION` is an error instead of using a fallback. Define a specific error union and make the capture step return `Result<Candidate, CaptureError>`.

Ensure that a missing setting is distinguishable from an out-of-range random-provider contract violation. Decide whether the latter should remain an exception or become an error case, and justify the choice based on who can recover.

[Read the chapter solutions](../solutions/ch-20-functional-core-effects).

## Model review {#model-review}

- Time, randomness, and environment access are inputs even when no parameter names them.
- Pass a captured value when one consistent snapshot is stronger than an ability to reread.
- A pure core transforms explicit values and can be replayed without runtime setup.
- Function injection exposes an effect; it does not purify the invoked function.
- Closures retain configuration or state, and their purity depends on what they capture and do.
- Small function records fit local F# orchestration; interfaces fit coherent component and .NET-facing boundaries.
- Keep real runtime calls in composition code and convert their foreign representations immediately.
- Deterministic substitutes should prove contracts directly, without sleeps, real environment mutation, or assumed random sequences.

The next chapter adds exceptions, disposable resources, and file I/O to this boundary while preserving the same functional core.

## Sources {#sources}

- [Microsoft Learn: F# functions](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/functions/)
- [Microsoft Learn: F# component design guidelines](https://learn.microsoft.com/en-us/dotnet/fsharp/style-guide/component-design-guidelines)
- [Microsoft Learn: `DateTimeOffset.UtcNow`](https://learn.microsoft.com/en-us/dotnet/api/system.datetimeoffset.utcnow?view=net-10.0)
- [Microsoft Learn: `Random.Next`](https://learn.microsoft.com/en-us/dotnet/api/system.random.next?view=net-10.0)
- [Microsoft Learn: `Environment.GetEnvironmentVariable`](https://learn.microsoft.com/en-us/dotnet/api/system.environment.getenvironmentvariable?view=net-10.0)
