---
title: "Chapter 24: Parallelism, Concurrency, Agents, and Controlled Mutation"
description: "Separate overlapping work from CPU parallelism, reproduce a race deterministically, and choose immutable data, locks, atomics, agents, or concurrent caches by invariant."
translationKey: part-04/ch-24-concurrency-agents-state
kind: chapter
part: 4
chapter: 24
status: complete
verifiedWith:
  fsharp: "10"
  dotnetSdk: "10.0.301"
exampleIds:
  - ch24-concurrency-agents-state
  - capstone-booking-domain
  - foundation-example-tests
exerciseIds:
  - ch24-exercise-01
  - ch24-exercise-02
  - ch24-exercise-03
termIds:
  - effect
sources:
  - id: dotnet-task-parallel-library
    url: https://learn.microsoft.com/en-us/dotnet/standard/parallel-programming/task-parallel-library-tpl
    checked: "2026-08-24"
  - id: dotnet-data-parallelism
    url: https://learn.microsoft.com/en-us/dotnet/standard/parallel-programming/data-parallelism-task-parallel-library
    checked: "2026-08-24"
  - id: fsharp-array-parallel
    url: https://fsharp.github.io/fsharp-core-docs/reference/fsharp-collections-arraymodule-parallel.html
    checked: "2026-08-24"
  - id: dotnet-interlocked
    url: https://learn.microsoft.com/en-us/dotnet/api/system.threading.interlocked?view=net-10.0
    checked: "2026-08-24"
  - id: fsharp-mailbox-processor
    url: https://fsharp.github.io/fsharp-core-docs/reference/fsharp-control-fsharpmailboxprocessor-1.html
    checked: "2026-08-24"
  - id: dotnet-concurrent-get-or-add
    url: https://learn.microsoft.com/en-us/dotnet/api/system.collections.concurrent.concurrentdictionary-2.getoradd?view=net-10.0
    checked: "2026-08-24"
---

# Chapter 24: Parallelism, Concurrency, Agents, and Controlled Mutation {#overview}

Two booking requests can overlap while waiting for storage, and two pricing calculations can run on different cores. Those are different problems. The first needs lifecycle and consistency rules even on one thread; the second is a performance technique whose overhead may exceed its benefit.

F# makes immutable values easy, which removes many accidental races. It does not make shared mutable state disappear from queues, caches, counters, files, databases, or external services. This chapter chooses a coordination boundary from the invariant that must remain true.

## What you will be able to do {#outcomes}

By the end of this chapter, you should be able to:

- distinguish asynchronous work, concurrency, and parallel execution;
- avoid claiming that a task or async computation creates a thread;
- use data parallelism only for independent, measured CPU work;
- reproduce a lost update with a deterministic barrier;
- keep mutation local or publish immutable snapshots when possible;
- use `lock` for short compound invariants and avoid awaiting while holding it;
- use `Interlocked` for supported operations on one shared location;
- model serialized in-process ownership with `MailboxProcessor`;
- state what an agent does not guarantee about durability or distributed effects;
- design a cache with explicit duplicate-work, failure, freshness, and eviction policy;
- test final invariants without asserting nondeterministic execution order.

## Three concepts, three questions {#three-concepts}

| Concept | Question | Example |
|---|---|---|
| Asynchrony | Can the caller yield while this result is pending? | Awaiting file or network I/O |
| Concurrency | Can several operations be in progress during overlapping lifetimes? | Two requests waiting on independent replies |
| Parallelism | Can work execute simultaneously on multiple processing resources? | Partitioning CPU-heavy array transformations |

They can combine, but none implies the others. An asynchronous operation may complete synchronously. A UI event loop can coordinate concurrent work on one thread. A parallel loop is often synchronous from its caller's perspective until all partitions finish.

Start with the requirement. Use asynchronous APIs to avoid blocking scarce threads during waits. Add concurrency only when operations may safely overlap. Add parallelism only after measuring enough independent CPU work to justify partitioning, scheduling, coordination, and allocation costs.

