---
title: "第 38 章：集成、诊断、C# 客户端与发布证据"
description: "用真实组合根、HTTP 集成测试、C# 契约客户端、受限诊断和可复现发布证据闭合预约系统。"
translationKey: part-06/ch-38-integration-diagnostics-release
kind: chapter
part: 6
chapter: 38
status: complete
verifiedWith:
  fsharp: "10"
  dotnetSdk: "10.0.301"
exampleIds:
  - capstone-booking-domain
  - capstone-booking-contracts
  - capstone-booking-infrastructure
  - capstone-booking-api
  - capstone-booking-csharp-client
  - foundation-contract-tests
exerciseIds:
  - ch38-exercise-01
  - ch38-exercise-02
  - ch38-exercise-03
termIds: []
sources:
  - id: microsoft-aspnet-integration-tests
    url: https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0
    checked: "2026-08-25"
  - id: microsoft-tracing-instrumentation
    url: https://learn.microsoft.com/en-us/dotnet/core/diagnostics/distributed-tracing-instrumentation-walkthroughs
    checked: "2026-08-25"
  - id: microsoft-metrics-instrumentation
    url: https://learn.microsoft.com/en-us/dotnet/core/diagnostics/metrics-instrumentation
    checked: "2026-08-25"
  - id: microsoft-aspnet-logging
    url: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/logging/?view=aspnetcore-10.0
    checked: "2026-08-25"
  - id: microsoft-dotnet-publishing
    url: https://learn.microsoft.com/en-us/dotnet/core/deploying/
    checked: "2026-08-25"
---

# 第 38 章：集成、诊断、C# 客户端与发布证据 {#overview}

前几章从内向外构建了预约系统：精确的领域模型逐步成为纯决策器、端口与适配器、HTTP 边界，最后成为一致性协议。但任何一个单独层次都无法证明可执行程序确实按预期顺序使用了它们。本章将闭合这处缺口。

目标不是再增加一种架构模式，而是连接唯一的组合根，让另一种 .NET 语言穿过公开契约，在不暴露敏感数据的前提下观察结果，并把这些证据变成他人可复现的一条命令。最终产物仍然是教学系统；它的价值有一部分恰恰来自精确说明它**没有**证明什么。

## 学完本章后，你将能够 {#outcomes}

学完本章后，你应该能够：

- 区分组件测试、进程内 HTTP 测试与独立进程冒烟测试；
- 验证生产入口点选择了预期的一致性服务；
- 把传输策略留在端点层，把业务策略留在类型化端口之后；
- 用 C# 客户端检验公开 CLR 与 JSON 契约，而不是 F# 内部实现；
- 在不使用无界指标维度的前提下关联响应、结构化日志、指标与追踪；
- 解释为什么插桩点不等于遥测后端；
- 设计有助于调查、却不记录命令正文或含机密值的日志；
- 让发布检查具有确定性、边界和自动清理能力；
- 区分 `build`、`publish`、部署与生产就绪；
- 维护一份已证明保证、明确限制和后续证据的台账。

## 把可执行程序读成组合证明 {#composition-proof}

组合根回答一个具体问题：运行中的进程究竟会使用哪些实现？如果可执行程序把旧工作流接在外层，再漂亮的领域函数和再强的适配器测试也无济于事。

第 37 章有意保留了这处缺口。较早的 `BookingEndpoints.map` 路径接收 `AsyncPorts`，无法提供聚合级幂等与容量保证。最终入口点则构造 `AtomicBookingStore`、受控支付与通知适配器以及 `IdempotentBookingService`，然后只向 HTTP 层暴露两个操作。

<<< @/../examples/capstone/src/Booking.Api/Program.fs#api-host{fsharp:line-numbers} [Program.fs]

从外到内阅读这段代码：

1. 监听器启动前先解析启动配置。
2. Kestrel 获得请求正文上限，并关闭会标识服务器的响应头。
3. 一个存储和一个服务共同负责一致性与外部效果次序。
4. 诊断中间件包住已映射的端点。
5. `mapConsistent` 接收函数，而不是获得越过服务直接访问存储的权限。
6. `application.Run()` 是最后一个长时间存活的效果。

