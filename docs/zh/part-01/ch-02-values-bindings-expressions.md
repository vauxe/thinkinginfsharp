---
title: "第 2 章：值、绑定与表达式"
description: "准确区分值、let 绑定、局部遮蔽与表达式，并学会阅读 F# 的基本类型和推断结果。"
translationKey: part-01/ch-02-values-bindings-expressions
---

# 第 2 章：值、绑定与表达式 {#overview}

第 1 章暂时把 `let eventName = ...` 读成“给值起名”。现在把这句话说准确：F# 先求右侧表达式的值，再用左侧模式建立**绑定**。绑定把名称与值关联起来；反复赋值需要明确声明可变存储位置。

这项术语区别会改变你阅读程序的方式。普通 `let` 绑定不会被重新赋值，因此更容易按源码顺序追踪值之间的依赖。类型推断会综合多处用法，在编译期确定类型。

函数会在下一章成为值的一种重要形式。本章只把函数调用当作已经可用的操作，例如 `decimal requestedSeats`；第 3 章再解释应用语法和函数类型。

## 从值到绑定 {#from-value-to-binding}

**值**是表达式正常完成后的结果。整数 `40` 是值，字符串拼接的结果是值，稍后函数本身也会是值。值有确定的静态类型。

**表达式**是被求值以产生结果的代码，例如 `20 + 4`。求值也可能产生可观察的副作用；`printfn` 会输出文本，但它仍返回 `unit` 值 `()`。

**绑定**是名称与值之间的关联。先看共享脚本中的一组绑定：

```fsharp:line-numbers [ch02-values-bindings-expressions.fsx]
let eventName = "Functional Foundations"
let capacity = 40
let fillRatio = 0.45
let ticketPrice = 19.50m
let eventCode = 'F'
let registrationOpen = true
let noFurtherResult = ()

printfn "%s (%c): capacity=%d, fill=%.2f, open=%b" eventName eventCode capacity fillRatio registrationOpen
```
### 怎样读 `let` {#read-let}

把 `let capacity = 40` 分三步读：

1. `40` 是一个整数字面量表达式；
2. 编译器确定这个表达式的类型，运行时求得值；
3. 模式 `capacity` 为该值建立名称。

这里的 `=` 分隔绑定左侧与右侧。在普通表达式中，`=` 用于结构相等比较；显式可变位置的更新使用 `<-`。按各自角色阅读这些符号，可以清楚区分绑定、比较与修改。

在模块或脚本顶层，`let` 引入声明；在局部作用域中，一串 `let` 绑定及其后续主体共同形成表达式。两种位置都遵守相同的核心顺序：先求右侧，再让新名称在后续范围内可见。普通非递归名称不能在定义之前使用；递归绑定留到第 6 章。

### 默认不可变到底意味着什么 {#immutability}

普通 `let` 绑定默认不可变：名称与值建立关联后，不能再改为表示另一个值。阅读代码时不必追踪名称后来的赋值，并发代码中也少了一种共享状态变化。

要区分两件事：**绑定能否重新赋值是一回事，对象内部能否修改是另一回事**。名称可以始终绑定到同一个 .NET 对象，而对象内部的字段仍然变化。集合与受控可变状态章节会分别讨论这两类变化。

F# 也支持 `let mutable`，因为局部计数、数组更新和某些互操作确实需要改变存储。应明确选择这种写法，并让可变范围尽量小。现在只需认出语法；第 5 章会在同一问题上比较变换与循环。

## 作用域与遮蔽 {#scope-shadowing}

作用域决定名称在哪里可见。F# 用缩进表达很多局部结构；下面 `normalizedCapacity` 的右侧包含两个局部绑定：

```fsharp:line-numbers [ch02-values-bindings-expressions.fsx]
let normalizedCapacity =
    let capacity = 20
    let capacity = capacity + 4
    capacity

printfn "Normalized capacity: %d; outer capacity: %d" normalizedCapacity capacity
```
第二个局部 `capacity` **遮蔽**第一个局部同名绑定：它先使用早先的值计算 `24`，再建立一个新绑定。在后续局部范围里，名称 `capacity` 会解析到新值。

局部表达式结束后，两个局部绑定都离开作用域，脚本顶层的 `capacity` 仍是 `40`。因此输出会同时显示 `normalizedCapacity` 是 `24`、外层 `capacity` 是 `40`。

