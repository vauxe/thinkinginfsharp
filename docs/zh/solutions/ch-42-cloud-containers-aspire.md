---
title: "第 42 章练习答案"
description: "选择合乎比例的计算模型，把 X42 切片转化为发布提案，并用诚实的未知结果设计幂等事件消费者。"
translationKey: solutions/ch-42-cloud-containers-aspire
kind: solution
part: 7
chapter: 42
status: complete
verifiedWith:
  fsharp: "10"
  dotnetSdk: "10.0.301"
exampleIds:
  - ecosystem-cloud-service
  - ecosystem-cloud-apphost
exerciseIds:
  - ch42-exercise-01
  - ch42-exercise-02
  - ch42-exercise-03
termIds: []
sources:
  - id: aspire-architecture
    url: https://aspire.dev/architecture/overview/
    checked: "2026-08-25"
  - id: aspire-health-checks
    url: https://aspire.dev/fundamentals/health-checks/
    checked: "2026-08-25"
  - id: aspire-service-defaults
    url: https://aspire.dev/get-started/csharp-service-defaults/
    checked: "2026-08-25"
  - id: aspire-deployment
    url: https://aspire.dev/deployment/
    checked: "2026-08-25"
  - id: aspire-cicd
    url: https://aspire.dev/deployment/ci-cd/
    checked: "2026-08-25"
  - id: dotnet-container-overview
    url: https://learn.microsoft.com/dotnet/core/containers/overview
    checked: "2026-08-25"
  - id: kubernetes-probes
    url: https://kubernetes.io/docs/concepts/workloads/pods/pod-lifecycle/#container-probes
    checked: "2026-08-25"
  - id: azure-functions-retries
    url: https://learn.microsoft.com/azure/azure-functions/functions-bindings-error-pages
    checked: "2026-08-25"
  - id: aws-lambda-best-practices
    url: https://docs.aws.amazon.com/lambda/latest/dg/best-practices.html
    checked: "2026-08-25"
---

# 第 42 章练习答案 {#overview}

这些答案会作出首个决策，却不假装决策永远正确。每个答案都说明已知内容、尚未证明的内容，以及哪些证据足以让团队转向更复杂或更简单的平台。

[返回第 42 章](../part-07/ch-42-cloud-containers-aspire).

## 练习 1：为三种工作负载选择计算模型 {#exercise-01}

### 场景 A：一个稳定的内部 HTTP API {#exercise-01-case-a}

从组织支持的最小托管应用平台开始。如果该路径能接收锁定的 .NET 发布制品，并提供所需的运行时、路由、身份、健康、日志与伸缩控制，容器目前不会增加业务能力。

如果组织已经晋升镜像摘要、要求基于镜像的安全门禁、需要让同一制品运行在多个兼容平台，或代码部署的构建契约过于不透明，则改选托管容器。两者差异在制品与运维所有权，而不在 F# 领域模型。

不把 Serverless 作为首选，因为 API 流量稳定、持续可用，也不天然是一个稀疏有限事件。函数层会增加触发器/运行时限制和第二套托管契约，却没有已证明的缩到零或事件集成收益。

不选择 Kubernetes，因为没有集群级需求，也没有团队拥有集群抽象。一个 Deployment 和 Service YAML 文件无法涵盖升级、入口、证书、策略、容量、存储、遥测、租户与事件响应。

第一个验收切片包含：

- 锁定 Release 构建与一个不可变发布制品或镜像摘要；
- 对数据库端点、身份与非秘密配置的启动验证；
- 使用托管身份访问数据库，不复制凭据；
- 分离的存活与就绪行为，包括数据库丢失和恢复；
- 请求取消、优雅排空、资源限制与代表性负载测试；
- 生产遥测查询、告警路径、分阶段发布、回滚与成本观测。

当制品漂移、原生依赖、平台可移植性或供应链策略变得重要时，从代码部署转到容器。当已验证需求超出所选托管产品的网络、伸缩、身份、运行时或运维限制时离开它。不要只因出现第二个服务就迁移。

### 场景 B：突发的图像元数据事件 {#exercise-01-case-b}

