---
title: "第 33 章：业务语言、命令、事件与模型"
description: "收束预约系统的领域语言，区分意图、事实、状态与边界数据，并在不预设事件溯源的前提下使用事件。"
translationKey: part-06/ch-33-domain-language-model
---

# 第 33 章：业务语言、命令、事件与模型 {#overview}

预约系统不需要从架构图起步。先用值和函数表达业务，再逐步加入经过验证的类型、工作流、外部依赖和公共模块。第六部分会沿着这个顺序展开，本章先说明后续代码使用的词汇。

这些名称表达不同含义。`PlaceBooking` 是一项请求，`BookingPlaced` 是已经发生的事实，`BookingState` 保存当前业务状态，JSON 请求则是尚未验证的外部数据。让它们共用一种记录类型虽然少写几个声明，却会让人分不清一个值何时有效、可以交给谁使用。

第 33–38 章使用一套逐步展开的**页内示例**。它用于解释类型和职责，不要求你把所有片段拼成一个完整项目。只有明确写出 `examples/...` 路径的命令，才对应仓库中可直接运行的文件。

代码块标题中的 `Domain.fs`、`Commands.fs` 等名称只用来说明代码应放在哪一层。阅读时，假定它们都位于 `Booking.Domain` 命名空间，并按 `Domain.fs` → `Commands.fs` → `Events.fs` → `Workflow.fs` → `PublicApi.fs` 排列。后续章节会在这个顺序中加入验证和统一决策函数。

## 从一份词汇表开始 {#glossary}

以下词语在本项目中具有明确的局部含义：

| 词语 | 在这里的含义 | 典型名称 | 值一经存在就可能表示失败吗？ |
|---|---|---|---|
| 命令 | 请求系统尝试一项业务动作 | `PlaceBooking` | 会；原始边界字段可能无效，有效意图也仍可能被拒绝 |
| 已验证命令 | 各个独立字段已通过验证的意图 | `ValidPlaceBooking` | 字段有效，但当前状态仍可能拒绝它 |
| 领域事件 | 对领域已经接受之业务事实的不可变描述 | `BookingPlaced` | 不会；它描述结果，而不是请求 |
| 状态 | 领域用来判断下一命令的当前视图 | `NotBooked`、`Booked booking` | 不会；可公开获得的状态应满足其不变量 |
| 边界 DTO | 按 JSON、存储、另一语言或另一进程的需要组织的数据 | `PlaceBookingRequestDto` | 会；映射并验证前，它仍不可信 |
| 端口 | 应用要求环境提供的一项能力 | `LoadBooking`、`AppendEvent` | 调用可能按契约被拒绝、取消或发生故障 |

示例中的 “Event” 有两种含义。类型 `Event` 是可以预约座位的排期活动；类型 `BookingEvent` 则是有关预约的事实。可能产生歧义时，本章会在行文中把前者称作**活动**。这两个含义都不同于基于委托的 .NET 事件。

表格描述的是角色，而不是强制后缀。小型内部类型即使名称中没有 `Command`，也可以足够清楚。真正的检验是：读者能否看出一个值是在请求、陈述、记忆，还是跨越边界。

## 看清模型如何生长 {#model-evolution}

前面的教学阶段是连续改进，而不是六套互相竞争的架构：

1. 第一部分用元组、列表、表达式与折叠发现座位分配行为。
2. 第二部分用记录、单用例联合与可区分联合取代松散的基本类型和布尔组合。
3. 第三部分按明确的编译顺序，把验证、决策与演化拆进不同模块。
4. 第四部分用异步端口、取消与有明确所有者的资源包围纯工作流。
5. 第五部分测试不变量，并在不泄漏工作流类型的情况下投影出稳定的面向 F# 公共模块。
6. 本部分统一这些语言，再把它连接到契约、存储、适配器与 HTTP。

保留每一个历史类型会制造两份事实来源。这套参考实现把调用方迁向同一模型；仅当较早章节仍需用旧名称时，才保留小型兼容别名。

## 先建模业务，再考虑传输 {#domain-model}

`Domain.fs` 先打开 `System`，声明度量单位 `[<Measure>] type seat`，再定义主模型所需的受保护值。下面的主模型不是空文件中的第一段代码；它依赖这些已经定义的类型：

