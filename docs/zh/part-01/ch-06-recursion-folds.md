---
title: "第 6 章：递归、尾调用与折叠"
description: "从列表结构推导递归，区分普通与尾递归，并用累加器和 List.fold 重写线性聚合。"
translationKey: part-01/ch-06-recursion-folds
---

# 第 6 章：递归、尾调用与折叠 {#overview}

列表只有两种结构：空列表 `[]`，或由 `head :: tail` 组成的非空列表。结构递归按照这两种结构来定义函数：空列表给出基础结果；非空列表处理首项，再用同一函数处理更短的尾部。

这种对应关系决定函数的分支，却不能证明终止性、效率或栈安全。下面会区分“结构上更小”“尾位置”和“编译器优化”，再用 `List.fold` 表达常见的线性累加模式。

这里先处理单链列表的一条递归路径。第 10 章再讨论树与多分支递归。异步与任务递归使用不同的执行模型，因此要分别分析栈行为。

## `rec` 让名称在主体中可见 {#rec-binding}

普通非递归 `let` 的名称只在右侧求值完成后进入后续作用域。`let rec` 则让函数名称在自己的主体中可见，因此可以调用自身：

```fsharp:line-numbers [ch06-recursion-folds.fsx]
let rec sumRecursive values =
    match values with
    | [] -> 0
    | head :: tail -> head + sumRecursive tail
```
`rec` 只改变绑定可见性。程序员仍需提供基础情况和递减步骤。递归分支若原样传回列表，代码可能无限递归；匹配若遗漏 `[]`，则会留下未覆盖的输入。

互相调用的函数可用 `let rec ... and ...` 一起定义。把这种形式留给真实的相互依赖；分开其他函数可以缩小推断与理解范围。

## 从数据结构推导分支 {#structural-recursion}

`sumRecursive` 是**结构递归**：匹配分支对应列表构造方式。

- `[]` 没有元素，其和使用加法单位元 `0`；
- `head :: tail` 把当前首项与更小尾部的和组合；
- 每次递归都传入 `tail`，长度严格减少一。

终止推理由两个部分组成：有限列表最终会到达 `[]`；递归分支确实使用结构上更小的 `tail`。类型系统确认两个分支都返回 `int`；递减论证来自对递归输入的检查。

基础结果表达空输入的含义。求积使用单位元 `1`；复制列表可从 `[]` 开始；查找元素则明确表示“没有找到”。错误的基础值会先破坏空输入，再沿递归传播到其他输入。

### 展开一次调用 {#expansion}

对 `[ 3; 0; 4 ]`，直接递归的含义是：

```text
3 + sumRecursive [0; 4]
3 + (0 + sumRecursive [4])
3 + (0 + (4 + sumRecursive []))
3 + (0 + (4 + 0))
```

展开过程揭示两件事：顺序从列表头向尾推进；每层在递归结果回来后还要做一次加法。第二点把这个函数归为非尾递归。

## 尾位置没有待完成工作 {#tail-position}

**尾调用**是分支返回前的最后一个操作：当前函数直接返回调用结果。尾递归要求递归路径上的自调用处于尾位置。

`head + sumRecursive tail` 中，递归调用返回后仍要与 `head` 相加，因此调用之后还有待完成工作。把这些工作放进累加器，才能形成尾位置；括号和换行会保留原有执行顺序。

### 累加器携带已完成结果 {#accumulator}

把此前的和作为额外参数传到下一步：

```fsharp:line-numbers [ch06-recursion-folds.fsx]
[<TailCall>]
let rec sumLoop accumulator values =
    match values with
    | [] -> accumulator
    | head :: tail -> sumLoop (accumulator + head) tail

let sumTailRecursive values = sumLoop 0 values
```
`sumLoop` 每轮先计算新的 `accumulator + head`，再以该值和 `tail` 调用自身。分支直接返回调用结果，所以递归调用位于尾位置。

理解累加器应写出不变量：在每一步，`accumulator + sum values` 等于原输入总和。开始时累加器为 `0`；移动一个 `head` 到累加器后等式仍成立；`values` 为空时，累加器就是完整答案。

外层 `sumTailRecursive` 隐藏初始累加器，给调用方保留 `int list -> int` 的清楚接口。让调用方传任意初始状态会泄漏实现细节，也更容易破坏不变量。

### `[<TailCall>]` 检查意图 {#tailcall-attribute}

