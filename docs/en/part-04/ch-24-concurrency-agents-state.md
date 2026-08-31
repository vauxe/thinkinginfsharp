---
title: "Chapter 24: Parallelism, Concurrency, Agents, and Controlled Mutation"
description: "Separate overlapping work from CPU parallelism, reproduce a race deterministically, and choose immutable data, locks, atomics, agents, or concurrent caches by invariant."
translationKey: part-04/ch-24-concurrency-agents-state
---

# Chapter 24: Parallelism, Concurrency, Agents, and Controlled Mutation {#overview}

Two booking requests can overlap while waiting for storage, and two pricing calculations can run on different cores. Those are different problems. The first needs lifecycle and consistency rules even on one thread; the second is a performance technique whose overhead may exceed its benefit.

F# makes immutable values easy, removing many accidental races. Shared mutable state still exists in queues, caches, counters, files, databases, and external services. Choose a synchronization mechanism based on the invariant that must remain true.

The main-line example lives at `examples/chapters/ch24/concurrency.fsx`. Its blocks share the following namespaces and three test helpers. They create controlled gates, start dedicated workers, and make two participants rendezvous at a barrier:

```fsharp:line-numbers
open System
open System.Collections.Concurrent
open System.Threading
open System.Threading.Tasks

let newGate<'T> () =
    TaskCompletionSource<'T>(TaskCreationOptions.RunContinuationsAsynchronously)

let startLongRunning (action: unit -> unit) =
    Task.Factory.StartNew(
        Action action,
        CancellationToken.None,
        TaskCreationOptions.LongRunning,
        TaskScheduler.Default
    )

let runTwoWithBarrier action =
    use barrier = new Barrier(2)
    let first = startLongRunning (fun () -> action barrier)
    let second = startLongRunning (fun () -> action barrier)
    Task.WaitAll [| first; second |]
```

These functions exist only to make the test schedule repeatable; they are not domain abstractions. Later uses of `newGate`, `runTwoWithBarrier`, and `startLongRunning` therefore have no hidden definitions.

## Three concepts, three questions {#three-concepts}

| Concept | Question | Example |
|---|---|---|
| Asynchrony | Can the caller yield while this result is pending? | Awaiting file or network I/O |
| Concurrency | Can several operations be in progress during overlapping lifetimes? | Two requests waiting on independent replies |
| Parallelism | Can work execute simultaneously on multiple processing resources? | Partitioning CPU-heavy array transformations |

They can combine, but none implies the others. An asynchronous operation may complete synchronously. A UI event loop can coordinate concurrent work on one thread. A parallel loop is often synchronous from its caller's perspective until all partitions finish.

Start with the requirement. Use asynchronous APIs to avoid blocking scarce threads during waits. Add concurrency only when operations may safely overlap. Add parallelism only after measuring enough independent CPU work to justify partitioning, scheduling, coordination, and allocation costs.

## Observe overlap without claiming threads {#concurrent-overlap}

The example starts two task expressions. Each records entry and then awaits the same closed gate:

```fsharp:line-numbers
let releaseWaits = newGate<unit> ()
let entered = [| 0 |]

let waitingWork label =
    task {
        Interlocked.Increment(&entered[0]) |> ignore
        do! releaseWaits.Task
        return label
    }

let firstWait = waitingWork "first"
let secondWait = waitingWork "second"
let bothPending = not firstWait.IsCompleted && not secondWait.IsCompleted

assert (entered[0] = 2)
assert bothPending
printfn "Concurrent waits: entered=%d pending=%b" entered[0] bothPending

releaseWaits.SetResult()

let waitResults =
    let running = Task.WhenAll [| firstWait; secondWait |]
    running.GetAwaiter().GetResult()

assert (waitResults = [| "first"; "second" |])
printfn "Concurrent results: %A" waitResults
```
Both operations are in progress and neither has completed. That proves concurrent lifetimes. It says nothing about simultaneous CPU execution or thread identity. Releasing one gate lets both resume, and `Task.WhenAll` returns results in input-task order even though completion scheduling is not asserted.

Unbounded concurrency is not a performance plan. Every external dependency has connection, queue, memory, and rate limits. Bound concurrency near the constrained resource and decide how excess work waits, fails, or is rejected.

## Data parallelism needs independent work and measurement {#data-parallelism}

