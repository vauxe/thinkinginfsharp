---
title: "第 10 章：递归类型与结构递归"
description: "用递归可区分联合建模树，再从类型案例直接推导遍历、map 与 fold。"
translationKey: part-02/ch-10-recursive-types
---

# 第 10 章：递归类型与结构递归 {#overview}

一个场馆可以分成多个区域，每个区域又分成更小区域，直到叶子保存可预约的小组。深度事先并不固定。使用 `Left`、`LeftLeft` 和 `LeftRight` 等字段的扁平记录无法表达这种开放层次；列表又会丢掉区域之间的分支关系。

递归类型通过引用自身来表示这种不固定的深度。处理它时，结构递归按照相同的案例分支，并且只递归进入案例中较小的子结构。因此，类型定义不仅说明数据如何存储，也提示了处理该数据的函数应有哪些分支。

这里使用有限的内存值，不讨论可变结构、惰性循环值或其他栈管理方案。

## 递归案例保存同类型的更小值 {#recursive-type}

共享类型表示空树、一个叶子值，或包含两棵子树的分支：

```fsharp:line-numbers
type BookingTree<'T> =
    | Empty
    | Leaf of 'T
    | Branch of left: BookingTree<'T> * right: BookingTree<'T>

let emptyTree: BookingTree<int> = Empty
let leafTree = Leaf 2

let branchTree = Branch(Leaf 2, Branch(Leaf 3, Leaf 4))
```
把这段完整起点保存为 `ch10-recursive-types.fsx`。除相互递归类型的独立语法示例外，本章后续代码块都按出现顺序承接这些定义。

`BookingTree<'T>` 出现在自己的 `Branch` 案例内部。这个自身引用让它成为递归类型。类型参数说明同一棵树的每个叶子都保存同一种类型的值，而分支结构与该类型无关。

把这些案例读成构造规则：

```text
一个 BookingTree<'T> 是
  Empty
  或者包含一个 'T 的 Leaf
  或者包含两个 BookingTree<'T> 值的 Branch
```

尽管类型允许任意深度，`Branch(Leaf 2, Branch(Leaf 3, Leaf 4))` 仍是有限值。每个分支值包含已经构造好的子树。声明允许继续嵌套，却不会自行创建无限值。

所选案例就是领域策略。若空树没有意义，应删去 `Empty` 并定义非空树。若分支节点也需要标签，就让 `Branch` 携带值。先决定哪些结构合法，再决定是否采用这个定义。

## 类型决定遍历方式 {#structural-traversal}

要使用每一种 `BookingTree`，就要覆盖每个案例。要处理 `Branch`，就递归处理它的两个子树字段：

```fsharp:line-numbers
let rec countLeaves tree =
    match tree with
    | Empty -> 0
    | Leaf _ -> 1
    | Branch(left, right) -> countLeaves left + countLeaves right

let rec totalSeats tree =
    match tree with
    | Empty -> 0
    | Leaf seats -> seats
    | Branch(left, right) -> totalSeats left + totalSeats right

printfn "Counts: empty=%d leaf=%d branch=%d" (countLeaves emptyTree) (countLeaves leafTree) (countLeaves branchTree)

printfn "Totals: empty=%d leaf=%d branch=%d" (totalSeats emptyTree) (totalSeats leafTree) (totalSeats branchTree)
```
这段代码承接三棵示例树，输出：

```text
Counts: empty=0 leaf=1 branch=3
Totals: empty=0 leaf=2 branch=9
```

`let rec` 让函数名可以在自己的主体中使用。两个函数具有相同的结构骨架：

- `Empty` 是基础案例，不发起递归调用；
- `Leaf` 处理其中的值，不发起递归调用；
- `Branch` 对 `left` 和 `right` 调用自身，再组合两个结果。

这就是**结构递归**。每次递归调用都接收匹配值的直接组成部分，因此对普通有限树而言，它会逐步走向 `Empty` 或 `Leaf`。终止性显现在类型与函数的关系里，而不是藏在数字计数器中。

