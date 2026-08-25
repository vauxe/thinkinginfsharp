---
title: "附录 G：答案与开放题评审指南"
description: "访问 45 章全部答案，并在不伪称工程问题只有唯一标准答案的前提下评审封闭题、诊断题与开放设计题。"
translationKey: appendices/g-solutions-guide
kind: appendix
appendix: G
status: complete
exampleIds: []
exerciseIds: []
termIds: []
sources: []
---

# 附录 G：答案与开放题评审指南 {#overview}

答案是反馈，不能替代亲自作答。比较表层语法前，应先比较契约、类型、副作用与证据。即使打印出同一行，也仍可能错过建模、所有权、失败或互操作目标。

有些练习具有狭窄的可观察结果；另一些要求诊断或工程设计。因此答案页展示推理、约束和代表性实现，并不声称每个开放问题都有唯一标准答案。

本页由章节与答案元数据生成。检查器要求每章都有中英文答案页、练习标识完全相同、练习锚点可达，而且章节与答案之间双向链接。

## 打开答案之前 {#before-opening}

1. 用自己的话重述必需行为与每项显式约束。
2. 运行代码前，预测重要类型签名、输出、失败与副作用顺序。
3. 运行范围最小的相关命令；遇到意外证据时保留它，不要盲目把代码改成书中输出。
4. 解释自己的答案为什么满足任务，再查看解答并比较决策，而不是比较行数。

## 三类练习 {#exercise-kinds}

| 类别 | 可以直接检查什么 | 哪些地方允许变化 |
|---|---|---|
| 封闭行为题 | 必需值、类型、输出顺序、诊断或测试 | 名称与实现可不同，但必须保留完整契约 |
| 诊断题 | 复现命令、第一条相关证据、根因与修复 | 多种修复都可能编译，但只有保留预期语义的才合格 |
| 开放设计题 | 约束、不变量、边界、失败策略与验证计划 | 表示、库、架构和发布方式可随显式取舍变化 |

## 开放设计题评审维度 {#open-design-rubric}

| 维度 | 达到练习要求 | 有力证据 |
|---|---|---|
| 契约 | 覆盖每项规定输入、输出、失败与非目标 | 找出歧义并记录有界假设 |
| 模型 | 类型表达必需状态，且没有无谓仪式 | 非法状态无法构造，或只在一条清楚边界拒绝 |
| 副作用与所有权 | 指明 I/O、时间、可变性、资源与取消的位置 | 生命周期和部分失败行为可测试且局部受控 |
| API 与互操作 | 调用者能用自己的语言和工具自然消费表层 | 检查编译后调用点、可空性、兼容性与表示泄漏 |
| 证据 | 给出可复现构建、测试、探针，或明确标为资料审阅 | 测试反例，并区分已执行、已审阅与未验证主张 |
| 清晰度与范围 | 解决所问问题，同时不隐藏关键决策 | 比较一种可信替代方案并解释停止条件 |

## 可接受变体与硬性失败 {#acceptable-variation}

只要栈使用、顺序和所有权匹配，递归、折叠或小循环都可能正确。记录或类、函数或接口、列表或数组、`Result` 或领域联合、`Async` 或 `Task` 也都应从边界决定，而不是孤立地按风格加分。

不同答案若保留显式约束、公开新增假设，并提供与风险相称的证据，就是可接受变体。若你的版本更简单且至少同样充分地得到证明，应反过来改进书中答案。

若答案屏蔽相关警告、用通配符隐藏新联合案例、以计时 sleep 充当并发证明、泄漏机密或偶然表示、把未运行的平台检查报告为通过、静默改变公共契约，或只给输出却不解释因果模型，就应判为不合格。

## 让主张与证据匹配 {#evidence}

| 主张 | 最低合适证据 |
|---|---|
| 类型关系或诊断 | 锁定编译器调用与精确相关签名/编号 |
| 纯行为或不变量 | 聚焦示例/性质测试，并含反例或边界 |
| 资源、异步、并发或互操作行为 | 使用确定性协调与清理的真实边界测试 |
| 框架/平台采用 | 可编译最小切片，加明确未测平台/部署边界 |
| 拟议架构或包选择 | 书面约束、官方资料审阅、试验计划与回滚/移除条件 |

