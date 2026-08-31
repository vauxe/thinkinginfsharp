---
title: "第 29 章：使用 FsCheck 做基于属性的测试"
description: "从选定示例走向领域不变量，控制生成、分类、缩减与重放，同时不把随机检查误作证明。"
translationKey: part-05/ch-29-property-testing
---

# 第 29 章：使用 FsCheck 做基于属性的测试 {#overview}

示例测试检查一个选定输入是否产生预期输出。基于属性的测试（下文简称“属性测试”）检查某种关系能否在大量生成输入上成立。FsCheck 不会证明定理，也不会穷举所有值；它会比简短的手写表格搜索更广，并在失败后尝试把输入缩减成更小的反例。

难点不在于写出 `[<Property>]`，而在于提出有用的不变量、生成符合领域的数据、检查输入分布，并在失败时区分随机种子与业务规则。下面以贪心座位分配器为例说明这些步骤。

本章主线代码不是一组互不相关、可分别粘贴的片段。完整测试项目位于 `examples/chapters/ch29/Ch29.Tests.fsproj`：项目先编译 `Generators.fs`，其中包含领域类型、分配器、属性函数、生成器和缩减器；再编译 `Properties.fs`，其中包含 FsCheck/xUnit 测试。后文标出文件名的代码块都按阅读顺序摘自这两个文件，因此后面的短片段会使用前面已经定义的名称。

## 从示例推广到不变量 {#examples-to-invariants}

设一个分配器从某个容量开始，按顺序处理正数座位请求。请求不超过剩余容量时接受，否则拒绝。容量 5、请求 `[2; 4; 3]` 是一个有用示例：第一个和最后一个请求被接受，中间请求被拒绝，最后剩余零个座位。

该示例很有价值，因为它传达了一项策略决定。但它不会搜索空输入、零容量、重复请求、恰好装满，或接受与拒绝的多种交错。与其手写数百个预期列表，不如询问对每个合法输入都必须保持什么：

1. 已接受座位数加剩余座位数等于初始容量；
2. 每个请求在决策中恰好出现一次，并保持原始顺序；
3. 剩余容量始终位于零和初始容量之间。

示例用智能构造类型表示每个请求，并用联合类型记录每项决策：

```fsharp:line-numbers [Generators.fs]
namespace ThinkingInFSharp.Ch29

open FsCheck
open FsCheck.FSharp

type AllocationCaseError =
    | NegativeCapacity of capacity: int
    | NonPositiveRequest of seats: int

type AllocationCase =
    private
        { Capacity: int
          Requests: int list }

module AllocationCase =
    let create capacity requests =
        if capacity < 0 then
            Error(NegativeCapacity capacity)
        else
            match requests |> List.tryFind (fun seats -> seats <= 0) with
            | Some seats -> Error(NonPositiveRequest seats)
            | None ->
                Ok
                    { Capacity = capacity
                      Requests = requests }

    let capacity sample = sample.Capacity
    let requests sample = sample.Requests

    let internal assumeValid capacity requests =
        match create capacity requests with
        | Ok sample -> sample
        | Error error -> invalidArg (nameof requests) $"invalid allocation case: {error}"

type Decision =
    | Accepted of seats: int
    | Rejected of seats: int

type Allocation =
    { Decisions: Decision list
      Remaining: int }

module SeatAllocation =
    let allocate sample =
        let folder (remaining, decisions) request =
            if request <= remaining then
                remaining - request, Accepted request :: decisions
            else
                remaining, Rejected request :: decisions

        let remaining, reversedDecisions =
            ((sample.Capacity, []), sample.Requests) ||> List.fold folder

        { Decisions = List.rev reversedDecisions
          Remaining = remaining }
```
这三条陈述描述关系，而非某个特定输出，因此许多正确的实现变更都不会破坏它们。每一条会发现不同问题：守恒检查容量丢失或凭空增加，保序检查请求被跳过或重排，上下界检查超额分配。

### 预期行为必须有独立依据 {#independent-oracle}

下面这个属性几乎毫无用处：

```fsharp
let allocationMatchesItself sample =
    SeatAllocation.allocate sample = SeatAllocation.allocate sample
```

在预期值一侧复制分配器的折叠逻辑同样无用，因为同一个缺陷可能同时出现在两边。属性应来自领域规则、代数定律、更简单的参考模型或可信逆运算，而不是从被测源码表达式中复制出来。

常见属性模式包括：

