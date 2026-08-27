---
title: "第 45 章：脚本、自动化、包生态与继续学习"
description: "把 F# 脚本变成确定性的本地自动化，审慎选择并锁定包，再建立持续掌握 F# 的实用途径。"
translationKey: part-07/ch-45-scripting-packages-next
---

# 第 45 章：脚本、自动化、包生态与继续学习 {#overview}

F# 脚本并不比编译项目低一等。两者使用相同的语言、FSharp.Core 和 .NET 运行时，脚本只是省去部分配置并缩短执行路径。因此，`.fsx` 很适合探索、仓库维护、数据修复、发布检查与小型本地工具。

更短的路径并不会消除工程责任。脚本可能依赖隐藏的会话状态、调用者工作目录、可变包源、不稳定遍历顺序、含糊退出码，或每次执行都会发生的写入。一旦其他人或 CI 依赖它，这些细节就是它的接口。

本书最后一章会把一份真实脚本变成可靠的本地自动化。随后讨论如何选择包、锁定实际交付的依赖图，以及何时把脚本迁移成项目或工具。最后给出一条循序渐进的 F# 学习路线。

## 选择能保留契约的最简单执行方式 {#execution-surface}

正确的执行方式是能让所需行为可重复的最简单方式。这里的“简单”指需要管理的运行条件少，而不只是文件数量少。

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

### 用脚本承载完整、可评审的操作 {#script-operation}

脚本应像小型应用一样清楚说明输入、输出、失败行为和可能产生的副作用。它仍可很简洁。清单脚本只有一个文件，只使用 .NET 随附的库，不建立全局安装，并可从示例所在目录调用。

真正有用的区分不是“一次性还是生产”，而是“范围明确的操作还是不断增长的产品”。一次数据修复对验证、备份和审计记录的要求，可能比长期开发辅助工具更严格。

### 当构建图成为答案的一部分时使用项目 {#project-promotion}

当构建本身需要结构时，应迁移到 F# 控制台项目，例如多个编译文件、分析器、项目引用、受控目标框架或 `packages.lock.json`。常规测试发现、生成文档、发布、裁剪/AOT 检查和稳定命令接口，也属于项目级需求。脚本中的纯函数几乎可以原样迁移；关键变化是构建与分发方式得到明确定义。

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

在仓库根目录运行已验证样例：

```console
dotnet fsi --exec examples/scripts/ch45-scripting-packages-next.fsx
```

登记的样例要求以下有序观察：

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

清单脚本面向可信本地产物树。暴露给不受信任路径的工具还需要文件数与字节限制、权限策略、特殊文件处理、竞态分析、超时/取消、输出大小边界，或许还需要沙箱。校验和清单不是永远读取攻击者控制设备文件的理由。

执行操作前解析破坏性目标。绝不能从缺失环境变量、宽泛通配符、仓库根或主目录推导递归删除根。优先使用任务创建的临时目录与可恢复移动。验证解析后的目标仍位于预期根中。

凭据应放在环境的机密机制中，而不是源码、测试文件、清单、异常消息或生成报告。本地脚本以调用用户权限运行；“只是一份脚本”并不是安全边界。

## 只有说清缺失能力后才添加包 {#package-choice}

清单脚本的第一个设计问题是它是否需要包。`System.IO`、`SHA256` 与 `Utf8JsonWriter` 已满足这份有界契约，所以除 SDK 与 FSharp.Core 外，正确依赖数为零。

这并非反对包的极简主义。受维护的解析器、协议客户端、数据库驱动、测试库或框架，可能消除远多于自身引入的风险。重点是用书面需求比较包，而不是把寻找包当成架构设计。

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
| F# 语言与 FSharp.Core | 联合、模式匹配、集合、async、quotations | 需要哪个语言/编译器和 FSharp.Core 契约？ |
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
| quotations | `<@ expression @>`、`<@@ expression @@>`、`Expr<'T>`、quotation 模式 | 库把 F# 代码表示成数据，用于 DSL、查询、分析或生成 | 区分构造/遍历表达式树与执行它；阅读库契约 |
| SRTP | `inline` 加静态/成员约束；当前简化语法可能使用 `'T`，旧式/复杂形式可能出现 `^T` | 运算符或基于成员的编译期抽象 | 不要同普通泛型混淆；检查推断约束与特化成本 |
| 灵活类型 | 类型标注内的 `#SomeBase`，等价于带子类型约束的泛型 | 高阶或嵌套输入应接受任何子类型/接口实现 | 同预处理指令和普通向上转型区分；保持公开签名可读 |
| byref 与 Span | `&value`、`byref<'T>`、`inref<'T>`、`outref<'T>`、`Span<'T>`、`ReadOnlySpan<'T>` | 互操作或经测量的同步缓冲区/复制热点 | 遵守栈与生命周期规则；不要跨 async 或堆边界捕获；采用前先测量 |

