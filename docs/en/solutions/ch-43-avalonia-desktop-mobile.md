---
title: "Chapter 43 Solutions"
description: "Choose proportional UI boundaries, turn the verified Avalonia slice into a desktop release plan, and design an honest mobile project and evidence graph."
translationKey: solutions/ch-43-avalonia-desktop-mobile
kind: solution
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
  - id: avalonia-templates
    url: https://github.com/AvaloniaUI/Avalonia.Templates
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
  - id: avalonia-threading
    url: https://docs.avaloniaui.net/docs/app-development/threading
    checked: "2026-08-25"
  - id: avalonia-accessibility
    url: https://docs.avaloniaui.net/docs/app-development/accessibility
    checked: "2026-08-25"
  - id: avalonia-headless-testing
    url: https://docs.avaloniaui.net/docs/testing/setting-up-the-headless-platform
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
---

# Chapter 43 Solutions {#overview}

These are reference designs, not universal verdicts. Each solution names a first candidate, the boundary that keeps F# useful, evidence that must still be collected, and a condition that would reverse the choice. A team with different controls, skills, devices, support contracts, or distribution channels can reasonably choose differently.

## Exercise 1: choose three UI boundaries {#exercise-01}

The deciding question is not “Which framework shares the most code?” It is “Which boundary gives this product the smallest justified platform surface while keeping the costly decisions testable?”

### A. Windows-only trading workstation {#windows-workstation}

**First candidate:** retain or create a WPF/Windows UI shell and place calculations, validation, order-state transitions, and service contracts in F# libraries.

The requirements already contain two decisive constraints: Windows-only scope and mature WPF controls. Rewriting those controls in Avalonia would spend risk to remove a platform restriction the product does not want. WinUI may deserve a separate modernization spike when a required Windows feature or vendor roadmap demands it, but it is not automatically an upgrade from a working WPF estate.

Use a narrow object-shaped boundary between the shell and F#:

- F# owns immutable market snapshots, validated identifiers, pricing functions, commands, outcomes, and cancellation-aware service ports;
- the UI adapter converts unions and results into properties, commands, notifications, and observable collection deltas;
- C# owns XAML code generation, control-vendor integration, window/dispatcher services, and installer-specific hooks;
- serialization and threading contracts receive tests on both sides of the boundary.

**Rejected first alternatives:** Avalonia adds control-replacement risk without a present cross-platform benefit; a browser surface may be unsuitable for existing controls, latency, multi-window, or enterprise integration; direct F# WPF UI is possible to investigate but is not needed to obtain an F# domain.

**Evidence gap:** representative vendor controls, high-frequency update behavior, UI-thread budget, accessibility, multi-monitor/DPI, authentication, crash recovery, enterprise installer, signed update, and upgrade from the installed estate.

**Reversal condition:** if WPF controls or supported Windows versions block the required roadmap, or a funded macOS/Linux requirement appears, compare a vertical Avalonia rewrite against another Windows modernization path using the same F# core.

### B. Offline cross-platform field tool {#field-tool}

**First candidate:** an Avalonia desktop application with a shared F# domain/presentation core and one desktop host, followed by explicit Windows, macOS, and named Linux package tracks.

The product needs exactly the desktop platforms Avalonia's desktop host targets and does not need phone lifecycle or stores. Keyboard plus touch and local documents fit a desktop UI, but they still require responsive controls, larger targets, file-picker adapters, durable atomic storage, conflict/recovery policy, and platform tests.

Keep these boundaries:

- pure F# state handles document identity, validation, edit history, synchronization state, and retry decisions;
- a persistence port owns atomic save, backup, migration, and recovery from interrupted writes;
- platform adapters own pickers, recent-document integration, protocol/file associations, secure credentials, and external links;
- Avalonia views own layout and input, with compiled binding or an explicit renderer;
- packaging projects or pipeline stages own each RID, metadata, signing, installer, and update channel.

