---
title: "第 9 章：缺失与预期失败"
description: "从有意义的缺失推导 option，从预期失败推导 Result，再在不丢失错误上下文的前提下组合二者。"
translationKey: part-02/ch-09-option-result
---

# 第 9 章：缺失与预期失败 {#overview}

一次查找可能合理地找不到预约。另一个预约请求可能已被找到，却因为索要过多座位而失败。两种计算都没有得到正常值，但它们传达的含义不同：前者中，“没有”就是完整答案；后者中，调用方还需要知道原因。

F# 用不同类型表达这两种含义。`'T option` 表示“可能有一个 `'T`”。`Result<'T, 'TError>` 表示“要么成功并得到 `'T`，要么得到有类型的错误”。二者都是可区分联合。调用方直接处理其案例，无需依赖 `-1`、空字符串等特殊值，也无需猜测未写明的异常约定。

这里先用普通函数讨论同步代码。第 18 章介绍计算表达式，第 19 章处理可空引用互操作，第 21 章讨论异常。

## 用案例表示缺失，不用隐含特殊值 {#absence-as-data}

设想一个函数在找不到参与者时返回 `""`。调用方无法分辨空字符串究竟表示“缺失”，还是存储的数据本来就是空字符串。返回 `null` 只是把歧义移入运行时约定；抛出异常则会把普通的查找结果伪装成异常情况。

option 恰有两个案例：

```fsharp
type Option<'T> =
    | Some of 'T
    | None
```

这里展示的是概念定义——该类型已经存在于 FSharp.Core。`Some value` 表示值存在，`None` 表示值缺失。返回类型在代码运行前就写明了这两种可能。

示例遵循标准的 `try` 命名惯例，用它命名可能找不到值的操作：

```fsharp:line-numbers
let attendees = [ "B-101", "Lin"; "B-102", "Ada" ]

let tryFindAttendee bookingId =
    attendees |> List.tryFind (fun (id, _) -> id = bookingId) |> Option.map snd

let knownAttendee = tryFindAttendee "B-101" |> Option.defaultValue "none"

let missingAttendee = tryFindAttendee "B-999" |> Option.defaultValue "none"

printfn "Lookup: known=%s missing=%s" knownAttendee missingAttendee
```
这段代码可单独运行，输出 `Lookup: known=Lin missing=none`。

`List.tryFind` 返回 option。`Option.map snd` 只在元组存在时变换它：`Some (id, name)` 变成 `Some name`，`None` 仍为 `None`。这个函数不会编造一个占位参加者。

### 明确处理缺失 {#consuming-option}

当两个案例会触发不同行为时，模式匹配会保留二者：

```fsharp
let lookupMessage bookingId =
    match tryFindAttendee bookingId with
    | Some attendee -> $"attendee:{attendee}"
    | None -> "booking not found"
```

这段定义承接前面的 `tryFindAttendee`。例如，`lookupMessage "B-101"` 得到 `"attendee:Lin"`，而 `lookupMessage "B-999"` 得到 `"booking not found"`。

确实有合适的后备值时，`Option.defaultValue fallback option` 会把两个案例归并为一个普通值。若计算后备值的成本较高，`Option.defaultWith` 会推迟到需要时再计算。

不要把 `.Value` 或 `Option.get` 当成解包 option 的常规方式。二者遇到 `None` 都会抛出异常，丢掉类型所表达的安全性。只有附近代码已经确认值是 `Some` 时，才可能合理使用；模式匹配通常更清楚。

## `map` 变换现有值，`bind` 继续查找 {#option-composition}

假设“找到行”和“接受其中的座位数”都可能不产生值。对第二个函数使用 `map` 会得到 `int option option`：

```text
// 产生嵌套 option，因为 tryPositiveSeats 自己已经返回 option。
rowOption |> Option.map tryPositiveSeats
```

这里是类型形状示意，不是独立脚本：假设 `rowOption : int option`，且 `tryPositiveSeats : int -> int option`，整个表达式的类型就是 `int option option`。下面的完整示例会定义实际数据和函数。

`Option.bind` 会连接这两个可能缺失的步骤：

