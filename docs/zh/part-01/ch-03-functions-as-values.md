---
title: "第 3 章：函数也是值"
description: "从函数值出发掌握应用、箭头类型、匿名函数、高阶函数、柯里化、元组参数、部分应用与自动泛化。"
translationKey: part-01/ch-03-functions-as-values
---

# 第 3 章：函数也是值 {#overview}

函数是普通的 F# 值：可以用 `let` 绑定名称，可以作为实参传入另一个函数，也可以作为结果返回。**函数应用**本身又是表达式，会产生一个值。

这个观点把前两章连成一条线。字面量产生数据值，函数值描述从输入到结果的计算，而高阶函数可以接收或返回函数，从而组合不同的行为。先学会准确阅读箭头类型，再学习管道，才能理解 `|>` 实际传递了什么值。

本章只使用简单算术、字符串和二元组。集合上的 `map`、`filter` 与管道在第 5 章出现；自动泛化的值限制和明确写出的约束留到第 11 章。

## 函数绑定仍然是绑定 {#function-binding}

先看一个计算预约行金额的函数：

```fsharp:line-numbers [ch03-functions-as-values.fsx]
let lineTotal unitPrice seats = unitPrice * decimal seats
let standardLineTotal = lineTotal 19.50m
let totalForThree = standardLineTotal 3

printfn "Curried total: %M" totalForThree
```
`let lineTotal unitPrice seats = ...` 建立名称 `lineTotal` 与一个函数值之间的绑定。`unitPrice` 和 `seats` 是**形参**：它们代表函数以后接收的输入。调用 `lineTotal 19.50m 3` 时，`19.50m` 与 `3` 是**实参**：调用方实际提供的值。

定义函数会创建函数值。函数收到足够实参后，主体 `unitPrice * decimal seats` 开始求值。最后一个表达式直接提供结果，因此普通 F# 函数用该值取代 `return` 语句。

### 用空格应用函数 {#application}

F# 主要用空格表达函数应用：函数值在前，实参随后。`standardLineTotal 3` 把整数 `3` 交给函数值 `standardLineTotal`。

应用的结合方向是从左到右，优先级高于多数中缀运算。括号用于控制分组，而不是每次调用都必须出现。因此 `transform (transform value)` 应读为：

1. 先计算内层 `transform value`；
2. 再把该结果交给外层 `transform`。

把用逗号分隔的值包在括号里会创建元组，并改变传参形式。柯里化的 `lineTotal` 期望连续两个实参，而 `lineTotal (19.50m, 3)` 提供一个元组实参，因此会产生类型错误。

### 函数主体产生结果 {#body-result}

函数主体可以包含局部 `let` 和副作用，但整个主体仍由最后一个表达式决定结果类型。若最后一个表达式是 `printfn`，函数结果就是 `unit`；若最后一个表达式是金额计算，结果就是 `decimal`。

函数值既可以是纯函数，也可以包含副作用。主体可以读取时钟、写文件或修改受控状态。纯函数只依赖输入，并通过返回值给出结果；`let` 本身只负责建立绑定。

## 箭头是函数类型 {#function-types}

FSI 为 `lineTotal` 推断出：

```text
val lineTotal: decimal -> int -> decimal
```

箭头 `->` 分隔输入与结果。它向右结合，所以该签名等价于：

```text
decimal -> (int -> decimal)
```

先提供 `decimal` 后，函数返回一个 `int -> decimal` 函数；再提供 `int`，便得到最终的 `decimal`。这就是为什么 `standardLineTotal` 的签名是：

```text
val standardLineTotal: int -> decimal
```

类型签名不记录形参名称时，只表达输入与输出类型的结构。源码中的好名称仍然重要：`decimal -> int -> decimal` 无法单独告诉读者第一个数是单价还是折扣率。

### 柯里化与连续应用 {#currying}

F# 中 `let` 绑定函数通常使用**柯里化**形式：代码中写多个形参，实际含义是连续的单参数函数。`lineTotal 19.50m 3` 按左结合解释为：

```text
(lineTotal 19.50m) 3
```

这种连续应用是理解函数调用的方式，不代表运行时一定会创建中间函数；实际分配情况需要测量。

### 元组参数是另一种输入方式 {#tupled-parameters}

同一计算也能写成接收一个二元组的函数：

