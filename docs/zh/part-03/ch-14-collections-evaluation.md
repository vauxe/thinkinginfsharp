---
title: "第 14 章：集合选择与求值模型"
description: "依据数据表示、求值时机、查找语义与转换成本，在列表、数组、序列、映射表、集合和 .NET 哈希集合之间作出选择。"
translationKey: part-03/ch-14-collections-evaluation
---

# 第 14 章：集合选择与求值模型 {#overview}

集合类型不只是围绕同一组元素换一套标点。它决定数据表示、求值时机、可用更新、键的相等规则和预期成本。到处使用 `seq` 会丢掉有用保证；到处使用 `list` 则可能隐藏索引或查找成本。

下面从程序所需操作出发，在 F# `list`、数组、`seq`、`Map` 和 `Set` 中选择。真正需要基于相等的哈希查找时，再使用 .NET `Dictionary` 或 `HashSet`。

## 从主要操作开始 {#decision-first}

先问消费者最常做什么：

1. 从头部分解一段不大的不可变序列；
2. 按索引读取或更新一个固定大小的数据块；
3. 只请求一个可能很大的生成器所产生的部分结果；
4. 按有序键查找不可变值；
5. 维护唯一且有序的元素；
6. 用自定义比较器执行基于相等的可变查找。

这些答案指向不同表示。“它包含多个值”并不足以作出选择。

## 五种核心集合形式 {#five-shapes}

### 列表：从头处理的不可变结构 {#list}

F# 列表是不可变单向链式结构。`head :: tail` 可以常数时间构造，也符合第 4～6 章介绍的递归结构。`List.map` 与 `List.filter` 会立即遍历输入并分配结果列表。

对于一批不大、已经到手、会被顺序变换或从头分解的数据，列表是很好的默认选择。它不适合反复索引：到达第 *i* 项必须走过此前节点。反复向尾部追加也不符合其结构；应考虑前插后反转、折叠或其他构建方式。

### 数组：固定大小与索引存储 {#array}

数组是固定大小、从零开始的 .NET 数组，其元素占据连续存储。按索引读取和替换元素都是常数时间。绑定可以保持不可变，而它引用的对象仍被修改：

```fsharp
let seats = [| false; false; false |]
seats[1] <- true
```

需要频繁按索引访问的固定大小数据、数值计算，以及直接使用数组的 API 都适合数组。`Array.map` 会立即返回新数组，而不会修改输入。切片会创建副本。数组复制是浅复制，因此引用类型元素仍指向相同底层对象。

示例把列表与数组行为并排展示：

```fsharp:line-numbers
let source = [ 1; 2; 3 ]
let doubledList = source |> List.map ((*) 2)
let doubledArray = source |> List.toArray |> Array.map ((*) 2)
doubledArray[0] <- 20

ensureEqual "list stays immutable" [ 2; 4; 6 ] doubledList
ensureEqual "array element changes" [| 20; 4; 6 |] doubledArray
ensureEqual "source stays unchanged" [ 1; 2; 3 ] source
printfn "Eager: list=%A array=%A source=%A" doubledList doubledArray source
```
两种表示都不具有普遍“更快”的地位。主要访问模式、分配特征、元素类型与实测工作负载才决定结果。

### 序列：规定如何枚举，不代表数据已存储 {#sequence}

`seq<'T>` 是 `System.Collections.Generic.IEnumerable<'T>` 的类型缩写。它描述消费者如何请求元素，却不表示全部元素已经存储，也不保证来源纯净、枚举便宜，甚至不保证重复遍历观察到相同外部状态。

很多值都能被视为序列：列表、数组、映射表、集合，以及大多数 .NET 可枚举集合。因此，接收 `seq<'T>` 可以让只读消费者适用于很多来源。它提供的保证也少于数组或列表：一般情况下没有常数时间计数、索引、快照或可重新遍历的承诺。

当按需生产很重要、消费者可能提前停止，或 API 自然接收 `IEnumerable<'T>` 时，使用序列。不要只为追求最大抽象程度就使用它；应要求实现与调用方真正需要的保证。

### Map 与 Set：按比较顺序进行不可变查找 {#map-and-set}

`Map<'Key,'Value>` 为每个键存储一个值；`Set<'T>` 存储唯一元素。二者都是基于树的不可变集合。添加或删除会返回新集合，旧值仍可使用。

