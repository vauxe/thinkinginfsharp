---
title: "第 45 章：脚本、自动化、包生态与继续学习"
description: "把 F# 脚本变成确定性的本地自动化，审慎选择并锁定包，再建立持续掌握 F# 的实用途径。"
translationKey: part-07/ch-45-scripting-packages-next
kind: chapter
part: 7
chapter: 45
status: complete
verifiedWith:
  fsharp: "10"
  dotnetSdk: "10.0.301"
exampleIds:
  - ch45-scripting-packages-next
exerciseIds:
  - ch45-exercise-01
  - ch45-exercise-02
  - ch45-exercise-03
termIds: []
sources:
  - id: microsoft-fsi
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/tools/fsharp-interactive/
    checked: "2026-08-25"
  - id: microsoft-fsi-options
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/fsharp-interactive-options
    checked: "2026-08-25"
  - id: microsoft-package-reference
    url: https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files
    checked: "2026-08-25"
  - id: microsoft-package-evaluation
    url: https://learn.microsoft.com/en-us/nuget/consume-packages/finding-and-choosing-packages
    checked: "2026-08-25"
  - id: microsoft-nuget-audit
    url: https://learn.microsoft.com/en-us/nuget/concepts/auditing-packages
    checked: "2026-08-25"
  - id: microsoft-package-source-mapping
    url: https://learn.microsoft.com/en-us/nuget/consume-packages/package-source-mapping
    checked: "2026-08-25"
  - id: microsoft-dotnet-tools
    url: https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools
    checked: "2026-08-25"
  - id: fake-build
    url: https://fake.build/
    checked: "2026-08-25"
  - id: paket-docs
    url: https://fsprojects.github.io/Paket/
    checked: "2026-08-25"
  - id: microsoft-quotations
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/code-quotations
    checked: "2026-08-25"
  - id: microsoft-srtp
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/generics/statically-resolved-type-parameters
    checked: "2026-08-25"
  - id: microsoft-flexible-types
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/flexible-types
    checked: "2026-08-25"
  - id: microsoft-byrefs
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/byrefs
    checked: "2026-08-25"
  - id: microsoft-fsharp-tour
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/tour
    checked: "2026-08-25"
  - id: fsharp-core-api
    url: https://fsharp.github.io/fsharp-core-docs/
    checked: "2026-08-25"
---

# 第 45 章：脚本、自动化、包生态与继续学习 {#overview}

F# 脚本并不是较低等的 F# 程序。它使用与编译项目相同的语言、FSharp.Core 和 .NET 运行时，只是选择了更短的装配与执行路径。因此，`.fsx` 很适合探索、仓库维护、数据修复、发布检查与小型本地工具。

更短的路径并不会消除工程责任。脚本可能依赖隐藏的会话状态、调用者工作目录、可变包源、不稳定遍历顺序、含糊退出码，或每次执行都会发生的写入。一旦其他人或 CI 依赖它，这些细节就是它的接口。

本书最后一章会把一份真实脚本变成可靠的本地自动化边界。随后解释如何判断一个包是否值得进入依赖图、如何锁定真正交付的图、脚本何时应升级为项目或工具，以及如何继续学习 F#，而不是立刻追逐每项高级特性。

## 学完本章后你将能够做什么 {#outcomes}

学完本章后，你应该能够：

- 在 REPL 提交、`.fsx` 脚本、控制台项目、本地 .NET 工具和构建 DSL 之间选择；
- 在全新进程中运行脚本并传入显式命令行参数；
- 区分进程工作目录与脚本源码目录；
- 使用 `#load`、`#r`、`#I` 与 `#r "nuget: ..."`，同时不隐藏其顺序和信任含义；
- 围绕显式输入、确定性规划、有界效果与有意义退出码设计自动化；
- 通过在替换前比较期望内容和现有内容，让生成文件具备幂等性；
- 解释 SHA-256 清单能检测什么，又不能认证什么；
- 从适配性、兼容性、来源、维护、许可证、漏洞和退出成本评估 NuGet 包；
- 区分精确直接包版本与已锁定的传递依赖闭包；
- 根据问题而不是 F# 身份，在 PackageReference、本地工具、FAKE 与 Paket 之间选择；
- 识别 quotations、SRTP、灵活类型和 byref/Span，同时不把它们当作前置知识；
- 把前 44 章变成一条带证据与反馈的项目式学习循环。

## 选择能保留契约的最小执行表面 {#execution-surface}

正确表面是能让所需行为可重复的最小表面。这里的“最小”指运维契约，而不只是文件数量。

