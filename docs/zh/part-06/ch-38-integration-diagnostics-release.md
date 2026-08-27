---
title: "第 38 章：集成、诊断、C# 客户端与发布验证"
description: "通过真实组合根、HTTP 集成测试、C# 契约客户端、受限诊断和可复现检查，验证完整预约流程。"
translationKey: part-06/ch-38-integration-diagnostics-release
---

# 第 38 章：集成、诊断、C# 客户端与发布验证 {#overview}

前几章从内向外构建了预约系统：领域模型、纯决策器、端口与适配器、HTTP API，最后是一致性协议。但任何一层都无法单独确认可执行程序按预期顺序连接了它们。本章补上这项验证。

本章连接唯一的组合根，让另一种 .NET 语言调用公开契约，并在不暴露敏感数据的前提下观察结果。最后用一条可复现命令验证完整路径。成品仍是教学系统，因此还要准确说明它**没有**验证什么。

## 在可执行程序中验证组合 {#composition-proof}

组合根回答一个具体问题：运行中的进程究竟会使用哪些实现？如果可执行程序把旧工作流接在外层，再漂亮的领域函数和再强的适配器测试也无济于事。

第 37 章有意保留了这处缺口。较早的 `BookingEndpoints.map` 接收 `AsyncPorts`，无法提供聚合级幂等与容量保证。最终入口点改为构造 `AtomicBookingStore`、受控支付与通知适配器以及 `IdempotentBookingService`。它只向 HTTP 层暴露两个操作。

```fsharp:line-numbers [Program.fs]
[<EntryPoint>]
let main arguments =
    match StartupConfiguration.load () with
    | Error error ->
        eprintfn "Booking API startup configuration is invalid (%s)." (errorCode error)
        2
    | Ok configuration ->
        let builder = WebApplication.CreateBuilder arguments

        builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning) |> ignore
        BookingDiagnostics.add builder.Services

        builder.WebHost.ConfigureKestrel(
            Action<KestrelServerOptions>(fun options ->
                options.AddServerHeader <- false
                options.Limits.MaxRequestBodySize <- int64 BookingEndpoints.MaxRequestBodyBytes)
        )
        |> ignore

        let store = AtomicBookingStore configuration.Store
        use payment = new PaymentStub(PaymentStubBehavior.Authorize "TX-LOCAL-STUB")
        use notification = new NotificationStub(NotificationStubBehavior.Deliver)

        let service =
            IdempotentBookingService(configuration.Activity, store, payment.Invoke, notification.Invoke)

        use application = builder.Build()

        BookingDiagnostics.useMiddleware application

        BookingEndpoints.mapConsistent
            application
            { Execute = fun command token -> service.Execute(command, token)
              Load = fun requestId token -> service.Load(requestId, token) }

        application.Run()
        0
```
从外到内阅读这段代码：

1. 监听器启动前先解析启动配置。
2. Kestrel 获得请求正文上限，并关闭会标识服务器的响应头。
3. 一个存储和一个服务共同负责一致性与外部副作用顺序。
4. 诊断中间件包住已映射的端点。
5. `mapConsistent` 接收函数，而不是获得越过服务直接访问存储的权限。
6. `application.Run()` 是最后一个长时间运行的副作用。

本地替身刻意保持醒目。`PaymentStubBehavior.Authorize` 不会因为藏在函数类型之后就变成真实支付。组合让选中的能力可以被审阅，却不会提升这种能力本身。

### 让 HTTP 策略只有一个实现入口 {#http-policy-surface}

最终集成没有复制四个端点。`map` 与 `mapConsistent` 共享正文上限、严格反序列化、DTO 映射、验证、成功序列化、路由提取与安全错误边界，只有命令执行和读取方式不同。

