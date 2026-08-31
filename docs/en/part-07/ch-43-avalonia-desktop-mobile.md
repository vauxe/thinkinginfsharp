---
title: "Chapter 43: Avalonia, Desktop, and Mobile"
description: "Design F# user interfaces from state, lifetime, platform, tooling, packaging, and evidence boundaries instead of treating cross-platform compilation as cross-platform validation."
translationKey: part-07/ch-43-avalonia-desktop-mobile
---

# Chapter 43: Avalonia, Desktop, and Mobile {#overview}

An F# user interface is not “some controls around the real program.” It is a long-lived boundary where input, time, cancellation, mutable platform objects, accessibility, operating-system services, and release mechanics meet. F# helps most when the application turns those events into explicit data and keeps decisions testable before a window exists.

Avalonia is a cross-platform .NET UI framework with official F# templates. It draws its own controls and provides desktop, mobile, and browser hosts, but that does not make every platform identical. A shared view can compile while its font, input, lifecycle, permission, native integration, package, signing, or accessibility path fails on one target. “Cross-platform” describes an architecture and support surface; it is not a test result.

Start with product and platform constraints, not XAML syntax. An in-page desktop project template then explains the file relationships before state patterns, binding boundaries, threading, platform services, mobile hosts, testing, packaging, and release checks.

This chapter mixes several vocabularies: records, discriminated unions, pattern matching, modules, and functions are F# language concepts; AXAML, controls, binding, and `AppBuilder` belong to Avalonia; MVU and MVVM are UI architecture patterns; RIDs, AOT, signing, and app stores belong to .NET or platform delivery. The latter three groups are not “standard F# terms.”