本地替身刻意保持醒目。`PaymentStubBehavior.Authorize` 不会因为藏在函数类型之后就变成真实支付。组合让选中的能力可以被审阅，却不会提升这种能力本身。

### 保留唯一的 HTTP 策略表面 {#http-policy-surface}

最终集成没有复制四个端点。`map` 与 `mapConsistent` 共享正文上限、严格反序列化、DTO 映射、验证、成功序列化、路由提取与安全错误边界，只有命令执行和读取方式不同。

<<< @/../examples/capstone/src/Booking.Api/Endpoints.fs#endpoint-map{fsharp:line-numbers} [Endpoints.fs]

`ConsistentBookingApiDependencies` 是用函数记录表达的窄适配器接口。端点层知道执行会返回 `Result<Booking, BookingConsistencyError>`，却不知道快照如何加锁或替换。穷尽模式匹配把每种已声明错误翻译成稳定状态码与 `ApiErrorDto` 代码。

这个边界也形成了实用的测试接缝。HTTP 契约测试可以提供受控函数，可执行程序则可以提供真实本地服务；两条路径都不需要服务定位器或可变全局依赖。

## 建立证据阶梯 {#evidence-ladder}

除非能说明每个测试穿过了哪个边界，否则“测试通过”并不完整。本项目采用几个有意重叠的层次：

| 证据层次 | 穿过的真实组件 | 可以支持的主张 | 无法支持的主张 |
|---|---|---|---|
| 纯示例/性质测试 | 领域值、决策器、映射 | 规则对示例与生成输入成立 | 文件、HTTP 与进程启动可用 |
| 适配器契约测试 | 严格 JSON、快照文件、配置 | 本地持久化和映射遵守契约 | 并发副本安全 |
| 一致性测试 | 聚合存储、服务、受控效果 | 建模的竞争、重试和重启阶段按规格运行 | 公开 HTTP 正确映射全部结果 |
| 进程内 HTTP 测试 | ASP.NET Core 管道、DTO、最终服务、文件适配器 | 状态、正文、响应头、持久化与效果能组合 | 套接字、命令行启动及另一进程可用 |
| 独立进程冒烟 | 真实 Kestrel 套接字与独立 C# 进程 | 从源码构建后，公开流程可在本机启动 | 生产拓扑、真实提供商或故障转移可用 |

Microsoft 的 [ASP.NET Core 集成测试指南](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0) 把集成测试描述为包含请求管道和支撑基础设施的较宽测试，同时建议把常规逻辑留在更快的单元测试中。这正是建立阶梯的原因，而不是让所有排列都经过 HTTP 的理由。

### 让 HTTP 效果在测试中可观察 {#http-effects}

端到端夹具构建真实 `WebApplication`，选择 `TestServer`，注册相同诊断中间件，映射相同的一致性端点，并使用临时快照。受控支付与通知函数通过线程安全计数器记录调用。

四项测试建立了以下事实：

- 规范化后完全相同的放置命令重放相同 `201` 正文，而且不重复效果；
- 同一操作身份下变更座位数会返回 `409 idempotency_conflict`；
- 无效 JSON 在创建快照或调用效果之前返回 `400 invalid_json`；
- 结果不明的支付首次返回 `503`，随后返回 `409 payment_outcome_unknown`，支付只调用一次；
- 诊断测试把响应相关 ID、受限指标和一个已停止的子活动对齐起来。

前两个事实放在同一项测试中，因为效果计数器才是因果观察。只断言响应会漏掉藏在重放正文之后的重复支付。

`TestServer` 在内存中传送 HTTP 抽象，因此管道测试快速且确定，但它刻意绕过端口分配、TLS 和内核网络。发布冒烟测试于是增加了第二种更小的测试，穿过真实回环套接字。

### 用信号代替延迟猜测 {#causal-tests}

收官项目其他位置的并发测试使用屏障和任务完成信号，迫使两个操作共同进入危险区间；重启测试则针对持久快照启动真正独立的进程。这些事实强于“多运行几次，期待调度器恰好倒霉”。