| 类型 | 智能构造规则 | 读取函数 |
|---|---|---|
| `EventId` | 去除首尾空白后必须非空 | `EventId.value` |
| `RequestId` | 去除首尾空白后非空，最长 64 个字符，只允许 URI 非保留字符，且不能是 `.` 或 `..` | `RequestId.value` |
| `Capacity` | 正整数，内部表示为 `int<seat>` | `Capacity.value` |
| `SeatCount` | 正整数，内部表示为 `int<seat>` | `SeatCount.value` |
| `ConfirmationCode` | 去除首尾空白后必须非空 | `ConfirmationCode.value` |
| `CancellationReason` | 去除首尾空白后必须非空 | `CancellationReason.value` |

每个智能构造函数都返回 `Result`，只有模块内部能调用私有联合用例。具备这些前置定义后，核心模型用领域词语命名活动、预约生命周期与失败：

```fsharp:line-numbers [Domain.fs]
type Event =
    private
        { Id: EventId
          Capacity: Capacity }

module Event =
    let create eventId capacity = { Id = eventId; Capacity = capacity }

    let id event = event.Id
    let capacity event = event.Capacity

type BookingStatus =
    | Pending
    | Confirmed of ConfirmationCode
    | Cancelled of CancellationReason

type BookingCreationError = RequestedSeatsExceedCapacity of requested: int<seat> * capacity: int<seat>

type BookingTransitionError =
    | CannotConfirmFrom of current: BookingStatus
    | CannotCancelFrom of current: BookingStatus

type Booking =
    private
        { RequestId: RequestId
          EventId: EventId
          Seats: SeatCount
          Status: BookingStatus }

module Booking =
    let create event requestId seats =
        let requested = SeatCount.value seats
        let capacity = event |> Event.capacity |> Capacity.value

        if requested > capacity then
            Error(RequestedSeatsExceedCapacity(requested, capacity))
        else
            Ok
                { RequestId = requestId
                  EventId = Event.id event
                  Seats = seats
                  Status = Pending }

    let requestId booking = booking.RequestId
    let eventId booking = booking.EventId
    let seats booking = booking.Seats
    let status booking = booking.Status

    let restore requestId eventId seats status =
        { RequestId = requestId
          EventId = eventId
          Seats = seats
          Status = status }

    let confirm confirmationCode booking =
        match booking.Status with
        | Pending ->
            Ok
                { booking with
                    Status = Confirmed confirmationCode }
        | current -> Error(CannotConfirmFrom current)

    let cancel reason booking =
        match booking.Status with
        | Pending
        | Confirmed _ ->
            Ok
                { booking with
                    Status = Cancelled reason }
        | Cancelled _ as current -> Error(CannotCancelFrom current)
```
这里有几项 F# 选择彼此配合：

- 记录把请求、活动、座位数和状态等具名值组合起来；
- 可区分联合明确表达生命周期选项和错误选项；
- 值默认不可变，所以一次转换会返回新的 `Booking`；
- 单用例联合区分底层基本表示相同的标识符、数量、代码与原因；
- 度量单位在模型内部防止把座位数与无关整数误做运算；
- 私有记录表示阻止调用方构造跳过规则的 `Booking`；
- 模块函数组成受支持的构造、观察与转换 API。

单靠类型不能执行每一项不变量。`BookingStatus` 能表达三种合法状态，但只有 `Booking.confirm` 和 `Booking.cancel` 定义允许哪些转换。`Booking.create` 会比较请求座位数与活动容量。表示方式、访问控制和少数能创建新值的函数共同保护这些约束。

`Booking.restore` 专供受信任的持久化重建边界使用。它仍要求调用方先把原始字段转换成 `RequestId`、`EventId`、`SeatCount` 与合法 `BookingStatus`；不能把未经检查的 DTO 直接交给它。

模型刻意不含 JSON 属性名、数据库路径、HTTP 状态码、日志级别或依赖注入服务。这些概念可以变化，而预约的业务含义不必改变。

## 命令描述意图 {#commands}

统一后的命令词汇很小：

