---
title: "Chapter 43: Avalonia, Desktop, and Mobile"
description: "Design F# user interfaces from state, lifetime, platform, tooling, packaging, and evidence boundaries instead of treating cross-platform compilation as cross-platform validation."
translationKey: part-07/ch-43-avalonia-desktop-mobile
kind: chapter
part: 7
chapter: 43
status: complete
verifiedWith:
  fsharp: "10"
  dotnetSdk: "10.0.301"
exampleIds:
  - ecosystem-avalonia-desktop
exerciseIds:
  - ch43-exercise-01
  - ch43-exercise-02
  - ch43-exercise-03
termIds: []
sources:
  - id: avalonia-get-started
    url: https://docs.avaloniaui.net/docs/get-started/
    checked: "2026-08-25"
  - id: avalonia-templates
    url: https://github.com/AvaloniaUI/Avalonia.Templates
    checked: "2026-08-25"
  - id: avalonia-desktop-nuget
    url: https://www.nuget.org/packages/Avalonia.Desktop/12.1.1
    checked: "2026-08-25"
  - id: avalonia-supported-platforms
    url: https://docs.avaloniaui.net/docs/supported-platforms
    checked: "2026-08-25"
  - id: avalonia-cross-platform-architecture
    url: https://docs.avaloniaui.net/docs/fundamentals/cross-platform-architecture
    checked: "2026-08-25"
  - id: avalonia-cross-platform-solution
    url: https://docs.avaloniaui.net/docs/app-development/cross-platform-solution-setup
    checked: "2026-08-25"
  - id: avalonia-application-lifetimes
    url: https://docs.avaloniaui.net/docs/fundamentals/application-lifetimes
    checked: "2026-08-25"
  - id: avalonia-12-breaking-changes
    url: https://docs.avaloniaui.net/docs/avalonia12-breaking-changes
    checked: "2026-08-25"
  - id: avalonia-xaml-compilation
    url: https://docs.avaloniaui.net/docs/xaml/compilation
    checked: "2026-08-25"
  - id: avalonia-coded-ui
    url: https://docs.avaloniaui.net/docs/fundamentals/coded-ui
    checked: "2026-08-25"
  - id: avalonia-threading
    url: https://docs.avaloniaui.net/docs/app-development/threading
    checked: "2026-08-25"
  - id: avalonia-responsive-layouts
    url: https://docs.avaloniaui.net/docs/layout/responsive-layouts
    checked: "2026-08-25"
  - id: avalonia-accessibility
    url: https://docs.avaloniaui.net/docs/app-development/accessibility
    checked: "2026-08-25"
  - id: avalonia-headless-testing
    url: https://docs.avaloniaui.net/docs/testing/setting-up-the-headless-platform
    checked: "2026-08-25"
  - id: avalonia-windows
    url: https://docs.avaloniaui.net/docs/platform-specific-guides/windows
    checked: "2026-08-25"
  - id: avalonia-macos
    url: https://docs.avaloniaui.net/docs/platform-specific-guides/macos
    checked: "2026-08-25"
  - id: avalonia-linux
    url: https://docs.avaloniaui.net/docs/platform-specific-guides/linux
    checked: "2026-08-25"
  - id: avalonia-android
    url: https://docs.avaloniaui.net/docs/platform-specific-guides/android/
    checked: "2026-08-25"
  - id: avalonia-ios
    url: https://docs.avaloniaui.net/docs/platform-specific-guides/ios
    checked: "2026-08-25"
  - id: avalonia-deploy-macos
    url: https://docs.avaloniaui.net/docs/deployment/macos
    checked: "2026-08-25"
  - id: avalonia-deploy-linux
    url: https://docs.avaloniaui.net/docs/deployment/linux
    checked: "2026-08-25"
  - id: avalonia-deploy-ios
    url: https://docs.avaloniaui.net/docs/deployment/ios
    checked: "2026-08-25"
  - id: dotnet-publishing
    url: https://learn.microsoft.com/dotnet/core/deploying/
    checked: "2026-08-25"
  - id: dotnet-wpf-migration
    url: https://learn.microsoft.com/dotnet/desktop/wpf/migration/
    checked: "2026-08-25"
  - id: dotnet-maui
    url: https://learn.microsoft.com/dotnet/maui/?view=net-maui-10.0
    checked: "2026-08-25"
  - id: dotnet-maui-templates
    url: https://github.com/dotnet/maui/tree/main/src/Templates/src/templates
    checked: "2026-08-25"
---

# Chapter 43: Avalonia, Desktop, and Mobile {#overview}

An F# user interface is not “some controls around the real program.” It is a long-lived boundary where input, time, cancellation, mutable platform objects, accessibility, operating-system services, and release mechanics meet. F# helps most when the application turns those events into explicit data and keeps decisions testable before a window exists.

