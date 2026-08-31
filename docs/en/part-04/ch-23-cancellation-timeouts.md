---
title: "Chapter 23: Cancellation, Timeouts, Faults, and Disposal"
description: "Propagate cooperative cancellation, distinguish stopping work from abandoning a wait, preserve faults, and release resources on every asynchronous exit path."
translationKey: part-04/ch-23-cancellation-timeouts
---

# Chapter 23: Cancellation, Timeouts, Faults, and Disposal {#overview}

An asynchronous booking operation can finish in more ways than “a value arrived.” The caller may cancel, its waiting time may expire, or the operation may fault. Resources may also need cleanup before the outcome is final. Conflating these paths leaks handles, misreports errors, and leaves work running after its caller has gone.

Treat completion as a set of distinct rules: cancellation requires cooperation, a timeout expresses policy, a fault is not cancellation, and cleanup is part of completion. Controlled signals test each behavior without racing the scheduler against `sleep`.

## Cancellation is a request carried by a token {#cooperative-cancellation}

.NET cancellation separates roles:

- `CancellationTokenSource` can issue a cancellation request;
- `CancellationToken` is the lightweight value passed to listeners;
- each operation decides where it can observe the request safely;
- `OperationCanceledException` can report that cooperative cancellation was observed.

Cancellation is cooperative and cannot undo completed work. An operation checks the token at safe points and passes it to cancellation-aware APIs. A payment or file replacement remains visible after its commit point, so the API must define what a later cancellation request means.

F# `task {}` does not implicitly obtain or check a token. Make it an argument and pass the same token down every cancelable call:

```fsharp
let reserve load save request cancellationToken =
    task {
        cancellationToken.ThrowIfCancellationRequested()
        let! state = load request.EventId cancellationToken
        let next = decide state request
        do! save next cancellationToken
        return next
    }
```

Using `CancellationToken.None` midway through the call chain silently stops propagation. It is appropriate only when the called operation is deliberately independent of the caller's lifetime.

The example registers a callback that completes a controlled task as canceled with the same token:

```fsharp:line-numbers
let cancellableTask (cancellationToken: CancellationToken) =
    let completion = newGate<string> ()

    task {
        use _registration =
            cancellationToken.Register(fun () -> completion.TrySetCanceled(cancellationToken) |> ignore)

        return! completion.Task
    }

let operationCancellation = new CancellationTokenSource()
let canceledOperation = cancellableTask operationCancellation.Token
assert (not canceledOperation.IsCompleted)

operationCancellation.Cancel()

let operationCanceled, matchingToken =
    try
        canceledOperation.GetAwaiter().GetResult() |> ignore
        false, false
    with :? OperationCanceledException as cause ->
        true, cause.CancellationToken = operationCancellation.Token

assert operationCanceled
assert matchingToken
assert canceledOperation.IsCanceled
printfn "Operation cancellation: canceled=%b token=%b" operationCanceled matchingToken
operationCancellation.Dispose()
```
The task is pending before `Cancel`. After the request, awaiting it raises `OperationCanceledException`; the exception carries the expected token, and the task has `IsCanceled = true`. A `use` binding removes the registration when the task leaves its scope. The creator disposes the token source after observing the operation.

Cancellation tokens are one-way: once requested, a token remains canceled. Create a new source for a logically new operation; do not attempt to reset and reuse the old request.

## Cancel the operation or abandon the wait? {#operation-versus-wait}

Two policies are often described with the same phrase, “cancel the call”:

| Policy | Token reaches underlying operation? | What stops? |
|---|---|---|
| Cancel operation | Yes | Cooperative operation stops after observing the request and cleaning up |
| Abandon wait | No; token controls a wrapper wait | This caller stops waiting; underlying work may continue |

`Task<'T>.WaitAsync(cancellationToken)` demonstrates the second policy. It returns another task that completes when either the original task completes or the wait token is canceled. Canceling that token does not modify the original task:

```fsharp:line-numbers
let underlyingCompletion = newGate<string> ()
let waitCancellation = new CancellationTokenSource()
let abandonedWait = underlyingCompletion.Task.WaitAsync(waitCancellation.Token)

waitCancellation.Cancel()

let waitCanceled =
    try
        abandonedWait.GetAwaiter().GetResult() |> ignore
        false
    with :? OperationCanceledException ->
        true

assert waitCanceled
assert abandonedWait.IsCanceled
assert (not underlyingCompletion.Task.IsCompleted)
printfn "Abandoned wait: waiter-canceled=%b operation-pending=%b" waitCanceled true

underlyingCompletion.SetResult("late-result")
let underlyingResult = underlyingCompletion.Task.GetAwaiter().GetResult()
assert (underlyingResult = "late-result")
printfn "Underlying after abandon: result=%s" underlyingResult
waitCancellation.Dispose()
```
The test cancels the waiter, confirms that the underlying operation remains pending, then completes that task and reads its result. Each state transition follows a direct test action, independent of machine speed.

