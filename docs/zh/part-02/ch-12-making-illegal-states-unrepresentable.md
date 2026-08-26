---
title: "第 12 章：让非法状态无法表示"
description: "用私有表示、伴生模块、智能构造函数和明确的文件级 API 边界保护领域不变量。"
translationKey: part-02/ch-12-making-illegal-states-unrepresentable
---

# 第 12 章：让非法状态无法表示 {#overview}

一个名为 `validateCapacity` 的函数保护不了普通 `int`。任何调用方都能跳过它、存入 `0`，再把这个值交给假定容量为正的代码。某处虽然做过验证，结果却没有携带证明。

F# 可以把这项约定变成 API 边界：公开 `Capacity` 类型、隐藏其表示，并让唯一公开构造路径返回 `Result<Capacity, CapacityError>`。调用方取得 `Capacity` 后，下游代码可以依赖构造函数建立的不变量，而无需反复检查同一个整数。

这里的“无法”指：在已说明的边界假设下，无法通过受支持的公开 API 产生。它不表示损坏的存储、恶意反射、不安全原语、null 互操作或并发竞争已经消失。

## 学完本章后你能做什么 {#outcomes}

学完本章后，你应该能够：

- 区分类型缩写与受保护领域类型；
- 公开类型，同时隐藏其联合或记录表示；
- 把智能构造函数与访问器放在同名伴生模块中；
- 为预期的无效输入返回带类型的拒绝原因；
- 把受保护组成类型组合为更大的有效模型；
- 解释 `private`、`internal` 与 `public` 的作用域；
- 用 `.fsi` 签名跨文件隐藏表示；
- 在每项可能产生新值的变换中保持不变量；
- 判断隐藏表示何时物有所值、何时是过度设计。

## 没有受保护结果的验证可以被绕过 {#bypassable-validation}

下面的类型缩写不会建立新的运行时或编译期区别：

```fsharp
type Capacity = int<seat>

let validateCapacity capacity =
    if capacity > 0<seat> then Ok capacity
    else Error "capacity must be positive"
```

即使一条路径调用 `validateCapacity`，另一条路径仍能写出 `let capacity: Capacity = 0<seat>`。类型缩写只是同一类型的另一个名称，并不控制构造。

当调用方可以直接填写字段时，公开记录也有同一弱点。返回未改变公开表示的验证在输入边界仍然有用，却不能让后续代码区分已经验证与尚未验证的数据。

修复需要两部分：

1. 一个调用方无法构造其表示的独立类型；
2. 一个先检查原始输入、再返回该类型的函数。

只有一半并不完整。带公开无检查构造函数的私有包装仍允许无效值；返回原始 `int` 的验证器仍不携带证明。

## 公开类型并隐藏构造器 {#private-representation}

共享脚本在显式模块中定义领域：

```fsharp:line-numbers [ch12-making-illegal-states-unrepresentable.fsx]
type CapacityError = NonPositiveCapacity of actual: int

type Capacity = private Capacity of int<seat>

module Capacity =
    let create raw =
        if raw > 0 then
            raw |> LanguagePrimitives.Int32WithMeasure<seat> |> Capacity |> Ok
        else
            Error(NonPositiveCapacity raw)

    let value (Capacity capacity) = capacity
```
注意修饰符的位置：

```fsharp
type Capacity = private Capacity of int<seat>
```

类型 `Capacity` 可见，其联合表示则是外围 `BookingDomain` 模块的私有内容。相比之下，`type private Capacity = ...` 会把类型本身隐藏起来，使它无法出现在公开签名中。

外层 `Capacity` 名称表示类型；内层案例还会构造或模式匹配其表示。访问边界外的代码可以传递、存储一个 `Capacity`，也能调用相关公开函数，却无法调用该案例。

下面这个仅用于诊断的绕过方式已经用 F# 10 验证：

```fsharp
let invalid = BookingDomain.Capacity 0<BookingDomain.seat>
// FS1093：无法从此代码位置访问 Capacity 的联合案例或字段。
```

F# 中，单个联合案例的可访问性不会低于联合表示本身。隐藏表示会一起隐藏全部构造/解构案例。私有记录表示同样会向消费者隐藏直接记录构造与字段模式访问。

## 伴生模块负责构造与观察 {#companion-module}

F# 允许类型与模块同名，从而形成聚焦的 API：

```fsharp
Capacity.create : int -> Result<Capacity, CapacityError>
Capacity.value : Capacity -> int<seat>
```

