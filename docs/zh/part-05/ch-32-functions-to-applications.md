---
title: "第 32 章：从函数到应用"
description: "通过显式表达配置、端口、组合、取消、所有权和最小可观测性，从纯工作流推导出一个小型可执行 F# 应用。"
translationKey: part-05/ch-32-functions-to-applications
kind: chapter
part: 5
chapter: 32
status: complete
verifiedWith:
  fsharp: "10"
  dotnetSdk: "10.0.301"
exampleIds:
  - ch32-functions-to-applications
exerciseIds:
  - ch32-exercise-01
  - ch32-exercise-02
  - ch32-exercise-03
termIds: []
sources:
  - id: microsoft-dotnet-generic-host
    url: https://learn.microsoft.com/en-us/dotnet/core/extensions/generic-host
    checked: "2026-08-24"
  - id: microsoft-dotnet-configuration
    url: https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration
    checked: "2026-08-24"
  - id: microsoft-dotnet-logging
    url: https://learn.microsoft.com/en-us/dotnet/core/extensions/logging
    checked: "2026-08-24"
  - id: microsoft-dotnet-metrics
    url: https://learn.microsoft.com/en-us/dotnet/core/diagnostics/metrics-instrumentation
    checked: "2026-08-24"
  - id: microsoft-dotnet-metric-collection
    url: https://learn.microsoft.com/en-us/dotnet/core/diagnostics/metrics-collection
    checked: "2026-08-24"
  - id: microsoft-dotnet-tracing
    url: https://learn.microsoft.com/en-us/dotnet/core/diagnostics/distributed-tracing-instrumentation-walkthroughs
    checked: "2026-08-24"
  - id: microsoft-dotnet-di-guidelines
    url: https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/guidelines
    checked: "2026-08-24"
---

# 第 32 章：从函数到应用 {#overview}

纯函数可以决定应该发生什么，但运行中的应用还必须取得配置、调用存储或网络、传播取消、报告发生的事情，并释放它拥有的资源。这些职责不会否定函数式核心，而是在它周围定义了一条边界。

本章会围绕此前开发的预约工作流构建最小而有用的边界。结果是一个没有依赖注入容器、也没有遥测厂商依赖的可执行控制台应用。它的架构直接体现在普通 F# 值和一个组合根中。重点并不是每个应用都应该永远这么小；而是更强的宿主应该解决已经明确的宿主问题，而不是掩盖尚未定义的设计。

## 你将能够做到 {#outcomes}

学完本章后，你应该能够：

- 区分纯领域工作流、应用编排、适配器、组合根和进程宿主；
- 从工作流实际需要的能力而不是框架推导狭窄的副作用端口；
- 在工作开始前，把不可信的配置文本转换成经过验证的领域值；
- 在每个可取消的端口调用之间传播同一个 `CancellationToken`；
- 说明每个 `IDisposable` 资源由谁拥有，以及所有权何时结束；
- 发出一个结构化事件、一个低基数指标和一个追踪活动；
- 区分插桩与收集、导出、存储、仪表板和告警；
- 在不让领域决策变得不纯的前提下测试装配和可观测性；
- 识别本样例证明了什么，以及它刻意缺少哪些生产保证；
- 判断何时显式构造已经足够，何时 .NET Generic Host 的成本是值得的。

## 把一个应用看成多条边界 {#application-boundaries}

“应用”作为推理单元过于粗糙。应按每一层知道哪类事实来分离职责：

| 层 | 知道 | 不负责决定 |
|---|---|---|
| 领域工作流 | 有效命令、预约状态、领域规则、领域事件 | 环境变量、数据库、日志提供程序 |
| 应用编排 | 副作用顺序、取消、预期与非预期结果 | 数据库或遥测后端如何工作 |
| 适配器 | 如何执行一种外部能力 | 预约策略 |
| 组合根 | 哪些具体适配器和设置构成本进程 | 每个请求的业务规则 |
| 进程宿主 | 参数、退出码、进程生命周期和关闭信号 | 领域状态转换 |

依赖方向指向内部。应用层调用领域，并引用小型端口类型；具体适配器从外部提供。领域不会反向调用 `Program`、读取全局配置或选择导出器。

一条有用的执行路径是：

```text
进程输入
  -> 验证配置
  -> 构造适配器和应用
  -> 接收命令与取消
  -> 验证命令
  -> 加载状态
  -> 纯决策
  -> 追加事件
  -> 记录可观察结果
  -> 释放拥有的资源
```

这是一个顺序，并非声称所有步骤都应该放在一个函数中。它揭示了可以讨论测试、故障和所有权的接缝。