重复仍有价值：它能发现共享状态泄漏和非确定性清理。但它不能代替控制定义缺陷的因果交错。

## 从 C# 证明公开契约 {#csharp-contract}

F# 与 C# 共享 CLR，却不具有完全相同的使用体验。公开 F# API 可能成功编译，却暴露普通 C# 调用者难以使用的柯里化函数、F# 专用联合、选项或泛型形状。第 27 章设计了单独的 CLR 友好 DTO；本章让真实 C# 可执行程序消费它们。

客户端只直接引用 `Booking.Contracts`，从不引用 `Booking.Domain` 或 `Booking.Infrastructure`，而且只通过 `HttpClient` 与 JSON 和服务通信。

<<< @/../examples/capstone/clients/Booking.CSharpClient/Program.cs#csharp-http-contract-client{csharp:line-numbers} [Program.cs]

这一条流程检查四项契约性质：

| 步骤 | 契约证据 |
|---|---|
| 放置 | 对象初始化器能构造 DTO；JSON 得到 `201` 与待确认预约 |
| 精确重放 | 应用幂等返回相同的已确认状态码与正文 |
| 确认 | 另一个 DTO 穿过同一边界并产生可表示的已确认响应 |
| GET | URL 转义与响应 DTO 反序列化无需了解 F# 领域类型即可工作 |

客户端刻意配置严格且区分大小写的反序列化，并拒绝未映射属性。这是在测试所选契约的兼容性，不是每个消费者都必须照搬的规则。对成功原始正文的比较也很窄：它证明当前契约版本产生确定输出，而不是宣称属性顺序不同的任意 JSON 文本在语义上不等价。

一个成功的 C# 客户端不能证明与每个历史程序集版本都二进制兼容；这需要保留消费者夹具，或让 API 兼容性工具对照已声明基线。它确实证明当前发布表面可用于最重要的外语种路径。

## 给边界插桩，不给机密插桩 {#diagnostics}

请求失败时，操作者首先需要少量答案：运行了哪个操作边界、何时运行、耗时多久、发生哪类结果，以及哪个追踪能连接这些证据？记录整个命令虽然方便，却可能把诊断系统变成数据泄漏源。

预约中间件用稳定字段名记录完成事件：

```text
Booking request completed correlationId=<trace-id> method=<method> endpoint=<route-template> statusCode=<status> outcome=<outcome> elapsedMs=<duration>
```

它不记录请求或响应正文、预约请求 ID、确认码、提供商交易文本、异常消息或快照路径。HTTP 响应获得 `X-Correlation-ID`。存在活动 `Activity` 时，该值是其 32 字符 W3C 追踪 ID；否则中间件创建同样受限格式的随机追踪 ID。

### 相关 ID 是连接键，不是身份证明 {#correlation}

同一个相关值出现在响应头、结构化完成事件、日志作用域和自定义活动标签中。客户端因此可以报告一个值，操作者也可以用它连接多种诊断信号。

传入的有效追踪上下文可能影响传播后的追踪 ID。因此，相关值不是认证、授权、请求所有权或可信业务标识。受限的十六进制格式能防止任意响应头文本进入日志，但访问控制与保留策略仍然不可缺少。

日志使用事件 ID `1000` 和预编译 `LoggerMessage` 模板。稳定名称使查询保持耐久，也避免把结构化字段变成一个不透明的插值句子。ASP.NET Core 日志作用域可以跨嵌套日志调用携带上下文值，平台还可以包含活动追踪与跨度 ID；参见官方[日志文档](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/logging/?view=aspnetcore-10.0)。

### 让指标维度保持有限 {#metric-cardinality}

自定义 `Meter` 只暴露：

| 仪器 | 单位 | 记录的维度 |
|---|---|---|
| `booking.http.requests` 计数器 | `{request}` | `outcome` |
| `booking.http.duration` 直方图 | `ms` | `outcome` |

`outcome` 只有四个受控值：`success`、`client_error`、`server_error` 与 `canceled`。请求 ID、含 ID 的路径、相关 ID、异常消息和提供商值都不是指标维度，否则每个请求都可能创建新时间序列，耗尽监控后端的基数预算。

