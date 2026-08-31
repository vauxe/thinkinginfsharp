---
title: "第 38 章：集成、诊断、C# 客户端与发布验证"
description: "连接预约系统的各层，并用 HTTP 测试、C# 客户端、诊断信息和可重复检查验证连接结果。"
translationKey: part-06/ch-38-integration-diagnostics-release
---

# 第 38 章：集成、诊断、C# 客户端与发布验证 {#overview}

前几章分别介绍了业务模型、纯决策函数、端口与适配器、HTTP API 和一致性处理。不过，分别测试这些部分，并不能证明最终程序把它们正确地连接在了一起。本章要检查的就是这件事。

本章会把具体实现集中在一个地方连接起来，再用 C# 程序调用公开 API。它还会说明如何记录必要的诊断信息而不泄露敏感数据。最后，我们把这些检查整理成一条可以重复执行的命令，同时说明它**不能**证明什么。

本章仍然是页面示例，不要求你组装完整项目。当前仓库没有完整的 Booking 解决方案、可运行 API、C# 客户端或下文提到的验收脚本。如果以后实现它，`Booking.Api` 可以在引用前三层后，按 `Diagnostics.fs` → `Endpoints.fs` → `Program.fs` 的顺序编译。

`Program.fs` 依赖第 36 章的启动配置和端点，以及第 37 章的存储和服务。`Diagnostics.fs` 还需要 `System.Diagnostics`、`System.Diagnostics.Metrics` 与 ASP.NET Core 日志/依赖注入类型。

下面几个词来自软件架构和可观测性，并不是 F# 语法名称：

- **组合根**：集中选择具体实现并连接所有组件的地方，本例中就是程序入口；
- **关联 ID**：把同一次请求的响应、日志和追踪连接起来的诊断编号；
- **指标基数**：一个指标可能产生的不同标签组合数量；
- **插桩**：在程序中加入日志、指标或追踪信号。

F# 在这里用函数记录表示边界接口，用模式匹配转换结果，并用 `task` 连接请求中的异步步骤。

## 在可执行程序中验证组合 {#composition-proof}

组合根回答一个具体问题：“程序运行时，实际使用的是哪些实现？”即使业务函数和适配器都分别通过了测试，程序入口接错了服务，最终行为仍然会错。

第 37 章有意保留了这处缺口。较早的 `BookingEndpoints.map` 接收 `AsyncPorts`，无法提供聚合级幂等与容量保证。建议的最终入口点改为构造 `AtomicBookingStore`、受控支付与通知适配器以及 `IdempotentBookingService`。它只向 HTTP 层暴露两个操作。

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

必须说明每个测试穿过了哪些组件，“测试通过”才有准确含义。将参考设计落成项目时，应使用几个有意重叠的层次：

| 测试层次 | 穿过的真实组件 | 支持的结论 | 不支持的结论 |
|---|---|---|---|
| 纯示例/属性测试 | 领域值、决策器、映射 | 规则对示例与生成输入成立 | 文件、HTTP 与进程启动可用 |
| 适配器契约测试 | 严格 JSON、快照文件、配置 | 本地持久化和映射遵守契约 | 并发副本安全 |
| 一致性测试 | 聚合存储、服务、受控副作用 | 建模的竞争、重试和重启阶段按规格运行 | 公开 HTTP 正确映射全部结果 |
| 进程内 HTTP 测试 | ASP.NET Core 管道、DTO、最终服务、文件适配器 | 状态、正文、响应头、持久化与副作用能组合 | 套接字、命令行启动及另一进程可用 |
| 独立进程冒烟 | 真实 Kestrel 套接字与独立 C# 进程 | 从源码构建后，公开流程可在本机启动 | 生产拓扑、真实提供商或故障转移可用 |

Microsoft 的 [ASP.NET Core 集成测试指南](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0) 将集成测试定义为覆盖请求管道与支撑基础设施的较宽测试。它同时建议把常规逻辑留在更快的单元测试中。因此要分层验证，而不是让所有排列都经过 HTTP。

### 在测试中观察 HTTP 副作用 {#http-effects}

端到端测试环境应构建真实 `WebApplication`，选择 `TestServer`，注册相同诊断中间件，映射相同的一致性端点，并使用临时快照。受控支付与通知函数通过线程安全计数器记录调用。

最低的聚焦集成测试应证明：

- 规范化后完全相同的放置命令重放相同 `201` 正文，而且不重复副作用；
- 同一操作身份下变更座位数会返回 `409 idempotency_conflict`；
- 无效 JSON 在创建快照或调用副作用之前返回 `400 invalid_json`；
- 结果不明的支付首次返回 `503`，随后返回 `409 payment_outcome_unknown`，支付只调用一次；
- 诊断测试把响应关联 ID、受限指标和一个已停止的子活动对齐起来。

