---
title: "第 34 章：纯预约工作流与验证"
description: "推导一个纯预约决策器，累积独立字段错误，短路依赖状态的规则，并且只演化已接受的事实。"
translationKey: part-06/ch-34-pure-booking-workflow
---

# 第 34 章：纯预约工作流与验证 {#overview}

第 33 章定义了词汇，本章用一个函数把它们连接起来。工作流接收受保护的活动、当前预约状态和原始命令，并返回已接受事实或分类后的拒绝。所需事实全部通过参数传入；数据库、时钟、传输故障和通知仍由应用适配器负责。

关键在于把不同失败放在正确的位置。互相独立的字段错误一起报告更有用，因此验证会累积它们。依赖状态的决策在前置条件失败后就会停止，因为后续规则已没有有效输入。两类问题若共用一种组合策略，要么丢掉有用错误，要么产生误导性错误。

## 从左向右阅读契约 {#contract}

公共决策契约是：

```fsharp
Event
    -> BookingState
    -> BookingCommand
    -> Result<BookingEvent, BookingDecisionError>
```

每个位置的有效性不同：

| 位置 | 含义 | 进入时是否有效 |
|---|---|---|
| `Event` | 排期活动及其受保护容量 | 已由 `EventId` 与 `Capacity` 构造 |
| `BookingState` | 本次决策的当前预约视图 | 已是有效联合值，其中包含零个或一个受保护预约 |
| `BookingCommand` | 预约、确认或取消意图 | 案例已知，但记录字段仍是原始字符串和整数 |
| `Ok BookingEvent` | 领域规则已接受的事实 | 可以按应用策略演化并随后提交 |
| `Error BookingDecisionError` | 已分类的预期拒绝 | 没有产生已接受事实 |

柯里化允许应用预先传入稳定的活动或状态。更重要的是，参数顺序清楚表达流程：上下文在前，每次变化的命令在后。

“纯”意味着相同输入产生相同结果，求值不产生可观察的外部副作用。命令能否接受、计算成本、.NET 其他位置的异常行为以及并发提交是否安全，都是独立问题。

## 分开两种失败 {#two-failure-kinds}

先问两项检查能否只根据同一组原始值彼此独立地判断：

| 失败种类 | 示例 | 组合策略 | 原因 |
|---|---|---|---|
| 独立字段验证 | 请求标识为空且确认码为空 | 同时累积 | 修复一个字段并不能决定另一个字段 |
| 表示到领域的转换 | 座位数非正 | 与其他独立字段错误一起累积 | 不需要状态 |
| 状态查找 | 没有预约与已验证请求标识匹配 | 停止 | 没有预约就不能执行转换 |
| 生命周期规则 | 再次确认已经确认的预约 | 停止 | 后续成功依赖当前状态允许该动作 |
| 容量规则 | 有效预约请求超过受保护容量 | 停止 | 它依赖已验证座位数与活动 |
| 重复规则 | 给定状态已经由一个预约占用 | 在创建前停止 | 再创建预约没有意义 |

累积并不比短路“更函数式”，两者回答不同问题。能独立判断、同时报告的错误应当累积；如果早期失败会让后续输入不存在或判断失去意义，就按顺序绑定决策。

## 把原始记录变成受保护命令 {#validated-commands}

预约命令已经有私有的已验证形式。本章为确认与取消增加了对应形式：

```fsharp:line-numbers [Validation.fs]
type ValidConfirmBooking =
    private
        { RequestId: RequestId
          ConfirmationCode: ConfirmationCode }

module ValidConfirmBooking =
    let requestId (command: ValidConfirmBooking) = command.RequestId
    let confirmationCode (command: ValidConfirmBooking) = command.ConfirmationCode

type ValidCancelBooking =
    private
        { RequestId: RequestId
          Reason: CancellationReason }

module ValidCancelBooking =
    let requestId (command: ValidCancelBooking) = command.RequestId
    let reason (command: ValidCancelBooking) = command.Reason
```
原始记录使用 `string` 和 `int`，因为调用方或未来 DTO 从表示数据开始。已验证记录包含 `RequestId`、`SeatCount`、`ConfirmationCode` 或 `CancellationReason`。它们的记录构造器是私有的；调用方只能通过验证器获得，再通过模块函数观察。

这种拆分避开两个错误极端。把原始命令构造器设为私有，会迫使不可信边界假装字段已经有效；让已验证记录保持公开，则允许调用方绕过规范化与不变量。两个独立类型既能如实表示原始输入，也能保护验证后的内部数据。

