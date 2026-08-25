---
title: "附录 C：集合选择与复杂度"
description: "按求值、更新、查找、顺序、键契约与有条件的复杂度选择 F#/.NET 集合，而不是只看熟悉的名称。"
translationKey: appendices/c-collections
---

# 附录 C：集合选择与复杂度 {#overview}

集合类型是一份行为契约，不只是带尖括号的拼写。先问元素何时产生、谁能更新存储、如何查找、可观察顺序是什么，以及哪个相等或比较契约拥有键；之后才用复杂度区分合理候选。

下面的界限描述 2026-08-25 复核的实现与官方 API 契约。`n` 是集合大小，`k` 是遍历前缀或索引，`m` 是另一输入大小。大 O 隐藏分配、局部性、比较器成本、元素大小、JIT 行为与 I/O。“期望”和“均摊”刻意不等于“最坏情况”。

## 从决策表开始 {#decision-table}

| 需求 | 首选候选 | 原因 | 何时重选 |
|---|---|---|---|
| 小/中型不可变有序数据、从头变换 | `'T list` | 持久化单链形状；模式匹配与前置自然 | 随机索引、反复追加或缓存局部性占主导 |
| 固定大小索引可变缓冲或密集变换 | `'T array` | 连续运行时数组；索引与长度 O(1) | 大小反复变化或旧版本必须仍是值 |
| 一般可枚举/延迟管道 | `seq<'T>` | 适配 `IEnumerable<'T>`，可按需产生 | 必须明确可重复性、生命周期、单次源或物化 |
| 可增长有序可变缓冲 | `ResizeArray<'T>` / `List<T>` | O(1) 索引与均摊 O(1) 尾部添加 | 需要共享修改或持久快照 |
| 不可变排序唯一值 | `Set<'T>` | 持久化二叉树，按比较排序 | 只支持相等的键或哈希查找更合适 |
| 不可变排序键值绑定 | `Map<'Key, 'Value>` | 持久化二叉树，按比较排序 | 热点可变更新或只支持相等的键占主导 |
| 可变键值查找 | `Dictionary<'Key, 'Value>` | 期望接近 O(1) 查找的哈希表 | 确定排序或持久版本更重要 |
| 可变唯一成员关系 | `HashSet<'T>` | 基于哈希的集合运算 | 排序迭代、重复项或持久版本更重要 |

这些只是起点。十个元素的数组可能比列表更清楚；Map 也可能因为所有权比常数因子重要而优于 Dictionary。只有语义合适后，才测量代表性操作。

## 分开五份契约 {#five-contracts}

对任意集合都记录：

1. **产生：** 立即值、延迟枚举、远程查询还是单次流？
2. **更新：** 结构共享的新值、完整复制的新值，还是原地修改？
3. **访问：** 头部、索引、扫描、比较树还是哈希查找？
4. **顺序：** 插入/源顺序、比较顺序、未指定，还是只在某次操作后排序？
5. **身份：** 无键契约、结构相等、泛型比较，还是显式比较器？

`seq<'T>` 只回答“能产生 `IEnumerator<'T>`”。它没有说明枚举是否便宜、可重复、有限、线程安全、纯，或独立于打开的资源。

## 求值与更新汇总 {#evaluation-update}

| 集合 | 求值/存储 | 更新行为 | 旧值仍可用？ |
|---|---|---|---|
| list | 立即不可变链节点 | 前置共享尾部；变换分配结果链 | 是 |
| array | 立即、固定大小连续存储 | 元素设置会修改；改大小需要另一数组 | 元素修改时否 |
| sequence | 由生产者决定；变换常为延迟 | 没有共同存储可更新 | 由源决定 |
| `ResizeArray` | 立即、数组支撑的可调整存储 | 方法修改并可能扩容/复制 | 否 |
| `Map` / `Set` | 立即不可变比较树 | add/remove 返回新树并共享未影响结构 | 是 |
| `Dictionary` / `HashSet` | 立即可变哈希桶/条目 | 方法修改并可能扩容/重哈希 | 否 |