`Array.Parallel.map` partitions an array transformation through the .NET parallel infrastructure:

```fsharp:line-numbers
let values = [| 1..8 |]
let sequentialSquares = values |> Array.map (fun value -> value * value)
let parallelSquares = values |> Array.Parallel.map (fun value -> value * value)
let parallelAgrees = parallelSquares = sequentialSquares

assert parallelAgrees
printfn "Parallel map agrees: %b" parallelAgrees
```
The mapping is pure and each output depends on one input, so scheduling order cannot change the result. The assertion proves that both versions return the same values, not that the parallel one is faster. For a small array, parallelism is probably unnecessary; Chapter 31 measures before choosing it.

Review a parallel mapping for:

- independence between elements;
- absence or synchronization of shared side effects;
- exception and cancellation behavior across partitions;
- ordering requirements on results and side effects;
- allocation and partitioning overhead;
- host limits—especially on servers already processing concurrent requests.

Do not wrap naturally asynchronous I/O in CPU-parallel APIs merely to “make it parallel.” Use the I/O API asynchronously and set a deliberate concurrency limit.

## A read-modify-write is not one operation {#lost-update}

The expression `counter <- counter + 1` reads, computes, and writes. Two threads can read the same old value and both write the same new value, losing one increment.

Probabilistic stress sometimes misses that race. The test uses a two-participant `Barrier`: both long-running workers read before either may write. The bad result is therefore forced, not hoped for:

```fsharp:line-numbers
let racyCounter = [| 0 |]

runTwoWithBarrier (fun barrier ->
    let snapshot = racyCounter[0]
    barrier.SignalAndWait() |> ignore
    racyCounter[0] <- snapshot + 1)

let lockedCounter = [| 0 |]
let counterLock = obj ()

runTwoWithBarrier (fun barrier ->
    barrier.SignalAndWait() |> ignore

    lock counterLock (fun () -> lockedCounter[0] <- lockedCounter[0] + 1))

let atomicCounter = [| 0 |]

runTwoWithBarrier (fun barrier ->
    barrier.SignalAndWait() |> ignore
    Interlocked.Increment(&atomicCounter[0]) |> ignore)

assert (racyCounter[0] = 1)
assert (lockedCounter[0] = 2)
assert (atomicCounter[0] = 2)

printfn "Shared counter: race=%d lock=%d interlocked=%d" racyCounter[0] lockedCounter[0] atomicCounter[0]
```
The same barrier starts two corrected variants. `lock` makes the whole read-modify-write critical section exclusive. `Interlocked.Increment` performs its supported update atomically. The deterministic results are `1`, `2`, and `2`.

`volatile` visibility alone would not turn a multi-step increment into an atomic operation. Likewise, a thread-safe collection protects its own methods; it does not automatically make a sequence across several calls a transaction.

## Choose the smallest mechanism that protects the invariant {#coordination-choice}

| Need | First choice | Protected scope |
|---|---|---|
| Values can be computed independently | Immutable values and pure functions | No shared write |
| One publisher can publish whole revisions | Immutable snapshot plus atomic reference swap | One snapshot identity |
| One numeric/reference update | `Interlocked` operation | One supported location and operation |
| Several fields must change together synchronously | `lock` on a private object | One short critical section |
| In-process requests should be serialized around private state | `MailboxProcessor` | One mailbox loop |
| Concurrent key operations with documented behavior | Concurrent collection | One collection method, not arbitrary workflows |
| Durable or cross-process consistency | Storage transaction, constraint, version, or distributed protocol | External source of truth |

Prefer mutation confined to one function over synchronized shared mutation. A locally built array or dictionary is efficient and simple when no other operation can observe it before an immutable result is published.

### Locks protect code regions {#locks}

Use a private lock object and keep the critical section short. Read and update every field involved in one invariant under the same lock. Never lock a public object, interned string, or externally supplied value, because unrelated code could lock it too.

Do not hold a monitor across `let!`, network I/O, callbacks, or work of unknown duration. A monitor is thread-affine and blocking; asynchronous suspension also makes lifetimes and deadlocks much harder to reason about. Split the operation into snapshot, external work, and a short validated commit, or choose an asynchronous coordination primitive.

When several locks are unavoidable, define one acquisition order. Otherwise two workers can each hold one lock while waiting forever for the other.

