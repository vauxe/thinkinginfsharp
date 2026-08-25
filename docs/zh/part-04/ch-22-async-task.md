---
title: "第 22 章：Async<'T> 与 Task<'T>"
description: "为稍后完成的工作建模，观察 F# async 与 task 表达式不同的启动语义，并在不阻塞的情况下跨越 .NET 边界。"
translationKey: part-04/ch-22-async-task
kind: chapter
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
termIds:
  - computation-expression
  - effect
sources:
  - id: microsoft-fsharp-async-task
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/tutorials/async
    checked: "2026-08-24"
  - id: microsoft-fsharp-async-expressions
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/async-expressions
    checked: "2026-08-24"
  - id: microsoft-fsharp-task-expressions
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/task-expressions
    checked: "2026-08-24"
  - id: fsharp-core-async
    url: https://fsharp.github.io/fsharp-core-docs/reference/fsharp-control-fsharpasync.html
    checked: "2026-08-24"
  - id: dotnet-task-completion-source
    url: https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.taskcompletionsource-1?view=net-10.0
    checked: "2026-08-24"
---

# 第 22 章：`Async<'T>` 与 `Task<'T>` {#overview}

预约服务向远程价格提供方请求报价。答案会稍后到达，但调用线程应当保持可用。两种 F# 值都能描述最终答案：F# 异步计算 `Async<Quote>`，或 .NET 任务 `Task<Quote>`。它们的结果类型看起来相似，启动语义和生态边界却不相同。

本章从所有权而不是语法开始。谁创建工作？工作何时启动？谁等待并观察结果？明确这些答案之后，`async {}` 和 `task {}` 就是两种精确工具，而不是可以互换的标点。

## 学完本章后你将能够 {#outcomes}

学完本章后，你应当能够：

- 把 `Async<'T>` 读作需要显式启动、可以组合的描述；
- 把已求值的 F# `task {}` 表达式结果读作已经启动的 .NET 任务；
- 避免“所有可能的 `Task` 天生都是热任务”这一不准确捷径；
- 用 `let!`、`do!`、`return` 和 `return!` 串联稍后产生的结果；
- 区分异步等待与 CPU 并行执行；
- 把 `Async<'T>` 转成 `Task<'T>`，并从 `Async<'T>` 等待任务；
- 在边界选择一种表示，避免来回转换；
- 只在有意的进程或测试边界进行阻塞等待；
- 用信号而不是猜测的延时证明启动时机；
- 让 API 明确呈现启动与结果的所有权。

## 稍后完成的操作不只由结果类型决定 {#three-questions}

面对任何稍后完成的操作，都要问三个问题：

1. **描述：** 这个值是可复用的描述，还是一次执行的句柄？
2. **启动：** 构造会推迟工作，还是求值表达式时已经启动？
3. **观察：** 哪个所有者等待并观察成功、失败或取消？

`Async<'T>` 和 `Task<'T>` 都携带最终的 `'T`，但对前两个问题的回答不同：

| 普通 F# 代码产生的值 | 返回点上的含义 | 启动动作 |
|---|---|---|
| `async { ... } : Async<'T>` | 一项计算的描述 | 调用方使用 `Async` 启动函数，或由另一工作流启动 |
| 已求值的 `task { ... } : Task<'T>` | 这次任务表达式执行的句柄 | 求值立即启动 |

这张表有意限定了范围。.NET `Task` 也可以通过更底层的 API 创建，包括尚未调度的构造函数。可靠的论断针对 F# **任务表达式**：它在求值时立即启动，并在当前线程运行到第一个未完成的异步操作。

两种表示都不承诺新线程。异步意味着工作挂起时调用方不必阻塞。并发与并行是第 24 章讨论的调度选择。

## `async {}` 构建工作但不启动 {#async-start}

共享示例创建两个显式信号。`asyncEntered` 记录是否进入主体；`asyncRelease` 阻止主体完成。这里没有时钟，也不假设调度器速度。

<<< @/../examples/scripts/ch22-async-task.fsx#async-start{fsharp:line-numbers} [ch22-async-task.fsx]

构造 `deferredAsync` 不会设置 `asyncEntered`，所以第一个断言观察的是事实而不是竞争。`Async.StartAsTask deferredAsync` 是显式启动边界，并返回表示这次执行的任务。等待进入信号证明主体已经开始。释放信号仍然关闭，因此返回的任务必定尚未完成。

