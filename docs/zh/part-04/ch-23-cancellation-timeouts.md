---
title: "第 23 章：取消、超时、故障与释放"
description: "传播协作式取消，区分停止工作与放弃等待，保留故障，并在每条异步退出路径上释放资源。"
translationKey: part-04/ch-23-cancellation-timeouts
---

# 第 23 章：取消、超时、故障与释放 {#overview}

异步预约操作的结束方式不只是“值已到达”。调用方可能请求取消，等待预算可能耗尽，操作可能发生故障，而资源可能必须在任何结果可观察之前完成清理。混淆这些路径会造成句柄泄漏、误导性的错误，以及所有者已经离开后仍在继续的工作。

本章把完成视为一项协议。取消是协作式通信，超时是一项策略，故障不等于取消，而清理也是完成的一部分。所有论断都用显式信号测试；没有测试要求调度器与 `sleep` 竞速并获胜。

## 学完本章后你将能够 {#outcomes}

学完本章后，你应当能够：

- 在基于任务的端口中接受并传播 `CancellationToken`；
- 解释请求取消为何不会强行终止工作；
- 区分取消操作与只取消某个调用方的等待；
- 决定超时应停止工作，还是仅停止等待；
- 区分超时、调用方取消、预期拒绝与故障；
- 观察任务故障而不意外得到 `AggregateException` 包装；
- 释放 `CancellationTokenSource` 和令牌注册；
- 使用同步 `IDisposable` 和异步 `IAsyncDisposable` 清理；
- 证明清理会在成功、故障和取消路径上完成；
- 用完成源或可控时间源测试全部路径。

## 取消是由令牌携带的请求 {#cooperative-cancellation}

.NET 取消模型分开了几种角色：

- `CancellationTokenSource` 拥有请求取消的权利；
- `CancellationToken` 是传给监听者的轻量值；
- 每项操作决定在哪里可以安全地观察请求；
- `OperationCanceledException` 可以报告已经观察到协作式取消。

令牌不会杀死线程，也不能回滚已经可见的效果。支付一旦接受或文件一旦替换，取消就不能假装效果从未发生。操作应当在不可逆工作前检查，把令牌传入支持取消的 API，并规定请求在提交点之后到达时会发生什么。

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

在中途使用 `CancellationToken.None` 会悄悄切断传播链。只有被调用操作有意独立于调用方生命周期时，这样做才合适。

共享示例注册一个回调，用相同令牌把受控任务完成为已取消：

```fsharp:line-numbers [ch23-cancellation-timeouts.fsx]
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
任务在 `Cancel` 之前处于挂起状态。请求之后，等待它会抛出 `OperationCanceledException`，异常携带预期令牌，且任务的 `IsCanceled = true`。注册由 `use` 持有，所以任务离开作用域时会移除它。操作被观察后，令牌源由其所有者释放。

取消令牌是单向的：一旦请求，令牌就会一直保持取消状态。为逻辑上的新操作创建新令牌源；不要试图重置并复用旧请求。

## 取消操作还是放弃等待？ {#operation-versus-wait}

两种策略常常都被称为“取消调用”：

| 策略 | 令牌是否到达底层操作？ | 什么会停止？ |
|---|---|---|
| 取消操作 | 是 | 协作式操作观察请求并清理后停止 |
| 放弃等待 | 否；令牌控制包装等待 | 这个调用方停止等待；底层工作可能继续 |

`Task<'T>.WaitAsync(cancellationToken)` 展示第二种策略。它返回另一个任务，在原任务完成或等待令牌取消时完成。取消该令牌不会修改原任务：

```fsharp:line-numbers [ch23-cancellation-timeouts.fsx]
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
测试取消等待者，证明底层操作仍在挂起，然后完成底层任务并观察其结果。每次状态转换都由显式调用引发，所以证明与机器速度无关。

工作另有所有者——例如共享缓存刷新——或不能安全中断时，放弃等待很有用。若已无人负责观察故障、限制资源用量或阻止后续重复效果，它就很危险。应当明确写出该所有者。

## 超时是策略，不是取消的同义词 {#timeout-policy}

超时回答“这个调用方会等多久？”它本身并不回答“操作是否应停止？”常见策略如下：

| 需求 | 机制 | 超时后的底层工作 |
|---|---|---|
| 停止调用方拥有的协作式操作 | 链接的 `CancellationTokenSource`、截止时间/`CancelAfter`，并把令牌传给操作 | 收到停止请求；观察并清理可能需要时间 |
| 停止等待由他人拥有的工作 | `WaitAsync(timeout)` | 继续，除非其所有者另行取消 |
| 区分超时与调用方取消 | 分离的截止信号，或检查哪个源发出请求 | 策略可以报告 `TimedOut` 或取消 |

`WaitAsync(TimeSpan)` 会让其包装任务以 `TimeoutException` 发生故障；它不会取消源任务。现代 .NET 中接受 `TimeProvider` 的重载使测试可以控制时间。如果超时应请求取消操作，就创建并释放链接令牌源，为它安排截止时间，并把其令牌传入操作。

共享测试完全移除了墙上时钟。注入的任务表示“截止时间已到”：

```fsharp:line-numbers [ch23-cancellation-timeouts.fsx]
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

