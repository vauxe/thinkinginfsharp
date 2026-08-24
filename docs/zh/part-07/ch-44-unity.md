---
title: "第 44 章：Unity 6.3 LTS 与 F#"
description: "通过明确的运行时、程序集、序列化、生命周期、性能、AOT、裁剪与 Player 构建边界，在 Unity 中使用 F#。"
translationKey: part-07/ch-44-unity
kind: chapter
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
  - id: unity-6-3-lts
    url: https://unity.com/blog/unity-6-3-lts-is-now-available
    checked: "2026-08-25"
  - id: unity-6000-3-22
    url: https://unity.com/releases/editor/whats-new/6000.3.22f1
    checked: "2026-08-25"
  - id: unity-dotnet-profile
    url: https://docs.unity3d.com/Manual/dotnet-profile-support.html
    checked: "2026-08-25"
  - id: unity-plugins
    url: https://docs.unity3d.com/Manual/plug-ins.html
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
  - id: unity-stripping
    url: https://docs.unity3d.com/Manual/managed-code-stripping.html
    checked: "2026-08-25"
  - id: unity-stripping-configure
    url: https://docs.unity3d.com/Manual/managed-code-stripping-configure.html
    checked: "2026-08-25"
  - id: unity-link-xml
    url: https://docs.unity3d.com/Manual/managed-code-stripping-xml-formatting.html
    checked: "2026-08-25"
  - id: unity-assembly-definitions
    url: https://docs.unity3d.com/Manual/assembly-definitions-creating.html
    checked: "2026-08-25"
  - id: unity-assembly-references
    url: https://docs.unity3d.com/Manual/assembly-definitions-referencing.html
    checked: "2026-08-25"
  - id: unity-testing
    url: https://docs.unity3d.com/Manual/testing-editortestsrunner.html
    checked: "2026-08-25"
  - id: unity-command-line-build
    url: https://docs.unity3d.com/Manual/build-command-line.html
    checked: "2026-08-25"
  - id: unity-gc-practices
    url: https://docs.unity3d.com/Manual/performance-garbage-collection-best-practices.html
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
  - id: fsharp-core-10-1-301
    url: https://www.nuget.org/packages/FSharp.Core/10.1.301
    checked: "2026-08-25"
  - id: dotnet-standard
    url: https://learn.microsoft.com/dotnet/standard/net-standard
    checked: "2026-08-25"
---

# 第 44 章：Unity 6.3 LTS 与 F# {#overview}

Unity 无需编译 F# 源码，也能执行以 F# 编写的代码。F# 会编译成托管 .NET 程序集，而 Unity 可以导入托管插件。这个技术事实很有用，但它只是较长链路中的第一环。

一个能在 `dotnet` 下构建的类库仍可能无法通过 Unity 的引用验证。成功导入的插件仍可能在场景加载时失败。Play Mode 可以正常运行，而 IL2CPP Player 仍可能在提前编译、裁剪、原生链接、启动或某条仅设备存在的路径上失败。因此，正确问题不是“Unity 支持 F# 吗？”，而是“这个确切的 Unity 版本、平台、脚本后端和发布流水线，能证明哪一种 F# 边界？”

本章把 X44 作为一个刻意缩小的答案：把游戏规则放在普通 F# 类库中，发布对 C# 友好的表面，并让很薄的 C# 组件拥有 Unity 特有行为。它也解释何时值得验证直接 F# 组件、何时 F# 增益很小，以及如何避免把类库构建成功误写成想象中的 Player 结果。

## 学完本章后你将能够做什么 {#outcomes}

学完本章后，你应该能够：

- 区分 F# 编译、托管插件导入、Unity 脚本编译、Editor 运行、Mono Player 和 IL2CPP Player 证据；
- 根据领域复杂度、帧预算、Unity 工具、团队技能与平台风险，选择 F# 应该处于何处；
- 以 Unity 的 .NET Standard 2.1 兼容表面为目标，而不把 API profile 当作运行时身份；
- 把 `FSharp.Core` 和所有其他运行期依赖与托管插件一起装配；
- 设计从 C# 看起来普通、且不泄漏可避免 F# 表示类型的公开 F# API；
- 把 Unity 序列化字段与 `UnityEngine.Object` 引用留在可复用领域模型之外；
- 在调用纯 F# 逻辑前，把 Unity 生命周期回调和输入映射成显式值；
- 区分 `Update`、`FixedUpdate`、后台工作和 Unity API 的主线程所有权；
- 检测游戏循环分配，而不是假设函数式代码天然昂贵或天然便宜；
- 解释 IL2CPP 从托管程序集、裁剪、生成 C++、原生编译到打包的顺序；
- 识别需要 AOT 证据的反射、动态泛型、代码生成与平台 API 路径；
- 使用窄保留规则，而不是默认保留整个 `FSharp.Core`；
- 把 Burst 和 Job System 当作独立的 HPC# 契约，而不是 F# DLL 自动拥有的性质；
- 沿证据阶梯从普通单元测试走到目标硬件上已导入、已启动、已裁剪的 Player；
- 在自动化中锁定确切 Unity 补丁和构建配置；
- 准确说明 X44 证明了什么、刻意留给 Unity 什么，以及这些限制为何重要。

## Unity 集成是一组层叠契约 {#unity-contract-stack}

