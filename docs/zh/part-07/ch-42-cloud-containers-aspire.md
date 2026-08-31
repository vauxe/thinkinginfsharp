---
title: "第 42 章：云、容器、Serverless 与 .NET Aspire"
description: "根据进程、状态、伸缩、运维、安全与验证需求选择部署模型，并保持 F# 应用边界清晰。"
translationKey: part-07/ch-42-cloud-containers-aspire
---

# 第 42 章：云、容器、Serverless 与 .NET Aspire {#overview}

F# 服务以 .NET 进程运行在云端。部署之后，领域类型、函数、端口、取消、配置与传输契约继续定义应用。云产品负责这些边界外围的机器、网络、伸缩、身份和运维。

先说明部署问题，再选择产品与层次。容器负责打包进程，Kubernetes 等计算平台负责运行它。Serverless 改变执行与计费契约；Aspire 描述应用模型，并可驱动本地编排或特定目标的部署工作。生产环境仍由目标运行时承载。

从进程与制品逐层向外，既能看清 F# 设计，也能分清页内设计、可本地验证的事实与仍需在目标平台取得的证据。

本章术语来自三个层次：记录、可区分联合、函数和模块属于 F#；`WebApplication`、`HttpContext` 与容器发布属于 .NET/ASP.NET Core；Serverless、Kubernetes、探针、AppHost、SBOM 和渐进发布属于云平台或运维领域。后两类不是 F# 标准语法，只是 F# 应用会调用或面对的边界。

