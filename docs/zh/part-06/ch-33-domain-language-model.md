---
title: "第 33 章：业务语言、命令、事件与模型"
description: "收束预约系统的领域语言，区分意图、事实、状态与边界数据，并在不预设事件溯源的前提下使用事件。"
translationKey: part-06/ch-33-domain-language-model
kind: chapter
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
  - id: microsoft-fsharp-access-control
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/access-control
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

# 第 33 章：业务语言、命令、事件与模型 {#overview}

预约系统并非从架构图起步。它先有值与函数，随后获得受保护的领域类型，长出工作流，接上副作用端口，最后得到稳定的公共模块。第六部分会把这些切片组装成一个应用。在增加更多机制之前，本章先固定每一层可以使用的词语。

这套词汇本身就是设计的一部分。`PlaceBooking` 请求执行工作；`BookingPlaced` 记录事实；`BookingState` 是当前领域视图；未来的 JSON 请求则是边界表示。让四者采用同一种记录形状，虽然少写几个声明，却会抹掉每个值何时有效、谁可以依赖它。

## 学完本章，你将能够 {#outcomes}

学完本章后，你应该能够：

- 不从控制器、数据表或框架出发，描述预约领域；
- 区分命令、已验证命令、领域事件、领域状态与 DTO；
- 解释命令为何表达祈使意图，而事件为何表达已经发生的事实；
- 用记录表达具名的积类型数据，用可辨识联合表达封闭选项；
- 用私有表示和智能构造函数保护不变量；
- 解释 `decide` 与 `evolve` 各自负责什么；
- 识别类型别名是迁移工具，而不是重复的运行时模型；
- 让稳定的公共表面不泄漏内部工作流表示；
- 在不要求消息代理、CQRS 或事件溯源的前提下使用领域事件；
- 识别事件溯源系统额外承担的设计承诺。

## 从一份词汇表开始 {#glossary}

以下词语在本项目中具有精确的局部含义：

| 词语 | 在这里的含义 | 典型名称 | 值一经存在就可能表示失败吗？ |
|---|---|---|---|
| 命令 | 请求系统尝试一项业务动作 | `PlaceBooking` | 会；原始边界字段可能无效，有效意图也仍可能被拒绝 |
| 已验证命令 | 各个独立字段已通过验证的意图 | `ValidPlaceBooking` | 字段有效，但当前状态仍可能拒绝它 |
| 领域事件 | 对领域已经接受之业务事实的不可变描述 | `BookingPlaced` | 不会；它描述结果，而不是请求 |
| 状态 | 领域用来判断下一命令的当前视图 | `NotBooked`、`Booked booking` | 不会；可公开获得的状态应满足其不变量 |
| 边界 DTO | 为 JSON、存储、另一语言或另一进程塑形的数据 | `PlaceBookingRequestDto` | 会；映射并验证前，它仍不可信 |
| 端口 | 应用要求环境提供的一项能力 | `LoadBooking`、`AppendEvent` | 调用可能按契约被拒绝、取消或发生故障 |

示例中的 “Event” 有两种含义。类型 `Event` 是可以预约座位的排期活动；类型 `BookingEvent` 则是有关预约的事实。可能产生歧义时，本章会在行文中把前者称作**活动**。这两个含义都不同于基于委托的 .NET 事件。

表格描述的是角色，而不是强制后缀。小型内部类型即使名称中没有 `Command`，也可以足够清楚。真正的检验是：读者能否看出一个值是在请求、陈述、记忆，还是跨越边界。

## 看清模型如何生长 {#model-evolution}

前面的教学切片是连续改进，而不是六套互相竞争的架构：

1. 第一部分用元组、列表、表达式与折叠发现座位分配行为。
2. 第二部分用记录、单案例联合与可辨识联合取代松散的基本类型和布尔组合。
3. 第三部分按明确的编译顺序，把验证、决策与演化拆进不同模块。
4. 第四部分用异步端口、取消与有明确所有者的资源包围纯工作流。
5. 第五部分测试不变量，并在不泄漏工作流类型的情况下投影出稳定的面向 F# 公共模块。
6. 本部分为累积形成的语言给出唯一权威归属，再把它连接到契约、存储、适配器与 HTTP。

