---
title: "第 32 章：从函数到应用"
description: "从纯工作流构建小型 F# 应用，并明确配置、依赖、组合、取消、资源生命周期与最小可观测性。"
translationKey: part-05/ch-32-functions-to-applications
---

# 第 32 章：从函数到应用 {#overview}

纯函数决定应该发生什么，但它本身不会启动一个应用。真正运行的程序还要读取配置、调用存储或网络、传递取消信号、报告结果并释放资源。包围纯函数、负责这些工作的代码，常被称为“应用外壳”。

本章为此前的预约工作流添加一个最小控制台外壳。启动代码会在一个位置创建并连接全部依赖，这个位置叫作“组合根”。程序还会直接记录日志、指标和追踪；这种记录代码统称为“插桩”（instrumentation）。只有确实需要分层配置、依赖作用域、后台工作器或框架集成时，才采用更复杂的宿主。

完整项目位于 `examples/chapters/ch32/Ch32.App.fsproj`。它按 F# 的固定文件顺序编译：

- `Domain.fs` 定义业务类型和纯工作流；
- `Ports.fs` 定义配置、外部依赖以及用于演示的内存实现；
- `Composition.fs` 定义副作用的执行顺序，并集中创建应用；
- `Program.fs` 最后处理进程参数、监听器和输出。

后文代码块是这些文件的连续摘录，因此会使用前面文件已经定义的名称。

## 把应用拆成多层 {#application-boundaries}

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
  -> 释放自己负责的资源
```

这只是执行顺序，并不要求所有步骤都放在一个函数中。分层后，测试、故障处理和资源责任各有明确位置。

## 从所需副作用推导依赖 {#derive-ports}

从纯工作流的输入和输出开始。`Domain.fs` 中的 `decidePlaceBooking` 需要一个 `Event`、当前 `BookingState` 和 `PlaceBookingCommand`；它返回 `Result<BookingEvent, BookingFailure>`。因此，运行中的应用必须取得当前状态并持久化被接受的事件。样例中只有这两种副作用能力。

这里的“端口”（port）是应用架构术语，不是 F# 关键字。样例用函数记录表示端口；`Ports.fs` 已经打开 `System`、`System.Threading`、`System.Threading.Tasks`、`Booking.Domain` 和 `Booking.Domain.Workflow`，所以下面的 .NET 与领域类型都有明确来源：

```fsharp:line-numbers [Ports.fs]
type BookingPorts =
    { LoadBooking: RequestId -> CancellationToken -> Task<BookingState>
      AppendEvent: RequestId -> BookingEvent -> CancellationToken -> Task<unit>
      OwnedResource: IDisposable }

type BookingLog =
    { EventName: string
      Outcome: string
      RequestId: string
      Seats: int
      Detail: string }
```
这条记录包含函数，而不是实现类。每个签名都表达了有用的信息：

- 应用只在验证通过后才调用存储并传入 `RequestId`；
- 每个可能阻塞的操作都接收 `CancellationToken`；
- `Task<'T>` 表示异步完成，也保留 .NET 异常；
- `AppendEvent` 返回 `Task<unit>`，因为应用只需知道操作完成，不需要存储特有的数据；
- `OwnedResource` 表明该资源由应用释放。

本章的 `RequestId` 只是 `string` 的类型别名，用来标注意图；它不会像私有单用例联合那样在编译期阻止未验证字符串。真正的保证来自 `BookingApplication.Place` 先调用 `validatePlaceBooking`，再调用端口。若边界需要更强的静态保证，应把请求 ID 建模为带智能构造函数的受保护类型。

不要为了模仿接口繁多的架构而给每个方法创建一个端口。由同一适配器实现且生命周期相同的操作可以放在一起；只有调用者、故障策略、安全要求或生命周期不同时才拆分。函数记录适合小型 F# API 和测试替身；面对 C# 调用者、框架激活或有状态协议时，接口可能更合适。

规则仍由领域定义。名为 `CanBook` 的端口会把策略推入存储；`LoadBooking` 只提供事实，交给纯函数解释。同样，适配器只存储 `BookingEvent`，不决定请求是否可接受。

## 把配置当作不可信输入 {#configuration}

环境变量、JSON、命令行参数和机密存储都始于外部数据。它们存在并不意味着它们是有效的领域配置。应在启动附近只解析并验证一次，再把类型化值向内传递：

