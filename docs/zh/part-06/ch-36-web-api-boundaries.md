---
title: "第 36 章：Web API、JSON 与输入边界"
description: "用小型 F# Minimal API 暴露预约工作流，并明确处理 JSON、验证、取消、失败与机密。"
translationKey: part-06/ch-36-web-api-boundaries
---

# 第 36 章：Web API、JSON 与输入边界 {#overview}

第 35 章已经在进程内连接了各项能力。本章再用一个小型 ASP.NET Core Minimal API 接收网络请求。“Minimal”只说明这种 API 的宿主方式比较精简，并不表示边界工作可以省略。API 仍要检查传入数据、把 DTO 转成业务值、传递取消信号，并把已知失败转成稳定的 HTTP 响应。

理解本章时，可以不断追问：“这件事应由哪一层决定？”各层的职责如下：

- HTTP 层决定媒体类型和状态码；
- JSON 契约决定网络上传输的数据格式；
- DTO 映射检查必填数据；
- 业务层判断命令是否有效，以及状态能否改变；
- 适配器执行文件读写、支付和通知等外部操作；
- API 层只协调这些步骤，并把结果翻译成 HTTP 响应。

本章仍然只是第 33–38 章的页面示例，不要求你把它们拼成完整项目。当前仓库也没有可直接运行的 `Booking.Api.fsproj`。如果以后要实现这个项目，可以先引用第 33–35 章的 Domain、Contracts 和 Infrastructure，再按 `Endpoints.fs` → `Program.fs` 的顺序编译文件。

所有 `Endpoints.fs` 片段都位于 `namespace Booking.Api`。它们会使用 ASP.NET Core、`System.Text.Json` 和前三层定义的类型。页面省略了一些重复的辅助代码，因此这些片段不能各自独立运行。

“Minimal API”和“路由处理程序”是 ASP.NET Core 的说法，不是 F# 术语。`RequestDelegate`、`HttpContext` 和 `WebApplication` 也来自 .NET/ASP.NET Core。F# 在这里主要用到函数、记录、可区分联合、模式匹配和 `task` 计算表达式。

## HTTP 负责解释外部请求 {#outer-interpreter}

请求在产生副作用前会穿过多种表示：

```text
HTTP 字节
  -> 有界的严格 JSON
  -> 命令 DTO
  -> 原始领域命令
  -> 已验证的受保护值
  -> 加载 + 纯决策
  -> 支付？+ 追加 + 通知
  -> 响应 DTO 或安全错误 DTO
```

关键在于每次转换都经过检查：它要么产生下一种表示，要么返回当前边界负责的失败结果并停止。

ASP.NET Core 把通过 `MapGet`、`MapPost` 等方法映射的函数称为路由处理程序。处理程序可以返回框架结果、字符串，或由框架序列化的值。本例改用显式 `RequestDelegate` 处理程序，让字节上限、JSON 错误体与取消行为在一个适合教学的文件中保持可见。

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
直接使用委托，让本章的处理策略可以执行并接受契约测试。Minimal API 自动绑定也能实现同一契约；练习 1 要求你验证这条替代路径。

## 只发布四条职责明确的路由 {#route-contract}

API 暴露命令，而不是用一个通用端点接收可区分联合的序列化结果：

| 方法与路由 | 请求表示 | 成功结果 | 含义 |
|---|---|---|---|
| `POST /api/bookings/place` | `PlaceBookingDto` | `201` + `BookingDto` | 验证、支付授权、追加、通知 |
| `POST /api/bookings/confirm` | `ConfirmBookingDto` | `200` + `BookingDto` | 验证转换、追加、通知 |
| `POST /api/bookings/cancel` | `CancelBookingDto` | `200` + `BookingDto` | 验证转换、追加、通知 |
| `GET /api/bookings/{requestId}` | 路由文本 | `200` + `BookingDto` | 加载键匹配的快照 |

独立路由让允许的命令容易发现，也让每个请求只有一种稳定的 JSON 表示。它们还避免把面向编译器的 `BookingCommand` 编码当成公共协议。

`201 Created` 带有由规范化请求 ID 构造的相对 `Location` 头。去除首尾空白后，ID 需要满足三条规则：

- 长度为 1–64 个字符；
- 每个字符都是 ASCII URI 非保留字符，即字母、数字、`-`、`.`、`_` 或 `~`；
- 完整值为 `.` 和 `..` 之外的内容，因为 URI 解析会把这两个值视为点段。

