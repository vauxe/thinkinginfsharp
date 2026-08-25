---
title: "版本与复现矩阵 / Versions and Reproduction"
description: "本书锁定的工具链、实测环境、生态切片与版本升级协议。 Locked toolchain, observed environment, ecosystem slices, and upgrade protocol for the book."
search: true
outline: deep
---

# 版本与复现矩阵 / Versions and Reproduction

这是一张**证据边界表**，不是“所有更高版本必然兼容”的承诺。日期为 **2026-08-25**。同一行中的四种状态不能互换：

- **锁定基线（locked baseline）：** 锁文件或配置实际选择的版本，自动门禁以它为准。
- **最低要求（minimum requirement）：** 仓库主动拒绝更低版本时声明的下限；它不表示所有更高版本都已验证。
- **实测环境（observed environment）：** 某次验证所在机器的版本，只能说明这次运行。
- **人工目标（manual review target）：** 计划用来做环境验证的精确版本；没有结果时仍是 `not run`。

This is an **evidence-boundary table**, not a promise that every later version is compatible. It was reviewed on **2026-08-25**. The four statuses above—locked baseline, minimum requirement, observed environment, and manual review target—are not interchangeable. A target without evidence remains `not run`.

## 核心工具链 / Core toolchain

| 组件 / Component | 状态 / Status | 本仓库的值 / Repository value | 权威位置与边界 / Authority and boundary |
| --- | --- | --- | --- |
| .NET SDK | 锁定基线 / locked baseline | `10.0.301`, `latestPatch`, no prerelease | `global.json`；只允许同一特征带的后续补丁，不自动跨特征带 |
| F# language | 锁定基线 / locked baseline | `10.0` | `Directory.Build.props` 的 F# 项目 `LangVersion`；不是“编译器默认值” |
| FSharp.Core | 解析结果 / resolved input | 通常 / normally `10.1.301` | 各项目的 `packages.lock.json`；Unity 插件还在项目文件中显式锁定 |
| Node.js | 最低要求 / minimum requirement | `>=22.0.0` | `package.json#engines`；实测版本见下表 |
| pnpm | 精确要求 / exact requirement | `11.7.0` | `package.json#packageManager`、`engines.pnpm` 与 `pnpm-lock.yaml` |
| VitePress | 锁定基线 / locked baseline | `1.6.4` | `package.json` 与 `pnpm-lock.yaml`；生成纯静态站点 |
| markdown-it | 锁定基线 / locked baseline | `15.0.0` | 自定义标题锚点与内容检查的直接依赖 |
| Playwright Core | 锁定基线 / locked baseline | `1.62.1` | 驱动本机 Chrome 的浏览器冒烟；不自动下载浏览器 |
| Fable CLI | 锁定工具 / locked tool | `5.13.0` | `.config/dotnet-tools.json`；由 `dotnet tool restore` 恢复 |
| Fantomas | 锁定工具 / locked tool | `7.0.5` | `.config/dotnet-tools.json`；发布检查使用 `--check`，不隐式改写 |

“通常 `10.1.301`”是锁定项目的解析结果，不是对所有传递依赖的统一覆盖。例如生态包可能带入它们自己的 FSharp.Core 依赖范围；锁文件才是逐项目的完整答案。

“Normally `10.1.301`” describes the resolved result for the locked projects, not a repository-wide override of every transitive dependency. Ecosystem packages can carry their own FSharp.Core ranges; each project lock file is the complete answer.

## 生态切片 / Ecosystem slices

这些版本只证明书中对应的小型切片，不等于整个框架或所有平台的兼容认证。

These versions prove only the corresponding focused samples. They are not certification of an entire framework across every platform.

| 切片 / Slice | 锁定输入 / Locked input | 自动证据 / Automated evidence | 未覆盖边界 / Boundary not covered |
| --- | --- | --- | --- |
| Fable browser | `Fable.Core 5.2.0`, `Fable.Browser.Dom 2.20.0`, `Vite 6.4.3` | F#→JS Release build and DOM smoke | 浏览器矩阵、生产 CDN、SSR |
| Avalonia desktop | `Avalonia 12.1.1` | Restore, Release compile, targeted tests | 原生窗口的逐平台人工交互 |
| .NET Aspire | `Aspire.AppHost.Sdk 13.5.2` and locked packages | AppHost/sample compilation | 云账号、部署、托管服务可用性 |
| Data | `FSharp.Data 8.2.0` | Locked compile and sample/test behavior | 外部数据源长期可用性与任意 schema |
| Benchmarking | `BenchmarkDotNet 0.15.8` | Benchmark project compilation | 一次普通 CI 构建不是可靠性能结论 |
| Unity managed plug-in | `netstandard2.1`, `FSharp.Core 10.1.301` | Release DLL build, pure transition tests, assembly/dependency checks | Editor import, C# adapter, Play Mode, IL2CPP and Player launch |
| Unity environment | `6000.3.22f1` | 人工目标 / manual target only | 本工作区没有 Unity；所有 Unity 环境检查均为 `not run` |

Unity 的可审计状态和复现实验步骤记录在 `reviews/unity-validation.md`。自动化构建出两个 DLL 不能替代 Unity Editor 或 Player 的结果。