模块位于同一个外围 `BookingDomain` 模块中，因此可以构造和模式匹配私有案例。调用方使用限定名称，不必知道表示。

`create` 是一个**智能构造函数**。它接受适合边界的原始数据、检查正数、附上 `seat` 度量，再返回受保护值或带类型的预期错误。它不会为普通拒绝抛出异常。

`value` 是有意提供的观察函数。返回带度量整数，让适配器可以显示或持久化它，却不会让适配器不经 `create` 就把任意整数重新变成 `Capacity`。

能直接构造受保护值的代码应保持很少。外围模块内每个可以直接调用 `Capacity` 案例的函数，都负责维护不变量。`private` 会阻止外部调用方，却不能证明内部代码正确。

## 智能构造可以同时验证与规范化 {#validation-and-normalization}

另两个受保护组成部分展示了两项策略：

```fsharp:line-numbers [ch12-making-illegal-states-unrepresentable.fsx]
type EventIdError = | BlankEventId

type EventId = private EventId of string

module EventId =
    let create raw =
        if String.IsNullOrWhiteSpace raw then
            Error BlankEventId
        else
            raw.Trim() |> EventId |> Ok

    let value (EventId eventId) = eventId

type SeatCountError = NonPositiveSeatCount of actual: int

type SeatCount = private SeatCount of int<seat>

module SeatCount =
    let create raw =
        if raw > 0 then
            raw |> LanguagePrimitives.Int32WithMeasure<seat> |> SeatCount |> Ok
        else
            Error(NonPositiveSeatCount raw)

    let value (SeatCount seats) = seats
```
`EventId.create` 拒绝空白输入，并去掉两端空白。`SeatCount.create` 拒绝非正数量，并恢复编译期度量。构造成功后：

- `EventId` 非空白，并按选定的 trim 规则规范化；
- `SeatCount` 为正，并以座位作为量纲。

规范化是领域策略，并非无害清理。这里适合去除两端空白；若外部标识区分大小写，静默改变大小写就可能错误。应明确说明每项规范化规则，并与拒绝行为一起测试。

错误类型保留被拒绝的事实：`NonPositiveSeatCount actual` 比 `Error "invalid"` 更有用。格式化与本地化仍留在构造器外。

不要只为方便而公开绕过检查的入口。若可信迁移代码确实需要，就应保持私有或严格限定为 internal，并明确测试该边界。

## 用受保护的值构造更大的有效状态 {#composing-invariants}

请求模型组合两个受保护的值，并且还隐藏自己的记录表示：

```fsharp:line-numbers [ch12-making-illegal-states-unrepresentable.fsx]
type BookingRequestError =
    | InvalidEventId of EventIdError
    | InvalidSeatCount of SeatCountError

type BookingRequest =
    private
        { EventId: EventId
          Seats: SeatCount }

module BookingRequest =
    let create rawEventId rawSeats =
        rawEventId
        |> EventId.create
        |> Result.mapError InvalidEventId
        |> Result.bind (fun eventId ->
            rawSeats
            |> SeatCount.create
            |> Result.mapError InvalidSeatCount
            |> Result.map (fun seats -> { EventId = eventId; Seats = seats }))

    let eventId request = request.EventId |> EventId.value

    let seats request = request.Seats |> SeatCount.value
```
`BookingRequest.create` 先构造 `EventId`，再构造 `SeatCount`，把每项组成错误映射进请求上下文。只有两者都成功后，它才会构造私有记录。通过这个 API 得到的值不可能包含空白标识或非正座位数。

正如第 9 章所述，这条 result 管道保留第一个错误。如果界面必须累积相互独立的错误，应在以后采用累积验证器；改变表示本身不会决定错误组合策略。

私有请求记录是一项设计选择，不是普遍要求。只包含已经受保护的 `EventId` 和 `SeatCount` 字段的公开记录仍能保持这两项组成不变量，还能让调用方方便地模式匹配。当外层存在跨字段规则、必须控制构造或表示很可能演进时，才隐藏外层记录；当透明数据组合正是预期 API 时，应保持公开。

## 每个生产者都必须保持不变量 {#invariant-preservation}

构造函数不是唯一能产生值的函数。更新、算术、解析、数据库读取与反序列化也都是构造路径。

对于不可变的受保护值，变换可以：