| 表面 | 最适合的首次用途 | 可复现边界 | 升级信号 |
|---|---|---|---|
| REPL 提交 | 检查一个表达式、类型或 API | 当前 FSI 进程及其隐藏状态 | 结果需要重复或评审 |
| `.fsx` 脚本 | 本地自动化、实验、迁移、报告 | 脚本、SDK、参数、文件、环境和包源 | 出现多个模块、测试、发布或稳定公开 CLI |
| 控制台项目 | 受维护命令、定时作业、更丰富测试 | 项目图、目标框架、锁文件、构建与发布产物 | 安装与跨仓库复用变得重要 |
| 本地 .NET 工具 | 仓库范围内具有稳定命令的可执行程序 | 工具清单、包还原与运行时兼容性 | 组织范围分发或 API 版本管理增长 |
| FAKE 等构建 DSL | 命名构建目标与依赖图 | DSL/工具版本、脚本依赖、目标图与被调用工具 | 图或定制集成足以证明另一层抽象的价值 |

不要只因项目看起来更正式，就升级一份 70 行脚本。当脚本已经成为产品时就应升级：调用者依赖它的命令语法，多个文件形成内部架构，还原必须锁定，单元测试需要普通发现机制，或部署需要自包含可执行程序。

同样，不要为避开 `.fsproj` 而把真实应用压进一份脚本。一旦程序需要文件顺序、公开 API 边界、构建属性、分析器、测试项目与发布，这些都是有用约束。

### 用 REPL 回答一个问题 {#repl-question}

`dotnet fsi` 会启动 F# Interactive。REPL 提交很适合询问编译器推断出什么签名、某个 BCL 方法怎样工作，或一项小转换是否可行。`;;` 终止符属于交互提交；普通 `.fsx` 文件并不需要它。

会话会记住此前的绑定、已打开命名空间、已加载文件、已引用程序集与包解析结果。探索时这很方便，作为证据时却很危险。保存一个结果前，应把必要代码放进脚本并在全新进程中执行。

### 用脚本承载完整、可评审的操作 {#script-operation}

脚本应像小型应用一样清楚说明输入、输出、失败行为和拥有的效果。它仍可很简洁。X45 只有一个文件，只使用 .NET 随附的库，不建立全局安装，并可从仓库根目录调用。

有用的区分不是“一次性与生产”，而是“有界操作与增长中的产品”。一次数据修复对验证、备份和审计证据的要求，可能比长期开发便利工具更严格。

### 当构建图成为答案的一部分时使用项目 {#project-promotion}

当你需要多个编译文件、普通单元测试发现、分析器、生成文档、项目引用、受控目标框架、`packages.lock.json`、发布、裁剪/AOT 检查或受支持命令契约时，应迁移到 F# 控制台项目。脚本里的纯函数几乎可以原样迁移；重要变化是明确了构建与分发边界。

如果贡献者应在仓库中调用一个有版本的命令，本地 .NET 工具可能合适。提交其 `.config/dotnet-tools.json`，用 `dotnet tool restore` 还原，并记住工具以用户权限运行。有版本的工具清单控制请求哪个工具包；它不会让不受信任的工具代码变安全。

## 理解 FSI 执行的内容 {#fsi-model}

Microsoft 把命令形状记为 `dotnet fsi [options] [script-file [arguments]]`。脚本运行时，`fsi.CommandLineArgs[0]` 是脚本路径，后续元素才是它的参数。当某个参数看起来可能像 FSI 选项时，`--` 会告诉 FSI 把余下 token 当作脚本参数。

X45 接受以下形式：

```console
dotnet fsi --exec examples/scripts/ch45-scripting-packages-next.fsx write ./artifacts ./artifacts.manifest.json
dotnet fsi --exec examples/scripts/ch45-scripting-packages-next.fsx check ./artifacts ./artifacts.manifest.json
```

`--exec` 会运行脚本后退出，而不是留在交互模式。`write` 让输出收敛到期望内容。`check` 不写入，并在输出缺失或过期时返回退出码 `2`。意外失败返回 `1`；成功返回 `0`。

### 工作目录与源码目录回答不同问题 {#script-paths}

相对进程路径从调用者当前工作目录解析。对于 `./artifacts` 这样的命令参数，这很有用，因为其含义由调用者拥有。这也意味着，从另一目录调用脚本时，脚本不能假设这些路径位于自身旁边。

当资源属于脚本自身时，用 `__SOURCE_DIRECTORY__` 锚定。`__SOURCE_FILE__` 标识当前源文件。调用者拥有的输入使用调用者相对路径，脚本拥有的资产使用源码相对路径，并在工作开始的边界把它们转成绝对路径。不要悄悄混合两种模型。

