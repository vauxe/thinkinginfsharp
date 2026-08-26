---
title: "第 9 章：缺失与预期失败"
description: "从有意义的缺失推导 option，从预期失败推导 Result，再在不丢失错误上下文的前提下组合二者。"
translationKey: part-02/ch-09-option-result
---

# 第 9 章：缺失与预期失败 {#overview}

一次查找可能合理地找不到预约。另一个预约请求可能已被找到，却因为索要过多座位而失败。两种计算都没有得到正常值，但它们传达的含义不同：前者中，“没有”就是完整答案；后者中，调用方还需要知道原因。

F# 用不同类型表达这两种含义。`'T option` 表示“可能有一个 `'T`”。`Result<'T, 'TError>` 表示“要么有成功的 `'T`，要么有已建模的 `'TError`”。二者都是可辨识联合，因此调用方必须处理其案例，而不必依赖 `-1`、空字符串等特殊值，也不必猜测未写明的异常约定。

## 学完本章后你能做什么 {#outcomes}

学完本章后，你应该能够：

- 在“缺失”无需进一步解释时选择 `option`；
- 在预期失败的原因有意义时选择 `Result`；
- 用模式匹配和默认值安全地使用这两种类型；
- 根据后续函数的返回类型区分 `map` 与 `bind`；
- 组合验证步骤，同时保留第一个错误；
- 用 `Result.mapError` 添加上下文而不改变成功值；
- 解释为什么 `Some null` 可能存在，以及为何通常应在边界处将其规范化。

本章只讨论同步代码，并使用显式函数。计算表达式见第 18 章，可空引用互操作见第 19 章，异常边界见第 21 章。

## 让缺失成为案例，而不是秘密值 {#absence-as-data}

设想一个函数在找不到参与者时返回 `""`。调用方无法分辨空字符串究竟表示“缺失”，还是存储的数据本来就是空字符串。返回 `null` 只是把歧义移入运行时约定；抛出异常则会把普通的查找结果伪装成异常情况。

option 恰有两个案例：

```fsharp
type Option<'T> =
    | Some of 'T
    | None
```

这里展示的是概念定义——该类型已经存在于 FSharp.Core。`Some value` 证明值存在，`None` 声明值缺失。返回类型在代码运行前就记录了这种可能性。

共享脚本遵循标准的 `try` 命名惯例，用它命名可能无法产生值的操作：

```fsharp:line-numbers [ch09-option-result.fsx]
let attendees = [ "B-101", "Lin"; "B-102", "Ada" ]

let tryFindAttendee bookingId =
    attendees |> List.tryFind (fun (id, _) -> id = bookingId) |> Option.map snd

let knownAttendee = tryFindAttendee "B-101" |> Option.defaultValue "none"

let missingAttendee = tryFindAttendee "B-999" |> Option.defaultValue "none"

printfn "Lookup: known=%s missing=%s" knownAttendee missingAttendee
```
`List.tryFind` 返回 option。`Option.map snd` 只在元组存在时变换它：`Some (id, name)` 变成 `Some name`，`None` 仍为 `None`。这个函数不会编造一个替代参与者。

### 有意识地处理缺失 {#consuming-option}

当两个案例会触发不同行为时，模式匹配会保留二者：

```fsharp
let lookupMessage bookingId =
    match tryFindAttendee bookingId with
    | Some attendee -> $"attendee:{attendee}"
    | None -> "booking not found"
```

当一个真正的后备值已经足够时，`Option.defaultValue fallback option` 会在该边界把两个案例收拢为一种值。若后备值的计算成本较高，`Option.defaultWith` 会把计算推迟到确实需要时。

不要把 `.Value` 或 `Option.get` 当成解包 option 的常规方式。二者遇到 `None` 都会抛出异常，丢掉类型所表达的安全性。只有当 `Some` 的证明就在局部且显而易见时，它们才可能合理；模式匹配通常能更清楚地记录这项证明。

