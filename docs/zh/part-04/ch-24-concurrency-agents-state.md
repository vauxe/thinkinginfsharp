---
title: "第 24 章：并行、并发、代理与受控可变性"
description: "区分重叠工作与 CPU 并行，确定性复现竞争，并根据不变量选择不可变数据、锁、原子操作、代理或并发缓存。"
translationKey: part-04/ch-24-concurrency-agents-state
---

# 第 24 章：并行、并发、代理与受控可变性 {#overview}

两个预约请求可以在等待存储时重叠，两个定价计算也可以在不同核心上运行。这是两类不同问题。前者即使只用一个线程也需要生命周期与一致性规则；后者是一项性能技术，其开销可能大于收益。

F# 便于使用不可变值，可以消除许多意外竞争。但队列、缓存、计数器、文件、数据库和外部服务仍然存在共享可变状态。应根据必须维持的不变量选择同步机制。

## 三个概念，三个问题 {#three-concepts}

| 概念 | 问题 | 例子 |
|---|---|---|
| 异步 | 结果挂起时，调用方能否交还控制权？ | 等待文件或网络 I/O |
| 并发 | 多项操作的生命周期能否重叠、同时处于进行中？ | 两个请求等待各自独立的回复 |
| 并行 | 工作能否在多个 CPU 核心等计算资源上同时执行？ | 对 CPU 密集型数组变换进行分区 |

三者可以组合，但任意一项都不意味着另外两项。异步操作可能同步完成；UI 事件循环可以在单线程上协调并发工作；并行循环对调用方而言往往仍是同步的，直到所有分区完成。

从需求出发。使用异步 API，避免在等待期间阻塞稀缺线程。只有操作可以安全重叠时才增加并发。只有测量表明独立 CPU 工作足以抵消分区、调度、协调和分配成本时才增加并行。

## 只验证工作重叠，不推断线程 {#concurrent-overlap}

共享示例启动两个任务表达式。每一个都会记录已经进入，然后等待同一个尚未触发的信号：

```fsharp:line-numbers [ch24-concurrency-agents-state.fsx]
let releaseWaits = newGate<unit> ()
let entered = [| 0 |]

let waitingWork label =
    task {
        Interlocked.Increment(&entered[0]) |> ignore
        do! releaseWaits.Task
        return label
    }

let firstWait = waitingWork "first"
let secondWait = waitingWork "second"
let bothPending = not firstWait.IsCompleted && not secondWait.IsCompleted

assert (entered[0] = 2)
assert bothPending
printfn "Concurrent waits: entered=%d pending=%b" entered[0] bothPending

releaseWaits.SetResult()

let waitResults =
    let running = Task.WhenAll [| firstWait; secondWait |]
    running.GetAwaiter().GetResult()

assert (waitResults = [| "first"; "second" |])
printfn "Concurrent results: %A" waitResults
```
两项操作都在进行中，而且都没有完成。这证明了工作在时间上重叠，却没有说明 CPU 是否同时执行或使用了哪些线程。触发同一个信号会让两者恢复，`Task.WhenAll` 按输入任务顺序返回结果，但测试不对实际完成顺序作断言。

无限并发不是性能计划。每个外部依赖都有连接数、队列、内存与速率限制。应当在受限资源附近限制并发，并决定超额工作是等待、失败还是被拒绝。

## 数据并行需要独立工作与测量 {#data-parallelism}

`Array.Parallel.map` 通过 .NET 并行基础设施对数组变换进行分区：

```fsharp:line-numbers [ch24-concurrency-agents-state.fsx]
let values = [| 1..8 |]
let sequentialSquares = values |> Array.map (fun value -> value * value)
let parallelSquares = values |> Array.Parallel.map (fun value -> value * value)
let parallelAgrees = parallelSquares = sequentialSquares

assert parallelAgrees
printfn "Parallel map agrees: %b" parallelAgrees
```
映射是纯函数，每个输出只依赖一个输入，因此调度顺序不会改变结果。断言只能证明两个版本返回相同的值，不能证明并行版本更快。对这个小数组来说，并行很可能没有必要；第 31 章会先测量再选择。