```fsharp:line-numbers [ch03-functions-as-values.fsx]
let lineTotalTupled (unitPrice, seats) = unitPrice * decimal seats
let tupledTotal = lineTotalTupled (19.50m, 3)

printfn "Tupled total: %M" tupledTotal
```
这里 `(unitPrice, seats)` 是一个元组模式，拆开单个实参中的两个位置。签名是：

```text
decimal * int -> decimal
```

类型中的 `*` 连接元组各项的类型。柯里化版本连续接收两个实参；元组版本接收一个包含两项的实参。它们通过不同的函数类型算出相同结果。

惯用的 `let` 绑定函数通常优先柯里化，便于部分应用和高阶组合。如果一个输入在领域上天然就是成组数据，元组也很清楚。.NET 方法的调用写法经常包含括号和逗号，但其 CLR 调用方式不能简单当成普通元组函数；互操作章节会单独处理。

## 部分应用保留剩余工作 {#partial-application}

向柯里化函数提供少于全部的实参，会得到等待剩余实参的新函数。这叫**部分应用**，不是错误的“不完整调用”。

在第一个示例中，`lineTotal 19.50m` 得到 `int -> decimal`，并绑定为 `standardLineTotal`。单价被固定，调用方以后只需提供座位数。相同思路也用于服务费：

```fsharp:line-numbers [ch03-functions-as-values.fsx]
let addFee fee subtotal = subtotal + fee
let addServiceFee = addFee 2.00m
let finalTotal = addServiceFee totalForThree

printfn "With service fee: %M" finalTotal
```
`addFee` 的类型是 `decimal -> decimal -> decimal`。`addFee 2.00m` 返回 `decimal -> decimal`，新函数在以后调用时仍能使用已经提供的 `2.00m`。这种会记住周围值的函数称为**闭包**。运行时可以优化它的具体表示，但函数仍会保留并使用这个值。

参数顺序因此是 API 设计的一部分。较稳定、适合预先固定的配置通常放在前面；频繁变化、最终流经计算的数据通常放在后面。第 13 章会系统讨论面向管道的参数顺序，现在先从部分应用观察这一后果。

## 匿名函数直接创建函数值 {#anonymous-functions}

有时一个短函数只在附近使用一次，无需先命名。`fun` 表达式直接产生匿名函数：

```fsharp:line-numbers [ch03-functions-as-values.fsx]
let increment seats = seats + 1
let incrementAnonymous = fun seats -> seats + 1

printfn "Named and anonymous: %d, %d" (increment 3) (incrementAnonymous 3)
```
`fun seats -> seats + 1` 可以读作“接收 seats，产生 seats 加一”。箭头左侧是形参模式，右侧是主体表达式。`increment` 与 `incrementAnonymous` 都推断为 `int -> int`，调用结果也相同。

名称能记录意图并改善诊断，所以不要为了短而把所有函数改成匿名形式。匿名函数最适合局部行为，尤其是作为另一个函数的实参；相同逻辑被多处使用或本身代表领域概念时，命名通常更清楚。

## 高阶函数组合行为 {#higher-order-functions}

**高阶函数**至少做一件事：接收函数值，或返回函数值。部分应用已经展示了返回函数；下面展示接收函数：

```fsharp:line-numbers [ch03-functions-as-values.fsx]
let applyTwice transform value = transform (transform value)
let incrementedTwice = applyTwice increment 3

printfn "Applied twice: %d" incrementedTwice
```
`applyTwice` 不知道 `transform` 的具体业务含义。它只要求第一次变换的输出能再次作为同一变换的输入，因此 FSI 推断：

```text
val applyTwice: ('a -> 'a) -> 'a -> 'a
```

括号非常关键。第一个实参本身具有函数类型 `'a -> 'a`；随后是一个 `'a` 值；结果仍为 `'a`。没有括号时，右结合会得到另一种函数类型。

当整体流程不变、只有其中的行为需要替换时，可以用高阶函数把这项行为作为参数传入。行为固定时，直接调用命名函数通常更清楚。是否使用高阶函数应由问题决定，而不是由代码行数决定。

## 泛型函数不依赖某个具体类型 {#generic-functions}

观察一个不检查、修改或构造输入的函数：

```fsharp:line-numbers [ch03-functions-as-values.fsx]
let identity value = value
let unchangedNumber = identity 42
let unchangedText = identity "F#"

printfn "Identity values: %d, %s" unchangedNumber unchangedText
```
`identity` 只返回它收到的值，主体没有要求具体类型。编译器把其类型**自动泛化**为：

```text
val identity: 'a -> 'a
```