Avalonia is a cross-platform .NET UI framework with official F# templates. It draws its own controls and provides desktop, mobile, and browser hosts, but that does not make every platform identical. A shared view can compile while its font, input, lifecycle, permission, native integration, package, signing, or accessibility path fails on one target. “Cross-platform” describes an architecture and support surface; it is not a test result.

This chapter therefore starts with product and platform constraints, not XAML syntax. It uses X43 to show one small, verified desktop slice, then expands outward to state patterns, binding boundaries, threading, platform services, mobile hosts, testing, packaging, and release evidence.

## What you will be able to do {#outcomes}

By the end of this chapter, you should be able to:

- separate domain state, presentation state, view objects, platform hosts, and distributable packages;
- choose a UI approach from users, devices, native capabilities, team skills, and release channels;
- explain why shared source, shared UI, and verified behavior are different claims;
- identify when Avalonia, a Windows-only UI, .NET MAUI, a browser UI, or thin native shells deserve a spike;
- read an Avalonia project as ordinary .NET plus XAML build tasks and native backends;
- keep an F# `update` function independent of controls and dispatch UI events into messages;
- choose deliberately among manual MVU, MVVM adapters, code-behind, and code-only UI;
- make F# records, unions, options, commands, and collections explicit at a binding boundary;
- use Avalonia 12 compiled bindings without hiding dynamic reflection behind accidental defaults;
- distinguish classic desktop, single-view, and Android activity lifetimes;
- keep blocking work off the UI thread and marshal only view updates through a dispatcher;
- isolate clipboard, dialogs, notifications, files, camera, permissions, and secure storage behind ports;
- design layouts for resize, scale, keyboard, touch, localization, and assistive technology;
- distinguish a XAML build, headless test, native launch, packaged install, signed artifact, and store release;
- publish per runtime identifier and choose framework-dependent or self-contained delivery consciously;
- state exactly what X43 verifies and why its attempted native launch did not pass;
- design a reversible desktop or mobile adoption spike with an explicit evidence matrix.

## A UI application is a stack of contracts {#ui-stack-contracts}

Treat a client application as five connected layers:

```text
domain rules and durable data
  -> presentation model and pure transitions
  -> toolkit controls, layout, binding, and input
  -> platform host, lifecycle, permissions, and native services
  -> architecture-specific package, signing, installation, and updates
```

The domain can often be a normal F# library. Presentation state turns domain outcomes and UI events into states the screen can render. Avalonia controls are mutable .NET objects owned by a UI dispatcher. The host decides whether the top level is a desktop window, an Android activity, or a single mobile view. Packaging then adds operating-system identity, architecture, metadata, signing, distribution, and upgrade behavior.

A green lower layer does not prove the layers above it. A pure transition test says nothing about XAML names. A XAML build says nothing about a native display. A native debug launch says nothing about signing. A signed package says nothing about upgrade safety or accessibility.

### Shared is not the same as identical {#shared-not-identical}

Use three separate percentages when discussing reuse:

1. **Shared logic:** domain rules, validation, network contracts, persistence abstractions, and presentation transitions.
2. **Shared UI:** views, styles, resources, navigation concepts, and toolkit-specific adapters.
3. **Shared evidence:** tests and observations that actually ran on each supported OS, architecture, input mode, and release channel.

The first can be high while the second is low for a native-feeling mobile product. The first two can be high while the third remains near zero before device and packaging tests. Reuse is valuable, but an honest denominator is more valuable than a large percentage.

## Choose from the product boundary {#decision-map}

Start with the users and release constraints:

| First candidate | Strong fit | F# boundary | Evidence still required |
| --- | --- | --- | --- |
| Avalonia desktop | New or rewritten Windows/macOS/Linux client; one rendered control system is acceptable | Official F# app and MVVM templates exist; pure core can stay idiomatic | Native launch, DPI/input, OS integration, packaging, signing, install/update on every supported desktop |
| Avalonia cross-platform | A shared Avalonia view layer across desktop plus selected mobile/browser targets is worth platform hosts | Official cross-platform template includes F#; keep hosts thin and state portable | Android/iOS workloads, lifetimes, permissions, device APIs, signing, simulator/device and store evidence |
| WPF or WinUI shell | Product is intentionally Windows-only or depends deeply on existing Windows controls and APIs | A narrow C# UI shell can reference an F# core; direct F# XAML tooling needs its own proof | Supported Windows versions, installer, enterprise deployment, accessibility, Windows-specific integration |
| .NET MAUI shell | Mobile-first product needs MAUI handlers, controls, ecosystem, or native platform integration | Official product and templates are C#/XAML-shaped; an F# core behind a thin shell is the low-friction baseline | Workloads, handler behavior, platform SDKs, devices, signing, stores; direct F# UI requires a separate toolchain spike |
| Fable or another web UI | Browser delivery, URL navigation, web accessibility, and instant updates dominate | F# can own browser state directly; Chapter 41 covers the runtime boundary | Browser/device matrix, offline needs, installability, native bridge, store or wrapper requirements |
| Thin native hosts | Platform conventions, camera/media, background modes, or native controls dominate more than shared UI | Share the F# domain only where the platform interop and AOT story is proven | Each native host, ABI, lifecycle, toolchain, device, signing, and store path |

