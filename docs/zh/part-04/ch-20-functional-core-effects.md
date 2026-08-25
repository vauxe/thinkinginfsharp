---
title: "第 20 章：函数式核心与副作用边界"
description: "把时间、随机数和环境访问变成显式值或函数依赖，让领域决策可重放，并让效果策略清晰可见。"
translationKey: part-04/ch-20-functional-core-effects
---

# 第 20 章：函数式核心与副作用边界 {#overview}

一个函数即使没有显式输入，也仍可能依赖外部世界。`DateTimeOffset.UtcNow`、随机抽取和环境查询都会取得函数实参中不存在的信息。若在定价或预约规则深处调用它们，表面上相同的输入就可能产生不同结果，测试也不得不控制进程全局状态。

F# 函数本身就是值，因此修复可以很小。读取一次效果并把结果作为数据传入，或者传入一个执行效果的窄函数。这样，领域核心就会通过普通实参接收所需的全部事实。当一组连贯组件契约或生命周期需要对象时，仍然可以使用对象与接口。

## 学完后你能够做什么 {#outcomes}

学完本章，你应该能够：

- 把时间、随机数、环境访问和可变性识别为可观察依赖；
- 区分“读取外部世界”的编排与纯领域决策；
- 当一项决策需要一致快照时，把一次观察捕获成数据；
- 当一项操作就是全部所需能力时，传递函数值；
- 在不创建全局服务定位器的前提下捆绑少量内部能力；
- 用闭包预先配置依赖，或保留刻意私有的状态；
- 为连贯且稳定的操作集合、对象生命周期或面向 .NET 的边界选择接口；
- 构造真实系统适配器，同时不让它泄漏到核心；
- 用固定值测试行为与调用顺序，而不使用 sleep 或进程全局变更；
- 解释为什么依赖注入会让效果可见，却不会让它变纯。

## 隐藏输入仍然是输入 {#hidden-input}

设想一个函数的可见形参是请求，但其函数体读取当前时间、选择随机数并读取 `BOOKING_REGION`。它的真实输入更接近：

```text
(请求, 当前时间, 随机源状态, 进程环境)
```

从类型中省略后三项并不会移除它们，只会让其取得时机、失败行为与测试控制变得隐含。

效果是在返回值之外仍可被观察的行为。读取时间或环境未必会改变代码中的数据，但另一次求值可能观察到不同结果。随机生成也会推进来源状态。控制台输出、I/O 与共享可变性是更明显的例子，后续章节会处理它们。

目标并不是“禁止效果”。有用程序必须与运行时交互。目标是让交互点足够小，使纯决策可以独立理解和重放。

## 先让决策成为值变换 {#pure-core}

示例只建模活动决策所需的事实：

```fsharp:line-numbers [ch20-functional-core-effects.fsx]
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

```fsharp:line-numbers [ch20-functional-core-effects.fsx]
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

纯净性是这项实现及其依赖的性质，不是 `let` 关键字或函数形状类型的性质。传入的函数仍然可能执行 I/O 或修改状态。

## 在薄编排步骤中捕获效果 {#capture-effects}

编排契约是一个包含三个具名函数值的记录：

```fsharp:line-numbers [ch20-functional-core-effects.fsx]
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
其形状为：

```text
UtcNow ───────────────┐
NextInt ──────────────┼──▶ captureCandidate ──▶ Candidate ──▶ decide
ReadSetting ──────────┘          效果                 数据        纯
```

`captureCandidate` 按可见顺序调用每项能力，验证随机数提供者承诺的范围，规范化可选设置，再为核心构造数据。该函数不会因为效果以形参形式到达就变纯；它正是显式效果边界。

`10_000` 上界属于调用契约。正式的 `Random.Next(10_000)` 会遵守它。若错误替身返回 `10_000`，代码会立即拒绝，而不是产生格式错误的代码。这项守卫保护适配器契约，不是返回给最终用户的领域拒绝。

这里适合使用记录，因为一个小型内部编排器同时需要三项独立能力。不要把它传入每个领域函数。如果这个捆绑每逢任何代码需要新服务就增长，它就会变成依赖图不清楚的服务定位器；应按工作流拆分。

## 把真实运行时访问留在组合边界 {#system-adapter}

系统适配器很小：

```fsharp:line-numbers [ch20-functional-core-effects.fsx]
let systemEffects (random: Random) =
    { UtcNow = fun () -> DateTimeOffset.UtcNow
      NextInt = fun upperExclusive -> random.Next upperExclusive
      ReadSetting = fun name -> Environment.GetEnvironmentVariable name |> Option.ofObj }
