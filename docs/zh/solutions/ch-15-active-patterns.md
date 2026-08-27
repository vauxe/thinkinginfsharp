---
title: "第 15 章练习答案"
description: "建立完整领域视图、保留解析错误，并把数据库工作移出活动模式匹配。"
translationKey: solutions/ch-15-active-patterns
---

# 第 15 章练习答案 {#overview}

活动模式应让匹配的领域视图更清楚，而不会让求值行为更不透明。当成本或失败不确定时，把模式展开回识别器函数。

[返回第 15 章](../part-03/ch-15-active-patterns)。

## 练习 1：设计两个完整视图 {#exercise-01}

### 完整工作流分区 {#exercise-01-complete}

```fsharp
type BookingStatus =
    | Pending
    | Confirmed of confirmationCode: string
    | Cancelled of reason: string

let (|Open|Closed|) status =
    match status with
    | Pending -> Open "pending"
    | Confirmed code -> Open $"confirmed:{code}"
    | Cancelled reason -> Closed reason

let canChange status =
    match status with
    | Open _ -> true
    | Closed _ -> false
```

两个活动案例覆盖每个声明状态。载荷还保留观察到哪一种打开情况，但只需要 `canChange` 的调用方可以忽略它。

### 完整单案例投影 {#exercise-01-single}

```fsharp
let (|StatusLabel|) status =
    match status with
    | Pending -> "pending"
    | Confirmed code -> $"confirmed:{code}"
    | Cancelled reason -> $"cancelled:{reason}"

let renderStatus (StatusLabel label) = label
```

`StatusLabel` 总会返回字符串，因此这个单 case 视图不会失败。若调用位置不需要模式匹配语法，直接使用返回 `string` 的 `statusLabel` 函数同样有效，而且可能更简单。

两个识别器都不会改变领域的可能值。它们都消费已有 `BookingStatus` 并计算视图。构造、合法转换与不变量仍属于原类型及其模块。

## 练习 2：保留有用失败 {#exercise-02}

### 返回完整错误的解析器 {#exercise-02-parser}

```fsharp
open System

type SeatCountError =
    | NotAnInteger of raw: string
    | NotPositive of actual: int

let parseSeatCount (raw: string) =
    match Int32.TryParse raw with
    | true, value when value > 0 -> Ok value
    | true, value -> Error(NotPositive value)
    | false, _ -> Error(NotAnInteger raw)
```

若真实领域使用受保护 `SeatCount`，成功分支应调用其智能构造函数并返回该类型，而不是裸 `int`。本练习保留 `int`，只是为了单独讨论失败信息的表示。

### 可选匹配视图 {#exercise-02-partial}

```fsharp
let (|SeatCount|_|) raw =
    match parseSeatCount raw with
    | Ok seats -> Some seats
    | Error _ -> None
```

这个模式适合多格式识别器，其中“不是座位数 token”只表示“尝试下一种 token 格式”。它不适合必须告诉调用方如何修正输入的路径，例如 HTTP 请求、表单提交或命令验证。这些位置应调用 `parseSeatCount` 并保留 `Error`。

转换会同时丢失错误案例及其载荷：

- `"oops"` 丢失 `NotAnInteger "oops"`；
- `"0"` 丢失 `NotPositive 0`；
- `"-3"` 丢失 `NotPositive -3`。

三者都变成 `None`。成功值仍被保留，但不重新解析就无法恢复原因与违规细节。

## 练习 3：把 I/O 移出匹配 {#exercise-03}

### 原代码隐藏了什么 {#exercise-03-problems}

`ExistingBooking booking` 语法看起来像廉价分解，却会执行数据库查询。这带来若干问题：

- 调用位置看不出延迟与资源使用；
- 两个出现位置可能为一个匹配输入执行两次查询；
- 若基础设施失败被错误压平，`None` 无法区分“不存在”和该失败；
- 抛出的数据库异常会在匹配期间出现，并绕过通配符后备；
- 没有仓库状态就难以测试识别器；
- 重排子句可能改变外部工作。

### 先加载，再使用纯视图 {#exercise-03-rewrite}

优先采用能区分基础设施失败与不存在的仓库契约：

```fsharp
type BookingLookupError =
    | StorageFailure of message: string

type BookingDecisionError =
    | NotFound
    | LookupFailed of BookingLookupError
    | BookingClosed of reason: string

let decide tryLoad bookingId =
    match tryLoad bookingId with
    | Error lookupError -> Error(LookupFailed lookupError)
    | Ok None -> Error NotFound
    | Ok(Some booking) ->
        match Booking.status booking with
        | Open detail -> Ok $"change:{detail}"
        | Closed reason -> Error(BookingClosed reason)
```

这里 `tryLoad` 的概念类型类似：

```fsharp
BookingId -> Result<Booking option, BookingLookupError>
```

这里先直接调用仓储取得数据，再把已加载状态交给纯 `Open | Closed` 视图。查找失败、不存在与已关闭领域状态仍是不同结果，加载与领域匹配也在结构上分开。

若现有仓库确实只返回 `Booking option`，至少应在匹配前把结果绑定一次，让查询既不隐藏也不重复；随后再改进边界，以建模基础设施错误。

### 重复出现位置会增加工作 {#exercise-03-repetition}

以下形式包含两个识别器出现位置：

```fsharp
match bookingId with
| ExistingBooking booking when canChange booking -> "change"
| ExistingBooking _ -> "closed"
| _ -> "missing"
```

若第一次出现匹配但守卫为 false，匹配会继续，第二次出现就可能再次调用识别器。`AtLeast 5` 后接 `AtLeast 2` 也有相同问题：第一项失败时，两种特化就是两次调用。对于廉价纯比较，这没有问题；对于数据库查询，则属于正确性与延迟问题。

## 应该注意什么 {#what-to-notice}

- **视图不会创建状态：** 完整活动案例只会重新划分或投影已有值。
- **Option 是有意的信息缩减：** 只有当不匹配就是全部所需结果时才使用它。
- **副作用先于匹配：** 只加载一次，保留加载失败，再分类已取得值。
- **出现位置就是工作：** 紧凑模式语法不会缓存识别器。
- **函数仍是一等选择：** 只有活动模式能让反复出现的匹配更清楚时，才使用它。
