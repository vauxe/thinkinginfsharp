---
title: "Chapter 43: Avalonia, Desktop, and Mobile"
description: "Design F# user interfaces from state, lifetime, platform, tooling, packaging, and evidence boundaries instead of treating cross-platform compilation as cross-platform validation."
translationKey: part-07/ch-43-avalonia-desktop-mobile
---

# Chapter 43: Avalonia, Desktop, and Mobile {#overview}

An F# user interface is not “some controls around the real program.” It is a long-lived boundary where input, time, cancellation, mutable platform objects, accessibility, operating-system services, and release mechanics meet. F# helps most when the application turns those events into explicit data and keeps decisions testable before a window exists.

Avalonia is a cross-platform .NET UI framework with official F# templates. It draws its own controls and provides desktop, mobile, and browser hosts, but that does not make every platform identical. A shared view can compile while its font, input, lifecycle, permission, native integration, package, signing, or accessibility path fails on one target. “Cross-platform” describes an architecture and support surface; it is not a test result.

Start with product and platform constraints, not XAML syntax. A small desktop sample with a defined verification scope leads into state patterns, binding boundaries, threading, platform services, mobile hosts, testing, packaging, and release checks.

::: tip Two reading passes
For a first pass, follow the [UI stack](#ui-stack-contracts), [decision map](#decision-map), and [verified desktop slice](#verified-slice). When implementing or adopting a toolkit, return to the sections on state, binding, lifetime, platforms, verification, and release as needed.
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

Choose from team fluency, tooling, binding needs, styling scale, hot reload or preview requirements, generated-code tolerance, and library maturity. Code-only does not make mutable controls pure. XAML does not require domain logic in a view model. The desktop sample uses AXAML plus a tiny F# code-behind because that exposes the boundary without another framework.

## The desktop sample: one verified desktop slice {#verified-slice}

The verified slice is deliberately one `net10.0` desktop executable with five primary files. Mobile target frameworks, platform workloads, MVVM infrastructure, and packaging enter later slices when their corresponding requirements are tested.

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
`Avalonia`, `Avalonia.Desktop`, and `Avalonia.Themes.Fluent` are pinned to 12.1.1 and resolved through a lock file. A copied project can also pin FSharp.Core 10.1.301. `WinExe` selects a graphical executable; `net10.0` remains a generic desktop target rather than `net10.0-macos` or `net10.0-windows`.

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

A focused xUnit test can reference the pure state module and check three additions, reset, lower-bound removal, and the unchanged initial value. It runs without Avalonia initialization because the tested function has no toolkit dependency. That speed and determinism are the payoff of the boundary.

### State the native launch result exactly {#native-launch-result}

| Check | Status in this chapter | What it verifies when run | What it does not verify |
| --- | --- | --- | --- |
| Project and lock configuration | Shown | The recorded NuGet graph resolves | Future versions or other runtime graphs |
| Release build | Run after copying | F# and AXAML compile for `net10.0` | A usable native window or package |
| Pure transition test | Shown | The checked state transitions | Control lookup, layout, input, rendering |
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

The desktop sample uses manual MVU at the smallest useful scale: one pure update and one imperative renderer. A larger application would normally separate model, update, effect descriptions, view adapters, navigation, and composition into modules or projects.

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

The desktop sample contains no binding expression, so it does not need an `x:DataType` and does not validate a view-model binding surface. A real binding spike should include nested templates, two-way editing, commands, validation, design data, trimming or AOT if planned, and the actual IDE used by the team.

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

Avalonia's one desktop project can target Windows, macOS, and Linux, but each backend and distribution system remains distinct. Support tiers and minimum OS versions change; this chapter checked the official matrix on 2026-08-25, and an application should recheck it before release.

### Windows {#windows}

Avalonia uses Win32 directly and requires no separate Windows .NET workload for its generic desktop target. A release still chooses `win-x64`, `win-arm64`, or another supported RID; framework-dependent versus self-contained delivery; installer technology; application identity; icons; file associations; signing; update policy; and enterprise deployment behavior.

Test keyboard and high-DPI behavior, multiple monitors, clipboard and dialogs, remote sessions if supported, Windows accessibility, clean install, per-user/per-machine data, upgrade, repair, and uninstall. A package built on macOS is cross-compilation evidence, not Windows runtime evidence.

### macOS {#macos}

The default Avalonia macOS backend ships its own native library and can be built without the `net10.0-macos` workload. Distribution still needs a correctly structured `.app` bundle and `Info.plist`; normal external distribution requires code signing and notarization, and those signing steps require macOS/Xcode tooling even when bundle construction was cross-platform.

Publish and test Apple Silicon and Intel artifacts when both are supported. Verify native menus, shortcuts, file dialogs, sandbox or entitlement choices, accessibility, app identity, quarantine/Gatekeeper behavior, upgrade, and uninstall. The desktop sample's `-6661` launch result is specifically not a passed macOS smoke test.

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

Use the cheapest useful layer first, but do not stop there:

1. **Pure tests:** update, validation, navigation decisions, stale-result rejection, formatting inputs, and effect descriptions.
2. **Adapter tests:** notifications, commands, collection deltas, validation projection, cancellation, and platform-port fakes.
3. **XAML/compile tests:** resources, classes, compiled binding paths, templates, and targeted framework graphs.
4. **Headless Avalonia tests:** construct real controls, apply styles and layout, simulate input, inspect the visual or automation tree, and optionally compare images.
5. **Native debug smoke:** start an unlocked native backend, exercise keyboard/pointer/touch, dialogs, clipboard, scaling, and shutdown.
6. **Publish and package tests:** produce each RID, inspect native assets and metadata, sign, install on a clean target, launch outside the SDK, upgrade, rollback, and uninstall.
7. **Device and store tests:** permissions, suspend/resume, process death, deep links, network loss, accessibility, performance, signing, staged distribution, crash reports, and update behavior.

Headless testing is valuable in CI, but it replaces the native windowing and rendering backend. It cannot certify Win32, macOS, X11/Wayland, Android, iOS, drivers, packaging, signing, or store behavior.

Record each result with its OS version, CPU, package, locale, scale, input, assistive technology, test date, and commit. “Works on my machine” becomes useful only after “machine” is named.

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
- Disabling compiled binding globally to silence an `x:DataType` or public-type mismatch.
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

Evaluate these products separately:

1. A Windows-only trading workstation must reuse mature WPF controls and enterprise deployment; its domain calculations are new F#.
2. An offline field tool needs Windows, macOS, and two named Linux distributions, plus keyboard, touch, and local documents. It has no phone release.
3. A consumer app needs Android and iOS, camera, push notifications, deep links, background upload, store distribution, and a small desktop viewer.

For each product, record the first candidate, rejected alternatives, missing verification, and conditions for changing direction. Compare Avalonia, a C# platform shell around an F# core, .NET MAUI, and a browser surface; each product may lead to a different boundary.

### Exercise 2: turn the desktop sample into a desktop release {#exercise-02}

Produce two artifacts for turning the desktop sample into a supported Windows/macOS/Linux application:

- **Application plan:** module boundaries, asynchronous effects, persistence, settings migration, accessibility, and localization.
- **Verification matrix:** headless tests, native smoke, runtime identifiers, framework-dependent and self-contained delivery, native assets, packages, signing or notarization, clean install, update, rollback, crash diagnostics, and the exact platform matrix.

Keep the existing `-6661` native-launch result in the matrix until a later run supplies stronger evidence.

### Exercise 3: extend the architecture to mobile {#exercise-03}

Design a Core/Desktop/Android/iOS project graph for a booking client. Organize the design around four concerns:

- **Shared behavior:** edit a draft, submit it, open a confirmation deep link, and export a receipt through a platform picker.
- **Lifetime and state:** survive rotation or activity recreation, resume after process termination, persist checkpoints, and reject stale results.
- **Platform boundary:** define F# state, messages, and effects alongside platform ports, permission outcomes, and host-language choices.
- **Delivery evidence:** lock workloads, run simulator and device tests, sign releases, stage store rollout, collect telemetry, and define reversal criteria.

Finish by stating exactly what a desktop build proves about the mobile targets.

[Read the chapter solutions](../solutions/ch-43-avalonia-desktop-mobile).

## Chapter review {#chapter-review}

- A client is a stack of domain, presentation, toolkit, host, and distribution contracts.
- Measure shared logic, shared UI, and verified platform coverage separately.
- Choose a UI boundary from users, devices, native capabilities, team skills, and release channels.
- Avalonia supplies official F# templates, compiled AXAML, shared controls, and multiple platform hosts; it does not erase platform behavior.
- The desktop sample pins Avalonia 12.1.1 and separates a pure `Counter.update` from an imperative desktop view.
- The project, pure test, and startup code are shown; each adopting application must run restore, build, tests, and native launch itself.
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
