---
title: "第 12 章：让非法状态无法表示"
description: "用私有表示、伴生模块、智能构造函数和明确的文件级 API 边界保护领域不变量。"
translationKey: part-02/ch-12-making-illegal-states-unrepresentable
---

# 第 12 章：让非法状态无法表示 {#overview}

一个名为 `validateCapacity` 的函数保护不了普通 `int`。任何调用方都能跳过它、存入 `0`，再把这个值交给假定容量为正的代码。验证后的结果与未检查整数没有区别。

F# 可以通过 API 强制这条规则：公开 `Capacity` 类型，隐藏其表示，并让唯一的公共构造函数返回 `Result<Capacity, CapacityError>`。调用方取得 `Capacity` 后，下游代码就能依赖其不变量，无需反复检查同一个整数。

这里的“无法”是指：在已说明的假设下，无法通过受支持的公共 API 产生。损坏的存储、恶意反射、不安全原语、null 互操作和并发竞争仍然存在。

## 没有受保护结果的验证可以被绕过 {#bypassable-validation}

下面的类型缩写不会建立新的运行时或编译期区别：

```fsharp
type Capacity = int<seat>

let validateCapacity capacity =
    if capacity > 0<seat> then Ok capacity
    else Error "capacity must be positive"
```

即使一条路径调用 `validateCapacity`，另一条路径仍能写出 `let capacity: Capacity = 0<seat>`。类型缩写只是同一类型的另一个名称，并不控制构造。

当调用方可以直接填写字段时，公开记录也有同一弱点。接收输入时，返回原表示的验证仍有用，却不能让后续代码区分已验证与未检查的数据。

修复需要两部分：

1. 一个调用方无法构造其表示的独立类型；
2. 一个先检查原始输入、再返回该类型的函数。

只做其中一项并不完整。带公共无检查构造函数的私有包装仍允许无效值；返回原始 `int` 的验证器也不能给后续代码增加保证。

## 公开类型并隐藏构造器 {#private-representation}

示例在显式模块中定义领域：

```fsharp:line-numbers
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

外层 `Capacity` 名称表示类型；内层案例负责构造或匹配其表示。`BookingDomain` 外的代码可以传递和存储 `Capacity`，也能调用相关公共函数，却无法调用该案例。

下面这个仅用于诊断的绕过方式已经用 F# 10 验证：

```fsharp
let invalid = BookingDomain.Capacity 0<BookingDomain.seat>
// FS1093：无法从此代码位置访问 Capacity 的联合案例或字段。
```

F# 中，单个联合案例的可访问性不会低于联合表示本身。隐藏表示会一起隐藏全部构造/解构案例。私有记录表示同样会向消费者隐藏直接记录构造与字段模式访问。

## 伴生模块集中构造与读取操作 {#companion-module}

F# 允许类型与模块同名，从而形成聚焦的 API：

```fsharp
Capacity.create : int -> Result<Capacity, CapacityError>
Capacity.value : Capacity -> int<seat>
```

模块位于同一个外围 `BookingDomain` 模块中，因此可以构造和模式匹配私有案例。调用方使用限定名称，不必知道表示。

`create` 是一个**智能构造函数**。它接收原始数据、检查正数、附上 `seat` 度量，再返回受保护值或有类型的预期错误。它不会为正常拒绝抛出异常。

`value` 是有意提供的观察函数。返回带度量整数，让适配器可以显示或持久化它，却不会让适配器不经 `create` 就把任意整数重新变成 `Capacity`。

能直接构造受保护值的代码应保持很少。外围模块内每个可以直接调用 `Capacity` 案例的函数，都负责维护不变量。`private` 会阻止外部调用方，却不能证明内部代码正确。

## 智能构造可以同时验证与规范化 {#validation-and-normalization}

另两个受保护组成部分展示了两项策略：

```fsharp:line-numbers
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

不要只为方便而公开绕过检查的入口。若可信迁移代码确实需要，应保持私有或严格限定为 `internal`，并直接测试这条例外路径。

## 把受保护值组合成更大的有效状态 {#composing-invariants}

