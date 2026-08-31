---
title: "第 31 章：先测量再优化"
description: "从用户可见的性能需求出发，经过剖析与受控的 F# 基准，得到保持等价且经过测量的修改，同时不把局部结果冒充普遍规律。"
translationKey: part-05/ch-31-measure-before-optimizing
---

# 第 31 章：先测量再优化 {#overview}

性能是代码在特定环境和工作负载下，相对于某项需求表现出的行为。“这段代码看起来很快”说明不了什么，单次秒表结果、一张剖析器截图或从另一台机器复制来的微基准同样不够。先明确用户或运维人员需要什么，再定位高成本路径、改变一个原因，并重新测量同一指标。

高效的 F# 不要求放弃表达式、不可变性或领域类型。清晰代码正是检验优化假设的基线。测量找到热点循环后，可以使用严格限制在局部的可变状态或较低层表示。公开 API 仍应在满足需求的前提下保持简单、准确。

本章主线代码来自可运行项目 `examples/chapters/ch31/Ch31.Benchmarks.fsproj`。`Benchmarks.fs` 依次定义参考实现、候选实现、等价性检查和 BenchmarkDotNet 基准；`Program.fs` 定义 `--verify-only`、`--smoke` 与默认 ShortRun 三种入口。后文短片段按这个文件顺序阅读，不是各自放进空文件就能运行的独立程序。

## 先定义性能问题 {#performance-question}

有用的性能陈述包含四部分：

| 部分 | 示例 | 缺失这一部分的后果 |
|---|---|---|
| 工作负载 | 以实际观察到的值分布聚合 4,096 个请求 | 过小或人为的数据分布可能优化错路径 |
| 环境 | .NET 10、arm64、Release、工作站或指定生产机器类别 | 运行时、JIT、CPU、GC 和电源行为均会不同 |
| 可观察量 | p95 请求延迟、每秒操作数、分配字节、启动时间或发布大小 | “更快”把互不相同的结果混在一起 |
| 目标 | 每秒 200 个请求时 p95 低于 150 ms | 任何修改都能被称为成功 |

选择直接对应需求的指标。若用户报告 HTTP 请求缓慢，应先看端到端请求延迟与吞吐量，而不是某个列表函数的纳秒数。若容器重启过慢，就测量进程就绪时间。若 GC 暂停占主导，就测量分配率、堆行为和暂停，而不只是 CPU 时间。

当分布尾部重要时，应使用百分位数。平均值可能改善，而 p99 反而变差。随结果记录并发量、数据分布、缓存状态、运行时配置及外部依赖。没有这些输入，基准数字就没有可解释的含义。

## 逐层深入测量 {#evidence-ladder}

可靠的优化循环具有明确顺序：

1. 复现相关工作负载，并为用户可见结果记录基线。
2. 用计数器或剖析器定位时间、分配、争用或 I/O 聚集在哪里。
3. 陈述一个因果假设，包括它预测会变化的可观察量。
4. 若可疑工作很小且确定，就把它隔离在微基准中。
5. 用示例、属性或可信参考实现检查功能等价。
6. 做一次聚焦修改，并在可比条件下重跑同一基准。
7. 重跑端到端工作负载；若局部更快却没有改善需求，就拒绝这项修改。

剖析回答“进程把资源花在哪里？”微基准回答“在这个隔离设置下，这些实现如何比较？”端到端测量回答“系统结果改善了吗？”三者不能相互替代。

达到目标，或测得的收益已不足以抵偿复杂度时，就应停止。优化会带来维护、可移植性、代码大小和调试成本。一项路径只占请求时间百分之一时，即使局部快百分之五，也无法实质修复整个请求。

## 案例：移除一次测得的中间分配 {#case-study}

样例对不大于配置上限的正座位值求和。基线是惯用的数组管道；候选实现以一次遍历完成同样的判断与加法：

```fsharp:line-numbers [Benchmarks.fs]
namespace ThinkingInFSharp.Ch31

open System
open BenchmarkDotNet.Attributes

module RequestAggregation =
    let arrayPipeline maxSeats (requests: int array) =
        requests
        |> Array.filter (fun seats -> seats > 0 && seats <= maxSeats)
        |> Array.sumBy int64

    let singlePass maxSeats (requests: int array) =
        let mutable total = 0L

        for seats in requests do
            if seats > 0 && seats <= maxSeats then
                total <- total + int64 seats

        total
```
`arrayPipeline` 清楚表达意图：选择符合条件的元素，再把它们转换为 `int64` 后求和。它也会在第二次遍历前生成并保存筛选后的数组。若不处于热点，这项成本可能无关紧要；因此没有测量指向它时，管道仍是良好默认选择。