## 从所需副作用推导端口 {#derive-ports}

从纯工作流的输入和输出开始。`decidePlaceBooking` 需要一个 `Event`、当前 `BookingState` 和 `PlaceBookingCommand`；它返回 `Result<BookingEvent, PlaceBookingError>`。因此，运行中的应用必须取得当前状态并持久化被接受的事件。样例中只有这两种副作用能力：

<<< @/../examples/chapters/ch32/Ports.fs#ports{fsharp:line-numbers} [Ports.fs]

这条记录包含函数，而不是实现类。每个签名都表达了有用的信息：

- 在存储边界处，`RequestId` 已经是经过验证的领域值；
- `CancellationToken` 是每个可能阻塞操作的显式输入；
- `Task<'T>` 承认 .NET 边界上的异步完成与故障；
- `AppendEvent` 返回 `Task<unit>`，因为应用需要的是完成信号，而不是带有存储形状的响应；
- `OwnedResource` 让本样例的所有权转移可见。

不要仅仅为了模仿接口繁多的架构，就为每个方法创建一个端口。共享同一连贯适配器和生命周期的能力可以放在一起；当调用者、故障策略、安全边界或生命周期确实不同时再拆分。函数记录适合小型、面向 F# 的边界和测试替身。面向 C# 使用者、框架激活或有状态协议时，接口可能更合适。

规则仍由领域拥有。名为 `CanBook` 的端口会把策略推入存储；名为 `LoadBooking` 的端口则提供事实，由纯函数解释。同样，适配器存储 `BookingEvent`，而不决定请求是否可接受。

## 把配置当作不可信输入 {#configuration}

环境变量、JSON、命令行参数和机密存储都始于外部数据。它们存在并不意味着它们是有效的领域配置。应在启动附近只解析并验证一次，再把类型化值向内传递：

<<< @/../examples/chapters/ch32/Ports.fs#configuration{fsharp:line-numbers} [Ports.fs]

`AppConfig.load` 接受查找函数，而不是直接读取 `Environment`。生产代码传入环境查找；固定演示和测试传入确定性函数。这个微小接缝避免了全局可变状态，也不需要配置框架。

加载器会累积 `BOOKING_EVENT_ID` 和 `BOOKING_CAPACITY` 的独立错误。如果两者都错了，运维人员可以在下次启动前一次修复。解析整数只是表示步骤；`Capacity.create` 执行容量必须为正的领域规则。私有 `AppConfig` 记录阻止后续代码直接构造未经验证的配置记录。

配置策略仍需要有意做出选择：

- 明确定义来源优先级，而不是依赖偶然的调用顺序；
- 对每个请求都必需的缺失设置，让启动直接失败；
- 验证范围、格式和跨字段规则，而不只是能否解析；
- 永远不要在错误、日志、指标标签或追踪标签中打印机密值；
- 明确决定设置是启动快照，还是可以重新加载；
- 测试已发布进程最终生效的配置。

更完整的 .NET 配置系统通过 `IConfiguration` 统一 JSON、环境变量、命令行参数、内存值和机密存储等提供程序。在需要提供程序分层和框架集成时使用它。它不能取代向领域特定的已验证类型转换。

## 把构造集中在一个组合根中 {#composition-root}

组合根是选择具体依赖并建立所有权的最外层位置。在样例中，可复用的构造函数刻意保持平淡无奇：

<<< @/../examples/chapters/ch32/Composition.fs#composition-root{fsharp:line-numbers} [Composition.fs]

`Program` 完成其余进程特定工作：选择查找函数、安装演示监听器、构造内存存储、创建应用、运行一条命令，并把结果转换成输出和退出码。领域模块不包含这些选择。

从字面意义上说，手工构造就是依赖注入：依赖通过参数到达。DI 容器会自动化注册、解析、作用域和释放；控制反转并不是由容器创造的。即使日后由容器完成构造，保留组合根仍然有价值。

不要在领域或应用函数内部从全局服务定位器解析依赖。那会把需求从签名中隐藏起来，使生命周期含糊不清，并迫使测试重建环境状态。显式参数让依赖图可以审查。

## 围绕纯决策编排副作用 {#orchestration}

应用方法拥有执行顺序，同时复用现有领域工作流：

<<< @/../examples/chapters/ch32/Composition.fs#place{fsharp:line-numbers} [Composition.fs]

按顺序阅读该方法：