环境变量、当前区域性、时区、当前时间、随机种子、网络状态与已安装 SDK 也都是输入。可复现性重要时，在边缘只读取一次、执行验证，再把普通值向内传递。

### 指令是有顺序的编译输入 {#directives}

FSI 按顺序处理脚本声明。其主要指令包括：

- `#load "helpers.fsx"` 会在后续代码使用其定义前编译并执行另一份脚本；
- `#r "library.dll"` 引用一个程序集文件；
- `#I "directory"` 为后续引用增加程序集搜索路径；
- `#r "nuget: PackageId, Version"` 还原并引用一个 NuGet 包；
- 当一个文件也会在别处编译时，`INTERACTIVE` 等条件符号可隔离只供 FSI 使用的声明。

这些不是普通运行期函数调用。引用缺失或不兼容会使后续脚本代码无法编译。`#load` 还会执行被加载脚本的顶层效果，因此在加载时写文件的“辅助脚本”具有隐藏启动行为。

让可复用的被加载脚本在顶层无效果。把行为放进命名函数，并由一份入口脚本拥有执行。当不断增长的 `#load` 集合开始重新发明项目文件顺序时，请使用项目。

## X45：生成稳定的产物清单 {#x45}

X45 解决一个实用本地问题：枚举产物目录下的文件，在确定性 JSON 中记录规范化相对路径、字节长度与 SHA-256 摘要，然后更新清单或验证它仍为当前版本。

它的契约有意保持狭窄：

- 输入是一个现有本地目录与一个输出文件路径；
- 目录遍历跳过符号链接，并拒绝符号链接形式的根；
- 若输出文件位于源目录下，会排除输出文件自身；
- 所有平台的路径都使用 `/`，条目按 ordinal 路径顺序排列；
- JSON 的 schema 版本为 `1`，采用无 BOM UTF-8，并恰有一个结尾换行；
- 期望内容不变时，不触碰现有输出；
- 替换先在输出目录创建唯一命名文件，再将其移动覆盖目标；
- 无参数执行会拥有并删除一个唯一临时夹具，供仓库验证使用。

### 建模可观察结果，而非偶然步骤 {#manifest-model}

脚本把规划数据与写入、检查结果区分开：

<<< @/../examples/scripts/ch45-scripting-packages-next.fsx#manifest-model{fsharp:line-numbers} [ch45-scripting-packages-next.fsx]

`ManifestPlan` 同时包含结构化条目和边界所需的精确文本字节。`Updated` 与 `Unchanged` 不是含义未说明的布尔值。`Current` 与 `Stale` 则把只读 CI 行为同变更操作分成不同契约。

模型保持很小，因为这是本地自动化。公开工具可能增加 schema 兼容性、结构化诊断、取消、日志与稳定序列化结果。那些需求就是升级信号。

### 明确遍历与哈希策略 {#artifact-scan}

文件系统适配器解析完整路径，用操作系统路径相等规则排除输出，递归跳过 reparse point，规范化所报告的分隔符，并对每个已打开流计算哈希：

<<< @/../examples/scripts/ch45-scripting-packages-next.fsx#artifact-scan{fsharp:line-numbers} [ch45-scripting-packages-next.fsx]

跳过链接能避免意外走出所选树或进入环。这是一项策略，而不是普遍规则：有意包含链接的部署格式需要安全记录链接目标。

以 `FileShare.Read` 打开文件，可阻止配合该约定的 Windows 写入者在哈希期间修改文件。这不是事务式文件系统快照，在跨平台时尤其如此。若生产者可能并发修改文件树，应先发布不可变暂存目录，或使用具有快照语义的存储机制。

SHA-256 让后续消费者检测字节是否不同于记录值。它不能确认谁生成了清单，也无法应对产物与清单同时被恶意替换。真实性需要签名或另一可信通道；发布来源还需要更多证据。

### 把确定性规划与效果应用分开 {#manifest-plan}

规划器用 `Utf8JsonWriter` 渲染 JSON，而不依赖未指定的反射顺序。它先排序条目，再固定属性顺序、大小写、缩进、编码与换行策略：

<<< @/../examples/scripts/ch45-scripting-packages-next.fsx#manifest-plan{fsharp:line-numbers} [ch45-scripting-packages-next.fsx]

该边界仍会读取文件，因此 `planManifest` 并非纯函数。重要分离在于，它会在决定是否改变输出前计算一份完整期望结果。对相同条目数组而言，`renderManifest` 本身是确定性的。

