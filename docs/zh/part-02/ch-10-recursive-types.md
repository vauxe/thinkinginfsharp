---
title: "第 10 章：递归类型与结构递归"
description: "用递归可辨识联合建模树，再从类型案例直接推导遍历、map 与 fold。"
translationKey: part-02/ch-10-recursive-types
kind: chapter
part: 2
chapter: 10
status: complete
verifiedWith:
  fsharp: "10"
  dotnetSdk: "10.0.301"
exampleIds:
  - ch10-recursive-types
exerciseIds:
  - ch10-exercise-01
  - ch10-exercise-02
  - ch10-exercise-03
termIds:
  - discriminated-union
  - fold
  - recursive-type
  - recursion
  - structural-recursion
  - tail-call
sources:
  - id: microsoft-discriminated-unions
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/discriminated-unions
    checked: "2026-08-24"
  - id: microsoft-recursive-functions
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/functions/recursive-functions-the-rec-keyword
    checked: "2026-08-24"
  - id: microsoft-functions
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/functions/
    checked: "2026-08-24"
---

# 第 10 章：递归类型与结构递归 {#overview}

一个场馆可以分成多个区域，每个区域又分成更小区域，直到叶子保存可预约的小组。深度事先并不固定。使用 `Left`、`LeftLeft` 和 `LeftRight` 等字段的扁平记录无法表达这种开放形状；列表又会丢掉小组会产生分支这一事实。

递归类型通过引用自身解决表示问题。结构递归沿用相同案例，并且只递归进入其中保存的更小值，从而解决处理问题。因此，类型定义并非只是存储语法，它也是使用该类型的函数计划。

## 学完本章后你能做什么 {#outcomes}

学完本章后，你应该能够：

- 为树定义一个递归可辨识联合；
- 把它的案例读作每个合法值的语法；
- 从该语法推导终止的遍历；
- 编写保持形状的泛型 `map`；
- 把重复的递归提取为可复用的 `fold`；
- 通过为每个案例提供一条规则来推导查询；
- 根据节点数估算时间，根据树高估算调用栈使用；
- 识别深度何时是输入边界，而不只是实现细节。

本章使用普通、有限的内存值，不加入可变性、惰性循环或栈优化方案。

## 递归案例保存同类型的更小值 {#recursive-type}

共享类型表示空树、一个叶子值，或包含两棵子树的分支：

<<< @/../examples/scripts/ch10-recursive-types.fsx#recursive-type{fsharp:line-numbers} [ch10-recursive-types.fsx]

`BookingTree<'T>` 出现在自己的 `Branch` 案例内部。这个自身引用让它成为递归类型。类型参数说明同一棵树的每个叶子携带同一种负载类型，而树形状与该类型无关。

把这些案例读成构造规则：

```text
一个 BookingTree<'T> 是
  Empty
  或者包含一个 'T 的 Leaf
  或者包含两个 BookingTree<'T> 值的 Branch
```

尽管类型允许任意深度，`Branch(Leaf 2, Branch(Leaf 3, Leaf 4))` 仍是有限值。每个分支值包含已经构造好的子树。声明允许继续嵌套，却不会自行创建无限值。

所选案例就是领域策略。若空树没有意义，应删去 `Empty` 并定义非空树。若分支节点也需要标签，就让 `Branch` 携带值。在判断哪些形状合法之前，不要照搬这个具体类型。

## 类型给出遍历骨架 {#structural-traversal}

要使用每一种 `BookingTree`，就要覆盖每个案例。要处理 `Branch`，就递归处理它的两个子树字段：

<<< @/../examples/scripts/ch10-recursive-types.fsx#structural-traversal{fsharp:line-numbers} [ch10-recursive-types.fsx]

`let rec` 让函数名可以在自己的主体中使用。两个函数具有相同的结构骨架：

- `Empty` 是基础案例，不发起递归调用；
- `Leaf` 处理负载，不发起递归调用；
- `Branch` 对 `left` 和 `right` 调用自身，再组合两个结果。

这就是**结构递归**。每次递归调用都接收匹配值的直接组成部分，因此对普通有限树而言，它会逐步走向 `Empty` 或 `Leaf`。终止性显现在类型与函数的关系里，而不是藏在数字计数器中。

结果规则仍由问题决定。`countLeaves` 给每个叶子 `1`，`totalSeats` 则给它负载值。两者都给 `Empty` 加法单位元 `0`，并用加法组合分支结果。

## `map` 改变负载并保持形状 {#tree-map}

树的 map 处理每个案例，却只改变叶子：

<<< @/../examples/scripts/ch10-recursive-types.fsx#tree-map{fsharp:line-numbers} [ch10-recursive-types.fsx]

它的推断形状是：

```fsharp
mapTree : ('T -> 'U) -> BookingTree<'T> -> BookingTree<'U>
```

`Empty` 仍是 `Empty`；`Leaf value` 变成 `Leaf (mapping value)`；`Branch` 用映射后的子树在原位置重新构造。映射函数无需知道分支，遍历也无需知道负载转换的细节。

两条有用的定律说明“保持形状”的含义：

```text
mapTree id tree = tree
mapTree (f >> g) tree = mapTree f tree |> mapTree g
```

第二条定律以普通纯函数为前提；测试时，相关值还应支持相等。这些定律是设计检查，不是特殊的编译器行为。重新排序或丢弃分支都会违反它们。

`renderTree` 是另一项结构遍历。它不会以树的形式保持形状，而是把相同案例转换成文本。相似骨架反复出现，说明可以把案例处理提取出来。

