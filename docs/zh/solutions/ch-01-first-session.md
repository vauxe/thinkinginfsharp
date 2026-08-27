---
title: "第 1 章练习答案"
description: "第一次 F# 会话的推理过程、迁移示例与运行入口选择。"
translationKey: solutions/ch-01-first-session
---

# 第 1 章练习答案 {#overview}

先完成自己的推理，再比较下面的过程。只得到相同输出还不够；你还应能追溯每个值，并说明每一行何时打印。

[返回第 1 章](../part-01/ch-01-first-session)。

## 练习 1：解释运行结果 {#exercise-01}

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

## 练习 2：迁移一个小程序 {#exercise-02}

独立答案脚本中的一种直接写法如下：

```fsharp:line-numbers
let guest = "Lin"
let requestedSeats = 3
let confirmation = $"{guest} booked {requestedSeats} seats."

printfn "%s" confirmation
```
三个 `let` 依次描述数据依赖，而不是声明三个以后必须改写的存储槽。`confirmation` 只依赖前两个已命名的值。最后的 `printfn` 把文本写到标准输出，并返回 `()`。

这里没有必要添加类型标注：字符串字面量、整数 `3`、字符串插值和 `printfn` 已经给编译器足够约束。也没有必要为了“更函数式”而创建自定义运算符或抽象；清楚的中间值正是本题的目标。

## 练习 3：选择入口 {#exercise-03}

| 工作 | 合适入口 | 理由 |
| --- | --- | --- |
| 检查 `17 * 23` | FSI | 问题只有一个表达式；立即看到值与类型最有用 |
| 每周生成本地报告 | 脚本 | 代码需要保存、审阅和重复运行，但未必需要应用发布边界 |
| 构建并部署 HTTP 服务 | 项目 | 多模块、测试、依赖、配置和发布都需要明确的构建边界 |

这些不是不可违反的规则。脚本扩大后可以迁移为项目；项目中的一个小表达式仍可以拿到 FSI 中实验。选择依据是当前最短而可靠的反馈回路，不是文件扩展名的身份等级。

## 应该注意什么 {#what-to-notice}

- **值与副作用是两件事：** `printfn` 的副作用是输出，返回值是 `()`。
- **求值顺序可以从依赖读出：** 必须先完成右侧表达式，才能建立 `let` 绑定。
- **推断仍然是静态类型：** 少写类型标注不等于运行时随意改变类型。
- **工具应随问题规模升级：** FSI、脚本和项目各自保留有价值的工作流。

如果你的答案不同但仍能运行，比较差异是否来自题目允许的表达方式，而不是偶然输出相同。例如，把所有文本直接写进一次 `printfn` 会得到同一行输出，却没有完成“用绑定表达数据依赖”这一学习目标。