审查并行映射时要考虑：

- 元素之间是否独立；
- 共享副作用是否不存在或已经同步；
- 跨分区异常与取消行为；
- 结果与副作用的顺序要求；
- 分配与分区开销；
- 宿主限制——尤其是已经在处理并发请求的服务器。

不要只为“并行”就把天然异步的 I/O 包进 CPU 并行 API。应按该 I/O API 的异步方式调用，并设置合理的并发上限。

## 读取—修改—写入并非一个操作 {#lost-update}

表达式 `counter <- counter + 1` 会读取、计算再写入。两个线程可以读到相同旧值，再写入相同新值，从而丢失一次递增。

只靠反复运行的压力测试有时会错过这项竞争。共享测试使用两个参与者的 `Barrier`：两个长时间运行的工作线程都在任一线程可以写入前完成读取。因此测试必然产生错误结果，而不是碰运气：

```fsharp:line-numbers [ch24-concurrency-agents-state.fsx]
let racyCounter = [| 0 |]

runTwoWithBarrier (fun barrier ->
    let snapshot = racyCounter[0]
    barrier.SignalAndWait() |> ignore
    racyCounter[0] <- snapshot + 1)

let lockedCounter = [| 0 |]
let counterLock = obj ()

runTwoWithBarrier (fun barrier ->
    barrier.SignalAndWait() |> ignore

    lock counterLock (fun () -> lockedCounter[0] <- lockedCounter[0] + 1))

let atomicCounter = [| 0 |]

runTwoWithBarrier (fun barrier ->
    barrier.SignalAndWait() |> ignore
    Interlocked.Increment(&atomicCounter[0]) |> ignore)

assert (racyCounter[0] = 1)
assert (lockedCounter[0] = 2)
assert (atomicCounter[0] = 2)

printfn "Shared counter: race=%d lock=%d interlocked=%d" racyCounter[0] lockedCounter[0] atomicCounter[0]
```
同一屏障启动两个修正版。`lock` 使整个读取—修改—写入临界区互斥。`Interlocked.Increment` 原子地执行它所支持的更新。确定性结果分别是 `1`、`2`、`2`。

仅有 `volatile` 可见性不会把多步骤递增变成原子操作。同样，线程安全集合只保护它自己的方法；它不会自动把跨多个调用的序列变成事务。

## 选择能保护不变量的最小机制 {#coordination-choice}

| 需求 | 首选 | 保护范围 |
|---|---|---|
| 可以独立计算各值 | 不可变值与纯函数 | 没有共享写入 |
| 单个发布者可以发布完整修订版 | 不可变快照加原子引用交换 | 一个快照身份 |
| 一个数值/引用更新 | `Interlocked` 操作 | 一个受支持的位置与操作 |
| 多个字段必须同步地一起改变 | 私有对象上的 `lock` | 一个短临界区 |
| 围绕私有状态串行化进程内请求 | `MailboxProcessor` | 一个邮箱循环 |
| 有文档行为保证的并发键操作 | 并发集合 | 一个集合方法，而非任意工作流 |
| 持久化或跨进程一致性 | 存储事务、约束、版本或分布式协议 | 外部事实来源 |

优先把可变状态限制在单个函数内，少用需要同步的共享可变状态。如果不可变结果发布前其他操作无法观察中间状态，那么在函数内部构建数组或字典既高效又简单。

### 锁保护代码区域 {#locks}

使用私有锁对象，并保持临界区短小。在同一把锁下读取和更新某项不变量涉及的全部字段。绝不要锁定公共对象、驻留字符串或外部传入值，因为无关代码也可能锁定它。

不要在持锁期间执行 `let!`、网络 I/O、回调或耗时未知的工作。监视器与线程绑定并会阻塞；异步挂起还会让生命周期和死锁更难分析。可把操作拆成读取快照、执行外部工作和短暂的校验提交，或改用异步协调原语。