`'a` 是类型变量，调用时可以由某个具体类型替换，但同一次调用的输入与输出必须是同一类型。因此，同一个绑定可以先用于 `int`，再用于 `string`；每次调用都会经过静态类型检查。

`applyTwice` 中同一个 `'a` 出现三次，表示变换的输入、输出和待变换值必须是同一类型。不同字母如 `'a` 与 `'b` 则表示这些位置不必使用相同类型。

写出全部形参的函数定义在安全时通常可以泛化。可变状态、部分应用与复杂值则可能触发**值限制**。遇到这项诊断时，第 11 章会给出准确规则和对应修复方法。

## 运行共享示例 {#run-example}

在示例所在目录执行：

```console
dotnet fsi --exec ch03-functions-as-values.fsx
```

应得到：

```text
Curried total: 58.50
Tupled total: 58.50
Named and anonymous: 4, 4
Applied twice: 5
With service fee: 60.50
Identity values: 42, F#
```

柯里化与元组版本都得到 `58.50`，但签名和部分应用方式不同。验证函数时应同时查看输出与类型。

## 先理清函数应用的分组 {#debugging}

函数相关诊断常来自应用边界。依次检查：

1. 在 FSI 中查看函数值的完整签名；
2. 按右结合为箭头类型写出括号；
3. 按左结合为函数应用写出括号；
4. 区分 `a b` 的连续应用与 `(a, b)` 的一个元组；
5. 检查部分应用后得到的是函数，还是已经得到最终值。

若诊断说“期望某个值，却得到函数”，请补充或追踪剩余实参。若诊断说某个值无法作为函数应用，前一步可能已经得到最终结果。

匿名函数的主体很长时，先把它绑定成有名称的函数，让 FSI 单独显示签名。修好类型后再决定是否内联；这比在深层括号里反复猜测有效。

## 练习 {#exercises}

先写签名，再计算输出。根据类型中的箭头和元组判断函数是否柯里化。

### 练习 1：解码箭头 {#exercise-01}

解释以下四个签名，并为每个箭头补出结合括号：

1. `lineTotal: decimal -> int -> decimal`；
2. `standardLineTotal: int -> decimal`；
3. `applyTwice: ('a -> 'a) -> 'a -> 'a`；
4. `identity: 'a -> 'a`。

对每个签名说明它依次接收什么、产生什么，以及 `'a` 的重复出现约束了什么。

### 练习 2：传入行为 {#exercise-02}

使用 `applyTwice` 完成两次调用：一次传入命名函数 `increment`，一次直接传入等价的匿名函数。两次都从 `3` 开始。

写出匿名函数、两次调用的结果和相关类型。然后说明 `applyTwice` 为什么不能直接接收一个把 `int` 转成 `string` 的函数。

### 练习 3：选择参数形式 {#exercise-03}

比较 `lineTotal` 与 `lineTotalTupled`：

1. 写出两者完整类型与合法调用；
2. 只固定单价 `19.50m` 时，哪个版本可以直接部分应用？
3. `addServiceFee` 保留了什么值，剩余输入类型是什么？
4. 若单价和座位数在领域中始终作为一个不可分的坐标对传递，元组版本为何可能更清楚？

[查看本章练习答案](../solutions/ch-03-functions-as-values)。

## 核心结论 {#summary}

- 函数是值；定义函数建立绑定，应用函数求值主体并产生结果。
- F# 用空格应用普通函数；应用左结合，箭头类型右结合。
- 柯里化函数表示连续单参数函数，部分应用会返回等待剩余实参的新函数。
- 元组函数接收一个组合值，柯里化函数接收连续实参；两种签名描述不同的输入形式。
- 匿名函数用 `fun parameter -> body` 直接创建函数值。
- 高阶函数接收或返回函数；闭包让返回的函数保留定义环境中的值。
- 自动泛化用 `'a` 等类型变量表达与具体类型无关、但位置之间仍一致的约束。

下一章会让函数主体开始做选择：`if` 与 `match` 都是产生值的表达式，模式则把输入结构与分支内绑定结合起来。

## 来源 {#sources}

- [Microsoft Learn：函数](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/functions/)
- [Microsoft Learn：匿名函数与 `fun`](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/functions/lambda-expressions-the-fun-keyword)
- [Microsoft Learn：形参与实参](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/parameters-and-arguments)
- [Microsoft Learn：类型推断](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/type-inference)
- [Microsoft Learn：自动泛化与值限制](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/generics/automatic-generalization)