- 原样返回现有值；
- 计算原始候选数据，再调用智能构造函数；
- 证明变换会保持不变量，并在可信模块内部直接构造。

例如，从容量中减去已预约座位可能得到零。零究竟表示“售罄但容量有效”“剩余座位而非容量”，还是无效值，属于建模决策。不要只因两者底层都是 `int<seat>`，就复用具有不同不变量的 `Capacity`。

避免让访问器暴露可变内部对象。这里的包装只含不可变字符串与数字。若受保护类型包含数组或可变 .NET 对象，直接返回它就会让调用方在证明背后修改状态；应返回副本、只读视图，或只提供保持不变量的操作。

## `private`、`internal` 与签名保护不同边界 {#access-boundaries}

F# 访问控制同时具有词法与程序集含义：

| 机制 | 可见范围 | 合适用途 |
| --- | --- | --- |
| `private` | 外围类型或模块 | 向同级模块与后续文件隐藏表示 |
| `internal` | 同一程序集内任意代码 | 程序集实现细节；在该程序集内部并非强不变量屏障 |
| `public` 或省略时的默认值 | 外围 API 所允许的全部调用方 | 供预期消费者使用的 API |
| `.fsi` 签名 | 实现文件外只能看到签名公开的声明 | 稳定的跨文件/组件抽象 |

当没有显式顶层命名空间/模块改变组织方式时，每个 F# 文件都会隐式成为模块。顶层模块只能包含在一个文件中。因此，私有表示及其伴生模块可以共享一个文件级模块，另一个文件却不能重新打开该模块来访问私有案例。

在共享脚本中，类型与伴生模块都位于 `BookingDomain` 内；该模块之后的代码即使仍在同一个 `.fsx` 文件中，也无法访问其中的 `private` 表示。访问范围由外围模块决定，而不只是文件名。

### 签名文件明确跨文件契约 {#signature-file}

对于稳定的库 API，`BookingDomain.fsi` 可以公开抽象类型：

```fsharp
namespace Booking.Domain

[<Measure>]
type seat

type CapacityError =
    | NonPositiveCapacity of actual: int

type Capacity

module Capacity =
    val create: raw: int -> Result<Capacity, CapacityError>
    val value: capacity: Capacity -> int<seat>
```

对应的 `BookingDomain.fs` 包含私有联合表示和实现。在项目中，`.fsi` 必须排在匹配的 `.fs` 文件之前。后续文件能看到 `type Capacity` 与已声明函数，却看不到联合案例。签名中省略的项目对实现文件之外是私有的。

签名文件会增加维护成本，因为公开变更必须在两个文件中一致。API 已稳定或跨组件隐藏表示确实重要时，它很有价值；不必机械地给每个探索性文件添加。第 16 章会在多文件设计中回到项目顺序与签名。

## 在外部边界准确说明保证 {#boundary-limits}

私有表示保护普通的已编译调用方。来自 JSON、数据库、环境变量或其他服务的数据会重新成为原始数据，必须再次通过验证。度量单位会被擦除，所以持久化也无法携带其证明。

基于反射的序列化器、不安全代码、`Unchecked.defaultof`、旧式 null 或损坏的持久化字节都可能绕过正常构造假设。应让适配器序列化明确的 DTO，并通过智能构造函数重建领域值。第 19 章处理 null 边界，后续完整项目章节处理持久化与并发。

一个有效 `Capacity` 也不能阻止两个并发请求超卖。该类型保护局部值不变量，而不是原子存储转换。保证必须说明自己的范围。

## 在能消除真实风险时使用该模式 {#avoiding-overdesign}

以下情况中，隐藏表示值得付出成本：

- 稳定不变量很容易被原始类型或公开记录违反；
- 值会跨越多层，或有很多生产者；
- 已经出现重复防御检查；
- 无效数据会导致昂贵或安全相关行为；
- 表示演进不应破坏消费者。

若值只在局部短暂存在、组成部分已经强制全部规则，或包装仍公开无检查构造因而没有证明任何事，它很可能是过度设计。当所有案例都合法且调用方能从穷尽匹配中获益时，公开可辨识联合通常更好。

从最小且有效的保护边界开始。如果非空标识在各处都重要，就保护 `EventId`；不要只为让类型列表更长而包装每个显示标签。

## 运行共享示例 {#run-example}

在示例所在目录执行：

```console
dotnet fsi --exec ch12-making-illegal-states-unrepresentable.fsx
```