`singlePass` 把可变累加器封闭在一个函数里。没有可变引用逸出，可观察结果仍是值。局部可变状态是一种实现技术，并不要求领域模型变成可变。两种计算期间，数组本身仍不得被并发修改。

候选实现改变了遍历结构，因此带来正确性风险。第一个有意写错的版本使用 `< maxSeats`，而不是 `<= maxSeats`；上限为 4、输入为 `[1; 4; 5; 0; -1; 2]` 时，基线返回 7，候选只返回 3。在修复这个语义差异之前，不应开始测量。

## 计时前先提供足够的等价证据 {#equivalence}

开始任何基准前，示例都会检查四个具名案例与 256 个确定性生成案例：

```fsharp:line-numbers [Benchmarks.fs]
module Equivalence =
    let private fixedCases =
        [| 0, [||]
           4, [| 1; 4; 5; 0; -1; 2 |]
           1, [| 1; 1; 2; -3 |]
           6, [| 6; 5; 4; 3; 2; 1 |] |]

    let verify () =
        let random = Random 31

        let generatedCases =
            Array.init 256 (fun length ->
                let maxSeats = random.Next(0, 8)
                let requests = Array.init length (fun _ -> random.Next(-2, 12))
                maxSeats, requests)

        Array.append fixedCases generatedCases
        |> Array.iteri (fun index (maxSeats, requests) ->
            let expected = RequestAggregation.arrayPipeline maxSeats requests
            let actual = RequestAggregation.singlePass maxSeats requests

            if actual <> expected then
                failwithf
                    "equivalence case %d failed: maxSeats=%d expected=%d actual=%d"
                    index
                    maxSeats
                    expected
                    actual)

        fixedCases.Length + generatedCases.Length
```
参考实现与候选实现采用不同结构，因此比较有意义。案例覆盖空输入、恰好位于上限的值、被拒绝值、负值和不同长度。260 个案例通过并非数学证明；生产规则可能还需要更多属性、溢出案例或领域级测试。

项目固定 BenchmarkDotNet 0.15.8，并提交了 `packages.lock.json`。首次运行先按锁文件还原，再只执行确定性的正确性检查：

```console
dotnet restore examples/chapters/ch31/Ch31.Benchmarks.fsproj --locked-mode
dotnet run --project examples/chapters/ch31/Ch31.Benchmarks.fsproj \
  --configuration Release --no-restore -- --verify-only
```

输出为 `Equivalence cases: 260`。这只是等价性关卡，不会启动计时作业。

不要在这个检查中写入容易波动的时间限制。正确性检查应当确定。性能历史可以帮助发现疑似退化，但把阈值放进 CI 前，需要受控执行器、重复测量以及处理方差的规则。

## 设计能隔离假设的基准 {#benchmark-design}

基准设置在 `GlobalSetup` 中构造可重现的输入，返回每次求和结果，测试两种数据规模，把管道标为基线，并启用托管分配报告：

```fsharp:line-numbers [Benchmarks.fs]
[<MemoryDiagnoser>]
type RequestAggregationBenchmarks() =
    let mutable requests = Array.empty<int>

    [<Params(256, 4096)>]
    member val Count = 0 with get, set

    [<GlobalSetup>]
    member this.Setup() =
        let random = Random 31
        requests <- Array.init this.Count (fun _ -> random.Next(-2, 12))

    [<Benchmark(Baseline = true)>]
    member _.ArrayPipeline() =
        RequestAggregation.arrayPipeline 6 requests

[<Benchmark>]
    member _.SinglePass() =
        RequestAggregation.singlePass 6 requests
```
这些特性来自文件开头的 `open BenchmarkDotNet.Attributes`，`Random` 来自 `open System`。命令行入口同样不是隐藏的脚手架；完整的 `Program.fs` 如下：

```fsharp:line-numbers [Program.fs]
namespace ThinkingInFSharp.Ch31

open BenchmarkDotNet.Configs
open BenchmarkDotNet.Jobs
open BenchmarkDotNet.Running

module Program =
    let private benchmarkConfig job =
        ManualConfig.Create(DefaultConfig.Instance).AddJob([| job |])

    [<EntryPoint>]
    let main arguments =
        let verifiedCases = Equivalence.verify ()

        if Array.contains "--verify-only" arguments then
            printfn "Equivalence cases: %d" verifiedCases
        else
            let job =
                if Array.contains "--smoke" arguments then
                    Job.Dry.WithId("Dry")
                else
                    Job.ShortRun.WithId("ShortRun")

            BenchmarkRunner.Run<RequestAggregationBenchmarks>(benchmarkConfig job)
            |> ignore

        0
```
每个选择都避免一个常见问题：

