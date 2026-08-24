---
title: "Appendix A: Cross-Platform Setup"
description: "Install and verify the smallest F# development environment on Windows, macOS, or Linux, then diagnose SDK, architecture, editor, and restore problems without hidden machine assumptions."
translationKey: appendices/a-setup
kind: appendix
appendix: A
status: complete
verifiedWith:
  fsharp: "10"
  dotnetSdk: "10.0.301"
exampleIds: []
exerciseIds: []
termIds: []
sources:
  - id: microsoft-dotnet-install-windows
    url: https://learn.microsoft.com/en-us/dotnet/core/install/windows
    checked: "2026-08-25"
  - id: microsoft-dotnet-install-macos
    url: https://learn.microsoft.com/en-us/dotnet/core/install/macos
    checked: "2026-08-25"
  - id: microsoft-dotnet-install-linux
    url: https://learn.microsoft.com/en-us/dotnet/core/install/linux
    checked: "2026-08-25"
  - id: microsoft-dotnet-install-ubuntu
    url: https://learn.microsoft.com/en-us/dotnet/core/install/linux-ubuntu-install
    checked: "2026-08-25"
  - id: microsoft-detect-dotnet
    url: https://learn.microsoft.com/en-us/dotnet/core/install/how-to-detect-installed-versions
    checked: "2026-08-25"
  - id: microsoft-dotnet-cli
    url: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet
    checked: "2026-08-25"
  - id: microsoft-global-json
    url: https://learn.microsoft.com/en-us/dotnet/core/tools/global-json
    checked: "2026-08-25"
  - id: microsoft-fsharp-vscode
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/get-started/get-started-vscode
    checked: "2026-08-25"
  - id: microsoft-fsharp-visual-studio
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/get-started/get-started-visual-studio
    checked: "2026-08-25"
  - id: jetbrains-rider-fsharp
    url: https://www.jetbrains.com/help/rider/F_Sharp.html
    checked: "2026-08-25"
---

# Appendix A: Cross-Platform Setup {#overview}

F# ships with the .NET SDK. You do not need a separate language runtime, a cloud account, a container engine, or a particular editor to work through the core of this book. Install the SDK, prove that the command line selects the intended version, and only then add an editor or platform workload that solves a real need.

This appendix targets F# 10 and .NET 10. Installation pages and supported operating-system versions change, so the links and version observations were checked on 2026-08-25. Recheck the official page before installing on a new machine.

## Choose the environment from the activity {#environment-contract}

| Activity | Required | Optional or later |
|---|---|---|
| read the static book | a current browser | nothing else |
| run Chapters 1–38 and ordinary `.fsx` files | .NET 10 SDK | Git, an editor |
| edit F# with language services | .NET 10 SDK plus an F#-capable editor | debugger integrations |
| build this documentation repository | .NET 10 SDK, Git, Node.js 22+, pnpm 11.7 | a system Chrome for browser smoke |
| run Fable sample and the full repository gate | preceding repository tools | Chrome-compatible browser automation |
| target mobile, Unity, native packaging, or a cloud provider | the relevant chapter's platform SDK/toolchain | accounts and devices only when that target truly needs them |

The **SDK** compiles, restores, tests, publishes, and includes F# Interactive plus the corresponding .NET and ASP.NET Core runtimes. A runtime-only installation can execute a compatible application but cannot create or compile the exercises. Install the SDK for learning.

Extra workloads are not a measure of completeness. The core book needs no `dotnet workload` installation. Android, iOS, and other workload-based targets belong to their own platform project and evidence matrix.

## Inspect before installing {#inspect-first}

If `dotnet` already exists, run these commands in a new terminal:

```console
dotnet --version
dotnet --list-sdks
dotnet --info
```

`--version` prints the SDK selected for the current directory, not necessarily the numerically highest installed SDK. `--list-sdks` shows installations visible to that `dotnet` executable. `--info` adds operating system, architecture, base path, runtimes, workloads, environment variables, and any discovered `global.json`.

Run the same command from the repository root. The working directory matters because the .NET host searches upward for `global.json`.

```console
dotnet --version
```

For this edition, the expected selected feature band is `10.0.3xx`; the checked machine selected `10.0.301`. A different patch in the same allowed band may be correct. A 9.x SDK or a later 10.0 feature band is not selected by this repository's policy.

If `dotnet` is absent, install for the operating system and CPU architecture below. If it exists but reports the wrong architecture or installation root, understand that conflict before adding another copy.

## Windows {#windows}

Use the official .NET 10 SDK installer or Windows Package Manager. In PowerShell or Windows Terminal, the current package identifier is:

```powershell
winget install Microsoft.DotNet.SDK.10
```

Installing the SDK also installs its runtimes; do not separately install the same runtime just to compile F#. Choose x64 on most Intel/AMD Windows machines and Arm64 on Windows on Arm. Confirm the result rather than inferring it from the installer filename:

```powershell
where.exe dotnet
dotnet --info
dotnet --list-sdks
```

