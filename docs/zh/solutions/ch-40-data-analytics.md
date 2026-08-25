---
title: "第 40 章练习答案"
description: "选择有界数据工具，显式吸收 CSV 模式漂移，并把探索分类器转化为可复现的训练与推理系统。"
translationKey: solutions/ch-40-data-analytics
kind: solution
part: 7
chapter: 40
status: complete
verifiedWith:
  fsharp: "10"
  dotnetSdk: "10.0.301"
exampleIds:
  - ecosystem-data-csv-provider
  - foundation-example-tests
exerciseIds:
  - ch40-exercise-01
  - ch40-exercise-02
  - ch40-exercise-03
termIds: []
sources:
  - id: fsharp-data-csv-provider
    url: https://fsprojects.github.io/FSharp.Data/library/CsvProvider.html
    checked: "2026-08-25"
  - id: ef-core-10
    url: https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-10.0/whatsnew
    checked: "2026-08-25"
  - id: dapper-nuget
    url: https://www.nuget.org/packages/Dapper/2.1.79
    checked: "2026-08-25"
  - id: deedle-nuget
    url: https://www.nuget.org/packages/Deedle/8.0.0
    checked: "2026-08-25"
  - id: plotly-net-nuget
    url: https://www.nuget.org/packages/Plotly.NET/5.1.0
    checked: "2026-08-25"
  - id: mlnet-overview
    url: https://learn.microsoft.com/en-us/dotnet/machine-learning/mldotnet-api
    checked: "2026-08-25"
  - id: onnxruntime-csharp
    url: https://onnxruntime.ai/docs/get-started/with-csharp.html
    checked: "2026-08-25"
  - id: dotnet-interactive-deprecation
    url: https://github.com/dotnet/interactive/issues/4163
    checked: "2026-08-25"
---

# 第 40 章练习答案 {#overview}

这些答案选择暂定边界，再说明能够推翻它们的证据。仅凭包语法无法决定事务所有权、模式兼容性、分析正确性或模型价值。

[返回第 40 章](../part-07/ch-40-data-analytics)。

## 练习 1：选择三个数据边界 {#exercise-01}

### 情况 A：事务型 PostgreSQL 预约数据 {#exercise-01-case-a}

