---
title: "Chapter 44: Unity 6.3 LTS and F#"
description: "Use F# inside Unity through explicit runtime, assembly, serialization, lifecycle, performance, AOT, stripping, and Player-build boundaries."
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
    url: https://docs.unity3d.com/6000.0/Documentation/Manual/testing-editortestsrunner.html
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

# Chapter 44: Unity 6.3 LTS and F# {#overview}

Unity does not need to compile F# source in order to execute code written in F#. F# compiles to managed .NET assemblies, and Unity can import managed plug-ins. That technical fact is useful, but it is only the first link in a longer chain.

A library that builds under `dotnet` may still fail Unity's reference validation. A plug-in that imports may still fail when a scene loads. Play Mode may work while an IL2CPP Player fails during ahead-of-time compilation, stripping, native linking, startup, or one device-only path. The right question is therefore not “Does Unity support F#?” but “Which F# boundary can this exact Unity version, platform, scripting backend, and release pipeline prove?”

This chapter uses X44 as a deliberately small answer: keep game rules in a normal F# library, publish a C#-friendly surface, and let a thin C# component own Unity-specific behavior. It also explains when a direct F# component deserves a spike, when F# adds little value, and how to avoid turning a successful class-library build into an imaginary Player result.

## What you will be able to do {#outcomes}

By the end of this chapter, you should be able to:

- separate F# compilation, managed plug-in import, Unity script compilation, Editor runtime, Mono Player, and IL2CPP Player evidence;
- choose where F# belongs from domain complexity, frame budget, Unity tooling, team skills, and platform risk;
- target Unity's .NET Standard 2.1 compatibility surface without confusing an API profile with a runtime identity;
- package `FSharp.Core` and every other runtime dependency beside a managed plug-in;
- design a public F# API that feels ordinary from C# and does not leak avoidable F# representation types;
- keep Unity serialization fields and `UnityEngine.Object` references out of the reusable domain model;
- map Unity lifecycle callbacks and input into explicit values before calling pure F# logic;
- distinguish `Update`, `FixedUpdate`, background work, and main-thread Unity API ownership;
- detect frame-loop allocations instead of assuming functional code is automatically cheap or expensive;
- explain the IL2CPP sequence from managed assemblies through stripping, generated C++, native compilation, and packaging;
- identify reflection, dynamic generic, code generation, and platform API paths that need AOT evidence;
- use narrow preservation rules instead of preserving all of `FSharp.Core` by default;
- treat Burst and the Job System as a separate HPC# contract, not an automatic property of an F# DLL;
- climb an evidence ladder from ordinary unit tests to an imported, launched, stripped Player on target hardware;
- pin an exact Unity patch and build profile in automation;
- state exactly what X44 proves, what it deliberately leaves to Unity, and why those limits matter.

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

| Claim | Minimum evidence |
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

| Candidate boundary | Strong fit | Main friction | First proof |
| --- | --- | --- | --- |
| Pure F# domain plug-in plus C# adapter | Economy, combat resolution, quests, dialogue state, inventory, procedural rules, save migration, server-shared validation | DLL/dependency packaging and language boundary | One rule through `dotnet` tests, Unity import, Play Mode, and representative IL2CPP Player |
| F# service/tool library used by Editor code | Import validation, content checks, deterministic generators, build metadata | `UnityEditor` ownership, asset database lifecycle, batch mode, diagnostics | One real asset pipeline in interactive and batch Editor modes |
| Direct F# `MonoBehaviour` in a managed plug-in | Team accepts external F# builds and needs a very small component | UnityEngine reference versioning, Inspector/serialization shape, component discovery, debugging | Compile against exact Editor assemblies; import, attach, serialize, reload, build Player |
| C# Unity application with no F# | Logic is mostly engine orchestration, visual scripting, shaders, packages, Jobs/Burst, or designers own the workflow | Less F# domain leverage | Compare the simplest C# vertical slice against the F# boundary, not against language preference |
| Separate F# backend or tooling process | Authoritative simulation, matchmaking, analytics, content build, or offline tools do not need to run in the Player | Network/process contracts and deployment | Keep Unity client thin; verify wire/version behavior independently |