```fsharp:line-numbers [Ports.fs]
type ConfigError =
    | MissingSetting of name: string
    | InvalidSetting of name: string * value: string

type AppConfig = private { Event: Event }

module AppConfig =
    [<Literal>]
    let EventIdSetting = "BOOKING_EVENT_ID"

    [<Literal>]
    let CapacitySetting = "BOOKING_CAPACITY"

    let private readEventId (lookup: string -> string option) =
        match lookup EventIdSetting with
        | None -> Error [ MissingSetting EventIdSetting ]
        | Some raw ->
            EventId.create raw
            |> Result.mapError (fun _ -> [ InvalidSetting(EventIdSetting, raw) ])

    let private readCapacity (lookup: string -> string option) =
        match lookup CapacitySetting with
        | None -> Error [ MissingSetting CapacitySetting ]
        | Some raw ->
            match Int32.TryParse raw with
            | true, value ->
                Capacity.create value
                |> Result.mapError (fun _ -> [ InvalidSetting(CapacitySetting, raw) ])
            | false, _ -> Error [ InvalidSetting(CapacitySetting, raw) ]

    let load lookup =
        match readEventId lookup, readCapacity lookup with
        | Ok eventId, Ok capacity -> Ok { Event = Event.create eventId capacity }
        | Error eventErrors, Error capacityErrors -> Error(eventErrors @ capacityErrors)
        | Error errors, Ok _
        | Ok _, Error errors -> Error errors

    let event config = config.Event
```
`AppConfig.load` 接受查找函数，而不直接读取 `Environment`。生产代码传入环境变量查找函数；固定演示和测试传入确定性函数。这样既避免全局可变状态，也不需要配置框架。

加载器会累积 `BOOKING_EVENT_ID` 和 `BOOKING_CAPACITY` 的独立错误。如果两者都错了，运维人员可以在下次启动前一次修复。解析整数只是表示步骤；`Capacity.create` 执行容量必须为正的领域规则。私有 `AppConfig` 记录阻止后续代码直接构造未经验证的配置记录。

配置策略仍需决定：

- 明确定义来源优先级，而不是依赖偶然的调用顺序；
- 对每个请求都必需的缺失设置，让启动直接失败；
- 验证范围、格式和跨字段规则，而不只是能否解析；
- 永远不要在错误、日志、指标标签或追踪标签中打印机密值；
- 设置是启动时快照，还是可以重新加载；
- 测试已发布进程最终生效的配置。

更完整的 .NET 配置系统通过 `IConfiguration` 统一 JSON、环境变量、命令行参数、内存值和机密存储等提供程序。在需要提供程序分层和框架集成时使用它。它不能取代向领域特定的已验证类型转换。

## 把构造集中在一个组合根中 {#composition-root}

“组合根”（composition root）同样是架构术语：它是选择具体依赖并分配清理责任的最外层位置，不是一种 F# 语法。下面的模块位于 `Composition.fs` 末尾；它调用同一文件中先定义的 `BookingApplication` 构造函数：

```fsharp:line-numbers [Composition.fs]
module Composition =
    let start config ports writeLog =
        new BookingApplication(AppConfig.event config, ports, writeLog)
```
`Program` 处理只属于当前进程的工作。它选择查找函数，安装演示监听器，创建内存存储和应用，运行一条命令，最后把结果转换成输出和退出码。业务模块不负责这些选择。

手工构造本身就是依赖注入：依赖通过参数传入。DI 容器可以自动完成注册、解析、作用域和释放，但使用容器并不是实现控制反转的前提。即使日后由容器构造对象，清晰的组合根仍然有价值。

不要在领域或应用函数中通过全局服务定位器解析依赖。这样会隐藏签名中的需求、模糊生命周期，并迫使测试重建环境状态。函数参数能让依赖图一目了然。

## 围绕纯决策编排副作用 {#orchestration}

应用方法控制副作用的执行顺序，同时复用已有的业务工作流。下面的代码不是独立函数，而是 `BookingApplication` 的成员。

构造函数接收已验证的 `event`、`BookingPorts` 和 `writeLog`。类内部创建 `ActivitySource` 和计数器；`ensureActive` 检查对象尚未释放；`observe` 统一记录追踪状态、指标和结构化日志。因此，下面成员中使用的每个名称都有明确来源：