## Observe overlap without claiming threads {#concurrent-overlap}

The shared example starts two task expressions. Each records entry and then awaits the same closed gate:

<<< @/../examples/scripts/ch24-concurrency-agents-state.fsx#concurrent-waits{fsharp:line-numbers} [ch24-concurrency-agents-state.fsx]

Both operations are in progress and neither has completed. That proves concurrent lifetimes. It says nothing about simultaneous CPU execution or thread identity. Releasing one gate lets both resume, and `Task.WhenAll` returns results in input-task order even though completion scheduling is not asserted.

Unbounded concurrency is not a performance plan. Every external dependency has connection, queue, memory, and rate limits. Bound concurrency near the constrained resource and decide how excess work waits, fails, or is rejected.

## Data parallelism needs independent work and measurement {#data-parallelism}

`Array.Parallel.map` partitions an array transformation through the .NET parallel infrastructure:

<<< @/../examples/scripts/ch24-concurrency-agents-state.fsx#parallel-map{fsharp:line-numbers} [ch24-concurrency-agents-state.fsx]

The mapping is pure and each output depends on one input, so scheduling order cannot change the value. The assertion proves functional equivalence, not speed. On a small array this parallel version is likely unnecessary; Chapter 31 measures before choosing it.

Review a parallel mapping for:

- independence between elements;
- absence or synchronization of shared effects;
- exception and cancellation behavior across partitions;
- ordering requirements on results and effects;
- allocation and partitioning overhead;
- host limits—especially on servers already processing concurrent requests.

Do not wrap naturally asynchronous I/O in CPU-parallel APIs merely to “make it parallel.” Use the I/O API's asynchronous contract and a deliberate concurrency limit.

## A read-modify-write is not one operation {#lost-update}

The expression `counter <- counter + 1` reads, computes, and writes. Two threads can read the same old value and both write the same new value, losing one increment.

Probabilistic stress sometimes misses that race. The shared test uses a two-participant `Barrier`: both long-running workers read before either may write. The bad result is therefore forced, not hoped for:

<<< @/../examples/scripts/ch24-concurrency-agents-state.fsx#shared-state{fsharp:line-numbers} [ch24-concurrency-agents-state.fsx]

The same barrier starts two corrected variants. `lock` makes the whole read-modify-write critical section exclusive. `Interlocked.Increment` performs its supported update atomically. The deterministic results are `1`, `2`, and `2`.

`volatile` visibility alone would not turn a multi-step increment into an atomic operation. Likewise, a thread-safe collection protects its own methods; it does not automatically make a sequence across several calls a transaction.

## Choose the smallest boundary that protects the invariant {#coordination-choice}

| Need | First choice | Boundary |
|---|---|---|
| Values can be computed independently | Immutable values and pure functions | No shared write |
| One owner can publish whole revisions | Immutable snapshot plus atomic reference swap | One snapshot identity |
| One numeric/reference update | `Interlocked` operation | One supported location and operation |
| Several fields must change together synchronously | `lock` on a private object | One short critical section |
| In-process requests should be serialized around private state | `MailboxProcessor` | One mailbox loop |
| Concurrent key operations with documented semantics | Concurrent collection | One collection method, not arbitrary workflows |
| Durable or cross-process consistency | Storage transaction, constraint, version, or distributed protocol | External authority |

Prefer local mutation that never escapes a function over synchronized shared mutation. A locally built array or dictionary can be efficient and simple when no other operation observes it until an immutable result is published.

### Locks protect code regions {#locks}

Use a private lock object and keep the protected region short. Read and update every field of one invariant under the same lock. Never use a public object, interned string, or externally supplied value as the monitor, because unrelated code could lock it too.

Do not hold a monitor across `let!`, network I/O, callbacks, or other unbounded work. A monitor is thread-affine and blocking; asynchronous suspension also makes lifetime and deadlock reasoning much harder. Split the operation into snapshot, external work, and short validated commit, or choose an asynchronous coordination primitive.

