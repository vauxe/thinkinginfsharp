---
title: "第 2 章：值、绑定与表达式"
description: "准确区分值、let 绑定、局部遮蔽与表达式，并学会阅读 F# 的基本类型和推断结果。"
translationKey: part-01/ch-02-values-bindings-expressions
kind: chapter
part: 1
chapter: 2
status: complete
verifiedWith:
  fsharp: "10"
  dotnetSdk: "10.0.301"
exampleIds:
  - ch02-values-bindings-expressions
exerciseIds:
  - ch02-exercise-01
  - ch02-exercise-02
  - ch02-exercise-03
termIds:
  - binding
  - expression
  - immutability
  - literal
  - numeric-conversion
  - shadowing
  - type-annotation
  - type-inference
  - value
sources:
  - id: microsoft-values
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/values/
    checked: "2026-08-24"
  - id: microsoft-let-bindings
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/functions/let-bindings
    checked: "2026-08-24"
  - id: microsoft-type-inference
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/type-inference
    checked: "2026-08-24"
  - id: microsoft-basic-types
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/basic-types
    checked: "2026-08-24"
  - id: microsoft-literals
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/literals
    checked: "2026-08-24"
  - id: microsoft-tour
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/tour
    checked: "2026-08-24"
---

# 第 2 章：值、绑定与表达式 {#overview}

第 1 章暂时把 `let eventName = ...` 读成“给值起名”。现在把这句话说准确：F# 先求右侧表达式的值，再用左侧模式建立**绑定**。绑定把名称与值关联起来；它默认不是可以反复赋值的存储槽。

这一区别看似只是术语，实际上决定了你怎样阅读程序。若名称不会在任意位置悄悄改指别的值，数据依赖就更接近源码中看到的顺序；类型推断也能把多个用法组合成一组编译期约束。

## 本章完成后你能做什么 {#outcomes}

完成本章后，你应该能够：

- 区分值、表达式、绑定和可变存储；
- 读懂常用基本类型的字面量与 FSI 类型签名；
- 根据字面量、运算和显式标注解释简单类型推断；
- 在不同数值类型之间做有意的显式转换；
- 解释局部遮蔽为何建立新绑定，而不是修改旧值；
- 选择少量有信息价值的类型标注，而不是给每个名称重复写类型。

函数会在下一章成为值的一种重要形式。本章只把函数调用当作已经可用的操作，例如 `decimal requestedSeats`；第 3 章再解释应用语法和函数类型。

## 从值到绑定 {#from-value-to-binding}

**值**是表达式正常完成后的结果。整数 `40` 是值，字符串拼接的结果是值，稍后函数本身也会是值。值有确定的静态类型。

**表达式**是被求值以产生结果的代码，例如 `20 + 4`。表达式也可能在求值时产生可观察效果；`printfn` 会输出文本，但它仍返回 `unit` 值 `()`。

**绑定**则不是另一个值。它是名称与值之间的关联。先看共享脚本中的一组绑定：

<<< @/../examples/scripts/ch02-values-bindings-expressions.fsx#basic-values{fsharp:line-numbers} [ch02-values-bindings-expressions.fsx]

### 怎样读 `let` {#read-let}

把 `let capacity = 40` 分三步读：

1. `40` 是一个整数字面量表达式；
2. 编译器确定这个表达式的类型，运行时求得值；
3. 模式 `capacity` 为该值建立名称。

这里的 `=` 分隔绑定左侧与右侧，不表示“以后把 40 写入 capacity”。在普通表达式中，`=` 用于结构相等比较；可变位置的更新使用 `<-`。三种写法角色不同，不要沿用其他语言里“等号总是赋值”的阅读习惯。

在模块或脚本顶层，`let` 引入声明；在局部作用域中，一串 `let` 绑定及其后续主体共同形成表达式。两种位置都遵守相同的核心顺序：先求右侧，再让新名称在后续范围内可见。普通非递归名称不能在定义之前使用；递归绑定留到第 6 章。

### 默认不可变到底意味着什么 {#immutability}

普通 `let` 绑定默认不可变：建立后，不能用 `<-` 让同一绑定改指另一个值。这减少了读代码时必须追踪的时间维度，也让并发代码少一种共享变化来源。

但要精确区分两件事：**绑定不可变不等于对象深度不可变**。若名称以后引用一个内部可变的 .NET 对象，不能重新绑定该名称，并不会冻结那个对象。本书会在集合与受控可变状态章节分别处理这个边界。

F# 也支持 `let mutable`，因为局部计数、数组更新和某些互操作确实需要存储变化。选择它应该是显式的，并让可变范围尽量小。现在只需认出语法；第 5 章会在同一问题上比较变换与循环。

