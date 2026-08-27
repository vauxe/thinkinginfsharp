---
title: "第 20 章：函数式核心与副作用边界"
description: "把时间、随机数和环境访问变成可见的数据或函数依赖，让领域决策可重放，并明确副作用策略。"
translationKey: part-04/ch-20-functional-core-effects
---

# 第 20 章：函数式核心与副作用边界 {#overview}

一个函数即使没有可见形参，也仍可能依赖外部世界。`DateTimeOffset.UtcNow`、随机抽取和环境查询都会取得实参中没有的信息。若在定价或预约规则深处调用它们，表面相同的输入就可能产生不同结果，测试也不得不控制进程全局状态。

F# 函数本身就是值，因此改法可以很小：读取一次外部信息并把结果作为数据传入，或者传入一个只执行所需操作的函数。这样，领域核心会通过普通实参接收全部必要信息。当一组相关操作或生命周期需要对象时，仍然可以使用对象与接口。

## 隐藏输入仍然是输入 {#hidden-input}

设想一个函数的可见形参是请求，但其函数体读取当前时间、选择随机数并读取 `BOOKING_REGION`。它的真实输入更接近：

```text
(请求, 当前时间, 随机源状态, 进程环境)
```

从类型中省略后三项并不会移除它们，只会让其取得时机、失败行为与测试控制变得隐含。

副作用是不能只由返回值描述的可观察行为。读取时间或环境未必修改代码中的数据，但再次求值可能得到不同结果；随机生成也会推进来源状态。控制台输出、I/O 与共享可变性是更明显的例子。

目标不是“禁止副作用”。有用程序必须与运行时交互；关键是缩小交互位置，让纯决策可以独立理解和重放。

## 先让决策成为值变换 {#pure-core}

示例只建模活动决策所需的事实：

```fsharp:line-numbers
type Campaign =
    { OpensAt: DateTimeOffset
      ClosesAt: DateTimeOffset
      CodePrefix: string
      DefaultRegion: string }

type Candidate =
    { SubmittedAt: DateTimeOffset
      Draw: int
      Region: string }

type Decision =
    | NotOpen
    | Closed
    | Accepted of code: string
```
`Campaign` 包含策略。`Candidate` 包含已为一次尝试捕获的观察。`Decision` 命名每个纯结果。决策函数因而很直接：

```fsharp:line-numbers
let decide campaign candidate =
    if candidate.SubmittedAt < campaign.OpensAt then
        NotOpen
    elif candidate.SubmittedAt >= campaign.ClosesAt then
        Closed
    else
        let suffix = candidate.Draw.ToString("D4")
        Accepted $"{campaign.CodePrefix}-{candidate.Region}-{suffix}"
```
`decide` 不会询问现在几点，而是把 `Candidate.SubmittedAt` 与所提供的窗口比较。它不会生成后缀或发现区域，因为这些值已经存在。给定相同的两个记录，它会返回相同联合用例，且不执行外部工作。

把时间作为数据传入还声明了快照语义。该决策中的每次比较都使用同一个已捕获时刻。若多次调用 `UtcNow`，一个逻辑决策执行到一半时就可能跨过窗口边界。

纯净性取决于实现及其依赖，不取决于 `let` 关键字或函数类型。传入的函数仍可能执行 I/O 或修改状态。

## 在简短的编排函数中读取外部信息 {#capture-effects}

编排函数接收一个包含三个具名函数值的记录：

```fsharp:line-numbers
type RuntimeEffects =
    { UtcNow: unit -> DateTimeOffset
      NextInt: int -> int
      ReadSetting: string -> string option }

let private normalizedRegion (fallback: string) (value: string option) =
    value
    |> Option.map (fun text -> text.Trim())
    |> Option.filter (String.IsNullOrWhiteSpace >> not)
    |> Option.defaultValue fallback

let captureCandidate campaign effects =
    let submittedAt = effects.UtcNow()
    let draw = effects.NextInt 10_000

    if draw < 0 || draw >= 10_000 then
        invalidArg (nameof effects) "NextInt returned a value outside its requested range."

    let region =
        effects.ReadSetting "BOOKING_REGION" |> normalizedRegion campaign.DefaultRegion

    { SubmittedAt = submittedAt
      Draw = draw
      Region = region }
```
数据流如下：

