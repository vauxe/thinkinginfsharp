---
title: "第 24 章：并行、并发、代理与受控可变性"
description: "区分重叠工作与 CPU 并行，确定性复现竞争，并根据不变量选择不可变数据、锁、原子操作、代理或并发缓存。"
translationKey: part-04/ch-24-concurrency-agents-state
kind: chapter
part: 4
chapter: 24
status: complete
verifiedWith:
  fsharp: "10"
  dotnetSdk: "10.0.301"
exampleIds:
  - ch24-concurrency-agents-state
exerciseIds:
  - ch24-exercise-01
  - ch24-exercise-02
  - ch24-exercise-03
termIds:
  - effect
sources:
  - id: dotnet-task-parallel-library
    url: https://learn.microsoft.com/en-us/dotnet/standard/parallel-programming/task-parallel-library-tpl
    checked: "2026-08-24"
  - id: dotnet-data-parallelism
    url: https://learn.microsoft.com/en-us/dotnet/standard/parallel-programming/data-parallelism-task-parallel-library
    checked: "2026-08-24"
  - id: fsharp-array-parallel
    url: https://fsharp.github.io/fsharp-core-docs/reference/fsharp-collections-arraymodule-parallel.html
    checked: "2026-08-24"
  - id: dotnet-interlocked
    url: https://learn.microsoft.com/en-us/dotnet/api/system.threading.interlocked?view=net-10.0
    checked: "2026-08-24"
  - id: fsharp-mailbox-processor
    url: https://fsharp.github.io/fsharp-core-docs/reference/fsharp-control-fsharpmailboxprocessor-1.html
    checked: "2026-08-24"
  - id: dotnet-concurrent-get-or-add
    url: https://learn.microsoft.com/en-us/dotnet/api/system.collections.concurrent.concurrentdictionary-2.getoradd?view=net-10.0
    checked: "2026-08-24"
---

# 第 24 章：并行、并发、代理与受控可变性 {#overview}

两个预约请求可以在等待存储时重叠，两个定价计算也可以在不同核心上运行。这是两类不同问题。前者即使只用一个线程也需要生命周期与一致性规则；后者是一项性能技术，其开销可能大于收益。

F# 让不可变值很容易使用，从而消除许多意外竞争。但队列、缓存、计数器、文件、数据库或外部服务中的共享可变状态不会因此消失。本章会从必须保持成立的不变量出发选择协调边界。

## 学完本章后你将能够 {#outcomes}

学完本章后，你应当能够：

- 区分异步工作、并发与并行执行；
- 避免声称 task 或 async 计算会创建线程；
- 只为独立且经过测量的 CPU 工作使用数据并行；
- 用确定性屏障复现丢失更新；
- 尽可能让可变性保持局部，或发布不可变快照；
- 用 `lock` 保护短小的复合不变量，并避免持锁等待；
- 用 `Interlocked` 对单个共享位置执行受支持的原子操作；
- 用 `MailboxProcessor` 建模串行化的进程内所有权；
- 说明代理不对持久性或分布式效果保证什么；
- 设计带有显式重复工作、失败、新鲜度与淘汰策略的缓存；
- 测试最终不变量，而不断言非确定的执行顺序。

## 三个概念，三个问题 {#three-concepts}

| 概念 | 问题 | 例子 |
|---|---|---|
| 异步 | 结果挂起时，调用方能否交还控制权？ | 等待文件或网络 I/O |
| 并发 | 多项操作的生命周期能否重叠、同时处于进行中？ | 两个请求等待各自独立的回复 |
| 并行 | 工作能否在多个处理资源上同时执行？ | 对 CPU 密集型数组变换进行分区 |

它们可以组合，但彼此都不蕴含另一个。异步操作可能同步完成。UI 事件循环可以在单线程上协调并发工作。并行循环从调用方视角看往往仍是同步的，直到所有分区都完成。