```fsharp:line-numbers [Endpoints.fs]
let private mapHandlers (application: WebApplication) place confirm cancel load =
    ArgumentNullException.ThrowIfNull(application, nameof application)

    let protectedHandler handler =
        RequestDelegate(fun context -> safely handler context)

    application.MapPost("/api/bookings/place", protectedHandler place) |> ignore

    application.MapPost("/api/bookings/confirm", protectedHandler confirm) |> ignore

    application.MapPost("/api/bookings/cancel", protectedHandler cancel) |> ignore

    application.MapGet("/api/bookings/{requestId}", protectedHandler load) |> ignore

let map (application: WebApplication) (dependencies: BookingApiDependencies) =
    let execute = executeCommand dependencies

    mapHandlers
        application
        (handlePlaceWith execute)
        (handleConfirmWith execute)
        (handleCancelWith execute)
        (handleGet dependencies)

let mapConsistent (application: WebApplication) (dependencies: ConsistentBookingApiDependencies) =
    let execute = executeConsistent dependencies

    mapHandlers
        application
        (handlePlaceWith execute)
        (handleConfirmWith execute)
        (handleCancelWith execute)
        (handleConsistentGet dependencies)
```
`ConsistentBookingApiDependencies` 是用函数记录表达的小型适配器接口。端点层知道执行会返回 `Result<Booking, BookingConsistencyError>`，却不知道快照如何加锁或替换。穷尽模式匹配把每种已声明错误转换成稳定状态码与 `ApiErrorDto` 代码。

这个边界也方便测试。HTTP 契约测试可以提供受控函数，可执行程序则可以提供真实本地服务；两条路径都不需要服务定位器或可变全局依赖。

## 建立分层验证路径 {#evidence-ladder}

必须说明每个测试穿过了哪些组件，“测试通过”才有准确含义。本项目采用几个有意重叠的层次：

| 测试层次 | 穿过的真实组件 | 支持的结论 | 不支持的结论 |
|---|---|---|---|
| 纯示例/属性测试 | 领域值、决策器、映射 | 规则对示例与生成输入成立 | 文件、HTTP 与进程启动可用 |
| 适配器契约测试 | 严格 JSON、快照文件、配置 | 本地持久化和映射遵守契约 | 并发副本安全 |
| 一致性测试 | 聚合存储、服务、受控副作用 | 建模的竞争、重试和重启阶段按规格运行 | 公开 HTTP 正确映射全部结果 |
| 进程内 HTTP 测试 | ASP.NET Core 管道、DTO、最终服务、文件适配器 | 状态、正文、响应头、持久化与副作用能组合 | 套接字、命令行启动及另一进程可用 |
| 独立进程冒烟 | 真实 Kestrel 套接字与独立 C# 进程 | 从源码构建后，公开流程可在本机启动 | 生产拓扑、真实提供商或故障转移可用 |

Microsoft 的 [ASP.NET Core 集成测试指南](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0) 将集成测试定义为覆盖请求管道与支撑基础设施的较宽测试。它同时建议把常规逻辑留在更快的单元测试中。因此要分层验证，而不是让所有排列都经过 HTTP。

### 在测试中观察 HTTP 副作用 {#http-effects}

端到端测试环境构建真实 `WebApplication`，选择 `TestServer`，注册相同诊断中间件，映射相同的一致性端点，并使用临时快照。受控支付与通知函数通过线程安全计数器记录调用。

聚焦集成测试表明：

- 规范化后完全相同的放置命令重放相同 `201` 正文，而且不重复副作用；
- 同一操作身份下变更座位数会返回 `409 idempotency_conflict`；
- 无效 JSON 在创建快照或调用副作用之前返回 `400 invalid_json`；
- 结果不明的支付首次返回 `503`，随后返回 `409 payment_outcome_unknown`，支付只调用一次；
- 诊断测试把响应关联 ID、受限指标和一个已停止的子活动对齐起来。

前两个结论放在同一项测试中，因为副作用计数器才能揭示因果关系。只断言响应会漏掉藏在重放正文后的重复支付。

`TestServer` 在内存中传送 HTTP 抽象，因此管道测试快速且确定，但它刻意绕过端口分配、TLS 和内核网络。发布冒烟测试于是增加了第二种更小的测试，穿过真实回环套接字。

### 用信号代替延迟猜测 {#causal-tests}