中间件把端点显示名记录为路由模板，例如 `HTTP: GET /api/bookings/{requestId}`，而不是具体 URL。目前该值只进入追踪和日志，不进入自定义指标。

`IMeterFactory` 来自依赖注入，也会让不同测试服务提供器的 Meter 相互隔离。Microsoft 的 [.NET 指标指南](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/metrics-instrumentation) 推荐这种面向宿主的模式，并分别用计数器表示总量、用直方图表示分布。

### 把活动视为可选插桩对象 {#activity-lifecycle}

只有监听器感兴趣时，`ActivitySource.StartActivity` 才创建内部 `booking.http.request` 子活动；它可能返回 `null`，但请求仍必须运行。因此中间件对标签和状态做空值检查，并在 `finally` 中释放已创建活动，使成功与失败路径都会停止它。

这个子活动在 ASP.NET Core 服务器活动之下增加预约专用结果标签。只有这些标签能回答追踪问题时才值得这样做。如果团队对内建服务器跨度和增强日志已经满意，就可以省略子活动，而不是制造冗余跨度。插桩应当服务于调查目的。

官方 [.NET 追踪指南](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/distributed-tracing-instrumentation-walkthroughs) 同样说明了返回 `null` 的行为，以及释放活动会停止它。

最重要的是，`Meter`、`ActivitySource` 和日志调用只是生产者，不会自动创建收集器、持久存储、仪表板、告警、保留策略或访问策略。样例通过 `MeterListener` 与 `ActivityListener` 测试信号生产；部署仍须单独配置并测试收集过程。

## 把证明收束为一条命令 {#release-check}

在仓库的 JavaScript 依赖已经安装后，收官项目验收命令是：

```console
pnpm check:capstone
```

脚本通过参数数组而不是拼接 shell 命令来启动程序。它创建名称唯一的临时目录，让 API 在 `127.0.0.1` 的 `0` 号端口启动，读取实际监听地址，并在 `finally` 中清理精确的子进程与目录。

它的阶段有意按以下顺序排列：

1. 以锁定模式还原解决方案；
2. 不再次还原，以 `Release` 构建完整解决方案；
3. 运行完全限定名包含 `Booking` 的全部测试；
4. 使用全新本地快照和确定性替身启动真实 API；
5. 让独立 C# 客户端完成放置、重放、确认与 GET；
6. 用另一个 HTTP 客户端发送格式错误的 JSON；
7. 把它的 32 字符响应相关 ID 与客户端错误日志匹配；
8. 要求至少一条成功日志，并拒绝已知含机密文本；
9. 即使失败，也停止服务器并删除临时快照。

成功结尾形如：

```text
Capstone check passed.
Placed: id=REQ-CAPSTONE-CHECK seats=2 status=pending
Replay: status=201 same-body=True
Confirmed: id=REQ-CAPSTONE-CHECK code=CONF-CSHARP status=confirmed
Loaded: status=200 same-body=True
Diagnostics: success=true client-error=true correlation=<32 lowercase hex characters> secrets=false
```

这些输出是紧凑见证，不是完整测试报告。失败时只包含有界的尾部输出，防止失控子进程无限占用内存；进程启动和 HTTP 调用也都有超时。

### 从干净状态复现 {#clean-state}

`examples/capstone/README.md` 列出了精确前置条件、冻结包安装、单命令检查，以及 Bash/zsh 和 PowerShell 手工流程。手工路径适合检查日志或单步跟踪请求；自动路径的价值则在于控制名称、端口、超时、断言与清理。

“无需外部服务”意味着流程不需要云账号、私有源、支付提供商、消息代理或遥测后端。当本地缓存为空时，锁定还原仍可能下载公开 NuGet 包。输入可复现不代表离线缓存一定存在。

不要为了让手工命令看起来方便，就让读者删除一个宽泛目录。README 创建唯一的可丢弃目录，并在 API 停止后只删除该精确路径；生产数据绝不能成为清理目标。

## 不要把构建称为部署 {#build-publish-deploy}