- 设置耗时不计入被比较的操作；
- 固定种子让两个方法看到可复现的输入；
- 返回结果可阻止死代码消除；
- 参数能揭示这种关系是否随输入规模改变；
- `Baseline = true` 给出每个参数组内部的比率；
- `MemoryDiagnoser` 报告每次操作的托管分配与 GC 频率。

该样例项目已经锁定 BenchmarkDotNet 0.15.8 及全部已解析依赖。应在没有附加调试器的情况下，从命令行以 Release 运行。BenchmarkDotNet 会构建基准可执行文件，执行预热与测量迭代，并报告运行时环境；手写 `Stopwatch` 循环则要自行重新实现这些控制。

快速模式只是执行检查：

```console
dotnet run --project examples/chapters/ch31/Ch31.Benchmarks.fsproj \
  --configuration Release --no-restore -- --smoke
```

它使用只有一次冷启动测量的 Dry 作业，因此均值与比率不能作为基线。不提供章节专用参数时会使用 ShortRun。重要决策需要更高精度时，应在受控机器上使用更长作业。

## 解读采集结果而不夸大 {#read-results}

下方基线记录了工具版本、作业、OS、运行时、架构、GC、配置、种子、工作负载和限制。在那台开发者工作站上，ShortRun 摘要为：

| 方法 | 数量 | 均值 | 误差（99.9% 置信区间半宽） | 标准差 | 比率 | 已分配 |
|---|---:|---:|---:|---:|---:|---:|
| `ArrayPipeline` | 256 | 339.9 ns | 27.92 ns | 1.53 ns | 1.00 | 520 B |
| `SinglePass` | 256 | 147.3 ns | 6.25 ns | 0.34 ns | 0.43 | 报告为 0 B |
| `ArrayPipeline` | 4,096 | 5,777.7 ns | 691.47 ns | 37.90 ns | 1.00 | 7,504 B |
| `SinglePass` | 4,096 | 2,475.9 ns | 70.92 ns | 3.89 ns | 0.43 | 报告为 0 B |

这次结果只支持范围很窄的结论。在本次环境和输入生成器下，单遍候选保持了全部已检查结果。在两种测试规模下，它的均值约为管道版本的 0.43；MemoryDiagnoser 也没有报告中间数组分配。

这项结论只适用于两种被测实现、两种规模和本次环境。关于循环、管道、数组、列表、可变状态、其他运行时或其他 CPU 的一般判断都需要重新测量。处理器查询遭拒，工作站电源状态与后台负载未受控，ShortRun 也只有三次测量迭代，因此应谨慎看待较宽的置信区间。

均值是各次测得操作的算术平均数；标准差描述观察到的离散程度。显示的误差是 BenchmarkDotNet 所声明置信区间的一半，适用于本次样本。比率只比较相应 `Count` 组内的方法。“报告为 0 B”表示诊断器在其分辨率下观察到每次操作分配零个托管字节；进程其他位置仍会使用内存。

## 理解分配假设 {#allocation}

在这个样例中，`Array.filter` 创建包含已接受值的中间数组，随后 `Array.sumBy` 遍历它。分配量随匹配数量增长。单遍实现读取源数组并累加 `int64`，不构造该结果数组。测量结果与这个具体因果解释一致。

分配并不自动等于缺陷。保存完整结果可能简化代码、支持复用，或避免重复执行延迟计算。短命分配可能成本很低，直到分配速率形成 GC 压力。只有剖析或实测分配率表明它影响需求时才应优化，不能只因为代码中出现“分配”一词。

不存在脱离上下文的“最快 F# 集合”：

| 需求 | 待测候选 | 重要成本 |
|---|---|---|
| 持久的头部更新与结构共享 | `list<'T>` | 随机索引差，且每个节点有开销 |
| 稠密索引、批量遍历、.NET 互操作 | `'T array` | 可变存储；结构变化时要复制整个数组 |
| 延迟或流式遍历 | `seq<'T>` | 枚举器/闭包开销，以及再次枚举时重复工作 |
| 保持顺序的键查找 | `Map<'K,'V>` | 树比较，以及更新时的分配 |
| 不要求顺序的键查找 | `Dictionary<'K,'V>` | 可变性、比较器质量、容量和扩容行为 |