五行输出覆盖接受容量、拒绝容量、标识规范化、有效请求构造及两条请求拒绝路径：

```fsharp:line-numbers [ch12-making-illegal-states-unrepresentable.fsx]
let describeCapacityError error =
    match error with
    | NonPositiveCapacity actual -> $"capacity must be positive: {actual}"

match Capacity.create 40 with
| Ok capacity -> printfn "Capacity: accepted=%d" (Capacity.value capacity)
| Error error -> printfn "Capacity: %s" (describeCapacityError error)

match Capacity.create 0 with
| Ok _ -> printfn "Capacity rejection: unexpected success"
| Error error -> printfn "Capacity rejection: %s" (describeCapacityError error)

let describeRequestError error =
    match error with
    | InvalidEventId BlankEventId -> "event id is blank"
    | InvalidSeatCount(NonPositiveSeatCount actual) -> $"seat count must be positive: {actual}"

match BookingRequest.create "  EVT-42  " 3 with
| Ok request -> printfn "Request: event=%s seats=%d" (BookingRequest.eventId request) (BookingRequest.seats request)
| Error error -> printfn "Request: %s" (describeRequestError error)

match BookingRequest.create "   " 3 with
| Ok _ -> printfn "Request rejection: unexpected event success"
| Error error -> printfn "Request rejection: %s" (describeRequestError error)

match BookingRequest.create "EVT-42" 0 with
| Ok _ -> printfn "Request rejection: unexpected seat success"
| Error error -> printfn "Request rejection: %s" (describeRequestError error)
```
## 练习 {#exercises}

### 练习 1：保护百分比 {#exercise-01}

把 `type FillRate = decimal` 替换为私有表示，其有效值从 `0m` 到 `1m`（含两端）。定义携带被拒绝值的错误类型、`FillRate.create` 与 `FillRate.value`。解释类型缩写加验证器为何不够。

### 练习 2：选择透明或私有外层记录 {#exercise-02}

假设 `EventId` 与 `SeatCount` 已受保护。比较两种设计：

```fsharp
type BookingRequest = { EventId: EventId; Seats: SeatCount }
type BookingRequest = private { EventId: EventId; Seats: SeatCount }
```

分别给出一条倾向该设计的需求。若选择私有设计，列出调用方所需的最小构造与观察函数。

### 练习 3：公开跨文件容量 API {#exercise-03}

为 `Capacity` 加上 `tryReserve : SeatCount -> Capacity -> Result<Capacity, ReservationError>` 操作，编写 `.fsi` 签名的公开部分。说明文件顺序、联合案例可在哪些位置使用，以及恰好订满活动时该操作怎样保持正容量不变量。

[查看本章练习答案](../solutions/ch-12-making-illegal-states-unrepresentable)。

## 模型复盘 {#model-review}

- 独立私有表示加检查式构造函数会携带原始验证器无法提供的证明。
- 同名模块集中构造与观察，同时在一个可信作用域内保留表示访问权。
- 组成不变量可以组合，但外层跨字段规则仍可能要求私有构造。
- `private`、`internal` 与 `.fsi` 签名保护不同的词法或程序集边界。
- 每个生产者与外部适配器都必须保持或重新建立不变量。
- “无法表示”相对于受支持 API 而言；它不解决损坏、null 互操作或并发。
- 保护那些在较大范围内都重要的不变量；如果公开构造和穷尽匹配本来就是设计目标，透明类型仍然更好。

## 第二部分检查点 {#part-checkpoint}

编译预约领域并运行它的聚焦公开 API 测试：

```console
dotnet test ExampleTests.fsproj --configuration Release --filter FullyQualifiedName~BookingDomainTests
```

测试通过表明：受支持的构造函数会拒绝无效标识、容量、座位数与状态转换，有效值仍可通过公开 API 使用。它们不证明外部适配器也保持这些不变量；后续边界章节会单独验证。

[继续阅读第 13 章](../part-03/ch-13-composition-pipeline-api)，开始组合这些带类型的操作。

## 资料来源 {#sources}

- [Microsoft Learn：访问控制](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/access-control)
- [Microsoft Learn：签名文件](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/signature-files)
- [Microsoft Learn：可辨识联合](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/discriminated-unions)
- [Microsoft Learn：模块](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/modules)
- [Microsoft Learn：F# 组件设计指南](https://learn.microsoft.com/en-us/dotnet/fsharp/style-guide/component-design-guidelines)