Abandoning a wait is useful when another component remains responsible for the work, such as a shared cache refresh, or when interruption is unsafe. It is dangerous if nobody will observe failure, limit resource use, or prevent duplicate side effects. Name the responsible component.

## A timeout is policy, not a synonym for cancellation {#timeout-policy}

A timeout answers “how long will this caller wait?” It does not by itself answer “should the operation stop?” Common policies are:

| Requirement | Mechanism | Underlying work after timeout |
|---|---|---|
| Stop a caller-owned cooperative operation | linked `CancellationTokenSource`, deadline/`CancelAfter`, token propagated to operation | Requested to stop; may take time to observe and clean up |
| Stop waiting for independently owned work | `WaitAsync(timeout)` | Continues unless its own owner cancels it |
| Distinguish timeout from caller cancellation | separate deadline signal or inspect which source requested | Policy can report `TimedOut` versus cancellation |

`WaitAsync(TimeSpan)` faults its wrapper task with `TimeoutException`; it does not cancel the source task. Overloads that accept `TimeProvider` let modern .NET tests control time. If timeout should also cancel the operation, create and dispose a linked source, schedule its deadline, and pass its token to the operation.

The test removes wall-clock time altogether. An injected task represents “the deadline fired”:

```fsharp:line-numbers
type WaitOutcome<'T> =
    | Completed of 'T
    | TimedOut

let awaitUntilSignal (operation: Task<'T>) (timeoutSignal: Task) =
    task {
        let! winner = Task.WhenAny [| operation :> Task; timeoutSignal |]

        if obj.ReferenceEquals(winner, operation) then
            let! value = operation
            return Completed value
        else
            return TimedOut
    }

let timedOperation = newGate<string> ()
let timeoutSignal = newGate<unit> ()
let timeoutObservation = awaitUntilSignal timedOperation.Task timeoutSignal.Task

assert (not timeoutObservation.IsCompleted)
timeoutSignal.SetResult()

let timedOut =
    match timeoutObservation.GetAwaiter().GetResult() with
    | TimedOut -> true
    | Completed _ -> false

assert timedOut
assert (not timedOperation.Task.IsCompleted)
printfn "Timeout signal: timed-out=%b operation-pending=%b" timedOut true
timedOperation.SetResult("finished-after-timeout")
```
`Task.WhenAny` identifies the first completed signal. Completing `timeoutSignal` deterministically yields `TimedOut`; the original operation remains pending. A production timer is one adapter that completes such a signal. The pure policy should not depend on a particular clock.

A timeout does not prove that the remote side did nothing. Before retrying a timed-out write, define idempotency or reconciliation. Chapter 37 returns to this distributed-systems problem.

## Fault, cancellation, and expected error are different outcomes {#faults}

A task has terminal states for successful completion, fault, and cancellation. Domain rejection such as “capacity exceeded” is normally a successful task whose value is `Error CapacityExceeded`, because the asynchronous mechanism worked and produced an expected business answer.

An unexpected exception inside `task {}` faults the returned task. Awaiting with `let!`, or using `GetAwaiter().GetResult()` in an outer test harness, surfaces the original exception:

```fsharp:line-numbers
let faultingTask () : Task<string> =
    task { return raise (InvalidOperationException "quote-failed") }

let faultedTask = faultingTask ()

let faultType, faultMessage =
    try
        faultedTask.GetAwaiter().GetResult() |> ignore
        "none", "none"
    with :? InvalidOperationException as cause ->
        cause.GetType().Name, cause.Message

assert faultedTask.IsFaulted
assert (faultType = "InvalidOperationException")
assert (faultMessage = "quote-failed")
printfn "Fault: type=%s message=%s" faultType faultMessage
```
By contrast, `.Wait()` and `.Result` are blocking APIs and commonly wrap task failures in `AggregateException`. Application workflows should use `let!`; tests and process entry points can use the awaiter form when they deliberately must bridge to synchronous code.

