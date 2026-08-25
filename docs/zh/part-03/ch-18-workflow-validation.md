---
title: "第 18 章：显式工作流组合与验证累积"
description: "依赖步骤采用首错停止，独立检查采用显式错误累积；先用普通 F# 函数建立语义，再考虑构建器语法。"
translationKey: part-03/ch-18-workflow-validation
---

# 第 18 章：显式工作流组合与验证累积 {#overview}

“验证失败”并不能决定失败应如何组合。如果后续步骤需要前一步产生的值，那么该值不可用时工作流就必须停止。如果三项检查分别查看独立字段，那么一次返回三个失败通常对表单更有用。

两种策略都可以返回 `Result`。区别不在容器名称，而在组合函数。本章用普通函数实现两种策略，让求值方式和错误顺序保持可见。只有当行为无需特殊语法也已经成立之后，才讨论计算表达式。

## 学完后你能够做什么 {#outcomes}

学完本章，你应该能够：

- 把检查分类为依赖检查、独立检查或有副作用的边界工作；
- 把 `Result.bind` 解释为遇到首个错误就短路；
- 组合依赖步骤，并保证失败后不调用后续函数；
- 求值独立字段检查并累积每项错误；
- 保持确定性的错误顺序；
- 只在所有组件成功后构造受保护结果；
- 提取可复用的普通累积函数；
- 避免只因为检查在逻辑上独立，就执行数据库或网络工作；
- 解释 FSharp.Core 为什么没有内置 `result` 或验证计算表达式；
- 把 `and!` 的行为视为某个特定计算表达式构建器的契约。

## 先问后续检查是否需要前一步的值 {#dependency-question}

选择语法之前，先画出数据依赖：

```text
原始座位文本 ──▶ 解析出正数 SeatCount ──▶ 与容量比较

原始请求标识 ──▶ 验证标识 ─────┐
原始参与者 ─────▶ 验证姓名 ─────┼──▶ 构造 ValidBooking
原始座位文本 ───▶ 验证座位数 ───┘
```

没有 `SeatCount` 就无法进行容量比较，因此它依赖解析与正数检查。三个字段分支都拥有原始请求，彼此不需要对方，因此相互独立。这里的“独立”是指数据需求独立，并不表示自动并行执行。

可以从下面的规则开始：

| 关系 | 组合策略 | 原因 |
|---|---|---|
| 后续步骤需要前一步成功 | 短路 | 失败后没有可供后续步骤使用的有效输入 |
| 检查查看手头已有的独立数据 | 当调用方需要全部失败时累积 | 每项检查都可以提供有用反馈 |
| 检查执行 I/O 或改变状态 | 放在显式的有副作用阶段 | 成本、故障、取消和陈旧性需要自己的策略 |

不要条件反射式地累积。命令行工具可能有意只报告第一个语法错误，安全边界也可能避免泄露多项细节。应根据消费者需求选择。

## 为每个成功字段赋予类型 {#model}

共享脚本把原始文本与成功检查后的值分开：

```fsharp:line-numbers [ch18-workflow-validation.fsx]
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

```fsharp:line-numbers [ch18-workflow-validation.fsx]
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
每个独立验证器目前要么产生一个值，要么产生只含一项的错误列表。列表给组合层提供统一的错误载体。从类型上说，它允许 `Error []`；本实现永远不产生该值。如果非空本身成为重要不变量，就用非空错误类型保护它，而不是依赖约定。

在 `validateSeats` 内部，整数解析必须先于正数比较。这两个检查相互依赖：解析失败时，没有整数可以比较。跨字段累积并不要求假装一个字段内部的所有操作也都彼此独立。

## `Result.bind` 保留第一个错误 {#first-error}

FSharp.Core 把 `Result.bind` 的核心行为定义为：

```fsharp
match input with
| Error error -> Error error
| Ok value -> binder value
```

在 `Error` 分支中不会调用 binder。错误类型保持不变；`bind` 没有追加错误的操作。

第一种策略嵌套了三个依赖的延续：

