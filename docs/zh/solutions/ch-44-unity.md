---
title: "第 44 章练习答案"
description: "选择合乎比例的 F#/C# Unity 边界，通过诚实的 IL2CPP 证据计划提升 X44，并在不隐藏 AOT 风险的前提下设计带版本任务数据。"
translationKey: solutions/ch-44-unity
kind: solution
part: 7
chapter: 44
status: complete
verifiedWith:
  fsharp: "10"
  dotnetSdk: "10.0.301"
exampleIds:
  - ecosystem-unity-fsharp-plugin
  - ecosystem-unity-csharp-adapter
exerciseIds:
  - ch44-exercise-01
  - ch44-exercise-02
  - ch44-exercise-03
termIds: []
sources:
  - id: unity-6000-3-22
    url: https://unity.com/releases/editor/whats-new/6000.3.22f1
    checked: "2026-08-25"
  - id: unity-dotnet-profile
    url: https://docs.unity3d.com/Manual/dotnet-profile-support.html
    checked: "2026-08-25"
  - id: unity-plugin-inspector
    url: https://docs.unity3d.com/Manual/plug-in-inspector.html
    checked: "2026-08-25"
  - id: unity-serialization
    url: https://docs.unity3d.com/Manual/script-serialization-rules.html
    checked: "2026-08-25"
  - id: unity-il2cpp
    url: https://docs.unity3d.com/Manual/il2cpp-introduction.html
    checked: "2026-08-25"
  - id: unity-scripting-restrictions
    url: https://docs.unity3d.com/Manual/scripting-restrictions.html
    checked: "2026-08-25"
  - id: unity-stripping-configure
    url: https://docs.unity3d.com/Manual/managed-code-stripping-configure.html
    checked: "2026-08-25"
  - id: unity-link-xml
    url: https://docs.unity3d.com/Manual/managed-code-stripping-xml-formatting.html
    checked: "2026-08-25"
  - id: unity-testing
    url: https://docs.unity3d.com/Manual/testing-editortestsrunner.html
    checked: "2026-08-25"
  - id: unity-command-line-build
    url: https://docs.unity3d.com/Manual/build-command-line.html
    checked: "2026-08-25"
  - id: unity-gc-tracking
    url: https://docs.unity3d.com/Manual/performance-track-garbage-collection.html
    checked: "2026-08-25"
  - id: unity-il2cpp-stack-traces
    url: https://docs.unity3d.com/Manual/il2cpp-managed-stack-traces.html
    checked: "2026-08-25"
  - id: unity-burst-language
    url: https://docs.unity3d.com/Packages/com.unity.burst@1.8/manual/csharp-language-support.html
    checked: "2026-08-25"
  - id: fsharp-component-guidelines
    url: https://learn.microsoft.com/dotnet/fsharp/style-guide/component-design-guidelines
    checked: "2026-08-25"
---

# 第 44 章练习答案 {#overview}

这些解答是边界设计，而不是某种语言最适合每个 Unity 子系统的声明。每个答案都会说明 F# 增加的价值、仍由 Unity 拥有的表面、能反转选择的证据，以及自动 .NET 结果停止生效的确切位置。

## 练习 1：为三种产品选择语言边界 {#exercise-01}

三种产品的主导风险不同。复用同一种语言划分是在优化一致性，而不是优化产品。

### A. 回合制战术与确定性重放 {#turn-based-tactics}

**第一边界：** 很薄 C# Unity 呈现/资源适配器后的 F# 模拟/领域程序集。

F# 应拥有：

- 经过验证的单位、技能、格子、阵营、资源与效果标识符；
- 合法行动生成、命令验证、战斗结算、回合顺序、胜利条件与 AI 评估输入；
- 从先前状态、命令、随机流/种子和规则版本到下一状态加事件的确定性状态转换；
- 以命令、种子、内容版本和校验和构成的重放序列化，而不是场景快照；
- 作为数据并按带版本 schema 与能力策略验证的 mod 内容；
- 守恒、边界、回合合法性与重放等价性的属性测试。

C# 应拥有场景对象、动画、摄像机、输入、音频、视觉效果、可寻址资源、Inspector 字段，以及从发出的领域事件到呈现的映射。动画完成可以发送呈现消息，但绝不能成为战斗结果的权威。

暴露 `ValidateCommand`、`Apply` 和 `TryLoadReplay` 之类普通 CLR 调用，并使用小型 DTO 或数组。把 F# union 与 map 留在内部。若模拟状态很大，不要每帧搬运整个图；回合结算是事件边界，所以交换一个命令和紧凑事件/结果批次。