| 模式 | 问题 | 示例 |
|---|---|---|
| 不变量 | 什么必须始终成立？ | 容量守恒 |
| 往返 | 编码后解码能否恢复原值？ | `decode (encode value) = Ok value` |
| 代数定律 | 组合应服从什么定律？ | 集合并运算满足结合律 |
| 模型对照 | 优化版本是否与简单版本一致？ | 索引查找与线性查找一致 |
| 输入变换关系 | 输入变化后，输出应如何变化？ | 排序两次等于排序一次 |

并非每个领域都有优雅的代数规律。小型模型或几个具体示例，可能比生硬拼出的“定律”更清楚。属性名称应说明用户可以依赖什么，而不只是函数“能工作”。

## FsCheck 生成并检查什么 {#fscheck-model}

FsCheck 会把“从生成参数返回 `bool`、`Property`、`Lazy`、`Async` 或 `Task` 等可测试类型”的函数视为属性。xUnit 集成会发现带 `FsCheck.Xunit.PropertyAttribute` 的函数；与 `[<Fact>]` 不同，这类函数可以接收参数。

```fsharp:line-numbers [Properties.fs]
namespace ThinkingInFSharp.Ch29

open FsCheck
open FsCheck.FSharp
open global.FsCheck.Xunit
open global.Xunit

[<Properties(
    Arbitrary = [| typeof<AllocationCaseArbitrary> |],
    QuietOnSuccess = true
)>]
module Ch29Properties =
    [<Property(MaxTest = 300)>]
    let ``allocation conserves capacity`` (sample: AllocationCase) =
        AllocationProperties.conservesCapacity sample
```

`MaxTest = 300` 要求 300 个成功案例。它既不是覆盖率，也不保证得到 300 个不同值。FsCheck 会逐渐改变规模参数，生成器自行决定它如何影响数值大小或集合长度。条件属性拒绝的案例不计为成功，其数量由 `MaxRejected` 另行限制。

共享的属性函数都是纯函数，也可以从示例测试或 FSI 直接调用：

```fsharp:line-numbers [Generators.fs]
module AllocationProperties =
    let private requestedSeats decision =
        match decision with
        | Accepted seats
        | Rejected seats -> seats

    let conservesCapacity sample =
        let allocation = SeatAllocation.allocate sample

        let acceptedSeats =
            allocation.Decisions
            |> List.sumBy (function
                | Accepted seats -> int64 seats
                | Rejected _ -> 0L)

        acceptedSeats + int64 allocation.Remaining = int64 sample.Capacity

    let preservesRequests sample =
        let actual =
            sample |> SeatAllocation.allocate |> _.Decisions |> List.map requestedSeats

        actual = sample.Requests

    let remainingIsBounded sample =
        let remaining = (SeatAllocation.allocate sample).Remaining
        0 <= remaining && remaining <= sample.Capacity

    let isOversubscribed sample =
        (sample.Requests |> List.sumBy int64) > int64 sample.Capacity

    // Plausible, but false: a rejected large request can be followed by a smaller accepted one.
    let acceptedRequestsFormPrefix sample =
        sample
        |> SeatAllocation.allocate
        |> _.Decisions
        |> List.fold
            (fun (stillValid, hasRejected) decision ->
                match decision with
                | Accepted _ -> stillValid && not hasRejected, hasRejected
                | Rejected _ -> stillValid, true)
            (true, false)
        |> fst
```
把属性主体与测试特性分开，可以让关系更易读、也更容易复用。特性只配置运行器，不定义领域规则。

## 生成有意义的领域数据 {#generation}

`Gen<'T>` 描述值如何随规模和伪随机状态变化而产生，声明时并不会立即产生值。FsCheck 的生成器计算表达式无需可变状态即可组合相互依赖的选择：

```fsharp:line-numbers [Generators.fs]
module private AllocationCaseGen =
    let private general size =
        let largest = max 1 (min 40 (size + 1))
        let longest = min 12 size

        gen {
            let! capacity = Gen.choose (0, largest)
            let! length = Gen.choose (0, longest)
            let! requests = Gen.choose (1, largest + 1) |> Gen.listOfLength length
            return AllocationCase.assumeValid capacity requests
        }

    let private rejectionThenFit size =
        let largest = max 1 (min 40 (size + 1))

        gen {
            let! capacity = Gen.choose (1, largest)
            let! tooLarge = Gen.choose (capacity + 1, capacity + largest)
            let! fits = Gen.choose (1, capacity)
            return AllocationCase.assumeValid capacity [ tooLarge; fits ]
        }

    let generator =
        Gen.sized (fun size -> Gen.frequency [ 4, general size; 1, rejectionThenFit size ])
```
一般分支生成非负容量和正数请求；定向分支先生成一个过大的请求，再生成一个可以容纳的请求。`Gen.frequency` 给两个分支的权重分别是 4 和 1。权重只表示相对比例，不保证有限运行中的实际百分比。