This is not a framework ranking. Existing expertise and code, accessibility requirements, control vendors, offline behavior, update policy, startup budget, package size, native API depth, and the number of platforms all change the answer.

### Require a vertical slice before commitment {#vertical-slice}

A useful spike contains the hardest real interaction, not a counter alone. Include one domain transition, asynchronous request, cancellation, error and retry, persisted setting, platform service, responsive screen, accessibility traversal, architecture-specific publish, signed or representative package, clean install, upgrade, rollback, and telemetry path.

Record which parts use official F# templates, which examples were translated from C#, which generated code or designers are involved, and which platform must build on a particular OS. The result should support a decision to adopt, constrain, wrap, or reject the toolkit.

## The Avalonia mental model {#avalonia-mental-model}

Avalonia supplies a retained control tree, styling, layout, input routing, data binding, accessibility automation peers, rendering, and platform backends. Its controls are Avalonia controls rather than wrappers around a native control for every platform. That improves visual consistency and shared UI, while platform conventions and native integrations still require deliberate work.

`UsePlatformDetect()` selects an available desktop backend. On Windows, Avalonia uses Win32; on macOS, its own Objective-C++ native backend; on Linux, X11 by default. Avalonia 12.1 offers an experimental opt-in Wayland backend, but `UsePlatformDetect()` does not select it automatically.

### XAML and code-only UI are two construction forms {#xaml-and-coded-ui}

AXAML is compiled by XamlX and creates the same runtime object graph that code can create. XAML offers declarative layout, styles, resources, previews, and familiar designer workflows. Code-only UI keeps construction in the language, can improve refactoring and F# expression flow, and can use community F#-first libraries such as Avalonia.FuncUI. They can be mixed.

Choose from team fluency, tooling, binding needs, styling scale, hot reload or preview requirements, generated-code tolerance, and library maturity. Code-only does not make mutable controls pure. XAML does not require domain logic in a view model. X43 uses AXAML plus a tiny F# code-behind because that exposes the boundary without another framework.

## X43: one verified desktop slice {#verified-slice}

X43 is deliberately one `net10.0` desktop executable. It has five primary files, no mobile target framework, no platform workload, no MVVM dependency, and no packaging configuration.

### A pinned ordinary .NET project {#pinned-project}

<<< @/../examples/ecosystem/avalonia/AvaloniaSample.fsproj{xml:line-numbers} [AvaloniaSample.fsproj]

`Avalonia`, `Avalonia.Desktop`, and `Avalonia.Themes.Fluent` are pinned to 12.1.1 and resolved through a lock file. The repository also locks FSharp.Core 10.1.301. `WinExe` selects a graphical executable; `net10.0` remains a generic desktop target rather than `net10.0-macos` or `net10.0-windows`.

The explicit F# compile order matters: `MainWindow.fs` defines types consumed by `Program.fs`. AXAML files are processed by Avalonia build targets and are not F# compile items.

### A pure transition owns the decision {#pure-transition}

<<< @/../examples/ecosystem/avalonia/MainWindow.fs{fsharp:line-numbers} [MainWindow.fs]

`Model`, `Message`, and `Counter.update` do not know about buttons, dispatchers, windows, or Avalonia. `RemoveSeat` enforces the lower bound. The view holds the current model only because this sample is intentionally local and ephemeral; a real workflow would decide separately what must survive navigation, suspension, restart, or upgrade.

The window loads AXAML, obtains named controls, turns clicks into messages, calls the pure update, and renders the result. This is a small manual model-view-update loop. It is not a claim that all UI effects should fit in one constructor.

### Markup owns shape, not business rules {#markup-shape}

<<< @/../examples/ecosystem/avalonia/MainWindow.axaml{xml:line-numbers} [MainWindow.axaml]

The markup owns layout, control identity, labels, and initial visual values. Text buttons already expose useful accessible names through their content. A production screen would add stable automation IDs, explicit labels where visible text is ambiguous, localized resources, keyboard behavior, contrast checks, and tests at large text and narrow widths.

`GetControl<T>` intentionally fails if a required name is absent. The string passed to `GetControl` is not a typed binding path, so a successful XAML compilation does not prove every lookup. A headless or native construction test closes that gap.

### The host chooses a desktop lifetime {#desktop-lifetime}

