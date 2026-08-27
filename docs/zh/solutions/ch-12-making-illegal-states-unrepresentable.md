---
title: "第 12 章练习答案"
description: "保护有界值、选择外层记录边界，并修正把容量与可用量混在一起的跨文件 API。"
translationKey: solutions/ch-12-making-illegal-states-unrepresentable
---

# 第 12 章练习答案 {#overview}

受保护表示是否可靠，取决于所有公开构造入口。应检查每个返回受保护类型的函数，而不只是名为 `create` 的函数。

[返回第 12 章](../part-02/ch-12-making-illegal-states-unrepresentable)。

## 练习 1：保护百分比 {#exercise-01}

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

## 练习 2：选择透明或私有外层记录 {#exercise-02}

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

## 练习 3：公开跨文件容量 API {#exercise-03}

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

## 应该注意什么 {#what-to-notice}

- **闭区间需要检查两个边界：** 智能构造函数只声明一次。
- **受保护组成部分可能已经让外层记录足够安全：** 不透明性必须对应仍存在的需求。
- **不会失败的构造器不应返回装饰性 `Result`：** 返回类型应匹配真实替代情况。
- **容量与可用量具有不同不变量：** 相似数值表示不等于同一个领域类型。
- **`.fsi` 中的抽象类型向后续文件隐藏全部案例：** 实现仍是唯一可信构造作用域。