F# 8 起可在模块函数或方法上使用 `[<TailCall>]`。编译器若发现该函数中的递归调用不在尾位置，会给出警告。把下面这个刻意无效的 `.fs` 示例放进启用“警告即错误”的最小项目，即可观察 `FS3569`：

```fsharp:line-numbers [NonTailRecursion.fs]
[<TailCall>]
let rec fibonacci n =
    match n with
    | 0
    | 1 -> n
    | value -> fibonacci (value - 1) + fibonacci (value - 2)
```
常规脚本检查也会用 `--warnaserror+` 运行 FSI。上面的编译型反例专门展示 `TailCall` 诊断。代码位置和下方的有限输入运行，则从另外两个角度检查共享循环。

这个属性只检查递归调用是否位于尾位置；它既不修改算法，也不证明函数会终止。跨函数调用、运行时行为、调试设置和计算表达式也会影响实际栈使用。这里能得出的结论仅是：这个同步自调用位于尾位置。

共享脚本还会让这个实现计算 100,000 项列表的长度：

```fsharp:line-numbers [ch06-recursion-folds.fsx]
[<TailCall>]
let rec countLoop accumulator values =
    match values with
    | [] -> accumulator
    | _ :: tail -> countLoop (accumulator + 1) tail

let countTailRecursive values = countLoop 0 values
let largeCount = countTailRecursive [ 1..100_000 ]

printfn "Tail-recursive count: %d" largeCount
```
请结合代码位置、编译器诊断和有限输入测试来比较实现。.NET 通常把栈溢出视为会终止进程的严重故障，因此不要故意耗尽进程栈。

## 尾递归解决栈使用问题 {#tail-recursion-limits}

尾递归主要改变栈使用方式。时间复杂度、数值溢出、副作用顺序和分配成本是独立问题。一个反复计算同一子问题的指数算法，即使某条调用位于尾位置，仍然具有指数复杂度。

累加器还可能改变结果顺序。第 5 章用 `::` 构造反向列表，最后必须 `List.rev`；若忘记反转，函数可能栈安全却语义错误。多个递归分支、异常处理或递归调用后的构造也需要单独分析。

因此评审递归至少问四个问题：问题是否缩小、基础情况是否可达、调用是否在尾位置、算法是否仍重复或累积了昂贵工作。

## `fold` 抽取线性累加模式 {#fold}

尾递归求和包含一个通用骨架：从初始状态开始，按顺序把每个元素并入状态，最后返回状态。`List.fold` 把骨架留在库中，只要求提供更新函数与初始状态：

```fsharp:line-numbers [ch06-recursion-folds.fsx]
let sumWithFold values =
    values |> List.fold (fun accumulator value -> accumulator + value) 0
```
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

```fsharp:line-numbers [ch06-recursion-folds.fsx]
let leftAssociated = List.fold (fun state value -> state - value) 0 [ 1; 2; 3 ]
let rightAssociated = List.foldBack (fun value state -> value - state) [ 1; 2; 3 ] 0

printfn "Fold order: left=%d right=%d" leftAssociated rightAssociated
```
左折叠计算 `((0 - 1) - 2) - 3 = -6`；右折叠计算 `1 - (2 - (3 - 0)) = 2`。加法在本例整数范围内不受方向影响，不能据此误以为所有折叠顺序等价。

不要仅根据语义展开猜测库函数的栈或分配实现。API 保证组合顺序与结果语义；具体性能应参考当前 FSharp.Core 实现并测量。需要方向敏感行为时，先写清数学结合方式。

## 何时直接递归，何时折叠 {#choosing-recursion}

| 问题结构 | 通常先考虑 | 原因 |
| --- | --- | --- |
| 线性遍历并携带一个状态 | `List.fold` 或专用库函数 | 通用遍历已封装，状态类型显式 |
| 求和、长度等已有专用操作 | `List.sum`、`List.length` | 领域意图比手写 folder 更直接 |
| 输出结构直接镜像输入结构 | 清楚的结构递归或 `map` | 构造关系可从模式看到 |
| 需要提前停止 | `tryFind`、`exists` 等专用函数，或谨慎递归 | `fold` 通常会遍历全部输入 |
| 树或多个递归分支 | 与类型结构一致的递归 | 一个线性累加器未必足够 |

当一个累加器能清楚表达状态转移时，优先考虑 `fold`。庞大而含义模糊的累加器会隐藏状态含义和更新顺序；应选择最能显露不变量、终止条件和结果顺序的形式。

## 成本与限制 {#costs}