最终项目其他位置的并发测试使用屏障和任务完成信号，迫使两个操作共同进入风险窗口；重启测试则针对持久快照启动真正独立的进程。这些事实强于“多运行几次，期待调度器恰好触发问题”。

重复仍有价值：它能发现共享状态泄漏和非确定性清理。但它不能代替控制定义缺陷的因果交错。

## 从 C# 验证公开契约 {#csharp-contract}

F# 与 C# 共享 CLR，但使用方式不同。公开 F# API 即使能编译，也可能暴露 C# 调用者难以使用的柯里化函数、F# 联合、选项或泛型结构。第 27 章设计了 CLR 友好的 DTO；本章由真实 C# 程序使用它们。

客户端只引用 `Booking.Contracts`，不引用 `Booking.Domain` 或 `Booking.Infrastructure`。它仅通过 `HttpClient` 和 JSON 与服务通信。

```csharp:line-numbers [Program.cs]
var place = new PlaceBookingDto
{
    RequestId = requestId,
    Seats = 2
};

using var placedResponse = await client.PostAsJsonAsync("api/bookings/place", place, json);
var placed = await ReadBooking(placedResponse, json);
Require(placed.Status == HttpStatusCode.Created, "Place must return 201 Created.");
Require(placed.Booking.RequestId == requestId, "Place request ID round-trip.");
Require(placed.Booking.Seats == 2, "Place seat count round-trip.");
Require(placed.Booking.Status == "pending", "Placed booking must be pending.");

using var replayedResponse = await client.PostAsJsonAsync("api/bookings/place", place, json);
var replayed = await ReadBooking(replayedResponse, json);
Require(replayed.Status == HttpStatusCode.Created, "Exact replay must return the acknowledged status.");
Require(replayed.Body == placed.Body, "Exact replay must return the acknowledged booking.");

var confirm = new ConfirmBookingDto
{
    RequestId = requestId,
    ConfirmationCode = "CONF-CSHARP"
};

using var confirmedResponse = await client.PostAsJsonAsync("api/bookings/confirm", confirm, json);
var confirmed = await ReadBooking(confirmedResponse, json);
Require(confirmed.Status == HttpStatusCode.OK, "Confirm must return 200 OK.");
Require(confirmed.Booking.Status == "confirmed", "Confirmed booking status.");
Require(confirmed.Booking.ConfirmationCode == "CONF-CSHARP", "Confirmation code round-trip.");

var escapedRequestId = Uri.EscapeDataString(requestId);
using var loadedResponse = await client.GetAsync($"api/bookings/{escapedRequestId}");
var loaded = await ReadBooking(loadedResponse, json);
Require(loaded.Body == confirmed.Body, "GET must return the current confirmed booking.");
```
这一条流程检查四项契约性质：

| 步骤 | 契约检查 |
|---|---|
| 放置 | 对象初始化器能构造 DTO；JSON 得到 `201` 与待确认预约 |
| 原样重放 | 应用幂等返回相同的已确认状态码与正文 |
| 确认 | 另一个 DTO 穿过同一边界并产生可表示的已确认响应 |
| GET | URL 转义与响应 DTO 反序列化无需了解 F# 领域类型即可工作 |

客户端刻意使用严格、区分大小写的反序列化，并拒绝未映射属性。这是在测试所选契约，不要求每个消费者照搬。比较成功响应的原始正文，只能确认当前版本输出稳定；属性顺序不同的 JSON 文本仍可能语义相同。

C# 客户端成功运行，不代表与每个历史程序集版本都二进制兼容。后者需要保留消费者测试项目，或用 API 兼容性工具对照已声明基线。当前检查只确认主要跨语言路径可用。

## 为边界插桩，但绝不采集机密 {#diagnostics}

请求失败时，操作者只需先回答几个问题：哪个操作在何时运行、耗时多久、结果类别是什么，以及哪条追踪能连接相关信号？记录整个命令虽然方便，却可能让诊断系统泄露数据。

预约中间件用稳定字段名记录完成事件：

```text
Booking request completed correlationId=<trace-id> method=<method> endpoint=<route-template> statusCode=<status> outcome=<outcome> elapsedMs=<duration>
```

