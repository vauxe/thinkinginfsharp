---
title: "附录 A：配置 F# 环境"
description: "安装 .NET SDK，启动 F# Interactive，运行脚本，并创建第一个项目。"
translationKey: appendices/a-setup
---

# 附录 A：配置 F# 环境 {#overview}

阅读本站不需要安装任何东西。要运行示例，请安装 .NET SDK；它包含 F# 编译器、F# Interactive 和 `dotnet` 命令。

## 1. 安装受支持的 .NET SDK {#install-sdk}

从 [.NET 下载页](https://dotnet.microsoft.com/zh-cn/download)安装仍受支持的 SDK。只安装运行时不够。

安装后打开一个新终端并运行：

```console
dotnet --info
dotnet fsi --help
```

两条命令都能运行，就说明工具链已经可用。本书示例以 F# 10 和 .NET 10 复核；耐久的语言基础也适用于后续兼容版本，但诊断文字可能变化。

## 2. 尝试 F# Interactive {#fsi}

启动交互提示符：

```console
dotnet fsi
```

输入下面的表达式，并用 `;;` 结束这次提交：

```fsharp
20 + 22;;
```

FSI 应报告值 `42` 和类型 `int`。输入 `#quit;;` 退出。

## 3. 运行脚本 {#script}

创建 `lesson.fsx`，内容如下：

```fsharp
let greet name = $"Hello, {name}!"
printfn "%s" (greet "F#")
```

在文件所在目录运行：

```console
dotnet fsi --exec lesson.fsx
```

脚本是保存并重复运行小实验的最简单方式。前几章的大多数示例都可以这样复制到 `.fsx` 文件中执行。

## 4. 创建项目 {#project}

代码需要多个文件、包、测试或发布时，改用项目：

```console
dotnet new console -lang "F#" -o HelloFSharp
dotnet run --project HelloFSharp
```

生成的 `.fsproj` 会记录目标框架和源文件顺序。F# 按项目中的顺序编译文件，因此定义必须出现在使用它的文件之前。

## 只在有帮助时选择编辑器 {#editor}

任何文本编辑器都可以。支持 F# 的编辑器会提供类型信息、补全、跳转、格式化和诊断，但不能代替命令行构建。始终保持终端命令可用，项目才不会依赖某一个编辑器。

## 常见环境问题 {#troubleshooting}

- **找不到 `dotnet`：** 打开新终端，并确认 SDK 安装目录位于 `PATH` 中。
- **只列出了运行时：** 从下载页安装 SDK。
- **FSI 一直等待：** 补齐当前表达式、字符串或括号；交互式提交以 `;;` 结束。
- **项目无法还原包：** 检查网络和已配置的 NuGet 源，再重试 `dotnet restore`。
- **诊断与书中不同：** 比较 `dotnet --version`；不同 SDK 的编译器措辞和诊断细节可能变化。

确认 `dotnet fsi` 可用后，继续阅读[第 1 章](../part-01/ch-01-first-session)。

## 来源 {#sources}

- [.NET 下载](https://dotnet.microsoft.com/zh-cn/download)
- [Microsoft Learn：从命令行开始使用 F#](https://learn.microsoft.com/zh-cn/dotnet/fsharp/get-started/get-started-command-line)
- [Microsoft Learn：F# Interactive](https://learn.microsoft.com/zh-cn/dotnet/fsharp/tools/fsharp-interactive/)
- [Microsoft Learn：F# 编译器选项与项目文件](https://learn.microsoft.com/zh-cn/dotnet/fsharp/language-reference/compiler-options)