```fsharp:line-numbers [Commands.fs]
type PlaceBooking = { RequestId: string; Seats: int }

type ConfirmBooking =
    { RequestId: string
      ConfirmationCode: string }

type CancelBooking = { RequestId: string; Reason: string }

[<RequireQualifiedAccess>]
type BookingCommand =
    | Place of PlaceBooking
    | Confirm of ConfirmBooking
    | Cancel of CancelBooking

module Commands =
    let place requestId seats : PlaceBooking =
        { RequestId = requestId; Seats = seats }

    let confirm requestId confirmationCode : ConfirmBooking =
        { RequestId = requestId
          ConfirmationCode = confirmationCode }

    let cancel requestId reason : CancelBooking =
        { RequestId = requestId
          Reason = reason }
```
`Place`、`Confirm` 与 `Cancel` 使用祈使式名称，因为命令请求系统尝试某件事。调用方不能如实把输入命名为 `BookingPlaced`：容量、重复请求标识、当前状态或格式错误的文本都可能阻止该事实发生。

这些命令记录有意包含对边界友好的基本类型。因此可以构造 `({ RequestId = " "; Seats = 0 } : PlaceBooking)`，值的构造**不**表示已接受。验证会把各自有效的字段转换成 `RequestId`、`SeatCount` 等受保护值；决策随后应用依赖当前状态的规则。

由此得到三个不同问题：

| 阶段 | 问题 | 失败示例 |
|---|---|---|
| 解析/映射 | 外部表示能否转换成命令的基本类型字段？ | JSON 值不是规定的数字形式 |
| 验证 | 字段本身是否有意义？ | 请求标识为空，或座位数非正 |
| 决策 | 当前状态是否允许这项有效意图？ | 预约已存在，或容量太小 |

分开这些问题后，可以累积互不依赖的验证错误；依赖状态的规则则在首次拒绝时停止。第 34 章会把所有命令连接到同一个纯决策器；本章先定义其输入语言。

`[<RequireQualifiedAccess>]` 要求调用处写完整名称，例如 `BookingCommand.Place`。随着领域扩展，这能避免 `Place`、`Confirm`、`Cancel` 等通用案例名产生歧义。

## 事件描述已接受的事实 {#events}

与之对应的事件词汇使用过去式：

```fsharp:line-numbers [Events.fs]
type BookingEvent =
    | BookingPlaced of Booking
    | BookingConfirmed of Booking
    | BookingCancelled of Booking

module BookingEvent =
    let booking event =
        match event with
        | BookingPlaced booking
        | BookingConfirmed booking
        | BookingCancelled booking -> booking

    let requestId event = event |> booking |> Booking.requestId
```
`BookingPlaced`、`BookingConfirmed` 与 `BookingCancelled` 陈述领域已经接受了什么。在当前模型中，每个事件携带转换后受保护的 `Booking`，因此演化无需重复转换规则就能投影新状态。这只是简单的进程内事实表示，尚未承诺任何持久化传输协议。

事件不应包含仍需批准的操作。后续副作用——例如发送通知——可能在处理时失败，但这项失败不会在语法上把已经接受的预约事实重新变成命令。应用策略负责决定如何重试该副作用或进行补偿。

联合案例是公开的，而 `Booking` 表示是私有的。因此，持有有效预约的代码仍可把它包装进某个事件案例。不要夸大保证：当前 API 保护预约的构造和转换，却不会以密码学方式证明事件来源。第 34 章会通过决策器收窄正常的事件产生路径。

领域事件也不意味着 .NET 事件或消息代理。纯函数可以把 `BookingEvent` 当作普通数据返回。应用可按明确的一致性规则，把它折叠进状态、持久化、发布为映射后的集成消息，或同时执行其中多项。

## 状态是当前决策上下文 {#state}

工作流只需要两种顶层状态：

```fsharp:line-numbers [Workflow.fs]
type BookingState =
    | NotBooked
    | Booked of Booking
```
`NotBooked` 表示当前考察的请求没有预约。`Booked booking` 携带受保护的预约，其自身状态可能是待确认、已确认或已取消。这种嵌套避免了“既未预约又已确认”之类非法组合。

演化函数有意只做直接转换：

```fsharp:line-numbers [Workflow.fs]
let evolve (_: BookingState) (event: BookingEvent) =
    match event with
    | BookingPlaced booking
    | BookingConfirmed booking
    | BookingCancelled booking -> Booked booking
```
`evolve` 回答“接受这项事实后是什么状态？”，而不回答“这项事实可以发生吗？”。后者由决策器和领域转换函数负责。如果 `evolve` 再次检查容量或状态，同一规则就可能漂移成两套实现。

当前事件携带完整的结果 `Booking`，所以 `evolve` 不需要读取先前状态实参。保留惯常的 `state -> event -> state` 签名，可以明确表达折叠，也为以后表示差量的事件留出空间。不要因为某个实参未使用，就推断历史与事件决策无关。