稳定输出能避免嘈杂 diff，并让相等比较具有意义。在枚举后排序可避免继承文件系统顺序。相对路径不会嵌入开发者的绝对目录。最终 JSON 不包含时间戳、机器名或随机标识符。

### 只有期望内容不同时才写入 {#idempotent-write}

应用层会比较现有文本与期望文本。只有存在差异时，才创建临时文件并替换目标：

<<< @/../examples/scripts/ch45-scripting-packages-next.fsx#idempotent-write{fsharp:line-numbers} [ch45-scripting-packages-next.fsx]

这给出了有用的幂等性质：一次成功 `write` 后，对未变化输入再次 `write` 会报告 `Unchanged`，且不改变输出时间戳。同目录临时文件让最终移动停留在一个文件系统内，并缩短可见不完整目标的窗口。

不要夸大该保证。代码没有请求持久化 flush，没有协调并发写入者，没有保留此前所有权限或元数据位，也不会恢复中断的网络文件系统。“完整本地写入后再替换”是准确说法；“任何崩溃下都具有事务式持久性”则不是。

`check` 会比较同一计划但不写入。这让 CI 失败可以行动：退出 `2` 表示应重新生成或提交清单，退出 `1` 表示操作本身失败。始终打印错误却返回 `0` 的脚本会破坏自动化组合。

### 用真实临时夹具验证幂等性 {#script-evidence}

无参数时，X45 会在 `Path.GetTempPath()` 下的唯一目录创建两个文件。它写入一次，把输出时间戳设置成哨兵值，再次写入，执行无变更检查，验证 ordinal 规范化路径，最后在 `finally` 中只删除这个自己拥有的目录。

从仓库根目录运行已验证切片：

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

仓库会在 .NET SDK `10.0.301` 与 F# 10 下，用全新 FSI 进程执行该脚本。证据覆盖临时夹具、精确输出顺序、幂等第二次写入、只读当前检查、路径规范化与清理。它不覆盖恶意目录、并发生产者、数百万文件、远程文件系统、签名，或每一种 Windows/Linux 文件系统。

## 一旦另一调用者依赖自动化，就把它当作公开接口 {#automation-interface}

脚本可能没有程序集 API，但仍会暴露契约：

- 命令名、参数顺序、默认值与帮助文本；
- 可接受的路径形式，以及路径是否相对于调用者；
- 标准输出中的数据、标准错误中的诊断与退出码；
- 创建、替换或删除的文件，以及每一项的所有权规则；
- 顺序、编码、区域性、时间与 schema 稳定性；
- 包、SDK、工具、操作系统和外部命令假设；
- 部分失败、取消、重复执行与并发调用下的行为。

记录调用者可能据以自动化的部分。面向人的措辞可以演化；机器消费的输出需要 schema 或明确的不稳定状态。当 JSON 结果或退出码才是真实契约时，不要让 CI 解析装饰性日志。

### 优先收敛，而不是顺序编辑 {#convergence}

幂等自动化从当前输入计算期望状态，再向它收敛。这比“按顺序执行这些追加操作”更强。每次运行都追加生成行、增加重复配置项，或重命名第一个碰巧匹配的文件，都会积累依赖历史的状态。

先规划还会支持 `check` 与 dry-run 模式。应用前可以把计划作为数据测试。当操作具有破坏性或外部可见时，应渲染精确目标集合并要求调用者明确选择模式，而不是从环境名称推断权限。

幂等并不免疫错误输入。确定性脚本可以可靠地生成错误文件。验证源契约，测试代表情形与失败，并保留可评审 diff。

### 用 shell 做组合，用 F# 做类型化决策 {#shell-boundary}

Shell 脚本很擅长调用命令和连接流。当数据解析、分支、转义、集合、错误模型或文件系统规则成为主体时，其可移植性会变差。F# 能给这些决策加上类型并使用普通 .NET API，同时仍可在适当时调用外部进程。

不要只是为了宣称构建使用 F#，就把每个 `dotnet build` 包进 F#。仓库提供的短任务运行器可能更清楚。当 F# 拥有实质解析、规划、验证、并发或可复用策略时再引入它。

调用进程时，应传入参数列表，而不是构造未转义 shell 字符串；捕获退出状态与有界输出，传播取消，并决定继承哪些环境变量。机密不能出现在命令行或普通日志中。

### 限制不受信任且昂贵的输入 {#automation-safety}