从供应商 ADO.NET 提供器加 [Dapper 2.1.79](https://www.nuget.org/packages/Dapper/2.1.79) 开始，并把它们限制在一个基础设施适配器内。保持五条调优 SQL 显式，把持久化 DTO 映射到现有预约领域。

应用端口应描述副作用，而不是查询机制：

```fsharp
type BookingStore =
    { TryAppend:
        expectedVersion: int64 ->
        events: BookingEvent list ->
        CancellationToken ->
        Task<Result<int64, AppendError>> }
```

一个事务加载或检查当前版本，并执行类似 `WHERE version = @expectedVersion` 的条件写入。零条受影响行变成 `VersionConflict`；唯一约束违反变成已声明重复结果；不盲目重试结果不明的提交失败。精确 SQL 取决于模式与驱动，因此要在生产使用的同一 PostgreSQL 大版本上测试。

直接 ADO.NET 是对照候选。如果流式处理、批量操作、提供器专属类型或映射控制让 Dapper 辅助函数价值很小，它就可能胜出。只有变更跟踪、迁移或 EF Core 应用模型能提供已证明价值时，EF Core 10 才是另一候选；五条调优查询和显式事件/版本语义本身不足以证明跟踪实体合理。

试验必须证明：

- 每个值都参数化，任何动态标识符都来自允许列表；
- 事务所有权可见，连接/读取器得到释放；
- 取消传到打开、执行与读取操作；
- 五条查询返回有界投影并使用预期索引；
- 两个受控写入方使用同一期望版本时，得到一个成功与一个冲突；
- 提交附近连接丢失会产生已定义的对账或幂等停点；
- SQL 诊断暴露耗时与行数，但不记录敏感参数；
- 锁定还原、迁移、发布、启动与回滚在目标环境工作。

如果 Dapper 让记录/空值映射更复杂或没有持续价值，就选择直接 ADO.NET。如果代表性写聚合与读取查询表明 EF Core 工作单元和迁移图减少了总所有权，且实体没有泄漏进领域，就选择 EF Core。

### 情况 B：每周 CSV 分析与可访问图表 {#exercise-01-case-b}

从 FSharp.Data 有界摄取、[Deedle 8.0.0](https://www.nuget.org/packages/Deedle/8.0.0) 日期键对齐与缺失数据处理开始，并只在渲染边界使用 [Plotly.NET 5.1.0](https://www.nuget.org/packages/Plotly.NET/5.1.0)。

先把 Deedle 与普通记录加 `Map<DateOnly, _>` 比较。如果三个小文件只有一个唯一日期键和两次连接，类型化映射可能更清晰，并提供更强的行形状可见性。当重复外连接、对齐、重采样与缺失值策略主导分析时，Deedle 暂定胜出。

每周任务应当：

- 锁定三个合成模式样本，并分别验证每个运行期文件；
- 记录输入摘要、获取时间、区域性、编码与预期日期范围；
- 拒绝重复键，或按一条有文档的规则解决；
- 区分缺失观察、零与无效单元格；
- 在已编译且测试过的函数中计算图表数据表；
- 随图表一同输出 CSV 数据表与文字摘要；
- 使用显式单位、时区、分母、颜色与可访问标签；
- 打包或批准浏览器资源，并扫描 HTML/工具提示中的敏感数据；
- 从干净进程无界面运行，并比较不变汇总值。

如果交付目标要求其导出路径无法稳定复现的静态格式、浏览器策略拒绝其资源，或更简单的报表工具已经拥有可访问性与分发，Plotly.NET 就落选。分析计算仍然保留，因为它不返回图表对象。

### 情况 C：Python 训练模型与本地 30 ms 推理 {#exercise-01-case-c}

从 ONNX 导出和 [ONNX Runtime .NET 绑定](https://onnxruntime.ai/docs/get-started/with-csharp.html) 开始。它保留 Python 团队的训练生态，同时让推理留在进程内并避免网络跳转。

在 HTTP 输入定义版本化 `FeatureDto`，验证后映射为中立 `FeatureVector`。一个推理适配器拥有张量名称、顺序、数据类型、维度、归一化、会话生命周期、执行提供器和输出到决策的映射。不要让张量对象或模型专属列名进入领域。

验收证据包括：

- Python 与 .NET 路径运行同一组黄金向量，并在已声明容差内一致；
- 模型摘要、opset、特征模式、预处理版本、标签、阈值与训练数据标识一同传递；
- 新进程在就绪成功前加载并预热不可变模型；
- 无效形状、`NaN`、无穷值、缺失特征与未知模式版本在推理前失败；
- 有界并发负载测试在每种目标架构满足 30 ms 百分位预算；
- 原生运行时包被锁定、发布、漏洞审阅，并在部署镜像中执行；
- 诊断报告模型版本、延迟与有界结果，而不是原始特征；
- 上一产物仍可部署，回滚不需要模式降级。

ML.NET 是对照候选，因为它能导入 ONNX 并加入 .NET 转换。只有这些转换减少了自有预处理且黄金向量保持一致时，它才胜出。本地 Python sidecar 在初始比较中落选，因为它在没有已证明需求时重新引入进程启动、打包、序列化与健康边界。

## 练习 2：为 CSV 模式漂移设计 {#exercise-02}

### 让版本识别显式 {#exercise-02-recognition}

不要替换数据样例后就假设所有生产方已经原子切换。定义两个源契约，并根据封套版本、清单、由接收方控制的文件名约定或有界表头检查选择。只有区分列集合不可能重叠时，表头猜测才可作为后备。

| 源契约 | 必需列 | 可选/扩展规则 |
|---|---|---|
| v1 | `OrderId,Region,Product,Units,UnitPrice,OrderedAt` | 未知列遵循显式忽略或拒绝策略 |
| v2 | `OrderId,Region,Product,Units,Price,Currency` 加 `OrderedAt` | 语法上允许空白 `OrderedAt`；未知列使用同一已声明策略 |

为每个接受版本保留一个小型合成编译期样本。v2 样本必须包含空白日期，并有足够代表值来推断预期可选数值/日期形状。生成提供器类型留在私有 `V1Adapter` 与 `V2Adapter` 内。

如果上游频繁漂移或表头真正开放，就用显式 CSV 模式/解析器替换由样本派生的运行期解码，同时保留样本作为夹具。类型提供器是可选易用工具，不是兼容性权威。

### 通过源 DTO 规范化 {#exercise-02-normalization}

使用这个边界：

```text
有界 UTF-8 CSV
  -> 已识别的 v1 或 v2 源行
  -> 语法与列诊断
  -> 版本专属规范化
  -> 领域验证
  -> 已接受行或隔离证据
```

只有把 `Currency` 按允许的 ISO 货币集合解析后，`Price` 才成为金额候选。对于 v1，只有供应商契约保证时，才使用 USD 等版本化配置货币；否则 v1 无法安全构造金额，必须拒绝或转入补全。绝不要相加不同货币的小数。

空白 v2 `OrderedAt` 在源 DTO 中映射为 `None`。领域再决定缺失是否允许、是否由可信封套时间替代，或是否拒绝。非空无效日期不是 `None`，而是格式错误输入。

通过其他适配器使用的相同构造器规范化 `OrderId`、地区、数量与产品。在产生副作用前检测文件内重复订单 ID。把行号、源版本、安全错误代码与输入摘要附到隔离证据；普通日志中排除完整敏感行。

对于封闭契约，未知列应拒绝；对于扩展容忍契约，则忽略并记录。两个版本都应用同一策略。根据提供器行为静默切换会让兼容性变成偶然。

### 建立演进证据 {#exercise-02-evidence}

夹具应覆盖：

- 精确当前 v1 与 v2 样本；
- 在显式货币保证下，v1 `UnitPrice` 与 v2 `Price` 映射到相同金额；
- 缺失、空白、格式错误、重复与大小写不同的表头；
- 空白和无效 `OrderedAt`；
- 受支持、缺失、未知和大小写错误的货币代码；
- 零、负数、溢出与高边界数量/价格；
- 带引号逗号、引号、CRLF/LF、Unicode、BOM、无效 UTF-8 与大小/行数上限；
- 允许和禁止的未知列；
- 不得部分提交的混合或歧义文件；
- 提交前取消，以及隔离后的确定性重试。

在无网络的干净锁定构建中编译两个适配器。让它们规范化后接受的行运行同一套下游契约测试。用有界标签观察各源版本的接受/拒绝计数。

只有供应商契约和迁移窗口结束、生产观察在约定保留区间内不再出现 v1、留存重放不再需要 v1、所有调用方和夹具已迁移，且回滚不会恢复产生 v1 的生产方后，才删除 v1。即使删除可执行支持，也要保留迁移决定与安全模式样例。

## 练习 3：把探索模型生产化 {#exercise-03}

### 固定问题与数据血缘 {#exercise-03-lineage}

选择算法前，写清产品决定、预测范围、标签定义、观察单位、排除用途和误报/漏报成本。创建只读数据清单，包含源版本、抽取查询或任务修订、时间范围、行数、模式摘要、内容/对象版本与访问策略。

绝不要把实时生产导出复制进源码控制。把它存入获批不可变位置；仓库中只保留微型合成夹具。训练任务接收清单标识，而不是“latest.csv”。

按因果单位切分。对于重复客户、设备或事件，让同一实体只位于一侧。对于预测，用过去训练，并在更晚时间区间验证。只在训练数据上拟合插补器、编码器、缩放、特征选择与词表，再把已拟合转换应用到验证与测试集。

### 提取经过测试的特征管道 {#exercise-03-features}

把解析、验证、特征派生与指标计算从 `.fsx` 文件移入编译项目。为已验证观察定义普通记录与版本化特征模式。单元测试边界值，并添加与推理共享的黄金向量测试。

迁移期间只把脚本保留为薄调用方；若继续保留，则从干净进程用 `dotnet fsi` 运行。不要在 .NET Interactive 或 Polyglot Notebooks 上建立新工作流：两者都已弃用且项目已归档。如果仍维护的 notebook 前端对呈现有用，它应调用相同编译管道，并且不能成为唯一可执行记录。

### 不可变地训练、评估与打包 {#exercise-03-training}

无界面训练命令锁定 SDK/包，并记录代码修订、数据清单、切分定义、种子、确定性限制、特征版本、训练器/超参数、运行时/架构与资源耗时。它训练简单基线与候选模型。

报告适合任务的指标，包括置信度或重复运行变化、决策阈值、相关时的校准，以及重要群组/时间范围切片。与基线和产品成本比较。即使总体改善，只要必需切片退化就拒绝晋级。

晋级产物包含：

- 不可变模型字节与密码学摘要；
- 模型格式/opset 及必需运行时/原生包版本；
- 有序特征/输出模式、数据类型、维度、标签与阈值；
- 预处理产物或精确特征代码版本；
- 训练数据清单、代码修订、指标、切片与批准；
- 许可证、安全、预期用途、限制与到期/复核元数据。

当训练与服务有意共享 ML.NET 管道时选择 ML.NET 模型；当来自 Python 训练器的可移植性是稳定接缝时选择 ONNX。转换要用黄金向量与代表批次测试，不能只因导出成功就接受。

### 把推理部署为独立有主组件 {#exercise-03-inference}

启动时加载精确配置模型，验证摘要与模式，创建长生命周期会话/引擎，预热后才报告就绪。请求路径验证特征 DTO，应用匹配的预处理版本，调用有界并发池，把输出映射为已声明决定，并且永不记录原始敏感特征。

契约测试覆盖模式版本、黄金预测、阈值、无效浮点值、超时/取消、损坏/缺失模型、原生库失败与安全错误。负载测试在每种 CPU/GPU 架构的部署镜像中运行。真实进程冒烟证明发布布局与启动所有权。

以保护隐私的有界维度监控输入有效性、缺失度、特征范围、预测分布、延迟、故障、模型版本与延迟决策结果。漂移是调查信号，不是自动再训练命令。

策略允许时，通过影子或金丝雀比较部署新不可变产物。通过选择上一模型/服务产物回滚；在回滚窗口保持输入兼容。再训练必须经过相同评估与批准门，而不只是拥有更新的时间戳。

原始图表成为带源数据表与文字摘要的生成审阅产物。它永远不是晋级的唯一证据。

## 答案回顾 {#solution-review}

- 显式 SQL 与版本检查暂定适合小型事务 PostgreSQL 表面。
- 只有带标签对齐和缺失数据工作胜过类型化映射时，Deedle 才值得存在。
- Plotly.NET 留在分析计算之外，并必须满足可访问性与数据策略。
- ONNX 是 Python 训练与 .NET 推理间的版本化张量/预处理契约。
- 黄金向量测试跨运行时语义；延迟测试在每种真实目标架构运行。
- CSV 版本得到显式识别，并通过私有源适配器规范化。
- 空白、无效、未知与缺失在整个摄取过程中保持为不同状态。
- 货币需要代码与契约；仅有小数不是金额。
- v1 移除依赖生产方、观察、重放、夹具与回滚证据。
- 训练数据、切分、预处理、模型、指标与批准形成一条血缘。
- 探索在成为生产证据前迁移到已编译且测试过的函数。
- 服务拥有模型加载、并发、诊断、原生依赖与回滚。
- 漂移触发调查；它不会静默替换已部署模型。
- 每项建议在其声明证据失败时都保持可逆。

## 资料来源 {#sources}

- [FSharp.Data：CSV 类型提供器](https://fsprojects.github.io/FSharp.Data/library/CsvProvider.html)
- [Microsoft Learn：EF Core 10 新增功能](https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-10.0/whatsnew)
- [NuGet：Dapper 2.1.79](https://www.nuget.org/packages/Dapper/2.1.79)
- [NuGet：Deedle 8.0.0](https://www.nuget.org/packages/Deedle/8.0.0)
- [NuGet：Plotly.NET 5.1.0](https://www.nuget.org/packages/Plotly.NET/5.1.0)
- [Microsoft Learn：ML.NET 的工作方式](https://learn.microsoft.com/en-us/dotnet/machine-learning/mldotnet-api)
- [ONNX Runtime：C# API](https://onnxruntime.ai/docs/get-started/with-csharp.html)
- [Microsoft：弃用 Polyglot Notebooks 与 .NET Interactive](https://github.com/dotnet/interactive/issues/4163)
