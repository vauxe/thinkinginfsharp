---
title: "第 29 章：使用 FsCheck 进行性质测试"
description: "从选定示例走向领域不变量，控制生成、分类、缩减与重放，同时不把随机检查误作证明。"
translationKey: part-05/ch-29-property-testing
kind: chapter
part: 5
chapter: 29
status: complete
verifiedWith:
  fsharp: "10"
  dotnetSdk: "10.0.301"
exampleIds:
  - foundation-example-tests
exerciseIds:
  - ch29-exercise-01
  - ch29-exercise-02
  - ch29-exercise-03
termIds: []
sources:
  - id: fscheck-properties
    url: https://fscheck.github.io/FsCheck/Properties.html
    checked: "2026-08-24"
  - id: fscheck-test-data
    url: https://fscheck.github.io/FsCheck/TestData.html
    checked: "2026-08-24"
  - id: fscheck-running-tests
    url: https://fscheck.github.io/FsCheck/RunningTests.html
    checked: "2026-08-24"
  - id: nuget-fscheck-xunit
    url: https://www.nuget.org/packages/FsCheck.Xunit/
    checked: "2026-08-24"
---

# 第 29 章：使用 FsCheck 进行性质测试 {#overview}

示例测试询问一个选定输入是否得到一个预期输出。性质测试询问一种关系能否经受许多生成输入。两者都是可执行示例；FsCheck 不会证明定理，也不会检查每个可能值。它的优势是搜索远大于简短手写表格的输入空间，并在失败时尝试把输入缩减成更小的反例。

因此，困难之处不是写出 `[<Property>]`，而是陈述有用的不变量、生成领域数据而非无意义噪声、观察分布，以及阅读失败时不把种子误作业务需求。本章围绕一个贪心座位分配器培养这些能力。

## 学完本章你将能够做什么 {#outcomes}

学完本章后，你应该能够：

- 把具体示例推广为不变量，同时不让实现充当自己的预言机；
- 区分全称性质、模型对照、代数定律与往返性质；
- 配合 xUnit 2 使用 FsCheck 的 `PropertyAttribute`；
- 用 `Gen<'T>` 构造合法领域输入，而不是筛掉大多数无效值；
- 把生成器与保持有效性的缩减器组合为 `Arbitrary<'T>`；
- 对生成案例分类，并检查重要区域是否得到覆盖；
- 把规模、测试数、拒绝与缩减理解为彼此独立的控制量；
- 用种子、gamma 与可选规模复现失败；
- 让示例、性质、边界与集成测试承担互补角色。

## 从示例推广到不变量 {#examples-to-invariants}

设一个分配器从某个容量开始，按顺序处理正数座位请求。请求不超过剩余容量时接受，否则拒绝。容量 5、请求 `[2; 4; 3]` 是一个有用示例：第一个和最后一个请求被接受，中间请求被拒绝，最后剩余零个座位。

该示例很有价值，因为它传达了一项策略决定。但它不会搜索空输入、零容量、重复请求、恰好装满，或接受与拒绝的多种交错。与其手写数百个预期列表，不如询问对每个合法输入都必须保持什么：

1. 已接受座位数加剩余座位数等于初始容量；
2. 每个请求在决策中恰好出现一次，并保持原始顺序；
3. 剩余容量始终位于零和初始容量之间。

样例通过智能构造器阻止外部表示无效输入，并显式保留每项决策：

<<< @/../examples/chapters/ch29/Generators.fs#allocation-core{fsharp:line-numbers} [Generators.fs]

这三条陈述描述的是关系，而非某个输出。许多正确的实现变更都不会破坏它们。它们也约束不同的故障：守恒能发现容量丢失或凭空增加，保序能发现请求被跳过或重排，边界则能发现超额分配。

### 性质仍需要独立依据 {#independent-oracle}

下面这个性质几乎毫无用处：

```fsharp
let allocationMatchesItself sample =
    SeatAllocation.allocate sample = SeatAllocation.allocate sample
```

在预期值一侧复制分配器的折叠逻辑同样无用。共有缺陷可能让实现和预言机一致。应从领域规则、代数定律、更简单的参考模型或可信逆运算推导性质，而不是从受测源码表达式推导。

常见性质形态包括：