“发布检查”这个名字描述的是验收门。它目前不会运行 `dotnet publish`、创建签名产物、生成 SBOM、扫描容器、部署环境、迁移存储或验证回滚。

各阶段含义不同：

| 阶段 | 回答的问题 |
|---|---|
| 还原 | 声明并锁定的依赖能否解析？ |
| 构建 | 源码能否在所选配置下编译？ |
| 测试 | 被检查的行为能否在测试环境成立？ |
| 发布 | 针对选定运行模型与目标，会产生哪些可部署文件？ |
| 部署 | 某环境能否用真实配置和依赖运行这个不可变产物？ |
| 运维 | 负责人能否检测、缓解、恢复故障并从中学习？ |

Microsoft 的 [.NET 发布概述](https://learn.microsoft.com/en-us/dotnet/core/deploying/) 区分依赖框架与自包含发布，也说明特定运行时和单文件选项。应根据部署环境、修补模型、目标操作系统/架构、启动与体积要求作选择，不能默默把开发机的 `dotnet run` 输出当成产物契约。

### 定义缺失的生产门 {#production-gate}

这个服务在处理真实预约前，具体系统至少需要为以下事项作出决定并取得证据：

- 调用方认证、授权策略、TLS 终止、速率限制与滥用处理；
- 真实机密注入、轮换、脱敏与最小权限访问；
- 多进程事务式或条件式存储、模式迁移、备份、还原、RPO 与 RTO；
- 支付提供商幂等，以及结果不明时的对账；
- 事务性发件箱、消费者去重、死信处理与重放策略；
- 能反映必要依赖且不泄露内部信息的健康/就绪行为；
- 遥测导出、采样、基数预算、保留、仪表板、告警与归属；
- 版本化发布产物、来源证明、漏洞审阅、晋级与回滚演练；
- 面向类生产依赖的负载、故障注入、重启及部署拓扑测试。

这份清单不是要求把每一种机制都加进教学仓库，而是一份边界清单。只有存在命名明确的需求与测试环境时，架构才应该增长。

## 维护保证台账 {#guarantee-ledger}

现在，收官项目可以作出以下窄而经过测试的主张：

- 受保护的 F# 构造器与决策器执行已建模的预约状态和转换；
- 严格 DTO 映射会在领域工作前拒绝格式错误与未知的传输数据；
- 同一进程、同一规范化快照路径中的协作服务不会超卖聚合容量；
- 完全相同的已完成命令会重放，不重复已建模的支付或通知调用；
- 同一操作身份下改变载荷会冲突；
- 取消会释放已提交占用，被取消的等待也会释放同步资源；
- 结果不明的支付不会被盲目重复；
- 有序重启能加载持久进度并重放已完成结果；
- 最终 HTTP 路由映射一致性服务，而不是早期的纯端口工作流；
- 当前 C# 消费者无需领域引用即可完成公开 JSON 流程；
- 受控日志、测量和活动能关联成功或被拒绝的请求；
- 声明的公开包可用后，完整本地检查无需外部运行时账号或服务即可运行。

同一份台账必须保留限制：

- 文件适配器不支持多个进程或多台机器共同写入；
- 整体替换不是一般性的 ACID 或掉电持久保证；
- 替身不会真实扣款或发送消息；
- 通知与跨系统效果不是恰好一次；
- 尚未实现支付对账与预留过期；
- 缺少认证、TLS 策略、机密管理和滥用控制；
- 插桩没有配置导出器或运维后端；
- 发布检查既不发布也不部署产物；
- 不主张任何生产 SLO、RPO、RTO、规模边界或受支持升级路径。

把两半放在一起，才能避免测试列表变成营销语言。一项主张应当说明它的拓扑、依赖、故障模型和观察方式。

## 注意 F# 为闭环贡献了什么 {#fsharp-role}

最终组合仍然体现了这门语言的优势：领域类型阻止任意非法状态，`Result` 让预期失败成为端点匹配的一部分，函数记录形成小端口，`task` 把取消穿过 HTTP 与 I/O，模式匹配让错误到状态码的表格可审阅，确定性序列化则为另一门语言提供普通契约。

F# 也让策略核心保持小于宿主成为自然选择。可执行程序绝大部分只是接线。C# 客户端证明，这种内部风格并不要求每个外部消费者采用 F# 表示。

语言本身不会选择生产数据库、让提供商变得幂等、导出遥测、保护网络或运维部署。类型的成熟用法是暴露这些剩余边界，而不是用通用“效果”抽象把它们藏起来。

## 避免常见的闭环错误 {#common-mistakes}

- 测试了服务却忘记把它接入可执行程序，会留下虚假的绿色路径。
- 为最终服务重写端点验证，会让两套公开路径发生漂移。
- 把 `TestServer` 称为真实网络测试，会忽略套接字、启动参数和进程寿命。
- 只使用真实进程冒烟测试，会让失败场景缓慢且难以控制。
- 让 C# 客户端引用领域内部实现，会破坏契约测试的意义。
- 把请求 ID 或相关 ID 放进指标，会产生无界基数。
- “暂时”记录正文往往会形成永久敏感数据存储。
- 假定 `StartActivity` 非空，会让行为取决于是否安装监听器。
- 在没有调查问题时创建自定义跨度，只会增加噪声与成本。
- 检查日志里一个禁用字面量，只能证明该夹具，并不是通用机密扫描器。
- 把 `dotnet build -c Release` 称为发布产物，会跳过目标与运行时决策。
- 为了让样例看似完整而加入生产基础设施，只会模糊而非修复其边界。

## 练习 {#exercises}

### 练习 1：审计三项夸大主张 {#exercise-01}

一份发布说明写道：“预约 API 在三个副本之间是安全的，支付与通知都恰好执行一次，而且所有测试已通过，所以系统已经生产就绪。”请把它重写成保证台账。对每项主张指出最强现有证据、缺失的拓扑或依赖、下一项机制，以及能产生缺失证据的测试。不要只是把每句话都替换成“不保证”。

### 练习 2：设计不欠下基数债务的收集方案 {#exercise-02}

服务将使用兼容 OpenTelemetry 的收集器。请在不改变领域模型的情况下设计配置与验证工作：选择订阅哪些内建及自定义源、哪些属性可以成为指标维度、如何采样、什么必须脱敏，以及日志如何连接追踪。指定一项自动测试和一项能发现基数错误的负载测试，并判断自定义子活动是否值得其成本。

### 练习 3：把检查变成真实发布计划 {#exercise-03}

选择一个明确目标，例如依赖框架的 Linux 容器，或自包含 `linux-x64` 服务。把验收门扩展成发布、晋级、部署与回滚计划。说明不可变产物、运行时/配置契约、存储迁移策略、健康门、安全检查、冒烟测试、遥测检查、推出策略和回滚触发条件，并指出哪些步骤可以留在本地、哪些必须使用类生产环境。

[阅读本章练习答案](../solutions/ch-38-integration-diagnostics-release)。

## 本章回顾 {#chapter-review}

- 组合根证明可执行程序选择了预期实现。
- 共享端点表面能阻止传输策略在不同编排版本间漂移。
- 纯、适配器、一致性、进程内 HTTP 与独立进程测试支持不同主张。
- 效果计数器使“不重复副作用”成为可观察事实，而不是从响应推断出来。
- C# HTTP 客户端在不暴露 F# 领域内部实现的情况下证明当前公开 DTO 路径。
- 相关 ID 用于连接证据，不代表调用者身份或授权。
- 指标需要受限维度，高基数细节应留在受控追踪或日志中。
- 插桩源在收集、存储、策略和责任归属建立前不会产生运维能力。
- `pnpm check:capstone` 是具有边界清理的可复现本地验收门。
- 构建、发布、部署和运维是具有不同证据的不同阶段。
- 保证台账必须同时保留已证明行为和明确限制。
- F# 使策略与边界精确；生产保证仍来自真实基础设施与运维。

第六部分至此完成。第七部分会把这套基础映射到更广的 F# 与 .NET 生态，同时避免假装每个有用库都应进入同一个应用。