The auditable Unity status and reproduction procedure live in `reviews/unity-validation.md`. Producing the two DLLs in automation cannot substitute for Unity Editor or Player evidence.

## 2026-08-25 实测环境 / Observed environment

| 项目 / Item | 实测值 / Observed value |
| --- | --- |
| OS | macOS `26.3` (`25D125`), Darwin `25.3.0`, arm64 |
| .NET SDK | `10.0.301` |
| F# Interactive | `15.2.301.0 for F# 10.0` |
| Node.js | `26.4.0` |
| pnpm | `11.7.0` |
| Google Chrome | `151.0.7922.174` |
| Unity | Not installed; `/Applications/Unity/Hub/Editor` absent |

这里的 Node 与 Chrome 版本只是运行记录，不会悄悄把最低要求抬到这些版本。另一台满足声明前置条件的机器如果失败，应记录为兼容性发现，而不是修改记录来掩盖失败。

The observed Node and Chrome versions are run metadata, not silent increases to the minimum requirements. If another machine that satisfies the declared prerequisites fails, record a compatibility finding instead of editing the observation to conceal it.

## 可复现安装 / Reproducible installation

在仓库根目录执行：

Run from the repository root:

```console
pnpm install --frozen-lockfile
dotnet tool restore
dotnet restore ThinkingInFSharp.slnx --locked-mode
pnpm test
dotnet fantomas . --check
pnpm check:capstone
```

`--frozen-lockfile` 与 `--locked-mode` 的意义是：当声明与锁文件不一致时失败，而不是现场选择新依赖。`pnpm test` 已包含书稿契约、示例、Fable、站点构建、内部链接与搜索检查；capstone 独立运行，因为它会临时启动本地 HTTP 进程。

The two lock-enforcing flags fail when declarations and lock files disagree instead of selecting new dependencies during verification. `pnpm test` includes book contracts, examples, Fable, site generation, internal links, and search checks. The capstone stays separate because it starts a temporary local HTTP process.

## 升级协议 / Upgrade protocol

一次版本升级必须形成一个可回滚、能回答“改了什么证据边界”的变更：

1. **选择候选。** 从官方发行说明、支持策略或包元数据选择一个精确版本；记录复核日期，不使用无界的 `latest`。
2. **更新权威输入。** 根据组件修改 `global.json`、`package.json`、`.config/dotnet-tools.json` 或项目文件；让包管理器正常更新相应锁文件，不手工拼接锁文件。
3. **检查解析结果。** 执行冻结的 pnpm 安装和 .NET locked restore，确认没有意外的特征带、预发布包、重复 `FSharp.Core` 或平台目标变化。
4. **扩大验证。** 依次运行内容测试、双语/内容门禁、示例、Fantomas、站点构建与冒烟、capstone。版本影响浏览器、桌面、Unity 或云边界时，还要使用相应人工记录。
5. **更新书稿。** 同步两种语言的 `verifiedWith`、版本说明、限制和来源复核日期；只有实际重新阅读或运行后才能更新日期或状态。
6. **记录与回退。** 在审阅记录中写候选版本、命令、环境、失败与残余风险。失败时回退声明和它生成的锁文件；不要保留一个未经证明的半升级状态。

Every upgrade must be a reversible change that explains its evidence boundary:

1. **Select an exact candidate** from official release notes, support policy, or package metadata; record the review date and avoid an unbounded `latest`.
2. **Change the authoritative input**—`global.json`, `package.json`, `.config/dotnet-tools.json`, or a project file—and let the package manager regenerate the relevant lock data.
3. **Inspect resolution** with a frozen pnpm install and locked .NET restore. Reject unintended feature-band, prerelease, duplicate FSharp.Core, or target-framework changes.
4. **Widen verification** through content tests, bilingual/content gates, examples, Fantomas, site build/smoke, and capstone. Add the relevant manual record when browser, desktop, Unity, or cloud boundaries change.
5. **Update both editions**: `verifiedWith`, version wording, limitations, and source review dates. Change a date or status only after the review or run actually happened.
6. **Record and roll back** the candidate, commands, environment, failures, and residual risk. Revert declarations and their generated lock files on failure; do not retain an unproved half-upgrade.

## 权威入口 / Primary references

- [.NET SDK `global.json` overview](https://learn.microsoft.com/en-us/dotnet/core/tools/global-json)
- [.NET 10 downloads and SDK builds](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
- [What is new in F# 10](https://learn.microsoft.com/en-us/dotnet/fsharp/whats-new/fsharp-10)
- [Node.js release lines](https://nodejs.org/en/about/previous-releases)
- [pnpm `packageManager` settings](https://pnpm.io/settings#packagemanager)
- [VitePress documentation](https://vitepress.dev/)
- [Fable documentation](https://fable.io/docs/)
- [Avalonia documentation](https://docs.avaloniaui.net/)
- [.NET Aspire documentation](https://learn.microsoft.com/en-us/dotnet/aspire/)
- [Unity 6000.3.22f1 release notes](https://unity.com/releases/editor/whats-new/6000.3.22f1)
- [Unity .NET profile support](https://docs.unity3d.com/Manual/dotnet-profile-support.html)
- [FSharp.Core 10.1.301 package metadata](https://www.nuget.org/packages/FSharp.Core/10.1.301)