先按行为选择集合，再剖析有代表性的操作和规模。更换集合可能改变顺序、相等性、可变性、惰性、线程安全与内存生命周期，而不仅是速度。

## 剖析应用，而不只是一个函数 {#profiling}

.NET 诊断工具可以观察实时或已记录进程。运行时计数器能快速展示 CPU 使用、分配率、GC 活动、线程池行为和异常；采样追踪可以把 CPU 时间与分配归因到调用栈；堆工具则回答分配微基准无法回答的保留问题。

应使用有代表性的流量，并保留运行上下文。微基准有意排除网络延迟、序列化、数据库等待、争用、排队、JIT 启动和应用组合。剖析找出某个热点函数后，它可以解释该函数，却无法单独预测 p95 请求延迟。

同理，一个函数出现在 CPU 剖析顶部，也不能单凭这一点证明因果。它可能因为上游设计而被频繁调用，或在归因到别处的操作内等待。应提出假设、改变一个原因，再同时确认剖析与端到端可观察量。

## 测量后再使用较低层 F# 工具 {#lower-level-tools}

较低层特性用便利性与通用性换取对表示或调用方式的控制。应把它们限制在很小的范围内，并针对实际用例做基准测试。

### `inline` 不是通用加速标记 {#inline}

F# 的 `inline` 函数会集成到调用点，并可使用静态解析的类型参数。有时类型系统角色要求这样做；普通泛型函数不需要它。编译器与 JIT 也能按自己的规则内联未标记代码。

给函数标记 `inline` 可能消除调用或 lambda 开销、暴露进一步优化、毫无作用，或增大生成代码并增加指令缓存压力。它也会让调用方对实现变化更敏感。应保留等价的非内联基线并测量整体行为，不要按审美规则在小函数上到处添加 `inline`。

### `voption` 用值复制换取包装器分配 {#voption}

`option<'T>` 是可选数据的自然模型。`voption<'T>` 是带 `ValueSome` 与 `ValueNone` 的结构体可区分联合。它可以在热点中避免分配 option 包装器，尤其是载荷较小时；但复制大型结构体可能更贵，装箱或经由泛型/接口使用也可能抹去预期收益。

把 `option` 改为 `voption` 会改变公开类型及其分支名称。应测量分配敏感路径，同时包含有值与无值两种分布，并保留行为测试。除非调用方确实从这个 API 受益，否则把值选项留在内部。F# 10 也支持基于结构体的可选成员参数，但同样需要测量依据。

### Span 与 byref 会施加生命周期规则 {#span-byref}

`byref<'T>`、`inref<'T>` 与 `outref<'T>` 是托管指针。`Span<'T>` 和 `ReadOnlySpan<'T>` 是连续内存上的 byref-like 视图；即便底层内存位于托管或非托管区域，视图本身也受栈约束。编译期逸出规则禁止把这些值存入普通堆对象，或在 lambda 与异步工作流中捕获它们。

在同步代码中，Span 可以消除切片复制，并高效接入面向缓冲区的 .NET API。它不是每个数组或列表的替代品。如果工作要异步继续或比当前调用存活更久，应使用 `Memory<'T>`、`ReadOnlyMemory<'T>` 或数组，并明确其生命周期。只有剖析表明复制或转换确实影响性能后，才添加 Span 或 byref。

## 区分运行时优化与部署优化 {#deployment}

裁剪与 Native AOT 会改变应用的发布方式。应通过启动时间、工作集、包大小、兼容性、构建时间和目标运行时标识符来评估它们，而不能从这个聚合微基准推断。

裁剪会从自包含发布中移除静态不可达代码，以减小部署大小。反射等动态模式可能让分析看不到必要代码，因此裁剪警告可能指出正确性问题。应解决警告并测试发布产物；只为生成更小包而压制警告，可能造成运行时失败。

Native AOT 在发布时把 IL 编译成平台专属原生代码，并去除运行时 JIT 依赖。它可改善适合应用的启动时间与内存占用，但会增加构建时间，并限制动态加载、运行时代码生成、重度反射库和部署目标。它不承诺每种工作负载的稳态吞吐量都会提高。

应在相同启动或服务工作负载下比较实际 JIT 与 AOT 产物。对每个受支持 RID 都应包含发布大小与功能冒烟测试。除非应用的部署需求提出这个问题，普通基准项目不必变成 AOT 兼容。