The capacity example updates `Remaining` and `Accepted` as one invariant:

```fsharp:line-numbers
type CapacityState =
    { mutable Remaining: int
      mutable Accepted: int }

let capacity = { Remaining = 3; Accepted = 0 }
let capacityLock = obj ()

let tryReserve seats =
    lock capacityLock (fun () ->
        if seats > 0 && seats <= capacity.Remaining then
            capacity.Remaining <- capacity.Remaining - seats
            capacity.Accepted <- capacity.Accepted + 1
            true
        else
            false)

let reservationResults = Array.zeroCreate<bool> 2
let reservationIndex = [| -1 |]

runTwoWithBarrier (fun barrier ->
    let index = Interlocked.Increment(&reservationIndex[0])
    barrier.SignalAndWait() |> ignore
    reservationResults[index] <- tryReserve 2)

let acceptedReservations = reservationResults |> Array.filter id |> Array.length
let capacityInvariant = capacity.Remaining = 1 && capacity.Accepted = 1

assert (acceptedReservations = 1)
assert capacityInvariant

printfn
    "Locked capacity: accepted=%d remaining=%d invariant=%b"
    acceptedReservations
    capacity.Remaining
    capacityInvariant
```
Two requests, each asking for two seats, race against a capacity of three. Exactly one succeeds, and both fields describe the same committed transition. Separate atomic decrements and increments would neither make the pair transactional nor prevent negative capacity.

### Atomics protect particular operations {#atomics}

`Interlocked` provides atomic increment, add, exchange, compare-exchange, and related operations for supported locations. It is ideal for counters, flags, and carefully designed state transitions that fit one atomic location.

Once correctness depends on several locations, a check followed by an update, or an external side effect, sprinkling atomics over individual fields cannot make the operation safe. Use compound synchronization or a storage system that is the source of truth.

## A mailbox serializes access to state {#mailbox}

`MailboxProcessor<'Message>` runs an asynchronous receive loop over an in-process queue. Callers post messages; the loop handles one received message at a time and can carry the next immutable state through recursion.

The reservation agent keeps `remaining` and `accepted` private:

```fsharp:line-numbers
type ReservationReply =
    | Accepted of remaining: int
    | Rejected of remaining: int

type ReservationMessage =
    | Reserve of seats: int * reply: AsyncReplyChannel<ReservationReply>
    | Stop of reply: AsyncReplyChannel<int * int>

let reservationAgent =
    MailboxProcessor.Start(fun inbox ->
        let rec loop remaining accepted =
            async {
                let! message = inbox.Receive()

                match message with
                | Reserve(seats, reply) when seats > 0 && seats <= remaining ->
                    let nextRemaining = remaining - seats
                    reply.Reply(Accepted nextRemaining)
                    return! loop nextRemaining (accepted + 1)
                | Reserve(_, reply) ->
                    reply.Reply(Rejected remaining)
                    return! loop remaining accepted
                | Stop reply -> reply.Reply(remaining, accepted)
            }

        loop 3 0)

let agentReplies =
    [| reservationAgent.PostAndAsyncReply(fun reply -> Reserve(2, reply))
       reservationAgent.PostAndAsyncReply(fun reply -> Reserve(2, reply)) |]
    |> Async.Parallel
    |> Async.RunSynchronously

let agentAccepted =
    agentReplies
    |> Array.filter (function
        | Accepted _ -> true
        | Rejected _ -> false)
    |> Array.length

let agentRemaining, agentAcceptedState = reservationAgent.PostAndReply Stop

assert (agentAccepted = 1)
assert (agentRemaining = 1)
assert (agentAcceptedState = 1)

printfn
    "Agent capacity: accepted=%d remaining=%d invariant=%b"
    agentAccepted
    agentRemaining
    (agentAcceptedState = agentAccepted)

reservationAgent.Dispose()
```
Two callers create computations that await replies, and `Async.Parallel` starts them together. Arrival order is unspecified, so the test asserts only the invariant: one request accepted, one seat remaining, and agent state matching the replies. `Stop` returns the final state and ends the loop; the processor is then disposed.

A reply channel lets the receiver send one reply and should be used exactly once. Define behavior for invalid messages, handler exceptions, cancellation, shutdown, and callers that stop waiting. Monitor the queue or limit admission if producers can outrun the single consumer.