X45 面向可信本地产物树。暴露给不受信任路径的工具还需要文件数与字节限制、权限策略、特殊文件处理、竞态分析、超时/取消、输出大小边界，或许还需要沙箱。校验和清单不是永远读取攻击者控制设备文件的理由。

执行操作前解析破坏性目标。绝不能从缺失环境变量、宽泛通配符、仓库根或主目录推导递归删除根。优先使用任务拥有的临时目录与可恢复移动。验证解析后的目标仍位于预期根中。

凭据应放在环境的机密机制中，而不是源码、夹具文件、清单、异常消息或生成报告。本地脚本以调用用户权限运行；“只是一份脚本”并不是安全边界。

## 只有说清缺失能力后才添加包 {#package-choice}

X45 的第一个设计问题是它是否需要包。`System.IO`、`SHA256` 与 `Utf8JsonWriter` 已满足这份有界契约，所以除 SDK 与 FSharp.Core 外，正确依赖数为零。

这并非反对包的极简主义。受维护的解析器、协议客户端、数据库驱动、测试库或框架，可能消除远多于自身引入的风险。重点是用书面需求比较包，而不是把寻找包当成架构设计。

### 先评估适配性，再看流行度 {#package-scorecard}

对于候选包，至少记录：

| 问题 | 要检查的证据 | 应拒绝或开展试验的情形 |
|---|---|---|
| API 是否解决精确问题？ | 最小代表调用、错误/取消模型、数据所有权 | 演示只有在巨大适配器或隐藏全局状态下才工作 |
| 是否支持目标？ | 包目标框架、运行时/原生资产、AOT/浏览器/平台说明 | 交付目标缺失或仅被假定兼容 |
| 谁拥有它？ | 包所有者、源码仓库、许可证、发布历史、issue/review 活动 | 无法确认来源或许可证 |
| 传递引入了什么？ | 完整依赖图、构建/分析器/内容资产、原生二进制 | 闭包不成比例或与宿主冲突 |
| 运维模型可接受吗？ | 线程、网络、文件、反射、生成代码、日志、配置 | 关键行为无法观察或控制 |
| 团队能更新并退出吗？ | 迁移说明、所用 API 表面、替代接缝、数据格式 | 移除需要重写领域或已存数据 |
| 存在哪些证据？ | 真实目标上的聚焦测试与还原/构建/运行检查 | 决策只有 README 片段或下载量支持 |

NuGet 的官方包评估指南指向版本历史、项目/源码链接、所有者、许可证、依赖、使用情况与漏洞信息。这些是信号，不是未来维护证明。流行包可能不适合目标；小包在契约与所有权清楚时也可能很优秀。

开展范围受限的采用试验（spike，即为验证关键风险而构建、便于删除的小型实现）。测试最困难的代表行为、一个失败、目标兼容性与移除接缝。记录检查的版本和日期，因为包状态会变化。

### `#r "nuget:"` 很方便，但不是锁文件 {#script-packages}

FSI 支持这样的包引用：

```fsharp
#r "nuget: PackageId, 1.2.3"
open PackageNamespace
```

省略版本会在解析时请求最高可用非预览版本。它适合可丢弃探索，却不适合版本化自动化契约。应在提交的脚本中写入精确直接版本。

一个 `#r` 指令中的精确版本不会为完整传递图创建仓库 `packages.lock.json`。它还依赖有效 NuGet 配置、包源、凭据、缓存与网络可用性。不要把固定指令描述成锁定还原。

FSI 通常不会使用包构建目标。其文档化 `usepackagetargets=true` 选项会为那些按设计需要此行为的包启用目标。只有理解确切需要后才启用：构建目标是可执行的还原/构建行为，会扩大信任与兼容表面。

如果脚本的依赖闭包必须在 CI 中评审并复现，就把自动化移进采用 PackageReference 与已提交锁文件的项目，或采用能为脚本工作流提供显式已提交锁的依赖管理器。一文件美感不值得换取不可验证供应链。

### 锁定真正运行的图 {#locking}

在 SDK 风格应用或工具项目中，指定直接 PackageReference 版本并启用 NuGet 锁文件生成。提交 `packages.lock.json`，再在 CI 中执行 `dotnet restore --locked-mode`。锁定模式会还原记录闭包，或在项目依赖会改变它时失败。

锁文件回答解析问题，而不是信任或运行期正确性。它不证明包安全、许可证适用于产品、与目标兼容或行为正确。它也不会强迫消费应用的图使用库项目的私有解析；顶层消费者会解析自己的闭包。