## 记录决策，不要只记录漂亮数字 {#decision-record}

有用的性能记录会说明：

- 需求及用户可见基线；
- 修订版本、完整命令、输入分布、环境和作业；
- 定位可疑成本的剖析结果；
- 假设与语义等价检查；
- 原始摘要统计与分配数据，而不只是最好的一次运行；
- 修改后的端到端结果；
- 已知限制、被拒绝方案和回滚条件。

把历史结果保存为上下文，不要直接当成永久的通过/失败阈值。运行时、依赖、硬件、工作负载或基准代码变化后，应主动重新建立基线。环境不同时，应先在同一次运行内比较候选，再跨历史比较绝对值。

## 练习 {#exercises}

### 练习 1：只陈述测量支持的结论 {#exercise-01}

利用采集表格，写出三项测量支持的结论和三项测量不支持的结论。解释为什么 Dry 冒烟输出不能替代 ShortRun 基线。


::: details 参考答案

#### 区分观察与外推 {#exercise-01-claims}

测量支持以下三项结论：

1. 两种实现在四个具名案例和 256 个固定种子生成案例上结果相等，其中包括最初失败的恰好装满用例。
2. 测试环境为 macOS arm64、.NET 10.0.9 和 ShortRun。元素数为 256 与 4,096 时，`SinglePass` 相对 `ArrayPipeline` 的比率均为 0.43。
3. MemoryDiagnoser 在这两种规模下分别报告管道每次操作分配 520 B 和 7,504 B，而单遍每次操作为 0 B；这与中间数组假设一致。

以下三项结论没有得到测量支持：

1. “F# 循环总比管道快 2.3 倍。”只比较了两个具体函数、一种分布、两种规模和一个环境。
2. “数组比列表或序列快。”两种替代集合均未测量，而且它们的语义不同。
3. “应用的 p95 延迟会改善 57%。”微基准排除了应用其余部分，也没有给出端到端占比或结果。

0 B 不表示进程完全不用内存；260 个案例也不能证明对全部整数等价。结论必须限于工具实际观察到的结果。

Dry 为每个基准执行一次冷启动测量。它确认发现、生成项目构建、设置、执行和结果收集这条链路可用。单个样本无法估计方差；对这些微小操作，启动和 JIT 开销又占主导。诊断器只对 ShortRun 或更长作业说明分配精度。因此，Dry 只能做冒烟检查，不能充当性能基线。

即使 ShortRun 也有意保持简短。它的三次测量和未受控工作站，使所存记录适用于本章假设，而不适合作为生产服务级阈值。

:::

### 练习 2：设计 `option` 与 `voption` 实验 {#exercise-02}

剖析器把大量分配归因于一个每秒数百万次返回 `Some smallStruct` 或 `None` 的查找。为 `option<'T>` 与 `voption<'T>` 实现设计基准与等价性关卡。说明有值/无值分布、设置、返回结果、分配观察和混杂因素。


::: details 参考答案

#### 保持算法、输入和返回结果相同 {#exercise-02-design}

先写两个只在可选表示上不同的函数：

```fsharp
[<Struct>]
type SmallValue = { Code: int }

let tryReadOption (values: SmallValue array) index =
    if uint index < uint values.Length then Some values[index] else None

let tryReadValueOption (values: SmallValue array) index =
    if uint index < uint values.Length then ValueSome values[index] else ValueNone

let checksumOption values indexes =
    let mutable total = 0

    for index in indexes do
        match tryReadOption values index with
        | Some value -> total <- total + value.Code
        | None -> ()

    total

let checksumValueOption values indexes =
    let mutable total = 0

    for index in indexes do
        match tryReadValueOption values index with
        | ValueSome value -> total <- total + value.Code
        | ValueNone -> ()

    total
```

返回相同的 `int` 校验和，会消费两种结果，同时不要求 BenchmarkDotNet 比较表示不同的返回值。调用边界应忠实于生产：若真实查找无法跨程序集边界内联，基准就不应意外把它变成本地内联函数。

计时前，先在空索引、全未命中、全命中和混合输入上比较两个校验和。再加入固定种子生成的索引数组，以及边界索引 `-1`、`0`、`Length - 1` 和 `Length`。若载荷有不变量，应通过生产使用的同一验证路径构造。

