---
title: "Chapter 22 Solutions"
description: "Verify async and task start semantics with gates, compose a task API with an Async validator, and define when each execution starts."
translationKey: solutions/ch-22-async-task
---

# Chapter 22 Solutions {#overview}

These solutions make causal order observable. A test controls completion through gates; it never assumes that a scheduler will act within an arbitrary number of milliseconds.

[Return to Chapter 22](../part-04/ch-22-async-task).

## Exercise 1: predict and verify entry {#exercise-01}

### Observe both start boundaries {#exercise-01-proof}

```fsharp
open System.Threading.Tasks

let gate<'T> () =
    TaskCompletionSource<'T>(TaskCreationOptions.RunContinuationsAsynchronously)

let asyncEntered = gate<unit> ()
let asyncRelease = gate<unit> ()
let mutable asyncEntries = 0

let prepared =
    async {
        asyncEntries <- asyncEntries + 1
        asyncEntered.SetResult()
        do! Async.AwaitTask asyncRelease.Task
        return asyncEntries
    }

assert (asyncEntries = 0)

let asyncRun = Async.StartAsTask prepared
asyncEntered.Task.GetAwaiter().GetResult()
assert (asyncEntries = 1)
assert (not asyncRun.IsCompleted)

asyncRelease.SetResult()
assert (asyncRun.GetAwaiter().GetResult() = 1)

let taskRelease = gate<unit> ()
let mutable taskEntries = 0

let makeTask () =
    task {
        taskEntries <- taskEntries + 1
        do! taskRelease.Task
        return taskEntries
    }

assert (taskEntries = 0)
let taskRun = makeTask ()
assert (taskEntries = 1)
assert (not taskRun.IsCompleted)

taskRelease.SetResult()
assert (taskRun.GetAwaiter().GetResult() = 1)
```

The async counter remains zero until `Async.StartAsTask`. The task counter changes inside `makeTask` before that call returns, because execution reaches the incomplete gate synchronously.

Awaiting `taskRun` twice observes the same execution and cached completion. Calling `makeTask ()` twice creates two task-expression executions and repeats the entry effect. Likewise, starting `prepared` twice creates two async executions; a restart is not a replay of stored output.

The mutable counters are test probes, not the domain design. Each is written by only one execution before its entry signal completes, and the assertion reads it after observing that signal.

## Exercise 2: keep one internal representation {#exercise-02}

### Make the public workflow task-native {#exercise-02-workflow}

```fsharp
open System.Threading.Tasks

type Request = { Id: string }
type Response = { Status: int; Body: string }
type ValidResponse = private ValidResponse of string
type ValidationError = UnexpectedStatus of int

let validate response =
    async {
        if response.Status = 200 then
            return Ok(ValidResponse response.Body)
        else
            return Error(UnexpectedStatus response.Status)
    }

let execute send request : Task<Result<ValidResponse, ValidationError>> =
    task {
        let! response = send request
        let! result = validate response
        return result
    }

let mutable sends = 0

let fakeSend request =
    sends <- sends + 1
    Task.FromResult { Status = 200; Body = $"reply:{request.Id}" }

let running = execute fakeSend { Id = "R-22" }
assert (sends = 1)

match running.GetAwaiter().GetResult() with
| Ok(ValidResponse body) -> assert (body = "reply:R-22")
| Error error -> failwithf "Unexpected error: %A" error
```

The public contract and outer workflow use `Task`, matching the .NET client. F# task expressions can bind `Async<'T>` directly, so the validator needs no blocking call and no intermediate `Async.StartAsTask`.

Calling `execute` evaluates its task expression. It then evaluates `send request`, which starts the client's network operation according to that client's task-returning contract. The first `let!` waits for its result. If the client can throw synchronously before returning a task, that is also part of the boundary contract and will fault the outer task expression.

In production, the public function would accept and propagate a cancellation token. Chapter 23 adds that policy rather than smuggling it into this start-timing exercise.

## Exercise 3: define when each execution starts {#exercise-03}

### Classify the three APIs {#exercise-03-classification}

| API | Execution semantics |
|---|---|
| `refresh : Task<Snapshot>` | One task was created already; all callers receive or share that execution |
| `refreshAgain : unit -> Task<Snapshot>` | Every call evaluates the factory and ordinarily starts a new execution |
| `prepareRefresh : unit -> Async<Snapshot>` | Every call creates a deferred description; a later start creates an execution |

The signatures do not specify retries, overlap, caching duration, who may cancel, or whether a completed value remains reusable. Those are separate policies.

### Add a single-flight coordinator {#exercise-03-single-flight}

For a refresh that may run again after completion but must not overlap, a task factory can sit behind a small coordinator:

```fsharp
open System.Threading.Tasks

type Snapshot = { Version: int }

let release =
    TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
let mutable starts = 0

let createRefresh () =
    task {
        starts <- starts + 1
        do! release.Task
        return { Version = starts }
    }

let syncRoot = obj ()
let mutable inFlight: Task<Snapshot> option = None

let refresh () =
    lock syncRoot (fun () ->
        match inFlight with
        | Some current when not current.IsCompleted -> current
        | _ ->
            let started = createRefresh ()
            inFlight <- Some started
            started)

let first = refresh ()
let same = refresh ()
assert (obj.ReferenceEquals(first, same))
assert (starts = 1)

release.SetResult()
assert (first.GetAwaiter().GetResult().Version = 1)

let retry = refresh ()
assert (starts = 2)
assert (retry.GetAwaiter().GetResult().Version = 2)
```

The lock protects only selection and publication of the in-flight task; it does not hold a monitor across asynchronous work. The closed gate proves that callers receive the same task while work is incomplete. A later caller after completion starts a retry; the already-open test gate lets that second execution finish immediately.

A real API must still define whether faults are retried, whether a successful snapshot is cached, and whether one caller may cancel shared work. The last question is especially important: canceling one caller's wait is not necessarily permission to cancel the shared operation.

## Solution review {#solution-review}

- Signals establish happens-before relationships without timing guesses.
- `Async.StartAsTask` is both a start and an adaptation boundary.
- Calling a task factory starts a new task-expression execution; awaiting a stored task does not restart it.
- A task workflow can bind an F# async computation directly and remain task-shaped publicly.
- Single-flight behavior requires explicit shared-state policy; a return type alone does not provide it.
- Blocking calls in these snippets belong only to their top-level assertion boundary.