Catch only where the code can decide what to do. Translate a documented remote rejection into a typed error if callers can act on it. Preserve unknown infrastructure exceptions, inner causes, and stack traces. Do not turn cancellation into `Error "failed"`, or treat any `OperationCanceledException` as this operation's cancellation without checking its token and API rules.

## Cleanup is part of asynchronous completion {#cleanup}

Chapter 21 used `use` for `IDisposable`. The same rule applies inside tasks: once acquisition succeeds, cleanup must run when the body succeeds, faults, or observes cancellation.

Some resources can dispose synchronously. Others implement `IAsyncDisposable`; their `DisposeAsync()` returns a `ValueTask` because flushing or closing may itself need asynchronous I/O. An outer task must not report completion until that cleanup completes.

Test probes expose both disposal paths:

```fsharp:line-numbers
type SyncProbe(label: string, disposed: ResizeArray<string>) =
    interface IDisposable with
        member _.Dispose() = disposed.Add label

type AsyncProbe
    (
        label: string,
        started: TaskCompletionSource<unit>,
        release: TaskCompletionSource<unit>,
        disposed: ResizeArray<string>
    ) =
    interface IAsyncDisposable with
        member _.DisposeAsync() =
            let disposal =
                task {
                    disposed.Add $"{label}:start"
                    started.TrySetResult() |> ignore
                    do! release.Task
                    disposed.Add $"{label}:done"
                }

            ValueTask(disposal)

let usingAsync (resource: IAsyncDisposable) (body: unit -> Task<'T>) =
    task {
        let! outcome =
            task {
                try
                    let! value = body ()
                    return Ok value
                with error ->
                    return Error(ExceptionDispatchInfo.Capture error)
            }

        do! resource.DisposeAsync()

        match outcome with
        | Ok value -> return value
        | Error failure ->
            failure.Throw()
            return Unchecked.defaultof<'T>
    }
```
In a compiled `.fs` file, a task expression can bind an `IAsyncDisposable` with `use`; the task builder awaits `DisposeAsync`. `use!` first awaits acquisition and then manages the acquired resource. Task-expression `with` and `finally` handlers are synchronous, so use a resource binding instead of placing asynchronous cleanup inside `finally`.

### A known FSI limitation {#fsi-async-disposal}

F# 10 supports task `use` with `IAsyncDisposable` in compiled projects. F# Interactive still has an open compiler issue: the same binding in an `.fsx` file can incorrectly require `IDisposable`.

Because the chapter example must run as an FSI script, its asynchronous probe uses a small `usingAsync` adapter. The adapter captures the body outcome, awaits disposal exactly once, then returns the value or rethrows the original failure through `ExceptionDispatchInfo`. It demonstrates lifecycle behavior but does not replace built-in `use` in normal compiled code.

### Prove all six cleanup paths {#all-cleanup-paths}

Synchronous cleanup is tested first:

```fsharp:line-numbers
let syncDisposed = ResizeArray<string>()

let runWithSyncResource path (cancellationToken: CancellationToken) =
    task {
        use _resource = new SyncProbe(pathLabel path, syncDisposed)

        match path with
        | Success -> return "ok"
        | Failure -> return raise (InvalidDataException "sync-failure")
        | Cancellation ->
            cancellationToken.ThrowIfCancellationRequested()
            return "unreachable"
    }

let syncSuccess =
    runWithSyncResource Success CancellationToken.None
    |> fun running -> running.GetAwaiter().GetResult() = "ok"

let syncFault =
    try
        let running = runWithSyncResource Failure CancellationToken.None
        running.GetAwaiter().GetResult() |> ignore

        false
    with :? InvalidDataException ->
        true

let syncCancellation = new CancellationTokenSource()
syncCancellation.Cancel()
let syncCanceledTask = runWithSyncResource Cancellation syncCancellation.Token

let syncCanceled =
    try
        syncCanceledTask.GetAwaiter().GetResult() |> ignore
        false
    with :? OperationCanceledException ->
        true

assert syncSuccess
assert syncFault
assert syncCanceled
assert syncCanceledTask.IsCanceled
assert (Seq.toList syncDisposed = [ "success"; "failure"; "cancel" ])
printfn "Sync dispose: success=%b fault=%b cancel=%b" syncSuccess syncFault syncCanceled
syncCancellation.Dispose()
```
The disposal log is exactly `success`, `failure`, `cancel`. A pre-canceled token is checked only after resource acquisition, showing that cancellation still exits through the managed scope.

