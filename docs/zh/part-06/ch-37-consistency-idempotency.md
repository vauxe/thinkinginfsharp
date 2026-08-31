---
title: "第 37 章：一致性、幂等、重试与部分失败"
description: "保护聚合级预约容量，明确建模命令重试与副作用进度，并准确说明本地 F# 一致性机制的适用范围。"
translationKey: part-06/ch-37-consistency-idempotency
---

# 第 37 章：一致性、幂等、重试与部分失败 {#overview}

第 36 章暴露了几个风险窗口：两个请求可能都读到旧容量，支付可能在本地状态改变前成功，通知可能在预约提交后失败。捕获异常并不能消除这些风险。本章为每个风险窗口建立状态模型，并划出一个刻意收窄的一致性边界。

本章加入 `AtomicBookingStore` 与 `IdempotentBookingService`。前者存储整个活动聚合与命令进度，后者据此协调支付和通知。聚焦测试直接调用该服务。第 36 章的 HTTP 端点仍使用原先的 `AsyncPorts` 工作流；第 38 章才会接入一致性服务。因此，本章的服务测试还不能代表已部署端点的行为。

## 确定不变量需要哪些状态 {#aggregate-invariant}

第 34 章的纯决策器只看到一项 `BookingState`。它能判定：总容量为四的活动无法接受五个座位的请求。但它无法判断两个各占两席的预约能否共同放入容量三，因为任一预约状态都不包含另一项。

这不是 `Booking.create` 的缺陷，而是暴露了两个不同的不变量：

| 不变量 | 所需状态 | 执行位置 |
|---|---|---|
| 单个请求的座位数为正且不超过总容量 | 活动加一个命令 | 领域构造器与决策器 |
| 所有活跃预约与进行中预留共同不超过容量 | 该活动的每项预约与预留 | 聚合一致性边界 |

若不向单项预约决策器提供聚合状态，却把第二项检查加入其中，这个函数仍然无法正确判断。只有在能够读取和提交某条规则所需全部事实的位置，才能执行该规则。

## 先复现超售，再修复它 {#overselling-race}

假设容量为三，两个调用方各请求两个座位：

| 步骤 | 请求 A | 请求 B | 已存占用座位 |
|---|---|---|---|
| 1 | 读取 `0` | | `0` |
| 2 | | 读取 `0` | `0` |
| 3 | 计算 `0 + 2 <= 3` | | `0` |
| 4 | | 计算 `0 + 2 <= 3` | `0` |
| 5 | 写入两个座位 | | `2` |
| 6 | | 写入两个座位 | `4` |

两项检查各自在局部都正确，其组合却错误，因为“读取已占座位、决策、写入接受状态”不是一个串行化或条件式操作。

线程安全字典无法修复这个序列。单独安全的 `get` 与 `set` 调用仍可包围一个基于旧值的决策。只在写文件时加锁也为时已晚：两个调用方早已根据同一份旧状态接受请求。

最小原子区域包括：

```text
加载所有与容量有关的状态
  -> 计算已占用与已预留座位
  -> 运行纯命令决策
  -> 拒绝，或持久化已接受的预留/状态迁移
```

这里的“原子”表示其他协作工作流无法观察中间状态并提交竞争决策。之后的外部副作用并不属于同一事务。

## 先定义座位计算，再选择实现工具 {#seat-accounting}

一致性边界采用一条明确策略：

```text
occupied = Pending 预约的座位 + Confirmed 预约的座位
reserved = 处于 Reserved 或 PaymentStarted 的放置操作座位
必要不变量：occupied + reserved <= 活动容量
```

`Cancelled` 预约占用零座。确认改变状态，但不改变占用。放置预留会在支付前占住座位，因此缓慢支付不会让另一请求取得同一份容量。已记录的拒付不再占座。

这是业务选择，不是通用票务规则。另一个系统也许会把座位保留至某个期限、支付过期后释放、单独管理无障碍库存，或只在确认后计算占用。这类变化应进入明确的策略及其测试，而不是换一种信号量 API。

容量拒绝本身不会被持久化为终态幂等结果。若另一项预约取消，同一个等待中的放置请求可以再次尝试并成功。相反，提供商拒绝支付会作为该放置身份的终态持久化。这个差异是有意的：可用容量可能改变；而本例没有新的支付方式输入可供重新考虑拒付。

## 存储活动聚合，而不是单项预约 {#aggregate-snapshot}

早先的 `FileBookingStore` 只保存一个 `BookingDto`；保存另一请求会覆盖它。本章引入独立的版本化快照，其中包含：

- 活动 ID 与配置容量；
- 以规范化请求 ID 为键的每项当前预约；
- 每个命令的种类、请求 ID、载荷指纹、进度阶段与候选预约。

