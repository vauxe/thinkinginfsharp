---
title: "第 22 章：Async<'T> 与 Task<'T>"
description: "为稍后完成的工作建模，比较 F# async 与 task 表达式的启动方式，并在不阻塞的情况下接入 .NET API。"
translationKey: part-04/ch-22-async-task
---

# 第 22 章：`Async<'T>` 与 `Task<'T>` {#overview}

预约服务向远程价格提供方请求报价。答案会稍后到达，但调用线程不应一直阻塞。F# 可以用两种值表示这个最终结果：异步计算 `Async<Quote>` 或 .NET 任务 `Task<Quote>`。两者的结果类型相似，启动方式和周边 API 却不同。

先确定谁创建工作、何时启动、由谁等待并处理结果，再讨论语法。明确这些责任后，`async {}` 和 `task {}` 才是用途不同的两种工具，而不是可以互换的写法。

## 稍后完成的操作不只由结果类型决定 {#three-questions}

面对任何稍后完成的操作，都要问三个问题：

1. **描述：** 这个值描述了可重复启动的工作，还是代表某一次执行？
2. **启动：** 构造会推迟工作，还是求值表达式时已经启动？
3. **观察：** 谁等待并处理成功、失败或取消？

`Async<'T>` 和 `Task<'T>` 都携带最终的 `'T`，但对前两个问题的回答不同：

| 普通 F# 代码产生的值 | 返回点上的含义 | 启动动作 |
|---|---|---|
| `async { ... } : Async<'T>` | 一项计算的描述 | 调用方使用 `Async` 启动函数，或由另一工作流启动 |
| 已求值的 `task { ... } : Task<'T>` | 这次任务表达式执行的句柄 | 求值立即启动 |

这张表有意限定了范围。.NET `Task` 还可以通过底层 API 创建，包括构造后尚未调度的任务。这里的结论针对 F# **任务表达式**：求值会立即启动它，并在当前线程运行到第一个尚未完成的异步操作。

两种表示都不承诺新线程。异步意味着工作挂起时调用方不必阻塞。并发与并行是第 24 章讨论的调度选择。

## `async {}` 构建工作但不启动 {#async-start}

示例创建两个控制信号：`asyncEntered` 记录是否进入主体，`asyncRelease` 阻止主体完成。测试不使用计时器，也不假设调度器速度。

```fsharp:line-numbers
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
构造 `deferredAsync` 不会设置 `asyncEntered`，所以第一个断言检查的是确定事实，而非竞争结果。`Async.StartAsTask deferredAsync` 会启动计算，并返回代表这次执行的任务。等待进入信号可以确认主体已经开始；释放信号尚未触发，因此任务仍未完成。

同一个 `Async<'T>` 值可以多次启动。每次启动都会产生一次新执行，并重复其中的副作用。这种可重复性利于组合，却不等于自动缓存结果。如果信用卡只能扣款一次，就要先规定谁可以启动工作，或让操作具备幂等性，再暴露可重复启动的值。

常见启动函数的行为不同：

| 操作 | 结果 | 调用方行为 |
|---|---|---|
| `Async.StartAsTask work` | `Task<'T>` | 启动工作，并把可观察句柄交给调用方 |
| `Async.RunSynchronously work` | `'T` | 启动并阻塞当前调用方，直到完成 |
| 对 `Async<unit>` 使用 `Async.Start work` | `unit` | 启动但不返回完成句柄 |

优先保留完成句柄，或让上层工作流等待该操作。即发即弃的 `Async.Start` 容易漏掉异常，也可能让工作超出调用方的生命周期。`Async.RunSynchronously` 适合脚本或程序入口这类有意阻塞的位置；不要把它散布在服务请求路径或 UI 处理程序中。

## `task {}` 在表达式求值时启动 {#task-start}

后半段使用相同的测试方法：

```fsharp:line-numbers
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
调用 `immediateTask ()` 会求值 `task {}` 表达式。其主体同步设置 `taskEntered`，到达尚未完成的 `taskRelease.Task`，然后返回未完成的 `Task<string>`。因此，调用之后立即检查的两个断言都成立：

- 已进入主体；
- 整体操作尚未完成。

如果主体没有遇到未完成的等待，它可能在工厂返回前就完成。“异步”不等于“总在以后完成”，`task { return 42 }` 完全可以产生已经完成的任务。

如果每次调用都应创建一次执行，就把任务表达式放进函数：

```fsharp
let fetchQuote requestId =
    task {
        // 调用 fetchQuote 会启动这次执行。
        return $"quote:{requestId}"
    }