## 作用域与遮蔽 {#scope-shadowing}

作用域决定名称在哪里可见。F# 用缩进表达很多局部结构；下面 `normalizedCapacity` 的右侧包含两个局部绑定：

<<< @/../examples/scripts/ch02-values-bindings-expressions.fsx#local-shadowing{fsharp:line-numbers} [ch02-values-bindings-expressions.fsx]

第二个局部 `capacity` **遮蔽**第一个局部同名绑定：它先使用旧值计算 `24`，再建立一个新绑定。旧值没有被改写，只是在后续局部范围里无法再通过名称 `capacity` 访问。

局部表达式结束后，两个局部绑定都离开作用域，脚本顶层的 `capacity` 仍是 `40`。因此输出同时证明 `normalizedCapacity` 是 `24`、外层 `capacity` 是 `40`。

遮蔽适合表达短小、线性的精化过程，也常见于 FSI 的连续实验。若多个同名阶段相隔很远，读者会难以分辨当前名称代表哪一步；此时使用描述阶段的新名称通常更清楚。遮蔽是作用域规则，不是“函数式风格分数”。

## 类型一直是静态的 {#types-are-static}

省略标注不等于省略类型。F# 编译器在编译时为每个值和表达式确定类型；只有源码经类型检查后才会运行。推断的目标是去掉可由上下文可靠得出的重复信息，而不是在运行时猜测。

### 常用基本类型 {#basic-types}

先掌握高频类型，而不是背完所有数值宽度：

| F# 类型 | 代表性字面量 | 说明 |
| --- | --- | --- |
| `int` | `40` | 32 位有符号整数；无其他上下文时的常用整数默认类型 |
| `int64` | `40L` | 64 位有符号整数；后缀 `L` 区分类型 |
| `float` | `0.45` | 64 位二进制浮点数，即 .NET `System.Double` |
| `decimal` | `19.50m` | 有限精度与比例的十进制数，常用于十进制业务量；后缀为 `m` 或 `M` |
| `bool` | `true` | 真假值只有 `true` 与 `false` |
| `char` | `'F'` | 单个 UTF-16 代码单元，使用单引号 |
| `string` | `"F#"` | .NET 字符串，使用双引号 |
| `unit` | `()` | 只有一个值 `()` 的类型 |

F# 还提供有符号、无符号的其他整数宽度、`float32` 和 `bigint` 等。让外部协议、范围或性能证据决定何时使用它们；不要因为类型列表很长就给每个小数值选择最窄表示。

`float` 能表示很大范围，但多数十进制小数只能近似存储。`decimal` 能精确表示许多系数与比例落在其有限表示范围内的日常十进制小数，例如 `19.50`，因此常适合货币规则。两者不是可以无条件互换的“带小数数字”。

### 推断来自约束 {#inference-constraints}

编译器综合多种约束：

- 字面量及其后缀提供候选类型；
- 运算符要求参与值具有兼容类型；
- 已知操作的参数和结果限制周围表达式；
- 类型标注增加显式约束；
- 后续用法也可能让先前未知的类型变得确定。

例如，脚本中的 `40` 没有其他上下文选择数值类型，所以 `capacity` 推断为 `int`；`0.45` 推断为 `float`；`19.50m` 的后缀使 `ticketPrice` 成为 `decimal`。若两种用法无法由同一个类型同时满足，编译器报告冲突，而不是偷偷转换其中一个值。

### 标注与转换解决不同问题 {#annotations-conversions}

下面一段同时展示两者：

<<< @/../examples/scripts/ch02-values-bindings-expressions.fsx#annotations-and-conversion{fsharp:line-numbers} [ch02-values-bindings-expressions.fsx]

`requestedSeats: int` 和 `pricePerSeat: decimal` 是**类型标注**。它们约束现有表达式必须具有所写类型；标注本身不会在运行时改变值。

`decimal requestedSeats` 是**显式转换**：它从 `int` 值产生新的 `decimal` 值。乘法两侧因此都是 `decimal`。F# 不会在普通数值算术中自动扩大这些已有值；显式边界避免符号、范围、精度和舍入规则被藏起来。

只在能传达意图、稳定公共边界或帮助编译器的位置写标注。局部值的类型已经显而易见时，重复标注会增加噪声；转换则应出现在真正改变表示的地方。

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

编译器错误也常用“期望类型 / 实际类型”描述无法满足的约束。先找冒号、类型名和发生冲突的表达式，再决定模型应改成什么；不要只为消除红线而随机加转换。

## 表达式与 `unit` 的边界 {#expressions-and-unit}

