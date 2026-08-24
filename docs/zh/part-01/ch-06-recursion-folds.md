---
title: "第 6 章：递归、尾调用与折叠"
description: "从列表结构推导递归，区分普通与尾递归，并用累加器和 List.fold 重写线性聚合。"
translationKey: part-01/ch-06-recursion-folds
kind: chapter
part: 1
chapter: 6
status: complete
verifiedWith:
  fsharp: "10"
  dotnetSdk: "10.0.301"
exampleIds:
  - ch06-recursion-folds
exerciseIds:
  - ch06-exercise-01
  - ch06-exercise-02
  - ch06-exercise-03
termIds:
  - accumulator
  - fold
  - list
  - pattern-matching
  - recursion
  - structural-recursion
  - tail-call
  - tail-recursion
sources:
  - id: microsoft-recursive-functions
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/functions/recursive-functions-the-rec-keyword
    checked: "2026-08-24"
  - id: microsoft-functions
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/functions/
    checked: "2026-08-24"
  - id: microsoft-lists
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/lists
    checked: "2026-08-24"
  - id: fsharp-core-list
    url: https://fsharp.github.io/fsharp-core-docs/reference/fsharp-collections-listmodule.html
    checked: "2026-08-24"
---

# 第 6 章：递归、尾调用与折叠 {#overview}

列表只有两种结构：空列表 `[]`，或由 `head :: tail` 组成的非空列表。递归不是凭空让函数“再调用一次”，而是让函数定义沿着数据结构本身下降：空结构给出基础结果，非空结构处理首项，再把更小的尾部交给同一规则。

这种直接对应很有解释力，却不自动保证终止、效率或栈安全。本章会把“结构上更小”“尾位置”和“编译器优化”分开讨论，再把常见的线性累加模式交给 `List.fold`。目标不是到处手写递归，而是能读懂、验证并选择它。

## 本章完成后你能做什么 {#outcomes}

完成本章后，你应该能够：

- 用 `let rec` 定义自引用函数，并解释为什么普通 `let` 不允许自引用；
- 从 `[] | head :: tail` 推导基础分支与递归分支；
- 说明递归参数如何结构性缩小以及终止依赖什么；
- 判断递归调用之后是否还有待完成工作；
- 用累加器把一个线性聚合改写为尾递归；
- 用 `[<TailCall>]` 检查尾调用意图，同时理解它不是普遍栈安全保证；
- 展开 `List.fold` 与 `List.foldBack` 的顺序，并选择合适抽象。

本章只处理单链列表的一条递归路径。树与多分支结构递归在第 10 章出现，异步/任务递归则必须服从各自执行模型，不能直接套用这里的同步尾调用结论。

## `rec` 让名称在主体中可见 {#rec-binding}

普通非递归 `let` 的名称只在右侧求值完成后进入后续作用域。`let rec` 则让函数名称在自己的主体中可见，因此可以调用自身：

<<< @/../examples/scripts/ch06-recursion-folds.fsx#direct-recursion{fsharp:line-numbers} [ch06-recursion-folds.fsx]

`rec` 只改变绑定可见性，不会替你添加基础情况，也不会证明每次调用都接近终止。若递归分支把原列表原样传回，代码可以无限递归；若漏掉 `[]`，匹配也会不穷尽。

互相调用的函数可用 `let rec ... and ...` 一起定义，但只有真实的相互依赖才需要它。把无关函数放进递归组会扩大推断和理解范围，本章不使用这种形式。

## 从数据结构推导分支 {#structural-recursion}

`sumRecursive` 是**结构递归**：匹配分支对应列表构造方式。

- `[]` 没有元素，其和使用加法单位元 `0`；
- `head :: tail` 把当前首项与更小尾部的和组合；
- 每次递归都传入 `tail`，长度严格减少一。

终止推理由两个部分组成：有限列表最终会到达 `[]`；递归分支确实使用结构上更小的 `tail`。类型系统帮助确认两个分支都返回 `int`，但一般不会证明这项递减论证。