已验证命令只保证字段有效。`ValidConfirmBooking` 的标识和代码非空且已规范化，但不保证预约存在或处于待确认状态。这些事实依赖状态，属于决策阶段。

## 有意累积独立错误 {#accumulation}

项目使用一个小型局部组合器，让验证策略直接可见：

```fsharp:line-numbers [Validation.fs]
let private applyValidation valueResult functionResult =
    match functionResult, valueResult with
    | Ok mapping, Ok value -> Ok(mapping value)
    | Error earlier, Error later -> Error(earlier @ later)
    | Error errors, Ok _
    | Ok _, Error errors -> Error errors

let private validateRequestId (raw: string) =
    RequestId.create raw
    |> Result.mapError (fun error -> [ InvalidRequestId error ])

let private validateSeatCount (raw: int) =
    SeatCount.create raw
    |> Result.mapError (fun error -> [ InvalidSeatCount error ])

let private validateConfirmationCode raw =
    ConfirmationCode.create raw
    |> Result.mapError (fun error -> [ InvalidConfirmationCode error ])

let private validateCancellationReason raw =
    CancellationReason.create raw
    |> Result.mapError (fun error -> [ InvalidCancellationReason error ])

let private createValidCommand (requestId: RequestId) (seats: SeatCount) : ValidPlaceBooking =
    { RequestId = requestId; Seats = seats }

let validatePlaceBooking (command: PlaceBookingCommand) =
    Ok createValidCommand
    |> applyValidation (validateRequestId command.RequestId)
    |> applyValidation (validateSeatCount command.Seats)
```
`applyValidation` 把包含构造函数的 `Result` 与一个已验证字段组合起来。它的四种情况已经穷尽：

- 两者成功时，把函数应用于值；
- 两者失败时，把较早错误列表与较晚列表拼接；
- 只有一方失败时，保留该错误列表；
- 所有字段成功前，不会构造受保护命令。

对于 `({ RequestId = " "; Seats = 0 } : PlaceBooking)`，求值过程如下：

```text
Ok createValidCommand
  + Error [InvalidRequestId BlankRequestId]
  = Error [InvalidRequestId BlankRequestId]
  + Error [InvalidSeatCount (NonPositiveSeatCount 0)]
  = Error [InvalidRequestId ...; InvalidSeatCount ...]
```

顺序稳定，是因为验证器按字段顺序应用，组合器使用 `earlier @ later`。稳定顺序让测试与面向用户的映射可预测；这并不声称某个无效字段更重要。

这些验证器既纯又廉价。即使累积中的函数结果已经是 `Error`，F# 仍会求值下一个验证器表达式，这正是收集错误所需的行为。不要把这种累积方式用于数据库调用、受速率限制的服务或破坏性操作：失败后仍执行全部操作可能代价高昂，甚至造成错误。

### 把同一策略扩展到生命周期命令 {#lifecycle-validation}

确认与取消各自按相同顺序验证两个独立字段：

```fsharp:line-numbers [Validation.fs]
let private createValidConfirmCommand requestId confirmationCode : ValidConfirmBooking =
    { RequestId = requestId
      ConfirmationCode = confirmationCode }

let validateConfirmBooking (command: ConfirmBooking) =
    Ok createValidConfirmCommand
    |> applyValidation (validateRequestId command.RequestId)
    |> applyValidation (validateConfirmationCode command.ConfirmationCode)

let private createValidCancelCommand requestId reason : ValidCancelBooking =
    { RequestId = requestId
      Reason = reason }

let validateCancelBooking (command: CancelBooking) =
    Ok createValidCancelCommand
    |> applyValidation (validateRequestId command.RequestId)
    |> applyValidation (validateCancellationReason command.Reason)
```
共享的 `CommandValidationError` 联合让统一决策器返回同一种错误列表，同时保留具体错误类型。确认验证器实际上不可能产生座位数错误；更宽的联合是统一命令级错误词汇的成本。测试固定每个验证器可能发出的案例。

不要把“预约存在”添加到这些函数里。那会要求状态，把纯字段转换改成业务决策，也让独立错误累积变得含糊。已验证命令就是两阶段之间的边界。

## 为工作流提供统一错误词汇 {#decision-errors}

决策器显式暴露预期拒绝类别：