从需求出发。使用异步 API，避免在等待期间阻塞稀缺线程。只有操作可以安全重叠时才增加并发。只有测量表明独立 CPU 工作足以抵消分区、调度、协调和分配成本时才增加并行。

## 观察重叠而不声称线程 {#concurrent-overlap}

共享示例启动两个任务表达式。每一个都会记录已经进入，然后等待同一个关闭的闩锁：

<<< @/../examples/scripts/ch24-concurrency-agents-state.fsx#concurrent-waits{fsharp:line-numbers} [ch24-concurrency-agents-state.fsx]

两项操作都在进行中，而且都没有完成。这证明了并发生命周期，却没有说明 CPU 是否同时执行或线程身份是什么。释放同一个闩锁会让两者恢复，`Task.WhenAll` 按输入任务顺序返回结果，但测试不对完成调度顺序作断言。

无限并发不是性能计划。每个外部依赖都有连接数、队列、内存与速率限制。应当在受限资源附近限制并发，并决定超额工作是等待、失败还是被拒绝。

## 数据并行需要独立工作与测量 {#data-parallelism}

`Array.Parallel.map` 通过 .NET 并行基础设施对数组变换进行分区：

<<< @/../examples/scripts/ch24-concurrency-agents-state.fsx#parallel-map{fsharp:line-numbers} [ch24-concurrency-agents-state.fsx]

映射是纯的，每个输出只依赖一个输入，因此调度顺序不会改变值。断言证明功能等价，而非速度。对于这个小数组，并行版本很可能没有必要；第 31 章会先测量再选择。

审查并行映射时要考虑：

- 元素之间是否独立；
- 共享效果是否不存在或已同步；
- 跨分区异常与取消行为；
- 结果与效果的顺序要求；
- 分配与分区开销；
- 宿主限制——尤其是已经在处理并发请求的服务器。

不要仅仅为了“并行”，就把天然异步的 I/O 包进 CPU 并行 API。使用该 I/O API 的异步契约与有意的并发限制。

## 读取—修改—写入并非一个操作 {#lost-update}

表达式 `counter <- counter + 1` 会读取、计算再写入。两个线程可以读到相同旧值，再写入相同新值，从而丢失一次递增。

概率式压力测试有时会错过这项竞争。共享测试使用两个参与者的 `Barrier`：两个长时间运行的工作线程都在任一线程可以写入前完成读取。因此错误结果是被强制产生，而不是碰运气：

<<< @/../examples/scripts/ch24-concurrency-agents-state.fsx#shared-state{fsharp:line-numbers} [ch24-concurrency-agents-state.fsx]

同一屏障启动两个修正版。`lock` 使整个读取—修改—写入临界区互斥。`Interlocked.Increment` 原子地执行它所支持的更新。确定性结果分别是 `1`、`2`、`2`。

仅有 `volatile` 可见性不会把多步骤递增变成原子操作。同样，线程安全集合只保护它自己的方法；它不会自动把跨多个调用的序列变成事务。

## 选择能保护不变量的最小边界 {#coordination-choice}

| 需求 | 首选 | 边界 |
|---|---|---|
| 可以独立计算各值 | 不可变值与纯函数 | 没有共享写入 |
| 单个所有者可以发布整个修订版 | 不可变快照加原子引用交换 | 一个快照身份 |
| 一个数值/引用更新 | `Interlocked` 操作 | 一个受支持的位置与操作 |
| 多个字段必须同步地一起改变 | 私有对象上的 `lock` | 一个短临界区 |
| 围绕私有状态串行化进程内请求 | `MailboxProcessor` | 一个邮箱循环 |
| 带有文档化语义的并发键操作 | 并发集合 | 一个集合方法，而非任意工作流 |
| 持久化或跨进程一致性 | 存储事务、约束、版本或分布式协议 | 外部权威 |

相较同步共享可变性，优先让可变性停留在函数内部。若其他操作在不可变结果发布前无法观察，函数内构建数组或字典既高效又简单。

### 锁保护代码区域 {#locks}