```fsharp:line-numbers [Composition.fs]
member _.Place(command: PlaceBookingCommand, cancellationToken: CancellationToken) =
    task {
        ensureActive ()

        let activity =
            activities.StartActivity(DiagnosticNames.PlaceActivityName, ActivityKind.Internal)

        try
            try
                cancellationToken.ThrowIfCancellationRequested()

                match validatePlaceBooking command with
                | Error errors ->
                    let failure = InvalidCommand errors
                    observe activity command "rejected" (sprintf "%A" failure)
                    return Error failure
                | Ok validCommand ->
                    let requestId = ValidPlaceBooking.requestId validCommand
                    let! state = ports.LoadBooking requestId cancellationToken

                    match decidePlaceBooking event state command with
                    | Error failure ->
                        observe activity command "rejected" (sprintf "%A" failure)
                        return Error failure
                    | Ok bookingEvent ->
                        do! ports.AppendEvent requestId bookingEvent cancellationToken
                        observe activity command "accepted" "event-appended"
                        return Ok bookingEvent
            with
            | :? OperationCanceledException as error ->
                observe activity command "canceled" "operation-canceled"
                return raise error
            | error ->
                observe activity command "faulted" (error.GetType().Name)
                return raise error
        finally
            match activity with
            | null -> ()
            | current -> current.Dispose()
    }
```
按顺序阅读该方法：

1. 拒绝释放后的使用，并启动一个可能为空的活动。
2. 在调用依赖前检查取消。
3. 验证原始命令，使存储收到类型化的 `RequestId`。
4. 用调用者的令牌加载当前状态。
5. 调用 `decidePlaceBooking` 完成领域决策。
6. 只追加被接受的领域事件，并使用同一个令牌。
7. 记录最终结果，并返回领域结果。
8. 观察取消或非预期故障，然后重新抛出。
9. 在 `finally` 中释放活动，覆盖每条退出路径。

独立的领域函数会再次验证命令。重复的只是一次廉价纯操作，两处都调用 `validatePlaceBooking`，没有复制规则。第一次验证用于在副作用前取得类型化键；公共工作流被单独调用时仍然安全。将来可以让 API 接受 `ValidPlaceBooking`，但前提是这能改善整体模型。

预期的业务拒绝仍然是 `Error BookingFailure`。取消仍然是 `OperationCanceledException`，因此 .NET 调用者和宿主能把它识别为取消。非预期的适配器故障仍然是故障任务。把三者都转成一个无法区分的 `Result` 会抹除运维含义。

样例不会重试。重试前必须知道故障是否暂时、追加是否幂等。没有幂等键时重试结果不明的写入，可能产生重复事件。先定义这些语义，再添加重试策略。

## 明确每项资源的生命周期 {#lifecycle}

每个可释放对象都需要一段代码负责清理。“它会被回收”不是生命周期规则：`Dispose` 往往释放句柄、套接字、缓冲区、订阅或遥测状态，而垃圾回收未必及时。

`Composition.start` 接收 `BookingPorts`。返回的 `BookingApplication` 负责清理 `ports.OwnedResource`。释放应用时，它会恰好一次释放该资源、`ActivitySource` 和 `Meter`。`Program` 使用 `use app = ...`，因此正常完成和异常都会触发清理。

这条生命周期规则清晰可见，但并非适用于所有场景：

| 构造规则 | 负责清理的组件 | 接收者的行为 |
|---|---|---|
| 应用为自身生命周期创建适配器 | 应用或外围组合根 | 等进行中的工作完成后释放 |
| 调用者传入共享适配器且仍负责清理 | 调用者 | 接收者不得释放 |
| DI 容器创建已注册的可释放服务 | 容器或作用域 | 使用者不得释放 |
| 工厂创建短生命周期资源 | 使用工厂结果的作用域 | 在该作用域使用 `use`/`use!` |

如果清理过程包含异步 I/O，应使用 `IAsyncDisposable`，并在合适的任务表达式中使用 `use!`。

关闭仍有请求运行的服务时，顺序很重要：先停止接收新工作，再发出取消信号，在限定时间内等待现有工作完成，最后释放依赖。本章的小进程只执行一条命令，因此不需要这套并发关闭流程。

释放不等于崩溃恢复。`SIGKILL`、断电或进程终止可以绕过清理。持久正确性必须来自存储事务、幂等性和恢复规则，而不是一个 `finally` 块。

