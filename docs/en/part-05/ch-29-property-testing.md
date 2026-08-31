---
title: "Chapter 29: Property Testing with FsCheck"
description: "Move from selected examples to domain invariants, then control generation, classification, shrinking, and replay without treating randomized checks as proof."
translationKey: part-05/ch-29-property-testing
---

# Chapter 29: Property Testing with FsCheck {#overview}

An example test asks whether one chosen input produces one expected output. A property test asks whether a relationship holds across many generated inputs. FsCheck does not prove a theorem or inspect every possible value. It searches much more broadly than a short hand-written table and, after a failure, tries to reduce the input to a smaller counterexample.

The difficult part is not writing `[<Property>]`. It is stating a useful invariant, generating meaningful domain data, inspecting the distribution, and diagnosing failures without confusing a random seed with a business rule. This chapter develops those skills around a greedy seat allocator.

## Generalize examples into invariants {#examples-to-invariants}

Suppose an allocator starts with a capacity and processes positive seat requests in order. It accepts a request when it fits in the remaining capacity and otherwise rejects it. A useful example is capacity 5 with requests `[2; 4; 3]`: the first and last requests are accepted, the middle request is rejected, and zero seats remain.

That example is valuable because it communicates one policy decision. It does not search empty input, zero capacity, repeated requests, exact fits, or many interleavings of accepted and rejected requests. Instead of writing hundreds of expected lists, ask what must remain true for every valid input:

1. accepted seats plus remaining seats equal the initial capacity;
2. every request appears exactly once in the decisions and in original order;
3. remaining capacity stays between zero and initial capacity.

The sample represents every request with a smart-constructed type and records each decision in a union:

```fsharp:line-numbers [Generators.fs]
type AllocationCaseError =
    | NegativeCapacity of capacity: int
    | NonPositiveRequest of seats: int

type AllocationCase =
    private
        { Capacity: int
          Requests: int list }

module AllocationCase =
    let create capacity requests =
        if capacity < 0 then
            Error(NegativeCapacity capacity)
        else
            match requests |> List.tryFind (fun seats -> seats <= 0) with
            | Some seats -> Error(NonPositiveRequest seats)
            | None ->
                Ok
                    { Capacity = capacity
                      Requests = requests }

    let capacity sample = sample.Capacity
    let requests sample = sample.Requests

    let internal assumeValid capacity requests =
        match create capacity requests with
        | Ok sample -> sample
        | Error error -> invalidArg (nameof requests) $"invalid allocation case: {error}"

type Decision =
    | Accepted of seats: int
    | Rejected of seats: int

type Allocation =
    { Decisions: Decision list
      Remaining: int }

module SeatAllocation =
    let allocate sample =
        let folder (remaining, decisions) request =
            if request <= remaining then
                remaining - request, Accepted request :: decisions
            else
                remaining, Rejected request :: decisions

        let remaining, reversedDecisions =
            ((sample.Capacity, []), sample.Requests) ||> List.fold folder

        { Decisions = List.rev reversedDecisions
          Remaining = remaining }
```
The three statements describe relationships, not particular outputs, so many correct implementation changes preserve them. Each catches a different defect: conservation finds lost or invented capacity, preservation finds skipped or reordered requests, and bounds find over-allocation.

### Expected behavior needs an independent basis {#independent-oracle}

This property is nearly useless:

```fsharp
let allocationMatchesItself sample =
    SeatAllocation.allocate sample = SeatAllocation.allocate sample
```

A test that copies the allocator's fold into its expected calculation is equally weak: the same defect can appear on both sides. Derive a property from a domain rule, algebraic law, simpler reference model, or trusted inverse—not from the source expression under test.

Common property shapes include:

| Pattern | Question | Example |
|---|---|---|
| Invariant | What must always remain true? | Capacity is conserved |
| Round trip | Can encoding and decoding recover a value? | `decode (encode value) = Ok value` |
| Algebraic law | What composition law should hold? | Set union is associative |
| Model comparison | Does the optimized version agree with a simpler one? | Indexed lookup agrees with linear search |
| Metamorphic relation | How should transformed input change output? | Sorting twice equals sorting once |

