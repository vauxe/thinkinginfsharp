---
title: "第 24 章练习答案"
description: "根据不变量选择协调方式，在不假设消息顺序的情况下扩展预约代理，并让缓存失效与重复工作策略可执行。"
translationKey: solutions/ch-24-concurrency-agents-state
---

# 第 24 章练习答案 {#overview}

同步范围应覆盖整个一致性规则。计数器只有一个位置，容量更新却是复合转换；代理可以串行管理进程内状态。持久的跨进程分配必须交给持久存储。

[返回第 24 章](../part-04/ch-24-concurrency-agents-state)。

## 练习 1：选择协调边界 {#exercise-01}

### 先分类，再编码 {#exercise-01-table}

| 需求 | 合适的首选边界 | 重要的非保证 |
|---|---|---|
| 请求指标递增 | `Interlocked.Increment` | 不会原子更新另一个字段或外部指标存储 |
| 剩余量与接受量上的容量转换 | 私有 `lock` 或单一串行状态循环 | 进程内锁不是跨进程事务 |
| 不可变配置刷新 | 构建新快照，原子发布其引用 | 读者仍可能持有上一个有效快照 |
| 按键计算缓存 | 并发字典加明确的 lazy/请求合并策略 | 线程安全没有定义数据何时过期或如何淘汰 |
| 持久的跨进程座位分配 | 数据库约束、事务或同等共享机制 | 进程内存协调无法强制它 |

### 强制计数器与容量调度 {#exercise-01-code}

```fsharp
open System
open System.Threading
open System.Threading.Tasks

let start action =
    Task.Factory.StartNew(
        Action action,
        CancellationToken.None,
        TaskCreationOptions.LongRunning,
        TaskScheduler.Default)

let runTwo action =
    use barrier = new Barrier(2)
    let first = start (fun () -> action barrier)
    let second = start (fun () -> action barrier)
    Task.WaitAll [| first; second |]

let metric = [| 0 |]

runTwo (fun barrier ->
    barrier.SignalAndWait() |> ignore
    Interlocked.Increment(&metric[0]) |> ignore)

assert (metric[0] = 2)

let remaining = [| 3 |]
let accepted = [| 0 |]
let gate = obj ()
let results = ResizeArray<bool>()

runTwo (fun barrier ->
    barrier.SignalAndWait() |> ignore

    let result =
        lock gate (fun () ->
            if remaining[0] >= 2 then
                remaining[0] <- remaining[0] - 2
                accepted[0] <- accepted[0] + 1
                true
            else
                false)

    lock results (fun () -> results.Add result))

assert (results |> Seq.filter id |> Seq.length = 1)
assert (remaining[0] = 1 && accepted[0] = 1)
```

外围结果列表的锁只是测试记账，与领域锁分离。生产代码应通过任务或消息返回结果，而不是暴露可变列表。

## 练习 2：扩展预约代理 {#exercise-02}

### 通过循环携带一个不可变状态 {#exercise-02-agent}

