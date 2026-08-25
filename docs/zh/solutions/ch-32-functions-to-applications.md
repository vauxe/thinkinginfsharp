---
title: "第 32 章练习答案"
description: "推导狭窄的发货端口与所有权，设计有界的可观察信号，并依据具体生命周期需求选择应用宿主。"
translationKey: solutions/ch-32-functions-to-applications
---

# 第 32 章练习答案 {#overview}

这些答案把领域策略保留在纯函数中，并让进程职责在其边缘清晰可见。具体名称可以不同；重要性质是能力狭窄、取消与故障含义显式、每个资源只有一个所有者，以及可观察字段的基数受控。

[返回第 32 章](../part-05/ch-32-functions-to-applications)。

## 练习 1：推导端口与所有权 {#exercise-01}

### 从事实和提交开始 {#exercise-01-ports}

假设工作器接收经过验证的 `Order`，而库存由经过验证的 `Sku` 标识。最小的第一版边界可以是：

```fsharp
open System.Threading
open System.Threading.Tasks

type DispatchPorts =
    { LoadInventory: Sku -> CancellationToken -> Task<VersionedInventory>
      CommitDispatch:
        InventoryVersion ->
            Dispatch -> CancellationToken -> Task<Result<unit, CommitError>> }
```

`VersionedInventory` 为领域 `Inventory` 增加一个存储版本。该版本不是发货规则；它让提交能够拒绝过期读取。没有它，分开的加载与提交调用无法防止两个工作器消耗同一份库存。

编排具有如下形状：

```fsharp
task {
    cancellationToken.ThrowIfCancellationRequested()
    let sku = Order.sku order
    let! current = ports.LoadInventory sku cancellationToken

    match decideDispatch current.Inventory order with
    | Error domainError -> return Error(DomainRejected domainError)
    | Ok dispatch ->
        let! committed =
            ports.CommitDispatch current.Version dispatch cancellationToken

        return committed |> Result.map (fun () -> dispatch) |> Result.mapError CommitRejected
}
```

同一个调用者令牌到达两个副作用。预先取消应该避开第一次调用。任一适配器抛出的取消仍然是取消；非预期数据库异常仍是故障任务。`DispatchError` 仍是预期业务拒绝。`VersionConflict` 等 `CommitError` 是持久化/并发结果，不应伪装成领域规则。

该模型不会自动重试版本冲突。只有当操作有明确的重试上限、订单身份稳定且提交幂等时，调用者才能重新加载并再次决策。库存变化后继续复用先前的领域结果是不正确的。

对于所有权，应区分长生命周期客户端与单次操作会话：

- 组合根创建数据库客户端或连接池，并把所有权转移给进程/应用所有者；
- 所有者在关闭时先停止新工作、排空未完成调用，然后释放该客户端；
- 适配器为一次操作创建会话或事务，并在操作内部用 `use` 或 `use!` 释放；
- 纯函数永远看不到这两种资源；
- 如果调用者提供共享客户端但不转移所有权，应用就不得释放它。

如果加载与提交必须共享同一个数据库事务，那么上面展示的两次调用端口并不足够。重新设计适配器边界，让加载、纯决策和条件提交在一个拥有的事务内执行；或者使用存储的比较并交换设施。不要仅仅因为两个调用相邻，就暗示它们具有原子性。

表达这些规则完全不需要容器。构造函数/函数参数暴露依赖，`use` 暴露局部所有权。容器日后可以自动化长生命周期注册和作用域，而无需改变领域工作流。

## 练习 2：设计三种可观察信号 {#exercise-02}

### 让每种信号只承担一种工作 {#exercise-02-signals}

一种连贯设计是：

| 信号 | 名称 | 字段/标签 | 终结结果 |
|---|---|---|---|
| 结构化日志 | `dispatch.attempt` | `outcome`、`orderId`、`sku`、`quantity`、`detail` | accepted、rejected、conflicted、canceled、faulted |
| 计数器 | `dispatch.attempts` | `outcome`，以及可选的有界 `channel` | 同一套有界结果词汇 |
| 活动 | `dispatch.place` | `dispatch.outcome`、`order.id`、`inventory.sku`；按策略设置状态和异常元数据 | 在每条被采样路径上停止 |