把 Unity 中使用 F# 的发布看成至少七个相连契约：

```text
F# 源码 + 锁定的 NuGet 图
  -> 面向兼容 API profile 的托管插件 DLL
  -> Unity 资源导入与程序集引用
  -> C# 脚本、场景、序列化字段与生命周期回调
  -> Editor 或 Mono 托管执行
  -> UnityLinker + IL2CPP + 原生工具链
  -> 架构专属 Player 包、启动与设备行为
```

每个箭头都能独立失败。前两层是普通 .NET 工作。中间几层属于 Unity 项目与 Editor。最后两层依赖构建模块、平台 SDK、原生编译器、架构、签名与目标设备。

为每个结果使用准确动词：

| 声明 | 最低证据 |
| --- | --- |
| F# 代码能编译 | 对所述目标框架执行锁定还原和 Release 构建 |
| 插件是完整的 | 主 DLL 与所有运行期依赖均存在，且引用检查正确 |
| Unity 接受插件 | 确切 Editor 补丁导入它，引用验证开启且无编译/导入错误 |
| 组件在 Editor 中工作 | 代表场景进入 Play Mode，并观察行为、重载与错误 |
| Mono Player 工作 | 命名目标的 Player 在 Editor 外构建、启动并运行代表测试 |
| IL2CPP Player 工作 | 命名架构、裁剪级别、原生工具链、构建、启动与运行路径均通过 |
| 发布受到支持 | 设备/平台矩阵、性能、诊断、打包、签名、升级与恢复均通过 |

“受到 .NET Standard 支持”和“能在我们发布的 IL2CPP Player 中工作”是不同种类的句子。两者都为真时保留两者；绝不要用一个替代另一个。

## 决定这个 Unity 项目是否需要 F# {#decision-map}

当游戏中存在值得脱离帧、GameObject、场景与资源状态来命名和测试的规则时，F# 最有价值。当大部分代码只是一小串引擎调用，或必须置于 Unity 特定编译流水线内时，它的价值较小。

| 候选边界 | 适合之处 | 主要摩擦 | 第一项证明 |
| --- | --- | --- | --- |
| 纯 F# 领域插件 + C# 适配器 | 经济、战斗结算、任务、对话状态、物品栏、程序化规则、存档迁移、服务端共享验证 | DLL/依赖装配与语言边界 | 让一条规则通过 `dotnet` 测试、Unity 导入、Play Mode 与代表 IL2CPP Player |
| 由 Editor 代码使用的 F# 服务/工具库 | 导入验证、内容检查、确定性生成器、构建元数据 | `UnityEditor` 所有权、资源数据库生命周期、batch mode、诊断 | 在交互与批处理 Editor 中跑通一条真实资源流水线 |
| 托管插件中的直接 F# `MonoBehaviour` | 团队接受外部 F# 构建，且只需要很小的组件 | UnityEngine 引用版本、Inspector/序列化形状、组件发现、调试 | 对确切 Editor 程序集编译；导入、挂载、序列化、重载、构建 Player |
| 不使用 F# 的 C# Unity 应用 | 逻辑主要是引擎编排、可视化脚本、Shader、包、Jobs/Burst，或设计师拥有工作流 | F# 领域收益较少 | 把最简单 C# 垂直切片与 F# 边界比较，而不是比较语言偏好 |
| 独立 F# 后端或工具进程 | 权威模拟、匹配、分析、内容构建或离线工具不必在 Player 中运行 | 网络/进程契约与部署 | 保持 Unity 客户端很薄；独立验证线协议/版本行为 |

### 低摩擦的默认选择 {#recommended-boundary}

新的实验应从不依赖 Unity 的 F# 类库与很薄的 C# 宿主开始。这保留了 F# 最有用的性质——显式类型、纯转换、属性测试和普通 .NET 工具——同时让 Unity 最强的工作流保持原生形状。

C# 层应拥有：

- `MonoBehaviour`、`ScriptableObject`、自定义 Inspector 与 Unity 特性；
- 场景与 prefab 的序列化字段；
- `GameObject`、`Transform`、`Rigidbody`、资源、句柄及其他 `UnityEngine.Object` 引用；
- `Awake`、`OnEnable`、`Update`、`FixedUpdate`、`OnDisable` 与场景回调；
- 输入包、平台 API、协程、Unity 日志与 Unity 专属异步适配器；
- Unity 值与领域值之间的映射。

F# 层应拥有离开引擎后仍有意义的决策：经过验证的标识符、规则、确定性状态转换、存档 schema 与迁移、以输入提供的随机种子，以及供宿主执行的效果端口。

### 直接 F# 组件可行，但并非免费 {#direct-fsharp-components}

Unity 的托管插件模型基于 .NET 程序集，而非源码语言身份。派生自 `MonoBehaviour` 的预编译类型原则上可以像其他托管插件类型一样挂载。F# 项目也可以引用某个特定 Editor 安装中的 Unity 程序集。

这并不使直接路径成为默认选择。构建现在依赖确切 Unity 程序集位置与版本。生成的 F# 表示可能不符合 Inspector 预期。Unity 示例、源码生成器、分析器、包设置、调试器工作流与 Editor 回调均呈 C# 形状。每项声明仍需要导入、挂载、序列化、重载和 Player 证据。

