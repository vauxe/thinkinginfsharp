---
title: "第 29 章练习答案"
description: "推导独立的流式属性，设计合法标识符的生成器与缩减器，并把顺序敏感反例转成持久回归示例。"
translationKey: solutions/ch-29-property-testing
---

# 第 29 章练习答案 {#overview}

这些是参考答案，并非仅有的正确属性或生成器。每项答案都先说明需求，再给出代码；生成值与缩减值始终合法，临时复现数据则与永久回归测试分开。

[返回第 29 章](../part-05/ch-29-property-testing)。

## 练习 1：推导独立属性 {#exercise-01}

### 追加输入不得改写先前决策 {#exercise-01-prefix-stability}

分配器被规定为流式过程：它按顺序处理请求，绝不回头修改早先决策。因此，追加一个正数请求后，原决策列表必须仍是新决策列表的前缀。

```fsharp
let appendingRequestPreservesPriorDecisions
    (sample: AllocationCase)
    (PositiveInt extra)
    =
    let original = SeatAllocation.allocate sample

    let extended =
        AllocationCase.create
            (AllocationCase.capacity sample)
            (AllocationCase.requests sample @ [ extra ])
        |> Result.map SeatAllocation.allocate

    match extended with
    | Error _ -> false
    | Ok allocation ->
        allocation.Decisions
        |> List.take original.Decisions.Length
        |> (=) original.Decisions
```

该属性来自已经声明的流式语义，而不是复刻容量折叠。它可以发现以下实现偏差：先对请求排序、全局优化整批请求，或看到后续输入后重建早先决策。

`PositiveInt` 提供正数追加值，但项目专用生成器也可以把全部输入策略都收进 `AllocationCaseArbitrary`。若在类型推断不清楚的代码中把 `PositiveInt` 转成 `int`，应显式使用它的 `Get` 成员。

应保留容量 5、请求 `[2; 4; 3]` 的具体示例。它清楚记录分配器接受 2、拒绝 4、接受 3，最终容量归零。属性能在大量输入上检查前缀稳定性，但不如这个示例直观。

另一个正确属性是用小型验证器重放决策：每个 `Accepted n` 都必须不超过当时的剩余容量，并减去 `n`；每个 `Rejected n` 都必须超过当时的剩余容量。该模型应独立编写并以策略命名，不能复用生产代码的折叠函数。

## 练习 2：设计生成与缩减 {#exercise-02}

### 从允许字符表构造标识符 {#exercise-02-generator}

让生成器层面无法产生非法字符：

```fsharp
let identifierGenerator =
    let alphabet = [ 'A' .. 'Z' ] @ [ '0' .. '9' ]

    Gen.sized (fun size ->
        gen {
            let! length = Gen.choose(1, max 1 (min 12 (size + 1)))
            let! characters = Gen.elements alphabet |> Gen.listOfLength length
            return System.String(characters |> List.toArray)
        })
```

它始终生成长度 1 到 12 且只含许可字符的值，没有拒绝循环。组装完整案例时，把结果交给标识符智能构造器；若领域规则日后变化，生成器应明显失败。

### 保持非空与合法地缩减 {#exercise-02-shrinker}

简单的标识符缩减器可以先在长度大于一时移除一个字符，再把一个字符替换成字符表中更靠前的成员。它绝不能产生空字符串或字符表之外的字符。

```fsharp
let shrinkIdentifier (value: string) =
    seq {
        if value.Length > 1 then
            for index in 0 .. value.Length - 1 do
                yield value.Remove(index, 1)

        for index in 0 .. value.Length - 1 do
            if value[index] <> 'A' then
                let chars = value.ToCharArray()
                chars[index] <- 'A'
                yield System.String chars
    }
    |> Seq.distinct
```

对于完整分配案例，应每次只改变一个字段，把标识符候选与现有容量、请求候选组合。一个良基的字典序度量是 `(标识符长度, 字符序位和, 请求数, 容量, 请求和)`。每个发出的候选必须严格减小某个较早分量，同时不增加任何更早分量，因此不可能无限循环。

值得使用的分类包括 `single-character-id` 和 `contains-digit`。视风险而定，也可观察最大长度标识符或超额请求。标签描述实际生成分布；若某种案例必须可靠出现，标签不能替代生成器分支。

上面的样例缩减器偏好易读的 `A` 字符，但团队也可以偏好数字或保留必需前缀。“更小”是一项测试策略，并非标识符上的内在次序。

## 练习 3：解释并保存失败 {#exercise-03}

### 贪心分配有意对顺序敏感 {#exercise-03-counterexample}

容量 2、请求 `[1; 2]` 时，分配器接受 1、拒绝 2，共接受 1。把列表反转为 `[2; 1]` 后，它接受 2、拒绝 1，共接受 2。所声称的置换不变性与有序贪心策略矛盾；这个反例没有揭示分配器缺陷。

把该行为保存为明确的示例：

```fsharp
let allocate capacity requests =
    AllocationCase.create capacity requests
    |> Result.map SeatAllocation.allocate

let acceptedTotal allocation =
    allocation.Decisions
    |> List.sumBy (function Accepted seats -> seats | Rejected _ -> 0)

let forward = allocate 2 [ 1; 2 ] |> Result.map acceptedTotal
let reversed = allocate 2 [ 2; 1 ] |> Result.map acceptedTotal

Assert.Equal(Ok 1, forward)
Assert.Equal(Ok 2, reversed)
```

诊断期间应保留原始与缩减参数、直接重放三元组 `(seed, gamma, size)`、FsCheck 版本及相关代码修订。它们让人能在修复或需求裁决前准确复现运行。

永久保留具名示例及其预期总数。不要让种子成为业务契约：生成顺序变化或锁定依赖升级都可能改变其含义。具体输入则始终易懂且稳定。

修正后的属性可以这样表述：请求总数不超过容量时，每项请求都被接受，已接受总数与顺序无关。没有该前提时，只保留守恒与边界等真实不变量。

## 答案回顾 {#solution-review}

- 前缀稳定性源自流式语义，可以发现排序或批量重优化。
- 一个具体的交错示例比一般属性更清楚地解释策略。
- 应从允许字符表构造标识符，而不是筛选任意字符串。
- 缩减器必须保持非空、字符表规则与递减的简洁度度量。
- 分类揭示分布，但不保证覆盖。
- 反序反例推翻的是属性，而不是符合规格的分配器。
- 诊断时保留重放元数据，永久回归保护则保留具体具名示例。
- 应用有依据的前提限制错误的一般主张，或用真实不变量替换它。