前两个结论应放在同一项测试中，因为副作用计数器才能揭示因果关系。只断言响应会漏掉藏在重放正文后的重复支付。

`TestServer` 在内存中传送 HTTP 抽象，因此管道测试快速且确定，但它刻意绕过端口分配、TLS 和内核网络。因此还应加入第二种更小的发布冒烟测试，穿过真实回环套接字。

### 用信号代替延迟猜测 {#causal-tests}

如果以后实现项目，并发测试应使用屏障和任务完成信号，主动让两个操作同时进入有风险的阶段。重启测试则应针对同一份快照启动真正独立的进程。这样可以直接复现要检查的情况，比反复运行并期待调度器偶然触发问题更可靠。

重复仍有价值：它能发现共享状态泄漏和非确定性清理。但它不能代替控制定义缺陷的因果交错。

## 从 C# 验证公开契约 {#csharp-contract}

F# 与 C# 共享 CLR，但使用方式不同。公开 F# API 即使能编译，也可能暴露 C# 调用者难以使用的柯里化函数、F# 可区分联合、选项或泛型结构。第 27 章设计了 CLR 友好的 DTO；项目落地后，应由独立 C# 程序真正消费它们。

建议的客户端只引用 `Booking.Contracts`，不引用 `Booking.Domain` 或 `Booking.Infrastructure`。它仅通过 `HttpClient` 和 JSON 与服务通信。

下方代码是 C# 控制台顶级程序的中段，不是完整文件。它假定前面已经导入 `System.Net`、`System.Net.Http.Json` 和 Contracts 命名空间；还已经创建 `requestId`、`HttpClient client`、`JsonSerializerOptions json`，并定义 `ReadBooking` 与 `Require` 辅助函数。

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
这条流程应检查四项契约性质：

| 步骤 | 契约检查 |
|---|---|
| 放置 | 对象初始化器能构造 DTO；JSON 得到 `201` 与待确认预约 |
| 原样重放 | 应用幂等返回相同的已确认状态码与正文 |
| 确认 | 另一个 DTO 穿过同一边界并产生可表示的已确认响应 |
| GET | URL 转义与响应 DTO 反序列化无需了解 F# 领域类型即可工作 |

客户端刻意使用严格、区分大小写的反序列化，并拒绝未映射属性。这是在测试所选契约，不要求每个消费者照搬。比较成功响应的原始正文，只能确认当前版本输出稳定；属性顺序不同的 JSON 文本仍可能语义相同。

该 C# 客户端验收通过，也不代表与每个历史程序集版本都二进制兼容。后者需要保留消费者测试项目，或用 API 兼容性工具对照已声明基线。这项验收只能确认主要跨语言路径可用。

## 为边界插桩，但绝不采集机密 {#diagnostics}

请求失败时，维护人员首先需要回答几个简单问题：

- 哪个操作在什么时间运行？
- 它用了多长时间？
- 结果属于成功、客户端错误、服务端错误还是取消？
- 哪个追踪 ID 能把相关日志和指标连接起来？

为回答这些问题，不需要记录整个命令。完整命令中可能含有业务数据，把它写进诊断系统会造成泄露风险。

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

`outcome` 只有四个固定值：`success`、`client_error`、`server_error` 和 `canceled`。不要把请求 ID、具体请求路径、关联 ID、异常消息或提供商返回值作为指标标签。这些值几乎每次请求都不同，会不断创建新的时间序列，最终给监控系统带来很大负担。

中间件把端点显示名记录为路由模板，例如 `HTTP: GET /api/bookings/{requestId}`，而不是具体 URL。目前该值只进入追踪和日志，不进入自定义指标。

`IMeterFactory` 来自依赖注入，也会让不同测试服务提供程序的 Meter 相互隔离。Microsoft 的 [.NET 指标指南](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/metrics-instrumentation) 推荐这种面向宿主的模式，并分别用计数器表示总量、用直方图表示分布。

### 把 `Activity` 视为可选插桩对象 {#activity-lifecycle}

只有监听器订阅了这个信号，`ActivitySource.StartActivity` 才会创建内部的 `booking.http.request` 子活动。否则它可能返回 `null`，但 HTTP 请求仍要正常执行。因此，中间件先检查活动是否存在，再写标签和状态；如果创建了活动，则在 `finally` 中结束它。