这些规则让每个存储 ID 恰好对应一个稳定路径段。`Uri.EscapeDataString` 继续充当防御性编码，HTTP 测试还会沿返回的位置取得 `200`。确认与取消修改该位置上的表示，因此返回 `200`。

命令式路由为这个工作流提供了一份小而一致的 REST 契约。其他资源语义可以采用其他设计。以后改变这些路由语义会形成公共 API 迁移，而非内部重构。

## 让响应类型留在边界 {#boundary-dtos}

成功的处理程序通过 `BookingMapping.ofDomain` 投影受保护的 `Booking` 值；它们从不把领域记录或联合交给序列化器。失败的处理程序只返回一种由 API 定义的结构：

```fsharp:line-numbers [Endpoints.fs]
[<CLIMutable>]
type ApiFieldErrorDto =
    { [<JsonPropertyName("field")>]
      Field: string
      [<JsonPropertyName("code")>]
      Code: string }

[<CLIMutable>]
type ApiErrorDto =
    { [<JsonPropertyName("code")>]
      Code: string
      [<JsonPropertyName("message")>]
      Message: string
      [<JsonPropertyName("errors")>]
      Errors: ApiFieldErrorDto array }
```
`code` 是稳定、机器可读的决定。`message` 是安全的说明文字，不是存放异常或提供商响应的位置。`errors` 包含稳定的字段/代码对；非字段失败时它为空。

传输消息使用英文，本书则以中英文解释。客户端应依据代码分支，而不是依据翻译后的散文。以后本地化面向人的文字时，协议行为因此可以保持不变。

## 在理解输入的层拒绝它 {#validation-layers}

“错误请求”可能由不同原因造成。实现把三个负责不同检查的层级分开。

### 传输与语法 {#transport-syntax}

在 DTO 出现之前，API 会执行三项检查：

| 检查 | 失败响应 |
|---|---|
| 可识别的 JSON 媒体类型 | `415 unsupported_media_type` |
| 最多读取 16 KiB | `413 request_too_large` |
| 使用严格选项反序列化 | `400 invalid_json` |

`HasJsonContentType` 能识别 JSON 媒体类型，包括结构化 `+json` 后缀。严格反序列化会拒绝格式错误的文档、大小写错误或未知的属性，以及 JSON 种类错误的值。每种拒绝都会发生在端口调用之前。

### DTO 存在性 {#dto-presence}

JSON `null` 可以反序列化为空 DTO；缺少 `seats` 属性会得到 `Nullable<int>()`。命令映射器将其报告为 `400 invalid_request`。通用案例包括 `MissingBody`、`MissingRequestId` 和 `MissingSeats`；各命令还有自己的缺失字段案例。

这一层判断传输表示是否提供了形成原始命令所需的数据。`0` 个座位或空白标识符是否合法，则由领域有效性层判断。

### 领域有效性 {#domain-validity}

现有验证模块负责检查请求 ID、非正座位数、空白确认码与空白取消原因。请求 ID 需要满足四条规则：

- 至少包含一个非空白字符；
- 最多包含 64 个字符；
- 只使用 ASCII URI 非保留字符；
- 完整值为点段 `.` 和 `..` 之外的内容。

字符规则会从路由身份中排除 `/`、`%`、`?` 和 Unicode。API 在 I/O 前完成验证，以取得受保护的存储键，并一次返回全部字段问题。纯决策器接收原始命令时会应用同一项验证。复用领域检查，能让规则只保留一个来源；HTTP 层负责执行顺序。

多个领域错误会成为一个 `validation_failed` 响应，其中包含有序字段错误。请求 ID 失败使用稳定字段代码 `blank`、`too_long` 或 `invalid_format`。传输、DTO 或领域验证失败时，不会发生存储、支付或通知调用。

## 在解释 JSON 前限制字节 {#bounded-body}

Kestrel 默认的请求体上限远大于这些微小的命令文档。宿主把它降低到 16 KiB，端点在读取时也执行同一上限：