## `map` 变换现有值，`bind` 继续查找 {#option-composition}

假设“找到行”和“接受其中的座位数”都可能不产生值。对第二个函数使用 `map` 会得到 `int option option`：

```fsharp
// 产生嵌套 option，因为 tryPositiveSeats 自己已经返回 option。
rowOption |> Option.map tryPositiveSeats
```

`Option.bind` 会连接这两个可能缺失的步骤：

```fsharp:line-numbers [ch09-option-result.fsx]
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
根据后续函数的返回类型来选择：

| 后续函数 | 操作 | 输入 `Some x` 时的结果 | 输入 `None` 时的结果 |
| --- | --- | --- | --- |
| `'T -> 'U` | `Option.map` | `Some (f x)` | `None` |
| `'T -> 'U option` | `Option.bind` | `f x` | `None` |

`bind` 会**短路**：一个步骤返回 `None` 后，后续依赖函数不会运行。这是由案例编码的普通数据流，不是隐藏的控制流。

option 有意不说明值为何缺失。如果调用方需要区分“未知预约”“标识无效”和“目录不可用”，`None` 已经抹掉了调用方所需的信息。此时，`Result` 才是更好的模型。

## 预期失败值得拥有错误类型 {#result-model}

`Result<'T, 'TError>` 也有两个案例：

```fsharp
// 概念形状；FSharp.Core 已经提供 Result。
type Result<'T, 'TError> =
    | Ok of 'T
    | Error of 'TError
```

`Ok value` 携带成功值；`Error error` 携带领域所选的原因。可辨识联合通常优于裸错误字符串，因为每种失败形状仍可供程序读取：

```fsharp:line-numbers [ch09-option-result.fsx]
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

```fsharp:line-numbers [ch09-option-result.fsx]
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
`addRequestContext` 只改变错误类型。`Ok request` 原样通过；`Error BookingError` 变成 `Error RequestFailure`。外层代码可以记录 `RequestId`、翻译 `Cause`，或把领域失败映射为 HTTP 响应，而无需解析文本。

不要在最深层函数里附上所有可能的细节。每一层只提供自己知道的错误事实；错误向外传递时，再加入请求、文件或端点等上下文。这样既能复用核心领域函数，也能把诊断信息保留为可检查的字段，而不是拼进字符串。

## `bind` 保留第一个失败，而非所有失败 {#short-circuiting}

共享请求违反了两条规则，但管道返回参与者错误：

```fsharp:line-numbers [ch09-option-result.fsx]
let doublyInvalidRequest = { Attendee = ""; Seats = 0 }

printfn "Short circuit: %s" (validate 4 doublyInvalidRequest |> describeResult)
```
对相互依赖的步骤而言，这种行为正确：只有前面的数据有效后，座位验证才可能有意义。它不会累积所有错误。如果表单应一次展示所有相互独立的问题，就要显式收集这些结果，或采用累积式验证；第 18 章会回到这个区别。

`Error` 应描述调用方能够合理检查或处理的失败。不要捕获所有异常并将它们变成模糊的 `Error "failed"`；这会破坏堆栈和原因信息。程序缺陷、取消、资源失败和领域拒绝各有不同边界，第 21 章会建立这项策略。

## 选择能准确表达含义的最小类型 {#choosing-a-type}

从调用方必须回答的问题出发：

| 情况 | 通常选择 | 调用方得知 |
| --- | --- | --- |
| 查找可能无匹配，知道“无匹配”就已足够 | `'T option` | 存在或缺失 |
| 解析或验证可能因有用的已知原因失败 | `Result<'T, 'Error>` | 成功或已建模原因 |
| 函数契约保证值存在 | `'T` | 一个值，没有公开的替代案例 |
| 失败出乎预期或无法在局部处理 | 不要自动选择 `Result` | 保留恰当的异常或取消边界 |