这个子活动在 ASP.NET Core 服务器活动之下增加预约专用结果标签。只有这些标签能回答追踪问题时才值得这样做。如果团队对内建服务器跨度和增强日志已经满意，就可以省略子活动，而不是制造冗余跨度。插桩应当服务于调查目的。

官方 [.NET 追踪指南](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/distributed-tracing-instrumentation-walkthroughs) 同样说明了返回 `null` 的行为，以及释放活动会停止它。

最重要的是，`Meter`、`ActivitySource` 和日志调用只是生产者，不会自动创建收集器、持久存储、仪表板、告警、保留策略或访问策略。项目落地后，可用 `MeterListener` 与 `ActivityListener` 验证信号生产；部署仍须单独配置并测试收集过程。

## 把验证收束为一条命令 {#release-check}

真实应用应把验收路径收束为一条有文档的命令。对于 .NET 解决方案，基础命令可以是：

```console
dotnet test path/to/YourSolution.slnx --configuration Release
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

这里的“发布检查”只是发布前的一组验收步骤。它不会运行 `dotnet publish`，也不会创建签名产物、生成 SBOM、扫描容器、部署环境、迁移存储或测试回滚。通过这项检查不等于系统已经部署。

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

## 规划验收后的保证清单 {#guarantee-ledger}

只有在页内设计被实现、且上述验收全部通过后，项目才可以作出以下范围明确的主张：

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

## 练习 {#exercises}

### 练习 1：审计三项夸大主张 {#exercise-01}

假设一支团队已经实现本章的项目，并完成了这里介绍的验收。一份发布说明写道：

> 预约 API 在三个副本之间是安全的。支付与通知都恰好执行一次。所有测试都已通过，因此系统已经可以投入生产。

请把这段话改写成一份有证据支持的保证清单。对每项主张分别写出：当前最强的证据、尚未覆盖的部署方式或外部依赖、下一项需要加入的机制，以及验证该机制的测试。不要简单地把所有内容改成“不保证”。


::: details 参考答案

#### 区分已有验证与尚未覆盖的边界 {#exercise-01-ledger}

先建立一张四列表格：

| 夸大主张 | 当前最强验证 | 缺失机制或边界 | 下一项决定性测试 |
|---|---|---|---|
| 在三个副本之间安全 | 同一进程中，共享规范化文件路径的多个服务对象执行并发命令时不会超卖 | 独立进程共享的事务式/条件式存储；按活动划分的聚合版本 | 针对真实存储启动三个宿主，迫使它们读取同一版本，再同时放行竞争条件写入，最后验证已提交占用 |
| 支付与通知恰好一次 | 完全相同的已完成重试不会重复调用受控替身；结果不明的支付不会盲目重试；待发送通知意图能经过正常重启 | 提供商幂等与查询；事务性发件箱；至少一次中继；消费者原子去重；对账 | 在每个提供商/发件箱确认边界终止进程，然后比较提供商记录、发件箱行、发布次数与消费者状态 |
| 因为测试通过，所以生产就绪 | 锁定 Release 构建、聚焦测试、TestServer HTTP 集成、真实本地 Kestrel 与 C# 冒烟、已脱敏样例日志 | 安全、真实依赖、发布产物、迁移、拓扑、负载边界、遥测后端、SLO/RPO/RTO、推出与恢复 | 把不可变候选部署到类生产环境，执行安全、迁移、负载、依赖故障、还原、推出与回滚门 |

第一项现有结论仍然有用：**在一个进程与一个规范化快照路径内，协作服务实例会串行化聚合容量决策**。尚未验证的范围从操作系统进程边界开始。

第二项原有结论应拆成两个：**完全相同的已完成操作会重放本地结果，而不重复已建模的替身调用**；以及**结果不明的支付会停下等待对账，而不会再次扣款**。两句话都没有声称真实提供商或通知消费者做了什么。

应用自己的检查通过后，第三项只能写成：**应用为已经说明的本地运行方式提供了可重复的验收检查**。要声称“可以投入生产”，还要明确环境、流量、安全、依赖、可用性、持久性和责任分工，并分别进行验证。

#### 重写发布说明 {#exercise-01-rewrite}

在所述验收路径成功运行后，一份可辩护的说明可以写成：

> 预约收官项目现已通过锁定的本地验收门。已验证拓扑是一个 API 进程使用一个本地快照路径和受控支付/通知适配器。完全相同的已完成重试不会重复这些适配器调用，变化的载荷会冲突，结果不明的支付会停下等待对账，C# HTTP 消费者可完成公开流程。多进程存储、真实提供商交付、安全控制、遥测导出、产物部署和生产恢复仍在本次发布范围之外。

这个陈述既保留成果，又让下一步工程工作清晰可见，比夸大主张或含糊的“什么都不保证”更有用。

:::

### 练习 2：设计不欠下基数债务的收集方案 {#exercise-02}

假设服务要使用兼容 OpenTelemetry 的收集器。不要修改业务模型，请设计诊断配置和验证方案，并说明：

- 订阅哪些内建信号源和自定义信号源；
- 哪些属性可以成为指标标签；
- 如何对追踪采样；
- 哪些数据必须删除或脱敏；
- 日志如何与追踪连接；
- 用什么自动测试检查信号内容；
- 用什么负载测试发现指标基数过高；
- 自定义子活动带来的信息是否值得额外成本。


::: details 参考答案

#### 先定义信号契约，再定义收集器 {#exercise-02-contract}

订阅受支持的 ASP.NET Core 服务器插桩，以及以下应用源：

| 信号 | 来源 | 保留 | 避免 |
|---|---|---|---|
| 追踪 | 内建 ASP.NET Core 服务器源 | 方法、匹配的路由模板、状态；标准追踪上下文 | 原始 URL 查询、请求正文、授权请求头 |
| 追踪 | `ThinkingInFSharp.Booking.Api` | 仅在结果有调查价值时保留 `booking.http.request` | 除非策略明确允许，否则不要把请求 ID 作为全局可搜索属性 |
| 指标 | `ThinkingInFSharp.Booking.Api` | 带受限 `outcome` 的请求计数器与耗时直方图 | 关联 ID、具体路径、异常消息、用户或提供商标识 |
| 日志 | 应用完成事件 1000 | 追踪/相关值、方法、路由模板、状态、结果、耗时 | 正文、确认码、交易文本、异常消息、快照路径 |

首次部署保留现有自定义子活动，因为 `booking.outcome` 提供了独立于状态码的稳定业务分类；同时设定复审日期。如果查询从不使用该活动，而且内建服务器 Span 与结构化日志能回答相同问题，就删除它以减少 Span 数量。

不要对指标采样。进程聚合每次请求的测量，指标管道按配置周期导出。追踪先采用基于父级的概率采样；错误是否保留或单独采样，取决于收集器的明确行为。头部采样器不知道后续结果，因此“保留每个错误”可能需要尾部采样或另一种错误信号。必须说明延迟、内存与故障取舍，不能假设没有成本。

从部署配置取得收集器端点和凭证，实施 TLS 与最小权限。在应用和收集器两端都使用属性允许列表或脱敏，因为收集器规则不能成为发送已知机密的理由。限制队列内存，定义导出超时，并决定遥测丢失是否可以影响请求成功；对多数服务而言不应影响。

Microsoft 的 [.NET 追踪指南](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/distributed-tracing-instrumentation-walkthroughs) 区分插桩创建与收集；其[指标指南](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/metrics-instrumentation) 推荐在依赖注入宿主中使用 `IMeterFactory`，与这里计划验证的设计一致。

#### 测试关联关系与时间序列增长 {#exercise-02-tests}

先实现进程内监听器测试，在不依赖厂商的情况下验证信号已经产生。再增加收集器集成测试：使用受控且有效的 `traceparent`，分别发送成功请求和无效请求，然后检查：

- 响应头与完成日志使用同一追踪 ID；
- 采样时，自定义活动是服务器活动的子级；
- 计数器和直方图包含 `success` 与 `client_error` 测量；
- 导出记录不包含禁用字段或测试数据中的机密；
- 禁用活动采样器不会改变 HTTP 行为。

针对基数，发送至少 10,000 个具有不同预约 ID 和具体 URL 值的请求。查询或检查测试后端，断言自定义指标时间序列数量有固定上限——按当前契约，每项仪器最多四条 `outcome` 序列。还要把不同路由值限制在已注册模板数，并验证任何关联 ID 或预约 ID 都没有成为指标属性。

这应作为负载测试运行，不能只扫描源码。应用测试通过后，一个看似无害的属性增强处理器仍可能加入标识符。不断改变 ID，同时观察收集器内存、丢弃数据、导出队列长度与后端摄取量。

:::

### 练习 3：把检查变成真实发布计划 {#exercise-03}

选择一个明确目标，例如依赖框架的 Linux 容器或自包含 `linux-x64` 服务。把验收检查扩展为发布、晋级、部署与回滚计划，并说明：

- 不可变产物与运行时/配置契约；
- 存储迁移策略；
- 健康门、安全检查、冒烟测试与遥测检查；
- 推出策略和回滚触发条件；
- 哪些步骤可留在本地，哪些必须使用类生产环境。


::: details 参考答案

#### 选择并命名唯一产物 {#exercise-03-artifact}

假设目标是运行于 `linux-x64` 的框架依赖型 Linux 容器。不可变发布身份是 OCI 镜像摘要，而不是可变标签。多阶段构建先从锁文件还原并运行完整验收套件。随后以 Release 发布 `Booking.Api`，只把发布输出复制到固定版本且受支持的 ASP.NET Core 运行时镜像。

随摘要一起记录：

- 源码提交与干净工作区状态；
- SDK、目标框架、运行时镜像摘要和锁文件哈希；
- 构建来源证明与软件物料清单；
- 在明确策略下得到的漏洞与许可证扫描结果；
- 配置模式版本和数据库迁移范围；
- 公开 DTO/API 版本与回滚兼容窗口。

依赖框架意味着运行时来自运行时镜像。固定该镜像可改善可复现性，但安全补丁要求重新构建和晋级新镜像。Microsoft 的 [.NET 发布概述](https://learn.microsoft.com/en-us/dotnet/core/deploying/) 把运行时模型视为有意的发布选择。

#### 让同一份字节经过各道门 {#exercise-03-pipeline}

一条具体流水线是：

1. **源码门：** 冻结 JavaScript 安装、锁定 .NET 还原、格式/内容检查、完整构建与测试。
2. **发布门：** 发布一次、构建镜像一次、生成来源证明/SBOM、扫描、签名，并按摘要存储。
3. **临时环境门：** 用临时真实数据库和受控提供商沙箱启动指定摘要；执行迁移、HTTP/C# 冒烟、格式错误输入、诊断导出与关闭测试。
4. **预发布门：** 恢复匿名化代表数据，运行前后向迁移检查、并发/负载测试、提供商对账、发件箱恢复与授权测试。
5. **晋级：** 把批准附着在摘要上，不为生产重新构建。
6. **金丝雀部署：** 只路由固定的一小部分合格流量，同时观察错误率、延迟、饱和度、支付不明、发件箱年龄和容量冲突。
7. **扩展：** 只有各道门保持健康时，才按固定时间间隔逐步增加流量。
8. **完成：** 在已声明的回滚窗口内保留前一个兼容摘要和迁移恢复材料。

服务应分别提供“存活”和“就绪”检查。存活检查只回答进程是否还能继续工作，不应因为任意远程系统暂时不可用就失败。就绪检查则回答该实例当前能否处理请求；必要依赖不可用时，应暂时停止向它发送流量。两个端点都不能返回凭据、文件路径、SQL 或提供商消息。

平台支持时，应用以下运行控制：

- 以非 root 用户运行，并使用只读基础文件系统；
- 通过平台注入机密，不写入镜像；
- 限制出站目标；
- 按文档化的信任模型终止 TLS；
- 实施认证、授权、请求大小限制与速率限制。

扫描结果要有负责人，每项例外都要有到期时间。扫描器显示绿色并不构成安全设计。

#### 让存储演进与回滚保持兼容 {#exercise-03-rollback}

在声称副本安全前替换文件快照。使用按活动键划分的事务式或版本条件式存储，再用扩展/迁移/收缩次序设计模式变化：

1. 部署能读取新旧形式、但写入向后兼容形式的代码；
2. 应用增量迁移，并在并发负载下验证；
3. 用大小受限且可监控的批次回填；
4. 旧读取者全部退出后才切换写入；
5. 在回滚窗口结束后的后续版本中删除旧字段。

只有旧版本程序仍能读取当前数据，并理解已经发出的消息时，直接回滚应用才安全。否则只能发布一个向前修复的新版本，或还原数据库。两种恢复方式都必须说明可能丢失多长时间范围内的数据。还要实际测试还原耗时和数据正确性，不能仅凭“有备份”就推断 RPO 或 RTO。

错误率、延迟或饱和度超过阈值时，自动停止推出。就绪失败、支付不明异常增加或发件箱积压增长时也应停止。只有兼容性契约允许时才回滚。保留相关日志、追踪和状态并启动事故处理，不要在诊断前删除失败环境。

本地检查可以验证构建是否可重复、单元测试和契约测试是否通过、静态安全规则是否满足，以及程序能否在本机启动。镜像运行、真实数据库迁移、提供商沙箱、遥测导出、负载、金丝雀流量、备份还原和回滚，都需要类生产环境或生产控制平台才能验证。

:::


第六部分至此完成。第七部分会把这套基础映射到更广的 F# 与 .NET 生态，同时避免假装每个有用库都应进入同一个应用。