An agent serializes only code inside this mailbox. It does **not** provide:

- durable messages or state after process loss;
- a transaction with a database, payment provider, or another agent;
- exactly-once delivery or exactly-once external side effects;
- automatic retries, idempotency, backpressure, supervision, or scale-out;
- protection for mutable state that other code can still access directly.

Use an agent when one in-process loop can safely serialize the work; it is not a shortcut to system-wide consistency.

## A cache is shared state with a time dimension {#cache}

A cache needs more than a thread-safe dictionary. Specify:

- key equality and normalization;
- value freshness and invalidation;
- size bounds and eviction;
- whether failures are cached and for how long;
- whether concurrent misses may duplicate computation;
- who may cancel shared computation;
- metrics or logs for hits, misses, loads, evictions, and failures.

`ConcurrentDictionary.GetOrAdd(key, valueFactory)` keeps dictionary operations thread-safe, but its documented factory may run more than once under contention because it executes outside internal locks. Never place an irreversible side effect in that factory and assume exactly-once execution.

The example stores `Lazy<int>` values:

```fsharp:line-numbers
let cache = ConcurrentDictionary<string, Lazy<int>>()
let computations = [| 0 |]

let getCached key =
    cache.GetOrAdd(
        key,
        fun _ ->
            lazy
                (Interlocked.Increment(&computations[0]) |> ignore
                 23)
    )
    |> fun delayed -> delayed.Value

let cacheBarrier = new Barrier(2)

let cachedTasks =
    [| startLongRunning (fun () ->
           cacheBarrier.SignalAndWait() |> ignore
           getCached "quote" |> ignore)
       startLongRunning (fun () ->
           cacheBarrier.SignalAndWait() |> ignore
           getCached "quote" |> ignore) |]

Task.WaitAll cachedTasks
let cachedValues = [| getCached "quote"; getCached "quote" |]

assert (cachedValues = [| 23; 23 |])
assert (computations[0] = 1)
assert (cache.Count = 1)
printfn "Cache: values=%A computations=%d entries=%d" cachedValues computations[0] cache.Count
cacheBarrier.Dispose()
```
Competing dictionary factories may allocate more than one `Lazy`, but callers evaluate only the instance returned by the dictionary. Default `Lazy` execution-and-publication behavior makes the demonstrated computation run once. It also caches exceptions thrown during value creation, while the dictionary grows without eviction. Neither behavior is a universally desirable default.

For remote work, a shared in-flight `Task<'T>` can implement single-flight behavior, but Chapter 23's lifecycle questions still apply. One caller must not accidentally cancel work shared by all.

Run `dotnet fsi examples/chapters/ch24/concurrency.fsx` from the repository root. It prints:

```text
Concurrent waits: entered=2 pending=true
Concurrent results: [|"first"; "second"|]
Parallel map agrees: true
Shared counter: race=1 lock=2 interlocked=2
Locked capacity: accepted=1 remaining=1 invariant=true
Agent capacity: accepted=1 remaining=1 invariant=true
Cache: values=[|23; 23|] computations=1 entries=1
```

The first three observations cover concurrent overlap, result ordering, and value agreement under data parallelism. The final four expose the lost update and verify the invariants of three protected-state designs.

## Test forced schedules and stable invariants {#testing}

Concurrency tests should control order only where the tested behavior requires it:

```text
both read old value → barrier opens → both write
```

That schedule demonstrates a lost update. Corrected implementations can run the same schedule and assert the final invariant. Do not assert which valid request wins unless the API promises an order.

Repeat focused tests to expose resource and lifecycle mistakes, but repetition is not a substitute for a forced interleaving. Avoid sleeps, CPU-count assumptions, thread IDs, and exact scheduler order. Always release barriers and gates during cleanup so a failed assertion cannot strand workers.

## Exercises {#exercises}

### Exercise 1: choose a synchronization mechanism {#exercise-01}

Classify these needs: request metric increment, two-field capacity transition, immutable configuration refresh, per-key computed cache, and durable cross-process seat allocation. Choose a mechanism for each and name one guarantee it does not provide.

Implement and test the counter and capacity cases with a barrier.


::: details Answer

#### Classify before coding {#exercise-01-table}

