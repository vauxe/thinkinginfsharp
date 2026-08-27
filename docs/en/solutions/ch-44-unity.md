---
title: "Chapter 44 Solutions"
description: "Choose proportional F#/C# Unity boundaries, verify the managed plug-in sample in IL2CPP, and design versioned quest data without hiding AOT risk."
translationKey: solutions/ch-44-unity
---

# Chapter 44 Solutions {#overview}

These designs do not assume that one language suits every Unity subsystem. Each answer explains where F# helps, what Unity and C# still handle, which results could reverse the choice, and what ordinary .NET tests cannot establish.

## Exercise 1: choose three language boundaries {#exercise-01}

The three products have different dominant risks. Reusing one language split would optimize for consistency instead of the product.

### A. Turn-based tactics and deterministic replay {#turn-based-tactics}

**Initial boundary:** put the simulation and domain rules in an F# assembly. Connect it to Unity through a thin C# presentation and asset adapter.

The F# assembly should contain:

- validated unit, ability, tile, faction, resource, and effect identifiers;
- legal action generation, command validation, combat resolution, turn order, victory conditions, and AI evaluation inputs;
- a deterministic transition from the prior state, command, random source or seed, and rules version to the next state and emitted events;
- replay serialization as commands, seeds, content version, and checksums rather than scene snapshots;
- mod content validation as data against a versioned schema and capability policy;
- property tests for conservation, bounds, turn legality, and replay equivalence.

C# should handle scene objects, animation, camera, input, audio, visual effects, addressable assets, Inspector fields, and the mapping from domain events to presentation. An animation-complete callback may send a presentation message, but it must not decide the combat result.

Expose conventional CLR methods such as `ValidateCommand`, `Apply`, and `TryLoadReplay`, using small DTOs or arrays. Keep F# unions and maps internal. If the simulation state is large, do not marshal the entire object graph every frame. Turn resolution is an event boundary, so exchange one command and a compact batch of events and results.

**Mod boundary:** accept declarative content, not arbitrary downloaded managed assemblies. Validate identifiers, limits, references, localization keys, deterministic expressions, and rules version before entering a match. Executable mods introduce trust, platform, AOT, signing, store, and anti-cheat problems far beyond an F# language choice.

**Verification matrix:**

- run pure replay and property tests, then compile a C# consumer;
- inspect the exact plug-in dependencies and import them into Unity;
- replay one scene in Play Mode and save and load across a reload;
- if Mono ships, run a diagnostic Mono Player;
- test every shipping IL2CPP architecture with an old replay and a malformed mod;
- compare checksums with an independent server or tool implementation;
- measure performance and memory on the target hardware.

**Rejected first alternatives:**

- Direct F# `MonoBehaviour` components would couple deterministic rules to scenes and Inspector representation.
- Letting animation callbacks decide combat outcomes would break replay.
- Putting every presentation event in the F# DLL would add engine detail without domain value.

**Reversal condition:** reduce the F# boundary if cross-language conversion dominates the turn budget, debugging becomes impractical, or IL2CPP cannot compile a required library path. Preserve the data-transfer and replay contracts so the implementation can move without invalidating saves.

### B. Console action game with Jobs and Burst {#console-action-game}

**Initial boundary:** keep the frame-critical Unity/DOTS/Burst application in C#. Use F# only where it pays for itself: slower metagame rules, build tools, shared backend validation, or offline analysis. Do not make it a required Player dependency on day one.

The dominant constraints are entity count, data layout, Burst's HPC# subset, scheduling, native platform SDKs, designer workflow, frame time, and console certification. Those are not improved merely by placing the source in another language.

If progression, inventory, economy, mission planning, or matchmaking rules become complex, add a small F# domain plug-in at an event boundary. Exchange flat arrays or compact structs before and after a batch, not callbacks or F# collections inside a job. Keep `NativeArray`, component data, attributes, safety handles, scheduling, and Burst compilation in the C# jobs.

**Verification matrix:** run a representative entity workload in a development Player on target hardware. Record CPU/GPU timelines, `GC.Alloc`, job dependencies, synchronization points, memory bandwidth, thermal behavior, and frame-time percentiles. Also verify IL2CPP and Burst AOT on every console architecture, platform SDK callbacks, symbols, crash capture, and the certification build path. For an F# metagame plug-in, add the dependency, import, and AOT checks used elsewhere in this chapter.