基础结果不是随意填充值。求积需要单位元 `1`；复制列表可能以 `[]` 为基础；寻找元素则需要表达“未找到”。选择错误基础值会让空输入先出错，并沿递归传播到所有输入。

### 展开一次调用 {#expansion}

对 `[ 3; 0; 4 ]`，直接递归的含义是：

```text
3 + sumRecursive [0; 4]
3 + (0 + sumRecursive [4])
3 + (0 + (4 + sumRecursive []))
3 + (0 + (4 + 0))
```

展开过程揭示两件事：顺序从列表头向尾推进；每层在递归结果回来后还要做一次加法。第二点决定它不是尾递归。

## 尾位置没有待完成工作 {#tail-position}

**尾调用**是分支返回前的最后一个操作：当前函数拿到调用结果后无需再加工，直接把它作为自己的结果。尾递归要求递归路径上的自调用处于尾位置。

`head + sumRecursive tail` 中，递归调用返回后仍要与 `head` 相加，因此不是尾调用。括号或换行不会改变这个事实；要改变的是数据怎样携带未完成工作。

### 累加器携带已完成结果 {#accumulator}

把此前的和作为额外参数传到下一步：

<<< @/../examples/scripts/ch06-recursion-folds.fsx#tail-recursion{fsharp:line-numbers} [ch06-recursion-folds.fsx]

`sumLoop` 每轮先计算新的 `accumulator + head`，再以该值和 `tail` 调用自身。递归调用之后没有加法或构造；分支结果就是调用结果，所以它位于尾位置。

理解累加器应写出不变量：在每一步，`accumulator + sum values` 等于原输入总和。开始时累加器为 `0`；移动一个 `head` 到累加器后等式仍成立；`values` 为空时，累加器就是完整答案。

外层 `sumTailRecursive` 隐藏初始累加器，给调用方保留 `int list -> int` 的清楚接口。让调用方传任意初始状态会泄漏实现细节，也更容易破坏不变量。

### `[<TailCall>]` 检查意图 {#tailcall-attribute}

F# 8 起可在模块函数或方法上使用 `[<TailCall>]`。编译器若发现该函数中的递归调用不在尾位置，会给出警告。本书使用 F# 10，并把警告视为错误，因此共享循环的尾调用意图可以自动检查。

这个属性不会把非尾递归魔法般改写成尾递归，也不证明函数一定终止。它只检查相关调用位置。尾位置是编译器消除递归栈增长的重要前提，但跨函数、运行时、调试设置、计算表达式与其他执行模型可能有不同限制；不要从一个同步自递归示例推导“所有递归都是栈安全的”。

共享脚本用尾递归计算 100,000 项列表长度，作为这个具体实现的运行证据：

<<< @/../examples/scripts/ch06-recursion-folds.fsx#tail-count{fsharp:line-numbers} [ch06-recursion-folds.fsx]

不要通过故意让非尾递归耗尽进程栈来做对比测试；.NET 的栈溢出通常不是应用可以可靠捕获并继续运行的普通错误。用代码位置、编译器诊断和有界测试判断。

## 尾递归不会修复所有算法 {#tail-recursion-limits}

尾递归主要改变栈使用方式，不自动改变时间复杂度、数值溢出、效果顺序或分配成本。一个重复计算同一子问题的指数算法，即使某条调用在尾位置，也不会因此变成线性算法。

累加器还可能改变结果顺序。第 5 章用 `::` 构造反向列表，最后必须 `List.rev`；若忘记反转，函数可能栈安全却语义错误。多个递归分支、异常处理或递归调用后的构造也需要单独分析。

因此评审递归至少问四个问题：问题是否缩小、基础情况是否可达、调用是否在尾位置、算法是否仍重复或累积了昂贵工作。

## `fold` 抽取线性累加模式 {#fold}

尾递归求和包含一个通用骨架：从初始状态开始，按顺序把每个元素并入状态，最后返回状态。`List.fold` 把骨架留在库中，只要求提供更新函数与初始状态：