Not every domain has elegant algebra. A small model or a few concrete examples may be clearer than a forced “law.” Name a property after what users can rely on, not merely that a function “works.”

## What FsCheck generates and checks {#fscheck-model}

FsCheck treats a function from generated arguments to `bool`, `Property`, or another supported testable form such as `Lazy`, `Async`, or `Task` as a property. The xUnit integration discovers functions marked with `FsCheck.Xunit.PropertyAttribute`; unlike `[<Fact>]`, such functions may take arguments.

```fsharp
[<Properties(
    Arbitrary = [| typeof<AllocationCaseArbitrary> |],
    QuietOnSuccess = true
)>]
module Ch29Properties =
    [<Property(MaxTest = 300)>]
    let ``allocation conserves capacity`` (sample: AllocationCase) =
        AllocationProperties.conservesCapacity sample
```

`MaxTest = 300` asks for 300 successful cases. It is neither a coverage percentage nor a promise of 300 distinct values. FsCheck gradually changes a size parameter; generators decide how it affects numeric magnitudes or collection lengths. Cases rejected by a conditional property do not count as successes and are limited separately by `MaxRejected`.

The shared property functions are pure functions and can also be called from example tests or FSI:

```fsharp:line-numbers [Generators.fs]
module AllocationProperties =
    let private requestedSeats decision =
        match decision with
        | Accepted seats
        | Rejected seats -> seats

    let conservesCapacity sample =
        let allocation = SeatAllocation.allocate sample

        let acceptedSeats =
            allocation.Decisions
            |> List.sumBy (function
                | Accepted seats -> int64 seats
                | Rejected _ -> 0L)

        acceptedSeats + int64 allocation.Remaining = int64 sample.Capacity

    let preservesRequests sample =
        let actual =
            sample |> SeatAllocation.allocate |> _.Decisions |> List.map requestedSeats

        actual = sample.Requests

    let remainingIsBounded sample =
        let remaining = (SeatAllocation.allocate sample).Remaining
        0 <= remaining && remaining <= sample.Capacity

    let isOversubscribed sample =
        (sample.Requests |> List.sumBy int64) > int64 sample.Capacity

    // Plausible, but false: a rejected large request can be followed by a smaller accepted one.
    let acceptedRequestsFormPrefix sample =
        sample
        |> SeatAllocation.allocate
        |> _.Decisions
        |> List.fold
            (fun (stillValid, hasRejected) decision ->
                match decision with
                | Accepted _ -> stillValid && not hasRejected, hasRejected
                | Rejected _ -> stillValid, true)
            (true, false)
        |> fst
```
Keeping the property body separate from the test attribute makes the relationship easy to read and reuse. The attribute configures the runner; it does not define the domain rule.

## Generate the domain, not accidental noise {#generation}

A `Gen<'T>` describes how to produce values as size and pseudo-random state vary. It does not produce a value when declared. FsCheck's generator computation expression composes dependent choices without mutation:

```fsharp:line-numbers [Generators.fs]
module private AllocationCaseGen =
    let private general size =
        let largest = max 1 (min 40 (size + 1))
        let longest = min 12 size

        gen {
            let! capacity = Gen.choose (0, largest)
            let! length = Gen.choose (0, longest)
            let! requests = Gen.choose (1, largest + 1) |> Gen.listOfLength length
            return AllocationCase.assumeValid capacity requests
        }

    let private rejectionThenFit size =
        let largest = max 1 (min 40 (size + 1))

        gen {
            let! capacity = Gen.choose (1, largest)
            let! tooLarge = Gen.choose (capacity + 1, capacity + largest)
            let! fits = Gen.choose (1, capacity)
            return AllocationCase.assumeValid capacity [ tooLarge; fits ]
        }

    let generator =
        Gen.sized (fun size -> Gen.frequency [ 4, general size; 1, rejectionThenFit size ])
```
The general branch creates nonnegative capacity and positive requests. The targeted branch creates an oversized request followed by one that fits. `Gen.frequency` gives the branches weights 4 and 1; those weights are relative and do not guarantee exact percentages in a finite run.