如果精确的提供方触发器能缓冲突发，每个条目都在经过测量的时长/内存/包限制内，而且缩到零能实质降低闲置所有权，则从 Serverless 事件工作进程开始。函数只是普通 F# 决策和限速媒体客户端端口之外的薄适配器。

队列事件必须携带稳定条目/事件 ID 与不可变对象版本。为每个语义身份持久化一条处理状态。并发重复要么观察现有结果，要么通过原子创建/比较操作竞争。绝不能把新的调用 ID 当作幂等键。

把并发限制在媒体 API 的安全速率以下。自动计算伸缩不等于有权倍增下游流量。同时使用提供方并发控制与客户端限流器；把限流当作声明过的瞬态结果，并采用带抖动退避。定义最大事件年龄、重试耗尽、毒性验证、部分批响应与死信重放。

首先拒绝 Kubernetes，因为没有需要集群的功能。只有在事件运行时的冷路径、限制、成本与本地/部署工具通过试验时，才先拒绝持续预热服务。

当流量变得稳定、冷启动/尾延迟违反目标、提供方时长或包限制约束处理、连接复用占主导，或每次调用成本超过预留容量时，转向托管工作进程/容器。队列在迁移后仍有价值，因为 F# 核心与持久事件契约不依赖处理器运行时。

试验测量冷/暖延迟、突发排空时间、重复率、下游限流、内存、包大小、重试/死信行为、遥测与成本。它把精确的 F# 包部署到提供方预发布环境；只有模拟器并不充分。

### 场景 C：已有平台上的二十个受监管服务 {#exercise-01-case-c}

Kubernetes 是首选，因为该场景既给出了具体集群级需求，也有配备人员的平台。准入策略、私有网络、批准的边车、租户调度与统一运维控制已经是平台职责，而非假想功能。

这并不授权使用每项 Kubernetes 功能。每个 F# 服务仍应是普通且可独立测试的进程，具有显式 HTTP/事件契约、外置状态、取消、健康与固定的非 root 镜像。领域项目不引用 Kubernetes API。

平台契约应定义：

- 批准的基础镜像、注册表、签名、SBOM 与漏洞例外；
- namespace/租户边界、服务账号、工作负载身份、秘密与网络策略；
- request、limit、中断预算、自动伸缩信号、配额与容量所有权；
- 启动、存活、就绪、终止宽限、排空与发布策略；
- 入口、证书、服务发现、出站、数据服务、备份与恢复；
- 日志、指标、跟踪、审计、告警路由、服务目标与成本分摊；
- 集群和工作负载升级节奏、兼容性测试与事件升级路径。

当团队有意采用当前部署集成时，Aspire 可以改善本地拓扑并输出目标制品。它不能替代平台策略、清单审阅、集群凭据、分阶段应用或回滚证据。

Serverless 仍可能赢得一个孤立事件边缘，托管服务也仍可能承载独立工作负载。答案不是“二十个都必须用 Kubernetes”；共享平台只在其控制与所有权降低总风险之处成为默认。

当平台增加的延迟、成本、耦合或事件负担超过策略价值时，反转单个工作负载。只有在共享监管控制有迁移证据时才反转平台决策，不能只凭更便宜的计算报价。

## 练习 2：把 X42 变成发布提案 {#exercise-02}

### 从精确基线出发 {#exercise-02-baseline}

仓库目前证明：

- F# 服务与 C# AppHost 在已检查的 macOS arm64 环境从锁文件还原并构建；
- 直接服务与 Aspire 编排服务都能回答三个已测试端点；
- AppHost 的本地资源检查会变为健康；
- SDK 为一个 `linux/arm64` 镜像归档生成并暴露元数据；
- 基础标签是 10.0.11、镜像用户是 1654、端口是 8080，且入口点已知；
- 最终容器命令之后，普通包锁保持不变。

它不证明容器运行、目标平台、云身份、注册表、生产探针、遥测导出、负载、安全策略、发布或回滚。发布提案从这条边界开始，而不是重新包装本地证据。

### 定义一个不可变制品 {#exercise-02-artifact}

选择托管环境支持的 CPU 架构。使用 SDK 10.0.301、锁定依赖与固定基础层在 CI 构建该架构，并在打包前运行完整测试套件。生成镜像摘要、SBOM、来源记录、许可证清单与漏洞报告。

