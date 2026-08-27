---
title: "第 37 章答案"
description: "把容量控制扩展到多个进程，对账结果不明的支付，并设计不宣称恰好一次投递的发件箱。"
translationKey: solutions/ch-37-consistency-idempotency
---

# 第 37 章答案 {#overview}

这些是设计，不是可直接投入生产的配方。每个答案都先说明不变量及执行它所需的持久状态，再选择协调机制。提供商、数据库与消息代理的契约仍需针对实际部署产品验证。

[返回第 37 章](../part-06/ch-37-consistency-idempotency)。

## 练习 1：跨越进程边界 {#exercise-01}

### 让活动成为并发键 {#exercise-01-key}

使用规范化活动 ID 作为聚合键或分区键。一个持久聚合包含：

- 活动 ID、容量，以及单调变化的版本或 ETag；
- 所有会计入占用的预约状态；
- 所有会计入占用的未完成预留；
- 每个命令的操作键、载荷指纹、阶段与重放结果。

这些信息与当前一致性设计相同，但现在必须由存储引擎拒绝旧版本提交，不能依赖进程内信号量。关系型设计可以锁住一行活动记录，并在一个事务内更新相关行。键值或文档设计可以只在 ETag 匹配时条件更新活动文档。

按活动 ID 分区意味着一个热点活动可以与自身争用，而不阻塞无关活动。只有不存在全局表锁、单例工作线程或串行化所有分区的共享事务时，这项收益才成立。跨多个活动的预约会越过此边界，需要不同模型。

### 版本冲突后重新决策 {#exercise-01-loop}

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

### 强制两个写入方使用同一版本 {#exercise-01-test}

集成测试应针对同一个真实存储分区启动两个独立服务宿主。测试存储挂钩中的屏障让两者都在读取版本 12 后暂停。一起释放两个条件写入，并断言：

- 使用预期版本 12 的写入恰好只有一个提交；
- 失败方读取版本 13 并重新决策；
- 已提交状态从不超过容量；
- 没有剩余容量时，只有获胜方开始支付；
- 若策略允许，取消一项已有预约会让失败方在下一次决策成功；
- 两个不同活动 ID 的并发命令都能推进。

不要用同一进程内的两个对象代替存储引擎并发测试。它们可能意外共享锁，根本不会触发生产存储的并发冲突机制。

## 练习 2：对账未知支付 {#exercise-02}

### 单独建模提供商状态 {#exercise-02-model}

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

### 让查询结果驱动迁移 {#exercise-02-transitions}

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

## 练习 3：把待发送通知变成发件箱 {#exercise-03}

### 一起提交业务状态与意图 {#exercise-03-commit}

在提交预约迁移的同一个数据库事务内，插入类似这样的发件箱行：

```text
messageId, eventId, requestId, operationKind,
messageType, schemaVersion, payload,
status, attemptCount, nextAttemptAt,
leaseOwner, leaseUntil, createdAt, completedAt
```

每次发布尝试都使用稳定的 `messageId`。载荷是带版本的集成契约，而不是序列化后的私有 F# 领域对象。操作/消息唯一约束会阻止请求事务插入两条逻辑通知。

若事务回滚，预约变化与投递意图都不存在；若事务提交，两者都存在。这就是发件箱的核心保证：这两条本地记录之间不会丢失意图。

### 租用、发送并确认 {#exercise-03-relay}

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

### 在消费者边界去重 {#exercise-03-consumer}

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

## 答案回顾 {#solution-review}

- 把每项容量相关事实置于一个聚合版本或事务之下。
- 条件写入失败方会重新加载并重新决策；它绝不提交旧决策。
- 只有存储路径维持分区时，按活动分区才会避免全局协调。
- 只有持久预留获胜后才运行支付。
- 结果不明的提供商调用会继续保留座位，直到查询返回可信终态。
- `NotFound` 只具有提供商契约明确承诺的含义。
- 对账保留原操作键与载荷指纹。
- 事务性发件箱防止本地状态与本地投递意图之间出现空隙。
- 若中继在发送后、本地确认前崩溃，它仍可能发布两次。
- 消费者端原子去重可让重复投递只产生一次本地结果。
- 稳定 ID、有界重试、租约、死信处理、保留与监控都是协议组成部分，不是可选装饰。
- 这些机制都不能单独保证跨越所有系统的恰好一次副作用。

## 来源 {#sources}

- [Microsoft Learn：重试模式](https://learn.microsoft.com/en-us/azure/architecture/patterns/retry)
- [Microsoft Learn：使用 Azure Cosmos DB 的事务性发件箱模式](https://learn.microsoft.com/en-us/azure/architecture/databases/guide/transactional-out-box-cosmos)
- [Microsoft Learn：最小化协调](https://learn.microsoft.com/en-us/azure/architecture/guide/design-principles/minimize-coordination)
