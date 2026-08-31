---
title: "第 45 章：脚本、自动化、包生态与继续学习"
description: "学习何时使用 F# 脚本、如何让自动化可以重复执行，以及怎样选择包并继续练习 F#。"
translationKey: part-07/ch-45-scripting-packages-next
---

# 第 45 章：脚本、自动化、包生态与继续学习 {#overview}

F# 脚本和普通 F# 项目使用同一种语言、FSharp.Core 和 .NET 运行时。脚本省去了一部分项目配置，所以 `.fsx` 很适合快速试验、仓库维护、数据修复、发布检查和小型本地工具。

但脚本短，并不代表它没有接口。脚本可能依赖先前的交互会话、当前工作目录、包源、文件遍历顺序或含糊的退出码，也可能每次运行都修改文件。一旦其他人或 CI 开始调用它，这些行为就必须保持清楚和稳定。

本章先分析一份真实脚本如何做到可重复运行，再说明怎样选择和锁定包，以及什么时候应把脚本迁移成项目或工具。最后给出一条继续练习 F# 的路线。

## 选择能保留契约的最简单执行方式 {#execution-surface}

正确的执行方式是能让所需行为可重复的最简单方式。这里的“简单”指需要管理的运行条件少，而不只是文件数量少。

术语分成三类：

- FSI、`.fsx` 和 F# Interactive 属于 F# 工具链；
- NuGet、PackageReference、目标框架和 .NET 工具属于 .NET 平台；
- CLI、锁文件、幂等性和供应链是通用工程概念。