```
构造该记录不会读取时间或环境。每个闭包只在 `captureCandidate` 调用它时执行操作：

- `DateTimeOffset.UtcNow` 读取当前 UTC 时刻；
- 传入的 `Random` 实例拥有随机源状态，并实现有界抽取；
- `Environment.GetEnvironmentVariable` 读取当前进程环境且可能返回 null，并按第 19 章的规则立即用 `Option.ofObj` 转换。

调用方创建并拥有 `Random` 实例。每次抽取都新建带种子的生成器会意外改变统计行为；共享可变随机状态则会引出第 24 章才处理的并发问题。`System.Random` 也不适合生成安全敏感令牌；该需求应使用密码学随机数 API。本章边界会让所有权与算法选择可见，但不会宣称某一种生命周期普遍正确。

只有应用组合代码应该知道 `systemEffects`。领域文件不应打开 `System.Environment` 或读取时钟。若只是把全局调用搬进名为 `Clock.now` 的帮助函数，却仍不传递该帮助函数，隐藏依赖依然存在。

## 闭包构造已配置的函数 {#closures}

闭包是函数值与它从定义范围捕获的值。确定性提供者是几个很小的闭包：

```fsharp:line-numbers [ch20-functional-core-effects.fsx]
let fixedClock instant = fun () -> instant

let fixedDraw draw =
    fun upperExclusive ->
        if draw < 0 || draw >= upperExclusive then
            invalidArg (nameof draw) "Fixed draw is outside the requested range."

        draw

let settingsFrom values = fun name -> Map.tryFind name values
```
`fixedClock instant` 返回一个记住 `instant` 的 `unit -> DateTimeOffset` 函数。`fixedDraw draw` 记住所选抽取值，但仍验证调用方要求的范围。`settingsFrom values` 记住不可变映射表。

这些闭包是纯的，因为其捕获值不可变，函数体也不执行效果。闭包也可以捕获可变计数器、数据库客户端或随机生成器；那样调用它就会有副作用。“闭包”描述上下文如何保留，不是纯净保证。

闭包尤其适合部分配置：

```fsharp
let campaignSettings = settingsFrom (Map [ "BOOKING_REGION", "eu-west" ])
```

结果只拥有消费者需要的操作。它不会暴露映射表，也不要求新建具名类型。

## 选择能诚实表达意图的最小依赖形状 {#dependency-shapes}

多种表示都可能有效。应按消费者需要、所有权与受众选择：

| 形状 | 何时优先选择 | 需要留意 |
|---|---|---|
| `DateTimeOffset` 等普通数据 | 整项决策应始终使用一次观察 | 在正确时刻捕获 |
| `unit -> DateTimeOffset` 等单个函数形参 | 一项局部操作就是全部能力 | 该函数仍可能抛出或执行效果 |
| 闭包 | 函数应保留配置、客户端或刻意私有的状态 | 捕获的生命周期与可变性仍然真实存在 |
| 小型函数记录 | 一个内部 F# 编排步骤同时需要若干具名能力 | 避免不断增长的服务定位器 |
| 接口 | 操作组成一个连贯稳定组件，需要对象生命周期/状态、框架 DI 或便于 .NET 使用的公开契约 | 不要只为模拟而创建宽接口 |

### 函数形参还是闭包 {#function-or-closure}

函数形参描述一个消费者需要什么：

```fsharp
let captureInstant (utcNow: unit -> DateTimeOffset) =
    utcNow ()
```

闭包提供该函数的一种已配置实现。两个概念互补：形参是消费者契约；闭包是一种可能的提供者。

若不需要随后再次读取，应优先使用普通值。`decide campaign candidate` 比向 `decide` 传入时钟更强，因为它的类型证明核心无法决定读取两次时间。

### 操作属于一体时使用接口 {#interface-choice}

当多项操作属于一个抽象，或对象必须携带身份、状态或生命周期时，接口很有用：

```fsharp
type IClock =
    abstract UtcNow: unit -> DateTimeOffset
```

宿主的依赖注入约定或跨语言公开 API 可能值得使用单成员接口。在小型纯 F# 算法内部，`unit -> DateTimeOffset` 通常更简洁。对于包含相关序列化/反序列化操作的序列化器，或必须释放的客户端，接口可以比无关函数实参表达更连贯的组件边界。

任何一种形状都不会决定失败策略。函数或接口成员可以返回普通值、`option`、`Result`、`Task`，也可能抛出。应另外选择返回契约。

## 确定性测试观察调用，而不是猜测等待时长 {#deterministic-tests}

脚本使用固定依赖，并且只把可变 `ResizeArray` 用作测试仪表：

```fsharp:line-numbers [ch20-functional-core-effects.fsx]
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
断言证明：