结果规则仍由问题决定。`countLeaves` 给每个叶子 `1`，`totalSeats` 则使用叶子中的值。两者都给 `Empty` 加法单位元 `0`，并用加法组合分支结果。

## `map` 改变叶子值并保持结构 {#tree-map}

树的 map 处理每个案例，却只改变叶子：

```fsharp:line-numbers
let rec mapTree mapping tree =
    match tree with
    | Empty -> Empty
    | Leaf value -> Leaf(mapping value)
    | Branch(left, right) -> Branch(mapTree mapping left, mapTree mapping right)

let rec renderTree formatValue tree =
    match tree with
    | Empty -> "Empty"
    | Leaf value -> $"Leaf({formatValue value})"
    | Branch(left, right) -> $"Branch({renderTree formatValue left},{renderTree formatValue right})"

let labeledTree = branchTree |> mapTree (fun seats -> $"{seats} seats")

printfn "Mapped: %s" (renderTree id labeledTree)
```
这段续接代码输出：

```text
Mapped: Branch(Leaf(2 seats),Branch(Leaf(3 seats),Leaf(4 seats)))
```

它的推断类型是：

```text
mapTree : ('T -> 'U) -> BookingTree<'T> -> BookingTree<'U>
```

上面是 FSI 显示的类型签名，不是要粘贴进脚本的声明。

`Empty` 仍是 `Empty`；`Leaf value` 变成 `Leaf (mapping value)`；`Branch` 用映射后的子树在原位置重新构造。映射函数无需知道分支，遍历也无需知道叶子值如何转换。

两条有用的定律说明“保持结构”的含义：

```text
mapTree id tree = tree
mapTree (f >> g) tree = mapTree f tree |> mapTree g
```

第二条定律以纯函数为前提；测试时，相关值还应支持相等。这些定律是设计检查，不是特殊的编译器行为。重新排序或丢弃分支都会违反它们。

`renderTree` 是另一项结构遍历。它不会保留树结构，而是把相同案例转换成文本。遍历代码反复出现，说明可以把案例处理提取出来。

## `fold` 抽出每个案例的处理规则 {#tree-fold}

前面的遍历重复了相同的递归过程，下面的 fold 把这个过程集中起来：

```fsharp:line-numbers
let rec foldTree onEmpty onLeaf onBranch tree =
    match tree with
    | Empty -> onEmpty
    | Leaf value -> onLeaf value
    | Branch(left, right) ->
        let leftResult = foldTree onEmpty onLeaf onBranch left
        let rightResult = foldTree onEmpty onLeaf onBranch right
        onBranch leftResult rightResult

let countWithFold = foldTree 0 (fun _ -> 1) (+)

let totalWithFold = foldTree 0 id (+)

printfn
    "Fold agrees: count=%b total=%b"
    (countWithFold branchTree = countLeaves branchTree)
    (totalWithFold branchTree = totalSeats branchTree)
```
这段续接代码输出 `Fold agrees: count=true total=true`。

从类型出发读取它的参数：

```text
foldTree :
    onEmpty:'State ->
    onLeaf:('T -> 'State) ->
    onBranch:('State -> 'State -> 'State) ->
    tree:BookingTree<'T> ->
    'State
```

这同样是供阅读的推断签名，不是独立代码块。

fold 让调用方分别提供处理三个案例的规则：遇到 `Empty` 时返回 `onEmpty`；遇到叶子时调用 `onLeaf value`；遇到分支时先折叠两棵子树，再用 `onBranch` 组合两个结果。

这只是从每个调用方移除显式递归，并未消除遍历工作。每个节点仍会被访问。`countWithFold` 与 `totalWithFold` 只在三条规则上不同，脚本还会检查它们与直接定义是否一致。

fold 不只可以产生数字。把 `'State` 设为记录，就能一起计算数量、总和与最大值；设为另一棵树，就能重建结构；设为函数，就能构造更专门的遍历。类型签名要求每条规则返回同一种结果类型。

