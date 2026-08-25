---
title: "第 36 章：Web API、JSON 与输入边界"
description: "用小型 F# Minimal API 暴露预约工作流，同时让 JSON、验证、取消、失败与机密停留在显式边界。"
translationKey: part-06/ch-36-web-api-boundaries
---

# 第 36 章：Web API、JSON 与输入边界 {#overview}

第 35 章装配了各项能力，却还没有网络边界。本章加入一个小型 ASP.NET Core Minimal API。“Minimal”描述的是宿主模型，而不是边界判断的数量：字节依旧不可信，DTO 依旧不是领域值，取消依旧可被观察，异常消息也依旧不是响应契约。

实现让每一步都能看见同一个问题：哪个层有权作出这个决定？HTTP 决定媒体类型与状态码；JSON 契约决定传输形状；DTO 映射决定必需的传输数据是否存在；领域决定业务有效性与状态转换；适配器决定效果；API 只协调这些决定，并且只翻译它们声明过的结果。

## 学完本章，你将能够 {#outcomes}

学完本章后，你应该能够：

- 映射一组很小的命令式路由，同时不暴露领域表示；
- 区分媒体类型、字节大小、JSON 形状、DTO 存在性与领域验证失败；
- 为请求和响应边界复用同一份严格 `JsonSerializerOptions` 策略；
- 即使没有 `Content-Length`，或测试服务器绕过 Kestrel 限制，也能真正限制请求；
- 把领域拒绝转换为稳定的状态码/代码组合，而不泄露受保护值；
- 把 `HttpContext.RequestAborted` 传给每一个异步端口；
- 区分客户端取消与依赖内部取消；
- 准确解释支付、持久化、通知与响应分别在何时变得可见；
- 返回安全的运维错误，而不返回异常消息；
- 加载配置，同时不打印被拒绝的值，也不提交机密；
- 用 `TestServer` 验证应用管线，并用真实 Kestrel 冒烟验证传输行为；
- 说明这个教学宿主有意省略了哪些生产关注点。

## 把 HTTP 看作外层解释器 {#outer-interpreter}

请求在产生效果之前会穿过多种表示：

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

箭头比方框更重要。没有一个箭头是未经检查的转换；每个箭头要么产生下一种表示，要么以该边界拥有的结果停止。

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
直接使用委托并不是说 Minimal API 自动绑定有错。它只是一个局部选择，让本章的边界策略可以执行并由契约测试固定。练习 1 会要求你用自动绑定保留同一份契约。

## 发布四条狭窄路由 {#route-contract}

API 暴露命令，而不是用一个通用端点接收可辨识联合的序列化结果：

| 方法与路由 | 请求表示 | 成功结果 | 含义 |
|---|---|---|---|
| `POST /api/bookings/place` | `PlaceBookingDto` | `201` + `BookingDto` | 验证、支付授权、追加、通知 |
| `POST /api/bookings/confirm` | `ConfirmBookingDto` | `200` + `BookingDto` | 验证转换、追加、通知 |
| `POST /api/bookings/cancel` | `CancelBookingDto` | `200` + `BookingDto` | 验证转换、追加、通知 |
| `GET /api/bookings/{requestId}` | 路由文本 | `200` + `BookingDto` | 加载键匹配的快照 |

独立路由让允许的命令容易发现，也让每个请求只有一种稳定 JSON 形状。它们还避免把面向编译器的 `BookingCommand` 编码当成公共协议。

`201 Created` 带有由规范化请求 ID 构造的相对 `Location` 头。去除首尾空白后，领域只接受 1–64 个 ASCII URI 非保留字符：字母、数字、`-`、`.`、`_` 与 `~`；完整值 `.` 与 `..` 会被排除，因为 URI 解析把它们视为点段。因此存储值恰好是一个稳定路径段；`Uri.EscapeDataString` 仍作为防御性编码步骤，HTTP 测试还会沿返回的位置取得 `200`。确认与取消修改的是该 ID 已经标识的表示，因此返回 `200`。

这并不是说命令式路由是唯一的 REST 设计，而是为这个工作流选择一份小而一致的契约。以后改变路由语义会是公共 API 迁移，不是内部重构。

## 让响应类型留在边界 {#boundary-dtos}

成功的处理程序通过 `BookingMapping.ofDomain` 投影受保护的 `Booking` 值；它们从不把领域记录或联合交给序列化器。失败的处理程序只返回一种由 API 拥有的形状：

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

“错误请求”并不是一种失败。实现把三个权威层级分开。

### 传输与语法 {#transport-syntax}

在 DTO 出现之前，API 会检查内容类型是否为可识别的 JSON 媒体类型、最多读取 16 KiB，并在严格选项下反序列化字节。结果分别是 `415 unsupported_media_type`、`413 request_too_large` 或 `400 invalid_json`。

