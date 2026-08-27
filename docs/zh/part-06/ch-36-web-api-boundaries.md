---
title: "第 36 章：Web API、JSON 与输入边界"
description: "用小型 F# Minimal API 暴露预约工作流，并明确处理 JSON、验证、取消、失败与机密。"
translationKey: part-06/ch-36-web-api-boundaries
---

# 第 36 章：Web API、JSON 与输入边界 {#overview}

第 35 章在进程内装配了各项能力。本章通过一个小型 ASP.NET Core Minimal API 加入网络边界。“Minimal”描述宿主模型；边界仍要验证传入字节、把 DTO 映射成领域值、传播可观察的取消，并把已声明失败翻译成稳定响应。

每一步都要回答同一个问题：由哪一层决定？HTTP 决定媒体类型与状态码；JSON 契约决定传输格式；DTO 映射检查必需数据；领域决定业务有效性与状态转换；适配器执行副作用；API 只负责协调，并翻译已声明的结果。

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

API 暴露命令，而不是用一个通用端点接收可辨识联合的序列化结果：

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

这层 HTTP 边界暴露这些事实，而不是用通用 `try/with` 把它们藏起来。第 37 章会加入原子容量与幂等策略，再定义重试和重启行为。在那之前，这个 API 是可运行的边界演示，不是具备一致性安全的商业预约服务。

对应测试确认了一个现象：通知失败时返回安全的 `503`，但记录状态已经是 `Booked`。测试揭示了问题，并没有解决问题。

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

删除指定的临时快照前，应先停止宿主。以后使用同一路径启动时会恢复已保存状态；第 37 章的重启测试会用到这一行为。

## 了解生产边界 {#production-boundary}

这个本地宿主刻意没有身份验证、授权、TLS 证书、CORS 策略、速率限制器、代理信任配置、分布式存储或真实支付提供商。不得把它原样暴露到不可信网络。

直接在边缘部署时，应决定 TLS、HSTS、允许的主机、速率限制、请求超时、身份验证、授权与机密存储。部署在反向代理后时，还要用显式受信代理配置转发头，并决定哪个层终止 TLS、执行每一种上限。

应根据威胁模型选择中间件。例如，CORS 约束浏览器来源，身份验证与网络控制则约束其他 HTTP 客户端；宽松的 CORS 策略会扩大浏览器访问范围。第 42 章会重新讨论部署选择，第 38 章则加入本例所需的诊断与发布检查。

## 避免常见 API 边界错误 {#boundary-mistakes}

- 直接序列化 `BookingCommand` 或 `Booking`，会把编译器表示变成公共协议。
- 把已反序列化 DTO 当成已验证领域数据，会跳过智能构造函数与累积规则。
- 只信任 `Content-Length`，会让未知长度请求体失去上限。
- 让持久化与 HTTP 使用不同 JSON 选项，会为一个 DTO 制造两种含义。
- 把每种拒绝都映射成 `400`，会抹去客户端可以安全采取的下一步。
- 返回 `exception.Message`，可能披露路径、提供商详情或实现名称。
- 把客户端取消捕获成 `500`，同时歪曲请求和服务器状态。
- 在支付或通知结果不明确后重试，可能重复副作用。
- 假设内存服务器会复现 Kestrel 传输行为，会留下验证缺口。
- 把生产凭据放入环境变量，并不等于加密或访问控制。
- 在分类与脱敏前启用请求体日志，可能让诊断变成数据泄漏。
- 把这个无认证回环样例称为生产就绪，会夸大它的边界。

## 练习 {#exercises}

### 练习 1：改变绑定但不改变契约 {#exercise-01}

把一个命令路由重新设计为使用 Minimal API 自动参数绑定。保留完全相同的严格 JSON 策略、16 KiB 有效上限、`ApiErrorDto` 表示、取消传播和全部状态码/代码组合。指出哪些行为属于配置、端点过滤器或中间件，以及处理程序。给出能够阻止框架默认值改变公共响应的契约测试。

### 练习 2：从最后可见的副作用开始推理 {#exercise-02}

针对每个中断——支付授权后追加失败、追加成功后通知失败、通知成功后客户端断连——说明提供商、快照、调用方与一次重试分别能观察什么。提出第 37 章必须持久化的最小幂等信息。不要声称存在分布式事务。

### 练习 3：审阅两种部署拓扑 {#exercise-03}

比较直接暴露 Kestrel 与把它放在反向代理之后。为 TLS、转发头、主机过滤、请求上限、速率限制、身份验证、机密获取与日志脱敏制作一张简短责任表。标出哪些控制是这个预约 API 必需的，哪些取决于部署需求。

[阅读本章答案](../solutions/ch-36-web-api-boundaries)。

## 模型回顾 {#model-review}

- HTTP 负责解释外部请求，不负责决定领域规则。
- 四条独立路由只接收和返回外部表示。
- 媒体类型、大小、语法、DTO 存在性与领域有效性是不同检查。
- 真正的上限即使没有 `Content-Length` 也会计数字节。
- 一份严格 JSON 策略可防止传输与持久化漂移。
- 稳定错误代码是公共数据；异常与提供商消息不是。
- `RequestAborted` 贯穿每个异步副作用与响应写入。
- 取消不会回滚已经可见的副作用。
- 支付先于追加、通知晚于追加，造成不同的重试风险。
- `TestServer` 验证管线；回环 Kestrel 验证选定的传输行为。
- 配置拒绝从不需要打印被拒绝值。
- 环境变量避免提交，但不是加密的机密存储。
- 请求体日志需要事先分类与脱敏。
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
