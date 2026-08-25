---
title: "第 13 章练习答案"
description: "在管道与组合之间转换调用、排列代表性 F# API，并简化装饰性管道。"
translationKey: solutions/ch-13-composition-pipeline-api
---

# 第 13 章练习答案 {#overview}

把每种改写展开回普通应用来验证。由左到右的视觉流动很有用，但类型对齐与真实应用顺序才是证明。

[返回第 13 章](../part-03/ch-13-composition-pipeline-api)。

## 练习 1：推导两个组合 {#exercise-01}

立即执行的管道是：

```fsharp
let result =
    text
    |> parse
    |> normalize
    |> label
```

两个可复用函数是：

```fsharp
let forward = parse >> normalize >> label
let backward = label << normalize << parse
```

二者的类型都是 `string -> string`。在两者中，`parse` 都最先运行，然后是 `normalize`，最后是 `label`。`>>` 由左到右列出执行顺序；`<<` 则从最终操作反向列到输入。

展开任一应用都能证明嵌套关系：

```fsharp
// forward text 展开为：
label (normalize (parse text))

// backward text 展开为相同表达式：
label (normalize (parse text))
```

在可执行代码中，应使用 `forward text = backward text` 比较结果值。

## 练习 2：排列面向 F# 的 API {#exercise-02}

若一个状态要复用于很多集合，就把状态放前、预约集合放最后：

```fsharp
let filterByStatus status bookings =
    bookings
    |> List.filter (fun booking -> Booking.status booking = status)

let pendingOnly = filterByStatus Pending
let pending = allBookings |> pendingOnly
```

若一个格式器要复用于很多集合，就采用与 `List.map` 相同的选择器优先惯例：

```fsharp
let renderMany formatter bookings =
    bookings |> List.map formatter

let renderForConsole = renderMany renderBookingForConsole
let labels = allBookings |> renderForConsole
```

对于容量与请求座位，两种顺序都可能合理。若一个活动容量会被复用，容量优先支持部分应用：

```fsharp
let fitsWithin capacity requested =
    SeatCount.value requested <= Capacity.value capacity

let fitsEvent = fitsWithin eventCapacity
let accepted = requestedSeats |> fitsEvent
```

对于一次检查，`fitsWithin eventCapacity requestedSeats` 作为二值关系读起来更直接。尽管二者都包含带度量整数，受保护类型会让反序成为编译期错误。这项安全性比最后一次调用是否包含 `|>` 更重要。

## 练习 3：移除装饰性管道 {#exercise-03}

题目给出的代码使用表示层谓词 `fitsWithin : int<seat> -> int<seat> -> bool`：两个受保护值都已经解包。这与练习 2 设计的受保护类型 API 有意不同。

直接版本命名两个数量，让最终命题保持直接：

```fsharp
let canAccept capacity request =
    let availableSeats = Capacity.value capacity
    let requestedSeats = request |> Booking.seats |> SeatCount.value
    fitsWithin availableSeats requestedSeats
```

管道导向版本仍可以保留重要中间名称：

```fsharp
let canAcceptPiped capacity request =
    let requestedSeats =
        request
        |> Booking.seats
        |> SeatCount.value

    requestedSeats
    |> fitsWithin (Capacity.value capacity)
```

这里我会选择第一版。提取过程很短，两个数量并列出现在最终关系旁，调试器还能检查每个命名值。第二版正确，也可能适合外围管道，但最后一次管道没有增加变换阶段，只是旋转了一个二元谓词。

如果 `fitsWithin` 改为直接接收受保护的 `Capacity` 与 `SeatCount`，最佳实现还能更短：

```fsharp
let canAccept capacity request =
    fitsWithin capacity (Booking.seats request)
```

把带度量解包留在领域谓词内，也会减少调用位置对表示的重复了解。

## 应该注意什么 {#what-to-notice}

- **组合方向改变拼写，不改变执行：** 把两个运算符都展开为嵌套调用。
- **可复用固定实参应放在柯里化 F# 函数前面：** 部分应用随后等待流动数据。
- **选择器优先 API 与 FSharp.Core 一致：** 熟悉顺序会减少适配 lambda。
- **二元关系通常适合直接阅读：** 管道可选，并非风格要求。
- **领域类型防止同原始类型的反序：** 单靠参数顺序无法提供这项安全性。
