---
title: "第 2 章练习答案"
description: "值、绑定、基本类型、显式转换与局部遮蔽的推理答案。"
translationKey: solutions/ch-02-values-bindings-expressions
---

# 第 2 章练习答案 {#overview}

先比较推理过程，再比较最终输出。若你依靠一次成功运行却无法说明类型约束来自哪里，这道题还没有完成。

[返回第 2 章](../part-01/ch-02-values-bindings-expressions)。

## 练习 1：读类型，不猜类型 {#exercise-01}

七个绑定的类型是：

| 名称 | 类型 | 主要约束 |
| --- | --- | --- |
| `eventName` | `string` | 双引号字符串字面量 |
| `capacity` | `int` | 无其他上下文的整数 `40` |
| `fillRatio` | `float` | 无后缀的小数字面量 `0.45` |
| `ticketPrice` | `decimal` | `m` 后缀 |
| `eventCode` | `char` | 单引号字符字面量 |
| `registrationOpen` | `bool` | `true` |
| `noFurtherResult` | `unit` | 唯一值 `()` |

`float` 使用二进制浮点表示，`decimal` 是独立的十进制数值类型；`m` 后缀明确选择后者。`char` 是单个 UTF-16 代码单元，`string` 是 UTF-16 代码单元序列，单引号与双引号也分别表达这两个类型。

没有显式标注时，编译器仍在编译期确定这些类型。FSI 可能把 `19.50m` 显示为 `19.50M`，所以值的显示形式不保证与源码字面量逐字符相同。

## 练习 2：修复表示边界 {#exercise-02}

一种直接答案放在独立答案脚本中：

```fsharp:line-numbers
let rawAttendeeCount = "24"
let attendeeCount = int rawAttendeeCount
let nextAttendeeCount = attendeeCount + 1

printfn "Next attendee count: %d" nextAttendeeCount
```
`rawAttendeeCount` 是 `string`，而整数加法的另一侧是 `int`；F# 不会自动把任意文本解释为整数。`int rawAttendeeCount` 先把文本转换为 `int`，因此 `attendeeCount` 与 `nextAttendeeCount` 都是 `int`。最终输出为 `Next attendee count: 25`。

这里仍有一个刻意保留的风险：文本不是有效整数时，`int` 转换会抛出异常。本题假设输入有效；在真实输入边界，后续章节会用失败类型或受控异常转换表达这个分支。不要把本例误读为“所有解析都应该直接调用 `int`”。

## 练习 3：追踪遮蔽 {#exercise-03}

重新看同一段代码：

```fsharp:line-numbers
let normalizedCapacity =
    let capacity = 20
    let capacity = capacity + 4
    capacity

printfn "Normalized capacity: %d; outer capacity: %d" normalizedCapacity capacity
```
求第一个局部右侧时，字面量直接产生 `20`。求第二个局部右侧 `capacity + 4` 时，`capacity` 仍指第一个局部绑定，因此得到 `24`；随后新绑定遮蔽它。最后的主体读取最新局部绑定，所以 `normalizedCapacity` 为 `24`。

离开右侧局部范围后，脚本顶层的 `capacity` 再次是可见绑定，值仍为 `40`。这段区域新建三个绑定：两个局部 `capacity` 和顶层 `normalizedCapacity`。它没有修改任何既有值。

## 应该注意什么 {#what-to-notice}

- **类型来自约束，不来自名称：** 把 `ticketPrice` 改名为 `x` 不会改变 `m` 后缀提供的类型信息。
- **标注不是转换：** 写 `: decimal` 只要求右侧已经满足该类型；`decimal value` 才产生新表示。
- **遮蔽不是赋值：** 两个同名绑定仍是两个绑定，外层值没有时间上的变化。
- **边界失败必须稍后补回模型：** 有效输入示例简化了本章，但答案明确记录了尚未处理的路径。

若你的实现使用不同名称但保持相同类型边界，依然可以正确。若只是把所有输入都留成字符串并拼接出 `25`，输出看似相近，却绕过了本题要建立的数值模型。
