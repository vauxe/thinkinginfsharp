---
title: "第 35 章：端口、持久化、配置与替身"
description: "用明确的 DTO 映射隔离 F# 领域值，安全持久化有界本地快照，并在装配确定性适配器时明确资源释放责任。"
translationKey: part-06/ch-35-ports-persistence-config
---

# 第 35 章：端口、持久化、配置与替身 {#overview}

第 34 章停在一个已接受事实处。本章开始执行外部操作，同时不让 JSON、路径或测试服务行为变成领域规则。设计仍然很小：一份带版本的 DTO 契约、一个限制大小的本地快照、行为确定的支付与通知替身，以及一个负责清理它们的组合对象。

核心问题是每项规则由谁决定。领域决定命令是否合法；映射器决定外部表示能否变成受保护数据；文件适配器决定如何替换字节；组合根决定哪些实现提供能力，以及由谁释放它们。分开这些决策，才能如实表示失败并进行测试。

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

`Booking.Domain` 命名命令、事实、受保护值、决策与所需端口；它不知道 JSON 或文件。`Booking.Contracts` 引用领域，只负责明确转换。`Booking.Infrastructure` 使用这两层实现副作用。未来 API 可以在组合根引用全部三层，但领域永远不会反向依赖外层。

这种分层有实际作用。如果领域引用 `JsonPropertyNameAttribute`、文件路径或支付替身，改变外层机制就可能迫使业务类型变化。依赖图能避免外层机制意外决定业务类型。

## 分离传输表示与领域表示 {#separate-shapes}

快照 DTO 有意采用常规 .NET 数据结构：

```fsharp:line-numbers [Dtos.fs]
[<CLIMutable>]
type BookingDto =
    { [<JsonPropertyName("schemaVersion")>]
      SchemaVersion: int
      [<JsonPropertyName("requestId")>]
      RequestId: string | null
      [<JsonPropertyName("eventId")>]
      EventId: string | null
      [<JsonPropertyName("seats")>]
      Seats: Nullable<int>
      [<JsonPropertyName("status")>]
      Status: string | null
      [<JsonPropertyName("confirmationCode")>]
      ConfirmationCode: string | null
      [<JsonPropertyName("cancellationReason")>]
      CancellationReason: string | null }
```
`[<CLIMutable>]` 为序列化器和其他 .NET 调用方添加无参构造函数与属性设置器；它不会让记录变成领域实体。`[<JsonPropertyName>]` 固定序列化格式中的名称，不受未来 F# 字段重命名影响。

DTO 允许领域禁止的状态：空标识、缺失的座位数、未知状态字符串，或两个状态载荷同时出现。这在不可信边界上是正确的。如果其类型假装这些值不可能出现，反序列化失败只会转移到反射或异常中，却没有给应用一项显式映射策略。

受保护的 `Booking` 记录继续保持私有，`BookingStatus` 继续作为有用的 F# 联合。两者都不直接序列化。因此领域表示可以演进，而不会悄悄重新定义已存储 JSON。

## 有意设计联合表示 {#union-representation}

版本 1 把 `BookingStatus` 投影成一个确定标签和至多一个载荷：

| 领域值 | `status` | 必需载荷 | 禁止载荷 |
|---|---|---|---|
| `Pending` | `"pending"` | 无 | 确认码与取消原因 |
| `Confirmed code` | `"confirmed"` | `confirmationCode` | 取消原因 |
| `Cancelled reason` | `"cancelled"` | `cancellationReason` | 确认码 |

这里的原始字符串比 CLR 枚举更合适。领域值并不是枚举：两个案例携带不同的受保护数据。字符串标签还让映射可以返回 `UnknownStatus actual`，而不是让序列化器默认值擅自发明数字约定。

省略空载荷可以缩小成功 JSON，又不会造成歧义。标签指出哪个载荷必须存在。契约测试断言每种案例的准确属性集合，防止序列化选项变化后悄悄加入两个空字段。

## 让反向映射显式化 {#explicit-mapping}

映射错误联合会命名表示失败，而不是把它们压扁成文本：

