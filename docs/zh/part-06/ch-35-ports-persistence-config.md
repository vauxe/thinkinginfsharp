---
title: "第 35 章：端口、持久化、配置与替身"
description: "用显式 DTO 映射隔离 F# 领域值，安全持久化一个有界本地快照，并以清晰所有权装配确定性适配器。"
translationKey: part-06/ch-35-ports-persistence-config
kind: chapter
part: 6
chapter: 35
status: complete
verifiedWith:
  fsharp: "10"
  dotnetSdk: "10.0.301"
exampleIds:
  - capstone-booking-domain
  - capstone-booking-contracts
  - capstone-booking-infrastructure
exerciseIds:
  - ch35-exercise-01
  - ch35-exercise-02
  - ch35-exercise-03
termIds: []
sources:
  - id: microsoft-json-property-names
    url: https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/customize-properties
    checked: "2026-08-25"
  - id: microsoft-json-unmapped-members
    url: https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/missing-members
    checked: "2026-08-25"
  - id: fsharp-core-climutable
    url: https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-climutableattribute.html
    checked: "2026-08-25"
  - id: microsoft-file-move
    url: https://learn.microsoft.com/en-us/dotnet/api/system.io.file.move?view=net-10.0
    checked: "2026-08-25"
  - id: microsoft-filestream-flush
    url: https://learn.microsoft.com/en-us/dotnet/api/system.io.filestream.flush?view=net-10.0
    checked: "2026-08-25"
  - id: microsoft-configuration-providers
    url: https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration-providers
    checked: "2026-08-25"
  - id: microsoft-cancellation-token
    url: https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken?view=net-10.0
    checked: "2026-08-25"
---

# 第 35 章：端口、持久化、配置与替身 {#overview}

第 34 章停在一个已接受事实处。本章越过效果边界，同时不让 JSON、路径或测试服务行为变成领域规则。结果有意保持很小：一份带版本的 DTO 契约、一个有界本地快照、确定性的支付与通知替身，以及一个拥有它们的组合对象。

核心问题是权威归属。领域决定命令是否合法；映射器决定外部表示能否变成受保护数据；文件适配器决定如何替换字节；组合根决定哪些实现提供能力，以及由谁释放它们。分开这些决策，失败才会既诚实又可测试。

## 学完本章，你将能够 {#outcomes}

学完本章后，你应该能够：

- 把端口读成应用需要的能力，而不是实现选择；
- 让传输和持久化 DTO 与私有 F# 记录及可辨识联合保持分离；
- 设计要么完整成功、要么返回精确错误的双向映射；
- 为联合选择并固定稳定的 JSON 表示；
- 把模式版本用作兼容性决策点，而不是装饰；
- 拒绝未知、大小写错误、过大、格式损坏或语义不可能的快照；
- 区分同目录替换、数据库事务与分布式保证；
- 加载可配置路径，而不把普通配置误当成机密；
- 为成功、拒绝、故障和取消构造确定性替身；
- 让调用方的取消令牌贯穿每个异步端口；
- 把资源所有权放在组合边界，并让释放具有幂等性；
- 精确说明这个本地适配器仍然不能保证什么。

## 遵循依赖方向 {#dependency-direction}

项目依赖向内指向：

```text
Booking.Api（下一章）
        |
        v
Booking.Infrastructure ---> Booking.Contracts ---> Booking.Domain
        |                                             ^
        +---------------------------------------------+
```

`Booking.Domain` 命名命令、事实、受保护值、决策与所需端口；它不知道 JSON 或文件。`Booking.Contracts` 引用领域，只为执行显式转换。`Booking.Infrastructure` 使用这两层实现效果。未来 API 可以在组合边界引用全部三层，但领域永远不会反向指向外层。

这不是仪式化分层。如果领域引用 `JsonPropertyNameAttribute`、文件路径或支付替身，改变外层机制就可能迫使业务类型变化。依赖图阻止这种意外的权威转移。

## 让边界形状与领域形状分离 {#separate-shapes}

快照 DTO 有意采用普通 .NET 数据形状：