The asynchronous test starts three operations, one for each body outcome. Every `DisposeAsync` announces entry and waits on a separate release gate:

```fsharp:line-numbers
let asyncDisposed = ResizeArray<string>()

let runWithAsyncResource label path (cancellationToken: CancellationToken) started release =
    let resource =
        new AsyncProbe(label, started, release, asyncDisposed) :> IAsyncDisposable

    usingAsync resource (fun () ->
        task {
            match path with
            | Success -> return "ok"
            | Failure -> return raise (InvalidDataException "async-failure")
            | Cancellation ->
                cancellationToken.ThrowIfCancellationRequested()
                return "unreachable"
        })

let successStarted, successRelease = newGate<unit> (), newGate<unit> ()
let failureStarted, failureRelease = newGate<unit> (), newGate<unit> ()
let cancelStarted, cancelRelease = newGate<unit> (), newGate<unit> ()
let asyncCancellation = new CancellationTokenSource()
asyncCancellation.Cancel()

let asyncSuccessTask =
    runWithAsyncResource "success" Success CancellationToken.None successStarted successRelease

let asyncFaultTask =
    runWithAsyncResource "failure" Failure CancellationToken.None failureStarted failureRelease

let asyncCanceledTask =
    runWithAsyncResource "cancel" Cancellation asyncCancellation.Token cancelStarted cancelRelease

successStarted.Task.GetAwaiter().GetResult()
failureStarted.Task.GetAwaiter().GetResult()
cancelStarted.Task.GetAwaiter().GetResult()

let allPendingBeforeRelease =
    not asyncSuccessTask.IsCompleted
    && not asyncFaultTask.IsCompleted
    && not asyncCanceledTask.IsCompleted

assert allPendingBeforeRelease

successRelease.SetResult()
let asyncSuccess = asyncSuccessTask.GetAwaiter().GetResult() = "ok"

failureRelease.SetResult()

let asyncFault =
    try
        asyncFaultTask.GetAwaiter().GetResult() |> ignore
        false
    with :? InvalidDataException ->
        true

cancelRelease.SetResult()

let asyncCanceled =
    try
        asyncCanceledTask.GetAwaiter().GetResult() |> ignore
        false
    with :? OperationCanceledException ->
        true

assert asyncSuccess
assert asyncFault
assert asyncCanceled
assert asyncCanceledTask.IsCanceled

assert
    (Seq.toList asyncDisposed = [ "success:start"
                                  "failure:start"
                                  "cancel:start"
                                  "success:done"
                                  "failure:done"
                                  "cancel:done" ])

printfn
    "Async dispose: pending=%b success=%b fault=%b cancel=%b"
    allPendingBeforeRelease
    asyncSuccess
    asyncFault
    asyncCanceled

asyncCancellation.Dispose()
```
All three outer tasks remain incomplete after disposal starts. Only after the corresponding gate is released does each task expose success, the original fault, or cancellation. This proves that disposal is awaited rather than merely invoked.

If cleanup fails while a body failure is already propagating, decide how diagnostics will retain both. Normal language-level cleanup may expose the cleanup exception and obscure the first one. At infrastructure integration points, log or aggregate them according to a stated policy; never silently discard a disposal failure.

## Manage cancellation helper lifetimes {#helper-lifetimes}

Cancellation machinery has lifetimes:

- dispose each `CancellationTokenRegistration` when it should stop listening;
- dispose each `CancellationTokenSource`, including linked and timeout sources;
- do not dispose a source while operations still depend on callbacks from it;
- never return a resource whose `use` scope has already ended;
- when acquisition itself is asynchronous, responsibility begins only after acquisition succeeds.

Cancellation should normally skip new optional work, but it must not skip cleanup for resources already acquired. Do not pass a canceled token to cleanup if that could leave required release unfinished; the resource's documented behavior determines whether cleanup may safely be canceled.

## Exercises {#exercises}

### Exercise 1: find the broken token chain {#exercise-01}

Implement `confirmBooking` over two ports, `charge` and `notify`, both accepting `CancellationToken`. Write recording fakes and prove the exact caller token reaches both. Then introduce `CancellationToken.None` at one call and make the test fail for the right reason.

State where cancellation checks belong relative to the irreversible charge and the optional notification.


::: details Answer

#### Record the exact token {#exercise-01-recording}