Quotations 表示表达式；它们不会自行执行。SRTP 在编译期特化内联代码，日常 `'T` 函数并不需要它。灵活 `#Type` 语法表达对象层次中的兼容性，不是注释或编译器命令。Byref-like 值用普通可组合性换取受限生命周期。

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

## 避免常见脚本与包错误 {#common-mistakes}

- 只在有状态 REPL 中验证结果，或把交互式 `;;` 带进普通脚本；
- 假定路径相对脚本，或让加载的辅助脚本在顶层写入；
- 省略 NuGet 版本、把一个 `#r` 当成闭包锁，或未读构建行为就启用 `usepackagetargets=true`；
- 按下载量/品牌选包、把干净审计当成安全证明，或使用 feed 却不控制包来源；
- 重写未变化输出，或依赖枚举顺序、区域性、本地时间和开发者路径；
- 打印错误却向 CI 返回退出码 `0`；
- 删除未经检查的宽泛路径，或记录/嵌入命令行机密；
- 声称摘要认证生产者，而不是只标识字节；
- 在目标图出现前引入构建 DSL，或脚本已成公开多模块产品后仍不迁移；
- 前置高级语言特性，或把完成特性清单误认为具备系统设计、交付与维护能力。

## 练习 {#exercises}

### 练习 1：增加排除规则而不失确定性 {#exercise-01}

扩展清单脚本设计，使其接受可重复的 `--exclude GLOB` 规则，用来排除生成日志与符号文件。定义 glob 语义、分隔符/大小写策略、规则匹配文件还是目录、无效模式行为、如何报告被排除链接，以及规则集如何进入清单 schema。保持 `write`/`check` 一致、稳定排序、输出排除、幂等性与跨 Windows/类 Unix 路径的有界测试。决定实现一份小型、有文档的匹配器，还是采用包。

### 练习 2：编写包采用记录 {#exercise-02}

团队想为升级后的清单工具增加命令行解析器。比较手写解析与两个当前 NuGet 候选。记录所需语法、帮助/错误行为、目标框架、包/来源身份、许可证、维护、传递/构建资产、漏洞、裁剪/AOT 需求、测试体验、直接版本、锁定步骤、更新负责人和替换边界。为最困难需求构建一个聚焦试验，并给出可逆决策。

### 练习 3：规划接下来十二周 {#exercise-03}

从本章选择一条项目路线。定义三个为期四周的增量，每个都要交付可运行结果，不能只完成阅读。写明要重读的 F# 概念、一个真实 .NET 或平台边界、测试与诊断、包预算、部署或分发目标、评审问题，以及简化或改用其他设计的标准。只有测量结果提出明确需求时才引入高级特性。

[阅读本章练习答案](../solutions/ch-45-scripting-packages-next)。

第七部分至此完成。附录会把本书变成工作参考：环境配置、语法、集合、C# 迁移、编译器诊断、术语、答案评审和高级特性识别索引。

## 资料来源 {#sources}

- [Microsoft Learn：使用 F# 进行交互式编程](https://learn.microsoft.com/en-us/dotnet/fsharp/tools/fsharp-interactive/)
- [Microsoft Learn：F# Interactive 选项](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/fsharp-interactive-options)
- [Microsoft Learn：PackageReference 与锁文件行为](https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files)
- [Microsoft Learn：查找与评估 NuGet 包](https://learn.microsoft.com/en-us/nuget/consume-packages/finding-and-choosing-packages)
- [Microsoft Learn：审计包依赖](https://learn.microsoft.com/en-us/nuget/concepts/auditing-packages)
- [Microsoft Learn：Package Source Mapping](https://learn.microsoft.com/en-us/nuget/consume-packages/package-source-mapping)
- [Microsoft Learn：.NET 工具](https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools)
- [FAKE 文档](https://fake.build/)
- [Paket 文档](https://fsprojects.github.io/Paket/)
- [Microsoft Learn：代码 quotations](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/code-quotations)
- [Microsoft Learn：静态解析类型参数](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/generics/statically-resolved-type-parameters)
- [Microsoft Learn：灵活类型](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/flexible-types)
- [Microsoft Learn：byref 与 byref-like 结构体](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/byrefs)
- [Microsoft Learn：F# 导览](https://learn.microsoft.com/en-us/dotnet/fsharp/tour)
- [FSharp.Core API 文档](https://fsharp.github.io/fsharp-core-docs/)
