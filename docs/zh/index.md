---
title: F# 思维
description: 从 F# 语言本身出发，学习函数式建模与生产级 .NET 工程。
translationKey: index
outline: false
aside: false
---

# F# 思维 {#overview}

本书先讲 F# 的表达式、值、类型和函数，再介绍副作用、测试、.NET 互操作和应用设计。

::: tip 从这里开始
如果你刚开始学习 F#，请先阅读[前言](./preface/)，再[进入第 1 章](./part-01/ch-01-first-session)。推荐路线如下：

- 第 1–18 章按顺序阅读，学习 F# 语言和程序结构；
- 第 19–32 章学习 I/O、异步、.NET 互操作、测试和应用组织；
- 第 33–38 章是预约系统的设计案例，只需理解各层如何配合，不必组装完整项目；
- 第 39–44 章是可选的应用领域介绍，第 45 章讲脚本和后续学习。
:::

## 第一部分 · 表达式与函数 {#part-1}

- [第 1 章：第一次 F# 会话](./part-01/ch-01-first-session)
- [第 2 章：值、绑定与表达式](./part-01/ch-02-values-bindings-expressions)
- [第 3 章：函数也是值](./part-01/ch-03-functions-as-values)
- [第 4 章：分支与基本模式](./part-01/ch-04-branching-patterns)
- [第 5 章：列表、管道与数据流](./part-01/ch-05-lists-pipelines)
- [第 6 章：递归、尾调用与折叠](./part-01/ch-06-recursion-folds)

## 第二部分 · 用类型建立模型 {#part-2}

- [第 7 章：记录、更新、相等与比较](./part-02/ch-07-records-equality)
- [第 8 章：可区分联合与状态建模](./part-02/ch-08-discriminated-unions)
- [第 9 章：缺失与预期失败](./part-02/ch-09-option-result)
- [第 10 章：递归类型与结构递归](./part-02/ch-10-recursive-types)
- [第 11 章：泛型、约束与度量单位](./part-02/ch-11-generics-constraints)
- [第 12 章：让非法状态无法表示](./part-02/ch-12-making-illegal-states-unrepresentable)

## 第三部分 · 组合与程序结构 {#part-3}

- [第 13 章：组合、参数顺序与管道 API](./part-03/ch-13-composition-pipeline-api)
- [第 14 章：集合选择与求值模型](./part-03/ch-14-collections-evaluation)
- [第 15 章：活动模式与领域匹配边界](./part-03/ch-15-active-patterns)
- [第 16 章：模块、命名空间、项目与编译设置](./part-03/ch-16-modules-namespaces-projects)
- [第 17 章：签名、访问控制与面向 F# 的 API](./part-03/ch-17-signatures-encapsulation)
- [第 18 章：显式工作流组合与验证累积](./part-03/ch-18-workflow-validation)

## 第四部分 · 副作用、异步与并发 {#part-4}

- [第 19 章：.NET API 与空值边界](./part-04/ch-19-dotnet-null-boundaries)
- [第 20 章：函数式核心与副作用边界](./part-04/ch-20-functional-core-effects)
- [第 21 章：异常、资源与 I/O](./part-04/ch-21-exceptions-resources-io)
- [第 22 章：Async<'T> 与 Task<'T>](./part-04/ch-22-async-task)
- [第 23 章：取消、超时、故障与释放](./part-04/ch-23-cancellation-timeouts)
- [第 24 章：并行、并发、代理与受控可变性](./part-04/ch-24-concurrency-agents-state)

## 第五部分 · .NET 互操作与工程质量 {#part-5}

- [第 25 章：在 F# 中定义对象](./part-05/ch-25-objects-interfaces)
- [第 26 章：深入 .NET 互操作](./part-05/ch-26-dotnet-runtime-boundaries)
- [第 27 章：为 C# 设计 F# API](./part-05/ch-27-fsharp-api-for-csharp)
- [第 28 章：示例测试、测试替身与契约测试](./part-05/ch-28-testing-boundaries)
- [第 29 章：使用 FsCheck 做基于属性的测试](./part-05/ch-29-property-testing)
- [第 30 章：诊断、调试、格式化与构建](./part-05/ch-30-diagnostics-tooling-builds)
- [第 31 章：先测量再优化](./part-05/ch-31-measure-before-optimizing)
- [第 32 章：从函数到应用](./part-05/ch-32-functions-to-applications)

## 第六部分 · 完整预约工作流 {#part-6}

- [第 33 章：业务语言、命令、事件与模型](./part-06/ch-33-domain-language-model)
- [第 34 章：纯预约工作流与验证](./part-06/ch-34-pure-booking-workflow)
- [第 35 章：端口、持久化、配置与替身](./part-06/ch-35-ports-persistence-config)
- [第 36 章：Web API、JSON 与输入边界](./part-06/ch-36-web-api-boundaries)
- [第 37 章：一致性、幂等、重试与部分失败](./part-06/ch-37-consistency-idempotency)
- [第 38 章：集成、诊断、C# 客户端与发布验证](./part-06/ch-38-integration-diagnostics-release)

## 第七部分 · F# 适合做什么 {#part-7}

- [第 39 章：ASP.NET Core 与 F# Web 生态](./part-07/ch-39-web-ecosystem)
- [第 40 章：数据、类型提供程序、分析与机器学习](./part-07/ch-40-data-analytics)
- [第 41 章：Fable、Elmish 与浏览器应用](./part-07/ch-41-fable-elmish)
- [第 42 章：云、容器、Serverless 与 .NET Aspire](./part-07/ch-42-cloud-containers-aspire)
- [第 43 章：Avalonia、桌面端与移动端](./part-07/ch-43-avalonia-desktop-mobile)
- [第 44 章：Unity 6.3 LTS 与 F#](./part-07/ch-44-unity)
- [第 45 章：脚本、自动化、包生态与继续学习](./part-07/ch-45-scripting-packages-next)

## 参考资料 {#reference}

- [附录 A：跨平台环境配置](./appendices/a-setup)
- [附录 B：语法与运算符速查](./appendices/b-syntax-reference)
- [附录 C：集合选择与复杂度](./appendices/c-collections)
- [附录 D：从 C# 迁移到 F# 与互操作](./appendices/d-csharp-migration)
- [附录 E：常见编译器诊断索引](./appendices/e-compiler-errors)
- [附录 F：F# 术语表](./glossary)
- [附录 G：练习与答案使用指南](./appendices/g-solutions-guide)
- [附录 H：高级特性识别索引](./appendices/h-advanced-index)