```fsharp:line-numbers [Mapping.fs]
[<RequireQualifiedAccess>]
type DtoMappingError =
    | MissingBody
    | UnsupportedSchemaVersion of actual: int
    | MissingRequestId
    | MissingEventId
    | MissingSeats
    | MissingStatus
    | MissingConfirmationCode
    | MissingCancellationReason
    | InvalidRequestId of RequestIdError
    | InvalidEventId of EventIdError
    | InvalidSeatCount of SeatCountError
    | InvalidConfirmationCode of ConfirmationCodeError
    | InvalidCancellationReason of CancellationReasonError
    | UnknownStatus of actual: string
    | UnexpectedConfirmationCode of status: string
    | UnexpectedCancellationReason of status: string
```
反向快照映射按已声明顺序进行：

```fsharp:line-numbers [Mapping.fs]
module BookingMapping =
    let ofDomain (booking: Booking) : BookingDto =
        let nullableText (value: string) : string | null = value
        let noText: string | null = null

        let status, confirmationCode, cancellationReason =
            match Booking.status booking with
            | Pending -> "pending", noText, noText
            | Confirmed code -> "confirmed", code |> ConfirmationCode.value |> nullableText, noText
            | Cancelled reason -> "cancelled", noText, reason |> CancellationReason.value |> nullableText

        { SchemaVersion = BookingContract.CurrentSchemaVersion
          RequestId = booking |> Booking.requestId |> RequestId.value
          EventId = booking |> Booking.eventId |> EventId.value
          Seats = booking |> Booking.seats |> SeatCount.value |> int |> Nullable
          Status = status
          ConfirmationCode = confirmationCode
          CancellationReason = cancellationReason }

    let toDomain (dto: BookingDto | null) =
        match dto with
        | null -> Error DtoMappingError.MissingBody
        | value when value.SchemaVersion <> BookingContract.CurrentSchemaVersion ->
            Error(DtoMappingError.UnsupportedSchemaVersion value.SchemaVersion)
        | value ->
            MappingInternals.requestId value.RequestId
            |> Result.bind (fun requestId ->
                MappingInternals.eventId value.EventId
                |> Result.map (fun eventId -> requestId, eventId))
            |> Result.bind (fun (requestId, eventId) ->
                MappingInternals.seats value.Seats
                |> Result.map (fun seats -> requestId, eventId, seats))
            |> Result.bind (fun (requestId, eventId, seats) ->
                MappingInternals.status value
                |> Result.map (Booking.restore requestId eventId seats))
```
首先检查模式版本。版本 2 文档即使其余字段碰巧类似版本 1，也并不兼容；因此映射器会在解释载荷前返回 `UnsupportedSchemaVersion 2`。

随后，标识与座位数原语经过已有智能构造函数。状态映射再检查标签是否匹配、载荷组合是否合法。只有每个值都受保护之后，`Booking.restore` 才重建私有记录。这个函数接收受保护值，不接收原始 JSON 字符串或整数。

对有效 `Booking` 的正向映射不会失败：每个联合案例都有一种已声明投影。反向映射可以失败，因为外部表示没有领域保证。这种不对称是有用信息，不是 API 缺陷。

### 命令映射只负责检查传输字段 {#command-mapping}

命令 DTO 做一项更窄的工作：

```fsharp:line-numbers [Mapping.fs]
module PlaceBookingMapping =
    let ofDomain (command: PlaceBooking) : PlaceBookingDto =
        { RequestId = command.RequestId
          Seats = Nullable command.Seats }

    let toDomain (dto: PlaceBookingDto | null) =
        match dto with
        | null -> Error DtoMappingError.MissingBody
        | value ->
            match value.RequestId with
            | null -> Error DtoMappingError.MissingRequestId
            | requestId -> MappingInternals.rawSeats value.Seats |> Result.map (Commands.place requestId)

module ConfirmBookingMapping =
    let ofDomain (command: ConfirmBooking) : ConfirmBookingDto =
        { RequestId = command.RequestId
          ConfirmationCode = command.ConfirmationCode }

    let toDomain (dto: ConfirmBookingDto | null) =
        match dto with
        | null -> Error DtoMappingError.MissingBody
        | value ->
            match value.RequestId, value.ConfirmationCode with
            | null, _ -> Error DtoMappingError.MissingRequestId
            | _, null -> Error DtoMappingError.MissingConfirmationCode
            | requestId, confirmationCode -> Ok(Commands.confirm requestId confirmationCode)

module CancelBookingMapping =
    let ofDomain (command: CancelBooking) : CancelBookingDto =
        { RequestId = command.RequestId
          Reason = command.Reason }

    let toDomain (dto: CancelBookingDto | null) =
        match dto with
        | null -> Error DtoMappingError.MissingBody
        | value ->
            match value.RequestId, value.Reason with
            | null, _ -> Error DtoMappingError.MissingRequestId
            | _, null -> Error DtoMappingError.MissingCancellationReason
            | requestId, reason -> Ok(Commands.cancel requestId reason)
```
它们拒绝传输数据缺失，例如没有请求体、请求标识、座位属性、确认码或原因。空白字符串与零座位则原样保留在领域命令中。第 34 章的验证器负责这些规则并累积错误；在 DTO 映射中重复检查会复制策略，还可能改变错误优先级。