```fsharp:line-numbers [Endpoints.fs]
// The small command contract is buffered only up to the documented limit. This also
// enforces the limit under TestServer, where Kestrel-specific limits do not run.
let private readBody (context: HttpContext) =
    task {
        if not (context.Request.HasJsonContentType()) then
            return Error UnsupportedMediaType
        elif
            context.Request.ContentLength.HasValue
            && context.Request.ContentLength.Value > int64 MaxRequestBodyBytes
        then
            return Error TooLarge
        else
            use body = new MemoryStream(MaxRequestBodyBytes)
            let chunk = Array.zeroCreate<byte> 4096
            let mutable finished = false
            let mutable tooLarge = false

            while not finished && not tooLarge do
                let remaining = MaxRequestBodyBytes - int body.Length
                let requested = min chunk.Length (remaining + 1)

                let! count = context.Request.Body.ReadAsync(chunk.AsMemory(0, requested), context.RequestAborted)

                if count = 0 then
                    finished <- true
                elif body.Length + int64 count > int64 MaxRequestBodyBytes then
                    tooLarge <- true
                else
                    body.Write(chunk, 0, count)

            if tooLarge then
                return Error TooLarge
            else
                return Ok(body.ToArray())
    }

let private deserialize<'dto when 'dto: not struct and 'dto: not null>
    (bytes: byte array)
    : Result<'dto | null, BodyError> =
    try
        let span = ReadOnlySpan<byte>(bytes)
        Ok(JsonSerializer.Deserialize<'dto>(span, jsonOptions))
    with :? JsonException ->
        Error InvalidJson
```
发送方提供 `Content-Length` 时，检查它可以尽早拒绝，但仅靠该头部并不构成上限。分块请求和自定义测试流可能没有声明长度。因此循环最多多读一个字节就停止，并且绝不按攻击者控制的输入大小分配内存。

这里缓冲请求体，是因为最大值刻意很小，而严格反序列化需要完整命令。文件上传端点需要不同的流式设计和自己的上限；不能不看端点需求就照搬这份 16 KiB 策略。

同一个 `BookingJson.configure` 调用固定大小写敏感性、未知成员拒绝、空值省略与深度。复用它可防止 HTTP 与持久化给同一个 DTO 赋予两个细微不同的含义。

## 协调工作流而不把规则移到外层 {#workflow}

完成映射与验证后，端点取得原始命令、受保护的请求 ID、可选支付请求和成功状态码。此时才可以协调外部操作：

```fsharp:line-numbers [Endpoints.fs]
let private executeCommand dependencies prepared (context: HttpContext) =
    task {
        let cancellationToken = context.RequestAborted
        cancellationToken.ThrowIfCancellationRequested()
        let! state = dependencies.Ports.LoadBooking prepared.RequestId cancellationToken

        match Decider.decide dependencies.Activity state prepared.Command with
        | Error error -> return! writeDecisionError context error
        | Ok bookingEvent ->
            let! payment = authorize dependencies.Ports prepared.Payment cancellationToken

            match payment with
            | PaymentRefused ->
                return!
                    writeError
                        context
                        StatusCodes.Status422UnprocessableEntity
                        "payment_declined"
                        "Payment was declined."
                        [||]
            | PaymentUnavailable ->
                return!
                    writeError
                        context
                        StatusCodes.Status503ServiceUnavailable
                        "dependency_unavailable"
                        "An external dependency is unavailable."
                        [||]
            | PaymentAccepted ->
                do! dependencies.Ports.AppendEvent prepared.RequestId bookingEvent cancellationToken

                let! notified =
                    tryExternal (fun () ->
                        dependencies.Ports.Notify (notificationFor bookingEvent) cancellationToken)

                match notified with
                | Error() ->
                    return!
                        writeError
                            context
                            StatusCodes.Status503ServiceUnavailable
                            "dependency_unavailable"
                            "An external dependency is unavailable."
                            [||]
                | Ok() -> return! writeBooking context prepared (BookingEvent.booking bookingEvent)
    }

let private executeConsistent (dependencies: ConsistentBookingApiDependencies) prepared (context: HttpContext) =
    task {
        let cancellationToken = context.RequestAborted
        cancellationToken.ThrowIfCancellationRequested()
        let! result = dependencies.Execute prepared.Command cancellationToken

        match result with
        | Ok booking -> return! writeBooking context prepared booking
        | Error error -> return! writeConsistencyError context error
    }
```
顺序是有意选择的：

1. 加载当前预约状态；
2. 调用纯决策器；
3. 仅对创建预约请求支付授权；
4. 追加已接受的事件；
5. 发送通知；
6. 序列化最终的预约 DTO。

