---
title: "第 32 章练习答案"
description: "推导最小发货端口与资源职责，设计基数受控的遥测，并依据具体生命周期需求选择应用宿主。"
translationKey: solutions/ch-32-functions-to-applications
---

# 第 32 章练习答案 {#overview}

这些答案把领域策略留在纯函数中，并在边缘明确进程职责。名称可以不同，但四项要求不变：能力最小化，取消与故障语义明确，每项资源都有人负责，以及遥测字段的基数受控。

[返回第 32 章](../part-05/ch-32-functions-to-applications)。

## 练习 1：推导端口与资源职责 {#exercise-01}

### 从所需数据与操作开始 {#exercise-01-ports}

假设后台任务接收已验证的 `Order`，库存由已验证的 `Sku` 标识。最小端口定义如下：

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

工作流如下：

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

两个 I/O 调用都接收调用方的同一个取消令牌。若令牌已取消，第一个调用就不应开始。适配器发出的取消仍按取消处理；非预期数据库异常则让任务失败。`DispatchError` 表示预期业务拒绝。`VersionConflict` 等 `CommitError` 来自持久化或并发，不应伪装成领域规则。

该模型不会自动重试版本冲突。只有当操作有明确的重试上限、订单身份稳定且提交幂等时，调用者才能重新加载并再次决策。库存变化后继续复用先前的领域结果是不正确的。

长生命周期客户端和单次操作会话应分别指定释放责任：

- 组合根创建数据库客户端或连接池，随后由进程负责管理；
- 进程关闭时先停止新工作、等待未完成调用，再释放客户端；
- 适配器为一次操作创建会话或事务，并在操作内部用 `use` 或 `use!` 释放；
- 纯函数永远看不到这两种资源；
- 如果共享客户端仍由调用方负责释放，应用就不得释放它。

如果加载与提交必须共享同一个数据库事务，上面的两次调用端口就不够。适配器应创建一个事务，在其中加载、执行纯决策并按条件提交，最后释放事务。也可以使用存储提供的 compare-and-swap 操作。两个调用相邻并不代表它们具有原子性。

表达这些规则不需要容器。构造函数或函数参数声明依赖，`use` 标出局部资源的释放范围。容器日后可以自动管理长生命周期注册和作用域，无需改变领域工作流。

## 练习 2：设计三种遥测信号 {#exercise-02}

### 让每种信号只承担一种工作 {#exercise-02-signals}

一种连贯设计是：

| 信号 | 名称 | 字段/标签 | 最终结果 |
|---|---|---|---|
| 结构化日志 | `dispatch.attempt` | `outcome`、`orderId`、`sku`、`quantity`、`detail` | accepted、rejected、conflicted、canceled、faulted |
| 计数器 | `dispatch.attempts` | `outcome`，以及可选的有界 `channel` | 同一套有界结果词汇 |
| 活动 | `dispatch.place` | `dispatch.outcome`、`order.id`、`inventory.sku`；按策略设置状态和异常元数据 | 在每条被采样路径上停止 |

`outcome` 只有应用定义的五个值，因此基数有限。含义稳定时，`web`、`batch`、`manual` 等枚举渠道也可作为低基数字段。`orderId` 和通常的 `sku` 都是高基数值，不能成为指标标签。只有访问、保留、采样和隐私策略允许时，它们才能进入日志或追踪。

不要记录客户姓名、地址、自由文本备注、身份验证令牌、连接字符串和原始载荷。若其他系统能通过订单 ID 找到个人，订单 ID 同样敏感。策略要求时，应脱敏或使用不可逆的关联值。

结构化事件应保留带类型的字段，不要拼成一句文本。使用 `ILogger` 时，采用稳定的消息模板和事件 ID，让提供程序保留这些属性。日志级别取决于运维动作：正常缺货拒绝可以记为信息，非预期适配器异常则记为错误。

每次尝试结束时，计数器只递增一次。计数器报告发生次数，由收集器计算总量或速率。需要时长时，应增加注明单位的直方图，不要把平均值塞进计数器。告警属于采集或后端配置，不属于领域函数。

围绕应用编排启动活动，并在 `finally` 中释放它。把 `null` 活动视为正常情况。在活动上放置有界结果，并一致使用状态：预期拒绝可以在协议层成功完成，而非预期异常是错误。单独记录取消，不要把它改写成故障。

本地 `MeterListener` 可确认进程发布了名称、数值和标签都符合预期的测量。`ActivityListener` 可确认采样活动已启动、添加标签并停止。捕获日志回调则确认结构化记录已产生。

这些监听器无法验证聚合、采样策略、传播标头、批处理、导出、身份验证、后端摄取、保留、仪表板或告警。应在集成或预发布环境测试真实的 OpenTelemetry 或提供程序管线。再根据运维重要性增加后端查询或健康信号。

## 练习 3：选择宿主层级 {#exercise-03}

### 让生命周期需求选择工具 {#exercise-03-hosts}

导入单个文件的命令适合直接构造依赖。它只有一次有限操作、自然的 `use` 作用域、简单的参数与配置解析，以及一个退出码。增加服务容器和托管服务生命周期不会减少实质复杂度。若需中断，可使用控制台信号产生的取消令牌。

对于包含三个后台消费者的进程，应使用 Generic Host。它已经协调托管服务、日志提供程序、分层配置、DI 作用域、关闭信号和优雅停止。当前指南建议新建非 Web 宿主使用 `Host.CreateApplicationBuilder`。每个消费者都应遵守所提供的停止令牌、停止接收新工作，并服从有界排空策略。

对于 ASP.NET Core API，应使用 `WebApplicationBuilder` 和 ASP.NET Core 宿主。HTTP 服务器生命周期、请求作用域、配置、日志、中间件、端点激活和优雅关闭属于框架职责。把 `HttpContext.RequestAborted` 或端点取消令牌传过应用端口。

以下边界在三个场景中都保持不变：

- `decideDispatch` 保持纯粹，不感知宿主；
- 外部输入在边缘转换成经过验证的命令和配置；
- 存储、时钟、消息和遥测依赖仍由参数明确传入适配器或应用服务；
- 预期业务拒绝仍能与取消和故障区分；
- 一个组合根选择实现和生命周期；
- 文档写明各项资源由谁释放以及关闭顺序；
- 指标维度保持有界，敏感字段遵守策略；
- 适配器集成与并发保证接受独立测试。

宿主只改变外层资源的构造与管理方式，不应改变发货决策。若迁移框架后，领域模块反而需要解析服务或读取环境配置，就说明框架职责侵入了领域层。

## 答案回顾 {#solution-review}

- 端口来自纯决策需要的事实，以及其结果所要求的提交。
- 当并发写入者不得超卖时，需要带版本或事务性的提交。
- 领域拒绝、提交冲突、取消和非预期故障保持不同。
- 除非有文档说明的清理策略要求不同，否则同一个取消令牌到达每项操作。
- 长生命周期客户端和单次操作会话可以有不同所有者。
- 本地监听器只能验证进程内插桩，不能验证数据已送达遥测后端。
- 指标标签使用小型有界词汇；请求标识符不属于指标标签。
- 日志和追踪只能在明确的隐私与保留策略下携带标识符。
- 直接构造适合有限操作；Generic Host 适合协调多个服务的生命周期。
- Web 宿主提供 HTTP 关注点，而函数式核心保持宿主无关。