### 从 `fold` 推导 `map` {#map-from-fold}

信任前面定义的 `foldTree` 后，就能不再编写 `let rec` 而表达 map：

```fsharp
let mapTreeWithFold mapping =
    foldTree
        Empty
        (mapping >> Leaf)
        (fun left right -> Branch(left, right))
```

三个实参正是保留三个构造器的规则。直接递归版揭示推导过程；熟悉这些规则后，fold 版本可以把遍历集中在一处。

## 高度预示直接遍历的栈需求 {#depth-and-stack}

示例用同一骨架定义高度：

```fsharp:line-numbers
let rec height tree =
    match tree with
    | Empty -> 0
    | Leaf _ -> 1
    | Branch(left, right) -> 1 + max (height left) (height right)

printfn "Heights: empty=%d leaf=%d branch=%d" (height emptyTree) (height leafTree) (height branchTree)

printfn "Shape preserved: before=%d after=%d" (countLeaves branchTree) (countLeaves labeledTree)
```
这段续接代码输出：

```text
Heights: empty=0 leaf=1 branch=3
Shape preserved: before=3 after=3
```

按照 `Empty = 0`、`Leaf = 1` 的约定，分支高度是较高子树的高度加一。示例分支有三个叶子，高度也为三。叶子数与高度衡量不同事实：平衡树可以用不高的高度容纳很多叶子；单侧树的高度却可能与节点数成正比。

对于 `countLeaves`、`mapTree` 和 `foldTree`：

- 运行时间为 `O(n)`，因为 `n` 个节点各访问一次；
- 直接调用栈使用为 `O(h)`，其中 `h` 是最大高度；
- `mapTree` 重建时还会分配 `O(n)` 个输出节点。

这些分支遍历不是尾递归：子调用返回后，函数仍需处理另一个子节点或组合结果。机械地加入累加器不会消除这些待处理工作。

对于深度有限的领域树，直接定义通常最清楚。如果输入来自不可信来源或可能极深，就应限制并测量高度，或改用自行维护工作栈的迭代遍历。是否需要这些措施取决于预期输入，不必因此预先复杂化所有递归定义。

## 相互递归的类型使用 `and` {#mutual-recursion}

有时两个类型相互包含。F# 用 `and` 连接其声明：

```fsharp
type Expression =
    | Literal of int
    | Let of Binding * Expression
and Binding =
    { Name: string
      Value: Expression }
```

这是一个与 `BookingTree` 无关、可以单独编译的语法示例。只有领域确实存在两个不同概念时，才使用相互递归。单个递归联合更容易遍历，不应只为展示语法而拆分。相互递归函数使用相应的 `let rec ... and ...` 形式。

## 练习 {#exercises}

### 练习 1：从案例推导查询 {#exercise-01}

用结构递归编写 `exists : ('T -> bool) -> BookingTree<'T> -> bool`。先说明每个案例的规则，再写代码。分支实现是否总应访问右子树？解释布尔短路带来的结果。


::: details 参考答案

规则如下：

- `Empty` 不包含匹配值，所以返回 `false`；
- `Leaf value` 返回 `predicate value`；
- `Branch(left, right)` 在任一子树成功时成功。

直接翻译为：

```fsharp
let rec exists predicate tree =
    match tree with
    | Empty -> false
    | Leaf value -> predicate value
    | Branch(left, right) ->
        exists predicate left || exists predicate right
```

F# 的布尔 `||` 会短路。左侧调用返回 `true` 时，右侧调用不会运行。对于纯粹的存在性查询，这是理想行为，而且可能避开树的大部分。最坏情况下——不存在匹配，或匹配位于最后访问的叶子——函数仍会访问每个节点，花费 `O(n)` 时间。