API 不会查看私有预约字段来重新实现状态转换。它使用 `Decider.decide`、`BookingEvent.booking`、受保护访问器和端口函数。HTTP 只负责排序与转换，领域模块仍决定哪些事实合法。

## 根据结果类型映射响应，不要解析字符串 {#error-map}

响应表是 API 契约的一部分：

| 状态码 | 稳定代码 | 来源 |
|---|---|---|
| `400` | `invalid_json` | 格式错误或与契约不兼容的 JSON |
| `400` | `invalid_request` | 缺少 DTO 必需数据 |
| `400` | `validation_failed` | 领域命令字段无效 |
| `404` | `booking_not_found` | 确认、取消或查询没有匹配预约 |
| `409` | `booking_already_exists` | 创建预约复用了已有请求 ID |
| `409` | `capacity_exceeded` | 请求座位数超过本活动容量 |
| `409` | `invalid_transition` | 当前状态拒绝请求的转换 |
| `413` | `request_too_large` | 命令体超过 16 KiB |
| `415` | `unsupported_media_type` | 请求不是 JSON 媒体类型 |
| `422` | `payment_declined` | 提供商产生预期拒绝 |
| `503` | `storage_unavailable` / `dependency_unavailable` | 运维依赖无法完成 |
| `500` | `internal_error` | 发生意外应用故障 |

支付拒绝属于预期提供商结果，容量拒绝属于领域冲突；格式错误的 JSON、资源缺失和基础设施离线各有自己的类别。稳定的公共代码保留这些区别，让客户端能够选择后续行为并进行诊断，同时把私有联合载荷留在应用内部。

不要从 `sprintf "%A" error` 推导 `code`，也不要使用 `exception.Message`。编译器名称、文件路径、提供商详情和未来重构都会变成意外公共数据。

## 传播请求取消 {#request-cancellation}

底层请求连接中止时，`HttpContext.RequestAborted` 会发出信号。端点把同一个令牌传给请求体读取、加载、支付授权、追加、通知和响应序列化。

进程内测试取消客户端令牌后，阻塞的 `LoadBooking` 会收到取消，HTTP 任务也保持取消状态。若 `RequestAborted` 已取消，错误处理会重新抛出 `OperationCanceledException`。它不会为已经离开的客户端制造 `500` JSON。

客户端仍连接时，操作也可能自行取消，例如依赖专属截止时间。本例把这种不同情况映射为 `503 dependency_unavailable`。生产系统可以进一步区分依赖超时与离线，但绝不能把两者中的任何一个与客户端断连混为一谈。

取消只请求停止，不会回滚。一旦副作用或文件替换已经可见，稍后的取消无法撤销它。下一节会具体说明这一限制。

## 如实描述部分失败 {#partial-failure}

当前顺序存在可观察的中断窗口：

| 最后完成的步骤 | 已成立的事实 | 当前响应或观察 | 安全结论 |
|---|---|---|---|
| 纯决策 | 没有副作用或快照变化 | 若有则为领域错误 | 重试有效输入尚不会重复副作用 |
| 支付授权 | 提供商可能已行动；快照仍旧 | 后续追加失败则为 `503` | 盲目重试可能重复授权 |
| 事件追加 | 预约快照已更新 | 通知失败则为 `503` | 重试可能看到“已存在”，而通知仍缺失 |
| 通知 | 所有已建模副作用完成 | 响应仍可能因取消丢失 | 没有响应不代表操作失败 |

这层 HTTP 边界暴露这些事实，而不是用通用 `try/with` 把它们藏起来。第 37 章会加入原子容量与幂等策略，再定义重试和重启行为。在那之前，这只是边界设计，不是具备一致性安全的商业预约服务。

将设计落成项目时，应加入一项契约测试：通知失败返回安全的 `503`，但已追加的状态仍是 `Booked`。这项测试用来固定部分失败窗口，并不解决它。

## 把异常细节留在进程内 {#safe-errors}

最外层处理程序区分客户端取消、Kestrel 的超大请求体异常、类型化存储适配器异常与意外故障：