Multiple SDKs may coexist. A 32-bit and 64-bit host see architecture-specific installations, so an old `dotnet.exe` earlier on `PATH` can make a valid SDK appear missing.

Visual Studio is optional and Windows-only. For .NET 10, use a Visual Studio release that officially supports that SDK and include F# support through the relevant .NET workload or individual component. The command-line build remains the portable source of truth.

## macOS {#macos}

The official signed installer is the simplest default. Download the .NET 10 **SDK** for the machine architecture:

- Apple silicon: Arm64;
- Intel Mac: x64.

Check the hardware and selected host:

```console
uname -m
which -a dotnet
dotnet --info
```

On Apple silicon, install x64 only when a particular x64 toolchain requires it. Running an x64 host through Rosetta is not evidence that an Arm64-native dependency or application works. `dotnet --info` should report the architecture used by the current invocation.

Package-manager, editor-managed, script, and official-installer copies can use different roots. Avoid stacking methods until you can explain which `dotnet` the shell and editor each launch. Restart the terminal and editor after a system installation.

Visual Studio for Mac is retired and is not a current F# setup recommendation. Use the CLI with VS Code/Ionide, Rider, or another editor that proves it can load the project.

## Linux {#linux}

First identify the distribution, version, and architecture:

```console
cat /etc/os-release
uname -m
```

Then follow the linked Microsoft page for that exact distribution. Some distributions publish .NET packages themselves; others use Microsoft's package repository. Do not paste Ubuntu feed instructions into a derived or unrelated distribution and assume support.

On a supported Ubuntu release whose configured feed supplies .NET 10, the SDK package is:

```console
sudo apt-get update
sudo apt-get install -y dotnet-sdk-10.0
```

That command is an Ubuntu example, not a universal Linux command. Fedora, RHEL, Alpine, Debian, SUSE, and other systems have distinct package ownership and support matrices. A manual archive or install script also requires you to manage native prerequisites, installation root, `PATH`, updates, and removal; it is usually better suited to CI or an isolated installation than a first workstation setup.

After installation, open a new shell and run `command -v dotnet`, `dotnet --info`, and `dotnet --list-sdks`. Do not run ordinary restore or build commands with `sudo`; that commonly leaves root-owned files in the project or user package cache.

## Choose an editor without making it a build dependency {#editors}

### VS Code and Ionide {#vscode-ionide}

VS Code plus the `Ionide-fsharp` extension is the cross-platform baseline used by Microsoft's F# getting-started guide. Install the .NET SDK yourself, install Ionide, open the **folder** containing the project, and save files to disk. Language services may not initialize for an isolated unsaved buffer.

Ionide provides project loading, completion, navigation, diagnostics, refactoring, FSI integration, and debugging connections. It does not replace `dotnet build`. When the editor disagrees with the CLI, first capture both SDK selections and the Ionide output log.

### Visual Studio and Rider {#visual-studio-rider}

Visual Studio provides F# support on Windows. Its supported SDK range depends on the exact Visual Studio version, so check the official compatibility table during upgrades.

JetBrains Rider is a cross-platform commercial alternative. Its F# Support plugin is bundled and enabled by default, and it handles `.fs`, `.fsi`, and `.fsx` files plus mixed F#/C# solutions. The installed .NET SDK is still a prerequisite.

Editor choice does not change F# semantics, file order, project references, lock files, or target frameworks. Keep a reproducible CLI command for every result that matters.

## Prove the first vertical slice {#first-slice}

In an empty working directory, create and run a console project:

```console
mkdir first-fsharp
cd first-fsharp
dotnet new console --language F#
dotnet run
```

The template should build and print:

```text
Hello from F#
```

Then start the REPL:

```console
dotnet fsi
```

At its prompt, submit `1 + 2;;`. The inferred type and value should include `val it: int = 3`. Exit with `#quit;;`.

This slice proves template discovery, restore, F# compilation, runtime selection, and process execution. It does not prove an IDE, debugger, Web certificate, database, mobile workload, Unity Editor, or deployment target.

## Understand this repository's version boundary {#repository-boundary}

The root `global.json` contains:

```json
{
  "sdk": {
    "version": "10.0.301",
    "rollForward": "latestPatch",
    "allowPrerelease": false
  }
}
```

`latestPatch` accepts an installed patch at or above 10.0.301 **within the 10.0.3xx feature band**. It does not mean “any newer .NET SDK.” `allowPrerelease: false` prevents a preview SDK from silently satisfying the selection.

Target frameworks such as `net10.0` answer a different question: which API/runtime contract a project compiles against. `global.json` selects the SDK toolchain. A machine can have several SDKs and runtimes side by side.

To read and run the ordinary F# chapters, the SDK is sufficient. To maintain the whole static site, also use the Node.js and pnpm versions declared by `package.json`, then perform a frozen install. Fable adds its locked .NET tool and npm graph. Browser tests require an available compatible browser. These are repository contribution requirements, not prerequisites for understanding functions, records, unions, or workflows.

