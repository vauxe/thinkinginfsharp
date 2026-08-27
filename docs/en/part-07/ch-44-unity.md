---
title: "Chapter 44: Unity 6.3 LTS and F#"
description: "Use F# inside Unity through explicit runtime, assembly, serialization, lifecycle, performance, AOT, stripping, and Player-build boundaries."
translationKey: part-07/ch-44-unity
---

# Chapter 44: Unity 6.3 LTS and F# {#overview}

Unity does not need to compile F# source in order to execute code written in F#. F# compiles to managed .NET assemblies, and Unity can import managed plug-ins. That technical fact is useful, but it is only the first link in a longer chain.

A library that builds under `dotnet` may still fail Unity's reference validation. A plug-in that imports may still fail when a scene loads. Play Mode may work while an IL2CPP Player fails during ahead-of-time compilation, stripping, native linking, startup, or one device-only path. The useful question is not “Does Unity support F#?” but “Which F# boundary has this exact Unity version, platform, scripting backend, and release pipeline validated?”

The deliberately small design keeps game rules in a normal F# library, publishes a C#-friendly API, and puts Unity-specific behavior in a thin C# component. It also shows when to test a direct F# component, when F# adds little value, and why a successful class-library build says nothing about a Player build.

::: tip Two reading passes
For a first pass, follow the [integration stack](#unity-contract-stack), [decision map](#decision-map), and [managed plug-in slice](#x44-verified-slice). When preparing a representative Player build, return to the sections on serialization, the game loop, IL2CPP, verification, and release.
:::

## Unity integration is a stack of contracts {#unity-contract-stack}

Treat an F#-inside-Unity release as at least seven connected contracts:

```text
F# source + locked NuGet graph
  -> managed plug-in DLLs for a compatible API profile
  -> Unity asset import and assembly references
  -> C# scripts, scenes, serialized fields, and lifecycle callbacks
  -> Editor or Mono managed execution
  -> UnityLinker + IL2CPP + native toolchain
  -> architecture-specific Player package, launch, and device behavior
```

Each arrow can fail independently. The first two are ordinary .NET work. The middle layers belong to the Unity project and Editor. The last two depend on build modules, platform SDKs, native compilers, architectures, signing, and target devices.

Use a precise verb for every result:

| Result you want to report | Minimum check |
| --- | --- |
| The F# code compiles | Locked restore and Release build of the stated target framework |
| The plug-in is complete | Main DLL and every runtime dependency are present and references inspect correctly |
| Unity accepts the plug-in | Exact Editor patch imports it with reference validation and zero compiler/import errors |
| The component works in Editor | Representative scene enters Play Mode and observes behavior, reload, and errors |
| A Mono Player works | Named target Player builds, launches, and runs representative tests outside the Editor |
| An IL2CPP Player works | Named architecture, stripping level, native toolchain, build, launch, and runtime paths pass |
| The release is supported | Device/platform matrix, performance, diagnostics, packaging, signing, upgrade, and recovery pass |

“Supported by .NET Standard” and “works in our shipped IL2CPP Player” are different kinds of sentence. Keep both when they are true; never substitute one for the other.

## Decide whether F# belongs in this Unity project {#decision-map}

F# is most valuable where the game contains rules worth naming and testing independently of frames, GameObjects, scenes, and asset state. It is less valuable when most code is a thin sequence of engine calls or must live inside a Unity-specific compiler pipeline.

| Candidate boundary | Strong fit | Main friction | First validation |
| --- | --- | --- | --- |
| Pure F# domain plug-in plus C# adapter | Economy, combat resolution, quests, dialogue state, inventory, procedural rules, save migration, server-shared validation | DLL/dependency packaging and language boundary | One rule through `dotnet` tests, Unity import, Play Mode, and representative IL2CPP Player |
| F# service/tool library used by Editor code | Import validation, content checks, deterministic generators, build metadata | `UnityEditor` coupling, asset database lifecycle, batch mode, diagnostics | One real asset pipeline in interactive and batch Editor modes |
| Direct F# `MonoBehaviour` in a managed plug-in | Team accepts external F# builds and needs a very small component | UnityEngine reference versioning, Inspector and serialization compatibility, component discovery, debugging | Compile against exact Editor assemblies; import, attach, serialize, reload, build Player |
| C# Unity application with no F# | Logic is mostly engine orchestration, visual scripting, shaders, packages, Jobs/Burst, or designer-led workflows | Few domain rules that benefit from F# | Compare the simplest C# vertical slice against the F# boundary, not against language preference |
| Separate F# backend or tooling process | Authoritative simulation, matchmaking, analytics, content build, or offline tools do not need to run in the Player | Network/process contracts and deployment | Keep Unity client thin; verify wire/version behavior independently |

### The low-friction default {#recommended-boundary}

For a new experiment, start with a Unity-independent F# library and a thin C# host. This preserves the most useful F# properties—explicit types, pure transitions, property tests, and ordinary .NET tooling—while keeping Unity workflows in their established C# form.

The C# layer should handle:

- `MonoBehaviour`, `ScriptableObject`, custom inspectors, and Unity attributes;
- serialized scene and prefab fields;
- `GameObject`, `Transform`, `Rigidbody`, assets, handles, and other `UnityEngine.Object` references;
- `Awake`, `OnEnable`, `Update`, `FixedUpdate`, `OnDisable`, and scene callbacks;
- input packages, platform APIs, coroutines, Unity logging, and Unity-specific async adapters;
- mapping between Unity values and domain values.

The F# layer should contain decisions that remain meaningful without the engine. These include validated identifiers, rules, deterministic state transitions, save schemas and migrations, input-supplied random seeds, and ports for host-run effects.

### Direct F# components are possible, not free {#direct-fsharp-components}

Unity's managed plug-in model is based on .NET assemblies, not source-language identity. A precompiled type derived from `MonoBehaviour` can in principle be attached like another managed plug-in type. An F# project can also reference Unity assemblies from a particular Editor installation.

That does not make the direct path the default. The build now depends on exact Unity assembly locations and versions. Generated F# representation may not match Inspector expectations. Unity examples, source generators, analyzers, package setup, debugger workflows, and Editor callbacks center on C#. Every result still needs import, attach, serialization, reload, and Player validation.

Use direct F# only when the vertical slice is simpler after measuring those costs. A ten-line C# component around a stable F# core is not a defeat; it is an adapter at the tool-owned boundary.

### Know when to stop adding language boundaries {#when-not-to-use}

Do not introduce an F# DLL merely to wrap calls such as `transform.Translate`, play an animation, or forward one collision callback. The extra compiler, package, import, symbols, and interop surface must buy testable domain value.

Likewise, do not push a frame-critical Burst kernel through F# because the rest of the game uses F#. Burst documents an HPC# subset and a Unity IL post-processing pipeline. Keep such kernels in the supported C# data-oriented form unless an exact F# experiment validates the package, attributes, IL, Editor, AOT, performance, and Player behavior.

## The managed plug-in sample: one verified managed plug-in boundary {#x44-verified-slice}

The managed plug-in sample implements one horizontal-motion rule. It is intentionally too small to justify a production architecture. Its purpose is to expose the build, API, dependency, host, allocation, linker, and verification boundaries.

### The project contract and dependency output {#project-contract}

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
The project targets `netstandard2.1`, names the assembly `FSharpGameplay`, and compiles only `Gameplay.fs`. `FSharp.Core` is already an implicit F# SDK package; `Update` fixes that one reference at 10.1.301 for this project instead of adding a duplicate.

`CopyLocalLockFileAssemblies` matters because Unity is not restoring this `.fsproj` when it imports the DLL. The post-build target turns a deployment assumption into a failure: both `FSharpGameplay.dll` and `FSharp.Core.dll` must exist in the output directory.

Package version and assembly version are not the same identifier. The locked NuGet package is 10.1.301; the built plug-in records an assembly reference to `FSharp.Core, Version=10.1.0.0`. Import the dependency produced by the locked build rather than guessing from either number.

### Pure logic behind a CLR-shaped surface {#pure-gameplay}

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
`Gameplay.Create` and `Gameplay.Step` are tupled static methods, so C# sees ordinary method calls instead of curried `FSharpFunc` values. `MotionState` exposes read-only float properties and hides its fields and non-default constructor.

The state is a struct. An earlier implementation used a class and therefore allocated a new managed object on every `FixedUpdate`. A regression test now checks `IsValueType` and decodes the managed body of `Gameplay.Step` to reject an explicit `box` instruction. This removes that specific state-object allocation in the managed build without pretending the whole Player allocates zero bytes. A large struct would introduce copying costs, so keep state small and profile the real target.

The transition clamps directional input, rejects non-finite values and negative time or speed, calculates velocity, and returns a new state. It has no `UnityEngine` reference, no current time lookup, and no mutation. Tests can supply every input directly.

### A thin Unity adapter {#csharp-adapter}

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
The file and public `MonoBehaviour` class share the name `UnityAdapter`, preserving Unity's ordinary script/component workflow. The Inspector exposes one primitive `speed` field. `OnValidate` protects authoring-time configuration, while the F# boundary still validates runtime calls.

`Awake` creates runtime state from the current transform. `FixedUpdate` supplies the input value, configured speed, and Unity's fixed delta time, then maps the returned position back to a `Vector3`. This is a transform sample, not a physics recommendation; an object controlled by a Rigidbody needs the corresponding physics API and tests.

`SetHorizontal(float)` deliberately avoids choosing the legacy Input Manager or the Input System package. A separate input adapter can call it. This keeps package choice and callback form out of the rule assembly.

The C# file is illustrative because the book site does not include UnityEngine assemblies. Copy it into a real Unity project and compile it there; mock engine types would validate only the mock host.

### Narrow linker roots {#linker-roots}

```xml:line-numbers [link.xml]
<linker>
  <assembly fullname="FSharpGameplay">
    <type fullname="ThinkingInFSharp.UnitySample.Gameplay" preserve="all" />
    <type fullname="ThinkingInFSharp.UnitySample.MotionState" preserve="all" />
  </assembly>
</linker>
```
Direct calls from the C# adapter should be visible to static reachability analysis. The managed plug-in sample still includes two explicit roots to make the intended cross-assembly bridge visible and to provide a concrete stripping artifact for the chapter.

The file does not preserve all of `FSharp.Core`. Broad preservation can hide missing reflection design, enlarge the Player, and increase IL2CPP work. Add a type or member only when an actual dynamic path needs it, then test the relevant stripping level.

Copy `link.xml` under the Unity project's `Assets` tree. A source file next to the external `.fsproj` has no effect until it becomes a Unity asset.

### Record exactly what has been verified {#evidence-ledger}

The chapter provides the design; an adopting Unity project must complete this verification record:

| Layer | Required check | What it verifies |
| --- | --- | --- |
| Locked .NET restore | Run in the copied project | The `netstandard2.1` graph resolves to the chosen FSharp.Core package |
| Release plug-in build | Run in the copied project | F# source compiles with the selected SDK |
| Output inspection | Check both DLLs and assembly identity | The plug-in and its exact FSharp.Core dependency are ready for import |
| Focused rule/API test | Run outside Unity | Clamp/step behavior, struct state, and the CLR-facing API |
| Unity import and C# compilation | Run in the selected Editor | UnityEngine host integration compiles |
| Play Mode | Run with the real scene | Lifecycle, input, and scene behavior work |
| Target IL2CPP Player | Build and launch | Native conversion, stripping, link, startup, and runtime behavior work |

An unrun row is useful because it keeps the claim bounded. A visible gap can be scheduled and priced; a false green row cannot.

## Target the compatibility profile, not a runtime name {#compatibility-target}

Unity 6 exposes .NET Standard 2.1 and a broader .NET Framework profile in Player settings. The .NET Standard profile is the cross-platform baseline and is the right first target for a reusable managed plug-in.

### An API profile is only a compile-time ceiling {#profile-not-runtime}

`.NET Standard 2.1` describes a set of APIs. It does not identify Unity's CoreCLR version or garbage collector, nor does it promise JIT on every platform. A library can compile against the profile and still rely on an unsupported implementation detail.

Avoid `net10.0`, `netcoreapp`, operating-system-specific target frameworks, dynamic code generation, and accidental platform APIs in the Player plug-in. If one library needs a broader host, keep it outside the Player or add a target-specific adapter and test it separately.

Compile against the smallest honest profile, then test on every scripting backend and platform that ships. Compatibility is an intersection:

```text
APIs used by the plug-in
  ∩ Unity API compatibility profile
  ∩ scripting backend implementation
  ∩ target-platform capabilities
  ∩ linker/AOT discoverability
```

### Carry the complete dependency closure {#dependency-closure}

An F# assembly usually references `FSharp.Core` even when its public API contains no F#-specific types. Compiler-generated calls, attributes, and implementation details still need that assembly.

The same rule applies to every NuGet dependency: Unity does not interpret the class library's lock file as an asset import plan. Import all required managed DLLs, native libraries, data files, and licenses in compatible versions. Do not copy framework reference assemblies or assume a `.deps.json` file will make Unity resolve packages like `dotnet` does.

Prefer a small dependency graph. For each package ask:

- Does its compile target fit .NET Standard 2.1?
- Does it use reflection, expression compilation, `Reflection.Emit`, dynamic proxies, unsupported encodings, native loading, or platform-specific files?
- Are its generic instantiations visible to IL2CPP?
- Does it ship native binaries for every architecture?
- Can UnityLinker see its dynamic entry points?
- What license and notices must enter the Player?
- Has the exact version run in the exact Player matrix?

One transitive package can be a larger Unity risk than the F# source itself.

### Control Unity references explicitly {#plugin-import}

Copy managed plug-ins under `Assets`, select their platform compatibility, and keep Validate References enabled. That catches missing references and strong-name mismatches earlier than runtime.

Auto Reference is convenient for a spike, but it makes every eligible script assembly see the plug-in and increases recompilation and accidental coupling. In a larger project, disable it and use assembly definitions with explicit precompiled references. Keep Editor-only adapters in Editor-only assemblies and exclude Player-incompatible plug-ins from Player platforms.

Never fix a reference problem by disabling assembly version validation before understanding the mismatch. If plug-ins require incompatible `FSharp.Core` versions, rebuild them against one tested version, isolate them in separate processes, or reject the combination. One load context cannot load two files with the same assembly identity as different assemblies.

## Design a boundary that is natural from C# {#design-csharp-boundary}

The F# implementation can remain idiomatic internally. The exported surface should follow the consumer's conventions.

### Prefer ordinary CLR types and calls {#clr-shaped-api}

Good Unity-facing choices include:

- namespaces, sealed classes or small structs, PascalCase methods, and read-only properties;
- tupled method arguments so C# calls `Step(state, input, speed, dt)`;
- primitive values, enums, arrays, `IReadOnlyList<T>`, and purpose-named DTOs where appropriate;
- `System.Action` or `System.Func` only when a callback is truly the right contract;
- explicit factory methods when construction must enforce invariants.

Keep F# lists, maps, options, results, discriminated unions, curried functions, and units of measure behind the boundary unless the C# consumer deliberately accepts their compiled forms. They are valid .NET types. The tradeoffs are caller complexity, representation coupling, AOT coverage, and maintenance.

Units of measure are erased in emitted .NET signatures. A C# float does not express whether it represents seconds, meters, or meters per second. Preserve the meaning with method names, DTO fields, validation, or distinct wrapper types.

### Translate outcomes once {#errors-and-outcomes}

Do not throw on every expected gameplay branch. Model domain outcomes internally with unions or results, then translate them once into a C#-friendly result class, enum plus payload, `Try...` method, or explicit callback message.

Reserve exceptions for broken contracts and failures the current call cannot represent. The managed plug-in sample rejects NaN, infinity, negative speed, and negative delta time because they indicate an invalid boundary call. The C# adapter prevents ordinary authoring errors before reaching it.

For asynchronous work, do not leak an F# `Async<'T>` into Unity. Publish `Task`, `ValueTask`, a C#-friendly polling handle, or a message interface according to the host. Define who can cancel and which thread receives completion. Unity object access still belongs to the main thread even if pure computation or I/O runs elsewhere.

### Separate durable data from engine objects {#units-and-data}

Do not put a `GameObject`, `Transform`, `Texture`, scene handle, open stream, cancellation source, or service singleton in a domain state that must be saved, replayed, tested, or sent to a server.

Use stable identifiers and values in F#. Let the adapter resolve them to current Unity objects. The mapping can fail because a scene unloaded, an asset changed, or an object was destroyed; represent that as a boundary outcome rather than pretending the reference is durable.

## Unity serialization is its own contract {#serialization}

Unity's serializer is not ordinary .NET serialization and does not persist arbitrary properties or object graphs.

### Start from supported fields {#supported-fields}

A serializable field is public or marked `[SerializeField]`, is not static, const, or readonly, and has a supported field type. Supported categories include primitives, enums of supported size, Unity built-in values, `UnityEngine.Object` references, serializable custom classes/structs, arrays, and `List<T>` of supported elements.

Properties are not the normal persistence surface. Dictionaries, multidimensional or jagged arrays, and nested containers need an explicit representation or serialization callback. `[SerializeReference]` changes reference and polymorphism behavior but adds its own identity, migration, and type-name risks.

F# records usually expose properties and compiler-generated representation. Unions, options, lists, maps, and closures are not Unity field formats merely because they are managed objects. A representation may work in one Editor path and still fail prefab persistence, domain reload, stripping, or Player builds.

### Map; do not teach the serializer F# {#map-dont-teach}

Keep Inspector-authored configuration in a C# `MonoBehaviour`, `ScriptableObject`, or deliberately serializable C# DTO. Validate it, then construct the richer F# model.

For saved games, define a versioned storage DTO independent of scene serialization. Map DTO to validated domain state with explicit migration and error reporting. Test older versions, missing fields, corrupt data, interrupted writes, cloud conflicts, downgrade policy, and deletion.

This creates three different models on purpose:

```text
Unity authoring fields -> validated F# runtime model -> versioned save/wire DTO
```

Trying to make one generated type satisfy Inspector editing, impossible-state modeling, network compatibility, and long-term save migration usually weakens all four.

### Rebuild runtime state across lifecycle changes {#reload-and-lifecycle}

Unity can reload scripts and assemblies, recreate components from serialized fields, enter Play Mode with configurable domain/scene reload behavior, unload scenes, disable objects, and destroy native objects while managed references still exist.

Treat `Awake` or another explicit composition point as construction from serialized configuration. Use `OnEnable` and `OnDisable` to pair subscriptions and cancellation. Do not assume a private managed cache survives reload, or that a non-null-looking `UnityEngine.Object` is still backed by a live native object.

The managed plug-in sample reconstructs `MotionState` in `Awake` and resets input in `OnDisable`. It does not validate save-game persistence or domain reload; those require a larger Unity project test.

## Respect the game loop and allocation budget {#game-loop}

Functional design helps isolate decisions; it does not exempt code from frame time, memory, cache, or thread constraints.

### Choose the callback from the engine contract {#update-and-fixed-update}

Use `Update` for frame-driven presentation and sampled input, `FixedUpdate` for fixed-step simulation and physics coordination, and late/render callbacks only for their documented purposes. A fixed callback can run zero, one, or multiple times around a rendered frame.

Do not read a transient button edge only inside a callback that may not run for that rendered frame. Capture input in the input layer, then feed stable values or queued commands to the simulation step.

Pass time explicitly into pure logic. That makes pause, replay, slow motion, deterministic tests, and server agreement visible. Determinism also requires controlled randomness, iteration order, floating-point expectations, and no hidden wall clock.

### Measure allocations; do not argue from style {#allocation-budget}

F# pipelines, closures, sequences, records, unions, arrays, and interface calls allocate differently according to their representation and use. The label “functional” alone tells you nothing about allocation.

Profile a development Player on the target device. Use the CPU Profiler's `GC.Alloc` column and call stacks, then confirm with representative workload and build configuration. Editor measurements include Editor-only behavior and can differ from a Player.

Common hot-loop risks include:

- allocating a class, list, sequence, closure, delegate, option, or formatted string every frame;
- boxing value types through object or interface paths;
- repeatedly enumerating lazy sequences or calling Unity APIs that return fresh arrays;
- copying a struct so large that avoiding the heap worsens the CPU/cache budget;
- retaining short-lived data until a large collection occurs.

Optimize the measured hotspot, not the whole domain. A turn-resolution pipeline that runs once per user action can prioritize clarity; a transform step called tens of thousands of times per frame may need compact structs, arrays, pools, or a Burst kernel.

### Keep Unity objects on their owning thread {#threading}

Most Unity APIs and objects are main-thread owned. Pure F# computation can use tasks or worker threads when its inputs are detached values and its outputs do not touch Unity objects.

Copy or map the required values on the main thread, perform bounded work with cancellation, then enqueue a result for the main thread. Include an operation or scene identity so a result from an unloaded scene, disabled component, or superseded request is rejected.

Immutability describes the copied F# value only. A `Transform`, asset, or destroyed Unity object still follows the engine's main-thread and lifetime contract, so worker code should consume detached values and return detached results.

## IL2CPP expands the required verification {#il2cpp-and-aot}

IL2CPP is not “Mono with a different optimizer.” It changes when and how executable code is produced.

### Follow the actual pipeline {#il2cpp-pipeline}

For an IL2CPP Player, Unity:

1. compiles project C# and required package code into managed assemblies;
2. applies managed code stripping;
3. converts managed IL—including imported F# assemblies—into C++;
4. invokes the target platform's native compiler and linker;
5. packages native output and required data into the Player.

The appropriate IL2CPP module and native toolchain must be installed. Cross-compilation is generally not supported; build on the required host except for documented exceptions such as supported Linux cross-compilation paths.

A green Editor session proves none of steps 2–5. A successful IL2CPP build still needs launch and behavioral tests because stripping or AOT gaps can surface only when a path executes.

### Make AOT reachability concrete {#aot-risk}

Ahead-of-time compilation cannot wait until runtime to generate arbitrary new code. Risk rises with:

- reflection that discovers types or members from names;
- serializers, dependency injection, or mapping libraries that generate accessors dynamically;
- `Reflection.Emit`, expression compilation, and runtime proxy generation;
- generic virtual methods and generic combinations never made concrete in reachable code;
- callbacks found only by native code, strings, attributes, or external data;
- platform invokes and native libraries with wrong signatures or architectures.

The remedy is not “avoid generics” or “preserve everything.” Prefer static calls and instantiate the required closed generic paths. Use AOT-supported library modes, add narrow roots or callback attributes where required, and run the exact Player path.

Record negative cases too. A loader that handles only the happy save type may pass while an older polymorphic save, error subtype, localized resource, or rare callback has been stripped.

### Treat linker rules as tested code {#reflection-and-stripping}

UnityLinker analyzes reachable code and removes code according to the selected Managed Stripping Level. Current Unity 6 documentation marks Minimal as the IL2CPP default, marks Low for future deprecation, and describes Medium and High as more aggressive choices; record the explicit setting because defaults can change.

Use `[Preserve]` when the preserved element can carry a Unity attribute without polluting the reusable layer. Use `link.xml` when preservation belongs to integration configuration or targets an external assembly. Keep type and member names exact, place the file under `Assets`, and test that the expected Player still works when the rule is narrowed.

Preservation prevents removal; it does not make an unsupported API, runtime code generator, native binary, or generic pattern AOT-compatible. It also does not test behavior.

### Burst and Jobs are a separate architecture {#burst-and-jobs}

Burst documents HPC#, a restricted high-performance C#/.NET subset built around unmanaged values, Unity collections, jobs or function pointers, attributes, and IL post-processing. Managed objects, many runtime services, and ordinary exception behavior are outside that kernel model.

The managed plug-in sample is a managed F# plug-in and does not validate Burst or the Job System. Adding `[BurstCompile]` to an F#-produced method does not establish support.

When profiling justifies Burst, a practical boundary is:

```text
F# rules and orchestration
  -> flat arrays/struct commands
  -> small C# Job/Burst kernel
  -> flat result values
  -> F# decision layer
```

Measure conversion cost, scheduling overhead, determinism, safety checks, Editor compilation, Player AOT, and target performance. If the hot kernel dominates the architecture, C# may appropriately handle more of that subsystem.

## Verify each layer in increasing cost order {#testing-ladder}

Use the cheapest test that can disprove the claim, then climb only as far as the release requires:

1. **Pure .NET tests:** rules, invariants, properties, save migrations, deterministic replay, and C#-oriented signatures.
2. **Artifact inspection:** target framework, assembly identities, dependency closure, symbols, native assets, and license files.
3. **Unity import check:** exact Editor patch, clean Library/cache, Validate References, zero Console errors, explicit platform compatibility.
4. **Edit Mode tests:** adapter mapping and asset/editor code that does not require a running scene.
5. **Play Mode tests:** component lifecycle, scene/prefab serialization, reload configuration, input, timing, and engine interaction.
6. **Mono Player test:** build and launch outside the Editor on a named platform when Mono is a shipping or diagnostic backend.
7. **IL2CPP Player test:** explicit architecture and stripping level, build logs, launch, rare reflection/generic paths, and crash symbols.
8. **Device/release test:** supported hardware, performance, memory, suspend/resume, platform services, packaging, signing, upgrade, telemetry, and recovery.

Unity Test Framework can run Edit Mode, Play Mode, and tests in a built Player. Keep ordinary F# tests outside Unity for fast feedback, then add Unity-owned C# tests for the adapter and host. A Player test is not redundant with Play Mode; it exercises a different runtime and package.

For failures, preserve the exact Editor version, build profile, target, backend, stripping level, command, exit code, Editor log, Player log, test XML, crash dump, symbols, and artifact hash. “CI failed” is not a diagnosis.

## Make the Player build reproducible {#build-and-release}

Unity is part of the compiler and asset pipeline. Version it like one.

### Pin the Editor, modules, packages, and plug-in {#pin-editor}

Record the full Editor patch, not only “Unity 6.3.” This sample selects 6000.3.22f1, the current 6.3 LTS patch when checked on 2026-08-25. It is a review target, not a claim about an installed tool.

Lock Unity packages and the F# NuGet graph. Build the F# DLL once from a clean locked restore, copy the exact dependency set into the Unity project, and hash or otherwise identify the imported artifacts. Avoid rebuilding the plug-in differently inside each platform job unless platform-specific output is intentional.

Use a clean import in CI often enough to catch hidden local Library state. Decide which generated Unity metadata and settings belong in source control, and do not let a developer's last active target silently choose a release.

### Drive one explicit build profile per invocation {#build-profiles-and-ci}

Unity's command-line build supports an explicit build target or saved build profile. Always specify one, use batch mode, write a log file, and run one target per Editor invocation. Target switching can require assembly reload and is not reliable midway through a batch script.

A release job should record:

- exact Editor path/version and installed module;
- project commit, lock files, imported plug-in hashes, and package manifest/lock;
- active build profile or target, scenes, architecture, backend, stripping, development/debug flags;
- platform SDK and native compiler versions;
- output path, exit code, logs, warnings policy, artifact hash, signing identity, and provenance;
- post-build launch/test result on representative hardware.

Compilation success without a Player launch is a build result, not a runtime result.

### Keep symbols and diagnostics useful {#symbols-and-diagnostics}

Retain F# portable PDBs and Unity/native symbols according to the release's privacy and storage policy. IL2CPP stack trace quality depends on build configuration and stack-trace settings; optimized native compilation can inline managed frames.

Validate that a deliberate F# exception in a development Player produces a useful method, file, and line path before an incident. Upload symbols to the crash system, preserve mapping/build identifiers, and verify symbolication from an actual captured crash.

Log domain outcomes with stable event names and identifiers, not entire save files or personal data. Separate a rejected gameplay command from a plug-in load error, stripped method, native crash, or asset problem so telemetry leads to the owning layer.

## Run a bounded adoption spike {#adoption-spike}

Before committing a production Unity codebase to F#, time-box one vertical slice containing the hardest representative risks:

- exact Unity patch, platform module, F# SDK, NuGet lock, and repeatable plug-in copy step;
- one domain rule with property or replay tests and one C#-friendly public contract;
- one Inspector-authored configuration mapped into validated F# state;
- scene/prefab save, script reload, domain-reload settings, enable/disable, and scene unload;
- one asynchronous operation with cancellation, stale-result rejection, and main-thread return;
- one save migration and one corrupt/older payload;
- a representative per-frame path measured for CPU, `GC.Alloc`, memory, and copying;
- one dynamic reflection/generic path at the intended stripping level;
- Play Mode, Mono if relevant, and the shipping IL2CPP architecture;
- clean CI import, command-line Player build, launch, logs, symbols, package, and signing path;
- onboarding, IDE/debugger friction, dependency updates, and a documented exit path to a C# host.

Adopt only the boundary that passes. F# may handle the entire deterministic simulation, only offline rules, or only server and tool code. C# may also be simpler for the whole subsystem; each is a valid engineering outcome.

## Avoid common Unity mistakes {#common-mistakes}

- Calling a successful `dotnet build` Unity support.
- Targeting `net10.0` because the developer machine has .NET 10, while the Player plug-in contract is .NET Standard 2.1.
- Copying `FSharpGameplay.dll` but omitting `FSharp.Core.dll` or another transitive dependency.
- Treating a NuGet lock file or `.deps.json` as something Unity automatically restores.
- Disabling reference or assembly-version validation to silence an unexplained mismatch.
- Exposing F# functions, lists, options, or unions to C# accidentally and then writing adapters at every call site.
- Asking Unity to serialize generated F# representations, properties, or arbitrary graphs without prefab, reload, and Player tests.
- Storing scene objects, assets, or open resources inside durable domain state.
- Reading Unity objects from a worker thread because the surrounding F# value is immutable.
- Sampling frame-edge input only in `FixedUpdate` and losing events.
- Allocating sequences, closures, strings, or state classes per entity per frame without measuring `GC.Alloc`.
- Replacing every clear immutable value with mutation before profiling.
- Preserving all of FSharp.Core to make one reflection failure disappear.
- Assuming `link.xml` makes runtime code generation or unsupported APIs work under AOT.
- Treating Play Mode as an IL2CPP test.
- Testing only one generic type, serialization subtype, locale, error callback, or old save format.
- Assuming a Burst attribute turns arbitrary F# IL into a supported HPC# kernel.
- Building with “the installed Unity 6.3” instead of an exact patch and module set.
- Letting CI reuse the last active platform or build multiple targets through one unreliable target switch.
- Discarding PDBs, IL2CPP symbols, Editor logs, or Player logs before a failure is diagnosable.
- Forcing every Unity-facing line into F# when a narrow C# adapter is the simpler long-term contract.

## Exercises {#exercises}

### Exercise 1: choose three language boundaries {#exercise-01}

Evaluate these products separately:

1. A turn-based tactics game has complex deterministic combat, replay, mod validation, and a modest presentation layer.
2. A console action game concentrates risk in thousands of physics-like entities, Jobs/Burst performance, platform SDKs, and designer-authored behaviors.
3. A Unity Editor content pipeline validates dialogue graphs, generates localization reports, and runs headlessly in CI.

For each product, record the first F# boundary, rejected alternatives, verification matrix, and conditions for changing direction. The three products may lead to different language splits.

### Exercise 2: turn the managed plug-in sample into a Unity vertical slice {#exercise-02}

Design the smallest Unity project and verification record that could move the sample from “managed DLL builds” to “representative macOS ARM64 IL2CPP Player works.” Organize the record into four groups:

- **Assembly import:** artifact copying, `FSharp.Core` identity, assembly definitions, and Validate References.
- **Editor behavior:** scene and input setup, Edit/Play Mode tests, reload behavior, and allocation profiling.
- **Player build:** stripping level, `link.xml`, the command-line build profile, launch, logs, and symbols.
- **Verification status:** the exact command, result, artifact, and failure meaning for every row.

Keep each unrun row marked as unrun until the corresponding step actually executes.

### Exercise 3: add saves, asynchronous operations, and dynamic content {#exercise-03}

Extend the architecture for a quest system whose rules are F#, configuration is authored in Unity, saves migrate across three versions, remote dialogue arrives asynchronously, and content names optional quest handlers.

Cover four boundaries:

- **Authored and saved data:** authoring DTOs, validated domain types, save DTOs, and migrations.
- **Public behavior:** the C# API plus cancellation and stale-result messages.
- **AOT discovery:** handler registration that avoids unrestricted runtime code generation, plus narrow preservation rules.
- **Runtime verification:** malformed and old-content tests plus Mono and IL2CPP results.

Place any feature whose safe AOT discovery remains unproven outside the Player.

[Read the chapter solutions](../solutions/ch-44-unity).

## Sources {#sources}

- [Unity 6000.3.22f1 release notes](https://unity.com/releases/editor/whats-new/6000.3.22f1)
- [Unity 6.3 Manual: managed plug-ins](https://docs.unity3d.com/6000.3/Documentation/Manual/plug-ins-managed.html)
- [Unity 6.3 Manual: IL2CPP scripting back end](https://docs.unity3d.com/6000.3/Documentation/Manual/scripting-backends-il2cpp.html)
- [Unity 6.3 Manual: build a Player from the command line](https://docs.unity3d.com/6000.3/Documentation/Manual/build-command-line.html)
- [Unity 6.3 Manual: Assembly Definition properties](https://docs.unity3d.com/6000.3/Documentation/Manual/class-AssemblyDefinitionImporter.html)

Chapter 45 returns to ordinary .NET tooling: scripts, automation, package evaluation, lock discipline, and a practical map for continuing to learn F#.