```text
UtcNow ───────────────┐
NextInt ──────────────┼──▶ captureCandidate ──▶ Candidate ──▶ decide
ReadSetting ──────────┘        副作用                 数据        纯
```

`captureCandidate` 按可见顺序调用每项依赖，检查随机数提供者承诺的范围，规范化可选设置，再为核心构造数据。把有副作用的函数作为形参传入，并不会让它变纯；副作用正是在这一编排层发生。

提供者必须遵守 `10_000` 上界。正式的 `Random.Next(10_000)` 会满足要求；若错误替身返回 `10_000`，代码会立即拒绝，而不会产生格式错误的代码。这项检查针对适配器，不是返回给最终用户的领域拒绝。

这里适合使用记录，因为一个小型内部编排器同时需要三项独立能力。不要把它传入每个领域函数。如果这个捆绑每逢任何代码需要新服务就增长，它就会变成依赖图不清楚的服务定位器；应按工作流拆分。

## 把真实运行时访问留在应用组装位置 {#system-adapter}

系统适配器很小：

```fsharp:line-numbers
let systemEffects (random: Random) =
    { UtcNow = fun () -> DateTimeOffset.UtcNow
      NextInt = fun upperExclusive -> random.Next upperExclusive
      ReadSetting = fun name -> Environment.GetEnvironmentVariable name |> Option.ofObj }
```
构造该记录不会读取时间或环境。每个闭包只在 `captureCandidate` 调用它时执行操作：

- `DateTimeOffset.UtcNow` 读取当前 UTC 时刻；
- 传入的 `Random` 实例保存随机源状态，并实现有界抽取；
- `Environment.GetEnvironmentVariable` 读取当前进程环境，并可能返回 null；
- 返回的 null 按第 19 章规则立即用 `Option.ofObj` 转换。

调用方创建并保留 `Random` 实例。每次抽取都新建带种子的生成器会意外改变统计行为；共享可变随机状态则会引出第 24 章讨论的并发问题。`System.Random` 也不适合生成安全敏感令牌，此时应使用密码学随机数 API。依赖传入位置会显示实例生命周期与算法选择，但不存在普遍正确的生命周期。

只有应用组合代码应该知道 `systemEffects`。领域文件不应打开 `System.Environment` 或读取时钟。若只是把全局调用搬进名为 `Clock.now` 的帮助函数，却仍不传递该帮助函数，隐藏依赖依然存在。

## 闭包构造已配置的函数 {#closures}

闭包是函数值与它从定义范围捕获的值。确定性提供者是几个很小的闭包：

```fsharp:line-numbers
let fixedClock instant = fun () -> instant

let fixedDraw draw =
    fun upperExclusive ->
        if draw < 0 || draw >= upperExclusive then
            invalidArg (nameof draw) "Fixed draw is outside the requested range."

        draw

let settingsFrom values = fun name -> Map.tryFind name values
```
`fixedClock instant` 返回一个记住 `instant` 的 `unit -> DateTimeOffset` 函数。`fixedDraw draw` 记住所选抽取值，但仍验证调用方要求的范围。`settingsFrom values` 记住不可变映射表。

这些闭包是纯的，因为捕获值不可变，函数体也不产生副作用。闭包也可以捕获可变计数器、数据库客户端或随机生成器；调用这种闭包便会产生副作用。“闭包”只描述如何保留上下文，不保证纯净。

闭包尤其适合部分配置：

```fsharp
let campaignSettings = settingsFrom (Map [ "BOOKING_REGION", "eu-west" ])
```

结果只公开调用方需要的操作，不会暴露映射表，也无需新建具名类型。

## 选择最能表达意图的依赖形式 {#dependency-shapes}