This targeting is useful because “a smaller request after a rejection” is an important business case. The invariants must hold for both branches. A generator should expose meaningful corner cases, not secretly encode the answer that the property expects.

### Prefer construction over filtering {#construction-not-filtering}

Generating arbitrary integers and then writing `capacity >= 0 ==> ...` wastes cases. Filtering is especially dangerous when valid inputs are rare: the run may exhaust its rejection budget, and the surviving distribution may be badly skewed.

Construct positive requests with `Gen.choose (1, upper)` and choose bounded list lengths directly. Use `Gen.filter` only when acceptance is high and direct construction would obscure the model. Keep smart constructors at the final step so generator mistakes fail near generation instead of silently entering the property.

Generator bounds are test-design choices, not domain limits. This sample caps values and list lengths so 300 cases remain fast and readable. Keep concrete example tests for true extremes such as `Int32.MaxValue`, overflow behavior, and a known maximum payload; random generation may never select them.

## Pair generation with shrinking {#shrinking}

An `Arbitrary<'T>` combines a `Gen<'T>` with a shrinker of type `'T -> seq<'T>`. After a failure, FsCheck tries candidates from that sequence and continues recursively from those that still fail. Shrinking searches for a smaller counterexample under the supplied strategy; it does not promise a unique global minimum.

The sample shrinker removes one request, lowers capacity, and lowers one request at a time:

```fsharp:line-numbers [Generators.fs]
module private AllocationCaseShrink =
    let private removeEach requests =
        requests
        |> List.indexed
        |> Seq.map (fun (index, _) -> List.removeAt index requests)

    let private shrinkOneRequest requests =
        seq {
            for index, request in List.indexed requests do
                for smaller in 1 .. request - 1 do
                    yield List.updateAt index smaller requests
        }

    let shrink sample =
        seq {
            for requests in removeEach sample.Requests do
                yield AllocationCase.assumeValid sample.Capacity requests

            for capacity in 0 .. sample.Capacity - 1 do
                yield AllocationCase.assumeValid capacity sample.Requests

            for requests in shrinkOneRequest sample.Requests do
                yield AllocationCase.assumeValid sample.Capacity requests
        }
        |> Seq.distinct
```
Every candidate still has nonnegative capacity and positive requests. It also reduces either list length or a number, so shrinking moves toward a base case instead of cycling. `Seq.distinct` removes duplicate candidates without changing validity.

The registration with FsCheck is small:

```fsharp
type AllocationCaseArbitrary =
    static member AllocationCase() : Arbitrary<AllocationCase> =
        Arb.fromGenShrink(
            AllocationCaseGen.generator,
            AllocationCaseShrink.shrink
        )
```

If a type's default generator already has the right distribution and invariants, use it. A custom `Arbitrary` earns its complexity only when it improves validity, distribution, performance, or counterexample quality.

### Shrink within the valid domain {#valid-shrinks}

A shrinker that turns a positive request into zero forces the property to handle values forbidden by both generator and public API. That failure diagnoses the shrinker, not the allocator. Shrink sorted lists to smaller sorted lists, nonempty identifiers to shorter valid identifiers, and state-machine traces while preserving legal transitions.

Overaggressive shrinking can also hide useful context. If two fields must stay related, shrink them together. When a shrinker becomes nontrivial, test it with sampled values or structural properties: every candidate should remain valid and become strictly simpler under a stated measure.

## Observe the input distribution {#classification}

A passing run says little if it generated mostly empty queues. `Prop.classify condition label property` records labels for cases satisfying each condition. Labels may overlap:

```fsharp
[<Property(MaxTest = 300)>]
let ``remaining capacity stays within bounds`` (sample: AllocationCase) =
    AllocationProperties.remainingIsBounded sample
    |> Prop.classify
        (AllocationCase.requests sample |> List.isEmpty)
        "empty"
    |> Prop.classify
        (AllocationProperties.isOversubscribed sample)
        "oversubscribed"
```

Temporarily set `QuietOnSuccess = false`, or run the property interactively, to inspect the summary. Classification reports distribution but does not fail when a class is absent. If a region is mandatory, make the generator produce it reliably and add a focused property or coverage assertion supported by the chosen FsCheck API.

`Prop.collect` groups arbitrary observations such as list length. Use a few labels tied to risk. Dozens of incidental buckets produce noise and can make a run look scientific without improving its ability to find defects.

## A counterexample can disprove a plausible property {#wrong-property}

This statement sounds plausible: “accepted requests form a prefix; after one rejection, every later request is rejected.” It is false for a greedy allocator that continues processing. With capacity 1 and requests `[2; 1]`, request 2 is rejected and request 1 is then accepted.

The sample keeps the false property as a named function, runs it with a collecting runner, and expects `TestResult.Failed`. The suite remains green by asserting that FsCheck disproves the proposed property:

```fsharp
let config =
    Config.Quick
        .WithMaxTest(300)
        .WithArbitrary([ typeof<AllocationCaseArbitrary> ])
        .WithReplay(13285693176119930639UL, 18364232908344279255UL, 4)
        .WithRunner(runner)

Check.One(
    "accepted requests form a prefix",
    config,
    AllocationProperties.acceptedRequestsFormPrefix
)

match runner.Result with
| Some(TestResult.Failed(data, _, shrunkArguments, _, _, _, _)) ->
    let shrunk = shrunkArguments |> List.exactlyOne |> unbox<AllocationCase>
    Assert.True(data.NumberOfShrinks > 0)
    Assert.Equal(1, AllocationCase.capacity shrunk)
    Assert.Equal<int list>([ 2; 1 ], AllocationCase.requests shrunk)
| _ -> Assert.Fail("expected a falsified property")
```

The counterexample does not say whether code or property is wrong. Return to the requirement. Under “stop after the first rejection,” the allocator would be wrong; under the stated continue-processing rule, the proposed property is wrong. Property testing finds disagreement, while domain reasoning identifies its source.

## Reproduce a failure with replay data {#replay}

A failure report includes the initial seed, the seed and size at the failing generation step, the original argument, and the shrunk argument. In `PropertyAttribute`, `Replay = "seed,gamma"` restarts the run. `Replay = "seed,gamma,size"` jumps directly to the reported failing step. `Config.WithReplay` provides corresponding overloads.

The two unsigned 64-bit values are pseudo-random state, not user data. During diagnosis, record the full triple printed after “Replay directly at failing step.” First reproduce the failure, then preserve the smallest business-relevant counterexample as a named example test when it guards an important regression.

Replay is most dependable with the same property, generator, shrinker, target runtime, and FsCheck version. The sample project uses `FsCheck.Xunit` 3.4.0, which pins `FsCheck` 3.4.0. Changing generation order or upgrading the package may change a seed's input; the concrete regression example then remains the durable check.

Do not make every ordinary passing run use one seed. Varying seeds searches new cases in routine runs; stored replay information is for diagnosis and stable demonstrations. A CI failure must print enough information to reproduce it locally.

## Combine property tests with other test types {#complementary-tests}

Property tests work best for pure, deterministic logic with a large structured input space. They do not replace Chapter 28's tests for a known example, a specific error, side-effect behavior, or real serializer integration.

| Need | Best starting test |
|---|---|
| Explain one business rule with concrete values | Example test |
| Search many values for an invariant or model mismatch | Property test |
| Verify a serializer, database mapping, or public metadata | Integration contract test |
| Verify components with real infrastructure | Integration test |
| Verify a critical deployment-like path | A few end-to-end tests |