`outcome` 是有界的，因为应用定义了五个合法值。如果含义稳定，`web`、`batch`、`manual` 之类的小型枚举渠道也可以有界。`orderId` 和通常的 `sku` 都是高基数；它们绝不能成为指标标签。在访问、保留、采样和隐私策略允许时，它们可以出现在日志或追踪中。

不应发出客户姓名、地址、自由文本备注、身份验证令牌、连接字符串和原始负载。当另一个系统可以把订单 ID 解析到个人时，即使订单 ID 也可能敏感。策略要求时，应使用脱敏或不可逆关联值。

结构化事件应保留类型化字段，而不是插值成一句话。使用 `ILogger` 时，应采用稳定的消息模板和事件 ID，使提供程序能保留这些属性。根据运维处置选择级别：普通缺货拒绝可以是信息，而非预期适配器异常则是错误。

在每次终结尝试后恰好递增一次计数器。计数器报告发生次数；收集器推导总量或速率。如果持续时间很重要，应增加带有明确时间单位的直方图，而不是把平均值编码进计数器。告警属于收集/后端配置，而不是领域函数。

围绕应用编排启动活动，并在 `finally` 中释放它。把 `null` 活动视为正常情况。在活动上放置有界结果，并一致使用状态：预期拒绝可以在协议层成功完成，而非预期异常是错误。单独记录取消，不要把它改写成故障。

本地 `MeterListener` 证明进程发布了一次具有预期值和标签的具名测量。本地 `ActivityListener` 证明被采样活动已启动、添加标签并停止。捕获日志回调证明结构化记录已经产生。

这些监听器无法证明聚合、采样策略、传播标头、批处理、导出、身份验证、后端摄取、保留、仪表板或告警。应为真实 OpenTelemetry/提供程序管线增加集成或预发布环境冒烟测试，并按其运维重要性增加后端查询或健康信号。

## 练习 3：选择宿主层级 {#exercise-03}

### 让生命周期需求选择工具 {#exercise-03-hosts}

对于导入单个文件的命令，应使用显式构造。它只有一个有界操作、自然的 `use` 作用域、简单的参数/配置解析和一个退出码。增加服务容器与托管服务生命周期并不会移除有意义的复杂度。如果需要中断，取消可以来自控制台信号令牌。

对于包含三个后台消费者的进程，应使用 Generic Host。它已经协调托管服务、日志提供程序、分层配置、DI 作用域、关闭信号和优雅停止。当前指南建议新建非 Web 宿主使用 `Host.CreateApplicationBuilder`。每个消费者都应遵守所提供的停止令牌、停止接收新工作，并服从有界排空策略。

对于 ASP.NET Core API，应使用 `WebApplicationBuilder` 和 ASP.NET Core 宿主。HTTP 服务器生命周期、请求作用域、配置、日志、中间件、端点激活和优雅关闭属于框架职责。把 `HttpContext.RequestAborted` 或端点取消令牌传过应用端口。

以下边界在三个场景中都保持不变：

- `decideDispatch` 保持纯粹，不感知宿主；
- 外部输入在边缘转换成经过验证的命令和配置；
- 存储、时钟、消息和遥测依赖保持为显式适配器或应用服务；
- 预期业务拒绝仍能与取消和故障区分；
- 一个组合根选择实现和生命周期；
- 资源所有权与关闭顺序有文档说明；
- 指标维度保持有界，敏感字段遵守策略；
- 适配器集成与并发保证接受独立测试。

宿主改变的是外层资源如何构造和治理，不应改变发货决策的含义。如果迁移到框架后，领域模块反而需要解析服务或读取环境配置，那么边界移动的方向就是错误的。

## 答案回顾 {#solution-review}

- 端口来自纯决策需要的事实，以及其结果所要求的提交。
- 当并发写入者不得超卖时，需要带版本或事务性的提交。
- 领域拒绝、提交冲突、取消和非预期故障保持不同。
- 除非有文档说明的清理策略要求不同，否则同一个取消令牌到达每项操作。
- 长生命周期客户端和单次操作会话可以有不同所有者。
- 本地监听器证明插桩，而不是到可观测性后端的交付。
- 指标标签使用小型有界词汇；请求标识符不属于指标标签。
- 日志和追踪只能在明确的隐私与保留策略下携带标识符。
- 显式构造适合有界进程；Generic Host 适合多服务生命周期协调。
- Web 宿主提供 HTTP 关注点，而函数式核心保持宿主无关。
