---
title: "第 22 章练习答案"
description: "用门闩证明 async 与 task 的启动语义，组合 Task API 与 Async 验证器，并明确单次执行所有权。"
translationKey: solutions/ch-22-async-task
kind: solution
part: 4
chapter: 22
status: complete
verifiedWith:
  fsharp: "10"
  dotnetSdk: "10.0.301"
exampleIds:
  - ch22-async-task
exerciseIds:
  - ch22-exercise-01
  - ch22-exercise-02
  - ch22-exercise-03
termIds: []
sources:
  - id: microsoft-fsharp-async-task
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/tutorials/async
    checked: "2026-08-24"
  - id: microsoft-fsharp-task-expressions
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/task-expressions
    checked: "2026-08-24"
  - id: fsharp-core-async
    url: https://fsharp.github.io/fsharp-core-docs/reference/fsharp-control-fsharpasync.html
    checked: "2026-08-24"
---

# 第 22 章练习答案 {#overview}

这些答案让因果顺序可观察。测试通过门闩控制完成，从不假设调度器会在任意指定的毫秒数内行动。

[返回第 22 章](../part-04/ch-22-async-task)。

## 练习 1：预测并证明进入时机 {#exercise-01}

### 观察两个启动边界 {#exercise-01-proof}

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

async 计数器在 `Async.StartAsTask` 之前保持为零。task 计数器则在 `makeTask` 返回前就在函数内部改变，因为执行同步到达了未完成的门闩。

等待 `taskRun` 两次观察的是同一次执行及其缓存的完成结果。调用 `makeTask ()` 两次会创建两次任务表达式执行，并重复进入效果。同理，启动 `prepared` 两次会创建两次 async 执行；重启不是重放已存储的输出。

可变计数器是测试探针，不是领域设计。每个计数器只由一次执行写入，写入发生在进入信号完成之前；断言则在观察该信号之后读取。

## 练习 2：保持一种内部表示 {#exercise-02}

### 让公共工作流保持任务风格 {#exercise-02-workflow}

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

## 练习 3：明确启动所有权 {#exercise-03}

### 分类三个 API {#exercise-03-classification}

| API | 所有权含义 |
|---|---|
| `refresh : Task<Snapshot>` | 已经创建一个任务；所有调用方取得或共享这次执行 |
| `refreshAgain : unit -> Task<Snapshot>` | 每次调用都求值工厂，通常会启动一次新执行 |
| `prepareRefresh : unit -> Async<Snapshot>` | 每次调用创建延迟描述；以后启动时才创建执行 |

这些签名本身没有规定重试、重叠、缓存期限、取消所有权，也没有规定已完成的值是否持续复用。这些是独立策略。

### 添加显式单航班所有者 {#exercise-03-single-flight}

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

锁只保护飞行中任务的选择与发布；它不会在异步工作期间持有监视器。关闭的门闩证明工作尚未完成时调用方会取得同一个任务。完成之后，后来的调用方会启动一次重试；测试门闩此时已经打开，所以第二次执行会立即完成。

真实 API 仍须规定故障是否重试、成功快照是否缓存，以及某个调用方能否取消共享工作。最后一个问题尤其重要：取消某个调用方的等待，不一定代表有权取消共享操作。

## 答案复盘 {#solution-review}

- 信号建立 happens-before 关系，无需猜测时间。
- `Async.StartAsTask` 同时是启动边界和适配边界。
- 调用任务工厂会启动一次新的任务表达式执行；等待已存储任务不会重启它。
- 任务工作流可以直接绑定 F# 异步计算，同时保持公共形状为任务。
- 单航班行为需要显式共享状态策略；单靠返回类型不会提供它。
- 这些片段中的阻塞调用只属于顶层断言边界。
