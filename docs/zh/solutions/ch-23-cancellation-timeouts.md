---
title: "第 23 章练习答案"
description: "验证令牌传播，用信号实现放弃等待与取消工作的超时策略，并测试编译代码中的异步释放。"
translationKey: solutions/ch-23-cancellation-timeouts
---

# 第 23 章练习答案 {#overview}

这些答案检验取消责任，而不依赖等待时长。记录令牌可以验证传播，受控任务分别表示调用方取消和截止信号，异步可释放闩锁则确认程序确实等待清理。

[返回第 23 章](../part-04/ch-23-cancellation-timeouts)。

## 练习 1：找到断裂的令牌链 {#exercise-01}

### 记录实际传入的令牌 {#exercise-01-recording}

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

上面的 `.Result` 只是紧凑的同步测试边界。在 `confirmBooking` 内部，两个调用都被异步等待，并收到调用方传入的同一个令牌。

错误版本会把 `CancellationToken.None` 传给 `Notify`。记录测试应断言两个条目都等于 `owner.Token`；第二个条目就会使断言失败。即使替身完成得太快、取消行为无法暴露问题，测试令牌身份仍能发现断开的传播链。

### 围绕提交点放置检查 {#exercise-01-commit}

若请求已放弃，就不应开始扣款，因此要在扣款前检查取消；若支付 API 支持安全取消，也应继续传递令牌。提供方一旦确认不可逆扣款，再返回整体“已取消”会隐藏已经提交的副作用。

生产工作流应先持久化收据或已提交状态，再处理可选通知。通知可以有自己的重试或取消策略，返回模型也可以区分 `ConfirmedButNotificationPending`。这个简单函数只验证令牌传递，不是完整的支付一致性协议。

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

超时只结束这次等待，不会停止操作。必须由另一个组件保留并观察 `operation.Task`。

### 请求取消该操作 {#exercise-02-cancel}

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

程序会先判断哪个信号胜出，再调用 `operationSource.Cancel()`，因此返回原因保持确定。辅助函数会等待协作式操作确认取消后再返回，所以清理仍发生在该函数管理的生命周期内。

真实代码中，应把操作源链接到实际调用方令牌，或主动注册回调。还要决定调用方取消应表现为任务取消，还是类型化 `Error`。两种契约都可能有效，但不能混用得不可预测。

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

修改 `DisposeAsync`，让它在闩锁之后抛出 `IOException("dispose-fault")`。主体成功时，调用方会观察到清理故障。主体已经故障时，清理机制通常会让清理故障成为可见异常；请用实际交付的构建器与运行时版本验证具体行为。

如果两个原因对运维都重要，就在能保留两者的边界捕获：清理前记录主体失败，然后把它与清理失败一起记录或聚合。不要盲目重试释放，也不要只返回一条消息字符串。资源契约决定重复释放是否安全。

## 答案复盘 {#solution-review}

- 记录实际令牌，无需制造时间竞争即可测试传播。
- 取消位置必须尊重不可逆提交点。
- 截止信号与调用方信号在请求停止工作前保持不同。
- 放弃等待后，必须由另一个组件观察继续运行的工作及其最终故障。
- 取消工作会等待协作式确认与清理。
- 编译的 task `use` 会在每种主体退出路径上等待 `IAsyncDisposable.DisposeAsync`。
- 已有另一个失败时，清理失败需要明确的诊断策略。