这种定向生成很有用，因为“拒绝后又出现较小请求”是重要业务情况。不变量必须在两个分支上都成立。生成器应覆盖有意义的特殊情况，而不是暗中编码属性希望看到的答案。

### 优先构造，不要依赖筛选 {#construction-not-filtering}

先生成任意整数再写 `capacity >= 0 ==> ...` 会浪费案例。当合法输入很少时，筛选尤其危险：运行可能耗尽拒绝预算，幸存值的分布也可能严重偏斜。

应直接用 `Gen.choose (1, upper)` 构造正数请求，并直接选择有界列表长度。只有谓词接受率很高，而且直接构造反而会模糊模型时，才使用 `Gen.filter`。最后一步仍应调用智能构造器，让生成器错误就近失败，不要让无效数据悄悄进入属性。

生成器中的界限属于测试设计，不是领域限制。示例限制数值和列表长度，让 300 个案例保持快速且可读。真正的极端值，例如 `Int32.MaxValue`、算术溢出规则和已知最大负载，仍要用具体示例测试覆盖；随机生成不保证会选中它们。

## 为生成器配套缩减器 {#shrinking}

`Arbitrary<'T>` 把 `Gen<'T>` 与类型为 `'T -> seq<'T>` 的缩减器组合在一起。失败后，FsCheck 会尝试序列中的候选，并从仍然失败的候选继续递归。缩减器按给定策略寻找较小反例，但不保证找到唯一的全局最小值。

样例缩减器会移除一个请求、降低容量，以及每次降低一个请求：

```fsharp:line-numbers [Generators.fs]
module private AllocationCaseShrink =
    let private removeEach requests =
        requests
        |> List.indexed
        |> Seq.map (fun (index, _) -> List.removeAt index requests)

    let private shrinkOneRequest requests =
        seq {
            for index, request in List.indexed requests do
                for smaller in 1 .. request - 1 do
                    yield List.updateAt index smaller requests
        }

    let shrink sample =
        seq {
            for requests in removeEach sample.Requests do
                yield AllocationCase.assumeValid sample.Capacity requests

            for capacity in 0 .. sample.Capacity - 1 do
                yield AllocationCase.assumeValid capacity sample.Requests

            for requests in shrinkOneRequest sample.Requests do
                yield AllocationCase.assumeValid sample.Capacity requests
        }
        |> Seq.distinct
```
每个候选仍保持非负容量和正数请求，同时会缩短列表或减小某个数值。因此缩减会走向基本情况，而不是不断循环。`Seq.distinct` 只去除重复候选，不改变有效性。

向 FsCheck 注册的类型很简单：

```fsharp
type AllocationCaseArbitrary =
    static member AllocationCase() : Arbitrary<AllocationCase> =
        Arb.fromGenShrink(
            AllocationCaseGen.generator,
            AllocationCaseShrink.shrink
        )
```

如果某类型的默认生成器已经具有正确分布并保持不变量，就使用默认生成器。只有当自定义 `Arbitrary` 能改善有效性、分布、性能或反例质量时，其复杂度才值得。

### 只在合法领域内缩减 {#valid-shrinks}

如果缩减器把正数请求变成零，就会迫使属性处理生成器和公共 API 都禁止的值。这种失败说明缩减器有误，而不是分配器有误。有序列表要缩成更小的有序列表，非空标识符要缩成更短的合法标识符，状态机轨迹则要保留合法迁移。

过度缩减也可能丢掉有用上下文。两个字段必须保持关联时，应一起缩减。缩减器变复杂后，可以用采样值或结构属性测试它本身：每个候选都必须合法，而且按事先说明的度量严格变简单。

## 观察输入分布 {#classification}

如果生成的几乎都是空队列，那么即使测试通过也说明不了多少问题。`Prop.classify condition label property` 会为满足条件的案例记录标签，多个标签可以重叠：

```fsharp [Properties.fs]
[<Property(MaxTest = 300)>]
let ``remaining capacity stays within bounds`` (sample: AllocationCase) =
    AllocationProperties.remainingIsBounded sample
    |> Prop.classify
        (AllocationCase.requests sample |> List.isEmpty)
        "empty"
    |> Prop.classify
        (AllocationProperties.isOversubscribed sample)
        "oversubscribed"
```