它们的决定性约束是排序：

```fsharp
Map<'Key, 'Value>  // 'Key : comparison
Set<'T>            // 'T : comparison
```

在这些树实现中，查找、插入与成员判断相对于集合大小都是对数时间。枚举遵循 F# 泛型比较顺序，而不是插入顺序。同一键的后续绑定会替换此前映射；比较结果相同的集合元素会合并为一项。

当不可变查找与确定的键排序遍历都有用时，选择 `Map`。当元素有意义且稳定的比较，并需要不可变成员判断、去重与集合代数时，选择 `Set`。

## 求值时机可以被观察 {#evaluation}

创建并变换列表与数组通常会立即遍历。很多 `Seq` 生产器和变换则把工作推迟到枚举时。因此，“惰性”不仅影响性能，也决定异常、状态读取、I/O 与其他副作用何时发生。

### 序列表达式定义生产器 {#sequence-expression}

序列表达式用 `seq { ... }` 描述如何产出元素：

```fsharp
let candidateSeatCounts maximum =
    seq {
        for seats in 1..maximum do
            if seats % 2 = 1 then
                yield seats
    }
```

调用 `candidateSeatCounts 1_000_000` 会创建序列值，而不会立即建立一百万项候选值。`Seq.truncate 3 >> Seq.toList` 这样的消费者可以只请求一个前缀。`yield!` 可以贡献内部序列的全部元素。

表达式主体仍是可执行代码，不是静态数据。其中的副作用会在元素被请求时发生。

### 重复枚举可能重复生产 {#repeated-enumeration}

示例用计数器让求值过程可见：

```fsharp:line-numbers
let mutable pulls = 0

let delayedSquares =
    seq {
        for value in 1..3 do
            pulls <- pulls + 1
            yield value * value
    }

ensureEqual "deferred before enumeration" 0 pulls
printfn "Deferred before enumeration: pulls=%d" pulls

let firstPass = delayedSquares |> Seq.toList
ensureEqual "first values" [ 1; 4; 9 ] firstPass
ensureEqual "first pass count" 3 pulls
printfn "First enumeration: values=%A pulls=%d" firstPass pulls

let secondPass = delayedSquares |> Seq.toList
ensureEqual "second values" firstPass secondPass
ensureEqual "second pass repeats production" 6 pulls
printfn "Second enumeration: values=%A pulls=%d" secondPass pulls
```
构造 `delayedSquares` 后，计数器仍为零。第一次 `Seq.toList` 拉取三项；第二次会为这个序列表达式开始新枚举，并让主体再运行三次。

这项观察并不表示每个 `IEnumerable<'T>` 都可以安全地重新遍历。具体来源控制其枚举器：它可能查询变化中的状态、包装资源、按约定只能使用一次，或者在再次遍历时抛出异常。`seq<'T>` 类型本身不承诺这些行为。

### 运算决定请求多少数据 {#operation-timing}

很多 `Seq` 变换——例如 `map`、`filter` 与 `choose`——会产生另一个延迟序列。`toList`、`toArray`、`fold` 与迭代等消费者会请求元素。搜索与前缀操作可以提前停止。排序和分组必须检查足够输入来组织结果，通常会先缓冲数据才能产生有用输出。

不要只根据 `Seq.` 前缀推断求值方式。应阅读运算说明，并找出最终请求数据的消费者。无界序列可以交给 `Seq.truncate 10`，却不能交给 `Seq.toList` 或完整排序。

### 缓存结果，或保存完整快照 {#cache-or-materialize}

`Seq.cache` 会按需计算元素，并为后续枚举记住它们：

```fsharp:line-numbers
let mutable cachedPulls = 0

let cachedSquares =
    seq {
        for value in 1..3 do
            cachedPulls <- cachedPulls + 1
            yield value * value
    }
    |> Seq.cache

let cachedFirst = cachedSquares |> Seq.toList
let cachedSecond = cachedSquares |> Seq.toList

ensureEqual "cached values" cachedFirst cachedSecond
ensureEqual "cached production count" 3 cachedPulls
printfn "Cached enumerations: first=%A second=%A pulls=%d" cachedFirst cachedSecond cachedPulls
```
当一项延迟计算必须重放，并且保留已产生元素可以接受时，缓存很合适。它不是普遍优化：缓存消耗内存、保留此前观察而不是重新取得最新值，并可能随很长或无限来源无界增长。