```fsharp:line-numbers [ch18-workflow-validation.fsx]
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
面对完全无效的请求，`validateRequestId` 返回 `Error [MissingRequestId]`。参与者和座位验证器位于成功延续内部，所以都不会运行，结果只含第一个错误。

当每一步都使用前一步的受保护输出时，这种行为是正确的。当产品策略只返回一条消息时，它同样有效。但仅仅重排 `Result.bind` 调用不会让它累积错误，只会改变哪个失败排在第一。

### 顺序成为可观察的策略 {#first-error-order}

当两个检查都可能失败时，首错组合会让它们的顺序对调用方可见。应把结构前提放在前面，并记录该策略。如果产品承诺了特定优先级，就不要只依赖碰巧方便的源码顺序。

短路还会避免不必要的工作，但这只是结果，并不意味着可以把副作用藏进验证器。第 20–23 章会明确处理时间、I/O、故障和取消。

## 累积先求值各项独立结果 {#accumulation}

累积策略会先求值三个字段函数，然后才判断是否能构造结果：

```fsharp:line-numbers [ch18-workflow-validation.fsx]
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

```text
[missing-request-id; missing-attendee; seats-not-integer:oops]
```

这就是验证错误累积：求值彼此独立的检查，并按显式顺序规则合并失败。普通 F# 求值顺序仍然是依次执行，但不会因为另一个字段失败而跳过任何检查。

应保持错误顺序确定。这里的请求标识、参与者、座位数顺序与输入布局一致。稳定顺序让测试、UI 焦点、日志和客户端行为可以预测。`Set` 会按比较规则去重并排序，而不是保持字段顺序；那会成为另一种契约。

### 累积不等于“任何失败后都继续” {#accumulation-limits}

只组合放在一起仍然有意义的错误。如果文档解码失败，就无法执行其缺失字段上的检查。如果没有经过认证的身份，授权决策就缺少主体。应先获得前提，再累积共同使用所得数据的检查。

同样，不要构造半有效领域记录再补丁更新。把成功的组件结果分别保留，按需收集错误，只在全 `Ok` 分支构造最终类型。

## 把组合规则提取成普通函数 {#reusable-accumulation}

显式三路匹配很容易审计。当这种模式重复出现时，只提取组合机制：

```fsharp:line-numbers [ch18-workflow-validation.fsx]
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

柯里化的 `createBooking` 从 `Ok` 内开始。每次应用提供一个独立计算出的组件。共享脚本断言，这次重构与显式匹配返回完全相同的错误、顺序和成功值。

函数先接收下一个值结果，使累积中的函数结果可以通过 `|>` 流动。这种参数顺序是局部的 F# API 选择，不是 applicative application 的通用命名。

对非常大的验证集合反复追加列表可能代价较高。小型固定表单的列表很短，应优先清楚。如果之后的分析测量发现真实的大规模成本，可以反向累积或有意使用非空结构；不要用推测性数据结构模糊三个字段。

## 依赖步骤应保持短路 {#dependent-workflow}

座位数一旦存在，与容量比较就是依赖性的业务检查：

```fsharp:line-numbers [ch18-workflow-validation.fsx]
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
`validateSeatsThenCapacity` 使用 `Result.bind`。它注入的容量函数只会收到有效 `SeatCount`。带仪表的包装函数统计调用次数，却没有把可变性放进任一正式函数：

| 输入 | 结果 | 容量检查次数 |
|---|---|---:|
| `"oops"` | `SeatsNotInteger "oops"` | 0 |
| 容量为 4 时输入 `"5"` | `ExceedsCapacity(5, 4)` | 1 |
| 容量为 4 时输入 `"3"` | `Ok(SeatCount 3)` | 1 |

零次是短路的直接证据，不是基于耗时的优化断言。计数只是测试仪表；正确性不能依赖它。

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

先累积手头已有的廉价确定性事实，再按显式策略执行必要的外部决策。最终写入仍必须原子地强制并发敏感规则；之前的验证查询不是锁。

这种分离也让测试诚实。纯累积只需要输入和预期值。有副作用的工作则需要端口、受控替身，以及后续的取消/资源测试。

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

同一组中的绑定不能相互依赖。绝不能脱离上下文把 `and!` 描述成“累积错误”或“并行运行”；应声明所用构建器及其 `MergeSources` 契约。当代码库反复出现已经证明的工作流时，自定义构建器可能非常好用，但它会引入另一套 API 与调试翻译。本章的普通函数仍是语义基线。