```fsharp
open System.Threading
open System.Threading.Tasks

type Booking = { Id: string; Amount: decimal }
type Receipt = { Id: string }

type Ports =
    {
        Charge: Booking -> CancellationToken -> Task<Receipt>
        Notify: Receipt -> CancellationToken -> Task<unit>
    }

let confirmBooking
    (ports: Ports)
    (booking: Booking)
    (cancellationToken: CancellationToken)
    =
    task {
        cancellationToken.ThrowIfCancellationRequested()
        let! receipt = ports.Charge booking cancellationToken
        do! ports.Notify receipt cancellationToken
        return receipt
    }

let seen = ResizeArray<string * CancellationToken>()

let ports =
    {
        Charge = fun booking token ->
            seen.Add("charge", token)
            Task.FromResult { Id = $"receipt:{booking.Id}" }
        Notify = fun receipt token ->
            seen.Add("notify", token)
            Task.FromResult(())
    }

let owner = new CancellationTokenSource()
let booking = { Id = "B-23"; Amount = 42M }
let receipt = confirmBooking ports booking owner.Token |> fun running -> running.Result

assert (receipt.Id = "receipt:B-23")
assert (seen.Count = 2)
assert (seen |> Seq.forall (fun (_, token) -> token = owner.Token))
owner.Dispose()
```

The use of `.Result` above is only a compact synchronous test boundary. Inside `confirmBooking`, both calls are awaited and receive the caller's exact token.

A broken variant passes `CancellationToken.None` to `Notify`. Its recording test should assert that both entries equal `owner.Token`; the second entry then makes the assertion fail. Testing token identity catches a disconnected chain even when the fake completes too quickly for cancellation behavior to reveal it.

#### Place checks around the commit point {#exercise-01-commit}

Check cancellation before starting a charge that should not begin for an abandoned request, and propagate the token if the payment API defines safe cancellation. Once the provider confirms an irreversible charge, returning a canceled overall result can hide a committed effect.

A production workflow should persist the receipt or committed state before optional notification. Notification can have its own retry or cancellation policy, and the returned model can distinguish `ConfirmedButNotificationPending`. The simple function verifies token wiring; it is not a complete payment consistency protocol.

:::

### Exercise 2: implement two timeout policies {#exercise-02}

Given a controlled underlying task, implement `abandonAfter` with a timeout signal and `cancelAfter` with an operation token. Prove the first leaves underlying work pending while the second makes a cooperating operation canceled.

Return distinct typed outcomes for timeout and caller cancellation. Do not use a duration in the test.


::: details Answer

#### Separate wait outcomes {#exercise-02-outcomes}

```fsharp
open System
open System.Threading
open System.Threading.Tasks

type WaitError =
    | TimedOut
    | CallerCanceled

let observe (operation: Task<'T>) (timeoutSignal: Task) (callerSignal: Task) =
    task {
        let! winner =
            Task.WhenAny [| operation :> Task; timeoutSignal; callerSignal |]

        if obj.ReferenceEquals(winner, operation) then
            let! value = operation
            return Ok value
        elif obj.ReferenceEquals(winner, timeoutSignal) then
            return Error TimedOut
        else
            return Error CallerCanceled
    }
```

The test supplies distinct, controlled deadline and caller signals. A production adapter may complete the latter from a `CancellationTokenRegistration` and the former from `TimeProvider`; the decision logic stays unchanged.

#### Abandon only this wait {#exercise-02-abandon}

```fsharp
let operation = TaskCompletionSource<string>()
let deadline = TaskCompletionSource<unit>()
let caller = TaskCompletionSource<unit>()

let waiting = observe operation.Task deadline.Task caller.Task
deadline.SetResult()

assert (waiting.GetAwaiter().GetResult() = Error TimedOut)
assert (not operation.Task.IsCompleted)

operation.SetResult("owned-elsewhere")
assert (operation.Task.GetAwaiter().GetResult() = "owned-elsewhere")
```

The timeout ends this observation, not the operation. Another component must retain and observe `operation.Task`.

#### Request cancellation of the operation {#exercise-02-cancel}