“F# 以表达式为中心”不意味着文件中没有声明。顶层 `let`、类型和模块都是声明；它们包含的右侧与主体由表达式构成。关键是，条件、匹配和局部绑定等结构都会产生结果，而不是只控制下一条语句。后续章节会逐一使用这些结果。

一串表达式按顺序执行时，非最后表达式通常应返回 `unit`；否则忽略一个有意义的值往往是错误，编译器也可能给出警告。`printfn` 很适合出现在这种位置，因为它的有意义行为是输出，返回值就是 `()`。

这条规则也解释了为什么“表达式有值”与“程序有副作用”并不矛盾。类型记录表达式交给后续计算的结果；输出、写文件或网络请求属于求值期间发生的效果。两条信息都需要阅读。

## 运行共享示例 {#run-example}

从仓库根目录执行：

```console
dotnet fsi --exec examples/scripts/ch02-values-bindings-expressions.fsx
```

应得到：

```text
Functional Foundations (F): capacity=40, fill=0.45, open=true
Ticket total: 58.50
Normalized capacity: 24; outer capacity: 40
Next attendee count: 25
```

manifest 按这个顺序断言四个确定性输出。脚本中的格式化只影响显示，不改变 `fillRatio` 或 `totalPrice` 的类型。

## 调试：追踪第一个冲突约束 {#debugging}

遇到类型错误时，用下面的顺序缩小问题：

1. 找诊断指向的最小表达式，不要先重写整段代码；
2. 在 FSI 中分别检查输入值的类型；
3. 看字面量后缀、运算符或已知 API 给出了什么约束；
4. 判断数据模型是否真的需要同一类型；
5. 只在明确边界添加标注或显式转换。

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

在 FSI 中验证，并比较 FSI 的值显示与源码字面量是否完全同形。

### 练习 2：修复表示边界 {#exercise-02}

假设外部输入把人数提供为字符串 `"24"`。解释为什么把它直接与整数 `1` 相加会失败，然后：

1. 在边界把文本显式转换成 `int`；
2. 计算下一个人数；
3. 写出三个名称的类型和最终输出；
4. 说明转换失败的风险暂时被放在哪里。

本章只处理有效输入；`option`、`Result` 与异常会在各自章节建立完整失败模型。

### 练习 3：追踪遮蔽 {#exercise-03}

逐行解释 `local-shadowing` 区域：

1. 每次右侧求值时，名称 `capacity` 指向哪个值？
2. `normalizedCapacity` 最终是多少？
3. 输出中的外层 `capacity` 为什么仍是 `40`？
4. 这段代码创建了几个绑定，又修改了几个既有值？

[查看本章练习答案](../solutions/ch-02-values-bindings-expressions)。

## 小结 {#summary}

- 表达式产生值；`let` 用模式把名称绑定到右侧表达式的值。
- 普通绑定默认不可变，但这不自动保证所引用对象深度不可变。
- 遮蔽建立同名新绑定，不修改旧值；作用域结束后外层绑定仍在。
- F# 静态推断来自字面量、运算、已知用法和标注共同形成的约束。
- 类型标注约束表达式；显式转换产生另一种表示的新值。
- 读 FSI 签名时，把 `val name: type = value` 分成名称、类型和显示值。

下一章会把函数纳入这幅图：函数也是值，应用也是表达式，而箭头类型会把数据依赖扩展为可组合的行为。

## 词汇 {#vocabulary}

- **值（value）：** 表达式正常完成后产生、可供后续表达式使用的结果。
- **表达式（expression）：** 被求值以产生结果的代码，也可能在求值期间产生效果。
- **绑定（binding）：** 名称与值之间的关联，通常由 `let` 和模式建立。
- **不可变性（immutability）：** 不原地改写的性质；绑定不可变不等于对象深度不可变。
- **类型推断（type inference）：** 编译器从约束推导每个构造的静态类型。
- **类型标注（type annotation）：** 源码中显式写出的类型约束。
- **数值转换（numeric conversion）：** 显式从一种数值表示产生另一种表示。
- **遮蔽（shadowing）：** 新的同名绑定在其范围内隐藏旧绑定，而不修改旧值。

## 来源 {#sources}

- [Microsoft Learn：值](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/values/)
- [Microsoft Learn：let 绑定](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/functions/let-bindings)
- [Microsoft Learn：类型推断](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/type-inference)
- [Microsoft Learn：基本类型](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/basic-types)
- [Microsoft Learn：字面量](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/literals)
- [Microsoft Learn：F# 导览中的遮蔽示例](https://learn.microsoft.com/en-us/dotnet/fsharp/tour)