只有垂直切片在度量这些成本后确实更简单，才使用直接 F#。围绕稳定 F# 核心的十行 C# 组件不是失败；它是工具所有边界上的适配器。

### 知道何时停止增加语言边界 {#when-not-to-use}

不要仅为了包装 `transform.Translate`、播放动画或转发一次碰撞回调就引入 F# DLL。额外的编译器、包、导入、符号与互操作表面必须换来可测试的领域价值。

同样，不要因为游戏其他部分使用 F#，就把帧关键的 Burst kernel 硬推过 F#。Burst 文档规定了 HPC# 子集与 Unity IL 后处理流水线。除非精确 F# 实验证明所需包、特性、IL、Editor、AOT、性能与 Player 行为，否则把这种 kernel 保持为受支持的 C# 数据导向形状。

## X44：一个已验证的托管插件边界 {#x44-verified-slice}

X44 实现一条水平移动规则。这条规则刻意小到不足以单独证明生产架构；它的目的在于让构建、API、依赖、宿主、分配、链接器与证据边界都可检查。

### 项目契约与依赖产物 {#project-contract}

<<< @/../examples/ecosystem/unity/FSharpGameplay/FSharpGameplay.fsproj{xml:line-numbers} [FSharpGameplay.fsproj]

项目目标为 `netstandard2.1`，程序集名为 `FSharpGameplay`，且只编译 `Gameplay.fs`。`FSharp.Core` 已经是 F# SDK 隐式包；`Update` 为本仓库固定这一个 10.1.301 引用，而不是增加重复项。

`CopyLocalLockFileAssemblies` 很重要，因为 Unity 导入 DLL 时不会还原这个 `.fsproj`。后置构建目标把部署假设变成失败条件：输出目录必须同时存在 `FSharpGameplay.dll` 与 `FSharp.Core.dll`。

包版本与程序集版本不是同一个标识符。锁定的 NuGet 包是 10.1.301；构建后的插件记录了对 `FSharp.Core, Version=10.1.0.0` 的程序集引用。应导入锁定构建产生的依赖，而不是从任一数字猜测文件。

### CLR 形状表面后的纯逻辑 {#pure-gameplay}

<<< @/../examples/ecosystem/unity/FSharpGameplay/Gameplay.fs{fsharp:line-numbers} [Gameplay.fs]

`Gameplay.Create` 与 `Gameplay.Step` 是元组式静态方法，因此 C# 看到的是普通方法调用，而不是柯里化的 `FSharpFunc` 值。`MotionState` 暴露只读 float 属性，并隐藏字段及非默认构造器。

状态是 struct。较早实现使用 class，因而每次 `FixedUpdate` 都分配新的托管对象。回归测试现在检查 `IsValueType`；它消除了这项特定状态分配，但并不假装整个 Player 每帧分配零字节。大型 struct 会带来复制成本，所以应让状态保持小巧并分析真实目标。

转换会夹紧方向输入，拒绝非有限值与负时间或速度，计算速度并返回新状态。它没有 `UnityEngine` 引用，不读取当前时间，也不发生可变更新。测试可以直接提供所有输入。

### 很薄的 Unity 所有适配器 {#csharp-adapter}

<<< @/../examples/ecosystem/unity/FSharpGameplay/UnityAdapter.cs{csharp:line-numbers} [UnityAdapter.cs]

文件与公开 `MonoBehaviour` 类同名为 `UnityAdapter`，保留 Unity 普通脚本/组件工作流。Inspector 只拥有一个基本类型 `speed` 字段。`OnValidate` 保护创作期配置，而 F# 边界仍验证运行期调用。

`Awake` 从当前 transform 创建运行期状态。`FixedUpdate` 提供输入值、配置速度和 Unity 固定 delta time，再把返回位置映射回 `Vector3`。这是 transform 示例，而不是物理建议；由 Rigidbody 拥有的对象需要相应物理 API 与测试。

`SetHorizontal(float)` 刻意不选择旧 Input Manager 或 Input System 包。独立输入适配器可以调用它。这让包选择与回调形状留在规则程序集之外。

C# 文件被登记为说明性代码，因为本仓库没有 UnityEngine 程序集。它经过源码审阅，但没有在这里编译。发明假的引擎类型只能证明一个假的宿主。

### 窄链接器根 {#linker-roots}

<<< @/../examples/ecosystem/unity/FSharpGameplay/link.xml{xml:line-numbers} [link.xml]

C# 适配器的直接调用应对静态可达性分析可见。X44 仍包含两个显式根，以展示预期跨程序集桥，并为本章提供具体裁剪产物。

文件没有保留整个 `FSharp.Core`。宽泛保留会隐藏缺失的反射设计、增大 Player，并增加 IL2CPP 工作量。只有真实动态路径需要某个类型或成员时才添加，然后证明对应裁剪级别。

把 `link.xml` 复制到 Unity 项目的 `Assets` 树下。外部 `.fsproj` 旁的源文件在成为 Unity 资源前没有任何作用。

### 按字面阅读证据账本 {#evidence-ledger}

截至 2026-08-25，X44 记录了：