超时并不能证明远端没有做任何事。在重试超时的写操作之前，应当定义幂等或对账策略。第 37 章会回到这个分布式边界。

## 故障、取消与预期错误是不同结果 {#faults}

任务的终止状态包括成功完成、故障与取消。“容量不足”这样的领域拒绝通常是值为 `Error CapacityExceeded` 的成功任务，因为异步机制正常工作并产生了预期业务答案。

在 `task {}` 中抛出的意外异常会使返回任务发生故障。用 `let!` 等待，或只在顶层测试边界使用 `GetAwaiter().GetResult()`，会暴露原始异常：

```fsharp:line-numbers [ch23-cancellation-timeouts.fsx]
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

只在存在策略的地方捕获。若调用方能处理，就把有文档保证的远程拒绝转换为类型化错误。保留未知基础设施异常、内部原因和堆栈跟踪。不要把取消变成 `Error "failed"`；在没有考虑令牌和契约时，也不要把任意 `OperationCanceledException` 都分类为这项操作的取消。

## 清理是异步完成的一部分 {#cleanup}

第 21 章对 `IDisposable` 使用了 `use`。同一规则也适用于任务：一旦成功获取资源，主体无论成功、故障还是观察到取消，都必须经过清理。

有些资源可以同步释放。另一些实现 `IAsyncDisposable`；它们的 `DisposeAsync()` 返回 `ValueTask`，因为刷新或关闭本身可能需要异步 I/O。外围任务必须等到清理完成后才能报告完成。

探针让两种协议都可观察：

```fsharp:line-numbers [ch23-cancellation-timeouts.fsx]
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
在编译的 `.fs` 文件中，任务表达式可以用 `use` 绑定 `IAsyncDisposable`；任务构建器会等待 `DisposeAsync`。`use!` 会先等待获取，再拥有获取到的资源。任务表达式的 `with` 和 `finally` 处理程序是同步的，所以应使用资源绑定，而不是在 `finally` 中放异步清理。

### 诚实面对 FSI 边界 {#fsi-async-disposal}

F# 10 在编译项目中支持 task `use` 与 `IAsyncDisposable`。F# Interactive 仍有一个开放的编译器问题：`.fsx` 文件中的相同绑定可能错误地要求 `IDisposable`。

由于本章登记的产物是 FSI 脚本，其异步探针使用一个小型 `usingAsync` 适配器。适配器捕获主体结果，只等待一次释放，然后返回值或通过 `ExceptionDispatchInfo` 重抛原始故障。这是生命周期行为的可执行证据，不是在普通编译代码中替换内置 `use` 的建议。

### 证明全部六条清理路径 {#all-cleanup-paths}

首先测试同步清理：

```fsharp:line-numbers [ch23-cancellation-timeouts.fsx]
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
精确释放日志是 `success`、`failure`、`cancel`。预先取消的令牌只在资源获取后检查，这证明取消仍会经过已拥有的作用域。

异步测试启动三个操作，分别对应三种主体结果。每个 `DisposeAsync` 都会报告已经进入，并等待独立释放的闩锁：

```fsharp:line-numbers [ch23-cancellation-timeouts.fsx]
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
释放开始后，三个外围任务都保持未完成。只有相应闩锁释放后，每个任务才暴露成功、原始故障或取消。这证明释放是被等待，而不是仅被调用。

如果主体故障正在传播时清理本身也失败，就要决定诊断如何保留两者。普通语言清理可能暴露清理异常而遮蔽第一个异常。在基础设施边界，应按显式策略记录或聚合；绝不能静默丢弃释放失败。