保留每一个历史类型会制造两份事实来源。贯穿项目会把调用方迁向同一模型；仅当较早章节仍需用旧名称编译时，才保留小型兼容别名。

## 先建模业务，再考虑传输 {#domain-model}

核心模型用领域词语命名活动、预约生命周期与失败：

<<< @/../examples/capstone/src/Booking.Domain/Domain.fs#booking-model{fsharp:line-numbers} [Domain.fs]

这里有几项 F# 选择彼此配合：

- 记录把请求、活动、座位数和状态等具名值组合起来；
- 可辨识联合明确表达生命周期选项和错误选项；
- 值默认不可变，所以一次转换会返回新的 `Booking`；
- 单案例联合区分底层基本表示相同的标识符、数量、代码与原因；
- 度量单位在模型内部防止把座位数与无关整数误做运算；
- 私有记录表示阻止调用方构造跳过规则的 `Booking`；
- 模块函数组成受支持的构造、观察与转换表面。

单靠类型不能执行每一项不变量。`BookingStatus` 能表达三种合法形状，但只有 `Booking.confirm` 和 `Booking.cancel` 定义允许哪些转换。`Booking.create` 会比较请求座位数与活动容量。保护来自表示、访问控制以及少数能够创建新值的函数共同作用。

模型刻意不含 JSON 属性名、数据库路径、HTTP 状态码、日志级别或依赖注入服务。这些概念可以变化，而预约的业务含义不必改变。

## 命令描述意图 {#commands}

权威命令词汇很小：

<<< @/../examples/capstone/src/Booking.Domain/Commands.fs#commands{fsharp:line-numbers} [Commands.fs]

`Place`、`Confirm` 与 `Cancel` 使用祈使式名称，因为命令请求系统尝试某件事。调用方不能如实把输入命名为 `BookingPlaced`：容量、重复请求标识、当前状态或格式错误的文本都可能阻止该事实发生。

这些命令记录有意包含对边界友好的基本类型。因此可以构造 `({ RequestId = " "; Seats = 0 } : PlaceBooking)`，值的构造**不**表示已接受。验证会把各自有效的字段转换成 `RequestId`、`SeatCount` 等受保护值；决策随后应用依赖当前状态的规则。

由此得到三个不同问题：

| 阶段 | 问题 | 失败示例 |
|---|---|---|
| 解析/映射 | 外部表示能否转换成命令的基本类型字段？ | JSON 数字的形状错误 |
| 验证 | 字段本身是否有意义？ | 请求标识为空，或座位数非正 |
| 决策 | 当前状态是否允许这项有效意图？ | 预约已存在，或容量太小 |

分开这些问题后，独立的验证错误可以累积，依赖状态的规则则能从一个已知状态短路。第 34 章会把所有命令连接到同一个纯决策器；本章先固定该决策器消费的语言。

`[<RequireQualifiedAccess>]` 要求调用位置写成 `BookingCommand.Place`、`BookingCommand.Confirm` 或 `BookingCommand.Cancel`。随着领域扩展，限定名称能避免通用案例名变得含糊。

## 事件描述已接受的事实 {#events}

与之对应的事件词汇使用过去式：

<<< @/../examples/capstone/src/Booking.Domain/Events.fs#events{fsharp:line-numbers} [Events.fs]

`BookingPlaced`、`BookingConfirmed` 与 `BookingCancelled` 陈述领域已经接受了什么。在当前模型中，每个事件携带转换后受保护的 `Booking`，因此演化无需重复转换规则就能投影新状态。这只是简单的进程内事实表示，尚未承诺任何持久线协议。

事件不应包含仍需批准的操作。后续副作用——例如发送通知——可能在处理时失败，但这项失败不会在语法上把已经接受的预约事实重新变成命令。应用策略负责决定如何重试该副作用或进行补偿。

