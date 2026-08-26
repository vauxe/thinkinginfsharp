---
title: "第 42 章：云、容器、Serverless 与 .NET Aspire"
description: "从进程、状态、伸缩、运维、安全与证据需求选择部署模型，同时保持 F# 应用边界显式可见。"
translationKey: part-07/ch-42-cloud-containers-aspire
---

# 第 42 章：云、容器、Serverless 与 .NET Aspire {#overview}

F# 服务以 .NET 进程运行在云端。部署之后，领域类型、函数、端口、取消、配置与传输契约继续定义应用。云产品负责这些边界外围的机器、网络、伸缩、身份和运维。

应先说明部署问题，再选择产品与层次。容器打包一个进程，Kubernetes 等计算平台运行它，Serverless 改变执行与计费契约，Aspire 描述应用模型并可驱动本地编排或面向目标的部署工作；生产环境由单独的目标运行时承载。

本章从进程与制品出发，再逐层向外。这一顺序让 F# 设计保持可见，也让每一项云端主张都与证据相称。

## 学完本章你将能够 {#outcomes}

学完本章后，你应该能够：

- 区分源码、应用制品、镜像、运行实例、平台配置和发布，并为每项声明选择相称的证据；
- 根据触发方式、生命周期、状态、伸缩、控制和运维所有权，在托管进程、托管容器、Kubernetes 与 Serverless 之间选择；
- 定义配置、秘密、身份、存储、网络、资源、关闭和健康等进程与平台契约；
- 针对重试、重复投递、部分完成和毒消息设计持久处理器，并把状态与幂等性放在临时实例之外；
- 把 F# 服务接入容器与 Aspire，同时分清 AppHost、服务健康、发布/部署和 CI/CD 的职责；
- 让同一不可变制品完成可逆发布，并为成本、安全、可观测性和回滚准备明确证据。