`HasJsonContentType` 能识别 JSON 媒体类型，包括结构化 `+json` 后缀。格式错误的文档、大小写错误的属性、未知属性或 JSON 种类错误的值，都会在调用任何端口之前失败。

### DTO 存在性 {#dto-presence}

JSON `null` 可以反序列化为空 DTO。缺少 `seats` 属性会得到 `Nullable<int>()`。因此命令映射器会把 `MissingBody`、`MissingRequestId`、`MissingSeats` 和相应的命令专属失败报告为 `400 invalid_request`。

这一层回答传输表示是否提供了形成原始命令所需的数据。它刻意不决定 `0` 个座位或空白标识符是不是合法的业务输入。

### 领域有效性 {#domain-validity}

现有验证模块仍然拥有请求 ID、非正座位数、空白确认码与空白取消原因。请求 ID 必须非空、至多 64 个字符、只含 ASCII URI 非保留字符，且完整值不能是点段 `.` 或 `..`；包含 `/`、`%`、`?` 或 Unicode 的值同样不能成为有歧义的路由身份。API 先验证以取得受保护的存储键，并在 I/O 前拒绝全部字段问题。纯决策器接收原始命令时会再次验证；这次重复的纯检查保留了唯一领域权威，而不是在 HTTP 代码中复制规则。

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

这里缓冲请求体，是因为最大值刻意很小，而严格反序列化需要完整命令。文件上传端点需要不同的流式设计和自己的上限；把这份 16 KiB 策略复制到每个端点只会成为照搬仪式。

同一个 `BookingJson.configure` 调用固定大小写敏感性、未知成员拒绝、空值省略与深度。复用它可防止 HTTP 与持久化给同一个 DTO 赋予两个细微不同的含义。

## 协调工作流而不把规则移到外层 {#workflow}

完成映射与验证后，端点拥有原始命令、受保护的请求 ID、可选的受保护支付请求和成功状态码。此时才可以协调效果：

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

API 不会查看私有预约字段来重新实现状态转换。它使用 `Decider.decide`、`BookingEvent.booking`、受保护访问器和端口函数。HTTP 拥有排序与翻译，领域模块仍然拥有合法事实。

## 映射结果，而不是映射字符串 {#error-map}

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

支付拒绝既不是格式错误的 JSON，也不是异常。容量拒绝既不是“未找到”，也不是基础设施离线。保留这些区别，让客户端能够决定后续行为并进行诊断，同时不返回私有联合载荷。

不要从 `sprintf "%A" error` 推导 `code`，也不要使用 `exception.Message`。编译器名称、文件路径、提供商详情和未来重构都会变成意外公共数据。

## 传播请求取消 {#request-cancellation}

底层请求连接中止时，`HttpContext.RequestAborted` 会发出信号。端点把同一个令牌传给请求体读取、加载、支付授权、追加、通知和响应序列化。

在进程内测试中取消客户端拥有的令牌时，阻塞的 `LoadBooking` 会观察到取消，HTTP 任务也保持取消状态。`RequestAborted` 已取消时，错误边界会重新抛出 `OperationCanceledException`；它不会为已经离开的客户端制造 `500` JSON。

客户端仍连接时，操作也可能自行取消，例如依赖专属截止时间。本例把这种不同情况映射为 `503 dependency_unavailable`。生产系统可以进一步区分依赖超时与离线，但绝不能把两者中的任何一个与客户端断连混为一谈。

取消是停止请求，不是回滚。一旦外部效果或文件替换可见，稍后的取消无法让它撤销。下一节会把这个限制说具体。

## 如实描述部分失败 {#partial-failure}

当前顺序存在可观察的中断窗口：

| 最后完成的步骤 | 已成立的事实 | 当前响应或观察 | 安全结论 |
|---|---|---|---|
| 纯决策 | 没有外部效果或快照变化 | 若有则为领域错误 | 重试有效输入尚不会重复效果 |
| 支付授权 | 提供商可能已行动；快照仍旧 | 后续追加失败则为 `503` | 盲目重试可能重复授权 |
| 事件追加 | 预约快照已更新 | 通知失败则为 `503` | 重试可能看到“已存在”，而通知仍缺失 |
| 通知 | 所有已建模效果完成 | 响应仍可能因取消丢失 | 没有响应并不能证明失败 |

这层 HTTP 边界暴露这些事实，而不是用通用 `try/with` 把它们藏起来。第 37 章会加入原子容量与幂等策略，再定义重试和重启行为。在那之前，这个 API 是可运行的边界演示，不是具备一致性安全的商业预约服务。