所以“映射成功”只表示传输层提供了表达一项意图所需的字段。它不表示意图已经通过领域验证或业务决策。

## 一次固定序列化策略 {#json-policy}

JSON 辅助模块在使用前配置一个私有选项对象：

```fsharp:line-numbers [Dtos.fs]
module BookingJson =
    // Wire names: https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/customize-properties
    // Unmapped data: https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/missing-members
    let configure (options: JsonSerializerOptions) =
        ArgumentNullException.ThrowIfNull(options, nameof options)
        options.PropertyNamingPolicy <- JsonNamingPolicy.CamelCase
        options.PropertyNameCaseInsensitive <- false
        options.UnmappedMemberHandling <- JsonUnmappedMemberHandling.Disallow
        options.DefaultIgnoreCondition <- JsonIgnoreCondition.WhenWritingNull
        options.MaxDepth <- 8

    let private options =
        let settings = JsonSerializerOptions()
        configure settings
        settings

    let serializeBooking (dto: BookingDto) =
        ArgumentNullException.ThrowIfNull(dto, nameof dto)
        JsonSerializer.Serialize(dto, options)

    let deserializeBooking (json: string) : BookingDto | null =
        ArgumentNullException.ThrowIfNull(json, nameof json)
        JsonSerializer.Deserialize<BookingDto>(json, options)

    let deserializePlaceBooking (json: string) : PlaceBookingDto | null =
        ArgumentNullException.ThrowIfNull(json, nameof json)
        JsonSerializer.Deserialize<PlaceBookingDto>(json, options)

    let deserializeConfirmBooking (json: string) : ConfirmBookingDto | null =
        ArgumentNullException.ThrowIfNull(json, nameof json)
        JsonSerializer.Deserialize<ConfirmBookingDto>(json, options)

    let deserializeCancelBooking (json: string) : CancelBookingDto | null =
        ArgumentNullException.ThrowIfNull(json, nameof json)
        JsonSerializer.Deserialize<CancelBookingDto>(json, options)
```
这些选择属于边界契约：

- 属性名称使用 camel case，并由特性显式指定；
- 读取区分大小写，所以 `RequestId` 不是 `requestId` 的别名；
- 未映射成员会被拒绝，而不是悄悄忽略；
- 写入时省略空属性；
- 嵌套深度受到限制；
- JSON 的 `null` 请求体仍可表示，并映射为 `MissingBody`。

遇到未知成员就拒绝，是一种严格的兼容策略。它能捕获拼写错误与生产者意外变化，但也意味着新增字段需要有意改变版本或策略。必须向客户端写清这项取舍；“JSON 很灵活”不是兼容性契约。

JSON 契约测试固定标签、属性集合、大小写、未知字段、版本优先级、所有受保护状态往返、缺失值、不可能的载荷组合以及原始命令保留。

## 把路径当作已验证配置 {#configuration}

文件适配器接收一个受保护配置值：