**Mod 边界：** 接受声明式内容，而非任意下载的托管程序集。进入对局前验证标识符、限制、引用、本地化键、确定性表达式与规则版本。可执行 mod 会引入远超 F# 语言选择的信任、平台、AOT、签名、商店与反作弊问题。

**证明矩阵：** 纯重放/属性测试；C# 消费者编译；精确插件/依赖检查；Unity 导入；在 Play Mode 重放一个场景；跨重载保存/加载；相关时的 Mono 诊断 Player；以至少一个旧重放和错误 mod 覆盖发布 IL2CPP 架构；与独立服务端/工具实现校验和一致；目标性能与内存。

**首先拒绝的替代：** 直接 F# `MonoBehaviour` 会把确定性规则耦合到场景和 Inspector 表示；把战斗权威放进动画回调会破坏重放；把每个呈现事件都放入 F# DLL 会增加没有领域价值的引擎细节。

**反转条件：** 若跨语言状态转换主导回合预算、调试无法支持，或 IL2CPP 在必需库路径失败，就缩小 F# 边界。保留线协议/重放契约，使实现移动时不使存档失效。

### B. 使用 Jobs 与 Burst 的主机动作游戏 {#console-action-game}

**第一边界：** C# 拥有帧关键 Unity/DOTS/Burst 应用。F# 可选地用于较慢元游戏规则、构建工具、与后端共享的验证或离线分析——第一天不要求 Player 依赖它。

主导约束是实体数量、数据布局、Burst 的 HPC# 子集、调度、原生平台 SDK、设计师工作流、帧时间与主机认证。仅把源码放进另一种语言并不会改善它们。

若成长、物品栏、经济、任务规划或匹配规则变复杂，可在事件边界增加小型 F# 领域插件。批处理前后交换扁平数组或紧凑 struct，而不是在 job 内交换回调或 F# collection。让 C# job 拥有 `NativeArray`、组件数据、特性、安全句柄、调度与 Burst 编译。

**证明矩阵：** 目标 development Player 中的代表实体负载；CPU/GPU 时间线、`GC.Alloc`、job 依赖、同步点、内存带宽、热行为与帧分位数；每个主机架构上的 IL2CPP 加 Burst AOT；平台 SDK 回调；符号与崩溃捕获；认证构建路径。任何 F# 元游戏插件还要加入第 44 章的依赖/导入/AOT 行。

**首先拒绝的替代：** 围绕每个 job 的 F# 包装增加互操作却不移动业务决策；直接 F# Burst 标注没有证据；每帧跨边界复制大型不可变世界快照违背数据导向设计。

**反转条件：** 若剩余规则一直很薄，或语言边界使主机支持变复杂，就完全不增加 F#。只有已证明非热子系统的模型/测试收益超过装配与调试成本时，才把它移到 F#。

### C. 无头 Editor 内容流水线 {#editor-content-pipeline}

**第一边界：** Unity-independent 的 F# 验证/报告类库，加小型 C# Editor 与 batch-mode 适配器。

F# 应拥有从稳定 DTO 解析对话图、引用检查、循环/可达性规则、本地化覆盖、确定性报告行、严重级别分类，以及由显式输入驱动的纯生成。普通 .NET 测试可使用小型 fixture，而无需启动 Unity。

C# 应拥有 `AssetDatabase`、导入回调、GUID/路径查找、`UnityEditor` 进度与取消、菜单/EditorWindow UI、Console 诊断，以及 Unity 调用的静态批处理入口方法。它会先把资源数据快照为脱离引擎的 DTO，再调用 F#，并把发现映射回资源路径与行/节点标识。

使用一次命令行调用，并带确切 Editor、`-batchmode`、`-quit`、`-projectPath`、`-executeMethod`、涉及构建时的 target/profile 和 `-logFile`。验证失败返回非零进程码，并与基础设施失败使用可区分代码。还要发出机器可读报告，使 CI 不必抓取本地化 Console 文本。

**证明矩阵：** 纯 fixture；交互 Editor 选择与取消；导入/重新导入无递归；干净项目导入；同一提交的批处理运行；两次干净运行的确定性报告比较；错误和巨大图；本地化编码；日志/报告保留；包更新；Player 运行时排除 Editor-only 程序集。