它不记录请求或响应正文、预约请求 ID、确认码、提供商交易文本、异常消息或快照路径。HTTP 响应获得 `X-Correlation-ID`。存在活动 `Activity` 时，该值是其 32 字符 W3C 追踪 ID；否则中间件创建同样受限格式的随机追踪 ID。

### 关联 ID 用于串联信号，不是身份凭据 {#correlation}

同一个关联值出现在响应头、结构化完成事件、日志作用域和自定义活动标签中。客户端因此可以报告一个值，操作者也可以用它串联多种诊断信号。

传入的有效追踪上下文可能影响传播后的追踪 ID。因此，关联值不能用于认证、授权、判断请求属于谁，也不是可信业务标识。受限的十六进制格式能防止任意响应头文本进入日志，但访问控制与保留策略仍然不可缺少。

日志使用事件 ID `1000` 和预编译 `LoggerMessage` 模板。稳定名称让查询不受显示文案变化影响，也避免把结构化字段拼成难以查询的字符串。ASP.NET Core 日志作用域可以跨嵌套日志调用携带上下文值，平台还可以包含活动追踪与跨度 ID；参见官方[日志文档](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/logging/?view=aspnetcore-10.0)。

### 让指标维度保持有限 {#metric-cardinality}

自定义 `Meter` 只暴露：

| 仪器 | 单位 | 记录的维度 |
|---|---|---|
| `booking.http.requests` 计数器 | `{request}` | `outcome` |
| `booking.http.duration` 直方图 | `ms` | `outcome` |

`outcome` 只有四个受控值：`success`、`client_error`、`server_error` 与 `canceled`。请求 ID、含 ID 的路径、关联 ID、异常消息和提供商值都不是指标维度，否则每个请求都可能创建新时间序列，耗尽监控后端的基数预算。

中间件把端点显示名记录为路由模板，例如 `HTTP: GET /api/bookings/{requestId}`，而不是具体 URL。目前该值只进入追踪和日志，不进入自定义指标。

`IMeterFactory` 来自依赖注入，也会让不同测试服务提供程序的 Meter 相互隔离。Microsoft 的 [.NET 指标指南](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/metrics-instrumentation) 推荐这种面向宿主的模式，并分别用计数器表示总量、用直方图表示分布。

### 把 `Activity` 视为可选插桩对象 {#activity-lifecycle}

只有监听器感兴趣时，`ActivitySource.StartActivity` 才创建内部 `booking.http.request` 子活动；它可能返回 `null`，但请求仍必须运行。因此中间件对标签和状态做空值检查，并在 `finally` 中释放已创建活动，使成功与失败路径都会停止它。

这个子活动在 ASP.NET Core 服务器活动之下增加预约专用结果标签。只有这些标签能回答追踪问题时才值得这样做。如果团队对内建服务器跨度和增强日志已经满意，就可以省略子活动，而不是制造冗余跨度。插桩应当服务于调查目的。

官方 [.NET 追踪指南](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/distributed-tracing-instrumentation-walkthroughs) 同样说明了返回 `null` 的行为，以及释放活动会停止它。

最重要的是，`Meter`、`ActivitySource` 和日志调用只是生产者，不会自动创建收集器、持久存储、仪表板、告警、保留策略或访问策略。样例通过 `MeterListener` 与 `ActivityListener` 测试信号生产；部署仍须单独配置并测试收集过程。

## 把验证收束为一条命令 {#release-check}

真实应用应把验收路径收束为一条有文档的命令。对于 .NET 解决方案，基础命令可以是：

```console
dotnet test Sample.slnx --configuration Release
```

若验收还需要独立 API 进程与客户端，应用脚本应创建名称唯一的临时目录，并让 API 在 `127.0.0.1` 的可用端口启动。脚本必须在 `finally` 中清理对应的子进程与目录。这种编排属于应用，不属于本书站点。

稳健的验收命令应有意按以下顺序排列阶段：