```fsharp:line-numbers [Configuration.fs]
[<RequireQualifiedAccess>]
module BookingStoreConfiguration =
    [<Literal>]
    let PathEnvironmentVariable = "BOOKING_STORE_PATH"

    let create (configuredPath: string | null) =
        match configuredPath with
        | null -> Error BookingStoreConfigurationError.MissingSnapshotPath
        | raw when String.IsNullOrWhiteSpace raw -> Error BookingStoreConfigurationError.MissingSnapshotPath
        | raw ->
            try
                let fullPath = raw.Trim() |> Path.GetFullPath
                let fileName = Path.GetFileName fullPath
                let directory = Path.GetDirectoryName fullPath

                match directory with
                | null -> Error BookingStoreConfigurationError.InvalidSnapshotPath
                | value when String.IsNullOrWhiteSpace fileName || Directory.Exists fullPath ->
                    Error BookingStoreConfigurationError.InvalidSnapshotPath
                | value ->
                    Ok
                        { SnapshotPath = fullPath
                          DirectoryPath = value }
            with
            | :? ArgumentException
            | :? NotSupportedException
            | :? PathTooLongException -> Error BookingStoreConfigurationError.InvalidSnapshotPath

    // Environment variables override file settings in the default .NET configuration stack:
    // https://learn.microsoft.com/dotnet/core/extensions/configuration-providers#environment-variable-configuration-provider
    let fromEnvironment () =
        Environment.GetEnvironmentVariable PathEnvironmentVariable |> create

    let snapshotPath configuration = configuration.SnapshotPath

    let internal directoryPath configuration = configuration.DirectoryPath
```
`create` 区分缺失值与无效文件路径，规范化成绝对路径，并拒绝已经指向目录的路径。因此适配器不会反复重新解释原始配置。

`BOOKING_STORE_PATH` 可以来自环境变量提供程序，而测试调用 `create` 时只传入操作系统临时目录下的路径。存储路径是配置，不是机密。凭据、API 密钥与证书需要机密提供程序，不能仅因环境变量也承载配置就把它们提交进仓库。

路径由部署配置控制，绝不从请求 ID 派生。这样不会把用户输入变成路径遍历入口或无限增长的文件集合。

## 持久化一个有界快照 {#bounded-snapshot}

`FileBookingStore` 对受保护 `Booking` 暴露异步 `Load` 与 `Save` 操作：

```fsharp:line-numbers [FileStore.fs]
type FileBookingStore(configuration: BookingStoreConfiguration) =
    let snapshotPath = BookingStoreConfiguration.snapshotPath configuration
    let directoryPath = BookingStoreConfiguration.directoryPath configuration

    static member MaxSnapshotBytes = FileStoreImplementation.MaxSnapshotBytes

    member _.Load(cancellationToken: CancellationToken) : Task<Result<Booking option, BookingStoreError>> =
        task {
            cancellationToken.ThrowIfCancellationRequested()

            let! bytesResult =
                FileStoreImplementation.readBounded
                    FileStoreImplementation.MaxSnapshotBytes
                    snapshotPath
                    cancellationToken

            match bytesResult with
            | Error error -> return Error error
            | Ok None -> return Ok None
            | Ok(Some bytes) ->
                match FileStoreImplementation.decode bytes with
                | Error error -> return Error error
                | Ok json ->
                    try
                        return
                            BookingJson.deserializeBooking json
                            |> BookingMapping.toDomain
                            |> Result.map Some
                            |> Result.mapError (
                                SnapshotCorruption.InvalidDomainData >> BookingStoreError.CorruptSnapshot
                            )
                    with :? JsonException ->
                        return Error(BookingStoreError.CorruptSnapshot SnapshotCorruption.InvalidJson)
        }

    member _.Save(booking: Booking, cancellationToken: CancellationToken) : Task<Result<unit, BookingStoreError>> =
        task {
            cancellationToken.ThrowIfCancellationRequested()

            let bytes =
                booking
                |> BookingMapping.ofDomain
                |> BookingJson.serializeBooking
                |> Encoding.UTF8.GetBytes

            if bytes.Length > FileStoreImplementation.MaxSnapshotBytes then
                return Error(BookingStoreError.SnapshotTooLarge FileStoreImplementation.MaxSnapshotBytes)
            else
                let temporaryPath =
                    Path.Combine(directoryPath, $".{Path.GetFileName(snapshotPath)}.{Guid.NewGuid():N}.tmp")

                try
                    let directoryResult =
                        try
                            Directory.CreateDirectory directoryPath |> ignore
                            Ok()
                        with
                        | :? IOException
                        | :? UnauthorizedAccessException -> Error BookingStoreError.CannotWriteTemporarySnapshot

                    match directoryResult with
                    | Error error -> return Error error
                    | Ok() ->
                        let! writeResult = FileStoreImplementation.writeTemporary temporaryPath bytes cancellationToken

                        match writeResult with
                        | Error error -> return Error error
                        | Ok() ->
                            cancellationToken.ThrowIfCancellationRequested()
                            return FileStoreImplementation.replace temporaryPath snapshotPath
                finally
                    FileStoreImplementation.cleanup temporaryPath
        }
```
内部保存过程把预约映射为 `BookingDto`，序列化为无字节顺序标记的 UTF-8，并拒绝大于 64 KiB 的输出。加载过程最多读取 64 KiB，再多读取一个字节来判断文件是否超限。它接受可选 UTF-8 BOM，但拒绝无效字节序列。

