---
title: "Chapter 22: Async<'T> and Task<'T>"
description: "Model work that completes later, observe the different start semantics of F# async and task expressions, and cross .NET boundaries without blocking."
translationKey: part-04/ch-22-async-task
---

# Chapter 22: `Async<'T>` and `Task<'T>` {#overview}

A booking service asks a remote price provider for a quote. The answer arrives later, but the calling thread should remain available. Two F# values can represent that eventual answer: an F# asynchronous computation, `Async<Quote>`, or a .NET task, `Task<Quote>`. Their result types look similar, but their start behavior and surrounding APIs differ.

Begin with responsibilities rather than syntax. Who creates the work? When does it start? Who awaits and handles the outcome? Once those answers are clear, `async {}` and `task {}` become distinct tools instead of interchangeable punctuation.

## A later result has more than a value type {#three-questions}

For any operation that completes later, ask three questions:

1. **Description:** is this value a reusable description or a handle for one execution?
2. **Start:** does construction defer work, or did evaluating the expression start it?
3. **Observation:** who awaits and handles success, failure, or cancellation?

`Async<'T>` and `Task<'T>` both carry an eventual `'T`, but they answer the first two questions differently:

| Value produced by ordinary F# code | Meaning at the return point | Start action |
|---|---|---|
| `async { ... } : Async<'T>` | A description of a computation | A caller uses an `Async` starting function or another workflow starts it |
| evaluated `task { ... } : Task<'T>` | A handle for that task-expression execution | Evaluation starts it immediately |

This table is deliberately narrow. Lower-level APIs can also create a .NET `Task`, including through a constructor whose task has not yet been scheduled. The stated behavior applies to an F# **task expression**: evaluation starts it immediately, and it runs on the current thread until its first incomplete asynchronous operation.

Neither representation promises a new thread. Asynchrony means the caller need not block while work is pending. Concurrency and parallelism are scheduling choices covered in Chapter 24.

## `async {}` builds work without starting it {#async-start}

The shared example creates two explicit signals. `asyncEntered` records entry into the body; `asyncRelease` keeps the body from finishing. There is no clock and no assumed scheduler speed.

```fsharp:line-numbers [ch22-async-task.fsx]
let asyncEntered = newGate<bool> ()
let asyncRelease = newGate<unit> ()

let deferredAsync =
    async {
        asyncEntered.SetResult true
        do! Async.AwaitTask asyncRelease.Task
        return "async-done"
    }

assert (not asyncEntered.Task.IsCompleted)
printfn "Async before start: entered=false"

let runningAsync = Async.StartAsTask deferredAsync
asyncEntered.Task.GetAwaiter().GetResult() |> ignore

assert asyncEntered.Task.IsCompleted
assert (not runningAsync.IsCompleted)
printfn "Async after StartAsTask: entered=true completed=false"

asyncRelease.SetResult()
let asyncResult = runningAsync.GetAwaiter().GetResult()
assert (asyncResult = "async-done")
printfn "Async result: %s" asyncResult
```
Constructing `deferredAsync` does not set `asyncEntered`, so the first assertion observes a fact rather than a race. `Async.StartAsTask deferredAsync` starts the computation and returns a task for this execution. Waiting for the entry signal proves that the body has begun. Because the release signal is still closed, the returned task remains incomplete.

An `Async<'T>` value may be started again. Each start creates a new execution and repeats its side effects. That repeatability helps composition, but it is not automatic memoization. If a card must be charged only once, define who may start the work or make the operation idempotent before exposing a restartable value.

Common starting functions behave differently:

| Operation | Result | Caller behavior |
|---|---|---|
| `Async.StartAsTask work` | `Task<'T>` | Starts work and gives the caller an observable handle |
| `Async.RunSynchronously work` | `'T` | Starts and blocks the current caller until completion |
| `Async.Start work` for `Async<unit>` | `unit` | Starts without returning a completion handle |

Prefer a completion handle or a parent workflow that awaits the operation. Fire-and-forget `Async.Start` makes it easy to lose exceptions and let work outlive its caller. `Async.RunSynchronously` suits a script or program entry point where blocking is intentional; do not scatter it through server request paths or UI handlers.

