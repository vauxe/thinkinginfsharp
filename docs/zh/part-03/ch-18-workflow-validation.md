---
title: "第 18 章：工作流组合与验证错误累积"
description: "依赖步骤遇到首错停止，独立检查累积错误；先用普通 F# 函数确定语义，再考虑构建器语法。"
translationKey: part-03/ch-18-workflow-validation
---

# 第 18 章：工作流组合与验证错误累积 {#overview}

“验证失败”并不能决定失败应如何组合。如果后续步骤需要前一步产生的值，那么该值不可用时工作流就必须停止。如果三项检查分别查看独立字段，那么一次返回三个失败通常对表单更有用。

两种策略都可以返回 `Result`，区别在组合函数而非返回类型名称。下面先用普通函数实现两种策略，让求值方式和错误顺序清楚可见，再讨论计算表达式。

本章主线代码是同一个脚本的连续片段，完整文件位于 `examples/chapters/ch18/validation.fsx`。按正文顺序放入一个 `.fsx` 文件即可运行；后文若只展示某个函数，会明确说明它依赖前面已经定义的类型或函数。

## 先问后续检查是否需要前一步的值 {#dependency-question}

选择语法之前，先画出数据依赖：

```text
原始座位文本 ──▶ 解析出正数 SeatCount ──▶ 与容量比较

原始请求标识 ──▶ 验证标识 ─────┐
原始参与者 ─────▶ 验证姓名 ─────┼──▶ 构造 ValidBooking
原始座位文本 ───▶ 验证座位数 ───┘
```

容量比较依赖经过解析且为正数的 `SeatCount`。三个字段分支则都能独立读取原始请求。这里的“独立”描述数据需求；除非代码主动并行，否则执行仍保持顺序。

可以从下面的规则开始：

| 关系 | 组合策略 | 原因 |
|---|---|---|
| 后续步骤需要前一步成功 | 短路 | 失败后没有可供后续步骤使用的有效输入 |
| 检查查看手头已有的独立数据 | 当调用方需要全部失败时累积 | 每项检查都可以提供有用反馈 |
| 检查执行 I/O 或改变状态 | 放在单独的副作用阶段 | 成本、故障、取消和陈旧性需要自己的策略 |

不要一看到验证就累积错误。命令行工具可能有意只报告第一个语法错误，安全边界也可能避免泄露多项细节。应根据消费者需求选择。

## 为每个成功字段赋予类型 {#model}

示例把原始文本与成功检查后的值分开：

```fsharp:line-numbers [validation.fsx — 从这里开始]
open System

type ValidationError =
    | MissingRequestId
    | MissingAttendee
    | SeatsNotInteger of raw: string
    | NonPositiveSeats of actual: int
    | ExceedsCapacity of requested: int * available: int

type RequestId = RequestId of string
type Attendee = Attendee of string
type SeatCount = SeatCount of int

type RawBooking =
    { RequestId: string
      Attendee: string
      Seats: string }

type ValidBooking =
    { RequestId: RequestId
      Attendee: Attendee
      Seats: SeatCount }
```
`RawBooking` 可以包含空白或格式错误的文本。`ValidBooking` 需要 `RequestId`、`Attendee` 和 `SeatCount` 值，因此要等三个组件检查全都成功之后才构造它。

错误联合类型保留事实，而不是格式化后的 UI 消息。每个字段验证器返回 `Result<'Value, ValidationError list>`：

```fsharp:line-numbers [validation.fsx — 继续]
let validateRequestId raw =
    if String.IsNullOrWhiteSpace raw then
        Error [ MissingRequestId ]
    else
        Ok(RequestId(raw.Trim()))

let validateAttendee raw =
    if String.IsNullOrWhiteSpace raw then
        Error [ MissingAttendee ]
    else
        Ok(Attendee(raw.Trim()))

let validateSeats (raw: string) =
    match Int32.TryParse raw with
    | true, value when value > 0 -> Ok(SeatCount value)
    | true, value -> Error [ NonPositiveSeats value ]
    | false, _ -> Error [ SeatsNotInteger raw ]
```
每个独立验证器目前要么产生一个值，要么产生只含一项的错误列表。这样，组合函数始终处理同一种错误类型。从类型上说，它允许 `Error []`；本实现永远不产生该值。如果错误列表必须非空，就用非空错误类型保证它，而不是依赖约定。