联合案例是公开的，而 `Booking` 表示是私有的。因此，持有有效预约的代码仍可把它包装进某个事件案例。不要夸大保证：当前 API 保护预约的构造和转换，却不会以密码学方式证明事件来源。第 34 章会通过决策器收窄正常的事件产生路径。

领域事件也不意味着 .NET 事件或消息代理。纯函数可以把 `BookingEvent` 当作普通数据返回。应用可按明确的一致性规则，把它折叠进状态、持久化、发布为映射后的集成消息，或同时执行其中多项。

## 状态是当前决策上下文 {#state}

工作流只需要两种顶层状态形状：

<<< @/../examples/capstone/src/Booking.Domain/Workflow.fs#booking-state{fsharp:line-numbers} [Workflow.fs]

`NotBooked` 表示当前考察的请求没有预约。`Booked booking` 携带受保护的预约，其自身状态可能是待确认、已确认或已取消。这种嵌套形状避免了“既未预约又已确认”之类非法组合。

演化有意保持机械化：

<<< @/../examples/capstone/src/Booking.Domain/Workflow.fs#evolve{fsharp:line-numbers} [Workflow.fs]

`evolve` 回答“接受这项事实后是什么状态？”，而不回答“这项事实可以发生吗？”。后者由决策器和领域转换函数负责。如果 `evolve` 再次检查容量或状态，同一规则就可能漂移成两套实现。

当前事件携带完整的结果 `Booking`，所以 `evolve` 不需要读取先前状态实参。保留惯常的 `state -> event -> state` 形状能显式表达折叠，也为以后表达差量的事件留出空间。不要因为某个实参未使用，就推断历史与事件决策无关。

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
| 何时改变形状 | 业务含义变化时 | 线协议/存储兼容性变化时 |
| F# 特性 | 私有记录、可辨识联合、选项、度量单位 | 显式基本类型字段和刻意版本化的表示 |
| 失败 | 领域错误，或根本无法构造 | 解析、模式、映射与兼容性错误 |

直接序列化 `Booking`、`BookingStatus` 或 `BookingEvent`，会把面向编译器的表示变成公开存储或网络契约。此时重命名联合案例、改变其载荷或重组私有字段都可能成为一次迁移。第 35 章会改为引入显式 DTO，以及要么完整成功、要么显式失败的映射。

DTO 并不是“糟糕的领域建模”。它是一个防腐边界，职责是在不削弱领域的情况下接纳外部表示规则。让它保持简单，记录其模式，并在把受保护值传入内部前完成验证。

## 提供稳定的公共路径 {#public-surface}

贯穿项目预期的面向 F# 入口从原始边界值开始，但返回不透明模型：

<<< @/../examples/capstone/src/Booking.Domain/PublicApi.fs#start{fsharp:line-numbers} [PublicApi.fs]

`PublicApi.BookingModel` 与 `BookingView` 隐藏了表示。消费者使用 `start`、`place`、`confirm`、`cancel` 和观察函数；其自身签名无需提到 `Event`、`Booking`、`BookingState`、`BookingEvent`、`RequestId` 或 `SeatCount`。公共模块把内部错误投影为更小的 `BookingError` 词汇。

这并不会让程序集中的所有其他类型都无法访问。它建立了一条稳定路径，让消费者无需耦合教学工作流的表示。如果将来的库契约要求更严格，还可用签名文件或独立的内部程序集进一步限制表面。

稳定 API 与边界 DTO 解决不同问题。前者保护同一库生态内的 F# 源码依赖；后者固定序列化或跨语言契约。第 27 章已经说明：面向 C# 的 API 可能需要类、枚举、成员、可空标注和异常，而不应直接暴露这种 F# 形状的表面。

## 迁移名称，但不复制模型 {#compatibility-aliases}

早期切片使用了 `Validation.PlaceBookingCommand` 和 `Workflow.BookingEvent`。收束后的代码只把这些声明保留为别名：

