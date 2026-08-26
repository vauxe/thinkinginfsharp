---
title: "第 1 章：第一次 F# 会话"
description: "在 F# Interactive、脚本与项目之间选择，并从表达式、值和 unit 建立第一个准确的心智模型。"
translationKey: part-01/ch-01-first-session
---

# 第 1 章：第一次 F# 会话 {#overview}

学习一门语言的最快方式，是先建立一个足够准确、又能立刻验证的心智模型；语法随后便有了上下文和用途。本章只解决第一个问题：**一段 F# 代码怎样变成你能观察到的结果？**

我们会在三种运行方式之间来回切换：交互式会话适合追问一个小问题，脚本适合保存一次实验，项目则为需要编译、测试和发布的程序提供边界。贯穿这三种方式的是 F# 的基本观点：代码由表达式组成；表达式正常完成时产生值。

## 本章完成后你能做什么 {#outcomes}

完成本章后，你应该能够：

- 启动 F# Interactive（FSI）并解释它打印的类型和值；
- 运行 `.fsx` 脚本，并区分脚本与交互输入；
- 判断一个实验何时应该升级为 `.fsproj` 项目；
- 读懂字符串、整数、布尔值、字符串插值和简单算术；
- 解释为什么 `printfn` 既产生输出，又返回 `unit` 值 `()`。

函数、模式匹配和集合会在后续章节出现。遇到 `let` 时，暂时把它读成“给这个值起一个名字”；第 2 章会准确讨论绑定与类型推断。

## 开始前 {#before-you-start}

示例以 F# 10 和 .NET 10 复核。先在终端确认已安装的 SDK：

```console
dotnet --version
```

第一章只使用 .NET SDK。编辑器或 IDE 可以按需选择，示例直接使用 SDK 自带的包。命令前的提示符用于标明终端环境，实际输入从提示符后开始。

## 选择最快的验证方式 {#feedback-loop}

三种入口适合不同规模的验证任务。

| 入口 | 最适合 | 需要保留什么 |
| --- | --- | --- |
| FSI 会话 | 验证一个表达式、查看推断类型 | 临时会话状态 |
| `.fsx` 脚本 | 可重复实验、自动化、小工具 | 一个或多个脚本文件 |
| `.fsproj` 项目 | 多文件代码、测试、包引用、应用发布 | 项目文件与有顺序的源文件 |

### F# Interactive {#fsi}

运行：

```console
dotnet fsi
```

FSI 是读取—求值—打印循环（REPL）。在交互提示符中输入 `20 + 22;;`，双分号结束这一次提交。FSI 不只打印 `42`，还会报告结果的静态类型是 `int`，并把未命名结果临时绑定为 `it`。

这里有两个早期线索。第一，`20 + 22` 是一个产生值 `42` 的表达式。第二，类型在运行前就由编译器检查。FSI 把交互反馈与静态类型结合在一起。

`;;` 用于结束一次交互式提交。日常 F# 源文件改由文件边界和语法结构划分内容。先把交互窗口用作实验台，再把需要长期保存的代码放入脚本或项目。

### F# 脚本 {#script}

F# 脚本使用 `.fsx` 扩展名。使用下面的命令执行脚本；完成后，FSI 会自动退出：

```console
dotnet fsi --exec ch01-first-session.fsx
```

脚本保存了实验的顺序、名称和输出，因此可以进入版本控制，也可以被质量门重复运行。脚本中通常不写 `;;`；文件边界和语法结构已经告诉编译器一次要处理什么。

脚本仍由 FSI 执行。项目进一步提供多文件编译顺序、测试入口、发布设置和可复用程序集。代码开始承担这些责任时，就应使用项目。

### F# 项目 {#project}

.NET SDK 可以创建最小 F# 控制台项目：

```console
dotnet new console -lang "F#" -o HelloFSharp
dotnet run --project HelloFSharp
```

项目文件记录目标框架、源文件顺序、包依赖和构建设置。它带来少量结构成本，却让 `dotnet build`、`dotnet test` 和发布工具拥有明确边界。本书会从脚本逐步过渡到项目：小型算术实验使用 FSI，需要测试或部署的应用使用项目。

## 把第一个程序理解为一组表达式 {#expressions}

先看每个表达式产生什么值，以及这些值如何进入后续计算。暂时不熟悉的符号会在后文逐一解释。

```fsharp:line-numbers [ch01-first-session.fsx]
let eventName = "Functional Foundations"
let capacity = 40
let booked = 18
let remaining = capacity - booked
let hasSeats = remaining > 0
let summary = $"{eventName}: {remaining} seats remaining"

let printResult = printfn "%s" summary
printfn "Seats available: %b" hasSeats
printfn "Printing returned: %A" printResult
```
### 字面量产生值 {#literals-and-values}

`"Functional Foundations"`、`40`、`18` 和 `0` 是**字面量**：它们直接在源码中表示值。`let eventName = ...` 为右侧计算出的值建立名称。显式可变存储使用另一种构造，后续章节会介绍。