When several locks are unavoidable, define one acquisition order. Otherwise two workers can each hold one lock while waiting forever for the other.

The capacity example updates `Remaining` and `Accepted` as one invariant:

<<< @/../examples/scripts/ch24-concurrency-agents-state.fsx#compound-invariant{fsharp:line-numbers} [ch24-concurrency-agents-state.fsx]

Two requests for two seats race against capacity three. Exactly one succeeds, and both fields describe the same committed transition. Separate atomic decrements and increments would not by themselves make the pair transactional or prevent capacity from going negative.

### Atomics protect particular operations {#atomics}

`Interlocked` provides atomic increment, add, exchange, compare-exchange, and related operations for supported locations. It is ideal for counters, flags, and carefully designed state transitions that fit one atomic location.

Once correctness depends on several locations, a check followed by an update, or an external effect, the operation is no longer “made safe” by sprinkling atomics over individual fields. Use a compound synchronization or authoritative storage boundary.

## A mailbox gives state one serialized owner {#mailbox}

`MailboxProcessor<'Message>` runs an asynchronous receive loop over an in-process queue. Callers post messages; the loop handles one received message at a time and can carry the next immutable state through recursion.

The shared reservation agent owns `remaining` and `accepted`:

<<< @/../examples/scripts/ch24-concurrency-agents-state.fsx#mailbox-agent{fsharp:line-numbers} [ch24-concurrency-agents-state.fsx]

Two callers create reply-awaiting computations, and `Async.Parallel` starts them together. Arrival order is intentionally unspecified, so the test asserts only the invariant: one accepted request, one seat remaining, and agent state agreeing with replies. `Stop` returns the final state and ends the loop; the processor is then disposed.

Reply channels are capabilities that must be replied to exactly once. Define behavior for malformed messages, handler exceptions, cancellation, shutdown, and callers that stop waiting. Monitor the queue or bound admission if producers can outrun the single consumer.

An agent serializes only code inside this mailbox. It does **not** provide:

- durable messages or state after process loss;
- a transaction with a database, payment provider, or another agent;
- exactly-once delivery or exactly-once external effects;
- automatic retries, idempotency, backpressure, supervision, or scale-out;
- protection for mutable state that other code can still access directly.

Use an agent when one in-process serialized owner fits the problem, not as a magic synonym for consistency.

## A cache is shared state with a time dimension {#cache}

A cache needs more than a thread-safe dictionary. Specify:

- key equality and normalization;
- value freshness and invalidation;
- size bounds and eviction;
- whether failures are cached and for how long;
- whether concurrent misses may duplicate computation;
- cancellation ownership for shared computation;
- observability of hit, miss, load, eviction, and failure.

`ConcurrentDictionary.GetOrAdd(key, valueFactory)` keeps dictionary operations thread-safe, but the documented factory may run more than once under contention because it executes outside internal locks. Never put an irreversible effect in that factory assuming exactly-once execution.

The example stores `Lazy<int>` values:

<<< @/../examples/scripts/ch24-concurrency-agents-state.fsx#cache{fsharp:line-numbers} [ch24-concurrency-agents-state.fsx]

Competing dictionary factories may allocate more than one `Lazy`, but callers evaluate the one instance actually returned by the dictionary. Default `Lazy` execution-and-publication semantics make the demonstrated computation run once. This also caches an exception thrown during value creation and the dictionary grows without eviction; those are policies, not universally desirable defaults.

For remote work, a shared in-flight `Task<'T>` can implement single-flight behavior, but Chapter 23's ownership questions still apply. One caller should not accidentally cancel work shared by all.

## Test forced schedules and stable invariants {#testing}

Concurrency tests should control order only where the property needs it:

```text
both read old value → barrier opens → both write
```

That schedule proves a lost update. Corrected implementations can run the same schedule and assert the final invariant. Do not assert which valid request wins unless ordering is part of the public contract.