请求模型组合两个受保护的值，并且还隐藏自己的记录表示：

```fsharp:line-numbers
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

## `private`、`internal` 与签名保护不同范围 {#access-boundaries}

F# 访问控制同时具有词法与程序集含义：

| 机制 | 可见范围 | 合适用途 |
| --- | --- | --- |
| `private` | 外围类型或模块 | 向同级模块与后续文件隐藏表示 |
| `internal` | 同一程序集内任意代码 | 程序集实现细节；在该程序集内部并非强不变量屏障 |
| `public` 或省略时的默认值 | 外围 API 所允许的全部调用方 | 供预期消费者使用的 API |
| `.fsi` 签名 | 实现文件外只能看到签名公开的声明 | 稳定的跨文件/组件抽象 |

当没有显式顶层命名空间/模块改变组织方式时，每个 F# 文件都会隐式成为模块。顶层模块只能包含在一个文件中。因此，私有表示及其伴生模块可以共享一个文件级模块，另一个文件却不能重新打开该模块来访问私有案例。

若类型与伴生模块都放在显式 `BookingDomain` 模块内，该模块之后的代码即使仍在同一个 `.fsx` 文件中，也无法访问其中的 `private` 表示。访问范围由外围模块决定，而不只是文件名。

### 签名文件定义跨文件 API {#signature-file}

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

## 外部数据进入后必须重新验证 {#boundary-limits}

私有表示约束普通的已编译调用方。来自 JSON、数据库、环境变量或其他服务的数据会重新成为原始数据，必须再次验证。度量单位在运行时被擦除，持久化数字也不会保留度量信息。

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

从能消除真实风险的最小类型开始。如果非空标识在各处都重要，就保护 `EventId`；不要只为增加类型数量而包装每个显示标签。

## 练习 {#exercises}

### 练习 1：保护百分比 {#exercise-01}

把 `type FillRate = decimal` 替换为私有表示，其有效值从 `0m` 到 `1m`（含两端）。定义携带被拒绝值的错误类型、`FillRate.create` 与 `FillRate.value`。解释类型缩写加验证器为何不够。


::: details 参考答案

私有单分支联合会把已验证比例与任意 decimal 区分开：

```fsharp
type FillRateError =
    | OutsideUnitInterval of actual: decimal

type FillRate = private FillRate of decimal

module FillRate =
    let create raw =
        if raw >= 0m && raw <= 1m then
            Ok(FillRate raw)
        else
            Error(OutsideUnitInterval raw)

    let value (FillRate rate) = rate