::: tip 分两轮阅读
初读时依次掌握[部署层次](#deployment-contracts)、[选型方法](#compute-decision-map)和[页内项目模板](#verified-slice)。准备真实部署时，再阅读[发布方案](#release-observe-rollback)、[分层验证](#evidence-ladder)和[采用试验](#adoption-spike)。
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
| VM 或直接管理的主机 | 遗留/原生依赖、特殊 OS 控制、稳定负载或迁移约束足以让团队直接管理主机 | 补丁、进程监管、容量、故障转移、证书、部署、遥测、备份、加固 |

这些是起点，不是排名。托管容器产品也许能缩到零；Serverless 产品也许接收容器镜像；Kubernetes 也许是托管的；VM 也能运行容器。应根据团队必须保留的职责决策，而不是根据营销分类决策。

## 页内项目模板：F# 服务与 Aspire AppHost {#verified-slice}

当前仓库已不包含原先的 `examples/ecosystem/cloud` 工程。本节保留一个可重建模板：一个 F# HTTP 服务、一个 C# 项目式 AppHost，不引入云账号或外部服务。它用于解释应用代码、开发期编排与容器打包的边界，不是当前仓库中的可执行项目，也不证明任何提供方部署。

重建时使用以下相对位置；这个布局使 AppHost 中的项目引用与后面的生成类型成立：

```text
CloudTemplate/
  CloudService.fsproj
  Program.fs
  AppHost/
    AppHost.csproj
    Program.cs
```

`CloudService.fsproj` 只编译根目录的 `Program.fs`。`AppHost.csproj` 引用上一级 F# 项目，Aspire SDK 才会生成 C# 代码所用的 `Projects.CloudService`。首次还原后，应分别生成并保留依赖锁，再用锁定模式验证。

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
Web SDK 以 `net10.0` 为目标。这里没有显式 `FSharp.Core` 包引用；默认版本来自所选 SDK，因此应检查锁文件中的实际解析结果，不能从正文猜一个版本。镜像基础层显式固定为 `mcr.microsoft.com/dotnet/aspnet:10.0.10`；采用前还应核对支持状态并最好固定摘要。浮动的 `10.0` 标签可能在提交未变时移动运行时，OS 与架构假设都必须在目标环境测试。

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
`CloudService.map` 只提供三个职责明确的 HTTP 端点：

- `/health/live` 表示进程能够响应；
- `/health/ready` 表示这个无依赖样例能接收流量；
- `/api/runtime` 返回受控教学值，默认为 `standalone`，不会转储环境变量。

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

项目启用 Aspire CLI bundle，并选择 `DnxPinned`。重建并还原这个版本时，SDK 应解析匹配的 `Aspire.Cli@13.5.2`，而不是要求全局安装 CLI；首次使用仍需要可用包源或已有缓存。这是可选的页内生态模板，不是书站依赖。

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
AppHost 为资源命名、显式声明 `http` 端点、注入 `DEPLOYMENT_MODE=aspire-local`，并附加 HTTP 健康检查。显式端点很重要：这个模板没有 `launchSettings.json` 提供端点元数据；省略 `.WithHttpEndpoint` 后，健康检查就没有可引用的 `http` 或 `https` 端点。

这是良好的语言互操作边界。F# 实现应用及其类型化行为，C# 只包含一小段基础设施 DSL；其当前模板、生成元数据与示例以 C# 为主。没有领域类型跨过此边界，因此替换编排工具无需重写服务。

### 重建后必须取得的证据 {#executed-evidence}

代码能读通不等于模板已经运行。重建后至少应完成以下检查：

- 用选定 SDK 生成锁文件，再以锁定模式还原并执行 Release 构建；
- 直接启动 F# 服务，确认三个端点分别返回 `healthy`、`ready` 与 `standalone`；
- 通过 AppHost 启动服务，确认资源健康检查完成，且 `/api/runtime` 返回 `aspire-local`；
- 为目标 OS/架构发布容器归档，检查基础层、摘要、非 root 用户、端口、入口点与架构；
- 在发布前后比较普通 `net10.0` 锁文件，防止容器参数悄悄改变依赖图；
- 在受限容器中实际启动镜像，并验证探针、取消、关闭和资源限制。

这些本地检查即使全部通过，也不证明注册表推送、签名、SBOM 证明、漏洞决策、云身份、外部依赖、负载、生产遥测、部署或回滚。本章没有执行这些操作。

### 本地 HTTP 是显式的测试例外 {#local-http-exception}

如果开发机没有受信任的 Aspire 开发证书，可以为一次受控实验显式启用不安全传输，并把 Dashboard、资源服务与 OTLP 端点全部固定在 `127.0.0.1`。所用环境变量、端口与清理步骤必须写进本地运行说明；更好的团队默认值仍是受信任的开发 HTTPS。

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

Kubernetes 明确赋予启动、存活与就绪探针不同作用。Aspire 也区分 AppHost 资源检查与服务端点检查。页内模板把 AppHost 检查连接到服务就绪 URL，但这段声明并不会配置生产负载均衡器。

让存活检查便宜，并与瞬态下游系统无关。就绪检查可以包含必需依赖，但无界依赖链可能在共享故障时让每个实例都不就绪。设置超时，避免泄露内部信息，控制缓存与暴露范围，并测试失败和恢复。

## 镜像是一项发布输入 {#image-contract}

.NET SDK 8.0.200 及以后版本能把受支持项目发布为容器，无需额外容器构建包。这让简单场景少写一个 Dockerfile，却不会消除镜像设计。

应记录并验证：

- SDK、目标框架、依赖锁、发布模式与容器工具版本；
- 基础镜像仓库、具体补丁版本或摘要、OS 发行版、架构与支持周期；
- 非 root 用户、可写路径、capability、环境、暴露端口、入口点与工作目录；
- 全球化、证书、时区数据、原生库、诊断以及内存/CPU 行为；
- 标签、许可证清单、SBOM、来源、签名、漏洞策略与例外所有者；
- 注册表目标、不可变摘要、保留、复制、访问、晋升与回滚。

标签是方便的名字；摘要标识镜像内容。让同一摘要依次晋升到测试、预发布与生产。日后重新构建“同一提交”，可能选择不同的基础镜像、包源、时钟或工具，从而产生不同制品。

多架构索引并不证明行为完全相同。原生库、全球化、JIT 行为、可用镜像与性能可能在 `amd64` 与 `arm64` 间不同。构建并冒烟每个实际部署架构，再在目标安全上下文与资源限制下测试。

### 避免发布命令悄悄改变锁文件 {#lock-file-lesson}

某些跨 OS/架构发布参数会触发带 RID 的还原，从而改变普通项目的包锁图。不要凭命令看起来“只是打包”就假定依赖未变；在发布前后比较锁文件，并再次执行普通 `--locked-mode` 还原。

当自包含或跨架构发布确实需要 RID 专用图时，应把它作为独立发布契约显式声明、锁定和测试。若只需让容器目标选择 Linux 镜像平台，则应选用不会意外改写开发依赖图的命令，并由干净工作树检查来证明这一点。

## Serverless 是调用契约 {#serverless-contract}

“Serverless”表示提供方管理更多执行机群，并提供更高层的调用、伸缩与计费模型。服务器依然存在，应用职责也依然存在。

当工作天然有限、稀疏或突发，提供方触发器能省去有意义的基础设施，缩到零可以接受，且团队能适应运行时限制时，可以选择函数或事件运行时。持续繁忙的低延迟 API、长连接、沉重的原生进程、稳定高吞吐工作进程，或需要复杂内存协调的工作流，通常更适合服务或容器。

### 用简短处理器连接纯决策 {#thin-handler}

采用下面的流程：

```text
提供方事件/绑定
  -> 验证并映射公开输入
  -> 纯 F# 决策或工作流
  -> 面向持久副作用的显式端口
  -> 提供方响应、确认、重试或死信结果
```

把提供方特性、触发器类型、上下文与 SDK 客户端留在边缘。核心应接收普通记录与联合，并返回声明过的决策。这样无需模拟器也能运行业务行为，也保留在函数与工作进程间迁移的可能。

只有理解绑定的失败语义时，绑定才真正减少代码。要问清哪个组件负责序列化、批处理、检查点、确认、重试、毒消息、部分批成功、并发、取消与遥测。若输出绑定隐藏了必须分类的错误，通过端口调用直接客户端 SDK 也许更安全。

### 假定会重试和重复投递 {#retries-duplicates}

提供方行为会随触发器和调用模式变化，但重复投递是正常可能性。AWS 明确记录事件源映射至少投递一次；Azure 的事件与重试指南同样要求考虑重复处理。“函数运行了一次”不是领域保证。

给每条命令或事件稳定身份。验证同一身份具有相同语义载荷。尽可能让外部操作与去重记录原子化。只有外部系统确认操作已提交后，才能记录成功。定义保留期、冲突、重放、部分批处理、重试耗尽与死信行为。

支付超时不代表支付失败。它可能是结果未知，需要在同一幂等键下查询或对账。用新身份重试可能造成重复扣款，不论函数扩展得多快。

### 冷启动、并发与限制都是设计输入 {#cold-start-concurrency-limits}

在所选计划与区域测量包大小、初始化、依赖连接建立、第一次调用、暖调用、扩展与尾延迟。不要承诺从另一种语言、包、计划或日期抄来的冷启动数字。

并发可能发生在实例内、实例间，或两者都有。静态可变值和本地缓存依照提供方工作进程模型共享，而不是每个逻辑事件独占。限制下游连接与请求速率；自动计算伸缩可能在帮助用户之前先压垮数据库或付费 API。

每个提供方都会定义受支持 .NET 版本、CPU 架构、时长、内存、临时存储、载荷、网络、并发、重试与部署包限制。把这张矩阵当作注明日期的依赖，并测试实际触发器与所选托管计划。

### 提供方 .NET 工作进程中的 F# {#fsharp-provider-workers}

提供方宣传“.NET”并不能证明存在完善的 F# 模板、分析器、生成绑定、本地工具、Native AOT 行为或文档。F# 可以使用常规 .NET 库，但代码生成和工具 API 可能偏向某一种语言。

截至 2026-08-31，Azure Functions 4.x 隔离工作进程文档列出 .NET 10，并注明 F# 应用对某些绑定扩展可能需要显式注册。文档还记录计划专用限制与最低工作进程包版本。本章审阅了这些资料，但没有构建或部署 Azure Function。

AWS 当前记录 .NET 10 Lambda 基础镜像与 .NET 打包路径，但术语和示例主要使用 C#。只有当 F# 项目试验验证处理器发现、序列化器行为、包、架构、本地调用、冷路径、部署与遥测后，编译后的 F# 处理器才是候选。页内模板没有实现 Lambda 处理器，也没有执行其中任何步骤。

不要为了逃避部署知识而选择 Serverless。它会增加提供方运行时、触发契约、身份、限制、计费维度、本地模拟器/工具和事件失败语义。只有这些新增项比其消除的自有基础设施风险更小，才应该选择它。

## Aspire 描述系统结构，但不会消除其复杂性 {#aspire-model}

Aspire AppHost 是声明资源与关系的代码。在 run 模式下，Developer Control Plane 启动和监控本地进程或容器，分配端点，注入配置，并向仪表板提供信息。官方架构指南明确说明 AppHost 不是生产运行时。

### 资源、引用与顺序 {#resources-references-ordering}

`AddProject`、`AddContainer` 与托管集成把资源加入应用模型。`WithReference` 表达关系，并可注入连接或端点信息。`WaitFor` 控制启动就绪顺序。三者解决不同问题：引用不会自动成为等待，任何一个也都不是生产授权策略。

Aspire 集成是教 AppHost 如何表示和连接资源的包。添加数据库集成也许会启动本地容器、连接现有服务，或参与部署。它不会决定谁管理 schema、事务边界在哪里，也不会决定备份、容量、故障转移、数据分类或删除策略。

把资源命名成稳定的运维概念。把生成的连接数据当作服务边界处的配置。让领域代码不知道 Aspire 资源类型，这样测试与替代宿主仍能直接构造同样的端口。

### 两套健康系统 {#two-health-systems}

AppHost 资源健康回答编排是否认为资源已就绪，包括依赖方的 `WaitFor` 能否继续。服务端点健康回答生产平台是否应向某个运行实例发送流量，或者重启它。

Aspire 仪表板可以显示 AppHost 声明的 HTTP 资源检查；重建模板时应验证该连线。生产环境仍然需要平台探针配置，指向正确的服务路径、端口、超时、阈值与安全边界。把显示正常的仪表板截图复制进运行手册，并不会生成这些配置。

### Service Defaults 是源码，不是魔法 {#service-defaults}

当前 C# Service Defaults 模板组合 OpenTelemetry、健康检查、服务发现与标准 `HttpClient` 韧性策略。它是可定制的共享项目。只有调用 `AddServiceDefaults` 和 `MapDefaultEndpoints` 才会安装这些行为；仅仅在 AppHost 下运行不会自动插桩应用。

页内模板刻意省略 Service Defaults。即使 AppHost 注入 OTLP 相关环境变量，F# 服务没有 OpenTelemetry SDK/导出器包也不会因此自动产生跟踪或指标。它的健康端点是显式教学处理器，不是 ASP.NET Core `IHealthCheck` 注册。

真实 F# 解决方案可以从三条可行路径中选择：

1. 引用小型 C# Service Defaults 适配器，并从 F# 组合根调用其公开扩展；
2. 用 F# 直接重现真正需要的注册，并锁定包和测试；
3. 创建语言中立的共享库，有意让公开 API 同时对 F# 与 C# 友好。

不要复制一次模板后就忘记它。团队必须定义并维护重试策略、超时、端点暴露、插桩源、导出器行为、采样、包升级与生产后端验证。

## 本地编排与部署是不同模式 {#local-versus-deployment}

当前 Aspire 部署采用流水线模型。部署目标或计算环境会向应用模型贡献目标专用步骤。

- `aspire publish` 求值 AppHost 并输出制品，留给后续工具或人应用；它是单向交接。
- `aspire deploy` 求值模型、解析参数、生成目标输出并直接应用。
- 当 CI/CD 需要拆分流程时，`aspire do <step>` 调用具名流水线步骤。

这些命令需要适当的 CLI 与目标集成。页内模板只声明固定 CLI bundle 和开发期编排，没有配置部署目标，也没有给出 publish 或 deploy 流程。即使 AppHost 构建通过，也只能算本地应用模型的证据。

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

1. 纯决策/幂等测试，再到配置、事件、取消与失败映射的适配器测试；
2. 锁定的 Release 构建/发布，以及镜像元数据、架构、SBOM、签名和漏洞检查；
3. 受限容器启动、探针、关闭与 AppHost/模拟器连线；
4. 在目标平台预发布环境验证真实身份、网络、数据、遥测、伸缩与故障注入；
5. 通过生产准入条件逐步发布，再演练数据兼容的回滚或向前修复。

模拟器不能证明提供方控制面、身份、配额、网络、重试或托管数据行为；这些必须在接近生产的环境验证。纯领域测试则无需搬进昂贵云环境。

## 为每项兼容性主张注明日期 {#version-evidence}

| 组件 | 已检查版本或陈述 | 采用它的应用必须验证什么 |
| --- | --- | --- |
| .NET SDK | 本地编辑环境为 10.0.302；模板目标为 `net10.0` | 选定并固定 SDK，再做锁定还原、Release 构建、测试与发布 |
| FSharp.Core | 模板没有显式固定 | 检查锁文件中的解析版本与运行时兼容性 |
| Aspire.AppHost.Sdk | 模板固定 13.5.2；2026-08-31 NuGet 列出 13.5.3 | 只有本地多服务编排确实值得时才采用，再测试启动与健康状态 |
| ASP.NET Core 基础镜像 | 模板固定标签 10.0.10 | 核对支持状态与摘要，再验证元数据、操作系统、架构、漏洞与容器启动 |
| Aspire CLI bundle/部署目标 | 模板请求 CLI 13.5.2；未执行 | 若采用，验证 bundle 输出与选定部署目标 |
| Azure Functions 隔离工作进程 | 文档列出 .NET 10 与 F# 绑定注意项 | 打包、模拟并部署真实触发路径 |
| AWS Lambda .NET 10 镜像/运行时路径 | 已审阅当前官方文档 | 打包、调用并部署真实处理器 |
| Kubernetes 探针/部署 | 已审阅当前官方语义 | 清单、集群行为与真实探针执行 |

模板保留 13.5.2，是为了让 `AppHost.csproj` 与其 CLI bundle 保持一致；这不是当前运行证据。采用 NuGet 列出的 13.5.3 时，应同时更新 SDK 与匹配的 CLI，再重复从还原到部署的检查。版本只记录“考虑了什么”，不代表应用支持；证据必须带上提供方计划、区域、架构、触发器、包、CLI、基础层摘要与日期。

## 开展范围受限的采用试验 {#adoption-spike}

选择一条可删除的代表路径，覆盖：

- 真实服务/事件契约，以及处理重复和未知结果的持久副作用；
- 不复制长期凭据的身份与秘密流，以及真实部署包；
- 就绪、存活、关闭、依赖失败和已查询的遥测；
- 经测量的容量，以及与观测用量核对过的成本；
- 部署、部分发布、回滚或向前修复与清理。

比较最小托管服务、托管容器、Serverless 候选，并只在合理时比较 Kubernetes。统计代码、基础设施、权限、流水线、告警、升级、事故与删除工作。把提供方类型留在核心外，保留普通宿主路径，并记录反转条件。

## 练习 {#exercises}

### 练习 1：为三种工作负载选择计算模型 {#exercise-01}

分别评估以下工作负载：

1. 一个团队维护稳定的内部 HTTP API。它使用托管数据库，流量适中，没有自定义网络、边车或平台团队。
2. 图像元数据任务以尖锐突发到来。每项任务在数秒内完成，可能重复投递，下游媒体 API 还有速率限制。
3. 二十个受监管服务需要统一准入策略、私有网络、边车和受控多租户调度，组织内已有配备人员的 Kubernetes 平台。

为每项工作负载记录首选候选、被拒方案、证据缺口和反转条件。比较托管进程或容器、Serverless 与 Kubernetes；三个场景可以得到不同答案。


::: details 参考答案

#### 场景 A：一个稳定的内部 HTTP API {#exercise-01-case-a}

从组织支持的最小托管应用平台开始。如果该路径能接收锁定的 .NET 发布制品，并提供所需的运行时、路由、身份、健康、日志与伸缩控制，容器目前不会增加业务能力。

如果组织已经按镜像摘要晋级、要求镜像安全门禁、需要让同一制品运行在多个兼容平台，或代码部署的构建过程不透明，则改选托管容器。两者差异在制品与运维职责，不在 F# 领域模型。

不把 Serverless 作为首选，因为 API 流量稳定、要求持续可用，也不是间歇出现的有限事件。函数层会增加触发器和运行时限制，以及第二套托管契约，却没有明确的缩到零或事件集成收益。

不选择 Kubernetes，因为没有集群级需求，也没有团队负责运营平台。一个 Deployment 和 Service YAML 文件无法涵盖升级、入口、证书、策略、容量、存储、遥测、租户与事故响应。

第一个验收切片包含：

- 锁定 Release 构建与一个不可变发布制品或镜像摘要；
- 对数据库端点、身份与非秘密配置的启动验证；
- 使用托管身份访问数据库，不复制凭据；
- 分离的存活与就绪行为，包括数据库丢失和恢复；
- 请求取消、优雅排空、资源限制与代表性负载测试；
- 生产遥测查询、告警路径、分阶段发布、回滚与成本观测。

当制品漂移、原生依赖、平台可移植性或供应链策略变得重要时，从代码部署转到容器。当已验证需求超出所选托管产品的网络、伸缩、身份、运行时或运维限制时离开它。不要只因出现第二个服务就迁移。

#### 场景 B：突发的图像元数据事件 {#exercise-01-case-b}

如果实际提供方触发器能缓冲突发，每个条目都符合测得的时长、内存与包限制，而且缩到零能显著降低闲置成本，就从 Serverless 事件工作进程开始。函数只是独立 F# 决策与限速媒体客户端端口之外的薄适配器。

队列事件必须携带稳定条目/事件 ID 与不可变对象版本。为每个语义身份持久化一条处理状态。并发重复要么观察现有结果，要么通过原子创建/比较操作竞争。绝不能把新的调用 ID 当作幂等键。

把并发限制在媒体 API 的安全速率以下。自动伸缩不代表可以任意增加下游流量。同时使用提供方并发控制与客户端限流器；把限流视为规定的瞬态结果，并采用带抖动的退避。定义最大事件年龄、重试耗尽、毒消息验证、部分批响应与死信重放。

首先排除 Kubernetes，因为没有功能需要集群。只有 Serverless 的冷启动、运行限制、成本和本地与部署工具都通过试验，才排除持续运行的工作服务。

当流量变得稳定、冷启动/尾延迟违反目标、提供方时长或包限制约束处理、连接复用占主导，或每次调用成本超过预留容量时，转向托管工作进程/容器。队列在迁移后仍有价值，因为 F# 核心与持久事件契约不依赖处理器运行时。

试验测量冷启动与预热后延迟、突发排空时间、重复率、下游限流、内存、包大小、重试与死信行为、遥测和成本。它要把实际 F# 包部署到提供方预发布环境；仅使用模拟器不够。

#### 场景 C：已有平台上的二十个受监管服务 {#exercise-01-case-c}

Kubernetes 是首选，因为该场景既给出了具体集群级需求，也有配备人员的平台。准入策略、私有网络、批准的边车、租户调度与统一运维控制已经是平台职责，而非假想功能。

这不代表要使用每项 Kubernetes 功能。每个 F# 服务仍应是可独立测试的标准进程，具有明确的 HTTP 或事件契约、外置状态、取消、健康检查与固定的非 root 镜像。领域项目不引用 Kubernetes API。

平台契约应定义：

- 批准的基础镜像、注册表、签名、SBOM 与漏洞例外；
- namespace/租户边界、服务账号、工作负载身份、秘密与网络策略；
- request、limit、中断预算、自动伸缩信号、配额，以及谁负责容量；
- 启动、存活、就绪、终止宽限、排空与发布策略；
- 入口、证书、服务发现、出站、数据服务、备份与恢复；
- 日志、指标、跟踪、审计、告警路由、服务目标与成本分摊；
- 集群和工作负载升级节奏、兼容性测试与事件升级路径。

团队明确采用当前部署集成时，Aspire 可以改善本地拓扑并输出目标制品。它不能替代平台策略、清单审阅、集群凭据、分阶段应用或回滚验证。

Serverless 仍可能适合一个独立事件入口，托管服务也可能承载独立工作负载。答案不是“二十个都必须用 Kubernetes”。只有平台控制与明确职责能降低总风险时，共享平台才是默认选项。

当平台增加的延迟、成本、耦合或事故负担超过策略价值时，可以迁出单个工作负载。只有共享监管控制已有可验证迁移路径时，才重新考虑整个平台；不能只凭更便宜的计算报价决定。

:::

### 练习 2：把页内云模板变成发布提案 {#exercise-02}

设计把页内模板中的 F# 服务部署到托管容器环境所需的最小工作。把提案分成四部分：

- **制品与供应链：** 架构、不可变镜像身份、注册表、SBOM、签名和漏洞策略。
- **运行契约：** 配置与秘密身份、Service Defaults 或替代遥测、生产探针、非 root/只读执行、资源限制和关闭。
- **发布路径：** 预发布冒烟、代表性负载、渐进发布、回滚和数据兼容。
- **责任：** 成本、清理，以及负责各项运维响应的团队。

为每项声明注明证据来源：来自页内代码设计、本地重建检查，或仍需在目标环境取得。


::: details 参考答案

#### 先建立可重复的本地基线 {#exercise-02-baseline}

提出发布方案前，重建后的模板必须先验证：

- F# 服务与 C# AppHost 在团队支持的环境中从锁文件还原并构建；
- 直接服务与 Aspire 编排服务都能回答三个端点；
- AppHost 的本地资源检查会变为健康；
- SDK 为目标架构生成镜像归档，并能检查其元数据；
- 基础层摘要、镜像用户、端口、架构与入口点符合书面契约；
- 容器命令运行后，常规包锁保持不变。

这些本地检查仍未验证目标平台、云身份、注册表、生产探针、遥测导出、负载、安全策略、发布或回滚。发布提案应从这里继续，不能把本地结果重新包装成生产结论。

#### 定义一个不可变制品 {#exercise-02-artifact}

选择托管环境支持的 CPU 架构。使用团队固定的 .NET 10 SDK、锁定依赖与固定基础层摘要在 CI 构建该架构，并在打包前运行完整测试套件。生成镜像摘要、SBOM、来源记录、许可证清单与漏洞报告。

只向受限注册表推送一次。使用组织批准的身份为摘要签名或生成证明。晋级记录引用该摘要，不能只引用 `latest`、分支或提交。注册表保留策略必须同时保留当前摘要与回滚摘要。

策略门禁检查基础层支持、非 root 用户、意外的可写或特权要求、端口与入口点、原生库、架构、机密、严重性例外和签名。可复现重建很有用，但不能替代按摘要晋级。

#### 清楚写出运行时与安全契约 {#exercise-02-runtime}

在本地或 CI 按目标架构和 UID 运行镜像。配置只读根文件系统、临时可写挂载、丢弃的 Linux capability、CPU 与内存限制，以及端口 8080。验证启动、全部 API 响应、存活、就绪、信号驱动排空、强制终止与重启恢复。

平台服务账号只取得模板所需权限。模板没有数据依赖，因此不虚构数据库或机密。通过带版本的配置加入非机密 `DEPLOYMENT_MODE`。未来需要机密时，使用平台机密管理或工作负载身份，并在不打印其值的情况下测试轮换。

只通过预期平台路由暴露应用流量与探针路径。保护或隔离运维端点。管理与遥测连接使用加密和认证；不能把匿名回环仪表板例外复制到部署中。

#### 只加入团队能够维护的服务默认项 {#exercise-02-observability}

一种方案是提供可由 F# 调用的小型 C# Service Defaults 适配器。另一种方案是直接用 F# 注册 OpenTelemetry 与 ASP.NET Core 健康检查。无论哪种方案，都要锁定新增包，并写清重试与超时策略。不要未经审阅就接受模板默认行为。

映射廉价存活检查，以及只包含真正必需依赖的就绪检查。为托管平台明确配置调用间隔、超时、阈值、启动余量与终止/排空行为。测试失败与恢复，而不只是 200 响应。

从预发布环境把安全日志、请求、错误与延迟指标、追踪、运行时指标、发布摘要和环境身份发送到真实生产遥测后端，并实际查询。触发测试告警，确认由谁响应。定义采样、个人数据策略、保留、访问与预期摄取成本。

#### 把部署生成与批准分开 {#exercise-02-pipeline}

最简单提案可能完全不需要 Aspire 部署：CI 系统可通过托管平台支持的声明式接口部署一个摘要，同时保留 AppHost 做本地编排。

若采用 Aspire 部署，就固定并安装 CLI，加入选定的目标集成，用 `aspire publish` 生成目标输出，审阅后由受保护部署阶段应用。只有该阶段明确授予 Aspire 直接应用权限时，才使用 `aspire deploy`。无论哪种方式，CI/CD 都负责审批、身份、环境保护、日志与保留。

预发布与生产使用不同配置，却使用相同摘要。预发布门禁执行 socket/TLS、身份、健康、遥测、重启、资源限制与代表性负载测试。把平台 revision 与目标配置和摘要一同捕获。

#### 安全地发布和反转 {#exercise-02-rollout}

先向一小部分流量或 revision 发布。根据错误率、尾延迟、就绪抖动、重启、资源压力、一个合成请求与成本决定是否扩大。在部署前就定义数值与观察窗口。

回滚把流量路由到保留的前一个摘要与兼容配置。这个模板没有数据迁移，因此练习中的反转较简单；加入第一个持久依赖时，必须补上模式兼容与向前修复分析。用一个有意不健康的候选演练回滚。

观察窗口结束后，保留发布记录，删除失败 revision 与无用临时资源，并核对注册表、遥测、出站和计算成本。清理属于提案的一部分，因为废弃环境既产生费用，也扩大攻击面。

:::

### 练习 3：设计幂等的 Serverless 预约消费者 {#exercise-03}

提供方事件至少投递一次 `BookingConfirmed`。处理器需要保留通知身份、调用邮件提供方、记录结果、重试瞬态故障、隔离毒消息，并能应对“邮件已被接受，成功记录尚未写入”时发生的崩溃。

设计应展示四部分：

- **核心状态：** F# 类型、持久状态转换、原子边界和并发控制。
- **提供方边界：** 邮件适配器，以及未知结果的对账方式。
- **运维策略：** 重试与死信、遥测、部署和回滚。
- **验证方式：** 覆盖重复投递、部分完成、毒消息和恢复的测试。

最后指出哪项保证必须由邮件提供方配合才能建立。


::: details 参考答案

#### 分开建模事实、尝试与不确定性 {#exercise-03-model}

用业务事件、渠道、收件人与模板版本派生稳定通知身份，不要使用提供方生成的调用 ID：

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

`Accepted` 只表示提供方接受了消息，不代表收件人已经收到或阅读。`OutcomeUnknown` 不等于 `Rejected`。载荷哈希防止同一身份在不报错的情况下对应不同收件人或内容。

纯核心只返回以下六种决策之一：

- `IgnoreAccepted`；
- `RejectConflict`；
- `AcquireAttempt`；
- `ReconcileUnknown`；
- `RetryTransient`；
- `RejectPermanent`。

提供方事件、时钟、存储版本与邮件响应都通过参数传入。存储与邮件调用仍隔离在端口之后。

#### 在外部副作用前后持久化 {#exercise-03-persistence}

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

原子创建或比较可防止两个并发处理器同时取得同一次发送资格。租约到期允许崩溃恢复；栅栏阻止较晚完成的旧工作进程覆盖新结果。保留期必须覆盖源端最长重放期与业务审计窗口。

邮件提供方与本地存储之间仍存在双写缺口。如果提供方接受消息后，进程在提交 `Accepted` 前死亡，本地状态就不确定。

F# 类型或本地事务无法单方面保证邮件只发送一次。它需要提供方配合：使用稳定幂等键持久去重，或支持按该键或消息 ID 查询状态。两者都没有时，对账只能按照明确的业务策略，在可能重复发送与可能漏发之间取舍。

#### 分类重试与毒消息 {#exercise-03-retries}

格式错误的 schema、不受支持版本、无效收件人以及身份/载荷冲突都会导致永久失败或毒消息。记录安全诊断并路由到死信/隔离路径，不做无尽重试。

提供方超时、限流与部分 5xx 响应可能是瞬态候选，但必须先核对提供方契约。使用有界指数退避加抖动、最大事件年龄、尝试次数与并发限制。发送后的超时进入 `OutcomeUnknown`，而不是自动重新发送。

当运行时支持时，部分批处理只报告失败条目，使一个毒性事件不会重放已成功同批条目。死信重放是一条保留原始身份与载荷的受审计命令，不是复制成新键。

同时限制函数并发与提供方调用。自动伸缩不能超过提供方配额或存储容量。发出队列年龄与限流信号，让延迟工作在重试过期前可见。

#### 从核心到提供方逐层验证 {#exercise-03-evidence}

纯测试覆盖首次事件、已接受重复事件、冲突哈希、并发租约决策、瞬态重试、永久拒绝、陈旧工作进程完成与未知结果对账。

存储契约测试验证原子创建、条件更新、租约到期、栅栏与保留。适配器测试覆盖规定的提供方请求、幂等头、状态与错误映射、取消、接受后的超时，以及脱敏诊断。事件测试样例覆盖缺失、多余、null、超大、旧版本与未来版本输入。

目标提供方预发布测试发送重复和并发事件，在外部调用附近杀死处理器，观察重试/死信行为，演练提供方查询/幂等，并查询遥测。测量冷/暖延迟、队列年龄、伸缩、下游速率与成本。

用锁定的工作进程/绑定版本、最小权限身份、加密配置、并发与重试策略、告警，以及禁用或零并发紧急停止机制部署一个不可变包。通过事件源分区、别名、版本或提供方支持的流量控制渐进发布。

回滚必须继续读取新版本写入的状态，而且不能重置通知身份。若某个 schema 或状态转换不向后兼容，就暂停消费并采用向前兼容修复，而不是盲目启用旧代码。

最终保证刻意保持有限：每项语义通知都会进入有记录的终态；提供方契约允许时会抑制重复；结果不明时可以发现并对账。收件人是否收到，以及外部邮件是否只发送一次，都不受消费者单方面控制。

:::


## 资料来源 {#sources}

- [Microsoft Learn：Aspire 架构与开发期编排](https://learn.microsoft.com/en-us/dotnet/aspire/architecture/overview)
- [Microsoft Learn：AppHost 配置](https://learn.microsoft.com/en-us/dotnet/aspire/app-host/configuration)
- [NuGet：Aspire.AppHost.Sdk 版本](https://www.nuget.org/packages/Aspire.AppHost.Sdk)
- [Microsoft Learn：.NET 容器基础](https://learn.microsoft.com/en-us/dotnet/core/containers/overview)
- [Microsoft Learn：Azure Functions .NET 隔离工作进程指南](https://learn.microsoft.com/en-us/azure/azure-functions/dotnet-isolated-process-guide)
- [Kubernetes：存活、就绪与启动探针](https://kubernetes.io/docs/concepts/configuration/liveness-readiness-startup-probes/)

第 43 章从云端系统结构回到面向用户的 .NET 运行时：Avalonia 桌面应用、平台打包与移动支持的明确边界。
