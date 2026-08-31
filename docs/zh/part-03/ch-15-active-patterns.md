---
title: "第 15 章：活动模式与领域视图"
description: "把完整、部分与参数化活动模式用作明确领域视图，同时让 I/O、昂贵工作和详细失败保持可见。"
translationKey: part-03/ch-15-active-patterns
---

# 第 15 章：活动模式与领域视图 {#overview}

普通模式按照值的类型定义进行匹配。活动模式则可以用函数按另一组名称分类或拆解已有值。因此，不改变预约状态的联合类型，也能把它分为 `Open` 或 `Closed`；文本也可以被识别为正数座位量，而无需让每个调用方都编写解析代码。

这种便利也有代价：活动模式是匹配期间执行的代码。若它打开文件、查询数据库、构造昂贵解析器、读取时钟或丢弃有用错误，匹配看似简单，行为却更难看清。好的活动模式只对已有值做廉价、稳定的检查。

## 优先使用普通模式 {#ordinary-patterns-first}

若可见可辨识联合已经准确表达消费者所需案例，就直接匹配。若只有一个调用位置需要某项计算，返回 `option` 或 `Result` 的具名函数通常更容易调用、测试与组合。当反复出现的另一种视图能让多处匹配使用领域语言阅读时，活动模式才值得引入这套语法。

它尤其适合：

- 声明表示应保持私有，但 F# 消费者需要可匹配视图；
- 同一类型存在多种合理分解方式；
- 廉价识别器能为反复出现的子集提供好名称；
- 参数可以在匹配位置特化一条简单识别规则。

它不会构造合法领域值。不变量验证与强制执行仍交给智能构造函数；活动模式用于查看值或识别子集。

## 活动模式就是识别器函数 {#recognizer-model}

活动模式用竖线和括号为识别函数的结果命名：

```fsharp
let (|CaseA|CaseB|) input =
    // 返回 CaseA payload 或 CaseB payload

let (|View|) input =
    // 返回总能成功视图的载荷

let (|Recognized|_|) input =
    // 返回 Some payload 或 None
```

这些名称出现在模式位置，但其定义会像函数一样执行。输入与返回数据都经过静态类型检查。编译器不会让识别器自动变得纯净、廉价或不抛异常。

三种主要形式只在一个问题上不同：

| 形式 | 定义语法 | 匹配行为 |
|---|---|---|
| 完整多案例 | `(|A|B|)` | 每个输入都匹配其中一个案例 |
| 完整单案例 | `(|View|)` | 每个输入都成功分解为返回数据 |
| 部分单案例 | `(|Case|_|)` | 某些输入匹配，其余输入继续尝试后续子句 |

## 完整活动模式划分全部输入 {#complete-active-patterns}

示例把三个声明状态归入一个双案例工作流视图：

```fsharp:line-numbers
let (|Open|Closed|) status =
    match status with
    | Pending -> Open "pending"
    | Confirmed code -> Open $"confirmed:{code}"
    | Cancelled reason -> Closed reason

let describeStatus status =
    match status with
    | Open detail -> $"open:{detail}"
    | Closed reason -> $"closed:{reason}"

printfn "Complete: pending=%s" (describeStatus Pending)
printfn "Complete: confirmed=%s" (describeStatus (Confirmed "C-42"))
printfn "Complete: cancelled=%s" (describeStatus (Cancelled "duplicate"))
```
`Open` 与 `Closed` 覆盖每个 `BookingStatus`。识别器必须为每个输入返回其中一个活动案例，同时包含两者的匹配具有穷尽性。每个案例都可以携带该视图所需的数据。

这不会向 `BookingStatus` 添加新状态。即使该工作流把 `Pending` 与 `Confirmed` 都归为打开，二者仍是不同领域状态。这种重新分类没有改写原模型，因而可以按不同用途提供不同视图。

一个完整活动模式最多可以声明七个案例。这是语言上限，不是设计目标：大量计算案例往往比直接联合或显式分类结果更难理解。

### 单一完整案例只负责分解 {#single-case-complete}

有时每个输入都具备一种有用投影：

```fsharp
let (|BookingSummary|) booking =
    Booking.requestId booking,
    Booking.seats booking,
    Booking.status booking

let render (BookingSummary(requestId, seats, status)) =
    // 使用投影出的值
    ()
```

`BookingSummary` 不会失败，因此是总能匹配的视图。完整单案例活动模式也能出现在函数形参与 `let` 模式中。当具名分解比返回元组的普通辅助函数传达更多含义时使用它；否则辅助函数更简单。

## 部分活动模式识别一个子集 {#partial-active-patterns}

名称列表中的通配符标志部分模式：

```fsharp
let (|Positive|_|) value =
    if value > 0 then Some value else None
```

`Some payload` 表示具名案例匹配，并绑定其中的数据。`None` 表示该模式没有匹配，因此匹配表达式会尝试后续子句。部分模式不必彼此互斥；由上到下的子句顺序会解决重叠。

示例把正整数文本识别为座位量：