当程序需要“现在保存全部值”时，用 `Seq.toList` 或 `Seq.toArray` 明确表达。完整快照有清楚的完成时间，之后可以反复读取；代价是一次完整遍历和全部元素的存储。

## 转换会改变行为与成本 {#conversions}

转换可能分配内存、枚举数据、复制引用、改变更新规则、丢弃重复项或加入排序。使用转换时应明确这些变化，并有意选择位置：

- `List.toArray` 分配索引存储，并复制元素值；
- `Array.toList` 分配列表节点，并捕获数组当时的元素值；
- `Seq.toList` 与 `Seq.toArray` 会立即枚举并保存全部产出元素；
- 把列表或数组视作 `seq` 并不会创建独立不可变快照；
- `Set.ofSeq` 会枚举并移除比较结果相同的重复项；
- `Map.ofSeq` 会枚举键值对，并为每个比较结果相同的键保留一项绑定。

这些复制都是浅复制。若元素是指向可变对象的引用，两个集合仍可能指向同一个对象。示例的数组到列表转换证明的是集合槽位彼此独立，而不是深复制：

```fsharp:line-numbers
let mutableArray = [| 1; 2; 3 |]
let listSnapshot = mutableArray |> Array.toList
mutableArray[0] <- 99

ensureEqual "list is an independent snapshot" [ 1; 2; 3 ] listSnapshot
printfn "Conversion snapshot: array=%A list=%A" mutableArray listSnapshot
```
不要只为调用熟悉的模块函数而在 `list`、数组和 `seq` 之间反复转换。应保留适合工作流的表示，或只在一个明确位置转换一次。

## 有序键与哈希键回答不同问题 {#lookup-semantics}

脚本中的有序集合直接暴露比较顺序：

```fsharp:line-numbers
let uniqueSeats = [ 3; 1; 3; 2 ] |> Set.ofList

let bookingByCode =
    [ "B2", "first"; "A1", "only"; "B2", "replacement" ] |> Map.ofList

ensureEqual "set removes duplicates and orders" [ 1; 2; 3 ] (Set.toList uniqueSeats)
ensureEqual "later map binding replaces earlier" "replacement" bookingByCode["B2"]

printfn "Ordered collections: set=%A map=%A" (Set.toList uniqueSeats) (Map.toList bookingByCode)
```
`Map` 与 `Set` 需要全序关系来导航其树。因此，它们的类型参数带有 `comparison`，而非只有 `equality`。值作为键或元素期间，这项顺序还必须保持稳定。

### 哈希集合需要相等与相容哈希码 {#hash-collections}

.NET `Dictionary<'Key,'Value>` 与 `HashSet<'T>` 通过 `IEqualityComparer` 而非全序关系来组织值。相等值必须产生相同哈希码；不相等值可能发生碰撞，此时再由相等判断区分。值作为键存储期间，影响相等或哈希的可变状态不得改变。

脚本定义了一个带 `[<NoComparison>]`、只支持相等的键：

```fsharp:line-numbers
[<CustomEquality; NoComparison>]
type EmailAddress =
    { Value: string }

    override this.Equals(other: obj) =
        match other with
        | :? EmailAddress as candidate -> StringComparer.OrdinalIgnoreCase.Equals(this.Value, candidate.Value)
        | _ -> false

    override this.GetHashCode() =
        StringComparer.OrdinalIgnoreCase.GetHashCode(this.Value)

let recipients = Dictionary<EmailAddress, string>()
recipients[{ Value = "lin@example.com" }] <- "first"
recipients[{ Value = "LIN@example.com" }] <- "second"

ensureEqual "hash equality replaces value" 1 recipients.Count
ensureEqual "case-insensitive lookup" "second" recipients[{ Value = "Lin@Example.com" }]
printfn "Hash dictionary: count=%d lookup=%s" recipients.Count recipients[{ Value = "Lin@Example.com" }]
```
字典会接受该键，因为它的相等与哈希语义已经足够。尝试使用 `Map<EmailAddress,string>` 会产生 FS0001：该类型明确不支持 `comparison` 约束。这是真实能力差异，不只是性能选择。

不区分大小写等相等规则可能只适用于特定场景。此时，向 `Dictionary` 或 `HashSet` 提供 `IEqualityComparer`，通常比改变领域类型的全局相等更合适。脚本把规则嵌入类型，只为展示“仅支持相等”的限制。

