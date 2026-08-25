# Unity 验证记录 / Unity Validation Record

本文件同时保存一次真实的环境记录和可复用的人工协议。它刻意不把 .NET 类库编译扩大成 Unity 兼容结论。

This file stores both an actual environment record and a reusable manual protocol. It deliberately does not turn a .NET class-library build into a Unity compatibility claim.

## 1. 当前记录 / Current record

| 字段 / Field | 观察值 / Observed value |
| --- | --- |
| Inspection date / 检查日期 | `2026-08-25` |
| Repository target / 仓库目标 | Unity `6000.3.22f1` (Unity 6.3 LTS review target) |
| Host / 主机 | macOS `26.3` (`25D125`), Darwin `25.3.0`, arm64 |
| Editor discovery / 编辑器发现 | `/Applications/Unity/Hub/Editor` absent |
| Installed Editor / 已安装编辑器 | `not run` — no Unity installation found |
| Player module / 平台模块 | `not run` — no Unity installation found |
| Planned target / 计划目标 | macOS ARM64, IL2CPP, managed stripping enabled |
| Plug-in target / 插件目标 | `netstandard2.1`, `FSharp.Core 10.1.301` |

`6000.3.22f1` 是 2026-08-25 选定并复核发布说明的**人工目标**。这并不表示本机安装或运行过该版本。

`6000.3.22f1` is the **manual review target** selected after checking its release notes on 2026-08-25. It does not mean that version was installed or run here.

## 2. 结果边界 / Result boundary

| Check / 检查 | Required environment / 所需环境 | Status / 状态 | What this status means / 含义 |
| --- | --- | --- | --- |
| Release build of `FSharpGameplay.fsproj` | .NET SDK 10.0.301 | `passed` — 2026-08-25 | `pnpm test` exited 0; `check:examples` completed the locked Release solution build |
| Pure transition and CLR-facing API tests | .NET test host | `passed` — 2026-08-25 | The same gate completed the registered Unity plug-in contract tests |
| Adjacent `FSharpGameplay.dll` and `FSharp.Core.dll` | .NET build output | `passed` — 2026-08-25 | The project post-build target and registered assembly/dependency checks completed |
| Managed plug-in import and reference validation | Unity 6000.3.22f1 Editor | `not run` — Editor absent | 没有导入资产或检查 Inspector/Console |
| `UnityAdapter.cs` compilation | Unity 6000.3.22f1 Editor | `not run` — Editor absent | C# 文件依赖 `UnityEngine`，不在普通 .NET 编译中冒充验证 |
| Play Mode motion behavior | Unity 6000.3.22f1 Editor | `not run` — Editor absent | 未观察负、零、正输入与 Transform |
| macOS ARM64 Player build | Unity + macOS module, IL2CPP | `not run` — Editor/module absent | 未运行 IL2CPP 或 managed stripping |
| Built Player launch and behavior | Built macOS Player | `not run` — no Player | 未启动 Player，未取得 Player 日志 |

前三行来自 2026-08-25 在上述主机执行的 `env CI=true pnpm test`，退出码为 0；决定性输出为 `Example build and execution checks passed.`。该命令覆盖整个示例矩阵，前三行的具体契约由 `examples/manifest.json`、项目的 `VerifyUnityPluginOutput` target 与注册测试共同定义。后五行只能由对应 Unity 环境刷新。自动门禁通过而 Unity 仍缺失时，Unity 结果仍为 `not run`。

The first three rows come from `env CI=true pnpm test` on the host above on 2026-08-25, with exit code 0 and the decisive output `Example build and execution checks passed.` The command covers the complete example matrix; `examples/manifest.json`, the project's `VerifyUnityPluginOutput` target, and the registered tests define the three specific contracts. Only the matching Unity environment can change the remaining five. A passing automated gate leaves the Unity results at `not run` when Unity is absent.

## 3. 自动化能证明什么 / What automation can prove

权威项目是 `examples/ecosystem/unity/FSharpGameplay/FSharpGameplay.fsproj`。自动门禁应当证明：

- `netstandard2.1` Release 构建成功；
- `Gameplay.fs` 中的纯状态转换和面向 CLR/C# 的公开形状通过测试；
- `FSharpGameplay.dll` 的程序集引用指向预期 `FSharp.Core`；
- 相同构建输出目录中存在 `FSharpGameplay.dll` 与锁定的 `FSharp.Core.dll`。

The authoritative project is `examples/ecosystem/unity/FSharpGameplay/FSharpGameplay.fsproj`. Automation should prove its `netstandard2.1` Release build, pure state transition and CLR/C#-facing API tests, expected assembly reference to FSharp.Core, and presence of both DLLs in the same build output.

Automation does **not** compile `UnityAdapter.cs`, import assets, run Unity serialization/lifecycle code, exercise Play Mode, invoke IL2CPP, test linker stripping, build a Player, or launch it.

自动化**不能**证明 `UnityAdapter.cs` 编译、资产导入、Unity 序列化/生命周期、Play Mode、IL2CPP、linker stripping、Player 构建或启动。

## 4. 可复用人工协议 / Reusable manual protocol

### A. 固定并记录环境 / Pin and record the environment