| 层 | 结果 | 它证明什么 |
| --- | --- | --- |
| 锁定 .NET 还原 | 通过 | `netstandard2.1` 图解析到 FSharp.Core 包 10.1.301 |
| Release 插件构建 | 通过，0 警告/错误 | F# 源码能在 .NET SDK 10.0.301 上编译 |
| 产物检查 | 通过 | 8,192 字节插件与 2,407,760 字节 FSharp.Core 相邻；程序集引用存在 |
| 聚焦规则/API 测试 | 通过，1/1 | 夹紧/步进行为、struct 状态、FSharp.Core 引用，以及公开签名不含 F# 专属类型 |
| 仓库示例矩阵 | 通过 | 锁定解决方案、69 个 ExampleTests、其他示例、Fable 构建与浏览器冒烟仍为绿色 |
| Unity 6000.3.22f1 导入 | 未运行 | 此机器没有 Editor |
| C# 编译与 Play Mode | 未运行 | UnityEngine 宿主与场景行为未验证 |
| macOS ARM64 IL2CPP Player | 未运行 | 原生转换、裁剪、链接、启动与运行期行为未验证 |

最后三行是证据，不是难堪。可见缺口可以安排和估价；虚假绿色行不行。

## 面向兼容 profile，而非运行时名称 {#compatibility-target}

Unity 6 在 Player 设置中提供 .NET Standard 2.1 与更宽的 .NET Framework profile。.NET Standard profile 是跨平台基线，也是可复用托管插件正确的第一目标。

### API profile 只是编译期上限 {#profile-not-runtime}

`.NET Standard 2.1` 描述一组 API。它并不表示 Unity 嵌入了与运行 `dotnet test` 的机器相同的 CoreCLR 版本、使用相同垃圾回收器、允许所有平台 JIT，或支持某个碰巧能对该 profile 编译的库的所有实现细节。

在 Player 插件中避免 `net10.0`、`netcoreapp`、操作系统专属目标框架、动态代码生成和意外平台 API。如果某个库需要更宽宿主，应把它放在 Player 外，或增加有明确证据的目标专属适配器。

对最小且诚实的 profile 编译，然后在每个发布脚本后端与平台上测试。兼容性是一个交集：

```text
插件使用的 API
  ∩ Unity API compatibility profile
  ∩ 脚本后端实现
  ∩ 目标平台能力
  ∩ 链接器/AOT 可发现性
```

### 携带完整依赖闭包 {#dependency-closure}

即使公开 API 不含 F# 专属类型，F# 程序集通常仍引用 `FSharp.Core`。编译器生成调用、特性与实现细节仍需要该程序集。

同一规则适用于每个 NuGet 依赖：Unity 不会把类库锁文件解释为资源导入计划。以兼容版本导入所有必需托管 DLL、原生库、数据文件与许可证。不要复制框架引用程序集，也不要假设 `.deps.json` 会让 Unity 像 `dotnet` 一样解析包。

优先选择小依赖图。对每个包询问：

- 它的编译目标适合 .NET Standard 2.1 吗？
- 它是否使用反射、表达式编译、`Reflection.Emit`、动态代理、不支持的编码、原生加载或平台专属文件？
- 它的泛型实例化对 IL2CPP 可见吗？
- 它为每个架构提供原生二进制吗？
- UnityLinker 能看到其动态入口吗？
- 哪些许可证与声明必须进入 Player？
- 确切版本是否在确切 Player 矩阵中运行过？

一个传递包可能比 F# 源码本身带来更大的 Unity 风险。

### 显式控制 Unity 引用 {#plugin-import}

把托管插件复制到 `Assets` 下，选择平台兼容性，并保持 Validate References 开启。这能比运行期更早发现缺失引用与强名称不匹配。

Auto Reference 对 spike 很方便，但它让每个符合条件的脚本程序集都看到插件，并增加重编译与意外耦合。较大项目中应关闭它，并用程序集定义显式引用预编译程序集。把 Editor-only 适配器放入 Editor-only 程序集，并从 Player 平台排除不兼容插件。

绝不要在理解不匹配前，通过关闭程序集版本验证来“修复”引用问题。如果多个插件要求不兼容 `FSharp.Core` 版本，应把它们重建到一个经过测试的版本、隔离成进程，或拒绝该组合；一个加载上下文无法诚实容纳两个程序集身份相同的文件。

## 设计从 C# 看来自然的边界 {#design-csharp-boundary}

F# 实现内部可以保持地道。导出表面应遵循消费者惯例。

### 优先使用普通 CLR 形状 {#clr-shaped-api}

适合 Unity 侧的选择包括：

- 命名空间、密封类或小 struct、PascalCase 方法与只读属性；
- 元组式方法参数，让 C# 调用 `Step(state, input, speed, dt)`；
- 适当使用基本值、枚举、数组、`IReadOnlyList<T>` 与有用途名称的 DTO；
- 只有回调确实是正确契约时才使用 `System.Action` 或 `System.Func`；
- 构造必须强制不变量时使用显式工厂方法。

把 F# list、map、option、result、可辨识联合、柯里化函数与度量单位留在边界后，除非 C# 消费者刻意接受其编译形状。它们是有效 .NET 类型，并非禁用类型；问题在于消费摩擦、表示耦合、AOT 表面与维护。

度量单位会从发出的 .NET 签名中擦除。C# float 无法证明它代表秒、米还是米每秒。用方法名、DTO 字段、验证或不同包装类型保留含义。

### 只翻译一次结果 {#errors-and-outcomes}