纯计算的概念路径如下：

```text
原始 DTO 或调用方输入
  -> 命令
  -> 字段验证
  -> 已验证命令 + 当前状态
  -> 决策
  -> 已接受事件或显式错误
  -> 演化
  -> 下一状态
  -> 边界投影或已提交副作用
```

只有最后一步需要外部副作用。从验证到演化的所有步骤都可以保持确定性，并在没有数据库、时钟或网络的情况下测试。

## 不要混淆状态与 DTO {#dto-boundary}

领域值与 DTO 即使暂时包含同一组信息，也仍然承担不同契约：

| 关注点 | 领域值 | 边界 DTO |
|---|---|---|
| 主要受众 | 领域函数与 F# 调用方 | 序列化器、数据库适配器、C# 调用方或远程客户端 |
| 有效性 | 通过受保护规则构造 | 可能包含缺失、空白、默认、未知或过时字段 |
| 何时改变表示 | 业务含义变化时 | 传输协议/存储兼容性变化时 |
| F# 特性 | 私有记录、可区分联合、选项、度量单位 | 显式基本类型字段和刻意版本化的表示 |
| 失败 | 领域错误，或根本无法构造 | 解析、模式、映射与兼容性错误 |

直接序列化 `Booking`、`BookingStatus` 或 `BookingEvent`，会把面向编译器的表示变成公开存储或网络契约。此时重命名联合案例、改变其载荷或重组私有字段都可能成为一次迁移。第 35 章会改为引入显式 DTO，以及要么完整成功、要么显式失败的映射。

DTO 并不是“糟糕的领域建模”。它负责隔开外部数据格式与内部领域模型，让外部表示规则不会削弱领域模型。让它保持简单，记录其模式，并在把受保护值传入内部前完成验证。

## 提供稳定的公共路径 {#public-surface}

参考实现预期的面向 F# 入口从原始边界值开始，但返回不透明模型。下面是 `module PublicApi` 中的后续片段；在它之前，该模块已经声明完整的 `BookingError` 可区分联合，以及私有包装 `type BookingModel = private BookingModel of Event * BookingState`：

```fsharp:line-numbers [PublicApi.fs]
let start rawEventId rawCapacity =
    let eventIdResult =
        EventId.create rawEventId
        |> Result.mapError (fun _ -> [ BookingError.BlankEventId ])

    let capacityResult =
        Capacity.create rawCapacity
        |> Result.mapError (fun (NonPositiveCapacity actual) -> [ BookingError.NonPositiveCapacity actual ])

    match eventIdResult, capacityResult with
    | Ok validEventId, Ok validCapacity -> BookingModel(Event.create validEventId validCapacity, NotBooked) |> Ok
    | Error eventErrors, Error capacityErrors -> Error(eventErrors @ capacityErrors)
    | Error errors, Ok _
    | Ok _, Error errors -> Error errors
```
公共模块为消费者提供四个聚焦的概念：

| 角色 | 公共名称 | 消费者操作 |
|---|---|---|
| 不透明状态 | `PublicApi.BookingModel` | 从 `start` 取得，再传给状态转换 |
| 命令 | `place`、`confirm`、`cancel` | 请求一次领域转换 |
| 观察 | `BookingView` 与观察函数 | 读取投影后的视图 |
| 失败 | `BookingError` | 匹配较小的公共错误词汇 |

内部工作流继续使用 `Event`、`Booking`、`BookingState`、`BookingEvent`、`RequestId` 和 `SeatCount`。消费者签名只需使用公共角色。

这个模块建立了稳定的消费路径，程序集中的其他类型仍然可访问。将来的库若需要程序集级限制，可以加入签名文件，或把实现类型移入独立的内部程序集。

稳定 API 与边界 DTO 解决不同问题。前者保护同一库生态内的 F# 源码依赖；后者固定序列化或跨语言契约。正如第 27 章所示，面向 C# 的 API 通常用类、枚举、成员、可空标注和异常表达公共 API。

## 迁移名称，但不复制模型 {#compatibility-aliases}

早期阶段使用了 `Validation.PlaceBookingCommand` 和 `Workflow.BookingEvent`。统一模型后的代码只把这些声明保留为别名：

```fsharp
// 兼容名称；不会创建第二种运行时表示。
type PlaceBookingCommand = PlaceBooking
type BookingEvent = Booking.Domain.BookingEvent
```

