---
title: "附录 A：跨平台环境配置"
description: "在 Windows、macOS 或 Linux 上安装并验证最小 F# 开发环境，再以显式机器边界诊断 SDK、架构、编辑器与还原问题。"
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

# 附录 A：跨平台环境配置 {#overview}

F# 随 .NET SDK 提供。完成本书核心内容不需要单独的语言运行时、云账号、容器引擎或指定编辑器。先安装 SDK，证明命令行选中了预期版本；只有真实问题需要时，再加入编辑器或平台 workload。

本附录以 F# 10 与 .NET 10 为目标。安装页面和受支持操作系统会变化，因此链接与版本观察均在 2026-08-25 复核。为新机器安装前，应再次检查官方页面。

## 按活动选择环境 {#environment-contract}

| 活动 | 必需 | 可选或稍后再装 |
|---|---|---|
| 阅读静态书 | 当前浏览器 | 无 |
| 学习第 1–37 章并运行普通 `.fsx` 文件 | .NET 10 SDK | Git、编辑器 |
| 运行第 38 章的自动验收命令 `pnpm check:capstone` | .NET 10 SDK、Node.js 22+、pnpm 11.7，以及冻结的工作区安装 | 本地缓存为空时需要访问公共 NuGet 源 |
| 用语言服务编辑 F# | .NET 10 SDK 加支持 F# 的编辑器 | 调试器集成 |
| 构建本文档仓库 | .NET 10 SDK、Git、Node.js 22+、pnpm 11.7 | 用于浏览器冒烟的系统 Chrome |
| 运行 Fable 样例与完整仓库门禁 | 上述仓库工具 | 与 Chrome 兼容的浏览器自动化 |
| 面向移动端、Unity、原生打包或云厂商 | 对应章节的平台 SDK/工具链 | 只有该目标确实需要时才加入账号和设备 |

**SDK** 能编译、还原、测试和发布，并包含 F# Interactive 以及对应的 .NET 与 ASP.NET Core 运行时。仅运行时安装可以执行兼容应用，却不能创建或编译练习。学习时应安装 SDK。

额外 workload 并不代表环境更完整。本书核心内容不需要安装任何 `dotnet workload`。Android、iOS 等基于 workload 的目标应属于自己的平台项目与证据矩阵。

## 安装前先检查 {#inspect-first}

若已有 `dotnet`，请在新终端中运行：

```console
dotnet --version
dotnet --list-sdks
dotnet --info
```

`--version` 输出当前目录选中的 SDK，并不一定是已安装 SDK 中数字最大的一个。`--list-sdks` 列出当前 `dotnet` 可见的安装。`--info` 还会给出操作系统、架构、基础路径、运行时、workload、环境变量和发现的 `global.json`。

从仓库根目录再次运行同一命令。工作目录很重要，因为 .NET 宿主会向父目录查找 `global.json`。

```console
dotnet --version
```

本版精确选择 SDK `10.0.301`。若未安装该 SDK，仓库会报告版本选择错误，而不会静默改变编译器提供的依赖。

若没有 `dotnet`，请按下面的操作系统和 CPU 架构安装。若它存在却报告错误架构或安装根，先理解冲突，再加入另一个副本。

## Windows {#windows}

使用官方 .NET 10 SDK 安装器或 Windows Package Manager。在 PowerShell 或 Windows Terminal 中，当前包标识为：

```powershell
winget install Microsoft.DotNet.SDK.10
```

安装 SDK 会同时安装其运行时；不必为了编译 F# 再单独安装同版本运行时。多数 Intel/AMD Windows 机器选择 x64，Windows on Arm 选择 Arm64。不要从安装器文件名猜测结果，应实际确认：

```powershell
where.exe dotnet
dotnet --info
dotnet --list-sdks
```

多个 SDK 可以共存。32 位和 64 位宿主看到按架构区分的安装，因此 `PATH` 前部的旧 `dotnet.exe` 可能让有效 SDK 看似缺失。

Visual Studio 是可选且仅限 Windows 的选择。使用 .NET 10 时，应选择官方支持该 SDK 的 Visual Studio 版本，并通过相应 .NET workload 或单独组件加入 F# 支持。命令行构建仍是可移植的事实来源。

## macOS {#macos}

官方签名安装器是最简单的默认选择。请按机器架构下载 .NET 10 **SDK**：

- Apple 芯片：Arm64；
- Intel Mac：x64。

检查硬件与当前宿主：

```console
uname -m
which -a dotnet
dotnet --info
```

在 Apple 芯片上，只有某个 x64 工具链确实需要时才安装 x64。通过 Rosetta 运行 x64 宿主，并不能证明 Arm64 原生依赖或应用可用。`dotnet --info` 应报告当前调用使用的架构。