```fsharp:line-numbers
let requestedSeats = [ "B-101", 3; "B-102", 0 ]

let tryPositiveSeats seats = if seats > 0 then Some seats else None

let tryRequestedSeats bookingId =
    requestedSeats
    |> List.tryFind (fun (id, _) -> id = bookingId)
    |> Option.map snd
    |> Option.bind tryPositiveSeats

let positiveSeats =
    tryRequestedSeats "B-101" |> Option.map string |> Option.defaultValue "none"

let nonPositiveSeats =
    tryRequestedSeats "B-102" |> Option.map string |> Option.defaultValue "none"

printfn "Option bind: positive=%s nonPositive=%s" positiveSeats nonPositiveSeats
```
这段代码可单独运行，输出 `Option bind: positive=3 nonPositive=none`。

根据后续函数的返回类型来选择：

| 后续函数 | 操作 | 输入 `Some x` 时的结果 | 输入 `None` 时的结果 |
| --- | --- | --- | --- |
| `'T -> 'U` | `Option.map` | `Some (f x)` | `None` |
| `'T -> 'U option` | `Option.bind` | `f x` | `None` |

`bind` 会**短路**：一个步骤返回 `None` 后，后续依赖函数不会运行。这是由案例自然决定的流程，不是隐藏控制。

option 有意不说明值为何缺失。如果调用方需要区分“未知预约”“标识无效”和“目录不可用”，`None` 已经抹掉了调用方所需的信息。此时，`Result` 才是更好的模型。

## 用错误类型表示预期失败 {#result-model}

`Result<'T, 'TError>` 也有两个案例：

```fsharp
// 概念结构；FSharp.Core 已经提供 Result。
type Result<'T, 'TError> =
    | Ok of 'T
    | Error of 'TError
```

`Ok value` 携带成功值；`Error error` 携带领域所选的原因。可区分联合通常优于裸错误字符串，因为程序仍能识别每种失败案例。下面的代码包含所需类型与命名空间，可单独运行：

```fsharp:line-numbers
open System

type BookingRequest =
    { Attendee: string
      Seats: int }

type BookingError =
    | EmptyAttendee
    | NonPositiveSeats of actual: int
    | TooManySeats of requested: int * maximum: int

let validateAttendee request =
    if String.IsNullOrWhiteSpace request.Attendee then
        Error EmptyAttendee
    else
        Ok request

let validateSeats maximum request =
    if request.Seats <= 0 then
        Error(NonPositiveSeats request.Seats)
    elif request.Seats > maximum then
        Error(TooManySeats(request.Seats, maximum))
    else
        Ok request

let validate maximum request =
    request |> validateAttendee |> Result.bind (validateSeats maximum)

let describeError error =
    match error with
    | EmptyAttendee -> "attendee is empty"
    | NonPositiveSeats actual -> $"seat count {actual} is not positive"
    | TooManySeats(requested, maximum) -> $"requested {requested} exceeds maximum {maximum}"

let describeResult result =
    match result with
    | Ok request -> $"ok:{request.Attendee}:{request.Seats}"
    | Error error -> $"error:{describeError error}"

let validRequest = { Attendee = "Lin"; Seats = 2 }

let emptyAttendeeRequest = { Attendee = ""; Seats = 2 }

printfn
    "Validation: success=%s failure=%s"
    (validate 4 validRequest |> describeResult)
    (validate 4 emptyAttendeeRequest |> describeResult)
```
输出为：

```text
Validation: success=ok:Lin:2 failure=error:attendee is empty
```

把这段完整示例保存为 `ch09-option-result.fsx`。本章后面的错误上下文与短路代码都按出现顺序承接这里的类型和函数。

`BookingError` 区分参与者为空、非正座位数及其实际值，以及超过已知上限的请求。格式化集中在 `describeError`，因此验证策略没有与英文界面文本耦合。

`validateAttendee` 和 `validateSeats` 返回 `Result<BookingRequest, BookingError>`。`validate` 管道使用 `Result.bind`，因为第二项验证本身也返回 result。若参与者验证返回 `Error`，座位验证会被跳过，同一个错误则被保留。

### 分别变换成功与错误 {#result-transformations}

`Result` 模块为成功和失败两种情况分别提供操作：

- `Result.map` 只变换 `Ok` 内的值；
- `Result.mapError` 只变换 `Error` 内的值；
- `Result.bind` 只在 `Ok` 时运行后续的 result 生产函数；
- `Result.defaultValue` 有意识地用后备值替代错误。

