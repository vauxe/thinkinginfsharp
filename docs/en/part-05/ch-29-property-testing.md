---
title: "Chapter 29: Property Testing with FsCheck"
description: "Move from selected examples to domain invariants, then control generation, classification, shrinking, and replay without treating randomized checks as proof."
translationKey: part-05/ch-29-property-testing
kind: chapter
part: 5
chapter: 29
status: complete
verifiedWith:
  fsharp: "10"
  dotnetSdk: "10.0.301"
exampleIds:
  - foundation-example-tests
exerciseIds:
  - ch29-exercise-01
  - ch29-exercise-02
  - ch29-exercise-03
termIds: []
sources:
  - id: fscheck-properties
    url: https://fscheck.github.io/FsCheck/Properties.html
    checked: "2026-08-24"
  - id: fscheck-test-data
    url: https://fscheck.github.io/FsCheck/TestData.html
    checked: "2026-08-24"
  - id: fscheck-running-tests
    url: https://fscheck.github.io/FsCheck/RunningTests.html
    checked: "2026-08-24"
  - id: nuget-fscheck-xunit
    url: https://www.nuget.org/packages/FsCheck.Xunit/
    checked: "2026-08-24"
---

# Chapter 29: Property Testing with FsCheck {#overview}

An example test asks whether one chosen input has one expected output. A property test asks whether a relationship survives many generated inputs. Both are executable examples; FsCheck does not prove a theorem or inspect every possible value. Its advantage is that it searches a much wider input space than a short hand-written table and, on failure, tries to reduce the input to a smaller counterexample.

The difficult part is therefore not writing `[<Property>]`. It is stating a useful invariant, generating the domain rather than meaningless noise, observing the distribution, and reading a failure without confusing a seed with a business requirement. This chapter develops those skills around a greedy seat allocator.

## What you will be able to do {#outcomes}

By the end of this chapter, you should be able to:

- generalize concrete examples into invariants without turning the implementation into its own oracle;
- distinguish universal properties, model comparisons, algebraic laws, and round trips;
- use FsCheck's `PropertyAttribute` with xUnit 2;
- construct valid domain inputs with `Gen<'T>` instead of filtering mostly invalid values;
- combine a generator and validity-preserving shrinker as `Arbitrary<'T>`;
- classify generated cases and inspect whether important regions are represented;
- understand size, test count, rejection, and shrinking as separate controls;
- reproduce a failure with its seed, gamma, and optional size;
- keep example, property, boundary, and integration tests in complementary roles.

## Generalize examples into invariants {#examples-to-invariants}

Suppose an allocator starts with a capacity and processes positive seat requests in order. It accepts a request when it fits in the remaining capacity and otherwise rejects it. A useful example is capacity 5 with requests `[2; 4; 3]`: the first and last requests are accepted, the middle request is rejected, and zero seats remain.

That example is valuable because it communicates one policy decision. It does not search empty input, zero capacity, repeated requests, exact fits, or many interleavings of accepted and rejected requests. Instead of writing hundreds of expected lists, ask what must remain true for every valid input:

1. accepted seats plus remaining seats equal the initial capacity;
2. every request appears exactly once in the decisions and in original order;
3. remaining capacity stays between zero and initial capacity.

The sample makes valid inputs unrepresentable outside a smart constructor and keeps each decision explicit:

<<< @/../examples/chapters/ch29/Generators.fs#allocation-core{fsharp:line-numbers} [Generators.fs]

The three statements describe relationships, not particular outputs. They tolerate many correct implementation changes. They also constrain different failure modes: conservation catches lost or invented capacity, preservation catches skipped or reordered requests, and bounds catch over-allocation.

### A property still needs an independent reason {#independent-oracle}

This property is nearly useless:

```fsharp
let allocationMatchesItself sample =
    SeatAllocation.allocate sample = SeatAllocation.allocate sample
```

So is a test that copies the allocator's fold into the expected side. A shared defect can make implementation and oracle agree. Derive a property from a domain rule, an algebraic law, a simpler reference model, or a trusted inverse—not from the source expression being tested.

Common property shapes include:

| Shape | Question | Example |
|---|---|---|
| Invariant | What must always remain true? | Capacity is conserved |
| Round trip | Can encoding and decoding recover a value? | `decode (encode value) = Ok value` |
| Algebraic law | What composition law should hold? | Set union is associative |
| Model comparison | Does the optimized version agree with a simpler one? | Indexed lookup agrees with linear search |
| Metamorphic relation | How should transformed input change output? | Sorting twice equals sorting once |

Not every domain has elegant algebra. A small model or a few exact examples may be clearer than a forced “law.” The property name should say what users can rely on, not merely that a function “works.”

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

`MaxTest = 300` asks for 300 successful cases. It is not a coverage percentage and does not mean 300 distinct values. FsCheck gradually changes a size parameter; generators decide how size affects magnitudes or collection lengths. Cases rejected by a conditional property do not count as successful and are bounded separately by `MaxRejected`.

The shared property functions are ordinary pure functions and can also be called by example tests or FSI:

<<< @/../examples/chapters/ch29/Generators.fs#property-functions{fsharp:line-numbers} [Generators.fs]