```fsharp:line-numbers [Endpoints.fs]
let private safely handler (context: HttpContext) =
    task {
        try
            return! handler context
        with
        | :? OperationCanceledException as error when context.RequestAborted.IsCancellationRequested ->
            return raise error
        | :? OperationCanceledException ->
            return!
                writeError
                    context
                    StatusCodes.Status503ServiceUnavailable
                    "dependency_unavailable"
                    "An external dependency is unavailable."
                    [||]
        | :? BadHttpRequestException as error when error.StatusCode = StatusCodes.Status413PayloadTooLarge ->
            return! writeBodyError context TooLarge
        | :? BookingStoreAdapterException ->
            return!
                writeError
                    context
                    StatusCodes.Status503ServiceUnavailable
                    "storage_unavailable"
                    "Booking storage is unavailable."
                    [||]
        | _ when context.Response.HasStarted -> context.Abort()
        | _ ->
            return!
                writeError
                    context
                    StatusCodes.Status500InternalServerError
                    "internal_error"
                    "The request could not be completed."
                    [||]
    }
```
已声明失败沿三条路径处理：

- 适配器把已知的提供方传输或可用性故障包装为 `DependencyUnavailableException`。原异常保留在 `InnerException` 中；对应的 `Charge` 或 `Notify` 分支把该信号映射成 `503 dependency_unavailable`。
- 存储适配器在内部报告 `BookingStoreAdapterException`。HTTP 层只返回 `503 storage_unavailable`。
- 意外程序缺陷到达最外层处理程序，并成为安全的 `500 internal_error`。

响应头开始后，替换 JSON 会破坏响应。处理程序因此中止连接。本例的 DTO 序列化较为简单，同一规则也能保护以后的响应路径。

第 38 章会先定义数据分类，再加入结构化故障诊断。在此之前，本例不会记录未知异常消息。生产服务仍需要职责明确的可观测性策略。

## 加载配置而不披露它 {#configuration-secrets}

宿主读取 `BOOKING_STORE_PATH`，还可读取 `BOOKING_EVENT_ID` 和 `BOOKING_CAPACITY`。随后，它构造经过验证的配置与领域值。无效设置只产生 `invalid_booking_store`、`invalid_event_id` 或 `invalid_capacity`，不会打印原始值。

下方 `Program.fs` 是第 38 章完成后的最终宿主预览，不是只有本章就能编译的程序。`AtomicBookingStore` 和 `IdempotentBookingService` 由第 37 章定义，`BookingDiagnostics` 由第 38 章定义。若按章节顺序实现，可暂时使用前文的 `BookingEndpoints.map` 与第 35 章端口，或等后两章完成后再编写这个文件。

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
样例中的路径、活动 ID 与容量是普通配置，不是凭据。规则依然有用：不能仅仅因为这个具体值不是机密，就回显一条不可信的配置路径。以后真正的支付密钥必须留在源码控制和响应之外。

环境变量能让值不进入已提交代码，但 Microsoft 明确警告，它们通常以明文保存；进程或机器失陷时仍然可见。开发 Secret Manager 只用于开发，部署时应选择受控的生产机密存储。

如果以后实现完整项目，冒烟测试还要检查日志内容。日志只能包含允许记录的方法、路由、状态、内容类型、长度和耗时，不能包含请求体或配置值。不要随意记录请求体和响应体；这样做可能需要缓冲数据，还可能收集个人信息或凭据。记录之前应先为数据分类并脱敏。

Kestrel 的 `Server` 头已关闭，以减少无必要的实现披露。这是加固，不是身份验证或授权。

## 规划管线与传输验证 {#testing}

如果把参考设计实现成项目，可以使用官方 `Microsoft.AspNetCore.TestHost` 包编写契约测试。每个测试创建真实的 `WebApplication`，映射实际端点并注入可控制的替身，再通过 `HttpClient` 向内存中的 HTTP 管线发送请求。

最低用例应覆盖：

- 成功 JSON 的具体格式、`Location`、查询、确认与取消；
- 格式错误 JSON、属性大小写错误、未知属性、缺失字段、空请求体与错误媒体类型；
- 在任何副作用前累积领域验证；
- 在 JSON 解析前执行 16 KiB 上限；
- 重复、缺失、容量与支付结果；
- 提交前支付故障与提交后通知故障；
- 类型化存储故障、意外故障与响应脱敏；
- 不用计时 sleep，让取消抵达受控阻塞端口。

`TestServer` 在内存中发送请求，并且有意不复现所有传输行为或头部。因此应用级字节上限要在其中测试，另一个回环 Kestrel 冒烟测试则验证真实启动、头部、路由与文件持久化。

