---
title: "第 34 章答案"
description: "追踪预订错误优先级，将独立验证扩展到三个字段，并比较取消优先级策略。"
translationKey: solutions/ch-34-pure-booking-workflow
---

# 第 34 章答案 {#overview}

这些答案遵循当前工作流可观察到的顺序，而不是收集所有想象得到的问题。在独立字段阶段，验证会完整进行；随后，状态与生命周期决策会在某个前置条件失败时立即停止。

[返回第 34 章](../part-06/ch-34-pure-booking-workflow)。

## 练习 1：追踪明确的错误优先级 {#exercise-01}

### 只沿真正能运行的分支前进 {#exercise-01-traces}

| 输入 | 具体结果 | 未执行的规则 |
|---|---|---|
| 空白 ID、零座位，状态为 `NotBooked` | `InvalidCommand [InvalidRequestId BlankRequestId; InvalidSeatCount (NonPositiveSeatCount 0)]` | 状态占用与容量 |
| 有效的五个座位，容量为四，状态为 `NotBooked` | `BookingCreationFailed (RequestedSeatsExceedCapacity (5<seat>, 4<seat>))` | 创建被拒绝之后的全部步骤 |
| 同一有效命令，状态为 `Booked existing` | `BookingAlreadyExists (Booking.requestId existing)` | 不调用 `Booking.create`，所以不会重新检查容量 |
| 空白 ID、空白确认码，状态为 `NotBooked` | `InvalidCommand [InvalidRequestId BlankRequestId; InvalidConfirmationCode BlankConfirmationCode]` | 预订查找与状态转换 |
| 有效确认命令，当前已是 `Confirmed currentCode` | `BookingTransitionFailed (CannotConfirmFrom (Confirmed currentCode))` | 事件包装与演进 |

案例 (a) 即使第一个字段失败，也会运行两个纯字段验证器。它从不检查 `NotBooked`；把状态改为 `Booked existing` 仍会得到同一个验证列表。

案例 (b) 会进入创建，因为两个字段都有效且状态为空。容量由 `Booking.create` 检查，工作流只包装它返回的错误，不再重复比较整数。

案例 (c) 展示业务短路。新请求的五个座位本身有效，但状态已被占用，因此根本不存在可供诊断的创建尝试。若同时报告重复和容量，就等于假装那次被拒绝的创建已经运行。

案例 (d) 确立了这个决策器会先验证彼此独立的生命周期字段，再进行查找。这是一项公开的优先级选择，不是普适的安全策略。练习 3 会考察另一种方案。

案例 (e) 先验证新命令，再找到匹配的预订，然后调用 `Booking.confirm`。错误携带当前预订已有的确认码，而不是新提出的确认码，因为它描述的是拒绝转换的现有状态。

## 练习 2：加入第三个独立字段 {#exercise-02}

### 每次给构造函数扩展一个参数 {#exercise-02-validation}

下面的自包含扩展沿用相同模式。它使用不同的类型名，避免让人误以为综合项目已经定义电子邮件策略：

```fsharp
open System
open Booking.Domain

type EmailAddressError = BlankEmailAddress
type EmailAddress = private EmailAddress of string

module EmailAddress =
    let create raw =
        if String.IsNullOrWhiteSpace raw then
            Error BlankEmailAddress
        else
            Ok(EmailAddress(raw.Trim()))

type PlaceBookingWithEmail =
    { RequestId: string
      AttendeeEmail: string
      Seats: int }

type PlaceWithEmailError =
    | InvalidRequestId of RequestIdError
    | InvalidEmailAddress of EmailAddressError
    | InvalidSeatCount of SeatCountError

type ValidPlaceBookingWithEmail =
    private
        { RequestId: RequestId
          AttendeeEmail: EmailAddress
          Seats: SeatCount }

let applyValidation valueResult functionResult =
    match functionResult, valueResult with
    | Ok mapping, Ok value -> Ok(mapping value)
    | Error earlier, Error later -> Error(earlier @ later)
    | Error errors, Ok _
    | Ok _, Error errors -> Error errors

let createValid requestId attendeeEmail seats : ValidPlaceBookingWithEmail =
    { RequestId = requestId
      AttendeeEmail = attendeeEmail
      Seats = seats }

let validate (command: PlaceBookingWithEmail) =
    let requestId =
        RequestId.create command.RequestId
        |> Result.mapError (fun error -> [ InvalidRequestId error ])

    let email =
        EmailAddress.create command.AttendeeEmail
        |> Result.mapError (fun error -> [ InvalidEmailAddress error ])

    let seats =
        SeatCount.create command.Seats
        |> Result.mapError (fun error -> [ InvalidSeatCount error ])

    Ok createValid
    |> applyValidation requestId
    |> applyValidation email
    |> applyValidation seats
```