| Need | Suitable first boundary | Important non-guarantee |
|---|---|---|
| Request metric increment | `Interlocked.Increment` | Does not atomically update another field or external metric store |
| Capacity transition over remaining and accepted | Private `lock` or one serialized state loop | In-process lock is not a cross-process transaction |
| Immutable configuration refresh | Build a new snapshot, atomically publish its reference | Readers may still hold the previous valid snapshot |
| Per-key computed cache | Concurrent dictionary plus explicit lazy/single-flight policy | Thread safety does not define freshness or eviction |
| Durable cross-process seat allocation | Database constraint/transaction or equivalent authority | Process memory coordination cannot enforce it |

#### Force the counter and capacity schedule {#exercise-01-code}

```fsharp
open System
open System.Threading
open System.Threading.Tasks

let start action =
    Task.Factory.StartNew(
        Action action,
        CancellationToken.None,
        TaskCreationOptions.LongRunning,
        TaskScheduler.Default)

let runTwo action =
    use barrier = new Barrier(2)
    let first = start (fun () -> action barrier)
    let second = start (fun () -> action barrier)
    Task.WaitAll [| first; second |]

let metric = [| 0 |]

runTwo (fun barrier ->
    barrier.SignalAndWait() |> ignore
    Interlocked.Increment(&metric[0]) |> ignore)

assert (metric[0] = 2)

let remaining = [| 3 |]
let accepted = [| 0 |]
let gate = obj ()
let results = ResizeArray<bool>()

runTwo (fun barrier ->
    barrier.SignalAndWait() |> ignore

    let result =
        lock gate (fun () ->
            if remaining[0] >= 2 then
                remaining[0] <- remaining[0] - 2
                accepted[0] <- accepted[0] + 1
                true
            else
                false)

    lock results (fun () -> results.Add result))

assert (results |> Seq.filter id |> Seq.length = 1)
assert (remaining[0] = 1 && accepted[0] = 1)
```

The outer result-list lock is test bookkeeping, separate from the domain lock. Production code should return results through tasks or messages rather than expose its mutable list.

:::

### Exercise 2: extend the reservation agent {#exercise-02}

Add `CancelReservation` and `Snapshot` messages. Keep an immutable `Map<RequestId, Seats>` inside the agent and derive remaining capacity from accepted entries or one consistently updated state.

Post concurrent reserve and cancel messages. Assert valid final invariants without assuming arrival order. Define shutdown and reply behavior for an unknown request ID.


::: details Answer

#### Carry one immutable state through the loop {#exercise-02-agent}

```fsharp
type Reply =
    | Applied
    | Rejected of reason: string

type Message =
    | Reserve of requestId: string * seats: int * AsyncReplyChannel<Reply>
    | CancelReservation of requestId: string * AsyncReplyChannel<Reply>
    | Snapshot of AsyncReplyChannel<Map<string, int>>
    | Stop of AsyncReplyChannel<unit>

let capacity = 3
let used reservations = reservations |> Map.values |> Seq.sum

let agent =
    MailboxProcessor.Start(fun inbox ->
        let rec loop reservations =
            async {
                let! message = inbox.Receive()

                match message with
                | Reserve(requestId, seats, reply) ->
                    if seats <= 0 then
                        reply.Reply(Rejected "non-positive")
                        return! loop reservations
                    elif Map.containsKey requestId reservations then
                        reply.Reply(Rejected "duplicate")
                        return! loop reservations
                    elif used reservations + seats > capacity then
                        reply.Reply(Rejected "capacity")
                        return! loop reservations
                    else
                        reply.Reply Applied
                        return! loop (Map.add requestId seats reservations)
                | CancelReservation(requestId, reply) ->
                    if Map.containsKey requestId reservations then
                        reply.Reply Applied
                        return! loop (Map.remove requestId reservations)
                    else
                        reply.Reply(Rejected "unknown")
                        return! loop reservations
                | Snapshot reply ->
                    reply.Reply reservations
                    return! loop reservations
                | Stop reply ->
                    reply.Reply()
            }

        loop Map.empty)

let initial = agent.PostAndReply(fun reply -> Reserve("A", 2, reply))
assert (initial = Applied)

let concurrentReplies =
    [|
        agent.PostAndAsyncReply(fun reply -> Reserve("B", 2, reply))
        agent.PostAndAsyncReply(fun reply -> CancelReservation("A", reply))
    |]
    |> Async.Parallel
    |> Async.RunSynchronously

let snapshot = agent.PostAndReply Snapshot
let occupied = used snapshot

assert (occupied >= 0 && occupied <= capacity)
assert (snapshot.Count <= 1)
assert (concurrentReplies.Length = 2)

agent.PostAndReply Stop
agent.Dispose()
```