## 加入最小而有用的可观测性 {#observability}

可观测性从问题开始，而不是从产品开始。样例提出三个不同问题，并使用三种不同信号：

| 信号 | 问题 | 样例字段 | 基数指导 |
|---|---|---|---|
| 结构化日志事件 | 一次具名尝试中发生了什么？ | 结果、请求 ID、座位数、细节 | 标识符可以是可搜索字段，但受隐私和保留策略约束 |
| 计数器测量 | 各种结果分别结束了多少次尝试？ | `booking.requests{outcome=accepted}` +1 | 保持标签值有界；绝不加入请求 ID |
| 追踪活动 | 该操作把时间花在哪里，又如何结束？ | 带结果和请求 ID 的 `booking.place` | 每次操作的上下文是合适的，但仍要遵守采样与敏感数据策略 |

这些信号彼此补充，而不是相互复制。指标以低成本提供趋势和告警，追踪关联路径中的工作，日志保留离散诊断事件。它们都无法修复错误的领域规则。

### 结构化日志保留字段 {#structured-logs}

`BookingLog` 是一条记录，演示会把字段序列化成一个 JSON 对象。与无法解析的自由文本相比，收集器可以分别保留 `eventName`、`outcome`、`requestId`、`seats` 和 `detail`。

但它仍然只是教学适配器。生产应用通常会用稳定的消息模板、事件 ID、级别、作用域、脱敏和已配置提供程序，把这个事件映射到 `ILogger`。提供程序决定日志去往何处；仅有控制台输出并不是持久存储。在日志系统能够过滤之前，不要插值机密或不受控负载。

样例把被接受或拒绝的领域决策分类为已完成操作，把抛出的适配器异常分类为 `faulted`。拒绝并不自动等于警告或追踪错误：“超出容量”可以是普通业务结果。严重程度应由运维处置方式决定，而不是由联合用例的名称决定。

### 指标发布测量；收集器负责聚合 {#metrics}

诊断名称是稳定常量，应用创建一个计数器：

```fsharp:line-numbers [Composition.fs]
module DiagnosticNames =
    [<Literal>]
    let MeterName = "ThinkingInFSharp.Ch32.Booking"

    [<Literal>]
    let ActivitySourceName = "ThinkingInFSharp.Ch32.Booking"

    [<Literal>]
    let RequestCounterName = "booking.requests"

    [<Literal>]
    let PlaceActivityName = "booking.place"
```
`Counter<int64>.Add` 发布一次增量。它本身不会创建历史存储、速率图、保留策略或告警。收集工具聚合测量，并可导出到后端。演示中的 `MeterListener` 只观察一次进程内测量，用于确认插桩已触发。

`outcome` 标签在本应用中有四个有界值：`accepted`、`rejected`、`canceled` 和 `faulted`。请求 ID 被刻意排除。指标系统通常会为每一种标签组合分配时间序列；无界 ID 会造成过多内存、存储和成本。

对只会增加的发生次数使用计数器。当持续时间或大小分布及其尾部很重要时，使用直方图。不要把延迟编码成一对平均值计数器，除非其局限性可以接受。定义单位和描述，让收集器不必猜测。

### 追踪围绕有意义的工作 {#traces}

没有感兴趣的监听器时，`ActivitySource.StartActivity` 可能返回 `null`。样例把这视为正常情况，不会解引用它。活动存在时，释放就会停止它；`finally` 确保成功、拒绝、取消和故障的每条路径都有一个完成的活动。

活动会记录含义明确的标签和状态。被接受和被拒绝的决策都按设计完成，因此状态是 `Ok`；非预期故障是 `Error`；取消保持独立。生产约定可以细化这些选择，但应在服务间保持一致。

创建 `ActivitySource` 只是提供插桩。OpenTelemetry 等收集器负责订阅、采样、扩充、批处理和导出活动。本地 `ActivityListener` 只能确认活动已经停止；跨进程传播、后端交付、保留和有效查询还需分别测试。

## 谨慎解读固定输出 {#fixed-evidence}

完成 Release 构建后，从仓库根目录运行确定性演示：

```console
dotnet run --project examples/chapters/ch32/Ch32.App.fsproj \
  --configuration Release --no-build -- --demo
```

它会输出：

