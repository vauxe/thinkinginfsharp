---
title: "第 13 章：组合、参数顺序与管道 API"
description: "从嵌套调用推导管道与函数组合，再围绕真实调用形式设计参数顺序，而不强迫每个表达式都进入管道。"
translationKey: part-03/ch-13-composition-pipeline-api
---

# 第 13 章：组合、参数顺序与管道 API {#overview}

小函数的输出类型与下一个函数的输入类型相容时，才能连接起来。连接可以写成嵌套调用，也可以让值通过管道，或组合出新的可复用函数。三种写法表达的是同一组函数应用。

这些调用的写法也会影响 API 设计。主要数据参数放在最后时，柯里化函数很容易部分应用并接入管道。但“永远使用管道”和“数据必须永远放最后”都不是规则。某些谓词、构造器、对称运算与 .NET 方法直接调用更清楚。

## 从嵌套调用中找出数据路径 {#repeated-nesting}

示例从普通函数调用开始：

```fsharp:line-numbers
let nestedLabel = toLabel (addChannel "web" (capSeats 4 (trimAttendee rawDraft)))

printfn "Nested: %s" nestedLabel
```
由最内层括号向外阅读：

1. 去除参与者两端空白；
2. 把请求座位上限设为四；
3. 添加 `web` 渠道；
4. 产生标签。

代码正确，阅读方向却反复逆转：源码以 `toLabel` 开始，执行却从 `trimAttendee` 开始。更多嵌套括号也让暂停并检查中间值更困难。

不要只因一次调用被嵌套就引入管道。只有当每项完整结果都会成为下一项操作的主要输入时，才形成值得改写的处理链。

## 管道现在就应用一个值 {#pipeline}

前向管道的基本行为是：

```fsharp
value |> functionValue
// 等同于
functionValue value
```

改写共享链会得到：

```fsharp:line-numbers
let pipedLabel =
    rawDraft |> trimAttendee |> capSeats 4 |> addChannel "web" |> toLabel

printfn "Pipeline matches nested: %b" (pipedLabel = nestedLabel)
```
现在源码顺序跟随数据流。每一行都变换上一行产生的值，最终结果会立即计算出来。

阶段之间的类型必须相容。`trimAttendee` 返回 `BookingDraft`，正好可交给接收 `BookingDraft` 的 `toLabel`。一个需要其他输入的函数，不会只因加上 `|>` 就能插入。

管道只是普通应用，不是副作用系统，也不会自动传播错误。把 `Result` 传给 `Result.bind next` 之所以可行，是因为该函数最后接收 result；`|>` 本身并不理解 `Ok` 或 `Error`。

### 用多行清楚展示各阶段 {#pipeline-formatting}

表达式稍长时，就像示例一样从值开始，每行写一个 `|>` 阶段。lambda 变长或需要在调试器中单独检查时，应给该步骤命名。管道可读是因为变换清楚，而不是因为字符最少。

## 组合创建稍后使用的函数 {#composition}

前向组合连接两个函数，却尚不提供输入：

```fsharp
(>>) : ('A -> 'B) -> ('B -> 'C) -> ('A -> 'C)

let composed = first >> second
let result = composed input
// 等同于：second (first input)
```

示例组合全部四个阶段：

```fsharp:line-numbers
let prepareLabel = trimAttendee >> capSeats 4 >> addChannel "web" >> toLabel

let prepareLabelBackward = toLabel << addChannel "web" << capSeats 4 << trimAttendee

printfn "Forward composition: %s" (prepareLabel rawDraft)
printfn "Backward composition: %s" (prepareLabelBackward rawDraft)
```
`prepareLabel` 是函数值，可以保存、传递、测试，并应用于很多草稿。相比之下，前面的管道会立即从 `rawDraft` 计算一个标签。

后向组合会反转函数的书写顺序：

```fsharp
second << first
// 同样表示：先运行 first，再运行 second
```

因此，`toLabel << addChannel "web" << capSeats 4 << trimAttendee` 从右向左执行，与嵌套调用顺序一致。从最终操作出发思考时，这种写法可能很自然；按数据流阅读时，前向 `>>` 通常更清楚。选择能让执行顺序一目了然的方向。

省略输入参数的组合有时称为“无参数风格”（point-free）。只有当所得函数仍容易识别时，这种省略才有用。当中间领域名称、标注、日志或断点很重要时，带命名参数和管道的 `let prepare draft = ...` 更好。

## 参数顺序让部分应用有用 {#parameter-order}

观察共享签名：

```fsharp
capSeats : int -> BookingDraft -> BookingDraft
addChannel : string -> BookingDraft -> BookingDraft
```

配置在前，主要流动值在后。固定座位上限或渠道后，两项函数的剩余类型都是 `BookingDraft -> BookingDraft`。因此都能直接加入管道或组合：

```fsharp:line-numbers
let deskLabel =
    { Attendee = "  Mira "
      RequestedSeats = 2
      Channel = None }
    |> trimAttendee
    |> capSeats 4
    |> addChannel "desk"
    |> toLabel

printfn "Configured pipeline: %s" deskLabel
```
面向 F# 的函数常按以下顺序排列：

1. 会在多次调用中保持固定的依赖或策略值；
2. 变换函数或选择器；
3. 被变换的集合或领域值。

FSharp.Core 中有三类常见例子：

- `List.map mapping list`；
- `List.filter predicate list`；
- `Option.defaultValue fallback option`。

先提供前面的实参，就会得到一个等待数据的函数。