### The low-friction default {#recommended-boundary}

For a new experiment, start with a Unity-independent F# library and a thin C# host. This preserves the most useful F# properties—explicit types, pure transitions, property tests, and ordinary .NET tooling—while leaving Unity's strongest workflows in their native shape.

The C# layer should own:

- `MonoBehaviour`, `ScriptableObject`, custom inspectors, and Unity attributes;
- serialized scene and prefab fields;
- `GameObject`, `Transform`, `Rigidbody`, assets, handles, and other `UnityEngine.Object` references;
- `Awake`, `OnEnable`, `Update`, `FixedUpdate`, `OnDisable`, and scene callbacks;
- input packages, platform APIs, coroutines, Unity logging, and Unity-specific async adapters;
- mapping between Unity values and domain values.

The F# layer should own decisions that remain meaningful without the engine: validated identifiers, rules, deterministic state transitions, save schemas and migrations, random seeds supplied as inputs, and ports for effects that the host performs.

### Direct F# components are possible, not free {#direct-fsharp-components}

Unity's managed plug-in model is based on .NET assemblies, not source-language identity. A precompiled type derived from `MonoBehaviour` can in principle be attached like another managed plug-in type. An F# project can also reference Unity assemblies from a particular Editor installation.

That does not make the direct path the default. The build now depends on exact Unity assembly locations and versions. Generated F# representation may not match Inspector expectations. Unity examples, source generators, analyzers, package setup, debugger workflows, and Editor callbacks are C#-shaped. Every claim still needs import, attach, serialization, reload, and Player evidence.

Use direct F# only when the vertical slice is simpler after measuring those costs. A ten-line C# component around a stable F# core is not a defeat; it is an adapter at the tool-owned boundary.

### Know when to stop adding language boundaries {#when-not-to-use}

Do not introduce an F# DLL merely to wrap calls such as `transform.Translate`, play an animation, or forward one collision callback. The extra compiler, package, import, symbols, and interop surface must buy testable domain value.

Likewise, do not push a frame-critical Burst kernel through F# because the rest of the game uses F#. Burst documents an HPC# subset and a Unity IL post-processing pipeline. Keep such kernels in the supported C# data-oriented shape unless an exact F# experiment proves the required package, attributes, IL, Editor, AOT, performance, and Player behavior.

## X44: one verified managed plug-in boundary {#x44-verified-slice}

X44 implements one horizontal-motion rule. The rule is intentionally too small to justify a production architecture by itself; its purpose is to make the build, API, dependency, host, allocation, linker, and evidence boundaries inspectable.

### The project contract and dependency output {#project-contract}

<<< @/../examples/ecosystem/unity/FSharpGameplay/FSharpGameplay.fsproj{xml:line-numbers} [FSharpGameplay.fsproj]

The project targets `netstandard2.1`, names the assembly `FSharpGameplay`, and compiles only `Gameplay.fs`. `FSharp.Core` is already an implicit F# SDK package; `Update` fixes that one reference at 10.1.301 for this repository instead of adding a duplicate.

`CopyLocalLockFileAssemblies` matters because Unity is not restoring this `.fsproj` when it imports the DLL. The post-build target turns a deployment assumption into a failure: both `FSharpGameplay.dll` and `FSharp.Core.dll` must exist in the output directory.

Package version and assembly version are not the same identifier. The locked NuGet package is 10.1.301; the built plug-in records an assembly reference to `FSharp.Core, Version=10.1.0.0`. Import the dependency produced by the locked build rather than guessing from either number.

### Pure logic behind a CLR-shaped surface {#pure-gameplay}

<<< @/../examples/ecosystem/unity/FSharpGameplay/Gameplay.fs{fsharp:line-numbers} [Gameplay.fs]

`Gameplay.Create` and `Gameplay.Step` are tupled static methods, so C# sees ordinary method calls instead of curried `FSharpFunc` values. `MotionState` exposes read-only float properties and hides its fields and non-default constructor.