不要把必须执行的副作用藏进 `predicate`，再假设每个叶子都会被访问。该函数只承诺回答是否存在，不承诺遍历全部节点。必须对每个值执行的工作，应使用另一项遍历。

:::

### 练习 2：检验 map 定律 {#exercise-02}

实现 `mapTreeWithFold`，再对 `emptyTree`、`leafTree` 和 `branchTree` 检查恒等律与复合律。解释为什么检查三个示例能增加信心，却不能证明该定律对每棵树都成立。


::: details 参考答案

fold 用相应的重建构造器替换每个构造器：

```fsharp
let mapTreeWithFold mapping =
    foldTree
        Empty
        (mapping >> Leaf)
        (fun left right -> Branch(left, right))

let examples = [ emptyTree; leafTree; branchTree ]
let increment seats = seats + 1
let double seats = seats * 2

let identityHolds =
    examples
    |> List.forall (fun tree -> mapTreeWithFold id tree = tree)

let compositionHolds =
    examples
    |> List.forall (fun tree ->
        mapTreeWithFold (increment >> double) tree
        = (tree |> mapTreeWithFold increment |> mapTreeWithFold double))
```

对三个示例而言，两个值都是 `true`。这能发现几类常见实现错误，但递归类型允许任意规模和结构的树，三个值不可能枚举全部情况。

证明也遵循相同结构。定律对 `Empty` 与 `Leaf` 直接成立。对于 `Branch`，先假设它们对两棵更小子树成立，再证明重建会保持组合结果。这种结构归纳正是结构递归在推理上的对应物。

:::

### 练习 3：用一次 fold 计算一份摘要 {#exercise-03}

定义一个包含 `LeafCount`、`TotalSeats` 和 `MaximumSeats : int option` 的摘要记录。用一次 `foldTree` 遍历计算它。分别给出 `Empty`、`Leaf 2` 与共享分支树的正确摘要，再说明时间复杂度与直接调用栈上界。


::: details 参考答案

空树规则没有最大值；叶子初始化三个字段；分支规则组合已经完成的摘要：

```fsharp
type TreeSummary =
    { LeafCount: int
      TotalSeats: int
      MaximumSeats: int option }

let emptySummary =
    { LeafCount = 0
      TotalSeats = 0
      MaximumSeats = None }

let summarizeLeaf seats =
    { LeafCount = 1
      TotalSeats = seats
      MaximumSeats = Some seats }

let combineSummaries left right =
    let maximum =
        match left.MaximumSeats, right.MaximumSeats with
        | None, other
        | other, None -> other
        | Some leftMax, Some rightMax -> Some(max leftMax rightMax)

    { LeafCount = left.LeafCount + right.LeafCount
      TotalSeats = left.TotalSeats + right.TotalSeats
      MaximumSeats = maximum }

let summarize tree =
    tree
    |> foldTree emptySummary summarizeLeaf combineSummaries
```

预期值如下：

| 树 | `LeafCount` | `TotalSeats` | `MaximumSeats` |
| --- | ---: | ---: | --- |
| `Empty` | 0 | 0 | `None` |
| `Leaf 2` | 1 | 2 | `Some 2` |
| 共享分支 | 3 | 9 | `Some 4` |

fold 会访问每个节点一次，因此时间为 `O(n)`。直接递归实现最多保留从根到当前节点的链以及待处理分支工作，所以调用栈深度为 `O(h)`。它在一次遍历中计算三个字段；编写三个独立 fold 在渐近意义上仍是 `O(n)`，但会访问树三遍。

:::


第 11 章会研究 `mapTree` 等泛型函数是怎样被推断的、泛化在哪里停止，以及操作会引入哪些类型约束。

## 资料来源 {#sources}

- [Microsoft Learn：可区分联合](https://learn.microsoft.com/zh-cn/dotnet/fsharp/language-reference/discriminated-unions)
- [Microsoft Learn：递归函数与 `rec`](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/functions/recursive-functions-the-rec-keyword)
- [Microsoft Learn：函数](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/functions/)