**Rejected first alternatives:**

- An F# wrapper around every job would add interop without moving business decisions.
- No measured result yet supports applying Burst directly to F# code.
- Copying a large immutable world snapshot across the boundary every frame would work against the data-oriented design.

**Reversal condition:** add no F# at all if the remaining rules stay thin or the language boundary complicates console support. Move a proven non-hot subsystem to F# only when its model/test leverage exceeds packaging and debugging cost.

### C. Headless Editor content pipeline {#editor-content-pipeline}

**Initial boundary:** use an F# validation and reporting library that does not depend on Unity. Add a small C# adapter for the Editor and batch mode.

The F# library should parse dialogue graphs from stable DTOs, check references, detect cycles and unreachable nodes, measure localization coverage, classify severity, and generate deterministic report entries from explicit inputs. Ordinary .NET tests can use small fixtures without starting Unity.

The C# adapter should handle `AssetDatabase`, import callbacks, GUID and path lookup, `UnityEditor` progress and cancellation, menu or EditorWindow UI, Console diagnostics, and the static batch entry point that Unity invokes. Before calling F#, it copies asset data into detached DTOs. It then maps each finding back to an asset path and a line or node identifier.

Invoke a specific Editor executable once with `-batchmode`, `-quit`, `-projectPath`, `-executeMethod`, and `-logFile`. Add the target and profile when the command builds a Player. Return a nonzero exit code for validation failures and a different code for infrastructure failures. Also write a machine-readable report so CI does not have to scrape localized Console messages.

**Verification matrix:** test pure fixtures, interactive Editor selection and cancellation, import and reimport without recursion, and a clean project import. Run the batch command twice from the same commit and compare the reports. Include malformed and very large graphs, localization encodings, log and report retention, a package update, and confirmation that Editor-only assemblies are absent from the runtime Player.

**Rejected first alternatives:**

- Referencing `UnityEditor` from the F# library would make tests expensive and increase version coupling.
- Running `dotnet` over raw `.meta` files without Unity could misread imported asset state.
- Providing only an Editor UI would leave CI without an entry point.

**Reversal condition:** move a rule into C# if it depends on active Editor objects and its DTO mapping is larger than the rule itself. Move the entire validator outside Unity only when the source format, GUID resolution, and import semantics are truly independent of Unity.

## Exercise 2: promote the managed plug-in sample to an IL2CPP vertical slice {#exercise-02}

The goal is not to add features. It is to exercise every unverified boundary once and retain reproducible results.

### Proposed project graph {#vertical-slice-graph}

Use two build roots and one contract for the copied artifacts:

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

Do not commit `Library`, `Temp`, or local build outputs. Either commit the imported plug-in binaries and document their update command, or generate them before Unity imports the project. In either case, CI must compare hashes and reject stale or mixed files.

### Artifact and assembly contract {#artifact-contract}

Use the project's selected .NET SDK. Run `dotnet restore --locked-mode`, then a Release build with `--no-restore`. Copy only:

- `FSharpGameplay.dll`;
- the adjacent `FSharp.Core.dll` resolved from package 10.1.301;
- portable PDBs for diagnostic builds;
- `UnityAdapter.cs` and `link.xml` from the same commit;
- a generated manifest containing each file's SHA-256 and size, both assembly identities, the package version, commit, and build command.

Fail before launching Unity if a required file is missing or its hash differs. Do not copy reference assemblies, use `.deps.json` as a resolver, or take arbitrary DLLs from the global NuGet cache.

### Unity assembly and scene boundary {#unity-project-boundary}

Pin `ProjectVersion.txt` to Unity 6000.3.22f1 and install the macOS IL2CPP module on the macOS builder. Set API Compatibility Level to .NET Standard. Keep Validate References and assembly-version validation enabled.

Create a runtime assembly definition for the adapter. Disable plug-in Auto Reference and explicitly reference `FSharpGameplay.dll` and `FSharp.Core.dll` where the selected Unity configuration requires it. Tests may reference the runtime adapter assembly; the runtime assembly must not reference the tests. Keep all Editor-only tests and code out of the Player.