可以暂时设置 `QuietOnSuccess = false`，或交互式运行属性来查看摘要。分类只报告分布，缺少某一类时不会自动失败。如果某个区域必须出现，应让生成器稳定地产生它，并使用专项属性或 FsCheck API 支持的覆盖断言进行约束。

`Prop.collect` 可以按列表长度等任意观察值分组。只使用少数与风险相关的标签。几十个偶然分桶会制造噪声，让运行看似科学，却不提升发现缺陷的能力。

## 反例可以推翻看似合理的属性 {#wrong-property}

这个说法听起来合理：“接受的请求构成前缀；一旦拒绝，后续请求全被拒绝。”对于会继续处理的贪心分配器，它是错的。容量为 1、请求为 `[2; 1]` 时，请求 2 被拒绝，随后请求 1 被接受。

示例把错误属性保留为具名函数，再由一个实现 `IRunner` 的小型收集器保存结果。`runner` 因而不是 FsCheck 内置的隐含变量。xUnit 测试期待 `TestResult.Failed`，通过断言 FsCheck 能推翻该属性而保持绿色：

```fsharp:line-numbers [Properties.fs]
type private CollectingRunner() =
    let mutable result = None

    member _.Result = result

    interface IRunner with
        member _.OnStartFixture _ = ()
        member _.OnArguments(_, _, _) = ()
        member _.OnShrink(_, _) = ()
        member _.OnFinished(_, finishedResult) = result <- Some finishedResult

type CounterexampleTests() =
    [<Fact>]
    member _.``false prefix property shrinks to the policy counterexample``() =
        let runner = CollectingRunner()

        let config =
            Config.Quick
                .WithMaxTest(300)
                .WithArbitrary([ typeof<AllocationCaseArbitrary> ])
                .WithReplay(13285693176119930639UL, 18364232908344279255UL, 4)
                .WithRunner(runner)

        Check.One(
            "accepted requests form a prefix",
            config,
            AllocationProperties.acceptedRequestsFormPrefix
        )

        match runner.Result with
        | Some(TestResult.Failed(data, _, shrunkArguments, _, _, _, _)) ->
            let shrunk = shrunkArguments |> List.exactlyOne |> unbox<AllocationCase>
            Assert.True(data.NumberOfShrinks > 0)
            Assert.Equal(1, AllocationCase.capacity shrunk)
            Assert.Equal<int list>([ 2; 1 ], AllocationCase.requests shrunk)
        | _ -> Assert.Fail("expected a falsified property")
```

反例不会自动说明代码与属性谁错了，必须回到需求。如果规则是“首次拒绝后停止”，那么分配器有误；在既定的继续处理规则下，则是提出的属性有误。属性测试发现分歧，领域推理判断原因。

## 用重放信息复现失败 {#replay}

失败报告包含初始种子、失败步骤的种子与规模、原始参数和缩减后的参数。在 `PropertyAttribute` 中，`Replay = "seed,gamma"` 会从头重启运行；`Replay = "seed,gamma,size"` 会直接跳到报告的失败步骤。`Config.WithReplay` 提供相应重载。

两个无符号 64 位值表示伪随机状态，不是用户数据。诊断时应记录“Replay directly at failing step”之后打印的完整三元组。先复现失败；如果最小反例能防止重要问题复发，再把它保存为具名示例测试。

属性、生成器、缩减器、目标运行时和 FsCheck 版本都相同时，重放最可靠。示例项目使用 `FsCheck.Xunit` 3.4.0，它会锁定 `FsCheck` 3.4.0。改变生成顺序或升级包可能改变某个种子产生的输入；此时具体的回归示例仍能长期稳定运行。

不要让每次常规运行都使用同一个种子。日常改变种子可以搜索新案例；保存的重放信息只用于诊断与稳定演示。CI 失败必须打印足够信息，以便在本地复现。

## 属性测试要与其他测试配合 {#complementary-tests}

属性测试最适合输入空间庞大且结构化的纯确定性逻辑。它不能替代第 28 章中的其他测试：解释已知示例、验证具体错误、观察副作用行为，或执行真实序列化集成。

| 需要 | 首选测试 |
|---|---|
| 用具体值解释一条业务规则 | 示例测试 |
| 在许多值中搜索不变量或模型分歧 | 属性测试 |
| 验证序列化器、数据库映射或公共元数据 | 集成契约测试 |
| 验证组件与真实基础设施 | 集成测试 |
| 验证接近部署环境的关键路径 | 少量端到端测试 |