<<< @/../examples/capstone/src/Booking.Contracts/Dtos.fs#booking-dto{fsharp:line-numbers} [Dtos.fs]

`[<CLIMutable>]` 为面向 CLI 的使用方添加无参构造函数和属性设置器；它不会让这个记录变成领域实体。`[<JsonPropertyName>]` 在两个序列化方向固定线格式名称，不受未来 F# 字段重命名影响。

DTO 允许领域禁止的状态：空标识、缺失的座位数、未知状态字符串，或两个状态载荷同时出现。这在不可信边界上是正确的。如果其类型假装这些值不可能出现，反序列化失败只会转移到反射或异常中，却没有给应用一项显式映射策略。

受保护的 `Booking` 记录继续保持私有，`BookingStatus` 继续作为有用的 F# 联合。两者都不直接序列化。因此领域表示可以演进，而不会悄悄重新定义已存储 JSON。

## 有意设计联合表示 {#union-representation}

版本 1 把 `BookingStatus` 投影成一个精确标签和至多一个载荷：

| 领域值 | `status` | 必需载荷 | 禁止载荷 |
|---|---|---|---|
| `Pending` | `"pending"` | 无 | 确认码与取消原因 |
| `Confirmed code` | `"confirmed"` | `confirmationCode` | 取消原因 |
| `Cancelled reason` | `"cancelled"` | `cancellationReason` | 确认码 |

这里的原始字符串比 CLR 枚举更合适。领域值并不是枚举：两个案例携带不同的受保护数据。字符串标签还让映射可以返回 `UnknownStatus actual`，而不是让序列化器默认值擅自发明数字约定。

省略空载荷会让每种成功形状更小，却不会造成歧义。标签指出哪个载荷必须存在。契约测试断言每种案例的精确属性集合，所以序列化选项变化不能悄悄加入两个空字段。

## 让反向映射显式化 {#explicit-mapping}

映射错误联合会命名表示失败，而不是把它们压扁成文本：

<<< @/../examples/capstone/src/Booking.Contracts/Mapping.fs#mapping-errors{fsharp:line-numbers} [Mapping.fs]

反向快照映射按已声明顺序进行：

<<< @/../examples/capstone/src/Booking.Contracts/Mapping.fs#snapshot-mapping{fsharp:line-numbers} [Mapping.fs]

首先检查模式版本。版本 2 文档即使其余字段碰巧类似版本 1，也并不兼容；因此映射器会在解释载荷前返回 `UnsupportedSchemaVersion 2`。

随后，标识与座位数原语经过已有智能构造函数。状态映射再检查精确标签和合法载荷组合。只有每个值都受保护之后，`Booking.restore` 才重建私有记录。这个函数接收受保护值，不接收原始 JSON 字符串或整数。

对有效 `Booking` 的正向映射不会失败：每个联合案例都有一种已声明投影。反向映射可以失败，因为外部表示没有领域保证。这种不对称是有用信息，不是 API 缺陷。

### 让命令映射停在正确信任层级 {#command-mapping}

命令 DTO 做一项更窄的工作：

<<< @/../examples/capstone/src/Booking.Contracts/Mapping.fs#command-mapping{fsharp:line-numbers} [Mapping.fs]

它们拒绝缺少请求体、请求标识、座位属性、确认码或原因等传输缺失。它们有意在原始领域命令中保留空白字符串与零座位。第 34 章验证器拥有这些规则，并能累积错误；在 DTO 映射中重复规则会制造互相竞争的权威和不同优先级。

所以“映射成功”只表示传输层提供了表达一项意图所需的字段。它不表示意图已经通过领域验证或业务决策。

## 一次固定序列化策略 {#json-policy}

JSON 辅助模块在使用前配置一个私有选项对象：

<<< @/../examples/capstone/src/Booking.Contracts/Dtos.fs#json-options{fsharp:line-numbers} [Dtos.fs]

这些选择属于边界契约：

- 属性名称使用 camel case，并由特性显式指定；
- 读取区分大小写，所以 `RequestId` 不是 `requestId` 的别名；
- 未映射成员会被拒绝，而不是悄悄忽略；
- 写入时省略空属性；
- 嵌套深度受到限制；
- JSON 的 `null` 请求体仍可表示，并映射为 `MissingBody`。