<<< @/../examples/ecosystem/avalonia/Program.fs{fsharp:line-numbers} [Program.fs]

`BuildAvaloniaApp` mirrors the official template seam used by tooling and startup. `StartWithClassicDesktopLifetime` creates an `IClassicDesktopStyleApplicationLifetime`; only after framework initialization does `App` assign `MainWindow`. The entry point has `STAThread`, which is relevant to Windows APIs such as COM and the clipboard.

Desktop assumptions are explicit here. iOS and browser use `ISingleViewApplicationLifetime`; Android uses `IActivityApplicationLifetime` with a view factory because activities can be recreated. Reusing this `MainWindow` startup path on mobile would be a design error, not a missing compiler switch.

### The focused test does not start a UI {#focused-test}

The repository's xUnit suite references the sample and checks three additions, reset, lower-bound removal, and the unchanged initial value. It runs without Avalonia initialization because the tested function has no toolkit dependency. That speed and determinism are the payoff of the boundary.

The final focused run passed 1/1; the current complete example suite passed 70/70. The sample's Release build under .NET SDK 10.0.301 completed with zero warnings and zero errors, and the full locked example gate—including the other .NET projects and Fable browser smoke—passed.

### State the native launch result exactly {#native-launch-result}

| Evidence | X43 result | What it proves | What it does not prove |
| --- | --- | --- | --- |
| Locked restore | Passed | The recorded NuGet graph resolves | Future versions or other runtime graphs |
| Release build | Passed, 0 warnings/errors | F# and AXAML compile for `net10.0` | A usable native window or package |
| Pure transition test | Passed | The checked state transitions | Control lookup, layout, input, rendering |
| Full example gate | Passed | Repository integration remains reproducible | Desktop/mobile platform behavior |
| macOS native start | Attempted; failed before a window with Avalonia.Native RenderTimer error `-6661` | The process reached the native macOS backend and exposed the automation session's missing graphical display context | A displayed window, user interaction, or a defect in the application logic |
| Windows/Linux/mobile/package/store | Not run | Nothing | Nothing |

The failed launch is useful evidence because it prevents an inflated claim. It should be rerun in an unlocked interactive desktop session. There is no justification for changing the pure model, suppressing the exception, or claiming macOS success merely because compilation passed.

## Choose a state pattern deliberately {#state-patterns}

F# offers several useful boundaries; toolkit choice does not dictate one architecture.

| Pattern | State and decisions | View connection | Main pressure |
| --- | --- | --- | --- |
| Manual MVU | Immutable model plus `Msg -> Model -> Model * Effect` functions | Event handlers dispatch; renderer updates controls | Renderer and effect scheduling become repetitive as screens grow |
| MVVM adapter | Domain and presentation core remain functional; adapter exposes properties, notifications, and commands | AXAML bindings connect to adapter | Mutable notification surface, command lifetime, binding-friendly type shapes |
| Code-behind orchestration | Small local state and event handlers in the view, domain calls delegated outward | Direct control references | Easy to let business decisions, I/O, and cancellation accumulate in the window |
| Code-only/FuncUI-style | UI tree expressed in F# and commonly driven by messages/state | Language-level combinators or DSL | Community dependency, API churn, tooling and performance need their own evaluation |

X43 uses manual MVU at the smallest useful scale: one pure update and one imperative renderer. A larger application would normally separate model, update, effect descriptions, view adapters, navigation, and composition into modules or projects.

### Effects are messages, not hidden branches {#effects-as-messages}

For asynchronous work, let the pure transition describe what should happen and receive the outcome as another message:

```fsharp
type LoadState<'value> =
    | Idle
    | Loading of requestId: Guid
    | Loaded of 'value
    | Failed of message: string

type Msg<'value> =
    | LoadRequested
    | LoadCompleted of requestId: Guid * Result<'value, string>
    | LoadCancelled of requestId: Guid
```

An effect runner owns HTTP, files, time, cancellation, and dispatch. The request identifier lets `update` reject a late result from an older screen or search. Do not hide fire-and-forget work inside a property setter or click handler where failure and staleness have no modeled destination.

## F# at the XAML and binding boundary {#fsharp-xaml-boundary}

Avalonia has official F# app, MVVM, and cross-platform templates. That is meaningful: project layout, startup, AXAML code-behind, and template generation are tested paths. It does not mean every documentation sample, designer, source generator, third-party control, or MVVM package has equally polished F# ergonomics.

### Match classes and respect file order {#classes-and-file-order}

The AXAML `x:Class` must match the namespace and type loaded in F#. F# source order must place a definition before consumers. Generated partial-class conventions common in C# do not erase those rules. Keep startup and view code small enough that failures remain attributable to AXAML compilation, F# compilation, binding, or native startup.

Named lookup and event hookup are simple for small views. Binding scales better for repeated presentation state, templates, validation, and commands, but it adds a public object-shaped interface. Treat that interface like any other API.