Keep property bodies pure when possible. Random network requests against shared infrastructure create slow, flaky failures that are hard to shrink and may damage external state. Use properties for pure request construction and decisions; use controlled contract or integration cases for the external protocol.

### Cost and failure readability set the test count {#test-count}

More cases do not provide confidence for free. A cheap pure property may run thousands of cases; one that allocates large arrays may need fewer cases and tighter size limits. Measure the suite, keep local feedback fast, and reserve longer campaigns for a separate job when they add value.

One hundred well-distributed cases with a readable shrinker can be more useful than ten thousand nearly identical cases. When a failure report is enormous, improve representation and shrinking before merely increasing the count.

## Run and diagnose this chapter {#running}

After placing the shown properties in your test project, replace the template path and run:

```console
dotnet test path/to/YourTests.fsproj \
  --configuration Release \
  --filter FullyQualifiedName~Ch29
```

Three properties each require 300 successful cases. The fourth uses a fixed failing-step replay and asserts that the false prefix property shrinks to capacity 1 with requests `[2; 1]`. Then run the whole test project without the filter before committing.

When a new property fails, read the report in this order: property name and labels, exception or false result, shrunk argument, original argument, then replay triple. Reproduce it before editing. Decide whether the implementation, property, generator, or shrinker broke its rule; guessing from the smallest value alone often fixes the wrong layer.

## Exercises {#exercises}

### Exercise 1: derive independent properties {#exercise-01}

For the allocator, propose one additional correct property and one concrete example that should remain outside it. Explain why the property is independent of the fold implementation and identify a defect it would catch.


::: details Answer

#### Appending input must not rewrite prior decisions {#exercise-01-prefix-stability}

The allocator is specified as a streaming process: it handles requests in order and never revisits an earlier decision. Therefore appending one positive request must leave the original decision list unchanged as a prefix.

```fsharp
let appendingRequestPreservesPriorDecisions
    (sample: AllocationCase)
    (PositiveInt extra)
    =
    let original = SeatAllocation.allocate sample

    let extended =
        AllocationCase.create
            (AllocationCase.capacity sample)
            (AllocationCase.requests sample @ [ extra ])
        |> Result.map SeatAllocation.allocate

    match extended with
    | Error _ -> false
    | Ok allocation ->
        allocation.Decisions
        |> List.take original.Decisions.Length
        |> (=) original.Decisions
```

This property comes from the declared streaming semantics rather than reproducing the capacity fold. It would catch an implementation that sorts requests, globally optimizes a batch, or rebuilds earlier decisions after seeing later input.

`PositiveInt` supplies a positive appended value, but a project-specific generator can instead keep all input policy in `AllocationCaseArbitrary`. If converting `PositiveInt` to `int`, use its `Get` member explicitly in code where inference is unclear.

Keep capacity 5 with requests `[2; 4; 3]` as an exact example: it documents that the allocator accepts 2, rejects 4, accepts 3, and reaches zero. The property establishes prefix stability over many inputs but does not communicate that concrete policy nearly as clearly.

Another valid property replays decisions with a small verifier: each `Accepted n` must fit the current remainder and subtract `n`; each `Rejected n` must exceed that remainder. This model is useful when written independently and named after the policy, but it should not reuse the production fold function.

:::

### Exercise 2: design generation and shrinking {#exercise-02}

Extend `AllocationCase` with a nonempty event identifier containing uppercase ASCII letters and digits. Design a generator and shrinker that preserve every invariant without a low-yield filter. Give a simplicity measure that prevents shrink cycles, and name two distribution classes worth observing.


::: details Answer

#### Construct the identifier from its alphabet {#exercise-02-generator}

Make invalid characters impossible at the generator level:

```fsharp
let identifierGenerator =
    let alphabet = [ 'A' .. 'Z' ] @ [ '0' .. '9' ]

    Gen.sized (fun size ->
        gen {
            let! length = Gen.choose(1, max 1 (min 12 (size + 1)))
            let! characters = Gen.elements alphabet |> Gen.listOfLength length
            return System.String(characters |> List.toArray)
        })
```