```

模块级绑定 `let quoteTask = fetchQuote "R-22"` 会保存模块初始化时启动的那个任务。之后的调用方共享同一次执行及其结果。应根据需求选择工厂函数或共享任务。

## 计算表达式决定操作顺序 {#workflow-syntax}

在任一种计算表达式中：

- `let name = expression` 现在求值普通表达式；
- `let! name = computation` 异步等待，并绑定其成功结果；
- `do! computation` 异步等待有用结果为 `unit` 的操作；
- `return value` 提供工作流结果；
- `return! computation` 把工作流结果委托给另一项计算。

例如，围绕任务式 .NET API 的代码可以始终使用任务：

```fsharp
let quoteAndReserve fetchQuote reserve request =
    task {
        let! quote = fetchQuote request
        do! reserve quote
        return quote.Id
    }
```

第二项操作只会在第一项产生 `quote` 后启动，因此这段代码是顺序执行的。从上到下的写法不会自动让独立调用并发。只在需求允许时引入并发，同时考虑速率限制、部分失败与取消。

用 `let!` 等待时，会按照工作流和被等待操作的规则交还控制权。读取 `.Result`、调用 `.Wait()` 或 `GetAwaiter().GetResult()` 都会阻塞当前线程。控制台测试可以只在最外层使用最后一种形式，让进程等待断言完成；应用工作流应继续使用 `let!`。

## 只在两种模型的交界处转换一次 {#interop}

平台提供大量 `Task<'T>` API，而已有 F# 库和代码库可能提供 `Async<'T>`。两者可以明确转换：

```fsharp:line-numbers
let taskFromAsync = async { return 21 } |> Async.StartAsTask

let asyncFromTask = task { return 42 } |> Async.AwaitTask

let fromAsync = taskFromAsync.GetAwaiter().GetResult()
let fromTask = Async.RunSynchronously asyncFromTask

assert (fromAsync = 21)
assert (fromTask = 42)
printfn "Interop: async-to-task=%d task-to-async=%d" fromAsync fromTask
```
`Async.StartAsTask` 会同时启动异步计算并返回任务。`Async.AwaitTask` 返回一项异步计算；当这项异步计算启动时，它会等待给定任务。它不会倒回或延迟已经运行的任务。

F# 任务表达式也可以直接用 `let!` 绑定 `Async<'T>`。选择能让外层工作流保持一致的形式。转换处的异常与取消行为会影响调用方，并不只是类型变化；第 23 章会直接测试这些行为。

在两类 API 的交界处，可以采用这条规则：

```text
外部 Task API → 必要时适配一次 → 一种内部工作流风格
                                 → 在公共 API 处适配一次
```

反复进行 `Async` → `Task` → `Async` 转换，会掩盖哪个调用启动工作、哪个取消策略生效。

## 根据周边 API 选择 {#choice}

不存在通用赢家：

| 场景 | 通常从这里开始 | 理由 |
|---|---|---|
| ASP.NET Core 或公共 .NET API | `Task<'T>` / `task {}` | 宿主和多数 .NET 库已经使用任务通信 |
| 以 `Async` 组合器为中心的 F# 代码 | `Async<'T>` / `async {}` | 延迟描述适合先组合、最后显式启动 |
| 现有依赖返回其中一种表示 | 沿用该表示 | 避免没有实际意义的转换 |
| 调用方必须决定是否启动工作 | `Async<'T>` 或工厂函数 | 构造可以与执行分离 |
| 应当共享一次执行 | 专门保存的 `Task<'T>` | 该值代表这次执行及其最终结果 |
| CPU 密集型计算 | 两者本身都不是答案 | 测量后选择显式调度或并行工具 |

新代码若大量使用任务式 .NET API，通常直接选择任务表达式。若设计依赖 `Async` 的延迟模型、组合器、异步尾调用或隐式取消令牌传递，`Async` 仍然很有价值。下一章会讨论取消差异，避免把这项选择简化成口号。

不要只为隐藏 `Task` 或 `Async` 而添加包装接口。如果测试或架构需要可替换的依赖，应抽象真正有意义的操作，例如 `QuoteRequest -> Task<Quote>`；返回值使用 `Task` 还是 `Async` 可以保持可见。

## 测试状态转换，而不是猜测等待时长 {#deterministic-testing}

测试若等待 20 毫秒后就认定工作已经开始，实际依赖的是机器负载与调度时机。控制信号可以直接表达因果顺序：

```text
构造/调用 → 观察进入信号 → 断言未完成 → 触发释放信号 → 观察结果
```

`TaskCompletionSource<'T>` 允许测试代码控制一个任务何时完成。示例使用 `RunContinuationsAsynchronously`，避免触发信号时在当前调用中直接执行后续代码。这一选项不会改变被测的启动规则。