多种表示都可能有效。应按调用方需求、生命周期和受众选择：

| 形式 | 何时优先选择 | 需要留意 |
|---|---|---|
| `DateTimeOffset` 等普通数据 | 整项决策应始终使用一次观察 | 在正确时刻捕获 |
| `unit -> DateTimeOffset` 等单个函数形参 | 一项局部操作就是全部能力 | 该函数仍可能抛出或产生副作用 |
| 闭包 | 函数应保留配置、客户端或只在内部使用的状态 | 捕获的生命周期与可变性仍然真实存在 |
| 小型函数记录 | 一个内部 F# 编排步骤同时需要若干具名能力 | 避免不断增长的服务定位器 |
| 接口 | 操作组成一个连贯稳定组件，需要对象生命周期/状态、框架 DI 或便于 .NET 使用的公共 API | 不要只为模拟而创建宽接口 |

### 函数形参还是闭包 {#function-or-closure}

函数形参描述一个消费者需要什么：

```fsharp
let captureInstant (utcNow: unit -> DateTimeOffset) =
    utcNow ()
```

闭包提供该函数的一种已配置实现。两个概念互补：形参说明调用方需要的函数类型，闭包则是其中一种实现。

若不需要随后再次读取，应优先使用普通值。`decide campaign candidate` 比向 `decide` 传入时钟更强，因为它的类型证明核心无法决定读取两次时间。

### 操作属于一体时使用接口 {#interface-choice}

当多项操作属于一个抽象，或对象必须携带身份、状态或生命周期时，接口很有用：

```fsharp
type IClock =
    abstract UtcNow: unit -> DateTimeOffset
```

宿主的依赖注入约定或跨语言公共 API 可能值得使用单成员接口。在小型纯 F# 算法内部，`unit -> DateTimeOffset` 通常更简洁。对于包含相关序列化与反序列化操作的序列化器，或必须释放的客户端，接口比几个无关函数实参更能表达一个连贯组件。

任何一种形式都不会决定失败策略。函数或接口成员可以返回普通值、`option`、`Result` 或 `Task`，也可能抛出异常。返回类型与异常策略要另行选择。

## 确定性测试观察调用，而不是猜测等待时长 {#deterministic-tests}

脚本使用固定依赖，并且只把可变 `ResizeArray` 用作测试仪表：

```fsharp:line-numbers
let instant = DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero)

let campaign =
    { OpensAt = instant.AddHours(-1.0)
      ClosesAt = instant.AddHours(1.0)
      CodePrefix = "BOOK"
      DefaultRegion = "global" }

let calls = ResizeArray<string>()

let observedEffects =
    { UtcNow =
        fun () ->
            calls.Add "clock"
            instant
      NextInt =
        fun upperExclusive ->
            calls.Add $"random:{upperExclusive}"
            7
      ReadSetting =
        fun name ->
            calls.Add $"environment:{name}"
            Some " eu-west " }

let candidate = captureCandidate campaign observedEffects
let firstDecision = decide campaign candidate
let replayedDecision = decide campaign candidate

let expectedCalls = [ "clock"; "random:10000"; "environment:BOOKING_REGION" ]

assert (candidate.SubmittedAt = instant)
assert (candidate.Draw = 7)
assert (candidate.Region = "eu-west")
assert (firstDecision = Accepted "BOOK-eu-west-0007")
assert (replayedDecision = firstDecision)
assert (List.ofSeq calls = expectedCalls)

let fallbackEffects =
    { UtcNow = fixedClock instant
      NextInt = fixedDraw 42
      ReadSetting = settingsFrom Map.empty }

let fallbackDecision =
    fallbackEffects |> captureCandidate campaign |> decide campaign

assert (fallbackDecision = Accepted "BOOK-global-0042")

let earlyDecision =
    decide
        campaign
        { candidate with
            SubmittedAt = campaign.OpensAt.AddTicks(-1L) }

let closedDecision =
    decide
        campaign
        { candidate with
            SubmittedAt = campaign.ClosesAt }

assert (earlyDecision = NotOpen)
assert (closedDecision = Closed)
```
断言验证：