Keeping the property body separate from the test attribute makes the claim easy to read and reuse. The attribute is runner configuration; it is not the domain specification.

## Generate the domain, not accidental noise {#generation}

A `Gen<'T>` describes how to produce values as size and pseudo-random state vary. It does not produce a value when declared. FsCheck's generator computation expression composes dependent choices without mutation:

<<< @/../examples/chapters/ch29/Generators.fs#generator{fsharp:line-numbers} [Generators.fs]

The general branch creates nonnegative capacity and only positive requests. The targeted branch creates a request too large to fit followed by one that can fit. `Gen.frequency` chooses the general branch with weight 4 and the targeted branch with weight 1; weights are relative, not guaranteed percentages in a finite run.

This targeting is legitimate because “a later small request after a rejection” is an important business shape. The correct invariants must survive both branches. A generator should expose meaningful corners, not secretly encode the answer a property hopes to see.

### Prefer construction over filtering {#construction-not-filtering}

Generating arbitrary integers and then writing `capacity >= 0 ==> ...` wastes cases. Filtering is especially dangerous when valid inputs are rare: the run may exhaust its rejection budget, and the surviving distribution may be badly skewed.

Construct positive requests with `Gen.choose (1, upper)` and bounded list lengths directly. Use `Gen.filter` only when the predicate has a high acceptance rate and direct construction would obscure the model. Smart constructors remain useful at the final step: if generator code drifts, invalid data should fail near generation rather than enter the property silently.

Bounds in a generator are test-design choices, not domain limits. This sample caps values and list lengths so 300 cases stay fast and readable. Keep explicit example tests for true extremes such as `Int32.MaxValue`, arithmetic overflow policy, and a known maximum payload; random generation is not guaranteed to select them.

## Pair generation with shrinking {#shrinking}

An `Arbitrary<'T>` bundles a `Gen<'T>` with a shrinker of type `'T -> seq<'T>`. After a failure, FsCheck tries candidates from that sequence and recursively continues from candidates that still fail. Shrinking seeks a smaller counterexample according to the supplied strategy; it does not promise a globally unique mathematical minimum.

The sample shrinker removes one request, lowers capacity, and lowers one request at a time:

<<< @/../examples/chapters/ch29/Generators.fs#shrinker{fsharp:line-numbers} [Generators.fs]

Every candidate still has nonnegative capacity and positive requests. Each candidate also decreases list length or a number, so shrinking moves toward a base case instead of cycling. `Seq.distinct` removes duplicate candidates; it does not change validity.

The bundle registered with FsCheck is small:

```fsharp
type AllocationCaseArbitrary =
    static member AllocationCase() : Arbitrary<AllocationCase> =
        Arb.fromGenShrink(
            AllocationCaseGen.generator,
            AllocationCaseShrink.shrink
        )
```

If a type's default generator already has the right distribution and invariants, use it. A custom `Arbitrary` earns its complexity only when it improves validity, distribution, performance, or counterexample quality.

### Shrink invariants, not only representation size {#valid-shrinks}

A shrinker that turns a positive request into zero forces the property to handle values the generator and public API forbid. That failure diagnoses the shrinker, not the allocator. For a sorted list, shrink to smaller sorted lists; for a nonempty identifier, shrink toward a short valid identifier; for a state machine trace, preserve legal transitions.

Overaggressive shrinking can also hide useful context. If two fields must remain related, shrink them together. Test the shrinker itself with sampled values or structural properties when it becomes nontrivial: every candidate should be valid and strictly simpler under an explicit measure.

## Observe the input distribution {#classification}

A passing run can be weak evidence if it generated mostly empty queues. `Prop.classify condition label property` records labels for cases satisfying each condition. Labels may overlap:

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

Set `QuietOnSuccess = false` temporarily or run the property interactively to inspect the summary. Classification observes distribution; by itself it does not fail when a class is absent. If a region is mandatory, design the generator to produce it reliably and add a focused property or explicit coverage assertion supported by the chosen FsCheck API.

`Prop.collect` groups arbitrary observations such as list length. Use a few labels tied to risk. Dozens of incidental buckets produce noise and can make a run look scientific without improving its ability to find defects.

## Let a wrong property meet a counterexample {#wrong-property}

This statement sounds plausible: “accepted requests form a prefix; after one rejection, every later request is rejected.” It is false for a greedy allocator that continues processing. With capacity 1 and requests `[2; 1]`, request 2 is rejected and request 1 is then accepted.

The sample keeps the false property as a named function, runs it under a collecting runner, and expects `TestResult.Failed`. The repository remains green because the test asserts that FsCheck disproves the claim:

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

The counterexample does not automatically say whether code or property is wrong. Return to the requirement. If the policy were “stop after the first rejection,” the allocator would be wrong; under the stated continue-processing policy, the proposed property is wrong. Property testing finds disagreement, while domain reasoning assigns blame.

## Replay a failure precisely {#replay}

A failure report includes the initial seed, the seed and size at the failing generation step, the original argument, and the shrunk argument. In `PropertyAttribute`, `Replay = "seed,gamma"` restarts a run, while `Replay = "seed,gamma,size"` can jump directly to the reported failing step. In `Config`, `WithReplay` provides corresponding overloads.