```fsharp
type Reply =
    | Applied
    | Rejected of reason: string

type Message =
    | Reserve of requestId: string * seats: int * AsyncReplyChannel<Reply>
    | CancelReservation of requestId: string * AsyncReplyChannel<Reply>
    | Snapshot of AsyncReplyChannel<Map<string, int>>
    | Stop of AsyncReplyChannel<unit>

let capacity = 3
let used reservations = reservations |> Map.values |> Seq.sum

let agent =
    MailboxProcessor.Start(fun inbox ->
        let rec loop reservations =
            async {
                let! message = inbox.Receive()

                match message with
                | Reserve(requestId, seats, reply) ->
                    if seats <= 0 then
                        reply.Reply(Rejected "non-positive")
                        return! loop reservations
                    elif Map.containsKey requestId reservations then
                        reply.Reply(Rejected "duplicate")
                        return! loop reservations
                    elif used reservations + seats > capacity then
                        reply.Reply(Rejected "capacity")
                        return! loop reservations
                    else
                        reply.Reply Applied
                        return! loop (Map.add requestId seats reservations)
                | CancelReservation(requestId, reply) ->
                    if Map.containsKey requestId reservations then
                        reply.Reply Applied
                        return! loop (Map.remove requestId reservations)
                    else
                        reply.Reply(Rejected "unknown")
                        return! loop reservations
                | Snapshot reply ->
                    reply.Reply reservations
                    return! loop reservations
                | Stop reply ->
                    reply.Reply()
            }

        loop Map.empty)

let initial = agent.PostAndReply(fun reply -> Reserve("A", 2, reply))
assert (initial = Applied)

let concurrentReplies =
    [|
        agent.PostAndAsyncReply(fun reply -> Reserve("B", 2, reply))
        agent.PostAndAsyncReply(fun reply -> CancelReservation("A", reply))
    |]
    |> Async.Parallel
    |> Async.RunSynchronously

let snapshot = agent.PostAndReply Snapshot
let occupied = used snapshot

assert (occupied >= 0 && occupied <= capacity)
assert (snapshot.Count <= 1)
assert (concurrentReplies.Length = 2)

agent.PostAndReply Stop
agent.Dispose()
```

若 `Reserve B` 先到，它会被拒绝，随后移除 `A`。若取消先到，`B` 就可以被接受。两种结果都保持容量，因此测试断言不变量，而不是某个调度。

取消未知请求会返回 `Rejected "unknown"`，并保持状态不变。`Stop` 回复一次并退出。若状态必须在进程之外存活，生产代理还需要接纳限制、错误观察和持久边界。

## 练习 3：规定缓存策略 {#exercise-03}

### 让失效与单次计算可见 {#exercise-03-cache}

```fsharp
open System
open System.Collections.Concurrent
open System.Threading
open System.Threading.Tasks

let cache = ConcurrentDictionary<string, Lazy<Result<int, string>>>()
let calls = [| 0 |]

let factory _ =
    let call = Interlocked.Increment(&calls[0])
    Ok call

let get key =
    cache.GetOrAdd(key, fun storedKey -> lazy (factory storedKey))
    |> fun delayed -> delayed.Value

let invalidate (key: string) =
    match cache.TryRemove key with
    | true, _ -> true
    | false, _ -> false

let startTogether = new Barrier(2)

let startGet () =
    Task.Factory.StartNew(
        (fun () ->
            startTogether.SignalAndWait() |> ignore
            get "quote"),
        CancellationToken.None,
        TaskCreationOptions.LongRunning,
        TaskScheduler.Default)

let first = startGet ()
let second = startGet ()
Task.WaitAll [| first :> Task; second :> Task |]

assert (first.Result = Ok 1 && second.Result = Ok 1)
assert (calls[0] = 1)
assert (invalidate "quote")
assert (get "quote" = Ok 2)
assert (calls[0] = 2)
startTogether.Dispose()
```

字典可能多次调用外层值工厂，但该工厂只创建 `Lazy`。真正有副作用的 `factory` 只会通过最终存入字典的 lazy 运行，并在失效前执行一次。主动失效后，后续调用可以再次计算。

这个设计有意缓存 `Result`，包括预期的 `Error`，直到失效。默认 `Lazy` 内抛出的意外异常也会缓存。真实策略必须决定过期与最大大小；这个无界教学字典不是生产缓存。

远程副作用要实现业务上的一次性，仍需由确认最终结果的系统提供幂等保证。`Lazy` 只能在当前进程和缓存生命周期内阻止重复求值。

## 答案复盘 {#solution-review}

- 同步整个不变量，而不是独立同步每个字段。
- 测试记账需要自己的协调，且不应泄漏进生产设计。
- 代理测试接受每种有效到达顺序，并拒绝无效最终状态。
- 邮箱只管理进程内状态与生命周期。
- `GetOrAdd` 可以多次调用工厂；不要假设不可逆副作用只会执行一次。
- `Lazy` 改变重复计算与失败缓存策略，却不定义数据何时过期，也不提供持久性。