在 `GlobalSetup` 中，创建一个值数组和例如存在率为 0%、50%、100% 的索引数组。把批长度与存在率参数化，但让两个基准方法使用完全相同的预建数组。不要在被测方法内生成随机索引、记录日志或准备测试数据。

在无调试器的 Release 模式下运行，把一个实现标为基线，返回校验和，并启用 MemoryDiagnoser。ShortRun 只用于判断初步趋势。若结果会推动重要 API 变更，应在接近生产的运行时与 CPU 上执行更长作业。

观察均值、离散程度、比率、每次操作分配字节数与 Gen0 频率。假设预测 `voption` 包装器分配较少；它并未预先决定吞吐量一定获胜。若两者都分配 0 B，应检查 JIT 内联或逃逸分析是否在这种调用形态中消除了包装器。

需要控制的混杂因素包括：

- 载荷大小与结构体复制成本；
- 命中分布、分支预测，以及查找本身是否掩盖包装器成本；
- 经由 `obj` 的装箱、泛型或接口调用；
- 编译器和 JIT 版本；
- 真实调用方是否保留结果。

改变公开类型前，应测试真实调用路径。

最后重跑应用分配剖析和用户可见工作负载。若拟议表示不能实质改善真实需求，或公开契约成本超过测得收益，就保留 `option`。

:::

### 练习 3：选择下一项测量 {#exercise-03}

对每种症状——API 的 p95 延迟高、已知聚合调用栈的分配率高、命令行启动缓慢——选择下一项端到端、剖析器、计数器或微基准观察。说明什么结果能支持实现实验，以及此后必须复测什么。


::: details 参考答案

#### 让每种症状与其边界匹配 {#exercise-03-observations}

对于 API 的高 p95 延迟，先复现有代表性的并发量与载荷，同时记录端到端 p50/p95/p99、吞吐量和错误率。观察 CPU、GC、线程池排队和异常等运行时计数器；当计数器指出可能的原因类别时，再采集覆盖慢请求的追踪。

只有追踪或排队数据表明可控路径占慢请求的显著部分，才值得尝试实现变更。修改后重跑相同负载，比较尾延迟、吞吐量、错误率与资源使用。单个辅助函数变快并不足以说明问题已解决。

对于已经关联到聚合调用栈的高分配率，用分配追踪或计数器确认每个请求的字节数与调用频率。随后用有代表性的规模和值，再加等价性关卡，把该聚合隔离到微基准。当估算速率对 GC 压力确有实质影响时，再进行实验。

此后应重新测量基准分配、进程分配率、GC 频率或暂停时间，以及原始端到端指标。若分配减少却增加 CPU，或服务表现不变，这项底层改动可能得不偿失。

对于命令行启动缓慢，先反复测量冷启动到一个明确定义的就绪事件，并记录分布。用启动追踪区分运行时/JIT 工作、模块初始化、依赖加载、配置和 I/O。稳态聚合基准回答的是错误问题。

若 JIT 或加载占据实质部分，应只在兼容性分析成功后比较 Native AOT 等真实发布替代方案。对目标 RID 测量冷启动就绪、工作集、文件大小、发布时间和功能行为。若 I/O 或急切初始化占主导，就修复那个原因。

每次都根据尚未解决的问题选择工具：计数器用于分类，追踪定位调用路径，微基准比较孤立机制，端到端测量判断用户需求是否改善。

:::


## 来源 {#sources}

- [BenchmarkDotNet：入门与 Release 执行](https://benchmarkdotnet.org/articles/guides/getting-started.html)
- [BenchmarkDotNet：良好实践与外推限制](https://benchmarkdotnet.org/articles/guides/good-practices.html)
- [BenchmarkDotNet：诊断器与分配报告](https://benchmarkdotnet.org/articles/configs/diagnosers.html)
- [NuGet：BenchmarkDotNet 0.15.8](https://www.nuget.org/packages/BenchmarkDotNet/0.15.8)
- [Microsoft Learn：.NET 诊断、计数器、追踪与剖析器](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/)
- [Microsoft Learn：F# 内联函数](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/functions/inline-functions)
- [Microsoft Learn：F# 值选项](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/value-options)
- [Microsoft Learn：F# byref 与 byref-like 结构体](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/byrefs)
- [Microsoft Learn：`Memory<'T>` 与 `Span<'T>` 使用指南](https://learn.microsoft.com/en-us/dotnet/standard/memory-and-spans/memory-t-usage-guidelines)
- [Microsoft Learn：裁剪自包含应用](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/trim-self-contained)
- [Microsoft Learn：Native AOT 部署](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/)