严格拒绝未知成员是一项失败关闭的兼容策略。它能捕获拼写错误与生产者意外漂移，但也意味着新增字段需要有意改变版本或策略。必须向客户端写清这项取舍；“JSON 很灵活”不是兼容性契约。

K08a 的九项契约测试固定标签、属性集合、大小写、未知字段、版本优先级、所有受保护状态往返、缺失值、不可能的载荷组合以及原始命令保留。

## 把路径当作已验证配置 {#configuration}

文件适配器接收一个受保护配置值：

<<< @/../examples/capstone/src/Booking.Infrastructure/Configuration.fs#store-configuration{fsharp:line-numbers} [Configuration.fs]

`create` 区分缺失值与无效文件路径，规范化成绝对路径，并拒绝已经指向目录的路径。因此适配器不会反复重新解释原始配置。

`BOOKING_STORE_PATH` 可以来自环境变量提供程序，而测试调用 `create` 时只传入操作系统临时目录下的路径。存储路径是配置，不是机密。凭据、API 密钥与证书需要机密提供程序，不能仅因环境变量也承载配置就把它们提交进仓库。

路径由部署配置控制，绝不从请求 ID 派生。这样不会把用户输入变成路径遍历入口或无限增长的文件集合。

## 持久化一个有界快照 {#bounded-snapshot}

`FileBookingStore` 对受保护 `Booking` 暴露异步 `Load` 与 `Save` 操作：

<<< @/../examples/capstone/src/Booking.Infrastructure/FileStore.fs#file-booking-store{fsharp:line-numbers} [FileStore.fs]

内部保存过程把预约映射为 `BookingDto`，序列化为无字节顺序标记的 UTF-8，并拒绝大于 64 KiB 的输出。加载过程最多读取 64 KiB 加一个哨兵字节，再决定是否允许解析。它接受可选 UTF-8 BOM，但拒绝无效字节序列。

固定上限会阻止损坏或被替换的本地文件导致无界分配。64 KiB 是针对一个小快照的样例上限，不是通用 JSON 限制。集合存储需要根据真实基数与流式策略推导上限。

文件或目录缺失意味着 `Ok None`，不属于损坏。读取或权限失败有独立运维案例。语法无效的 JSON、无效 UTF-8，以及不能变成受保护预约的有效 JSON，是三种不同损坏类别。

## 通过同一目录完成替换 {#replacement}

保存遵循以下顺序：

1. 在触碰目标前先序列化并检查大小。
2. 必要时创建已配置父目录。
3. 在同一目录创建名称唯一的临时文件。
4. 写入完整字节并调用 `Flush(true)`。
5. 提交前再次观察取消。
6. 启用覆盖，把临时文件移动到目标位置。
7. 在 `finally` 中尽力删除任何残留临时文件。

把两个文件放在同一目录可确保移动不跨卷。这样避开文档所述的跨卷移动可能退化为复制后删除，也避免直接写目标时向读者暴露部分文档。

保证范围很窄。在本地文件系统的同卷替换语义下，读者看到旧完整文件或新移动的完整文件。`Flush(true)` 会请求 .NET 和操作系统刷新中间缓冲区，但本例不声称能抵御所有设备、文件系统、内核或突然断电行为。

它也不是覆盖加载、领域决策、支付、通知与保存的事务。两个调用方仍可能读到同一旧状态并竞争。第 37 章会加入原子状态边界，并显式测试并发容量。

## 在一个边缘装配能力 {#composition}

基础设施组合对象提供领域的 `AsyncPorts` 记录：

<<< @/../examples/capstone/src/Booking.Infrastructure/Composition.fs#infrastructure-composition{fsharp:line-numbers} [Composition.fs]

每个函数都保留调用方的 `CancellationToken`。存储错误变成 `BookingStoreAdapterException`，既保留类型化内部类别，也让后续 HTTP 层只有一个位置映射安全响应。异常消息既不含文件内容，也不含已配置路径。