不要为每个预期游戏分支抛异常。在内部用联合或 result 建模领域结果，再一次性翻译为 C# 友好的结果类、枚举加负载、`Try...` 方法或显式回调消息。

把异常留给破坏契约且当前调用无法表示的失败。X44 拒绝 NaN、无穷、负速度与负 delta time，因为它们表示无效边界调用。C# 适配器会在到达边界前防止普通创作错误。

异步工作不要向 Unity 泄漏 F# `Async<'T>`。根据宿主发布 `Task`、`ValueTask`、C# 友好的轮询句柄或消息接口。定义取消所有权以及完成在哪个线程交付。即使纯计算或 I/O 在其他地方运行，Unity 对象访问仍属于主线程。

### 把持久数据与引擎对象分开 {#units-and-data}

不要把 `GameObject`、`Transform`、`Texture`、场景句柄、打开的流、取消源或服务单例放进需要保存、重放、测试或发给服务端的领域状态。

在 F# 中使用稳定标识符和值。让适配器把它们解析为当前 Unity 对象。映射可能因场景卸载、资源变更或对象销毁而失败；把它表示为边界结果，而不是假装引用持久。

## Unity 序列化是自己的契约 {#serialization}

Unity 序列化器不是普通 .NET 序列化，也不会持久化任意属性或对象图。

### 从受支持字段开始 {#supported-fields}

可序列化字段是 public 或标记 `[SerializeField]`，不是 static、const 或 readonly，且具有支持的字段类型。支持类别包括基本类型、受支持大小的枚举、Unity 内建值、`UnityEngine.Object` 引用、可序列化自定义类/struct、数组，以及由支持元素组成的 `List<T>`。

属性不是普通持久化表面。字典、多维或交错数组、嵌套容器需要显式表示或序列化回调。`[SerializeReference]` 会改变引用与多态行为，但也带来自己的身份、迁移与类型名风险。

F# record 通常暴露属性与编译器生成表示。union、option、list、map 和闭包不会因为是托管对象就变成 Unity 字段格式。某种形状可能在一条 Editor 路径上看似可用，却仍在 prefab 持久化、domain reload、裁剪或 Player 构建中失败。

### 做映射，不要教序列化器理解 F# {#map-dont-teach}

把 Inspector 创作配置放在 C# `MonoBehaviour`、`ScriptableObject` 或刻意可序列化的 C# DTO 中。验证它，再构造更丰富的 F# 模型。

对游戏存档，定义独立于场景序列化且带版本的存储 DTO。通过显式迁移与错误报告把 DTO 映射为已验证领域状态。测试旧版本、缺失字段、损坏数据、中断写入、云冲突、降级策略与删除。

这里有意产生三个不同模型：

```text
Unity 创作字段 -> 已验证 F# 运行期模型 -> 带版本存档/线协议 DTO
```

试图让一个生成类型同时满足 Inspector 编辑、不可能状态建模、网络兼容与长期存档迁移，通常会削弱全部四项。

### 跨生命周期变化重建运行期状态 {#reload-and-lifecycle}

Unity 可以重载脚本与程序集、从序列化字段重建组件、以可配置 domain/scene reload 行为进入 Play Mode、卸载场景、禁用对象，并在托管引用仍存在时销毁原生对象。

把 `Awake` 或另一显式组合点看作从序列化配置构造。使用 `OnEnable` 与 `OnDisable` 配对订阅和取消。不要假设私有托管缓存能跨重载存活，也不要假设看似非 null 的 `UnityEngine.Object` 仍拥有活着的原生对象。

X44 在 `Awake` 中重建 `MotionState`，并在 `OnDisable` 中重置输入。它不声称存档持久化或 domain-reload 证据；这些属于更大 Unity 项目测试。

## 尊重游戏循环与分配预算 {#game-loop}

函数式设计有助于隔离决策；它不会让代码免受帧时间、内存、缓存或线程约束。

### 根据引擎契约选择回调 {#update-and-fixed-update}

把 `Update` 用于帧驱动呈现与采样输入，把 `FixedUpdate` 用于固定步长模拟和物理协调，只为文档规定目的使用较晚/渲染回调。一个固定回调可以在一个渲染帧周围运行零次、一次或多次。

不要只在某个渲染帧可能不运行的回调中读取瞬时按钮边缘。在输入层捕获输入，再把稳定值或排队命令交给模拟步骤。

把时间显式传给纯逻辑。这让暂停、重放、慢动作、确定性测试与服务端一致都可见。确定性还要求受控随机性、迭代顺序、浮点预期，并且没有隐藏墙钟。

### 度量分配，不要从风格争论 {#allocation-budget}

F# 管道、闭包、序列、record、union、数组与接口调用会根据表示和用法呈现不同分配行为。“函数式”既不是发生分配的证明，也不是零分配的证明。

在目标设备上分析 development Player。使用 CPU Profiler 的 `GC.Alloc` 列与调用栈，再用代表工作负载和构建配置确认。Editor 度量包含 Editor-only 行为，可能与 Player 不同。

常见热循环风险包括：

- 每帧分配 class、list、sequence、闭包、委托、option 或格式化字符串；
- 通过 object 或接口路径装箱值类型；
- 重复枚举惰性序列，或调用每次返回新数组的 Unity API；
- 复制过大的 struct，导致避免堆反而恶化 CPU/缓存预算；
- 保留短生命数据直到发生大型回收。