两种测试都不能取代另一种。为每条契约断言启动随机真实端口会增加噪声；只依赖 `TestServer` 又会让 Kestrel 配置得不到观察。

## 组装后的本地运行模板 {#local-run}

下方命令只是运行模板，当前仓库不能直接执行。只有在你主动把第 33–38 章实现成 ASP.NET Core 项目后，才需要把 `path/to/Booking.Api.fsproj` 换成真实路径。模板使用临时快照，并且只监听本机回环地址。

### 启动宿主 {#local-start}

在 macOS 或 Linux 上：

```bash
BOOKING_STORE_PATH="${TMPDIR:-/tmp}/thinking-in-fsharp-booking.json" \
BOOKING_EVENT_ID="EVT-LOCAL" \
BOOKING_CAPACITY="4" \
ASPNETCORE_URLS="http://127.0.0.1:5086" \
dotnet run --project path/to/Booking.Api.fsproj -c Release
```

在 PowerShell 中：

```powershell
$env:BOOKING_STORE_PATH = Join-Path ([IO.Path]::GetTempPath()) "thinking-in-fsharp-booking.json"
$env:BOOKING_EVENT_ID = "EVT-LOCAL"
$env:BOOKING_CAPACITY = "4"
$env:ASPNETCORE_URLS = "http://127.0.0.1:5086"
dotnet run --project path/to/Booking.Api.fsproj -c Release
```

### 发送成功请求 {#local-success}

在另一个终端中放置并读取预约：

```bash
curl --fail-with-body -i \
  -H 'Content-Type: application/json' \
  -d '{"requestId":"REQ-36","seats":2}' \
  http://127.0.0.1:5086/api/bookings/place

curl --fail-with-body -i \
  http://127.0.0.1:5086/api/bookings/REQ-36
```

若组装后的实现符合本章契约，第一个响应应是 `201`，包含 `Location: /api/bookings/REQ-36`，并返回待确认的 `BookingDto`。第二个响应应是 `200`，内容是已持久化表示。

### 观察严格失败 {#local-failure}

属性名区分大小写：

```bash
curl -i \
  -H 'Content-Type: application/json' \
  -d '{"requestId":"REQ-BAD","Seats":2}' \
  http://127.0.0.1:5086/api/bookings/place
```

响应稳定，而且不包含解析器异常：

```json
{"code":"invalid_json","message":"The request body is not valid JSON for this endpoint.","errors":[]}
```

删除指定的临时快照前，应先停止宿主。以后使用同一路径启动时会恢复已保存状态；第 37 章的重启测试会用到这一行为。

## 了解生产边界 {#production-boundary}

这个本地宿主刻意没有身份验证、授权、TLS 证书、CORS 策略、速率限制器、代理信任配置、分布式存储或真实支付提供商。不得把它原样暴露到不可信网络。

直接在边缘部署时，应决定 TLS、HSTS、允许的主机、速率限制、请求超时、身份验证、授权与机密存储。部署在反向代理后时，还要用显式受信代理配置转发头，并决定哪个层终止 TLS、执行每一种上限。

应根据威胁模型选择中间件。例如，CORS 约束浏览器来源，身份验证与网络控制则约束其他 HTTP 客户端；宽松的 CORS 策略会扩大浏览器访问范围。第 42 章会重新讨论部署选择，第 38 章则加入本例所需的诊断与发布检查。

## 练习 {#exercises}

### 练习 1：改变绑定但不改变契约 {#exercise-01}

把一个命令路由重新设计为使用 Minimal API 自动参数绑定。保留完全相同的严格 JSON 策略、16 KiB 有效上限、`ApiErrorDto` 表示、取消传播和全部状态码/代码组合。指出哪些行为属于配置、端点过滤器或中间件，以及处理程序。给出能够阻止框架默认值改变公共响应的契约测试。


::: details 参考答案

#### 先固定对外行为 {#exercise-01-contract}

重构必须保留以下不变量：

- 接受的 JSON 媒体类型与严格区分大小写的属性名；
- 拒绝未知成员与过深嵌套；
- 无论是否有 `Content-Length` 都有效的 16 KiB 字节上限；
- 格式错误或结构不符合要求的 JSON 返回 `invalid_json`；
- 空请求体或 DTO 字段缺失返回 `invalid_request`；
- 在任何端口调用前累积领域字段错误；
- 每个端口都得到调用方的请求中止令牌；
- 每种成功状态、错误状态、稳定代码与响应 DTO 结构。