SDK 与工具版本也应显式。本仓库用 `global.json` 固定 SDK，用 `.config/dotnet-tools.json` 固定本地工具，用项目锁文件固定 NuGet 图，并用工作区锁文件固定 JavaScript 工具。每种机制覆盖不同的图。

有意更新：改变一个有界集合，重新生成锁，检查直接与传递差异，阅读相关发布说明，运行聚焦测试和完整测试，并保留回滚。“最新”是查询结果，不是评审策略。

### 把还原当作供应链操作 {#package-security}

包与 .NET 工具会通过运行时代码、构建目标、分析器、生成器、原生资产或工具入口点，以实质权限执行。使用可信来源、保护凭据并评审源配置。公有和私有 feed 并存时，Package Source Mapping 可以约束哪些源可提供每个直接与传递包 ID。

NuGet audit 会在还原期间把已解析依赖与已知漏洞数据比较。按策略处理发现，并保持 audit 源可用。没有发现意味着“此配置下未报告匹配的已知公告”，而不是“包是安全的”。

优先使用仓库范围配置与本地工具，而不是未记录的机器状态。不要提交还原包缓存或凭据。当依赖事故需要解释时，应保留还原日志与锁 diff。

## 把 F# 生态读成分层，而不是购物清单 {#ecosystem-map}

第七部分探索的生态位于多层所有权之上：

| 层 | 本书中的例子 | 第一个兼容性问题 |
|---|---|---|
| F# 语言与 FSharp.Core | 联合、模式匹配、集合、async、quotations | 需要哪个语言/编译器和 FSharp.Core 契约？ |
| .NET 运行时与 BCL | 文件、JSON、HTTP、task、诊断、密码学 | 需要哪个 TFM、运行时、OS 与 API 行为？ |
| Microsoft 平台框架 | ASP.NET Core、hosting、容器、Aspire 集成 | 适用哪个受支持平台版本与部署模型？ |
| F# 社区库 | FsCheck、Giraffe/Falco/Oxpecker、FSharp.Data、Elmish | 哪种 API 价值足以抵消包与维护成本？ |
| 跨语言 UI/工具链 | Fable/npm/浏览器、Avalonia 后端、Unity Editor/IL2CPP | 哪些编译器、宿主、原生工具与发布矩阵必须一致？ |
| 仓库自动化 | 脚本、本地工具、FAKE、Paket、CI runner | 哪个图拥有顺序、还原、凭据与证据？ |

F# 参与整个 NuGet 生态，而不仅是名称含“FSharp”的包。许多普通 .NET 库可以直接使用。集成问题在于 API 形状：null、委托、task、异常、可变性、反射、重载、序列化和面向 C# 的 builder 可能需要窄适配器。

反过来，F# 原生包也不会自动成为最佳选择。像评估其他依赖一样检查目标框架、发布证据、传递资产与团队理解。

### FAKE 与 Paket 解决不同问题 {#fake-paket}