If `Reserve B` arrives first, it is rejected and then `A` is removed. If cancellation arrives first, `B` can be accepted. Both outcomes preserve capacity, so the test asserts the invariant rather than one schedule.

An unknown cancellation returns `Rejected "unknown"` and leaves state unchanged. `Stop` replies once and exits. A production agent also needs admission limits, error observation, and a durable boundary if state must survive the process.

:::

### Exercise 3: state a cache policy {#exercise-03}

Extend the `Lazy` cache with a maximum size or manual invalidation, and decide whether failures remain cached. Use a controlled factory to count the actual computations during concurrent misses.

Explain why a thread-safe dictionary alone cannot guarantee freshness, bounded memory, one external side effect, or distributed consistency.


::: details Answer

#### Make invalidation and single computation visible {#exercise-03-cache}

```fsharp
open System
open System.Collections.Concurrent
open System.Threading
open System.Threading.Tasks

let cache = ConcurrentDictionary<string, Lazy<Result<int, string>>>()
let calls = [| 0 |]

let factory _ =
    let call = Interlocked.Increment(&calls[0])
    Ok call

let get key =
    cache.GetOrAdd(key, fun storedKey -> lazy (factory storedKey))
    |> fun delayed -> delayed.Value

let invalidate (key: string) =
    match cache.TryRemove key with
    | true, _ -> true
    | false, _ -> false

let startTogether = new Barrier(2)

let startGet () =
    Task.Factory.StartNew(
        (fun () ->
            startTogether.SignalAndWait() |> ignore
            get "quote"),
        CancellationToken.None,
        TaskCreationOptions.LongRunning,
        TaskScheduler.Default)

let first = startGet ()
let second = startGet ()
Task.WaitAll [| first :> Task; second :> Task |]

assert (first.Result = Ok 1 && second.Result = Ok 1)
assert (calls[0] = 1)
assert (invalidate "quote")
assert (get "quote" = Ok 2)
assert (calls[0] = 2)
startTogether.Dispose()
```

The dictionary may invoke its outer value factory more than once, but that factory only allocates a `Lazy`. The effectful `factory` runs through the winning lazy and is executed once before invalidation. Explicit invalidation makes a later computation possible.

This design intentionally caches `Result`, including an expected `Error`, until invalidation. Unexpected exceptions raised inside the default `Lazy` are also cached. A real policy must decide expiry and maximum size; this unbounded teaching dictionary is not a production cache.

Exactly-once remote effects still require idempotency at their authoritative boundary. A `Lazy` prevents duplicate evaluation only inside this process and cache lifetime.

:::


## Part IV checkpoint {#part-checkpoint}

Use controlled synchronization to force a lost update, then verify the lock, atomic, compound-invariant, agent, and cache cases above. Assert final invariants rather than arrival order. These in-process checks do not establish cross-process consistency or durability.

[Continue to Chapter 25](../part-05/ch-25-objects-interfaces), which examines object-oriented interfaces in the wider .NET ecosystem.

## Sources {#sources}

- [Microsoft Learn: Task Parallel Library](https://learn.microsoft.com/en-us/dotnet/standard/parallel-programming/task-parallel-library-tpl)
- [Microsoft Learn: data parallelism](https://learn.microsoft.com/en-us/dotnet/standard/parallel-programming/data-parallelism-task-parallel-library)
- [FSharp.Core reference: `Array.Parallel`](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-collections-arraymodule-parallel.html)
- [Microsoft Learn: `Interlocked`](https://learn.microsoft.com/en-us/dotnet/api/system.threading.interlocked?view=net-10.0)
- [FSharp.Core reference: `MailboxProcessor<'Msg>`](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-control-fsharpmailboxprocessor-1.html)
- [Microsoft Learn: `ConcurrentDictionary.GetOrAdd`](https://learn.microsoft.com/en-us/dotnet/api/system.collections.concurrent.concurrentdictionary-2.getoradd?view=net-10.0)
