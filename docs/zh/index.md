---
title: F# 思维
description: 从 F# 语言本身出发，学习函数式建模与生产级 .NET 工程。
translationKey: index
---

# F# 思维 {#overview}

这个站点从零开始讲解 F#。内容从表达式、值、类型和函数起步，再逐渐进入副作用、测试、.NET 互操作和完整应用。

你可以先打开[完整目录](./contents)，也可以直接从[第 1 章](./part-01/ch-01-first-session)开始。前三部分请按顺序阅读；后续部分可在需要相应主题时继续学习。

## 你将形成的能力 {#capabilities}

- 用类型表达业务规则，并让非法状态难以出现；
- 把纯逻辑与副作用分开，同时诚实处理现实世界的 I/O；
- 编写可测试、可诊断、能与 C# 协作的 F# 程序；
- 判断 F# 在 Web、数据、云、桌面、自动化和 Unity 中的适用边界。

## 最短起步路线 {#quick-start}

安装仍受支持的 [.NET SDK](https://dotnet.microsoft.com/zh-cn/download)，然后在终端确认：

```console
dotnet --version
```

启动 F# Interactive：

```console
dotnet fsi
```

在提示符中输入：

```fsharp
20 + 22;;
```

FSI 应报告值 `42` 和类型 `int`。第 1 章会解释这段输出的含义，以及如何把更长的示例保存为 `.fsx` 脚本。