```text
{"eventName":"booking.place","outcome":"accepted","requestId":"REQ-32","seats":2,"detail":"event-appended"}
result: accepted=true
metric: name=booking.requests value=1 outcome=accepted
trace: name=booking.place outcome=accepted
lifecycle: store-disposed=true
```

输出表明，一条固定命令依次经过配置、依赖连接、纯决策、内存追加、日志/指标/追踪记录和资源释放。

可执行脚本 `examples/chapters/ch32/application-contracts.fsx` 还验证四件事：独立配置错误会一起返回；同一个取消令牌会传到两个依赖；接受命令后只追加一次事件；命令预先取消时不会调用依赖。可从仓库根目录运行 `dotnet fsi --exec examples/chapters/ch32/application-contracts.fsx`。

这些结果只覆盖进程内装配。外部交付、持久存储和并发容量各自需要集成测试。`LoadBooking` 与 `AppendEvent` 是两项独立操作，因此两个调用者可能在任一方追加前读到同一状态。内存适配器只演示装配；生产存储必须原子地保证一致性。

## 测试应用职责，不重复测试领域规则 {#boundary-tests}

应用测试应观察应用层独有的职责：

- 多个独立错误的设置会产生所有相关启动错误；
- 无效命令不会产生存储副作用；
- 被接受的决策恰好追加一个事件；
- 被拒绝的决策不追加事件；
- 调用者的取消令牌到达每个依赖；
- 预先取消会避开依赖调用，并保持为取消；
- 适配器故障仍是故障，同时发出所选择的终结信号；
- 每次尝试恰好发出一个终结指标/日志结果；
- 当监听器采样时，每条路径上的活动都会停止；
- 负责清理的组件会恰好一次释放每项资源。

把领域规则样例和属性测试留在领域测试中。应用层测试可以使用确定性函数记录和跟踪型可释放对象，不必连接真实数据库或遥测厂商。再用独立的集成测试验证各生产适配器的协议、序列化和故障行为。

不要让每个测试都断言每种信号。用一个聚焦契约测试固定遥测结构；大多数编排测试只关注副作用和结果。否则，无害的措辞变化会在整个测试套件中制造噪声。

## 知道何时需要更强的宿主 {#stronger-host}

如果程序只是命令行工具、小型单用途进程或由其他宿主调用的库内工作器，而且全部依赖和生命周期一眼就能看清，那么显式创建依赖通常已经足够。

当进程同时需要多项标准设施时，.NET Generic Host 就会变得有用：

- 分层配置提供程序和环境约定；
- 日志提供程序、过滤和作用域；
- DI 注册、作用域和由容器负责的释放；
- 多个 `IHostedService` 或 `BackgroundService` 工作器；
- 协调的启动、关闭信号和优雅停止；
- 本就期望宿主服务的框架集成。

对于新的非 Web 宿主应用，当前 .NET 指南推荐 `Host.CreateApplicationBuilder`。Web 应用通常使用建立在相关宿主设施上的 `WebApplicationBuilder`。无论选择哪个，领域规则都不应进入服务或控制器。保留纯工作流、精简的依赖、类型化配置和单一组合根。

当容器管理真实对象图和作用域时，它很有价值。为了解析三个一目了然的值而加入容器，通常只会增加间接性，并未解决问题。反过来，手工构建数十个有作用域的服务和关闭回调，也可能是在拙劣地重造一个更弱的容器。让依赖数量、生命周期多样性、框架集成和运维需求来决定。

## 练习 {#exercises}

### 练习 1：推导依赖与生命周期 {#exercise-01}

纯函数 `decideDispatch : Inventory -> Order -> Result<Dispatch, DispatchError>` 已经可以在工作器中运行。为加载库存和提交发货推导最少的依赖。说明其类型、取消行为、预期错误，以及由谁释放数据库会话。不要引入容器。


::: details 参考答案

#### 从所需数据与操作开始 {#exercise-01-ports}

假设后台任务接收已验证的 `Order`，库存由已验证的 `Sku` 标识。最小端口定义如下：

```fsharp
open System.Threading
open System.Threading.Tasks

type DispatchPorts =
    { LoadInventory: Sku -> CancellationToken -> Task<VersionedInventory>
      CommitDispatch:
        InventoryVersion ->
            Dispatch -> CancellationToken -> Task<Result<unit, CommitError>> }
```

