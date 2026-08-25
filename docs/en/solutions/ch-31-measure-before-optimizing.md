---
title: "Chapter 31 Solutions"
description: "Bound conclusions to the captured benchmark, design a behavior-preserving option versus voption allocation experiment, and choose evidence for three different system symptoms."
translationKey: solutions/ch-31-measure-before-optimizing
---

# Chapter 31 Solutions {#overview}

These solutions keep each conclusion attached to its workload and environment. A plausible mechanism is not yet a measured cause, and a locally measured cause is not yet an end-to-end improvement.

[Return to Chapter 31](../part-05/ch-31-measure-before-optimizing).

## Exercise 1: state only supported conclusions {#exercise-01}

### Separate observations from extrapolations {#exercise-01-claims}

Three supported conclusions are:

1. The two implementations returned equal results for four named cases and 256 fixed-seed generated cases, including the exact-fit boundary that initially failed.
2. On the captured macOS arm64, .NET 10.0.9 ShortRun, `SinglePass` measured a ratio of 0.43 relative to `ArrayPipeline` for both 256 and 4,096 elements.
3. MemoryDiagnoser reported 520 B and 7,504 B per pipeline operation at those sizes, while reporting 0 B per single-pass operation; this agrees with the intermediate-array hypothesis.

Three unsupported claims are:

1. “F# loops are always 2.3 times faster than pipelines.” Only two concrete functions, one distribution, two sizes, and one environment were compared.
2. “Arrays are faster than lists or sequences.” Neither alternative collection was measured, and their semantics differ.
3. “The application will improve p95 latency by 57%.” The microbenchmark excludes the rest of the application, and no end-to-end share or result was provided.

It is also unsupported to claim that 0 B means no process memory use or that 260 cases prove equivalence for all integers. State exactly what the tools observed.

Dry performs one cold-start measurement for each benchmark. It proves discovery, generated-project build, setup, execution, and result collection work. One sample has no useful variance estimate; startup and JIT effects dominate these very small operations, and the diagnoser documents its allocation accuracy for ShortRun or longer jobs. Dry is therefore a smoke signal, not a replacement baseline.

Even ShortRun is deliberately brief. Its three measurements and uncontrolled workstation make the stored record appropriate for this chapter's hypothesis, not for a production service-level threshold.

## Exercise 2: design an `option` versus `voption` experiment {#exercise-02}

### Keep algorithm, inputs, and returned observable equal {#exercise-02-design}

Begin with two functions that differ only in optional representation:

```fsharp
[<Struct>]
type SmallValue = { Code: int }

let tryReadOption (values: SmallValue array) index =
    if uint index < uint values.Length then Some values[index] else None

let tryReadValueOption (values: SmallValue array) index =
    if uint index < uint values.Length then ValueSome values[index] else ValueNone

let checksumOption values indexes =
    let mutable total = 0

    for index in indexes do
        match tryReadOption values index with
        | Some value -> total <- total + value.Code
        | None -> ()

    total

let checksumValueOption values indexes =
    let mutable total = 0

    for index in indexes do
        match tryReadValueOption values index with
        | ValueSome value -> total <- total + value.Code
        | ValueNone -> ()

    total
```

Returning the same `int` checksum consumes both results without asking BenchmarkDotNet to compare differently represented return values. Keep the call boundary faithful to production: if the real lookup cannot be inlined across an assembly boundary, the benchmark should not accidentally make it a local inline function.

Before timing, compare both checksums for empty indexes, all absent, all present, and mixed inputs. Add fixed-seed generated index arrays and exact edge indexes `-1`, `0`, `Length - 1`, and `Length`. If the payload has invariants, construct it through the same validated path used by production.

In `GlobalSetup`, create one value array and index arrays with, for example, 0%, 50%, and 100% present cases. Parameterize the batch length and present percentage, but give both benchmark methods the identical prebuilt array. Do not generate random indexes, log, or allocate fixtures inside a measured method.

Run Release without a debugger, mark one implementation as the baseline, return the checksum, enable MemoryDiagnoser, and use ShortRun only for an initial direction. A consequential API change deserves longer runs on a production-like runtime and CPU.

Observe mean, spread, ratio, bytes allocated per operation, and Gen0 frequency. The hypothesis predicts less wrapper allocation for `voption`; it does not predetermine a throughput win. If both allocate 0 B, inspect whether JIT inlining or escape analysis removed the wrapper in this call shape.

Confounders include payload size and struct-copy cost, hit distribution, branch prediction, lookup work overwhelming wrapper cost, boxing through `obj`, generic or interface boundaries, compiler/JIT versions, and whether the real caller retains the result. Test the actual public boundary before changing its type.

Finally rerun the application allocation profile and user-visible workload. Keep `option` if the proposed representation does not materially improve the real requirement or if the public-contract cost outweighs the measured gain.

## Exercise 3: choose the next measurement {#exercise-03}

### Match each symptom to its boundary {#exercise-03-observations}

For high p95 API latency, first reproduce representative concurrency and payloads while recording end-to-end p50/p95/p99, throughput, and error rate. Observe runtime counters for CPU, GC, thread-pool queueing, and exceptions; collect a trace spanning slow requests when counters identify a likely class of cause.

An implementation experiment is justified when the trace or queue evidence attributes a material part of slow requests to a controllable path. After the change, rerun the same load and compare tail latency, throughput, errors, and resource use. A faster isolated helper is insufficient.

For high allocation rate already associated with an aggregation call stack, confirm bytes per request and call frequency with an allocation trace or counters. Then isolate that aggregation in a microbenchmark using representative sizes and values, plus an equivalence gate. Experiment when its estimated rate is material to GC pressure.

Afterward, remeasure benchmark allocation, process allocation rate, GC frequency or pause behavior, and the original end-to-end observable. An allocation decrease that raises CPU or leaves service behavior unchanged may not justify lower-level code.

For slow command-line startup, first measure repeated cold launches to a defined readiness event and record the distribution. Use a startup trace to separate runtime/JIT work, module initialization, dependency loading, configuration, and I/O. A steady-state aggregation benchmark answers the wrong question.

If JIT or loading is material, compare an actual published alternative such as Native AOT only after its compatibility analysis succeeds. Measure cold readiness, working set, file size, publish time, and functional behavior for the target RID. If I/O or eager initialization dominates, repair that cause instead.

In every case, the next tool is chosen by the unresolved question. Counters classify, traces attribute, microbenchmarks compare isolated mechanisms, and end-to-end measurements decide whether the requirement improved.

## Solution review {#solution-review}

- Observations name the exact methods, workload, job, runtime, and environment.
- Dry proves execution but supplies neither a stable timing estimate nor useful variance.
- A local ratio cannot predict an application's p95 improvement without end-to-end evidence.
- An option/value-option benchmark must preserve algorithm, inputs, call shape, and returned observable.
- Fixed present/absent distributions expose representation behavior better than one accidental mix.
- Lower allocation is a hypothesis about throughput and GC, not proof of either.
- Counters classify resource pressure; traces attribute it to call paths.
- Microbenchmarks compare a suspected mechanism after profiling, not before.
- Startup needs cold-process evidence and the real published artifact.
- Every optimization ends by remeasuring the original user-visible requirement.