名为“dependency failures are safe and reveal the post-commit notification window”的测试证明：通知失败会返回安全 `503`，而记录状态已经是 `Booked`。这是问题存在的证据，不是问题已经解决的证据。

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
适配器把已知的提供商传输或可用性故障包装为 `DependencyUnavailableException`，并把原异常保留为 `InnerException` 供内部诊断。具体 `Charge` 或 `Notify` 调用只把这一类型化信号转换为 `503 dependency_unavailable`；任意程序缺陷异常会继续到最外层边界，成为安全的 `500 internal_error`。`BookingStoreAdapterException` 同样为内部代码保留类型化类别，但通过 HTTP 只暴露 `storage_unavailable`。

如果响应头已经开始后才发生错误，写入第二份 JSON 文档会破坏响应。处理程序会改为中止该连接。已知 DTO 的序列化刻意很简单，但边界仍不会假装已经开始的响应可以被替换。

本章不加入详细故障日志。第 38 章会在显式数据分类下加入结构化诊断。在该策略存在之前，沉默比记录未知异常消息安全；但生产环境的沉默并不等于可观测性。

## 加载配置而不披露它 {#configuration-secrets}

宿主读取 `BOOKING_STORE_PATH`，以及可选的 `BOOKING_EVENT_ID` 和 `BOOKING_CAPACITY`，然后构造受保护的配置与领域值。被拒绝的设置只产生 `invalid_booking_store`、`invalid_event_id` 或 `invalid_capacity`；原始值不会被打印。

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

冒烟测试中观察到的默认宿主日志会记录方法、路由、状态、内容类型、长度和耗时，不记录请求体或配置值。不要随意启用请求/响应体日志：它会缓冲数据，并可能捕获个人信息或凭据。应先分类与脱敏。

Kestrel 的 `Server` 头已关闭，以减少无必要的实现披露。这是加固，不是身份验证或授权。

## 同时测试管线与传输 {#testing}

契约测试使用官方 `Microsoft.AspNetCore.TestHost` 包。每个测试都构建真实 `WebApplication`、映射真实端点、注入受控端口、启动内存管线，再通过 `HttpClient` 发送请求。

聚焦用例覆盖：

- 精确成功 JSON、`Location`、查询、确认与取消；
- 格式错误 JSON、属性大小写错误、未知属性、缺失字段、空请求体与错误媒体类型；
- 在任何效果前累积领域验证；
- 在 JSON 解析前执行 16 KiB 上限；
- 重复、缺失、容量与支付结果；
- 提交前支付故障与提交后通知故障；
- 类型化存储故障、意外故障与响应脱敏；
- 不用计时 sleep，让取消抵达受控阻塞端口。

`TestServer` 在内存中发送请求，并且有意不复现所有传输行为或头部。因此应用级字节上限要在其中测试，另一个回环 Kestrel 冒烟测试则验证真实启动、头部、路由与文件持久化。

两种测试都不能取代另一种。为每条契约断言启动随机真实端口会增加噪声；只依赖 `TestServer` 又会让 Kestrel 配置得不到观察。

## 在本地运行 API {#local-run}

以下命令使用临时快照，并且只绑定到回环地址。请在示例所在目录运行。

### 启动宿主 {#local-start}

在 macOS 或 Linux 上：

```bash
BOOKING_STORE_PATH="${TMPDIR:-/tmp}/thinking-in-fsharp-booking.json" \
BOOKING_EVENT_ID="EVT-LOCAL" \
BOOKING_CAPACITY="4" \
ASPNETCORE_URLS="http://127.0.0.1:5086" \
dotnet run --project Booking.Api.fsproj -c Release
```

在 PowerShell 中：

