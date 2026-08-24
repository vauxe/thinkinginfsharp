---
title: "第 1 章：第一次 F# 会话"
description: "在 F# Interactive、脚本与项目之间选择，并从表达式、值和 unit 建立第一幅准确心智图。"
translationKey: part-01/ch-01-first-session
kind: chapter
part: 1
chapter: 1
status: complete
verifiedWith:
  fsharp: "10"
  dotnetSdk: "10.0.301"
exampleIds:
  - ch01-first-session
exerciseIds:
  - ch01-exercise-01
  - ch01-exercise-02
  - ch01-exercise-03
termIds:
  - expression
  - fsharp-interactive
  - fsharp-script
  - literal
  - unit
  - value
sources:
  - id: microsoft-fsi
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/tools/fsharp-interactive/
    checked: "2026-08-24"
  - id: microsoft-fsi-options
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/fsharp-interactive-options
    checked: "2026-08-24"
  - id: microsoft-fsharp-cli
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/get-started/get-started-command-line
    checked: "2026-08-24"
  - id: microsoft-fsharp-unit
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/unit-type
    checked: "2026-08-24"
  - id: microsoft-fsharp-literals
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/literals
    checked: "2026-08-24"
---

# 第 1 章：第一次 F# 会话 {#overview}

学习一门语言的最快方式，不是先背完语法，而是建立一个足够准确、又能立刻验证的心智模型。本章只解决第一个问题：**一段 F# 代码怎样变成你能观察到的结果？**

我们会在三种运行方式之间来回切换：交互式会话适合追问一个小问题，脚本适合保存一次实验，项目则为需要编译、测试和发布的程序提供边界。贯穿这三种方式的是 F# 的基本观点：代码由表达式组成；表达式正常完成时产生值。

## 本章完成后你能做什么 {#outcomes}

完成本章后，你应该能够：

- 启动 F# Interactive（FSI）并解释它打印的类型和值；
- 运行 `.fsx` 脚本，并知道脚本与交互输入为何不完全相同；
- 判断一个实验何时应该升级为 `.fsproj` 项目；
- 读懂字符串、整数、布尔值、字符串插值和简单算术；
- 解释为什么 `printfn` 既产生输出，又返回 `unit` 值 `()`。

本章不要求你掌握函数、模式匹配或集合。遇到 `let` 时，暂时把它读成“给这个值起一个名字”；第 2 章会准确讨论绑定与类型推断。

## 开始前 {#before-you-start}

本书的示例以 F# 10 和 .NET SDK `10.0.301` 验证。仓库根目录的 `global.json` 会选择这一 SDK 特征带。先在终端确认环境：

```console
dotnet --version
```

若你使用本书仓库，下面的命令都从仓库根目录执行。命令前的提示符不属于命令本身。第一章只需要 .NET SDK；编辑器、IDE 和额外 NuGet 包都不是前置条件。

## 选择最短反馈回路 {#feedback-loop}

三种入口不是互相替代，而是服务于不同长度的反馈回路。

| 入口 | 最适合 | 需要保留什么 |
| --- | --- | --- |
| FSI 会话 | 验证一个表达式、查看推断类型 | 可以什么都不保存 |
| `.fsx` 脚本 | 可重复实验、自动化、小工具 | 一个或多个脚本文件 |
| `.fsproj` 项目 | 多文件代码、测试、包引用、应用发布 | 项目文件与有顺序的源文件 |

### F# Interactive {#fsi}

运行：

```console
dotnet fsi
```

FSI 是读取—求值—打印循环（REPL）。在交互提示符中输入 `20 + 22;;`，双分号结束这一次提交。FSI 不只打印 `42`，还会报告结果的静态类型是 `int`，并把未命名结果临时绑定为 `it`。

这里有两个早期线索。第一，`20 + 22` 不是一条只会“做事”的命令，而是一个产生值 `42` 的表达式。第二，类型在运行前就由编译器检查；交互并不意味着动态类型。

`;;` 是交互式提交的终止符，不是日常 F# 源文件每行都要写的分号。把交互窗口当实验台，而不是最终程序的存储位置。