属性主体应尽量保持纯函数。随机生成网络请求并发送到共享基础设施，会产生缓慢、易波动且难以缩减的失败，还可能破坏外部状态。纯请求构造与决策使用属性测试，外部协议则使用受控的契约测试或集成测试。

### 测试数取决于成本和失败可读性 {#test-count}

增加案例并不能无成本地提高可信度。成本低的纯属性可以运行数千个案例；会分配大数组的属性则可能需要更少案例和更严格的规模限制。测量测试套件耗时，保持本地反馈迅速；只有确有价值时，才把长时间探索放入独立任务。

一百个分布良好且缩减清晰的案例，可能比一万个近乎相同的案例更有用。当失败报告巨大时，应先改善表示与缩减，而不是只提高数量。

仓库中的项目已经固定 `FsCheck.Xunit` 3.4.0，并包含全部 `open` 声明和测试依赖。要使用 `Gen`、`Arb`、`Prop` 和 `gen {}` 等 F# 专用辅助 API，需要同时打开 `FsCheck.FSharp`；`FsCheck` 本身则提供 `Arbitrary<'T>`、`Config`、`IRunner` 等核心类型。从仓库根目录可以直接运行，不需要替换模板路径：

```console
dotnet test examples/chapters/ch29/Ch29.Tests.fsproj --configuration Release
```

三个属性各要求 300 个成功案例。第四项重放固定的失败步骤，并断言错误的前缀属性会缩减到容量 1、请求 `[2; 1]`。提交前还应去掉过滤器，运行整个测试项目。

新属性失败时，按以下顺序阅读报告：属性名称与标签、异常或 false 结果、缩减参数、原始参数，最后是重放三元组。修改代码前先复现，再判断实现、属性、生成器或缩减器中的哪一个违反了规则。只凭最小值猜测，常会修错位置。

## 练习 {#exercises}

答案中的短代码继续使用本章项目已经定义的类型以及 `FsCheck`、`FsCheck.FSharp`、`Xunit` 中的名称；若把答案单独放进新文件，需要加入与 `Properties.fs` 相同的 `open` 声明。

### 练习 1：推导独立属性 {#exercise-01}

为分配器提出一个额外的正确属性，以及一个不应写成属性的具体示例。解释该属性为何独立于折叠实现，并指出它能发现哪类缺陷。


::: details 参考答案

#### 追加输入不得改写先前决策 {#exercise-01-prefix-stability}

分配器被规定为流式过程：它按顺序处理请求，绝不回头修改早先决策。因此，追加一个正数请求后，原决策列表必须仍是新决策列表的前缀。

```fsharp
let appendingRequestPreservesPriorDecisions
    (sample: AllocationCase)
    (PositiveInt extra)
    =
    let original = SeatAllocation.allocate sample

    let extended =
        AllocationCase.create
            (AllocationCase.capacity sample)
            (AllocationCase.requests sample @ [ extra ])
        |> Result.map SeatAllocation.allocate

    match extended with
    | Error _ -> false
    | Ok allocation ->
        allocation.Decisions
        |> List.take original.Decisions.Length
        |> (=) original.Decisions
```

该属性来自已经声明的流式语义，而不是复刻容量折叠。它可以发现以下实现偏差：先对请求排序、全局优化整批请求，或看到后续输入后重建早先决策。

`PositiveInt` 提供正数追加值，但项目专用生成器也可以把全部输入策略都收进 `AllocationCaseArbitrary`。若在类型推断不清楚的代码中把 `PositiveInt` 转成 `int`，应显式使用它的 `Get` 成员。

应保留容量 5、请求 `[2; 4; 3]` 的具体示例。它清楚记录分配器接受 2、拒绝 4、接受 3，最终容量归零。属性能在大量输入上检查前缀稳定性，但不如这个示例直观。

另一个正确属性是用小型验证器重放决策：每个 `Accepted n` 都必须不超过当时的剩余容量，并减去 `n`；每个 `Rejected n` 都必须超过当时的剩余容量。该模型应独立编写并以策略命名，不能复用生产代码的折叠函数。

:::

### 练习 2：设计生成与缩减 {#exercise-02}

为 `AllocationCase` 增加一个由大写 ASCII 字母和数字组成的非空活动标识符。设计生成器与缩减器，在不使用低命中率筛选的同时保持全部不变量。给出一种可防止缩减循环的简单度量，并列出两个值得观察的分布类别。


