---
title: "前言：如何使用本书"
description: "选择学习路线、阅读 F# 类型、运行证据，并理解本书的 F# 10 与 .NET 10 范围。"
translationKey: preface/index
kind: preface
status: complete
verifiedWith:
  fsharp: "10"
  dotnetSdk: "10.0.301"
exampleIds: []
exerciseIds: []
termIds: []
sources:
  - id: microsoft-fsharp-get-started
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/get-started/
    checked: "2026-08-25"
  - id: microsoft-fsharp-10
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/whats-new/fsharp-10
    checked: "2026-08-25"
  - id: microsoft-dotnet-10-download
    url: https://dotnet.microsoft.com/en-us/download/dotnet/10.0
    checked: "2026-08-25"
  - id: microsoft-global-json
    url: https://learn.microsoft.com/en-us/dotnet/core/tools/global-json
    checked: "2026-08-25"
---

# 前言：如何使用本书 {#overview}

这是一本帮助你用 F# 思考的书，而不是教你逐句翻译另一门语言。它从表达式、类型、函数与数据变换出发，再把这些思想带入副作用、.NET 边界、测试、架构和生态选择。

中英文版本具有相同的内容契约、示例、练习、证据与稳定锚点。中文版可以独立阅读；不会英文也不会缺少任何前置知识。

## 本书适合谁，又不适合谁 {#audience}

如果你已经能用任意语言编写小程序，并希望完成下列至少一项目标，本书就适合你：

- 学习函数式编程，同时不把 F# 当作数学装饰；
- 从 C# 或另一门 .NET 语言转向 F#，并保留已有的平台知识；
- 显式建模领域规则、失败、副作用与并发；
- 现实地判断 F# 在 Web、数据、云、桌面、自动化或 Unity 中的位置；
- 从脚本逐步走向经过测试且可诊断的应用。

本书没有按“第一次接触变量、循环、文件、HTTP 或测试”的节奏优化，也不是穷尽式语言规范、包目录、框架菜谱、编译器内部课程，或“一种工具适合所有产品”的承诺。当精确语法或易变平台契约很重要时，请进入所链接的官方参考。

## “用 F# 思考”是什么意思 {#fsharp-first}

F# 不是“少写花括号的 C#”。本书会反复追问：

1. 这个表达式产生什么值？
2. 推断类型或公开类型允许什么？
3. 数据模型显式列出了哪些情况？
4. 哪段变换是纯的，副作用由哪条边界拥有？
5. 哪项可观察证据能区分正确设计和看似可信的错误设计？

这种视角并不排斥对象、可变性、异常、任务或 .NET API，而是要求你在有意选择的边界使用它们。这里的“掌握”是能够解释边界与取舍，而不只是记住语法。

## 从三条阅读路线中选择 {#reading-routes}

| 路线 | 按此顺序阅读 | 可以继续前进的证据 |
|---|---|---|
| 快速入门 | [环境准备](../appendices/a-setup)、[第 1 章](../part-01/ch-01-first-session)到[第 6 章](../part-01/ch-06-recursion-folds)，再运行第一部分预约脚本 | 你能从类型预测小脚本，并解释它的管道或折叠 |
| 系统学习 | 按顺序读第 1–38 章；再按平台兴趣选择第 39–45 章；附录随查随用 | 你能建模并测试预约工作流，再为一条生态边界给出理由 |
| C#/.NET 转向 F# | 先浏览[迁移地图](../appendices/d-csharp-migration)，运行第 1 章，用练习自测第 2–18 章，重点学习第 19–32 章，再构建第 33–38 章 | 你不再逐句翻译语法，并能为 F# 与 C# 调用者暴露有意设计的 API |

“浏览”不等于假定自己已经会了。阅读类型签名、尝试练习；只要你的预测与编译器或测试不同，就停下来学习。即使语法看似熟悉，后续部分也依赖第 7–18 章的建模词汇。

## 把每章用作反馈循环 {#chapter-loop}

学习每章时：

1. 阅读本章成果，并检查重要类型签名；
2. 运行前先预测输出、失败、顺序或所有权；
3. 运行范围最小的引用示例或测试；
4. 不照抄正文，用自己的话解释结果；
5. 完成全部三道练习后再打开答案；
6. 比较契约与证据，然后修订自己的答案。