| 形态 | 问题 | 示例 |
|---|---|---|
| 不变量 | 什么必须始终成立？ | 容量守恒 |
| 往返 | 编码后解码能否恢复原值？ | `decode (encode value) = Ok value` |
| 代数定律 | 组合应服从什么定律？ | 集合并运算满足结合律 |
| 模型对照 | 优化版本是否与简单版本一致？ | 索引查找与线性查找一致 |
| 变形关系 | 输入变换应如何改变输出？ | 排序两次等于排序一次 |

并非每个领域都有优雅代数。小型模型或几个精确示例可能比生硬拼出的“定律”更清楚。性质名称应说明用户可以依赖什么，而不只是函数“能工作”。

## FsCheck 生成并检查什么 {#fscheck-model}

FsCheck 把“由生成参数到 `bool`、`Property`，或 `Lazy`、`Async`、`Task` 等其他受支持可测试形式”的函数视作性质。xUnit 集成会发现标有 `FsCheck.Xunit.PropertyAttribute` 的函数；与 `[<Fact>]` 不同，这类函数可以接收参数。

```fsharp
[<Properties(
    Arbitrary = [| typeof<AllocationCaseArbitrary> |],
    QuietOnSuccess = true
)>]
module Ch29Properties =
    [<Property(MaxTest = 300)>]
    let ``allocation conserves capacity`` (sample: AllocationCase) =
        AllocationProperties.conservesCapacity sample
```

`MaxTest = 300` 要求 300 个成功案例。它不是覆盖率，也不表示 300 个互异值。FsCheck 会逐渐改变一个规模参数；生成器自行决定规模如何影响数值大小或集合长度。条件性质拒绝的案例不计作成功，其数量由 `MaxRejected` 另行限制。

共享性质函数都是普通纯函数，也可以由示例测试或 FSI 直接调用：

<<< @/../examples/chapters/ch29/Generators.fs#property-functions{fsharp:line-numbers} [Generators.fs]

把性质主体与测试特性分离，可以让主张易读且可复用。特性是运行器配置，不是领域规格。

## 生成领域数据，而非偶然噪声 {#generation}

`Gen<'T>` 描述值如何随规模和伪随机状态变化而产生，声明时并不会立即产生值。FsCheck 的生成器计算表达式无需可变状态即可组合相互依赖的选择：

<<< @/../examples/chapters/ch29/Generators.fs#generator{fsharp:line-numbers} [Generators.fs]

一般分支生成非负容量和全为正数的请求。定向分支生成一个大到无法容纳的请求，再跟一个可以容纳的请求。`Gen.frequency` 以权重 4 选择一般分支、以权重 1 选择定向分支；权重是相对值，并不保证有限运行中的精确百分比。

这种定向是合理的，因为“拒绝后又出现较小请求”是一种重要业务形态。正确不变量必须经受两个分支。生成器应暴露有意义的角落，而不应暗中编码性质希望看到的答案。

### 优先构造，不要依赖筛选 {#construction-not-filtering}

先生成任意整数再写 `capacity >= 0 ==> ...` 会浪费案例。当合法输入很少时，筛选尤其危险：运行可能耗尽拒绝预算，幸存值的分布也可能严重偏斜。

应直接用 `Gen.choose (1, upper)` 构造正数请求和有界列表长度。只有当谓词接受率很高，且直接构造反而会模糊模型时才使用 `Gen.filter`。最后一步仍适合调用智能构造器：如果生成器代码发生漂移，无效数据应在生成附近失败，而不是悄悄进入性质。

生成器中的界限是测试设计选择，不是领域限制。本样例限制数值和列表长度，让 300 个案例保持快速且可读。真正极端的值，例如 `Int32.MaxValue`、算术溢出策略和已知最大负载，仍应保留显式示例测试；随机生成不保证会选到它们。

## 把生成与缩减配对 {#shrinking}

`Arbitrary<'T>` 把 `Gen<'T>` 与类型为 `'T -> seq<'T>` 的缩减器捆在一起。失败后，FsCheck 尝试序列中的候选，并从仍然失败的候选继续递归。缩减会依照给定策略寻找较小反例，但不承诺找到全局唯一的数学最小值。

样例缩减器会移除一个请求、降低容量，以及每次降低一个请求：