### F# 脚本 {#script}

F# 脚本使用 `.fsx` 扩展名。下面的稳定命令执行脚本并在完成后退出 FSI：

```console
dotnet fsi --exec examples/scripts/ch01-first-session.fsx
```

脚本保存了实验的顺序、名称和输出，因此可以进入版本控制，也可以被质量门重复运行。脚本中通常不写 `;;`；文件边界和语法结构已经告诉编译器一次要处理什么。

脚本仍由 FSI 执行。它不会自然地提供应用项目的多文件编译顺序、测试入口、发布设置或可复用程序集。代码开始承担这些责任时，就应使用项目。

### F# 项目 {#project}

.NET SDK 可以创建最小 F# 控制台项目：

```console
dotnet new console -lang "F#" -o HelloFSharp
dotnet run --project HelloFSharp
```

项目文件记录目标框架、源文件顺序、包依赖和构建设置。它带来少量结构成本，却让 `dotnet build`、`dotnet test` 和发布工具拥有明确边界。本书会从脚本逐步过渡到项目；不要为了一个算术实验先建项目，也不要把一个需要测试和部署的应用永远留在单个脚本里。

## 把第一个程序读成表达式 {#expressions}

先读共享示例的主体，不必急着记住每个符号。

<<< @/../examples/scripts/ch01-first-session.fsx#first-session{fsharp:line-numbers} [ch01-first-session.fsx]

### 字面量产生值 {#literals-and-values}

`"Functional Foundations"`、`40`、`18` 和 `0` 是**字面量**：它们直接在源码中表示值。`let eventName = ...` 为右侧计算出的值建立名称；它不是先创建一个空盒子再往里赋值。

后续表达式使用这些值：

- `capacity - booked` 计算出整数 `22`；
- `remaining > 0` 计算出布尔值 `true`；
- `$"{eventName}: {remaining} seats remaining"` 把已有值插入字符串。

编译器从用法推断出一组静态类型：

| 名称 | 推断类型 | 依据 |
| --- | --- | --- |
| `eventName` | `string` | 右侧是字符串字面量 |
| `capacity`、`booked`、`remaining` | `int` | 没有其他上下文指定数值类型，因此无后缀整数按 `int` 解释，减法也成为 `int` 运算 |
| `hasSeats` | `bool` | `>` 比较产生真假值 |
| `summary` | `string` | 字符串插值产生文本 |

类型推断省掉重复标注，却没有取消类型。若把字符串当整数相减，编译会失败，而不是等到某条罕见运行路径才猜测转换规则。

### 打印也返回一个值 {#unit}

`printfn` 把文本写到标准输出，这是它可观察到的效果。但它仍然是 F# 表达式，所以还要有结果。这里的结果类型是 `unit`，这个类型只有一个值：`()`。

示例先执行 `printfn "%s" summary`，因此屏幕出现摘要；随后名称 `printResult` 绑定到返回值 `()`。最后一行把这个值打印出来。可以把 `unit` 与 C# 的 `void` 类比来获得直觉，但两者不完全相同：`void` 表示没有可用返回值，`unit` 则是具有唯一值的普通 F# 类型。

这个区别以后会很重要。类型签名以 `unit` 结尾，通常提醒你“有意义的结果在效果中”，例如写文件、发送响应或记录日志。它并不证明效果一定发生，也不等于错误处理成功。

## 运行共享示例 {#run-example}

从仓库根目录执行：

```console
dotnet fsi --exec examples/scripts/ch01-first-session.fsx
```

应得到：

```text
Functional Foundations: 22 seats remaining
Seats available: true
Printing returned: ()
Lin booked 3 seats.
```

FSI 在交互模式下会主动显示提交的值和类型；以 `--exec` 运行脚本时，上面这些行都来自脚本显式调用 `printfn`。仓库的示例 manifest 还断言其中的关键输出，因此正文与可运行行为共用同一份证据。

## 调试：先识别运行边界 {#debugging}

初次会话最常见的问题通常不在业务逻辑，而在运行边界。