1. 拒绝释放后的使用，并启动一个可能为空的活动。
2. 在调用端口前观察取消。
3. 验证原始命令，使存储收到类型化的 `RequestId`。
4. 用调用者的令牌加载当前状态。
5. 调用 `decidePlaceBooking` 完成领域决策。
6. 只追加被接受的领域事件，并使用同一个令牌。
7. 记录一个终结结果，并返回领域结果。
8. 观察取消或非预期故障，然后重新抛出。
9. 在 `finally` 中释放活动，覆盖每条退出路径。

独立的领域函数会再次验证命令。这重复的是一次廉价纯操作，而不是验证规则：两次都调用 `validatePlaceBooking`。第一次调用是在副作用前取得类型化键；公共工作流在被独立调用时仍然安全。日后的公共 API 可以暴露接受 `ValidPlaceBooking` 的工作流，但前提是这一边界能改善整个模型。

预期的业务拒绝仍然是 `Error PlaceBookingError`。取消仍然是 `OperationCanceledException`，因此 .NET 调用者和宿主能把它识别为取消。非预期的适配器故障仍然是故障任务。把三者都转成一个无法区分的 `Result` 会抹除运维含义。

样例不会重试。重试策略必须知道操作是否为暂时故障，以及追加是否幂等。没有幂等键时重试一次结果不明确的写入，可能重复事件。只有在这些语义定义清楚后，才应在边界加入策略。

## 让生命周期所有权显式可见 {#lifecycle}

每个可释放对象都需要一个所有者。“它会被回收”不是生命周期规则：`Dispose` 往往释放句柄、套接字、缓冲区、订阅或遥测状态，垃圾回收不会及时释放这些资源。

样例建立了如下契约：`Composition.start` 接收 `BookingPorts`，得到的 `BookingApplication` 拥有 `ports.OwnedResource`。释放应用时，会且只会一次释放该资源、它的 `ActivitySource` 和 `Meter`。`Program` 使用 `use app = ...`，因此正常完成和异常都会通过确定性清理离开作用域。

这条契约刻意保持可见，但它并非普遍规则：

| 构造规则 | 资源所有者 | 接收者的行为 |
|---|---|---|
| 应用为自身生命周期显式创建适配器 | 应用或外围组合根 | 工作排空后释放 |
| 调用者传入共享适配器且不转移所有权 | 调用者 | 接收者不得释放 |
| DI 容器创建已注册的可释放服务 | 容器/作用域 | 使用者不得释放 |
| 工厂创建短生命周期资源 | 使用工厂结果的作用域 | 在该作用域使用 `use`/`use!` |

如果清理会执行异步 I/O，应建模为 `IAsyncDisposable`，并在适当的任务表达式中使用 `use!`。如果仍有请求正在运行，关闭流程必须先停止接收新工作、发出取消信号、允许有上界的排空，最后才释放依赖。这个仅执行一条命令的小进程没有并发排空协议。

释放不等于崩溃恢复。`SIGKILL`、断电或进程终止可以绕过清理。持久正确性必须来自存储事务、幂等性和恢复规则，而不是一个 `finally` 块。

## 加入最小而有用的可观测性 {#observability}

可观测性从问题开始，而不是从产品开始。样例提出三个不同问题，并使用三种不同信号：

| 信号 | 问题 | 样例证据 | 基数指导 |
|---|---|---|---|
| 结构化日志事件 | 一次具名尝试中发生了什么？ | 结果、请求 ID、座位数、细节 | 标识符可以是可搜索字段，但受隐私和保留策略约束 |
| 计数器测量 | 各种结果分别结束了多少次尝试？ | `booking.requests{outcome=accepted}` +1 | 保持标签值有界；绝不加入请求 ID |
| 追踪活动 | 该操作把时间花在哪里，又如何结束？ | 带结果和请求 ID 的 `booking.place` | 每次操作的上下文是合适的，但仍要遵守采样与敏感数据策略 |

这些信号彼此补充，而不是相互复制。指标以低成本提供趋势和告警，追踪关联路径中的工作，日志保留离散诊断事件。它们都无法修复错误的领域规则。

### 结构化日志保留字段 {#structured-logs}

`BookingLog` 是一条记录，演示会把它的字段序列化成一个 JSON 对象。这比构建无法解析的散文句子更强，因为收集器可以分别保留 `eventName`、`outcome`、`requestId`、`seats` 和 `detail` 字段。

但它仍然只是教学适配器。生产应用通常会用稳定的消息模板、事件 ID、级别、作用域、脱敏和已配置提供程序，把这个事件映射到 `ILogger`。提供程序决定日志去往何处；仅有控制台输出并不是持久存储。在日志系统能够过滤之前，不要插值机密或不受控负载。

样例把被接受或拒绝的领域决策分类为已完成操作，把抛出的适配器异常分类为 `faulted`。拒绝并不自动等于警告或追踪错误：“超出容量”可以是普通业务结果。严重程度应由运维处置方式决定，而不是由联合类型案例的拼写决定。