## Diagnose by layer {#troubleshooting}

### The shell cannot find `dotnet` {#missing-command}

Open a new terminal. Use `where.exe dotnet` on Windows or `which -a dotnet`/`command -v dotnet` on macOS and Linux. If installation succeeded but the executable is absent from `PATH`, follow the installer-specific troubleshooting page rather than copying an unrelated `DOTNET_ROOT` value.

### The required SDK is installed but not selected {#sdk-selection}

Compare `dotnet --list-sdks` with `dotnet --version` from the repository root. Inspect the `global.json` path printed by `dotnet --info`. A parent-directory file can affect an unrelated experiment; a repository file can intentionally reject an otherwise installed feature band.

Install a compatible SDK rather than editing version policy merely to make an error disappear. Change `global.json` only as a reviewed repository upgrade with full tests and lock-file review.

### The architecture is wrong {#architecture}

Record OS architecture and the host architecture from `dotnet --info`. On .NET 10, `dotnet --list-sdks --arch arm64` or `--arch x64` can query another installed architecture. Package native assets, emulation, and target RID still require their own tests.

### Restore fails {#restore-failure}

Separate four causes: the SDK cannot resolve the project; a package source is unreachable; credentials/proxy/TLS fail; or the resolved graph conflicts with the committed lock. Preserve the first useful error. Do not delete `packages.lock.json` or add an untrusted feed as a generic repair.

For this repository, restore in locked mode through the documented quality commands. A locked failure is evidence that inputs or policy changed; it is not an invitation to regenerate silently.

### The editor shows errors but the CLI builds {#editor-failure}

Confirm that the editor opened the repository folder, loaded the intended solution, and selected the same SDK. Save the file, restart the editor after installing the SDK, inspect the F# language-service log, and reduce the failure to one project. Treat stale editor diagnostics as an editor-state problem only after `dotnet build` proves the project state.

### A workload or HTTPS certificate is missing {#optional-platform-state}

Ask whether the current chapter needs it. Console, library, test, and most server compilation paths do not require mobile workloads. A development HTTPS certificate is relevant to a chosen local Web path, not to F# itself. Install or trust platform state only for a sample whose evidence contract names it.

## Record enough evidence to ask for help {#support-evidence}

Include:

- operating system version and CPU architecture;
- `dotnet --info` and `dotnet --list-sdks` output;
- current directory and the discovered `global.json` path;
- exact command, first relevant error, and exit status;
- whether the CLI, editor, or both fail;
- project/target framework and whether restore uses a lock file;
- any proxy, package-source, workload, emulator, container, or native-toolchain boundary involved—without secrets.

“F# does not work” merges too many layers. “SDK 10.0.301 is selected on Arm64; locked restore succeeds; Ionide fails to load this project and reports this path/error” gives another person a falsifiable starting point.

## Verification record for this edition {#verification-record}

On 2026-08-25, the repository commands were run on macOS 26.3 Arm64. `dotnet --info` selected SDK 10.0.301 and F# Interactive 10.0; SDK 9.0.315 also coexisted. No extra .NET workloads were installed. A fresh temporary F# console project was created and executed successfully, printing `Hello from F#`, then removed.

Windows and Linux installation commands were reviewed against the official pages above, not executed on this Mac. Visual Studio and Rider were likewise documentation-reviewed. That is the correct evidence boundary: one current platform was exercised; other platforms have sourced instructions and still need their own machine-level verification.

Continue with [Chapter 1](../part-01/ch-01-first-session) for the first language session. Use [Chapter 16](../part-03/ch-16-modules-namespaces-projects) when project/file order becomes the problem, and [Chapter 30](../part-05/ch-30-diagnostics-tooling-builds) for reproducible build and diagnostic policy.

## Official entry points {#official-entry-points}

- [Install .NET on Windows](https://learn.microsoft.com/en-us/dotnet/core/install/windows)
- [Install .NET on macOS](https://learn.microsoft.com/en-us/dotnet/core/install/macos)
- [Install .NET on Linux](https://learn.microsoft.com/en-us/dotnet/core/install/linux)
- [Install .NET on Ubuntu](https://learn.microsoft.com/en-us/dotnet/core/install/linux-ubuntu-install)
- [Detect installed SDKs and runtimes](https://learn.microsoft.com/en-us/dotnet/core/install/how-to-detect-installed-versions)
- [.NET CLI reference](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet)
- [`global.json` overview](https://learn.microsoft.com/en-us/dotnet/core/tools/global-json)
- [Get started with F# in VS Code and Ionide](https://learn.microsoft.com/en-us/dotnet/fsharp/get-started/get-started-vscode)
- [Get started with F# in Visual Studio](https://learn.microsoft.com/en-us/dotnet/fsharp/get-started/get-started-visual-studio)
- [Rider F# support](https://www.jetbrains.com/help/rider/F_Sharp.html)