固定上限会阻止损坏或被替换的本地文件导致无界分配。64 KiB 是针对一个小快照的样例上限，不是通用 JSON 限制。集合存储需要根据真实基数与流式策略推导上限。

文件或目录缺失表示普通缺失，并映射为 `Ok None`。读取与权限失败使用独立运维案例。损坏则分为 JSON 语法无效、UTF-8 无效，以及结构有效的 JSON 无法转换成受保护预约三类。

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

## 在单一组合边界装配能力 {#composition}

基础设施组合对象提供领域的 `AsyncPorts` 记录：

```fsharp:line-numbers [Composition.fs]
type InfrastructureComposition
    internal
    (
        configuration: BookingStoreConfiguration,
        paymentBehavior: PaymentStubBehavior,
        notificationBehavior: NotificationStubBehavior,
        getUtcNow: CancellationToken -> Task<DateTimeOffset>
    ) =

    let syncRoot = obj ()
    let store = FileBookingStore configuration
    let payment = new PaymentStub(paymentBehavior)
    let notification = new NotificationStub(notificationBehavior)
    let mutable disposed = false

    let ensureActive (cancellationToken: CancellationToken) =
        cancellationToken.ThrowIfCancellationRequested()

        lock syncRoot (fun () ->
            if disposed then
                raise (ObjectDisposedException(nameof InfrastructureComposition)))

    let unwrapStoreResult result =
        match result with
        | Ok value -> value
        | Error error -> raise (BookingStoreAdapterException error)

    let ports: AsyncPorts =
        { LoadBooking =
            fun requestId cancellationToken ->
                task {
                    ensureActive cancellationToken
                    let! stored = store.Load cancellationToken

                    return
                        match unwrapStoreResult stored with
                        | Some booking when Booking.requestId booking = requestId -> Booked booking
                        | Some _
                        | None -> NotBooked
                }
          AppendEvent =
            fun requestId bookingEvent cancellationToken ->
                task {
                    ensureActive cancellationToken
                    let booking = BookingEvent.booking bookingEvent

                    if Booking.requestId booking <> requestId then
                        invalidArg (nameof requestId) "The event request ID must match the storage key."

                    let! saved = store.Save(booking, cancellationToken)
                    return unwrapStoreResult saved
                }
          Charge =
            fun request cancellationToken ->
                ensureActive cancellationToken
                payment.Invoke request cancellationToken
          Notify =
            fun request cancellationToken ->
                ensureActive cancellationToken
                notification.Invoke request cancellationToken
          GetUtcNow =
            fun cancellationToken ->
                ensureActive cancellationToken
                getUtcNow cancellationToken }

    member _.Ports = ports
    member _.PaymentStub = payment
    member _.NotificationStub = notification
    member _.IsDisposed = lock syncRoot (fun () -> disposed)

    interface IDisposable with
        member _.Dispose() =
            let shouldDispose =
                lock syncRoot (fun () ->
                    if disposed then
                        false
                    else
                        disposed <- true
                        true)

            if shouldDispose then
                (notification :> IDisposable).Dispose()
                (payment :> IDisposable).Dispose()

[<RequireQualifiedAccess>]
module Composition =
    // The returned composition creates and owns both stubs; dispose it at the application boundary.
    let start configuration paymentBehavior notificationBehavior getUtcNow =
        new InfrastructureComposition(configuration, paymentBehavior, notificationBehavior, getUtcNow)
```
每个函数都保留调用方的 `CancellationToken`。存储错误变成 `BookingStoreAdapterException`，既保留类型化内部类别，也让后续 HTTP 层只有一个位置映射安全响应。异常消息既不含文件内容，也不含已配置路径。

`LoadBooking` 尊重请求键，对不同的已存请求返回 `NotBooked`。`AppendEvent` 在保存事件结果预约之前，会拒绝参数键与事件受保护请求 ID 不一致。

