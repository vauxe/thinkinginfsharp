---
title: "Chapter 29 Solutions"
description: "Derive an independent streaming property, design a valid identifier generator and shrinker, and turn an order-sensitive counterexample into a durable regression example."
translationKey: solutions/ch-29-property-testing
---

# Chapter 29 Solutions {#overview}

These are reference solutions, not the only correct properties or generators. Each one states the requirement before showing code, keeps generated and shrunk values valid, and separates temporary reproduction data from permanent regression tests.

[Return to Chapter 29](../part-05/ch-29-property-testing).

## Exercise 1: derive independent properties {#exercise-01}

### Appending input must not rewrite prior decisions {#exercise-01-prefix-stability}

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

## Exercise 2: design generation and shrinking {#exercise-02}

### Construct the identifier from its alphabet {#exercise-02-generator}

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

### Shrink while remaining nonempty and legal {#exercise-02-shrinker}

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

## Exercise 3: interpret and preserve a failure {#exercise-03}

### Greedy allocation is intentionally order-sensitive {#exercise-03-counterexample}

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

## Solution review {#solution-review}

- Prefix stability follows from streaming semantics and catches sorting or batch reoptimization.
- An exact interleaved example explains policy more clearly than the general property.
- Construct identifiers from the allowed alphabet instead of filtering arbitrary strings.
- A shrinker must preserve nonemptiness, alphabet rules, and a decreasing simplicity measure.
- Classification reveals distribution but does not guarantee coverage.
- The reverse-order counterexample disproves the property, not the specified allocator.
- Keep replay metadata for diagnosis and a concrete named example for permanent regression protection.
- Restrict a false general claim with a justified precondition, or replace it with a true invariant.
