---
title: "Chapter 44 Solutions"
description: "Choose proportional F#/C# Unity boundaries, promote X44 through an honest IL2CPP evidence plan, and design versioned quest data without hiding AOT risk."
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
    url: https://docs.unity3d.com/6000.0/Documentation/Manual/testing-editortestsrunner.html
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

# Chapter 44 Solutions {#overview}

These solutions are boundary designs, not claims that one language is best for every Unity subsystem. Each answer names the value F# adds, the Unity-owned surface that remains, the evidence that can reverse the choice, and the exact point at which an automated .NET result stops.

## Exercise 1: choose three language boundaries {#exercise-01}

The three products have different dominant risks. Reusing one language split would optimize for consistency instead of the product.

### A. Turn-based tactics and deterministic replay {#turn-based-tactics}

**First boundary:** an F# simulation/domain assembly behind a thin C# Unity presentation and asset adapter.

F# should own:

- validated unit, ability, tile, faction, resource, and effect identifiers;
- legal action generation, command validation, combat resolution, turn order, victory conditions, and AI evaluation inputs;
- a deterministic state transition from prior state, command, random stream/seed, and rules version to next state plus events;
- replay serialization as commands, seeds, content version, and checksums rather than scene snapshots;
- mod content validation as data against a versioned schema and capability policy;
- property tests for conservation, bounds, turn legality, and replay equivalence.

C# should own scene objects, animation, camera, input, audio, visual effects, addressable assets, Inspector fields, and the mapping from emitted domain events to presentation. Animation completion may send a presentation message, but it must not become the authority for combat outcomes.

Expose ordinary CLR calls such as `ValidateCommand`, `Apply`, and `TryLoadReplay`, with small DTOs or arrays. Keep F# unions and maps inside. If the simulation state is large, do not marshal the whole graph every frame; turn resolution is an event boundary, so exchange one command and a compact event/result batch.

**Mod boundary:** accept declarative content, not arbitrary downloaded managed assemblies. Validate identifiers, limits, references, localization keys, deterministic expressions, and rules version before entering a match. Executable mods introduce trust, platform, AOT, signing, store, and anti-cheat problems far beyond an F# language choice.

**Proof matrix:** pure replay/property tests; C# consumer compile; exact plug-in/dependency inspection; Unity import; one scene replay in Play Mode; save/load across reload; Mono diagnostic Player if used; shipping IL2CPP architectures with at least one old replay and malformed mod; checksum agreement with an independent server/tool implementation; target performance and memory.

**Rejected first alternatives:** direct F# `MonoBehaviour` components would couple deterministic rules to scenes and Inspector representation; putting combat authority in animation callbacks breaks replay; placing every presentation event in the F# DLL adds engine detail without domain value.

**Reversal condition:** reduce the F# boundary if cross-language state conversion dominates the turn budget, debugging is unsupportable, or IL2CPP fails required library paths. Preserve the wire/replay contract so the implementation can move without invalidating saves.

### B. Console action game with Jobs and Burst {#console-action-game}

**First boundary:** C# owns the frame-critical Unity/DOTS/Burst application. F# is optional for slower metagame rules, build tools, backend-shared validation, or offline analysis—not a required Player dependency on day one.

The dominant constraints are entity count, data layout, Burst's HPC# subset, scheduling, native platform SDKs, designer workflow, frame time, and console certification. Those are not improved merely by placing the source in another language.

If progression, inventory, economy, mission planning, or matchmaking rules become complex, add a small F# domain plug-in at an event boundary. Exchange flat arrays or compact structs before/after a batch, not callbacks or F# collections inside a job. Let C# jobs own `NativeArray`, component data, attributes, safety handles, scheduling, and Burst compilation.

**Proof matrix:** representative entity workload in a target development Player; CPU/GPU timeline, `GC.Alloc`, job dependencies, sync points, memory bandwidth, thermal behavior, and frame percentiles; IL2CPP plus Burst AOT on every console architecture; platform SDK callbacks; symbols and crash capture; certification build path. For any F# metagame plug-in, add the dependency/import/AOT rows from Chapter 44.

**Rejected first alternatives:** an F# wrapper around every job adds interop without moving business decisions; direct F# Burst attribution has no evidence; a large immutable world snapshot copied across the boundary each frame fights the data-oriented design.

**Reversal condition:** add no F# at all if the remaining rules stay thin or the language boundary complicates console support. Move a proven non-hot subsystem to F# only when its model/test leverage exceeds packaging and debugging cost.

### C. Headless Editor content pipeline {#editor-content-pipeline}