**Rejected first alternatives:** WPF fails macOS/Linux; MAUI does not target desktop Linux and adds mobile-shaped tooling the product does not require; a browser/PWA is plausible only if its offline file, device integration, update, and enterprise deployment evidence beats the desktop package.

**Evidence gap:** the hardest document and list screen, large data, offline restart, file locking, fonts/locales, keyboard and touch, screen readers, Windows/macOS/Linux native launch, X11/XWayland scope, signed packages, clean install, upgrade, rollback, and field-device performance.

**Reversal condition:** if native document integration, Linux backend behavior, control performance, or packaging cost fails the spike budget, preserve the F# core and compare native shells or a browser surface.

### C. Consumer mobile app and companion viewer {#consumer-mobile}

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

**Rejected first alternatives:** WPF cannot deliver mobile; a browser-only surface cannot be assumed to satisfy background upload, push, camera, and stores; one codebase claim is not a substitute for device proof.

**Evidence gap:** denied permissions, capture and upload interruption, notification tap from every lifecycle state, deep links, offline queue, duplicate submission, Android activity recreation, iOS suspend/termination, device accessibility, energy/memory/startup, signing, staged store release, crash symbols, and update compatibility.

**Reversal condition:** choose Avalonia only if the shared UI materially reduces total cost and all critical native paths remain supportable. Choose the C# shell if native integrations, tooling, or platform UX are substantially safer. The F# core survives either result.

## Exercise 2: turn X43 into a desktop release {#exercise-02}

Begin with the evidence ledger. X43 currently proves a locked Avalonia 12.1.1 graph, `net10.0` Release compilation, AXAML compilation, one pure-state test, 68 passing example tests, and repository integration. It does not prove a displayed native window: the automated macOS attempt stopped before window creation with RenderTimer error `-6661`. Windows, Linux, publish output, packages, signing, installation, updates, and accessibility are unexecuted.

### Restructure without losing the small core {#desktop-structure}

A proportional target structure is:

```text
DesktopApp.Domain        immutable rules and validated values
DesktopApp.Presentation  Model, Msg, update, Effect descriptions
DesktopApp.Core          shared Avalonia views and UI adapters
DesktopApp.Desktop       AppBuilder, lifetime, platform composition
DesktopApp.Tests         pure and adapter tests
DesktopApp.UiTests       headless control/layout/input tests
packaging/               one owned track per supported OS/package
```

This need not become seven projects immediately. The boundary matters before the physical split. Start by moving `Model`, `Message`, and `Counter.update` out of the window file, then introduce modules or projects only when dependency direction or platform variation justifies them.

### Add a supervised effect loop {#desktop-effects}

Replace the local mutable counter with an application store that owns the current model, serial message processing, effect execution, and subscription by views. An effect has a cancellation token and reports success, failure, or cancellation as a message. Each request carries an identity so a completion is accepted only when it matches the active model state.

Use ports for documents, settings, secure credentials, dialogs, external links, update checks, and crash reporting. Keep implementation details in the desktop composition root. Persist only durable application data; rebuild view objects and derived presentation values.

For local documents:

1. validate and serialize to a new temporary file in the destination filesystem;
2. flush as required by the durability contract;
3. atomically replace the previous file where the platform/filesystem supports it;
4. keep a recoverable backup or journal for irreplaceable data;
5. record a schema version and test every supported migration;
6. surface permission, conflict, disk-full, cancellation, and corrupt-data outcomes separately.

### Close view and accessibility gaps {#desktop-view-quality}

Adopt compiled bindings with explicit `x:DataType` if the application moves from the X43 renderer to MVVM. Add stable automation IDs and labels, keyboard navigation and shortcuts, focus restoration, error/live announcements, contrast, large text, high DPI, reduced motion where relevant, and screen-reader checks.