使用私有锁对象，并保持受保护区域短小。在同一把锁下读取和更新某项不变量的所有字段。绝不要把公共对象、驻留字符串或外部传入值用作监视器，因为无关代码也可能锁住它。

不要在监视器内跨越 `let!`、网络 I/O、回调或其他无界工作。监视器具有线程亲和性且会阻塞；异步挂起还会让生命周期与死锁推理更加困难。把操作拆成快照、外部工作和短小的验证提交，或选择异步协调原语。

若无法避免多把锁，就规定唯一获取顺序。否则两个工作线程可能各持有一把锁，并永远等待另一把。

容量示例把 `Remaining` 与 `Accepted` 作为一项不变量更新：

<<< @/../examples/scripts/ch24-concurrency-agents-state.fsx#compound-invariant{fsharp:line-numbers} [ch24-concurrency-agents-state.fsx]

两个两座请求争抢容量三。恰好一个成功，两个字段描述同一个已提交转换。分别进行原子递减与递增，本身不能让这一对操作成为事务，也不能阻止容量变成负数。

### 原子操作保护特定操作 {#atomics}

`Interlocked` 为受支持的位置提供原子递增、相加、交换、比较并交换等操作。它适合计数器、标志，以及能容纳在一个原子位置中的精心设计状态转换。

一旦正确性依赖多个位置、先检查后更新，或依赖外部效果，在各字段上散布原子操作就不能“让它安全”。应当使用复合同步或权威存储边界。

## 邮箱让状态只有一个串行所有者 {#mailbox}

`MailboxProcessor<'Message>` 在进程内队列上运行异步接收循环。调用方投递消息；循环每次处理一条已接收消息，并可通过递归携带下一个不可变状态。

共享预约代理拥有 `remaining` 与 `accepted`：

<<< @/../examples/scripts/ch24-concurrency-agents-state.fsx#mailbox-agent{fsharp:line-numbers} [ch24-concurrency-agents-state.fsx]

两个调用方创建等待回复的计算，`Async.Parallel` 同时启动它们。到达顺序有意保持未指定，因此测试只断言不变量：一个请求被接受、剩余一个座位，而且代理状态与回复一致。`Stop` 返回最终状态并结束循环；随后释放处理器。

回复通道是一种能力，必须恰好回复一次。应定义畸形消息、处理程序异常、取消、关闭，以及调用方停止等待时的行为。若生产者可能超过单一消费者，就监视队列或限制接纳。

代理只串行化这个邮箱内的代码。它**不会**提供：

- 进程丢失后的消息或状态持久化；
- 与数据库、支付提供方或另一个代理的事务；
- 恰好一次投递或恰好一次外部效果；
- 自动重试、幂等、背压、监管或横向扩展；
- 对其他代码仍可直接访问的可变状态进行保护。

只有一个进程内串行所有者适合问题时才使用代理，不要把它当作一致性的魔法同义词。

## 缓存是带有时间维度的共享状态 {#cache}

缓存需要的不只是线程安全字典，还要规定：

- 键相等与规范化；
- 值的新鲜度与失效；
- 大小限制与淘汰；
- 是否缓存失败、缓存多久；
- 并发未命中能否重复计算；
- 共享计算的取消所有权；
- 命中、未命中、加载、淘汰与失败的可观测性。

`ConcurrentDictionary.GetOrAdd(key, valueFactory)` 会保持字典操作线程安全，但文档明确指出，竞争时工厂可能运行多次，因为它在内部锁之外执行。绝不要在这个工厂中放入不可逆效果，并假定它恰好执行一次。

示例存储 `Lazy<int>` 值：

<<< @/../examples/scripts/ch24-concurrency-agents-state.fsx#cache{fsharp:line-numbers} [ch24-concurrency-agents-state.fsx]

竞争的字典工厂可能分配多个 `Lazy`，但调用方会求值字典实际返回的那一个实例。默认 `Lazy` 的执行并发布语义使演示中的计算只运行一次。这也会缓存值创建期间抛出的异常，而且字典会无限增长；这些是策略，而不是普遍理想的默认值。