一个 `Async<'T>` 值可以再次启动。每次启动都会产生一次新执行，包括再次发生其中的效果。这种可重复性利于组合，但并非自动记忆化。如果信用卡扣款只能发生一次，就不要在没有所有权或幂等策略时暴露可以任意重启的值。

常见启动函数具有不同契约：

| 操作 | 结果 | 调用方行为 |
|---|---|---|
| `Async.StartAsTask work` | `Task<'T>` | 启动工作，并把可观察句柄交给调用方 |
| `Async.RunSynchronously work` | `'T` | 启动并阻塞当前调用方，直到完成 |
| 对 `Async<unit>` 使用 `Async.Start work` | `unit` | 启动但不返回完成句柄 |

优先保留可观察句柄或结构化父工作流。即发即弃的 `Async.Start` 很容易丢失异常观察和生命周期所有权。`Async.RunSynchronously` 适用于脚本或可执行程序边界中有意阻塞的场合；不要把它散布在服务请求路径或 UI 处理程序中。

## `task {}` 在表达式求值时启动 {#task-start}

后半段使用相同的实验形状：

<<< @/../examples/scripts/ch22-async-task.fsx#task-start{fsharp:line-numbers} [ch22-async-task.fsx]

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

模块级绑定 `let quoteTask = fetchQuote "R-22"` 则会存储模块初始化时已经启动的那一个任务。后来的消费者共享这次执行及其结果。应当有意选择工厂或共享任务。

## 计算表达式语法排列上下文 {#workflow-syntax}

在任一种计算表达式中：

- `let name = expression` 现在求值普通表达式；
- `let! name = computation` 异步等待，并绑定其成功结果；
- `do! computation` 异步等待有用结果为 `unit` 的操作；
- `return value` 提供工作流结果；
- `return! computation` 把工作流结果委托给另一项计算。

例如，使用任务的 .NET 边界可以始终保持任务风格：

```fsharp
let quoteAndReserve fetchQuote reserve request =
    task {
        let! quote = fetchQuote request
        do! reserve quote
        return quote.Id
    }
```

第二项操作只会在第一项产生 `quote` 后启动；这段代码是顺序执行的。看起来从上到下的语法不会自动让独立调用并发。只启动需求允许的并发，同时考虑速率限制、部分失败和取消。

用 `let!` 等待时，会按照工作流和被等待操作的语义交还控制权。读取 `.Result`、调用 `.Wait()` 或 `GetAwaiter().GetResult()` 都会阻塞当前线程。共享脚本只在顶层测试边界使用最后一种形式，让进程存活并断言结果；应用工作流应继续使用 `let!`。

## 只在边界互操作一次 {#interop}

平台提供大量 `Task<'T>` API，而已有 F# 库和代码库可能提供 `Async<'T>`。转换是显式的：

<<< @/../examples/scripts/ch22-async-task.fsx#interop{fsharp:line-numbers} [ch22-async-task.fsx]

`Async.StartAsTask` 会同时启动异步计算并返回任务。`Async.AwaitTask` 返回一项异步计算；当这项异步计算启动时，它会等待给定任务。它不会倒回或延迟已经运行的任务。

F# 任务表达式也可以直接用 `let!` 绑定 `Async<'T>`。选择能让外围工作流保持一致的形式。桥接处的异常与取消细节是可观察契约，而不只是类型转换；第 23 章会明确测试它们。

一条实用的边界规则是：

```text
外部 Task API → 必要时适配一次 → 一种内部工作流风格
                                 → 在公共边界适配一次
```

反复进行 `Async` → `Task` → `Async` 转换，会掩盖哪个调用启动工作、哪个取消策略生效。

## 从周围契约做选择 {#choice}

不存在通用赢家：

| 场景 | 通常从这里开始 | 理由 |
|---|---|---|
| ASP.NET Core 或公共 .NET API | `Task<'T>` / `task {}` | 宿主和多数 .NET 库已经交换任务 |
| 以 `Async` 组合器为中心的 F# 代码 | `Async<'T>` / `async {}` | 延迟描述适合先组合、最后显式启动 |
| 现有依赖返回其中一种表示 | 沿用该表示 | 避免没有新增策略的适配器 |
| 调用方必须决定是否启动工作 | `Async<'T>` 或显式工厂 | 构造可以与执行分离 |
| 应当共享一次执行 | 有意存储的 `Task<'T>` | 该值命名这次执行及其最终结果 |
| CPU 密集型计算 | 两者本身都不是答案 | 测量后选择显式调度或并行工具 |

