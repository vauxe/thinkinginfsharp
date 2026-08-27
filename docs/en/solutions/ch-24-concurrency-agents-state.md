---
title: "Chapter 24 Solutions"
description: "Choose coordination from invariants, extend a reservation agent without assuming message order, and make cache invalidation and duplicate-work policy executable."
translationKey: solutions/ch-24-concurrency-agents-state
---

# Chapter 24 Solutions {#overview}

The synchronization scope should cover the whole invariant. A counter is one location; capacity is a compound transition; an agent serializes in-process state. Durable cross-process allocation belongs to durable storage.

[Return to Chapter 24](../part-04/ch-24-concurrency-agents-state).

## Exercise 1: choose a coordination boundary {#exercise-01}

### Classify before coding {#exercise-01-table}

| Need | Suitable first boundary | Important non-guarantee |
|---|---|---|
| Request metric increment | `Interlocked.Increment` | Does not atomically update another field or external metric store |
| Capacity transition over remaining and accepted | Private `lock` or one serialized state loop | In-process lock is not a cross-process transaction |
| Immutable configuration refresh | Build a new snapshot, atomically publish its reference | Readers may still hold the previous valid snapshot |
| Per-key computed cache | Concurrent dictionary plus explicit lazy/single-flight policy | Thread safety does not define freshness or eviction |
| Durable cross-process seat allocation | Database constraint/transaction or equivalent authority | Process memory coordination cannot enforce it |

### Force the counter and capacity schedule {#exercise-01-code}

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

## Exercise 2: extend the reservation agent {#exercise-02}

### Carry one immutable state through the loop {#exercise-02-agent}

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

## Exercise 3: state a cache policy {#exercise-03}

### Make invalidation and single computation visible {#exercise-03-cache}

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

## Solution review {#solution-review}

- Synchronize the whole invariant, not each field independently.
- Test bookkeeping needs its own coordination and should not leak into production design.
- Agent tests accept every valid arrival order and reject invalid final state.
- A mailbox manages only in-process state and lifecycle.
- `GetOrAdd` can invoke its factory more than once; keep irreversible effects outside that assumption.
- `Lazy` changes duplicate-computation and failure-caching policy but not freshness or durability.