```fsharp
// 兼容名称；不会创建第二种运行时表示。
type PlaceBookingCommand = PlaceBooking
type BookingEvent = Booking.Domain.BookingEvent
```

类型别名为同一个类型提供另一个名字。它没有独立构造函数、序列化形状、相等语义或运行时身份，因此适合分阶段迁移教学代码。

别名不能成为永久保留两套词汇的借口。新代码使用 `PlaceBooking` 与命名空间级 `BookingEvent`；旧示例则在修订对应章节时迁移。如果两个名字开始拥有不同规则，应定义两个诚实的概念与显式映射，不要让别名掩盖分歧。

## 事件不要求事件溯源 {#events-not-event-sourcing}

返回领域事件，意味着代码采用了一套事实词汇；它**不**意味着系统必须永远保存每一项事实，或用它们重建状态。

| 设计 | 事实来源 | 如何取得当前状态 | 额外义务 |
|---|---|---|---|
| 当前状态持久化 | 最新预约 DTO 或数据库行 | 读取已保存的当前表示 | 原子更新、并发检查、模式迁移、恢复 |
| 领域事件加当前状态持久化 | 最新状态；事件可触发进程内工作或集成 | 读取当前状态；处理选定事实 | 分发时机、副作用一致性，以及投递可能重复时的去重处理 |
| 事件溯源 | 只追加的有序事件流 | 重放事件，通常辅以快照或投影 | 流并发、事件模式演化、确定性重放、投影重建、幂等、保留、隐私与运维工具 |

事件溯源是一种存储架构：每个实体的有序事件流是权威历史，当前状态通过重放导出。CQRS 是另一个独立选择，它把写命令与读查询分开。两者经常组合，但都不会因为定义了一个名为 `BookingEvent` 的 F# 联合就自动成立。

贯穿项目目前只证明了纯事实词汇和演化函数。较早的内存适配器演示的是接线，而不是持久事件存储。后续章节可以持久化当前 DTO、追加选定事实或把事实映射成集成消息，而不把事件列表变成唯一事实来源。

只有当历史访问、时态决策、审计需要或投影灵活性足以抵偿迁移和运维成本时，才选择事件溯源。“我们已经有事件”不是充分证据。

## 按时间与权责命名类型 {#naming}

可以用时态与所有权快速审查名称：

- 命令名是祈使式业务动作：`PlaceBooking`，而不是 `SetStatus`；
- 事件名是过去式事实：`BookingConfirmed`，不要使用听起来尚待执行的 `ConfirmBookingEvent`；
- 状态名描述当前为真的事情：`NotBooked`、`Booked`；
- 错误名陈述尝试转换失败的原因：`CannotConfirmFrom`；
- 存在歧义时，DTO 名称应指出边界与方向：`PlaceBookingRequestDto`；
- 端口名描述能力，而不是选定产品：`LoadBooking`，而不是 `ReadPostgresRow`。

名称应采用定义规则之人的语言。技术精度仍很重要：如果业务参与者用“活动”含义的 event 表示排期对象，应把事实限定为 `BookingEvent`，而不是悄悄改掉他们的词语。

避免在整个领域范围使用 `Request`、`Response`、`Data` 或 `StatusChanged` 等泛化容器；它们迫使读者从文件夹或注释恢复上下文。也不要把实现承诺编码进领域名称：`BookingSavedToJson` 是适配器结果，不是预约事实。

## 严格限定 K06 证据的含义 {#evidence}

收束后的实现和聚焦测试确定了以下事实：

- 三个命令都表达意图，三个事件都表达已接受的事实；
- 预约构造与转换仍经过既有的受保护领域函数；
- `PublicApi` 的函数签名不暴露内部领域或工作流类型；
- 旧命令和事件名称只是别名，而不是第二套运行时模型；
- 增加事件案例后，旧模式匹配因无法通过穷尽性检查而失败，直至显式更新；
- 领域、工作流与性质测试在 F# 10、空值检查及警告即错误设置下继续通过。

这些证据尚未证明所有命令共用一个决策器、持久 JSON 兼容性、原子持久化、幂等、HTTP 行为或重启恢复。它们是后续的显式切片，不能从整洁的类型名称中推断出来。