只向受限注册表推送一次。通过组织批准的身份签名或证明摘要。晋升记录引用该摘要，而不是仅引用 `latest`、分支或提交。注册表保留策略必须同时保留活动摘要和回滚摘要。

策略门禁检查基础层支持、非 root 用户、是否意外要求可写或特权、端口/入口点、原生库、架构、秘密、严重性例外与签名。可复现重建是有用证据，却不能替代摘要晋升。

### 让运行时与安全契约显式可见 {#exercise-02-runtime}

在本地或 CI 以目标架构、UID、只读根文件系统、临时可写挂载、丢弃 capability、CPU/内存限制和端口 8080 运行镜像。验证启动、全部 API 响应、存活、就绪、信号驱动排空、强制终止与重启恢复。

平台服务账号只取得样例必需权限。X42 没有数据依赖，因此不虚构数据库或秘密。通过版本化配置加入非秘密 `DEPLOYMENT_MODE`。若未来出现秘密，使用平台秘密/身份路径，并在不打印其值的前提下测试轮换。

只通过预期平台路由暴露应用流量与探针路径。保护或隔离运维端点。管理与遥测连接使用加密和认证；不能把匿名回环仪表板例外复制到部署中。

### 只加入团队拥有的服务默认项 {#exercise-02-observability}

在可从 F# 调用的小型 C# Service Defaults 适配器，和显式 F# OpenTelemetry/ASP.NET Core 健康检查注册之间选择。锁定每个新增包。明确陈述重试与超时策略，而不是不加审阅地接受模板行为。

映射廉价存活检查，以及只包含真正必需依赖的就绪检查。为托管平台明确配置调用间隔、超时、阈值、启动余量与终止/排空行为。测试失败与恢复，而不只是 200 响应。

从预发布环境把安全日志、请求/错误/延迟指标、跟踪、运行时指标、发布摘要与环境身份发送到真实生产遥测后端，并实际查询。触发测试告警并确认所有者。定义采样、个人数据策略、保留、访问与预期摄取成本。

### 把部署生成与批准分开 {#exercise-02-pipeline}

最简单提案可能完全不需要 Aspire 部署：CI 系统可通过托管平台支持的声明式接口部署一个摘要，同时保留 AppHost 做本地编排。

若采用 Aspire 部署，就固定并安装 CLI，加入精确目标集成，用 `aspire publish` 生成目标输出，审阅后让受保护部署阶段应用。只有当阶段有意授予 Aspire 直接应用权限时，才用 `aspire deploy`。无论哪种方式，CI/CD 都拥有审批、身份、环境保护、日志与保留。

预发布与生产使用不同配置，却使用相同摘要。预发布门禁执行 socket/TLS、身份、健康、遥测、重启、资源限制与代表性负载测试。把平台 revision 与目标配置和摘要一同捕获。

### 安全地发布和反转 {#exercise-02-rollout}

先向一小部分流量或 revision 发布。根据错误率、尾延迟、就绪抖动、重启、资源压力、一个合成请求与成本决定是否扩大。在部署前就定义数值与观察窗口。

回滚把流量路由到保留的前一个摘要与兼容配置。X42 没有数据迁移，所以反转很简单；第一个持久依赖必须加入 schema 兼容与向前修复分析。用一个有意不健康的候选演练回滚。

观察窗口结束后，保留证据，删除失败 revision 与无用临时资源，并核对注册表、遥测、出站和计算成本。清理属于提案的一部分，因为废弃环境既是费用也是攻击面。

## 练习 3：设计幂等的 Serverless 预约消费者 {#exercise-03}

### 分开建模事实、尝试与不确定性 {#exercise-03-model}

用业务事件、渠道、收件人与模板版本派生稳定通知身份，而不是使用提供方调用：

```fsharp
type NotificationId = private NotificationId of string
type PayloadHash = private PayloadHash of string

type DeliveryState =
    | Reserved of payloadHash: PayloadHash
    | Sending of payloadHash: PayloadHash * attempt: int * lease: string
    | Accepted of payloadHash: PayloadHash * providerMessageId: string
    | OutcomeUnknown of payloadHash: PayloadHash * attempt: int
    | Rejected of payloadHash: PayloadHash * safeReason: string
```

