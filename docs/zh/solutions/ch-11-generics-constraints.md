---
title: "第 11 章练习答案"
description: "推断泛型签名、按意图修复值限制，并跨边界保留度量量纲。"
translationKey: solutions/ch-11-generics-constraints
---

# 第 11 章练习答案 {#overview}

不要根据定义的名字听起来有多抽象来猜它是否泛型。追踪每项操作，再只保留该操作造成的一致性与能力要求。

[返回第 11 章](../part-02/ch-11-generics-constraints)。

## 练习 1：推断泛型程度与约束 {#exercise-01}

最一般的签名如下：

```fsharp
pair : 'Left -> 'Right -> 'Left * 'Right

contains : 'T -> 'T list -> bool
    when 'T : equality

orderedPair : 'T -> 'T -> 'T * 'T
    when 'T : comparison

wrap : 'T -> Envelope<'T>
```

`pair` 把值放入不同元组位置，所以两种类型不必相同，也未使用任何能力。`contains` 必须用 F# 相等比较候选值与列表元素，因此元素类型必须一致，并加入 `'T : equality`。`orderedPair` 使用 `<=`，所以两个输入共享一种支持比较的类型。`wrap` 只存储值，构造本身不要求相等或比较。

`pair` 的任一位置都能接收函数，`wrap` 也能构造 `Envelope<('A -> 'B)>`。`contains` 不能用 F# 泛型相等搜索函数列表，`orderedPair` 也不能给函数排序。`Envelope` *能够包装*函数，并不表示这种载荷的 `Envelope` 随后可以使用自动生成的结构相等。

## 练习 2：修复两项值限制 {#exercise-02}

若意图是一个共享数组和一种确定元素类型，就专门化绑定：

```fsharp
let bookingBuckets: BookingRequest list array =
    Array.create 2 []
```

右侧只在绑定初始化时运行一次。所有调用方都看到同一个双槽数组，因此必须明确谁能修改它，以及并发访问如何同步。

若意图是每次调用得到新数组，并让每次调用分别推断元素类型，就把构造变成函数：

```fsharp
let makeBuckets () =
    Array.create 2 []

let bookingBuckets: BookingRequest list array = makeBuckets ()
let labelBuckets: string list array = makeBuckets ()
```

函数主体每次调用都会运行，因此数组彼此不同。`unit` 实参不是无意义装饰，它公开了请求创建新值这一事件。

对于泛型变换，应把数据变成函数参数：

```fsharp
let keepAll values =
    List.filter (fun _ -> true) values
```

函数定义初始化一次，筛选则在每次调用时执行。推断签名是 `'T list -> 'T list`。`let keepAll = id` 对不可变列表也能满足“保留全部值”的可观察结果，但它绕过了部分应用修复的演示，而且可能具有不同的共享/分配行为。

三者不是可互换的编译器技巧：类型标注产生一个共享的确定类型值；`()` 表示每次重新构造；数据参数则定义可复用变换。

## 练习 3：跨边界保留量纲 {#exercise-03}

直接的带度量实现如下：

```fsharp
[<Measure>]
type seat

[<Measure>]
type minute

let throughput
    (processed: float<seat>)
    (elapsed: float<minute>)
    : float<seat/minute> =
    processed / elapsed

let seatsFromValidatedInt raw : int<seat> =
    LanguagePrimitives.Int32WithMeasure raw

// 仅用于诊断：度量不同，所以报告 FS0001。
let invalid = 2<seat> + 3<minute>
```

`throughput` 的返回标注并非必需，因为除法能够推断它，但该标注明确记录了边界。`seatsFromValidatedInt` 假定验证已经发生；诚实的名称能防止读者误把附着度量当成验证。

运行时以及常规序列化后，只剩底层 `float` 或 `int`。接收端必须先验证数值含义，再重新附加预期度量。JSON 数字本身不会记录生产者表达的是座位还是分钟。

度量也不能强制经过的时间不为零、座位数为正或请求座位不超过剩余容量。这些是值级不变量。除零和带负数的度量字面量仍然可能存在，除非验证或受保护领域类型拒绝它们。

## 应该注意什么 {#what-to-notice}

- **单纯存储不会加入结构约束：** 后续执行的操作决定相等或比较要求。
- **绑定生命周期是 FS0030 修复的一部分：** 一个带标注数组与接收 unit 的工厂语义不同。
- **函数值揭示组成规则：** 它们能被泛型存储，却不能交给泛型相等或比较。
- **度量推断追随算术：** 除法无需 SRTP 样板就会构造商的度量。
- **擦除带来边界责任：** 反序列化数字、验证含义，然后恢复度量。
