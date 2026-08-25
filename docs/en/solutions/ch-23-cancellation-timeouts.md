---
title: "Chapter 23 Solutions"
description: "Verify token propagation, implement abandon-wait and cancel-work timeout policies with signals, and test compiled asynchronous disposal."
translationKey: solutions/ch-23-cancellation-timeouts
---

# Chapter 23 Solutions {#overview}

These solutions test ownership decisions, not elapsed time. A recorded token proves propagation; explicit tasks represent caller and deadline signals; an async-disposable gate proves cleanup is awaited.

[Return to Chapter 23](../part-04/ch-23-cancellation-timeouts).

## Exercise 1: find the broken token chain {#exercise-01}

### Record the exact token {#exercise-01-recording}

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

### Place checks around the commit point {#exercise-01-commit}

Check cancellation before starting a charge that should not begin for an abandoned request, and propagate the token if the payment API defines safe cancellation. Once the provider confirms an irreversible charge, returning a canceled overall result can hide a committed effect.

A production workflow should persist the receipt or committed state before optional notification. Notification can have its own retry or cancellation policy, and the returned model can distinguish `ConfirmedButNotificationPending`. The simple function proves token wiring; it is not a complete payment consistency protocol.

## Exercise 2: implement two timeout policies {#exercise-02}

### Separate wait outcomes {#exercise-02-outcomes}

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

### Abandon only this wait {#exercise-02-abandon}

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

The timeout ends this observation, not the operation. Some other owner must retain and observe `operation.Task`.

### Request that owned work stop {#exercise-02-cancel}

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

## Exercise 3: audit asynchronous cleanup {#exercise-03}

### Use the compiled task binding {#exercise-03-compiled}

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

### Make cleanup failure explicit {#exercise-03-cleanup-fault}

Change `DisposeAsync` so it raises `IOException("dispose-fault")` after its gate. On a successful body, the caller observes that cleanup fault. On an already-faulted body, the cleanup fault is ordinarily the visible exception from the cleanup mechanism; verify the exact behavior of the builder and runtime version you ship.

If both causes matter operationally, catch at a boundary that can preserve them: record the body failure before cleanup, then log or aggregate it with the cleanup failure. Do not blindly retry disposal or return only one message string. Resource contracts determine whether repeated disposal is safe.

## Solution review {#solution-review}

- Recording the exact token tests propagation without a timing race.
- Cancellation placement must respect irreversible commit points.
- A deadline signal and caller signal remain distinct before either asks owned work to stop.
- Abandon-wait requires another owner for continued work and its eventual fault.
- Cancel-work waits for cooperative acknowledgement and cleanup.
- Compiled task `use` waits for `IAsyncDisposable.DisposeAsync` on every body exit.
- Cleanup failure needs an explicit diagnostic policy when another failure already exists.