`LoadBooking` 尊重请求键，对不同的已存请求返回 `NotBooked`。`AppendEvent` 在保存事件结果预约之前，会拒绝参数键与事件受保护请求 ID 不一致。

当前适配器只存一个快照。因此另一个请求在稍后成功追加后可以替换先前快照。这是明确承认的教学阶段限制，不是多预约仓库。K11 会在把 API 称作一致性安全之前替换这种读写模型。

## 使用确定性替身，而不是伪装集成 {#deterministic-stubs}

支付替身在构造时固定行为：

<<< @/../examples/capstone/src/Booking.Infrastructure/PaymentStub.fs#payment-stub{fsharp:line-numbers} [PaymentStub.fs]

它使用给定交易 ID 授权，返回给定拒绝原因，或抛出 `DependencyUnavailableException`，其 `InnerException` 携带给定故障详情。通知替身同样会交付，或抛出相同的类型化可用性信号：

<<< @/../examples/capstone/src/Booking.Infrastructure/NotificationStub.fs#notification-stub{fsharp:line-numbers} [NotificationStub.fs]

两者都在记录调用前检查取消。它们不使用 HTTP、时钟、随机数、休眠、凭据或环境状态。调用列表是同步快照，因此无需模拟框架也能确定地断言。

这些替身用于学习和控制集成。它们不模拟支付授权协议、重试、Webhook 交付、消息持久性、欺诈检查或提供商幂等性。以 `Stub` 命名，可防止读者误把确定性行为当成生产集成。

## 让所有权与构造相邻 {#ownership}

`Composition.start` 构造两个替身，并返回拥有它们的对象。应用应在最外层生命周期边界用 `use` 绑定该对象。释放会把组合标记为关闭，先释放通知再释放支付，而且重复调用仍然安全。

端口会拒绝释放后的调用。预先取消的调用会先观察取消，再检查是否已释放；这个顺序由 `ensureActive` 固定。文件适配器不会在调用之间保留打开的流，所以每个 `use stream` 都在一次操作内部拥有并释放句柄。

如果组合接收任意外部拥有的 `IDisposable` 值，却不说明它是借用还是接管，所有权就会含糊。在 `start` 内构造被拥有值，会让策略清晰可见。

## 保持失败类别分离 {#failure-categories}

| 边界 | 预期表示 | 处理方式 |
|---|---|---|
| 缺少传输字段 | `DtoMappingError` | 返回值；尚不调用领域验证 |
| 无效领域原语或联合载荷 | `DtoMappingError` | 返回值并拒绝重建 |
| 未知模式版本 | `UnsupportedSchemaVersion` | 在解释版本特定载荷前停止 |
| 损坏或过大快照 | `BookingStoreError` | 保留类型化存储分类 |
| I/O 或替换失败 | `BookingStoreError` | 作为运维适配器失败向外传递 |
| 支付拒绝 | `PaymentOutcome.Declined` | 预期服务结果，不是异常 |
| 替身提供商离线 | `DependencyUnavailableException` | 让异步操作进入故障状态，并把替身原因保留为 `InnerException` |
| 调用方取消 | 已取消的 `Task` / `OperationCanceledException` | 传播调用方令牌；不记录新的替身工作 |
| 领域拒绝 | `BookingDecisionError` | 仍留在纯工作流，不进入适配器 |

一个通用 `Error of string` 会抹掉哪个层有权恢复或报告。相反，为每种领域拒绝发明独立异常类，又会把普通业务结果变成出人意料的控制流。

## 用真实边界验证效果 {#testing}

K08b 的八项契约测试只写入唯一的系统临时目录。它们证明真实 JSON 往返、无临时残留的替换、缺失文件行为、严格编码、损坏分类、大小上限、路径验证，以及保存前取消会保留先前完整快照。

K09 的六项测试运行真实文件适配器和确定性替身。它们覆盖授权、拒绝、交付、精确故障、取消且不记录副作用、令牌传递到时钟、通过组合端口持久化、类型化损坏错误、重复释放，以及释放后拒绝使用。