```fsharp:line-numbers
let (|SeatCount|_|) raw =
    match parseSeatCount raw with
    | Ok value -> Some value
    | Error _ -> None

let describeRawSeatCount raw =
    match raw with
    | SeatCount value -> $"matched:{value}"
    | _ -> "not-matched"

printfn
    "Partial: three=%s zero=%s text=%s"
    (describeRawSeatCount "3")
    (describeRawSeatCount "0")
    (describeRawSeatCount "oops")
```
`"0"` 与 `"oops"` 都变成“不匹配”。只有当调用方只需要是/否分类时，这才合适。

### 不匹配比有类型的错误信息更少 {#non-match-versus-error}

底层 `parseSeatCount` 返回两种不同错误。把该 `Result` 转成 `Some`/`None` 会有意擦除原因。脚本另外打印明确错误，让这项损失可见。

以下情况使用部分活动模式：

- 失败只表示“尝试下一个模式”；
- 该决策位置不关心诊断；
- 识别器不需要验证并返回带丰富错误的受保护值。

若 UI、API、日志、重试策略或测试必须知道识别失败原因，就保留 `Result`。绝不能在部分模式内捕获任意异常再转成 `None`；那会把缺陷或基础设施失败伪装成“不匹配”。识别器抛出的其他异常会照常传播，`match` 不会压制它们。

## 给识别器传入参数 {#parameterized-active-patterns}

额外实参放在最终被匹配值之前：

```fsharp
let (|AtLeast|_|) minimum value =
    if value >= minimum then Some value else None

match seats with
| AtLeast 5 actual -> $"large:{actual}"
| AtLeast 2 actual -> $"group:{actual}"
| actual -> $"single:{actual}"
```

`minimum` 写在模式中，`value` 来自被匹配的输入。只有单案例活动模式——完整或部分——可以参数化；多案例活动模式不能接收这些额外实参。

示例用计数器记录识别器的调用次数：

```fsharp:line-numbers
let mutable thresholdChecks = 0

let (|AtLeast|_|) minimum value =
    thresholdChecks <- thresholdChecks + 1

    if value >= minimum then Some value else None

let classifyParty seats =
    match seats with
    | AtLeast 5 actual -> $"large:{actual}"
    | AtLeast 2 actual -> $"group:{actual}"
    | actual -> $"single:{actual}"

let classifyWithCount seats =
    thresholdChecks <- 0
    let label = classifyParty seats
    label, thresholdChecks

let largeLabel, largeChecks = classifyWithCount 6
let groupLabel, groupChecks = classifyWithCount 3
let singleLabel, singleChecks = classifyWithCount 1

printfn
    "Parameterized: six=%s/%d three=%s/%d one=%s/%d"
    largeLabel
    largeChecks
    groupLabel
    groupChecks
    singleLabel
    singleChecks
```
六个座位经过一次检查就满足第一条子句。三个座位先不满足 `AtLeast 5`，再满足 `AtLeast 2`，所以识别器运行两次。一个座位也会检查子句中的两个参数化模式，随后才进入后备分支。

计数器用于展示求值次数，并非推荐设计。每个模式位置都会执行代码；重构子句可能改变运行次数。正确性绝不能依赖隐藏的可变调用计数。

## 让匹配保持廉价、确定且局部 {#effect-boundary}

活动模式通常应检查已经在匹配的值。合适工作包括字段投影、算术分类、有界字符串检查，或适配私有表示。可疑工作包括：

- 数据库、HTTP、文件系统或其他 I/O；
- 读取当前时间、随机数、环境变量或可变全局状态；
- 无界遍历或重复枚举延迟来源；
- 每次尝试模式都编译正则表达式或建立大型索引；
- 吞掉异常或详细领域错误；
- 让后续子句含义发生变化的可变操作。

应先取得数据，再匹配所得值：

```fsharp
let decide loadBooking bookingId =
    match loadBooking bookingId with
    | Error loadError -> Error loadError
    | Ok booking ->
        match Booking.status booking with
        | Open detail -> Ok $"change:{detail}"
        | Closed reason -> Error $"closed:{reason}"
```

函数调用标明加载发生的位置，并保留其错误。内部活动模式现在只查看内存中的状态。后续章节会让加载步骤异步化，但仍不会把 I/O 移入模式。

若识别器需要预编译解析器或策略，应把准备好的廉价值作为参数传入，或由模块级定义通过闭包捕获。不要让紧凑语法隐藏准备成本。

## 公开活动模式属于公共 API {#public-contract}

活动模式可以让 F# 调用方匹配联合案例或字段仍为私有的类型。这允许内部表示演进，却不表示模式可以任意改变。案例名称和数量、输入类型、携带数据的类型，以及完整或部分行为，都会影响调用方源码。

应公开小型稳定视图并记录其语义。不要通过活动模式映射每个私有字段，否则只会把私有表示重新变成公共 API。第 17 章会把这类视图放进签名文件，并测试调用方真正能够观察什么。

