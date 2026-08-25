---
title: "第 28 章练习答案"
description: "按风险选择最小测试层，为缺失产品路径编写手写替身，并设计可选 JSON 字段的兼容演进。"
translationKey: solutions/ch-28-testing-boundaries
---

# 第 28 章练习答案 {#overview}

答案先写要排除的风险，再选择参与者。更大的运行范围只有在引入新的真实证据时才更有价值；单纯增加宿主、网络或 mock 配置不会自动提高可信度。

[返回第 28 章](../part-05/ch-28-testing-boundaries)。

## 练习 1：为三类风险选测试层 {#exercise-01}

### 让每项测试只拥有必要参与者 {#exercise-01-selection}

| 风险 | 最小层 | 真实参与者 | 替代参与者 | 关键断言 |
|---|---|---|---|---|
| 折扣总价错误 | 纯值示例测试 | 折扣/定价函数 | 无 | 完整结果等于一个手写金额值 |
| 库存不足后仍保存 | 端口替身单元测试 | 工作流与纯决策 | 产品查询、时钟、保存 | 精确错误；保存和时钟均未调用 |
| `orderId` 漂移为 `OrderId` | JSON 边界契约测试 | DTO、实际 options、`System.Text.Json` | 无序列化替身 | 解析后的输出含 `orderId` 且不含错误拼写 |

折扣规则不需要工作流或序列化器；加入它们只会增加无关失败来源。保存协议必须运行工作流，但固定产品和时间已经提供充分控制，真实数据库不会改变“是否请求保存”的证据。

字段拼写风险恰好来自序列化器与配置，所以不能替换它们。测试可以完全在内存中执行；“边界真实”不等于“必须启动服务器”。如果还需证明 HTTP content type 或数据库列名，再为那些不同风险增加更外层测试。

每项测试都应先观察一次可信红灯：临时改错总价期望、让失败分支保存，或改变 naming policy，确认相应测试确实因目标风险失败，再恢复实现。

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

测试证明四件相互关联的事实：工作流用规范化后的 `FSP-BOOK` 查询；返回 `ProductNotFound "FSP-BOOK"`；没有读取时间；没有保存。后两项共同表达“决策失败后不执行成功效果”。

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

有三种诚实选择：先发布能忽略/识别 `note` 的读取方，再发布写入方；创建版本化消息/端点；或者将协议的未知字段策略改为宽容，并承担拼写错误可能被忽略的代价。应根据部署顺序和错误检测需求选择，而不是只改一个记录字段。

输出契约测试应解析 JSON 并检查现有三个字段仍在、`note` 按策略出现或省略；输入测试覆盖上述四种载荷以及错误类型。无需固定属性顺序或空白，除非协议另有规范化要求。

### 大小写宽容是行为变化 {#exercise-03-casing}

把 `PropertyNameCaseInsensitive` 从 `false` 改为 `true` 后，`OrderId`、`ORDERID` 等大小写变体开始匹配 `orderId`；正确 camel-case 输入仍通过。这通常是输入接受集合的扩大，属于行为兼容性变化，而不是二进制签名变化。

扩大接受集合可能帮助迁移，也可能隐藏发送方拼写漂移。增加测试证明所需变体被接受，并检查同一对象中两个仅大小写不同的字段如何处理；若结果依赖顺序或产生歧义，协议应拒绝这种载荷或明确规范。

## 答案复盘 {#solution-review}

- 纯计算、端口协议和 JSON 配置分别需要不同真实参与者。
- 更大测试只有引入新证据时才值得成本。
- 替身记录的是公开效果协议，不是私有控制流。
- 一个测试保持一个场景；异常映射应由另一项产品决策驱动。
- 新字段是否兼容取决于旧读取方，而不只取决于字段是否“可选”。
- 严格未知字段策略提高拼写检测，却限制前向兼容。
- 大小写宽容扩大输入集合，是应由契约测试记录的行为变化。