## `fold` 为每个案例命名一条规则 {#tree-fold}

共享 fold 捕获了递归机制：

<<< @/../examples/scripts/ch10-recursive-types.fsx#tree-fold{fsharp:line-numbers} [ch10-recursive-types.fsx]

从类型出发读取它的参数：

```fsharp
foldTree :
    onEmpty:'State ->
    onLeaf:('T -> 'State) ->
    onBranch:('State -> 'State -> 'State) ->
    tree:BookingTree<'T> ->
    'State
```

fold 用调用方提供的规则替换每个构造器。`Empty` 变成 `onEmpty`；叶子变成 `onLeaf value`；分支先折叠两棵子树，再用 `onBranch` 组合其结果。

这只是从每个调用方移除显式递归，并未消除遍历工作。每个节点仍会被访问。`countWithFold` 与 `totalWithFold` 只在三条规则上不同，脚本还会检查它们与直接定义是否一致。

fold 不只可以产生数字。把 `'State` 选为记录，就能一起计算数量、总和与最大值；把它选为另一棵树，就能重建结构；把它选为函数，就能构造更专门的遍历。类型签名说明每条规则都必须返回相同的状态类型。

### 从 `fold` 推导 `map` {#map-from-fold}

信任 `foldTree` 后，就能不再编写 `let rec` 而表达 map：

```fsharp
let mapTreeWithFold mapping =
    foldTree
        Empty
        (mapping >> Leaf)
        (fun left right -> Branch(left, right))
```

三个实参恰好是保持三个构造器的规则。显式递归版仍有价值，因为它揭示推导过程；熟悉形状后，fold 版本则把遍历集中在一处。

## 高度预示直接遍历的栈需求 {#depth-and-stack}

示例用同一骨架定义高度：

<<< @/../examples/scripts/ch10-recursive-types.fsx#tree-depth{fsharp:line-numbers} [ch10-recursive-types.fsx]

按照 `Empty = 0`、`Leaf = 1` 的约定，分支高度是较高子树的高度加一。示例分支有三个叶子，高度也为三。叶子数与高度衡量不同事实：平衡树可以用不高的高度容纳很多叶子；单侧树的高度却可能与节点数成正比。

对于 `countLeaves`、`mapTree` 和 `foldTree`：

- 运行时间为 `O(n)`，因为 `n` 个节点各访问一次；
- 直接调用栈使用为 `O(h)`，其中 `h` 是最大高度；
- `mapTree` 重建时还会分配 `O(n)` 个输出节点。

这些分支遍历不是尾递归：子调用返回后，函数仍需处理另一个子节点或组合结果。机械地加入累加器不会消除这些待处理工作。

对于普通且有界的领域树，直接定义通常最清楚。如果输入可能带有攻击性或极深，高度就需要显式限制、测量，或改用带显式工作栈的迭代遍历。这应是由预期输入支撑的需求，而不是预先模糊所有递归定义的理由。

## 相互递归的形状使用 `and` {#mutual-recursion}

有时两个类型相互包含。F# 用 `and` 连接其声明：

```fsharp
type Expression =
    | Literal of int
    | Let of Binding * Expression
and Binding =
    { Name: string
      Value: Expression }
```

只有领域确实存在两个不同概念时，才使用相互递归。单个递归联合更容易遍历，不应只为展示语法而拆分。相互递归函数使用相应的 `let rec ... and ...` 形式。

## 运行共享示例 {#run-example}

在仓库根目录执行：

```console
dotnet fsi --exec examples/scripts/ch10-recursive-types.fsx
```

六行确定性输出覆盖空树、叶子与分支树、直接遍历、改变类型的 map、由 fold 推导的查询、高度，以及叶子数保持不变。

## 练习 {#exercises}

### 练习 1：从案例推导查询 {#exercise-01}

用结构递归编写 `exists : ('T -> bool) -> BookingTree<'T> -> bool`。先说明每个案例的规则，再写代码。分支实现是否总应访问右子树？解释布尔短路带来的结果。

### 练习 2：检验 map 定律 {#exercise-02}

实现 `mapTreeWithFold`，再对 `emptyTree`、`leafTree` 和 `branchTree` 检查恒等律与复合律。解释为什么检查三个示例能增加信心，却不能证明该定律对每棵树都成立。

### 练习 3：用一次 fold 计算一份摘要 {#exercise-03}

定义一个包含 `LeafCount`、`TotalSeats` 和 `MaximumSeats : int option` 的摘要记录。用一次 `foldTree` 遍历计算它。分别给出 `Empty`、`Leaf 2` 与共享分支树的正确摘要，再说明时间与直接调用栈边界。

[查看本章练习答案](../solutions/ch-10-recursive-types)。

## 模型复盘 {#model-review}

- 递归类型表达可任意嵌套、但通常有限的值。
- 结构递归映照类型案例，并递归进入直接的递归字段。
- `map` 改变负载类型，同时保持树的构造形状。
- `fold` 为每个构造器公开一条规则，并集中递归管线。
- 节点数预示遍历工作，最大高度预示直接调用栈深度。
- 当树可能不受信任或极深时，深度限制属于输入契约。

第 11 章会研究 `mapTree` 等泛型函数是怎样被推断的、泛化在哪里停止，以及操作会引入哪些类型约束。

## 资料来源 {#sources}

- [Microsoft Learn：可辨识联合](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/discriminated-unions)
- [Microsoft Learn：递归函数与 `rec`](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/functions/recursive-functions-the-rec-keyword)
- [Microsoft Learn：函数](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/functions/)