## 审查领域语言 {#review-checklist}

扩展模型前，请逐项检查：

- 领域专家能否认出动作、事实、状态与拒绝原因？
- 每个命令描述的是一项业务动作尝试，而不是字段更新吗？
- 能否区分原始命令字段与已验证值？
- 每个事件都陈述了已经接受的事情吗？
- 是否由 `decide` 决定许可、由 `evolve` 负责投影？
- 可公开构造的值是否保证有效，或已明确标作边界输入？
- 领域表示是否受到保护，同时仍提供有用的观察函数？
- DTO 是否能因兼容性原因独立于领域类型变化？
- 兼容别名是否临时存在，并沿单一方向迁移调用方？
- 对事件的描述是否意外暗示了消息代理、CQRS 或事件溯源？
- 未经证明的并发、持久性、重试与恢复保证是否明确标为缺失？

## 练习 {#exercises}

### 练习 1：按角色分类值 {#exercise-01}

把下列值分别归类为命令、已验证命令、领域事件、状态、边界 DTO、端口或领域错误：`PlaceBooking`、`ValidPlaceBooking`、`BookingPlaced`、`Booked booking`、`PlaceBookingRequestDto`、`AppendEvent` 与 `RequestedSeatsExceedCapacity`。逐项说明谁能创建它，以及仅仅构造该值是否就表示请求的预约已经发生。

### 练习 2：先扩展语言，再扩展代码 {#exercise-02}

业务要求修改待确认预约的座位数。请提出一个命令、一个已接受事件、依赖状态的错误和受保护的领域转换。说明哪些内容必须独立验证，哪些内容需要活动容量和当前预约状态，以及 `evolve` 应做什么。暂时不要添加 JSON 或存储字段。

### 练习 3：判断历史是否为事实来源 {#exercise-03}

比较当前状态持久化与事件溯源在本预约系统中的应用。需求是：防止超卖、重启后恢复、快速回答当前预约状态，并保留 90 天审计记录。指出两种设计都需要什么、事件溯源额外增加什么，并仅根据这些事实做出选择。列出哪些新证据会让你重新考虑。

[阅读本章练习答案](../solutions/ch-33-domain-language-model)。

## 模型回顾 {#model-review}

- 领域语言先于控制器、数据表、序列化器与宿主。
- 命令提出请求；已验证命令的字段可靠；决策仍可能拒绝它。
- 事件陈述已接受的事实，并采用过去式业务语言。
- 状态是下一次决策的当前上下文，不自动等于其存储格式。
- DTO 属于兼容性边界，在完成映射前仍不可信。
- 记录表达具名积类型；可辨识联合表达封闭选项。
- 私有表示配合智能构造函数与转换函数保护不变量。
- `decide` 决定事实能否发生；`evolve` 把已接受事实投影为状态。
- 稳定公共模块能隐藏工作流表示，但不会因此成为线协议契约。
- 类型别名帮助迁移，却不能永久保留两套词汇。
- 领域事件可以只是普通返回数据；消息代理和处理器是可选的应用选择。
- 事件溯源与 CQRS 是独立架构承诺，不是定义事件联合后的必然结果。
- 当前 K06 证据证明词汇与封装，而非持久化或并发保证。

## 资料来源 {#sources}

- [Microsoft Learn：F# 记录、不可变性、构造与访问修饰符](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/records)
- [Microsoft Learn：F# 可辨识联合与具名案例](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/discriminated-unions)
- [Microsoft Learn：F# 访问控制](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/access-control)
- [Microsoft Learn：领域事件作为事实与显式领域副作用](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/domain-events-design-implementation)
- [Azure 架构中心：CQRS 命令、读取与独立复杂度](https://learn.microsoft.com/en-us/azure/architecture/patterns/cqrs)
- [Azure 架构中心：事件溯源、重放、投影与取舍](https://learn.microsoft.com/en-us/azure/architecture/patterns/event-sourcing)
