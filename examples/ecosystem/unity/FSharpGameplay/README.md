# F# gameplay plug-in for Unity 6.3 LTS

This sample draws a narrow, testable boundary:

- `Gameplay.fs` is a pure F# library with no `UnityEngine` reference.
- `UnityAdapter.cs` is the Unity-owned `MonoBehaviour`, serialization, lifecycle, time, and transform adapter.
- `link.xml` preserves only the two public bridge types; it does not preserve all of `FSharp.Core`.

The public F# surface uses an ordinary .NET value type, properties, and tupled static methods. It does not expose F# functions, options, lists, or discriminated unions to C#. `MotionState` is a struct so the fixed-step transition does not allocate a state class on every tick.

## Automated evidence

Build from the repository root:

```console
dotnet restore examples/ecosystem/unity/FSharpGameplay/FSharpGameplay.fsproj
dotnet build examples/ecosystem/unity/FSharpGameplay/FSharpGameplay.fsproj \
  --configuration Release --no-restore
```

The project targets `netstandard2.1` and pins `FSharp.Core` 10.1.301 to the repository's .NET 10.0.301 toolchain. `CopyLocalLockFileAssemblies` copies the dependency next to the plug-in, and the post-build target fails unless both of these files exist:

```text
bin/Release/netstandard2.1/FSharpGameplay.dll
bin/Release/netstandard2.1/FSharp.Core.dll
```

Import the exact `FSharp.Core.dll` produced by this build. `FSharpGameplay.dll` declares that assembly reference; replacing it with an arbitrary version or keeping two versions in one Unity project is not a supported deployment strategy. The repository test also calls the CLR-friendly API and checks the declared `FSharp.Core` reference.

This automated evidence proves .NET compilation, the pure transition, the public C#-oriented shape, and dependency copying. It does **not** compile `UnityAdapter.cs`, import an asset into Unity, run the Editor, exercise IL2CPP, or build a Player.

## Unity import recipe

The review target is **Unity 6000.3.22f1**, selected on 2026-08-25 as the current Unity 6.3 LTS patch. Use that exact patch when recording the manual evidence below.

1. Create a project with API Compatibility Level set to **.NET Standard** (the Unity 6 profile is .NET Standard 2.1).
2. Copy `FSharpGameplay.dll` and the adjacent `FSharp.Core.dll` into `Assets/Plugins/ThinkingInFSharp/`.
3. Keep **Validate References** enabled for both managed plug-ins.
4. Copy `UnityAdapter.cs` into `Assets/Scripts/` and `link.xml` into `Assets/` or a subdirectory of `Assets/`.
5. Attach `UnityAdapter` to a GameObject. The file and `MonoBehaviour` class deliberately have the same name.
6. Feed `SetHorizontal(float)` from the chosen input adapter. The sample does not force either the legacy Input Manager or the Input System package.
7. In Play Mode, verify negative, zero, and positive input; also inspect the Console for missing assemblies or type-load failures.
8. Build and launch a representative **macOS ARM64 IL2CPP Player** with managed stripping enabled. A successful Editor import or Mono Player is not IL2CPP evidence.

Unity serializes only the C# `speed` field. `MotionState` is runtime state rebuilt in `Awake`; Unity is not asked to serialize an F# record, union, option, list, or property. If a game needs durable state, map it explicitly to Unity-supported fields or a versioned save DTO.

Direct C# calls are normally visible to UnityLinker. The narrow `link.xml` makes the intended bridge roots explicit for this teaching sample. Any later reflection, generic instantiation, or dynamically discovered handler needs its own AOT and stripping tests; preserving all of `FSharp.Core` would hide that design work and inflate the Player.

## Manual evidence record

Environment inspection on 2026-08-25 found no `/Applications/Unity/Hub/Editor` directory, so Unity was not installed in this workspace. These rows are deliberately recorded as **not run**, not as passes:

| Evidence | Exact target | Status |
| --- | --- | --- |
| Managed plug-in import and reference validation | Unity 6000.3.22f1 | Not run — Editor absent |
| C# adapter compilation and Play Mode motion | Unity 6000.3.22f1 Editor | Not run — Editor absent |
| Representative Player build and launch | macOS ARM64, IL2CPP, managed stripping | Not run — Editor/module absent |

Update this table only with the exact Editor patch, target, scripting backend, stripping level, build result, launch result, and relevant log path. Do not convert the automated .NET build into a Unity checkmark.

## Official sources reviewed

- [Unity 6000.3.22f1 release notes](https://unity.com/releases/editor/whats-new/6000.3.22f1)
- [Unity .NET profile support](https://docs.unity3d.com/Manual/dotnet-profile-support.html)
- [Import and configure plug-ins](https://docs.unity3d.com/Manual/plug-in-inspector.html)
- [Unity serialization rules](https://docs.unity3d.com/Manual/script-serialization-rules.html)
- [IL2CPP overview](https://docs.unity3d.com/Manual/scripting-backends-il2cpp.html)
- [Managed code stripping](https://docs.unity3d.com/Manual/managed-code-stripping.html)
- [Link XML formatting reference](https://docs.unity3d.com/Manual/managed-code-stripping-xml-formatting.html)
- [F# component design guidelines](https://learn.microsoft.com/dotnet/fsharp/style-guide/component-design-guidelines)
- [FSharp.Core 10.1.301](https://www.nuget.org/packages/FSharp.Core/10.1.301)

---

# 面向 Unity 6.3 LTS 的 F# 游戏逻辑插件

本例刻意保持一条很窄、可测试的边界：`Gameplay.fs` 是不引用 `UnityEngine` 的纯 F# 类库；`UnityAdapter.cs` 独占 `MonoBehaviour`、Unity 序列化、生命周期、时间与 Transform；`link.xml` 只保留两个公开桥接类型，而不粗暴保留整个 `FSharp.Core`。

F# 的公开面只使用普通 .NET 值类型、属性和元组式静态方法，不向 C# 暴露 F# 函数、option、list 或可辨识联合；`MotionState` 是 struct，因此固定步长转换不会在每次 tick 分配状态类。仓库自动验证 `netstandard2.1` Release 编译、纯状态转换、程序集对 `FSharp.Core` 的引用，以及 `FSharpGameplay.dll` 旁确实复制了锁定的 `FSharp.Core.dll`。

导入 Unity 时必须同时复制本次构建产生的两个 DLL；不要随意替换 `FSharp.Core.dll`，也不要让一个 Unity 项目保留多个版本。将 DLL 放入 `Assets/Plugins/ThinkingInFSharp/`，保持 Validate References 开启；将同名的 `UnityAdapter.cs` 放入 `Assets/Scripts/`，将 `link.xml` 放在 `Assets/` 下。输入系统只需调用 `SetHorizontal(float)`，因此本例不强迫选择旧 Input Manager 或 Input System 包。

Unity 只序列化 C# 薄层中的 `speed` 字段；`MotionState` 是 `Awake` 重建的运行期状态。需要持久化时，应显式映射到 Unity 支持的字段或带版本的保存 DTO，而不是假设 Unity 能理解 F# 的 record、union、option、list 或属性。

本仓库没有安装 Unity，因此 Unity 6000.3.22f1 的插件导入、C# 编译、Play Mode，以及 macOS ARM64 IL2CPP Player 构建和启动均未执行。上面的英文证据表是实际边界记录，不是待办占位；只有拿到对应 Editor、平台模块、构建日志和 Player 启动结果后才能改成通过。