在 `validateSeats` 内部，整数解析必须先于正数比较。这两个检查相互依赖：解析失败时，没有整数可以比较。跨字段累积并不要求假装一个字段内部的所有操作也都彼此独立。

## `Result.bind` 保留第一个错误 {#first-error}

FSharp.Core 把 `Result.bind` 的核心行为定义为：

```fsharp
match input with
| Error error -> Error error
| Ok value -> binder value
```

在 `Error` 分支中不会调用绑定函数。错误类型保持不变；`bind` 也不会追加错误。

第一种策略嵌套了三个依赖的延续：

```fsharp:line-numbers [validation.fsx — 继续]
let validateFirstError (raw: RawBooking) =
    validateRequestId raw.RequestId
    |> Result.bind (fun requestId ->
        validateAttendee raw.Attendee
        |> Result.bind (fun attendee ->
            validateSeats raw.Seats
            |> Result.map (fun seats ->
                { RequestId = requestId
                  Attendee = attendee
                  Seats = seats })))
```
面对完全无效的请求，`validateRequestId` 返回 `Error [MissingRequestId]`。参与者和座位验证器只会在前一步成功后运行，所以结果只含第一个错误。

当每一步都使用前一步的受保护输出时，这种行为是正确的。当产品策略只返回一条消息时，它同样有效。但仅仅重排 `Result.bind` 调用不会让它累积错误，只会改变哪个失败排在第一。

### 顺序成为可观察的策略 {#first-error-order}

当两个检查都可能失败时，首错组合会让它们的顺序对调用方可见。应把结构前提放在前面，并记录该策略。如果产品承诺了特定优先级，就不要只依赖碰巧方便的源码顺序。

短路还会避免不必要的工作，但这只是结果，并不意味着可以把副作用藏进验证器。第 20–23 章会明确处理时间、I/O、故障和取消。

## 累积先求值各项独立结果 {#accumulation}

累积策略会先求值三个字段函数，然后才判断是否能构造结果：

```fsharp:line-numbers [validation.fsx — 继续]
let errorsOf result =
    match result with
    | Ok _ -> []
    | Error errors -> errors

let validateAccumulating (raw: RawBooking) =
    let requestIdResult = validateRequestId raw.RequestId
    let attendeeResult = validateAttendee raw.Attendee
    let seatsResult = validateSeats raw.Seats

    match requestIdResult, attendeeResult, seatsResult with
    | Ok requestId, Ok attendee, Ok seats ->
        Ok
            { RequestId = requestId
              Attendee = attendee
              Seats = seats }
    | _ ->
        [ yield! errorsOf requestIdResult
          yield! errorsOf attendeeResult
          yield! errorsOf seatsResult ]
        |> Error
```
如果所有结果都是 `Ok`，匹配就构造一个 `ValidBooking`。否则，`errorsOf` 按字段顺序贡献每份失败列表。因此，无效示例会产生：

```fsharp
Error [ MissingRequestId; MissingAttendee; SeatsNotInteger "oops" ]
```

这就是验证错误累积：求值彼此独立的检查，并按规定顺序合并失败。普通 F# 仍依次求值，但不会因为另一个字段失败而跳过任何检查。

应保持错误顺序确定。这里按输入布局依次返回请求标识、参与者和座位数错误。稳定顺序让测试、UI 焦点、日志和客户端行为可以预测。`Set` 会去重并按比较规则排序，从而改变可观察行为。

### 累积不等于“任何失败后都继续” {#accumulation-limits}

只组合放在一起仍然有意义的错误。如果文档解码失败，就无法执行其缺失字段上的检查。如果没有经过认证的身份，授权决策就缺少主体。应先获得前提，再累积共同使用所得数据的检查。

同样，不要先构造部分有效的领域记录，再逐字段补上剩余值。应分别保留成功结果，按需收集错误，只在全部为 `Ok` 时构造最终类型。