若无法避免多把锁，就规定唯一获取顺序。否则两个工作线程可能各持有一把锁，并永远等待另一把。

容量示例把 `Remaining` 与 `Accepted` 作为一项不变量更新：

```fsharp:line-numbers [ch24-concurrency-agents-state.fsx]
type CapacityState =
    { mutable Remaining: int
      mutable Accepted: int }

let capacity = { Remaining = 3; Accepted = 0 }
let capacityLock = obj ()

let tryReserve seats =
    lock capacityLock (fun () ->
        if seats > 0 && seats <= capacity.Remaining then
            capacity.Remaining <- capacity.Remaining - seats
            capacity.Accepted <- capacity.Accepted + 1
            true
        else
            false)

let reservationResults = Array.zeroCreate<bool> 2
let reservationIndex = [| -1 |]

runTwoWithBarrier (fun barrier ->
    let index = Interlocked.Increment(&reservationIndex[0])
    barrier.SignalAndWait() |> ignore
    reservationResults[index] <- tryReserve 2)

let acceptedReservations = reservationResults |> Array.filter id |> Array.length
let capacityInvariant = capacity.Remaining = 1 && capacity.Accepted = 1

assert (acceptedReservations = 1)
assert capacityInvariant

printfn
    "Locked capacity: accepted=%d remaining=%d invariant=%b"
    acceptedReservations
    capacity.Remaining
    capacityInvariant
```
两个请求各需要两个座位，而总容量只有三个，因此恰好一个成功。两个字段共同描述同一次已提交转换。分别执行原子递减和递增，既不能让两项更新成为事务，也不能阻止容量变成负数。

### 原子操作保护特定操作 {#atomics}

`Interlocked` 为受支持的位置提供原子递增、相加、交换、比较并交换等操作。它适合计数器、标志，以及能容纳在一个原子位置中的精心设计状态转换。

一旦正确性依赖多个位置、先检查再更新，或外部副作用，零散地给各字段加原子操作就无法保证安全。此时应使用复合同步，或由作为事实来源的存储系统保证一致性。

## 邮箱串行访问状态 {#mailbox}

`MailboxProcessor<'Message>` 在进程内队列上运行异步接收循环。调用方投递消息；循环每次处理一条已接收消息，并可通过递归携带下一个不可变状态。

共享预约代理把 `remaining` 与 `accepted` 保持为私有状态：

```fsharp:line-numbers [ch24-concurrency-agents-state.fsx]
type ReservationReply =
    | Accepted of remaining: int
    | Rejected of remaining: int

type ReservationMessage =
    | Reserve of seats: int * reply: AsyncReplyChannel<ReservationReply>
    | Stop of reply: AsyncReplyChannel<int * int>

let reservationAgent =
    MailboxProcessor.Start(fun inbox ->
        let rec loop remaining accepted =
            async {
                let! message = inbox.Receive()

                match message with
                | Reserve(seats, reply) when seats > 0 && seats <= remaining ->
                    let nextRemaining = remaining - seats
                    reply.Reply(Accepted nextRemaining)
                    return! loop nextRemaining (accepted + 1)
                | Reserve(_, reply) ->
                    reply.Reply(Rejected remaining)
                    return! loop remaining accepted
                | Stop reply -> reply.Reply(remaining, accepted)
            }

        loop 3 0)

let agentReplies =
    [| reservationAgent.PostAndAsyncReply(fun reply -> Reserve(2, reply))
       reservationAgent.PostAndAsyncReply(fun reply -> Reserve(2, reply)) |]
    |> Async.Parallel
    |> Async.RunSynchronously

let agentAccepted =
    agentReplies
    |> Array.filter (function
        | Accepted _ -> true
        | Rejected _ -> false)
    |> Array.length

let agentRemaining, agentAcceptedState = reservationAgent.PostAndReply Stop

assert (agentAccepted = 1)
assert (agentRemaining = 1)
assert (agentAcceptedState = 1)

printfn
    "Agent capacity: accepted=%d remaining=%d invariant=%b"
    agentAccepted
    agentRemaining
    (agentAcceptedState = agentAccepted)

reservationAgent.Dispose()
```
两个调用方分别创建等待回复的计算，`Async.Parallel` 同时启动它们。到达顺序未作规定，因此测试只断言不变量：一个请求被接受、剩余一个座位，而且代理状态与回复一致。`Stop` 返回最终状态并结束循环，随后释放处理器。

