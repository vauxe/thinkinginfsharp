---
title: "第 23 章：取消、超时、故障与释放"
description: "传播协作式取消，区分停止工作与放弃等待，保留故障，并在每条异步退出路径上释放资源。"
translationKey: part-04/ch-23-cancellation-timeouts
---

# 第 23 章：取消、超时、故障与释放 {#overview}

异步预约操作不只会“返回一个值”：调用方可能取消，等待时间可能耗尽，操作也可能发生故障。资源还可能需要先完成清理，结果才算最终确定。混淆这些情况会造成句柄泄漏、错误分类，以及调用方离开后工作仍在运行。

应分别处理各种结束情况：取消需要操作主动配合，超时体现业务策略，故障不等于取消，清理也是完成过程的一部分。测试用受控信号触发每种行为，不依赖 `sleep` 或调度速度。

## 取消是由令牌携带的请求 {#cooperative-cancellation}

.NET 取消模型分开了几种角色：

- `CancellationTokenSource` 可以发出取消请求；
- `CancellationToken` 是传给监听者的轻量值；
- 每项操作决定在哪里可以安全地观察请求；
- `OperationCanceledException` 可以报告已经观察到协作式取消。

取消需要协作，而且不能撤销已经完成的工作。操作应在安全点检查令牌，并把它传给支持取消的 API。支付或文件替换一旦越过提交点，其结果仍然可见，因此 API 必须规定随后到达的取消请求意味着什么。

F# `task {}` 不会隐式取得或检查令牌。把它作为参数，并向下传递给每个可取消调用：

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

在调用链中途使用 `CancellationToken.None` 会悄悄切断传播。只有被调用操作确实独立于调用方生命周期时，这样做才合适。

示例注册一个回调，用相同令牌把受控任务完成为已取消：

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
任务在 `Cancel` 前处于挂起状态。发出请求后，等待它会抛出 `OperationCanceledException`；异常携带预期令牌，且任务的 `IsCanceled = true`。`use` 绑定会在任务离开作用域时移除注册。创建令牌源的代码在观察操作结果后将其释放。

取消令牌是单向的：一旦请求，令牌就会一直保持取消状态。为逻辑上的新操作创建新令牌源；不要试图重置并复用旧请求。

## 取消操作还是放弃等待？ {#operation-versus-wait}

两种策略常常都被称为“取消调用”：

| 策略 | 令牌是否到达底层操作？ | 什么会停止？ |
|---|---|---|
| 取消操作 | 是 | 协作式操作观察请求并清理后停止 |
| 放弃等待 | 否；令牌控制包装等待 | 这个调用方停止等待；底层工作可能继续 |

`Task<'T>.WaitAsync(cancellationToken)` 展示第二种策略。它返回另一个任务，在原任务完成或等待令牌取消时完成。取消该令牌不会修改原任务：

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
测试先取消等待者，确认底层操作仍然挂起，再完成底层任务并读取结果。每次状态转换都由测试直接触发，与机器速度无关。

如果工作由其他组件负责，例如共享缓存刷新，或者无法安全中断，那么放弃等待很有用。若此后无人观察故障、限制资源用量或阻止重复副作用，这种做法就很危险。应明确写出继续负责的组件。

## 超时是策略，不是取消的同义词 {#timeout-policy}

超时回答“这个调用方会等多久？”它本身并不回答“操作是否应停止？”常见策略如下：

| 需求 | 机制 | 超时后的底层工作 |
|---|---|---|
| 停止由调用方负责的协作式操作 | 链接的 `CancellationTokenSource`、截止时间/`CancelAfter`，并把令牌传给操作 | 收到停止请求；观察并清理可能需要时间 |
| 停止等待由其他组件负责的工作 | `WaitAsync(timeout)` | 继续，除非负责它的组件另行取消 |
| 区分超时与调用方取消 | 分离的截止信号，或检查哪个源发出请求 | 策略可以报告 `TimedOut` 或取消 |