## 把组合规则提取成普通函数 {#reusable-accumulation}

直接写出的三路匹配很容易审计。当这种模式重复出现时，只提取组合机制：

```fsharp:line-numbers [validation.fsx — 继续]
let applyValidation valueResult functionResult =
    match functionResult, valueResult with
    | Ok mapping, Ok value -> Ok(mapping value)
    | Error functionErrors, Error valueErrors -> Error(functionErrors @ valueErrors)
    | Error errors, Ok _
    | Ok _, Error errors -> Error errors

let createBooking requestId attendee seats : ValidBooking =
    { RequestId = requestId
      Attendee = attendee
      Seats = seats }

let validateAccumulatingWithApply (raw: RawBooking) =
    Ok createBooking
    |> applyValidation (validateRequestId raw.RequestId)
    |> applyValidation (validateAttendee raw.Attendee)
    |> applyValidation (validateSeats raw.Seats)
```
`applyValidation` 有四种情况：

- 把成功函数应用于成功值；
- 两边都失败时，从左到右追加错误；
- 只有一边失败时，保留那一份错误列表。

柯里化的 `createBooking` 从 `Ok` 内开始。每次应用提供一个独立计算出的组件。示例断言，这次重构与直接匹配返回完全相同的错误、顺序和成功值。

函数先接收下一个值结果，使包含构造函数的 `Result` 可以通过 `|>` 继续传递。这只是为了让当前 F# API 便于使用，不代表通用命名规则。

对很大的验证集合反复追加列表，代价可能较高。这里的表单只有几个固定字段，应先选择最容易读懂的实现。

如果以后测量到真实的性能问题，可以改为反向累积，或使用非空集合类型。当前不要为了尚未出现的成本引入更复杂的数据结构。

## 依赖步骤应保持短路 {#dependent-workflow}

座位数一旦存在，与容量比较就是依赖性的业务检查：

```fsharp:line-numbers [validation.fsx — 继续]
let ensureWithin capacity (SeatCount requested as seats) =
    if requested <= capacity then
        Ok seats
    else
        Error [ ExceedsCapacity(requested, capacity) ]

let validateSeatsThenCapacity checkCapacity rawSeats =
    validateSeats rawSeats |> Result.bind checkCapacity

let observeDependentValidation rawSeats =
    let mutable capacityChecks = 0

    let observedCheck seats =
        capacityChecks <- capacityChecks + 1
        ensureWithin 4 seats

    validateSeatsThenCapacity observedCheck rawSeats, capacityChecks
```
`validateSeatsThenCapacity` 使用 `Result.bind`。它注入的容量函数只会收到有效 `SeatCount`。测试包装函数用计数器统计调用次数，却没有把可变性放进正式函数：

| 输入 | 结果 | 容量检查次数 |
|---|---|---:|
| `"oops"` | `SeatsNotInteger "oops"` | 0 |
| 容量为 4 时输入 `"5"` | `ExceedsCapacity(5, 4)` | 1 |
| 容量为 4 时输入 `"3"` | `Ok(SeatCount 3)` | 1 |

零次直接展示了短路行为，并非基于耗时的优化结论。计数只是测试工具；正确性不能依赖它。

脚本最后用一组具体输入把上述定义连起来。`invalid` 同时包含三个独立错误，`valid` 则展示修剪后的成功值：

```fsharp:line-numbers [validation.fsx — 最终检查]
let invalid: RawBooking =
    { RequestId = " "
      Attendee = ""
      Seats = "oops" }

let valid: RawBooking =
    { RequestId = " REQ-18 "
      Attendee = " Lin "
      Seats = "3" }

let expectedErrors =
    Error [ MissingRequestId; MissingAttendee; SeatsNotInteger "oops" ]

let expectedValid =
    Ok
        { RequestId = RequestId "REQ-18"
          Attendee = Attendee "Lin"
          Seats = SeatCount 3 }

printfn "first-error: %b" (validateFirstError invalid = Error [ MissingRequestId ])
printfn "all-errors: %b" (validateAccumulating invalid = expectedErrors)
printfn "apply-agrees: %b" (validateAccumulatingWithApply invalid = expectedErrors)
printfn "valid-booking: %b" (validateAccumulating valid = expectedValid)

printfn
    "dependent: parse=%A over=%A fit=%A"
    (observeDependentValidation "oops")
    (observeDependentValidation "5")
    (observeDependentValidation "3")
```

