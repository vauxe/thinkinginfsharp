---
layout: doc
title: F# 思维
description: 从 F# 语言本身出发，学习函数式建模与生产级 .NET 工程。
translationKey: index
kind: home
status: complete
verifiedWith:
  fsharp: "10"
  dotnetSdk: "10.0.301"
exampleIds:
  - capstone-part-01-booking-basics
exerciseIds: []
termIds: []
sources:
  - id: dotnet-10-download
    url: https://dotnet.microsoft.com/en-us/download/dotnet/10.0
    checked: "2026-08-25"
---

# F# 思维 {#overview}

本书面向已有编程经验、但尚未系统学习函数式编程的开发者。它从表达式、值、类型和函数出发，逐步抵达可测试的工作流、异步与并发、.NET 互操作，以及一个完整的活动预约系统。

[阅读前言](./preface/)可在六章快速入门、系统学习和 C#/.NET 转向 F# 三条路线中选择。每条路线都使用完整的中文版，不要求读者懂英文。

## 你将形成的能力 {#capabilities}

- 用类型表达业务规则，并让非法状态难以出现；
- 把纯逻辑与副作用分开，同时诚实处理现实世界的 I/O；
- 编写可测试、可诊断、能与 C# 协作的 F# 程序；
- 判断 F# 在 Web、数据、云、桌面、自动化和 Unity 中的适用边界。

每段有效代码都来自两种语言共享的可执行源文件。

## 最短起步路线 {#quick-start}

本书假设你已有基本编程经验，但不假设你会函数式编程或英文。准备好仓库与其 [.NET SDK 10.0.301 复现基线](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)后，从仓库根目录确认工具链：

```console
dotnet --version
```

仓库中的 `global.json` 会选择本书验证过的 SDK 特征带。然后按顺序阅读[第 1 章](./part-01/ch-01-first-session)到[第 6 章](./part-01/ch-06-recursion-folds)；每章先预测共享脚本的行为，再运行脚本，最后独立完成练习后查看答案。

完成六章后，运行第一部分的预约切片：

```console
dotnet fsi --exec examples/capstone/part-01/BookingBasics.fsx
```

应得到：

```text
Rows: valid=4 invalid=2
Labels: ["B-101:Lin:3"; "B-102:Ada:2"; "B-103:Sam:4"; "B-104:Mira:2"]
Accepted IDs: ["B-101"; "B-102"; "B-104"]
Rejected IDs: ["B-103"]
Capacity: booked=7 remaining=1
```

这个脚本故意只使用第一部分已经出现的值、函数、元组、列表模式、`option`、管道和折叠。六行固定输入中，一行形状错误、一行座位文本无效；剩余四行先转换为标签，再按容量 `8` 顺序折叠。`B-103` 请求 `4` 个座位时只剩 `3` 个，因此被拒绝；最后已预约 `7`、剩余 `1`。这里的有限字面量匹配不是通用整数解析器，记录、联合类型和真实边界会在后续部分逐步替换这些临时表示。