::: details 参考答案

#### 从允许字符表构造标识符 {#exercise-02-generator}

让生成器层面无法产生非法字符：

```fsharp
let identifierGenerator =
    let alphabet = [ 'A' .. 'Z' ] @ [ '0' .. '9' ]

    Gen.sized (fun size ->
        gen {
            let! length = Gen.choose(1, max 1 (min 12 (size + 1)))
            let! characters = Gen.elements alphabet |> Gen.listOfLength length
            return System.String(characters |> List.toArray)
        })
```

它始终生成长度 1 到 12 且只含许可字符的值，没有拒绝循环。组装完整案例时，把结果交给标识符智能构造器；若领域规则日后变化，生成器应明显失败。

#### 保持非空与合法地缩减 {#exercise-02-shrinker}

简单的标识符缩减器可以先在长度大于一时移除一个字符，再把一个字符替换成字符表中更靠前的成员。它绝不能产生空字符串或字符表之外的字符。

```fsharp
let shrinkIdentifier (value: string) =
    seq {
        if value.Length > 1 then
            for index in 0 .. value.Length - 1 do
                yield value.Remove(index, 1)

        for index in 0 .. value.Length - 1 do
            if value[index] <> 'A' then
                let chars = value.ToCharArray()
                chars[index] <- 'A'
                yield System.String chars
    }
    |> Seq.distinct
```

对于完整分配案例，应每次只改变一个字段，把标识符候选与现有容量、请求候选组合。一个良基的字典序度量是 `(标识符长度, 字符序位和, 请求数, 容量, 请求和)`。每个发出的候选必须严格减小某个较早分量，同时不增加任何更早分量，因此不可能无限循环。

值得使用的分类包括 `single-character-id` 和 `contains-digit`。视风险而定，也可观察最大长度标识符或超额请求。标签描述实际生成分布；若某种案例必须可靠出现，标签不能替代生成器分支。

上面的样例缩减器偏好易读的 `A` 字符，但团队也可以偏好数字或保留必需前缀。“更小”是一项测试策略，并非标识符上的内在次序。

:::

### 练习 3：解释并保存失败 {#exercise-03}

某属性声称反转请求列表不会改变已接受座位总数。FsCheck 找到容量 2、请求 `[1; 2]`。判断该属性是否符合贪心规则，写出最小的具体回归示例，并区分诊断期间暂存的重放信息与永久保留的测试。


::: details 参考答案

#### 贪心分配有意对顺序敏感 {#exercise-03-counterexample}

容量 2、请求 `[1; 2]` 时，分配器接受 1、拒绝 2，共接受 1。把列表反转为 `[2; 1]` 后，它接受 2、拒绝 1，共接受 2。所声称的置换不变性与有序贪心策略矛盾；这个反例没有揭示分配器缺陷。

把该行为保存为明确的示例：

```fsharp
let allocate capacity requests =
    AllocationCase.create capacity requests
    |> Result.map SeatAllocation.allocate

let acceptedTotal allocation =
    allocation.Decisions
    |> List.sumBy (function Accepted seats -> seats | Rejected _ -> 0)

let forward = allocate 2 [ 1; 2 ] |> Result.map acceptedTotal
let reversed = allocate 2 [ 2; 1 ] |> Result.map acceptedTotal

Assert.Equal(Ok 1, forward)
Assert.Equal(Ok 2, reversed)
```

诊断期间应保留原始与缩减参数、直接重放三元组 `(seed, gamma, size)`、FsCheck 版本及相关代码修订。它们让人能在修复或需求裁决前准确复现运行。

永久保留具名示例及其预期总数。不要让种子成为业务契约：生成顺序变化或锁定依赖升级都可能改变其含义。具体输入则始终易懂且稳定。

修正后的属性可以这样表述：请求总数不超过容量时，每项请求都被接受，已接受总数与顺序无关。没有该前提时，只保留守恒与边界等真实不变量。

:::


## 来源 {#sources}

- [FsCheck：编写和观察属性](https://fscheck.github.io/FsCheck/Properties.html)
- [FsCheck：生成器、缩减器与 Arbitrary 实例](https://fscheck.github.io/FsCheck/TestData.html)
- [FsCheck：运行器、xUnit 集成与重放](https://fscheck.github.io/FsCheck/RunningTests.html)
- [NuGet：FsCheck.Xunit 3.4.0 包及其依赖](https://www.nuget.org/packages/FsCheck.Xunit/3.4.0)