只用于持久化的 CLR DTO 仍与受保护领域类型分离。加载时，`BookingMapping` 先把严格 JSON 映回领域值。随后再拒绝重复键、活动不匹配、不可能的阶段/种类组合、过大座位数、损坏的操作链接、同一请求的多个未完成操作以及聚合超售。

已持久化的活动 ID 与容量必须匹配进程提供的活动。用不同配置重启会产生 `SnapshotActivityMismatch`；若在新容量下默默解释旧预约，恢复看似成功，实际却改变了不变量。

快照上限为 1 MiB，并按严格 UTF-8 读取。这足以承载教学负载，但不是无限生产数据库。保留、归档、schema 1 之后的迁移、备份、加密与防篡改仍是明确未实现项。

## 用进程内信号量保护完整决策 {#process-local-gate}

针对同一规范化路径构造的每个 `AtomicBookingStore` 都会取得共享的状态信号量和工作流信号量。只有大小写不同的路径也会保守地共享信号量。工作流信号量保护完整应用命令；状态信号量保护每次快照读取或替换。

```fsharp:line-numbers [AtomicBookingStore.fs]
// These gates coordinate every store instance for the same path in this process. They do not
// claim to serialize writers in different processes or machines.
module private AtomicPathGates =
    // Treat case-only variants conservatively as one path. On a case-sensitive file system this
    // may serialize unrelated files, but it cannot weaken consistency for either file.
    let private stateGates =
        ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.OrdinalIgnoreCase)

    let private workflowGates =
        ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.OrdinalIgnoreCase)

    let state (path: string) =
        stateGates.GetOrAdd(path, fun _ -> new SemaphoreSlim(1, 1))

    let workflow (path: string) =
        workflowGates.GetOrAdd(path, fun _ -> new SemaphoreSlim(1, 1))
```
`WaitAsync cancellationToken` 让已取消调用方可在等待时退出。`finally` 保证每次成功进入都配对 `Release`。Microsoft 把 `SemaphoreSlim` 说明为用于单个应用内部同步的本地信号量，并明确说明它不支持命名系统信号量。这正是已实现范围，而非隐藏的分布式锁。参见 [.NET 10 `SemaphoreSlim` 文档](https://learn.microsoft.com/en-us/dotnet/api/system.threading.semaphoreslim?view=net-10.0)。

在支付或通知运行期间持有工作流信号量是保守选择。它既阻止本进程的两个服务实例并发启动同一外部操作，也让示例易于理解；代价是一个缓慢依赖会阻塞该活动的无关预约。`SemaphoreSlim` 不承诺 FIFO 公平性。

对于小型本地应用，这是范围明确的取舍。若需高吞吐量，应按活动分区、使用数据库条件更新，并把投递工作移出请求路径。不要先删掉协调，再期待测试碰巧通过。

聚合决策与第一个持久阶段在状态信号量保护下发生：

```fsharp:line-numbers [AtomicBookingStore.fs]
match identity.Kind with
| AtomicOperationKind.Place ->
    let requested = candidate |> Booking.seats |> SeatCount.value |> int64

    let remaining =
        (state.Capacity |> Capacity.value |> int64)
        - AtomicStoreImplementation.occupiedSeats state
        - AtomicStoreImplementation.reservedSeats state
        |> max 0L

    if requested > remaining then
        return
            Ok(AtomicBeginResult.AggregateCapacityExceeded(int requested, int remaining))
    else
        let operation: StoredOperation =
            { Identity = identity
              Phase = Reserved
              Candidate = candidate }

        let changed =
            { state with
                Operations = Map.add identity.Key operation state.Operations }

        let! saved =
            AtomicStoreImplementation.writeState
                directoryPath
                snapshotPath
                changed
                cancellationToken

        return saved |> Result.map (fun () -> AtomicBeginResult.StartPayment token)
| AtomicOperationKind.Confirm
| AtomicOperationKind.Cancel ->
    let operation: StoredOperation =
        { Identity = identity
          Phase = NotificationPending
          Candidate = candidate }

    let changed =
        { state with
            Bookings =
                Map.add (RequestId.value identity.RequestId) candidate state.Bookings
            Operations = Map.add identity.Key operation state.Operations }

    let! saved =
        AtomicStoreImplementation.writeState
            directoryPath
            snapshotPath
            changed
            cancellationToken

    return saved |> Result.map (fun () -> AtomicBeginResult.SendNotification token)
```
领域决策器仍负责合法生命周期迁移。存储只补入单项预约状态无法知道的聚合事实。被接受的放置操作先记录预留；确认与取消则一起更新预约并记录待发送通知。

## 不要把安全替换误认为数据库事务 {#file-replacement}

写入器先序列化完整 DTO 并检查大小上限，再在目标目录创建随机临时文件。它以 `WriteThrough` 写入，调用 `Flush(true)`，最后执行 `File.Move(temp, destination, true)`。

Microsoft 文档说明，[`Flush(true)` 会清除中间文件缓冲区](https://learn.microsoft.com/en-us/dotnet/api/system.io.filestream.flush?view=net-10.0)，而 [`File.Move` 的 `overwrite = true` 会替换已有目标](https://learn.microsoft.com/en-us/dotnet/api/system.io.file.move?view=net-10.0)。把临时文件留在同一目录也避开文档所述的跨卷复制行为。

这些 API 行为不能保证所有文件系统、断电、目录元数据失败、网络共享、防病毒挂钩或硬件缓存下的 ACID 持久性。测试只确认受测环境中的整体替换与正常重启恢复，没有模拟断电。生产持久性承诺还需针对真实文件系统、挂载、存储、备份与恢复进行验证。

在本进程内，协作读取方使用同一状态信号量，所以不会解析临时文件，也不会在替换期间读取。第二个操作系统进程不共享该信号量，因此仍可能发生竞争。类的 XML 文档直接说明了这个限制。

## 为操作提供稳定身份 {#idempotency-identity}

请求 ID 标识一项预约，但同一预约会合法地收到放置、确认与取消命令。因此，一致性设计以如下组合定义操作键：

```text
操作种类 + 规范化请求 ID
```

它另外对一个带长度前缀的规范序列计算散列；序列包含种类、规范化 ID 与规范化命令载荷。领域验证先执行去除首尾空白，再计算指纹。因此 `" REQ-7 "` 与 `"REQ-7"` 标识同一放置操作；一个座位与两个座位则不同。

SHA-256 指纹只是紧凑的相等性令牌，不是密码散列、签名或授权决策。不能因指纹看似有效就信任快照；所有持久字段仍会被解析和检查。

由此得到明确策略：

| 传入命令 | 已存证据 | 结果 |
|---|---|---|
| 种类、规范化 ID 与载荷相同；阶段为 `Completed` | 原操作的已确认结果 | 重放已存候选项；不支付、不通知 |
| 种类与 ID 相同，载荷不同 | 键存在但指纹不同 | `IdempotencyConflict`；无副作用 |
| 一项预约完成后收到不同种类命令 | 独立操作键 | 运行下一个合法生命周期决策 |
| 前一操作未完成时收到不同种类命令 | 存在未完成进度 | `PreviousOperationIncomplete` |
| 无效命令 | 无法形成受保护身份 | 领域验证错误；不访问存储 |
| 当前没有聚合容量 | 无终态记录 | 此次拒绝；之后重试可重新求值 |

安全的幂等身份由两部分组成：稳定操作键为服务器提供查找进度的持久地址，载荷指纹则能识别同一键搭配不同座位数的冲突。重放旧结果前应同时核对两者。

## 区分 HTTP 方法语义与应用幂等 {#http-idempotency}

RFC 9110 根据重复相同请求对服务器的预期作用来定义幂等 HTTP 方法。安全方法、`PUT` 与 `DELETE` 属于幂等方法，`POST` 则不天然幂等。除非客户端知道请求语义幂等，或确认原请求未生效，否则不应自动重试非幂等请求。参见 [HTTP Semantics 第 9.2.2 节](https://www.rfc-editor.org/rfc/rfc9110.html#section-9.2.2)。

已存身份规则让一个应用命令在操作键和载荷指纹完全一致时可安全重试。HTTP 方法语义与中间代理的重试权限仍是独立策略。最终 HTTP 边界必须暴露复用键冲突与支付结果不明确，让客户端可以有意识地行动。

Microsoft 的[重试模式](https://learn.microsoft.com/en-us/azure/architecture/patterns/retry)在运维层面作出相同区分：对故障分类、限制尝试次数，并在重复前询问操作是否幂等。“收到 `503`”并不足以判断能否安全地再次扣款。

## 围绕外部副作用持久化进度 {#effect-progress}

同一命令会经过以下持久阶段：

```text
Place:
  Reserved -> PaymentStarted -> NotificationPending -> Completed
                              \-> PaymentDeclined

Confirm / Cancel:
  NotificationPending -> Completed
```

阶段名称描述已经确认的事实，而非乐观愿望：

- `Reserved`：容量已保留；此工作流确定尚未开始支付；
- `PaymentStarted`：提供商可能已经行动，所以中断后的结果未知；
- `PaymentDeclined`：提供商拒绝已记录，之后会重放拒绝而不扣款；
- `NotificationPending`：预约状态已提交，通知仍可投递；
- `Completed`：所建模的副作用已成功返回，且完成状态已持久化。

服务据此安排存储与副作用顺序：

```fsharp:line-numbers [Idempotency.fs]
let sendNotification
    (token: AtomicOperationToken)
    (cancellationToken: CancellationToken)
    : Task<Result<Booking, BookingConsistencyError>> =
    task {
        let! delivered = tryExternal cancellationToken (fun () -> notify (notificationFor token) cancellationToken)

        match delivered with
        | Error error -> return Error error
        | Ok() ->
            let! completed = store.CompleteNotification(activity, token, cancellationToken)

            return completed |> storage |> Result.map (fun () -> token.Candidate)
    }

let chargeAndCommit
    (token: AtomicOperationToken)
    (payment: PaymentRequest)
    (cancellationToken: CancellationToken)
    : Task<Result<Booking, BookingConsistencyError>> =
    task {
        let! marked = store.MarkPaymentStarted(activity, token, cancellationToken)

        match storage marked with
        | Error error -> return Error error
        | Ok() ->
            let! paymentResult = tryExternal cancellationToken (fun () -> charge payment cancellationToken)

            match paymentResult with
            | Error error -> return Error error
            | Ok(PaymentOutcome.Declined _) ->
                let! recorded = store.RecordPaymentDeclined(activity, token, cancellationToken)

                return
                    match storage recorded with
                    | Error error -> Error error
                    | Ok() -> Error BookingConsistencyError.PaymentDeclined
            | Ok(PaymentOutcome.Authorized _) ->
                let! committed = store.CommitAuthorizedBooking(activity, token, cancellationToken)

                match storage committed with
                | Error error -> return Error error
                | Ok() -> return! sendNotification token cancellationToken
    }

let executePrepared
    (prepared: PreparedCommand)
    (cancellationToken: CancellationToken)
    : Task<Result<Booking, BookingConsistencyError>> =
    task {
        let! begun = store.Begin(activity, prepared.Identity, prepared.Command, cancellationToken)

        match storage begun with
        | Error error -> return Error error
        | Ok(AtomicBeginResult.Replay booking) -> return Ok booking
        | Ok(AtomicBeginResult.DecisionRejected error) ->
            return Error(BookingConsistencyError.DecisionRejected error)
        | Ok(AtomicBeginResult.AggregateCapacityExceeded(requested, remaining)) ->
            return Error(BookingConsistencyError.AggregateCapacityExceeded(requested, remaining))
        | Ok AtomicBeginResult.IdempotencyConflict -> return Error BookingConsistencyError.IdempotencyConflict
        | Ok AtomicBeginResult.PreviousOperationIncomplete ->
            return Error BookingConsistencyError.PreviousOperationIncomplete
        | Ok AtomicBeginResult.PaymentDeclined -> return Error BookingConsistencyError.PaymentDeclined
        | Ok AtomicBeginResult.PaymentOutcomeUnknown -> return Error BookingConsistencyError.PaymentOutcomeUnknown
        | Ok(AtomicBeginResult.SendNotification token) -> return! sendNotification token cancellationToken
        | Ok(AtomicBeginResult.StartPayment token) ->
            match prepared.Payment with
            | Some payment -> return! chargeAndCommit token payment cancellationToken
            | None ->
                return
                    Error(
                        BookingConsistencyError.StorageUnavailable(
                            BookingStoreError.CorruptSnapshot SnapshotCorruption.InconsistentData
                        )
                    )
    }
```
调用支付前，服务把 `Reserved` 改为 `PaymentStarted`。若在该写入之后、提供商调用之前崩溃，即使没有扣款，恢复时也会保守地报告“未知”。另一种顺序——先调用、后记录——会产生一个窗口：已经完成的扣款看似不存在，于是被盲目重复。对于金钱，停下来对账是本例更安全的策略。

授权返回后，预约与 `NotificationPending` 阶段通过一次聚合替换共同保存。因此通知失败不会抹去预约。重试同一命令会跳过支付，只尝试待发送通知。

通知返回后，服务持久化 `Completed`。如果投递成功但确认丢失、进程崩溃，或最后一次写入失败，快照仍可能显示待发送。重试可能再次通知。这就是至少一次投递。

## 根据持久记录决定如何重试 {#retry-matrix}

“重试”一词掩盖了多种不同迁移：

| 最后持久状态 | 首次观察到的失败 | 确定的重试行为 | 副作用策略 |
|---|---|---|---|
| 无操作 | 验证或领域拒绝 | 重新决策 | 没有副作用发生 |
| 无操作 | 聚合容量拒绝 | 重新求值当前容量 | 取消后可能成功 |
| `Reserved` | 支付开始前取消 | 启动一次支付 | 预留仍可安全恢复 |
| `PaymentStarted` | 支付调用故障、取消或崩溃 | `PaymentOutcomeUnknown` | 不自动再次扣款 |
| `PaymentDeclined` | 预期拒付 | 重放拒绝 | 不再次扣款 |
| `NotificationPending` | 提交后通知故障 | 只重试通知 | 不重复支付和预约提交 |
| `Completed` | 完成后响应丢失 | 重放已存预约 | 不重复任何已建模副作用 |

第一次支付异常返回 `DependencyUnavailable`，因为该次调用失败。下一次完全相同的尝试看到 `PaymentStarted`，因此返回 `PaymentOutcomeUnknown`。两次观察不同，是因为调用前持久知识已经改变。

操作员或提供商查询必须解决未知支付。真实工作流可在用提供商幂等键查询后记录 `Authorized`、`Declined` 或 `Released`。这个教学示例不会编造答案，也不会自动释放座位。

## 理解为何恰好一次不是本地属性 {#exactly-once}

快照文件与支付提供商是两个独立系统。任何本地 `SemaphoreSlim`、散列或 `File.Move` 都无法把两者作为一个事务提交。文件与电子邮件或消息服务也是如此。

缩小缺口只有这些明确可行的做法：

- 向提供商传递稳定幂等键，让它持久去重请求；
- 决定一个歧义操作能否继续前，按该键查询提供商；
- 在一个真正的存储事务中保存业务状态与发件箱条目；
- 让独立中继程序重试发件箱条目；
- 给每个消费者稳定消息 ID 与幂等处理程序；
- 当副作用无法安全重复时定义补偿。

Microsoft 的[事务性发件箱指南](https://learn.microsoft.com/en-us/azure/architecture/databases/guide/transactional-out-box-cosmos)在同一数据库事务中存储业务对象与事件，再由另一进程发布待处理事件。它也讨论了重放时的重复发布与下游重复检测。事务防止本地意图丢失，却不会把远端发送变成同一事务。

把 `NotificationPending` 与预约一同存储，类似一个微型内联发件箱。它并非完整发件箱：没有独立工作线程、租约、退避、死信策略、顺序策略或保留清理。把它称为完整发件箱会夸大实现。

## 从正常重启中恢复 {#restart-recovery}

每一项影响决策的值都会留在聚合快照中。新进程无需依赖内存缓存，就能重建领域预约、命令阶段、容量计算，并原样重放结果。

重启测试不只是构造第二个对象。它先完成放置操作，读取 JSON 以确认 schema 为 1 且没有支付交易文本，再启动独立的 `dotnet fsi` 进程。该进程加载已构建程序集与同一快照，提供一旦调用就会失败的支付和通知函数，然后重复该放置操作。

子进程输出：

```text
restored|REQ-RESTART|2|pending
```

退出码为零表明，持久化完成状态已被重放，支付和通知都没有再次运行。父进程随后确认，原先替身仍各只有一次调用。

这只验证正常重启。它不覆盖多进程并发写入、每个指令级崩溃点或磁盘丢失；这些目标需要不同的存储与故障注入测试。

## 用表格陈述保证 {#guarantee-table}

| 问题 | 当前答案 |
|---|---|
| 一个进程内的两个受控命令能否让一个活动超售？ | 不能，前提是使用 `IdempotentBookingService` 与同一配置路径 |
| 待处理与已确认预约是否占座？ | 是 |
| 取消是否释放座位？ | 是，在其预约迁移提交时 |
| 完全相同的已完成重试会再次扣款或通知吗？ | 不会 |
| 相同操作键会接受不同内容吗？ | 不会；它产生冲突 |
| 结果不明的支付会自动重试吗？ | 不会；它需要对账 |
| 失败通知能否重试？ | 可以，且不重复支付或预约提交 |
| 确认结果不明确时，通知能否投递多次？ | 可以 |
| 状态能否在匹配活动配置的新进程中存续？ | 可以，在受测的正常重启场景中 |
| 两个操作系统进程或容器能否安全并发写文件？ | 不能 |
| 快照是 ACID、复制、加密且已备份的数据库吗？ | 不是 |
| 第 36 章 HTTP 端点是否已使用此服务？ | 尚未；最终集成在第 38 章 |

收窄措辞本身就是正确性的一部分。除非说明范围、状态、失败与观察者，否则“线程安全”“原子”“持久”“幂等”都是不完整的承诺。

## 用因果控制而非固定等待来测试竞态 {#deterministic-tests}

两个竞争测试都先创建任务，让每个任务报告就绪，并用 `TaskCompletionSource` 将它们挡住。只有两者都就绪后，测试才释放它们。断言不依赖哪个请求获胜。

当容量为三、两个请求各需两个座位时，必要结果为：

- 恰好一个 `Ok booking`；
- 恰好一个 `AggregateCapacityExceeded(2, 1)`；
- 一次支付与一次通知调用；
- 持久化占用座位合计为二。

重复请求测试同时释放同一命令的两个规范化形式。两者都成功，但计数仍为一次支付与一次通知。把同一操作键用于不同座位数会得到 `IdempotencyConflict`，且两个计数都不变。

其他测试表明，通知失败后预约仍会提交，重试时只发送通知。它们还覆盖结果未知时不重复扣款、取消后释放容量，以及独立进程重放已完成结果。

聚焦测试通过信号控制操作顺序，不使用计时休眠。它不能覆盖所有调度，但比在 `Task.Delay(50)` 后假设某个分支通常获胜更可靠。

## 根据需求选择生产边界 {#production-upgrades}

本地设计有意保持串行且易于检查。当需求跨越它的边界时，再升级机制：

| 需求 | 候选机制 | 仍需的证据 |
|---|---|---|
| 多个 API 进程写入一个活动 | 使用行/键范围锁的数据库事务，或乐观版本/ETag 加重试 | 冲突写入无法同时提交 |
| 大量独立活动 | 按活动 ID 分区/加锁 | 热点键行为与跨活动隔离 |
| 可靠的延迟通知 | 事务性发件箱加工作线程 | 中继恢复、重复处理、顺序、保留 |
| 超时后重试支付 | 提供商支持的幂等键与查询 | 提供商的具体语义与保留窗口 |
| 不允许无限期预留 | 由已存时间和对账策略驱动的过期命令 | 时钟、竞态、支付查询与释放测试 |
| 区域级持久性 | 复制式托管存储与受测备份/恢复 | 明确一致性级别、RPO、RTO 与故障切换演练 |

冲突罕见时，乐观并发通常比进程级锁更合适。条件写入表示“只在我读取的版本仍是当前版本时提交”；失败方重新加载并再次决策。不变量依旧要求把所有容量相关状态放在一个事务或条件边界内。

Microsoft 的[最小化协调指南](https://learn.microsoft.com/en-us/azure/architecture/guide/design-principles/minimize-coordination)建议尽可能使用幂等操作与乐观并发，同时也承认某些不变量需要协调。“最小化”并不等于“删除”。选择更复杂设计前，应先测量争用。

## 看清 F# 能贡献什么，也看清它不能做什么 {#fsharp-role}

F# 可以直接表达进度与失败状态。`AtomicOperationPhase` 避免在编排中使用含义不明的字符串；`BookingConsistencyError` 要求调用方区分容量、冲突、拒付、支付未知、依赖失败与存储失败。模式匹配展示策略分支，受保护领域构造器则继续负责从持久数据还原领域值。

`Result` 把已声明结果与故障分开。`task` 组合文件和依赖工作，同时保留取消。记录类型便于在各阶段携带不可变候选项。这些语言特性都不能提供跨两个系统的事务。

真正有用的 F# 经验不是“函数式编程解决并发”，而是定义明确的数据类型能让代码命名已知事实，纯决策则缩小了需要协调的区域。剩余协调仍必须匹配部署拓扑。

## 练习 {#exercises}

### 练习 1：跨越进程边界 {#exercise-01}

API 必须以三个副本运行，每个活动各有独立容量。用能防止超售的存储设计替换本地信号量与快照。说明聚合键、持久版本、条件或事务式写入、冲突重试循环，以及取消如何影响重试。说明一个热点活动能否阻塞无关活动，并命名一项强制两个副本使用同一版本的测试。


::: details 参考答案

#### 让活动成为并发键 {#exercise-01-key}

使用规范化活动 ID 作为聚合键或分区键。一个持久聚合包含：

- 活动 ID、容量，以及单调变化的版本或 ETag；
- 所有会计入占用的预约状态；
- 所有会计入占用的未完成预留；
- 每个命令的操作键、载荷指纹、阶段与重放结果。

这些信息与当前一致性设计相同，但现在必须由存储引擎拒绝旧版本提交，不能依赖进程内信号量。关系型设计可以锁住一行活动记录，并在一个事务内更新相关行。键值或文档设计可以只在 ETag 匹配时条件更新活动文档。

按活动 ID 分区意味着一个热点活动可以与自身争用，而不阻塞无关活动。只有不存在全局表锁、单例工作线程或串行化所有分区的共享事务时，这项收益才成立。跨多个活动的预约会越过此边界，需要不同模型。

#### 版本冲突后重新决策 {#exercise-01-loop}

概念循环如下：

```fsharp
let rec execute remaining cancellationToken = task {
    cancellationToken.ThrowIfCancellationRequested()
    let! snapshot, version = store.Load(eventId, cancellationToken)
    let decision = decideAgainstAggregate command snapshot

    match decision with
    | Error error -> return Error error
    | Ok nextSnapshot ->
        match! store.TryReplace(eventId, version, nextSnapshot, cancellationToken) with
        | Written -> return Ok nextSnapshot
        | VersionConflict when remaining > 0 ->
            return! execute (remaining - 1) cancellationToken
        | VersionConflict -> return Error ContentionLimitExceeded
}
```

重试要基于新加载的状态重新运行纯决策，不能再次提交同一个过期写入。若竞争命令用掉最后一个座位，第二次决策会返回容量拒绝；若它取消了一项占座预约，第二次决策可能接受等待中的预约请求。

限制尝试次数并检查取消令牌。只有反复冲突值得处理时，才加入带抖动的退避。存储超时、身份验证失败、损坏数据与领域拒绝都不是版本冲突。[重试模式](https://learn.microsoft.com/en-us/azure/architecture/patterns/retry)建议先分类故障，不要重试所有失败。

容量预留通过条件提交之前，不得调用支付。用生产存储替换本地存储后，本章定义的外部操作阶段与提供商键仍然必需。

#### 强制两个写入方使用同一版本 {#exercise-01-test}

集成测试应针对同一个真实存储分区启动两个独立服务宿主。测试存储挂钩中的屏障让两者都在读取版本 12 后暂停。一起释放两个条件写入，并断言：

- 使用预期版本 12 的写入恰好只有一个提交；
- 失败方读取版本 13 并重新决策；
- 已提交状态从不超过容量；
- 没有剩余容量时，只有获胜方开始支付；
- 若策略允许，取消一项已有预约会让失败方在下一次决策成功；
- 两个不同活动 ID 的并发命令都能推进。

不要用同一进程内的两个对象代替存储引擎并发测试。它们可能意外共享锁，根本不会触发生产存储的并发冲突机制。

:::

### 练习 2：对账未知支付 {#exercise-02}

不编写代码，扩展进度模型。加入提供商键，以及操作员或后台任务查询不明确支付所需的最少状态。定义提供商返回 `Authorized`、`Declined` 与 `NotFound` 时的迁移。决定座位何时继续预留、何时释放，以及哪些迁移可以发送通知。包括命令载荷改变时的冲突规则。


::: details 参考答案

#### 单独建模提供商状态 {#exercise-02-model}

加入由操作身份派生的稳定提供商幂等键，并在任何提供商调用前持久化。一个有用的最小模型是：

| 本地阶段 | 持久知识 | 座位策略 |
|---|---|---|
| `Reserved` | 尚未开始任何提供商尝试 | 预留 |
| `PaymentStarted` | 请求可能已经抵达提供商 | 预留 |
| `PaymentUnknown` | 调用结束，但结果未知，需要查询 | 预留 |
| `Authorized` | 提供商确认授权 | 作为预约占用 |
| `Declined` | 提供商确认拒绝 | 释放 |
| `Released` | 策略或操作员完成安全释放 | 释放 |
| `NotificationPending` | 预约与本地投递意图均已提交 | 作为预约占用 |
| `Completed` | 所建模投递已确认，结果已存储 | 作为预约占用 |

当调用故障、超时、取消，或恢复发现它未完成时，`PaymentStarted` 可以变为 `PaymentUnknown`。若前者是短期执行标记、后者会安排对账，保留两种状态很有用；若行为没有差异，也可以合并。

#### 让查询结果驱动迁移 {#exercise-02-transitions}

通过稳定键查询提供商。响应不能直接修改本地状态，而要交给受约束的状态转换：

| 提供商报告 | 允许的本地动作 | 通知 |
|---|---|---|
| `Authorized(providerTransactionId)` | 持久化提供商引用，把已预留候选项提交为预约，并原子保存 `NotificationPending` | 只能在该提交后运行 |
| `Declined(reasonCode)` | 持久化终态拒绝并释放预留 | 无 |
| 提供商文档所述最终确定窗口之前的 `NotFound` | 保持 `PaymentUnknown`；安排再次查询 | 无 |
| 文档所述最终确定/保留阈值之后的 `NotFound` | 遵循明确的释放或人工审阅策略 | 安全终态决策前不通知 |
| 传输或提供商失败 | 保持 `PaymentUnknown`；按有界策略重试查询 | 无 |

`NotFound` 不会自动等于“从未扣款”。请求可能仍在传输中、稍后才建立索引、只在另一 API 中可见，或已超出提供商查询保留期。集成必须使用提供商记录的语义，而不是方便的猜测。

每个对账命令都携带原始操作键与已存指纹。请求 ID、活动 ID、座位数或其他影响决策的载荷一旦改变，就产生 `IdempotencyConflict`；对账绝不会修改原命令以迎合查询。

只有一个受约束的转换可以把预留变成预约。重复的 `Authorized` 回调或操作员重试只会重放该状态。迟到且矛盾的提供商报告属于需要审阅的运维异常，不能据此执行两个相互冲突的转换。

:::

### 练习 3：把待发送通知变成发件箱 {#exercise-03}

为预约通知设计真正的发件箱。说明与预约共同保存的内容、工作线程如何认领工作、如何记录重试与退避、稳定消息 ID 如何抵达消费者、消费者如何去重，以及超过重试上限后发生什么。区分“不丢失本地意图”“至少一次发布”与“相同的消费者可观察结果”。


::: details 参考答案

#### 一起提交业务状态与意图 {#exercise-03-commit}

在提交预约迁移的同一个数据库事务内，插入类似这样的发件箱行：

```text
messageId, eventId, requestId, operationKind,
messageType, schemaVersion, payload,
status, attemptCount, nextAttemptAt,
leaseOwner, leaseUntil, createdAt, completedAt
```

每次发布尝试都使用稳定的 `messageId`。载荷是带版本的集成契约，而不是序列化后的私有 F# 领域对象。操作/消息唯一约束会阻止请求事务插入两条逻辑通知。

若事务回滚，预约变化与投递意图都不存在；若事务提交，两者都存在。这就是发件箱的核心保证：这两条本地记录之间不会丢失意图。

#### 租用、发送并确认 {#exercise-03-relay}

中继程序执行一种可恢复协议：

1. 选择 `nextAttemptAt` 已到的合格待处理行。
2. 用条件更新与有界租约认领每行。
3. 使用稳定 `messageId` 发布带版本的载荷。
4. 成功确认后，以条件方式把该行标记为完成。
5. 遇到经分类的瞬时失败时，增加 `attemptCount`、记录已脱敏诊断，并安排带抖动的有界指数退避。
6. 遇到永久失败或策略耗尽时，移至死信/审阅状态，并通知负责人员。
7. 工作线程崩溃后，重新认领过期租约。

保持数据库事务短暂；不要跨消息代理调用持有事务。这个选择会产生不可避免的崩溃窗口：

| 崩溃点 | 持久行 | 恢复结果 |
|---|---|---|
| 认领提交前 | 待处理 | 另一工作线程可认领 |
| 认领后、发布前 | 已租用 | 租约过期后重试发布 |
| 发布后、更新完成前 | 已租用，或之后再次待处理 | 同一消息可能再次发布 |
| 更新完成后 | 已完成 | 正常扫描会跳过 |

表中第三种崩溃会导致至少一次投递。消息代理端去重可以减少重复，但必须验证键的作用范围与保留窗口；它不能保证不加限定的恰好一次投递。

#### 在消费者边界去重 {#exercise-03-consumer}

每个消费者都要在其自身状态变化的同一本地事务中保存已处理 `messageId`。收到消息时：

- 若 ID 是新的，应用处理程序并原子记录 ID；
- 若 ID 已存在，不再次应用状态变化，直接确认；
- 若任一本地写入失败，不确认，让重新投递能一起重试两者。

保留期必须覆盖消息代理最长重投与重放期限。处理程序也应优先采用天然幂等的状态赋值，不要累加副作用。电子邮件、第三方 webhook 或物理动作会引入新的独立边界，各自需要键、查询或对账策略。

由此得到刻意区分的承诺：

| 承诺 | 该设计能保证什么 |
|---|---|
| 不丢失本地意图 | 预约变化与发件箱行一起提交或回滚 |
| 至少一次发布尝试 | 在存储与中继保持可用时，未完成行保持可恢复、可重试 |
| 相同的消费者本地结果 | 消费者按稳定消息 ID 原子去重 |
| 在每个外部系统都恰好一次 | 不保证 |

监控待处理时长、尝试次数、过期租约、死信量、端到端延迟与重复率。若消费者需要顺序，应按聚合定义；在重放期限后归档已完成行；并在崩溃表的每一行测试工作线程死亡。

Microsoft 的[事务性发件箱指南](https://learn.microsoft.com/en-us/azure/architecture/databases/guide/transactional-out-box-cosmos)同样把本地事务与之后的发布分开，并指出重复处理问题。具体 schema 与租约机制仍取决于数据库。

:::


第 38 章会通过 HTTP 接入此服务，加入 C# 契约客户端与端到端测试，再完成诊断与发布检查。
