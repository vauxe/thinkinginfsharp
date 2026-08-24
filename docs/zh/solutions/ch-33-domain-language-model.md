---
title: "第 33 章练习答案"
description: "按角色分类预约值，在不跨越边界的情况下设计座位变更命令与事实，并依据明确保证选择持久化方式。"
translationKey: solutions/ch-33-domain-language-model
kind: solution
part: 6
chapter: 33
status: complete
verifiedWith:
  fsharp: "10"
  dotnetSdk: "10.0.301"
exampleIds:
  - capstone-booking-domain
exerciseIds:
  - ch33-exercise-01
  - ch33-exercise-02
  - ch33-exercise-03
termIds: []
sources:
  - id: microsoft-fsharp-records
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/records
    checked: "2026-08-24"
  - id: microsoft-fsharp-discriminated-unions
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/discriminated-unions
    checked: "2026-08-24"
  - id: microsoft-domain-events
    url: https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/domain-events-design-implementation
    checked: "2026-08-24"
  - id: microsoft-cqrs-pattern
    url: https://learn.microsoft.com/en-us/azure/architecture/patterns/cqrs
    checked: "2026-08-24"
  - id: microsoft-event-sourcing-pattern
    url: https://learn.microsoft.com/en-us/azure/architecture/patterns/event-sourcing
    checked: "2026-08-24"
---

# 第 33 章练习答案 {#overview}

这些答案按照值在何时有意义、谁有权创建它进行分类。建议的座位变更语言只是一份设计提案，并不是贯穿项目已经支持的代码。持久化方式取决于所需保证，而不是是否存在一个事件联合。

[返回第 33 章](../part-06/ch-33-domain-language-model)。

## 练习 1：按角色分类值 {#exercise-01}

### 先问这个值声称了什么 {#exercise-01-classification}

| 值 | 角色 | 正常创建者 | 构造它是否表示请求的预约已经发生？ |
|---|---|---|---|
| `PlaceBooking` | 原始命令 | 边界映射器或 `Commands.place` | 否；其基本类型字段可能无效，领域也可能拒绝它 |
| `ValidPlaceBooking` | 已验证命令 | 在独立字段检查后由 `validatePlaceBooking` 创建 | 否；重复标识或容量仍可能拒绝它 |
| `BookingPlaced booking` | 领域事件 | 正常由预约决策器在 `Booking.create` 成功后创建 | 就值的含义而言是；不过当前公开案例仍能重新包装既有的有效预约 |
| `Booked booking` | 领域状态 | `evolve` 或受信任的状态重建边界 | 它说明预约当前存在；并不声称某项新请求刚刚成功 |
| `PlaceBookingRequestDto` | 边界 DTO | JSON 序列化器/客户端或 HTTP 适配器 | 否；它仍是不可信的表示 |
| `AppendEvent` | 端口能力 | 组合根提供适配器函数 | 否；持有或调用这项能力都不是领域决策，调用也可能失败 |
| `RequestedSeatsExceedCapacity` | 领域错误 | 受保护座位数超过受保护容量时，由 `Booking.create` 创建 | 否；它说明预约尝试遭到拒绝 |

决定性的区别在于语法与时间。命令指向未来可能执行的工作。已验证命令排除了格式错误输入，但仍是在请求。事件使用过去式。状态说明现在什么为真。DTO 只说明什么跨越了表示边界。

`BookingPlaced` 这一行需要一项限定。正常产生路径赋予它强语义，因为 `Booking.create` 已保护载荷。但其联合案例仍是公开的，所以已经持有有效 `Booking` 的代码可以包装它。如果来源本身成为不变量，应通过签名或内部边界隐藏事件构造；不要声称当前类型已经证明来源。

`AppendEvent` 是一个值为函数的记录字段。端口承诺一项能力及其运维契约；它不会把判断预约是否合法的权力交给适配器。应用只在纯计算得到已接受结果后调用它。

## 练习 2：先扩展语言，再扩展代码 {#exercise-02}

### 先明确策略选择 {#exercise-02-policy}

一套自洽的词汇如下：

| 角色 | 建议名称 | 数据或含义 |
|---|---|---|
| 命令 | `ChangeBookingSeats` | 原始 `RequestId: string` 与 `Seats: int` |
| 已验证命令 | `ValidChangeBookingSeats` | 受保护的 `RequestId` 与 `SeatCount` |
| 已接受事件 | `BookingSeatCountChanged` | 转换后受保护的 `Booking` |
| 字段错误 | `BlankRequestId`、`NonPositiveSeatCount` | 彼此独立的格式错误字段 |
| 状态错误 | `BookingDoesNotExist`、`RequestIdDoesNotMatch`、`CannotChangeSeatsFrom`、`InsufficientCapacity`、`SeatCountUnchanged` | 需要预约或活动级状态才能判定的拒绝 |

命令使用祈使式动作，事件使用过去式事实。两者名称都不提 JSON、数据库列或 HTTP 动词。携带转换后的预约符合当前事件/演化设计，不过未来的持久事件模式也可能改为携带稳定请求标识与变更前后数量。