包管理器、编辑器管理、脚本与官方安装器的副本可能使用不同根目录。在能解释 shell 和编辑器分别启动哪个 `dotnet` 前，不要叠加安装方式。系统安装后应重启终端和编辑器。

Visual Studio for Mac 已退役，不是当前 F# 环境建议。请使用 CLI 搭配 VS Code/Ionide、Rider，或另一款能证明成功加载项目的编辑器。

## Linux {#linux}

先识别发行版、版本与架构：

```console
cat /etc/os-release
uname -m
```

然后按链接中的 Microsoft 页面选择该发行版的精确说明。一些发行版自行发布 .NET 包，另一些使用 Microsoft 包仓库。不要把 Ubuntu 源配置复制到衍生或无关发行版后，就假定它受支持。

在配置的软件源提供 .NET 10 的受支持 Ubuntu 版本上，SDK 包为：

```console
sudo apt-get update
sudo apt-get install -y dotnet-sdk-10.0
```

这是 Ubuntu 示例，不是通用 Linux 命令。Fedora、RHEL、Alpine、Debian、SUSE 等系统有不同的包所有权与支持矩阵。手动压缩包或安装脚本还要求你自行管理原生前置项、安装根、`PATH`、更新和移除；它通常更适合 CI 或隔离安装，而不是首台开发工作站。

安装后打开新 shell，运行 `command -v dotnet`、`dotnet --info` 和 `dotnet --list-sdks`。不要用 `sudo` 执行普通还原或构建，否则很容易在项目或用户包缓存中留下 root 所有的文件。

## 选择编辑器，但不让它成为构建依赖 {#editors}

### VS Code 与 Ionide {#vscode-ionide}

VS Code 加 `Ionide-fsharp` 扩展是 Microsoft F# 入门指南采用的跨平台基线。自行安装 .NET SDK，再安装 Ionide，打开包含项目的**文件夹**并把文件保存到磁盘。孤立且未保存的缓冲区可能不会初始化语言服务。

Ionide 提供项目加载、补全、导航、诊断、重构、FSI 集成与调试连接。它不会取代 `dotnet build`。编辑器与 CLI 结论不一致时，先记录两者选择的 SDK 和 Ionide 输出日志。

### Visual Studio 与 Rider {#visual-studio-rider}

Visual Studio 在 Windows 上支持 F#。它能使用哪些 SDK 取决于精确 Visual Studio 版本，因此升级时要检查官方兼容表。

JetBrains Rider 是跨平台商业替代方案。其 F# Support 插件默认捆绑并启用，处理 `.fs`、`.fsi`、`.fsx` 以及 F#/C# 混合解决方案。已安装的 .NET SDK 仍是前置条件。

编辑器选择不会改变 F# 语义、文件顺序、项目引用、锁文件或目标框架。每个重要结果都应保留可复现的 CLI 命令。

## 证明第一个垂直切片 {#first-slice}

在空工作目录创建并运行控制台项目：

```console
mkdir first-fsharp
cd first-fsharp
dotnet new console --language F#
dotnet run
```

模板应构建并输出：

```text
Hello from F#
```

然后启动 REPL：

```console
dotnet fsi
```

在提示符输入 `1 + 2;;`。推断的类型和值应包含 `val it: int = 3`。用 `#quit;;` 退出。

这个切片证明模板发现、还原、F# 编译、运行时选择和进程执行。它不证明 IDE、调试器、Web 证书、数据库、移动 workload、Unity Editor 或部署目标。

## 理解本仓库的版本边界 {#repository-boundary}

根目录的 `global.json` 为：

```json
{
  "sdk": {
    "version": "10.0.301",
    "rollForward": "disable",
    "allowPrerelease": false
  }
}
```

`disable` 要求精确 SDK，使 SDK 提供的依赖与已提交包锁保持一致。升级 SDK 时应有意修改版本、重新生成受影响的锁文件并重跑相应证据；`allowPrerelease: false` 还会排除预览 SDK。

`net10.0` 等目标框架回答的是另一问题：项目针对哪个 API/运行时契约编译。`global.json` 选择 SDK 工具链。一台机器可以并列安装多个 SDK 与运行时。

阅读和运行普通 F# 章节只需要 SDK。第 38 章的标准自动验收命令还会调用仓库的 JavaScript 运行器，因此需要声明的 Node.js、pnpm 版本与冻结安装。维护整套静态站也使用同一工具链。Fable 会加入锁定的 .NET tool 与 npm 依赖图，浏览器测试还需要可用的兼容浏览器。这些是仓库自动化要求，不是理解函数、记录、联合或工作流的前置知识。

## 分层诊断 {#troubleshooting}

### Shell 找不到 `dotnet` {#missing-command}

打开新终端。在 Windows 用 `where.exe dotnet`，macOS/Linux 用 `which -a dotnet` 或 `command -v dotnet`。若安装成功但可执行文件不在 `PATH`，请按该安装器的故障排查页修复，不要复制无关的 `DOTNET_ROOT` 值。

