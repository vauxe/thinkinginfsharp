---
title: "Chapter 31: Measure Before Optimizing"
description: "Move from a user-visible performance requirement through profiling and a controlled F# benchmark to an equivalent, measured change without turning local results into universal rules."
translationKey: part-05/ch-31-measure-before-optimizing
---

# Chapter 31: Measure Before Optimizing {#overview}

Performance is behavior under a workload, on an environment, against a requirement. “This code looks fast” is not evidence. Neither is one stopwatch result, one profiler screenshot, or a microbenchmark copied from another machine. Begin with what a user or operator needs, locate the expensive path, change one cause, and measure the same observable again.

Efficient F# does not require abandoning expressions, immutability, or domain types. Clear code provides the baseline for testing an optimization hypothesis. When measurement identifies a hot loop, tightly scoped local mutation or a lower-level representation may be appropriate. Keep the public API as simple and accurate as requirements allow.

## Define the performance question first {#performance-question}

A useful performance statement contains four parts:

| Part | Example | Missing-part failure |
|---|---|---|
| Workload | Aggregate 4,096 requests with the observed value distribution | A tiny or artificial distribution may optimize the wrong path |
| Environment | .NET 10, arm64, Release, workstation or named production class | Runtime, JIT, CPU, GC, and power behavior differ |
| Observable | p95 request latency, operations/second, allocated bytes, startup time, or publish size | “Faster” combines unrelated outcomes |
| Target | p95 below 150 ms at 200 requests/second | Any change can be called successful |

Choose a metric that directly matches the requirement. If users report slow HTTP requests, start with end-to-end latency and throughput, not the nanoseconds of a list function. If containers restart too slowly, measure process readiness. If GC pauses dominate, measure allocation rate, heap behavior, and pauses rather than only CPU time.

Use percentiles when the tail of a distribution matters. An average can improve while p99 becomes worse. Record concurrency, data distribution, cache state, runtime configuration, and external dependencies with the result. Without those inputs, a benchmark number has no useful meaning.

## Measure in increasing detail {#evidence-ladder}

A reliable optimization loop is ordered:

1. Reproduce the relevant workload and record a baseline for the user-visible result.
2. Use counters or a profiler to locate where time, allocation, contention, or I/O accumulates.
3. State one causal hypothesis, including the observable it predicts will change.
4. If the suspected work is small and deterministic, isolate it in a microbenchmark.
5. Check functional equivalence with examples, properties, or a trusted reference implementation.
6. Make one focused change and rerun the same benchmark under comparable conditions.
7. Rerun the end-to-end workload; reject a locally faster change that does not improve the requirement.

Profiling answers “where is the process spending resources?” A microbenchmark answers “under this isolated setup, how do these implementations compare?” End-to-end measurement answers “did the system outcome improve?” None substitutes for the others.

Stop when the target is met or the measured benefit no longer justifies complexity. Optimization has maintenance, portability, code-size, and debugging costs. A five-percent local gain on a path consuming one percent of request time cannot materially fix the request.

## Case study: remove one measured intermediate allocation {#case-study}

The sample sums positive seat values no greater than a configured maximum. Its baseline is an idiomatic array pipeline; its candidate performs the same decision and addition in one pass:

```fsharp:line-numbers [Benchmarks.fs]
module RequestAggregation =
    let arrayPipeline maxSeats (requests: int array) =
        requests
        |> Array.filter (fun seats -> seats > 0 && seats <= maxSeats)
        |> Array.sumBy int64

    let singlePass maxSeats (requests: int array) =
        let mutable total = 0L

        for seats in requests do
            if seats > 0 && seats <= maxSeats then
                total <- total + int64 seats

        total
```
`arrayPipeline` exposes intent clearly: select qualifying elements, then sum their `int64` values. It also materializes the filtered array before the second traversal. That cost may be irrelevant outside a hot path, so the pipeline remains a good default when no measurement implicates it.

`singlePass` keeps a mutable accumulator inside one function. No mutable reference escapes, and the observable result remains a value. Local mutation is an implementation technique, not a demand that the domain model become mutable. The array itself must still not be concurrently modified during either calculation.

The candidate changes traversal structure and therefore creates correctness risk. The first deliberately faulty version used `< maxSeats` instead of `<= maxSeats`; for maximum 4 and input `[1; 4; 5; 0; -1; 2]`, the baseline returned 7 while the candidate returned 3. Measurement must wait until that semantic difference is repaired.

## Prove enough equivalence before timing {#equivalence}

Before any benchmark begins, the sample checks four named cases and 256 deterministically generated cases:

```fsharp:line-numbers [Benchmarks.fs]
module Equivalence =
    let private fixedCases =
        [| 0, [||]
           4, [| 1; 4; 5; 0; -1; 2 |]
           1, [| 1; 1; 2; -3 |]
           6, [| 6; 5; 4; 3; 2; 1 |] |]

    let verify () =
        let random = Random 31

        let generatedCases =
            Array.init 256 (fun length ->
                let maxSeats = random.Next(0, 8)
                let requests = Array.init length (fun _ -> random.Next(-2, 12))
                maxSeats, requests)

        Array.append fixedCases generatedCases
        |> Array.iteri (fun index (maxSeats, requests) ->
            let expected = RequestAggregation.arrayPipeline maxSeats requests
            let actual = RequestAggregation.singlePass maxSeats requests

            if actual <> expected then
                failwithf
                    "equivalence case %d failed: maxSeats=%d expected=%d actual=%d"
                    index
                    maxSeats
                    expected
                    actual)

        fixedCases.Length + generatedCases.Length
```
The reference implementation and candidate use different structures, which makes comparison useful. Cases include empty input, values exactly at the limit, rejected and negative values, and varying lengths. Passing 260 cases is not a mathematical proof; production rules may require more properties, overflow cases, or domain-level tests.

Run only the semantic gate while editing:

```console
dotnet run --project Ch31.Benchmarks.fsproj \
  --configuration Release --no-restore -- --verify-only
```

Do not encode a noisy time limit in this check. Correctness checks should be deterministic. Performance history can reveal a suspected regression, but a CI threshold requires controlled runners, repeated measurements, and a policy for variance.

## Design a benchmark that isolates the hypothesis {#benchmark-design}

The benchmark setup creates deterministic input in `GlobalSetup`, returns each computed sum, tests two data sizes, marks the pipeline as the baseline, and enables managed-allocation reporting:

```fsharp:line-numbers [Benchmarks.fs]
[<MemoryDiagnoser>]
type RequestAggregationBenchmarks() =
    let mutable requests = Array.empty<int>

    [<Params(256, 4096)>]
    member val Count = 0 with get, set

    [<GlobalSetup>]
    member this.Setup() =
        let random = Random 31
        requests <- Array.init this.Count (fun _ -> random.Next(-2, 12))

    [<Benchmark(Baseline = true)>]
    member _.ArrayPipeline() =
        RequestAggregation.arrayPipeline 6 requests

    [<Benchmark>]
    member _.SinglePass() =
        RequestAggregation.singlePass 6 requests
```
Each choice closes a common loophole:

- setup time is excluded from the operation being compared;
- the fixed seed gives both methods reproducible inputs;
- returning the result discourages dead-code elimination;
- parameters reveal whether the relationship changes with input size;
- `Baseline = true` gives ratios within each parameter group;
- `MemoryDiagnoser` reports managed allocation per operation and GC frequency.

The project locks BenchmarkDotNet 0.15.8 and all resolved dependencies. Run it from the command line in Release without an attached debugger. BenchmarkDotNet builds benchmark executables, performs warmup and measurement iterations, and reports the runtime environment; a hand-written `Stopwatch` loop would need to reimplement those controls.

The quick mode is only an execution check:

```console
dotnet run --project Ch31.Benchmarks.fsproj \
  --configuration Release --no-restore -- --smoke
```

It uses a Dry job with one cold-start measurement, so its means and ratios are not a baseline. Running without the chapter-specific arguments uses ShortRun. Use a longer job and a controlled machine when an important decision requires greater precision.

## Read the captured result without overclaiming {#read-results}

The committed baseline records the tool version, job, OS, runtime, architecture, GC, configuration, seed, workload, and limitations. On that one developer workstation, the ShortRun summary was:

| Method | Count | Mean | Error (99.9% CI half-width) | StdDev | Ratio | Allocated |
|---|---:|---:|---:|---:|---:|---:|
| `ArrayPipeline` | 256 | 339.9 ns | 27.92 ns | 1.53 ns | 1.00 | 520 B |
| `SinglePass` | 256 | 147.3 ns | 6.25 ns | 0.34 ns | 0.43 | 0 B reported |
| `ArrayPipeline` | 4,096 | 5,777.7 ns | 691.47 ns | 37.90 ns | 1.00 | 7,504 B |
| `SinglePass` | 4,096 | 2,475.9 ns | 70.92 ns | 3.89 ns | 0.43 | 0 B reported |

The result supports only a narrow conclusion. In this environment and with this input generator, the single-pass candidate preserved every checked result. At both tested sizes, its mean was about 0.43 times the pipeline mean, and MemoryDiagnoser reported no intermediate-array allocation.