## 辅助资源也需要所有者 {#helper-lifetimes}

取消机制本身也有生命周期：

- 当不应继续监听时，释放每个 `CancellationTokenRegistration`；
- 释放每个 `CancellationTokenSource`，包括链接源和超时源；
- 操作仍依赖令牌源回调时，不要释放该源；
- 不要返回 `use` 作用域已经结束的资源；
- 获取本身为异步时，只有成功获取后所有权才开始。

取消通常应绕过新的可选工作，但不得绕过已经获取工作的清理。如果把已取消令牌传给清理 API 可能使必要释放无法完成，就不要这样做；清理是否可以取消取决于资源契约。

## 边界检查表 {#checklist}

对每个异步端口检查：

1. 如果操作由调用方拥有且可取消，公共签名是否接受令牌？
2. 该令牌是否传给每个相关依赖的正确重载？
3. 超时策略是取消工作还是放弃等待？谁拥有继续运行的工作？
4. 能否用 `Result` 表示预期失败，同时不隐藏故障？
5. 谁观察比当前调用方活得更久的任务？
6. 获取哪些资源？释放是同步还是异步？
7. 成功、故障、取消与清理失败是否都有测试？
8. 测试由信号或可控时间驱动，还是在猜测需要等待多久？

这份检查表比通用辅助函数更有用。数据库事务、共享刷新、支付请求与 UI 预览具有不同的提交和所有权规则。

## 运行共享示例 {#run-example}

在示例所在目录运行：

```console
dotnet fsi --checknulls+ --exec ch23-cancellation-timeouts.fsx
```

七行确定性输出证明操作取消、放弃等待、受控超时、原始故障传播，以及全部主体结果上的同步与异步清理。

## 练习 {#exercises}

### 练习 1：找到断裂的令牌链 {#exercise-01}

基于两个都接受 `CancellationToken` 的端口 `charge` 和 `notify` 实现 `confirmBooking`。编写记录型替身，证明精确的调用方令牌到达两者。然后在一个调用中引入 `CancellationToken.None`，让测试因正确原因失败。

说明取消检查相对于不可逆扣款和可选通知应当放在哪里。

### 练习 2：实现两种超时策略 {#exercise-02}

给定受控底层任务，使用超时信号实现 `abandonAfter`，使用操作令牌实现 `cancelAfter`。证明前者让底层工作保持挂起，后者使协作式操作取消。

为超时与调用方取消返回不同类型化结果。测试中不要使用时长。

### 练习 3：审计异步清理 {#exercise-03}

在编译的任务表达式中使用实现 `IAsyncDisposable` 的探针和 `use`。让释放等待闩锁。证明外围任务在成功、故障与取消后的清理期间保持挂起。

然后让释放发生故障。记录调用方收到哪个异常，并提出同时保留主体故障与清理故障的诊断策略。

[阅读本章练习答案](../solutions/ch-23-cancellation-timeouts)。

## 模型复盘 {#model-review}

- 取消是协作式请求；操作必须收到并观察其令牌。
- 取消 `WaitAsync` 可以停止一次等待而不停止底层任务。
- 超时必须规定工作是停止、在另一所有者下继续，还是需要对账。
- 预期领域错误、故障任务与取消任务是不同契约。
- 在异步工作流中应等待任务故障，而不是使用阻塞包装器。
- 一旦获取资源，成功、故障与取消都必须经过清理。
- 异步释放完成之前，外围任务尚未完成。
- 信号与可控时间无需猜测调度器即可证明状态转换。

下一章会区分并发与并行，并比较不可变协调、代理、锁、原子操作和有意受控的可变性。

## 资料来源 {#sources}

- [Microsoft Learn：F# 任务表达式](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/task-expressions)
- [Microsoft Learn：托管线程中的取消](https://learn.microsoft.com/en-us/dotnet/standard/threading/cancellation-in-managed-threads)
- [Microsoft Learn：任务取消](https://learn.microsoft.com/en-us/dotnet/standard/parallel-programming/task-cancellation)
- [Microsoft Learn：`Task<TResult>.WaitAsync`](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task-1.waitasync?view=net-10.0)
- [Microsoft Learn：`IAsyncDisposable`](https://learn.microsoft.com/en-us/dotnet/api/system.iasyncdisposable?view=net-10.0)
- [dotnet/fsharp 问题 #14454：FSI 的 `IAsyncDisposable` `use` 限制](https://github.com/dotnet/fsharp/issues/14454)