The state is a struct. An earlier implementation used a class and therefore allocated a new managed object on every `FixedUpdate`. A regression test now checks `IsValueType` and decodes the managed body of `Gameplay.Step` to reject an explicit `box` instruction. This removes that specific state-object allocation in the managed build without pretending the whole Player allocates zero bytes. A large struct would introduce copying costs, so keep state small and profile the real target.

The transition clamps directional input, rejects non-finite values and negative time or speed, calculates velocity, and returns a new state. It has no `UnityEngine` reference, no current time lookup, and no mutation. Tests can supply every input directly.

### A thin Unity-owned adapter {#csharp-adapter}

<<< @/../examples/ecosystem/unity/FSharpGameplay/UnityAdapter.cs{csharp:line-numbers} [UnityAdapter.cs]

The file and public `MonoBehaviour` class share the name `UnityAdapter`, preserving Unity's ordinary script/component workflow. The Inspector owns one primitive `speed` field. `OnValidate` protects authoring-time configuration, while the F# boundary still validates runtime calls.

`Awake` creates runtime state from the current transform. `FixedUpdate` supplies the input value, configured speed, and Unity's fixed delta time, then maps the returned position back to a `Vector3`. This is a transform sample, not a physics recommendation; a Rigidbody-owned object needs the corresponding physics API and tests.

`SetHorizontal(float)` deliberately avoids choosing the legacy Input Manager or the Input System package. A separate input adapter can call it. This keeps package choice and callback shape out of the rule assembly.

The C# file is registered as illustrative because this repository has no UnityEngine assemblies. It has been source-reviewed but not compiled here. Inventing fake engine types would only prove a fake host.

### Narrow linker roots {#linker-roots}

<<< @/../examples/ecosystem/unity/FSharpGameplay/link.xml{xml:line-numbers} [link.xml]

Direct calls from the C# adapter should be visible to static reachability analysis. X44 still includes two explicit roots to make the intended cross-assembly bridge visible and to provide a concrete stripping artifact for the chapter.

The file does not preserve all of `FSharp.Core`. Broad preservation can hide missing reflection design, enlarge the Player, and increase IL2CPP work. Add a type or member only because an actual dynamic path needs it, then prove the relevant stripping level.

Copy `link.xml` under the Unity project's `Assets` tree. A source file next to the external `.fsproj` has no effect until it becomes a Unity asset.

### Read the evidence ledger literally {#evidence-ledger}

As of 2026-08-25, X44 records:

| Layer | Result | What it proves |
| --- | --- | --- |
| Locked .NET restore | Pass | `netstandard2.1` graph resolves to FSharp.Core package 10.1.301 |
| Release plug-in build | Pass, 0 warnings/errors | F# source compiles on .NET SDK 10.0.301 |
| Output inspection | Pass | 8,704-byte plug-in and 2,407,760-byte FSharp.Core are adjacent; assembly reference is present |
| Focused rule/API test | Pass, 1/1 | Clamp/step behavior, struct state, no explicit `box` in `Step`, FSharp.Core reference, and no F#-specific public signature types |
| Repository example matrix | Pass | The complete ExampleTests suite, other examples, Fable build, and browser smoke remain green |
| Unity 6000.3.22f1 import | Not run | Editor is absent from this machine |
| C# compilation and Play Mode | Not run | UnityEngine host and scene behavior are unverified |
| macOS ARM64 IL2CPP Player | Not run | Native conversion, stripping, link, launch, and runtime behavior are unverified |

The final three rows are evidence, not embarrassment. A visible gap can be scheduled and priced; a false green row cannot.

## Target the compatibility profile, not a runtime name {#compatibility-target}

Unity 6 exposes .NET Standard 2.1 and a broader .NET Framework profile in Player settings. The .NET Standard profile is the cross-platform baseline and is the right first target for a reusable managed plug-in.

### An API profile is only a compile-time ceiling {#profile-not-runtime}

`.NET Standard 2.1` describes a set of APIs. It does not say that Unity embeds the same CoreCLR version as the machine running `dotnet test`, uses the same garbage collector, permits JIT on every platform, or supports every implementation detail of a library that happens to compile against the profile.