不要仅仅为了模仿 option 而返回 `Result<'T, unit>`；错误不携带信息时就使用 option。反过来，也不要只为缩短签名而把有意义的错误压缩成 `None`。

嵌套类型可能准确表达含义。`Result<'T option, 'Error>` 可以表示“操作本身可能失败；成功后仍可能找不到值”。只有当领域表明两个维度是同一事实时，才应该把它们合并。

## `Some null` 仍有可能 {#some-null}

option 只表示值是否存在，不会检查值本身。因此，可空引用仍能被放进 `Some`：

```fsharp:line-numbers [ch09-option-result.fsx]
let riskyPayload: (string | null) option = Some null

let payloadIsNull =
    match riskyPayload with
    | Some value -> isNull value
    | None -> false

printfn "Some null: isSome=%b payloadIsNull=%b" riskyPayload.IsSome payloadIsNull
```
这样会产生三种可表示状态：`None`、`Some null` 和 `Some "Lin"`。这通常是意外复杂度。在 .NET 边界处，应先把可空结果规范化为 `None`，或拒绝它，再让核心代码接收该值。

启用 F# 空值检查后，标注 `(string | null) option` 会明确表示其中的值可以为 null。本章只需记住：`Some` 本身不能证明内部引用一定非 null。第 19 章会完整解释 `T | null`、`Nullable<T>`、旧式 .NET 标注与边界转换。

## 运行共享示例 {#run-example}

在示例所在目录执行：

```console
dotnet fsi --exec ch09-option-result.fsx
```

六行输出覆盖成功查找、缺失、option 组合、验证成功与失败、附加错误信息、遇到第一个错误后停止，以及 `Some null` 边界情况。请逐行核对。

## 练习 {#exercises}

### 练习 1：选择返回类型 {#exercise-01}

为每个函数选择 `'T`、`'T option` 或 `Result<'T, 'Error>`，并说明保留了哪些信息：

1. 用一个在其他方面有效的标识查找预约；
2. 解析以文本提供的座位数；
3. 从已经验证为非空的姓名计算参与者姓名首字母；
4. 查询一个可能失败的外部服务，而一次成功查询仍可能找不到预约。

### 练习 2：组合可选数据 {#exercise-02}

从以下两个函数开始：

```fsharp
tryFindBooking : string -> Booking option
tryConfirmedCode : Booking -> string option
```

定义 `tryFindConfirmedCode : string -> string option`。让结果保持扁平，并直接表达组合过程，无需使用模式匹配。然后解释 `bind` 为什么适合这项组合，以及 `map` 会怎样改变结果类型。

### 练习 3：保留验证上下文 {#exercise-03}

完成以下四步：

1. 向 `BookingError` 添加 `EventClosed` 案例。
2. 编写 `validateOpen : bool -> BookingRequest -> Result<BookingRequest, BookingError>`。
3. 把它组合在现有验证之后。
4. 用 `Result.mapError` 同时附上请求标识和活动标识。

最后说明一个有两处无效的请求会返回哪个错误，并解释这项优先级。

[查看本章练习答案](../solutions/ch-09-option-result)。

## 模型复盘 {#model-review}

- `option` 建模有意义的缺失，而不是无法解释的失败。
- `Result` 保留成功值或带类型的预期失败。
- `map` 变换已包装值；`bind` 使用一个已经返回同种包装的函数继续计算。
- `Option.bind` 和 `Result.bind` 会在第一个 `None` 或 `Error` 处停止。
- `Result.mapError` 丰富失败上下文，而不打扰成功值。
- option 可以包含 `null`；应在有意设置的边界规范化可空 .NET 值。

第 10 章会把同样由案例驱动的推理从两个案例的容器推广到递归树。

## 资料来源 {#sources}

- [Microsoft Learn：Option](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/options)
- [Microsoft Learn：Result](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/results)
- [FSharp.Core：Option 模块](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-optionmodule.html)
- [FSharp.Core：Result 模块](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-resultmodule.html)