```fsharp:line-numbers [Decider.fs]
[<RequireQualifiedAccess>]
type BookingDecisionError =
    | InvalidCommand of CommandValidationError list
    | BookingAlreadyExists of existingRequestId: RequestId
    | BookingDoesNotExist
    | BookingCreationFailed of BookingCreationError
    | BookingTransitionFailed of BookingTransitionError
```
每个案例保留其来源：

| 案例 | 来源 | 含义 |
|---|---|---|
| `InvalidCommand errors` | 验证 | 一个或多个原始字段格式错误 |
| `BookingAlreadyExists id` | 预约状态检查 | 给定状态已经包含预约 |
| `BookingDoesNotExist` | 确认/取消查找 | 给定状态中没有预约与已验证目标匹配 |
| `BookingCreationFailed error` | `Booking.create` | 有效预约意图违反容量等创建规则 |
| `BookingTransitionFailed error` | `Booking.confirm` 或 `Booking.cancel` | 受保护预约拒绝生命周期转换 |

一个联合让外层应用穷尽处理每种预期结果，但不会把所有问题都简化成字符串。详细领域错误仍可供后续 HTTP、日志或公共 API 转换使用。

这些案例是预期值，而不是异常。数据库超时、取消令牌、序列化器缺陷或程序员破坏不变量属于另一边界。纯函数不执行那些操作，因此没有理由捕获它们。

## 路由封闭的命令集合 {#routing}

统一函数对 `BookingCommand` 做穷尽匹配：

```fsharp:line-numbers [Decider.fs]
let decide
    (activity: Event)
    (state: BookingState)
    (command: BookingCommand)
    : Result<BookingEvent, BookingDecisionError> =
    match command with
    | BookingCommand.Place placeCommand ->
        decidePlaceBooking activity state placeCommand |> Result.mapError mapPlaceError
    | BookingCommand.Confirm confirmCommand ->
        validateConfirmBooking confirmCommand
        |> Result.mapError BookingDecisionError.InvalidCommand
        |> Result.bind (decideConfirm state)
    | BookingCommand.Cancel cancelCommand ->
        validateCancelBooking cancelCommand
        |> Result.mapError BookingDecisionError.InvalidCommand
        |> Result.bind (decideCancel state)
```
分支结构有意保持直白：

- 预约分支委托给已经测试的 `decidePlaceBooking`，并映射其错误联合；
- 确认分支验证原始字段，映射验证失败，再绑定状态决策；
- 取消分支用自己的已验证类型跨越同一阶段边界；
- 每个成功分支恰好返回一个 `BookingEvent`。

决策器不会重复预约容量规则。委托让 `Workflow.decidePlaceBooking` 成为这项规则的唯一实现，同时提供统一命令入口。同样，两个生命周期分支也不会自行检查状态。

因为命令是封闭的可辨识联合，增加一个案例会让这里的匹配不完整。在警告即错误设置下，构建会强制做出明确路由选择。通配符会丢弃这项维护信号，只应保留给真正开放或有意忽略的输入空间。

`BookingCommand` 上的 `[<RequireQualifiedAccess>]` 还要求调用处写成 `BookingCommand.Place`、`.Confirm` 或 `.Cancel`。多个联合都包含 `Cancel`、`Confirm` 等普通词时，限定名称会很有帮助。

## 短路依赖状态的决策 {#business-short-circuit}

字段验证后，确认与取消使用顺序式 `Result` 组合：

```fsharp:line-numbers [Decider.fs]
let private requireBooking requestId state =
    match state with
    | NotBooked -> Error BookingDecisionError.BookingDoesNotExist
    | Booked booking when Booking.requestId booking = requestId -> Ok booking
    | Booked _ -> Error BookingDecisionError.BookingDoesNotExist

let private decideConfirm state command =
    requireBooking (ValidConfirmBooking.requestId command) state
    |> Result.bind (fun booking ->
        Booking.confirm (ValidConfirmBooking.confirmationCode command) booking
        |> Result.map BookingConfirmed
        |> Result.mapError BookingDecisionError.BookingTransitionFailed)

let private decideCancel state command =
    requireBooking (ValidCancelBooking.requestId command) state
    |> Result.bind (fun booking ->
        Booking.cancel (ValidCancelBooking.reason command) booking
        |> Result.map BookingCancelled
        |> Result.mapError BookingDecisionError.BookingTransitionFailed)
```
`requireBooking` 必须先成功，转换才有输入。本示例在状态为 `NotBooked` 和所含请求标识不同时都返回 `BookingDoesNotExist`。只有匹配的受保护预约会进入下一步。