`WaitAsync(TimeSpan)` 会使包装任务以 `TimeoutException` 结束，但不会取消源任务。现代 .NET 中接受 `TimeProvider` 的重载可以让测试控制时间。如果超时还应取消操作，就创建并释放链接令牌源，设置截止时间，并把令牌传给操作。

测试完全不使用真实时钟。注入的任务表示“截止时间已到”：

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
`Task.WhenAny` 识别最先完成的信号。完成 `timeoutSignal` 会确定性地产生 `TimedOut`；原操作仍然挂起。生产计时器只是完成这种信号的一种适配器。纯策略不应依赖特定时钟。

超时并不能证明远端没有执行操作。重试超时的写入前，应先设计幂等或对账策略。第 37 章会继续讨论这个分布式系统问题。

## 故障、取消与预期错误是不同结果 {#faults}

任务的终止状态包括成功完成、故障与取消。“容量不足”这样的领域拒绝通常是值为 `Error CapacityExceeded` 的成功任务，因为异步机制正常工作并产生了预期业务答案。

`task {}` 中的意外异常会使返回任务进入故障状态。用 `let!` 等待，或在最外层测试代码中使用 `GetAwaiter().GetResult()`，都能得到原始异常：

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
相比之下，`.Wait()` 与 `.Result` 是阻塞 API，通常会用 `AggregateException` 包装任务故障。应用工作流应使用 `let!`；测试和进程入口点只有在有意桥接同步代码时才使用 awaiter 形式。

只在代码知道如何处理时捕获异常。若调用方能够处理，就把文档规定的远程拒绝转换成类型化错误。保留未知的基础设施异常、内部原因与堆栈。不要把取消变成 `Error "failed"`。判断 `OperationCanceledException` 是否来自本操作时，还要检查令牌和 API 规则。

## 清理是异步完成的一部分 {#cleanup}

第 21 章对 `IDisposable` 使用了 `use`。同一规则也适用于任务：一旦成功获取资源，主体无论成功、故障还是观察到取消，都必须经过清理。

有些资源可以同步释放。另一些实现 `IAsyncDisposable`；它们的 `DisposeAsync()` 返回 `ValueTask`，因为刷新或关闭本身可能需要异步 I/O。外层任务必须等到清理完成后才能报告完成。

测试对象会记录两种释放过程：

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
在编译后的 `.fs` 文件中，任务表达式可以用 `use` 绑定 `IAsyncDisposable`，任务构建器会等待 `DisposeAsync`。`use!` 会先等待获取，再负责释放所得资源。任务表达式中的 `with` 与 `finally` 处理程序是同步的，因此应使用资源绑定，不要把异步清理放进 `finally`。

### FSI 的已知限制 {#fsi-async-disposal}

F# 10 在编译项目中支持 task `use` 与 `IAsyncDisposable`。F# Interactive 仍有一个开放的编译器问题：`.fsx` 文件中的相同绑定可能错误地要求 `IDisposable`。

由于本章示例需要作为 FSI 脚本运行，异步测试对象使用一个小型 `usingAsync` 适配器。它保存主体结果，只等待一次释放，再返回值或通过 `ExceptionDispatchInfo` 重抛原始故障。它只用于展示生命周期行为，不应在常规编译代码中替代内置 `use`。

### 证明全部六条清理路径 {#all-cleanup-paths}

首先测试同步清理：

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
释放日志依次是 `success`、`failure`、`cancel`。预先取消的令牌只在资源获取后检查，说明取消仍会从管理该资源的作用域退出。

异步测试启动三个操作，分别对应三种主体结果。每个 `DisposeAsync` 都会报告已经进入，并等待各自的释放信号：

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
释放开始后，三个外层任务都保持未完成。只有相应信号触发后，每个任务才返回成功、原始故障或取消。这证明外层任务会等待释放完成，而不只是调用释放方法。