## `task {}` starts when the expression is evaluated {#task-start}

The second half uses the same test setup:

```fsharp:line-numbers [ch22-async-task.fsx]
let taskEntered = newGate<bool> ()
let taskRelease = newGate<unit> ()

let immediateTask () =
    task {
        taskEntered.SetResult true
        do! taskRelease.Task
        return "task-done"
    }

let runningTask = immediateTask ()

assert taskEntered.Task.IsCompleted
assert (not runningTask.IsCompleted)
printfn "Task after call: entered=true completed=false"

taskRelease.SetResult()
let taskResult = runningTask.GetAwaiter().GetResult()
assert (taskResult = "task-done")
printfn "Task result: %s" taskResult
```
Calling `immediateTask ()` evaluates the `task {}` expression. Its body sets `taskEntered` synchronously, reaches the incomplete `taskRelease.Task`, and returns an incomplete `Task<string>`. Thus both assertions are valid immediately after the call:

- the body has entered;
- the overall operation has not completed.

If the body encounters no incomplete await, it may complete before the factory returns. “Asynchronous” does not mean “always later,” and `task { return 42 }` is allowed to produce an already-completed task.

Place a task expression inside a function when each call should create one execution:

```fsharp
let fetchQuote requestId =
    task {
        // Calling fetchQuote starts this execution.
        return $"quote:{requestId}"
    }
```

A module-level binding such as `let quoteTask = fetchQuote "R-22"` instead stores the one task that was started while the module initialized. Later consumers share that execution and result. Choose a factory or a shared task deliberately.

## Computation expressions define operation order {#workflow-syntax}

Inside either computation expression:

- `let name = expression` evaluates an ordinary expression now;
- `let! name = computation` waits asynchronously and binds its successful result;
- `do! computation` waits asynchronously for an operation whose useful result is `unit`;
- `return value` supplies the workflow result;
- `return! computation` delegates the workflow result to another computation.

For example, code around a task-based .NET API can stay task-based:

```fsharp
let quoteAndReserve fetchQuote reserve request =
    task {
        let! quote = fetchQuote request
        do! reserve quote
        return quote.Id
    }
```

The second operation starts only after the first produces `quote`, so this code is sequential. Top-to-bottom syntax does not automatically make independent calls concurrent. Introduce concurrency only where requirements allow it, while accounting for rate limits, partial failure, and cancellation.

Waiting with `let!` yields control according to the workflow and awaited operation. Reading `.Result`, calling `.Wait()`, or using `GetAwaiter().GetResult()` blocks the current thread. The shared script uses the last form only in its outer test harness, keeping the process alive for assertions; application workflows should continue with `let!`.

## Convert once where the two models meet {#interop}

The platform exposes many `Task<'T>` APIs, while existing F# libraries and codebases may expose `Async<'T>`. Conversion is explicit:

```fsharp:line-numbers [ch22-async-task.fsx]
let taskFromAsync = async { return 21 } |> Async.StartAsTask

let asyncFromTask = task { return 42 } |> Async.AwaitTask

let fromAsync = taskFromAsync.GetAwaiter().GetResult()
let fromTask = Async.RunSynchronously asyncFromTask

assert (fromAsync = 21)
assert (fromTask = 42)
printfn "Interop: async-to-task=%d task-to-async=%d" fromAsync fromTask
```
`Async.StartAsTask` both starts the async computation and returns a task. `Async.AwaitTask` returns an async computation that will wait for the supplied task when that async computation is started; it does not rewind or delay a task that is already running.

An F# task expression can also bind an `Async<'T>` directly with `let!`. Choose the form that keeps the surrounding workflow consistent. At this conversion point, exception and cancellation behavior affects callers; it is not merely a type conversion. Chapter 23 tests that behavior.

A useful rule at integration points is:

```text
external Task API → adapt once if needed → one internal workflow style
                                      → adapt once at the public boundary
```