<<< @/../examples/scripts/ch06-recursion-folds.fsx#fold-sum{fsharp:line-numbers} [ch06-recursion-folds.fsx]

其核心类型为：

```text
List.fold : ('State -> 'T -> 'State) -> 'State -> 'T list -> 'State
```

第一实参接收当前累加器与元素并产生下一状态；第二实参是初始状态；第三实参是列表。参数顺序让 `values |> List.fold folder initial` 成为自然管道。

对于 `[ a; b; c ]`，左折叠展开为：

```text
folder (folder (folder initial a) b) c
```

`sumWithFold` 中，`'State` 与 `'T` 都实例化为 `int`，但它们不必相同。例如可以把预约元组折叠成文本、计数与金额组成的另一个状态类型。

### `foldBack` 的方向和参数顺序都不同 {#foldback}

`List.foldBack folder [ a; b; c ] initial` 从右侧组合，语义展开为：

```text
folder a (folder b (folder c initial))
```

它的 folder 先接元素、再接状态，与 `List.fold` 的状态优先顺序不同。共享脚本用减法让差异可见：

<<< @/../examples/scripts/ch06-recursion-folds.fsx#fold-order{fsharp:line-numbers} [ch06-recursion-folds.fsx]

左折叠计算 `((0 - 1) - 2) - 3 = -6`；右折叠计算 `1 - (2 - (3 - 0)) = 2`。加法在本例整数范围内不受方向影响，不能据此误以为所有折叠顺序等价。

不要仅根据语义展开猜测库函数的栈或分配实现。API 保证组合顺序与结果语义；具体性能应参考当前 FSharp.Core 实现并测量。需要方向敏感行为时，先写清数学结合方式。

## 何时直接递归，何时折叠 {#choosing-recursion}

| 问题形状 | 通常先考虑 | 原因 |
| --- | --- | --- |
| 线性遍历并携带一个状态 | `List.fold` 或专用库函数 | 通用遍历已封装，状态类型显式 |
| 求和、长度等已有专用操作 | `List.sum`、`List.length` | 领域意图比手写 folder 更直接 |
| 输出结构直接镜像输入结构 | 清楚的结构递归或 `map` | 构造关系可从模式看到 |
| 需要提前停止 | `tryFind`、`exists` 等专用函数，或谨慎递归 | `fold` 通常会遍历全部输入 |
| 树或多个递归分支 | 与类型结构一致的递归 | 一个线性累加器未必足够 |

`fold` 不是“更函数式”的勋章。把简单业务规则塞进难懂的巨大累加器，会让状态含义和更新顺序更隐蔽。选择能让不变量、终止和结果顺序最明显的形式。

## 成本与边界 {#costs}

对长度为 `n` 的有限列表，三个求和版本正常完成时都做线性数量的元素处理。主要差别是控制状态放在哪里：

| 版本 | 时间 | 递归栈直觉 | 其他状态 |
| --- | --- | --- | --- |
| 直接递归 | `O(n)` | 非尾调用，深度随 `n` 增长 | 返回后逐层完成加法 |
| 累加器递归 | `O(n)` | 简单自尾调用可由编译器消除增长；属性检查本例意图 | 一个累加器 |
| `List.fold` | `O(n)` | 遍历由 FSharp.Core 实现管理 | 一个累加状态 |

三者都不会自动防止 `int` 范围溢出，也不会验证输入业务含义。栈安全、算术安全和领域正确性是不同性质，测试一个不能代替另两个。

## 运行共享示例 {#run-example}

从仓库根目录执行：

```console
dotnet fsi --exec examples/scripts/ch06-recursion-folds.fsx
```

应得到：

```text
Sums: recursive=9 tail=9 fold=9
Empty sums: 0, 0, 0
Singleton sums: 5, 5, 5
Tail-recursive count: 100000
Fold order: left=-6 right=2
```

空、一项、普通列表与大列表分别验证基础情况、统一语义和这个尾递归实现的有界运行行为。manifest 按顺序检查五行。

## 调试：先检查递减，再检查尾位置 {#debugging}