Avoid `net10.0`, `netcoreapp`, operating-system-specific target frameworks, dynamic code generation, and accidental platform APIs in the Player plug-in. If one library needs a broader host, keep it outside the Player or add a target-specific adapter with explicit evidence.

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

Never fix a reference problem by disabling assembly version validation before understanding the mismatch. If multiple plug-ins demand incompatible `FSharp.Core` versions, rebuild them onto one tested version, isolate a process, or reject the combination; a single load context cannot honestly contain two files with the same assembly identity.

## Design a boundary that is natural from C# {#design-csharp-boundary}

The F# implementation can remain idiomatic internally. The exported surface should follow the consumer's conventions.

### Prefer ordinary CLR shapes {#clr-shaped-api}

Good Unity-facing choices include:

- namespaces, sealed classes or small structs, PascalCase methods, and read-only properties;
- tupled method arguments so C# calls `Step(state, input, speed, dt)`;
- primitive values, enums, arrays, `IReadOnlyList<T>`, and purpose-named DTOs where appropriate;
- `System.Action` or `System.Func` only when a callback is truly the right contract;
- explicit factory methods when construction must enforce invariants.

Keep F# lists, maps, options, results, discriminated unions, curried functions, and units of measure behind the boundary unless the C# consumer deliberately accepts their compiled forms. They are valid .NET types, not forbidden types; the issue is consumer friction, representation coupling, AOT surface, and maintenance.

Units of measure are erased in emitted .NET signatures. A C# float cannot prove whether it represents seconds, meters, or meters per second. Preserve the meaning with method names, DTO fields, validation, or distinct wrapper types.

### Translate outcomes once {#errors-and-outcomes}

Do not throw on every expected gameplay branch. Model domain outcomes internally with unions or results, then translate them once into a C#-friendly result class, enum plus payload, `Try...` method, or explicit callback message.

Reserve exceptions for broken contracts and failures the current call cannot represent. X44 rejects NaN, infinity, negative speed, and negative delta time because they indicate an invalid boundary call. The C# adapter prevents ordinary authoring errors before reaching it.

For asynchronous work, do not leak an F# `Async<'T>` into Unity. Publish `Task`, `ValueTask`, a C#-friendly polling handle, or a message interface according to the host. Define cancellation ownership and which thread receives completion. Unity object access still belongs to the main thread even if pure computation or I/O runs elsewhere.

### Separate durable data from engine objects {#units-and-data}

Do not put a `GameObject`, `Transform`, `Texture`, scene handle, open stream, cancellation source, or service singleton in a domain state that must be saved, replayed, tested, or sent to a server.

Use stable identifiers and values in F#. Let the adapter resolve them to current Unity objects. The mapping can fail because a scene unloaded, an asset changed, or an object was destroyed; represent that as a boundary outcome rather than pretending the reference is durable.

## Unity serialization is its own contract {#serialization}

Unity's serializer is not ordinary .NET serialization and does not persist arbitrary properties or object graphs.

### Start from supported fields {#supported-fields}

A serializable field is public or marked `[SerializeField]`, is not static, const, or readonly, and has a supported field type. Supported categories include primitives, enums of supported size, Unity built-in values, `UnityEngine.Object` references, serializable custom classes/structs, arrays, and `List<T>` of supported elements.

Properties are not the normal persistence surface. Dictionaries, multidimensional or jagged arrays, and nested containers need an explicit representation or serialization callback. `[SerializeReference]` changes reference and polymorphism behavior but adds its own identity, migration, and type-name risks.

F# records usually expose properties and compiler-generated representation. Unions, options, lists, maps, and closures are not Unity field formats merely because they are managed objects. Some shape may appear to work in one Editor path and still fail prefab persistence, domain reload, stripping, or Player builds.

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

Treat `Awake` or another explicit composition point as construction from serialized configuration. Use `OnEnable` and `OnDisable` to pair subscriptions and cancellation. Do not assume a private managed cache survives reload, or that a non-null-looking `UnityEngine.Object` still owns a live native object.

X44 reconstructs `MotionState` in `Awake` and resets input in `OnDisable`. It does not claim save-game persistence or domain-reload evidence; those belong in a larger Unity project test.

