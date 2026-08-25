---
title: "第 23 章练习答案"
description: "验证令牌传播，用信号实现放弃等待与取消工作的超时策略，并测试编译代码中的异步释放。"
translationKey: solutions/ch-23-cancellation-timeouts
kind: solution
part: 4
chapter: 23
status: complete
verifiedWith:
  fsharp: "10"
  dotnetSdk: "10.0.301"
exampleIds:
  - ch23-cancellation-timeouts
exerciseIds:
  - ch23-exercise-01
  - ch23-exercise-02
  - ch23-exercise-03
termIds: []
sources:
  - id: microsoft-fsharp-task-expressions
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/task-expressions
    checked: "2026-08-24"
  - id: dotnet-cooperative-cancellation
    url: https://learn.microsoft.com/en-us/dotnet/standard/threading/cancellation-in-managed-threads
    checked: "2026-08-24"
  - id: dotnet-iasyncdisposable
    url: https://learn.microsoft.com/en-us/dotnet/api/system.iasyncdisposable?view=net-10.0
    checked: "2026-08-24"
---

# 第 23 章练习答案 {#overview}

这些答案检验所有权决策，而不依赖等待多久。记录的令牌证明传播；显式任务表示调用方和截止信号；异步可释放闩锁证明清理确实被等待。

[返回第 23 章](../part-04/ch-23-cancellation-timeouts)。

## 练习 1：找到断裂的令牌链 {#exercise-01}

### 记录精确令牌 {#exercise-01-recording}

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

上面的 `.Result` 只是紧凑的同步测试边界。在 `confirmBooking` 内部，两个调用都被异步等待，并收到调用方的精确令牌。

错误版本会把 `CancellationToken.None` 传给 `Notify`。记录测试应断言两个条目都等于 `owner.Token`；第二个条目就会使断言失败。即使替身完成得太快、取消行为无法暴露问题，测试令牌身份仍能发现断开的传播链。

### 围绕提交点放置检查 {#exercise-01-commit}

如果请求已经放弃就不应开始扣款，应在扣款前检查取消；若支付 API 定义了安全取消，也应传播令牌。提供方一旦确认不可逆扣款，返回整体已取消结果可能会隐藏已经提交的效果。

生产工作流应先持久化收据或已提交状态，再处理可选通知。通知可以有自己的重试或取消策略，返回模型也可以区分 `ConfirmedButNotificationPending`。这个简单函数证明令牌接线，并不是完整的支付一致性协议。

## 练习 2：实现两种超时策略 {#exercise-02}

### 分离等待结果 {#exercise-02-outcomes}

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

测试提供不同且可控的截止信号和调用方信号。生产适配器可以由 `CancellationTokenRegistration` 完成后者，由 `TimeProvider` 完成前者；决策逻辑保持不变。

### 只放弃这一次等待 {#exercise-02-abandon}

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

超时结束的是这次观察，而不是操作。必须有另一个所有者保留并观察 `operation.Task`。

### 请求停止拥有的工作 {#exercise-02-cancel}

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

胜出信号会在调用 `operationSource.Cancel()` 前分类，所以类型化原因保持确定。辅助函数会等待协作式操作确认取消后再返回，因此清理位于所拥有的生命周期内。

真实代码中，应把操作源链接到实际调用方令牌，或显式注册它。应决定调用方取消是否应表现为任务取消，而不是类型化 `Error`；两种契约都可能有效，但不要不可预测地混用。

## 练习 3：审计异步清理 {#exercise-03}

### 使用编译的任务绑定 {#exercise-03-compiled}

由于本章所述 FSI 限制，请把以下代码放在编译的 `.fs` 文件中：

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

每个用例都创建带 `RunContinuationsAsynchronously` 的全新 `TaskCompletionSource<unit>`。启动 `run`，在测试边界等待 `started.Task`，并断言外围任务未完成。释放清理，然后分别断言：

- 结果等于 `"ok"`；
- 等待抛出 `InvalidDataException("body-fault")`；
- 等待抛出 `OperationCanceledException`，且任务已取消。

这就是共享 FSI 适配器所模拟的编译语言形式。

### 让清理失败可见 {#exercise-03-cleanup-fault}

修改 `DisposeAsync`，让它在闩锁之后抛出 `IOException("dispose-fault")`。主体成功时，调用方会观察到清理故障。主体已经故障时，清理机制通常会让清理故障成为可见异常；请验证你实际交付的构建器与运行时版本的精确行为。

如果两个原因对运维都重要，就在能保留两者的边界捕获：清理前记录主体失败，然后把它与清理失败一起记录或聚合。不要盲目重试释放，也不要只返回一条消息字符串。资源契约决定重复释放是否安全。

## 答案复盘 {#solution-review}

- 记录精确令牌无需时间竞争即可测试传播。
- 取消位置必须尊重不可逆提交点。
- 截止信号与调用方信号在请求停止工作前保持不同。
- 放弃等待要求另有所有者负责继续运行的工作及其最终故障。
- 取消工作会等待协作式确认与清理。
- 编译的 task `use` 会在每种主体退出路径上等待 `IAsyncDisposable.DisposeAsync`。
- 已有另一个失败时，清理失败需要显式诊断策略。