优化测到的热点，而不是整个领域。每次用户操作只运行一次的回合结算管道可以优先清晰度；每帧调用数万次的 transform 步骤可能需要紧凑 struct、数组、池或 Burst kernel。

### 让 Unity 对象留在所属线程 {#threading}

大多数 Unity API 与对象由主线程拥有。当输入是脱离引擎的值且输出不接触 Unity 对象时，纯 F# 计算可以使用 task 或工作线程。

在主线程复制或映射所需值，执行带取消的有界工作，再把结果排队回主线程。包含操作或场景身份，使来自已卸载场景、已禁用组件或已被新请求取代的结果遭到拒绝。

绝不要把“不可变”当作从工作线程读取 `Transform`、资源或已销毁 Unity 对象的许可。不可变描述你的值；它不会改变引擎对象的线程或生命周期契约。

## IL2CPP 改变证明义务 {#il2cpp-and-aot}

IL2CPP 不是“换了优化器的 Mono”。它改变可执行代码产生的时间与方式。

### 遵循真实流水线 {#il2cpp-pipeline}

对 IL2CPP Player，Unity 会：

1. 把项目 C# 与所需包代码编译成托管程序集；
2. 应用托管代码裁剪；
3. 把托管 IL——包括导入的 F# 程序集——转换成 C++；
4. 调用目标平台原生编译器与链接器；
5. 把原生产物与所需数据打包成 Player。

必须安装对应 IL2CPP 模块与原生工具链。通常不支持交叉编译；除文档明确例外（例如受支持 Linux 交叉编译路径）外，应在所需宿主上构建。

绿色 Editor 会话没有证明第 2–5 步。IL2CPP 构建成功后仍需启动与行为测试，因为裁剪或 AOT 缺口可能仅在路径执行时出现。

### 让 AOT 可达性具体化 {#aot-risk}

提前编译无法等到运行期再生成任意新代码。以下情况会增大风险：

- 通过名字发现类型或成员的反射；
- 动态生成访问器的序列化器、依赖注入或映射库；
- `Reflection.Emit`、表达式编译与运行期代理生成；
- 泛型虚方法，以及在可达代码中从未具体化的泛型组合；
- 仅由原生代码、字符串、特性或外部数据发现的回调；
- 具有错误签名或架构的平台调用与原生库。

补救办法不是“避免泛型”或“全部保留”。优先使用静态显式调用；实例化所需闭合泛型路径；使用库的 AOT 支持模式；在需要处添加窄根或回调特性；并运行确切 Player 路径。

也要记录负面情况。一个只处理快乐路径存档类型的加载器可能通过，而旧多态存档、错误子类型、本地化资源或罕见回调已经被裁剪。

### 把链接器规则当作经过测试的代码 {#reflection-and-stripping}

UnityLinker 分析可达代码，并按选定 Managed Stripping Level 删除代码。当前 Unity 6 文档把 Minimal 标为 IL2CPP 默认值，把 Low 标为未来弃用，并把 Medium 与 High 描述为更激进选择；应记录显式设置，因为默认值可以改变。

当保留元素可以携带 Unity 特性而不污染可复用层时使用 `[Preserve]`。当保留属于集成配置或目标是外部程序集时使用 `link.xml`。让类型与成员名称精确，把文件放在 `Assets` 下，并测试规则收窄后预期 Player 仍工作。

保留只能防止删除；它无法让不支持的 API、运行期代码生成器、原生二进制或泛型模式变得 AOT 兼容。它也不测试行为。

### Burst 与 Jobs 是独立架构 {#burst-and-jobs}

Burst 文档规定 HPC#：围绕非托管值、Unity collection、job 或函数指针、特性与 IL 后处理的受限高性能 C#/.NET 子集。托管对象、许多运行时服务与普通异常行为都在该 kernel 模型之外。

X44 是托管 F# 插件，没有任何 Burst 或 Job System 证据。不要给 F# 产出方法添加 `[BurstCompile]`，然后从特性存在推断支持。

当性能分析证明需要 Burst 时，一个实用边界是：

```text
F# 规则与编排
  -> 扁平数组/struct 命令
  -> 小型 C# Job/Burst kernel
  -> 扁平结果值
  -> F# 决策层
```

度量转换成本、调度开销、确定性、安全检查、Editor 编译、Player AOT 与目标性能。如果热 kernel 主导架构，让 C# 适当拥有该子系统的更多部分。

## 建立证据阶梯 {#testing-ladder}

使用能推翻声明的最便宜测试，然后只攀登到发布所需高度：

1. **纯 .NET 测试：** 规则、不变量、属性、存档迁移、确定性重放与 C# 导向签名。
2. **产物检查：** 目标框架、程序集身份、依赖闭包、符号、原生资源与许可证文件。
3. **Unity 导入检查：** 确切 Editor 补丁、干净 Library/cache、Validate References、Console 零错误、显式平台兼容性。
4. **Edit Mode 测试：** 不要求运行场景的适配器映射与资源/Editor 代码。
5. **Play Mode 测试：** 组件生命周期、场景/prefab 序列化、重载配置、输入、时间与引擎交互。
6. **Mono Player 测试：** 当 Mono 是发布或诊断后端时，在命名平台上于 Editor 外构建并启动。
7. **IL2CPP Player 测试：** 显式架构与裁剪级别、构建日志、启动、罕见反射/泛型路径与崩溃符号。
8. **设备/发布测试：** 受支持硬件、性能、内存、挂起/恢复、平台服务、打包、签名、升级、遥测与恢复。