不要先删除契约测试。它们就是让机制可以安全变化的规格。

#### 把策略分配给最窄的可复用层 {#exercise-01-layers}

一种可行设计按如下方式划分责任：

| 层 | 责任 |
|---|---|
| HTTP JSON 配置 | 对 Minimal API 绑定使用的选项调用 `BookingJson.configure` |
| Kestrel 配置 | 在真实服务器拒绝超过 16 KiB 的请求体 |
| 早期中间件或端点过滤器 | 传输特性未提供上限时，执行同样的流式字节计数 |
| 绑定失败边界 | 把格式错误 JSON 与绑定失败转换为稳定 `ApiErrorDto` 契约 |
| 路由处理程序 | 接收 `PlaceBookingDto`，执行映射和验证，再调用应用工作流 |
| 最外层异常边界 | 保留请求取消，并隐藏运维或意外故障细节 |

于是处理程序可以具有紧凑的概念签名：

```fsharp
PlaceBookingDto -> CancellationToken -> Task<IResult>
```

这个签名没有覆盖完整的公开契约，因为框架绑定先于处理程序运行。根据配置与宿主，绑定失败可能返回框架生成的错误体，也可能把异常抛给 `TestServer`。绑定失败层必须在两种结果抵达调用方前统一格式。

不要让中间件先读取请求体计数，再让绑定器读取已经耗尽的流。可以在绑定前安装真正限制字节数的包装流。也可以只在规定的小上限内缓冲，再用可重绕流替换请求体。前者避免重复缓冲；后者更简单，但请求结束时必须释放所创建的缓冲区。

#### 保持黑盒测试 {#exercise-01-tests}

原样运行现有 HTTP 用例。再加入两个没有 `Content-Length` 的请求：一个恰好达到上限，一个超过一个字节。加入 `application/problem+json` 或另一种有效的 `+json` 媒体类型，确认内容类型策略符合预期。

同时检查响应及负责处理它的层。无效输入必须在调用 `LoadBooking` 前产生 `400`。取消必须先抵达阻塞端口，客户端任务随后以取消状态结束。最后执行真实 Kestrel 冒烟测试，覆盖 `TestServer` 模型之外的传输上限与响应头。

如果状态码、代码、字段错误或副作用次数发生变化，重构就改变了 API。此时应明确设计迁移，不能把变化当作绑定实现细节。

:::

### 练习 2：从最后可见的副作用开始推理 {#exercise-02}

分别分析以下三种中断：

1. 支付授权后，追加状态失败；
2. 追加状态成功后，发送通知失败；
3. 发送通知成功后，客户端断开连接。

对每种情况，说明支付提供商、快照、调用方和一次重试分别能看到什么。然后列出第 37 章必须持久保存的最少幂等信息。这里没有分布式事务，不要假设失败会自动撤销之前的操作。


::: details 参考答案

#### 记录歧义，而不是猜测 {#exercise-02-table}

三种中断会产生不同事实：

| 中断 | 提供商所见 | 快照内容 | 调用方所见 | 盲目重试风险 |
|---|---|---|---|---|
| 支付授权后追加失败 | 一笔授权可能存在 | 旧状态 | `503` 或响应丢失 | 第二笔授权 |
| 追加成功后通知失败 | 授权存在 | 新预约 | `503` | 重复命令，而通知仍缺失 |
| 通知成功后响应丢失 | 授权与通知都存在 | 新预约 | 取消/无响应 | 已完成工作仍重复支付或通知 |

调用方无法从 HTTP 响应存在与否推导持久事实。服务器也无法仅仅因为发送请求后连接失败，就推导提供商是否已经行动。双方都需要跨进程与网络故障存续的标识符。

#### 持久化最小重放状态 {#exercise-02-evidence}

第 37 章需要一条以规范化请求 ID 为键的持久记录。它至少要保留：

- 原始命令的指纹，使同一 ID 用于不同输入时成为冲突；
- 已接受的预约或决策结果；
- 稳定支付幂等键，以及授权是待处理、已知接受、已知拒绝还是结果不明确；
- 通知是待处理还是已投递；
- 足以重放同一完成结果、且不重复外部调用的响应数据。