### 已安装所需 SDK，但没有被选中 {#sdk-selection}

在仓库根目录比较 `dotnet --list-sdks` 与 `dotnet --version`。检查 `dotnet --info` 打印的 `global.json` 路径。父目录文件可能影响无关实验；仓库文件也可能有意拒绝另一功能带。

应安装兼容 SDK，而不是为了消除错误就修改版本策略。只有作为经过评审的仓库升级，并跑过全量测试和锁文件审查时，才改 `global.json`。

### 架构不正确 {#architecture}

记录操作系统架构以及 `dotnet --info` 中的宿主架构。在 .NET 10 上，`dotnet --list-sdks --arch arm64` 或 `--arch x64` 可以查询另一已安装架构。包的原生资产、模拟和目标 RID 仍需自己的测试。

### 还原失败 {#restore-failure}

区分四类原因：SDK 无法解析项目；包源不可达；凭据/代理/TLS 失败；解析图与已提交锁不一致。保留第一条有用错误。不要把删除 `packages.lock.json` 或加入不可信源当作通用修复。

本仓库应通过已记录的质量命令以锁定模式还原。锁定失败说明输入或策略发生了变化，并不意味着可以静默重建。

### 编辑器报错，但 CLI 能构建 {#editor-failure}

确认编辑器打开了仓库文件夹、加载了预期解决方案并选择相同 SDK。保存文件，安装 SDK 后重启编辑器，查看 F# 语言服务日志，并把失败缩小到一个项目。只有 `dotnet build` 证明项目状态后，才把陈旧诊断判定为编辑器状态问题。

### 缺少 workload 或 HTTPS 证书 {#optional-platform-state}

先问当前章节是否需要。控制台、类库、测试和大多数服务端编译路径不需要移动 workload。开发 HTTPS 证书只与选定的本地 Web 路径有关，不属于 F# 本身。只有样例的证据契约明确要求时，才安装或信任平台状态。

## 记录足够的求助证据 {#support-evidence}

应包含：

- 操作系统版本和 CPU 架构；
- `dotnet --info` 与 `dotnet --list-sdks` 输出；
- 当前目录和发现的 `global.json` 路径；
- 精确命令、第一条相关错误与退出状态；
- CLI、编辑器还是两者都失败；
- 项目/目标框架，以及还原是否使用锁文件；
- 涉及的代理、包源、workload、模拟器、容器或原生工具链边界——但不包含机密。

“F# 不能用”混合了过多层次。“Arm64 上选中 SDK 10.0.301；锁定还原成功；Ionide 无法加载此项目并报告如下路径/错误”才给别人一个可证伪的起点。

## 本版验证记录 {#verification-record}

2026-08-25，仓库命令在 macOS 26.3 Arm64 上执行。`dotnet --info` 选中 SDK 10.0.301 与 F# Interactive 10.0；SDK 9.0.315 也并列存在。没有安装额外 .NET workload。一个全新的临时 F# 控制台项目成功创建并执行，输出 `Hello from F#`，随后已移除。

Windows 与 Linux 安装命令依据上面的官方页面审阅，并未在这台 Mac 上执行。Visual Studio 与 Rider 同样只做资料审阅。这才是正确的证据边界：一个当前平台经过执行；其他平台有来源明确的说明，仍需各自机器级验证。

接下来进入[第 1 章](../part-01/ch-01-first-session)开始第一次语言会话。项目/文件顺序成为问题时查阅[第 16 章](../part-03/ch-16-modules-namespaces-projects)，可复现构建与诊断策略则见[第 30 章](../part-05/ch-30-diagnostics-tooling-builds)。

## 官方入口 {#official-entry-points}

- [在 Windows 上安装 .NET](https://learn.microsoft.com/en-us/dotnet/core/install/windows)
- [在 macOS 上安装 .NET](https://learn.microsoft.com/en-us/dotnet/core/install/macos)
- [在 Linux 上安装 .NET](https://learn.microsoft.com/en-us/dotnet/core/install/linux)
- [在 Ubuntu 上安装 .NET](https://learn.microsoft.com/en-us/dotnet/core/install/linux-ubuntu-install)
- [检测已安装 SDK 与运行时](https://learn.microsoft.com/en-us/dotnet/core/install/how-to-detect-installed-versions)
- [.NET CLI 参考](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet)
- [`global.json` 概览](https://learn.microsoft.com/en-us/dotnet/core/tools/global-json)
- [使用 VS Code 与 Ionide 开始 F#](https://learn.microsoft.com/en-us/dotnet/fsharp/get-started/get-started-vscode)
- [使用 Visual Studio 开始 F#](https://learn.microsoft.com/en-us/dotnet/fsharp/get-started/get-started-visual-studio)
- [Rider F# 支持](https://www.jetbrains.com/help/rider/F_Sharp.html)
