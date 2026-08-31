---
title: "第 44 章：Unity 6.3 LTS 与 F#"
description: "通过明确的运行时、程序集、序列化、生命周期、性能、AOT、裁剪与 Player 构建边界，在 Unity 中使用 F#。"
translationKey: part-07/ch-44-unity
---

# 第 44 章：Unity 6.3 LTS 与 F# {#overview}

Unity 无需编译 F# 源码，也能执行以 F# 编写的代码。F# 会编译成托管 .NET 程序集，而 Unity 可以导入托管插件。这个技术事实很有用，但它只是完整流程的第一步。

一个能在 `dotnet` 下构建的类库仍可能无法通过 Unity 的引用验证。成功导入的插件仍可能在场景加载时失败。Play Mode 可以正常运行，而 IL2CPP Player 仍可能在提前编译、裁剪、原生链接、启动或某条设备专有路径上失败。因此，真正有用的问题是：“这个 Unity 版本、平台、脚本后端和发布流水线，已经验证了哪一层 F# 集成？”

下面用一个小型托管插件回答这个问题：把游戏规则放在普通 F# 类库中，对外提供 C# 友好的 API，并让简短的 C# 组件处理 Unity 特有行为。随后再讨论何时值得测试直接 F# 组件、何时 F# 收益很小，以及为什么类库构建成功不能代表 Player 可以运行。