### Avalonia 12 makes bindings compiled by default {#compiled-bindings}

In Avalonia 12, ordinary `{Binding ...}` maps to compiled binding by default. A compiled binding needs an `x:DataType`; the XAML compiler can then reject missing paths and incompatible types. Use `{ReflectionBinding ...}` only for an intentionally dynamic value, not as a blanket escape from type errors.

X43 contains no binding expression, so it does not need an `x:DataType` and proves nothing about a view-model binding surface. A real binding spike should include nested templates, two-way editing, commands, validation, design data, trimming or AOT if planned, and the actual IDE used by the team.

### Adapt functional types instead of weakening them {#binding-adapters}

- An immutable F# record exposes readable .NET properties but does not automatically raise `INotifyPropertyChanged`; replace and re-render it, or wrap it in a notifying adapter.
- A discriminated union is excellent presentation state, but XAML often needs derived properties such as `IsBusy`, `ErrorText`, or a selected template; compute those at the adapter edge.
- Convert `option` and `Result` intentionally into visibility, nullable payload, error text, or validation state; do not let null conventions silently define domain meaning.
- Immutable lists are good model values. Use a new `ItemsSource` snapshot for modest collections, or an observable adapter when incremental updates materially matter.
- Commands are an effect boundary. Give them explicit can-execute rules, cancellation, error routing, and lifetime rather than placing network calls in anonymous setters.
- Keep toolkit types out of the reusable domain unless the coupling is an explicit product decision.

An adapter is not a betrayal of functional design. It prevents the binding engine's mutation, reflection, null, and notification conventions from leaking inward.

## Threading, cancellation, and lifetime {#threading-and-lifetime}

Avalonia uses a single UI thread. Control creation, property access, layout, rendering, and input belong to its dispatcher. `Dispatcher.UIThread.Post` schedules work without waiting; `InvokeAsync` lets a caller await completion. Avalonia 12 can have multiple dispatchers on different threads, although multiple UI threads remain unsupported; reusable control code should prefer the control's dispatcher when appropriate.

Do not use `Task.Run` for naturally asynchronous I/O merely to avoid the UI thread. Await the I/O, keep CPU-heavy work off the dispatcher, and deliver a small result message back to the view. Never block with `.Result`, `.Wait()`, or long synchronous file/database work in an event handler.

Tie each operation to a lifetime: window, view, navigation entry, application, or durable background job. Cancel best effort when that lifetime ends, but still reject stale completions because cancellation races. Route every exception into a modeled failure, logging boundary, or supervised task; an unobserved fire-and-forget exception is not user feedback.

### Desktop and mobile do not share one lifetime {#platform-lifetimes}

Desktop applications can have multiple windows and shutdown policies. iOS and browser expose one main view. Android may recreate activities, so Avalonia asks for a factory that creates a fresh view. The operating system may suspend or terminate a mobile process after it leaves the foreground.

Therefore, do not store irreplaceable work only in a `Window`, control tree, view model instance, or static singleton. Persist drafts and identifiers at deliberate checkpoints. Rehydrate from durable state. Make navigation and resume explicit inputs to the presentation state machine.

## Put platform services behind ports {#platform-services}

Common .NET libraries cover networking, serialization, cryptography primitives, and much storage logic. UI platforms still differ in dialogs, clipboard, notifications, secure storage, file pickers, deep links, camera, biometrics, sharing, background execution, menus, tray icons, and permission prompts.

Define capability-oriented ports in the shared project:

```fsharp
type PickDocument = CancellationToken -> Task<Result<string option, string>>
type SaveDraft = Draft -> CancellationToken -> Task<Result<unit, string>>
type OpenExternalUri = Uri -> Task<Result<unit, string>>
```

Implement them in the platform host and inject them during composition. Model “cancelled,” “unavailable,” “permission denied,” and “failed” separately where the user needs different recovery. Avoid a global `if OperatingSystem.Is...` forest and avoid exposing Android, UIKit, Win32, or Avalonia objects through the core interface.

`OnPlatform` and `OnFormFactor` are useful for small resource or layout differences. They are not substitutes for a platform service when behavior, permissions, or lifecycle changes.

## Desktop is already three platform programs {#desktop-platforms}

Avalonia's one desktop project can target Windows, macOS, and Linux, but each backend and distribution system remains distinct. Support tiers and minimum OS versions change; the repository checked the official matrix on 2026-08-25 and should recheck it before release.

### Windows {#windows}

Avalonia uses Win32 directly and requires no separate Windows .NET workload for its generic desktop target. A release still chooses `win-x64`, `win-arm64`, or another supported RID; framework-dependent versus self-contained delivery; installer technology; application identity; icons; file associations; signing; update policy; and enterprise deployment behavior.

