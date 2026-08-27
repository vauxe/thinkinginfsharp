---
title: "第 28 章练习答案"
description: "按风险选择最小测试层，为缺失产品路径编写手写替身，并设计可选 JSON 字段的兼容演进。"
translationKey: solutions/ch-28-testing-boundaries
---

# 第 28 章练习答案 {#overview}

答案先写风险，再选择实际运行哪些组件。扩大运行范围只有在检查新风险时才有价值；增加宿主、网络或 mock 配置不会自动提高可信度。

[返回第 28 章](../part-05/ch-28-testing-boundaries)。

## 练习 1：为三类风险选测试层 {#exercise-01}

### 每项测试只运行必要组件 {#exercise-01-selection}

| 风险 | 最小层 | 真实参与者 | 替代参与者 | 关键断言 |
|---|---|---|---|---|
| 折扣总价错误 | 纯值示例测试 | 折扣/定价函数 | 无 | 完整结果等于一个手写金额值 |
| 库存不足后仍保存 | 端口替身单元测试 | 工作流与纯决策 | 产品查询、时钟、保存 | 错误完全匹配；保存和时钟均未调用 |
| `orderId` 漂移为 `OrderId` | JSON 边界契约测试 | DTO、实际 options、`System.Text.Json` | 无序列化替身 | 解析后的输出含 `orderId` 且不含错误拼写 |

折扣规则不需要工作流或序列化器；加入它们只会增加无关失败来源。保存协议必须运行工作流，但固定产品和时间已经足够。真实数据库不会改变“工作流是否请求保存”这一判断。

字段拼写风险来自序列化器及其配置，因此两者都必须真实运行。测试仍可完全在内存中执行；测试真实边界不等于启动服务器。只有要检查 HTTP content type、数据库列名等其他风险时，才增加更外层测试。

先确认每项测试确实能发现目标风险。可临时改错总价期望、让失败分支执行保存，或改变 naming policy。确认对应测试失败后，再恢复实现。

## 练习 2：写一个不锁定实现的替身测试 {#exercise-02}

### 只记录调用者能观察的端口协议 {#exercise-02-double}

共享答案创建一个返回 `None` 的产品查询，记录传入 SKU，并为时钟和保存设置独立记录：

```fsharp
let request =
    match PlaceOrderCommand.create "ORD-28" "FSP-BOOK" 2 with
    | Ok value -> value
    | Error error -> failwithf "unexpected input: %A" error

let lookups = ResizeArray<string>()
let saved = ResizeArray<PlacedOrder>()
let mutable clockCalls = 0

let ports: OrderPorts =
    { FindProduct =
        fun sku ->
            lookups.Add sku
            None
      GetUtcNow =
        fun () ->
            clockCalls <- clockCalls + 1
            DateTimeOffset.MaxValue
      SaveOrder = saved.Add }

Assert.Equal(
    Error(ProductNotFound "FSP-BOOK"),
    OrderWorkflow.place ports request
)

Assert.True(([ "FSP-BOOK" ] = (lookups |> Seq.toList)))
Assert.Equal(0, clockCalls)
Assert.Empty saved
```

测试检查四项相关行为：工作流用规范化后的 `FSP-BOOK` 查询；返回 `ProductNotFound "FSP-BOOK"`；不读取时间；不保存。后两项共同保证“决策失败后不执行成功路径的副作用”。

它没有断言 `OrderDecision.decide` 的调用次数，也没有知道工作流是管道还是 `match`。实现可以增加纯缓存、重命名 helper 或改变组合形式，只要可观察结果和端口协议不变，测试仍通过。

如果产品查询本身抛异常，本章工作流会让异常传播，而且时钟与保存不会运行。是否把它映射为领域错误是另一项产品决策，应以单独测试驱动，而不要在本测试里用 `try/with` 混入第二个场景。

## 练习 3：设计一次 JSON 契约演进 {#exercise-03}

### 先写兼容矩阵再改 DTO {#exercise-03-matrix}

假设 `note` 真正可选且缺失与 null 都表示“没有备注”。发布前至少覆盖：

| 写入方 | 读取方 | 输入/输出 | 期望 |
|---|---|---|---|
| 旧写入方 | 新读取方 | 没有 `note` | 成功，领域得到无备注 |
| 新写入方 | 新读取方 | `note` 为文本 | 成功并保留文本 |
| 新写入方 | 新读取方 | `note` 为 null | 按已记录策略得到无备注或明确拒绝 |
| 新写入方 | 旧读取方 | 含 `note` | 取决于旧读取方未知字段策略 |
| 新写入方 | 任意读取方 | 输出 JSON | 原字段名和类型完全不变；只增加 `note` |

当前样例对未知成员使用 `Disallow`，所以旧读取方若保持该策略，会拒绝新写入方的 `note`。这意味着“给 JSON 增加可选字段”对该消费关系并不兼容。可选字段只描述新 DTO 的验证，不自动保证旧解析器接受它。

有三种可行选择：

1. 先部署能忽略或识别 `note` 的读取方，再部署写入方。
2. 创建有版本的消息或端点。
3. 允许未知字段，同时接受拼写错误可能被忽略的代价。

选择取决于部署顺序和错误检测需求，不能只改一个记录字段。

输出契约测试应解析 JSON 并检查现有三个字段仍在、`note` 按策略出现或省略；输入测试覆盖上述四种载荷以及错误类型。无需固定属性顺序或空白，除非协议另有规范化要求。

### 大小写宽容是行为变化 {#exercise-03-casing}

启用 `PropertyNameCaseInsensitive` 后，`OrderId`、`ORDERID` 等大小写变体会匹配 `orderId`。正确的 camelCase 输入仍然有效。输入接受集合由此扩大，属于行为兼容性变化，而非二进制签名变化。

扩大接受集合可能帮助迁移，也可能隐藏发送方拼写漂移。用测试确认所需变体可被接受，并检查同一对象中两个仅大小写不同的字段如何处理；若结果依赖顺序或产生歧义，协议应拒绝这种载荷或明确规范。

## 答案复盘 {#solution-review}

- 纯计算、端口协议和 JSON 配置分别需要不同真实参与者。
- 更大测试只有检查额外风险时才值得成本。
- 替身记录外部可观察调用，不记录私有控制流。
- 一个测试保持一个场景；异常映射应由另一项产品决策驱动。
- 新字段是否兼容取决于旧读取方，而不只取决于字段是否“可选”。
- 严格未知字段策略提高拼写检测，却限制前向兼容。
- 大小写宽容扩大输入集合，是应由契约测试记录的行为变化。