- 捕获的时刻、抽取值和去除空白后的区域正是所提供的值；
- 三个效果函数按文档顺序各调用一次；
- 在已捕获数据上重放纯核心会产生相同决策，且不会增加效果调用；
- 缺失设置使用活动的显式后备值；
- 根据代码中的比较，开放时刻包含在内，关闭时刻排除在外。

测试不会 sleep、修改进程环境，也不会猜测某个种子会让 `Random` 返回什么。种子可以让一种实现可重现，但测试框架生成的精确序列可能让领域测试耦合到自己并不拥有的算法。固定函数直接表达真实契约：返回这个范围内的抽取值。

## 运行共享示例 {#run-example}

在示例所在目录运行：

```console
dotnet fsi --exec ch20-functional-core-effects.fsx
```

六行确定性输出会报告捕获快照、接受代码、后备区域、窗口边界、确切效果顺序和重放结果。请比较其顺序与文本。

## 效果仍然需要失败契约 {#failure-contracts}

让效果显式并不会说明它失败时会发生什么。`Environment.GetEnvironmentVariable` 可能返回 null，并有文档列出的异常。时钟提供者或远程客户端可能抛出。测试替身也可能违反范围契约。

应使用最小且准确的返回类型。可选配置可以返回 `option`；必填配置可以返回 `Result`；意外运行时故障可以保留为异常，直到边界能加入上下文。不要在未识别预期失败前，把每项能力都变成 `unit -> Result<_, string>`。第 21 章会展开异常与资源策略。

同样，不要仅仅因为某项效果已被注入就反复调用它。应判断一个工作流需要一次快照、每步骤刷新读取，还是变化中的观察流，并通过调用函数的位置编码该选择。

## 练习 {#exercises}

### 练习 1：暴露三个隐藏输入 {#exercise-01}

重构一个在函数体中读取 `DateTimeOffset.UtcNow`、调用 `Random.Next(100)` 并读取 `OFFER_REGION` 的函数。把它拆成接收已捕获事实记录的纯 `decideOffer`，以及接收窄依赖的编排函数。

编写提供固定时刻、抽取值和区域的确定性测试。证明调用纯函数两次不会再次调用任何依赖。

### 练习 2：选择数据、函数、闭包或接口 {#exercise-02}

为每种情况选择依赖形状并说明理由：

1. 一次到期比较必须始终使用同一时刻；
2. 局部重试策略需要在每次失败后请求新的延迟；
3. 已配置格式化器需要不可变区域性与前缀；
4. 跨语言存储客户端拥有相关读写操作和必须释放的连接；
5. 一个内部工作流需要时钟、随机抽取和设置查询，但没有任何领域函数同时需要三者。

即使选择的类型是函数，也要说明生命周期与失败行为。

### 练习 3：让边界失败显式 {#exercise-03}

修改设置查询，让缺失的 `BOOKING_REGION` 成为错误，而不是使用后备值。定义专门的错误联合，并让捕获步骤返回 `Result<Candidate, CaptureError>`。

确保设置缺失可以与随机数提供者超出范围的契约违规区分。判断后者应保留为异常还是变成错误用例，并根据谁能够恢复来说明理由。

[阅读本章答案](../solutions/ch-20-functional-core-effects)。

## 模型回顾 {#model-review}

- 即使没有形参命名，时间、随机数与环境访问仍然是输入。
- 当一次一致快照比重新读取能力更强时，应传入已捕获值。
- 纯核心变换显式值，无须运行时设置即可重放。
- 函数注入会暴露效果；它不会让被调用函数自动变纯。
- 闭包保留配置或状态，其纯净性取决于捕获了什么以及执行了什么。
- 小型函数记录适合局部 F# 编排；接口适合连贯组件与面向 .NET 的边界。
- 把真实运行时调用留在组合代码中，并立即转换其外部表示。
- 确定性替身应直接证明契约，而不使用 sleep、真实环境变更或假定随机序列。

下一章会在保留同一个函数式核心的同时，为该边界加入异常、可释放资源与文件 I/O。

## 资料来源 {#sources}

- [Microsoft Learn：F# 函数](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/functions/)
- [Microsoft Learn：F# 组件设计指南](https://learn.microsoft.com/en-us/dotnet/fsharp/style-guide/component-design-guidelines)
- [Microsoft Learn：`DateTimeOffset.UtcNow`](https://learn.microsoft.com/en-us/dotnet/api/system.datetimeoffset.utcnow?view=net-10.0)
- [Microsoft Learn：`Random.Next`](https://learn.microsoft.com/en-us/dotnet/api/system.random.next?view=net-10.0)
- [Microsoft Learn：`Environment.GetEnvironmentVariable`](https://learn.microsoft.com/en-us/dotnet/api/system.environment.getenvironmentvariable?view=net-10.0)