这里的“持久化”表示更新后先前的集合值仍有效；不表示数据能跨进程故障存活或已持久存储。

数组可变，但 `Array.map`、`Array.filter` 与 `Array.sort` 会按各自函数契约返回数组；`Array.sortInPlace` 等明确命名的函数会修改。应阅读精确操作，不要把一条规则套到整个模块。

## 列表：面向头部的持久链 {#lists}

F# list 是不可变单链表。它的实用成本模型直接来自这种形状：

| 操作 | 典型界限 | 原因/条件 |
|---|---|---|
| `head :: tail` | O(1) | 分配一个节点并共享 `tail` |
| `List.head` / `List.tail` | O(1) | 读取首节点；空输入失败 |
| 枚举/map/fold/length | O(n) | 访问每个节点 |
| `List.item k` | O(k)，因此最坏 O(n) | 从头开始行走 |
| `left @ right` / `List.append left right` | O(左侧长度) | 复制左链并复用 `right` |
| reverse | O(n) | 构造反向链 |

反复向增长列表尾部追加一个元素，会产生二次方总遍历。应向累加器头部前置并最终反转、使用能构造目标顺序的折叠，或选择可增长缓冲。

结构共享不等于零分配：前置会分配节点；映射会分配新链；被捕获的值可能让很大的共享尾部一直存活。

## 数组与可增长缓冲 {#arrays-resizearray}

| 操作 | Array | `ResizeArray<'T>` / `List<T>` |
|---|---|---|
| 索引读/写 | O(1) | O(1) |
| 长度/计数 | O(1) | O(1) |
| 完整扫描/map | O(n) | O(n) 扫描 |
| 添加一个尾项 | 需要新建/复制操作，O(n) | 均摊 O(1)，扩容时最坏 O(n) |
| 中部插入/删除 | 在新结构或显式可变方案中复制/移动，O(n) | 移动后缀，O(n) |
| 快照复制 | O(n) | O(n) |

均摊尾部添加表示偶尔扩容会复制现有元素；它不保证每次调用都是 O(1)。可信上界能避免实质扩容时可设置初始容量，但不要按不可信输入声称的大小无限制分配。

密集连续存储通常改善局部性与互操作，也使别名问题重要：两个名称可能指向同一个可变数组或 list 对象。暴露为 `seq<'T>` 的类型仍可能由可变数组支撑，并在两次枚举之间变化。

## 序列：枚举契约 {#sequences}

许多 `Seq` 变换返回延迟可枚举值。创建管道可能为 O(1)，而以后每次枚举才执行工作。`Seq.fold`、`Seq.toList` 或完整 `Seq.length` 等终端操作会消费元素。

```fsharp:line-numbers [ch14-collections-evaluation.fsx]
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
对产生 `n` 个元素的有限序列：

- 完整扫描为 O(n)，另加生产者与回调成本；
- 到达第 `k` 项为 O(k)，除非具体源另有被直接使用的索引器；
- `Seq.take k` 本身延迟，但消费它仍会请求至多 `k` 个值；
- 排序、分组、反转及许多集合式操作必须缓冲实质数据；
- 源可能无限、途中抛错、读取实时状态、执行 I/O，或只允许枚举一次。

`Seq.cache` 会在请求时记忆值，避免再次生产已经缓存的前缀。它也会保留缓存值和源状态；这是所有权决策，不是通用性能开关。真实契约是有界快照时，应一次性用 `Seq.toList` 或 `Seq.toArray` 物化。

不要为了判断是否为空先调用 `Seq.length`，随后再枚举。可以用 `Seq.isEmpty` 之类单遍判断；若同时需要计数与内容且规模有界，则物化。

## Map 与 Set：按比较排序的持久树 {#map-set}

FSharp.Core 把 `Map` 与 `Set` 记录为由 F# 泛型比较排序的不可变二叉树集合。它们的类型携带 `comparison` 约束。

```fsharp:line-numbers [ch14-collections-evaluation.fsx]
let uniqueSeats = [ 3; 1; 3; 2 ] |> Set.ofList

