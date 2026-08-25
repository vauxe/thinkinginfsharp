---
title: "第 31 章练习答案"
description: "把结论约束在已采集基准内，设计保持行为的 option 与 voption 分配实验，并为三种不同系统症状选择证据。"
translationKey: solutions/ch-31-measure-before-optimizing
---

# 第 31 章练习答案 {#overview}

这些答案让每项结论都与其工作负载和环境绑定。一个看似合理的机制尚不是已测得的原因，而局部测得的原因也尚不是端到端改进。

[返回第 31 章](../part-05/ch-31-measure-before-optimizing)。

## 练习 1：只陈述证据支持的结论 {#exercise-01}

### 区分观察与外推 {#exercise-01-claims}

证据支持的三项结论是：

1. 两种实现在四个具名案例和 256 个固定种子生成案例上返回相等结果，其中包括最初失败的精确匹配边界。
2. 在已采集的 macOS arm64、.NET 10.0.9 ShortRun 上，256 与 4,096 个元素时，`SinglePass` 相对于 `ArrayPipeline` 测得的比率均为 0.43。
3. MemoryDiagnoser 在这两种规模下分别报告管道每次操作分配 520 B 和 7,504 B，而单遍每次操作为 0 B；这与中间数组假设一致。

证据不支持的三项主张是：

1. “F# 循环总比管道快 2.3 倍。”只比较了两个具体函数、一种分布、两种规模和一个环境。
2. “数组比列表或序列快。”两种替代集合均未测量，而且它们的语义不同。
3. “应用的 p95 延迟会改善 57%。”微基准排除了应用其余部分，也没有给出端到端占比或结果。

同样不能声称 0 B 表示进程完全不用内存，或 260 个案例证明对全部整数等价。应精确陈述工具观察到了什么。

Dry 对每个基准执行一次冷启动测量。它证明发现、生成项目构建、设置、执行和结果收集都能工作。一次样本无法提供有用的方差估计；对于这些极小操作，启动与 JIT 影响占主导，而且诊断器只为 ShortRun 或更长作业说明了分配精度。因此 Dry 是冒烟信号，不能替代基线。

即使 ShortRun 也有意保持简短。它的三次测量和未受控工作站，使所存记录适用于本章假设，而不适合作为生产服务级阈值。

## 练习 2：设计 `option` 与 `voption` 实验 {#exercise-02}

### 保持算法、输入和返回可观察量相同 {#exercise-02-design}

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

计时前，应在空索引、全缺失、全存在与混合输入上比较两个校验和。添加固定种子生成的索引数组，以及精确边界索引 `-1`、`0`、`Length - 1` 和 `Length`。若载荷有不变量，应通过生产使用的同一验证路径来构造。

在 `GlobalSetup` 中，创建一个值数组和例如存在率为 0%、50%、100% 的索引数组。把批长度与存在率参数化，但让两个基准方法使用完全相同的预建数组。不要在被测方法内生成随机索引、记录日志或分配夹具。

以 Release 且无调试器运行，把一个实现标为基线，返回校验和，启用 MemoryDiagnoser，并只把 ShortRun 用于初步方向。影响重大的 API 变更值得在接近生产的运行时和 CPU 上运行更长作业。

观察均值、离散程度、比率、每次操作分配字节数与 Gen0 频率。假设预测 `voption` 包装器分配较少；它并未预先决定吞吐量一定获胜。若两者都分配 0 B，应检查 JIT 内联或逃逸分析是否在这种调用形态中消除了包装器。

混杂因素包括载荷大小与结构体复制成本、命中分布、分支预测、查找工作掩盖包装器成本、经由 `obj` 装箱、泛型或接口边界、编译器/JIT 版本，以及真实调用方是否保留结果。改变公开类型之前，应测试实际公开边界。

最后重跑应用分配剖析和用户可见工作负载。若拟议表示不能实质改善真实需求，或公开契约成本超过测得收益，就保留 `option`。

## 练习 3：选择下一项测量 {#exercise-03}

### 让每种症状与其边界匹配 {#exercise-03-observations}

对于 API 的高 p95 延迟，先复现有代表性的并发量与载荷，同时记录端到端 p50/p95/p99、吞吐量和错误率。观察 CPU、GC、线程池排队和异常等运行时计数器；当计数器指出可能的原因类别时，再采集覆盖慢请求的追踪。

当追踪或排队证据把慢请求的实质部分归因于可控制路径时，实现实验才有依据。修改后，重跑相同负载，并比较尾延迟、吞吐量、错误与资源使用。一个隔离辅助函数更快并不充分。

对于已经关联到聚合调用栈的高分配率，用分配追踪或计数器确认每个请求的字节数与调用频率。随后用有代表性的规模和值，再加等价性关卡，把该聚合隔离到微基准。当估算速率对 GC 压力确有实质影响时，再进行实验。

此后应重新测量基准分配、进程分配率、GC 频率或暂停行为，以及原始端到端可观察量。若减少分配却增加 CPU，或服务行为不变，较低层代码可能并不值得。

对于命令行启动缓慢，先反复测量冷启动到一个明确定义的就绪事件，并记录分布。用启动追踪区分运行时/JIT 工作、模块初始化、依赖加载、配置和 I/O。稳态聚合基准回答的是错误问题。

若 JIT 或加载占据实质部分，应只在兼容性分析成功后比较 Native AOT 等真实发布替代方案。对目标 RID 测量冷启动就绪、工作集、文件大小、发布时间和功能行为。若 I/O 或急切初始化占主导，就修复那个原因。

每种情况下，下一种工具都由尚未解决的问题选择。计数器分类、追踪归因、微基准比较隔离机制，端到端测量则裁决需求是否得到改善。

## 答案回顾 {#solution-review}

- 观察应指出精确方法、工作负载、作业、运行时和环境。
- Dry 证明可以执行，却既不提供稳定计时估计，也不提供有用方差。
- 若无端到端证据，局部比率不能预测应用的 p95 改善。
- option/值选项基准必须保持算法、输入、调用形态和返回可观察量。
- 固定的有值/无值分布比一次偶然混合更能揭示表示行为。
- 分配较少只是关于吞吐量和 GC 的假设，而不是二者的证明。
- 计数器给资源压力分类；追踪把它归因到调用路径。
- 微基准应在剖析之后比较可疑机制，而不是之前。
- 启动需要冷进程证据与真实发布产物。
- 每项优化都以重新测量原始用户可见需求结束。