```

两个端点都会被接受，因为题设区间是闭区间。`create -0.1m` 与 `create 1.01m` 会在 `Error` 中保留被拒绝值；预期输入拒绝不需要异常。

使用 `type FillRate = decimal` 时，调用方无需验证就能把 `2m` 标注为 `FillRate`。改用私有联合后，模块外只能通过公开函数构造 `FillRate`。访问器仍可返回 decimal 供计算或序列化，但不会绕过构造规则。

若算术产生新的填充率，它必须重新调用 `create`；也可以留在可信模块内部，但要能说明结果始终不越界。有效比例乘以 `2m` 后，不一定仍然有效。

:::

### 练习 2：选择透明或私有外层记录 {#exercise-02}

假设 `EventId` 与 `SeatCount` 已受保护。比较两种设计：

```fsharp
type BookingRequest = { EventId: EventId; Seats: SeatCount }
type BookingRequest = private { EventId: EventId; Seats: SeatCount }
```

分别给出一条倾向该设计的需求。若选择私有设计，列出调用方所需的最小构造与观察函数。


::: details 参考答案

若完整规则只是“包含一个有效 `EventId` 与一个有效 `SeatCount`”，应倾向公开记录。两个字段已经分别通过验证，且所有组合都合法。调用方还能继续使用记录构造、复制更新和模式匹配。

若存在跨字段规则、必须保持同步的派生字段、需要整体执行的规范化，或很可能改变表示且不应破坏消费者，就应倾向私有记录。例如，小组预约策略可能要求 `SeatCount` 超过阈值时必须提供联系人地址。

若没有额外验证会失败，最小私有 API 是：

```fsharp
BookingRequest.create : EventId -> SeatCount -> BookingRequest
BookingRequest.eventId : BookingRequest -> EventId
BookingRequest.seats : BookingRequest -> SeatCount
```

`create` 直接返回 `BookingRequest` 是准确的，因为两个实参已经受保护，而且没有新的拒绝规则。若构造会检查跨字段规则，应改为 `Result<BookingRequest, BookingRequestError>`。若调用方普遍需要某项变换，应公开该操作，不要泄露记录并迫使各处重写策略。

private 并不会自动更安全：缺少观察操作的不透明类型会迫使调用方采取笨拙变通；构造器不执行任何检查的不透明类型则只是增加仪式。

:::

### 练习 3：公开跨文件容量 API {#exercise-03}

为 `Capacity` 加上 `tryReserve : SeatCount -> Capacity -> Result<Capacity, ReservationError>` 操作，编写 `.fsi` 签名的公开部分。说明文件顺序、联合案例可在哪些位置使用，以及恰好订满活动时该操作怎样保持正容量不变量。


::: details 参考答案

题设签名暴露了一处建模错误：

```fsharp
tryReserve : SeatCount -> Capacity -> Result<Capacity, ReservationError>
```

本章的 `Capacity` 是正数且固定的活动容量。预约不会改变这个事实。若返回值实际表示剩余座位，恰好订满会产生零，违反该类型的正数不变量。把它称为 `Capacity` 已经合并了两个概念。

应单独建模可用量，并允许零：

```fsharp
namespace Booking.Domain

[<Measure>]
type seat

type CapacityError =
    | NonPositiveCapacity of actual: int

type Capacity

module Capacity =
    val create: raw: int -> Result<Capacity, CapacityError>
    val value: Capacity -> int<seat>

type SeatCountError =
    | NonPositiveSeatCount of actual: int

type SeatCount

module SeatCount =
    val create: raw: int -> Result<SeatCount, SeatCountError>
    val value: SeatCount -> int<seat>

type AvailableSeats

type ReservationError =
    | InsufficientSeats of requested: int<seat> * available: int<seat>

module AvailableSeats =
    val fromCapacity: Capacity -> AvailableSeats
    val value: AvailableSeats -> int<seat>
    val tryReserve:
        requested: SeatCount ->
        available: AvailableSeats ->
        Result<AvailableSeats, ReservationError>
```

`AvailableSeats` 的不变量是“零或正数”，`Capacity` 则保持正数且不变。恰好订满会返回有效的零 `AvailableSeats`；请求超过可用量则返回 `InsufficientSeats`。另一种有效设计是使用 `SoldOut | SeatsRemain of PositiveSeatCount` 联合，让零由具名案例表示。

把这组公开 API 放进 `Capacity.fsi`，项目顺序中紧接 `Capacity.fs`，再放调用方文件。`.fs` 实现可以匹配并构造隐藏的联合 case；后续文件只能看到抽象类型与列出的值。签名中省略的辅助函数仍是实现细节。

若保留原签名，实现就只能拒绝恰好订满、撒谎把零作为正数 `Capacity` 返回，或返回未改变的容量而不表示预约状态。类型评审已经正确揭示三种选择都错误。

:::


## 第二部分检查点 {#part-checkpoint}

用上述构造函数测试有效请求、空活动 ID、非正容量和非正座位数。有效请求必须成功构造，每个无效值都必须在自己的边界被拒绝。状态转换与外部适配器会在后文加入。

[继续阅读第 13 章](../part-03/ch-13-composition-pipeline-api)，开始组合这些带类型的操作。

## 资料来源 {#sources}

- [Microsoft Learn：访问控制](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/access-control)
- [Microsoft Learn：签名文件](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/signature-files)
- [Microsoft Learn：可辨识联合](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/discriminated-unions)
- [Microsoft Learn：模块](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/modules)
- [Microsoft Learn：F# 组件设计指南](https://learn.microsoft.com/en-us/dotnet/fsharp/style-guide/component-design-guidelines)