let bookingByCode =
    [ "B2", "first"; "A1", "only"; "B2", "replacement" ] |> Map.ofList

ensureEqual "set removes duplicates and orders" [ 1; 2; 3 ] (Set.toList uniqueSeats)
ensureEqual "later map binding replaces earlier" "replacement" bookingByCode["B2"]

printfn "Ordered collections: set=%A map=%A" (Set.toList uniqueSeats) (Map.toList bookingByCode)
```
| 操作 | `Map` / `Set` 官方界限 | 条件 |
|---|---|---|
| find/tryFind/contains | O(log n) | 树比较路径 |
| add/remove/change | O(log n) | 返回新集合 |
| 枚举/映射值 | O(n) | 按比较顺序；映射键/重建不同 |
| `Map.count` / `Set.count` | 当前 FSharp.Core 文档为 O(n) | 不要假定缓存了计数 |
| 通过普通 `ofList`/`ofArray` 构建 | 官方为 O(n log n) | 反复插入树 |
| filter | 官方为 O(n log n) | 结果树会重建 |

枚举顺序是键/元素比较顺序，不是插入顺序。修改比较契约或参与结构比较的表示，会改变可观察顺序与查找身份。

`comparison` 约束比相等更强。函数、标为 `NoComparison` 的类型与只支持相等的领域键不能直接使用。如果排序不属于领域，带显式相等比较器的哈希集合可能更准确地表达需求。

## Dictionary 与 HashSet：可变哈希身份 {#hash-collections}

.NET 把 Dictionary 键检索记录为非常快、接近 O(1)，其速度取决于哈希质量。.NET 集合复杂度表会区分哈希插入/查找的均摊或期望 O(1) 与最坏 O(n)。

```fsharp:line-numbers [ch14-collections-evaluation.fsx]
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
| 操作 | 期望/均摊 | 最坏情况或限制 |
|---|---|---|
| dictionary 查找/add | O(1) | 碰撞或扩容路径 O(n) |
| hash-set membership/add | O(1) | 碰撞或扩容路径 O(n) |
| count | O(1) | 不说明遍历成本 |
| 枚举 | O(n) | 顺序不是可移植语义契约 |
| dictionary 中 contains value | O(n) | 值不是哈希键 |

键存续于集合期间，必须在集合的 `IEqualityComparer<'T>` 下保持稳定。若相等认为两个键相同，它们的哈希码必须一致。在存储期间修改参与相等/哈希的字段，可能让条目不可达或破坏逻辑契约。

`Dictionary` 与 `HashSet` 没有 F# `comparison` 约束；其 CLR API 使用传入比较器或 `EqualityComparer<'T>.Default`。这种灵活性会把正确性责任从编译期约束移到比较器与键设计。

`HashSet` 明确没有特定顺序。除非另一文档化层建立并测试该顺序，否则不要把当前 `Dictionary` 枚举行为空间暴露成稳定排序或插入顺序 API。顺序是契约时，应在边界显式排序。

普通可变集合不会自动支持并发写。应采用所有权/限制、同步、不可变快照，或原子方法与复合不变量匹配的并发集合。“线程安全的方法”不能证明检查后执行序列是原子的。

## 相等、比较与键 {#key-contracts}

| 集合/操作 | 所需身份契约 |
|---|---|
| list/array/sequence 存储与遍历 | 仅存储/枚举时无 |
| `List.contains`、`Array.distinct`、分组等 | 精确操作需要的相等/哈希 |
| `Map<'K, 'V>` / `Set<'T>` | F# 泛型比较 |
| `Dictionary<'K, 'V>` / `HashSet<'T>` | 相等比较器加兼容且稳定的哈希 |
| 排序 | 比较，或传入比较器/函数 |

不要从显示格式、当前文化、不稳定时间戳、可变字段或有损规范化派生键比较器。字符串要明确选择 ordinal、ordinal-ignore-case、文化感知或领域规范化。持久化或跨进程键还需要独立于内存哈希码的版本化表示。

相等可以正确而比较不可用。第 14 章的 `EmailAddress` 有意采用忽略大小写的相等和 `NoComparison`，因此 Dictionary 自然，而 Map 的拒绝有价值。