答案页里的设计叙述并非全都是仓库中已执行的制品。各页会区分可运行样例证据、编译器证据、官方资料审阅与拟议工作。不要只因一项提案出现在“答案”之下，就把它升级成“已验证”。

## 全部章节答案 {#answer-index}

下面每个练习链接都指向精确的答案标题。“评审重点”取自对应答案页，用于概括比较时应学到什么。

## 第一部分 · 表达式与函数 {#part-1}

### 第 1 章：第一次 F# 会话 {#chapter-01}

[本章](../part-01/ch-01-first-session#overview) · [答案页](../solutions/ch-01-first-session#overview)

**各题答案:** [练习 1](../solutions/ch-01-first-session#exercise-01) · [练习 2](../solutions/ch-01-first-session#exercise-02) · [练习 3](../solutions/ch-01-first-session#exercise-03)

**评审重点:** 第一次 F# 会话的推理过程、迁移示例与运行入口选择。

### 第 2 章：值、绑定与表达式 {#chapter-02}

[本章](../part-01/ch-02-values-bindings-expressions#overview) · [答案页](../solutions/ch-02-values-bindings-expressions#overview)

**各题答案:** [练习 1](../solutions/ch-02-values-bindings-expressions#exercise-01) · [练习 2](../solutions/ch-02-values-bindings-expressions#exercise-02) · [练习 3](../solutions/ch-02-values-bindings-expressions#exercise-03)

**评审重点:** 值、绑定、基本类型、显式转换与局部遮蔽的推理答案。

### 第 3 章：函数也是值 {#chapter-03}

[本章](../part-01/ch-03-functions-as-values#overview) · [答案页](../solutions/ch-03-functions-as-values#overview)

**各题答案:** [练习 1](../solutions/ch-03-functions-as-values#exercise-01) · [练习 2](../solutions/ch-03-functions-as-values#exercise-02) · [练习 3](../solutions/ch-03-functions-as-values#exercise-03)

**评审重点:** 函数类型、匿名函数、高阶函数、柯里化、元组参数与部分应用的推理答案。

### 第 4 章：分支与基本模式 {#chapter-04}

[本章](../part-01/ch-04-branching-patterns#overview) · [答案页](../solutions/ch-04-branching-patterns#overview)

**各题答案:** [练习 1](../solutions/ch-04-branching-patterns#exercise-01) · [练习 2](../solutions/ch-04-branching-patterns#exercise-02) · [练习 3](../solutions/ch-04-branching-patterns#exercise-03)

**评审重点:** 条件结果、匹配顺序、守卫、元组与列表模式的推理答案。

### 第 5 章：列表、管道与数据流 {#chapter-05}

[本章](../part-01/ch-05-lists-pipelines#overview) · [答案页](../solutions/ch-05-lists-pipelines#overview)

**各题答案:** [练习 1](../solutions/ch-05-lists-pipelines#exercise-01) · [练习 2](../solutions/ch-05-lists-pipelines#exercise-02) · [练习 3](../solutions/ch-05-lists-pipelines#exercise-03)

**评审重点:** 列表变换、管道、choose、for、while 与局部可变状态的推理答案。

### 第 6 章：递归、尾调用与折叠 {#chapter-06}

[本章](../part-01/ch-06-recursion-folds#overview) · [答案页](../solutions/ch-06-recursion-folds#overview)

**各题答案:** [练习 1](../solutions/ch-06-recursion-folds#exercise-01) · [练习 2](../solutions/ch-06-recursion-folds#exercise-02) · [练习 3](../solutions/ch-06-recursion-folds#exercise-03)

**评审重点:** 结构递归、累加器不变量、尾调用与左右折叠的推理答案。

## 第二部分 · 用类型建立模型 {#part-2}

### 第 7 章：记录、更新、相等与比较 {#chapter-07}

[本章](../part-02/ch-07-records-equality#overview) · [答案页](../solutions/ch-07-records-equality#overview)

**各题答案:** [练习 1](../solutions/ch-07-records-equality#exercise-01) · [练习 2](../solutions/ch-07-records-equality#exercise-02) · [练习 3](../solutions/ch-07-records-equality#exercise-03)

**评审重点:** 元组迁移、不可变更新、结构相等、引用身份、哈希契约与业务排序的推理答案。

### 第 8 章：可辨识联合与状态建模 {#chapter-08}

[本章](../part-02/ch-08-discriminated-unions#overview) · [答案页](../solutions/ch-08-discriminated-unions#overview)

**各题答案:** [练习 1](../solutions/ch-08-discriminated-unions#exercise-01) · [练习 2](../solutions/ch-08-discriminated-unions#exercise-02) · [练习 3](../solutions/ch-08-discriminated-unions#exercise-03)

**评审重点:** 标志组合、联合案例、穷尽性与状态转换策略的推理答案。

### 第 9 章：缺失与预期失败 {#chapter-09}

[本章](../part-02/ch-09-option-result#overview) · [答案页](../solutions/ch-09-option-result#overview)

**各题答案:** [练习 1](../solutions/ch-09-option-result#exercise-01) · [练习 2](../solutions/ch-09-option-result#exercise-02) · [练习 3](../solutions/ch-09-option-result#exercise-03)

**评审重点:** 围绕 option、Result、组合、短路和结构化错误上下文进行推理。

### 第 10 章：递归类型与结构递归 {#chapter-10}

[本章](../part-02/ch-10-recursive-types#overview) · [答案页](../solutions/ch-10-recursive-types#overview)

**各题答案:** [练习 1](../solutions/ch-10-recursive-types#exercise-01) · [练习 2](../solutions/ch-10-recursive-types#exercise-02) · [练习 3](../solutions/ch-10-recursive-types#exercise-03)

**评审重点:** 从递归案例推导短路查询、map 定律和单次遍历的树摘要。

### 第 11 章：泛型、约束与度量单位 {#chapter-11}

[本章](../part-02/ch-11-generics-constraints#overview) · [答案页](../solutions/ch-11-generics-constraints#overview)

**各题答案:** [练习 1](../solutions/ch-11-generics-constraints#exercise-01) · [练习 2](../solutions/ch-11-generics-constraints#exercise-02) · [练习 3](../solutions/ch-11-generics-constraints#exercise-03)

**评审重点:** 推断泛型签名、按意图修复值限制，并跨边界保留度量量纲。

### 第 12 章：让非法状态无法表示 {#chapter-12}

[本章](../part-02/ch-12-making-illegal-states-unrepresentable#overview) · [答案页](../solutions/ch-12-making-illegal-states-unrepresentable#overview)

**各题答案:** [练习 1](../solutions/ch-12-making-illegal-states-unrepresentable#exercise-01) · [练习 2](../solutions/ch-12-making-illegal-states-unrepresentable#exercise-02) · [练习 3](../solutions/ch-12-making-illegal-states-unrepresentable#exercise-03)

**评审重点:** 保护有界值、选择外层记录边界，并修正把容量与可用量混在一起的跨文件 API。

## 第三部分 · 组合与程序结构 {#part-3}

### 第 13 章：组合、参数顺序与管道 API {#chapter-13}

[本章](../part-03/ch-13-composition-pipeline-api#overview) · [答案页](../solutions/ch-13-composition-pipeline-api#overview)

**各题答案:** [练习 1](../solutions/ch-13-composition-pipeline-api#exercise-01) · [练习 2](../solutions/ch-13-composition-pipeline-api#exercise-02) · [练习 3](../solutions/ch-13-composition-pipeline-api#exercise-03)

**评审重点:** 在管道与组合之间转换调用、排列代表性 F# API，并简化装饰性管道。

### 第 14 章：集合选择与求值模型 {#chapter-14}

[本章](../part-03/ch-14-collections-evaluation#overview) · [答案页](../solutions/ch-14-collections-evaluation#overview)

**各题答案:** [练习 1](../solutions/ch-14-collections-evaluation#exercise-01) · [练习 2](../solutions/ch-14-collections-evaluation#exercise-02) · [练习 3](../solutions/ch-14-collections-evaluation#exercise-03)

**评审重点:** 根据工作负载选择集合、精确计算延迟请求量，并区分有序键与基于相等的哈希键。

### 第 15 章：活动模式与领域匹配边界 {#chapter-15}

[本章](../part-03/ch-15-active-patterns#overview) · [答案页](../solutions/ch-15-active-patterns#overview)

**各题答案:** [练习 1](../solutions/ch-15-active-patterns#exercise-01) · [练习 2](../solutions/ch-15-active-patterns#exercise-02) · [练习 3](../solutions/ch-15-active-patterns#exercise-03)

**评审重点:** 建立完整领域视图、保留解析错误，并把数据库工作移出活动模式匹配。

### 第 16 章：模块、命名空间、项目与编译设置 {#chapter-16}

[本章](../part-03/ch-16-modules-namespaces-projects#overview) · [答案页](../solutions/ch-16-modules-namespaces-projects#overview)

**各题答案:** [练习 1](../solutions/ch-16-modules-namespaces-projects#exercise-01) · [练习 2](../solutions/ch-16-modules-namespaces-projects#exercise-02) · [练习 3](../solutions/ch-16-modules-namespaces-projects#exercise-03)

**评审重点:** 排列多文件项目，修复命名空间级绑定，并让显式可空引用契约通过包装函数继续传递。

### 第 17 章：签名、访问控制与面向 F# 的 API {#chapter-17}

[本章](../part-03/ch-17-signatures-encapsulation#overview) · [答案页](../solutions/ch-17-signatures-encapsulation#overview)

**各题答案:** [练习 1](../solutions/ch-17-signatures-encapsulation#exercise-01) · [练习 2](../solutions/ch-17-signatures-encapsulation#exercise-02) · [练习 3](../solutions/ch-17-signatures-encapsulation#exercise-03)

**评审重点:** 规定抽象电子邮件类型，收窄不一致的分配表面，并让函数元数与辅助函数可访问性在签名文件对中保持一致。

### 第 18 章：显式工作流组合与验证累积 {#chapter-18}

[本章](../part-03/ch-18-workflow-validation#overview) · [答案页](../solutions/ch-18-workflow-validation#overview)

**各题答案:** [练习 1](../solutions/ch-18-workflow-validation#exercise-01) · [练习 2](../solutions/ch-18-workflow-validation#exercise-02) · [练习 3](../solutions/ch-18-workflow-validation#exercise-03)

**评审重点:** 分离纯检查、依赖检查和有副作用检查，实现有序错误累积，并把未说明的计算表达式改写为显式语义。

## 第四部分 · 副作用、异步与并发 {#part-4}

### 第 19 章：.NET API 与空值边界 {#chapter-19}

[本章](../part-04/ch-19-dotnet-null-boundaries#overview) · [答案页](../solutions/ch-19-dotnet-null-boundaries#overview)

**各题答案:** [练习 1](../solutions/ch-19-dotnet-null-boundaries#exercise-01) · [练习 2](../solutions/ch-19-dotnet-null-boundaries#exercise-02) · [练习 3](../solutions/ch-19-dotnet-null-boundaries#exercise-03)

**评审重点:** 分类可空边界，在不抹掉失败的前提下包装真实 .NET 可空返回，并证明 option 载荷为何仍可能为 null。

### 第 20 章：函数式核心与副作用边界 {#chapter-20}

[本章](../part-04/ch-20-functional-core-effects#overview) · [答案页](../solutions/ch-20-functional-core-effects#overview)

**各题答案:** [练习 1](../solutions/ch-20-functional-core-effects#exercise-01) · [练习 2](../solutions/ch-20-functional-core-effects#exercise-02) · [练习 3](../solutions/ch-20-functional-core-effects#exercise-03)

**评审重点:** 暴露隐藏运行时输入，选择最小而诚实的依赖形状，并保留预期边界失败而不压平契约违规。

### 第 21 章：异常、资源与 I/O {#chapter-21}

[本章](../part-04/ch-21-exceptions-resources-io#overview) · [答案页](../solutions/ch-21-exceptions-resources-io#overview)

**各题答案:** [练习 1](../solutions/ch-21-exceptions-resources-io#exercise-01) · [练习 2](../solutions/ch-21-exceptions-resources-io#exercise-02) · [练习 3](../solutions/ch-21-exceptions-resources-io#exercise-03)

**评审重点:** 把资源安全读取与纯解析组合，用结构化策略替换全捕获字符串，并验证双 reader 在成功与失败时都会释放。

### 第 22 章：Async<'T> 与 Task<'T> {#chapter-22}

[本章](../part-04/ch-22-async-task#overview) · [答案页](../solutions/ch-22-async-task#overview)

**各题答案:** [练习 1](../solutions/ch-22-async-task#exercise-01) · [练习 2](../solutions/ch-22-async-task#exercise-02) · [练习 3](../solutions/ch-22-async-task#exercise-03)

**评审重点:** 用闩锁证明 async 与 task 的启动语义，组合 Task API 与 Async 验证器，并明确单次执行所有权。

### 第 23 章：取消、超时、故障与释放 {#chapter-23}

[本章](../part-04/ch-23-cancellation-timeouts#overview) · [答案页](../solutions/ch-23-cancellation-timeouts#overview)

**各题答案:** [练习 1](../solutions/ch-23-cancellation-timeouts#exercise-01) · [练习 2](../solutions/ch-23-cancellation-timeouts#exercise-02) · [练习 3](../solutions/ch-23-cancellation-timeouts#exercise-03)

**评审重点:** 验证令牌传播，用信号实现放弃等待与取消工作的超时策略，并测试编译代码中的异步释放。

### 第 24 章：并行、并发、代理与受控可变性 {#chapter-24}

[本章](../part-04/ch-24-concurrency-agents-state#overview) · [答案页](../solutions/ch-24-concurrency-agents-state#overview)

**各题答案:** [练习 1](../solutions/ch-24-concurrency-agents-state#exercise-01) · [练习 2](../solutions/ch-24-concurrency-agents-state#exercise-02) · [练习 3](../solutions/ch-24-concurrency-agents-state#exercise-03)

**评审重点:** 根据不变量选择协调方式，在不假设消息顺序的情况下扩展预约代理，并让缓存失效与重复工作策略可执行。

## 第五部分 · .NET 互操作与工程质量 {#part-5}

### 第 25 章：在 F# 中定义对象 {#chapter-25}

[本章](../part-05/ch-25-objects-interfaces#overview) · [答案页](../solutions/ch-25-objects-interfaces#overview)

**各题答案:** [练习 1](../solutions/ch-25-objects-interfaces#exercise-01) · [练习 2](../solutions/ch-25-objects-interfaces#exercise-02) · [练习 3](../solutions/ch-25-objects-interfaces#exercise-03)

**评审重点:** 移除仪式性类、比较函数与接口策略边界，并重新设计结构体，让其默认表示有效。

### 第 26 章：深入 .NET 边界 {#chapter-26}

[本章](../part-05/ch-26-dotnet-runtime-boundaries#overview) · [答案页](../solutions/ch-26-dotnet-runtime-boundaries#overview)

**各题答案:** [练习 1](../solutions/ch-26-dotnet-runtime-boundaries#exercise-01) · [练习 2](../solutions/ch-26-dotnet-runtime-boundaries#exercise-02) · [练习 3](../solutions/ch-26-dotnet-runtime-boundaries#exercise-03)

**评审重点:** 只解码一次对象输入、拥有事件订阅，并证明自定义字典比较器遵守相等与哈希契约。

### 第 27 章：为 C# 设计 F# API {#chapter-27}

[本章](../part-05/ch-27-fsharp-api-for-csharp#overview) · [答案页](../solutions/ch-27-fsharp-api-for-csharp#overview)

**各题答案:** [练习 1](../solutions/ch-27-fsharp-api-for-csharp#exercise-01) · [练习 2](../solutions/ch-27-fsharp-api-for-csharp#exercise-02) · [练习 3](../solutions/ch-27-fsharp-api-for-csharp#exercise-03)

**评审重点:** 把泄露的 F# 结果投影为受控 .NET 响应，用重载演进查询，并用专用 DTO 隔离序列化要求。

### 第 28 章：示例测试、替身与边界测试 {#chapter-28}

[本章](../part-05/ch-28-testing-boundaries#overview) · [答案页](../solutions/ch-28-testing-boundaries#overview)

**各题答案:** [练习 1](../solutions/ch-28-testing-boundaries#exercise-01) · [练习 2](../solutions/ch-28-testing-boundaries#exercise-02) · [练习 3](../solutions/ch-28-testing-boundaries#exercise-03)

**评审重点:** 按风险选择最小测试层，为缺失产品路径编写手写替身，并设计可选 JSON 字段的兼容演进。

### 第 29 章：使用 FsCheck 进行性质测试 {#chapter-29}

[本章](../part-05/ch-29-property-testing#overview) · [答案页](../solutions/ch-29-property-testing#overview)

**各题答案:** [练习 1](../solutions/ch-29-property-testing#exercise-01) · [练习 2](../solutions/ch-29-property-testing#exercise-02) · [练习 3](../solutions/ch-29-property-testing#exercise-03)

**评审重点:** 推导独立的流式性质，设计合法标识符的生成器与缩减器，并把顺序敏感反例转成持久回归示例。

### 第 30 章：诊断、调试、格式化与构建 {#chapter-30}

[本章](../part-05/ch-30-diagnostics-tooling-builds#overview) · [答案页](../solutions/ch-30-diagnostics-tooling-builds#overview)

**各题答案:** [练习 1](../solutions/ch-30-diagnostics-tooling-builds#exercise-01) · [练习 2](../solutions/ch-30-diagnostics-tooling-builds#exercise-02) · [练习 3](../solutions/ch-30-diagnostics-tooling-builds#exercise-03)

**评审重点:** 修复 F# 文件顺序导致的级联错误，为 FSI、测试和调试器分配不同问题，并审计一个被有意改动的锁定依赖图。

### 第 31 章：先测量再优化 {#chapter-31}

[本章](../part-05/ch-31-measure-before-optimizing#overview) · [答案页](../solutions/ch-31-measure-before-optimizing#overview)

**各题答案:** [练习 1](../solutions/ch-31-measure-before-optimizing#exercise-01) · [练习 2](../solutions/ch-31-measure-before-optimizing#exercise-02) · [练习 3](../solutions/ch-31-measure-before-optimizing#exercise-03)

**评审重点:** 把结论约束在已采集基准内，设计保持行为的 option 与 voption 分配实验，并为三种不同系统症状选择证据。

### 第 32 章：从函数到应用 {#chapter-32}

[本章](../part-05/ch-32-functions-to-applications#overview) · [答案页](../solutions/ch-32-functions-to-applications#overview)

**各题答案:** [练习 1](../solutions/ch-32-functions-to-applications#exercise-01) · [练习 2](../solutions/ch-32-functions-to-applications#exercise-02) · [练习 3](../solutions/ch-32-functions-to-applications#exercise-03)

**评审重点:** 推导狭窄的发货端口与所有权，设计有界的可观察信号，并依据具体生命周期需求选择应用宿主。

## 第六部分 · 活动预约系统 {#part-6}

### 第 33 章：业务语言、命令、事件与模型 {#chapter-33}

[本章](../part-06/ch-33-domain-language-model#overview) · [答案页](../solutions/ch-33-domain-language-model#overview)

**各题答案:** [练习 1](../solutions/ch-33-domain-language-model#exercise-01) · [练习 2](../solutions/ch-33-domain-language-model#exercise-02) · [练习 3](../solutions/ch-33-domain-language-model#exercise-03)

**评审重点:** 按角色分类预约值，在不跨越边界的情况下设计座位变更命令与事实，并依据明确保证选择持久化方式。

### 第 34 章：纯预约工作流与验证 {#chapter-34}

[本章](../part-06/ch-34-pure-booking-workflow#overview) · [答案页](../solutions/ch-34-pure-booking-workflow#overview)

**各题答案:** [练习 1](../solutions/ch-34-pure-booking-workflow#exercise-01) · [练习 2](../solutions/ch-34-pure-booking-workflow#exercise-02) · [练习 3](../solutions/ch-34-pure-booking-workflow#exercise-03)

**评审重点:** 追踪预订错误优先级，将独立验证扩展到三个字段，并比较取消优先级策略。

### 第 35 章：端口、持久化、配置与替身 {#chapter-35}

[本章](../part-06/ch-35-ports-persistence-config#overview) · [答案页](../solutions/ch-35-ports-persistence-config#overview)

**各题答案:** [练习 1](../solutions/ch-35-ports-persistence-config#exercise-01) · [练习 2](../solutions/ch-35-ports-persistence-config#exercise-02) · [练习 3](../solutions/ch-35-ports-persistence-config#exercise-03)

**评审重点:** 演进带版本快照，审计替换过程中的中断点，并为借用的生产客户端重新设计组合。

### 第 36 章：Web API、JSON 与输入边界 {#chapter-36}

[本章](../part-06/ch-36-web-api-boundaries#overview) · [答案页](../solutions/ch-36-web-api-boundaries#overview)

**各题答案:** [练习 1](../solutions/ch-36-web-api-boundaries#exercise-01) · [练习 2](../solutions/ch-36-web-api-boundaries#exercise-02) · [练习 3](../solutions/ch-36-web-api-boundaries#exercise-03)

**评审重点:** 在自动绑定下保留 HTTP 契约，推理不明确效果，并为不同部署拓扑分配安全控制。

### 第 37 章：一致性、幂等、重试与部分失败 {#chapter-37}

[本章](../part-06/ch-37-consistency-idempotency#overview) · [答案页](../solutions/ch-37-consistency-idempotency#overview)

**各题答案:** [练习 1](../solutions/ch-37-consistency-idempotency#exercise-01) · [练习 2](../solutions/ch-37-consistency-idempotency#exercise-02) · [练习 3](../solutions/ch-37-consistency-idempotency#exercise-03)

**评审重点:** 把容量控制扩展到跨进程，对账结果不明确的支付，并设计不冒充恰好一次投递的发件箱。

### 第 38 章：集成、诊断、C# 客户端与发布证据 {#chapter-38}

[本章](../part-06/ch-38-integration-diagnostics-release#overview) · [答案页](../solutions/ch-38-integration-diagnostics-release#overview)

**各题答案:** [练习 1](../solutions/ch-38-integration-diagnostics-release#exercise-01) · [练习 2](../solutions/ch-38-integration-diagnostics-release#exercise-02) · [练习 3](../solutions/ch-38-integration-diagnostics-release#exercise-03)

**评审重点:** 审计夸大的保证，设计受限遥测收集，并把本地预约检查变成具体发布计划。

## 第七部分 · 生态地图 {#part-7}

### 第 39 章：ASP.NET Core 与 F# Web 生态 {#chapter-39}

[本章](../part-07/ch-39-web-ecosystem#overview) · [答案页](../solutions/ch-39-web-ecosystem#overview)

**各题答案:** [练习 1](../solutions/ch-39-web-ecosystem#exercise-01) · [练习 2](../solutions/ch-39-web-ecosystem#exercise-02) · [练习 3](../solutions/ch-39-web-ecosystem#exercise-03)

**评审重点:** 为具体团队选择 Web 表面，设计保留契约的 Falco 试验，并可逆地迁移绑定框架的端点。

### 第 40 章：数据、类型提供器、分析与机器学习 {#chapter-40}

[本章](../part-07/ch-40-data-analytics#overview) · [答案页](../solutions/ch-40-data-analytics#overview)

**各题答案:** [练习 1](../solutions/ch-40-data-analytics#exercise-01) · [练习 2](../solutions/ch-40-data-analytics#exercise-02) · [练习 3](../solutions/ch-40-data-analytics#exercise-03)

**评审重点:** 选择有界数据工具，显式吸收 CSV 模式漂移，并把探索分类器转化为可复现的训练与推理系统。

### 第 41 章：Fable、Elmish 与浏览器应用 {#chapter-41}

[本章](../part-07/ch-41-fable-elmish#overview) · [答案页](../solutions/ch-41-fable-elmish#overview)

**各题答案:** [练习 1](../solutions/ch-41-fable-elmish#exercise-01) · [练习 2](../solutions/ch-41-fable-elmish#exercise-02) · [练习 3](../solutions/ch-41-fable-elmish#exercise-03)

**评审重点:** 选择与问题成比例的浏览器架构，拒绝陈旧异步结果，并按诚实的运行时边界拆分共享定价库。

### 第 42 章：云、容器、Serverless 与 .NET Aspire {#chapter-42}

[本章](../part-07/ch-42-cloud-containers-aspire#overview) · [答案页](../solutions/ch-42-cloud-containers-aspire#overview)

**各题答案:** [练习 1](../solutions/ch-42-cloud-containers-aspire#exercise-01) · [练习 2](../solutions/ch-42-cloud-containers-aspire#exercise-02) · [练习 3](../solutions/ch-42-cloud-containers-aspire#exercise-03)

**评审重点:** 选择合乎比例的计算模型，把 X42 切片转化为发布提案，并用诚实的未知结果设计幂等事件消费者。

### 第 43 章：Avalonia、桌面端与移动端 {#chapter-43}

[本章](../part-07/ch-43-avalonia-desktop-mobile#overview) · [答案页](../solutions/ch-43-avalonia-desktop-mobile#overview)

**各题答案:** [练习 1](../solutions/ch-43-avalonia-desktop-mobile#exercise-01) · [练习 2](../solutions/ch-43-avalonia-desktop-mobile#exercise-02) · [练习 3](../solutions/ch-43-avalonia-desktop-mobile#exercise-03)

**评审重点:** 选择合乎比例的 UI 边界，把已验证 Avalonia 切片变成桌面发布计划，并设计诚实的移动项目图与证据图。

### 第 44 章：Unity 6.3 LTS 与 F# {#chapter-44}

[本章](../part-07/ch-44-unity#overview) · [答案页](../solutions/ch-44-unity#overview)

**各题答案:** [练习 1](../solutions/ch-44-unity#exercise-01) · [练习 2](../solutions/ch-44-unity#exercise-02) · [练习 3](../solutions/ch-44-unity#exercise-03)

**评审重点:** 选择合乎比例的 F#/C# Unity 边界，通过诚实的 IL2CPP 证据计划提升 X44，并在不隐藏 AOT 风险的前提下设计带版本任务数据。

### 第 45 章：脚本、自动化、包生态与继续学习 {#chapter-45}

[本章](../part-07/ch-45-scripting-packages-next#overview) · [答案页](../solutions/ch-45-scripting-packages-next#overview)

**各题答案:** [练习 1](../solutions/ch-45-scripting-packages-next#exercise-01) · [练习 2](../solutions/ch-45-scripting-packages-next#exercise-02) · [练习 3](../solutions/ch-45-scripting-packages-next#exercise-03)

**评审重点:** 扩展确定性产物自动化，在不夸大证据的前提下评估当前命令行包，并把本书转化为十二周 F# 交付循环。

## 最终自我评审 {#final-review}

- 你能否不依赖答案文字，解释推断类型或公共类型？
- 是否保留了顺序、求值、所有权、失败、取消与兼容性要求？
- 哪些证据真实运行过，哪些主张只是资料审阅或拟议边界？
- 哪个反例能区分你的设计与表面相似但错误的设计？
- 若答案不同，审阅者能否看到取舍，以及你会改用书中版本的条件？