- **FSI 一直等待输入：** 交互提交可能还没有以 `;;` 结束，或括号、引号尚未闭合。
- **脚本路径不存在：** 先确认当前目录。书中的相对路径都以仓库根目录为起点。
- **脚本没有显示某个值：** 脚本不会像交互提示符那样逐项展示绑定；需要显式 `printfn`，或回到 FSI 检查小表达式。
- **整数与字符串不能直接混算：** F# 不会为方便而随意猜测转换。先看诊断中期望类型与实际类型，再决定数据本来应该是什么。
- **输出正确但设计仍不清楚：** 输出只证明这一次运行；类型、失败路径和可测试边界要在后续章节逐步建立。

一个有效节奏是：把最小表达式送入 FSI，理解类型后放回脚本，再从头运行整个脚本。这样既保留快速反馈，也不会只验证被你手工挑中的一行。

## 练习 {#exercises}

先独立作答，再运行或修改自己的脚本。答案的价值在于比较推理过程，而不只是核对最终文本。

### 练习 1：解释运行结果 {#exercise-01}

根据刚才的运行结果，回答以下问题：

1. `remaining`、`hasSeats`、`summary` 和 `printResult` 的类型分别是什么？
2. 四行输出按什么顺序出现？为什么摘要会在打印 `printResult` 之前出现？
3. 在实际修改前预测：把 `booked` 改为 `40` 后，哪些值会变化，输出怎样变化？

然后复制脚本到临时位置验证第 3 问的预测，不要修改仓库中的共享答案。

### 练习 2：迁移一个小程序 {#exercise-02}

设想一段命令式程序依次创建可改写变量 `guest`、`requestedSeats` 和 `confirmation`，最后打印“Lin booked 3 seats.”。只使用本章见过的构造，把它改写为 F#：

1. 用三个 `let` 绑定表达数据依赖；
2. 用字符串插值构造确认文本；
3. 用 `printfn` 输出文本；
4. 说明最后一次调用的返回值，而不只说明屏幕内容。

### 练习 3：选择入口 {#exercise-03}

为下面三种工作各选 FSI、脚本或项目，并写一句理由：

1. 检查 `17 * 23` 的结果和类型；
2. 每周运行、受版本控制、输出一份本地报告的小工具；
3. 具有多个模块、自动测试并需要部署的 HTTP 服务。

[查看本章练习答案](../solutions/ch-01-first-session)。

## 小结 {#summary}

- FSI 提供最短反馈回路，并同时显示值与推断类型。
- `.fsx` 把实验保存为可重复执行的脚本；`--exec` 在执行后退出。
- 项目为多文件编译、依赖、测试和发布建立边界。
- F# 的基本阅读单位是表达式；表达式正常完成时产生值。
- 输出是效果，`printfn` 的返回值是 `unit` 的唯一值 `()`。

下一章会收紧这里暂用的说法：`let` 到底绑定了什么、名称为什么默认不可改写，以及编译器如何从约束推断类型。

## 词汇 {#vocabulary}

- **表达式（expression）：** 被求值并在正常完成时产生值的代码。
- **值（value）：** 求值结果，可供其他表达式继续使用；函数以后也会被视为值。
- **字面量（literal）：** 在源码中直接写出的值表示，例如 `40` 或 `"hello"`。
- **F# Interactive：** .NET SDK 中的 F# REPL，也能执行 `.fsx` 脚本。
- **F# 脚本：** 通常由 FSI 直接执行的 `.fsx` 源文件。
- **unit：** 只有一个值 `()` 的类型，常用作只有效果需要关注的表达式结果。

## 来源 {#sources}

- [Microsoft Learn：F# Interactive](https://learn.microsoft.com/en-us/dotnet/fsharp/tools/fsharp-interactive/)
- [Microsoft Learn：F# Interactive 选项](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/fsharp-interactive-options)
- [Microsoft Learn：使用 .NET CLI 开始学习 F#](https://learn.microsoft.com/en-us/dotnet/fsharp/get-started/get-started-command-line)
- [Microsoft Learn：unit 类型](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/unit-type)
- [Microsoft Learn：字面量](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/literals)