Before changing any status, record all of the following in a dated copy of this file or in a new review record:

在更改任何状态前，把以下信息写入带日期的副本或新审阅记录：

```text
Repository commit (full SHA):
Unity Editor exact patch:
Unity project template and packages-lock.json:
Host OS and architecture:
API Compatibility Level:
Build target and architecture:
Scripting backend:
Managed stripping level:
FSharpGameplay.dll SHA-256:
FSharp.Core.dll version and SHA-256:
Editor.log path:
Player build log path:
Player log path:
```

Do not substitute another Unity patch silently. If the exact target is unavailable, record the replacement as a new test environment and keep the original target result unchanged.

不要静默替换 Unity 补丁版本。若精确目标不可用，把替代版本记录为新的测试环境，并保留原目标的状态。

### B. Build the managed plug-in / 构建托管插件

From the repository root:

```console
dotnet restore examples/ecosystem/unity/FSharpGameplay/FSharpGameplay.fsproj --locked-mode
dotnet build examples/ecosystem/unity/FSharpGameplay/FSharpGameplay.fsproj --configuration Release --no-restore
```

Compute hashes for these exact outputs and keep them together:

对以下两个精确产物计算哈希，并始终成对保留：

```text
examples/ecosystem/unity/FSharpGameplay/bin/Release/netstandard2.1/FSharpGameplay.dll
examples/ecosystem/unity/FSharpGameplay/bin/Release/netstandard2.1/FSharp.Core.dll
```

Import the pair generated by the same build. Do not replace FSharp.Core with an arbitrary copy and do not retain two FSharp.Core versions in the Unity project.

### C. Import into Unity / 导入 Unity

1. Create or open the recorded Unity project and set API Compatibility Level to **.NET Standard**.
2. Copy both DLLs into `Assets/Plugins/ThinkingInFSharp/`; keep **Validate References** enabled and constrain platforms only when the project has an explicit reason.
3. Copy `UnityAdapter.cs` to `Assets/Scripts/` and `link.xml` below `Assets/`.
4. Wait for a complete script reload. Record all Console errors and relevant warnings; a clean import means zero compiler or duplicate-assembly errors, not merely that the progress bar stopped.
5. Inspect the assembly and adapter in the Unity Inspector. Confirm there is exactly one intended FSharp.Core and that `UnityAdapter` is attachable.

### D. Exercise Play Mode / 运行 Play Mode

Attach `UnityAdapter` to a GameObject, then observe the object under three explicit inputs:

把 `UnityAdapter` 挂到 GameObject，并对三个明确输入观察对象：

| Input / 输入 | Required observation / 必须观察 |
| --- | --- |
| Negative horizontal value | Position changes in the negative direction at the configured rate |
| Zero | Position remains stable apart from explicitly documented external effects |
| Positive horizontal value | Position changes in the positive direction at the configured rate |

Record the configured speed, duration or fixed-step count, starting and ending positions, Console result, and whether a domain reload changes initialization behavior. A visual “seems to move” is not sufficient evidence.

记录 speed、持续时间或 fixed-step 次数、起止位置、Console 结果，以及 domain reload 是否影响初始化。仅写“看起来在动”不构成证据。

### E. Build and launch the Player / 构建并启动 Player

1. Select macOS ARM64, IL2CPP, and the recorded managed stripping level; keep `link.xml` in the build.
2. Produce a Development build first so logs are diagnosable. Save the exact build result and log path.
3. Launch the built Player, repeat negative/zero/positive behavior, quit normally, and save the Player log.
4. Treat missing methods, type initialization failures, duplicate assemblies, linker warnings promoted by policy, crashes, or behavior differing from Play Mode as failures.
5. If the Development build passes, record separately whether a non-Development build was made and launched. Do not infer it.

### F. Update the evidence table / 更新证据表

Use exactly `passed`, `failed`, `not run`, or `not applicable`. For each `passed`, include the exact Unity patch, target, backend, stripping level, artifact hashes, result, and log paths. A failure remains visible until a retest identifies the fixing commit and new artifacts.

严格使用 `passed`、`failed`、`not run` 或 `not applicable`。每个 `passed` 都要附带精确 Unity 补丁、目标、backend、stripping level、产物哈希、结果与日志路径。失败必须保留，直到复测记录修复提交和新产物。

## 5. Sources / 来源

- [Unity 6000.3.22f1 release notes](https://unity.com/releases/editor/whats-new/6000.3.22f1)
- [Unity .NET profile support](https://docs.unity3d.com/Manual/dotnet-profile-support.html)
- [Import and configure plug-ins](https://docs.unity3d.com/Manual/plug-in-inspector.html)
- [Unity serialization rules](https://docs.unity3d.com/Manual/script-serialization-rules.html)
- [IL2CPP overview](https://docs.unity3d.com/Manual/scripting-backends-il2cpp.html)
- [Managed code stripping](https://docs.unity3d.com/Manual/managed-code-stripping.html)
- [Link XML formatting reference](https://docs.unity3d.com/Manual/managed-code-stripping-xml-formatting.html)
- [FSharp.Core 10.1.301 package metadata](https://www.nuget.org/packages/FSharp.Core/10.1.301)