The two unsigned 64-bit values are pseudo-random state, not user data. Record the full triple printed after “Replay directly at failing step” when debugging. First replay the exact failure, then promote the smallest business-relevant counterexample into a named example test if it guards an important regression.

Replay is most dependable with the same property, generator, shrinker, target runtime, and FsCheck version. This repository pins `FsCheck.Xunit` 3.4.0, which pins `FsCheck` 3.4.0. Changing generation order or upgrading the package may change which input a seed produces; the explicit regression example then remains the durable contract.

Do not make every ordinary passing run use one seed. Varying seeds searches new cases in routine runs; stored replay information is for diagnosis and stable demonstrations. A CI failure must print enough information to reproduce it locally.

## Use property tests alongside other evidence {#complementary-tests}

Property tests are strongest for pure, deterministic logic with a large structured input space. They do not replace the Chapter 28 tests that explain a known example, verify an exact error message, observe effect protocol, or execute a real serializer boundary.

| Need | Best starting evidence |
|---|---|
| Explain one business rule with concrete values | Example test |
| Search many values for an invariant or model mismatch | Property test |
| Verify a serializer, database mapping, or public metadata | Boundary contract test |
| Verify components with real infrastructure | Integration test |
| Verify a critical deployed-shaped path | A few end-to-end tests |

Keep property bodies pure when possible. Generating random network requests against shared infrastructure produces slow, flaky failures that are difficult to shrink and may damage external state. Test pure request construction and decisions with properties; test the protocol boundary with controlled contract or integration cases.

### Cost and failure readability set the test count {#test-count}

More cases are not free confidence. A cheap pure property may run thousands of cases; a property allocating large arrays may need fewer and tighter size limits. Measure the suite, keep local feedback short, and reserve longer campaigns for a separate job when they add value.

One hundred well-distributed cases with a readable shrinker can be more useful than ten thousand nearly identical cases. When a failure report is enormous, improve representation and shrinking before merely increasing the count.

## Run and diagnose this chapter {#running}

Run the four Chapter 29 tests from the repository root:

```console
dotnet test tests/ExampleTests/ExampleTests.fsproj \
  --configuration Release \
  --filter FullyQualifiedName~Ch29
```

Three properties each require 300 successful cases. The fourth uses a fixed failing-step replay and asserts that the false prefix property shrinks to capacity 1 with requests `[2; 1]`. Before committing, run `pnpm check:examples` to restore locked packages, compile all projects, execute all tests, and check every registered example.

When a new property fails, read the report in this order: property name and labels, exception or false result, shrunk argument, original argument, then replay triple. Reproduce before editing. Decide whether the implementation, property, generator, or shrinker violated its contract; guessing from the smallest value alone often fixes the wrong layer.

## Exercises {#exercises}

### Exercise 1: derive independent properties {#exercise-01}

For the allocator, propose one additional correct property and one exact example that should remain outside the property. Explain why your property is independent of the fold implementation and identify a defect it would catch.

### Exercise 2: design generation and shrinking {#exercise-02}

Extend `AllocationCase` with a nonempty event identifier consisting of uppercase ASCII letters and digits. Design a generator and shrinker that preserve all invariants without a low-yield filter. State a simplicity measure that proves shrinking cannot cycle, and name two distribution classes worth observing.

### Exercise 3: interpret and preserve a failure {#exercise-03}

A property claims that reversing the request list cannot change the accepted-seat total. FsCheck finds capacity 2 with requests `[1; 2]`. Determine whether the property matches the greedy policy, write the smallest explicit regression example, and describe what replay information you would retain during diagnosis versus what you would keep permanently.

[Read the chapter solutions](../solutions/ch-29-property-testing).

## Model review {#model-review}

- A property test samples many generated cases; it is strong evidence, not exhaustive proof.
- Derive properties from domain invariants, algebra, inverses, or simpler models rather than copying implementation.
- `Gen<'T>` produces values; a shrinker proposes smaller candidates; `Arbitrary<'T>` bundles both.
- Construct valid values directly and use filtering only when acceptance is high.
- Target meaningful shapes while inspecting distribution with classification or collection.
- Shrinkers must preserve domain validity and move toward a well-founded simpler case.
- A minimal counterexample reveals disagreement; requirements decide whether code or property is wrong.
- Replay uses seed, gamma, and optionally size, and depends on stable code and package versions.
- Preserve important discovered failures as clear example tests.
- Example, property, contract, integration, and end-to-end tests answer different risks.

## Sources {#sources}

- [FsCheck: writing and observing properties](https://fscheck.github.io/FsCheck/Properties.html)
- [FsCheck: generators, shrinkers, and arbitrary instances](https://fscheck.github.io/FsCheck/TestData.html)
- [FsCheck: runners, xUnit integration, and replay](https://fscheck.github.io/FsCheck/RunningTests.html)
- [NuGet: FsCheck.Xunit 3.4.0 package and dependencies](https://www.nuget.org/packages/FsCheck.Xunit/)