::: tip 分两轮阅读
初读时依次掌握[部署层次](#deployment-contracts)、[选型方法](#compute-decision-map)和[本地验证样例](#verified-slice)。准备真实部署时，再阅读[发布方案](#release-observe-rollback)、[分层验证](#evidence-ladder)和[采用试验](#adoption-spike)。
:::

## 部署由多层契约组成 {#deployment-contracts}

把一次发布看成几个有关联、却不能互相替代的对象：

```text
F# 源码 + 锁定依赖
  -> 编译/发布后的应用制品
  -> 面向一个 OS/架构的可选容器镜像
  -> 平台配置与外部资源
  -> 运行实例、路由、身份与数据
  -> 带有发布和回滚状态的可观测发布
```

成功的 `dotnet build` 证明所选目标框架能够编译。`dotnet publish` 证明发布布局能够生成。镜像归档生成成功只证明容器打包完成。本地进程能回答健康 URL，只证明一条运行路径。它们单独都不能证明注册表完整性、目标架构、生产身份、网络策略、持久存储、负载行为、托管服务兼容性、渐进发布安全或回滚。

### 在说出平台名字前先问六个问题 {#six-questions}

1. **触发方式：** 工作由 HTTP、队列、计划、流还是长连接驱动？
2. **生命周期：** 它是连续进程、有限调用、批任务，还是跨越等待的持久工作流？
3. **状态：** 什么必须在重启、重复、横向扩展、区域丢失与部署之后仍然存在？
4. **伸缩：** 需要怎样的并发、延迟、突发、预热容量与地域行为？
5. **控制：** 团队必须掌握哪些运行时、网络、文件系统、加速器、边车或策略细节？
6. **运维：** 谁负责补丁、监控、响应、成本控制、变更批准与恢复演练？

答案可能只是一个小型托管 Web 服务。使用分布式架构并不表示系统更成熟。每增加一个进程边界，就会用序列化、网络、部分失败、认证、版本、遥测与运维责任替换一次函数调用。

## 按比例选择计算模型 {#compute-decision-map}

| 首选候选 | 适合的约束 | 必须有理由承担的摩擦 |
| --- | --- | --- |
| 托管应用/进程平台 | 一个普通 Web 或工作进程服务；团队希望平台负责主机、路由、补丁与基本伸缩 | 平台构建契约、受支持 .NET 版本、受限主机控制、提供方配置与诊断 |
| 托管容器平台 | 可移植镜像已是发布契约；伸缩和网络需求适中；不需要集群 API | 注册表、镜像生命周期、入口、身份、卷、探针、伸缩限制、冷容量 |
| Kubernetes | 多个工作负载需要统一的集群调度、自定义控制器、网络策略、边车，或已有平台团队运维它 | 集群升级、策略、容量、入口、证书、存储、租户、可观测性、事件响应负担 |
| Serverless 函数/事件运行时 | 稀疏或突发的有限工作天然映射到提供方触发器，并能接受调用契约 | 绑定/运行时兼容、冷启动、时长与载荷限制、重试、并发、临时状态、本地保真度、提供方耦合 |
| VM 或直接管理的主机 | 遗留/原生依赖、特殊 OS 控制、稳定负载或迁移约束使主机所有权值得承担 | 补丁、进程监管、容量、故障转移、证书、部署、遥测、备份、加固 |

这些是起点，不是排名。托管容器产品也许能缩到零；Serverless 产品也许接收容器镜像；Kubernetes 也许是托管的；VM 也能运行容器。应从必须拥有的契约决策，而不是从营销分类决策。

## 本地云样例：一次经过验证的本地运行 {#verified-slice}

本地云样例刻意只包含一个 F# HTTP 服务、一个 C# 项目式 AppHost，并且不需要云账号或外部服务。它展示应用代码、开发期编排与容器打包之间的边界，却不假装验证过提供方部署。

### F# 服务与固定的镜像基础层 {#fsharp-service}

```xml:line-numbers [CloudService.fsproj]
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ContainerRepository>thinking-in-fsharp-cloud-service</ContainerRepository>
    <ContainerBaseImage>mcr.microsoft.com/dotnet/aspnet:10.0.10</ContainerBaseImage>
  </PropertyGroup>

  <ItemGroup>
    <Compile Include="Program.fs" />
  </ItemGroup>
</Project>
```
Web SDK 以 `net10.0` 为目标；复制样例后，可以在项目文件中固定 FSharp.Core 10.1.301。镜像基础层显式固定为 `mcr.microsoft.com/dotnet/aspnet:10.0.10`；浮动的 `10.0` 标签会让运行时在提交未变时悄然移动。与较早版本使用 Debian 基础层不同，.NET 10 未限定的微软镜像标签使用 Ubuntu，因此 OS 假设需要测试。

```fsharp:line-numbers [Program.fs]
namespace ThinkingInFSharp.Ecosystem.Cloud

open System
open System.Text.Json.Serialization
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http

[<CLIMutable>]
type HealthResponse =
    { [<JsonPropertyName("status")>]
      Status: string }

[<CLIMutable>]
type RuntimeResponse =
    { [<JsonPropertyName("service")>]
      Service: string
      [<JsonPropertyName("deploymentMode")>]
      DeploymentMode: string }

[<RequireQualifiedAccess>]
module CloudService =
    let private writeJson (context: HttpContext) (value: 'value) : Task =
        context.Response.WriteAsJsonAsync<'value>(value, context.RequestAborted)

    let private live context =
        writeJson context { Status = "healthy" }

    let private ready context =
        // This sample has no required external dependency. A real readiness probe
        // must test only dependencies that should stop this instance receiving traffic.
        writeJson context { Status = "ready" }

    let private runtime context =
        let deploymentMode =
            match Environment.GetEnvironmentVariable "DEPLOYMENT_MODE" with
            | null -> "standalone"
            | value when String.IsNullOrWhiteSpace value -> "standalone"
            | value -> value

        writeJson
            context
            { Service = "cloud-service"
              DeploymentMode = deploymentMode }

    let map (application: WebApplication) =
        ArgumentNullException.ThrowIfNull(application, nameof application)

        application.MapGet("/health/live", RequestDelegate live) |> ignore
        application.MapGet("/health/ready", RequestDelegate ready) |> ignore
        application.MapGet("/api/runtime", RequestDelegate runtime) |> ignore

module Program =
    [<EntryPoint>]
    let main arguments =
        let builder = WebApplication.CreateBuilder arguments
        use application = builder.Build()
        CloudService.map application
        application.Run()
        0
```
`CloudService.map` 只提供三个职责明确的 HTTP 端点。`/health/live` 表示进程能够响应，`/health/ready` 表示这个无依赖样例能接收流量，`/api/runtime` 只返回一个受控教学值，默认是 `standalone`；它不会转储环境变量。

尽管两个探针的当前实现都立即返回，它们仍使用不同路径。这样，日后出现真实就绪条件时，契约不必改变。数据库故障也许应该让实例退出流量，但若同一瞬态故障也让存活检查失败，就可能重启所有副本并放大事故。

### 用 C# 基础设施外壳包围 F# 项目 {#csharp-apphost}

```xml:line-numbers [AppHost.csproj]
<Project Sdk="Aspire.AppHost.Sdk/13.5.2">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <AspireUseCliBundle>true</AspireUseCliBundle>
    <AspireCliInvocationMode>DnxPinned</AspireCliInvocationMode>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="../CloudService.fsproj" />
  </ItemGroup>
</Project>
```
AppHost SDK 固定为 13.5.2，并以 .NET 10 为目标。它的项目引用指向 F# 服务。Aspire 根据该引用生成 `Projects.CloudService` 元数据类型；服务并不会变成 C#，也不会引用 AppHost。

项目启用 Aspire CLI bundle，并选择 `DnxPinned`。SDK 因而解析匹配的 `Aspire.Cli@13.5.2`，而不会把宿主平台专用的 Dashboard 与编排包写入 AppHost 锁文件。它不要求全局安装 CLI，但首次使用需要可用包源或已有缓存。这是可选生态样例，不是书站依赖。

```csharp:line-numbers [AppHost Program.cs]
using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

builder
    .AddProject<Projects.CloudService>("cloud-service")
    .WithHttpEndpoint(name: "http")
    .WithEnvironment("DEPLOYMENT_MODE", "aspire-local")
    .WithHttpHealthCheck("/health/ready");

builder.Build().Run();
```
AppHost 为资源命名、显式声明 `http` 端点、注入 `DEPLOYMENT_MODE=aspire-local`，并附加 HTTP 健康检查。显式端点很重要：没有启动配置提供端点元数据时，第一次真实运行在编排之前就失败了，因为健康检查找不到可选的 `http` 或 `https` 端点。

这是良好的语言互操作边界。F# 拥有应用及其类型化行为，C# 拥有一个很小的基础设施 DSL；其当前模板、生成元数据与示例以 C# 为主。没有领域类型跨过此边界，因此替换编排工具无需重写服务。

### 实际执行了什么 {#executed-evidence}

已验证流程建立了以下证据：

- .NET SDK 10.0.301 从锁文件还原 F# 与 AppHost 项目；
- 两个项目都以 Release 构建，且为 0 警告、0 错误；
- 直接请求服务分别返回 `healthy`、`ready` 与 `standalone`；
- Aspire 13.5.2 启动 F# 子进程并注入 `aspire-local`；
- 仅回环 HTTP 仪表板显示资源为 `Running`、健康状态为 `Healthy`，且 `/health/ready` 检查为 `Healthy`；
- 经 Aspire 分配的端口访问服务时返回 `aspire-local`；
- .NET 容器目标从固定 ASP.NET Core 10.0.10 基础层生成约 89 MB 的 `linux/arm64` 归档；
- 归档元数据显示非 root UID 1654、端口 8080，入口点为 `dotnet /app/CloudService.dll`；
- 最终发布命令未改变普通 `net10.0` 锁文件的哈希；
- 完整锁定解决方案构建、测试、Fable 生产构建与浏览器冒烟仍然通过。

机器上有 Docker CLI，但没有运行 daemon。归档经过检查后已删除；本章不声称启动过容器。没有执行注册表推送、签名、SBOM 证明、漏洞决策、云身份、外部依赖、负载测试、生产遥测导出、部署或回滚。

### 本地 HTTP 是显式的测试例外 {#local-http-exception}

机器也没有受信任的 Aspire 开发证书。第一次 HTTPS 仪表板启动了服务，却无法验证自己的资源服务证书，因此 UI 一直断开。最终检查按照 Aspire 本地配置允许的方式，使用严格绑定到 `127.0.0.1` 的固定匿名 HTTP 端点。

这种模式不是生产建议。其他本地进程可以读取或提交仪表板遥测。绝不能把匿名未加密的仪表板或 OTLP 接收器绑定到局域网或公网地址。普通团队使用应建立受信任的开发 HTTPS；远程与生产端点需要符合环境要求的认证、加密、网络策略与秘密处理。

## 先设计进程，再设计容器 {#process-contract}

如果应用假定只有一台机器、内存永不消失、秘密在本地，或中断前一定能优雅完成，容器无法修复它。应先让进程契约显式可见。

### 配置、秘密与身份 {#configuration-secrets-identity}

配置名称与验证属于源码；环境专用值不属于源码。启动时验证必需值，区分秘密与非秘密配置，并在接收流量之前安全失败。不要记录原始配置对象，也不要把进程环境作为诊断结果返回。

环境变量只是一种传递配置的方式，不是秘密管理器。只授予必要权限的平台身份通常优于复制到文件或变量中的长期凭据。应定义谁能读取每个秘密、如何轮换、实例能否刷新，以及新旧凭据重叠时回滚如何工作。

不要把环境名、连接字符串、证书、令牌或提供方账号 ID 烘焙进镜像。只构建一次，在部署时注入值。若配置会实质改变应用行为，就应像代码一样对配置契约做版本管理和测试。

### 状态、存储与文件系统 {#state-storage-filesystem}

假定实例可能在任意两个副作用之间消失。除非业务能够承受丢失，否则内存只是缓存。可写容器层通常是临时且实例私有的；两个副本不会共享它，替换实例也会丢弃它。

必须长期保留的数据应位于显式数据服务、对象存储、队列或挂载卷中，并且要理解其一致性、并发、备份、还原、加密、保留、地域与迁移语义。“平台会持久化”不是数据契约。

让缓存可重建。给临时文件设置大小上限与清理所有者。在目标支持时测试只读根文件系统。不要依赖本地时钟、主机名、实例数量或请求顺序来维持领域不变量。

### 启动、关闭与取消 {#startup-shutdown-cancellation}

ASP.NET Core 的 Generic Host 响应进程关闭信号并尝试优雅停止。应用代码必须传播请求取消，在开始等待现有工作完成时停止接收新工作，限制清理时间，并且在持久副作用提交前不声称完成。

关闭是尽力而为，不是事务。崩溃、强杀、节点丢失或超时可能绕过清理。因此持久工作需要幂等性、租约、检查点或事务性发件箱/收件箱边界，而不能信赖 `finally` 或 `StopAsync`。

应在真实镜像和平台下测量启动与关闭。探针延迟、终止宽限期、负载均衡停止转发流量的时间、长请求、后台工作与存储可见性会互相作用。本地优雅响应 Ctrl+C 只是最初级的证据。

## 健康是控制信号，不是状态页面 {#health-signals}

不同消费者需要不同答案：

| 信号 | 问题 | 典型反应 |
| --- | --- | --- |
| 启动 | 初始化是否已完成到足以开始其他探针？ | 继续等待或终止失败的启动 |
| 存活 | 这个进程是否已不可恢复地卡住？ | 重启这个实例 |
| 就绪 | 现在是否应把新流量发给这个实例？ | 从路由中移除或加入实例 |
| 依赖/资源就绪 | 依赖资源是否可以启动或继续？ | 暂停编排顺序 |
| 合成/用户旅程 | 重要操作能否穿过真实边界成功？ | 告警、停止发布或调查 |
| 业务健康 | 领域结果、队列、延迟、错误与成本是否在目标内？ | 运维或产品响应 |

Kubernetes 明确赋予启动、存活与就绪探针不同作用。Aspire 也区分 AppHost 资源检查与服务端点检查。本地云样例把 AppHost 检查连接到服务就绪 URL，但一次绿色本地检查并不会配置生产负载均衡器。

让存活检查便宜，并与瞬态下游系统无关。就绪检查可以包含必需依赖，但无界依赖链可能在共享故障时让每个实例都不就绪。设置超时，避免泄露内部信息，控制缓存与暴露范围，并测试失败和恢复。

## 镜像是一项发布输入 {#image-contract}

.NET SDK 8.0.200 及以后版本能把受支持项目发布为容器，无需额外容器构建包。这让简单场景少写一个 Dockerfile，却不会消除镜像设计。

应记录并验证：

- SDK、目标框架、依赖锁、发布模式与容器工具版本；
- 基础镜像仓库、精确补丁或摘要、OS 发行版、架构与支持周期；
- 非 root 用户、可写路径、capability、环境、暴露端口、入口点与工作目录；
- 全球化、证书、时区数据、原生库、诊断以及内存/CPU 行为；
- 标签、许可证清单、SBOM、来源、签名、漏洞策略与例外所有者；
- 注册表目标、不可变摘要、保留、复制、访问、晋升与回滚。

标签是方便的名字；摘要标识镜像内容。让同一摘要依次晋升到测试、预发布与生产。日后重新构建“同一提交”，可能选择不同的基础镜像、包源、时钟或工具，从而产生不同制品。

多架构索引并不证明行为完全相同。原生库、全球化、JIT 行为、可用镜像与性能可能在 `amd64` 与 `arm64` 间不同。构建并冒烟每个实际部署架构，再在目标安全上下文与资源限制下测试。

### 本地云样例的锁文件教训 {#lock-file-lesson}

第一个容器命令加入了 `--os linux`。随后还原把项目锁图改写为带运行时标识符 `linux-arm64`，而下一次普通解决方案 `--locked-mode` 还原失败，因为项目本身没有声明 RID。

最终已检查命令让容器目标选择 Linux 镜像平台，而不改变应用项目的运行时标识符。锁文件哈希保持稳定。当自包含或跨架构发布确实需要 RID 专用图时，应有意隔离并提交那项发布契约；不要让临时发布悄悄修改开发锁。

## Serverless 是调用契约 {#serverless-contract}

“Serverless”表示提供方拥有执行机群的更多部分，并暴露更高层的调用、伸缩与计费模型。服务器依然存在，应用职责也依然存在。

当工作天然有限、稀疏或突发，提供方触发器能省去有意义的基础设施，缩到零可以接受，且团队能适应运行时限制时，可以选择函数或事件运行时。持续繁忙的低延迟 API、长连接、沉重的原生进程、稳定高吞吐工作进程，或需要复杂内存协调的工作流，通常更适合服务或容器。

### 用简短处理器连接纯决策 {#thin-handler}

采用下面的形状：

```text
提供方事件/绑定
  -> 验证并映射公开输入
  -> 纯 F# 决策或工作流
  -> 面向持久副作用的显式端口
  -> 提供方响应、确认、重试或死信结果
```

把提供方特性、触发器类型、上下文与 SDK 客户端留在边缘。核心应接收普通记录与联合，并返回声明过的决策。这样无需模拟器也能运行业务行为，也保留在函数与工作进程间迁移的可能。

只有理解绑定的失败语义时，绑定才真正减少代码。要问清谁拥有序列化、批处理、检查点、确认、重试、毒消息、部分批成功、并发、取消与遥测。若输出绑定隐藏了必须分类的错误，通过端口调用直接客户端 SDK 也许更安全。

### 假定会重试和重复投递 {#retries-duplicates}

提供方行为会随触发器和调用模式变化，但重复投递是正常可能性。AWS 明确记录事件源映射至少投递一次；Azure 的事件与重试指南同样要求考虑重复处理。“函数运行了一次”不是领域保证。

给每条命令或事件稳定身份。验证同一身份具有相同语义载荷。尽可能让副作用与去重记录原子化。只在权威副作用提交后记录成功。定义保留期、冲突、重放、部分批处理、重试耗尽与死信行为。

支付超时不代表支付失败。它可能是结果未知，需要在同一幂等键下查询或对账。用新身份重试可能造成重复扣款，不论函数扩展得多快。

### 冷启动、并发与限制都是设计输入 {#cold-start-concurrency-limits}

在所选计划与区域测量包大小、初始化、依赖连接建立、第一次调用、暖调用、扩展与尾延迟。不要承诺从另一种语言、包、计划或日期抄来的冷启动数字。

并发可能发生在实例内、实例间，或两者都有。静态可变值和本地缓存依照提供方工作进程模型共享，而不是每个逻辑事件独占。限制下游连接与请求速率；自动计算伸缩可能在帮助用户之前先压垮数据库或付费 API。

每个提供方都会定义受支持 .NET 版本、CPU 架构、时长、内存、临时存储、载荷、网络、并发、重试与部署包限制。把这张矩阵当作注明日期的依赖，并测试精确的触发器与托管计划。

### 提供方 .NET 工作进程中的 F# {#fsharp-provider-workers}

提供方宣传“.NET”并不能证明存在完善的 F# 模板、分析器、生成绑定、本地工具、Native AOT 行为或文档。F# 可以使用普通 .NET 库，但代码生成和工具 API 可能更适合某种语言。

截至 2026-08-25，Azure Functions 4.x 隔离工作进程文档列出 .NET 10，并注明 F# 应用对某些绑定扩展可能需要显式注册。文档还记录计划专用限制与最低工作进程包版本。本章审阅了这些资料，但没有构建或部署 Azure Function。

AWS 当前记录 .NET 10 Lambda 基础镜像与 .NET 打包路径，但术语和示例主要使用 C#。只有当 F# 项目试验验证处理器发现、序列化器行为、包、架构、本地调用、冷路径、部署与遥测后，编译后的 F# 处理器才是候选。本地云样例没有执行其中任何步骤。

不要为了逃避部署知识而选择 Serverless。它会增加提供方运行时、触发契约、身份、限制、计费维度、本地模拟器/工具和事件失败语义。只有这些新增项比其消除的自有基础设施风险更小，才应该选择它。

## Aspire 描述系统结构，但不会消除其复杂性 {#aspire-model}

Aspire AppHost 是声明资源与关系的代码。在 run 模式下，Developer Control Plane 启动和监控本地进程或容器，分配端点，注入配置，并向仪表板提供信息。官方架构指南明确说明 AppHost 不是生产运行时。

### 资源、引用与顺序 {#resources-references-ordering}

`AddProject`、`AddContainer` 与托管集成把资源加入应用模型。`WithReference` 表达关系，并可注入连接或端点信息。`WaitFor` 控制启动就绪顺序。三者解决不同问题：引用不会自动成为等待，任何一个也都不是生产授权策略。

Aspire 集成是教 AppHost 如何表示和连接资源的包。添加数据库集成也许会启动本地容器、连接现有服务，或参与部署。它不会决定 schema 所有权、事务边界、备份、容量、故障转移、数据分类或删除。

把资源命名成稳定的运维概念。把生成的连接数据当作服务边界处的配置。让领域代码不知道 Aspire 资源类型，这样测试与替代宿主仍能直接构造同样的端口。

### 两套健康系统 {#two-health-systems}

AppHost 资源健康回答编排是否认为资源已就绪，包括依赖方的 `WaitFor` 能否继续。服务端点健康回答生产平台是否应向某个运行实例发送流量，或者重启它。

仪表板可以显示 HTTP 资源检查，本地云样例已验证这一点。生产环境仍然需要平台探针配置，指向正确的服务路径、端口、超时、阈值与安全边界。把显示正常的仪表板截图复制进运行手册，并不会生成这些配置。

### Service Defaults 是源码，不是魔法 {#service-defaults}

当前 C# Service Defaults 模板组合 OpenTelemetry、健康检查、服务发现与标准 `HttpClient` 韧性策略。它是可定制的共享项目。只有调用 `AddServiceDefaults` 和 `MapDefaultEndpoints` 才会安装这些行为；仅仅在 AppHost 下运行不会自动插桩应用。

本地云样例刻意省略 Service Defaults。AppHost 会注入 OTLP 相关环境变量，但 F# 服务没有 OpenTelemetry SDK/导出器包，因此本章不声称产生了跟踪或指标。它的健康端点是显式教学处理器，不是 ASP.NET Core `IHealthCheck` 注册。

真实 F# 解决方案可以从三条可行路径中选择：

1. 引用小型 C# Service Defaults 适配器，并从 F# 组合根调用其公开扩展；
2. 用 F# 直接重现真正需要的注册，并锁定包和测试；
3. 创建语言中立的共享库，有意让公开 API 同时对 F# 与 C# 友好。

不要复制一次模板后就忘记它。团队必须拥有重试策略、超时、端点暴露、插桩源、导出器行为、采样、包升级与生产后端验证。

## 本地编排与部署是不同模式 {#local-versus-deployment}

当前 Aspire 部署采用流水线模型。部署目标或计算环境会向应用模型贡献目标专用步骤。

- `aspire publish` 求值 AppHost 并输出制品，留给后续工具或人应用；它是单向交接。
- `aspire deploy` 求值模型、解析参数、生成目标输出并直接应用。
- 当 CI/CD 需要拆分流程时，`aspire do <step>` 调用具名流水线步骤。

这些命令需要适当的 CLI 与目标集成。本地云样例只用固定 CLI bundle 做开发期编排，没有配置部署目标，也没有执行 publish 或 deploy。其 AppHost 构建只是本地应用模型与编排检查。

### 环境不等于执行模式 {#environment-execution-mode}

Aspire 把 Development 或 Production 这样的环境名称，与 run 或 publish 这样的执行上下文区分开。部署命令与开发命令的默认值不同，Aspire 环境也不会自动设置 `DOTNET_ENVIRONMENT` 或其他子进程运行时变量。

当行为依赖子进程环境时，应显式传递它。让拓扑分支保持很小，并测试每个分支。只在 Production 出现的条件资源仍然需要验证；只在部署期间运行的分支是可执行基础设施代码，而不是无害配置。

### CI/CD 仍然负责治理 {#cicd-governance}

Aspire 可以定义应用专用的构建、发布、推送与部署步骤。CI/CD 仍然负责检出、测试检查、身份、审批、制品保留、环境保护、并发、审计、晋升与紧急控制。

优先使用工作负载身份或其他短期凭据机制。把计划/发布证据与应用权限分开。对破坏性数据或网络变更要求审核。捕获目标输出、不含秘密值的参数、镜像摘要、工具版本、日志与部署结果。

## 发布、观察与回滚 {#release-observe-rollback}

可信的发布记录应写明：

- 不可变应用/镜像摘要及其来源；
- 数据库或消息 schema 兼容性与迁移顺序；
- 目标环境、身份、配置版本、路由与功能开关；
- 启动、就绪、冒烟、契约、安全与性能检查；
- 遥测查询、服务级指标、成本信号与告警所有者；
- 发布阶段、暂停条件、中止阈值与最大观察窗口；
- 回滚制品与配置、数据向前修复策略及负责操作员。

回滚不总是“部署旧镜像”。破坏性迁移、已发事件、已扣支付、已发通知或不兼容缓存项可能比代码活得更久。应优先采用扩展—收缩 schema、容忍版本的消费者、幂等副作用，以及在无法逆转时测试过的向前修复。

可观测性也是边界。日志、指标与跟踪必须携带安全的关联标识和发布身份，而不含秘密或个人数据。本地仪表板可见并不能证明生产导出、保留、采样、后端摄取、查询正确、告警送达或事件响应。

成本是运维信号，不是事后补项。记录请求、时长、CPU、内存、出站流量、存储、托管资源单位、闲置容量、构建分钟、日志量与支持人力。Serverless 对稀疏突发可能经济，对稳定或高交互工作可能昂贵；Kubernetes 可能降低单位计算成本，却增加平台人力。

## 按风险逐层验证 {#evidence-ladder}

只按风险需要向外推进：

1. 针对决策、幂等性与状态转换的纯 F# 测试；
2. 针对配置、序列化、提供方事件、取消与失败映射的适配器测试；
3. 面向每个目标框架/RID 的锁定 Release 构建与普通发布；
4. 针对用户、端口、入口点、基础层、架构、SBOM、签名与漏洞的镜像元数据和策略检查；
5. 在只读/非 root/资源限制设置下启动容器，再测试探针与关闭；
6. 使用 AppHost 或模拟器测试资源连线与代表性依赖路径；
7. 在目标平台预发布环境用真实身份、网络、数据、探针、遥测、伸缩与故障注入做部署；
8. 满足用户、可靠性、安全与成本准入条件后，逐步发布到生产；
9. 演练回滚或向前修复，并证明数据保持兼容。

模拟器与本地编排器很有用，却不是权威。提供方控制面、身份、配额、网络、重试与托管数据服务必须在接近生产的环境验证。反过来，也不要把纯领域测试搬进昂贵的云测试环境。

## 为每项兼容性主张注明日期 {#version-evidence}

| 组件 | 已检查版本或陈述 | 采用它的应用必须验证什么 |
| --- | --- | --- |
| .NET SDK | 10.0.301 | 锁定还原、Release 构建、测试与发布 |
| FSharp.Core | 10.1.301 | 解析后的依赖图与运行时兼容性 |
| Aspire.AppHost.Sdk | 13.5.2，发布于 2026-08-21 | 只有本地多服务编排确实值得时才采用，再测试启动与健康状态 |
| ASP.NET Core 基础镜像 | 10.0.10 | 镜像元数据、操作系统、架构、漏洞与容器启动 |
| Aspire CLI bundle/部署目标 | CLI 13.5.2 | 若采用，验证 bundle 输出与选定部署目标 |
| Azure Functions 隔离工作进程 | 文档列出 .NET 10 与 F# 绑定注意项 | 打包、模拟并部署真实触发路径 |
| AWS Lambda .NET 10 镜像/运行时路径 | 已审阅当前官方文档 | 打包、调用并部署真实处理器 |
| Kubernetes 探针/部署 | 已审阅当前官方语义 | 清单、集群行为与真实探针执行 |

版本回答“考虑了什么”，而不是“你的应用支持什么”。让提供方计划、区域、架构、触发器、集成包、CLI、基础层摘要与测试日期和证据放在一起。

## 开展范围受限的采用试验 {#adoption-spike}

选择一条代表性路径，而不是搭建预想中的最终全套系统：

- 一个具有真实公开/事件契约的 F# 服务或处理器；
- 一个带有重复和未知结果处理的持久副作用；
- 一条不复制长期凭据的身份与秘密流；
- 一个面向真实架构的镜像或部署包；
- 一组就绪、存活、关闭与依赖失败序列；
- 一条在目标后端实际查询过的遥测路径；
- 一个经过测量的伸缩或冷容量场景；
- 一次部署、部分发布、回滚或向前修复以及清理；
- 一项与观测用量核对过的成本估算。

比较最小托管服务、托管容器、Serverless 候选，并且只在 Kubernetes 真有可能时比较 Kubernetes。统计代码、清单、包、控制面对象、权限、流水线步骤、告警、升级责任、事故路径与删除工作。

试验应便于删除。把提供方类型留在领域核心外，保留普通宿主路径，并记录会推翻选择的条件。

## 避免常见云端错误 {#common-mistakes}

- 把容器本身当作运维模型或安全边界。
- 在识别集群级需求与所有者之前选择 Kubernetes。
- 一边称 Serverless 无状态，一边把业务真相保留在内存或 `/tmp`。
- 假定事件只执行一次、顺序执行并且只在一个实例上执行。
- 在结果未知后用新身份重试支付或写入。
- 把每个下游依赖都放进存活检查，制造重启风暴。
- 在没有访问控制时暴露健康详情、仪表板、OTLP 或管理端点。
- 把秘密、环境值、证书或提供方 ID 放进镜像。
- 晋升可变标签，或为每个环境分别重建。
- 忽略 OS、CPU 架构、非 root 用户、文件系统、信号与内存差异。
- 添加 Aspire 资源，却不理解连接、顺序、健康与生产所有权。
- 假定 AppHost 环境会自动成为子应用环境。
- 假定存在 OTLP 变量就表示 F# 服务已插桩或遥测已到达后端。
- 把绿色本地仪表板当作生产部署或探针配置。
- 抑制一大组警告，而不是记录一个注明日期的迁移提示。
- 让发布命令悄悄改写共享锁文件。
- 声称“.NET 支持”就证明有一流 F# 模板与绑定。
- 把本地模拟器当作提供方身份、重试、配额、网络或成本证据。
- 回滚代码却不检查 schema、消息、支付与其他不可逆副作用。
- 测量云成本，却忽略工程与事故责任。

## 练习 {#exercises}

### 练习 1：为三种工作负载选择计算模型 {#exercise-01}

分别评估以下工作负载：

1. 一个团队维护稳定的内部 HTTP API。它使用托管数据库，流量适中，没有自定义网络、边车或平台团队。
2. 图像元数据任务以尖锐突发到来。每项任务在数秒内完成，可能重复投递，下游媒体 API 还有速率限制。
3. 二十个受监管服务需要统一准入策略、私有网络、边车和受控多租户调度，组织内已有配备人员的 Kubernetes 平台。

为每项工作负载记录首选候选、被拒方案、证据缺口和反转条件。比较托管进程或容器、Serverless 与 Kubernetes；三个场景可以得到不同答案。

### 练习 2：把本地云样例变成发布提案 {#exercise-02}

设计把本地云样例中的 F# 服务部署到托管容器环境所需的最小工作。把提案分成四部分：

- **制品与供应链：** 架构、不可变镜像身份、注册表、SBOM、签名和漏洞策略。
- **运行契约：** 配置与秘密身份、Service Defaults 或替代遥测、生产探针、非 root/只读执行、资源限制和关闭。
- **发布路径：** 预发布冒烟、代表性负载、渐进发布、回滚和数据兼容。
- **所有权：** 成本、清理，以及负责各项运维响应的团队。

为每项声明注明证据来源：本章已经展示，或仍需在目标环境取得。

### 练习 3：设计幂等的 Serverless 预约消费者 {#exercise-03}

提供方事件至少投递一次 `BookingConfirmed`。处理器需要保留通知身份、调用邮件提供方、记录结果、重试瞬态故障、隔离毒消息，并能应对“邮件已被接受，成功记录尚未写入”时发生的崩溃。

设计应展示四部分：

- **核心状态：** F# 类型、持久状态转换、原子边界和并发控制。
- **提供方边界：** 邮件适配器，以及未知结果的对账方式。
- **运维策略：** 重试与死信、遥测、部署和回滚。
- **证明方式：** 覆盖重复投递、部分完成、毒消息和恢复的测试。

最后指出哪项保证必须由邮件提供方配合才能建立。

[阅读本章练习答案](../solutions/ch-42-cloud-containers-aspire).

## 本章回顾 {#chapter-review}

- F# 云端代码仍是普通 .NET 应用代码；部署改变外部契约，而不改变类型与函数的价值。
- 区分编译、发布布局、镜像、平台配置、运行实例与可观测发布证据。
- 根据触发、生命周期、状态、伸缩、控制与运维所有权选择计算模型。
- 容器打包进程；平台运行它；Serverless 定义调用模型；Aspire 声明应用模型。
- 保持配置外置、秘密不进制品、身份只拥有必要权限，并把持久状态放在临时实例之外。
- 把关闭视为尽力而为并传播取消；持久工作需要幂等性与恢复。
- 存活、就绪、启动、资源就绪、合成旅程与业务健康面向不同消费者和反应。
- 固定并检查基础镜像、架构、用户、端口、入口点、供应链证据与不可变摘要。
- Serverless 处理器需要薄提供方适配器、显式重试语义、重复处理、并发限制与经过测量的冷路径。
- 提供方“.NET 支持”不足以证明 F# 模板、绑定、代码生成或工具链。
- C# AppHost 可以成为围绕 F# 服务的职责明确的小型基础设施适配器。
- AppHost 是开发期编排器，不是生产运行时；本地资源健康不是生产探针配置。
- Service Defaults 是可选且由团队拥有的源码；仅注入环境变量不会为服务插桩。
- `aspire publish` 输出供后续步骤使用的制品，`aspire deploy` 应用目标流水线，CI/CD 仍负责治理。
- 晋升同一个不可变制品，设计兼容数据变更，观察渐进发布，并演练回滚或向前修复。
- 本地云样例只验证本地 F# 服务、C# AppHost、仪表板健康与镜像归档；所有提供方路径都未执行。

第 43 章从云端系统结构回到面向用户的 .NET 运行时：Avalonia 桌面应用、平台打包与移动支持的明确边界。