**首先拒绝的替代：** 让 F# 类库引用 `UnityEditor` 会阻碍廉价测试并扩大版本耦合；不用 Unity 而让 `dotnet` 直接处理原始 `.meta` 文件，可能误解已导入资源状态；把 Editor UI 作为唯一入口会阻塞 CI。

**反转条件：** 如果规则从根本上依赖活的 Editor 对象，且 DTO 映射比规则更大，就把该规则移入 C#。只有源格式、GUID 解析与导入语义真正独立时，才把整个验证器移出 Unity。

## 练习 2：把 X44 提升为 IL2CPP 垂直切片 {#exercise-02}

目标不是增加很多功能，而是用可复现证据把每个缺失边界执行一次。

### 建议项目图 {#vertical-slice-graph}

使用两个构建根与一个复制产物契约：

```text
src/FSharpGameplay/                 # 现有锁定 netstandard2.1 项目
artifacts/unity-plugin/             # 生成且带哈希的 DLL/PDB bundle
unity/FSharpVerticalSlice/
  Assets/Plugins/ThinkingInFSharp/  # 复制的 FSharpGameplay.dll + FSharp.Core.dll
  Assets/Scripts/                   # UnityAdapter.cs + 输入适配器
  Assets/Tests/EditMode/            # 映射/导入测试
  Assets/Tests/PlayMode/            # 场景/生命周期测试
  Assets/Linker/link.xml
  Assets/Settings/Build Profiles/   # 版本化 macOS ARM64 profile
  Packages/manifest.json + packages-lock.json
  ProjectSettings/ProjectVersion.txt and Player settings
```

不要提交 `Library`、`Temp` 或本地构建输出。要么提交导入插件二进制并提供文档化更新命令，要么在 Unity 导入前生成它们；无论选择哪种策略，CI 都必须比较哈希并拒绝过时/混合文件。

### 产物与程序集契约 {#artifact-contract}

使用仓库锁定 .NET SDK 与 `dotnet restore --locked-mode` 构建，再执行 Release `--no-restore`。精确复制：

- `FSharpGameplay.dll`；
- 从包 10.1.301 解析并位于旁边的 `FSharp.Core.dll`；
- 诊断构建所需 portable PDB；
- 同一提交中的 `UnityAdapter.cs` 与 `link.xml`；
- 包含 SHA-256、文件大小、包版本、插件程序集身份、FSharp.Core 程序集身份、提交与构建命令的生成 manifest。

任何必需文件或哈希不同时，在启动 Unity 前失败。不要复制引用程序集、把 `.deps.json` 当解析器，或从全局 NuGet cache 任意取 DLL。

### Unity 程序集与场景边界 {#unity-project-boundary}

把 `ProjectVersion.txt` 锁到 Unity 6000.3.22f1，并在 macOS builder 上安装 macOS IL2CPP 模块。把 API Compatibility Level 设为 .NET Standard。保持 Validate References 与程序集版本验证开启。

为适配器创建运行时程序集定义。关闭插件 Auto Reference，并在所选 Unity 配置要求之处显式引用 `FSharpGameplay.dll` 与 `FSharp.Core.dll`；确保测试引用运行时适配器程序集，而不是反过来。让 Editor 测试/代码留在 Player 外。

创建一个场景，包含命名 GameObject 与 `UnityAdapter`、可见位置标记，以及确定性测试输入适配器。输入测试通过 `SetHorizontal` 发送 `-1`、`0`、`1` 与越界值；它不依赖物理控制器或项目范围输入包。

Edit Mode 测试验证 DTO/映射帮助器，以及预期插件类型与程序集身份能加载。Play Mode 测试验证 `Awake` 初始化、已知固定步骤后的正/负移动、禁用/重置行为、Console 无异常、场景重载，以及项目选择的 domain/scene reload 设置。

### 性能与裁剪证明 {#performance-and-stripping-proof}

分析 development Player，而不只是 Editor。预热后捕获固定步骤帧，并对适配器/步进路径断言约定分配预算。struct 回归避免 `MotionState` class 分配，但 Unity 调用、测试框架、日志与输入仍可能分配。

选择并记录一个显式 managed stripping level。从发布预期设置开始，而不是不断更改直到构建通过。执行两个公开桥类型与每条动态发现路径。在负面实验中逐项暂时移除 `link.xml` 条目，可以显示它是否必要；只保留由行为证明的根。

同时构建具有有用调用栈/符号设置的诊断 IL2CPP profile，以及接近发布的 profile。只有绿色诊断构建并不能证明优化裁剪行为。