`VersionedInventory` 为领域 `Inventory` 增加一个存储版本。该版本不是发货规则；它让提交能够拒绝过期读取。没有它，分开的加载与提交调用无法防止两个工作器消耗同一份库存。

工作流如下：

```fsharp
task {
    cancellationToken.ThrowIfCancellationRequested()
    let sku = Order.sku order
    let! current = ports.LoadInventory sku cancellationToken

    match decideDispatch current.Inventory order with
    | Error domainError -> return Error(DomainRejected domainError)
    | Ok dispatch ->
        let! committed =
            ports.CommitDispatch current.Version dispatch cancellationToken

        return committed |> Result.map (fun () -> dispatch) |> Result.mapError CommitRejected
}
```

两个 I/O 调用都接收调用方的同一个取消令牌。若令牌已取消，第一个调用就不应开始。适配器发出的取消仍按取消处理；非预期数据库异常则让任务失败。`DispatchError` 表示预期业务拒绝。`VersionConflict` 等 `CommitError` 来自持久化或并发，不应伪装成领域规则。

该模型不会自动重试版本冲突。只有当操作有明确的重试上限、订单身份稳定且提交幂等时，调用者才能重新加载并再次决策。库存变化后继续复用先前的领域结果是不正确的。

长生命周期客户端和单次操作会话应分别指定释放责任：

- 组合根创建数据库客户端或连接池，随后由进程负责管理；
- 进程关闭时先停止新工作、等待未完成调用，再释放客户端；
- 适配器为一次操作创建会话或事务，并在操作内部用 `use` 或 `use!` 释放；
- 纯函数永远看不到这两种资源；
- 如果共享客户端仍由调用方负责释放，应用就不得释放它。

如果加载与提交必须共享同一个数据库事务，上面的两次调用端口就不够。适配器应创建一个事务，在其中加载、执行纯决策并按条件提交，最后释放事务。也可以使用存储提供的 compare-and-swap 操作。两个调用相邻并不代表它们具有原子性。

表达这些规则不需要容器。构造函数或函数参数声明依赖，`use` 标出局部资源的释放范围。容器日后可以自动管理长生命周期注册和作用域，无需改变领域工作流。

:::

### 练习 2：设计三种可观察信号 {#exercise-02}

为一次发货尝试定义结构化日志事件、指标和活动。选择名称、字段或标签以及最终结果；指出哪些值有界、哪些是高基数、哪些可能敏感。说明本地监听器能验证什么，哪些内容仍需收集器或导出器测试。


::: details 参考答案

#### 让每种信号只承担一种工作 {#exercise-02-signals}

一种连贯设计是：

| 信号 | 名称 | 字段/标签 | 最终结果 |
|---|---|---|---|
| 结构化日志 | `dispatch.attempt` | `outcome`、`orderId`、`sku`、`quantity`、`detail` | accepted、rejected、conflicted、canceled、faulted |
| 计数器 | `dispatch.attempts` | `outcome`，以及可选的有界 `channel` | 同一套有界结果词汇 |
| 活动 | `dispatch.place` | `dispatch.outcome`、`order.id`、`inventory.sku`；按策略设置状态和异常元数据 | 在每条被采样路径上停止 |

`outcome` 只有应用定义的五个值，因此基数有限。含义稳定时，`web`、`batch`、`manual` 等枚举渠道也可作为低基数字段。`orderId` 和通常的 `sku` 都是高基数值，不能成为指标标签。只有访问、保留、采样和隐私策略允许时，它们才能进入日志或追踪。

不要记录客户姓名、地址、自由文本备注、身份验证令牌、连接字符串和原始载荷。若其他系统能通过订单 ID 找到个人，订单 ID 同样敏感。策略要求时，应脱敏或使用不可逆的关联值。

结构化事件应保留带类型的字段，不要拼成一句文本。使用 `ILogger` 时，采用稳定的消息模板和事件 ID，让提供程序保留这些属性。日志级别取决于运维动作：正常缺货拒绝可以记为信息，非预期适配器异常则记为错误。

每次尝试结束时，计数器只递增一次。计数器报告发生次数，由收集器计算总量或速率。需要时长时，应增加注明单位的直方图，不要把平均值塞进计数器。告警属于采集或后端配置，不属于领域函数。