Externalize strings and test English, Chinese, long translations, missing glyphs, number/date formats, and narrow layouts. Do not infer touch support from pointer clicks; test touch targets, scrolling, selection, drag behavior, and software keyboards on representative hardware.

### Climb the test and release matrix {#desktop-release-matrix}

| Layer | Windows | macOS | Named Linux targets |
| --- | --- | --- | --- |
| Locked build | `net10.0` plus chosen RIDs | `net10.0` plus `osx-arm64`/`osx-x64` as supported | `linux-x64`/`linux-arm64` as required |
| Headless | bindings, layout, input, automation tree | same shared suite | same shared suite |
| Native smoke | Win32, DPI, keyboard, dialogs, shutdown | unlocked native backend, menus, shortcuts, dialogs | X11/XWayland or explicitly selected backend, desktop environments |
| Package | selected signed installer | `.app` bundle, identity, signing, notarization, chosen archive | explicitly named `.deb`/RPM/other formats and native dependencies |
| Lifecycle | install, first run, update, rollback, uninstall | quarantine/Gatekeeper, install, update, rollback, uninstall | clean distro image, install, update, rollback, remove |
| Accessibility | Windows screen reader and keyboard | VoiceOver and keyboard | AT-SPI screen reader and keyboard |

Publish each RID from locked inputs. Decide explicitly between framework-dependent and self-contained output. If self-contained, establish a rebuild cadence for .NET security patches. Test single-file, trimming, ReadyToRun, or AOT only if a measured startup/size goal justifies them; fail the build on compatibility warnings and run the packaged artifact.

### Package, update, observe, recover {#desktop-operations}

Give the app a stable ID, semantic display version and monotonic build version, deterministic data/log/cache locations, signed update metadata, and a channel policy. Sign and notarize where the platform expects it. Generate and retain checksums, dependency inventory, symbols, and source/commit provenance.

Instrument startup stage, handled/unhandled failure, update state, migration version, performance, and feature outcomes without collecting document contents or secrets. Make crash reporting consent and privacy behavior explicit.

Test clean install; upgrade from every supported predecessor; interrupted download, install, and migration; incompatible downgrade; rollback or forward repair; and uninstall with both “keep user data” and “remove data” policy where offered. A last-known-good package is useful only when its data format can still open the user's state.

### Preserve the native evidence limit {#desktop-evidence-limit}

Rerun the macOS smoke in an unlocked interactive session and record OS, CPU, display, locale, scale, commit, and result. If it passes, the evidence becomes “this build displayed and interacted on this macOS target,” not “all macOS works.” If `-6661` repeats in a valid display session, reduce to the official template and then investigate configuration, dependency, and framework issues with a minimal reproduction.

Do not close the Windows or Linux rows using macOS results. Release only the matrix rows the team is prepared to support, diagnose, patch, and retire.

## Exercise 3: extend the architecture to mobile {#exercise-03}

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

The repository would pin the .NET SDK, Avalonia packages, NuGet locks, workload manifest/version set, Android SDK/JDK expectations, and Xcode compatibility. Platform CI images are part of the toolchain, not invisible infrastructure.

### Model draft and submission states {#mobile-state}

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

### Define platform ports and outcomes {#mobile-ports}

The shared projects can own ports for:

- draft storage with atomic replace, migration, corruption recovery, and test fakes;
- authenticated booking submission and status lookup with cancellation and idempotency identity;
- receipt export returning completed, user-cancelled, permission-denied, unavailable, or failed;
- deep-link parsing as a pure function, with host registration kept outside;
- connectivity hints that improve UX but never claim a request succeeded;
- telemetry with consent, redaction, correlation, and offline buffering policy.

Android implements activity entry, intents, runtime permissions, document picker, secure storage, notification channels, and background scheduling. iOS implements scenes/app delegate, URL handling, permission descriptions, document/share UI, Keychain access, notifications, and allowed background modes. Each adapter translates native callbacks into shared messages.

