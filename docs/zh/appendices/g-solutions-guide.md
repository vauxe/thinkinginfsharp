---
title: "附录 G：答案与开放题评审指南"
description: "访问 45 章全部答案，并按不同标准评审固定答案题、诊断题和开放设计题，不把工程问题说成只有唯一解。"
translationKey: appendices/g-solutions-guide
---

# 附录 G：答案与开放题评审指南 {#overview}

答案用于反馈，不能替代亲自作答。先比较题目要求、类型、副作用和实际结果，再比较代码写法。即使打印出同一行，也可能没有满足建模、资源管理、失败处理或互操作要求。

有些练习只有一个明确结果；另一些要求诊断或工程设计。因此，答案页会展示推理、限制条件和代表性实现，但不会把开放问题说成只有唯一标准答案。

每章都链接到对应答案页。请先独立作答，再用答案比较推理过程与取舍。

## 打开答案之前 {#before-opening}

1. 用自己的话重述必需行为与每项已写明的限制。
2. 运行代码前，预测重要类型签名、输出、失败与副作用顺序。
3. 运行最小的相关命令；遇到意外输出或诊断时保留下来，不要盲目把代码改成书中答案。
4. 解释自己的答案为什么满足任务，再查看解答并比较决策，而不是比较行数。

## 三类练习 {#exercise-kinds}

| 类别 | 可以直接检查什么 | 哪些地方允许变化 |
|---|---|---|
| 固定答案题 | 必需值、类型、输出顺序、诊断或测试 | 名称与实现可不同，但必须满足全部要求 |
| 诊断题 | 复现命令、第一条相关证据、根因与修复 | 多种修复都可能编译，但只有保留预期语义的才合格 |
| 开放设计题 | 约束、不变量、边界、失败策略与验证计划 | 表示、库、架构和发布方式可随显式取舍变化 |

## 开放设计题评审维度 {#open-design-rubric}

| 维度 | 达到练习要求 | 优秀答案还会 |
|---|---|---|
| 要求 | 覆盖每项规定输入、输出、失败与非目标 | 找出歧义，并把假设限制在明确范围内 |
| 模型 | 类型表达必需状态，没有多余包装 | 非法状态无法构造，或只在一个明确入口被拒绝 |
| 副作用与所有者 | 写明 I/O、时间、可变状态、资源与取消发生在哪里 | 生命周期和部分失败行为可测试，并由局部代码控制 |
| API 与互操作 | 调用方能用自己的语言和工具自然使用 API | 检查编译后的调用方式、可空性、兼容性与内部表示泄漏 |
| 验证 | 给出可复现构建、测试、小型验证程序，或明确标为资料审阅 | 测试反例，并区分已经执行、只做资料审阅和尚未验证的结论 |
| 清晰度与范围 | 解决所问问题，同时不隐藏关键决策 | 比较一种可信替代方案并解释停止条件 |

## 可接受变体与硬性失败 {#acceptable-variation}

只要栈使用、顺序和状态管理符合要求，递归、折叠或小循环都可能正确。记录还是类、函数还是接口、列表还是数组、`Result` 还是领域联合、`Async` 还是 `Task`，也应由需求和调用方决定，不能只按个人风格评分。

不同答案若满足已写明的限制、说明新增假设，并验证了真正重要的风险，就可以接受。若你的版本更简单，而且验证程度不低于书中方案，就应反过来改进书中答案。

答案出现以下任一情况，就应判为不合格：

- 屏蔽相关警告，或用通配符隐藏新增联合案例；
- 用定时 `sleep` 证明并发行为；
- 泄漏机密或意外公开内部表示；
- 把尚未运行的平台检查报告为通过；
- 未说明就修改公共 API；
- 只给输出，不解释产生该结果的原因。

## 让主张与证据匹配 {#evidence}

| 主张 | 最低合适证据 |
|---|---|
| 类型关系或诊断 | 具体的 SDK/编译器命令，以及相关签名或诊断编号 |
| 纯行为或不变量 | 聚焦示例/属性测试，并含反例或边界 |
| 资源、异步、并发或互操作行为 | 使用确定性协调与清理的真实边界测试 |
| 框架/平台采用 | 可编译最小切片，加明确未测平台/部署边界 |
| 拟议架构或包选择 | 书面约束、官方资料审阅、试验计划与回滚/移除条件 |