回复通道允许接收方发送一次回复，因此必须恰好使用一次。应规定无效消息、处理程序异常、取消、关闭，以及调用方停止等待时的行为。若生产速度可能超过单一消费者，就监控队列或限制进入量。

代理只串行化这个邮箱内的代码。它**不会**提供：

- 进程丢失后的消息或状态持久化；
- 与数据库、支付提供方或另一个代理的事务；
- 恰好一次投递或恰好一次外部副作用；
- 自动重试、幂等、背压、监管或横向扩展；
- 对其他代码仍可直接访问的可变状态进行保护。

只有问题适合由一个进程内循环串行处理时才使用代理；它不会让外部系统自动获得一致性。

## 缓存还需要新鲜度与失效策略 {#cache}

缓存需要的不只是线程安全字典，还要规定：

- 键相等与规范化；
- 值的新鲜度与失效；
- 大小限制与淘汰；
- 是否缓存失败、缓存多久；
- 并发未命中能否重复计算；
- 谁可以取消共享计算；
- 用于记录命中、未命中、加载、淘汰与失败的指标或日志。

`ConcurrentDictionary.GetOrAdd(key, valueFactory)` 可以保证字典操作的线程安全。但文档说明，`valueFactory` 在内部锁外执行，因此发生竞争时可能运行多次。绝不要把不可逆副作用放进该工厂，再假定它只会执行一次。

示例存储 `Lazy<int>` 值：

```fsharp:line-numbers [ch24-concurrency-agents-state.fsx]
let cache = ConcurrentDictionary<string, Lazy<int>>()
let computations = [| 0 |]

let getCached key =
    cache.GetOrAdd(
        key,
        fun _ ->
            lazy
                (Interlocked.Increment(&computations[0]) |> ignore
                 23)
    )
    |> fun delayed -> delayed.Value

let cacheBarrier = new Barrier(2)

let cachedTasks =
    [| startLongRunning (fun () ->
           cacheBarrier.SignalAndWait() |> ignore
           getCached "quote" |> ignore)
       startLongRunning (fun () ->
           cacheBarrier.SignalAndWait() |> ignore
           getCached "quote" |> ignore) |]

Task.WaitAll cachedTasks
let cachedValues = [| getCached "quote"; getCached "quote" |]

assert (cachedValues = [| 23; 23 |])
assert (computations[0] = 1)
assert (cache.Count = 1)
printfn "Cache: values=%A computations=%d entries=%d" cachedValues computations[0] cache.Count
cacheBarrier.Dispose()
```
发生竞争时，字典工厂可能分配多个 `Lazy`，但调用方只会求值字典实际返回的那个实例。`Lazy` 默认的执行与发布方式使示例中的计算只运行一次。它也会缓存创建值时抛出的异常，而字典会在没有淘汰机制时持续增长；这两点并非任何场景都适合。

对于远程工作，共享一个进行中的 `Task<'T>` 可以合并相同的并发请求（single-flight），但第 23 章讨论的生命周期问题仍然存在。任何一个调用方都不应意外取消大家共享的工作。

## 用受控交错测试不变量 {#testing}

并发测试只应在被测行为需要时控制顺序：

```text
两者读取旧值 → 屏障打开 → 两者写入
```