[FAKE](https://fake.build/) 是带目标依赖和常用工具模块的 F# 构建任务 DSL。当命名目标图、可复用构建集成或更丰富编排能实质澄清构建时选择它。对于四条线性命令，普通仓库任务文件可能仍更清楚。

[Paket](https://fsprojects.github.io/Paket/) 是另一种 .NET 依赖管理器，具有自己的依赖与锁模型，也支持脚本集成。应因为该模型或现有仓库需要它而选择，不要因为 F# 代码必须使用与 F# 相关的包管理器。没有明确分工时，不要让 NuGet 与 Paket 同时管理同一所有权边界。

两种工具都会增加概念、引导过程、版本与失败模式。当这些成本替代了更大的偶然复杂度时，其价值是真实的。迁移前应针对实际 CI 与开发环境开展试验。

## 识别高级特性，不要把它们前置 {#advanced-recognition}

掌握本书已经覆盖的基础后，你就能阅读大多数生产 F#：类型、函数、模式匹配、集合、模块、效果、async/task、.NET 边界与测试。四项特性经常显得比引入它们的问题更神秘。现在只需学习其识别信号与停止条件：

| 特性 | 识别信号 | 可能遇到它的原因 | 下一步 |
|---|---|---|---|
| quotations | `<@ expression @>`、`<@@ expression @@>`、`Expr<'T>`、quotation 模式 | 库把 F# 代码表示成数据，用于 DSL、查询、分析或生成 | 区分构造/遍历表达式树与执行它；阅读库契约 |
| SRTP | `inline` 加静态/成员约束；当前简化语法可能使用 `'T`，旧式/复杂形式可能出现 `^T` | 运算符或基于成员的编译期抽象 | 不要同普通泛型混淆；检查推断约束与特化成本 |
| 灵活类型 | 类型标注内的 `#SomeBase`，等价于带子类型约束的泛型 | 高阶或嵌套输入应接受任何子类型/接口实现 | 同预处理指令和普通向上转型区分；保持公开签名可读 |
| byref 与 Span | `&value`、`byref<'T>`、`inref<'T>`、`outref<'T>`、`Span<'T>`、`ReadOnlySpan<'T>` | 互操作或经测量的同步缓冲区/复制热点 | 遵守栈与生命周期规则；不要跨 async 或堆边界捕获；采用前先测量 |

Quotations 表示表达式；它们不会自行执行。SRTP 在编译期特化内联代码，日常 `'T` 函数并不需要它。灵活 `#Type` 语法表达对象层次中的兼容性，不是注释或编译器命令。Byref-like 值用普通可组合性换取受限生命周期。

[附录 H：高级特性识别索引](../appendices/h-advanced-index)提供聚焦入口与交叉链接。它有意不把这些特性变成第二套入门课程。第 11 章锚定泛型约束与 SRTP；第 31 章锚定经过测量的 Span/byref 决策。

## 通过建立反馈循环继续，而不是勾完特性清单 {#learning-next}

读完一本书会给你地图，而不会自动带来熟练度。熟练来自重复循环：编译器、测试、运行时证据与另一位读者都可以推翻你的第一版设计。

使用以下循环：

1. 选择一个真实、有界的问题，其失败足以暴露权衡；
2. 在选择框架前建模输入、有效状态、预期失败与效果；
3. 穿过真实边界构建最小垂直切片；
4. 检查推断签名，并让含糊所有权变显式；
5. 按比例测试纯规则、适配器、失败路径与真实目标；
6. 在为性能改变表示前，先分析或插桩；
7. 评审依赖图与部署图，而不只是源码；
8. 写下证据证明什么、不证明什么，以及何种情况会逆转选择；
9. 学习后简化，再以稍难一点的边界重复。

### 根据想学习的风险选择项目路线 {#project-tracks}

| 路线 | 第一个项目 | 更难的第二切片 | 应重读章节 |
|---|---|---|---|
| 语言与建模 | 验证并转换有版本本地格式的 CLI | 跨三个 schema 版本迁移并加入属性 | 7–18、28–30 |
| 后端与分布式系统 | 围绕纯工作流的已认证 API | 幂等持久化、重试、追踪、容器发布 | 20–24、33–39、42 |
| 数据与分析 | 可复现的摄取/清洗/报告流水线 | schema 漂移、大数据、notebook 升级项目 | 14–15、29–31、40 |
| 浏览器应用 | 带一个真实 API 的 Fable 状态机 | URL 所有权、取消、无障碍、bundle 预算 | 20、22–24、41 |
| 桌面或移动端 | 带纯更新逻辑的 Avalonia 桌面切片 | 打包、平台服务、已签名目标产物 | 25–32、43 |
| 游戏与模拟 | 薄宿主后的确定性 F# 规则 | 重放、存档迁移、帧分析、真实 IL2CPP Player | 12、20、24、27–31、44 |
| 工具与库 | 把 X45 升级为有测试的控制台工具 | 稳定 API/CLI、包发布、升级兼容性 | 16–17、26–31、本章 |

不要构建七个起步项目。选择未知点接近你的工作或兴趣的一条路线，再持续深入，直到部署与维护改变你的设计。

### 学会在三个层次导航来源 {#source-reading}

用语言参考核对精确语法与约束，用 FSharp.Core API 文档核对函数签名与行为，再用相关 .NET/平台文档核对运行时边界。社区抽象进入决策后，还要检查包自身源码、发布说明、测试与 issue。

运行小型编译器实验，而不是凭记忆争论。记录 SDK 与包版本。一篇博客可以教授持久思想，同时其设置命令、语法或兼容表已经老化；应把思想同当前契约分开。

从类型向内阅读陌生 F#：公开签名、领域 case、纯转换、效果端口、组合根，最后才是实现细节。当聪明运算符隐藏数据流时，索要推断类型并显式改写一次调用。

### 寻求能改变设计的反馈 {#community-feedback}

向评审者提出可证伪问题：“这个状态能否被非法构造？”、“哪个取消拥有这个 task？”、“第二次运行后会怎样？”，或“哪个 Player 证据支持这个包？”泛泛请求“评审我的 F#”只会得到泛泛认可。

向社区提问时，应提供最小复现、完整诊断、SDK/包版本、目标、期望行为、实际行为和已经排除的情况。这既尊重他人时间，也让答案对下一位读者有用。

从最小持久边界回馈：改进复现、文档示例、测试、issue、包元数据或聚焦修复。参与生态并不需要编译器专长。

## 避免常见脚本与包错误 {#common-mistakes}

- 在有状态 REPL 会话中证明结果，却从不运行全新脚本进程；
- 因交互提交使用 `;;`，就在 `.fsx` 中到处添加它；
- 无论调用者工作目录为何，都假定相对路径位于脚本旁边；
- 让被加载的辅助脚本在顶层执行写入；
- 省略 NuGet 版本，却称结果可复现；
- 把一个精确 `#r` 版本称为完整依赖闭包的锁；
- 未理解包的构建行为就使用 `usepackagetargets=true`；
- 按下载量或 F# 品牌选包，却没有代表性目标测试；
- 把干净漏洞审计当作依赖安全证明；
- 使用多个 feed，却不控制每个包可由哪个 feed 提供；
- 每次运行都重写生成文件，制造时间戳或 diff 噪声；
- 依赖文件系统枚举顺序、当前区域性、本地时间或开发者绝对路径；
- 打印错误却向 CI 返回退出码 `0`；
- 删除从未检查参数或环境变量推导出的宽泛路径；
- 记录命令行机密或把它们嵌入生成输出；
- 声称摘要认证生产者，而不是只标识字节；
- 在目标图出现之前引入构建 DSL；
- 脚本已经拥有公开 CLI、多个模块、包、测试与发布需求，却仍不升级；
- 在熟悉普通建模与效果前就学习 quotations、SRTP、灵活类型和 byref；
- 把完成特性清单误认为有能力设计、测试、发布并维护系统。

## 练习 {#exercises}

### 练习 1：增加排除规则而不失确定性 {#exercise-01}

扩展 X45 设计，使其接受可重复的 `--exclude GLOB` 规则，用来排除生成日志与符号文件。定义 glob 语义、分隔符/大小写策略、规则匹配文件还是目录、无效模式行为、如何报告被排除链接，以及规则集如何进入清单 schema。保持 `write`/`check` 一致、稳定排序、输出排除、幂等性与跨 Windows/类 Unix 路径的有界测试。决定实现一份小型、有文档的匹配器，还是采用包。

### 练习 2：编写包采用记录 {#exercise-02}

团队想为升级后的清单工具增加命令行解析器。比较手写解析与两个当前 NuGet 候选。记录所需语法、帮助/错误行为、目标框架、包/来源身份、许可证、维护、传递/构建资产、漏洞、裁剪/AOT 需求、测试体验、直接版本、锁定步骤、更新负责人和移除接缝。为最困难需求构建一个聚焦试验，并给出可逆决策。

### 练习 3：规划接下来十二周 {#exercise-03}

从本章选择一条项目路线。定义三个为期四周的增量，每个都以可执行证据而非只读材料结束。包括要重读的 F# 概念、一个真实 .NET 或平台边界、测试与诊断、包预算、部署或分发目标、评审问题，以及简化或逆转设计的标准。只有经测量问题要求时才引入高级特性。

[阅读本章练习答案](../solutions/ch-45-scripting-packages-next)。

## 本章回顾 {#model-review}

- REPL 回答一个问题；脚本保存一个有界操作；项目拥有增长中的构建与分发契约。
- FSI 按顺序执行声明，暴露显式脚本参数，并区分调用者工作目录与源码目录。
- 指令影响编译与还原；被加载脚本不应隐藏顶层效果。
- 可靠自动化具有显式输入、确定性期望输出、有界效果、有意义退出码与检查模式。
- X45 创建稳定 SHA-256 JSON 清单，按策略跳过链接，只在变化时写入，并在真实临时夹具中证明幂等性。
- 摘要检测字节差异，却不认证来源；同目录替换并非通用崩溃持久性。
- 为命名能力添加包前，应测试 API 适配、目标支持、来源、闭包、运维、维护与退出成本。
- 精确 `#r "nuget:"` 版本固定一个请求，却不是已提交的传递锁图。
- PackageReference 锁文件、本地工具清单、FAKE 与 Paket 解决不同所有权问题。
- 还原是供应链操作；可信来源、源映射、审计、锁评审与回滚是不同控制。
- F# 生态包括整个 .NET 生态、F# 原生抽象与跨语言工具链。
- Quotations、SRTP、灵活类型和 byref/Span 在具体问题证明需要深入前，都只是识别主题。
- 持续掌握来自垂直项目、编译器与运行时证据、评审问题、简化和重复发布循环。

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