### 可复现构建与启动 {#build-and-launch}

用 `-batchmode`、`-quit`、`-projectPath`、`-activeBuildProfile` 与 `-logFile`，为 macOS profile 调用一次确切 Editor 可执行文件。不要在方法内部切换目标。按约定策略把任何意外警告视为失败。

构建后：

1. 验证退出码并解析结构化构建结果，而不是一个成功字符串；
2. 归档 Editor 日志、测试 XML、构建报告、插件 manifest、符号与 Player 哈希；
3. 检查 Player 架构与签名状态；
4. 在图形化 macOS 会话中带超时启动 `.app`；
5. 等待包含构建与插件身份的机器可读 ready 标记；
6. 驱动或自动运行代表移动/生命周期检查；
7. 收集 Player 日志与退出状态；
8. 干净终止，并为失败运行保留产物。

没有可用图形上下文的自动化会话是环境失败行，不是应用通过或失败。应在预期交互/CI runner 上重试，并保留两次结果。

### 证据记录与失败语义 {#vertical-slice-evidence}

独立记录各行：

| 行 | 通过条件 | 失败所有者 |
| --- | --- | --- |
| 锁定 F# 构建 | 确切 SDK/lock，0 警告/错误，预期 bundle manifest | F# 源码/包流水线 |
| 干净 Unity 导入 | 确切补丁、干净导入、引用有效、无编译/导入错误 | 资源/插件集成 |
| Edit Mode | 映射与身份通过，并输出 XML | 适配器/程序集配置 |
| Play Mode | 场景、生命周期、重载、移动与 Console 通过 | Unity 宿主行为 |
| 分配 | 目标 Player 捕获满足命名预算 | 表示/热路径 |
| IL2CPP 构建 | 显式 profile/后端/裁剪/架构完成 | 链接器/AOT/原生工具链 |
| Player 启动 | 命名产物启动并发出 ready 身份 | 包/运行时/环境 |
| Player 行为 | 代表检查与日志通过 | 集成应用 |
| 诊断 | 刻意失败可符号化到有用 F# 与 C# 帧 | 符号/崩溃流水线 |

只有全部行通过，声明才能变成：“X44 的代表切片能在该 build profile 下的 Unity 6000.3.22f1 macOS ARM64 IL2CPP Player 中工作。”它仍不说明 Windows、移动、主机、Web、另一裁剪级别或整个游戏。

## 练习 3：存档、异步效果与动态内容 {#exercise-03}

核心动作是分离创作、已验证运行期状态、持久存储与处理器发现。

### 四种具有显式所有权的模型 {#quest-models}

使用这些边界：

| 模型 | 所有者 | 形状 |
| --- | --- | --- |
| 任务创作 DTO | C#/Unity | `[Serializable]` 类/struct、受支持字段、资源 GUID、基本类型 list |
| 已验证任务定义/状态 | F# | 私有构造器、内部 record/union/map、不含 Unity 对象 |
| 公开桥 DTO | CLR 导向 F# 类型或 C# | 枚举、小 struct/class、数组、静态方法、显式结果/错误负载 |
| 存档 DTO | 带版本存储契约 | 稳定 ID、基本/数组数据、schema 版本、校验和；无场景引用 |

C# 适配器读取创作字段或资源，把它们快照为桥 DTO，并调用 `QuestApi.ValidateDefinitions`。验证返回所有带任务/节点/字段身份的可操作错误；它不会为普通错误内容抛异常。

验证后，F# 拥有不可能状态建模：任务 ID 不能为空、转换目标必须是已知节点、完成与取消不同、奖励已验证、处理器名通过 allowlist。Unity 接收紧凑呈现快照和发出的命令/事件。

### 通过纯迁移给存档版本化 {#save-migrations}

定义三个显式持久 schema，而不是反序列化今天的领域类型：

- v1 存储任务 ID 与已完成节点 ID；
- v2 增加目标进度，并在 v1→v2 迁移时推导显式默认值；
- v3 以内容版本加稳定任务键替换原始任务 ID，并记录进行中操作身份。

解析到版本专属 DTO，验证大小/校验和，逐步迁移，再构造当前领域状态。在新状态验证并原子写入前保留原始字节。未知未来版本必须安全失败且不覆盖数据。