这个执行顺序会稳定地产生丢失更新。修正后的实现可以复用同一顺序，再断言最终不变量。除非 API 承诺处理顺序，否则不要断言哪个合法请求获胜。

重复运行专项测试有助于暴露资源与生命周期错误，但不能替代受控交错。避免依赖 sleep、CPU 数量、线程 ID 或调度器的具体顺序。清理时务必释放屏障并触发等待信号，避免断言失败后工作线程无法退出。

## 运行共享示例 {#run-example}

在示例所在目录运行：

```console
dotnet fsi --checknulls+ --exec ch24-concurrency-agents-state.fsx
```

七行输出覆盖并发等待、数据并行等价、强制丢失更新、锁与原子修正、复合容量不变量、代理串行化，以及单次计算缓存。

## 练习 {#exercises}

### 练习 1：选择同步机制 {#exercise-01}

对以下需求分类：请求指标递增、双字段容量转换、不可变配置刷新、按键计算缓存，以及持久的跨进程座位分配。逐一选择同步机制，并说出一项它不提供的保证。

用屏障实现并测试计数器与容量用例。

### 练习 2：扩展预约代理 {#exercise-02}

添加 `CancelReservation` 与 `Snapshot` 消息。在代理内部保持不可变 `Map<RequestId, Seats>`，并从已接受条目推导剩余容量，或一致地更新一个状态。

并发投递预约与取消消息。断言有效最终不变量，不假设到达顺序。定义未知请求 ID 的关闭和回复行为。

### 练习 3：规定缓存策略 {#exercise-03}

为 `Lazy` 缓存增加最大容量或手动失效，并决定是否缓存失败。使用受控工厂统计并发未命中期间实际执行了多少次计算。

解释为什么单靠线程安全字典无法保证新鲜度、有界内存、单次外部副作用或分布式一致性。

[阅读本章练习答案](../solutions/ch-24-concurrency-agents-state)。

## 模型复盘 {#model-review}

- 异步、并发与并行回答不同问题。
- 不可变数据消除共享写入；它不会让外部资源自动一致。
- 屏障可以强制产生丢失更新，把概率性的竞争变成确定性测试。
- `lock` 保护短小的复合不变量；`Interlocked` 保护受支持的单位置操作。
- 绝不要持有监视器等待无界工作。
- 邮箱只串行化一个进程内循环，无法协调周围的其他系统。
- 并发集合具有方法级保证；应阅读工厂和组合语义。
- 缓存正确性还包括新鲜度、生命周期、失败与资源策略。

## 第四部分检查点 {#part-checkpoint}

用行为确定的异步替身运行预约 API 测试：

```console
dotnet test ExampleTests.fsproj --configuration Release --filter FullyQualifiedName~BookingAsyncPortTests
```

测试通过表明：调用方的取消令牌会传给每个依赖，受控操作在成功、失败或响应取消前会保持等待。这些测试不能证明跨进程一致性或持久性。

[继续阅读第 25 章](../part-05/ch-25-objects-interfaces)，考察更广泛 .NET 生态中的面向对象接口。

## 资料来源 {#sources}

- [Microsoft Learn：任务并行库](https://learn.microsoft.com/en-us/dotnet/standard/parallel-programming/task-parallel-library-tpl)
- [Microsoft Learn：数据并行](https://learn.microsoft.com/en-us/dotnet/standard/parallel-programming/data-parallelism-task-parallel-library)
- [FSharp.Core 参考：`Array.Parallel`](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-collections-arraymodule-parallel.html)
- [Microsoft Learn：`Interlocked`](https://learn.microsoft.com/en-us/dotnet/api/system.threading.interlocked?view=net-10.0)
- [FSharp.Core 参考：`MailboxProcessor<'Msg>`](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-control-fsharpmailboxprocessor-1.html)
- [Microsoft Learn：`ConcurrentDictionary.GetOrAdd`](https://learn.microsoft.com/en-us/dotnet/api/system.collections.concurrent.concurrentdictionary-2.getoradd?view=net-10.0)