**First boundary:** a Unity-independent F# validation/report library plus a small C# Editor and batch-mode adapter.

F# should own dialogue graph parsing from stable DTOs, reference checks, cycle/reachability rules, localization coverage, deterministic report rows, severity classification, and pure generation from explicit inputs. Ordinary .NET tests can use small fixtures without starting Unity.

C# should own `AssetDatabase`, import callbacks, GUID/path lookup, `UnityEditor` progress and cancellation, menu/EditorWindow UI, Console diagnostics, and the static batch entry method Unity invokes. It snapshots asset data into detached DTOs before calling F# and maps findings back to asset paths and line/node identifiers.

Use one command-line invocation with the exact Editor, `-batchmode`, `-quit`, `-projectPath`, `-executeMethod`, target/profile when a build is involved, and `-logFile`. Return a nonzero process code for validation failure and a distinguishable code for infrastructure failure. Also emit a machine-readable report so CI does not scrape localized Console prose.

**Proof matrix:** pure fixtures; interactive Editor selection and cancellation; import/reimport without recursion; clean project import; batch run on the same commit; deterministic report comparison across two clean runs; malformed and huge graphs; localization encodings; log/report retention; package update; runtime Player exclusion of Editor-only assemblies.

**Rejected first alternatives:** making the F# library reference `UnityEditor` prevents cheap tests and expands version coupling; running `dotnet` over raw `.meta` files without Unity can misinterpret imported asset state; using Editor UI as the only entry point blocks CI.

**Reversal condition:** move a rule into C# if it fundamentally depends on live Editor objects and the DTO mapping is larger than the rule. Move the entire validator outside Unity only when its source format, GUID resolution, and import semantics are genuinely independent.

## Exercise 2: promote X44 to an IL2CPP vertical slice {#exercise-02}

The goal is not to add many features. It is to execute every missing boundary once with reproducible evidence.

### Proposed project graph {#vertical-slice-graph}

Use two build roots and one copied artifact contract:

```text
src/FSharpGameplay/                 # existing locked netstandard2.1 project
artifacts/unity-plugin/             # generated, hashed DLL/PDB bundle
unity/FSharpVerticalSlice/
  Assets/Plugins/ThinkingInFSharp/  # copied FSharpGameplay.dll + FSharp.Core.dll
  Assets/Scripts/                   # UnityAdapter.cs + input adapter
  Assets/Tests/EditMode/            # mapping/import tests
  Assets/Tests/PlayMode/            # scene/lifecycle tests
  Assets/Linker/link.xml
  Assets/Settings/Build Profiles/   # versioned macOS ARM64 profile
  Packages/manifest.json + packages-lock.json
  ProjectSettings/ProjectVersion.txt and Player settings
```

Do not commit `Library`, `Temp`, or local build outputs. Either commit imported plug-in binaries with a documented update command or generate them before Unity import; whichever policy is chosen, CI must compare hashes and reject stale/mixed files.

### Artifact and assembly contract {#artifact-contract}

Build with the repository's locked .NET SDK and `dotnet restore --locked-mode`, then Release `--no-restore`. Copy exactly:

- `FSharpGameplay.dll`;
- the adjacent `FSharp.Core.dll` resolved from package 10.1.301;
- portable PDBs for diagnostic builds;
- `UnityAdapter.cs` and `link.xml` from the same commit;
- a generated manifest containing SHA-256, file size, package version, plug-in assembly identity, FSharp.Core assembly identity, commit, and build command.

Fail before launching Unity if a required file or hash differs. Do not copy reference assemblies, `.deps.json` as a resolver, or arbitrary DLLs from the global NuGet cache.

### Unity assembly and scene boundary {#unity-project-boundary}

Pin `ProjectVersion.txt` to Unity 6000.3.22f1 and install the macOS IL2CPP module on the macOS builder. Set API Compatibility Level to .NET Standard. Keep Validate References and assembly-version validation enabled.

Create a runtime assembly definition for the adapter. Disable plug-in Auto Reference and explicitly reference `FSharpGameplay.dll` and `FSharp.Core.dll` where the selected Unity configuration requires it; ensure tests reference the runtime adapter assembly, not the reverse. Keep Editor tests/code out of the Player.

Create one scene with a named GameObject and `UnityAdapter`, a visible position marker, and a deterministic test input adapter. The input test sends `-1`, `0`, `1`, and out-of-range values through `SetHorizontal`; it does not depend on a physical controller or project-wide input package.