后续表达式使用这些值：

- `capacity - booked` 计算出整数 `22`；
- `remaining > 0` 计算出布尔值 `true`；
- `$"{eventName}: {remaining} seats remaining"` 把已有值插入字符串。

编译器从用法推断出一组静态类型：

| 名称 | 推断类型 | 依据 |
| --- | --- | --- |
| `eventName` | `string` | 右侧是字符串字面量 |
| `capacity`、`booked`、`remaining` | `int` | 在当前上下文中，无后缀整数默认按 `int` 解释，减法也成为 `int` 运算 |
| `hasSeats` | `bool` | `>` 比较产生真假值 |
| `summary` | `string` | 字符串插值产生文本 |

类型推断省掉重复标注，同时保留静态类型。字符串与整数相减会在编译期失败，运行路径因此只会接收已经通过检查的表达式。

### 打印也返回一个值 {#unit}

`printfn` 把文本写到标准输出，这是它可观察到的效果。但它仍然是 F# 表达式，所以还要有结果。这里的结果类型是 `unit`，这个类型只有一个值：`()`。

示例先执行 `printfn "%s" summary`，因此屏幕出现摘要；随后名称 `printResult` 绑定到返回值 `()`。最后一行把这个值打印出来。C# 的 `void` 表示缺少可用返回值；F# 的 `unit` 则是具有唯一值的普通类型。

这个区别以后会很重要。类型签名以 `unit` 结尾，通常提醒你“有意义的结果在效果中”，例如写文件、发送响应或记录日志。签名只说明返回值类型；效果是否完成、如何失败，要由测试或显式结果类型说明。

## 运行示例 {#run-example}

把前面的代码块复制到 `ch01-first-session.fsx`，然后运行：

```console
dotnet fsi --exec ch01-first-session.fsx
```

应得到：

```text
Functional Foundations: 22 seats remaining
Seats available: true
Printing returned: ()
```

FSI 在交互模式下会主动显示提交的值和类型；以 `--exec` 运行脚本时，上面这些行都来自脚本显式调用 `printfn`，因此重复运行文件会得到相同的有序输出。

## 调试：先识别运行边界 {#debugging}

初次会话最常见的问题通常不在业务逻辑，而在运行边界。

- **FSI 一直等待输入：** 交互提交可能还没有以 `;;` 结束，或括号、引号尚未闭合。
- **脚本路径不存在：** 确认终端当前位于保存脚本的目录。
- **脚本看起来没有输出：** 脚本只显示显式输出。加入 `printfn`，或回到 FSI 查看小表达式的自动展示。
- **整数与字符串表达式编译失败：** 先看诊断中的期望类型与实际类型，再选择符合数据含义的显式转换。
- **输出正确而设计仍不清楚：** 输出描述这一次运行；后续章节会继续补充类型、失败路径和可测试边界。

一个有效节奏是：把最小表达式送入 FSI，理解类型后放回脚本，再从头运行整个脚本。这样可以同时获得快速反馈与整份脚本的验证。

## 练习 {#exercises}

先独立作答，再运行或修改自己的脚本。使用答案同时比较推理过程与最终文本。

### 练习 1：解释运行结果 {#exercise-01}

根据刚才的运行结果，回答以下问题：

1. `remaining`、`hasSeats`、`summary` 和 `printResult` 的类型分别是什么？
2. 输出按什么顺序出现？为什么摘要会在打印 `printResult` 之前出现？
3. 在实际修改前预测：把 `booked` 改为 `40` 后，哪些值会变化，输出怎样变化？

然后修改自己的本地副本，验证第 3 问的预测。

### 练习 2：迁移一个小程序 {#exercise-02}

设想一段命令式程序依次创建可改写变量 `guest`、`requestedSeats` 和 `confirmation`，最后打印“Lin booked 3 seats.”。只使用本章见过的构造，把它改写为 F#：

1. 用三个 `let` 绑定表达数据依赖；
2. 用字符串插值构造确认文本；
3. 用 `printfn` 输出文本；
4. 同时说明最后一次调用的返回值与屏幕内容。

### 练习 3：选择入口 {#exercise-03}

为下面三种工作各选 FSI、脚本或项目，并写一句理由：

1. 检查 `17 * 23` 的结果和类型；
2. 每周运行、受版本控制、输出一份本地报告的小工具；
3. 具有多个模块、自动测试并需要部署的 HTTP 服务。

[查看本章练习答案](../solutions/ch-01-first-session)。

## 小结 {#summary}

- FSI 能最快验证小段代码，并同时显示值与推断类型。
- `.fsx` 把实验保存为可重复执行的脚本；`--exec` 在执行后退出。
- 项目为多文件编译、依赖、测试和发布建立边界。
- F# 的基本阅读单位是表达式；表达式正常完成时产生值。
- 输出是效果，`printfn` 的返回值是 `unit` 的唯一值 `()`。

下一章会把这里暂用的说法讲准确：`let` 到底绑定了什么、默认不可变名称如何工作，以及编译器如何从约束推断类型。

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