对于大量互操作任务式 .NET API 的新代码，任务表达式通常是直接选择。若设计依赖 `Async` 的延迟模型、组合器、异步尾调用或隐式取消令牌流，`Async` 仍然很有价值。取消差异留到下一章，避免把选择简化成口号。

不要为了隐藏 `Task` 或 `Async` 而添加包装接口。在测试或架构需要端口时，抽象真正有意义的操作，例如 `QuoteRequest -> Task<Quote>`。最终结果的载体可以保持可见。

## 测试状态转换，而不是猜测等待时长 {#deterministic-testing}

测试若睡眠 20 毫秒后期望工作已经开始，实际测试的是机器负载与调度器运气。信号能明确表达因果顺序：

```text
构造/调用 → 观察进入闩锁 → 断言未完成 → 释放闩锁 → 观察结果
```

`TaskCompletionSource<'T>` 允许测试代码控制一个任务何时完成。示例请求 `RunContinuationsAsynchronously`，使释放闩锁不会意外地在释放调用中内联执行其后续。这一选项不会改变被测的启动规则。

每次执行都应使用一套新闩锁。若失败的断言可能使工作永远挂起，应在清理中完成所有闩锁。生产代码应等待真实 API，而不是暴露测试闩锁；闩锁只是外部完成事件的确定性替身。

## 运行共享示例 {#run-example}

在仓库根目录运行：

```console
dotnet fsi --exec examples/scripts/ch22-async-task.fsx
```

六行确定性输出证明构造与启动的区别、完成前的挂起、最终结果以及两个互操作方向。manifest 会检查其精确顺序。

## 练习 {#exercises}

### 练习 1：预测并证明进入时机 {#exercise-01}

编写一个 `Async<int>` 和一个 `unit -> Task<int>`。两者在等待注入闩锁之前各自递增私有计数器。不要使用 sleep，证明构造或调用后、显式启动 async 后以及释放后的计数器值。

解释调用任务工厂两次与等待同一个返回任务两次为何不同。

### 练习 2：保持一种内部表示 {#exercise-02}

某个 .NET 客户端公开 `send: Request -> Task<Response>`，已有 F# 验证器公开 `validate: Response -> Async<Result<ValidResponse, Error>>`。

实现一个返回任务的公共工作流。在组合点适配，不要在内部阻塞，并准确指出网络工作何时启动。

### 练习 3：明确启动所有权 {#exercise-03}

审计这些 API：

```fsharp
val refresh : Task<Snapshot>
val refreshAgain : unit -> Task<Snapshot>
val prepareRefresh : unit -> Async<Snapshot>
```

逐一说明调用方是共享一次执行、创建并立即启动一次执行，还是创建延迟描述。为可以重试但不得重叠的刷新选择最安全的契约，并说明仍缺少的并发策略。

[阅读本章练习答案](../solutions/ch-22-async-task)。

## 模型复盘 {#model-review}

- `Async<'T>` 描述一项计算；构造它不会运行主体。
- 再次启动同一个 async 描述会创建另一次执行，并重复其中的效果。
- 已求值的 F# `task {}` 会立即启动，并在当前线程运行到第一个未完成的异步操作。
- 这项陈述针对任务表达式，而非每个继承 `Task` 的对象。
- `let!` 等待时不会有意阻塞当前线程；`.Result` 与 `.Wait()` 会阻塞。
- 异步本身既不保证新线程，也不保证并发或 CPU 并行。
- 只在真实边界转换一次，并保持启动、取消、失败与观察的所有权可见。
- 确定性信号能证明顺序；任意 sleep 只能猜测。

下一章会把这些启动模型带入取消、超时、故障传播，以及每种完成路径上的资源释放。

## 资料来源 {#sources}

- [Microsoft Learn：F# 中的 Async 与 Task 编程](https://learn.microsoft.com/en-us/dotnet/fsharp/tutorials/async)
- [Microsoft Learn：F# 异步表达式](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/async-expressions)
- [Microsoft Learn：F# 任务表达式](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/task-expressions)
- [FSharp.Core 参考：`Async`](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-control-fsharpasync.html)
- [Microsoft Learn：`TaskCompletionSource<TResult>`](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.taskcompletionsource-1?view=net-10.0)