The conclusion applies only to these two implementations, sizes, and this environment. Statements about loops, pipelines, arrays, lists, mutation, other runtimes, or other CPUs require new measurements. The processor query was denied, workstation power and background load were uncontrolled, and ShortRun supplied only three measured iterations, so treat the wide confidence interval cautiously.

Mean is the arithmetic mean of measured operations. Standard deviation describes observed spread. The displayed error is half of BenchmarkDotNet's stated confidence interval and applies to this sample rather than every future run. Ratio compares methods within the corresponding `Count` group. “0 B reported” means the diagnoser observed zero managed bytes per operation at its resolution; the process still uses memory elsewhere.

## Understand the allocation hypothesis {#allocation}

In this sample, `Array.filter` creates an intermediate array containing accepted values, then `Array.sumBy` traverses it. The allocation grows with the number of matches. The single pass reads the source array and accumulates an `int64` without constructing that result array. The measurements agree with this specific causal explanation.

Allocation is not automatically a defect. A materialized value may simplify code, enable reuse, or avoid repeated deferred work. Short-lived allocations may be inexpensive until their rate creates GC pressure. Optimize allocation when profiling or measured rates show that it affects the requirement, not because the word “allocation” appears in code.

There is no context-free fastest F# collection:

| Need | Candidate to measure | Important cost |
|---|---|---|
| Persistent head-oriented updates and structural sharing | `list<'T>` | Poor random indexing and per-node overhead |
| Dense indexing, bulk traversal, .NET interop | `'T array` | Mutable storage and whole-array copying for structural changes |
| Deferred or streaming traversal | `seq<'T>` | Enumerator/closure costs and repeated work on re-enumeration |
| Key lookup with ordering | `Map<'K,'V>` | Tree comparisons and allocation on updates |
| Key lookup without ordering | `Dictionary<'K,'V>` | Mutation, comparer quality, capacity, and resize behavior |

Choose by behavior first, then profile representative operations and sizes. Changing a collection can alter ordering, equality, mutation, laziness, thread safety, and memory lifetime—not only speed.

## Profile the application, not only a function {#profiling}

.NET diagnostics tools observe a live or recorded process. Runtime counters can quickly show CPU use, allocation rate, GC activity, thread-pool behavior, and exceptions. A sampling trace can attribute CPU time and allocations to call stacks. Heap tools answer retention questions that an allocation microbenchmark cannot.

Use representative traffic and preserve operational context. A microbenchmark deliberately excludes network latency, serialization, database waits, contention, queueing, JIT startup, and application composition. It can explain one hot function after profiling identifies it; it cannot predict p95 request latency by itself.

Likewise, a CPU profile does not prove causation merely because a function appears near the top. It may be called often because of an upstream design, or it may wait inside an operation attributed elsewhere. Form a hypothesis, change one cause, and confirm both the profile and end-to-end observable.

## Use lower-level F# tools only after measuring {#lower-level-tools}

Lower-level features trade convenience and generality for control over representation or calls. Keep them narrowly scoped and benchmark the actual use case.

### `inline` is not a universal speed annotation {#inline}

An F# `inline` function is integrated at call sites and can use statically resolved type parameters. That type-system role is sometimes necessary; ordinary generic functions do not need it. The compiler and JIT can also inline unmarked code according to their own rules.

Marking a function `inline` may remove call or lambda overhead, expose further optimization, do nothing, or increase generated code size and instruction-cache pressure. It can also make callers more sensitive to implementation changes. Keep an equivalent non-inline baseline and measure total behavior; do not scatter `inline` across small functions by aesthetic rule.

### `voption` trades wrapper allocation for value copying {#voption}

`option<'T>` is the natural model for optional data. `voption<'T>` is a struct discriminated union with `ValueSome` and `ValueNone`. It can avoid allocating an option wrapper in a hot path, especially for small payloads, but copying a large struct can cost more and boxing or generic/interface use may erase the expected benefit.

Changing `option` to `voption` changes a public type and its case names. Measure an allocation-sensitive path, include both present and absent distributions, and preserve behavior tests. Keep the value option internal unless callers truly benefit from that API. F# 10 also supports struct-backed optional member parameters, but the same measurement requirement applies.

### Span and byrefs impose lifetime rules {#span-byref}

`byref<'T>`, `inref<'T>`, and `outref<'T>` are managed pointers. `Span<'T>` and `ReadOnlySpan<'T>` are byref-like views over contiguous memory; the view itself is stack-bound even when the underlying memory is managed or unmanaged. Compile-time escape rules prevent storing or capturing these values in ordinary heap objects, lambdas, or asynchronous workflows.