## 先测量，再优化返回形式 {#return-forms}

普通部分形式返回 `option`。当模式不携带载荷时，F# 9 及更高版本也允许 `bool`：

```fsharp
let (|Even|_|) value =
    value % 2 = 0
```

若实测热点路径必须返回数据又要避免分配 `Some`，部分模式可以声明返回 `voption`：

```fsharp
[<return: Struct>]
let (|Integer|_|) (raw: string) =
    match System.Int32.TryParse raw with
    | true, value -> ValueSome value
    | false, _ -> ValueNone
```

返回属性必不可少；只把表达式改成 `ValueSome`/`ValueNone` 并不足够。应从 `option` 开始，测量真实分配问题，只优化对应热点。结构体返回不会让昂贵识别变廉价。

F# 还为可空引用提供 `Null`/`NonNull` 活动模式。第 19 章会结合完整 .NET 空值模型处理它们，这里不把空值问题混入领域识别。

## 一条小型选择规则 {#selection-rule}

| 需求 | 优先选择 |
|---|---|
| 匹配已声明的公开联合案例 | 直接模式匹配 |
| 计算一次并保留详细失败 | 返回 `Result` 的函数 |
| 构造值并强制不变量 | 智能构造函数 |
| 在匹配中复用另一种完整视图 | 完整活动模式 |
| 在匹配中复用是/否子集视图 | 部分活动模式 |
| 在每个匹配位置特化廉价单案例视图 | 参数化活动模式 |
| 加载或查询外部状态 | 有副作用的普通函数，再匹配其结果 |

可读模式语法是结果，而不是目标。若识别器名称隐藏的内容多于其案例揭示的内容，就回到普通函数。

## 练习 {#exercises}

### 练习 1：设计两个完整视图 {#exercise-01}

给定 `Pending`、`Confirmed of code` 与 `Cancelled of reason`，定义：

1. 完整 `Open | Closed` 活动模式，其中待处理与已确认都属于打开；
2. 为每个状态返回显示文本的单案例 `StatusLabel` 活动模式。

在匹配表达式中使用二者。解释为何两个模式都没有添加或移除领域状态。


::: details 参考答案

#### 完整工作流分区 {#exercise-01-complete}

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

#### 完整单案例投影 {#exercise-01-single}

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

:::

### 练习 2：保留有用失败 {#exercise-02}

部分 `SeatCount` 模式会把非数字文本与非正整数都转成不匹配。另写一个返回不同错误的 `parseSeatCount : string -> Result<int, SeatCountError>`。再分别指出一个适合部分模式的调用处，以及一个必须使用 `Result` 的调用处。

准确说明结果变成 option 时丢失了什么信息。


::: details 参考答案

#### 返回完整错误的解析器 {#exercise-02-parser}

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

#### 可选匹配视图 {#exercise-02-partial}

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

:::

### 练习 3：把 I/O 移出匹配 {#exercise-03}

评审以下识别器：

```fsharp
let (|ExistingBooking|_|) bookingId =
    repository.tryLoad bookingId
```

假设 `tryLoad` 会查询数据库并返回 `Booking option`。解释模式隐藏的成本与失败问题。重写工作流，直接调用加载函数，并只在取得预约后应用纯活动模式。说明参数化模式子句为何可能重复工作。


::: details 参考答案

#### 原代码隐藏了什么 {#exercise-03-problems}

`ExistingBooking booking` 语法看起来像廉价分解，却会执行数据库查询。这带来若干问题：

- 调用位置看不出延迟与资源使用；
- 两个出现位置可能为一个匹配输入执行两次查询；
- 若基础设施失败被错误压平，`None` 无法区分“不存在”和该失败；
- 抛出的数据库异常会在匹配期间出现，并绕过通配符后备；
- 没有仓库状态就难以测试识别器；
- 重排子句可能改变外部工作。

#### 先加载，再使用纯视图 {#exercise-03-rewrite}

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

#### 重复出现位置会增加工作 {#exercise-03-repetition}

以下形式包含两个识别器出现位置：

```fsharp
match bookingId with
| ExistingBooking booking when canChange booking -> "change"
| ExistingBooking _ -> "closed"
| _ -> "missing"
```

若第一次出现匹配但守卫为 false，匹配会继续，第二次出现就可能再次调用识别器。`AtLeast 5` 后接 `AtLeast 2` 也有相同问题：第一项失败时，两种特化就是两次调用。对于廉价纯比较，这没有问题；对于数据库查询，则属于正确性与延迟问题。

:::


第 16 章转向程序结构：模块、命名空间、文件顺序、项目和编译器设置会决定代码能引用哪些定义。

## 资料来源 {#sources}

- [Microsoft Learn：活动模式](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/active-patterns)
- [Microsoft Learn：模式匹配](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/pattern-matching)
- [Microsoft Learn：F# 组件设计指南](https://learn.microsoft.com/en-us/dotnet/fsharp/style-guide/component-design-guidelines)
- [FSharp.Core：ValueOption 模块](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-valueoption.html)