随后 `Result.bind` 恰好提供所需语义：`Error` 原样通过，不会求值绑定函数；`Ok booking` 则调用下一函数。确认调用 `Booking.confirm`，取消调用 `Booking.cancel`。只有这些领域函数能决定生命周期转换是否合法。

`Result.map` 把成功更新的预约包装成过去式事实。`Result.mapError` 把领域转换错误原样保存在工作流级 `BookingTransitionFailed` 案例下。两个组合器都不会再次执行转换或修改预约。

预约创建需要排期 `Event` 输入，因为它会检查容量；确认和取消只需要状态中的受保护预约。保持统一决策器签名能简化路由；它不能成为在没有相应规则的分支里添加虚假活动检查的理由。

## 保留预约优先级 {#placement-precedence}

较早的专用决策继续清晰可见，并保留为规则实现：

```fsharp:line-numbers [Workflow.fs]
type PlaceBookingError =
    | InvalidCommand of CommandValidationError list
    | BookingAlreadyExists of existingRequestId: RequestId
    | BookingCreationFailed of BookingCreationError

let decidePlaceBooking (event: Event) (state: BookingState) (command: PlaceBookingCommand) =
    match validatePlaceBooking command with
    | Error errors -> Error(InvalidCommand errors)
    | Ok validCommand ->
        match state with
        | Booked existing -> Error(BookingAlreadyExists(Booking.requestId existing))
        | NotBooked ->
            Booking.create event (ValidPlaceBooking.requestId validCommand) (ValidPlaceBooking.seats validCommand)
            |> Result.map BookingPlaced
            |> Result.mapError BookingCreationFailed
```
它的嵌套结构定义了可观察的优先级：

1. 验证请求标识与座位数，并累积两项独立失败。
2. 字段有效后检查状态。
3. 如果预约已经存在，返回 `BookingAlreadyExists`，不尝试创建。
4. 只有 `NotBooked` 才调用 `Booking.create` 并执行容量限制。
5. 把创建出的预约包装成 `BookingPlaced`。

因此，对已有状态提交无效且超量的命令会先报告字段错误；有效但超量的命令对已有状态会先报告重复；同一项有效超量命令对 `NotBooked` 则报告容量。这些不是偶然实现细节，而是由测试固定的决策策略；只有业务理由明确变化时才应改变。

试图在这里“收集全部业务错误”会产生可疑输出。预约已经存在时，就没有一个应继续诊断创建过程的新预约；请求标识无效时，查找该目标也没有意义。顺序结构会阻止这些虚构组合。

## 只演化已接受的事实 {#evolution}

决策与演化继续保持为两个函数：

```fsharp:line-numbers [Workflow.fs]
let evolve (_: BookingState) (event: BookingEvent) =
    match event with
    | BookingPlaced booking
    | BookingConfirmed booking
    | BookingCancelled booking -> Booked booking
```
`decide` 回答命令是否可以产生事实；`evolve` 回答已经接受该事实后是什么状态。应用按此顺序使用它们：

```fsharp
match Decider.decide activity state command with
| Error error -> Error error
| Ok bookingEvent -> Ok(Workflow.evolve state bookingEvent)
```

真实应用通常要先提交已接受事件或下一状态 DTO，再报告成功。该副作用属于此表达式之外。如果提交失败，领域决策不会因此变得无效；应用遇到的是运维失败，必须按其原子性、重试与幂等契约处理。

当前事件携带完整的结果预约，所以 `evolve` 只需直接转换。这项设计不要求事件溯源，也不能证明任意重放的公开事件值都来自决策器。它只是为纯工作流提供一条明确的已接受事实边界。

## 让每项规则只有一个实现 {#rule-ownership}

列出每项规则的唯一实现，更容易审查：

| 规则 | 唯一实现 | 被谁复用 |
|---|---|---|
| 规范化并拒绝空白标识、代码与原因 | 各自的智能构造模块 | 所有命令验证器与公共投影 |
| 累积独立命令字段 | `Validation.applyValidation` 与各验证器声明的顺序 | 预约、确认、取消验证 |
| 拒绝非正座位数 | `SeatCount.create` | 预约验证 |
| 拒绝大于活动容量的预约 | `Booking.create` | 专用与统一预约路径 |
| 拒绝向已占用状态预约 | `Workflow.decidePlaceBooking` | 统一决策器与早期公共工作流 |
| 要求匹配的预约目标 | `Decider.requireBooking` | 确认与取消分支 |
| 允许从某状态确认或取消 | `Booking.confirm` 与 `Booking.cancel` | 决策器与公共 API |
| 把已接受预约事实投影成状态 | `Workflow.evolve` | 测试与应用编排 |