每次执行都应使用一套新信号。若失败的断言可能使工作永远挂起，应在清理中触发所有信号。生产代码应等待真实 API，而不是暴露测试信号；这些信号只是结果可控的外部完成事件替身。

## 练习 {#exercises}

### 练习 1：预测并证明进入时机 {#exercise-01}

编写一个 `Async<int>` 和一个 `unit -> Task<int>`。两者都在等待注入信号前递增私有计数器。不要使用 sleep；分别检查构造或调用后、启动 async 后以及解除等待后的计数器值。

解释调用任务工厂两次与等待同一个返回任务两次为何不同。


::: details 参考答案

#### 观察两个启动边界 {#exercise-01-proof}

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

async 计数器在 `Async.StartAsTask` 之前保持为零。task 计数器则在 `makeTask` 返回前就在函数内部改变，因为执行同步到达了未完成的闩锁。

等待 `taskRun` 两次观察的是同一次执行及其缓存结果。调用 `makeTask ()` 两次会运行两次任务表达式，并重复入口副作用。同理，启动 `prepared` 两次会创建两次 async 执行；重新启动不是重放已保存输出。

可变计数器是测试探针，不是领域设计。每个计数器只由一次执行写入，写入发生在进入信号完成之前；断言则在观察该信号之后读取。

:::

### 练习 2：保持一种内部表示 {#exercise-02}

某个 .NET 客户端公开 `send: Request -> Task<Response>`，已有 F# 验证器公开 `validate: Response -> Async<Result<ValidResponse, Error>>`。

实现一个返回任务的公共工作流。在组合点适配，不要在内部阻塞，并准确指出网络工作何时启动。


::: details 参考答案

#### 让公共工作流保持任务风格 {#exercise-02-workflow}

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

公共契约和外围工作流使用 `Task`，与 .NET 客户端保持一致。F# 任务表达式可以直接绑定 `Async<'T>`，因此验证器不需要阻塞调用，也不需要中间的 `Async.StartAsTask`。

调用 `execute` 会求值它的任务表达式。随后它求值 `send request`；根据该客户端返回任务的契约，这会启动客户端网络操作。第一个 `let!` 等待其结果。如果客户端可能在返回任务之前同步抛出异常，这同样属于边界契约，并会使外围任务表达式出错。

生产代码中的公共函数还会接受并传播取消令牌。第 23 章会添加这项策略，而不会把它偷塞进当前的启动时机练习。

:::

### 练习 3：明确由谁启动 {#exercise-03}

审计这些 API：

```fsharp
val refresh : Task<Snapshot>
val refreshAgain : unit -> Task<Snapshot>
val prepareRefresh : unit -> Async<Snapshot>
```

逐一说明调用方是共享同一次执行、创建并立即启动新执行，还是创建延迟描述。为“可以重试但不得重叠”的刷新选择最安全的 API，并说明还需要哪条并发规则。


::: details 参考答案

#### 分类三个 API {#exercise-03-classification}

| API | 执行语义 |
|---|---|
| `refresh : Task<Snapshot>` | 任务已经创建；所有调用方取得或共享这次执行 |
| `refreshAgain : unit -> Task<Snapshot>` | 每次调用都求值工厂，通常会启动一次新执行 |
| `prepareRefresh : unit -> Async<Snapshot>` | 每次调用创建延迟描述；以后启动时才创建执行 |

这些签名本身没有规定重试、重叠、缓存期限、谁能取消，也没有规定已完成结果是否持续复用。这些都是独立策略。

#### 用协调器合并并发请求（single-flight） {#exercise-03-single-flight}

对于完成后可以再次刷新、但不得重叠的操作，可以把任务工厂放在小型协调器之后：

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

锁只保护进行中任务的选择与发布，不会在异步工作期间一直持有监视器。关闭的闩锁可以确认：工作尚未完成时，调用方会取得同一个任务。任务完成后，后来的调用方会启动新一次执行；测试闩锁此时已经打开，所以它会立即完成。

真实 API 仍须规定故障是否重试、成功快照是否缓存，以及某个调用方能否取消共享工作。最后一个问题尤其重要：取消某个调用方的等待，不一定代表有权取消共享操作。

:::


下一章会把这些启动模型带入取消、超时、故障传播，以及每种完成路径上的资源释放。

## 资料来源 {#sources}

- [Microsoft Learn：F# 中的 Async 与 Task 编程](https://learn.microsoft.com/en-us/dotnet/fsharp/tutorials/async)
- [Microsoft Learn：F# 异步表达式](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/async-expressions)
- [Microsoft Learn：F# 任务表达式](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/task-expressions)
- [FSharp.Core 参考：`Async`](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-control-fsharpasync.html)
- [Microsoft Learn：`TaskCompletionSource<TResult>`](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.taskcompletionsource-1?view=net-10.0)