Test keyboard and high-DPI behavior, multiple monitors, clipboard and dialogs, remote sessions if supported, Windows accessibility, clean install, per-user/per-machine data, upgrade, repair, and uninstall. A package built on macOS is cross-compilation evidence, not Windows runtime evidence.

### macOS {#macos}

The default Avalonia macOS backend ships its own native library and can be built without the `net10.0-macos` workload. Distribution still needs a correctly structured `.app` bundle and `Info.plist`; normal external distribution requires code signing and notarization, and those signing steps require macOS/Xcode tooling even when bundle construction was cross-platform.

Publish and test Apple Silicon and Intel artifacts when both are supported. Verify native menus, shortcuts, file dialogs, sandbox or entitlement choices, accessibility, app identity, quarantine/Gatekeeper behavior, upgrade, and uninstall. X43's `-6661` launch result is specifically not a passed macOS smoke test.

### Linux {#linux}

Avalonia targets X11 by default; Wayland normally uses XWayland unless the experimental native Wayland backend is explicitly selected. Linux release scope must name distributions, versions, CPU architectures, display backends, GPU/software rendering paths, desktop environments, native libraries, fonts, packaging formats, and update channels.

A `.deb` built successfully does not prove an RPM, Flatpak, Snap, AppImage, or unpacked archive. Test clean supported images, desktop entries, icons, executable permissions, native dependency failures, locale/font behavior, accessibility through AT-SPI, installation, upgrade, and removal.

## Mobile support is a project graph, not a checkbox {#mobile-boundary}

The official Avalonia cross-platform structure contains a shared Core project plus separate Desktop, Android, iOS, and optionally Browser hosts. Views and presentation logic can live in Core; each host supplies its target framework, entry point, SDK integration, metadata, permissions, native services, signing, and deployment path.

As of the checked support matrix, Avalonia mobile targets require .NET 10. Android and iOS support follow the .NET MAUI platform lifecycle. Those are dated constraints, so pin the SDK/workload set and review the matrix for every release train.

### Android {#android}

An Android host is a .NET Android project with a `MainActivity` derived from Avalonia's activity base. Building it requires the .NET Android workload, Android SDK, JDK, and matching target components. Runtime evidence needs an emulator and representative physical devices, not merely a shared project build.

Test activity recreation, back navigation, configuration changes, process death, permissions, deep links, keyboard and insets, touch, accessibility, offline behavior, background restrictions, package identity, architecture splits, signing, upgrade, and store policy. State that must survive belongs outside the activity instance.

### iOS and iPadOS {#ios}

An iOS host is a .NET iOS project with an Avalonia app delegate and current scene-based initialization. The toolchain needs the iOS workload; running and device validation require macOS hardware with Xcode. Physical devices add certificates and provisioning, while store distribution adds signed archives, App Store metadata, review, and update constraints.

Test simulator and device paths, foreground/background transitions, memory pressure, permissions, safe areas, rotation, keyboard, touch and pointer where applicable, VoiceOver, deep links, offline recovery, package identity, signing, upgrade, and store delivery. Mac Catalyst is a distinct UIKit-based option, not the same backend as the default Avalonia macOS desktop target.

### F# support needs two statements {#fsharp-support-boundary}

First, Avalonia's official templates list F# for desktop, MVVM, and cross-platform solutions. Second, the surrounding mobile SDKs, most native examples, store tooling, and many third-party libraries remain C#-shaped. Both can be true.

Prefer an idiomatic F# shared core and the thinnest host that the tested toolchain supports. A small C# platform adapter is often cheaper than forcing generated or designer conventions through F#. If direct F# hosts work in the chosen template and IDE, keep them—but preserve a reproducible command-line build and device proof.

.NET MAUI is a separate UI product targeting Android, iOS, Mac Catalyst, and Windows. Its official documentation and current template source are C#/XAML-oriented. That does not prevent an F# library from powering a MAUI app, nor prove a community F# template unsuitable; it means direct F# MAUI UI work is a separate adoption decision rather than evidence for Avalonia.

## Accessibility, input, and responsive layout are behavior {#accessible-responsive-ui}

Avalonia's built-in controls expose automation peers to platform accessibility APIs. Use semantic controls first. Supply `AutomationProperties.Name`, `LabeledBy`, `HelpText`, live settings, or stable `AutomationId` when visible content is insufficient. Custom controls need deliberate automation behavior.

Test keyboard traversal, focus order and restoration, shortcuts, screen readers, contrast, large text, zoom or scale, high DPI, reduced motion where relevant, error announcements, and input without a pointer. Color alone must not carry meaning.

Responsive design is more than detecting “mobile.” Let layouts react to available size; use form-factor or platform conditions only when the interaction truly differs. Test long English strings, compact Chinese labels, localization expansion, right-to-left text if supported, narrow windows, touch targets, software keyboards, safe areas, rotation, and resizable desktop windows.