类型别名为同一个类型提供另一个名字。它没有独立构造函数、独立序列化表示、相等语义或运行时身份，因此适合分阶段迁移教学代码。

别名不能成为永久保留两套词汇的借口。新代码使用 `PlaceBooking` 与命名空间级 `BookingEvent`；旧示例则在修订对应章节时迁移。如果两个名字开始遵循不同规则，应定义两个含义明确的概念和映射，不要让别名掩盖分歧。

## 事件不要求事件溯源 {#events-not-event-sourcing}

返回领域事件，意味着代码采用了一套事实词汇；它**不**意味着系统必须永远保存每一项事实，或用它们重建状态。

| 设计 | 事实来源 | 如何取得当前状态 | 额外义务 |
|---|---|---|---|
| 当前状态持久化 | 最新预约 DTO 或数据库行 | 读取已保存的当前表示 | 原子更新、并发检查、模式迁移、恢复 |
| 领域事件加当前状态持久化 | 最新状态；事件可触发进程内工作或集成 | 读取当前状态；处理选定事实 | 分发时机、副作用一致性，以及投递可能重复时的去重处理 |
| 事件溯源 | 只追加的有序事件流 | 重放事件，通常辅以快照或投影 | 流并发、事件模式演化、确定性重放、投影重建、幂等、保留、隐私与运维工具 |

事件溯源是一种存储架构：每个实体的有序事件流是唯一可信的历史，当前状态通过重放导出。CQRS 是另一个独立选择，它把写命令与读查询分开。两者经常组合，但都不会因为定义了一个名为 `BookingEvent` 的 F# 联合就自动成立。

到这里，页内代码只定义了纯事实词汇和演化函数。第 32 章的内存适配器演示的是组件连接，而不是持久事件存储。后续章节可以持久化当前 DTO、追加选定事实或把事实映射成集成消息，而不把事件列表变成唯一事实来源。

只有当历史访问、时态决策、审计需要或投影灵活性足以抵偿迁移和运维成本时，才选择事件溯源。“我们已经有事件”不是充分证据。

## 按时间与权责命名类型 {#naming}

可以用时态与职责快速审查名称：

- 命令名是祈使式业务动作：`PlaceBooking`，而不是 `SetStatus`；
- 事件名是过去式事实：`BookingConfirmed`，不要使用听起来尚待执行的 `ConfirmBookingEvent`；
- 状态名描述当前为真的事情：`NotBooked`、`Booked`；
- 错误名陈述尝试转换失败的原因：`CannotConfirmFrom`；
- 存在歧义时，DTO 名称应指出边界与方向：`PlaceBookingRequestDto`；
- 端口名描述能力，而不是选定产品：`LoadBooking`，而不是 `ReadPostgresRow`。

名称应采用定义规则之人的语言。技术精度仍很重要：如果业务参与者用“活动”含义的 event 表示排期对象，应把事实限定为 `BookingEvent`，而不是悄悄改掉他们的词语。

避免在整个领域范围使用 `Request`、`Response`、`Data` 或 `StatusChanged` 等泛化容器；它们迫使读者从文件夹或注释恢复上下文。也不要把实现承诺编码进领域名称：`BookingSavedToJson` 是适配器结果，不是预约事实。

本章的范围到领域词汇、受保护构造、转换、事件和演化为止。页内片段没有证明持久 JSON 兼容性、原子持久化、幂等、HTTP 行为或重启恢复；这些能力需要后续边界设计和真实可执行测试。类型名称整洁并不表示系统已经具备它们。

## 练习 {#exercises}

### 练习 1：按角色分类值 {#exercise-01}

对以下值进行分类：

- `PlaceBooking`
- `ValidPlaceBooking`
- `BookingPlaced`
- `Booked booking`
- `PlaceBookingRequestDto`
- `AppendEvent`
- `RequestedSeatsExceedCapacity`

逐项说明它属于哪一类：命令、已验证命令、业务事件、状态、边界 DTO、外部依赖或业务错误。还要说明谁能创建它，以及创建这个值是否意味着预约已经发生。


::: details 参考答案

#### 先问这个值声称了什么 {#exercise-01-classification}