## 选择最小而诚实的组合 {#selection-rule}

| 需求 | 优先选择 |
|---|---|
| 下一步需要前一步的成功值 | `Result.bind` 或显式匹配 |
| 返回一个按优先级选择的失败 | 有顺序的首错组合 |
| 返回所有独立的纯输入错误 | 显式累积或小型、经过测试的 apply/map 函数 |
| 执行外部检查 | 更晚的显式有副作用工作流步骤 |
| 重复且稳定的语法，团队/库契约已经统一 | 有文档的计算表达式构建器 |
| 一次性的特殊组合 | 直接模式匹配，而不是新抽象 |

类型声明可能的结果；组合函数声明求值策略。两者都需要评审。

## 运行共享示例 {#run-example}

在示例所在目录运行：

```console
dotnet fsi --exec ch18-workflow-validation.fsx
```

七行确定性输出与断言证明首错结果、三项和两项错误累积、有效输入时两种策略一致，以及无效、超量和接受的座位文本各自对应的容量检查次数。请比较确切顺序。

## 练习 {#exercises}

### 练习 1：画出两个验证阶段 {#exercise-01}

某预约命令必须检查请求标识、参与者姓名、整数座位文本、容量，以及数据库中请求标识的唯一性。把每项检查分类为独立纯输入验证、依赖领域验证或有副作用的边界工作。

画出执行顺序，使有用的输入错误能一起报告，同时避免对结构上无效的输入查询数据库。说明哪里必须短路，哪里适合累积。

### 练习 2：实现有序累积 {#exercise-02}

为 `Result<'T, 'Error list>` 编写普通 `applyValidation`，再用柯里化构造函数验证姓名、电子邮件和座位数。当三者都失败时，错误必须按字段顺序出现。断言全有效输入能构造最终记录。

解释 `Error []` 表示什么，以及你的 API 是否应让该状态无法表示。

### 练习 3：审计计算表达式主张 {#exercise-03}

评审下面这段没有展示导入或构建器定义的代码：

```fsharp
result {
    let! requestId = validateRequestId raw.RequestId
    and! seats = validateSeats raw.Seats
    return requestId, seats
}
```

解释为什么只凭 FSharp.Core 不能断定它能编译或累积错误。说出与 `let!` 和 `and!` 相关的构建器操作，说明独立性限制，然后用错误策略明确的普通函数重写这两项检查。

[阅读本章答案](../solutions/ch-18-workflow-validation)。

## 模型回顾 {#model-review}

- 决定后续检查能否运行的是依赖关系，而不是视觉语法。
- `Result.bind` 会直接返回已有 `Error`，而不调用成功延续。
- 独立纯检查可以全部运行，并按有文档的顺序累积错误。
- 即使多个字段一起累积，一个字段内部仍可能包含依赖子步骤。
- 只在全部成功的分支中构造有效领域值。
- 让数据库、网络、时间和其他副作用离开纯输入累积。
- FSharp.Core 有 `Result` 及其组合函数，却没有内置 result 或验证计算表达式构建器。
- `and!` 表达独立绑定；其合并行为属于选定的构建器。
- 在自定义语法赢得成本之前，普通函数提供可读的语义基线。

## 第三部分检查点 {#part-checkpoint}

在示例所在目录运行聚焦工作流测试：

```console
dotnet test ExampleTests.fsproj --configuration Release --filter FullyQualifiedName~BookingWorkflowTests
```

测试通过表明：独立命令错误按字段顺序累积，有效命令产生事件，已有状态会使后续容量工作短路。它们直接调用普通函数，因此证据不依赖未声明的计算表达式构建器。

[继续阅读第 19 章](../part-04/ch-19-dotnet-null-boundaries)，让外部 .NET 值第一次穿过显式边界。

## 资料来源 {#sources}

- [Microsoft Learn：F# Result](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/results)
- [FSharp.Core：`Result` 模块](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-resultmodule.html)
- [Microsoft Learn：计算表达式与 `and!`](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/computation-expressions)
- [Microsoft Learn：F# 组件设计指南](https://learn.microsoft.com/en-us/dotnet/fsharp/style-guide/component-design-guidelines)