1. 以锁定模式还原解决方案；
2. 不再次还原，以 `Release` 构建完整解决方案；
3. 运行完全限定名包含 `Booking` 的全部测试；
4. 使用全新本地快照和结果固定的测试替身启动真实 API；
5. 让独立 C# 客户端完成放置、重放、确认与 GET；
6. 用另一个 HTTP 客户端发送格式错误的 JSON；
7. 把它的 32 字符响应关联 ID 与客户端错误日志匹配；
8. 要求至少一条成功日志，并拒绝已知含机密文本；
9. 即使失败，也停止服务器并删除临时快照。

简洁的最终报告可以形如：

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

应用的 README 应列出明确的前置条件、单命令检查与手工调试流程。手工路径适合检查日志或单步跟踪请求；自动路径的价值则在于控制名称、端口、超时、断言与清理。

“无需外部服务”意味着流程不需要云账号、私有源、支付提供商、消息代理或遥测后端。当本地缓存为空时，锁定还原仍可能下载公开 NuGet 包。输入可复现不代表离线缓存一定存在。

不要为了让手工命令看起来方便，就让读者删除一个宽泛目录。应创建唯一的可丢弃目录，并在 API 停止后只删除该目录；生产数据绝不能成为清理目标。

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

这个服务处理真实预约前，具体系统至少要决定并验证：

- 调用方认证、授权策略、TLS 终止、速率限制与滥用处理；
- 真实机密注入、轮换、脱敏与最小权限访问；
- 多进程事务式或条件式存储、模式迁移、备份、还原、RPO 与 RTO；
- 支付提供商幂等，以及结果不明时的对账；
- 事务性发件箱、消费者去重、死信处理与重放策略；
- 能反映必要依赖且不泄露内部信息的健康/就绪行为；
- 遥测导出、采样、基数预算、保留、仪表板、告警与归属；
- 版本化发布产物、来源记录、漏洞审阅、晋级与回滚演练；
- 面向类生产依赖的负载、故障注入、重启及部署拓扑测试。

这份清单不是要求把每一种机制都加进教学仓库，而是一份边界清单。只有存在命名明确的需求与测试环境时，架构才应该增长。

## 维护已验证保证清单 {#guarantee-ledger}

现在，最终项目可以作出以下范围明确且经过测试的主张：

- 受保护的 F# 构造器与决策器执行已建模的预约状态和转换；
- 严格 DTO 映射会在领域工作前拒绝格式错误与未知的传输数据；
- 同一进程、同一规范化快照路径中的协作服务不会超卖聚合容量；
- 完全相同的已完成命令会重放，不重复已建模的支付或通知调用；
- 同一操作身份下改变载荷会冲突；
- 取消会释放已提交占用，被取消的等待也会释放同步资源；
- 结果不明的支付不会被盲目重复；
- 正常重启能加载持久进度并重放已完成结果；
- 最终 HTTP 路由映射一致性服务，而不是早期的纯端口工作流；
- 当前 C# 消费者无需领域引用即可完成公开 JSON 流程；
- 受控日志、测量和活动能关联成功或被拒绝的请求；
- 声明的公开包可用后，完整本地检查无需外部运行时账号或服务即可运行。

同一份台账必须保留限制：

- 文件适配器不支持多个进程或多台机器共同写入；
- 整体替换不是一般性的 ACID 或掉电持久保证；
- 替身不会真实扣款或发送消息；
- 通知与跨系统副作用不是恰好一次；
- 尚未实现支付对账与预留过期；
- 缺少认证、TLS 策略、机密管理和滥用控制；
- 插桩没有配置导出器或运维后端；
- 发布检查既不发布也不部署产物；
- 不主张任何生产 SLO、RPO、RTO、规模边界或受支持升级路径。

把两半放在一起，才能避免测试列表变成营销语言。一项主张应当说明它的拓扑、依赖、故障模型和观察方式。

## 注意 F# 为完整流程贡献了什么 {#fsharp-role}

最终组合仍然体现了 F# 的优势。领域类型阻止任意非法状态，`Result` 让端点直接匹配预期失败。函数记录形成精简端口，`task` 把取消传递到 HTTP 与 I/O。模式匹配展示错误到状态码的映射，确定性序列化则为另一门语言提供简单契约。