与 option 一样，后续函数返回普通值时选择 `map`；后续函数本身已经返回 `Result` 时选择 `bind`。例如，`Result.map bookingLabel` 会保留错误，而 `Result.bind reserveSeats` 可以产生新的错误。

## 在错误向外传递时逐层补充上下文 {#error-context}

底层验证错误可能没有指出是哪个请求导致了它。不要依赖拼接字符串的约定，而要使用结构化上下文：

```fsharp:line-numbers
type RequestFailure =
    { RequestId: string
      Cause: BookingError }

let addRequestContext requestId result =
    result
    |> Result.mapError (fun error -> { RequestId = requestId; Cause = error })

let oversizedRequest = { Attendee = "Ada"; Seats = 6 }

let contextualFailure = oversizedRequest |> validate 4 |> addRequestContext "R-9"

match contextualFailure with
| Ok _ -> printfn "Context: unexpected success"
| Error failure -> printfn "Context: %s -> %s" failure.RequestId (describeError failure.Cause)
```
这段续接代码输出 `Context: R-9 -> requested 6 exceeds maximum 4`。

`addRequestContext` 只改变错误类型。`Ok request` 原样通过；`Error BookingError` 变成 `Error RequestFailure`。外层代码可以记录 `RequestId`、翻译 `Cause`，或把领域失败映射为 HTTP 响应，而无需解析文本。

不要在最深层函数里附上所有可能的细节。每一层只提供自己知道的错误事实；错误向外传递时，再加入请求、文件或端点等上下文。这样既能复用核心领域函数，也能把诊断信息保留为可检查的字段，而不是拼进字符串。

## `bind` 保留第一个失败，而非所有失败 {#short-circuiting}

共享请求违反了两条规则，但管道返回参与者错误：

```fsharp:line-numbers
let doublyInvalidRequest = { Attendee = ""; Seats = 0 }

printfn "Short circuit: %s" (validate 4 doublyInvalidRequest |> describeResult)
```
这段续接代码输出 `Short circuit: error:attendee is empty`。座位数也无效，但第一项验证失败后，第二项不会运行。

对相互依赖的步骤而言，这种行为正确：只有前面的数据有效，座位验证才有意义。它不会累积所有错误。如果表单要一次展示所有相互独立的问题，就应主动收集结果，或采用累积式验证；第 18 章会再谈这个区别。

`Error` 应描述调用方能够合理检查或处理的失败。不要捕获所有异常并将它们变成模糊的 `Error "failed"`；这会破坏堆栈和原因信息。程序缺陷、取消、资源失败和领域拒绝需要不同处理，第 21 章会给出具体策略。

## 选择能准确表达含义的最小类型 {#choosing-a-type}

从调用方必须回答的问题出发：

| 情况 | 通常选择 | 调用方得知 |
| --- | --- | --- |
| 查找可能无匹配，知道“无匹配”就已足够 | `'T option` | 存在或缺失 |
| 解析或验证可能因有用的已知原因失败 | `Result<'T, 'Error>` | 成功或已建模原因 |
| 函数保证值存在 | `'T` | 一个值，没有公开的替代案例 |
| 失败出乎预期或无法在局部处理 | 不要自动选择 `Result` | 保留异常或取消的原有语义 |

不要仅仅为了模仿 option 而返回 `Result<'T, unit>`；错误不携带信息时就使用 option。反过来，也不要只为缩短签名而把有意义的错误压缩成 `None`。

嵌套类型可能准确表达含义。`Result<'T option, 'Error>` 可以表示“操作本身可能失败；成功后仍可能找不到值”。只有当领域表明两个维度是同一事实时，才应该把它们合并。

## `Some null` 仍有可能 {#some-null}

option 只表示值是否存在，不会检查值本身。因此，可空引用仍能被放进 `Some`：

```fsharp:line-numbers
let riskyPayload: (string | null) option = Some null

let payloadIsNull =
    match riskyPayload with
    | Some value -> isNull value
    | None -> false

printfn "Some null: isSome=%b payloadIsNull=%b" riskyPayload.IsSome payloadIsNull
```
这段代码可单独运行，输出 `Some null: isSome=true payloadIsNull=true`。它需要支持 F# 空值检查语法的当前工具链。

