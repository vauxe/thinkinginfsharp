---
title: "第 1 章：第一次 F# 会话"
description: "在 F# Interactive、脚本与项目之间选择，并从表达式、值和 unit 建立第一个准确的心智模型。"
translationKey: part-01/ch-01-first-session
---

# 第 1 章：第一次 F# 会话 {#overview}

学习一门语言的最快方式，是先建立一个足够准确、又能立刻验证的心智模型；语法随后便有了上下文和用途。本章只解决第一个问题：**一段 F# 代码怎样变成你能观察到的结果？**

我们会在三种运行方式之间切换：交互式会话适合追问一个小问题，脚本适合保存一次实验，项目则用来组织需要编译、测试和发布的程序。三种方式都遵循 F# 的基本规则：代码由表达式组成，表达式正常完成时产生值。

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

脚本保存了实验的顺序、名称和输出，因此可以进入版本控制，也可以由自动检查重复运行。脚本中通常不写 `;;`；文件和语法结构已经告诉编译器要处理什么。

脚本仍由 FSI 执行。项目进一步提供多文件编译顺序、测试入口、发布设置和可复用程序集。代码开始承担这些责任时，就应使用项目。

### F# 项目 {#project}

.NET SDK 可以创建最小 F# 控制台项目：

```console
dotnet new console -lang "F#" -o HelloFSharp
dotnet run --project HelloFSharp
```

项目文件记录目标框架、源文件顺序、包依赖和构建设置。它会增加少量结构，但能让 `dotnet build`、`dotnet test` 和发布工具按统一方式工作。本书会从脚本逐步过渡到项目：小型算术实验使用 FSI，需要测试或部署的应用使用项目。

## 把第一个程序理解为一组表达式 {#expressions}

先看每个表达式产生什么值，以及这些值如何进入后续计算。暂时不熟悉的符号会在后文逐一解释。

```fsharp:line-numbers
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

类型推断省掉重复标注，同时保留静态类型。字符串与整数相减会在编译期失败，因此问题会在程序运行前暴露。

### 打印也返回一个值 {#unit}

`printfn` 把文本写到标准输出，这是它可观察到的副作用。但它仍然是 F# 表达式，所以还要有结果。这里的结果类型是 `unit`，这个类型只有一个值：`()`。

示例先执行 `printfn "%s" summary`，因此屏幕出现摘要；随后名称 `printResult` 绑定到返回值 `()`。最后一行把这个值打印出来。C# 的 `void` 表示缺少可用返回值；F# 的 `unit` 则是具有唯一值的普通类型。

这个区别以后会很重要。类型签名以 `unit` 结尾，通常说明调用的目的在于产生副作用，例如写文件、发送响应或记录日志。返回类型本身不能说明副作用是否完成或失败；这些结果要由测试和明确的错误类型验证。

## 练习 {#exercises}

先独立作答，再运行或修改自己的脚本。使用答案同时比较推理过程与最终答案。

### 练习 1：解释运行结果 {#exercise-01}

根据刚才的运行结果，回答以下问题：

1. `remaining`、`hasSeats`、`summary` 和 `printResult` 的类型分别是什么？
2. 输出按什么顺序出现？为什么摘要会在打印 `printResult` 之前出现？
3. 在实际修改前预测：把 `booked` 改为 `40` 后，哪些值会变化，输出怎样变化？

然后修改自己的本地副本，验证第 3 问的预测。

::: details 参考答案

类型分别是：

| 名称 | 类型 | 求得的值 |
| --- | --- | --- |
| `remaining` | `int` | `22` |
| `hasSeats` | `bool` | `true` |
| `summary` | `string` | `"Functional Foundations: 22 seats remaining"` |
| `printResult` | `unit` | `()` |

求 `printResult` 的右侧时，`printfn "%s" summary` 必须先执行，所以摘要是第一行输出。完成这次打印后，调用返回 `()`，这个值才被绑定到 `printResult`。接下来两个 `printfn` 依次打印布尔值与 `()`。

若把 `booked` 改为 `40`，`remaining` 从 `22` 变为 `0`，`hasSeats` 从 `true` 变为 `false`。`summary` 也因依赖 `remaining` 而变成以 `0 seats remaining` 结尾。`printResult` 的类型和值不变：打印不同文本仍然返回 `()`。

这道题的关键不是心算减法，而是沿依赖方向推导：输入值变化，先影响算术表达式，再影响比较和字符串插值，最后影响输出。

:::

### 练习 2：迁移一个小程序 {#exercise-02}

设想一段命令式程序。它依次创建三个可改写变量：`guest`、`requestedSeats` 和 `confirmation`；最后打印“Lin booked 3 seats.”。只使用本章见过的写法，把它改写为 F#：

1. 用三个 `let` 绑定表达数据依赖；
2. 用字符串插值构造确认文本；
3. 用 `printfn` 输出文本；
4. 同时说明最后一次调用的返回值与屏幕内容。

::: details 参考答案

一种直接写法如下：

```fsharp:line-numbers
let guest = "Lin"
let requestedSeats = 3
let confirmation = $"{guest} booked {requestedSeats} seats."

printfn "%s" confirmation
```
三个 `let` 依次描述数据依赖，而不是声明三个以后必须改写的存储槽。`confirmation` 只依赖前两个已命名的值。最后的 `printfn` 把文本写到标准输出，并返回 `()`。

这里没有必要添加类型标注：字符串字面量、整数 `3`、字符串插值和 `printfn` 已经给编译器足够约束。也没有必要为了“更函数式”而创建自定义运算符或抽象；清楚的中间值正是本题的目标。

:::

### 练习 3：选择入口 {#exercise-03}

为下面三种工作各选 FSI、脚本或项目，并写一句理由：

1. 检查 `17 * 23` 的结果和类型；
2. 每周运行、受版本控制、输出一份本地报告的小工具；
3. 具有多个模块、自动测试并需要部署的 HTTP 服务。

::: details 参考答案

| 工作 | 合适入口 | 理由 |
| --- | --- | --- |
| 检查 `17 * 23` | FSI | 问题只有一个表达式；立即看到值与类型最有用 |
| 每周生成本地报告 | 脚本 | 代码需要保存、审阅和重复运行，但未必需要应用发布边界 |
| 构建并部署 HTTP 服务 | 项目 | 多模块、测试、依赖、配置和发布都需要明确的构建边界 |

这些不是不可违反的规则。脚本扩大后可以迁移为项目；项目中的一个小表达式仍可以拿到 FSI 中实验。选择依据是当前最短而可靠的反馈回路，不是文件扩展名的身份等级。

:::

下一章会把这里暂用的说法讲准确：`let` 到底绑定了什么、默认不可变名称如何工作，以及编译器如何从约束推断类型。

## 来源 {#sources}

- [Microsoft Learn：F# Interactive](https://learn.microsoft.com/en-us/dotnet/fsharp/tools/fsharp-interactive/)
- [Microsoft Learn：F# Interactive 选项](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/fsharp-interactive-options)
- [Microsoft Learn：使用 .NET CLI 开始学习 F#](https://learn.microsoft.com/en-us/dotnet/fsharp/get-started/get-started-command-line)
- [Microsoft Learn：unit 类型](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/unit-type)
- [Microsoft Learn：字面量](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/literals)