This always produces length 1 through 12 and only permitted characters. There is no rejection loop. Pass the result through the identifier smart constructor when assembling the full case so the generator fails visibly if domain rules later change.

#### Shrink while remaining nonempty and legal {#exercise-02-shrinker}

A simple identifier shrinker can first remove one character when length exceeds one, then replace one character with an earlier member of the alphabet. It must never yield the empty string or a character outside the alphabet.

```fsharp
let shrinkIdentifier (value: string) =
    seq {
        if value.Length > 1 then
            for index in 0 .. value.Length - 1 do
                yield value.Remove(index, 1)

        for index in 0 .. value.Length - 1 do
            if value[index] <> 'A' then
                let chars = value.ToCharArray()
                chars[index] <- 'A'
                yield System.String chars
    }
    |> Seq.distinct
```

For the complete allocation case, combine identifier candidates with the existing capacity and request candidates one field at a time. A well-founded lexicographic measure is `(identifier length, character-rank sum, request count, capacity, request sum)`. Every emitted candidate must strictly decrease an earlier component without increasing any earlier component, so an infinite cycle is impossible.

Useful classifications include `single-character-id` and `contains-digit`. Depending on risk, also observe maximum-length identifiers or oversubscribed allocations. Labels describe the actual generated distribution; they do not replace a generator branch when a case must occur reliably.

The sample shrinker above favors readable `A` characters, but a team could prefer digits or preserve a required prefix. “Smaller” is a testing policy, not an intrinsic order on identifiers.

:::

### Exercise 3: interpret and preserve a failure {#exercise-03}

A property says that reversing the request list cannot change the accepted-seat total. FsCheck finds capacity 2 with requests `[1; 2]`. Decide whether the property matches the greedy rule, write the smallest concrete regression example, and distinguish temporary replay data from the permanent test.


::: details Answer

#### Greedy allocation is intentionally order-sensitive {#exercise-03-counterexample}

With capacity 2 and requests `[1; 2]`, the allocator accepts 1, rejects 2, and accepts a total of 1. Reversing the list to `[2; 1]` accepts 2, rejects 1, and accepts a total of 2. The claimed permutation invariance contradicts the greedy, ordered policy; this counterexample does not reveal an allocator defect.

Preserve the behavior as an explicit example:

```fsharp
let allocate capacity requests =
    AllocationCase.create capacity requests
    |> Result.map SeatAllocation.allocate

let acceptedTotal allocation =
    allocation.Decisions
    |> List.sumBy (function Accepted seats -> seats | Rejected _ -> 0)

let forward = allocate 2 [ 1; 2 ] |> Result.map acceptedTotal
let reversed = allocate 2 [ 2; 1 ] |> Result.map acceptedTotal

Assert.Equal(Ok 1, forward)
Assert.Equal(Ok 2, reversed)
```

During diagnosis retain the original and shrunk arguments, the direct replay triple `(seed, gamma, size)`, FsCheck version, and relevant code revision. They let the exact run be reproduced before a fix or requirement decision.

Permanently retain the named example and its expected totals. Do not make the seed the business contract: generator order or a pinned dependency upgrade can change its meaning. The concrete input remains understandable and stable.

A corrected property could say that when total requested seats do not exceed capacity, every request is accepted and accepted total is order-independent. Without that precondition, preserve only true invariants such as conservation and bounds.

:::


## Sources {#sources}

- [FsCheck: writing and observing properties](https://fscheck.github.io/FsCheck/Properties.html)
- [FsCheck: generators, shrinkers, and arbitrary instances](https://fscheck.github.io/FsCheck/TestData.html)
- [FsCheck: runners, xUnit integration, and replay](https://fscheck.github.io/FsCheck/RunningTests.html)
- [NuGet: FsCheck.Xunit 3.4.0 package and dependencies](https://www.nuget.org/packages/FsCheck.Xunit/)