### Choose host languages pragmatically {#mobile-host-languages}

Start from the official F# Avalonia cross-platform template and build both hosts from the CLI. Keep an F# host when the generated project, IDE, workload, binding, native callback, signing, and device paths remain routine. Use a tiny C# host where platform source generation, examples, or SDK conventions make it materially safer. Neither choice changes ownership of the F# domain and presentation state.

Do not rewrite native SDK types into an elaborate language-neutral framework. Keep adapters thin, test their contracts, and allow platform-specific behavior where users expect it.

### Verify lifecycle and distribution separately {#mobile-evidence-matrix}

| Scenario | Android evidence | iOS evidence |
| --- | --- | --- |
| Build | locked `net10.0-android` workload and target SDK | locked `net10.0-ios` workload and compatible Xcode |
| Basic runtime | supported emulator plus representative physical devices/architectures | current simulator plus representative iPhone/iPad devices |
| Recreation | rotation/configuration and activity recreation | scene transitions and view recreation as applicable |
| Process loss | kill in background, cold restore, reconcile in-flight operation | terminate/suspend, cold restore, reconcile in-flight operation |
| Links/notifications | cold, background, foreground intents and notification taps | cold, background, foreground links and notification responses |
| Export/permissions | grant, deny, deny permanently, cancel, provider unavailable | grant, deny, cancel, unavailable share/document target |
| Accessibility/input | TalkBack, switch/keyboard/touch, large font | VoiceOver, switch/keyboard/touch, Dynamic Type |
| Distribution | signed internal track, staged rollout, upgrade, rollback plan | provisioned archive, TestFlight/staged release, upgrade, rollback plan |

Add offline, slow and changing networks; duplicate taps; server timeout after acceptance; clock changes; low storage; localization; memory pressure; startup and interaction budgets; crash symbol upload; privacy disclosures; and telemetry queries. Store review success is distribution evidence, not proof of business correctness.

Release one immutable backend contract and compatible client sequence. Mobile clients update slowly, so servers must support older app versions during the stated window. Feature flags and minimum-version gates need an offline and failure policy; they must not destroy drafts.

### State the desktop inference limit {#mobile-inference-limit}

The X43 desktop build proves that the shared compiler can build its current desktop project and that its pure counter transitions pass. After extracting a mobile-neutral Domain/Presentation project, those pure tests can become evidence for shared logic.

It proves nothing about `net10.0-android` or `net10.0-ios` restore, workload compatibility, host startup, Activity/scenes, AXAML on those targets, permissions, native services, touch, accessibility, package metadata, signing, physical devices, stores, or lifecycle recovery. Every one of those needs its own row.

**Reversal criteria:** abandon shared Avalonia UI—not the F# core—if critical camera/background/notification integrations lack a supportable path, device UX or accessibility misses product thresholds, platform regressions dominate delivery, packaging/store work exceeds the budget, or the team cannot diagnose native failures. A thin C# or native UI shell remains a planned exit, not a rewrite of business rules.

## Solution takeaways {#solution-takeaways}

- Preserve the F# core across framework experiments; do not make the product decision depend on forcing every host into one language.
- Reuse an existing Windows UI when cross-platform reach has no product value.
- Avalonia is a strong first candidate for a named cross-platform desktop scope, subject to native and package evidence.
- Mobile capability and lifecycle paths should decide the mobile shell; shared markup percentage comes later.
- Grow X43 through supervised effects, persistence, accessibility, headless tests, native smoke, per-RID packages, signing, update, and recovery.
- Keep the `-6661` attempt as a failed native row until an interactive macOS run replaces it with new evidence.
- Mobile architecture needs shared Core plus distinct Android/iOS hosts, durable checkpoints, stale-result protection, and idempotent server cooperation.
- A desktop build proves no mobile workload, device, signing, or store path.

[Return to Chapter 43](../part-07/ch-43-avalonia-desktop-mobile).