`Accepted` 表示提供方确认消息，不证明人已经收到或阅读。`OutcomeUnknown` 不是 `Rejected`。载荷哈希防止同一身份悄悄携带不同收件人或内容语义。

纯核心在 `IgnoreAccepted`、`RejectConflict`、`AcquireAttempt`、`ReconcileUnknown`、`RetryTransient` 与 `RejectPermanent` 中决策。提供方事件、时钟、存储版本与邮件响应都是显式输入；存储与邮件调用仍是端口。

### 在外部副作用前后持久化 {#exercise-03-persistence}

收到事件时：

1. 验证 schema、事件类型、预约身份、收件人策略与载荷大小；
2. 派生 `NotificationId` 与 `PayloadHash`；
3. 原子创建 `Reserved`，或加载现有记录；
4. 对内容相同的 `Accepted` 状态立即返回成功；
5. 当同一身份已有其他载荷哈希时拒绝并告警；
6. 原子获取有界租约/栅栏令牌并转到 `Sending`；
7. 在提供方支持时，以通知 ID 作为其幂等键发起调用；
8. 使用租约/版本条件写入 `Accepted`、`Rejected` 或 `OutcomeUnknown`；
9. 只有持久状态允许时才确认源事件。

原子创建/比较防止两个并发处理器同时拥有一次尝试。租约到期允许崩溃恢复；栅栏阻止较晚的旧工作进程覆盖更新结果。保留期必须覆盖源最大重放期与业务审计窗口。

邮件提供方与本地存储之间仍存在双写缺口。如果提供方接受消息后，进程在提交 `Accepted` 前死亡，本地状态就不确定。

F# 类型或本地事务无法凭空制造恰好一次邮件。它需要提供方配合：用稳定幂等键做持久去重，或能按该键/消息 ID 查询状态。两者都没有时，对账只能依照显式业务策略，在可能重复发送与可能漏发之间选择。

### 分类重试与毒消息 {#exercise-03-retries}

格式错误的 schema、不受支持版本、无效收件人以及身份/载荷冲突都会导致永久失败或毒消息。记录安全诊断并路由到死信/隔离路径，不做无尽重试。

提供方超时、限流与部分 5xx 响应可能是瞬态候选，但必须先核对提供方契约。使用有界指数退避加抖动、最大事件年龄、尝试次数与并发限制。发送后的超时进入 `OutcomeUnknown`，而不是自动重新发送。

当运行时支持时，部分批处理只报告失败条目，使一个毒性事件不会重放已成功同批条目。死信重放是一条保留原始身份与载荷的受审计命令，不是复制成新键。

同时限制函数并发与提供方调用。自动伸缩不能超过提供方配额或存储容量。发出队列年龄与限流信号，让延迟工作在重试过期前可见。

### 从核心验证到提供方 {#exercise-03-evidence}

纯测试覆盖首次事件、已接受重复事件、冲突哈希、并发租约决策、瞬态重试、永久拒绝、陈旧工作进程完成与未知结果对账。

存储契约测试证明原子创建、条件更新、租约到期、栅栏与保留。适配器测试覆盖精确提供方请求、幂等头、状态/错误映射、取消、接受后的超时与脱敏诊断。事件夹具覆盖缺失、额外、null、超大、旧版本与未来版本输入。

目标提供方预发布测试发送重复和并发事件，在外部调用附近杀死处理器，观察重试/死信行为，演练提供方查询/幂等，并查询遥测。测量冷/暖延迟、队列年龄、伸缩、下游速率与成本。

用锁定的工作进程/绑定版本、最小权限身份、加密配置、并发与重试策略、告警，以及禁用或零并发紧急停止机制部署一个不可变包。通过事件源分区、别名、版本或提供方支持的流量控制渐进发布。

回滚必须继续读取新版本写入的状态，而且不能重置通知身份。若某个 schema 或状态转换不向后兼容，就暂停消费并采用向前兼容修复，而不是盲目启用旧代码。

最终保证刻意保持狭窄：每项语义通知都会进入有记录的终态，在提供方契约允许时抑制重复，不确定性可见且可对账。人工送达与外部副作用恰好一次，仍不受消费者单方面控制。