| 值 | 角色 | 正常创建者 | 构造它是否表示请求的预约已经发生？ |
|---|---|---|---|
| `PlaceBooking` | 原始命令 | 边界映射器或 `Commands.place` | 否；其基本类型字段可能无效，领域也可能拒绝它 |
| `ValidPlaceBooking` | 已验证命令 | 在独立字段检查后由 `validatePlaceBooking` 创建 | 否；重复标识或容量仍可能拒绝它 |
| `BookingPlaced booking` | 领域事件 | 正常由预约决策器在 `Booking.create` 成功后创建 | 就值的含义而言是；不过当前公开案例仍能重新包装既有的有效预约 |
| `Booked booking` | 领域状态 | `evolve` 或受信任的状态重建边界 | 它说明预约当前存在；并不声称某项新请求刚刚成功 |
| `PlaceBookingRequestDto` | 边界 DTO | JSON 序列化器/客户端或 HTTP 适配器 | 否；它仍是不可信的表示 |
| `AppendEvent` | 端口能力 | 组合根提供适配器函数 | 否；持有或调用这项能力都不是领域决策，调用也可能失败 |
| `RequestedSeatsExceedCapacity` | 领域错误 | 已验证座位数超过已验证容量时，由 `Booking.create` 创建 | 否；它说明预约尝试遭到拒绝 |

关键区别在于语法与时间。命令请求未来的工作；已验证命令排除了格式错误，但仍可能被业务拒绝。事件陈述已经发生的事，状态描述当前事实，DTO 则只是跨边界的数据格式。

`BookingPlaced` 需要一项限定。正常路径中，`Booking.create` 已确保载荷合法。但联合用例仍然公开，任何持有合法 `Booking` 的代码都能包装它。若事件来源也必须受约束，应通过签名文件或内部边界隐藏构造入口；仅凭当前类型无法确认事件来自哪里。

`AppendEvent` 是值为函数的记录字段。端口只声明写入能力及其调用约定，不让适配器判断预约是否合法。应用仅在纯计算接受请求后调用它。

:::

### 练习 2：先扩展语言，再扩展代码 {#exercise-02}

业务要求修改待确认预约的座位数。请提出一个命令、一个已接受事件、依赖状态的错误和受保护的领域转换。说明哪些内容必须独立验证，哪些内容需要活动容量和当前预约状态，以及 `evolve` 应做什么。暂时不要添加 JSON 或存储字段。


::: details 参考答案

#### 先明确策略选择 {#exercise-02-policy}

一套自洽的词汇如下：

| 角色 | 建议名称 | 数据或含义 |
|---|---|---|
| 命令 | `ChangeBookingSeats` | 原始 `RequestId: string` 与 `Seats: int` |
| 已验证命令 | `ValidChangeBookingSeats` | 已验证的 `RequestId` 与 `SeatCount` |
| 已接受事件 | `BookingSeatCountChanged` | 转换后的合法 `Booking` |
| 字段错误 | `BlankRequestId`、`NonPositiveSeatCount` | 彼此独立的格式错误字段 |
| 状态错误 | `BookingDoesNotExist`、`RequestIdDoesNotMatch`、`CannotChangeSeatsFrom`、`InsufficientCapacity`、`SeatCountUnchanged` | 需要预约或活动级状态才能判定的拒绝 |

命令使用祈使式动作，事件使用过去式事实。两者名称都不提 JSON、数据库列或 HTTP 动词。携带转换后的预约符合当前事件/演化设计，不过未来的持久事件模式也可能改为携带稳定请求标识与变更前后数量。

分别验证请求标识和座位数。这些检查不依赖当前状态，可以同时累积两个错误。此时不要用原始整数检查容量；决策应使用已经验证的 `SeatCount` 与活动 `Capacity`。

依赖状态的决策可以按以下顺序执行：

1. 对 `NotBooked` 返回 `BookingDoesNotExist`。
2. 对 `Booked booking`，要求命令请求标识等于 `Booking.requestId booking`。
3. 要求 `Booking.status booking` 是 `Pending`；对 `Confirmed` 与 `Cancelled` 返回 `CannotChangeSeatsFrom current`。
4. 使用活动级预留状态，要求替换后的数量能被可用容量容纳。仅凭 `Event.capacity` 和一个预约无法防止合计超卖。
5. 选择并记录数量相等时的策略。本答案返回 `SeatCountUnchanged`，而不是发出虚假的“已变更”事实。
6. 调用唯一可构造合法结果的 `Booking.changeSeats`，并返回 `BookingSeatCountChanged updated`。