Accessibility and localization defects are platform defects even when the shared AXAML is identical. Include representative assistive technologies and input devices in the platform matrix.

## Build an evidence ladder {#testing-evidence-ladder}

Use the cheapest useful layer first, but do not stop there:

1. **Pure tests:** update, validation, navigation decisions, stale-result rejection, formatting inputs, and effect descriptions.
2. **Adapter tests:** notifications, commands, collection deltas, validation projection, cancellation, and platform-port fakes.
3. **XAML/compile tests:** resources, classes, compiled binding paths, templates, and targeted framework graphs.
4. **Headless Avalonia tests:** construct real controls, apply styles and layout, simulate input, inspect the visual or automation tree, and optionally compare images.
5. **Native debug smoke:** start an unlocked native backend, exercise keyboard/pointer/touch, dialogs, clipboard, scaling, and shutdown.
6. **Publish and package tests:** produce each RID, inspect native assets and metadata, sign, install on a clean target, launch outside the SDK, upgrade, rollback, and uninstall.
7. **Device and store tests:** permissions, suspend/resume, process death, deep links, network loss, accessibility, performance, signing, staged distribution, crash reports, and update behavior.

Headless testing is valuable in CI, but it replaces the native windowing and rendering backend. It cannot certify Win32, macOS, X11/Wayland, Android, iOS, drivers, packaging, signing, or store behavior.

Keep an evidence table keyed by OS version, CPU, package, locale, scale, input, assistive technology, test date, commit, and result. “Works on my machine” becomes useful only after “machine” is named.

## Publishing is not packaging or release {#publishing-and-release}

`dotnet build` compiles. `dotnet publish` creates a deployment layout. Packaging adds platform structure and metadata. Signing establishes publisher identity and integrity. Distribution moves the artifact through an installer, repository, MDM system, or store. Release adds rollout, observation, support, update, and rollback.

### Choose runtime delivery per target {#runtime-delivery}

A framework-dependent publish is smaller and uses a compatible installed .NET runtime, including its serviced patches. A self-contained publish carries the selected runtime and must be rebuilt to take runtime security updates. A runtime identifier selects OS and architecture; since .NET 8, a RID alone no longer implies self-contained delivery, so state the choice explicitly.

Single-file, trimming, ReadyToRun, and Native AOT change size, startup, reflection, diagnostics, and compatibility. Avalonia's AOT guidance requires compiled bindings. Enable these only with warnings clean and with runtime tests over bindings, serialization, dependency injection, plugins, resources, native libraries, and every packaged architecture.

Do not overwrite one shared lock graph accidentally while publishing multiple RIDs. Use explicit restore inputs or isolated output/lock strategy, and prove that the committed dependency graph remains unchanged.

### Design install, update, and recovery {#install-update-recovery}

Define application ID, version semantics, data directories, settings schema, cache policy, logs, crash reports, file associations, protocol handlers, certificates, entitlements, and update channel. Never assume the working directory is writable or stable. Keep secrets out of packages and logs; use platform credential storage where appropriate.

An update can change both executable and user data. Use backward-compatible settings/data migrations, atomic writes, backups for irreplaceable local data, downgrade policy, and a recovery path when startup fails. Test clean install, update from every supported predecessor, interrupted update, rollback or forward fix, and uninstall with an explicit user-data retention policy.

## Run a bounded adoption spike {#adoption-spike}

For a serious client, time-box one representative vertical slice and measure:

- official F# template creation, locked restore, CLI build, IDE edit/preview, and debugger behavior;
- one immutable domain workflow through the chosen state pattern and binding or renderer;
- one virtualized list or otherwise demanding real screen at representative data volume;
- asynchronous cancellation, stale completion, offline error, retry, and restart recovery;
- one platform service with denied permission and cancellation paths;
- keyboard, touch, focus, screen reader, large text, localization, and narrow layout;
- startup, interaction latency, memory, package size, and crash diagnostics on target hardware;
- per-RID publish, clean installation, signing or representative signing, upgrade, and uninstall;
- mobile lifecycle and physical-device behavior if mobile is in scope;
- dependency maintenance, licensing, support policy, control ecosystem, and an exit condition.

Compare implementation and operational cost, not screenshot similarity. A framework is acceptable only if the team can build, diagnose, distribute, update, and support it within the product's actual platform matrix.

## Avoid common UI mistakes {#common-mistakes}