“一个实现”并不表示一个巨型函数，而是其他层调用规则，不再重写条件。把错误映射进更宽的联合不是复制规则；在两个模块里都检查 `requested > capacity` 才是。

## 在没有副作用的情况下测试行为 {#testing}

聚焦工作流测试只调用普通值与函数。决策器没有外部依赖，因此不需要 mock 框架。测试覆盖：

- 预约、确认与取消都会按顺序累积各自独立的格式错误字段；
- 无效生命周期字段优先于缺失状态检查；
- 有效预约发出 `BookingPlaced` 并演化为 `Booked`；
- 容量拒绝保留带度量单位的具体领域错误；
- 已占用状态优先于后续预约容量检查；
- 有效确认规范化代码并发出 `BookingConfirmed`；
- 目标状态不存在或不匹配时返回 `BookingDoesNotExist`；
- 第二次确认保留 `CannotConfirmFrom`；
- 取消发出经过规范化的终态事实；
- 重复取消保留 `CannotCancelFrom`。

范围更广的领域、工作流、属性和决策器测试也会通过。完整示例检查会按锁文件还原依赖，在启用空值检查和警告即错误的条件下构建 Release，运行所有测试与脚本，并核对预期的编译器诊断。

这些结果表明，已覆盖的模型决策是确定的。它们不能说明多个预约不会共同超出活动容量、状态加载始终一致，或事实恰好提交一次。这些保证需要原子持久化和集成测试。

## 避免看似简单却改变含义的做法 {#false-simplifications}

- 用 `Result.bind` 连接独立字段验证器只会报告第一项错误；只有契约本就要求快速失败时才这样做。
- 累积依赖状态的拒绝，可能报告从未被有意义地判断过的条件。
- 在 `Decider` 中重新检查状态或容量，会复制受保护领域规则。
- 对普通无效输入抛异常，会让类型隐藏预期结果。
- 在 `decide` 内读取存储库，会让可重复性与并发策略变得隐式。
- 在这项设计中只返回新状态而不返回已接受事实，会丢失有用的应用边界。
- 同时返回事件和另行计算的状态可能产生分歧；应使用 `evolve` 导出状态。
- 把函数称作纯函数，不会让后续“加载—决策—提交”序列自动具备原子性。

## 练习 {#exercises}

### 练习 1：推导错误优先级 {#exercise-01}

运行测试前，预测以下输入对应的具体 `BookingDecisionError`：

1. 对 `NotBooked` 提交空白标识和零座位；
2. 对容量为四的活动和 `NotBooked` 提交有效五座请求；
3. 对 `Booked existing` 提交同一项有效请求；
4. 对 `NotBooked` 提交空白标识与空白确认码；
5. 对已确认预约提交有效确认。

逐项指出求值跳过了哪些规则。

### 练习 2：增加第三个独立字段 {#exercise-02}

设想预约还接收 `AttendeeEmail: string`，并有受保护的 `EmailAddress` 智能构造函数。为请求标识、电子邮件和座位数勾画 `ValidPlaceBooking` 与 `validatePlaceBooking`。保持字段顺序累积，并解释为什么活动剩余容量仍不属于这个验证器。

### 练习 3：规定取消优先级 {#exercise-03}

考虑一个已取消预约和三项取消命令：空白标识加空白原因、有效但不同的标识，以及正确标识加有效新原因。说明当前策略对每项命令的结果。再提出一种替代优先级策略、它在用户体验或安全方面的动机，以及需要改变的测试与公共契约。

[阅读本章练习答案](../solutions/ch-34-pure-booking-workflow)。

## 资料来源 {#sources}

- [Microsoft Learn：F# `Result` 类型](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/results)
- [FSharp.Core 参考：`Result.bind`、`map` 与 `mapError`](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-resultmodule.html)
- [Microsoft Learn：匹配表达式、守卫与穷尽性](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/match-expressions)
- [Microsoft Learn：可辨识联合与具名案例](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/discriminated-unions)
- [Microsoft Learn：记录、不可变性与私有构造](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/records)