Unity Test Framework 可以在构建后的 Player 中运行 Edit Mode、Play Mode 与测试。把普通 F# 测试留在 Unity 外以快速反馈，再为适配器与宿主增加 Unity 所有的 C# 测试。Player 测试不是 Play Mode 的重复；它运行不同的运行时与包。

失败时保留确切 Editor 版本、构建配置、目标、后端、裁剪级别、命令、退出码、Editor 日志、Player 日志、测试 XML、崩溃转储、符号与产物哈希。“CI 失败”不是诊断。

## 让 Player 构建可复现 {#build-and-release}

Unity 是编译器与资源流水线的一部分。像给编译器版本化一样给它版本化。

### 锁定 Editor、模块、包与插件 {#pin-editor}

记录完整 Editor 补丁，而不只写“Unity 6.3”。X44 选择 6000.3.22f1，因为它是 2026-08-25 核对时当前 6.3 LTS 补丁；这是审阅目标，不是已安装工具声明。

锁定 Unity 包与 F# NuGet 图。从干净锁定还原只构建一次 F# DLL，把确切依赖集复制进 Unity 项目，并用哈希或其他方式标识导入产物。除非平台专属输出有意为之，不要在每个平台 job 中以不同方式重建插件。

应足够频繁地在 CI 中执行干净导入，以发现隐藏本地 Library 状态。决定哪些生成的 Unity metadata 与设置应进源码控制，且不要让开发者上次活动目标悄悄选择发布。

### 每次调用驱动一个显式构建配置 {#build-profiles-and-ci}

Unity 命令行构建支持显式 build target 或保存的 build profile。始终指定其中一个，使用 batch mode，写日志文件，并让每个 Editor 调用只跑一个目标。切换目标可能要求程序集重载，在批处理脚本中途并不可靠。

发布 job 应记录：

- 确切 Editor 路径/版本与已安装模块；
- 项目提交、锁文件、导入插件哈希与包 manifest/lock；
- 活动 build profile 或 target、场景、架构、后端、裁剪、development/debug 标志；
- 平台 SDK 与原生编译器版本；
- 输出路径、退出码、日志、警告策略、产物哈希、签名身份与来源；
- 在代表硬件上的构建后启动/测试结果。

编译成功但没有 Player 启动，是构建结果，不是运行期结果。

### 保持符号与诊断可用 {#symbols-and-diagnostics}

按发布的隐私与存储策略保留 F# portable PDB 和 Unity/原生符号。IL2CPP 调用栈质量取决于构建配置与 stack-trace 设置；优化的原生编译可能内联托管帧。

事故发生前，在 development Player 中验证一次刻意 F# 异常能否给出有用的方法、文件与行路径。把符号上传到崩溃系统，保留映射/构建标识，并从实际捕获崩溃验证符号化。

以稳定事件名与标识符记录领域结果，不要记录整个存档或个人数据。把被拒绝的游戏命令与插件加载错误、被裁剪方法、原生崩溃或资源问题分开，使遥测能指向所有层。

## 运行有界采用 spike {#adoption-spike}

在把生产 Unity 代码库投入 F# 前，用一个垂直切片限时覆盖最难的代表风险：

- 确切 Unity 补丁、平台模块、F# SDK、NuGet lock 与可重复插件复制步骤；
- 一条带属性或重放测试的领域规则，以及一个 C# 友好公开契约；
- 一份映射成已验证 F# 状态的 Inspector 创作配置；
- 场景/prefab 保存、脚本重载、domain-reload 设置、启用/禁用与场景卸载；
- 一项带取消、陈旧结果拒绝和主线程返回的异步操作；
- 一次存档迁移与一份损坏/旧 payload；
- 一条针对 CPU、`GC.Alloc`、内存与复制分析的代表逐帧路径；
- 一条处于预期裁剪级别的动态反射/泛型路径；
- Play Mode、相关时的 Mono，以及发布 IL2CPP 架构；
- 干净 CI 导入、命令行 Player 构建、启动、日志、符号、包与签名路径；
- 上手成本、IDE/调试器摩擦、依赖更新，以及退回 C# 宿主的文档化退出路径。

只采用通过的边界。结果可以是“F# 拥有整个确定性模拟”“F# 拥有离线规则但不拥有帧代码”“F# 留在服务端与工具”“这里 C# 更简单”。它们都是有效工程结果。

## 避免常见 Unity 错误 {#common-mistakes}