从仓库根目录运行 `dotnet fsi examples/chapters/ch18/validation.fsx`，会得到：

```text
first-error: true
all-errors: true
apply-agrees: true
valid-booking: true
dependent: parse=(Error [SeatsNotInteger "oops"], 0) over=(Error [ExceedsCapacity (5, 4)], 1) fit=(Ok (SeatCount 3), 1)
```

这些布尔值不是业务结果，而是可以运行的检查。它们分别验证首个错误、错误顺序、两种累积实现是否等价，以及成功值能否正确构造。

真实预约工作流通常同时使用两种策略：

```text
累积独立的原始字段错误
          ↓ 仅当有效
短路执行依赖性的领域决策
          ↓ 仅当接受
在边界显式执行副作用
```

不要用“验证”一个标签抹平这些不同语义。

## 让外部检查离开纯累积 {#effect-boundary}

对内存字符串进行电子邮件格式检查可以加入字段累积。“电子邮件在数据库中唯一”则不同：它有延迟、可能故障、需要取消，而且查询完成后就可能立即过时。只为收集消息而运行三项此类检查，可能放大成本并暴露不一致快照。

先累积手头已有的廉价确定性事实，再按既定策略执行必要的外部决策。最终写入仍必须原子地强制并发敏感规则；之前的验证查询不是锁。

这种分离也让测试范围更准确。纯累积只需要输入和预期值。有副作用的工作则需要端口、受控替身，以及后续的取消和资源测试。

## 计算表达式不会替你选择语义 {#computation-expressions}

计算表达式的形式为 `builder { ... }`。`let!`、`return` 和 `and!` 等关键字会通过该特定构建器提供的方法进行翻译。脱离构建器后，语法没有唯一的通用含义。

FSharp.Core 提供内置的序列、async、task 和查询计算表达式。它没有内置 `result {}` 或累积式 `validation {}` 表达式。内置的是 `Result` 类型与 `Result.bind` 函数；使用它们的构建器必须来自你自己的代码或选定的库。

简单的 result 构建器通常用首错行为定义 `Bind`。连续写多个 `let!` 会缩短代码，却不会把它变成累积。

### 延伸：由构建器定义的 `and!` {#and-bang-extension}

F# 支持在一个计算表达式绑定组内用 `let! ... and! ...` 声明彼此独立的绑定。构建器的 `MergeSources`（或相关优化成员）决定如何组合来源。某个验证构建器可以追加错误列表；async/task 构建器可以安排并发启动；其他构建器还可以选择别的行为。

下面只是说明形式；若没有专门定义或导入的 `validation` 构建器，它不能运行：

```fsharp
validation {
    let! requestId = validateRequestId raw.RequestId
    and! attendee = validateAttendee raw.Attendee
    and! seats = validateSeats raw.Seats
    return createBooking requestId attendee seats
}
```

同一组中的绑定不能相互依赖。绝不能脱离上下文把 `and!` 描述成“累积错误”或“并行运行”；必须说明所用构建器及其 `MergeSources` 行为。工作流稳定且反复出现时，自定义构建器可能很好用，但会引入另一套 API 与调试方式。这里的普通函数仍是语义基线。

## 选择能准确表达规则的最小组合 {#selection-rule}

| 需求 | 优先选择 |
|---|---|
| 下一步需要前一步的成功值 | `Result.bind` 或直接匹配 |
| 返回一个按优先级选择的失败 | 有顺序的首错组合 |
| 返回所有独立的纯输入错误 | 直接累积或小型、经过测试的 apply/map 函数 |
| 执行外部检查 | 更晚的副作用工作流步骤 |
| 语法反复出现且稳定，团队或库已有统一约定 | 有文档的计算表达式构建器 |
| 一次性的特殊组合 | 直接模式匹配，而不是新抽象 |