Edit Mode tests verify DTO/mapping helpers and that expected plug-in types and assembly identities load. Play Mode tests verify `Awake` initialization, positive/negative movement across known fixed steps, disable/reset behavior, no Console exception, scene reload, and the project's chosen domain/scene reload settings.

### Performance and stripping proof {#performance-and-stripping-proof}

Profile a development Player, not only the Editor. After warm-up, capture fixed-step frames and assert an agreed allocation budget for the adapter/step path. The struct regression prevents a `MotionState` class allocation, but Unity calls, test harnesses, logging, and input can still allocate.

Select and record one explicit managed stripping level. Start with the release's intended setting rather than changing it until the build passes. Exercise both public bridge types and every dynamically found path. Temporarily removing each `link.xml` entry in a negative experiment can show whether it is necessary; keep only roots justified by behavior.

Build both a diagnostic IL2CPP profile with useful stack trace/symbol settings and a release-like profile. A green diagnostic build alone does not prove optimized stripping behavior.

### Reproducible build and launch {#build-and-launch}

Invoke the exact Editor executable once for the macOS profile with `-batchmode`, `-quit`, `-projectPath`, `-activeBuildProfile`, and `-logFile`. Do not switch targets inside the method. Treat any unexpected warning under the agreed policy as a failure.

After build:

1. verify exit code and parse the structured build result rather than one success string;
2. archive Editor log, test XML, build report, plug-in manifest, symbols, and Player hash;
3. inspect the Player architecture and signing state;
4. launch the `.app` in a graphical macOS session with a timeout;
5. wait for a machine-readable ready marker containing build and plug-in identities;
6. drive or automatically run the representative motion/lifecycle checks;
7. collect Player log and exit status;
8. terminate cleanly and retain artifacts for a failed run.

An automation session without a usable graphical context is an environment failure row, not an application pass or fail. Retry in the intended interactive/CI runner and preserve both results.

### Evidence record and failure semantics {#vertical-slice-evidence}

Record rows independently:

| Row | Pass condition | Failure owner |
| --- | --- | --- |
| Locked F# build | Exact SDK/lock, 0 warnings/errors, expected bundle manifest | F# source/package pipeline |
| Clean Unity import | Exact patch, clean import, references valid, no compiler/import errors | Asset/plugin integration |
| Edit Mode | Mapping and identities pass with XML output | Adapter/assembly configuration |
| Play Mode | Scene, lifecycle, reload, movement, and Console pass | Unity host behavior |
| Allocation | Target Player capture meets named budget | Representation/hot path |
| IL2CPP build | Explicit profile/backend/stripping/architecture completes | Linker/AOT/native toolchain |
| Player launch | Named artifact starts and emits ready identity | Package/runtime/environment |
| Player behavior | Representative checks and logs pass | Integrated application |
| Diagnostics | Deliberate failure symbolizes to useful F# and C# frames | Symbols/crash pipeline |

Only after all rows pass may the statement become: “X44's representative slice works in a Unity 6000.3.22f1 macOS ARM64 IL2CPP Player under this build profile.” It still says nothing about Windows, mobile, consoles, Web, another stripping level, or the whole game.

## Exercise 3: saves, asynchronous effects, and dynamic content {#exercise-03}

The central move is to separate authoring, validated runtime state, durable storage, and handler discovery.

### Four models with explicit ownership {#quest-models}

Use these boundaries:

| Model | Owner | Shape |
| --- | --- | --- |
| Quest authoring DTO | C#/Unity | `[Serializable]` classes/structs, supported fields, asset GUIDs, primitive lists |
| Validated quest definition/state | F# | private constructors, records/unions/maps internally, no Unity objects |
| Public bridge DTO | CLR-oriented F# types or C# | enums, small structs/classes, arrays, static methods, explicit result/error payload |
| Save DTO | versioned storage contract | stable IDs, primitive/array data, schema version, checksums; no scene references |

The C# adapter reads authoring fields or assets, snapshots them into bridge DTOs, and calls `QuestApi.ValidateDefinitions`. Validation returns all actionable errors with quest/node/field identity; it does not throw on ordinary bad content.

After validation, F# owns impossible-state modeling: a quest ID cannot be blank, a transition targets a known node, completion and cancellation are distinct, rewards are validated, and handler names have passed an allowlist. Unity receives a compact presentation snapshot and emitted commands/events.

### Version saves through pure migrations {#save-migrations}

Define three explicit persisted schemas rather than deserializing today's domain type:

- v1 stores quest ID and completed node IDs;
- v2 adds objective progress with explicit defaults derived during v1→v2 migration;
- v3 replaces raw quest IDs with content-version plus stable quest key and records an in-flight operation identity.

Parse into the version-specific DTO, validate size/checksum, migrate one step at a time, then construct current domain state. Keep the original bytes until the new state is validated and atomically written. Unknown future versions must fail safely without overwriting data.

Golden fixtures cover valid v1/v2/v3, missing optional fields, duplicate IDs, unknown quest content, corrupt/truncated data, oversized collections, interrupted replacement, downgrade, and migration idempotence. Run them under ordinary .NET and again through the Unity Player serializer/file adapter actually shipped.

### Model asynchronous delivery as messages {#async-quest-effects}

Give every remote dialogue request an operation ID, quest/content version, and cancellation owner. The F# transition emits `FetchDialogue` and enters a loading state. The C# host performs network work without touching scene objects from a worker and later dispatches one of completed, unavailable, cancelled, malformed, or failed messages.

The update accepts completion only when operation ID, quest, content version, and current state match. Scene unload, component disable, a new request, logout, or content update cancels or supersedes the old operation. A late response becomes an ignored diagnostic event, not new UI.

Persist enough identity before a consequential request to reconcile an unknown outcome after process death. Do not persist cancellation tokens, tasks, UnityWebRequest objects, delegates, or GameObjects.

### Replace unrestricted reflection with a closed registry {#closed-handler-registry}

Content may name optional handlers, but it must select from a compiled allowlist:

| Content name | Statically registered operation |
| --- | --- |
| `grant-item` | validates item ID/count and emits a grant command |
| `set-flag` | validates flag/value and emits a flag command |
| `start-timer` | validates duration and emits a timer effect |

Build the registry through explicit calls in reachable code. The public API can accept a handler name and payload DTO, but no arbitrary assembly-qualified type name. Unknown names are validation errors. This is safer for trust, migration, tooling, stripping, and IL2CPP.

If a library internally reflects over known DTO members, select its documented AOT mode, generate metadata at build time where supported, and add only the exact preservation entries it requires. Prove every registered handler and error path in the intended IL2CPP/stripping matrix.

When content truly requires arbitrary executable extensions, keep execution on a controlled server/tool process or adopt a deliberately sandboxed, platform-supported data language. Do not smuggle downloaded managed assemblies into a signed IL2CPP Player and call `link.xml` a sandbox.

### Complete evidence matrix {#quest-evidence}

The minimum evidence includes:

- property tests for quest transitions and handler allowlist completeness;
- golden save migrations plus corrupt, old, future, and huge payloads;
- C# consumer/API surface reflection tests with no accidental F# types;
- Unity authoring round-trip across prefab/asset save and script reload;
- cancellation, timeout, process loss, late response, content version change, and duplicate callback scenarios;
- clean import with exact dependencies and assembly identities;
- every handler in Mono if shipped and in each IL2CPP architecture at the explicit stripping level;
- target allocation, latency, offline, memory, suspend/resume, log, symbol, and crash evidence;
- rollback behavior when a new content version or save migration is withdrawn.

**Reversal condition:** if dynamic extension requirements cannot be expressed as a closed, testable AOT surface, move that execution outside the Player. Keep the validated quest protocol and F# domain where useful; do not preserve an unbounded runtime merely to retain one implementation.

## Solution takeaways {#solution-takeaways}

- Let the dominant product risk choose the language boundary.
- Complex turn/replay rules are a strong F# core candidate; scene presentation remains Unity-owned.
- A Burst-heavy action game may reasonably keep the Player in C# and use F# only off the hot path or not at all.
- Editor tooling benefits from a pure F# validator only when `UnityEditor` stays in a thin adapter.
- Promoting X44 requires clean Unity import, Edit/Play Mode, target profiling, IL2CPP build, graphical launch, behavior, and diagnostics as separate rows.
- Exact DLL identities and hashes are part of the import contract, especially for FSharp.Core.
- Use Unity-supported authoring DTOs, rich validated F# state, CLR bridge types, and versioned save DTOs as distinct models.
- Migrate stored schemas stepwise and never deserialize durable data directly into today's domain representation.
- Model asynchronous completions with identities and reject stale results after scene, content, or operation changes.
- Replace content-named reflection with a statically reachable allowlist whenever possible.
- Preservation rules keep code; they do not provide trust, sandboxing, AOT support, or behavior proof.
- Move unbounded executable extensions outside the Player rather than hiding them behind broad linker roots.

[Return to Chapter 44](../part-07/ch-44-unity).