对长度为 `n` 的有限列表，三个求和版本正常完成时都做线性数量的元素处理。主要差别是控制状态放在哪里：

| 版本 | 时间 | 递归栈直觉 | 其他状态 |
| --- | --- | --- | --- |
| 直接递归 | `O(n)` | 非尾调用，深度随 `n` 增长 | 返回后逐层完成加法 |
| 累加器递归 | `O(n)` | 简单自尾调用可由编译器消除增长；属性检查本例意图 | 一个累加器 |
| `List.fold` | `O(n)` | 遍历由 FSharp.Core 实现管理 | 一个累加状态 |

每种实现都要分别检查 `int` 溢出和输入的业务含义。栈安全、算术安全与领域正确性也必须分别验证。

## 运行共享示例 {#run-example}

在仓库根目录执行：

```console
dotnet fsi --warnaserror+ --exec examples/scripts/ch06-recursion-folds.fsx
```

应得到：

```text
Sums: recursive=9 tail=9 fold=9
Empty sums: 0, 0, 0
Singleton sums: 5, 5, 5
Tail-recursive count: 100000
Fold order: left=-6 right=2
```

空列表、单项列表、普通列表与大列表分别验证基础情况、结果一致性，以及这个尾递归实现能处理给定的大输入。请按顺序比较五行输出。

## 先检查递减，再检查尾位置 {#debugging}

递归出错时按顺序问：

1. 每个输入构造是否有规则；
2. 基础规则返回的是否是正确单位元或终止结果；
3. 递归参数是否严格更小或更接近终止；
4. 递归调用回来后是否还有运算、构造或副作用；
5. 累加器不变量在初始、推进与结束三处是否成立。

结果顺序颠倒通常来自前插累积后漏掉反转。`fold` 类型错误常来自把 accumulator 与 element 参数顺序写反；先从完整签名标出 `'State` 与 `'T`。

运行时间异常增长时，同时检查尾位置和重复工作。画出一个小输入的调用树，观察同一子问题是否出现多次。尾调用处理栈帧保留；记忆化或更换算法处理重复工作。

## 练习 {#exercises}

用小输入手工展开，并结合代码位置、编译器诊断和有限输入上的运行结果进行验证。每题都要写出基础情况、递减参数和结果顺序。

### 练习 1：展开结构递归 {#exercise-01}

对 `sumRecursive [ 3; 0; 4 ]`：

1. 写出直到 `[]` 的完整展开；
2. 标出每次调用的 `head` 与 `tail`；
3. 解释为什么函数会终止；
4. 圈出每层递归返回后仍待执行的工作，据此判断调用的尾位置。

### 练习 2：证明累加器含义 {#exercise-02}

对 `sumLoop 0 [ 3; 0; 4 ]`，列出每一步 `(accumulator, values)`。用“累加器加剩余列表之和等于原列表之和”检查每一步。

然后设想把递归分支改为“先递归，再把 `head` 相加”。说明 `[<TailCall>]` 应该提醒什么，以及即使通过尾调用检查，仍有哪些终止或数值性质没有被证明。

### 练习 3：展开折叠并选择抽象 {#exercise-03}

1. 展开 `List.fold` 对 `[ 1; 2; 3 ]` 做减法的括号；
2. 展开 `List.foldBack` 的对应括号并算出结果；
3. 写出用 `List.fold` 计算列表长度的初始状态与 folder 类型；
4. 为普通求和、提前寻找首个匹配项和遍历二叉树分别选择专用函数、折叠或直接递归，并说明理由。

[查看本章练习答案](../solutions/ch-06-recursion-folds)。

## 第一部分检查点 {#part-checkpoint}

在仓库根目录运行集成后的预约脚本：

```console
dotnet fsi --warnaserror+ --exec examples/capstone/part-01/BookingBasics.fsx
```

输出必须区分有效与无效输入行，接受容量允许的请求，拒绝超容量请求，并得到正确的已预约与剩余容量。这完成了第一部分从基础语法到小型数据处理流程的学习路径；持久化与并发保证会在后续部分加入。

[继续阅读第 7 章](../part-02/ch-07-records-equality)，用记录与联合类型把更多隐含规则提升为类型。

## 来源 {#sources}

- [Microsoft Learn：递归函数与 `rec`、尾递归、`TailCall`](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/functions/recursive-functions-the-rec-keyword)
- [Microsoft Learn：函数](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/functions/)
- [Microsoft Learn：列表递归与折叠](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/lists)
- [FSharp.Core：List 模块参考](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-collections-listmodule.html)