类型声明可能的结果；组合函数声明求值策略。两者都需要评审。

## 练习 {#exercises}

### 练习 1：画出两个验证阶段 {#exercise-01}

某预约命令必须检查请求标识、参与者姓名、整数座位文本、容量，以及数据库中请求标识的唯一性。把每项检查分类为独立纯输入验证、依赖领域验证或外部副作用操作。

画出执行顺序，使有用的输入错误能一起报告，同时避免对结构上无效的输入查询数据库。说明哪里必须短路，哪里适合累积。


::: details 参考答案

#### 对检查进行分类 {#exercise-01-classification}

给定内存中的原始命令：

| 检查 | 分类 | 原因 |
|---|---|---|
| 请求标识非空且格式正确 | 独立纯输入验证 | 只使用原始请求标识文本 |
| 参与者姓名非空 | 独立纯输入验证 | 只使用原始参与者文本 |
| 座位文本可解析为正数 | 与其他字段独立；内部有依赖 | 正数检查依赖解析成功，但不需要其他字段 |
| 请求座位数不超过给定 `Capacity` | 依赖性的纯领域验证 | 需要有效 `SeatCount` 与 `Capacity` |
| 请求标识在数据库中唯一 | 有副作用的边界工作 | 需要有效标识与可能立即过时的外部查询 |

如果当前容量也必须加载，而不是以领域值传入，那么获取容量同样有副作用。加载完成后，比较仍可保持为纯函数。

#### 排列各个阶段 {#exercise-01-order}

```text
累积请求标识 + 参与者 + 座位文本错误
                    ↓ 仅当成功
把有效 SeatCount 与给定/当前 Capacity 比较
                    ↓ 仅当成功
查询有效 RequestId 的唯一性
                    ↓ 仅当成功
在提交时原子地强制容量与唯一性
```

第一阶段运行三项有用的输入检查。座位解析失败时，容量比较因缺少 `SeatCount` 而短路。唯一性查询要等廉价验证与领域决策通过后再执行，从而避免不必要的 I/O。

数据库唯一性查询只在写入前提供建议。查询之后，另一个请求仍可能抢先占用同一标识，所以提交边界必须原子地强制唯一性。实时容量同理：预检查无法阻止后续竞争。

测量成本或产品策略不同时，可以调整容量检查与唯一性查询的顺序，但二者都必须位于类型化前提之后。这是工作流主动选择的顺序，不是 `Result` 自带的性质。

:::

### 练习 2：实现有序累积 {#exercise-02}

为 `Result<'T, 'Error list>` 编写普通 `applyValidation`，再用柯里化构造函数验证姓名、电子邮件和座位数。当三者都失败时，错误必须按字段顺序出现。断言全有效输入能构造最终记录。

解释 `Error []` 表示什么，以及你的 API 是否应让该状态无法表示。


::: details 参考答案

#### 小型可复用 apply 函数 {#exercise-02-apply}

```fsharp
let applyValidation valueResult functionResult =
    match functionResult, valueResult with
    | Ok mapping, Ok value -> Ok(mapping value)
    | Error earlier, Error later -> Error(earlier @ later)
    | Error errors, Ok _
    | Ok _, Error errors -> Error errors
```

已有的累积函数位于左边，所以两边失败时追加 `earlier @ later`。这会把错误顺序固定为值结果进入管道的顺序。

#### 验证三个字段 {#exercise-02-fields}

```fsharp
open System

type FormError =
    | MissingName
    | InvalidEmail of raw: string
    | InvalidSeats of raw: string

type ValidForm =
    { Name: string
      Email: string
      Seats: int }

let validateName (raw: string) =
    if String.IsNullOrWhiteSpace raw then Error [ MissingName ]
    else Ok(raw.Trim())

let validateEmail (raw: string) =
    if raw.Contains('@') then Ok raw
    else Error [ InvalidEmail raw ]

let validateSeats (raw: string) =
    match Int32.TryParse raw with
    | true, seats when seats > 0 -> Ok seats
    | _ -> Error [ InvalidSeats raw ]

let createForm name email seats =
    { Name = name
      Email = email
      Seats = seats }

let validateForm name email seats =
    Ok createForm
    |> applyValidation (validateName name)
    |> applyValidation (validateEmail email)
    |> applyValidation (validateSeats seats)
```