## 只有明确声明时，顺序才属于 API {#ordering}

| 源 | 可观察顺序 |
|---|---|
| list/array/`ResizeArray` | 显式修改/重排前的索引/源顺序 |
| sequence | 每次枚举时生产者发出的顺序 |
| `Map`/`Set` | F# 泛型比较顺序 |
| `Dictionary` | 不要把枚举顺序当作可移植领域语义 |
| `HashSet` | 无特定顺序 |
| 排序操作 | 比较器顺序，包括该操作文档化的并列行为 |

确定性输出通常需要最终显式排序，即使内部查找使用哈希。稳定排序是另一承诺：只有所选函数文档说明稳定时，同键元素才保留输入顺序。

## 转换是一道边界，通常也是遍历 {#conversion}

`List.toArray`、`Array.toList`、`Seq.toList`、`Set.ofSeq` 等函数会按目标分配/物化。典型列表/数组快照转换为 O(n)；普通 Map/Set 构建的官方复杂度为 O(n log n)。

转换可能会：

- 强制求值延迟或单次源；
- 快照可变状态；
- 去除重复项；
- 按比较重排；
- 用后来的重复键绑定替换早先绑定；
- 分配另一份完整表示；
- 移动相等/比较策略。

应在真实边界转换一次，而不是在循环或属性中反复转换。命名为什么需要目标契约。

## 精确阅读复杂度声明 {#complexity-rules}

1. 写明操作、集合类型、运行时/FSharp.Core 版本和 `n`。
2. 明确写期望、均摊、平均还是最坏情况。
3. 在实质相关时包含回调、比较器、哈希、生产者、分配与 I/O 成本。
4. 区分管道构造与枚举。
5. 说明更新是修改、完整复制还是结构共享。
6. 包含顺序与重复行为；速度本身不能保持含义。
7. 语义契约匹配后，才对代表性规模与访问模式做基准。

“Dictionary 查找是 O(1)”不完整。“在稳定、分布良好的相等/哈希比较器下期望接近 O(1)；最坏 O(n)；不承诺排序”才可用于决策。

## 常见选择失败 {#common-failures}

- 把列表当作索引表或只追加缓冲。
- 为了显得节省内存选择 `seq`，却反复枚举。
- 缓存无界序列并保留每个已产生值。
- 把可变数组作为 `seq` 返回，并假设类型会建立快照。
- 只因为不可变就选 `Map`，尽管键没有有意义的比较。
- 为速度选择 `Dictionary`，却暴露偶然枚举顺序。
- 修改已经存储的哈希键中参与身份的字段。
- 为了调用熟悉模块函数而反复转换集合。
- 把持久内存结构当作耐久存储。
- 在测量实际数据大小与热点操作前优化渐近成本。

回到[第 14 章](../part-03/ch-14-collections-evaluation)查看可执行求值例，[第 24 章](../part-04/ch-24-concurrency-agents-state)讨论并发下的集合所有权，[第 31 章](../part-05/ch-31-measure-before-optimizing)则建立测量纪律。

## 官方入口 {#official-entry-points}

- [Microsoft Learn：F# 集合类型](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/fsharp-collection-types)
- [Microsoft Learn：F# 列表](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/lists)
- [Microsoft Learn：F# 序列](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/sequences)
- [FSharp.Core 集合命名空间](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-collections.html)
- [FSharp.Core List 模块](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-collections-listmodule.html)
- [FSharp.Core Seq 模块](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-collections-seqmodule.html)
- [FSharp.Core Map 模块](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-collections-mapmodule.html)
- [FSharp.Core Set 模块](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-collections-setmodule.html)
- [.NET 集合与算法复杂度](https://learn.microsoft.com/en-us/dotnet/standard/collections/)
- [.NET `List<T>`](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.list-1?view=net-10.0)
- [.NET `Dictionary<TKey,TValue>`](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2?view=net-10.0)
- [.NET `HashSet<T>`](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.hashset-1?view=net-10.0)