::: tip Two reading passes
For a first pass, follow the [UI stack](#ui-stack-contracts), [decision map](#decision-map), and [in-page desktop template](#verified-slice). When implementing or adopting a toolkit, return to the sections on state, binding, lifetime, platforms, verification, and release as needed.
:::

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

Verify each layer at its own boundary. Pure transition tests check presentation logic, while a XAML build checks declared names and markup. Native launches cover rendering and interaction; package, upgrade, and accessibility tests cover the release path.

### Shared is not the same as identical {#shared-not-identical}

Use three separate percentages when discussing reuse:

1. **Shared logic:** domain rules, validation, network contracts, persistence abstractions, and presentation transitions.
2. **Shared UI:** views, styles, resources, navigation concepts, and toolkit-specific adapters.
3. **Verified coverage:** tests and observations that actually ran on each supported OS, architecture, input mode, and release channel.

The first can be high while the second is low for a native-feeling mobile product. The first two can be high while the third remains near zero before device and packaging tests. Reuse is valuable, but an honest denominator is more valuable than a large percentage.

## Choose from the product boundary {#decision-map}

Start with the users and release constraints:

| First candidate | Strong fit | F# boundary | Verification still required |
| --- | --- | --- | --- |
| Avalonia desktop | New or rewritten Windows/macOS/Linux client; one rendered control system is acceptable | Official F# app and MVVM templates exist; pure core can stay idiomatic | Native launch, DPI/input, OS integration, packaging, signing, install/update on every supported desktop |
| Avalonia cross-platform | A shared Avalonia view layer across desktop plus selected mobile/browser targets justifies maintaining platform hosts | Official cross-platform template includes F#; keep hosts thin and state portable | Android/iOS workloads, lifetimes, permissions, device APIs, signing, simulator/device tests, and store validation |
| WPF or WinUI shell | Product is intentionally Windows-only or depends deeply on existing Windows controls and APIs | A narrow C# UI shell can reference an F# core; direct F# XAML tooling needs separate validation | Supported Windows versions, installer, enterprise deployment, accessibility, Windows-specific integration |
| .NET MAUI shell | Mobile-first product needs MAUI handlers, controls, ecosystem, or native platform integration | Official product and templates are C#/XAML-shaped; an F# core behind a thin shell is the low-friction baseline | Workloads, handler behavior, platform SDKs, devices, signing, stores; direct F# UI requires a separate toolchain spike |
| Fable or another web UI | Browser delivery, URL navigation, web accessibility, and instant updates dominate | F# can own browser state directly; Chapter 41 covers the runtime boundary | Browser/device matrix, offline needs, installability, native bridge, store or wrapper requirements |
| Thin native hosts | Platform conventions, camera/media, background modes, or native controls dominate more than shared UI | Share the F# domain only after validating platform interop and AOT | Each native host, ABI, lifecycle, toolchain, device, signing, and store path |

This is not a framework ranking. Existing expertise and code, accessibility requirements, control vendors, offline behavior, update policy, startup budget, package size, native API depth, and the number of platforms all change the answer.

### Require a vertical slice before commitment {#vertical-slice}

A useful spike contains the hardest real interaction, not a counter alone. Include one domain transition, asynchronous request, cancellation, error and retry, persisted setting, platform service, responsive screen, accessibility traversal, architecture-specific publish, signed or representative package, clean install, upgrade, rollback, and telemetry path.

Record which parts use official F# templates, which examples were translated from C#, which generated code or designers are involved, and which platform must build on a particular OS. The result should support a decision to adopt, constrain, wrap, or reject the toolkit.

## The Avalonia mental model {#avalonia-mental-model}

Avalonia supplies a retained control tree, styling, layout, input routing, data binding, accessibility automation peers, rendering, and platform backends. Its controls are Avalonia controls rather than wrappers around a native control for every platform. That improves visual consistency and shared UI, while platform conventions and native integrations still require deliberate work.

`UsePlatformDetect()` selects an available desktop backend. On Windows, Avalonia uses Win32; on macOS, its own Objective-C++ native backend; on Linux, X11 by default. Avalonia 12.1 offers an experimental opt-in Wayland backend, but `UsePlatformDetect()` does not select it automatically.

### XAML and code-only UI are two construction forms {#xaml-and-coded-ui}

AXAML is compiled by XamlX and creates the same runtime object graph that code can create. XAML offers declarative layout, styles, resources, previews, and familiar designer workflows. Code-only UI keeps construction in the language, can improve refactoring and F# expression flow, and can use community F#-first libraries such as Avalonia.FuncUI. They can be mixed.

Choose from team fluency, tooling, binding needs, styling scale, hot reload or preview requirements, generated-code tolerance, and library maturity. Code-only does not make mutable controls pure. XAML does not require domain logic in a view model. The in-page template uses AXAML plus a tiny F# code-behind because that exposes the boundary without another framework.

## In-page project template: one minimal desktop application {#verified-slice}

The current repository no longer contains the former `examples/ecosystem/avalonia` project. This section preserves a reconstructable template with `AvaloniaSample.fsproj`, `App.axaml`, `MainWindow.axaml`, `MainWindow.fs`, and `Program.fs` in one directory. Mobile target frameworks, platform workloads, MVVM infrastructure, and packaging are not part of the template.

Read all five files together: the project file defines F# compile order; the two AXAML files declare application resources and the window; `MainWindow.fs` implements the window class; and `Program.fs` defines `App` and the desktop entry point. The blocks below are the complete minimal contents of those files, not fragments to paste independently into F# Interactive.

### A pinned ordinary .NET project {#pinned-project}

```xml:line-numbers [AvaloniaSample.fsproj]
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <AssemblyName>ThinkingInFSharp.AvaloniaSample</AssemblyName>
    <RootNamespace>ThinkingInFSharp.AvaloniaSample</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <Compile Include="MainWindow.fs" />
    <Compile Include="Program.fs" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Avalonia" Version="12.1.1" />
    <PackageReference Include="Avalonia.Desktop" Version="12.1.1" />
    <PackageReference Include="Avalonia.Themes.Fluent" Version="12.1.1" />
  </ItemGroup>
</Project>
```
`Avalonia`, `Avalonia.Desktop`, and `Avalonia.Themes.Fluent` are pinned to 12.1.1. After reconstruction, the first restore should generate a lock and a locked-mode restore should verify it. The project has no explicit `FSharp.Core` reference; its resolved version comes from the selected SDK and graph, so inspect the lock rather than guessing from prose. `WinExe` selects a graphical executable; `net10.0` remains a generic desktop target rather than `net10.0-macos` or `net10.0-windows`.

The explicit F# compile order matters: `MainWindow.fs` defines types consumed by `Program.fs`. AXAML files are processed by Avalonia build targets and are not F# compile items.

### A pure transition owns the decision {#pure-transition}

```fsharp:line-numbers [MainWindow.fs]
namespace ThinkingInFSharp.AvaloniaSample

open Avalonia.Controls
open Avalonia.Markup.Xaml

type Model = { Seats: int }

type Message =
    | AddSeat
    | RemoveSeat
    | Reset

[<RequireQualifiedAccess>]
module Counter =
    let initial = { Seats = 0 }

    let update message model =
        match message with
        | AddSeat -> { model with Seats = model.Seats + 1 }
        | RemoveSeat ->
            { model with
                Seats = max 0 (model.Seats - 1) }
        | Reset -> initial

type MainWindow() as this =
    inherit Window()

    do
        AvaloniaXamlLoader.Load(this)

        let countText = this.GetControl<TextBlock>("CountText")
        let statusText = this.GetControl<TextBlock>("StatusText")
        let removeButton = this.GetControl<Button>("RemoveButton")
        let mutable model = Counter.initial

        let render state =
            countText.Text <- string state.Seats

            statusText.Text <-
                if state.Seats = 0 then "No seats selected"
                elif state.Seats = 1 then "1 seat selected"
                else $"{state.Seats} seats selected"

            removeButton.IsEnabled <- state.Seats > 0

        let dispatch message =
            model <- Counter.update message model
            render model

        this.GetControl<Button>("AddButton").Click.Add(fun _ -> dispatch AddSeat)
        removeButton.Click.Add(fun _ -> dispatch RemoveSeat)
        this.GetControl<Button>("ResetButton").Click.Add(fun _ -> dispatch Reset)
        render model
```
`Model`, `Message`, and `Counter.update` do not know about buttons, dispatchers, windows, or Avalonia. `RemoveSeat` enforces the lower bound. The view holds the current model only because this sample is intentionally local and ephemeral; a real workflow would decide separately what must survive navigation, suspension, restart, or upgrade.

The window loads AXAML, obtains named controls, turns clicks into messages, calls the pure update, and renders the result. This is a small manual model-view-update loop. It is not a claim that all UI effects should fit in one constructor.

### Markup defines structure, not business rules {#markup-shape}

```xml:line-numbers [MainWindow.axaml]
<Window
    xmlns="https://github.com/avaloniaui"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    x:Class="ThinkingInFSharp.AvaloniaSample.MainWindow"
    Title="Thinking in F# — Avalonia"
    Width="520"
    Height="400"
    MinWidth="420"
    MinHeight="340"
    WindowStartupLocation="CenterScreen">
  <Grid RowDefinitions="Auto,*,Auto" Margin="32">
    <StackPanel Grid.Row="0" Spacing="6">
      <TextBlock FontSize="13" FontWeight="SemiBold" Text="THINKING IN F#" />
      <TextBlock FontSize="28" FontWeight="Bold" Text="Pure update, thin view" />
      <TextBlock Opacity="0.72" Text="Avalonia owns the window; F# owns the state transition." />
    </StackPanel>

    <Border Grid.Row="1" Margin="0,24" Padding="24" CornerRadius="16" BorderThickness="1">
      <StackPanel HorizontalAlignment="Center" VerticalAlignment="Center" Spacing="8">
        <TextBlock HorizontalAlignment="Center" Opacity="0.72" Text="Seats requested" />
        <TextBlock
            x:Name="CountText"
            HorizontalAlignment="Center"
            FontSize="64"
            FontWeight="Bold"
            Text="0" />
        <TextBlock x:Name="StatusText" HorizontalAlignment="Center" Text="No seats selected" />
      </StackPanel>
    </Border>

    <StackPanel Grid.Row="2" HorizontalAlignment="Center" Orientation="Horizontal" Spacing="12">
      <Button x:Name="RemoveButton" MinWidth="100" HorizontalContentAlignment="Center" Content="Remove" />
      <Button x:Name="ResetButton" MinWidth="100" HorizontalContentAlignment="Center" Content="Reset" />
      <Button x:Name="AddButton" MinWidth="100" HorizontalContentAlignment="Center" Content="Add a seat" />
    </StackPanel>
  </Grid>
</Window>
```
Markup defines layout, control identity, labels, and initial visual values. Text buttons already expose useful accessible names through their content. A production screen would add stable automation IDs, explicit labels where visible text is ambiguous, localized resources, keyboard behavior, contrast checks, and tests at large text and narrow widths.

`GetControl<T>` intentionally fails if a required name is absent. The string passed to `GetControl` is not a typed binding path, so a successful XAML compilation does not prove every lookup. A headless or native construction test closes that gap.

### The host chooses a desktop lifetime {#desktop-lifetime}

`App.Initialize()` in `Program.fs` loads `App.axaml`. Without this file, the `FluentTheme` is not registered; that is a runtime prerequisite the preceding four blocks cannot express by themselves:

```xml:line-numbers [App.axaml]
<Application
    xmlns="https://github.com/avaloniaui"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    x:Class="ThinkingInFSharp.AvaloniaSample.App"
    RequestedThemeVariant="Default">
  <Application.Styles>
    <FluentTheme />
  </Application.Styles>
</Application>
```

Its `x:Class` must exactly match the F# `App` type below:

```fsharp:line-numbers [Program.fs]
namespace ThinkingInFSharp.AvaloniaSample

open System
open Avalonia
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Markup.Xaml

type App() =
    inherit Application()

    override this.Initialize() = AvaloniaXamlLoader.Load(this)

    override this.OnFrameworkInitializationCompleted() =
        match this.ApplicationLifetime with
        | :? IClassicDesktopStyleApplicationLifetime as desktop -> desktop.MainWindow <- MainWindow()
        | _ -> ()

        base.OnFrameworkInitializationCompleted()

module Program =
    [<CompiledName("BuildAvaloniaApp")>]
    let buildAvaloniaApp () =
        AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace(areas = Array.empty)

    [<EntryPoint; STAThread>]
    let main args =
        buildAvaloniaApp().StartWithClassicDesktopLifetime(args)
```
`BuildAvaloniaApp` mirrors the official template seam used by tooling and startup. `StartWithClassicDesktopLifetime` creates an `IClassicDesktopStyleApplicationLifetime`; only after framework initialization does `App` assign `MainWindow`. The entry point has `STAThread`, which is relevant to Windows APIs such as COM and the clipboard.

Desktop assumptions are explicit here. iOS and browser use `ISingleViewApplicationLifetime`; Android uses `IActivityApplicationLifetime` with a view factory because activities can be recreated. Reusing this `MainWindow` startup path on mobile would be a design error, not a missing compiler switch.

### The focused test does not start a UI {#focused-test}

After reconstruction, add a separate test project that references the pure state module and checks three additions, reset, lower-bound removal, and the unchanged initial value. Those tests require no Avalonia initialization because the tested function has no toolkit dependency. This repository has no Avalonia test project now, so this paragraph defines the testing boundary rather than reporting an executed result.

### State the native launch result exactly {#native-launch-result}

| Check | Status in this chapter | What it verifies when run | What it does not verify |
| --- | --- | --- | --- |
| Project and lock configuration | In-page template | Whether the graph resolves after reconstruction | The template itself is not restore evidence |
| Release build | Run after reconstruction | F# and AXAML compile for `net10.0` | A usable native window or package |
| Pure transition test | Write and run after reconstruction | The checked state transitions | Control lookup, layout, input, rendering |
| Native start | Run on every supported desktop OS | A real window starts in that environment | Other operating systems or packages |
| Mobile/package/store | Not covered | Nothing | Mobile lifetime, signing, installation, or store behavior |

A successful build does not verify native startup. Run the application in an interactive desktop session on each supported operating system. Record failures without weakening the pure model or suppressing host exceptions.

## Choose a state pattern deliberately {#state-patterns}

F# offers several useful boundaries; toolkit choice does not dictate one architecture.

| Pattern | State and decisions | View connection | Main pressure |
| --- | --- | --- | --- |
| Manual MVU | Immutable model plus `Msg -> Model -> Model * Effect` functions | Event handlers dispatch; renderer updates controls | Renderer and effect scheduling become repetitive as screens grow |
| MVVM adapter | Domain and presentation core remain functional; adapter exposes properties, notifications, and commands | AXAML bindings connect to adapter | Mutable notification surface, command lifetime, binding-friendly type shapes |
| Code-behind orchestration | Small local state and event handlers in the view, domain calls delegated outward | Direct control references | Easy to let business decisions, I/O, and cancellation accumulate in the window |
| Code-only/FuncUI-style | UI tree expressed in F# and commonly driven by messages/state | Language-level combinators or DSL | Community dependency, API churn, tooling and performance need their own evaluation |

The in-page template uses manual MVU at the smallest useful scale: one pure update and one imperative renderer. A larger application would normally separate model, update, effect descriptions, view adapters, navigation, and composition into modules or projects.

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

The in-page template contains no binding expression, so it does not need an `x:DataType` and does not validate a view-model binding surface. A real binding spike should include nested templates, two-way editing, commands, validation, design data, trimming or AOT if planned, and the actual IDE used by the team.

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

Avalonia's one desktop project can target Windows, macOS, and Linux, but each backend and distribution system remains distinct. Support tiers and minimum OS versions change; this chapter checked the official matrix on 2026-08-31, and an application should recheck it before release.

### Windows {#windows}

Avalonia uses Win32 directly and requires no separate Windows .NET workload for its generic desktop target. A release still chooses `win-x64`, `win-arm64`, or another supported RID; framework-dependent versus self-contained delivery; installer technology; application identity; icons; file associations; signing; update policy; and enterprise deployment behavior.

Test keyboard and high-DPI behavior, multiple monitors, clipboard and dialogs, remote sessions if supported, Windows accessibility, clean install, per-user/per-machine data, upgrade, repair, and uninstall. A package built on macOS is cross-compilation evidence, not Windows runtime evidence.

### macOS {#macos}

The default Avalonia macOS backend ships its own native library and can be built without the `net10.0-macos` workload. Distribution still needs a correctly structured `.app` bundle and `Info.plist`; normal external distribution requires code signing and notarization, and those signing steps require macOS/Xcode tooling even when bundle construction was cross-platform.

Publish and test Apple Silicon and Intel artifacts when both are supported. Verify native menus, shortcuts, file dialogs, sandbox or entitlement choices, accessibility, app identity, quarantine/Gatekeeper behavior, upgrade, and uninstall. The in-page template has no macOS launch result; a successful build cannot substitute for that smoke test.

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

Prefer an idiomatic F# shared core and the thinnest host that the tested toolchain supports. A small C# platform adapter is often cheaper than forcing generated or designer conventions through F#. If direct F# hosts work in the chosen template and IDE, keep them—but preserve a reproducible command-line build and device test.

.NET MAUI is a separate UI product for Android, iOS, Mac Catalyst, and Windows. Its official documentation and current templates center on C# and XAML. An F# library can still power a MAUI app, and a community F# template may work well. Direct F# MAUI UI development simply requires its own adoption decision; Avalonia results do not validate it.

## Accessibility, input, and responsive layout are behavior {#accessible-responsive-ui}

Avalonia's built-in controls expose automation peers to platform accessibility APIs. Use semantic controls first. Supply `AutomationProperties.Name`, `LabeledBy`, `HelpText`, live settings, or stable `AutomationId` when visible content is insufficient. Custom controls need deliberate automation behavior.

Test keyboard traversal, focus order and restoration, shortcuts, screen readers, contrast, large text, zoom or scale, high DPI, reduced motion where relevant, error announcements, and input without a pointer. Color alone must not carry meaning.

Responsive design is more than detecting “mobile.” Let layouts react to available size; use form-factor or platform conditions only when the interaction truly differs. Test long English strings, compact Chinese labels, localization expansion, right-to-left text if supported, narrow windows, touch targets, software keyboards, safe areas, rotation, and resizable desktop windows.

Accessibility and localization defects are platform defects even when the shared AXAML is identical. Include representative assistive technologies and input devices in the platform matrix.

## Verify from pure logic to released packages {#testing-evidence-ladder}

Use the cheapest useful layer first:

1. **Pure and adapter tests:** state, validation, navigation, stale-result rejection, effects, notifications, commands, cancellation, and platform ports.
2. **XAML and headless tests:** resources, compiled bindings, templates, real controls, layout, input, and visual/automation trees.
3. **Native smoke:** keyboard/pointer/touch, dialogs, clipboard, scaling, lifetime, and shutdown on each supported backend.
4. **Publish and package tests:** every RID, native assets, signing, clean install, SDK-independent launch, upgrade, rollback, and uninstall.
5. **Device and store tests:** lifecycle, permissions, deep links, network loss, accessibility, performance, distribution, crashes, and updates.

Headless CI cannot certify native backends, drivers, packaging, signing, or stores. Record OS, CPU, package, locale, scale, input, assistive technology, date, and commit with every result.

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

Time-box one representative vertical slice covering:

- official F# template, locked restore, CLI/IDE build-edit-debug workflow;
- an immutable domain workflow and one demanding screen at representative volume;
- cancellation, stale completion, offline/retry/restart, and a denied platform permission;
- keyboard/touch/focus, screen reader, large text, localization, and narrow layout;
- target-hardware performance plus per-RID publish, clean install, signing, upgrade, and uninstall;
- physical-device lifecycle when relevant, and maintenance, licensing, support, controls, and an exit condition.

Compare implementation and operational cost, not screenshot similarity. A framework is acceptable only if the team can build, diagnose, distribute, update, and support it within the product's actual platform matrix.

## Exercises {#exercises}

### Exercise 1: choose three UI boundaries {#exercise-01}

Evaluate these products separately:

1. A Windows-only trading workstation must reuse mature WPF controls and enterprise deployment; its domain calculations are new F#.
2. An offline field tool needs Windows, macOS, and two named Linux distributions, plus keyboard, touch, and local documents. It has no phone release.
3. A consumer app needs Android and iOS, camera, push notifications, deep links, background upload, store distribution, and a small desktop viewer.

For each product, record the first candidate, rejected alternatives, missing verification, and conditions for changing direction. Compare Avalonia, a C# platform shell around an F# core, .NET MAUI, and a browser surface; each product may lead to a different boundary.


::: details Answer

The deciding question is not “Which framework shares the most code?” It is “Which boundary minimizes platform-specific code and tooling while keeping important product decisions testable?”

#### A. Windows-only trading workstation {#windows-workstation}

**First candidate:** retain or create a WPF/Windows UI shell and place calculations, validation, order-state transitions, and service contracts in F# libraries.

The requirements already contain two decisive constraints: Windows-only scope and mature WPF controls. Rewriting those controls in Avalonia would spend risk to remove a platform restriction the product does not want. WinUI may deserve a separate modernization spike when a required Windows feature or vendor roadmap demands it, but it is not automatically an upgrade from a working WPF estate.

Use a narrow object-oriented API between the shell and F#:

- F# contains immutable market snapshots, validated identifiers, pricing functions, commands, outcomes, and cancellation-aware service ports;
- the UI adapter converts unions and results into properties, commands, notifications, and observable collection deltas;
- C# handles XAML code generation, control-vendor integration, window and dispatcher services, and installer-specific hooks;
- serialization and threading contracts receive tests on both sides of the boundary.

**Not first choices:**

- Avalonia adds control-replacement risk without a current cross-platform benefit.
- A browser UI may not suit the existing controls, latency, multi-window workflow, or enterprise integration.
- A direct F# WPF UI is possible, but an F# domain does not require it.

**Still to verify:** representative vendor controls, high-frequency updates, UI-thread budget, accessibility, multi-monitor and DPI behavior, authentication, crash recovery, the enterprise installer, signed updates, and upgrades from installed versions.

**Reversal condition:** reconsider WPF if its controls or supported Windows versions block the roadmap, or if a funded macOS or Linux requirement appears. Compare one vertical Avalonia slice with another Windows modernization path, using the same F# core.

#### B. Offline cross-platform field tool {#field-tool}

**First candidate:** an Avalonia desktop application with a shared F# domain/presentation core and one desktop host, followed by explicit Windows, macOS, and named Linux package tracks.

The product needs exactly the desktop platforms Avalonia's desktop host targets and does not need phone lifecycle or stores. Keyboard plus touch and local documents fit a desktop UI, but they still require responsive controls, larger targets, file-picker adapters, durable atomic storage, conflict/recovery policy, and platform tests.

Keep these boundaries:

- pure F# state handles document identity, validation, edit history, synchronization state, and retry decisions;
- a persistence port defines atomic save, backup, migration, and recovery from interrupted writes;
- platform adapters handle pickers, recent-document integration, protocol and file associations, secure credentials, and external links;
- Avalonia views handle layout and input through compiled binding or a defined renderer;
- packaging projects or pipeline stages handle each RID, metadata, signing, installer, and update channel.

**Not first choices:**

- WPF cannot cover macOS or Linux.
- MAUI does not target desktop Linux and adds mobile tooling the product does not require.
- A browser or PWA is plausible only if its offline files, device integration, updates, and enterprise deployment outperform the desktop package.

**Still to verify:**

- the hardest document and list screen with large data;
- offline restart and file locking;
- fonts, locales, keyboard, touch, and screen readers;
- native launch on Windows, macOS, and named Linux targets, including the chosen X11 or XWayland scope;
- signed packages, clean install, upgrade, rollback, and field-device performance.

**Reversal condition:** if native document integration, Linux backend behavior, control performance, or packaging cost fails the spike budget, preserve the F# core and compare native shells or a browser surface.

#### C. Consumer mobile app and companion viewer {#consumer-mobile}

**First candidate:** do not choose a shared UI yet. Build one mobile vertical slice in both (1) Avalonia cross-platform with thin Android/iOS hosts and (2) a C# mobile shell—MAUI or native platform UI—around the same F# core. Make the desktop viewer a separate, smaller decision.

Camera, push notifications, deep links, background upload, permissions, process death, signing, and stores dominate this product. They are platform capabilities and lifecycle contracts, not drawing primitives. A high shared-AXAML percentage is useful only after these paths work on devices.

The comparison should measure:

| Dimension | Avalonia cross-platform candidate | C# mobile-shell candidate |
| --- | --- | --- |
| Shared view | Potentially high, including desktop viewer | Mobile views use MAUI/native conventions; desktop may be separate |
| F# core | High | High |
| Native integrations | Avalonia host adapters plus platform SDKs | MAUI handlers/plugins or direct native APIs |
| F# UI ergonomics | Official Avalonia F# templates; surrounding samples still often C# | F# library is straightforward; official UI/tooling is C#-shaped |
| Evidence cost | Android/iOS hosts plus every mobile path and desktop host | Two mobile hosts plus a separate viewer boundary |

**Not first choices:** WPF cannot deliver mobile. A browser-only UI cannot be assumed to support background upload, push, camera, and stores. One codebase does not replace device testing.

**Still to verify:**

- denied permissions, capture interruption, and upload interruption;
- notification taps from every lifecycle state, deep links, offline queues, and duplicate submission;
- Android activity recreation and iOS suspension or termination;
- device accessibility, energy use, memory, and startup;
- signing, staged store release, crash symbols, and update compatibility.

**Reversal condition:** choose Avalonia only if the shared UI materially reduces total cost and all critical native paths remain supportable. Choose the C# shell if native integrations, tooling, or platform UX are substantially safer. The F# core survives either result.

:::

### Exercise 2: turn the in-page desktop template into a desktop release {#exercise-02}

Produce two artifacts for turning the in-page template into a supported Windows/macOS/Linux application:

- **Application plan:** module boundaries, asynchronous effects, persistence, settings migration, accessibility, and localization.
- **Verification matrix:** headless tests, native smoke, runtime identifiers, framework-dependent and self-contained delivery, native assets, packages, signing or notarization, clean install, update, rollback, crash diagnostics, and the exact platform matrix.

Mark unexecuted matrix cells as “no evidence” until an interactive desktop session on the corresponding system supplies a result.


::: details Answer

Begin with a verification ledger. This chapter shows a project pinned to Avalonia 12.1.1, `net10.0` source, and AXAML, and describes the pure-state tests to add. After reconstruction, run restore, Release compilation, tests, and native launch before claiming they pass. Windows, macOS, Linux, publish output, packages, signing, installation, updates, and accessibility each require their own result.

#### Restructure without losing the small core {#desktop-structure}

A proportional target structure is:

```text
DesktopApp.Domain        immutable rules and validated values
DesktopApp.Presentation  Model, Msg, update, Effect descriptions
DesktopApp.Core          shared Avalonia views and UI adapters
DesktopApp.Desktop       AppBuilder, lifetime, platform composition
DesktopApp.Tests         pure and adapter tests
DesktopApp.UiTests       headless control/layout/input tests
packaging/               one maintained track per supported OS/package
```

This need not become seven projects immediately. The boundary matters before the physical split. Start by moving `Model`, `Message`, and `Counter.update` out of the window file, then introduce modules or projects only when dependency direction or platform variation justifies them.

#### Add a supervised effect loop {#desktop-effects}

Replace the local mutable counter with an application store that manages the current model, serial message processing, external operations, and view subscriptions. Each operation receives a cancellation token and reports success, failure, or cancellation as a message. Every request has an ID, so only a completion matching the active model state is accepted.

Use ports for documents, settings, secure credentials, dialogs, external links, update checks, and crash reporting. Keep implementation details in the desktop composition root. Persist only durable application data; rebuild view objects and derived presentation values.

For local documents:

1. validate and serialize to a new temporary file in the destination filesystem;
2. flush as required by the durability contract;
3. atomically replace the previous file where the platform/filesystem supports it;
4. keep a recoverable backup or journal for irreplaceable data;
5. record a schema version and test every supported migration;
6. surface permission, conflict, disk-full, cancellation, and corrupt-data outcomes separately.

#### Close view and accessibility gaps {#desktop-view-quality}

Adopt compiled bindings with explicit `x:DataType` if the application moves from the in-page template's renderer to MVVM. Add stable automation IDs and labels, keyboard navigation and shortcuts, focus restoration, error/live announcements, contrast, large text, high DPI, reduced motion where relevant, and screen-reader checks.

Externalize strings and test English, Chinese, long translations, missing glyphs, number/date formats, and narrow layouts. Do not infer touch support from pointer clicks; test touch targets, scrolling, selection, drag behavior, and software keyboards on representative hardware.

#### Climb the test and release matrix {#desktop-release-matrix}

| Layer | Windows | macOS | Named Linux targets |
| --- | --- | --- | --- |
| Locked build | `net10.0` plus chosen RIDs | `net10.0` plus `osx-arm64`/`osx-x64` as supported | `linux-x64`/`linux-arm64` as required |
| Headless | bindings, layout, input, automation tree | same shared suite | same shared suite |
| Native smoke | Win32, DPI, keyboard, dialogs, shutdown | unlocked native backend, menus, shortcuts, dialogs | X11/XWayland or explicitly selected backend, desktop environments |
| Package | selected signed installer | `.app` bundle, identity, signing, notarization, chosen archive | explicitly named `.deb`/RPM/other formats and native dependencies |
| Lifecycle | install, first run, update, rollback, uninstall | quarantine/Gatekeeper, install, update, rollback, uninstall | clean distro image, install, update, rollback, remove |
| Accessibility | Windows screen reader and keyboard | VoiceOver and keyboard | AT-SPI screen reader and keyboard |

Publish each RID from locked inputs. Decide explicitly between framework-dependent and self-contained output. If self-contained, establish a rebuild cadence for .NET security patches. Test single-file, trimming, ReadyToRun, or AOT only if a measured startup/size goal justifies them; fail the build on compatibility warnings and run the packaged artifact.

#### Package, update, observe, recover {#desktop-operations}

Give the app a stable ID, semantic display version and monotonic build version, deterministic data/log/cache locations, signed update metadata, and a channel policy. Sign and notarize where the platform expects it. Generate and retain checksums, dependency inventory, symbols, and source/commit provenance.

Instrument startup stage, handled/unhandled failure, update state, migration version, performance, and feature outcomes without collecting document contents or secrets. Make crash reporting consent and privacy behavior explicit.

Test clean install; upgrade from every supported predecessor; interrupted download, install, and migration; incompatible downgrade; rollback or forward repair; and uninstall with both “keep user data” and “remove data” policy where offered. A last-known-good package is useful only when its data format can still open the user's state.

#### State the limit of native verification {#desktop-evidence-limit}

Run the macOS smoke test in an interactive session. Record the OS, CPU, display, locale, scale, commit, and result. If it passes, report only: “This build displayed and accepted input on this macOS target.” Do not generalize that result to every macOS system. If startup fails, reduce the case to the official template, then investigate configuration, dependency, and framework issues with a minimal reproduction.

Do not close the Windows or Linux rows using macOS results. Release only the matrix rows the team is prepared to support, diagnose, patch, and retire.

:::

### Exercise 3: extend the architecture to mobile {#exercise-03}

Design a Core/Desktop/Android/iOS project graph for a booking client. Organize the design around four concerns:

- **Shared behavior:** edit a draft, submit it, open a confirmation deep link, and export a receipt through a platform picker.
- **Lifetime and state:** survive rotation or activity recreation, resume after process termination, persist checkpoints, and reject stale results.
- **Platform boundary:** define F# state, messages, and effects alongside platform ports, permission outcomes, and host-language choices.
- **Delivery evidence:** lock workloads, run simulator and device tests, sign releases, stage store rollout, collect telemetry, and define reversal criteria.

Finish by stating exactly what a desktop build proves about the mobile targets.


::: details Answer

Use the official multi-project shape and keep the decision points visible:

```text
Booking.Client.Domain
Booking.Client.Presentation
Booking.Client.Core          shared Avalonia views and adapters
Booking.Client.Desktop       classic desktop lifetime
Booking.Client.Android       .NET Android + activity/view factory
Booking.Client.iOS           .NET iOS + app delegate/scenes
Booking.Client.Tests         pure workflow, persistence, port contracts
Booking.Client.UiTests       shared headless view behavior
```

The application should pin the .NET SDK, Avalonia packages, NuGet locks, workload manifest/version set, Android SDK/JDK expectations, and Xcode compatibility. Platform CI images are part of the toolchain, not invisible infrastructure.

#### Model draft and submission states {#mobile-state}

A compact shared model could distinguish:

```fsharp
type Submission =
    | Editing
    | Submitting of operationId: Guid
    | OutcomeUnknown of operationId: Guid
    | Confirmed of bookingId: string
    | Rejected of message: string

type Msg =
    | DraftChanged of DraftChange
    | SubmitRequested
    | SubmitFinished of operationId: Guid * Result<string, SubmitError>
    | AppSuspending
    | AppResumed
    | ConfirmationLinkOpened of Uri
    | ReceiptExportRequested
    | ReceiptExportFinished of Result<unit, ExportError>
```

`update` validates the draft, allocates one stable operation ID, ignores completions for older IDs, and describes save/submit/export effects. The server must honor the operation ID for idempotency; the UI cannot manufacture exactly-once submission after an unknown network outcome.

Persist the draft after meaningful edits with debounce and at suspend/navigation checkpoints. Persist the operation ID before sending. On resume or cold start, load durable state and reconcile `Submitting`/`OutcomeUnknown` with the server before allowing a new identity. Never serialize controls, activity instances, cancellation tokens, or open streams.

#### Define platform ports and outcomes {#mobile-ports}

The shared projects can define ports for:

- draft storage with atomic replace, migration, corruption recovery, and test fakes;
- authenticated booking submission and status lookup with cancellation and idempotency identity;
- receipt export returning completed, user-cancelled, permission-denied, unavailable, or failed;
- deep-link parsing as a pure function, with host registration kept outside;
- connectivity hints that improve UX but never claim a request succeeded;
- telemetry with consent, redaction, correlation, and offline buffering policy.

Android implements activity entry, intents, runtime permissions, document picker, secure storage, notification channels, and background scheduling. iOS implements scenes/app delegate, URL handling, permission descriptions, document/share UI, Keychain access, notifications, and allowed background modes. Each adapter translates native callbacks into shared messages.

#### Choose host languages pragmatically {#mobile-host-languages}

Start from the official F# Avalonia cross-platform template and build both hosts from the CLI. Keep an F# host when the generated project, IDE, workload, binding, native callback, signing, and device paths remain routine. Use a tiny C# host where platform source generation, examples, or SDK conventions make it materially safer. Either way, the F# projects still contain the domain and presentation state.

Do not rewrite native SDK types into an elaborate language-neutral framework. Keep adapters thin, test their contracts, and allow platform-specific behavior where users expect it.

#### Verify lifecycle and distribution separately {#mobile-evidence-matrix}

| Scenario | Android checks | iOS checks |
| --- | --- | --- |
| Build | locked `net10.0-android` workload and target SDK | locked `net10.0-ios` workload and compatible Xcode |
| Basic runtime | supported emulator plus representative physical devices/architectures | current simulator plus representative iPhone/iPad devices |
| Recreation | rotation/configuration and activity recreation | scene transitions and view recreation as applicable |
| Process loss | kill in background, cold restore, reconcile in-flight operation | terminate/suspend, cold restore, reconcile in-flight operation |
| Links/notifications | cold, background, foreground intents and notification taps | cold, background, foreground links and notification responses |
| Export/permissions | grant, deny, deny permanently, cancel, provider unavailable | grant, deny, cancel, unavailable share/document target |
| Accessibility/input | TalkBack, switch/keyboard/touch, large font | VoiceOver, switch/keyboard/touch, Dynamic Type |
| Distribution | signed internal track, staged rollout, upgrade, rollback plan | provisioned archive, TestFlight/staged release, upgrade, rollback plan |

Also test:

- offline, slow, and changing networks;
- duplicate taps and a server timeout after acceptance;
- clock changes, low storage, localization, and memory pressure;
- startup and interaction budgets;
- crash-symbol upload, privacy disclosures, and telemetry queries.

Store review verifies distribution, not business correctness.

Release one immutable backend contract and compatible client sequence. Mobile clients update slowly, so servers must support older app versions during the stated window. Feature flags and minimum-version gates need an offline and failure policy; they must not destroy drafts.

#### State the desktop inference limit {#mobile-inference-limit}

The in-page template alone proves no desktop or mobile behavior. After reconstruction, a successful build proves only that the current desktop project compiles; the pure counter transitions are evidence only after their separate tests run. Once mobile-neutral Domain and Presentation projects are extracted, the same pure tests can also verify shared logic.

It does not verify any of the following:

- `net10.0-android` or `net10.0-ios` restore and workload compatibility;
- host startup, Activity or scene lifecycle, and AXAML on those targets;
- permissions, native services, touch, and accessibility;
- package metadata, signing, physical devices, stores, and lifecycle recovery.

Each item needs its own matrix row.

**Reversal criteria:** abandon the shared Avalonia UI, but keep the F# core, if any of these conditions holds:

- critical camera, background, or notification integrations lack a supportable path;
- device UX or accessibility misses product thresholds;
- platform regressions dominate delivery;
- packaging and store work exceeds the budget; or
- the team cannot diagnose native failures.

A thin C# or native UI shell remains a planned exit, not a rewrite of business rules.

:::


## Sources {#sources}

- [Avalonia documentation and Avalonia 12 guidance](https://docs.avaloniaui.net/)
- [Avalonia supported-platform matrix](https://docs.avaloniaui.net/docs/supported-platforms)
- [Avalonia application lifetimes](https://docs.avaloniaui.net/docs/fundamentals/application-lifetimes)
- [NuGet: Avalonia package versions](https://www.nuget.org/packages/Avalonia)

Chapter 44 crosses another host boundary: using F# domain code inside Unity while keeping Unity serialization, component lifecycles, IL2CPP, and player builds in an explicit adapter layer.