遮蔽适合在很短的代码中逐步整理同一个值，也常见于 FSI 的连续实验。如果多个同名阶段相隔很远，换用能说明阶段含义的新名称更容易阅读。是否使用遮蔽，应以局部代码是否清楚为准。

## 类型在编译期确定 {#types-are-static}

每个值和表达式都有编译期类型，包括源码省略标注的情况。源码通过类型检查后才会运行；推断负责补上上下文已经能够可靠确定的信息。

### 常用基本类型 {#basic-types}

先掌握高频类型，而不是背完所有数值宽度：

| F# 类型 | 代表性字面量 | 说明 |
| --- | --- | --- |
| `int` | `40` | 32 位有符号整数；无其他上下文时的常用整数默认类型 |
| `int64` | `40L` | 64 位有符号整数；后缀 `L` 区分类型 |
| `float` | `0.45` | 64 位二进制浮点数，即 .NET `System.Double` |
| `decimal` | `19.50m` | 十进制数，精度和可表示范围有限；常用于货币等十进制业务量，后缀为 `m` 或 `M` |
| `bool` | `true` | 真假值只有 `true` 与 `false` |
| `char` | `'F'` | 单个 UTF-16 代码单元，使用单引号 |
| `string` | `"F#"` | .NET 字符串，使用双引号 |
| `unit` | `()` | 只有一个值 `()` 的类型 |

F# 还提供其他宽度的有符号与无符号整数、`float32` 和 `bigint` 等。只有外部协议、数值范围或实测性能要求时，才需要选择这些类型。

`float` 能表示很大范围，但多数十进制小数只能近似存储。`decimal` 可以精确表示许多日常使用的十进制小数，例如 `19.50`，因此通常更适合货币计算；它仍有有限的精度和范围。两种类型服务于不同的数值需求。

### 推断来自约束 {#inference-constraints}

编译器综合多种约束：

- 字面量及其后缀提供候选类型；
- 运算符要求参与值具有兼容类型；
- 已知操作的参数和结果限制周围表达式；
- 类型标注增加显式约束；
- 后续用法也可能让先前未知的类型变得确定。

例如，无后缀的 `40` 在当前上下文中默认推断为 `int`；`0.45` 推断为 `float`；`19.50m` 的后缀使 `ticketPrice` 成为 `decimal`。两种用法要求不兼容类型时，编译器会报告约束冲突，并把转换策略交给程序员决定。

### 标注与转换解决不同问题 {#annotations-conversions}

下面一段同时展示两者：

```fsharp:line-numbers [ch02-values-bindings-expressions.fsx]
let requestedSeats: int = 3
let pricePerSeat: decimal = 19.50m
let totalPrice = decimal requestedSeats * pricePerSeat

printfn "Ticket total: %M" totalPrice
```
`requestedSeats: int` 和 `pricePerSeat: decimal` 是**类型标注**。它们约束现有表达式必须具有所写类型；标注本身不会在运行时改变值。

`decimal requestedSeats` 是**显式转换**：它从 `int` 值产生新的 `decimal` 值。乘法两侧因此都是 `decimal`。F# 不会在普通数值算术中自动把已有值转换成更宽的类型。把转换写出来，也会让符号、范围、精度和舍入方面的选择留在源码中。

只在能传达意图、固定公开 API 或帮助编译器的位置写标注。局部值的类型已经显而易见时，重复标注会增加噪声；转换则应出现在真正改变表示的地方。

### 阅读类型签名 {#read-signatures}

在 FSI 中逐项提交前面的绑定，会看到类似：

```text
val capacity: int = 40
val fillRatio: float = 0.45
val ticketPrice: decimal = 19.50M
val eventCode: char = 'F'
val noFurtherResult: unit = ()
```

把冒号读作“具有类型”：`capacity` 具有类型 `int`。等号右侧是 FSI 为当前值选择的显示形式，不是类型的一部分。`decimal` 值显示时可能使用大写 `M`，与源码中的小写 `m` 表示同一后缀含义。

编译器错误也常用“期望类型 / 实际类型”描述无法满足的约束。先找冒号、类型名和发生冲突的表达式，再决定模型应改成什么；只在模型有意改变表示的边界添加转换。