分别验证请求标识和座位数。这些检查不需要当前状态，可以同时累积两个错误。此时不要直接比较原始整数来检查容量：决策应使用受保护的 `SeatCount` 和受保护的活动 `Capacity`。

依赖状态的决策可以按以下顺序执行：

1. 对 `NotBooked` 返回 `BookingDoesNotExist`。
2. 对 `Booked booking`，要求命令请求标识等于 `Booking.requestId booking`。
3. 要求 `Booking.status booking` 是 `Pending`；对 `Confirmed` 与 `Cancelled` 返回 `CannotChangeSeatsFrom current`。
4. 使用活动级预留状态，要求替换后的数量能被可用容量容纳。仅凭 `Event.capacity` 和一个预约无法防止合计超卖。
5. 选择并记录数量相等时的策略。本答案返回 `SeatCountUnchanged`，而不是发出虚假的“已变更”事实。
6. 调用唯一受保护的 `Booking.changeSeats` 转换，并返回 `BookingSeatCountChanged updated`。

建议的 `Booking.changeSeats` 函数应成为预约局部状态转换和构造更新预约的唯一权威。聚合决策器使用扣除该预约旧数量后的已预留座位，负责活动级容量等式，再调用该转换。每项规则仍只有一份权威实现。

接受后，`evolve state (BookingSeatCountChanged updated)` 应返回 `Booked updated`，不再验证状态或容量。测试需要覆盖不存在、标识不匹配、已确认、已取消、数量未变、容量不足、减少和增加中的待确认请求，并用性质检查已预留座位总数永不超过活动容量。

这套纯语言仍未解决两次同时发生的座位变更：两者可能从同一个活动级总数决策。持久化/应用层必须让提交以读取版本为条件，或在一个原子事务内决策并提交。整洁的事件名称并不是并发保证。

实现前还要确认两项业务歧义：已确认预约是否允许减少座位，以及容量预留变化是否需要调整支付。不同答案会产生不同命令、事件与副作用，不能根据既有字段名猜测。

## 练习 3：判断历史是否为事实来源 {#exercise-03}

### 把共同要求与可选架构分开 {#exercise-03-decision}

两种持久化设计都需要：

- 持久写入以及经过测试的重启路径；
- 原子容量决策或乐观并发检查，防止竞争者超卖；
- 稳定请求身份，以及重试调用的幂等规则；
- 模式/版本迁移与损坏处理；
- 授权、隐私、保留、备份与恢复策略；
- 覆盖真实持久化边界的集成测试。

当前状态持久化可以把最新预约和活动容量保存成带版本 DTO。条件更新拒绝过期写入。独立的只追加审计表可保留 90 天内所需的少量事实，最好与状态在同一事务提交，或通过明确延迟与重试语义的发件箱提交。读取当前状态仍然直接。

事件溯源把有序事件流保存为事实来源。重启恢复会重放该流；当前状态投影则让读取保持快速。这会额外引入流版本并发、确定性重放、不可变事件模式演化或向上转换、幂等投影处理器、投影重建、流增长后的快照策略，以及检查和修复事件流的运维工具。

90 天审计要求本身并不要求事件溯源。它要求保留证据，而事件溯源通常让事件成为得到全部当前状态的权威路径。90 天后删除或脱敏旧权威事件，反而可能让重放和隐私策略更复杂。

仅根据已给事实，应选择带版本当前状态持久化，加上事务性审计记录。它直接服务主要查询，可以在重启后恢复，也能在不引入重放和投影运维的情况下执行容量限制。这只是暂定选择，并非声称事件溯源普遍不适合预约系统。

如果证据表明业务必须重建任意历史时刻的状态、根据当时精确输入解释每次决策、增加许多可以独立重建的投影、以非破坏方式纠正历史事实，或把完整生命周期历史视为法律记录，就应重新考虑。作出承诺前，还要测量事件流大小、重建时间、存储成本、团队运维经验与故障恢复演练。

CQRS 仍然独立。当前状态设计可以使用独立读投影，事件溯源设计也仍可暴露简单查询表面。只有读写工作负载、安全或表示需求的分歧足以抵偿同步复杂度时，才采用读写分离。

## 答案回顾 {#solution-review}

- 按值的权责、时间、有效性与受众分类，而不是只看记录字段。
- 原始命令和已验证命令都表达意图；验证不保证业务接受。
- 事件的过去式含义比通知机制更根本。
- 当调用方已经持有有效载荷时，公开事件案例不能证明来源。
- 状态描述当前决策上下文；DTO 描述外部表示。
- 新语言应先解决策略歧义，再引入字段或函数。
- 独立字段错误可以累积；依赖状态的决策使用受保护值。
- 每项规则只有一个函数负责，而 `evolve` 只投影已接受事实。
- 并发控制属于原子提交或带版本提交边界。
- 有限审计记录可以伴随当前状态持久化，而无需成为其事实来源。
- 事件溯源额外引入重放、事件演化、投影与运维义务。
- 依据所需保证和实测成本选择架构，并在证据变化时重新判断。