当前适配器只存一个快照。因此另一个请求在稍后成功追加后可以替换先前快照。这是明确承认的教学阶段限制，不是多预约仓库。第 37 章会在把 API 称作一致性安全之前替换这种读写模型。

## 使用结果固定的测试替身 {#deterministic-stubs}

支付替身在构造时固定行为：

```fsharp:line-numbers [PaymentStub.fs]
type PaymentStub(behavior: PaymentStubBehavior) =
    let syncRoot = obj ()
    let calls = ResizeArray<PaymentRequest>()
    let mutable disposed = false

    let ensureActive () =
        if disposed then
            raise (ObjectDisposedException(nameof PaymentStub))

    member _.Calls: IReadOnlyList<PaymentRequest> =
        lock syncRoot (fun () -> calls.ToArray())

    member _.IsDisposed = lock syncRoot (fun () -> disposed)

    member _.Invoke (request: PaymentRequest) (cancellationToken: CancellationToken) : Task<PaymentOutcome> =
        task {
            cancellationToken.ThrowIfCancellationRequested()

            lock syncRoot (fun () ->
                ensureActive ()
                calls.Add request)

            match behavior with
            | PaymentStubBehavior.Authorize transactionId -> return Authorized transactionId
            | PaymentStubBehavior.Decline reason -> return Declined reason
            | PaymentStubBehavior.Fail message ->
                return
                    raise (
                        DependencyUnavailableException(
                            "Payment dependency is unavailable.",
                            InvalidOperationException message
                        )
                    )
        }

    interface IDisposable with
        member _.Dispose() =
            lock syncRoot (fun () -> disposed <- true)
```
它有三种固定结果：使用给定交易 ID 授权、返回给定拒绝原因，或抛出 `DependencyUnavailableException`。最后一种情况下，`InnerException` 保存给定的故障详情。通知替身同样会交付，或抛出相同的类型化故障：

```fsharp:line-numbers [NotificationStub.fs]
type NotificationStub(behavior: NotificationStubBehavior) =
    let syncRoot = obj ()
    let calls = ResizeArray<NotificationRequest>()
    let mutable disposed = false

    let ensureActive () =
        if disposed then
            raise (ObjectDisposedException(nameof NotificationStub))

    member _.Calls: IReadOnlyList<NotificationRequest> =
        lock syncRoot (fun () -> calls.ToArray())

    member _.IsDisposed = lock syncRoot (fun () -> disposed)

    member _.Invoke (request: NotificationRequest) (cancellationToken: CancellationToken) : Task<unit> =
        task {
            cancellationToken.ThrowIfCancellationRequested()

            lock syncRoot (fun () ->
                ensureActive ()
                calls.Add request)

            match behavior with
            | NotificationStubBehavior.Deliver -> return ()
            | NotificationStubBehavior.Fail message ->
                return
                    raise (
                        DependencyUnavailableException(
                            "Notification dependency is unavailable.",
                            InvalidOperationException message
                        )
                    )
        }

    interface IDisposable with
        member _.Dispose() =
            lock syncRoot (fun () -> disposed <- true)
```
两者都在记录调用前检查取消，也不使用 HTTP、时钟、随机数、休眠、凭据或环境状态。调用列表是同步快照，因此无需 mock 框架也能稳定断言。

这些替身用于学习和控制集成。它们不模拟支付授权协议、重试、Webhook 交付、消息持久性、欺诈检查或提供商幂等性。以 `Stub` 命名，可防止读者误把确定性行为当成生产集成。

## 在构造处说明资源所有权 {#ownership}

`Composition.start` 构造两个替身，并返回负责释放它们的对象。应用应在最外层生命周期边界用 `use` 绑定该对象。释放时，组合先把自身标记为关闭，再依次释放通知和支付替身；重复释放仍然安全。

端口会拒绝释放后的调用。预先取消的调用会先观察取消，再检查是否已释放；这个顺序由 `ensureActive` 固定。文件适配器不会在调用之间保留打开的流，因此每个 `use stream` 都在一次操作内打开并释放句柄。

如果组合接收在外部创建的 `IDisposable` 值，却不说明是借用还是接管其释放责任，资源所有权就会含糊。在 `start` 内构造由组合负责释放的值，可以直接看出这项策略。

## 保持失败类别分离 {#failure-categories}