- 捕获的时刻、抽取值和去除空白后的区域正是所提供的值；
- 三个依赖函数按文档顺序各调用一次；
- 在已捕获数据上重放纯核心会产生相同决策，且不会增加依赖调用；
- 缺失设置使用活动中配置的后备值；
- 根据代码中的比较，开放时刻包含在内，关闭时刻排除在外。

测试不会 sleep、修改进程环境，也不会猜测某个种子会让 `Random` 返回什么。种子可以让某种实现可重现，但断言框架生成的具体序列会让领域测试耦合到领域并未定义的算法。固定函数直接表达真实要求：返回这个范围内的抽取值。

## 有副作用的依赖仍需失败策略 {#failure-contracts}

让有副作用的依赖可见，并不会说明失败时会发生什么。`Environment.GetEnvironmentVariable` 可能返回 null，也可能抛出文档列出的异常。时钟提供者或远程客户端可能抛出，测试替身也可能返回范围外的值。

应使用最小且准确的返回类型。可选配置可以返回 `option`；必填配置可以返回 `Result`；意外运行时故障可以保留为异常，直到某一层能补充上下文。不要在识别预期失败之前，把每项能力都变成 `unit -> Result<_, string>`。第 21 章会展开异常与资源策略。

同样，不要仅仅因为某项外部操作已作为参数传入，就反复调用它。应判断工作流需要一次快照、每一步重新读取，还是持续接收变化，并用函数调用的位置表达这一选择。

## 练习 {#exercises}

### 练习 1：暴露三个隐藏输入 {#exercise-01}

重构一个在函数体中读取 `DateTimeOffset.UtcNow`、调用 `Random.Next(100)` 并读取 `OFFER_REGION` 的函数。把它拆成接收已捕获事实记录的纯 `decideOffer`，以及接收窄依赖的编排函数。

编写提供固定时刻、抽取值和区域的确定性测试。证明调用纯函数两次不会再次调用任何依赖。

### 练习 2：选择数据、函数、闭包或接口 {#exercise-02}

为每种情况选择依赖形式并说明理由：

1. 一次到期比较必须始终使用同一时刻；
2. 局部重试策略需要在每次失败后请求新的延迟；
3. 已配置格式化器需要不可变区域性与前缀；
4. 跨语言存储客户端包含相关读写操作，并管理必须释放的连接；
5. 一个内部工作流需要时钟、随机抽取和设置查询，但没有任何领域函数同时需要三者。

即使选择的类型是函数，也要说明生命周期与失败行为。

### 练习 3：让适配器失败可见 {#exercise-03}

修改设置查询，让缺失的 `BOOKING_REGION` 成为错误，而不是使用后备值。定义专门的错误联合，并让捕获步骤返回 `Result<Candidate, CaptureError>`。

确保设置缺失可以与随机数提供者返回范围外数值区分。判断后者应保留为异常还是变成错误案例，并根据谁能够恢复来说明理由。

[阅读本章答案](../solutions/ch-20-functional-core-effects)。

下一章会保留同一个函数式核心，并在编排层加入异常、可释放资源与文件 I/O。

## 资料来源 {#sources}

- [Microsoft Learn：F# 函数](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/functions/)
- [Microsoft Learn：F# 组件设计指南](https://learn.microsoft.com/en-us/dotnet/fsharp/style-guide/component-design-guidelines)
- [Microsoft Learn：`DateTimeOffset.UtcNow`](https://learn.microsoft.com/en-us/dotnet/api/system.datetimeoffset.utcnow?view=net-10.0)
- [Microsoft Learn：`Random.Next`](https://learn.microsoft.com/en-us/dotnet/api/system.random.next?view=net-10.0)
- [Microsoft Learn：`Environment.GetEnvironmentVariable`](https://learn.microsoft.com/en-us/dotnet/api/system.environment.getenvironmentvariable?view=net-10.0)
