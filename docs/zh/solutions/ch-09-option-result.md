---
title: "第 9 章练习答案"
description: "围绕 option、Result、组合、短路和结构化错误上下文进行推理。"
translationKey: solutions/ch-09-option-result
---

# 第 9 章练习答案 {#overview}

好的答案会精确保留调用方必须区分的替代情况。如果更短的类型抹掉了有用的失败原因，它就不更好；如果更丰富的类型多出的案例不携带信息，它同样不更好。

[返回第 9 章](../part-02/ch-09-option-result)。

## 练习 1：选择返回类型 {#exercise-01}

1. **用有效标识查找：** `Booking option`。无匹配是普通答案，题设还说明标识验证已经完成。如果存储访问本身可能失败，那是另一个维度，类型可以变成 `Result<Booking option, StorageError>`。
2. **解析座位数：** `Result<int, SeatCountError>`。文本可能格式不对、超出 `int` 范围，或数值不被业务接受。带类型的错误让调用方能够解释或响应这些区别。只有当所有失败都被有意视为“没有解析出来”时，`int option` 才可能足够。
3. **计算姓名首字母：** `string`。题设承诺姓名已经验证为非空。公开缺失或失败会迫使每位调用方处理契约声称不会发生的案例。若实际上无法信任前提，就应修复输入类型或在边界验证。
4. **查询服务：** `Result<Booking option, ServiceError>`。`Error` 表示查询未成功完成；`Ok None` 表示查询完成但没有找到值；`Ok (Some booking)` 表示查询完成且找到了值。压平任一层都会合并不同事实。

类型应追随含义，而不是实现上的方便。

## 练习 2：组合可选数据 {#exercise-02}

直接定义如下：

```fsharp
let tryFindConfirmedCode bookingId =
    bookingId
    |> tryFindBooking
    |> Option.bind tryConfirmedCode
```

`tryConfirmedCode` 已经返回 `string option`。`Option.map tryConfirmedCode` 会再次包装这个返回的 option，产生 `string option option`。`Option.bind` 把函数应用于 `Some booking`，直接返回函数产生的 option；遇到 `None` 则不调用函数并原样保留。

显式模式匹配具有同样行为：

```fsharp
let tryFindConfirmedCodeExplicit bookingId =
    match tryFindBooking bookingId with
    | Some booking -> tryConfirmedCode booking
    | None -> None
```

第一版并非更加正确，只是更紧凑地表达了相同的案例分析。

## 练习 3：保留验证上下文 {#exercise-03}

联合案例组成封闭集合，因此应修改原定义，而不是试图在其他位置扩展它：

```fsharp
type BookingError =
    | EmptyAttendee
    | NonPositiveSeats of actual: int
    | TooManySeats of requested: int * maximum: int
    | EventClosed

type ValidationFailure =
    { RequestId: string
      EventId: string
      Cause: BookingError }

let validateOpen isOpen request =
    if isOpen then Ok request else Error EventClosed

let validateBooking maximum isOpen request =
    request
    |> validateAttendee
    |> Result.bind (validateSeats maximum)
    |> Result.bind (validateOpen isOpen)

let addContext requestId eventId result =
    result
    |> Result.mapError (fun cause ->
        { RequestId = requestId
          EventId = eventId
          Cause = cause })

let checkRequest request =
    request
    |> validateBooking 4 false
    |> addContext "R-9" "E-2"
```

按这个顺序，把参与者为空且活动关闭的请求交给 `checkRequest` 会产生 `EmptyAttendee`；遇到第一个 `Error` 后，`Result.bind` 不会运行后面的座位或开放状态检查。通过前两项检查但活动已关闭的请求会产生 `EventClosed`。随后，`addContext` 包装最终保留下来的领域错误，而不会改变 `Ok` 值。

如果界面必须报告全部三个相互独立的违规项，这条管道就使用了错误的组合规则。必须运行每项验证并有意累积其错误；调整 `bind` 调用顺序无法产生累积效果。

## 应该注意什么 {#what-to-notice}

- **缺失与操作失败相互独立：** 嵌套可能是最诚实的表示。
- **`map` 与 `bind` 的区别来自后续返回类型：** 普通值与已包装值。
- **验证顺序就是策略：** 第一个错误短路会让较早的检查成为可观察行为。
- **结构化上下文仍然是数据：** 请求与活动标识无需解析消息就能被记录或翻译。
- **类型不应公开不可能的分支：** 有保证的计算返回普通值。