## 表达式结果与 `unit` {#expressions-and-unit}

以表达式为中心的 F# 文件仍然包含声明。顶层 `let`、类型和模块都是声明；它们包含的右侧与主体由表达式构成。条件、匹配和局部绑定都会产生可供后续计算使用的结果。后续章节会逐一运用这一性质。

一串表达式按顺序执行时，非最后表达式通常应返回 `unit`；否则忽略一个有意义的值往往是错误，编译器也可能给出警告。`printfn` 很适合出现在这种位置，因为它的有意义行为是输出，返回值就是 `()`。

这条规则也解释了为什么“表达式有值”与“程序有副作用”并不矛盾。类型说明表达式会把什么结果交给后续计算；输出、写文件或网络请求则是在求值期间发生的副作用。阅读代码时两者都要检查。

## 运行共享示例 {#run-example}

在仓库根目录执行：

```console
dotnet fsi --exec examples/scripts/ch02-values-bindings-expressions.fsx
```

应得到：

```text
Functional Foundations (F): capacity=40, fill=0.45, open=true
Ticket total: 58.50
Normalized capacity: 24; outer capacity: 40
```

请逐行核对上面的输出。脚本中的格式化只影响显示，不改变 `fillRatio` 或 `totalPrice` 的类型。

## 追踪第一个冲突约束 {#debugging}

遇到类型错误时，用下面的顺序缩小问题：

1. 从诊断指向的最小表达式开始；
2. 在 FSI 中分别检查输入值的类型；
3. 看字面量后缀、运算符或已知 API 给出了什么约束；
4. 判断数据模型是否真的需要同一类型；
5. 只在模型确实需要改变类型或表示的位置添加标注或显式转换。

常见误区是把错误都归因于“推断猜错了”。推断没有个人偏好；它只是在求解源码提供的约束。有时真正错误的是把人数读成字符串后直接参与算术，有时是货币选择了 `float`，也可能只是某个字面量漏写 `m`。

遮蔽造成困惑时，先标出每个名称的缩进范围。若你无法一句话说清新旧绑定各自代表什么，改用不同名称通常比继续添加注释有效。

## 练习 {#exercises}

先独立写出类型和求值过程，再运行临时副本。答案应解释编译器为什么接受或拒绝代码。

### 练习 1：读类型，不猜类型 {#exercise-01}

针对 `basic-values` 区域：

1. 写出七个绑定各自的类型；
2. 说明 `0.45` 与 `19.50m` 为什么不是同一类型；
3. 说明 `eventCode` 与 `eventName` 为什么不是同一类型；
4. 判断把所有标注都省略后，程序是否仍然是静态类型。

在 FSI 中验证，并比较 FSI 显示的值与源码字面量写法是否完全相同。

### 练习 2：转换外部表示 {#exercise-02}

假设外部输入把人数提供为字符串 `"24"`。解释为什么把它直接与整数 `1` 相加会失败，然后：

1. 在边界把文本显式转换成 `int`；
2. 计算下一个人数；
3. 写出三个名称的类型和最终输出；
4. 说明转换失败的风险暂时被放在哪里。

本章只处理有效输入；`option`、`Result` 与异常会在后续章节补全失败处理。

### 练习 3：追踪遮蔽 {#exercise-03}

逐行解释 `local-shadowing` 区域：

1. 每次右侧求值时，名称 `capacity` 指向哪个值？
2. `normalizedCapacity` 最终是多少？
3. 输出中的外层 `capacity` 为什么仍是 `40`？
4. 这段代码创建了几个绑定，又修改了几个既有值？

[查看本章练习答案](../solutions/ch-02-values-bindings-expressions)。

下一章会把函数纳入这幅图：函数也是值，应用也是表达式，而箭头类型会把数据依赖扩展为可组合的行为。

## 来源 {#sources}

- [Microsoft Learn：值](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/values/)
- [Microsoft Learn：let 绑定](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/functions/let-bindings)
- [Microsoft Learn：类型推断](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/type-inference)
- [Microsoft Learn：基本类型](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/basic-types)
- [Microsoft Learn：字面量](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/literals)
- [Microsoft Learn：F# 导览中的遮蔽示例](https://learn.microsoft.com/en-us/dotnet/fsharp/tour)