建议只在 `Booking.changeSeats` 中执行预约内部的状态转换，并构造更新后的预约。聚合决策器先从已预留座位中扣除该预约的旧数量，再检查活动总容量，最后调用这一转换。每项规则只实现一次。

接受事件后，`evolve state` 对 `BookingSeatCountChanged updated` 应返回 `Booked updated`。它不再验证状态或容量。示例测试覆盖预约不存在、标识不匹配、已确认、已取消、数量未变和容量不足。还要覆盖待确认预约减少与增加座位，并用属性测试检查预留总数从不超过活动容量。

这套领域模型仍未解决两次并发的座位变更：两者可能基于同一个活动总数作出决策。持久化或应用层必须按读取版本提交，或在一个原子事务内完成决策与提交。事件名称再清楚，也不能保证并发正确性。

实现前还要确认两项业务歧义：已确认预约是否允许减少座位，以及容量预留变化是否需要调整支付。不同答案会产生不同命令、事件与副作用，不能根据既有字段名猜测。

:::

### 练习 3：判断历史是否为事实来源 {#exercise-03}

比较本预约系统采用当前状态持久化与事件溯源的差异。系统必须防止超卖、支持重启恢复、快速返回当前预约状态，并保留 90 天审计记录。指出两种设计的共同要求、事件溯源增加的义务，并仅根据这些信息做出选择。再列出哪些新信息会让你重新考虑。


::: details 参考答案

#### 把共同要求与可选架构分开 {#exercise-03-decision}

两种持久化设计都需要：

- 持久写入以及经过测试的重启路径；
- 原子容量决策或乐观并发检查，防止竞争者超卖；
- 稳定请求身份，以及重试调用的幂等规则；
- 模式/版本迁移与损坏处理；
- 授权、隐私、保留、备份与恢复策略；
- 覆盖真实持久化边界的集成测试。

保存当前状态时，可以把最新预约和活动容量写成带版本的 DTO，并用条件更新拒绝过期写入。另建一张只追加的审计表，保留 90 天内需要的少量事实。审计记录最好与状态在同一事务中提交；做不到时，再使用明确规定延迟和重试行为的发件箱。读取当前状态仍然很直接。

事件溯源把有序事件流保存为事实来源。重启时通过重放事件恢复状态，再用投影提供快速查询。

这种选择会增加许多工作：处理事件流版本并发；保证重放结果确定；演进事件格式；让投影处理可以安全重试；重建投影；为长事件流制作快照；以及提供检查和修复事件流的运维工具。

90 天审计要求本身并不要求事件溯源。它只要求保留审计记录；事件溯源则通常把事件作为重建全部当前状态的唯一来源。90 天后删除或脱敏这些事件，反而会让重放与隐私策略更复杂。

仅根据已给事实，应选择带版本当前状态持久化，加上事务性审计记录。它直接服务主要查询，可以在重启后恢复，也能在不引入重放和投影运维的情况下执行容量限制。这只是暂定选择，并非声称事件溯源普遍不适合预约系统。

出现以下需求时，应重新考虑事件溯源：

- 重建任意历史时刻的状态；
- 根据当时输入解释每次决策；
- 独立构建多种投影；
- 不覆盖旧记录地纠正历史事实；
- 把完整生命周期历史作为法律记录。

作出决定前，还要测量事件流大小、重建时间和存储成本，并评估团队运维经验与恢复演练结果。

CQRS 是独立选择。当前状态设计可以使用独立读投影，事件溯源也可以提供简单查询 API。只有读写在负载、安全或数据表示上的差异足以抵消同步成本时，才采用读写分离。

:::


## 资料来源 {#sources}

- [Microsoft Learn：F# 记录、不可变性、构造与访问修饰符](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/records)
- [Microsoft Learn：F# 可区分联合与联合用例](https://learn.microsoft.com/zh-cn/dotnet/fsharp/language-reference/discriminated-unions)
- [Microsoft Learn：F# 访问控制](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/access-control)
- [Microsoft Learn：领域事件作为事实与显式领域副作用](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/domain-events-design-implementation)
- [Azure 架构中心：CQRS 命令、读取与独立复杂度](https://learn.microsoft.com/en-us/azure/architecture/patterns/cqrs)
- [Azure 架构中心：事件溯源、重放、投影与取舍](https://learn.microsoft.com/en-us/azure/architecture/patterns/event-sourcing)