- 把成功的 `dotnet build` 称为 Unity 支持。
- 因开发机有 .NET 10 就以 `net10.0` 为目标，而 Player 插件契约是 .NET Standard 2.1。
- 复制 `FSharpGameplay.dll`，却漏掉 `FSharp.Core.dll` 或另一传递依赖。
- 把 NuGet lock 或 `.deps.json` 当作 Unity 自动还原的东西。
- 为消除未解释不匹配而关闭引用或程序集版本验证。
- 意外向 C# 暴露 F# 函数、list、option 或 union，然后在每个调用点写适配器。
- 未经 prefab/reload/Player 证据，就让 Unity 序列化生成的 F# 表示、属性或任意图。
- 把场景对象、资源或打开资源存进持久领域状态。
- 因周围 F# 值不可变就从工作线程读取 Unity 对象。
- 只在 `FixedUpdate` 采样帧边缘输入并丢失事件。
- 未度量 `GC.Alloc` 就为每个实体每帧分配 sequence、闭包、字符串或状态 class。
- 在性能分析前把每个清晰不可变值都换成可变更新。
- 为让一个反射失败消失而保留全部 FSharp.Core。
- 假设 `link.xml` 能让运行期代码生成或不支持 API 在 AOT 下工作。
- 把 Play Mode 当作 IL2CPP 测试。
- 只测试一种泛型类型、序列化子类型、locale、错误回调或旧存档格式。
- 假设 Burst 特性能把任意 F# IL 变成受支持 HPC# kernel。
- 使用“已安装的 Unity 6.3”构建，而不指定确切补丁与模块集。
- 让 CI 复用上次活动平台，或在一次不可靠目标切换中构建多个目标。
- 在失败可诊断前丢弃 PDB、IL2CPP 符号、Editor 日志或 Player 日志。
- 当窄 C# 适配器是更简单长期契约时，仍强迫每行 Unity 面向代码使用 F#。

## 练习 {#exercises}

### 练习 1：为三种产品选择语言边界 {#exercise-01}

为以下产品选择第一 F# 边界、被拒绝替代、证明矩阵与反转条件：(a) 一款拥有复杂确定性战斗、重放、mod 验证与适中呈现层的回合制战术游戏；(b) 一款风险来自数千物理式实体、Jobs/Burst 性能、平台 SDK 与设计师创作行为的主机动作游戏；(c) 一条验证对话图、生成本地化报告并在 CI 中无头运行的 Unity Editor 内容流水线。不要为三者选择同一种语言划分。

### 练习 2：把 X44 变成 Unity 垂直切片 {#exercise-02}

设计能把 X44 从“托管 DLL 能构建”提升为“代表 macOS ARM64 IL2CPP Player 工作”的最小 Unity 项目与证据记录。包括产物复制、FSharp.Core 身份、程序集定义、Validate References、场景与输入设置、Edit/Play Mode 测试、重载行为、分配分析、裁剪级别、`link.xml`、命令行 build profile、启动、日志、符号与精确失败语义。让每个未运行行保持原样，直到它真实执行。

### 练习 3：增加存档、异步效果与动态内容 {#exercise-03}

扩展架构以支持一套任务系统：规则使用 F#，配置在 Unity 中创作，存档必须跨三个版本迁移，远程对话异步到达，可选任务处理器以内容中的名称指定。定义创作 DTO、已验证领域类型、C# API 形状、取消与陈旧结果消息、存档 DTO/迁移、不依赖无限制运行时代码生成的处理器注册、窄保留规则、错误/旧内容测试，以及 Mono/IL2CPP 证据。若无法证明安全 AOT 发现，说明什么应放在 Player 外。

[阅读本章练习答案](../solutions/ch-44-unity)。

## 本章回顾 {#chapter-review}

- Unity 可以执行导入的托管 F# 程序集，但源码语言兼容只是第一项契约。
- 区分 .NET 构建、依赖闭包、Unity 导入、Editor 运行、Mono Player、IL2CPP Player 与发布证据。
- 默认低摩擦边界是很薄 C# Unity 适配器后的纯 F# 类库。
- 直接 F# 组件是可行的托管插件，但需要确切 Unity 程序集、Inspector、重载与 Player 证明。
- 以 `netstandard2.1` 为 Unity 跨平台 API profile 目标；不要把它与 CoreCLR 或 JIT 行为混淆。
- 发布确切锁定的 `FSharp.Core.dll` 及所有运行期/原生依赖；Unity 不会还原 `.fsproj` 图。
- 向 C# 发布普通 CLR 形状的方法和值，同时把地道 F# 类型留在内部。
- 把 Unity 序列化字段、引擎对象与生命周期回调留在适配器；映射成已验证领域状态。
- 跨重载、启用/禁用、场景与进程生命周期有意重建运行期状态。
- 把输入、时间、随机性与效果显式传给纯逻辑。
- 在目标 Player 中度量帧代码；函数式风格既不保证分配，也不禁止分配。
- 回归测试暴露每步 class 分配后，X44 使用小型 struct 状态。
- IL2CPP 裁剪托管代码、把 IL 转成 C++、调用原生工具链并创建平台包。
- 反射、运行期生成、动态泛型、回调与原生库会扩大 AOT 证明表面。
- 使用窄且经过测试的保留规则；保留并非兼容或行为证据。
- Burst/Jobs 使用独立 HPC# 契约，且 X44 未验证它们。
- 锁定确切 Unity 补丁、模块、包、build profile、后端、裁剪级别、工具与产物。
- 保留日志、PDB、原生符号、哈希与启动结果，使失败仍可归因。
- X44 证明了锁定 F# 插件构建、依赖产物、纯规则、CLR 面向 API 与仓库兼容性。
- 因 Editor 不存在，它没有证明 Unity 6000.3.22f1 导入、Play Mode 或 macOS ARM64 IL2CPP Player。

第 45 章回到普通 .NET 工具：脚本、自动化、包评估、锁定纪律，以及继续学习 F# 的实用地图。