```powershell
$env:BOOKING_STORE_PATH = Join-Path ([IO.Path]::GetTempPath()) "thinking-in-fsharp-booking.json"
$env:BOOKING_EVENT_ID = "EVT-LOCAL"
$env:BOOKING_CAPACITY = "4"
$env:ASPNETCORE_URLS = "http://127.0.0.1:5086"
dotnet run --project Booking.Api.fsproj -c Release
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

第一个响应是 `201`，包含 `Location: /api/bookings/REQ-36`，并返回待确认的 `BookingDto`。第二个响应是 `200`，内容是已持久化表示。

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

删除显式临时快照之前应先停止宿主。以后使用同一路径启动时会恢复已保存状态；这个事实会用于第 37 章的重启测试。

## 了解生产边界 {#production-boundary}

这个本地宿主刻意没有身份验证、授权、TLS 证书、CORS 策略、速率限制器、代理信任配置、分布式存储或真实支付提供商。不得把它原样暴露到不可信网络。

直接在边缘部署时，应决定 TLS、HSTS、允许的主机、速率限制、请求超时、身份验证、授权与机密存储。部署在反向代理后时，还要用显式受信代理配置转发头，并决定哪个层终止 TLS、执行每一种上限。

不要在没有威胁模型时为了“安全”加入所有中间件。例如，CORS 约束浏览器，而不是任意 HTTP 客户端；启用宽松策略会削弱边界，而不是补全边界。第 42 章会重新讨论部署选择，第 38 章则只加入本例需要的诊断与发布检查。

## 避免常见 API 边界错误 {#boundary-mistakes}

- 直接序列化 `BookingCommand` 或 `Booking`，会把编译器表示变成公共协议。
- 把已反序列化 DTO 当成已验证领域数据，会跳过智能构造函数与累积规则。
- 只信任 `Content-Length`，会让未知长度请求体失去上限。
- 让持久化与 HTTP 使用不同 JSON 选项，会为一个 DTO 制造两种含义。
- 把每种拒绝都映射成 `400`，会抹去客户端可以安全采取的下一步。
- 返回 `exception.Message`，可能披露路径、提供商详情或实现名称。
- 把客户端取消捕获成 `500`，同时歪曲请求和服务器状态。
- 在支付或通知结果不明确后重试，可能重复效果。
- 假设内存服务器会复现 Kestrel 传输行为，会留下验证缺口。
- 把生产凭据放入环境变量，并不等于加密或访问控制。
- 在分类与脱敏前启用请求体日志，可能让诊断变成数据泄漏。
- 把这个无认证回环样例称为生产就绪，会夸大它的边界。

## 练习 {#exercises}

### 练习 1：改变绑定但不改变契约 {#exercise-01}

把一个命令路由重新设计为使用 Minimal API 自动参数绑定。保留完全相同的严格 JSON 策略、16 KiB 有效上限、`ApiErrorDto` 形状、取消传播和全部状态码/代码组合。指出哪些行为属于配置、端点过滤器或中间件，以及处理程序。给出能够阻止框架默认值改变公共响应的契约测试。

### 练习 2：从最后可见效果开始推理 {#exercise-02}

针对每个中断——支付授权后追加失败、追加成功后通知失败、通知成功后客户端断连——说明提供商、快照、调用方与一次重试分别能观察什么。提出第 37 章必须持久化的最小幂等信息。不要声称存在分布式事务。

### 练习 3：审阅两种部署拓扑 {#exercise-03}

比较直接暴露 Kestrel 与把它放在反向代理之后。为 TLS、转发头、主机过滤、请求上限、速率限制、身份验证、机密获取与日志脱敏制作一张简短责任表。标出哪些控制是这个预约 API 必需的，哪些取决于部署需求。

[阅读本章答案](../solutions/ch-36-web-api-boundaries)。

## 模型回顾 {#model-review}

- HTTP 是外层解释器，不是领域规则的拥有者。
- 四条显式路由只接收和返回边界表示。
- 媒体类型、大小、语法、DTO 存在性与领域有效性是不同检查。
- 真正的上限即使没有 `Content-Length` 也会计数字节。
- 一份严格 JSON 策略可防止传输与持久化漂移。
- 稳定错误代码是公共数据；异常与提供商消息不是。
- `RequestAborted` 贯穿每个异步效果与响应写入。
- 取消不会回滚已经可见的效果。
- 支付先于追加、通知晚于追加，造成不同的重试风险。
- `TestServer` 证明管线；回环 Kestrel 证明选定的传输行为。
- 配置拒绝从不需要打印被拒绝值。
- 环境变量避免提交，但不是加密的机密存储。
- 请求体日志需要显式分类与脱敏。
- 关闭服务器头部是加固，不是授权系统。
- 当前宿主可运行、可测试，但还不具备一致性安全或生产完整性。

## 来源 {#sources}

- [Microsoft Learn：Minimal API 快速参考](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis?view=aspnetcore-10.0)
- [Microsoft Learn：`HttpRequestJsonExtensions` 与 JSON 内容类型](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.http.httprequestjsonextensions?view=aspnetcore-10.0)
- [Microsoft Learn：拒绝未映射的 JSON 成员](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/missing-members)
- [Microsoft Learn：`HttpContext.RequestAborted`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.http.defaulthttpcontext.requestaborted?view=aspnetcore-10.0)
- [Microsoft Learn：用 `TestServer` 测试 ASP.NET Core 中间件](https://learn.microsoft.com/en-us/aspnet/core/test/middleware?view=aspnetcore-10.0)
- [Microsoft Learn：Kestrel 安全注意事项与可配置上限](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/security-considerations?view=aspnetcore-10.0)
- [Microsoft Learn：在开发中安全存储应用机密](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets?view=aspnetcore-10.0)
- [Microsoft Learn：HTTP 日志与脱敏](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/http-logging/?view=aspnetcore-10.0)