黄金 fixture 覆盖有效 v1/v2/v3、缺失可选字段、重复 ID、未知任务内容、损坏/截断数据、超大集合、中断替换、降级与迁移幂等。在普通 .NET 下运行它们，再通过实际发布的 Unity Player 序列化器/文件适配器运行。

### 把异步交付建模为消息 {#async-quest-effects}

为每个远程对话请求提供操作 ID、任务/内容版本与取消所有者。F# 转换发出 `FetchDialogue` 并进入 loading 状态。C# 宿主执行网络工作，且不从工作线程接触场景对象，之后派发 completed、unavailable、cancelled、malformed 或 failed 消息之一。

只有操作 ID、任务、内容版本与当前状态都匹配时，update 才接受完成。场景卸载、组件禁用、新请求、登出或内容更新会取消或取代旧操作。迟到响应成为被忽略的诊断事件，而非新 UI。

在有后果请求前持久化足够身份，以便进程死亡后协调未知结果。不要持久化 cancellation token、task、UnityWebRequest 对象、委托或 GameObject。

### 用闭合注册表替换无限制反射 {#closed-handler-registry}

内容可以命名可选处理器，但必须从编译的 allowlist 选择：

| 内容名称 | 静态注册操作 |
| --- | --- |
| `grant-item` | 验证物品 ID/数量并发出授予命令 |
| `set-flag` | 验证 flag/值并发出 flag 命令 |
| `start-timer` | 验证持续时间并发出计时效果 |

通过可达代码中的显式调用构建注册表。公开 API 可以接受处理器名与 payload DTO，但不能接受任意程序集限定类型名。未知名称是验证错误。这对信任、迁移、工具、裁剪与 IL2CPP 都更安全。

若某个库内部对已知 DTO 成员反射，应选择其文档化 AOT 模式，在受支持处构建时生成元数据，并只添加它所需的确切保留条目。在预期 IL2CPP/裁剪矩阵中证明每个注册处理器与错误路径。

当内容真正需要任意可执行扩展时，把执行留在受控服务端/工具进程，或采用刻意沙箱化且平台支持的数据语言。不要把下载的托管程序集塞进已签名 IL2CPP Player，再把 `link.xml` 叫作沙箱。

### 完整证据矩阵 {#quest-evidence}

最低证据包括：

- 任务转换属性测试与处理器 allowlist 完整性；
- 黄金存档迁移，以及损坏、旧、未来与巨大 payload；
- C# 消费者/API 表面反射测试，不含意外 F# 类型；
- Unity 创作内容跨 prefab/资源保存与脚本重载的往返；
- 取消、超时、进程丢失、迟到响应、内容版本变化与重复回调场景；
- 具有确切依赖与程序集身份的干净导入；
- 显式裁剪级别下 Mono（若发布）与每个 IL2CPP 架构中的全部处理器；
- 目标分配、延迟、离线、内存、挂起/恢复、日志、符号与崩溃证据；
- 新内容版本或存档迁移撤回时的回滚行为。

**反转条件：** 如果动态扩展要求无法表达成闭合且可测试的 AOT 表面，就把该执行移到 Player 外。保留有用的已验证任务协议与 F# 领域；不要为了保留一种实现而保留无限运行时。

## 解答要点 {#solution-takeaways}

- 让主导产品风险选择语言边界。
- 复杂回合/重放规则是很强的 F# 核心候选；场景呈现仍由 Unity 拥有。
- Burst 密集动作游戏可以合理地把 Player 留在 C#，只在非热路径使用 F#，或完全不用。
- 只有 `UnityEditor` 留在薄适配器时，Editor 工具才会从纯 F# 验证器受益。
- 提升 X44 要把干净 Unity 导入、Edit/Play Mode、目标性能分析、IL2CPP 构建、图形启动、行为与诊断作为独立行。
- 精确 DLL 身份与哈希是导入契约的一部分，尤其是 FSharp.Core。
- 把 Unity 支持的创作 DTO、丰富已验证 F# 状态、CLR 桥类型与带版本存档 DTO 作为不同模型。
- 逐步迁移存储 schema，绝不要把持久数据直接反序列化成今天的领域表示。
- 用身份建模异步完成，并在场景、内容或操作变化后拒绝陈旧结果。
- 尽可能用静态可达 allowlist 替换内容命名的反射。
- 保留规则只能留下代码；它不提供信任、沙箱、AOT 支持或行为证明。
- 把无限可执行扩展移出 Player，而不是藏在宽泛链接器根后。

[返回第 44 章](../part-07/ch-44-unity)。
