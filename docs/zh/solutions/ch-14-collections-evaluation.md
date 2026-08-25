---
title: "第 14 章练习答案"
description: "根据工作负载选择集合、精确计算延迟请求量，并区分有序键与基于相等的哈希键。"
translationKey: solutions/ch-14-collections-evaluation
---

# 第 14 章练习答案 {#overview}

说明主要操作、所需语义与转换边界。只有类型名称而没有这套推理，仍只是猜测。

[返回第 14 章](../part-03/ch-14-collections-evaluation)。

## 练习 1：根据工作负载选择 {#exercise-01}

### 1. 通过头/尾处理不可变命令 {#exercise-01-list}

从 `Command list` 开始。这批数据不大、已经到手、不可变，并按列表的递归形状消费。前插与头/尾匹配都符合该表示。

若输入以 `seq<Command>` 到达，就在验证大小限制后物化一次：

```fsharp
let commands = incoming |> Seq.toList
```

这声明处理过程使用稳定快照。在每次列表操作之间又转换回 `seq` 只会浪费工作。

### 2. 按索引表示座位占用 {#exercise-01-array}

从 `bool array` 或信息更丰富的状态数组开始。活动的座位范围固定，按索引读取和局部更新是主要操作：

```fsharp
let occupied = Array.create capacity false
occupied[seatIndex] <- true
```

把这项可变限制在狭窄所有者内部。在向外边界，根据 API 契约返回数组副本、不可变摘要或领域事件；暴露工作数组会让调用方修改内部状态。

### 3. 前十项候选分配 {#exercise-01-sequence}

从 `seq<Allocation>` 开始，因为生产量可能很大，消费者又有意提前停止：

```fsharp
let selected =
    generateCandidates request
    |> Seq.filter isValid
    |> Seq.truncate 10
    |> Seq.toList
```

最终列表是那批很小的已接受快照。当少于十项候选属于正常结果时，`Seq.truncate` 比 `Seq.take` 更合适，因为来源过短会让 `take` 失败。

### 4. 不可变查找与有序报告 {#exercise-01-map}

若 `ConfirmationCode` 具有稳定比较语义，就从 `Map<ConfirmationCode, Booking>` 开始。`Map.tryFind` 提供不可变查找，`Map.toList` 无需另行排序便按键的比较顺序产生结果。

若业务顺序不同于类型的泛型比较——例如按确认时间排序——就显式存储该信息，并按业务键排序或建立索引。不要把泛型结构顺序假装成它并不具备的领域含义。

### 5. 大小写不敏感的可变邮箱成员判断 {#exercise-01-hashset}

从 .NET `HashSet<string>` 开始，并显式提供相等规则：

```fsharp
open System
open System.Collections.Generic

let attendees = HashSet<string>(StringComparer.OrdinalIgnoreCase)
attendees.Add("Lin@example.com") |> ignore
```

这里不需要全序关系。若只在输出时需要字母顺序报告，就投影为字符串并在那里排序。若集合要跨越所有权边界，应复制它或暴露只读结果，而不是共享可变状态。

## 练习 2：预测请求量与缓存 {#exercise-02}

定义 `values` 后，`reads` 立即值为 `0`：还没有消费者请求元素。

不使用缓存时：

1. `Seq.take 2 |> Seq.toList` 请求两项，所以 `reads = 2`，`firstTwo = [ 2; 4 ]`；
2. `values |> Seq.toList` 开始另一次枚举并请求全部三项，所以 `reads = 5`，`all = [ 2; 4; 6 ]`。

第二次遍历不会从此前两项之后恢复。它向这个序列表达式请求新枚举器，生产会重新开始。

使用全新计数器和缓存序列时：

```fsharp
let cached = values |> Seq.cache
let firstTwo = cached |> Seq.take 2 |> Seq.toList
let all = cached |> Seq.toList
```

执行 `firstTwo` 后，`reads = 2`。执行 `all` 时，前两项来自缓存，只有第三项重新生产，因此最终计数为 `3`。

应有意选择暴露的含义：

- **新鲜枚举：** 当来源便宜、纯净、可重新遍历且需要当前观察时，保留未缓存序列；
- **缓存重放：** 当既需要延迟消费前缀，又需要重放相同已产生值时，使用 `Seq.cache`；
- **完整快照：** 当全部工作应只完成一次，且后续遍历必须可预测时，在边界使用 `Seq.toList` 或 `Seq.toArray`。

对于带效果或由资源支持的来源，“新鲜枚举”需要显式来源契约。`seq<'T>` 类型本身不足以证明这一点。

## 练习 3：顺序与相等 {#exercise-03}

`Map<'Key,'Value>` 与 `Set<'T>` 需要导航有序树，因此其键或元素必须满足 F# `comparison`。`[<NoComparison>]` 显式阻止该约束，编译器会拒绝有序集合。

`Dictionary<'Key,'Value>` 与 `HashSet<'T>` 改为使用 `IEqualityComparer`，或使用类型的默认相等与哈希实现。它们需要确定桶位置，再判断候选项是否相等；并不需要判断一个值是否小于另一个值。

对于上下文特定的大小写不敏感邮箱集合，优先在集合边界提供比较器：

```fsharp
let emails =
    System.Collections.Generic.HashSet<string>(
        System.StringComparer.OrdinalIgnoreCase
    )
```

偶尔需要排序报告时，可以投影出可排序表示并显式排序：

```fsharp
let report =
    emails
    |> Seq.sortWith (fun left right ->
        System.StringComparer.OrdinalIgnoreCase.Compare(left, right))
    |> Seq.toList
```

这项排序会为报告物化排序工作，却不会把哈希集合变成有序集合。若领域键是受保护且只支持相等的类型，应先投影其规范化显示值再排序，而不是仅为满足 `Map` 就添加没有意义的比较。

必须遵守的哈希规则是单向的：

```text
若 comparer.Equals(left, right)，则 comparer.GetHashCode(left) = comparer.GetHashCode(right)
```

不相等值可以具有相同哈希码。碰撞可能降低性能，但相等仍会区分这些值。绝不能把哈希码本身作为身份证明或排序键。

## 应该注意什么 {#what-to-notice}

- **表示跟随反复执行的操作：** 一次罕见调用不应决定整个数据形状。
- **物化传达含义：** 它常常是有用的快照边界，并不自动意味着函数式风格失败。
- **缓存是增量式的：** 已经产生的前缀会被重放，后续遍历还能继续生产剩余部分。
- **比较与哈希是不同契约：** 有序树需要稳定全序关系；哈希表需要相容的相等与哈希码。
- **可变需要所有权：** 只要调用方无法意外修改，数组与 .NET 哈希集合就是有效的局部工具。