当 ID 和电子邮件为空白、座位数为零时，错误顺序是请求 ID、电子邮件、座位数。调整管道中的验证顺序就会改变结果，因此要明确选择顺序并用测试固定。

三个验证器都只依赖各自的原始字段。活动剩余容量则依赖已验证的活动数据，也可能依赖活动中的全部预约。容量检查应放在后续有状态决策中，并在那里规定失败即停与原子提交要求。

示例只检查空白电子邮件，因为这就是已声明的规则。生产环境中的 `EmailAddress` 策略需要明确需求，再加入语法、规范化、国际化或可投递性检查。不要悄悄把任意正则表达式塞进智能构造函数。

## 练习 3：规定取消优先级 {#exercise-03}

### 当前策略 {#exercise-03-current}

假设状态包含请求 `REQ-7`，其状态是 `Cancelled oldReason`：

1. ID 与原因都为空白时，返回两个字段错误：`BlankRequestId` 在前，`BlankCancellationReason` 在后。不检查状态。
2. ID 合法但不匹配、原因合法时，返回 `BookingDoesNotExist`。由于目标不匹配，不检查已取消状态。
3. ID 匹配且新原因合法时，外层错误为 `BookingTransitionFailed`。内层是 `CannotCancelFrom (Cancelled oldReason)`；新原因不会替换已有终态。

这个顺序会在领域查找之前向调用者提供完整字段反馈。它简单、确定，并与下单和确认的验证顺序一致。它可能暴露不存在目标的验证细节，而某些公开边界倾向于避免这一点。

### 一种可辩护的替代方案 {#exercise-03-alternative}

注重隐私的 API 可以只验证请求 ID，执行授权和查找，然后仅对已授权且存在的目标验证原因。有效但不存在的 ID 总是返回同一个不可区分的未找到结果，不论原因是否空白。这可以减少账户或资源探测，也避免对被隐藏的目标继续花费验证工作。

这是应用边界策略，不能悄悄改动纯函数。它需要单独的认证后查找阶段、文档化的错误契约，以及端点测试来确认“目标不存在”和“未授权”无法区分。决策器输入也要调整，例如改为已授权的合法预约与原因命令。

它也会放弃同时返回 ID 与原因错误。只有安全或隐私比即时字段反馈更重要时，这个取舍才合理。通常可以保留内部决策器，再把内部错误转换为更粗粒度的外部错误。这样既满足两项需求，也不用重复状态转换规则。

无论选择哪种策略，测试都应写清错误优先级。“所有错误都会处理”既没有说明哪个错误优先，也没有说明实际执行了哪些检查。

## 答案回顾 {#solution-review}

- 只追踪前置条件成功产出值的规则。
- 字段累积与业务短路处在不同阶段。
- 容量规则来自 `Booking.create`；重复状态会阻止创建运行。
- 转换错误描述拒绝命令的当前状态。
- 加入字段就是扩展已验证构造函数与一个独立管道步骤。
- 返回错误列表时，顺序属于 API 行为。
- 活动级可用性是有状态规则，不是电子邮件或整数字段检查。
- 电子邮件策略应来自需求，而非顺手写出的正则表达式。
- 当前策略在状态查找前验证所有命令字段。
- 公开安全边界可以有意隐藏查找与验证细节。
- 改变优先级需要新的类型或编排、文档和测试。
- 任何优先级策略都不能替代原子的加载—决策—提交边界。