Repeated `Async` → `Task` → `Async` conversion obscures which call starts work and which cancellation policy is active.

## Choose for the surrounding API {#choice}

There is no universal winner:

| Context | Usually start with | Reason |
|---|---|---|
| ASP.NET Core or a public .NET API | `Task<'T>` / `task {}` | The host and most .NET libraries already exchange tasks |
| F# code centered on `Async` combinators | `Async<'T>` / `async {}` | Deferred descriptions compose naturally before one explicit start |
| Existing dependency returns one representation | That representation | Avoid adapters that add no policy |
| Caller must decide whether work starts | `Async<'T>` or a factory function | Construction can remain separate from execution |
| One execution should be shared | A deliberately stored `Task<'T>` | The value names that execution and eventual outcome |
| CPU-bound calculation | Neither by itself | Measure and choose explicit scheduling or parallel tools |

Task expressions are usually the direct choice for new code that works extensively with task-based .NET APIs. `Async` remains useful when the design relies on its deferred model, combinators, asynchronous tail calls, or implicit cancellation-token flow. The next chapter covers cancellation differences so this choice does not collapse into a slogan.

Do not add a wrapper interface merely to hide `Task` or `Async`. When tests or architecture need a replaceable dependency, abstract the meaningful operation, such as `QuoteRequest -> Task<Quote>`. Its eventual-result type can remain visible.

## Test state transitions, not elapsed time {#deterministic-testing}

A test that sleeps for 20 milliseconds and expects work to have started is testing machine load and scheduler luck. A signal makes the causal order explicit:

```text
construct/call → observe entered gate → assert incomplete → release gate → observe result
```

`TaskCompletionSource<'T>` lets test code control completion of a task. The example requests `RunContinuationsAsynchronously` so releasing a gate does not unexpectedly execute its continuations inline on the releasing call. This option does not change the start rule being tested.

Use a fresh gate set for each execution. Complete every gate in cleanup when a failed assertion could otherwise leave work pending. Production code should await real APIs, not expose test gates; the gate is a deterministic substitute for an external completion event.

## Run the shared example {#run-example}

From the repository root:

```console
dotnet fsi --exec examples/scripts/ch22-async-task.fsx
```

Six deterministic lines demonstrate construction versus start, suspension before completion, eventual results, and conversion in both directions. Compare their order.

## Exercises {#exercises}

### Exercise 1: predict and prove entry {#exercise-01}

Write one `Async<int>` and one `unit -> Task<int>`. Each increments a private counter before waiting on an injected gate. Without using sleep, prove the counter values immediately after construction or call, after explicit async start, and after release.

Explain why calling the task factory twice differs from awaiting the same returned task twice.

### Exercise 2: keep one internal representation {#exercise-02}

A .NET client exposes `send: Request -> Task<Response>`, while an existing F# validator exposes `validate: Response -> Async<Result<ValidResponse, Error>>`.

Implement a task-returning public workflow. Adapt at the point of composition, do not block inside it, and identify exactly when network work starts.

### Exercise 3: make start ownership explicit {#exercise-03}

Audit these APIs:

```fsharp
val refresh : Task<Snapshot>
val refreshAgain : unit -> Task<Snapshot>
val prepareRefresh : unit -> Async<Snapshot>
```

For each API, state whether callers share an execution, create and immediately start one, or create a deferred description. Choose the safest API for a refresh that may be retried but must not overlap, and describe the concurrency rule still required.

[Read the chapter solutions](../solutions/ch-22-async-task).

The next chapter carries these start models into cancellation, timeout, fault propagation, and resource release on every completion path.

## Sources {#sources}

- [Microsoft Learn: Async and Task Programming in F#](https://learn.microsoft.com/en-us/dotnet/fsharp/tutorials/async)
- [Microsoft Learn: F# async expressions](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/async-expressions)
- [Microsoft Learn: F# task expressions](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/task-expressions)
- [FSharp.Core reference: `Async`](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-control-fsharpasync.html)
- [Microsoft Learn: `TaskCompletionSource<TResult>`](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.taskcompletionsource-1?view=net-10.0)