答案页描述的设计并非都已经执行。应区分自己运行过的代码、编译器结果、官方资料审阅和尚未实施的方案。不要只因一项方案出现在“答案”之下，就把它说成“已经验证”。

## 全部章节答案 {#answer-index}

下面每个练习链接都直接跳到对应答案标题。“评审重点”取自答案页，用于概括比较时应学到什么。

## 第一部分 · 表达式与函数 {#part-1}

### 第 1 章：第一次 F# 会话 {#chapter-01}

[本章](../part-01/ch-01-first-session#overview) · [答案页](../solutions/ch-01-first-session#overview)

**各题答案:** [练习 1](../solutions/ch-01-first-session#exercise-01) · [练习 2](../solutions/ch-01-first-session#exercise-02) · [练习 3](../solutions/ch-01-first-session#exercise-03)

**评审重点:** 完成第一次 F# 会话，把一个小型命令式示例改写成 F#，并在 FSI、脚本和项目之间选择运行方式。

### 第 2 章：值、绑定与表达式 {#chapter-02}

[本章](../part-01/ch-02-values-bindings-expressions#overview) · [答案页](../solutions/ch-02-values-bindings-expressions#overview)

**各题答案:** [练习 1](../solutions/ch-02-values-bindings-expressions#exercise-01) · [练习 2](../solutions/ch-02-values-bindings-expressions#exercise-02) · [练习 3](../solutions/ch-02-values-bindings-expressions#exercise-03)

**评审重点:** 值、绑定、基本类型、明确的数值转换与局部遮蔽。

### 第 3 章：函数也是值 {#chapter-03}

[本章](../part-01/ch-03-functions-as-values#overview) · [答案页](../solutions/ch-03-functions-as-values#overview)

**各题答案:** [练习 1](../solutions/ch-03-functions-as-values#exercise-01) · [练习 2](../solutions/ch-03-functions-as-values#exercise-02) · [练习 3](../solutions/ch-03-functions-as-values#exercise-03)

**评审重点:** 函数类型、匿名函数、高阶函数、柯里化、元组参数与部分应用。

### 第 4 章：分支与基本模式 {#chapter-04}

[本章](../part-01/ch-04-branching-patterns#overview) · [答案页](../solutions/ch-04-branching-patterns#overview)

**各题答案:** [练习 1](../solutions/ch-04-branching-patterns#exercise-01) · [练习 2](../solutions/ch-04-branching-patterns#exercise-02) · [练习 3](../solutions/ch-04-branching-patterns#exercise-03)

**评审重点:** 条件表达式的结果、匹配顺序、守卫，以及元组与列表模式。

### 第 5 章：列表、管道与数据流 {#chapter-05}

[本章](../part-01/ch-05-lists-pipelines#overview) · [答案页](../solutions/ch-05-lists-pipelines#overview)

**各题答案:** [练习 1](../solutions/ch-05-lists-pipelines#exercise-01) · [练习 2](../solutions/ch-05-lists-pipelines#exercise-02) · [练习 3](../solutions/ch-05-lists-pipelines#exercise-03)

**评审重点:** 列表变换、管道、`choose`、循环与局部可变状态。

### 第 6 章：递归、尾调用与折叠 {#chapter-06}

[本章](../part-01/ch-06-recursion-folds#overview) · [答案页](../solutions/ch-06-recursion-folds#overview)

**各题答案:** [练习 1](../solutions/ch-06-recursion-folds#exercise-01) · [练习 2](../solutions/ch-06-recursion-folds#exercise-02) · [练习 3](../solutions/ch-06-recursion-folds#exercise-03)

**评审重点:** 结构递归、累加器保持的条件、尾调用与左右折叠。

## 第二部分 · 用类型建立模型 {#part-2}

### 第 7 章：记录、更新、相等与比较 {#chapter-07}

[本章](../part-02/ch-07-records-equality#overview) · [答案页](../solutions/ch-07-records-equality#overview)

**各题答案:** [练习 1](../solutions/ch-07-records-equality#exercise-01) · [练习 2](../solutions/ch-07-records-equality#exercise-02) · [练习 3](../solutions/ch-07-records-equality#exercise-03)

**评审重点:** 元组迁移、不可变更新、结构相等、引用身份、哈希规则与业务排序。

### 第 8 章：可辨识联合与状态建模 {#chapter-08}

[本章](../part-02/ch-08-discriminated-unions#overview) · [答案页](../solutions/ch-08-discriminated-unions#overview)

**各题答案:** [练习 1](../solutions/ch-08-discriminated-unions#exercise-01) · [练习 2](../solutions/ch-08-discriminated-unions#exercise-02) · [练习 3](../solutions/ch-08-discriminated-unions#exercise-03)

**评审重点:** 布尔标志组合、联合案例、穷尽性与状态转换规则。

### 第 9 章：缺失与预期失败 {#chapter-09}

[本章](../part-02/ch-09-option-result#overview) · [答案页](../solutions/ch-09-option-result#overview)

**各题答案:** [练习 1](../solutions/ch-09-option-result#exercise-01) · [练习 2](../solutions/ch-09-option-result#exercise-02) · [练习 3](../solutions/ch-09-option-result#exercise-03)

**评审重点:** `option`、`Result`、组合、短路和带上下文的错误信息。

### 第 10 章：递归类型与结构递归 {#chapter-10}

[本章](../part-02/ch-10-recursive-types#overview) · [答案页](../solutions/ch-10-recursive-types#overview)

**各题答案:** [练习 1](../solutions/ch-10-recursive-types#exercise-01) · [练习 2](../solutions/ch-10-recursive-types#exercise-02) · [练习 3](../solutions/ch-10-recursive-types#exercise-03)

**评审重点:** 从递归类型的案例推导短路查询、`map` 定律和只遍历一次的树摘要。

### 第 11 章：泛型、约束与度量单位 {#chapter-11}

[本章](../part-02/ch-11-generics-constraints#overview) · [答案页](../solutions/ch-11-generics-constraints#overview)

**各题答案:** [练习 1](../solutions/ch-11-generics-constraints#exercise-01) · [练习 2](../solutions/ch-11-generics-constraints#exercise-02) · [练习 3](../solutions/ch-11-generics-constraints#exercise-03)

**评审重点:** 推断泛型签名、按实际用途修复值限制，并在 API 两侧保留度量单位。

### 第 12 章：让非法状态无法表示 {#chapter-12}

[本章](../part-02/ch-12-making-illegal-states-unrepresentable#overview) · [答案页](../solutions/ch-12-making-illegal-states-unrepresentable#overview)

**各题答案:** [练习 1](../solutions/ch-12-making-illegal-states-unrepresentable#exercise-01) · [练习 2](../solutions/ch-12-making-illegal-states-unrepresentable#exercise-02) · [练习 3](../solutions/ch-12-making-illegal-states-unrepresentable#exercise-03)

**评审重点:** 保护有范围限制的值，判断外层记录能否保持公开，并修正把总容量与剩余容量混为一谈的跨文件 API。

## 第三部分 · 组合与程序结构 {#part-3}

### 第 13 章：组合、参数顺序与管道 API {#chapter-13}

[本章](../part-03/ch-13-composition-pipeline-api#overview) · [答案页](../solutions/ch-13-composition-pipeline-api#overview)

**各题答案:** [练习 1](../solutions/ch-13-composition-pipeline-api#exercise-01) · [练习 2](../solutions/ch-13-composition-pipeline-api#exercise-02) · [练习 3](../solutions/ch-13-composition-pipeline-api#exercise-03)

**评审重点:** 用管道或函数组合改写调用，选择便于使用的参数顺序，并删除没有增加清晰度的管道。

### 第 14 章：集合选择与求值模型 {#chapter-14}

[本章](../part-03/ch-14-collections-evaluation#overview) · [答案页](../solutions/ch-14-collections-evaluation#overview)

**各题答案:** [练习 1](../solutions/ch-14-collections-evaluation#exercise-01) · [练习 2](../solutions/ch-14-collections-evaluation#exercise-02) · [练习 3](../solutions/ch-14-collections-evaluation#exercise-03)

**评审重点:** 根据实际操作选择集合，准确计算延迟序列会请求多少元素，并区分有序键与哈希键。

### 第 15 章：活动模式与领域匹配边界 {#chapter-15}

[本章](../part-03/ch-15-active-patterns#overview) · [答案页](../solutions/ch-15-active-patterns#overview)

**各题答案:** [练习 1](../solutions/ch-15-active-patterns#exercise-01) · [练习 2](../solutions/ch-15-active-patterns#exercise-02) · [练习 3](../solutions/ch-15-active-patterns#exercise-03)

**评审重点:** 用活动模式提供覆盖全部输入的领域视图，保留解析错误，并把数据库访问移出模式匹配。

### 第 16 章：模块、命名空间、项目与编译设置 {#chapter-16}

[本章](../part-03/ch-16-modules-namespaces-projects#overview) · [答案页](../solutions/ch-16-modules-namespaces-projects#overview)

**各题答案:** [练习 1](../solutions/ch-16-modules-namespaces-projects#exercise-01) · [练习 2](../solutions/ch-16-modules-namespaces-projects#exercise-02) · [练习 3](../solutions/ch-16-modules-namespaces-projects#exercise-03)

**评审重点:** 安排多文件项目的编译顺序，修复命名空间中的非法值绑定，并让包装函数继续保留可空引用信息。

### 第 17 章：签名、访问控制与面向 F# 的 API {#chapter-17}

[本章](../part-03/ch-17-signatures-encapsulation#overview) · [答案页](../solutions/ch-17-signatures-encapsulation#overview)

**各题答案:** [练习 1](../solutions/ch-17-signatures-encapsulation#exercise-01) · [练习 2](../solutions/ch-17-signatures-encapsulation#exercise-02) · [练习 3](../solutions/ch-17-signatures-encapsulation#exercise-03)

**评审重点:** 定义抽象电子邮件类型，缩小前后不一致的分配 API，并让 `.fsi` 与 `.fs` 中的函数参数个数和辅助函数访问级别保持一致。

### 第 18 章：显式工作流组合与验证累积 {#chapter-18}

[本章](../part-03/ch-18-workflow-validation#overview) · [答案页](../solutions/ch-18-workflow-validation#overview)

**各题答案:** [练习 1](../solutions/ch-18-workflow-validation#exercise-01) · [练习 2](../solutions/ch-18-workflow-validation#exercise-02) · [练习 3](../solutions/ch-18-workflow-validation#exercise-03)

**评审重点:** 分开纯检查、相互依赖的检查和带副作用的检查，按固定顺序累积错误，并把含义不清的计算表达式改成直接代码。

## 第四部分 · 副作用、异步与并发 {#part-4}

### 第 19 章：.NET API 与空值边界 {#chapter-19}

[本章](../part-04/ch-19-dotnet-null-boundaries#overview) · [答案页](../solutions/ch-19-dotnet-null-boundaries#overview)

**各题答案:** [练习 1](../solutions/ch-19-dotnet-null-boundaries#exercise-01) · [练习 2](../solutions/ch-19-dotnet-null-boundaries#exercise-02) · [练习 3](../solutions/ch-19-dotnet-null-boundaries#exercise-03)

**评审重点:** 区分三类可空表示，包装真实的 .NET 可空返回且不丢失错误，并说明 `option` 内部为什么仍可能装着 `null`。

### 第 20 章：函数式核心与副作用边界 {#chapter-20}

[本章](../part-04/ch-20-functional-core-effects#overview) · [答案页](../solutions/ch-20-functional-core-effects#overview)

**各题答案:** [练习 1](../solutions/ch-20-functional-core-effects#exercise-01) · [练习 2](../solutions/ch-20-functional-core-effects#exercise-02) · [练习 3](../solutions/ch-20-functional-core-effects#exercise-03)

**评审重点:** 把隐藏的运行时输入改成参数，选择最小且准确的依赖 API，并把预期的外部失败与程序调用错误分开。

### 第 21 章：异常、资源与 I/O {#chapter-21}

[本章](../part-04/ch-21-exceptions-resources-io#overview) · [答案页](../solutions/ch-21-exceptions-resources-io#overview)

**各题答案:** [练习 1](../solutions/ch-21-exceptions-resources-io#exercise-01) · [练习 2](../solutions/ch-21-exceptions-resources-io#exercise-02) · [练习 3](../solutions/ch-21-exceptions-resources-io#exercise-03)

**评审重点:** 把安全释放资源的读取过程与纯解析组合，用类型化规则替代捕获全部异常后返回字符串，并验证两个 reader 在成功和失败时都会释放。

### 第 22 章：Async<'T> 与 Task<'T> {#chapter-22}

[本章](../part-04/ch-22-async-task#overview) · [答案页](../solutions/ch-22-async-task#overview)

**各题答案:** [练习 1](../solutions/ch-22-async-task#exercise-01) · [练习 2](../solutions/ch-22-async-task#exercise-02) · [练习 3](../solutions/ch-22-async-task#exercise-03)

**评审重点:** 用确定性闸门测试 async 工作流和 task 何时启动，组合 Task API 与 Async 验证器，并确保只有一个组件启动工作一次。

### 第 23 章：取消、超时、故障与释放 {#chapter-23}

[本章](../part-04/ch-23-cancellation-timeouts#overview) · [答案页](../solutions/ch-23-cancellation-timeouts#overview)

**各题答案:** [练习 1](../solutions/ch-23-cancellation-timeouts#exercise-01) · [练习 2](../solutions/ch-23-cancellation-timeouts#exercise-02) · [练习 3](../solutions/ch-23-cancellation-timeouts#exercise-03)

**评审重点:** 验证取消令牌是否传到底层，用信号分别实现“停止等待”和“取消工作”两种超时策略，并测试编译后的异步释放。

### 第 24 章：并行、并发、代理与受控可变性 {#chapter-24}

[本章](../part-04/ch-24-concurrency-agents-state#overview) · [答案页](../solutions/ch-24-concurrency-agents-state#overview)

**各题答案:** [练习 1](../solutions/ch-24-concurrency-agents-state#exercise-01) · [练习 2](../solutions/ch-24-concurrency-agents-state#exercise-02) · [练习 3](../solutions/ch-24-concurrency-agents-state#exercise-03)

**评审重点:** 根据必须始终成立的规则选择协调方式，在不假设消息顺序的情况下扩展预约代理，并用测试固定缓存失效和重复工作行为。

## 第五部分 · .NET 互操作与工程质量 {#part-5}

### 第 25 章：在 F# 中定义对象 {#chapter-25}

[本章](../part-05/ch-25-objects-interfaces#overview) · [答案页](../solutions/ch-25-objects-interfaces#overview)

**各题答案:** [练习 1](../solutions/ch-25-objects-interfaces#exercise-01) · [练习 2](../solutions/ch-25-objects-interfaces#exercise-02) · [练习 3](../solutions/ch-25-objects-interfaces#exercise-03)

**评审重点:** 删除没有实际作用的类，比较函数和接口哪种更适合表达策略，并重新设计结构体，使其默认值也合法。

### 第 26 章：深入 .NET 互操作 {#chapter-26}

[本章](../part-05/ch-26-dotnet-runtime-boundaries#overview) · [答案页](../solutions/ch-26-dotnet-runtime-boundaries#overview)

**各题答案:** [练习 1](../solutions/ch-26-dotnet-runtime-boundaries#exercise-01) · [练习 2](../solutions/ch-26-dotnet-runtime-boundaries#exercise-02) · [练习 3](../solutions/ch-26-dotnet-runtime-boundaries#exercise-03)

**评审重点:** 只转换一次 `obj` 输入，控制事件订阅的生命周期，并证明自定义字典比较器的相等与哈希规则一致。

### 第 27 章：为 C# 设计 F# API {#chapter-27}

[本章](../part-05/ch-27-fsharp-api-for-csharp#overview) · [答案页](../solutions/ch-27-fsharp-api-for-csharp#overview)

**各题答案:** [练习 1](../solutions/ch-27-fsharp-api-for-csharp#exercise-01) · [练习 2](../solutions/ch-27-fsharp-api-for-csharp#exercise-02) · [练习 3](../solutions/ch-27-fsharp-api-for-csharp#exercise-03)

**评审重点:** 把 F# 专用结果转换成稳定的 .NET 响应，通过重载扩展查询，并把序列化要求限制在专用 DTO 中。

### 第 28 章：示例测试、测试替身与契约测试 {#chapter-28}

[本章](../part-05/ch-28-testing-boundaries#overview) · [答案页](../solutions/ch-28-testing-boundaries#overview)

**各题答案:** [练习 1](../solutions/ch-28-testing-boundaries#exercise-01) · [练习 2](../solutions/ch-28-testing-boundaries#exercise-02) · [练习 3](../solutions/ch-28-testing-boundaries#exercise-03)

**评审重点:** 根据风险选择最小测试层，为“找不到产品”路径手写测试替身，并让新增可选 JSON 字段保持兼容。

### 第 29 章：使用 FsCheck 做基于属性的测试 {#chapter-29}

[本章](../part-05/ch-29-property-testing#overview) · [答案页](../solutions/ch-29-property-testing#overview)

**各题答案:** [练习 1](../solutions/ch-29-property-testing#exercise-01) · [练习 2](../solutions/ch-29-property-testing#exercise-02) · [练习 3](../solutions/ch-29-property-testing#exercise-03)

**评审重点:** 为流式代码推导独立性质，设计合法标识符的生成器与缩减器，并把依赖顺序的反例保留为回归测试。

### 第 30 章：诊断、调试、格式化与构建 {#chapter-30}

[本章](../part-05/ch-30-diagnostics-tooling-builds#overview) · [答案页](../solutions/ch-30-diagnostics-tooling-builds#overview)

**各题答案:** [练习 1](../solutions/ch-30-diagnostics-tooling-builds#exercise-01) · [练习 2](../solutions/ch-30-diagnostics-tooling-builds#exercise-02) · [练习 3](../solutions/ch-30-diagnostics-tooling-builds#exercise-03)

**评审重点:** 修复 F# 文件顺序导致的连锁错误，让 FSI、测试和调试器分别回答不同问题，并审查一次有意修改锁定依赖图的变更。

### 第 31 章：先测量再优化 {#chapter-31}

[本章](../part-05/ch-31-measure-before-optimizing#overview) · [答案页](../solutions/ch-31-measure-before-optimizing#overview)

**各题答案:** [练习 1](../solutions/ch-31-measure-before-optimizing#exercise-01) · [练习 2](../solutions/ch-31-measure-before-optimizing#exercise-02) · [练习 3](../solutions/ch-31-measure-before-optimizing#exercise-03)

**评审重点:** 只对实际运行过的基准下结论，在不改变行为的前提下比较 `option` 与 `voption` 分配，并为三种系统症状选择合适的测量方法。

### 第 32 章：从函数到应用 {#chapter-32}

[本章](../part-05/ch-32-functions-to-applications#overview) · [答案页](../solutions/ch-32-functions-to-applications#overview)

**各题答案:** [练习 1](../solutions/ch-32-functions-to-applications#exercise-01) · [练习 2](../solutions/ch-32-functions-to-applications#exercise-02) · [练习 3](../solutions/ch-32-functions-to-applications#exercise-03)

**评审重点:** 定义小型发货接口及其负责人，限制可观察信号的数量和大小，并根据具体生命周期选择应用宿主。

## 第六部分 · 活动预约系统 {#part-6}

### 第 33 章：业务语言、命令、事件与模型 {#chapter-33}

[本章](../part-06/ch-33-domain-language-model#overview) · [答案页](../solutions/ch-33-domain-language-model#overview)

**各题答案:** [练习 1](../solutions/ch-33-domain-language-model#exercise-01) · [练习 2](../solutions/ch-33-domain-language-model#exercise-02) · [练习 3](../solutions/ch-33-domain-language-model#exercise-03)

**评审重点:** 按角色分类预约值，在不混淆职责的情况下设计座位变更命令与事件，并根据需要保证的行为选择存储方式。

### 第 34 章：纯预约工作流与验证 {#chapter-34}

[本章](../part-06/ch-34-pure-booking-workflow#overview) · [答案页](../solutions/ch-34-pure-booking-workflow#overview)

**各题答案:** [练习 1](../solutions/ch-34-pure-booking-workflow#exercise-01) · [练习 2](../solutions/ch-34-pure-booking-workflow#exercise-02) · [练习 3](../solutions/ch-34-pure-booking-workflow#exercise-03)

**评审重点:** 追踪预订错误优先级，将独立验证扩展到三个字段，并比较取消优先级策略。

### 第 35 章：端口、持久化、配置与替身 {#chapter-35}

[本章](../part-06/ch-35-ports-persistence-config#overview) · [答案页](../solutions/ch-35-ports-persistence-config#overview)

**各题答案:** [练习 1](../solutions/ch-35-ports-persistence-config#exercise-01) · [练习 2](../solutions/ch-35-ports-persistence-config#exercise-02) · [练习 3](../solutions/ch-35-ports-persistence-config#exercise-03)

**评审重点:** 升级带版本的快照，检查文件替换过程中的每个失败点，并围绕由外部管理生命周期的生产客户端构造应用。

### 第 36 章：Web API、JSON 与输入边界 {#chapter-36}

[本章](../part-06/ch-36-web-api-boundaries#overview) · [答案页](../solutions/ch-36-web-api-boundaries#overview)

**各题答案:** [练习 1](../solutions/ch-36-web-api-boundaries#exercise-01) · [练习 2](../solutions/ch-36-web-api-boundaries#exercise-02) · [练习 3](../solutions/ch-36-web-api-boundaries#exercise-03)

**评审重点:** 使用自动绑定时保持 HTTP 行为不变，处理结果未知的副作用，并为不同部署方式安排安全控制。

### 第 37 章：一致性、幂等、重试与部分失败 {#chapter-37}

[本章](../part-06/ch-37-consistency-idempotency#overview) · [答案页](../solutions/ch-37-consistency-idempotency#overview)

**各题答案:** [练习 1](../solutions/ch-37-consistency-idempotency#exercise-01) · [练习 2](../solutions/ch-37-consistency-idempotency#exercise-02) · [练习 3](../solutions/ch-37-consistency-idempotency#exercise-03)

**评审重点:** 让多个进程共同遵守容量限制，处理结果未知的支付，并设计发件箱而不声称消息一定只投递一次。

### 第 38 章：集成、诊断、C# 客户端与发布验证 {#chapter-38}

[本章](../part-06/ch-38-integration-diagnostics-release#overview) · [答案页](../solutions/ch-38-integration-diagnostics-release#overview)

**各题答案:** [练习 1](../solutions/ch-38-integration-diagnostics-release#exercise-01) · [练习 2](../solutions/ch-38-integration-diagnostics-release#exercise-02) · [练习 3](../solutions/ch-38-integration-diagnostics-release#exercise-03)

**评审重点:** 找出代码无法兑现的保证，限制遥测收集的数量和大小，并把本地预约检查扩展成具体发布计划。

## 第七部分 · 生态地图 {#part-7}

### 第 39 章：ASP.NET Core 与 F# Web 生态 {#chapter-39}

[本章](../part-07/ch-39-web-ecosystem#overview) · [答案页](../solutions/ch-39-web-ecosystem#overview)

**各题答案:** [练习 1](../solutions/ch-39-web-ecosystem#exercise-01) · [练习 2](../solutions/ch-39-web-ecosystem#exercise-02) · [练习 3](../solutions/ch-39-web-ecosystem#exercise-03)

**评审重点:** 为具体团队选择 Web API 风格，在不改变行为的前提下试用 Falco，并为依赖框架的端点设计可回滚迁移。

### 第 40 章：数据、类型提供程序、分析与机器学习 {#chapter-40}

[本章](../part-07/ch-40-data-analytics#overview) · [答案页](../solutions/ch-40-data-analytics#overview)

**各题答案:** [练习 1](../solutions/ch-40-data-analytics#exercise-01) · [练习 2](../solutions/ch-40-data-analytics#exercise-02) · [练习 3](../solutions/ch-40-data-analytics#exercise-03)

**评审重点:** 根据已知数据规模选择工具，明确处理 CSV 模式变化，并把探索性分类器改造成可复现的训练和推理系统。

### 第 41 章：Fable、Elmish 与浏览器应用 {#chapter-41}

[本章](../part-07/ch-41-fable-elmish#overview) · [答案页](../solutions/ch-41-fable-elmish#overview)

**各题答案:** [练习 1](../solutions/ch-41-fable-elmish#exercise-01) · [练习 2](../solutions/ch-41-fable-elmish#exercise-02) · [练习 3](../solutions/ch-41-fable-elmish#exercise-03)

**评审重点:** 选择不超过问题所需规模的浏览器架构，拒绝过期的异步结果，并按各运行时真正支持的能力拆分共享定价库。

### 第 42 章：云、容器、Serverless 与 .NET Aspire {#chapter-42}

[本章](../part-07/ch-42-cloud-containers-aspire#overview) · [答案页](../solutions/ch-42-cloud-containers-aspire#overview)

**各题答案:** [练习 1](../solutions/ch-42-cloud-containers-aspire#exercise-01) · [练习 2](../solutions/ch-42-cloud-containers-aspire#exercise-02) · [练习 3](../solutions/ch-42-cloud-containers-aspire#exercise-03)

**评审重点:** 选择符合工作负载的计算方式，把本地云示例扩展成发布方案，并让幂等事件消费者明确表示结果未知的情况。

### 第 43 章：Avalonia、桌面端与移动端 {#chapter-43}

[本章](../part-07/ch-43-avalonia-desktop-mobile#overview) · [答案页](../solutions/ch-43-avalonia-desktop-mobile#overview)

**各题答案:** [练习 1](../solutions/ch-43-avalonia-desktop-mobile#exercise-01) · [练习 2](../solutions/ch-43-avalonia-desktop-mobile#exercise-02) · [练习 3](../solutions/ch-43-avalonia-desktop-mobile#exercise-03)

**评审重点:** 决定 UI 应共享到哪一层，把已验证的 Avalonia 小型实现扩展成桌面发布计划，并为移动项目列出清楚的验证矩阵。

### 第 44 章：Unity 6.3 LTS 与 F# {#chapter-44}

[本章](../part-07/ch-44-unity#overview) · [答案页](../solutions/ch-44-unity#overview)

**各题答案:** [练习 1](../solutions/ch-44-unity#exercise-01) · [练习 2](../solutions/ch-44-unity#exercise-02) · [练习 3](../solutions/ch-44-unity#exercise-03)

**评审重点:** 合理划分 Unity 中 F# 与 C# 的职责，列出托管插件发布前必须通过的 IL2CPP 检查，并在不隐藏 AOT 风险的前提下为任务数据增加版本。

### 第 45 章：脚本、自动化、包生态与继续学习 {#chapter-45}

[本章](../part-07/ch-45-scripting-packages-next#overview) · [答案页](../solutions/ch-45-scripting-packages-next#overview)

**各题答案:** [练习 1](../solutions/ch-45-scripting-packages-next#exercise-01) · [练习 2](../solutions/ch-45-scripting-packages-next#exercise-02) · [练习 3](../solutions/ch-45-scripting-packages-next#exercise-03)

**评审重点:** 扩展可复现的产物自动化，只根据实际验证范围评价当前命令行包，并把本书安排成十二周的构建与复盘计划。

## 最终自我评审 {#final-review}

- 你能否不依赖答案文字，解释推断类型或公共类型？
- 是否保留了顺序、求值、资源和状态管理、失败、取消与兼容性要求？
- 哪些验证实际执行过，哪些结论只来自资料审阅或尚未实施的方案？
- 哪个反例能区分你的设计与表面相似但错误的设计？
- 若答案不同，审阅者能否看到取舍，以及你会改用书中版本的条件？