“待处理”与“结果不明确”不同。待处理表示尚不知道有任何尝试开始；结果不明确表示尝试已经开始，但结果未知。再次授权前需要查询提供商状态，或使用提供商支持的幂等键。

本地提交后的通知适合使用持久发件箱记录。把预约与“通知待处理”一起提交，再独立投递并标记完成。确定性替身可以验证状态转换，却无法验证真实消息代理或邮件提供商的投递语义。

这不是分布式事务，而是一套支持重放、对账和至少一次尝试的明确协议；可用时再做去重。补偿、授权过期和提供商回调还需要额外业务规则，本例没有定义这些规则。

:::

### 练习 3：审阅两种部署拓扑 {#exercise-03}

比较直接暴露 Kestrel 与把它放在反向代理之后。为 TLS、转发头、主机过滤、请求上限、速率限制、身份验证、机密获取与日志脱敏制作一张简短责任表。标出哪些控制是这个预约 API 必需的，哪些取决于部署需求。


::: details 参考答案

#### 把控制放在掌握可信信息的位置 {#exercise-03-table}

把下表当作起点，而不是普适基础设施策略：

| 关注点 | Kestrel 位于边缘 | 前方有受信反向代理 | 预约需求 |
|---|---|---|---|
| TLS 与 HSTS | 在应用/服务器配置 | 通常由代理终止；正确保留安全协议 | 不可信网络上必需 |
| 转发头 | 保持关闭 | 只为明确配置的可信代理/网络启用 | 取决于拓扑 |
| 主机过滤 | 配置允许的主机 | 代理验证；应用可纵深防御 | 主机参与安全判断时必需 |
| 16 KiB 请求体上限 | Kestrel 加应用上限 | 代理、Kestrel 与应用上限应一致 | 这些命令路由必需 |
| 速率限制/超时 | 应用/服务器策略 | 协调代理与应用策略 | 公开前必需；数值取决于负载 |
| 身份验证/授权 | 应用验证身份与权限 | 代理可以认证，但应用必须明确信任并授权所得身份 | 暴露预约数据前必需 |
| 机密获取 | 进程可访问的受控存储 | 受控存储或工作负载身份，绝不用代理头传递原始机密 | 真实提供商必需 |
| HTTP 日志 | 在应用分类与脱敏 | 两层都分类脱敏；避免重复捕获请求体 | 诊断策略必需；请求体日志可选 |

信任所有发送方时，转发头很危险：客户端可以伪造协议或地址。反之，在终止 TLS 的代理后仍关闭它们，会使 HTTPS 重定向、安全链接与审计数据出错。配置应服从真实信任边界。

只有浏览器源需要直接调用此 API 时，CORS 才有必要。它不验证调用方身份，也不能防御脚本、服务器或命令行客户端。如果不存在跨源浏览器客户端，保持 CORS 关闭才是更小的正确策略。

关闭 `Server: Kestrel` 可以减少被动披露，却不能修复缺失的身份验证、TLS 或速率限制。同样，把凭据从源码移入明文环境变量可以防止意外提交，却不能加密它。

发布审阅应为每项必要控制写明负责组件或团队，以及验证方式。验证方式可以是配置测试、部署探针、日志样本或安全测试。没有拓扑和实际结果的勾选框不构成控制。

:::


## 来源 {#sources}

- [Microsoft Learn：Minimal API 快速参考](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis?view=aspnetcore-10.0)
- [Microsoft Learn：`HttpRequestJsonExtensions` 与 JSON 内容类型](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.http.httprequestjsonextensions?view=aspnetcore-10.0)
- [Microsoft Learn：拒绝未映射的 JSON 成员](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/missing-members)
- [Microsoft Learn：`HttpContext.RequestAborted`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.http.defaulthttpcontext.requestaborted?view=aspnetcore-10.0)
- [Microsoft Learn：用 `TestServer` 测试 ASP.NET Core 中间件](https://learn.microsoft.com/en-us/aspnet/core/test/middleware?view=aspnetcore-10.0)
- [Microsoft Learn：Kestrel 安全注意事项与可配置上限](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/security-considerations?view=aspnetcore-10.0)
- [Microsoft Learn：在开发中安全存储应用机密](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets?view=aspnetcore-10.0)
- [Microsoft Learn：HTTP 日志与脱敏](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/http-logging/?view=aspnetcore-10.0)