<<< @/../examples/chapters/ch29/Generators.fs#shrinker{fsharp:line-numbers} [Generators.fs]

每个候选仍拥有非负容量和正数请求。每个候选也会减少列表长度或某个数值，因此缩减会走向基本情形而非循环。`Seq.distinct` 去除重复候选，不改变有效性。

向 FsCheck 注册的捆绑类型很小：

```fsharp
type AllocationCaseArbitrary =
    static member AllocationCase() : Arbitrary<AllocationCase> =
        Arb.fromGenShrink(
            AllocationCaseGen.generator,
            AllocationCaseShrink.shrink
        )
```

如果某类型的默认生成器已经具有正确分布并保持不变量，就使用默认生成器。只有当自定义 `Arbitrary` 能改善有效性、分布、性能或反例质量时，其复杂度才值得。

### 缩减不变量，而不只是表示大小 {#valid-shrinks}

若缩减器把正数请求变成零，就迫使性质处理生成器和公共 API 都禁止的值。这种失败诊断的是缩减器，而非分配器。对于有序列表，应缩成更小的有序列表；对于非空标识符，应缩向短小但有效的标识符；对于状态机轨迹，应保留合法迁移。

过于激进的缩减也可能丢掉有用上下文。如果两个字段必须保持关联，应一起缩减。当缩减器变得复杂时，用采样值或结构性质测试缩减器本身：每个候选都应有效，并在明确的度量下严格更简单。

## 观察输入分布 {#classification}

如果生成的主要是空队列，那么通过的运行也可能只是薄弱证据。`Prop.classify condition label property` 会为满足条件的案例记录标签。标签可以重叠：

```fsharp
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

可暂时设置 `QuietOnSuccess = false`，或交互式运行性质来查看摘要。分类用于观察分布；某一类缺失时，它本身不会让测试失败。如果某个区域必须出现，应设计生成器可靠地产生它，并用聚焦性质或所选 FsCheck API 支持的显式覆盖断言加以约束。

`Prop.collect` 可以按列表长度等任意观察值分组。只使用少数与风险相关的标签。几十个偶然分桶会制造噪声，让运行看似科学，却不提升发现缺陷的能力。

## 让错误性质遇到反例 {#wrong-property}

这个说法听起来合理：“接受的请求构成前缀；一旦拒绝，后续请求全被拒绝。”对于会继续处理的贪心分配器，它是错的。容量为 1、请求为 `[2; 1]` 时，请求 2 被拒绝，随后请求 1 被接受。

样例把错误性质保留为具名函数，用收集结果的运行器执行，并期待 `TestResult.Failed`。仓库仍然保持绿色，因为测试断言 FsCheck 能推翻该主张：

```fsharp
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

反例不会自动说明代码和性质谁错了。应回到需求。若策略是“首次拒绝后停止”，那么分配器错误；在既定的继续处理策略下，提出的性质错误。性质测试发现分歧，领域推理负责归因。

## 精确重放失败 {#replay}

失败报告包含初始种子、失败生成步骤的种子与规模、原始参数，以及缩减后的参数。在 `PropertyAttribute` 中，`Replay = "seed,gamma"` 会从头重启一次运行，而 `Replay = "seed,gamma,size"` 可直接跳到报告的失败步骤。`Config` 的 `WithReplay` 提供对应重载。

两个无符号 64 位值是伪随机状态，不是用户数据。调试时应记录“Replay directly at failing step”之后打印的完整三元组。先精确重放失败；如果最小反例守护着重要回归，再把它提升为具名示例测试。

在性质、生成器、缩减器、目标运行时和 FsCheck 版本相同时，重放最可靠。本仓库锁定 `FsCheck.Xunit` 3.4.0，后者精确锁定 `FsCheck` 3.4.0。改变生成顺序或升级包可能改变某个种子产生的输入；此时显式回归示例仍是持久契约。

不要让每次普通通过运行都使用同一个种子。日常运行改变种子可以搜索新案例；保存的重放信息用于诊断与稳定演示。CI 失败必须打印足够信息，让人能在本地复现。

## 让性质测试与其他证据互补 {#complementary-tests}

性质测试最适合输入空间庞大且结构化的纯确定性逻辑。它不能替代第 28 章中解释已知示例、验证精确错误消息、观察副作用协议或执行真实序列化边界的测试。