### 指标发布测量；收集器负责聚合 {#metrics}

诊断名称是稳定常量，应用创建一个计数器：

<<< @/../examples/chapters/ch32/Composition.fs#diagnostics-names{fsharp:line-numbers} [Composition.fs]

`Counter<int64>.Add` 发布一次增量。它本身不会创建历史存储、速率图、保留策略或告警。收集工具聚合测量，并可以把它们导出到后端。演示中的 `MeterListener` 只观察一次进程内测量，以便样例和测试证明插桩确实触发。

`outcome` 标签在本应用中有四个有界值：`accepted`、`rejected`、`canceled` 和 `faulted`。请求 ID 被刻意排除。指标系统通常会为每一种标签组合分配时间序列；无界 ID 会造成过多内存、存储和成本。

对只会增加的发生次数使用计数器。当持续时间或大小分布及其尾部很重要时，使用直方图。不要把延迟编码成一对平均值计数器，除非其局限性可以接受。定义单位和描述，让收集器不必猜测。

### 追踪围绕有意义的工作 {#traces}

没有感兴趣的监听器时，`ActivitySource.StartActivity` 可能返回 `null`。样例把这视为正常情况，不会解引用它。活动存在时，释放就会停止它；`finally` 确保成功、拒绝、取消和故障的每条路径都有一个完成的活动。

活动会记录低歧义标签和状态。被接受和被拒绝的决策都按设计完成，因此状态是 `Ok`；非预期故障是 `Error`；取消保持独立。生产约定可以细化这些选择，但应在服务间保持一致。

创建 `ActivitySource` 是插桩，而不是分布式追踪收集。OpenTelemetry 等收集器必须订阅、采样、扩充、批处理并导出活动。本地 `ActivityListener` 证明一个活动已停止；它不能证明跨进程传播、后端交付、保留或有用的追踪查询。

## 谨慎解读固定证据 {#fixed-evidence}

完成 Release 构建后运行确定性演示：

```console
dotnet examples/chapters/ch32/bin/Release/net10.0/Ch32.App.dll --demo
```

它精确输出：

```text
{"eventName":"booking.place","outcome":"accepted","requestId":"REQ-32","seats":2,"detail":"event-appended"}
result: accepted=true
metric: name=booking.requests value=1 outcome=accepted
trace: name=booking.place outcome=accepted
lifecycle: store-disposed=true
```

这证明一条固定命令经过了配置、组合、纯决策、内存追加、三种本地可观察信号和确定性释放。聚焦测试还证明：独立配置错误会累积，同一个取消令牌到达两个端口，一个被接受的事件被追加，而预先取消的令牌不会调用端口。

它不能证明日志或遥测到达外部后端、适配器持久可靠，或并发请求能保持容量。`LoadBooking` 后接 `AppendEvent` 并不是原子事务。两个调用者可能在任一方追加前读到同一状态。内存适配器是装配演示，而不是生产预约存储。

## 测试边界而不重复测试领域 {#boundary-tests}

应用测试应观察应用层独有的职责：

- 多个独立错误的设置会产生所有相关启动错误；
- 无效命令不会产生存储副作用；
- 被接受的决策恰好追加一个事件；
- 被拒绝的决策不追加事件；
- 调用者的取消令牌到达每个端口；
- 预先取消会避开端口调用，并保持为取消；
- 适配器故障仍是故障，同时发出所选择的终结信号；
- 每次尝试恰好发出一个终结指标/日志结果；
- 当监听器采样时，每条路径上的活动都会停止；
- 声明的所有者会且只会一次释放每个拥有的资源。

把领域规则样例和性质测试留在领域测试中。边界测试可以使用确定性的函数记录和跟踪型可释放对象，而不是真实数据库或遥测厂商。独立的集成测试随后应证明每个生产适配器的协议、序列化和故障行为。

不要让每个测试都断言每种信号。一个聚焦契约测试可以固定遥测形状；大多数编排测试应强调副作用和结果。否则，无害的措辞变化会在整个测试套件中制造噪声。

## 知道何时需要更强的宿主 {#stronger-host}

对于命令行工具、小型单用途进程、由另一宿主调用的库内工作器，或依赖和生命周期一屏可见的早期应用，显式组合根通常已经足够。

当进程同时需要多项标准设施时，.NET Generic Host 就会变得有用：

- 分层配置提供程序和环境约定；
- 日志提供程序、过滤和作用域；
- DI 注册、作用域和由容器拥有的释放；
- 多个 `IHostedService` 或 `BackgroundService` 工作器；
- 协调的启动、关闭信号和优雅停止；
- 本就期望宿主服务的框架集成。