若草稿放在前面，函数会写成 `capSeatsDataFirst draft maximum`。接入管道时便需要 `draft |> fun value -> capSeatsDataFirst value 4`。一个 lambda 没有问题；每个调用处都重复适配 lambda，才说明参数顺序不便于 F# 调用。

### 参数顺序不是普遍定律 {#parameter-order-limits}

已有惯例与含义应优先于管道便利：

- `max left right` 等对称操作数没有优先流动值；
- 构造器通常以直接列出必需实参最自然；
- 谓词可能比较两个语义权重相同的领域值；
- .NET 方法为了跨语言使用，通常采用带括号的元组式参数；
- 改变成熟公开函数的顺序属于破坏性 API 变更。

若两个形参共享一种原始类型，反序仍可能编译。`EventId` 与 `RequestId` 等受保护领域类型比巧妙管道更能降低这种风险。

## 直接调用可能最清楚 {#direct-call}

最后一个示例让小谓词保持直接调用：

```fsharp:line-numbers
let fitsWithin capacity requested = requested <= capacity

let requested = 3
let capacity = 4
let fits = fitsWithin capacity requested

printfn "Direct predicate: requested=%d capacity=%d fits=%b" requested capacity fits
```
`fitsWithin capacity requested` 把关系展示在一处。`requested |> fitsWithin capacity` 同样有效，却没有揭示更长的变换路径，还可能让简单比较显得像过程。

以下情况优先直接调用：

- 只有一项操作，而非一条链；
- 多个实参具有相同语义权重；
- 函数名与直接实参组成清楚命题；
- 管道会要求打包/拆包元组或加入本不需要的 lambda；
- 熟悉的 .NET 方法已经有清楚调用惯例。

类似地，`<|` 可以移除括号——`printfn "%s" <| prepareLabel draft`——但普通括号通常更为熟悉。运算符知识应减少噪声，而不是考验读者。

## 从代表性调用方式设计 API {#api-design-workflow}

在发布一个 F# 函数前，写出三个代表性调用：

1. 提供所有实参的一次直接调用；
2. 在多个值上复用的一次部分应用；
3. 预期工作流中的一次管道或组合。

若签名让常见调用简洁、少见调用仍然可行，其顺序通常合理。若每次调用都需要翻转、元组适配或匿名函数，应在使用者开始依赖前修改 API。

不要为已经有良好领域名称的操作发明自定义符号。`Booking.confirm code booking` 比未解释的运算符更容易搜索、记录与理解。标准 `|>`、`>>` 和 `<<` 已经能表达应用顺序。

## 练习 {#exercises}

### 练习 1：推导两个组合 {#exercise-01}

已有三个函数：`parse : string -> Draft`、`normalize : Draft -> Draft` 和 `label : Draft -> string`。把 `label (normalize (parse text))` 改写为：

1. 现在就使用 `text` 的管道；
2. 前向组合函数；
3. 后向组合函数。

写出每个组合函数的类型，并说明哪个函数最先运行。


::: details 参考答案

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

展开任一应用都能看出嵌套关系：

```fsharp
// forward text 展开为：
label (normalize (parse text))

// backward text 展开为相同表达式：
label (normalize (parse text))
```

在可执行代码中，应使用 `forward text = backward text` 比较结果值。

:::

### 练习 2：排列面向 F# 的 API {#exercise-02}

为以下函数设计参数顺序，并分别展示一次部分应用与一次管道调用：

- 按一个固定 `BookingStatus` 筛选预约；
- 用一个固定的文化特定格式器渲染多项预约；
- 检查请求 `SeatCount` 是否适合一个 `Capacity`。

指出哪一个函数使用直接调用比管道更易读。


::: details 参考答案

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

对于一次检查，`fitsWithin eventCapacity requestedSeats` 直接表达两个值的关系。尽管二者都包含带度量整数，受保护类型会让实参互换成为编译期错误。这项安全性比最后一次调用是否包含 `|>` 更重要。

:::

### 练习 3：移除装饰性管道 {#exercise-03}

评审以下代码：

```fsharp
let canAccept capacity request =
    request
    |> Booking.seats
    |> SeatCount.value
    |> fitsWithin (Capacity.value capacity)
```

给出一个直接版本，以及一个带有一个有意义中间名称的管道版本。为生产代码选择其一，并从可读性与调试角度说明原因，而不是比较字符数。


::: details 参考答案

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

这里我会选择第一版。提取过程很短，两个数量并列出现在最终关系旁，调试器还能检查每个命名值。第二版也正确，并可能适合外围管道，但最后一个 `|>` 没有增加变换，只是调整了二元谓词的实参顺序。

如果 `fitsWithin` 改为直接接收受保护的 `Capacity` 与 `SeatCount`，最佳实现还能更短：

```fsharp
let canAccept capacity request =
    fitsWithin capacity (Booking.seats request)
```

把度量值的解包留在领域谓词内，也能减少调用位置对底层表示的重复了解。

:::


第 14 章会把这套 API 推理应用于集合；所选表示还会决定求值时机、查找规则与转换成本。

## 资料来源 {#sources}

- [Microsoft Learn：函数、管道与组合](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/functions/)
- [Microsoft Learn：形参与实参](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/parameters-and-arguments)
- [Microsoft Learn：F# 格式指南](https://learn.microsoft.com/en-us/dotnet/fsharp/style-guide/formatting)
- [Microsoft Learn：F# 组件设计指南](https://learn.microsoft.com/en-us/dotnet/fsharp/style-guide/component-design-guidelines)