| 需要 | 首选证据 |
|---|---|
| 用具体值解释一条业务规则 | 示例测试 |
| 在许多值中搜索不变量或模型分歧 | 性质测试 |
| 验证序列化器、数据库映射或公共元数据 | 边界契约测试 |
| 验证组件与真实基础设施 | 集成测试 |
| 验证部署形态的关键路径 | 少量端到端测试 |

尽可能让性质主体保持纯粹。随机生成网络请求去打共享基础设施，会产生缓慢、易抖动且难以缩减的失败，还可能破坏外部状态。用性质测试纯请求构造与决策；用受控契约或集成案例测试协议边界。

### 测试数取决于成本和失败可读性 {#test-count}

更多案例不等于免费信心。廉价纯性质可以运行数千个案例；会分配大数组的性质可能需要更少案例与更紧的规模限制。测量测试套件，保持本地反馈简短；只有确有价值时，才把更长的探索放入独立任务。

一百个分布良好且缩减清晰的案例，可能比一万个近乎相同的案例更有用。当失败报告巨大时，应先改善表示与缩减，而不是只提高数量。

## 运行并诊断本章 {#running}

从仓库根目录运行第 29 章测试：

```console
dotnet test tests/ExampleTests/ExampleTests.fsproj \
  --configuration Release \
  --filter FullyQualifiedName~Ch29
```

三个性质各要求 300 个成功案例。第四项使用固定的失败步骤重放，并断言错误的前缀性质会缩减到容量 1、请求 `[2; 1]`。提交前运行 `pnpm check:examples`，以锁定还原包、编译全部项目、执行所有测试并检查每个已登记样例。

新性质失败时，应按此顺序阅读报告：性质名称与标签、异常或假结果、缩减参数、原始参数，最后是重放三元组。编辑代码前先复现。判断是实现、性质、生成器还是缩减器违反契约；只凭最小值猜测，常会修错层次。

## 练习 {#exercises}

### 练习 1：推导独立性质 {#exercise-01}

为分配器提出一个额外的正确性质，以及一个应留在性质之外的精确示例。解释你的性质为何独立于折叠实现，并指出它能捕获哪类缺陷。

### 练习 2：设计生成与缩减 {#exercise-02}

为 `AllocationCase` 增加一个由大写 ASCII 字母和数字组成的非空活动标识符。设计无需低命中率筛选且保持全部不变量的生成器与缩减器。给出一个能证明缩减不会循环的简洁度度量，并列出两个值得观察的分布类别。

### 练习 3：解释并保存失败 {#exercise-03}

某性质声称反转请求列表不会改变已接受座位总数。FsCheck 找到容量 2、请求 `[1; 2]`。判断该性质是否符合贪心策略，写出最小显式回归示例，并说明诊断期间应保留哪些重放信息、永久保留什么。

[阅读本章练习答案](../solutions/ch-29-property-testing)。

## 模型回顾 {#model-review}

- 性质测试会采样许多生成案例；它是强证据，不是穷尽证明。
- 应从领域不变量、代数、逆运算或简单模型推导性质，而不是复制实现。
- `Gen<'T>` 产生值，缩减器提出更小候选，`Arbitrary<'T>` 捆绑两者。
- 直接构造合法值；只有接受率高时才使用筛选。
- 应定向覆盖有意义的形态，同时用分类或收集观察分布。
- 缩减器必须保持领域有效性，并走向具有良基的更简单案例。
- 最小反例揭示分歧；需求决定代码还是性质有错。
- 重放使用种子、gamma 与可选规模，并依赖稳定代码和包版本。
- 应把重要的已发现失败保存为清晰示例测试。
- 示例、性质、契约、集成与端到端测试回答不同风险。

## 来源 {#sources}

- [FsCheck：编写和观察性质](https://fscheck.github.io/FsCheck/Properties.html)
- [FsCheck：生成器、缩减器与 Arbitrary 实例](https://fscheck.github.io/FsCheck/TestData.html)
- [FsCheck：运行器、xUnit 集成与重放](https://fscheck.github.io/FsCheck/RunningTests.html)
- [NuGet：FsCheck.Xunit 3.4.0 包及其依赖](https://www.nuget.org/packages/FsCheck.Xunit/)