## Respect the game loop and allocation budget {#game-loop}

Functional design helps isolate decisions; it does not exempt code from frame time, memory, cache, or thread constraints.

### Choose the callback from the engine contract {#update-and-fixed-update}

Use `Update` for frame-driven presentation and sampled input, `FixedUpdate` for fixed-step simulation and physics coordination, and late/render callbacks only for their documented purposes. A fixed callback can run zero, one, or multiple times around a rendered frame.

Do not read a transient button edge only inside a callback that may not run for that rendered frame. Capture input in the input layer, then feed stable values or queued commands to the simulation step.

Pass time explicitly into pure logic. That makes pause, replay, slow motion, deterministic tests, and server agreement visible. Determinism also requires controlled randomness, iteration order, floating-point expectations, and no hidden wall clock.

### Measure allocations; do not argue from style {#allocation-budget}

F# pipelines, closures, sequences, records, unions, arrays, and interface calls have different allocation behavior depending on representation and use. “Functional” is neither a proof of allocation nor a proof of zero allocation.

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

Never use “immutable” as permission to read a `Transform`, asset, or destroyed Unity object from a worker. Immutability describes your value; it does not change the engine object's thread or lifetime contract.

## IL2CPP changes the proof obligation {#il2cpp-and-aot}

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

The remedy is not “avoid generics” or “preserve everything.” Prefer static, explicit calls; instantiate required closed generic paths; use AOT-supported library modes; add narrow roots or callback attributes where required; and run the exact Player path.

Record negative cases too. A loader that handles only the happy save type may pass while an older polymorphic save, error subtype, localized resource, or rare callback has been stripped.

### Treat linker rules as tested code {#reflection-and-stripping}

UnityLinker analyzes reachable code and removes code according to the selected Managed Stripping Level. Current Unity 6 documentation marks Minimal as the IL2CPP default, marks Low for future deprecation, and describes Medium and High as more aggressive choices; record the explicit setting because defaults can change.

Use `[Preserve]` when the preserved element can carry a Unity attribute without polluting the reusable layer. Use `link.xml` when preservation belongs to integration configuration or targets an external assembly. Keep type and member names exact, place the file under `Assets`, and test that the expected Player still works when the rule is narrowed.

Preservation prevents removal; it does not make an unsupported API, runtime code generator, native binary, or generic pattern AOT-compatible. It also does not test behavior.

### Burst and Jobs are a separate architecture {#burst-and-jobs}

Burst documents HPC#, a restricted high-performance C#/.NET subset built around unmanaged values, Unity collections, jobs or function pointers, attributes, and IL post-processing. Managed objects, many runtime services, and ordinary exception behavior are outside that kernel model.

X44 is a managed F# plug-in and contains no Burst or Job System evidence. Do not add `[BurstCompile]` to an F#-produced method and infer support from the attribute's presence.

When profiling justifies Burst, a practical boundary is:

```text
F# rules and orchestration
  -> flat arrays/struct commands
  -> small C# Job/Burst kernel
  -> flat result values
  -> F# decision layer
```

Measure conversion cost, scheduling overhead, determinism, safety checks, Editor compilation, Player AOT, and target performance. If the hot kernel dominates the architecture, C# may appropriately own more of that subsystem.

## Build an evidence ladder {#testing-ladder}

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

Record the full Editor patch, not only “Unity 6.3.” X44 selects 6000.3.22f1 because it was the current 6.3 LTS patch when checked on 2026-08-25; that is a review target, not an installed-tool claim.

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

Adopt only the boundary that passes. The result may be “F# owns the entire deterministic simulation,” “F# owns offline rules but not frame code,” “F# stays on the server and tools,” or “C# is simpler here.” All are valid engineering outcomes.

## Avoid common Unity mistakes {#common-mistakes}