对于新的非 Web 宿主应用，当前 .NET 指南推荐 `Host.CreateApplicationBuilder`。Web 应用通常使用建立在相关宿主设施上的 `WebApplicationBuilder`。选择任何一个都不会把领域规则移入服务或控制器。应保留纯工作流、狭窄端口、类型化配置和组合边界。

当容器管理真实对象图和作用域时，它很有价值。为了解析三个一目了然的值而加入容器，通常只会增加间接性，并未解决问题。反过来，手工构建数十个有作用域的服务和关闭回调，也可能是在拙劣地重造一个更弱的容器。让依赖数量、生命周期多样性、框架集成和运维需求来决定。

## 审查应用边界 {#review-checklist}

在认为宿主完整之前，请逐项询问：

- 领域能否在没有环境、网络、文件系统、时钟或遥测全局量时运行？
- 每种副作用是否都出现在一个使用领域相关输入且显式接收取消的端口中？
- 外部字符串是否在长生命周期资源启动前就被解析为经过验证的值？
- 是否只有一个清晰可见的组合根？
- 每个可释放对象是否恰好有一个有文档说明的所有者？
- 业务拒绝、取消和非预期故障是否仍然可以区分？
- 日志是否保留字段并排除机密？
- 指标标签组合是否有界且具有运维意义？
- 活动缺失时，行为能否保持不变？
- 生产中的插桩是否连接到真实的收集/导出路径？
- 并发、幂等、重试和恢复保证是否被明确说明，而不是暗示？
- 更强的框架是否移除了已经证实的生命周期工作，而不只是把它换了位置？

## 练习 {#exercises}

### 练习 1：推导端口与所有权 {#exercise-01}

一个纯函数 `decideDispatch : Inventory -> Order -> Result<Dispatch, DispatchError>` 已经可以在工作器中运行。为加载库存和提交发货推导最小端口。说明类型、取消行为、预期错误边界，以及可释放数据库会话的所有者。不要引入容器。

### 练习 2：设计三种可观察信号 {#exercise-02}

为发货尝试定义一个结构化日志事件、一个指标和一个活动。选择名称、字段或标签以及终结结果。识别哪些值有界、哪些是高基数、哪些可能敏感。解释本地监听器能证明什么，以及哪些内容仍需收集器/导出器测试。

### 练习 3：选择宿主层级 {#exercise-03}

为以下场景在显式构造与 Generic Host 之间选择：（a）导入一个文件后退出的命令；（b）运行三个后台消费者，并需要优雅关闭、配置分层和日志提供程序的进程；（c）ASP.NET Core API。说明每个选择的理由，并指出哪些架构边界应该保持不变。

[阅读本章练习答案](../solutions/ch-32-functions-to-applications)。

## 模型回顾 {#model-review}

- 函数式核心负责决定；应用边界取得事实并执行副作用。
- 端口描述必需能力和领域相关数据，而不是框架对象。
- 在解析与领域验证成功前，配置始终不可信。
- 手工构造就是依赖注入；容器只是可选的自动化。
- 一个组合根让实现和所有权可见。
- 取消原样通过每个可取消副作用传播。
- 业务拒绝、取消和故障具有不同的运维含义。
- 每个可释放对象都有一个所有者；关闭必须先排空工作，再释放资源。
- 日志、指标和追踪回答不同问题。
- 插桩产生信号；监听器、收集器、导出器、存储和告警是独立关注点。
- 指标维度必须有界；每请求标识符属于受控日志或追踪，而不是指标标签。
- 固定演示证明装配，而不证明持久性、原子性、恢复或后端交付。
- 更强宿主应由真实的配置、作用域、工作器和关闭需求来证明必要性。

## 来源 {#sources}

- [Microsoft Learn：.NET Generic Host 的职责与生命周期](https://learn.microsoft.com/en-us/dotnet/core/extensions/generic-host)
- [Microsoft Learn：配置提供程序与 `IConfiguration`](https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration)
- [Microsoft Learn：结构化日志与消息模板](https://learn.microsoft.com/en-us/dotnet/core/extensions/logging)
- [Microsoft Learn：`Meter`、插桩、标签与基数指导](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/metrics-instrumentation)
- [Microsoft Learn：指标收集、聚合与导出](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/metrics-collection)
- [Microsoft Learn：`ActivitySource`、可空活动、标签与收集](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/distributed-tracing-instrumentation-walkthroughs)
- [Microsoft Learn：DI 所有权与释放指南](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/guidelines)