Release 解决方案构建在 F# 10 空值检查和警告即错误下通过。完整示例门会还原锁定依赖、构建每个已注册项目、运行测试与脚本，而且没有引入服务账号或第三方包。

这些证据尚未覆盖 HTTP 输入、并发容量、重试、多预约存储重启或 C# 客户端。它们属于接下来三章，不是本章隐藏的假设。

## 避免常见边界错误 {#boundary-mistakes}

- 直接序列化私有领域记录，会把存储耦合到面向编译器的表示。
- 对携带数据的 F# 联合使用 CLR 枚举，会丢失载荷契约。
- 把 DTO 映射成功当成领域有效，会重复或绕过第 34 章验证。
- 忽略未知 JSON 成员，可能掩盖拼写错误和生产者意外漂移。
- 直接写目标，可能在中断后暴露部分文档。
- 把单文件替换称作数据库事务，夸大了保证范围。
- 从请求数据派生文件名，会引入本设计根本不需要的路径边界。
- 把 `OperationCanceledException` 当作 I/O 失败捕获，会破坏取消语义。
- 随机或延迟替身会让测试不稳定，却不会更真实。
- 在离释放位置很远处构造资源，会让所有权难以审查。
- 从未来 API 直接返回异常消息，可能泄露运维细节。

## 练习 {#exercises}

### 练习 1：演进快照契约 {#exercise-01}

版本 2 必须增加可选的 `customerNote`，同时仍能加载旧版版本 1 文件。提出 DTO 与映射策略。说明版本 1 是在内存中升级、立即重写，还是仅在下一次成功保存时重写。定义未知字段与版本 3 的精确行为。

### 练习 2：审计每个保存中断点 {#exercise-02}

对于发生在以下位置的取消或失败，说明目标文件与临时文件可能包含什么：(a) 创建临时文件前；(b) 写入期间；(c) 刷新之后、移动之前；(d) 移动之后。分开进程可见替换、缓冲区刷新与断电持久性主张。

### 练习 3：消除歧义地改变所有权 {#exercise-03}

假设生产支付与通知客户端由宿主容器创建，并在多个工作流之间共享。重新设计 `Composition.start`，让它借用而不是拥有这些客户端。展示释放移到何处、如何阻止释放后使用，以及确定性测试如何继续显式控制成功、拒绝、故障与取消。

[阅读本章答案](../solutions/ch-35-ports-persistence-config)。

## 模型回顾 {#model-review}

- 端口陈述所需能力；适配器选择机制。
- DTO 是宽松表示，不是领域实体。
- 标签、载荷、字段名称、大小写、空值省略与版本共同形成 JSON 契约。
- 反向映射检查版本、存在性、智能构造函数与合法联合形状。
- 原始命令映射保留领域验证权威。
- 已配置绝对路径不同于机密，也不同于请求输入。
- 有界严格解码会把损坏文件变成显式结果。
- 同目录临时写入、刷新与移动可避免暴露原地写入的部分目标。
- 这种替换不是原子的多操作业务事务。
- 确定性替身控制结果，但不伪装成网络集成。
- 取消会在记录替身副作用前传播。
- 组合根构造、暴露并释放它拥有的值。
- K08 与 K09 证明了本边界；后续章节仍必须证明 HTTP 与一致性。

## 资料来源 {#sources}

- [Microsoft Learn：自定义 `System.Text.Json` 属性名称与枚举表示](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/customize-properties)
- [Microsoft Learn：拒绝未映射 JSON 成员](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/missing-members)
- [FSharp.Core 参考：`CLIMutableAttribute`](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-climutableattribute.html)
- [Microsoft Learn：`File.Move` 重载与跨卷行为](https://learn.microsoft.com/en-us/dotnet/api/system.io.file.move?view=net-10.0)
- [Microsoft Learn：`FileStream.Flush(Boolean)`](https://learn.microsoft.com/en-us/dotnet/api/system.io.filestream.flush?view=net-10.0)
- [Microsoft Learn：.NET 配置提供程序与环境变量优先级](https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration-providers)
- [Microsoft Learn：`CancellationToken`](https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken?view=net-10.0)