| 边界 | 预期表示 | 处理方式 |
|---|---|---|
| 缺少传输字段 | `DtoMappingError` | 返回值；尚不调用领域验证 |
| 无效领域原语或联合载荷 | `DtoMappingError` | 返回值并拒绝重建 |
| 未知 schema 版本 | `UnsupportedSchemaVersion` | 在解释版本特定载荷前停止 |
| 损坏或过大快照 | `BookingStoreError` | 保留类型化存储分类 |
| I/O 或替换失败 | `BookingStoreError` | 作为运维适配器失败向外传递 |
| 支付拒绝 | `PaymentOutcome.Declined` | 预期服务结果，不是异常 |
| 替身提供商离线 | `DependencyUnavailableException` | 让异步操作进入故障状态，并把替身原因保留为 `InnerException` |
| 调用方取消 | 已取消的 `Task` / `OperationCanceledException` | 传播调用方令牌；不记录新的替身工作 |
| 领域拒绝 | `BookingDecisionError` | 仍留在纯工作流，不进入适配器 |

一个通用 `Error of string` 会抹掉哪个层有权恢复或报告。相反，为每种领域拒绝发明独立异常类，又会把普通业务结果变成出人意料的控制流。

## 用真实实现验证副作用 {#testing}

文件存储契约测试只写入各自唯一的系统临时目录。测试覆盖真实 JSON 往返、替换后无临时文件残留、缺失文件行为、严格编码、损坏分类、大小上限与路径验证。测试还确认，保存前取消会保留原有完整快照。

适配器测试运行真实文件适配器和结果固定的测试替身。它们覆盖授权、拒绝、交付、指定故障、取消且不记录副作用、令牌传递到时钟、通过组合端口持久化、类型化损坏错误、重复释放，以及释放后拒绝使用。

Release 解决方案构建在 F# 10 空值检查和警告即错误下通过。完整示例检查会还原锁定依赖、构建每个已注册项目，并运行测试与脚本。贯穿项目的运行时工程不增加第三方运行时包，也不需要服务账号；测试与工具检查仍会还原其锁定包。

这些测试尚未覆盖 HTTP 输入、并发容量、重试、多预约存储重启或 C# 客户端。接下来三章会逐项处理，而不是在本章中默认它们已经成立。

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
- 在离释放位置很远处构造资源，会让释放责任难以审查。
- 从未来 API 直接返回异常消息，可能泄露运维细节。

## 练习 {#exercises}

### 练习 1：演进快照契约 {#exercise-01}

版本 2 必须增加可选的 `customerNote`，同时仍能加载旧版版本 1 文件。提出 DTO 与映射策略。说明版本 1 是在内存中升级、立即重写，还是仅在下一次成功保存时重写。明确说明如何处理未知字段与版本 3。

### 练习 2：审计每个保存中断点 {#exercise-02}

对于发生在以下位置的取消或失败，说明目标文件与临时文件可能包含什么：(a) 创建临时文件前；(b) 写入期间；(c) 刷新之后、移动之前；(d) 移动之后。分开进程可见替换、缓冲区刷新与断电持久性主张。

### 练习 3：明确改变资源释放责任 {#exercise-03}

假设生产环境的支付与通知客户端由宿主容器创建，并在多个工作流之间共享。重新设计 `Composition.start`，让它借用而不是拥有这些客户端。展示释放移到何处、如何阻止释放后使用，以及确定性测试如何继续控制成功、拒绝、故障与取消。

[阅读本章答案](../solutions/ch-35-ports-persistence-config)。

## 资料来源 {#sources}

- [Microsoft Learn：自定义 `System.Text.Json` 属性名称与枚举表示](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/customize-properties)
- [Microsoft Learn：拒绝未映射 JSON 成员](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/missing-members)
- [FSharp.Core 参考：`CLIMutableAttribute`](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-climutableattribute.html)
- [Microsoft Learn：`File.Move` 重载与跨卷行为](https://learn.microsoft.com/en-us/dotnet/api/system.io.file.move?view=net-10.0)
- [Microsoft Learn：`FileStream.Flush(Boolean)`](https://learn.microsoft.com/en-us/dotnet/api/system.io.filestream.flush?view=net-10.0)
- [Microsoft Learn：.NET 配置提供程序与环境变量优先级](https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration-providers)
- [Microsoft Learn：`CancellationToken`](https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken?view=net-10.0)