对于远程工作，共享进行中的 `Task<'T>` 可以合并同一项并发请求（single-flight），但第 23 章的所有权问题仍然成立。一个调用方不应意外取消所有人共享的工作。

## 测试强制调度与稳定不变量 {#testing}

并发测试只应在性质需要的地方控制顺序：

```text
两者读取旧值 → 屏障打开 → 两者写入
```

这一调度能证明丢失更新。修正实现可以运行同一调度并断言最终不变量。除非顺序是公共契约的一部分，否则不要断言哪个合法请求获胜。

重复聚焦测试有助于暴露资源与生命周期错误，但重复不能替代强制交错。避免 sleep、CPU 数量假设、线程 ID 和精确调度器顺序。总要在清理时释放屏障与闩锁，避免失败断言困住工作线程。

## 运行共享示例 {#run-example}

在仓库根目录运行：

```console
dotnet fsi --checknulls+ --exec examples/scripts/ch24-concurrency-agents-state.fsx
```

七行确定性输出覆盖并发等待、数据并行等价、强制丢失更新、锁与原子修正、复合容量不变量、代理串行化，以及单次计算缓存。

## 练习 {#exercises}

### 练习 1：选择协调边界 {#exercise-01}

对以下需求分类：请求指标递增、双字段容量转换、不可变配置刷新、按键计算缓存，以及持久的跨进程座位分配。逐一选择边界，并说出一项它不提供的保证。

用屏障实现并测试计数器与容量用例。

### 练习 2：扩展预约代理 {#exercise-02}

添加 `CancelReservation` 与 `Snapshot` 消息。在代理内部保持不可变 `Map<RequestId, Seats>`，并从已接受条目推导剩余容量，或一致地更新一个状态。

并发投递预约与取消消息。断言有效最终不变量，不假设到达顺序。定义未知请求 ID 的关闭和回复行为。

### 练习 3：规定缓存策略 {#exercise-03}

为 `Lazy` 缓存增加最大大小或显式失效，并决定失败是否保持缓存。使用受控工厂证明并发未命中期间实际进行了多少次计算。

解释为什么单靠线程安全字典无法保证新鲜度、有界内存、单次外部效果或分布式一致性。

[阅读本章练习答案](../solutions/ch-24-concurrency-agents-state)。

## 模型复盘 {#model-review}

- 异步、并发与并行回答不同问题。
- 不可变数据消除共享写入；它不会让外部资源自动一致。
- 屏障可以强制丢失更新，把概率式竞争变成确定性证据。
- `lock` 保护短小的复合不变量；`Interlocked` 保护受支持的单位置操作。
- 绝不要持有监视器等待无界工作。
- 邮箱串行化一个进程内所有者的消息处理，而非周围世界。
- 并发集合具有方法级保证；应阅读工厂和组合语义。
- 缓存正确性包括时间、所有权、失败与资源策略。

下一个实现切片会把这些规则应用到异步预约端口，并用确定性替身表示成功、故障、取消与延迟完成。

## 资料来源 {#sources}

- [Microsoft Learn：任务并行库](https://learn.microsoft.com/en-us/dotnet/standard/parallel-programming/task-parallel-library-tpl)
- [Microsoft Learn：数据并行](https://learn.microsoft.com/en-us/dotnet/standard/parallel-programming/data-parallelism-task-parallel-library)
- [FSharp.Core 参考：`Array.Parallel`](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-collections-arraymodule-parallel.html)
- [Microsoft Learn：`Interlocked`](https://learn.microsoft.com/en-us/dotnet/api/system.threading.interlocked?view=net-10.0)
- [FSharp.Core 参考：`MailboxProcessor<'Msg>`](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-control-fsharpmailboxprocessor-1.html)
- [Microsoft Learn：`ConcurrentDictionary.GetOrAdd`](https://learn.microsoft.com/en-us/dotnet/api/system.collections.concurrent.concurrentdictionary-2.getoradd?view=net-10.0)