- Treating a successful shared-project build as Windows, macOS, Linux, Android, and iOS validation.
- Choosing a framework before naming required platforms, native APIs, input modes, stores, and update policy.
- Letting business decisions, HTTP, files, and cancellation accumulate in window code-behind.
- Calling a mutable view model “the model” and losing the immutable domain state beneath it.
- Exposing discriminated unions or options directly to XAML without a deliberate adapter contract.
- Disabling compiled binding globally to silence an `x:DataType` or public-shape error.
- Blocking the UI dispatcher or starting unsupervised fire-and-forget tasks from event handlers.
- Accepting a late asynchronous result after navigation or a newer request.
- Storing irreplaceable state only in a window, activity, control, singleton, cache, or working directory.
- Scattering operating-system checks through shared logic instead of injecting platform capabilities.
- Assuming identical controls imply identical lifecycle, accessibility, fonts, input, or native services.
- Designing a fixed desktop canvas and calling it mobile support.
- Using custom clickable visuals without keyboard, focus, semantics, or automation peers.
- Testing only headless and claiming a native rendering or packaging result.
- Publishing one architecture, mutable dependency graph, or unsigned folder and calling it a release.
- Enabling trimming, single-file, or AOT from a size goal without testing reflection and native assets.
- Forgetting settings/data migration, interrupted update, rollback, uninstall, and user-data policy.
- Forcing every platform host to be F# when a tiny C# adapter would reduce toolchain risk.
- Treating one failed graphical automation launch as proof that the application logic is defective.
- Treating one local successful launch as proof that installers, stores, and supported OS versions work.

## Exercises {#exercises}

### Exercise 1: choose three UI boundaries {#exercise-01}

Choose a first candidate, rejected alternatives, evidence gap, and reversal condition for each product: (a) a Windows-only trading workstation must reuse mature WPF controls and enterprise deployment; the domain calculations are new F#; (b) an offline field tool needs Windows, macOS, and two named Linux distributions, keyboard and touch, local documents, and no phone release; (c) a consumer app needs Android and iOS, camera, push notifications, deep links, background upload, store distribution, and a small companion desktop viewer. Compare Avalonia, a C# platform shell around an F# core, .NET MAUI, and a browser surface without forcing one answer across all products.

### Exercise 2: turn X43 into a desktop release {#exercise-02}

Design the minimum changes and evidence needed to turn X43 into a supported Windows/macOS/Linux application. Cover module boundaries, asynchronous effects, persistence, settings migration, accessibility, localization, headless tests, native smoke, runtime identifiers, framework-dependent versus self-contained delivery, native assets, packages, signing/notarization, clean install, update, rollback, crash diagnostics, and the exact platform matrix. Preserve the honest limit of the existing `-6661` launch result.

### Exercise 3: extend the architecture to mobile {#exercise-03}

Design a Core/Desktop/Android/iOS project graph for a booking client. The shared screen edits a draft, submits it, survives rotation or activity recreation, resumes after process termination, opens a confirmation deep link, and exports a receipt through a platform picker. Define F# state/messages/effects, platform ports, lifetime ownership, persistence checkpoints, permission outcomes, stale-result protection, host language choices, workload locks, simulator/device tests, signing, staged store release, telemetry, and reversal criteria. State what a desktop build proves about the mobile targets.

[Read the chapter solutions](../solutions/ch-43-avalonia-desktop-mobile).

## Chapter review {#chapter-review}

- A client is a stack of domain, presentation, toolkit, host, and distribution contracts.
- Measure shared logic, shared UI, and shared evidence separately.
- Choose a UI boundary from users, devices, native capabilities, team skills, and release channels.
- Avalonia supplies official F# templates, compiled AXAML, shared controls, and multiple platform hosts; it does not erase platform behavior.
- X43 pins Avalonia 12.1.1 and separates a pure `Counter.update` from an imperative desktop view.
- Its locked restore, build, tests, and repository gate pass; its automated macOS native launch did not, so native success is not claimed.
- Manual MVU, MVVM adapters, code-behind, and code-only UI are choices with different pressure points.
- Avalonia 12 bindings are compiled by default and require an explicit data type; reflection binding is an intentional exception.
- Adapt immutable records, unions, options, collections, and commands at the UI boundary rather than weakening the domain.
- Controls belong to a dispatcher; model asynchronous outcomes and cancellation as messages and reject stale results.
- Desktop windows, iOS/browser single views, and Android activity factories have different lifetimes.
- Put native capabilities behind ports and keep platform objects out of the reusable core.
- One desktop project still needs separate Windows, macOS, and Linux runtime and package evidence.
- Mobile requires platform projects, .NET 10 workloads, SDKs, permissions, signing, devices, and stores.
- Accessibility, responsive layout, localization, keyboard, touch, and lifecycle are behavior, not polish.
- Climb from pure tests through headless controls, native smoke, packages, devices, and store evidence.
- Publish, package, sign, distribute, update, observe, and recover are separate release stages.

Chapter 44 crosses another host boundary: using F# domain code inside Unity while keeping Unity serialization, component lifecycles, IL2CPP, and player builds in an explicit adapter layer.