- Calling a successful `dotnet build` Unity support.
- Targeting `net10.0` because the developer machine has .NET 10, while the Player plug-in contract is .NET Standard 2.1.
- Copying `FSharpGameplay.dll` but omitting `FSharp.Core.dll` or another transitive dependency.
- Treating a NuGet lock file or `.deps.json` as something Unity automatically restores.
- Disabling reference or assembly-version validation to silence an unexplained mismatch.
- Exposing F# functions, lists, options, or unions to C# accidentally and then writing adapters at every call site.
- Asking Unity to serialize generated F# representations, properties, or arbitrary graphs without prefab/reload/Player evidence.
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

Choose the first F# boundary, rejected alternatives, proof matrix, and reversal condition for: (a) a turn-based tactics game with complex deterministic combat, replay, mod validation, and a modest presentation layer; (b) a console action game whose risk is thousands of physics-like entities, Jobs/Burst performance, platform SDKs, and designer-authored behaviors; (c) a Unity Editor content pipeline that validates dialogue graphs, generates localization reports, and runs headlessly in CI. Do not choose one language split for all three.

### Exercise 2: turn X44 into a Unity vertical slice {#exercise-02}

Design the smallest Unity project and evidence record that could promote X44 from “managed DLL builds” to “representative macOS ARM64 IL2CPP Player works.” Include artifact copying, FSharp.Core identity, assembly definitions, Validate References, scene and input setup, Edit/Play Mode tests, reload behavior, allocation profiling, stripping level, `link.xml`, command-line build profile, launch, logs, symbols, and exact failure semantics. Preserve every unrun row until it actually executes.

### Exercise 3: add saves, asynchronous effects, and dynamic content {#exercise-03}

Extend the architecture for a quest system whose rules are F#, configuration is authored in Unity, saves must migrate across three versions, remote dialogue arrives asynchronously, and optional quest handlers are named in content. Define authoring DTOs, validated domain types, C# API shape, cancellation and stale-result messages, save DTO/migrations, handler registration without unrestricted runtime code generation, narrow preservation rules, malformed/old content tests, and Mono/IL2CPP evidence. State what belongs outside the Player if safe AOT discovery cannot be proven.

[Read the chapter solutions](../solutions/ch-44-unity).

## Chapter review {#chapter-review}

- Unity can execute imported managed F# assemblies, but source-language compatibility is only the first contract.
- Separate .NET build, dependency closure, Unity import, Editor runtime, Mono Player, IL2CPP Player, and release evidence.
- The default low-friction boundary is a pure F# library behind a thin C# Unity adapter.
- Direct F# components are possible managed plug-ins but need exact Unity assembly, Inspector, reload, and Player proof.
- Target `netstandard2.1` for Unity's cross-platform API profile; do not confuse it with CoreCLR or JIT behavior.
- Ship the exact locked `FSharp.Core.dll` and every runtime/native dependency; Unity does not restore the `.fsproj` graph.
- Publish ordinary CLR-shaped methods and values to C# while keeping idiomatic F# types inside.
- Keep Unity serialization fields, engine objects, and lifecycle callbacks in the adapter; map into validated domain state.
- Reconstruct runtime state deliberately across reload, enable/disable, scene, and process lifetimes.
- Pass input, time, randomness, and effects explicitly to pure logic.
- Measure frame code in a target Player; functional style neither guarantees nor forbids allocation.
- X44 uses a small struct state after a regression test exposed a per-step class allocation.
- IL2CPP strips managed code, converts IL to C++, invokes a native toolchain, and creates a platform package.
- Reflection, runtime generation, dynamic generics, callbacks, and native libraries enlarge the AOT proof surface.
- Use narrow, tested preservation rules; preservation is not compatibility or behavior evidence.
- Burst/Jobs use a separate HPC# contract and are not verified by X44.
- Pin the exact Unity patch, modules, packages, build profile, backend, stripping level, tools, and artifacts.
- Keep logs, PDBs, native symbols, hashes, and launch results so failures remain attributable.
- X44 proves a locked F# plug-in build, dependency output, pure rule, CLR-facing API, and repository compatibility.
- It does not prove Unity 6000.3.22f1 import, Play Mode, or a macOS ARM64 IL2CPP Player because the Editor is absent.

Chapter 45 returns to ordinary .NET tooling: scripts, automation, package evaluation, lock discipline, and a practical map for continuing to learn F#.