需要进行下面的检查：

```fsharp
assert (
    validateForm "" "wrong" "zero" =
        Error [ MissingName; InvalidEmail "wrong"; InvalidSeats "zero" ]
)

assert (
    validateForm " Lin " "lin@example.test" "3" =
        Ok { Name = "Lin"; Email = "lin@example.test"; Seats = 3 }
)
```

`Error []` 表示验证失败，却没有提供原因，这与 API 的约定矛盾。普通列表无法阻止这个状态。

如果调用方或自定义组合函数可以直接构造错误列表，应使用非空列表类型，避免出现空错误列表。如果只有这些小型且受控的函数能够构造结果，那么经过测试的约定也可能足够。

:::

### 练习 3：核查计算表达式说法 {#exercise-03}

评审下面这段没有展示导入或构建器定义的代码：

```fsharp
result {
    let! requestId = validateRequestId raw.RequestId
    and! seats = validateSeats raw.Seats
    return requestId, seats
}
```

解释为什么只凭 FSharp.Core 不能断定它能编译或累积错误。说出与 `let!` 和 `and!` 相关的构建器操作，说明独立性限制，然后用错误策略明确的普通函数重写这两项检查。


::: details 参考答案

#### 找出缺失的契约 {#exercise-03-builder}

`result` 必须是一个值，其类型提供计算表达式成员。FSharp.Core 定义了 `Result` 及其模块函数，却没有名为 `result`、能够建立该构建器的内置值。导入某个库或定义构建器后，这段代码可能编译；缺少该上下文时，它并不完整。

`let!` 主要使用构建器的 `Bind`。`and!` 主要使用 `MergeSources`，还可能使用可选的 `MergeSourcesN`、`BindN` 或 `BindNReturn` 优化。同一个 `let!`/`and!` 组中的请求标识与座位计算不能引用彼此绑定出来的值。

即使代码能够编译，是否累积仍取决于该构建器如何合并两个 `Error`。语法本身不会提供列表追加。

#### 直接写出累积规则 {#exercise-03-rewrite}

对于返回错误列表的验证器，完整的两项检查规则是：

```fsharp
let validatePair raw =
    let requestIdResult = validateRequestId raw.RequestId
    let seatsResult = validateSeats raw.Seats

    match requestIdResult, seatsResult with
    | Ok requestId, Ok seats -> Ok(requestId, seats)
    | Error requestErrors, Error seatErrors -> Error(requestErrors @ seatErrors)
    | Error errors, Ok _
    | Ok _, Error errors -> Error errors
```

两个验证器调用都在匹配之前发生，两个失败都被保留，而且请求标识错误在前。如果座位验证改为需要请求标识验证产生的值，这种组合就不诚实；应使用 `Result.bind` 短路该依赖。

之后可以用自定义验证构建器编码相同规则，并以直接函数为基准测试。这份重写也明确记录了 `MergeSources` 必须实现的语义。

:::


## 第三部分检查点 {#part-checkpoint}

沿上述普通函数追踪两种输入。结构无效时，应按字段顺序累积独立错误且不查询容量；输入有效时，应进入依赖性查询并构造请求。结果不能依赖未声明的计算表达式构建器。

[继续阅读第 19 章](../part-04/ch-19-dotnet-null-boundaries)，通过专门的适配层接收外部 .NET 值。

## 资料来源 {#sources}

- [Microsoft Learn：F# Result](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/results)
- [FSharp.Core：`Result` 模块](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-resultmodule.html)
- [Microsoft Learn：计算表达式与 `and!`](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/computation-expressions)
- [Microsoft Learn：F# 组件设计指南](https://learn.microsoft.com/en-us/dotnet/fsharp/style-guide/component-design-guidelines)