Span can remove slicing copies and adapt efficiently to buffer-oriented .NET APIs in synchronous code. It is not a replacement for every array or list. When work must continue asynchronously or outlive the call, use `Memory<'T>`, `ReadOnlyMemory<'T>`, or an array with a clear lifetime. Add Span or byref only after profiling shows that copying or conversion materially affects performance.

## Separate runtime optimization from deployment optimization {#deployment}

Trimming and Native AOT change how an application is published. They should be evaluated with startup time, working set, package size, compatibility, build time, and the target runtime identifier—not inferred from this aggregation microbenchmark.

Trimming removes statically unreachable code from self-contained publications to reduce deployment size. Reflection and other dynamic patterns can hide required code from analysis, so trim warnings may indicate correctness problems. Resolve them and test the published artifact; suppressing them merely to produce a smaller package can create runtime failures.

Native AOT compiles IL to platform-specific native code at publish time and removes the runtime JIT dependency. It can improve startup and memory footprint for suitable applications, while increasing build time and constraining dynamic loading, runtime code generation, reflection-heavy libraries, and deployment targets. It does not promise better steady-state throughput for every workload.

Compare the actual JIT and AOT artifacts under the same startup or service workload. Include publish size and functional smoke tests for every supported RID. A normal benchmark project need not become AOT-compatible unless the application's deployment requirement asks that question.

## Record a decision, not a trophy number {#decision-record}

A useful performance record states:

- the requirement and user-visible baseline;
- the revision, full command, input distribution, environment, and job;
- profiler results locating the suspected cost;
- the hypothesis and semantic-equivalence checks;
- raw summary statistics and allocation data, not only the best run;
- the end-to-end result after the change;
- known limitations, rejected alternatives, and a rollback condition.

Keep historical results as context, not as a permanent pass/fail threshold. Rebaseline deliberately after runtime, dependency, hardware, workload, or benchmark-code changes. If the environment differs, compare alternatives within the same run before comparing absolute values across history.

## Exercises {#exercises}

### Exercise 1: state only measured conclusions {#exercise-01}

Using the captured table, write three conclusions supported by the measurements and three that are not. Explain why the Dry smoke output cannot replace the ShortRun baseline.

### Exercise 2: design an `option` versus `voption` experiment {#exercise-02}

A profiler attributes substantial allocation to a lookup returning `Some smallStruct` or `None` millions of times. Design a benchmark and equivalence gate for an `option<'T>` and `voption<'T>` implementation. Specify present/absent distributions, setup, returned result, allocation observation, and confounders.

### Exercise 3: choose the next measurement {#exercise-03}

For each symptom—high p95 API latency, high allocation rate in a known aggregation call stack, and slow command-line startup—choose the next end-to-end, profiler, counter, or microbenchmark observation. State what result would justify an implementation experiment and what must be remeasured afterward.

[Read the chapter solutions](../solutions/ch-31-measure-before-optimizing).

## Model review {#model-review}

- Performance statements require a workload, environment, observable, and target.
- Profile a representative system before isolating a suspected function.
- Preserve semantic equivalence before comparing performance.
- Release, no debugger, controlled setup, consumed results, and recorded context make a microbenchmark interpretable.
- Dry is an execution smoke, not a measurement baseline.
- Ratios and allocations support only the tested methods, inputs, runtime, and environment.
- Local mutation can remove a measured allocation without making the public model mutable.
- Collection choice begins with semantics; measurement follows representative operations.
- `inline`, `voption`, and Span/byref are hypotheses with representation and maintenance costs.
- Trimming and Native AOT optimize deployment dimensions and require published-artifact tests.
- A microbenchmark improvement matters only when the end-to-end requirement also improves.

## Sources {#sources}

- [BenchmarkDotNet: getting started and Release execution](https://benchmarkdotnet.org/articles/guides/getting-started.html)
- [BenchmarkDotNet: good practices and limits on extrapolation](https://benchmarkdotnet.org/articles/guides/good-practices.html)
- [BenchmarkDotNet: diagnosers and allocation reporting](https://benchmarkdotnet.org/articles/configs/diagnosers.html)
- [Microsoft Learn: .NET diagnostics, counters, traces, and profilers](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/)
- [Microsoft Learn: F# inline functions](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/functions/inline-functions)
- [Microsoft Learn: F# value options](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/value-options)
- [Microsoft Learn: F# byrefs and byref-like structs](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/byrefs)
- [Microsoft Learn: `Memory<'T>` and `Span<'T>` usage guidelines](https://learn.microsoft.com/en-us/dotnet/standard/memory-and-spans/memory-t-usage-guidelines)
- [Microsoft Learn: trimming self-contained applications](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/trim-self-contained)
- [Microsoft Learn: Native AOT deployment](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/)