F# 也让策略核心自然地小于宿主；可执行程序绝大部分只负责连接组件。C# 客户端确认，这种内部风格不要求外部消费者采用 F# 表示。

语言本身不会选择生产数据库、让提供商变得幂等、导出遥测、保护网络或运维部署。成熟的类型设计应暴露这些剩余约束，而不是用通用“副作用”抽象把它们藏起来。

## 避免常见的最终集成错误 {#common-mistakes}

- 测试了服务却忘记把它接入可执行程序，会造成测试通过但实际程序未使用它的假象。
- 为最终服务重写端点验证，会让两套公开路径发生漂移。
- 把 `TestServer` 称为真实网络测试，会忽略套接字、启动参数和进程寿命。
- 只使用真实进程冒烟测试，会让失败场景缓慢且难以控制。
- 让 C# 客户端引用领域内部实现，会破坏契约测试的意义。
- 把请求 ID 或关联 ID 放进指标，会产生无界基数。
- “暂时”记录正文往往会形成永久敏感数据存储。
- 假定 `StartActivity` 非空，会让行为取决于是否安装监听器。
- 在没有调查问题时创建自定义跨度，只会增加噪声与成本。
- 检查日志中的一个禁用字面量，只能覆盖当前测试数据，不能替代通用机密扫描。
- 把 `dotnet build -c Release` 称为发布产物，会跳过目标与运行时决策。
- 为了让样例看似完整而加入生产基础设施，只会模糊而非修复其边界。

## 练习 {#exercises}

### 练习 1：审计三项夸大主张 {#exercise-01}

一份发布说明写道：“预约 API 在三个副本之间是安全的，支付与通知都恰好执行一次，而且所有测试已通过，所以系统已经生产就绪。”请把它重写成已验证保证清单。对每项主张指出最强现有证据、缺失的拓扑或依赖、下一项机制，以及能产生缺失证据的测试。不要只是把每句话都替换成“不保证”。

### 练习 2：设计不欠下基数债务的收集方案 {#exercise-02}

服务将使用兼容 OpenTelemetry 的收集器。请在不改变领域模型的情况下设计配置与验证工作：选择订阅哪些内建及自定义源、哪些属性可以成为指标维度、如何采样、什么必须脱敏，以及日志如何连接追踪。指定一项自动测试和一项能发现基数错误的负载测试，并判断自定义子活动是否值得其成本。

### 练习 3：把检查变成真实发布计划 {#exercise-03}

选择一个明确目标，例如依赖框架的 Linux 容器或自包含 `linux-x64` 服务。把验收检查扩展为发布、晋级、部署与回滚计划，并说明：

- 不可变产物与运行时/配置契约；
- 存储迁移策略；
- 健康门、安全检查、冒烟测试与遥测检查；
- 推出策略和回滚触发条件；
- 哪些步骤可留在本地，哪些必须使用类生产环境。

[阅读本章练习答案](../solutions/ch-38-integration-diagnostics-release)。

## 本章回顾 {#chapter-review}

- 组合根确认可执行程序选择了哪些实现。
- 共享端点 API 能阻止传输策略在不同编排版本间漂移。
- 纯、适配器、一致性、进程内 HTTP 与独立进程测试支持不同主张。
- 副作用计数器直接显示是否发生重复调用，无需从响应推断。
- C# HTTP 客户端在不暴露 F# 领域内部实现的情况下验证当前公开 DTO 路径。
- 关联 ID 用于串联诊断信号，不代表调用者身份或授权。
- 指标需要受限维度，高基数细节应留在受控追踪或日志中。
- 插桩源在收集、存储、策略和责任归属建立前不会产生运维能力。
- 一条有文档的验收命令应负责清理，并在任一必要阶段失败时整体失败。
- 构建、发布、部署和运维是不同阶段，需要不同检查。
- 已验证保证清单必须同时保留已确认行为和明确限制。
- F# 让策略与边界写得更明确；生产保证仍来自真实基础设施与运维。

第六部分至此完成。第七部分会把这套基础映射到更广的 F# 与 .NET 生态，同时避免假装每个有用库都应进入同一个应用。