Create one scene with a named GameObject and `UnityAdapter`, a visible position marker, and a deterministic test input adapter. The input test sends `-1`, `0`, `1`, and out-of-range values through `SetHorizontal`; it does not depend on a physical controller or project-wide input package.

Edit Mode tests verify DTO and mapping helpers, plug-in type loading, and assembly identities. Play Mode tests cover `Awake` initialization, positive and negative movement over known fixed steps, disable and reset behavior, Console exceptions, scene reload, and the selected domain-reload and scene-reload settings.

### Verify performance and stripping {#performance-and-stripping-proof}

Profile a development Player, not only the Editor. After warm-up, capture fixed-step frames and check the adapter and step path against an agreed allocation budget. Making `MotionState` a struct avoids one class allocation, but Unity calls, test harnesses, logging, and input may still allocate.

Select and record one managed stripping level. Start with the intended release setting instead of weakening it until the build passes. Exercise both public bridge types and every dynamically discovered path. To test a `link.xml` entry, remove it temporarily and rerun the relevant behavior. Keep only the roots that a failing negative test shows are necessary.

Build both a diagnostic IL2CPP profile with useful stack trace/symbol settings and a release-like profile. A green diagnostic build alone does not prove optimized stripping behavior.

### Reproducible build and launch {#build-and-launch}

Invoke the selected Editor executable once for the macOS profile with `-batchmode`, `-quit`, `-projectPath`, `-activeBuildProfile`, and `-logFile`. Do not switch targets inside the build method. Fail on warnings that the project's warning policy does not allow.

After build:

1. verify the exit code and parse the structured build result instead of looking for one success string;
2. archive Editor log, test XML, build report, plug-in manifest, symbols, and Player hash;
3. inspect the Player architecture and signing state;
4. launch the `.app` in a graphical macOS session with a timeout;
5. wait for a machine-readable ready marker containing build and plug-in identities;
6. drive or automatically run the representative motion/lifecycle checks;
7. collect Player log and exit status;
8. terminate cleanly and retain the artifacts from any failed run.

An automation session without a usable graphical context is an environment failure, not an application result. Retry on the intended interactive or CI runner and retain both records.

### Verification record and failure categories {#vertical-slice-evidence}

Record rows independently:

| Check | Pass condition | Responsible area |
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

Only after every check passes can you make this statement:

> The representative managed plug-in slice works in a Unity 6000.3.22f1 macOS ARM64 IL2CPP Player under this build profile.

This result does not cover Windows, mobile, consoles, Web, another stripping level, or the entire game.

## Exercise 3: saves, asynchronous effects, and dynamic content {#exercise-03}

Separate authoring data, validated runtime state, durable storage, and handler discovery.

### Four models with distinct responsibilities {#quest-models}

Use these boundaries:

| Model | Managed by | Representation |
| --- | --- | --- |
| Quest authoring DTO | C#/Unity | `[Serializable]` classes/structs, supported fields, asset GUIDs, primitive lists |
| Validated quest definition/state | F# | private constructors, records/unions/maps internally, no Unity objects |
| Public bridge DTO | CLR-oriented F# types or C# | enums, small structs/classes, arrays, static methods, explicit result/error payload |
| Save DTO | versioned storage contract | stable IDs, primitive/array data, schema version, checksums; no scene references |

The C# adapter reads authoring fields or assets, snapshots them into bridge DTOs, and calls `QuestApi.ValidateDefinitions`. Validation returns all actionable errors with quest/node/field identity; it does not throw on ordinary bad content.

After validation, the F# model enforces its invariants: quest IDs are nonblank, transitions target known nodes, completion and cancellation remain distinct, rewards are valid, and handler names belong to the allowlist. Unity receives a compact presentation snapshot and the emitted commands and events.

### Version saves through pure migrations {#save-migrations}

Define three explicit persisted schemas rather than deserializing today's domain type:

- v1 stores quest ID and completed node IDs;
- v2 adds objective progress with explicit defaults derived during v1→v2 migration;
- v3 replaces raw quest IDs with content-version plus stable quest key and records an in-flight operation identity.

Parse into the version-specific DTO, validate size/checksum, migrate one step at a time, then construct current domain state. Keep the original bytes until the new state is validated and atomically written. Unknown future versions must fail safely without overwriting data.