Repeat focused tests to expose resource and lifecycle mistakes, but repetition is not a substitute for a forced interleaving. Avoid sleeps, CPU-count assumptions, thread IDs, and exact scheduler order. Always release barriers and gates during cleanup so a failed assertion cannot strand workers.

## Run the shared example {#run-example}

From the repository root:

```console
dotnet fsi --checknulls+ --exec examples/scripts/ch24-concurrency-agents-state.fsx
```

Seven deterministic lines cover concurrent waiting, data-parallel equivalence, forced lost update, lock and atomic corrections, a compound capacity invariant, agent serialization, and a single-computation cache.

## Exercises {#exercises}

### Exercise 1: choose a coordination boundary {#exercise-01}

Classify these needs: request metric increment, two-field capacity transition, immutable configuration refresh, per-key computed cache, and durable cross-process seat allocation. Choose a boundary for each and name one guarantee it does not provide.

Implement and test the counter and capacity cases with a barrier.

### Exercise 2: extend the reservation agent {#exercise-02}

Add `CancelReservation` and `Snapshot` messages. Keep an immutable `Map<RequestId, Seats>` inside the agent and derive remaining capacity from accepted entries or one consistently updated state.

Post concurrent reserve and cancel messages. Assert valid final invariants without assuming arrival order. Define shutdown and reply behavior for an unknown request ID.

### Exercise 3: state a cache policy {#exercise-03}

Extend the `Lazy` cache with a maximum size or explicit invalidation and decide whether failures remain cached. Use a controlled factory to prove how many actual computations occur during concurrent misses.

Explain why a thread-safe dictionary alone cannot guarantee freshness, bounded memory, single external effect, or distributed consistency.

[Read the chapter solutions](../solutions/ch-24-concurrency-agents-state).

## Model review {#model-review}

- Asynchrony, concurrency, and parallelism answer different questions.
- Immutable data removes shared writes; it does not make external resources consistent.
- A barrier can force a lost update and turn a probabilistic race into deterministic evidence.
- `lock` protects a short compound invariant; `Interlocked` protects supported single-location operations.
- Never await unbounded work while holding a monitor.
- A mailbox serializes one in-process owner's message handling, not the surrounding world.
- Concurrent collections have method-specific guarantees; read the factory and composition semantics.
- Cache correctness includes time, ownership, failure, and resource policies.

## Part IV checkpoint {#part-checkpoint}

Run the booking port contract against deterministic asynchronous substitutes:

```console
dotnet test tests/ExampleTests/ExampleTests.fsproj --configuration Release --filter FullyQualifiedName~BookingAsyncPortTests
```

Passing tests show that caller cancellation tokens reach every port and that controlled operations remain pending until they explicitly succeed, fail, or observe cancellation. They do not establish cross-process consistency or durability.

[Continue to Chapter 25](../part-05/ch-25-objects-interfaces), which examines object-oriented boundaries in the wider .NET ecosystem.

## Sources {#sources}

- [Microsoft Learn: Task Parallel Library](https://learn.microsoft.com/en-us/dotnet/standard/parallel-programming/task-parallel-library-tpl)
- [Microsoft Learn: data parallelism](https://learn.microsoft.com/en-us/dotnet/standard/parallel-programming/data-parallelism-task-parallel-library)
- [FSharp.Core reference: `Array.Parallel`](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-collections-arraymodule-parallel.html)
- [Microsoft Learn: `Interlocked`](https://learn.microsoft.com/en-us/dotnet/api/system.threading.interlocked?view=net-10.0)
- [FSharp.Core reference: `MailboxProcessor<'Msg>`](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-control-fsharpmailboxprocessor-1.html)
- [Microsoft Learn: `ConcurrentDictionary.GetOrAdd`](https://learn.microsoft.com/en-us/dotnet/api/system.collections.concurrent.concurrentdictionary-2.getoradd?view=net-10.0)