::: tip 分两轮阅读
初读时依次掌握[集成层次](#unity-contract-stack)、[选型方法](#decision-map)和[托管插件样例](#x44-verified-slice)。准备代表性 Player 构建时，再按需查阅序列化、游戏循环、IL2CPP、验证与发布各节。
:::

## Unity 集成由多层契约组成 {#unity-contract-stack}

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

| 想报告的结果 | 最低检查 |
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

| 候选边界 | 适合之处 | 主要摩擦 | 首项验证 |
| --- | --- | --- | --- |
| 纯 F# 领域插件 + C# 适配器 | 经济、战斗结算、任务、对话状态、物品栏、程序化规则、存档迁移、服务端共享验证 | DLL/依赖装配与语言边界 | 让一条规则通过 `dotnet` 测试、Unity 导入、Play Mode 与代表 IL2CPP Player |
| 由 Editor 代码使用的 F# 服务/工具库 | 导入验证、内容检查、确定性生成器、构建元数据 | `UnityEditor` 耦合、资源数据库生命周期、batch mode、诊断 | 在交互与批处理 Editor 中跑通一条真实资源流水线 |
| 托管插件中的直接 F# `MonoBehaviour` | 团队接受外部 F# 构建，且只需要很小的组件 | UnityEngine 引用版本、Inspector 与序列化兼容性、组件发现、调试 | 对确切 Editor 程序集编译；导入、挂载、序列化、重载、构建 Player |
| 不使用 F# 的 C# Unity 应用 | 逻辑主要是引擎编排、可视化脚本、Shader、包、Jobs/Burst，或由设计师主导工作流 | 值得交给 F# 的领域规则较少 | 用最简单的 C# 端到端小样与 F# 方案比较，而不是比较语言偏好 |
| 独立 F# 后端或工具进程 | 决定最终结果的模拟、匹配、分析、内容构建或离线工具不必在 Player 中运行 | 网络/进程契约与部署 | 保持 Unity 客户端很薄；独立验证传输协议/版本行为 |

### 通常更容易实现的默认选择 {#recommended-boundary}

新的实验应从不依赖 Unity 的 F# 类库与很薄的 C# 宿主开始。这保留了 F# 最有用的性质——明确类型、纯转换、属性测试和普通 .NET 工具——同时沿用 Unity 最成熟的 C# 工作方式。

C# 层负责：

- `MonoBehaviour`、`ScriptableObject`、自定义 Inspector 与 Unity 特性；
- 场景与 prefab 的序列化字段；
- `GameObject`、`Transform`、`Rigidbody`、资源、句柄及其他 `UnityEngine.Object` 引用；
- `Awake`、`OnEnable`、`Update`、`FixedUpdate`、`OnDisable` 与场景回调；
- 输入包、平台 API、协程、Unity 日志与 Unity 专属异步适配器；
- Unity 值与领域值之间的映射。

F# 层包含离开引擎后仍有意义的决策：标识符与规则、确定性状态转换、存档 schema 与迁移。随机种子作为输入传入，副作用则通过端口交给宿主执行。

### 直接 F# 组件可行，但并非免费 {#direct-fsharp-components}

Unity 的托管插件模型基于 .NET 程序集，而非源码语言身份。派生自 `MonoBehaviour` 的预编译类型原则上可以像其他托管插件类型一样挂载。F# 项目也可以引用某个特定 Editor 安装中的 Unity 程序集。

这并不使直接路径成为默认选择。构建现在依赖确切 Unity 程序集位置与版本，生成的 F# 表示也可能不符合 Inspector 预期。Unity 示例、源码生成器、分析器、包设置、调试器工作流与 Editor 回调都以 C# 为主。每项结果仍需通过导入、挂载、序列化、重载和 Player 验证。

只有端到端小样在度量这些成本后确实更简单，才让 Unity 直接调用 F#。围绕稳定 F# 核心编写十行 C# 组件并不是失败；它只是工具边界上的适配器。

### 知道何时不应再引入 F# {#when-not-to-use}

不要仅为了包装 `transform.Translate`、播放动画或转发一次碰撞回调就引入 F# DLL。额外的编译器、包、导入、符号与互操作 API 必须换来可测试的领域价值。

同样，不要因为游戏其他部分使用 F#，就强行用 F# 编写帧关键的 Burst kernel。Burst 文档规定了 HPC# 子集与 Unity IL 后处理流水线。除非针对所选配置的 F# 实验验证了包、特性、IL、Editor、AOT、性能与 Player 行为，否则应采用受支持的 C# 数据导向形式。

## 托管插件样例：一个已验证的托管插件边界 {#x44-verified-slice}

托管插件样例实现一条水平移动规则。它刻意小到不足以支撑生产架构，其目的只是暴露构建、API、依赖、宿主、内存分配、链接器与验证边界。

### 项目契约与依赖产物 {#project-contract}

```xml:line-numbers [FSharpGameplay.fsproj]
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.1</TargetFramework>
    <AssemblyName>FSharpGameplay</AssemblyName>
    <RootNamespace>ThinkingInFSharp.UnitySample</RootNamespace>
    <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
  </PropertyGroup>

  <ItemGroup>
    <Compile Include="Gameplay.fs" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Update="FSharp.Core" Version="10.1.301" />
  </ItemGroup>

  <Target Name="VerifyUnityPluginOutput" AfterTargets="Build">
    <Error
      Condition="!Exists('$(TargetPath)')"
      Text="The Unity plug-in assembly was not produced at $(TargetPath)." />
    <Error
      Condition="!Exists('$(TargetDir)FSharp.Core.dll')"
      Text="FSharp.Core.dll must be copied beside FSharpGameplay.dll for Unity import." />
  </Target>
</Project>
```
项目目标为 `netstandard2.1`，程序集名为 `FSharpGameplay`，且只编译 `Gameplay.fs`。`FSharp.Core` 已经是 F# SDK 隐式包；`Update` 为这个项目固定 10.1.301 引用，而不是增加重复项。

`CopyLocalLockFileAssemblies` 很重要，因为 Unity 导入 DLL 时不会还原这个 `.fsproj`。后置构建目标把部署假设变成失败条件：输出目录必须同时存在 `FSharpGameplay.dll` 与 `FSharp.Core.dll`。

包版本与程序集版本不是同一个标识符。锁定的 NuGet 包是 10.1.301；构建后的插件记录了对 `FSharp.Core, Version=10.1.0.0` 的程序集引用。应导入锁定构建产生的依赖，而不是从任一数字猜测文件。

### 普通 CLR API 背后的纯逻辑 {#pure-gameplay}

```fsharp:line-numbers [Gameplay.fs]
namespace ThinkingInFSharp.UnitySample

open System

module private Guard =
    let finite parameterName (value: single) =
        if Single.IsNaN value || Single.IsInfinity value then
            invalidArg parameterName "Value must be finite."

    let nonNegative parameterName value =
        finite parameterName value

        if value < 0.0f then
            invalidArg parameterName "Value must be non-negative."

[<Struct; NoEquality; NoComparison>]
type MotionState =
    val private positionX: single
    val private velocityX: single

    internal new(positionX, velocityX) =
        { positionX = positionX
          velocityX = velocityX }

    member this.PositionX = this.positionX
    member this.VelocityX = this.velocityX

[<AbstractClass; Sealed>]
type Gameplay private () =
    static member Create(positionX: single) =
        Guard.finite (nameof positionX) positionX
        MotionState(positionX, 0.0f)

    static member Step(state: MotionState, horizontal: single, speed: single, deltaTime: single) =
        Guard.finite (nameof horizontal) horizontal
        Guard.nonNegative (nameof speed) speed
        Guard.nonNegative (nameof deltaTime) deltaTime

        let normalizedInput = max -1.0f (min 1.0f horizontal)
        let velocityX = normalizedInput * speed
        let positionX = state.PositionX + velocityX * deltaTime

        Guard.finite "resultingVelocity" velocityX
        Guard.finite "resultingPosition" positionX
        MotionState(positionX, velocityX)
```
`Gameplay.Create` 与 `Gameplay.Step` 是元组式静态方法，因此 C# 看到的是普通方法调用，而不是柯里化的 `FSharpFunc` 值。`MotionState` 暴露只读 float 属性，并隐藏字段及非默认构造器。

状态是 struct。较早实现使用 class，因而每次 `FixedUpdate` 都分配新的托管对象。回归测试现在检查 `IsValueType`，并解码 `Gameplay.Step` 的托管方法体以拒绝显式 `box` 指令。它消除了托管构建中的这项特定状态对象分配，但并不假装整个 Player 每帧分配零字节。大型 struct 会带来复制成本，所以应让状态保持小巧并分析真实目标。

转换会夹紧方向输入，拒绝非有限值与负时间或速度，计算速度并返回新状态。它没有 `UnityEngine` 引用，不读取当前时间，也不发生可变更新。测试可以直接提供所有输入。

### Unity 专用的薄适配层 {#csharp-adapter}

```csharp:line-numbers [UnityAdapter.cs]
using ThinkingInFSharp.UnitySample;
using UnityEngine;

namespace ThinkingInFSharp.UnityHost
{
    public sealed class UnityAdapter : MonoBehaviour
    {
        [SerializeField, Min(0.0f)]
        private float speed = 6.0f;

        private MotionState state;
        private float horizontal;

        public void SetHorizontal(float value)
        {
            horizontal = Mathf.Clamp(value, -1.0f, 1.0f);
        }

        private void Awake()
        {
            state = Gameplay.Create(transform.position.x);
        }

        private void FixedUpdate()
        {
            state = Gameplay.Step(state, horizontal, speed, Time.fixedDeltaTime);

            Vector3 position = transform.position;
            transform.position = new Vector3(state.PositionX, position.y, position.z);
        }

        private void OnDisable()
        {
            horizontal = 0.0f;
        }

        private void OnValidate()
        {
            speed = Mathf.Max(0.0f, speed);
        }
    }
}
```
文件与公开 `MonoBehaviour` 类同名为 `UnityAdapter`，保留 Unity 常规脚本/组件工作流。Inspector 只暴露一个基本类型 `speed` 字段。`OnValidate` 检查创作期配置，F# 边界仍会验证运行期调用。

`Awake` 从当前 transform 创建运行期状态。`FixedUpdate` 提供输入值、配置速度和 Unity 固定 delta time，再把返回位置映射回 `Vector3`。这是 transform 示例，不是物理实现建议；由 Rigidbody 控制的对象需要相应物理 API 与测试。

`SetHorizontal(float)` 刻意不选择旧 Input Manager 或 Input System 包。独立输入适配器可以调用它。这样，规则程序集不必知道输入包和回调形式。

C# 文件只是说明性代码，因为书站不包含 UnityEngine 程序集。请把它复制进真实 Unity 项目并在那里编译；模拟引擎类型只能验证模拟宿主。

### 最小化链接器保留根 {#linker-roots}

```xml:line-numbers [link.xml]
<linker>
  <assembly fullname="FSharpGameplay">
    <type fullname="ThinkingInFSharp.UnitySample.Gameplay" preserve="all" />
    <type fullname="ThinkingInFSharp.UnitySample.MotionState" preserve="all" />
  </assembly>
</linker>
```
C# 适配器的直接调用应对静态可达性分析可见。托管插件样例仍包含两个显式根，以展示预期跨程序集桥，并为本章提供具体裁剪产物。

文件没有保留整个 `FSharp.Core`。宽泛保留会隐藏缺失的反射设计、增大 Player，并增加 IL2CPP 工作量。只有真实动态路径需要某个类型或成员时才添加，然后测试对应裁剪级别。

把 `link.xml` 复制到 Unity 项目的 `Assets` 树下。外部 `.fsproj` 旁的源文件在成为 Unity 资源前没有任何作用。

### 严格按验证结果陈述结论 {#evidence-ledger}

本章提供设计；采用它的 Unity 项目必须补全以下验证清单：

| 层 | 必需检查 | 验证内容 |
| --- | --- | --- |
| 锁定 .NET 还原 | 在复制后的项目中运行 | `netstandard2.1` 图解析到选定 FSharp.Core 包 |
| Release 插件构建 | 在复制后的项目中运行 | F# 源码能用选定 SDK 编译 |
| 产物检查 | 检查两个 DLL 与程序集身份 | 插件及其指定的 FSharp.Core 依赖可供导入 |
| 聚焦规则/API 测试 | 在 Unity 外运行 | 夹紧/步进行为、struct 状态与 CLR 面向 API |
| Unity 导入与 C# 编译 | 在选定 Editor 中运行 | UnityEngine 宿主集成可以编译 |
| Play Mode | 用真实场景运行 | 生命周期、输入与场景行为成立 |
| 目标 IL2CPP Player | 构建并启动 | 原生转换、裁剪、链接、启动与运行期行为成立 |

把未运行的项目明确标出仍有价值，因为它限制了主张。可见缺口可以安排和估价；把未验证项目标成通过则不行。

## 面向兼容性配置文件，而非运行时名称 {#compatibility-target}

Unity 6 在 Player 设置中提供 .NET Standard 2.1 与更宽的 .NET Framework profile。.NET Standard profile 是跨平台基线，也是可复用托管插件正确的第一目标。

### API profile 只是编译期上限 {#profile-not-runtime}

`.NET Standard 2.1` 只描述一组 API。它没有规定 Unity 使用哪个 CoreCLR 版本或垃圾回收器，也不保证每个平台都允许 JIT。一个库即使能对该 profile 编译，仍可能依赖 Unity 不支持的实现细节。

在 Player 插件中避免 `net10.0`、`netcoreapp`、操作系统专属目标框架、动态代码生成和意外平台 API。如果某个库需要更宽宿主，应把它放在 Player 外，或增加单独测试的目标专属适配器。

对满足需求的最小 profile 编译，然后在每个发布所用的脚本后端与平台上测试。兼容性取以下范围的交集：

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

### 明确管理 Unity 引用 {#plugin-import}

把托管插件复制到 `Assets` 下，选择平台兼容性，并保持 Validate References 开启。这能比运行期更早发现缺失引用与强名称不匹配。

Auto Reference 对验证性试验（spike）很方便，但它让每个符合条件的脚本程序集都看到插件，并增加重编译与意外耦合。较大项目中应关闭它，并用程序集定义显式引用预编译程序集。把 Editor-only 适配器放入 Editor-only 程序集，并从 Player 平台排除不兼容插件。

绝不要在理解不匹配前，通过关闭程序集版本验证来“修复”引用问题。如果插件要求不兼容的 `FSharp.Core` 版本，应让它们改用同一已测试版本、隔离到不同进程，或拒绝该组合。一个加载上下文无法把两个身份相同的文件当成不同程序集加载。

## 设计从 C# 看来自然的边界 {#design-csharp-boundary}

F# 实现内部可以保持地道。导出的 API 应遵循消费者惯例。

### 优先使用常规 CLR 类型与调用形式 {#clr-shaped-api}

适合 Unity 侧的选择包括：

- 命名空间、密封类或小 struct、PascalCase 方法与只读属性；
- 元组式方法参数，让 C# 调用 `Step(state, input, speed, dt)`；
- 适当使用基本值、枚举、数组、`IReadOnlyList<T>` 与有用途名称的 DTO；
- 只有回调确实是正确契约时才使用 `System.Action` 或 `System.Func`；
- 构造必须强制不变量时使用显式工厂方法。

把 F# list、map、option、result、可辨识联合、柯里化函数与度量单位留在边界内，除非 C# 调用方明确接受其编译后形式。它们都是有效 .NET 类型；代价在于调用复杂度、表示耦合、AOT 验证范围与维护成本。

度量单位会从生成的 .NET 签名中擦除。C# float 不会说明它代表秒、米还是米每秒。用方法名、DTO 字段、验证或不同包装类型保留含义。

### 只翻译一次结果 {#errors-and-outcomes}

不要为每个预期游戏分支抛异常。在内部用联合或 result 建模领域结果，再一次性翻译为 C# 友好的结果类、枚举加负载、`Try...` 方法或显式回调消息。

把异常留给破坏契约且当前调用无法表示的失败。托管插件样例拒绝 NaN、无穷、负速度与负 delta time，因为它们表示无效边界调用。C# 适配器会在到达边界前防止普通创作错误。

异步工作不要向 Unity 泄漏 F# `Async<'T>`。根据宿主提供 `Task`、`ValueTask`、C# 友好的轮询句柄或消息接口。明确谁可以取消，以及结果在哪个线程交付。即使纯计算或 I/O 在其他地方运行，也只能在主线程访问 Unity 对象。

### 把持久数据与引擎对象分开 {#units-and-data}

不要把 `GameObject`、`Transform`、`Texture`、场景句柄、打开的流、取消源或服务单例放进需要保存、重放、测试或发给服务端的领域状态。

在 F# 中使用稳定标识符和值。让适配器把它们解析为当前 Unity 对象。映射可能因场景卸载、资源变更或对象销毁而失败；把它表示为边界结果，而不是假装引用持久。

## Unity 序列化是自己的契约 {#serialization}

Unity 序列化器不是普通 .NET 序列化，也不会持久化任意属性或对象图。

### 从受支持字段开始 {#supported-fields}

可序列化字段必须为 public 或标记 `[SerializeField]`，不能是 static、const 或 readonly，而且字段类型必须受支持。可用类型包括：

- 基本类型、受支持大小的枚举和 Unity 内建值；
- `UnityEngine.Object` 引用与可序列化的自定义 class/struct；
- 数组，以及元素类型受支持的 `List<T>`。

属性不是普通持久化字段。字典、多维或交错数组、嵌套容器需要显式表示或序列化回调。`[SerializeReference]` 会改变引用与多态行为，但也带来自己的身份、迁移与类型名风险。

F# record 通常暴露属性与编译器生成表示。union、option、list、map 和闭包不会因为是托管对象就变成 Unity 字段格式。某种表示可能在一条 Editor 路径上看似可用，却仍在 prefab 持久化、domain reload、裁剪或 Player 构建中失败。

### 做映射，不要教序列化器理解 F# {#map-dont-teach}

把 Inspector 创作配置放在 C# `MonoBehaviour`、`ScriptableObject` 或刻意可序列化的 C# DTO 中。验证它，再构造更丰富的 F# 模型。

对游戏存档，定义独立于场景序列化且带版本的存储 DTO。通过显式迁移与错误报告把 DTO 映射为已验证领域状态。测试旧版本、缺失字段、损坏数据、中断写入、云冲突、降级策略与删除。

这里有意产生三个不同模型：

```text
Unity 创作字段 -> 已验证 F# 运行期模型 -> 带版本的存档/传输 DTO
```

试图让一个生成类型同时满足 Inspector 编辑、不可能状态建模、网络兼容与长期存档迁移，通常会削弱全部四项。

### 跨生命周期变化重建运行期状态 {#reload-and-lifecycle}

Unity 可以重载脚本与程序集，也能从序列化字段重建组件。进入 Play Mode 时，domain/scene reload 行为可以配置；运行期间还会发生场景卸载、对象禁用，以及托管引用仍存在但原生对象已销毁的情况。

把 `Awake` 或另一个明确的组合点视为从序列化配置构造运行时状态。使用 `OnEnable` 与 `OnDisable` 配对订阅和取消。不要假设私有托管缓存能跨重载存活，也不要把看似非 null 的 `UnityEngine.Object` 当成仍有对应原生对象。

托管插件样例在 `Awake` 中重建 `MotionState`，并在 `OnDisable` 中重置输入。它没有验证存档持久化或 domain reload；这些需要更大的 Unity 项目测试。

## 尊重游戏循环与分配预算 {#game-loop}

函数式设计有助于隔离决策；它不会让代码免受帧时间、内存、缓存或线程约束。

### 根据引擎契约选择回调 {#update-and-fixed-update}

把 `Update` 用于帧驱动呈现与采样输入，把 `FixedUpdate` 用于固定步长模拟和物理协调，只为文档规定目的使用较晚/渲染回调。一个固定回调可以在一个渲染帧周围运行零次、一次或多次。

不要只在某个渲染帧可能不运行的回调中读取瞬时按钮边缘。在输入层捕获输入，再把稳定值或排队命令交给模拟步骤。

把时间显式传给纯逻辑。这让暂停、重放、慢动作、确定性测试与服务端一致都可见。确定性还要求受控随机性、迭代顺序、浮点预期，并且没有隐藏墙钟。

### 测量分配，不要陷入风格争论 {#allocation-budget}

F# 管道、闭包、序列、record、union、数组与接口调用，会因表示和用法不同而产生不同的内存分配。“函数式”这个标签本身无法说明是否分配。

在目标设备上分析 development Player。使用 CPU Profiler 的 `GC.Alloc` 列与调用栈，再用代表工作负载和构建配置确认。Editor 度量包含 Editor-only 行为，可能与 Player 不同。

常见热循环风险包括：

- 每帧分配 class、list、sequence、闭包、委托、option 或格式化字符串；
- 通过 object 或接口路径装箱值类型；
- 重复枚举惰性序列，或调用每次返回新数组的 Unity API；
- 复制过大的 struct，导致避免堆反而恶化 CPU/缓存预算；
- 保留短生命数据直到发生大型回收。

优化测到的热点，而不是整个领域。每次用户操作只运行一次的回合结算管道可以优先清晰度；每帧调用数万次的 transform 步骤可能需要紧凑 struct、数组、池或 Burst kernel。

### 只在主线程访问 Unity 对象 {#threading}

大多数 Unity API 与对象只能在主线程使用。当输入是脱离引擎的值且输出不接触 Unity 对象时，纯 F# 计算可以使用 task 或工作线程。

在主线程复制或映射所需值，执行带取消的有界工作，再把结果排队回主线程。包含操作或场景身份，使来自已卸载场景、已禁用组件或已被新请求取代的结果遭到拒绝。

不可变只描述复制出的 F# 值。`Transform`、资源或已销毁 Unity 对象仍遵循引擎的主线程与生命周期契约，因此工作线程应接收脱离引擎的值，并返回同类结果。

## IL2CPP 增加了验证要求 {#il2cpp-and-aot}

IL2CPP 不是“换了优化器的 Mono”。它改变可执行代码产生的时间与方式。

### 遵循真实流水线 {#il2cpp-pipeline}

对 IL2CPP Player，Unity 会：

1. 把项目 C# 与所需包代码编译成托管程序集；
2. 应用托管代码裁剪；
3. 把托管 IL——包括导入的 F# 程序集——转换成 C++；
4. 调用目标平台原生编译器与链接器；
5. 把原生产物与所需数据打包成 Player。

必须安装对应 IL2CPP 模块与原生工具链。通常不支持交叉编译；除文档明确例外（例如受支持 Linux 交叉编译路径）外，应在所需宿主上构建。

Editor 会话通过，并不表示第 2–5 步已经运行。IL2CPP 构建成功后仍需启动与行为测试，因为裁剪或 AOT 缺口可能只在路径执行时出现。

### 明确 AOT 必须能找到的代码 {#aot-risk}

提前编译无法等到运行期再生成任意新代码。以下情况会增大风险：

- 通过名字发现类型或成员的反射；
- 动态生成访问器的序列化器、依赖注入或映射库；
- `Reflection.Emit`、表达式编译与运行期代理生成；
- 泛型虚方法，以及在可达代码中从未生成过具体类型代码的泛型组合；
- 仅由原生代码、字符串、特性或外部数据发现的回调；
- 具有错误签名或架构的平台调用与原生库。

补救办法不是“避免泛型”或“全部保留”。优先使用静态调用，并实例化所需的闭合泛型路径。使用库的 AOT 支持模式，在必要处添加窄根或回调特性，再运行确切 Player 路径。

也要记录失败情况。一个只处理成功路径存档类型的加载器可能通过，而旧多态存档、错误子类型、本地化资源或罕见回调已经被裁剪。

### 把链接器规则当作经过测试的代码 {#reflection-and-stripping}

UnityLinker 分析可达代码，并按选定 Managed Stripping Level 删除代码。当前 Unity 6 文档把 Minimal 标为 IL2CPP 默认值，把 Low 标为未来弃用，并把 Medium 与 High 描述为更激进选择；应记录显式设置，因为默认值可以改变。

当保留元素可以携带 Unity 特性而不污染可复用层时使用 `[Preserve]`。当保留属于集成配置或目标是外部程序集时使用 `link.xml`。准确填写类型与成员名称，把文件放在 `Assets` 下，并测试规则收窄后预期 Player 仍工作。

保留只能防止删除；它无法让不支持的 API、运行期代码生成器、原生二进制或泛型模式变得 AOT 兼容。它也不测试行为。

### Burst 与 Jobs 是独立架构 {#burst-and-jobs}

Burst 文档规定 HPC#：围绕非托管值、Unity collection、job 或函数指针、特性与 IL 后处理的受限高性能 C#/.NET 子集。托管对象、许多运行时服务与普通异常行为都在该 kernel 模型之外。

托管插件样例是托管 F# 插件，没有验证 Burst 或 Job System。仅给 F# 生成的方法添加 `[BurstCompile]`，不能说明它受支持。

当性能分析显示确实需要 Burst 时，可以采用以下边界：

```text
F# 规则与编排
  -> 扁平数组/struct 命令
  -> 小型 C# Job/Burst kernel
  -> 扁平结果值
  -> F# 决策层
```

度量转换成本、调度开销、确定性、安全检查、Editor 编译、Player AOT 与目标性能。如果热 kernel 主导架构，可以让 C# 负责该子系统的更多部分。

## 按层次验证 Unity 项目 {#testing-ladder}

使用能推翻声明的最便宜测试：

1. **纯测试与产物检查：** 规则、不变量、属性、重放、迁移、C# 签名、目标框架、依赖、符号、原生资产与许可证。
2. **导入与 Edit Mode：** 确切 Editor 补丁、干净缓存、引用/平台验证、适配器映射和 Editor 代码。
3. **Play Mode：** 生命周期、场景/prefab 序列化、重载设置、输入、时间与引擎交互。
4. **Player 测试：** 适用时先测 Mono，再测发布 IL2CPP 架构/裁剪、反射路径、日志与符号。
5. **设备/发布测试：** 硬件性能、生命周期、平台服务、打包、签名、升级、遥测与恢复。

把快速 F# 测试留在 Unity 外，并由 Unity 宿主负责适配器周围的 C# 测试。Play Mode 不能替代构建后的 Player。失败时保留确切 Editor/配置/目标/后端/裁剪设置、命令、退出码、日志、测试 XML、转储、符号与产物哈希。

## 让 Player 构建可复现 {#build-and-release}

Unity 是编译器与资源流水线的一部分。像给编译器版本化一样给它版本化。

### 锁定 Editor、模块、包与插件 {#pin-editor}

记录完整 Editor 补丁，而不只写“Unity 6.3”。本样例选择 6000.3.22f1，即 2026-08-25 核对时最新的 6.3 LTS 补丁。它是审阅目标，不代表本机已经安装。

锁定 Unity 包与 F# NuGet 图。从干净锁定还原只构建一次 F# DLL，把确切依赖集复制进 Unity 项目，并用哈希或其他方式标识导入产物。除非平台专属输出有意为之，不要在每个平台 job 中以不同方式重建插件。

应足够频繁地在 CI 中执行干净导入，以发现隐藏本地 Library 状态。决定哪些生成的 Unity metadata 与设置应进源码控制，且不要让开发者上次活动目标悄悄选择发布。

### 每次调用只执行一个显式构建配置 {#build-profiles-and-ci}

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

以稳定事件名与标识符记录领域结果，不要记录整个存档或个人数据。区分游戏命令被拒、插件加载错误、方法被裁剪、原生崩溃和资源问题，让遥测直接指向负责处理的层。

## 开展范围受限的采用试验 {#adoption-spike}

采用 F# 前，限定时间验证一个端到端小样：

- 确切 Unity/模块/F# SDK 版本、NuGet lock 和可重复插件复制；
- 一条已测试领域规则、C# 友好契约和已验证 Inspector 配置；
- 场景/重载/生命周期，带陈旧结果拒绝和主线程返回的可取消异步工作，以及存档迁移/损坏；
- 测得的逐帧 CPU、`GC.Alloc`、内存/复制和裁剪后的反射/泛型路径；
- Play Mode、适用的 Mono、发布 IL2CPP，以及从干净 CI Player 构建到签名；
- 上手、IDE/调试、依赖更新和退回 C# 宿主的路径。

只采用验证通过的边界。F# 可以负责整个确定性模拟、只负责离线规则，或只留在服务端与工具中。整个子系统都用 C# 也可能更简单；这些都是合理的工程结论。

## 练习 {#exercises}

### 练习 1：为三种产品选择语言边界 {#exercise-01}

分别评估以下产品：

1. 一款回合制战术游戏包含复杂的确定性战斗、重放、mod 验证和适中的呈现层。
2. 一款主机动作游戏的主要风险来自数千个物理式实体、Jobs/Burst 性能、平台 SDK 和设计师创作行为。
3. 一条 Unity Editor 内容流水线需要验证对话图、生成本地化报告，并在 CI 中无头运行。

为每个产品记录首选 F# 边界、被拒方案、验证矩阵，以及改用其他方案的触发条件。三个产品可以采用不同的语言划分。


::: details 参考答案

三种产品的主导风险不同。复用同一种语言划分是在优化一致性，而不是优化产品。

#### A. 回合制战术与确定性重放 {#turn-based-tactics}

**初始边界：** 把模拟与领域规则放入 F# 程序集，通过一层很薄的 C# 适配器连接 Unity 的呈现和资源系统。

F# 程序集负责：

- 经过验证的单位、技能、格子、阵营、资源与状态效果标识符；
- 合法行动生成、命令验证、战斗结算、回合顺序、胜利条件与 AI 评估输入；
- 根据先前状态、命令、随机数源或种子以及规则版本，确定性地产生下一状态和事件；
- 以命令、种子、内容版本和校验和构成的重放序列化，而不是场景快照；
- 作为数据并按带版本 schema 与能力策略验证的 mod 内容；
- 守恒、边界、回合合法性与重放等价性的属性测试。

C# 负责场景对象、动画、摄像机、输入、音频、视觉效果、可寻址资源、Inspector 字段，以及把领域事件映射到画面。动画完成回调可以发送呈现消息，但不能决定战斗结果。

对外提供 `ValidateCommand`、`Apply` 和 `TryLoadReplay` 之类常规 CLR 方法，参数使用小型 DTO 或数组。F# 的 union 与 map 留在程序集内部。若模拟状态很大，不要每帧搬运整个对象图。回合结算本身就是事件边界，只需交换一个命令以及一批紧凑的事件和结果。

**Mod 边界：** 接受声明式内容，而非任意下载的托管程序集。进入对局前验证标识符、限制、引用、本地化键、确定性表达式与规则版本。可执行 mod 会引入远超 F# 语言选择的信任、平台、AOT、签名、商店与反作弊问题。

**验证矩阵：**

- 运行纯重放测试和属性测试，再编译一个 C# 调用方；
- 检查插件及其依赖的确切版本，并导入 Unity；
- 在 Play Mode 中重放一个场景，并验证重载前后的保存与加载；
- 若产品发布 Mono Player，则运行其诊断构建；
- 在每种发布用 IL2CPP 架构上测试旧重放和错误 mod；
- 与独立服务端或工具实现比较校验和；
- 在目标硬件上测量性能与内存。

**首先拒绝的方案：**

- 直接使用 F# `MonoBehaviour`，会把确定性规则耦合到场景和 Inspector 表示；
- 让动画回调决定战斗结果，会破坏重放；
- 把所有呈现事件都放入 F# DLL，会引入对领域没有价值的引擎细节。

**反转条件：** 若跨语言转换占据大部分回合耗时、调试代价无法接受，或 IL2CPP 无法编译必需的库路径，就缩小 F# 边界。保留数据传输与重放契约，使实现迁移不会破坏存档。

#### B. 使用 Jobs 与 Burst 的主机动作游戏 {#console-action-game}

**初始边界：** 把帧循环关键路径上的 Unity/DOTS/Burst 应用留在 C#。F# 只用于收益明确的部分，例如低频元游戏规则、构建工具、与后端共享的验证或离线分析。第一天不让 Player 强制依赖 F#。

主导约束是实体数量、数据布局、Burst 的 HPC# 子集、调度、原生平台 SDK、设计师工作流、帧时间与主机认证。仅把源码放进另一种语言并不会改善它们。

若成长、物品栏、经济、任务规划或匹配规则变复杂，可在事件边界增加小型 F# 领域插件。批处理前后交换扁平数组或紧凑 struct，不要在 Job 内使用回调或 F# 集合。`NativeArray`、组件数据、特性、安全句柄、调度与 Burst 编译仍由 C# Job 处理。

**验证矩阵：** 在目标硬件的开发版 Player 中运行代表性实体负载。记录 CPU/GPU 时间线、`GC.Alloc`、Job 依赖、同步点、内存带宽、热表现和帧时间分位数。还要验证每种主机架构上的 IL2CPP 与 Burst AOT、平台 SDK 回调、符号、崩溃捕获和认证构建流程。若加入 F# 元游戏插件，还需执行本章其他方案中的依赖、导入和 AOT 检查。

**首先拒绝的方案：**

- 为每个 Job 包一层 F#，只会增加互操作，并未移动业务决策；
- 目前没有实测结果支持直接给 F# 代码使用 Burst；
- 每帧跨边界复制大型不可变世界快照，违背数据导向设计。

**反转条件：** 若剩余规则一直很简单，或语言边界增加了主机支持成本，就完全不引入 F#。只有非热路径子系统在建模和测试上的收益已超过打包与调试成本时，才把它移到 F#。

#### C. 无头 Unity Editor 内容流水线 {#editor-content-pipeline}

**初始边界：** 使用一个不依赖 Unity 的 F# 验证与报告库，再增加小型 C# Editor 和批处理模式适配器。

F# 库负责从稳定 DTO 解析对话图、检查引用、发现环与不可达节点、统计本地化覆盖率、划分严重级别，并根据明确输入生成确定的报告条目。常规 .NET 测试可使用小型样本，无需启动 Unity。

C# 适配器负责 `AssetDatabase`、导入回调、GUID 与路径查找，以及 `UnityEditor` 的进度与取消。它还提供菜单或 EditorWindow UI、Console 诊断信息，以及供 Unity 调用的静态批处理入口。调用 F# 前，它先把资源数据复制为不依赖引擎的 DTO。调用结束后，再把每项问题映射回资源路径以及行号或节点标识。

通过命令行调用一次指定的 Editor 可执行文件。基本参数包括 `-batchmode`、`-quit`、`-projectPath`、`-executeMethod` 与 `-logFile`。若命令还会构建 Player，则同时指定 target 和 profile。内容验证失败应返回非零退出码，基础设施失败则使用另一退出码。另写一份机器可读报告，避免 CI 抓取本地化的 Console 文本。

**验证矩阵：** 测试纯函数样本、Editor 交互式选择与取消、无递归的导入和重新导入，以及干净项目导入。在同一提交上运行两次批处理并比较报告。还应覆盖错误或超大对话图、本地化编码、日志与报告保留、包升级，并确认运行时 Player 不包含 Editor-only 程序集。

**首先拒绝的方案：**

- 让 F# 类库引用 `UnityEditor`，会提高测试成本并扩大版本耦合；
- 不经 Unity 而让 `dotnet` 直接处理原始 `.meta` 文件，可能误判导入后的资源状态；
- 只提供 Editor UI，会使 CI 没有可调用的入口。

**反转条件：** 如果规则依赖运行中的 Editor 对象，而且 DTO 映射代码比规则本身还多，就把该规则移入 C#。只有源格式、GUID 解析和导入语义都真正独立于 Unity 时，才把整个验证器移出 Unity。

:::

### 练习 2：把托管插件样例变成 Unity 端到端小样 {#exercise-02}

设计最小 Unity 项目与验证记录，把托管插件样例从“托管 DLL 能构建”推进到“代表性 macOS ARM64 IL2CPP Player 可以运行”。把记录分成四组：

- **程序集导入：** 产物复制、`FSharp.Core` 身份、程序集定义和 Validate References。
- **Editor 行为：** 场景与输入设置、Edit/Play Mode 测试、重载行为和分配分析。
- **Player 构建：** 裁剪级别、`link.xml`、命令行 build profile、启动、日志和符号。
- **验证状态：** 每一行对应的确切命令、结果、产物和失败含义。

只有对应步骤真实执行后，才能更新“未运行”状态。


::: details 参考答案

目标不是增加功能，而是把每个未经验证的边界执行一次，并保留可复现结果。

#### 建议项目图 {#vertical-slice-graph}

使用两个构建根，并为复制的产物定义一份契约：

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

不要提交 `Library`、`Temp` 或本地构建输出。可以提交导入的插件二进制并记录更新命令，也可以在 Unity 导入项目前生成它们。无论采用哪种方式，CI 都必须比较哈希，拒绝过期或混合版本的文件。

#### 产物与程序集契约 {#artifact-contract}

使用项目选定的 .NET SDK。先运行 `dotnet restore --locked-mode`，再用 `--no-restore` 执行 Release 构建。只复制：

- `FSharpGameplay.dll`；
- 由 10.1.301 包解析得到、位于输出目录旁的 `FSharp.Core.dll`；
- 诊断构建所需 portable PDB；
- 同一提交中的 `UnityAdapter.cs` 与 `link.xml`；
- 一份生成的 manifest，其中记录各文件的 SHA-256 和大小、两个程序集的标识、包版本、提交以及构建命令。

若必需文件缺失或哈希不符，应在启动 Unity 前失败。不要复制引用程序集，不要把 `.deps.json` 当成解析器，也不要从全局 NuGet 缓存随意取 DLL。

#### Unity 程序集与场景边界 {#unity-project-boundary}

把 `ProjectVersion.txt` 锁定为 Unity 6000.3.22f1，并在 macOS 构建机上安装 macOS IL2CPP 模块。把 API Compatibility Level 设为 .NET Standard，同时开启 Validate References 和程序集版本验证。

为适配器创建运行时程序集定义。关闭插件的 Auto Reference。若当前 Unity 配置要求显式引用插件，则引用 `FSharpGameplay.dll` 与 `FSharp.Core.dll`。测试可以引用运行时适配器程序集，运行时程序集不能反向引用测试。所有 Editor-only 测试和代码都不得进入 Player。

创建一个场景，包含命名 GameObject 与 `UnityAdapter`、可见位置标记，以及确定性测试输入适配器。输入测试通过 `SetHorizontal` 发送 `-1`、`0`、`1` 与越界值；它不依赖物理控制器或项目范围输入包。

Edit Mode 测试验证 DTO 与映射辅助函数、插件类型加载和程序集标识。Play Mode 测试覆盖 `Awake` 初始化、经过已知固定步数后的正向与负向移动，以及禁用与重置。它还应检查 Console 异常、场景重载，以及选定的域重载和场景重载设置。

#### 验证性能与裁剪 {#performance-and-stripping-proof}

应分析开发版 Player，而不只是 Editor。预热后捕获固定步进帧，并检查适配器与步进路径是否满足约定的分配预算。把 `MotionState` 定义为 struct 可避免一次类分配，但 Unity 调用、测试框架、日志与输入仍可能产生分配。

选择并记录一个 managed stripping level。从发布预期设置开始，不要为了让构建通过而逐步降低裁剪强度。执行两个公开桥接类型和每条动态发现路径。验证某项 `link.xml` 配置时，可暂时删除它并重新运行相关行为。只有负向测试失败所需的保留根才应留下。

同时构建便于查看调用栈和符号的诊断 IL2CPP profile，以及接近正式发布设置的 profile。诊断构建通过，并不代表优化裁剪后的构建也能正常工作。

#### 可复现构建与启动 {#build-and-launch}

为 macOS profile 调用一次指定的 Editor 可执行文件。传入 `-batchmode`、`-quit`、`-projectPath`、`-activeBuildProfile` 与 `-logFile`。不要在构建方法内部切换目标。凡项目警告策略未允许的警告都应导致失败。

构建后：

1. 验证退出码并解析结构化构建结果，不要只搜索一个成功字符串；
2. 归档 Editor 日志、测试 XML、构建报告、插件 manifest、符号与 Player 哈希；
3. 检查 Player 架构与签名状态；
4. 在图形化 macOS 会话中带超时启动 `.app`；
5. 等待包含构建与插件身份的机器可读 ready 标记；
6. 驱动或自动运行代表移动/生命周期检查；
7. 收集 Player 日志与退出状态；
8. 正常终止进程，并保留每次失败运行的产物。

自动化会话若没有可用的图形环境，应记为环境失败，而不是应用结果。随后在预期的交互式环境或 CI 运行器上重试，并保留两次记录。

#### 验证记录与失败分类 {#vertical-slice-evidence}

独立记录各行：

| 检查项 | 通过条件 | 责任范围 |
| --- | --- | --- |
| 锁定 F# 构建 | 指定 SDK 与 lock，0 警告/错误，生成预期文件包 manifest | F# 源码/包流水线 |
| 干净 Unity 导入 | 指定补丁版本、干净导入、引用有效、无编译/导入错误 | 资源/插件集成 |
| Edit Mode | 映射与身份通过，并输出 XML | 适配器/程序集配置 |
| Play Mode | 场景、生命周期、重载、移动与 Console 通过 | Unity 宿主行为 |
| 分配 | 目标 Player 捕获满足命名预算 | 表示/热路径 |
| IL2CPP 构建 | 显式 profile/后端/裁剪/架构完成 | 链接器/AOT/原生工具链 |
| Player 启动 | 命名产物启动并发出 ready 身份 | 包/运行时/环境 |
| Player 行为 | 代表检查与日志通过 | 集成应用 |
| 诊断 | 刻意失败可符号化到有用 F# 与 C# 帧 | 符号/崩溃流水线 |

只有全部检查通过，才能作出以下结论：

> 托管插件的代表性切片能在该 build profile 下的 Unity 6000.3.22f1 macOS ARM64 IL2CPP Player 中工作。

这个结果不涵盖 Windows、移动端、主机、Web、其他裁剪级别或整个游戏。

:::

### 练习 3：增加存档、异步操作与动态内容 {#exercise-03}

扩展架构以支持一套任务系统：规则使用 F#，配置在 Unity 中创作，存档跨三个版本迁移，远程对话异步到达，内容还会指定可选任务处理器。

覆盖四类边界：

- **创作与存档数据：** 创作 DTO、已验证领域类型、存档 DTO 和迁移。
- **公开行为：** C# API，以及取消和陈旧结果消息。
- **AOT 发现：** 避免无限制运行时代码生成的处理器注册，以及窄保留规则。
- **运行时验证：** 错误内容、旧内容测试，以及 Mono 和 IL2CPP 结果。

无法安全完成 AOT 发现的功能应放在 Player 之外。


::: details 参考答案

把创作数据、验证后的运行时状态、持久存储和处理器发现分开。

#### 职责不同的四种模型 {#quest-models}

使用这些边界：

| 模型 | 管理方 | 表示形式 |
| --- | --- | --- |
| 任务创作 DTO | C#/Unity | `[Serializable]` 类/struct、受支持字段、资源 GUID、基本类型 list |
| 已验证任务定义/状态 | F# | 私有构造器、内部 record/union/map、不含 Unity 对象 |
| 公开桥接 DTO | 面向 CLR 的 F# 类型或 C# | 枚举、小型 struct/class、数组、静态方法、明确的结果与错误载荷 |
| 存档 DTO | 带版本存储契约 | 稳定 ID、基本/数组数据、schema 版本、校验和；无场景引用 |

C# 适配器读取创作字段或资源，把它们复制为桥接 DTO，再调用 `QuestApi.ValidateDefinitions`。验证会一次返回所有可操作错误，并附任务、节点和字段的定位信息；普通的内容错误不会触发异常。

验证后，由 F# 模型保证约束成立：任务 ID 不为空，转换只指向已知节点，完成与取消保持为不同状态，奖励有效，处理器名属于 allowlist。Unity 只接收紧凑的呈现快照以及发出的命令和事件。

#### 通过纯迁移给存档版本化 {#save-migrations}

定义三个显式持久 schema，而不是反序列化今天的领域类型：

- v1 存储任务 ID 与已完成节点 ID；
- v2 增加目标进度，并在 v1→v2 迁移时推导显式默认值；
- v3 以内容版本加稳定任务键替换原始任务 ID，并记录进行中操作身份。

解析到版本专属 DTO，验证大小/校验和，逐步迁移，再构造当前领域状态。在新状态验证并原子写入前保留原始字节。未知未来版本必须安全失败且不覆盖数据。

黄金测试样本覆盖有效 v1/v2/v3、缺失可选字段、重复 ID、未知任务内容、损坏或截断数据、超大集合、中断替换、降级与迁移幂等。先在常规 .NET 测试中运行，再通过实际发布的 Unity Player 序列化器和文件适配器运行。

#### 把异步结果建模为消息 {#async-quest-effects}

每个远程对话请求都要带操作 ID、任务版本、内容版本，以及负责取消它的组件或会话。F# 状态转换发出 `FetchDialogue` 并进入 loading 状态。C# 宿主执行网络请求，工作线程不得接触场景对象。请求结束后，它派发 completed、unavailable、cancelled、malformed 或 failed 五种消息之一。

只有操作 ID、任务、内容版本与当前状态都匹配时，update 才接受完成。场景卸载、组件禁用、新请求、登出或内容更新会取消或取代旧操作。迟到响应成为被忽略的诊断事件，而非新 UI。

在执行会改变状态的请求前，持久化足够的标识，以便进程终止后恢复结果不明的操作。不要持久化 cancellation token、task、`UnityWebRequest` 对象、委托或 GameObject。

#### 用闭合注册表替换无限制反射 {#closed-handler-registry}

内容可以命名可选处理器，但必须从编译的 allowlist 选择：

| 内容名称 | 静态注册操作 |
| --- | --- |
| `grant-item` | 验证物品 ID/数量并发出授予命令 |
| `set-flag` | 验证 flag/值并发出 flag 命令 |
| `start-timer` | 验证持续时间并发出启动计时器命令 |

通过可达代码中的明确调用构建注册表。公开 API 可以接受处理器名与载荷 DTO，但不能接受任意程序集限定类型名。未知名称属于内容验证错误。这样更容易控制信任边界、迁移、工具、裁剪与 IL2CPP 行为。

若某个库会在内部反射已知 DTO 成员，应选择其文档规定的 AOT 模式，并在受支持时于构建阶段生成元数据。只添加该库真正需要的保留条目，再在预期的 IL2CPP 与裁剪配置中运行每个已注册处理器和错误路径。

当内容真正需要任意可执行扩展时，把执行留在受控服务端/工具进程，或采用刻意沙箱化且平台支持的数据语言。不要把下载的托管程序集塞进已签名 IL2CPP Player，再把 `link.xml` 叫作沙箱。

#### 完整验证清单 {#quest-evidence}

至少验证：

- 任务转换属性测试与处理器 allowlist 完整性；
- 黄金存档迁移，以及损坏、旧版、未来版本与超大载荷；
- C# 调用方和公开 API 的反射测试，并检测意外暴露的 F# 类型；
- Unity 创作内容跨 prefab/资源保存与脚本重载的往返；
- 取消、超时、进程丢失、迟到响应、内容版本变化与重复回调场景；
- 使用指定依赖版本与程序集标识的干净导入；
- 在指定裁剪级别下，运行 Mono（若发布）与每个 IL2CPP 架构中的全部处理器；
- 目标设备上的分配、延迟、离线行为、内存、挂起与恢复、日志、符号和崩溃捕获；
- 新内容版本或存档迁移撤回时的回滚行为。

**反转条件：** 如果动态扩展无法收敛为闭合且可测试的 AOT API，就把它放到 Player 外执行。保留仍有价值的任务协议与 F# 领域模型，但不要为了维持一种实现而保留不受限制的扩展机制。

:::


## 资料来源 {#sources}

- [Unity 6000.3.22f1 发布说明](https://unity.com/releases/editor/whats-new/6000.3.22f1)
- [Unity 6.3 手册：托管插件](https://docs.unity3d.com/6000.3/Documentation/Manual/plug-ins-managed.html)
- [Unity 6.3 手册：IL2CPP 脚本后端](https://docs.unity3d.com/6000.3/Documentation/Manual/scripting-backends-il2cpp.html)
- [Unity 6.3 手册：从命令行构建 Player](https://docs.unity3d.com/6000.3/Documentation/Manual/build-command-line.html)
- [Unity 6.3 手册：程序集定义属性](https://docs.unity3d.com/6000.3/Documentation/Manual/class-AssemblyDefinitionImporter.html)

第 45 章回到普通 .NET 工具：脚本、自动化、包评估、锁定纪律，以及继续学习 F# 的实用地图。