封闭题可能只有很窄的可观察结果，诊断题和设计题则可能有多个可靠答案。[答案与评审指南](../appendices/g-solutions-guide)解释评分维度并链接全部答案；书中解答是反馈，不是自动成立的证明，也不是唯一可接受设计。

## 只运行当前需要的范围 {#running-examples}

阅读静态书不需要安装工具链。若要执行核心 F# 示例，请安装[附录 A](../appendices/a-setup)所述 SDK、克隆仓库，并从仓库根目录运行命令。

检查仓库实际选择的 SDK：

```console
dotnet --version
```

不进入交互提示符，直接运行一个脚本：

```console
dotnet fsi --exec examples/scripts/ch01-first-session.fsx
```

运行第一份集成预约切片：

```console
dotnet fsi --exec examples/capstone/part-01/BookingBasics.fsx
```

维护者完成仓库的冻结 Node 安装后，可以运行完整示例门：

```console
pnpm check:examples
```

不要把 `examples/expected-errors/` 下的文件当成成功程序运行；检查器会编译它们，并期待特定诊断。部分生态叙述明确标为资料审阅、提案或人工平台检查；它们不会被悄悄升级成“已执行证据”。

## 先读类型，再读实现 {#reading-signatures}

先看最外层形状，从左向右阅读箭头，同时记住 `->` 向右结合。

| 签名 | 阅读方式 |
|---|---|
| `string -> int` | 输入一个字符串，输出一个整数 |
| `int -> int -> int` | 输入一个整数，再输入一个整数，最后输出整数；它可以被部分应用 |
| `(int * int) -> int` | 输入一个包含两个整数的元组，再输出一个整数 |
| `'T list -> 'T option` | 输入元素类型相同的列表；输出同类型的一个元素，或没有值 |
| `Request -> Result<Reservation, BookingError>` | 输入请求；输出预约成功值，或预期的预约错误 |
| `unit -> Task<'T>` | 输入显式启动信号；输出一个异步产生 `'T` 的 .NET 任务 |

例如，把 `values: 'T list -> 'T option` 读作：参数名为 `values`；列表中所有元素共享某个类型 `'T`；成功时返回同一类型；缺失被显式表示。名称有帮助，但类型才是编译器能检查的契约。

签名显得密集时，给每个输入和中间结果命名，不要猜标点。[附录 B](../appendices/b-syntax-reference)是紧凑速查；[术语表](../glossary)用中文完整定义词汇并显示英文对照，不要求你懂英文。

## 理解版本与证据边界 {#version-scope}

仓库设置 `<LangVersion>10.0</LangVersion>`，通常以 `net10.0` 为目标，并把 SDK `10.0.301` 记录为可复现基线。其 `global.json` 使用 `latestPatch`，因此 SDK 选择最多移动到同一 `10.0.3xx` 特征带中已安装的补丁。这个基线确定了书中报告的编译器行为，但不表示 `10.0.301` 永远是最新安全维护版本。

部署软件时应使用仍受支持且打过当前补丁的 SDK 与运行时，并在升级后重跑证据。大多数示例面向 .NET 10；Unity 库为适应宿主边界而有意面向 `netstandard2.1`，编辑器与 Player 结果仍作为独立的人工证据记录。

F# 10 是语言范围，不是展示每项新特性的理由。本书优先教授耐久基础，只在版本行为改变真实决策时引入它。包、浏览器、云、移动与 Unity 事实都带复核日期，因为它们的契约比语言核心变化更快。

## 遇到阻塞时 {#recovery}

- 工具缺失或 SDK 选择错误时，查[附录 A](../appendices/a-setup)。
- 标点或优先级阻碍阅读时，查[附录 B](../appendices/b-syntax-reference)。
- 编译器报告陌生 `FS` 编号时，查[附录 E](../appendices/e-compiler-errors)，先修第一条相关错误。
- 术语含义不清时，查[双语术语表](../glossary)。
- 在库代码中遇到高级特性时，用[附录 H](../appendices/h-advanced-index)决定深入学习、包装还是推迟。

然后从[第 1 章](../part-01/ch-01-first-session)开始。把预测保留下来：预测与编译器证据之间的落差，正是本书最能帮助你学习的地方。