```fsharp
let startCooperating (token: CancellationToken) =
    let completion =
        TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously)

    let registration =
        token.Register(fun () -> completion.TrySetCanceled(token) |> ignore)

    completion.Task, registration

let cancelAfter start timeoutSignal callerSignal =
    task {
        use operationSource = new CancellationTokenSource()
        let operation, registration = start operationSource.Token
        use _registration = registration
        let! observed = observe operation timeoutSignal callerSignal

        match observed with
        | Ok value -> return Ok value
        | Error reason ->
            operationSource.Cancel()

            try
                let! _ = operation
                return Error reason
            with :? OperationCanceledException ->
                return Error reason
    }

let deadline2 = TaskCompletionSource<unit>()
let caller2 = TaskCompletionSource<unit>()
let timed = cancelAfter startCooperating deadline2.Task caller2.Task
deadline2.SetResult()
assert (timed.GetAwaiter().GetResult() = Error TimedOut)

let deadline3 = TaskCompletionSource<unit>()
let caller3 = TaskCompletionSource<unit>()
let canceled = cancelAfter startCooperating deadline3.Task caller3.Task
caller3.SetResult()
assert (canceled.GetAwaiter().GetResult() = Error CallerCanceled)
```

The winning signal is classified before `operationSource.Cancel()` is called, so the typed reason remains deterministic. The helper waits for the cooperating operation to acknowledge cancellation before returning; cleanup is therefore inside the owned lifetime.

In real code, link the operation source to the actual caller token or register it explicitly. Decide whether a caller cancellation should be returned as task cancellation instead of a typed `Error`; either contract can be valid, but do not mix them unpredictably.

:::

### Exercise 3: audit asynchronous cleanup {#exercise-03}

Write a compiled task expression using an `IAsyncDisposable` probe and `use`. Make disposal wait on a gate. Prove the outer task remains pending during cleanup after success, fault, and cancellation.

Then make disposal fault. Record which exception reaches the caller and propose a diagnostic policy that retains both a body fault and cleanup fault.


::: details Answer

#### Use the compiled task binding {#exercise-03-compiled}

Place this code in a compiled `.fs` file rather than FSI, because of the FSI limitation described in the chapter:

```fsharp
open System
open System.IO
open System.Threading
open System.Threading.Tasks

type Exit = Success | Fault | Cancel

let run
    exit
    (cancellationToken: CancellationToken)
    (started: TaskCompletionSource<unit>)
    (release: TaskCompletionSource<unit>)
    =
    task {
        use _resource =
            { new IAsyncDisposable with
                member _.DisposeAsync() =
                    let disposing =
                        task {
                            started.SetResult()
                            do! release.Task
                        }

                    ValueTask(disposing) }

        match exit with
        | Success -> return "ok"
        | Fault -> return raise (InvalidDataException "body-fault")
        | Cancel ->
            cancellationToken.ThrowIfCancellationRequested()
            return "unreachable"
    }
```

For each case, create fresh `TaskCompletionSource<unit>` values with `RunContinuationsAsynchronously`. Start `run`, await `started.Task` at the test boundary, and assert the outer task is incomplete. Release disposal, then assert respectively:

- result equals `"ok"`;
- awaiting raises `InvalidDataException("body-fault")`;
- awaiting raises `OperationCanceledException` and the task is canceled.

This is the compiled-language form that the shared FSI adapter models.

#### Make cleanup failure explicit {#exercise-03-cleanup-fault}

Change `DisposeAsync` so it raises `IOException("dispose-fault")` after its gate. On a successful body, the caller observes that cleanup fault. On an already-faulted body, the cleanup fault is ordinarily the visible exception from the cleanup mechanism; verify the exact behavior of the builder and runtime version you ship.

If both causes matter operationally, catch at a boundary that can preserve them: record the body failure before cleanup, then log or aggregate it with the cleanup failure. Do not blindly retry disposal or return only one message string. Resource contracts determine whether repeated disposal is safe.

:::


The next chapter separates concurrency from parallelism and compares immutable coordination, agents, locks, atomics, and intentionally controlled mutation.

## Sources {#sources}

- [Microsoft Learn: F# task expressions](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/task-expressions)
- [Microsoft Learn: cancellation in managed threads](https://learn.microsoft.com/en-us/dotnet/standard/threading/cancellation-in-managed-threads)
- [Microsoft Learn: task cancellation](https://learn.microsoft.com/en-us/dotnet/standard/parallel-programming/task-cancellation)
- [Microsoft Learn: `Task<TResult>.WaitAsync`](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task-1.waitasync?view=net-10.0)
- [Microsoft Learn: `IAsyncDisposable`](https://learn.microsoft.com/en-us/dotnet/api/system.iasyncdisposable?view=net-10.0)
- [dotnet/fsharp issue #14454: FSI `IAsyncDisposable` `use` limitation](https://github.com/dotnet/fsharp/issues/14454)