这样会产生三种可表示状态：`None`、`Some null` 和 `Some "Lin"`。这通常是意外复杂度。在 .NET 边界处，应先把可空结果规范化为 `None`，或拒绝它，再让核心代码接收该值。

启用 F# 空值检查后，`(string | null) option` 明确表示其中的值可以为 null。这里先记住：`Some` 不能保证内部引用非 null。第 19 章会完整解释 `T | null`、`Nullable<T>`、旧式 .NET 标注与互操作转换。

## 练习 {#exercises}

### 练习 1：选择返回类型 {#exercise-01}

为每个函数选择 `'T`、`'T option` 或 `Result<'T, 'Error>`，并说明保留了哪些信息：

1. 用一个在其他方面有效的标识查找预约；
2. 解析以文本提供的座位数；
3. 从已经验证为非空的姓名计算参与者姓名首字母；
4. 查询一个可能失败的外部服务，而一次成功查询仍可能找不到预约。


::: details 参考答案

1. **用有效标识查找：** `Booking option`。查无结果属于正常情况，题设还说明标识验证已经完成。如果存储访问本身可能失败，那是另一个维度，类型可以变成 `Result<Booking option, StorageError>`。
2. **解析座位数：** `Result<int, SeatCountError>`。文本可能格式不对、超出 `int` 范围，或数值不被业务接受。带类型的错误让调用方能够解释或响应这些区别。只有当所有失败都被有意视为“没有解析出来”时，`int option` 才可能足够。
3. **计算姓名首字母：** `string`。题设承诺姓名已经验证为非空。公开缺失或失败会迫使每位调用方处理契约声称不会发生的案例。若实际上无法信任前提，就应修复输入类型或在边界验证。
4. **查询服务：** `Result<Booking option, ServiceError>`。`Error` 表示查询未成功完成；`Ok None` 表示查询完成但没有找到值；`Ok (Some booking)` 表示查询完成且找到了值。压平任一层都会合并不同事实。

类型应追随含义，而不是实现上的方便。

:::

### 练习 2：组合可选数据 {#exercise-02}

从以下两个函数开始：

```fsharp
tryFindBooking : string -> Booking option
tryConfirmedCode : Booking -> string option
```

定义 `tryFindConfirmedCode : string -> string option`。让结果保持扁平，并直接表达组合过程，无需使用模式匹配。然后解释 `bind` 为什么适合这项组合，以及 `map` 会怎样改变结果类型。


::: details 参考答案

直接定义如下：

```fsharp
let tryFindConfirmedCode bookingId =
    bookingId
    |> tryFindBooking
    |> Option.bind tryConfirmedCode
```

`tryConfirmedCode` 已经返回 `string option`。`Option.map tryConfirmedCode` 会再次包装这个返回的 option，产生 `string option option`。`Option.bind` 把函数应用于 `Some booking`，直接返回函数产生的 option；遇到 `None` 则不调用函数并原样保留。

直接使用模式匹配也有相同行为：

```fsharp
let tryFindConfirmedCodeExplicit bookingId =
    match tryFindBooking bookingId with
    | Some booking -> tryConfirmedCode booking
    | None -> None
```

第一版并非更加正确，只是更紧凑地表达了相同的案例分析。

:::

### 练习 3：保留验证上下文 {#exercise-03}

完成以下四步：

1. 向 `BookingError` 添加 `EventClosed` 案例。
2. 编写 `validateOpen : bool -> BookingRequest -> Result<BookingRequest, BookingError>`。
3. 把它组合在现有验证之后。
4. 用 `Result.mapError` 同时附上请求标识和活动标识。

最后说明一个有两处无效的请求会返回哪个错误，并解释这项优先级。


::: details 参考答案

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

如果界面必须报告全部三个相互独立的违规项，这条管道就选错了组合规则。此时要运行每项验证并主动累积错误；调整 `bind` 顺序无法实现累积。

:::


第 10 章会把同样由案例驱动的推理从两个案例的容器推广到递归树。

## 资料来源 {#sources}

- [Microsoft Learn：Option](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/options)
- [Microsoft Learn：Result](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/results)
- [FSharp.Core：Option 模块](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-optionmodule.html)
- [FSharp.Core：Result 模块](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-resultmodule.html)