::: tip 第一次阅读
初学者先阅读[执行方式](#execution-surface)、[FSI 的执行模型](#fsi-model)和[继续学习](#learning-next)。需要编写仓库自动化或引入 NuGet 包时，再阅读清单脚本、包选择和供应链部分。
:::

| 执行方式 | 最适合的首次用途 | 可复现范围 | 迁移信号 |
|---|---|---|---|
| REPL 提交 | 检查一个表达式、类型或 API | 当前 FSI 进程及其隐藏状态 | 结果需要重复或评审 |
| `.fsx` 脚本 | 本地自动化、实验、迁移、报告 | 脚本、SDK、参数、文件、环境和包源 | 出现多个模块、测试、发布或稳定公开 CLI |
| 控制台项目 | 受维护命令、定时作业、更丰富测试 | 项目图、目标框架、锁文件、构建与发布产物 | 安装与跨仓库复用变得重要 |
| 本地 .NET 工具 | 仓库范围内具有固定调用方式的可执行程序 | 工具清单、包还原与运行时兼容性 | 组织范围分发或 API 版本管理增长 |
| FAKE 等构建 DSL | 命名构建目标与依赖图 | DSL/工具版本、脚本依赖、目标图与被调用工具 | 图或定制集成复杂到值得增加抽象层 |

不要只因项目看起来更正式，就迁移一份 70 行脚本。当调用者开始依赖命令语法，或多个文件已经形成内部架构时，它就成了需要维护的产品。锁定还原、常规测试发现和自包含部署，也都是迁移信号。

同样，不要为避开 `.fsproj` 而把真实应用压进一份脚本。一旦程序需要文件顺序、公开 API 边界、构建属性、分析器、测试项目与发布，这些都是有用约束。

### 用 REPL 回答一个问题 {#repl-question}

`dotnet fsi` 会启动 F# Interactive。REPL 提交很适合询问编译器推断出什么签名、某个 BCL 方法怎样工作，或一项小转换是否可行。`;;` 终止符属于交互提交；普通 `.fsx` 文件并不需要它。

会话会记住此前的绑定、已打开命名空间、已加载文件、已引用程序集与包解析结果。探索时这很方便，却不能作为可复现结论的依据。保存结果前，应把必要代码放进脚本并在全新进程中执行。

不要再把 Polyglot Notebooks 或 .NET Interactive 当作默认的新项目入口：维护方已分别在 2026 年 3 月和 4 月弃用它们，并归档仓库。已有 notebook 应规划迁移；本章需要可重复执行时使用仍受支持的 FSI 脚本。

### 用脚本承载完整、可评审的操作 {#script-operation}

脚本应像小型应用一样清楚说明输入、输出、失败行为和可能产生的副作用。它仍可很简洁。仓库中的 `examples/scripts/ch45-scripting-packages-next.fsx` 是完整清单脚本：它只有一个文件，只使用 .NET 随附的库，不建立全局安装，并可从仓库根目录运行。

真正有用的区分不是“一次性还是生产”，而是“范围明确的操作还是不断增长的产品”。一次数据修复对验证、备份和审计记录的要求，可能比长期开发辅助工具更严格。

### 当构建图成为答案的一部分时使用项目 {#project-promotion}

当构建过程本身也需要明确结构时，就应迁移到 F# 控制台项目。例如：代码分成多个编译文件，需要分析器或项目引用，需要固定目标框架和 `packages.lock.json`，或者需要常规测试发现、发布和稳定的命令接口。脚本中的纯函数通常可以直接搬过去，主要变化是构建和分发方式变得明确。

如果贡献者需要在仓库中调用固定版本的命令，本地 .NET 工具可能合适。提交其 `.config/dotnet-tools.json`，再用 `dotnet tool restore` 还原。工具仍以用户权限运行；锁定工具包版本不会让不受信任的代码变安全。

## 理解 FSI 执行的内容 {#fsi-model}

Microsoft 把命令格式记为 `dotnet fsi [options] [script-file [arguments]]`。脚本运行时，`fsi.CommandLineArgs[0]` 是脚本路径，后续元素才是它的参数。当某个参数可能被误认成 FSI 选项时，`--` 会让 FSI 把余下 token 当作脚本参数。

清单脚本接受以下形式：

```console
dotnet fsi --exec examples/scripts/ch45-scripting-packages-next.fsx write ./artifacts ./artifacts.manifest.json
dotnet fsi --exec examples/scripts/ch45-scripting-packages-next.fsx check ./artifacts ./artifacts.manifest.json
```

`--exec` 会运行脚本后退出，而不是留在交互模式。`write` 让输出收敛到期望内容。`check` 不写入，并在输出缺失或过期时返回退出码 `2`。意外失败返回 `1`；成功返回 `0`。

### 工作目录与源码目录回答不同问题 {#script-paths}

相对进程路径从调用者当前工作目录解析。对于 `./artifacts` 这样的命令参数，这很有用，因为其含义由调用者决定。这也意味着，从另一目录调用脚本时，脚本不能假设这些路径位于自身旁边。

脚本自带的资源应以 `__SOURCE_DIRECTORY__` 为基准；`__SOURCE_FILE__` 标识当前源码文件。调用者提供的输入使用工作目录相对路径，随脚本提供的资源使用源码相对路径。开始工作前把两者都转成绝对路径，不要悄悄混用。

环境变量、当前区域性、时区、当前时间、随机种子、网络状态与已安装 SDK 也都是输入。可复现性重要时，在边缘只读取一次、执行验证，再把普通值向内传递。

### 指令是有顺序的编译输入 {#directives}

FSI 按顺序处理脚本声明。其主要指令包括：

- `#load "helpers.fsx"` 会在后续代码使用其定义前编译并执行另一份脚本；
- `#r "library.dll"` 引用一个程序集文件；
- `#I "directory"` 为后续引用增加程序集搜索路径；
- `#r "nuget: PackageId, Version"` 还原并引用一个 NuGet 包；
- 当一个文件也会在别处编译时，`INTERACTIVE` 等条件符号可隔离只供 FSI 使用的声明。

这些不是普通运行期函数调用。引用缺失或不兼容会使后续脚本代码无法编译。`#load` 还会执行被加载脚本的顶层副作用，因此在加载时写文件的“辅助脚本”具有隐藏启动行为。

让可复用的被加载脚本不产生顶层副作用。把行为放进命名函数，并由一份入口脚本启动执行。当不断增长的 `#load` 集合开始重新发明项目文件顺序时，请使用项目。

## 清单脚本：生成稳定的产物清单 {#x45}

清单脚本解决一个实用的本地问题。它枚举产物目录下的文件，把规范化相对路径、字节长度与 SHA-256 摘要写入确定性 JSON。随后可以更新清单，或检查现有清单是否匹配。

下面四个代码块来自同一个 `.fsx` 文件，并按源码顺序排列。后面的代码会使用前面定义的类型和函数，所以它们不能各自独立运行。完整文件开头还导入了 `System`、`System.IO`、`System.Security.Cryptography`、`System.Text` 和 `System.Text.Json`。阅读时可以逐块理解，实际运行时请执行仓库中的完整脚本。

它只承担以下几项明确职责：

- 输入是一个现有本地目录与一个输出文件路径；
- 目录遍历跳过符号链接，并拒绝符号链接形式的根；
- 若输出文件位于源目录下，会排除输出文件自身；
- 所有平台的路径都使用 `/`，条目按 ordinal 路径顺序排列；
- JSON 的 schema 版本为 `1`，采用无 BOM UTF-8，并恰有一个结尾换行；
- 期望内容不变时，不触碰现有输出；
- 替换先在输出目录创建唯一命名文件，再将其移动覆盖目标；
- 无参数执行会创建并删除一个唯一临时测试目录，完成自检。

### 建模可观察结果，而非偶然步骤 {#manifest-model}

脚本把规划数据与写入、检查结果区分开：

```fsharp:line-numbers [ch45-scripting-packages-next.fsx]
type ManifestEntry =
    { Path: string
      Bytes: int64
      Sha256: string }

type ManifestPlan =
    { Entries: ManifestEntry array
      Json: string }

type WriteOutcome =
    | Updated of fileCount: int
    | Unchanged of fileCount: int

type CheckOutcome =
    | Current of fileCount: int
    | Stale of fileCount: int
```
`ManifestPlan` 同时包含结构化条目和要写入的完整文本。`Updated` 与 `Unchanged` 代替了含义需要额外解释的布尔值；`Current` 与 `Stale` 则把只读 CI 检查同写入操作分开。

模型保持很小，因为这是本地自动化。公开工具可能增加 schema 兼容性、结构化诊断、取消、日志与稳定序列化结果。那些需求就是升级信号。

### 明确遍历与哈希策略 {#artifact-scan}

文件系统适配器解析完整路径，用操作系统路径相等规则排除输出，递归跳过 reparse point，规范化所报告的分隔符，并对每个已打开流计算哈希：

```fsharp:line-numbers [ch45-scripting-packages-next.fsx]
let pathComparer =
    if OperatingSystem.IsWindows() then
        StringComparer.OrdinalIgnoreCase
    else
        StringComparer.Ordinal

let samePath left right =
    pathComparer.Equals(Path.GetFullPath left, Path.GetFullPath right)

let isReparsePoint (attributes: FileAttributes) =
    attributes.HasFlag FileAttributes.ReparsePoint

let rec regularFilesUnder directory =
    seq {
        for path in Directory.EnumerateFileSystemEntries directory do
            let attributes = File.GetAttributes path

            if not (isReparsePoint attributes) then
                if attributes.HasFlag FileAttributes.Directory then
                    yield! regularFilesUnder path
                else
                    yield path
    }

let normalizedRelativePath root path =
    Path
        .GetRelativePath(root, path)
        .Replace(Path.DirectorySeparatorChar, '/')
        .Replace(Path.AltDirectorySeparatorChar, '/')

let hashFile path =
    use input = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read)
    let length = input.Length

    let digest =
        SHA256.HashData input
        |> Convert.ToHexString
        |> fun text -> text.ToLowerInvariant()

    length, digest
```
跳过链接能避免意外走出所选树或进入环。这是一项策略，而不是普遍规则：有意包含链接的部署格式需要安全记录链接目标。

以 `FileShare.Read` 打开文件，可阻止配合该约定的 Windows 写入者在哈希期间修改文件。这不是事务式文件系统快照，在跨平台时尤其如此。若生产者可能并发修改文件树，应先发布不可变暂存目录，或使用具有快照语义的存储机制。

SHA-256 让后续消费者检测字节是否不同于记录值。它不能确认谁生成了清单，也无法发现产物与清单被同时替换。真实性需要签名或另一条可信通道；发布来源还需要额外记录。

### 把确定性规划与副作用分开 {#manifest-plan}

规划器用 `Utf8JsonWriter` 渲染 JSON，而不依赖未指定的反射顺序。它先排序条目，再固定属性顺序、大小写、缩进、编码与换行策略：

```fsharp:line-numbers [ch45-scripting-packages-next.fsx]
let renderManifest (entries: ManifestEntry array) =
    use buffer = new MemoryStream()

    use writer = new Utf8JsonWriter(buffer, JsonWriterOptions(Indented = true))

    writer.WriteStartObject()
    writer.WriteNumber("schemaVersion", 1)
    writer.WriteStartArray("files")

    for entry in entries do
        writer.WriteStartObject()
        writer.WriteString("path", entry.Path)
        writer.WriteNumber("bytes", entry.Bytes)
        writer.WriteString("sha256", entry.Sha256)
        writer.WriteEndObject()

    writer.WriteEndArray()
    writer.WriteEndObject()
    writer.Flush()

    Encoding.UTF8.GetString(buffer.ToArray()) + "\n"

let planManifest sourceDirectory outputFile =
    let sourceRoot = Path.GetFullPath sourceDirectory
    let outputPath = Path.GetFullPath outputFile

    if not (Directory.Exists sourceRoot) then
        invalidArg (nameof sourceDirectory) $"Source directory does not exist: {sourceRoot}"

    if isReparsePoint (File.GetAttributes sourceRoot) then
        invalidArg (nameof sourceDirectory) $"Source directory must not be a symbolic link: {sourceRoot}"

    let entries =
        regularFilesUnder sourceRoot
        |> Seq.filter (fun path -> not (samePath path outputPath))
        |> Seq.map (fun path ->
            let length, digest = hashFile path

            { Path = normalizedRelativePath sourceRoot path
              Bytes = length
              Sha256 = digest })
        |> Seq.sortWith (fun left right -> StringComparer.Ordinal.Compare(left.Path, right.Path))
        |> Seq.toArray

    { Entries = entries
      Json = renderManifest entries }
```
该边界仍会读取文件，因此 `planManifest` 并非纯函数。重要分离在于，它会在决定是否改变输出前计算一份完整期望结果。对相同条目数组而言，`renderManifest` 本身是确定性的。

稳定输出能避免嘈杂 diff，并让相等比较具有意义。在枚举后排序可避免继承文件系统顺序。相对路径不会嵌入开发者的绝对目录。最终 JSON 不包含时间戳、机器名或随机标识符。

### 只有期望内容不同时才写入 {#idempotent-write}

应用层会比较现有文本与期望文本。只有存在差异时，才创建临时文件并替换目标：

```fsharp:line-numbers [ch45-scripting-packages-next.fsx]
let readExisting outputPath =
    if File.Exists outputPath then
        Some(File.ReadAllText(outputPath, Encoding.UTF8))
    else
        None

let replaceFromSameDirectory (outputPath: string) (content: string) =
    let outputDirectory =
        match Path.GetDirectoryName outputPath with
        | null -> invalidArg (nameof outputPath) "Output path must include a directory."
        | directory -> directory

    Directory.CreateDirectory outputDirectory |> ignore

    let temporaryPath =
        Path.Combine(outputDirectory, $".{Path.GetFileName outputPath}.{Guid.NewGuid():N}.tmp")

    try
        File.WriteAllText(temporaryPath, content, UTF8Encoding(false))
        File.Move(temporaryPath, outputPath, overwrite = true)
    finally
        if File.Exists temporaryPath then
            File.Delete temporaryPath

let writeManifest sourceDirectory outputFile =
    let outputPath = Path.GetFullPath outputFile
    let plan = planManifest sourceDirectory outputPath

    match readExisting outputPath with
    | Some existing when existing = plan.Json -> Unchanged plan.Entries.Length
    | _ ->
        replaceFromSameDirectory outputPath plan.Json
        Updated plan.Entries.Length

let checkManifest sourceDirectory outputFile =
    let outputPath = Path.GetFullPath outputFile
    let plan = planManifest sourceDirectory outputPath

    match readExisting outputPath with
    | Some existing when existing = plan.Json -> Current plan.Entries.Length
    | _ -> Stale plan.Entries.Length
```
这给出了有用的幂等性质：一次成功 `write` 后，对未变化输入再次 `write` 会报告 `Unchanged`，且不改变输出时间戳。同目录临时文件让最终移动停留在一个文件系统内，并缩短可见不完整目标的窗口。

应准确表述保证：脚本会在同一文件系统完成本地写入后替换目标。持久化 flush、并发写入者协调、完整权限与元数据保留、崩溃持久性和网络文件系统中断恢复都需要额外机制。

`check` 会比较同一计划但不写入。这让 CI 失败可以行动：退出 `2` 表示应重新生成或提交清单，退出 `1` 表示操作本身失败。始终打印错误却返回 `0` 的脚本会破坏自动化组合。

### 用真实临时目录验证幂等性 {#script-evidence}

无参数时，清单脚本会在 `Path.GetTempPath()` 下的唯一目录创建两个文件。它先写入一次，再把输出时间戳设为哨兵值，然后再次写入、执行只读检查并验证 ordinal 规范化路径。最后，`finally` 只删除脚本自己创建的目录。

本次校订在 2026-08-31 使用 .NET SDK 10.0.302，从仓库根目录重新运行了完整脚本：

```console
dotnet fsi --exec examples/scripts/ch45-scripting-packages-next.fsx
```

这次运行产生了以下有序输出：

```text
First write: updated files=2
Second write: unchanged files=2
Check mode: current files=2
Stable timestamp: true
Manifest paths: nested/beta.bin, notes.txt
Cleanup: removed=true
```

请用全新 FSI 进程运行该脚本。输出可供检查临时测试目录、输出顺序、幂等的第二次写入、只读当前检查、路径规范化与清理。它不覆盖恶意目录、并发生产者、数百万文件、远程文件系统、签名，或每一种 Windows/Linux 文件系统。

## 一旦另一调用者依赖自动化，就把它当作公开接口 {#automation-interface}

脚本可能没有程序集 API，但仍会暴露契约：

- 命令名、参数顺序、默认值与帮助文本；
- 可接受的路径形式，以及路径是否相对于调用者；
- 标准输出中的数据、标准错误中的诊断与退出码；
- 创建、替换或删除哪些文件，以及每一项由谁负责；
- 顺序、编码、区域性、时间与 schema 稳定性；
- 包、SDK、工具、操作系统和外部命令假设；
- 部分失败、取消、重复执行与并发调用下的行为。

记录调用者可能据以自动化的部分。面向人的措辞可以演化；机器消费的输出需要 schema 或明确的不稳定状态。当 JSON 结果或退出码才是真实契约时，不要让 CI 解析装饰性日志。

### 优先收敛，而不是顺序编辑 {#convergence}

幂等自动化从当前输入计算期望状态，再向它收敛。这比“按顺序执行这些追加操作”更强。每次运行都追加生成行、增加重复配置项，或重命名第一个碰巧匹配的文件，都会积累依赖历史的状态。

先规划还会支持 `check` 与 dry-run 模式。应用前可以把计划作为数据测试。当操作具有破坏性或外部可见时，应准确列出目标，并要求调用者明确选择模式，而不是从环境名称推断权限。

幂等并不免疫错误输入。确定性脚本可以可靠地生成错误文件。验证源契约，测试代表情形与失败，并保留可评审 diff。

### 用 shell 做组合，用 F# 做类型化决策 {#shell-boundary}

Shell 脚本很擅长调用命令和连接流。当数据解析、分支、转义、集合、错误模型或文件系统规则成为主体时，其可移植性会变差。F# 能给这些决策加上类型并使用普通 .NET API，同时仍可在适当时调用外部进程。

不要只是为了宣称构建使用 F#，就把每个 `dotnet build` 包进 F#。简短任务文件可能更清楚。只有任务确实需要解析、规划、验证、并发或可复用策略时，才引入 F#。

调用进程时，应传入参数列表，而不是构造未转义 shell 字符串；捕获退出状态与有界输出，传播取消，并决定继承哪些环境变量。机密不能出现在命令行或普通日志中。

### 限制不受信任且昂贵的输入 {#automation-safety}

清单脚本只面向可信的本地产物目录。如果工具要读取不受信任的路径，还必须限制文件数量、总字节数和输出大小，并处理权限、特殊文件、并发变化、超时和取消；有时还需要沙箱。需要计算校验和，并不表示程序应该无条件读取任何设备文件。

执行操作前解析破坏性目标。绝不能从缺失环境变量、宽泛通配符、仓库根或主目录推导递归删除根。优先使用任务创建的临时目录与可恢复移动。验证解析后的目标仍位于预期根中。

凭据应放在环境的机密机制中，而不是源码、测试文件、清单、异常消息或生成报告。本地脚本以调用用户权限运行；“只是一份脚本”并不是安全边界。

## 只有说清缺失能力后才添加包 {#package-choice}

清单脚本的第一个设计问题是它是否需要包。`System.IO`、`SHA256` 与 `Utf8JsonWriter` 已满足这份有界契约，所以除 SDK 与 FSharp.Core 外，正确依赖数为零。

这并不是反对使用包。一个维护良好的解析器、协议客户端、数据库驱动、测试库或框架，可能比自行实现更可靠。关键是先写清缺少什么能力，再判断包是否解决这个问题，而不是先找一个流行包再围绕它设计程序。

### 先评估适配性，再看流行度 {#package-scorecard}

对于候选包，至少记录：

| 问题 | 要检查什么 | 应拒绝或开展试验的情形 |
|---|---|---|
| API 是否解决具体问题？ | 最小代表调用、错误/取消模型、数据生命周期 | 演示只有在巨大适配器或隐藏全局状态下才工作 |
| 是否支持目标？ | 包目标框架、运行时/原生资产、AOT/浏览器/平台说明 | 交付目标缺失或仅被假定兼容 |
| 谁维护它？ | 包所有者、源码仓库、许可证、发布历史、issue/review 活动 | 无法确认来源或许可证 |
| 传递引入了什么？ | 完整依赖图、构建/分析器/内容资产、原生二进制 | 闭包不成比例或与宿主冲突 |
| 运维模型可接受吗？ | 线程、网络、文件、反射、生成代码、日志、配置 | 关键行为无法观察或控制 |
| 团队能更新并退出吗？ | 迁移说明、所用 API、可替换边界、数据格式 | 移除需要重写领域或已存数据 |
| 已验证哪些内容？ | 真实目标上的小范围测试与还原/构建/运行检查 | 决策只有 README 片段或下载量支持 |

NuGet 的官方包评估指南建议检查版本历史、项目/源码链接、维护者、许可证、依赖、使用情况与漏洞信息。这些信号无法预测未来维护。流行包可能不适合目标；小包在契约清楚、维护者明确时也可能很优秀。

开展范围受限的采用试验（spike，即为验证关键风险而构建、便于删除的小型实现）。测试最困难的代表行为、一个失败、目标兼容性与替换边界。记录检查的版本和日期，因为包状态会变化。

### `#r "nuget:"` 很方便，但不是锁文件 {#script-packages}

FSI 支持这样的包引用：

```fsharp
#r "nuget: PackageId, 1.2.3"
open PackageNamespace
```

省略版本会在解析时请求最高可用非预览版本。它适合可丢弃探索，却不适合版本化自动化契约。提交的脚本应明确写出直接依赖版本。

在一个 `#r` 指令中固定版本，并不会为完整传递图创建仓库 `packages.lock.json`。该指令还依赖有效的 NuGet 配置、包源、凭据、缓存与网络可用性。不要把固定指令描述成锁定还原。

FSI 通常不会使用包构建目标。其文档化 `usepackagetargets=true` 选项会为那些按设计需要此行为的包启用目标。只有理解确切需要后才启用：构建目标是可执行的还原和构建行为，会扩大需要信任和验证的范围。

如果 CI 必须评审并复现完整依赖闭包，就把自动化移进采用 PackageReference 和已提交锁文件的项目。只有另一依赖管理器也能为脚本提供已提交锁时，才适合替代。一文件形式不值得换取无法验证的供应链。

### 锁定真正运行的图 {#locking}

在 SDK 风格应用或工具项目中，指定直接 PackageReference 版本并启用 NuGet 锁文件生成。提交 `packages.lock.json`，再在 CI 中执行 `dotnet restore --locked-mode`。锁定模式会还原记录闭包，或在项目依赖会改变它时失败。

锁文件回答解析问题，而不是信任或运行期正确性。它不证明包安全、许可证适用于产品、与目标兼容或行为正确。它也不会强迫消费应用的图使用库项目的私有解析；顶层消费者会解析自己的闭包。

SDK 与工具版本也应明确。用 `global.json` 固定 SDK，用 `.config/dotnet-tools.json` 固定本地工具。项目锁文件覆盖 NuGet 依赖，工作区锁文件覆盖 JavaScript 工具；只使用项目需要的机制。

有意更新：改变一个有界集合，重新生成锁，检查直接与传递差异，阅读相关发布说明，运行聚焦测试和完整测试，并保留回滚。“最新”是查询结果，不是评审策略。

### 把还原当作供应链操作 {#package-security}

包与 .NET 工具会通过运行时代码、构建目标、分析器、生成器、原生资产或工具入口点，以当前用户或构建进程的权限执行。使用可信来源、保护凭据并评审源配置。公有和私有 feed 并存时，Package Source Mapping 可以约束哪些源可提供每个直接与传递包 ID。

NuGet audit 会在还原期间把已解析依赖与已知漏洞数据比较。按策略处理发现，并保持 audit 源可用。没有发现意味着“此配置下未报告匹配的已知公告”，而不是“包是安全的”。

优先使用仓库范围配置与本地工具，而不是未记录的机器状态。不要提交还原包缓存或凭据。当依赖事故需要解释时，应保留还原日志与锁 diff。

## 按职责理解 F# 生态，而不是罗列工具 {#ecosystem-map}

第七部分涉及以下职责层：

| 层 | 本书中的例子 | 第一个兼容性问题 |
|---|---|---|
| F# 语言与 FSharp.Core | 可区分联合、模式匹配、集合、async、代码引用 | 需要哪个语言/编译器和 FSharp.Core 契约？ |
| .NET 运行时与 BCL | 文件、JSON、HTTP、task、诊断、密码学 | 需要哪个 TFM、运行时、OS 与 API 行为？ |
| Microsoft 平台框架 | ASP.NET Core、hosting、容器、Aspire 集成 | 适用哪个受支持平台版本与部署模型？ |
| F# 社区库 | FsCheck、Giraffe/Falco/Oxpecker、FSharp.Data、Elmish | 哪种 API 价值足以抵消包与维护成本？ |
| 跨语言 UI/工具链 | Fable/npm/浏览器、Avalonia 后端、Unity Editor/IL2CPP | 哪些编译器、宿主、原生工具与发布矩阵必须一致？ |
| 仓库自动化 | 脚本、本地工具、FAKE、Paket、CI 运行器 | 哪个工具控制顺序、还原、凭据与验证？ |

F# 可以使用整个 NuGet 生态，而不仅是名称含“FSharp”的包。许多普通 .NET 库可以直接调用。应检查 API 如何表示 null、委托、task、异常、可变性、反射、重载、序列化和面向 C# 的 builder；必要时加一层小型适配器。

反过来，F# 原生包也不会自动成为最佳选择。像评估其他依赖一样检查目标框架、发布历史与测试、传递资产和团队理解。

### FAKE 与 Paket 解决不同问题 {#fake-paket}

[FAKE](https://fake.build/) 是带目标依赖和常用工具模块的 F# 构建任务 DSL。当命名目标图、可复用构建集成或更丰富编排能实质澄清构建时选择它。对于四条线性命令，普通仓库任务文件可能仍更清楚。

[Paket](https://fsprojects.github.io/Paket/) 是另一种 .NET 依赖管理器，具有自己的依赖与锁模型，也支持脚本集成。应因为该模型或现有仓库需要它而选择，不要因为 F# 代码就必须使用与 F# 相关的包管理器。没有明确分工时，不要让 NuGet 与 Paket 同时管理同一组依赖。

两种工具都会增加概念、引导过程、版本与失败模式。当这些成本替代了更大的偶然复杂度时，其价值是真实的。迁移前应针对实际 CI 与开发环境开展试验。

## 识别高级特性，不要把它们前置 {#advanced-recognition}

掌握本书已经覆盖的基础后，你就能阅读大多数生产 F#：类型、函数、模式匹配、集合、模块、副作用、async/task、.NET 边界与测试。四项特性经常显得比引入它们的问题更神秘。现在只需学习其识别信号与停止条件：

| 特性 | 识别信号 | 可能遇到它的原因 | 下一步 |
|---|---|---|---|
| 代码引用（quotations） | `<@ expression @>`、`<@@ expression @@>`、`Expr<'T>`、代码引用模式 | 库把 F# 代码表示成数据，用于 DSL、查询、分析或生成 | 区分构造/遍历表达式树与执行它；阅读库契约 |
| 静态解析的类型参数（SRTP） | `inline` 加静态/成员约束；当前简化语法可能使用 `'T`，旧式/复杂形式可能出现 `^T` | 运算符或基于成员的编译期抽象 | 不要同普通泛型混淆；检查推断约束与特化成本 |
| 灵活类型 | 类型标注内的 `#SomeBase`，等价于带子类型约束的泛型 | 高阶或嵌套输入应接受任何子类型/接口实现 | 同预处理指令和普通向上转型区分；保持公开签名可读 |
| byref 与 Span | `&value`、`byref<'T>`、`inref<'T>`、`outref<'T>`、`Span<'T>`、`ReadOnlySpan<'T>` | 互操作或经测量的同步缓冲区/复制热点 | 遵守栈与生命周期规则；不要跨 async 或堆边界捕获；采用前先测量 |

代码引用把表达式表示成数据；它们不会自行执行。静态解析的类型参数会在编译期特化内联代码，日常 `'T` 函数并不需要它。灵活 `#Type` 语法表达对象层次中的兼容性，不是注释或编译器命令。类似 byref 的值用普通可组合性换取受限生命周期。

[附录 H：高级特性识别索引](../appendices/h-advanced-index)提供聚焦入口与交叉链接。它有意不把这些特性变成第二套入门课程。第 11 章锚定泛型约束与 SRTP；第 31 章锚定经过测量的 Span/byref 决策。

## 通过反复实践继续学习，不要只勾完特性清单 {#learning-next}

读完一本书会给你地图，却不会自动带来熟练度。熟练来自反复实践：让编译器、测试、运行结果和其他读者不断检验第一版设计。

使用以下循环：

1. 选择一个真实、有界的问题，其失败足以暴露权衡；
2. 在选择框架前建模输入、有效状态、预期失败与副作用；
3. 构建一个穿过真实边界的最小端到端样例；
4. 检查推断签名，并明确含糊的职责；
5. 按比例测试纯规则、适配器、失败路径与真实目标；
6. 在为性能改变表示前，先分析或插桩；
7. 评审依赖图与部署图，而不只是源码；
8. 记录已经验证什么、仍有哪些未知，以及何种情况会改变选择；
9. 学习后简化，再以稍难一点的边界重复。

### 根据想学习的风险选择项目路线 {#project-tracks}

| 路线 | 第一阶段 | 更难的第二阶段 | 应重读章节 |
|---|---|---|---|
| 语言与建模 | 验证并转换有版本本地格式的 CLI | 跨三个 schema 版本迁移并加入属性 | 7–18、28–30 |
| 后端与分布式系统 | 围绕纯工作流的已认证 API | 幂等持久化、重试、追踪、容器发布 | 20–24、33–39、42 |
| 数据与分析 | 可复现的摄取/清洗/报告流水线 | schema 漂移、大数据、notebook 升级项目 | 14–15、29–31、40 |
| 浏览器应用 | 带一个真实 API 的 Fable 状态机 | URL 状态与导航、取消、无障碍、bundle 预算 | 20、22–24、41 |
| 桌面或移动端 | 带纯更新逻辑的 Avalonia 桌面样例 | 打包、平台服务、已签名目标产物 | 25–32、43 |
| 游戏与模拟 | 薄宿主后的确定性 F# 规则 | 重放、存档迁移、帧分析、真实 IL2CPP Player | 12、20、24、27–31、44 |
| 工具与库 | 把清单脚本升级为有测试的控制台工具 | 稳定 API/CLI、包发布、升级兼容性 | 16–17、26–31、本章 |

不要构建七个起步项目。选择未知点接近你的工作或兴趣的一条路线，再持续深入，直到部署与维护改变你的设计。

### 学会在三个层次导航来源 {#source-reading}

用语言参考核对具体语法与约束，用 FSharp.Core API 文档核对函数签名与行为，再用相关 .NET/平台文档核对运行时边界。社区抽象进入决策后，还要检查包自身源码、发布说明、测试与 issue。

运行小型编译器实验，而不是凭记忆争论。记录 SDK 与包版本。一篇博客可以教授持久思想，同时其设置命令、语法或兼容表已经老化；应把思想同当前契约分开。

从类型向内阅读陌生 F#：公开签名、领域 case、纯转换、副作用端口、组合根，最后才是实现细节。当巧妙运算符隐藏数据流时，先查看推断类型，再把一次调用完整写开。

### 寻求能改变设计的反馈 {#community-feedback}

向评审者提出一个可以直接验证的问题，例如：

- 这个状态能否被非法构造？
- 哪个 token 控制取消？
- 第二次运行后会怎样？
- 哪项 Player 测试验证了这个包？

泛泛请求“评审我的 F#”，只会得到泛泛认可。

向社区提问时，应提供最小复现、完整诊断、SDK/包版本、目标、期望行为、实际行为和已经排除的情况。这既尊重他人时间，也让答案对下一位读者有用。

从最小持久边界回馈：改进复现、文档示例、测试、issue、包元数据或聚焦修复。参与生态并不需要编译器专长。

## 练习 {#exercises}

### 练习 1：增加排除规则而不失确定性 {#exercise-01}

扩展清单脚本，让调用方可以重复传入 `--exclude GLOB`，排除生成的日志和符号文件。请先定义：

- `GLOB` 支持哪些语法；
- 路径分隔符和大小写如何比较；
- 规则匹配文件、目录还是两者；
- 无效模式如何报错；
- 符号链接如何处理和报告；
- 排除规则如何写进清单 schema。

`write` 和 `check` 必须使用同一套规则，并继续保证稳定排序、排除输出文件和重复运行结果不变。还要为 Windows 与类 Unix 路径写有界测试。最后决定是实现一个范围很小且有文档的匹配器，还是采用现有包。


::: details 参考答案

“glob”一词并不是完整契约。不同 Shell 和库对分隔符、大小写、隐藏文件、字符类、递归、错误模式和目录剪枝采用不同规则。添加 `--exclude` 前，应先逐项定义这些规则。

#### 契约 {#exclusion-contract}

一种可接受的版本 2 契约是：

- `--exclude PATTERN` 可以在模式参数之后、两个位置路径参数之前多次出现；
- 模式和候选路径不论宿主 OS 都使用 `/`；
- 所有平台都按序号值（ordinal）比较，并区分大小写；
- `*` 匹配一个路径段内零个或更多字符；
- `?` 恰好匹配一个非 `/` 字符；
- `**` 只有作为完整路径段时才有效，并匹配零个或更多完整路径段；
- 不支持并拒绝 `[abc]`、brace 展开、转义、绝对路径、空段、`.` 段与 `..` 段；
- 模式只匹配文件；排除整个目录树要明确写成 `logs/**`；
- 在评估用户规则前，先根据解析后的文件标识排除输出文件；
- 符号链接仍按现有遍历策略跳过，与排除规则无关；
- 重复模式会被删除，再对规范化模式按 ordinal 排序；
- 无效模式在哈希或写入前失败，并作为用法错误返回退出码 `2`。

区分大小写可能不符合 Windows 用户的习惯，但它让仓库在所有平台上遵循同一规则。也可以选择跟随各文件系统的大小写行为，但必须记录并测试这种平台差异。

#### Schema 与规划 {#exclusion-schema}

应升级清单版本，因为排除规则改变了“完整”的含义：

```json
{
  "schemaVersion": 2,
  "exclusions": ["**/*.pdb", "logs/**"],
  "files": []
}
```

记录规范化规则后，即使两份清单当前列出相同文件，只要排除策略不同，也能区分。评审者也能直接看出某个产物为何没有出现。

从概念上把规划边界重构为：

```text
解析参数
  -> 验证并规范化模式
  -> 枚举但不跟随链接
  -> 规范化每个相对路径
  -> 应用排除规则
  -> 对纳入文件计算哈希并排序
  -> 渲染一份版本 2 期望文档
```

`write` 与 `check` 必须调用同一个规划器，分开实现迟早会产生差异。输出文件的排除逻辑应独立于用户规则，避免调用方意外让清单计算自身的哈希。

目录剪枝是一项可能改变行为的优化。`logs/**` 可以安全跳过 `logs`，但未来若加入否定或 include 规则，匹配器仍可能需要进入该目录。本契约没有否定规则，因此可用经过测试的前缀分析来剪枝。第一版也可以更简单：遍历完成后再过滤文件路径。

#### 匹配器选择 {#matcher-choice}

不要把任意用户模式直接翻译成无界正则表达式。可以用线性路径段匹配器只实现上述语法，并限制模式长度；也可以让一个仍在维护的 glob 包逐项通过书面规则。

只有验证性试验（spike）满足以下全部条件，本答案才会选择包：

- 支持明确指定按序号值比较，并区分大小写；
- `**` 与分隔符规范化符合书面契约；
- 错误模式可以在访问文件系统前拒绝；
- 遍历仍受清单脚本的链接与根目录策略控制，而不是让库自行遍历；
- 包目标与传递图适合升级后的控制台项目；
- 锁文件与锁定还原能复现该图。

若没有候选满足这些约束，有意缩小的语法比宣称完全 glob 兼容更安全。把它命名为“产物模式语法”、写出文档，并拒绝不支持的语法，而不是近似模拟 shell。

#### 测试矩阵 {#exclusion-tests}

同时使用纯路径/模式测试与真实临时目录：

| 情形 | 期望结果 |
|---|---|
| 无规则 | 版本 2 包含与现有清单脚本相同的两个文件 |
| `**/*.pdb` | 嵌套和根目录中的 `.pdb` 都不在清单内；`.PDB` 仍在 |
| `logs/**` | 规范化 `logs/` 下的文件都不在清单内 |
| `a/?eta.bin` | `a/beta.bin` 匹配；`a/longbeta.bin` 不匹配 |
| 不同输入顺序中的重复规则 | 规范化 JSON 与摘要行逐字节相同 |
| 模式中出现反斜线 | 用法失败发生在输出变更前 |
| `../secret` 或绝对模式 | 验证拒绝路径穿越语义 |
| 输出位于源内 | 即使没有规则命名它，输出仍被排除 |
| 第二次写入 | `Unchanged` 且哨兵时间戳不变 |
| 过期检查 | 退出 `2`，不写入，原输出字节不变 |
| 指向树外的链接 | 链接被跳过；永不哈希外部文件 |

在每个 OS 的纯函数测试中模拟 Windows 风格字符串，再到真实 Windows 和类 Unix CI 运行器上执行文件系统测试。只有 Linux 路径测试，不能说明跨平台行为正确。

:::

### 练习 2：编写包采用记录 {#exercise-02}

团队想为升级后的清单工具增加命令行解析器。请比较手写解析器和两个当前 NuGet 候选，并记录：

- 必需的命令语法，以及帮助和错误行为；
- 目标框架、包来源和许可证；
- 维护状态、传递依赖、构建资产和已知漏洞；
- 裁剪或 AOT 要求；
- 测试体验、固定的直接版本和锁定步骤；
- 谁负责更新，以及如何替换或移除该包。

针对最困难的一项需求编写一个小型试验，再给出可以撤回的选择结论。


::: details 参考答案

从需求而不是候选开始。升级后的工具需要：

- `write` 与 `check` 子命令；
- 两个必需路径参数，以及可重复的 `--exclude` 选项；
- 自动生成的帮助和可预测的用法错误；
- 测试调用时不会终止进程的解析器；
- 按标准方式发布为 .NET 10 应用。

Shell 补全与原生 AOT 值得考虑，但不是发布要求。

#### 截至 2026-08-31 的候选记录 {#candidate-record}

为本答案核对的 NuGet 官方页面显示：

| 选择 | 核对版本 | 适用点 | 成本或待验证问题 |
|---|---:|---|---|
| 手写解析器 | 仓库代码 | 无依赖图；能准确控制当前三个参数 | 帮助、重复选项、别名、诊断和未来子命令都要自行维护 |
| [Argu](https://www.nuget.org/packages/Argu) | 6.2.5 | 面向 F#、使用可区分联合的声明式解析器；目标为 .NET Standard 2.0 | 包最后更新于 2024 年 12 月；引入 FSharp.Core 与 `System.Configuration.ConfigurationManager`；裁剪/AOT 行为需要真实试验 |
| [System.CommandLine](https://www.nuget.org/packages/System.CommandLine) | 2.0.11 | 命令、选项、参数、验证、帮助、补全与异步 action；目标为 .NET 8 和 .NET Standard 2.0 | API 使用 C# 中常见的对象/构建器模式；F# 重载与 null 适配、帮助和错误文本的稳定性需要试验 |

两个包版本都是在该日期核对的事实，都不是书站依赖。运行针对包的试验之前，清单脚本只验证了自己的 BCL 解析器。

不要把下载量当成正确性指标。应检查维护者、MIT 许可证、源码仓库、依赖标签、发布历史、安全公告和实际 `.nupkg`。然后使用采用项目的有效包源运行还原审计。

#### 聚焦试验 {#parser-spike}

为每个包创建可丢弃的 `net10.0` F# 控制台项目。锁定准确的直接版本和解析后的依赖图。用一个可测试的适配器把 `string array` 转成：

```fsharp
type Command =
    | Write of source: string * output: string * exclusions: string list
    | Check of source: string * output: string * exclusions: string list
    | ShowHelp

type ParseFailure =
    { ExitCode: int
      StandardError: string }
```

库专属类型停在该适配器内。清单规划只接收 `Command`。这样移除包时不必触碰哈希、schema 或文件系统策略。

针对手写解析、Argu 与 System.CommandLine 运行相同黄金向量：

- 有效 `write` 与 `check`，分别带零、一和三项排除；
- 若契约允许，选项分别位于位置参数前后；
- `--help`、未知选项、缺少源、重复不可重复选项与无效模式；
- `--` 终止符后以 `-` 开头的路径；
- 作为已分隔参数 token 传入的 Unicode 与含空格路径；
- 准确的退出类别与输出流；对于有意不稳定的装饰文本，可以先规范化再比较；
- 在 Windows 与一个类 Unix 目标调用发布的可执行程序；
- 只有当裁剪与原生 AOT 成为声明发布要求时才验证它们。

记录还原过程、锁文件 diff、构建警告、包审计、发布大小、启动时间和适配器代码量。不要根据目标框架兼容性推断 AOT 支持。

#### 可逆决策 {#package-decision}

清单脚本当前只有一个模式和两个路径，因此保留手写解析器。升级后的工具需要可重复排除和自动生成帮助；若所有测试向量都通过，则暂定选择 System.CommandLine `2.0.11`。它的命令模型符合计划中的子命令与重复选项，项目归属也让维护路径较为清楚。

这并不表示 Argu 普遍更差。如果团队重视用 DU 声明 F# API，而且试验得到更清晰的代码与可接受的维护、部署结果，就选择 Argu。应由适配器和测试矩阵决定，而不是语言偏好。

采用变更应包括：

- 控制台项目中固定到具体版本的 `PackageReference`；
- 已提交 `packages.lock.json` 与 CI `dotnet restore --locked-mode`；
- 有效包源与 Package Source Mapping 评审；
- 依策略处理警告的还原审计；
- 一个集中封装所有包专属类型的模块；
- 黄金 CLI 契约测试与发布后进程冒烟；
- 明确的维护者和季度评审触发器；
- 一份移除说明：只替换解析适配器，保留 `Command` 与全部核心函数。

若包未通过某项必需向量，就保留手写解析器或测试另一候选。不要为了迎合库的默认行为而扩张公开 CLI。

:::

### 练习 3：规划接下来十二周 {#exercise-03}

从本章选择一条实践路线，并规划三个连续的四周阶段。每个阶段都要交付一个能运行的结果，不能只安排阅读。每个阶段写清：

- 需要复习的 F# 概念；
- 一个真实的 .NET 或平台边界；
- 要做的测试和诊断；
- 最多允许增加多少包；
- 部署或分发目标；
- 评审时要回答的问题；
- 什么情况下应简化方案或换一种设计。

只有测量结果提出明确需求时，才引入高级语言特性。


::: details 参考答案

该示例选择工具与库路线。每个四周增量都会交付可用边界，并以能缩小范围的评审结束。

#### 第 1–4 周：在不改变语义的前提下升级清单脚本 {#weeks-01-04}

**成果：** 一个带 `Manifest.Core`、薄 CLI 与已发布可执行程序的 `net10.0` 控制台项目，保持清单脚本的 schema 版本 1。

工作包括：

- 把清单条目、渲染、规划结果与路径规范化移入有顺序的 `.fs` 模块；
- 用小型函数隔离文件系统和控制台 I/O；
- 为渲染添加基于示例的测试，并为退出 `0`、`1`、`2` 添加进程级测试；
- 在 Windows 与 Linux/macOS 临时目录测试分隔符、支持时的链接和输出排除；
- 发布依赖框架的产物，并在源码树外运行；
- 只有用户需要时才把脚本保留为小型兼容启动器，否则记录新命令。

重读第 9、16–18、21、26、28 与 30 章。不使用新的运行期包。真实边界是文件系统与进程 CLI。诊断记录命令、退出、stderr 类别、SDK、OS 与输出哈希。

评审问题：“无效参数或文件系统故障是否可能留下部分目标文件，或错误地返回成功？”如果项目只增加复杂度，却没有改善测试、分发或可复现性，就撤销这次升级。

#### 第 5–8 周：增加版本化排除并验证依赖 {#weeks-05-08}

**成果：** schema 版本 2、练习 1 中明确的排除规则、仍可读取版本 1 清单，以及一项已锁定的解析器决策。

工作包括：

- 分开建模 `ManifestV1` 与 `ManifestV2`，并定义一条单向升级；
- 把规范化排除规则加入期望状态，让 `write`/`check` 共享一个规划器；
- 在添加任何包前执行练习 2 的解析器试验；
- 若采用包，提交准确的包版本、完整锁文件和还原审计结果；
- 使用有界生成器，对排序、重复规则与渲染/解析稳定性执行模糊测试或属性测试；
- 增加迁移测试样例，并在不覆盖的前提下拒绝未知未来 schema 版本；
- 在并行化前，针对代表文件树测量遍历与哈希。

重读第 10–15、27–31 与 37 章。包预算是一个 CLI 解析器，glob 包为零，除非匹配器试验确认必须采用。真正要守住的是 schema 兼容性与跨平台匹配规则。

评审问题：“版本 2 能否解释每个省略文件，并在不悄悄重解释的情况下保留版本 1 产物？”如果解析包的适配器与图超过它替代的行为，就移除它。

#### 第 9–12 周：分发并运维工具 {#weeks-09-12}

**成果：** 由第二个测试仓库消费的仓库本地工具或有版本可执行程序，具备可复现还原/构建、发布说明与回滚。

工作包括：

- 根据安装需求选择本地工具或标准发布的可执行程序；
- 固定工具清单或产物版本，并测试从获准来源执行干净机器还原；
- 增加可选结构化 JSON 诊断，同时把面向人的 stderr 分开；
- 为大型扫描定义取消与最大支持文件数/输出大小；
- 构建未签名的本地测试包与 feed，因此不需要专有账号；
- 若发布策略要求，则生成 SBOM 或依赖清单，但不能只凭它断言软件来源（provenance）；
- 测试从上一命令升级并回滚；
- 请一位无上下文评审者只凭文档复现一个成功与一个清单过期失败。

重读第 20–24、27、30–32、38 与 45 章。包预算仍是获准解析器；每个新包都需要独立采用记录。真实边界是安装、来源信任、取消与兼容性。

评审问题：“新贡献者能否在不依赖未记录机器知识的情况下还原、验证、升级、诊断并移除该工具？”若不能，就缩小分发范围，或明确由谁补齐缺失工作。

#### 高级特性预算 {#advanced-budget}

三个增量都不需要代码引用、静态解析的类型参数（SRTP）、灵活类型或自定义 byref。常规记录、联合类型与函数已经足够。用接口隔离副作用，用 task 支持取消，用数组或流传输数据。

只有性能分析表明跨边界复制占主要成本，而且生命周期确定为同步时，才考虑 `Span`。只有多个具体算法需要同一种成员约束抽象时，才考虑 SRTP。只有产品需要读取或生成表达式树时，代码引用才有意义。库签名中可能出现灵活类型；决定是否暴露前，应先识别它们。

这种克制是计划的一部分，而不是缺少雄心。学习目标是交付越来越可信的 F#，再在系统给出理由时深入某项语言特性。

:::


第七部分至此完成。附录会把本书变成工作参考：环境配置、语法、集合、C# 迁移、编译器诊断、术语、答案评审和高级特性识别索引。

## 资料来源 {#sources}

- [Microsoft Learn：使用 F# 进行交互式编程](https://learn.microsoft.com/en-us/dotnet/fsharp/tools/fsharp-interactive/)
- [Microsoft Learn：F# Interactive 选项](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/fsharp-interactive-options)
- [.NET Interactive：Polyglot Notebooks 与 .NET Interactive 弃用公告](https://github.com/dotnet/interactive/issues/4163)
- [Microsoft Learn：PackageReference 与锁文件行为](https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files)
- [Microsoft Learn：查找与评估 NuGet 包](https://learn.microsoft.com/en-us/nuget/consume-packages/finding-and-choosing-packages)
- [Microsoft Learn：审计包依赖](https://learn.microsoft.com/en-us/nuget/concepts/auditing-packages)
- [Microsoft Learn：Package Source Mapping](https://learn.microsoft.com/en-us/nuget/consume-packages/package-source-mapping)
- [Microsoft Learn：.NET 工具](https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools)
- [FAKE 文档](https://fake.build/)
- [Paket 文档](https://fsprojects.github.io/Paket/)
- [Microsoft Learn：代码引用](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/code-quotations)
- [Microsoft Learn：静态解析的类型参数](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/generics/statically-resolved-type-parameters)
- [Microsoft Learn：灵活类型](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/flexible-types)
- [Microsoft Learn：byref 与 byref-like 结构体](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/byrefs)
- [Microsoft Learn：F# 导览](https://learn.microsoft.com/en-us/dotnet/fsharp/tour)
- [FSharp.Core API 文档](https://fsharp.github.io/fsharp-core-docs/)