围绕应用编排启动活动，并在 `finally` 中释放它。把 `null` 活动视为正常情况。在活动上放置有界结果，并一致使用状态：预期拒绝可以在协议层成功完成，而非预期异常是错误。单独记录取消，不要把它改写成故障。

本地 `MeterListener` 可确认进程发布了名称、数值和标签都符合预期的测量。`ActivityListener` 可确认采样活动已启动、添加标签并停止。捕获日志回调则确认结构化记录已产生。

这些监听器无法验证聚合、采样策略、传播标头、批处理、导出、身份验证、后端摄取、保留、仪表板或告警。应在集成或预发布环境测试真实的 OpenTelemetry 或提供程序管线。再根据运维重要性增加后端查询或健康信号。

:::

### 练习 3：选择宿主层级 {#exercise-03}

分别为以下场景选择直接构造或 Generic Host：

- 导入一个文件后退出的命令；
- 运行三个后台消费者，并需要优雅关闭、分层配置和日志提供程序的进程；
- ASP.NET Core API。

说明每个选择的理由，并指出哪些架构边界应保持不变。


::: details 参考答案

#### 让生命周期需求选择工具 {#exercise-03-hosts}

导入单个文件的命令适合直接构造依赖。它只有一次有限操作、自然的 `use` 作用域、简单的参数与配置解析，以及一个退出码。增加服务容器和托管服务生命周期不会减少实质复杂度。若需中断，可使用控制台信号产生的取消令牌。

对于包含三个后台消费者的进程，应使用 Generic Host。它已经协调托管服务、日志提供程序、分层配置、DI 作用域、关闭信号和优雅停止。当前指南建议新建非 Web 宿主使用 `Host.CreateApplicationBuilder`。每个消费者都应遵守所提供的停止令牌、停止接收新工作，并服从有界排空策略。

对于 ASP.NET Core API，应使用 `WebApplicationBuilder` 和 ASP.NET Core 宿主。HTTP 服务器生命周期、请求作用域、配置、日志、中间件、端点激活和优雅关闭属于框架职责。把 `HttpContext.RequestAborted` 或端点取消令牌传过应用端口。

以下边界在三个场景中都保持不变：

- `decideDispatch` 保持纯粹，不感知宿主；
- 外部输入在边缘转换成经过验证的命令和配置；
- 存储、时钟、消息和遥测依赖仍由参数明确传入适配器或应用服务；
- 预期业务拒绝仍能与取消和故障区分；
- 一个组合根选择实现和生命周期；
- 文档写明各项资源由谁释放以及关闭顺序；
- 指标维度保持有界，敏感字段遵守策略；
- 适配器集成与并发保证接受独立测试。

宿主只改变外层资源的构造与管理方式，不应改变发货决策。若迁移框架后，领域模块反而需要解析服务或读取环境配置，就说明框架职责侵入了领域层。

:::


## 第五部分检查点 {#part-checkpoint}

在仓库根目录构建并运行自包含的组合示例：

```console
dotnet build examples/chapters/ch32/Ch32.App.fsproj --configuration Release
dotnet run --project examples/chapters/ch32/Ch32.App.fsproj --configuration Release --no-build -- --demo
```

确定性输出确认请求被接受，并产生一条结构化日志、一个指标、一个已结束活动，最后释放所拥有的存储。它验证可执行的进程内装配，不覆盖每条失败路径、生产遥测导出或持久交付。

[继续阅读第 33 章](../part-06/ch-33-domain-language-model)，把贯穿项目重建为一条连贯的应用路径。

## 来源 {#sources}

- [Microsoft Learn：.NET Generic Host 的职责与生命周期](https://learn.microsoft.com/en-us/dotnet/core/extensions/generic-host)
- [Microsoft Learn：配置提供程序与 `IConfiguration`](https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration)
- [Microsoft Learn：结构化日志与消息模板](https://learn.microsoft.com/en-us/dotnet/core/extensions/logging)
- [Microsoft Learn：`Meter`、插桩、标签与基数指导](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/metrics-instrumentation)
- [Microsoft Learn：指标收集、聚合与导出](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/metrics-collection)
- [Microsoft Learn：`ActivitySource`、可空活动、标签与收集](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/distributed-tracing-instrumentation-walkthroughs)
- [Microsoft Learn：DI 所有权与释放指南](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/guidelines)