Golden fixtures cover valid v1/v2/v3, missing optional fields, duplicate IDs, unknown quest content, corrupt/truncated data, oversized collections, interrupted replacement, downgrade, and migration idempotence. Run them under ordinary .NET and again through the Unity Player serializer/file adapter actually shipped.

### Model asynchronous results as messages {#async-quest-effects}

Give every remote dialogue request an operation ID, a quest and content version, and a component or session responsible for cancellation. The F# transition emits `FetchDialogue` and enters a loading state. The C# host performs the network request without touching scene objects from a worker thread. It later dispatches one of five messages: completed, unavailable, cancelled, malformed, or failed.

The update accepts completion only when operation ID, quest, content version, and current state match. Scene unload, component disable, a new request, logout, or content update cancels or supersedes the old operation. A late response becomes an ignored diagnostic event, not new UI.

Before a consequential request, persist enough identifiers to recover an ambiguous outcome after process termination. Do not persist cancellation tokens, tasks, `UnityWebRequest` objects, delegates, or GameObjects.

### Replace unrestricted reflection with a closed registry {#closed-handler-registry}

Content may name optional handlers, but it must select from a compiled allowlist:

| Content name | Statically registered operation |
| --- | --- |
| `grant-item` | validates item ID/count and emits a grant command |
| `set-flag` | validates flag/value and emits a flag command |
| `start-timer` | validates duration and emits a start-timer command |

Build the registry through explicit calls in reachable code. The public API can accept a handler name and payload DTO, but no arbitrary assembly-qualified type name. Unknown names are validation errors. This is safer for trust, migration, tooling, stripping, and IL2CPP.

If a library internally reflects over known DTO members, select its documented AOT mode and generate metadata at build time where supported. Add only the preservation entries it requires. Run every registered handler and error path in the intended IL2CPP and stripping configurations.

When content truly requires arbitrary executable extensions, keep execution on a controlled server/tool process or adopt a deliberately sandboxed, platform-supported data language. Do not smuggle downloaded managed assemblies into a signed IL2CPP Player and call `link.xml` a sandbox.

### Complete verification checklist {#quest-evidence}

At minimum, verify:

- property tests for quest transitions and handler allowlist completeness;
- golden save migrations plus corrupt, old, future, and huge payloads;
- C# consumer and public-API reflection tests that detect accidental F# types;
- Unity authoring round-trip across prefab/asset save and script reload;
- cancellation, timeout, process loss, late response, content version change, and duplicate callback scenarios;
- clean import with exact dependencies and assembly identities;
- every handler in Mono if shipped and in each IL2CPP architecture at the explicit stripping level;
- target allocation, latency, offline behavior, memory, suspend and resume, logs, symbols, and crash capture;
- rollback behavior when a new content version or save migration is withdrawn.

**Reversal condition:** if dynamic extensions cannot fit a closed, testable AOT API, run them outside the Player. Keep the validated quest protocol and F# domain where they remain useful. Do not retain an unbounded extension mechanism merely to preserve one implementation.

## Solution takeaways {#solution-takeaways}

- Let the dominant product risk choose the language boundary.
- Complex turn/replay rules are a strong F# core candidate; scene presentation remains Unity-owned.
- A Burst-heavy action game may reasonably keep the Player in C# and use F# only off the hot path or not at all.
- Editor tooling benefits from a pure F# validator only when `UnityEditor` stays in a thin adapter.
- Promoting the managed plug-in sample requires clean Unity import, Edit/Play Mode, target profiling, IL2CPP build, graphical launch, behavior, and diagnostics as separate rows.
- Exact DLL identities and hashes are part of the import contract, especially for FSharp.Core.
- Use Unity-supported authoring DTOs, rich validated F# state, CLR bridge types, and versioned save DTOs as distinct models.
- Migrate stored schemas stepwise and never deserialize durable data directly into today's domain representation.
- Model asynchronous completions with identities and reject stale results after scene, content, or operation changes.
- Replace content-named reflection with a statically reachable allowlist whenever possible.
- Preservation rules keep code; they do not provide trust, sandboxing, AOT support, or behavior proof.
- Move unbounded executable extensions outside the Player rather than hiding them behind broad linker roots.

[Return to Chapter 44](../part-07/ch-44-unity).