### 先选择所需行为，再比较复杂度 {#hash-or-tree}

F# `Map` 与 `Set` 不可变且有序，树操作是对数时间。.NET `Dictionary` 与 `HashSet` 可变；哈希分布良好时，它们提供接近常数时间的查找。后两者不保证枚举结果有序。

首先根据所需语义选择：

- 需要不可变更新与按键顺序遍历：`Map` 或 `Set`；
- 需要自定义相等、不需要排序且可接受受控局部可变：`Dictionary` 或 `HashSet`；
- 偶尔需要从哈希集合产生排序报告：生成输出时对投影明确排序；
- 需要带自定义相等的持久数据：考虑 .NET 生态中的不可变哈希集合，并把其比较器纳入设计。

然后再对代表性工作负载做基准。大 O 记号没有涵盖集合大小、分配、缓存局部性、比较器成本或并发。

## 紧凑决策表 {#decision-table}

| 主要需求 | 首选起点 | 重要检查 |
|---|---|---|
| 从头处理并模式匹配不可变数据 | `list<'T>` | 反复索引或向尾部追加意味着可能需要其他表示 |
| 固定大小索引数据或基于数组的 .NET 互操作 | `'T array` | 元素可变；复制为浅复制 |
| 按需生产或提前终止 | `seq<'T>` | 枚举时机、可重复性、生命周期与缓冲 |
| 带确定排序遍历的不可变键查找 | `Map<'K,'V>` | 键需要稳定的 F# 比较 |
| 不可变唯一性与有序集合代数 | `Set<'T>` | 元素需要稳定的 F# 比较 |
| 带自定义相等的可变查找 | `Dictionary` / `HashSet` | 相等与哈希码必须一致；不保证排序顺序 |

这张表是起点，并不禁止转换。程序可以接收 `seq`，验证后只枚举一次并保存为数组，再公开不可变结果。每次转换都应有明确理由。

## 练习 {#exercises}

### 练习 1：根据工作负载选择 {#exercise-01}

为每种情况选择一个起始集合并说明理由：

1. 通过头/尾递归处理的一批不大的不可变命令；
2. 按数值索引反复更新和读取的固定座位占用表；
3. 只需前十项有效结果的候选分配生成器；
4. 还必须按确认码顺序产生报告的不可变预约查找表；
5. 不需要排序、按大小写不敏感规则判断成员的可变参与者邮箱集合。

说明你会在输入或输出阶段进行哪些转换。

### 练习 2：预测请求量与缓存 {#exercise-02}

不要运行以下代码，预测每次生成完整结果后的 `reads`：

```fsharp
let mutable reads = 0

let values =
    seq {
        for value in 1..3 do
            reads <- reads + 1
            yield value * 2
    }

let firstTwo = values |> Seq.take 2 |> Seq.toList
let all = values |> Seq.toList
```

然后插入 `let cached = values |> Seq.cache`，改为消费 `cached`，并再次预测。说明调用代码应暴露哪种含义：新鲜枚举、缓存重放，还是完整快照。

### 练习 3：顺序与相等 {#exercise-03}

某领域键支持不区分大小写的相等与哈希，并标有 `[<NoComparison>]`。解释它为何能用于 `Dictionary` 或 `HashSet`，却不能用于 `Map` 或 `Set`。再设计一种偶尔生成字母顺序报告的方法，不改变键的类型定义，并说明相等与哈希码必须遵守的规则。

[查看本章练习答案](../solutions/ch-14-collections-evaluation)。

第 15 章将介绍活动模式：匹配抽象应揭示领域分类，同时不能隐藏昂贵计算或失败。

## 资料来源 {#sources}

- [Microsoft Learn：F# 集合类型](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/fsharp-collection-types)
- [Microsoft Learn：列表](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/lists)
- [Microsoft Learn：数组](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/arrays)
- [Microsoft Learn：序列](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/sequences)
- [FSharp.Core：集合命名空间](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-collections.html)
- [FSharp.Core：Map 模块](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-collections-mapmodule.html)
- [FSharp.Core：Set 模块](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-collections-setmodule.html)
- [Microsoft Learn：`Dictionary<TKey,TValue>`](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2?view=net-10.0)
- [Microsoft Learn：`HashSet<T>`](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.hashset-1?view=net-10.0)