递归出错时按顺序问：

1. 每个输入构造是否有规则；
2. 基础规则返回的是否是正确单位元或终止结果；
3. 递归参数是否严格更小或更接近终止；
4. 递归调用回来后是否还有运算、构造或效果；
5. 累加器不变量在初始、推进与结束三处是否成立。

结果顺序颠倒通常来自前插累积后漏掉反转。`fold` 类型错误常来自把 accumulator 与 element 参数顺序写反；先从完整签名标出 `'State` 与 `'T`。

运行时间异常增长时，不要只看是否尾递归。画出一个小输入的调用树，检查同一子问题是否被多次计算。尾调用解决栈帧保留，不解决重复工作。

## 练习 {#exercises}

用小输入手工展开，不要用栈溢出作为实验手段。每题都要写出基础情况、递减参数和结果顺序。

### 练习 1：展开结构递归 {#exercise-01}

对 `sumRecursive [ 3; 0; 4 ]`：

1. 写出直到 `[]` 的完整展开；
2. 标出每次调用的 `head` 与 `tail`；
3. 解释为什么函数会终止；
4. 圈出每层递归返回后仍待执行的工作，并说明为何非尾递归。

### 练习 2：证明累加器含义 {#exercise-02}

对 `sumLoop 0 [ 3; 0; 4 ]`，列出每一步 `(accumulator, values)`。用“累加器加剩余列表之和等于原列表之和”检查每一步。

然后设想把递归分支改为“先递归，再把 `head` 相加”。说明 `[<TailCall>]` 应该提醒什么，以及即使通过尾调用检查，仍有哪些终止或数值性质没有被证明。

### 练习 3：展开折叠并选择抽象 {#exercise-03}

1. 展开 `List.fold` 对 `[ 1; 2; 3 ]` 做减法的括号；
2. 展开 `List.foldBack` 的对应括号并算出结果；
3. 写出用 `List.fold` 计算列表长度的初始状态与 folder 类型；
4. 为普通求和、提前寻找首个匹配项和遍历二叉树分别选择专用函数、折叠或直接递归，并说明理由。

[查看本章练习答案](../solutions/ch-06-recursion-folds)。

## 小结 {#summary}

- `let rec` 让函数名在自身主体可见，但不自动提供基础情况或终止证明。
- 结构递归让模式对应数据构造，并把更小组成部分交给递归调用。
- 递归调用后仍有工作时不是尾调用；换行和括号不能改变这一点。
- 累加器携带已完成结果，使简单线性递归可把自调用放到尾位置。
- `[<TailCall>]` 检查尾调用意图，不证明终止，也不保证所有执行模型的栈安全。
- `List.fold` 从左向右穿行状态；`foldBack` 从右组合且 folder 参数顺序不同。
- 尾递归、时间复杂度、算术安全与领域正确性必须分别验证。

至此，第一部分的语言基础闭合：值、绑定、函数、分支、列表数据流与递归。接下来的贯穿项目会把它们组合成一个纯脚本预约切片，然后第二部分用记录和联合类型把隐含约束提升为领域模型。

## 词汇 {#vocabulary}

- **递归（recursion）：** 函数直接或间接调用自身来处理更小问题。
- **结构递归（structural recursion）：** 按数据构造分支并递归处理结构上更小的组成部分。
- **尾调用（tail call）：** 分支返回前最后执行、其结果无需再加工的调用。
- **尾递归（tail recursion）：** 递归路径把自调用放在尾位置的形式。
- **累加器（accumulator）：** 每一步携带已完成结果到下一步的状态值。
- **折叠（fold）：** 按确定顺序把元素逐项并入累加状态的高阶操作。

## 来源 {#sources}

- [Microsoft Learn：递归函数与 `rec`、尾递归、`TailCall`](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/functions/recursive-functions-the-rec-keyword)
- [Microsoft Learn：函数](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/functions/)
- [Microsoft Learn：列表递归与折叠](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/lists)
- [FSharp.Core：List 模块参考](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-collections-listmodule.html)