如果主体故障正在传播时清理也失败，就要决定如何在诊断信息中保留两者。语言内置的清理机制可能只暴露清理异常，从而遮蔽第一个异常。在基础设施接入处，应按既定策略记录或聚合两者，绝不能静默丢弃释放失败。

## 取消辅助对象也有生命周期 {#helper-lifetimes}

取消机制本身也有生命周期：

- 当不应继续监听时，释放每个 `CancellationTokenRegistration`；
- 释放每个 `CancellationTokenSource`，包括链接源和超时源；
- 操作仍依赖令牌源回调时，不要释放该源；
- 不要返回 `use` 作用域已经结束的资源；
- 获取本身为异步时，只有成功获取后才开始承担释放责任。

取消通常应跳过尚未开始的可选工作，但不得跳过已获取资源的清理。如果把已取消令牌传给清理 API 可能导致必要释放无法完成，就不要这样做；能否取消清理取决于资源的文档说明。

## 异步 API 检查表 {#checklist}

对每个异步 API 检查：

1. 如果可取消操作的生命周期由调用方控制，公共签名是否接受令牌？
2. 该令牌是否传给每个相关依赖的正确重载？
3. 超时是取消工作还是只放弃等待？之后由谁负责仍在运行的工作？
4. 能否用 `Result` 表示预期失败，同时不隐藏故障？
5. 谁观察比当前调用方活得更久的任务？
6. 获取哪些资源？释放是同步还是异步？
7. 成功、故障、取消与清理失败是否都有测试？
8. 测试由信号或可控时间驱动，还是在猜测需要等待多久？

这份检查表比通用辅助函数更有用。数据库事务、共享刷新、支付请求与 UI 预览的提交点和生命周期规则各不相同。

## 练习 {#exercises}

### 练习 1：找到断裂的令牌链 {#exercise-01}

基于两个都接受 `CancellationToken` 的接口函数 `charge` 和 `notify` 实现 `confirmBooking`。编写可记录调用的替身，确认同一个调用方令牌传给了两者。然后在其中一个调用中改用 `CancellationToken.None`，让测试准确指出传播链断裂。

说明取消检查相对于不可逆扣款和可选通知应当放在哪里。

### 练习 2：实现两种超时策略 {#exercise-02}

给定受控底层任务，使用超时信号实现 `abandonAfter`，使用操作令牌实现 `cancelAfter`。证明前者让底层工作保持挂起，后者使协作式操作取消。

为超时与调用方取消返回不同类型化结果。测试中不要使用时长。

### 练习 3：审计异步清理 {#exercise-03}

在编译的任务表达式中使用实现 `IAsyncDisposable` 的测试对象和 `use`。让释放等待信号。证明外层任务在成功、故障与取消后的清理期间保持挂起。

然后让释放发生故障。记录调用方收到哪个异常，并提出同时保留主体故障与清理故障的诊断策略。

[阅读本章练习答案](../solutions/ch-23-cancellation-timeouts)。

下一章会区分并发与并行，并比较不可变协调、代理、锁、原子操作和有意受控的可变性。

## 资料来源 {#sources}

- [Microsoft Learn：F# 任务表达式](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/task-expressions)
- [Microsoft Learn：托管线程中的取消](https://learn.microsoft.com/en-us/dotnet/standard/threading/cancellation-in-managed-threads)
- [Microsoft Learn：任务取消](https://learn.microsoft.com/en-us/dotnet/standard/parallel-programming/task-cancellation)
- [Microsoft Learn：`Task<TResult>.WaitAsync`](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task-1.waitasync?view=net-10.0)
- [Microsoft Learn：`IAsyncDisposable`](https://learn.microsoft.com/en-us/dotnet/api/system.iasyncdisposable?view=net-10.0)
- [dotnet/fsharp 问题 #14454：FSI 的 `IAsyncDisposable` `use` 限制](https://github.com/dotnet/fsharp/issues/14454)
