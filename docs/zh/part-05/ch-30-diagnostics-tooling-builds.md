---
title: "第 30 章：诊断、调试、格式化与构建"
description: "阅读首个相关编译器诊断，按所需证据选择 FSI 或调试器，以只读方式执行格式检查，并复现锁定的 Release 构建。"
translationKey: part-05/ch-30-diagnostics-tooling-builds
kind: chapter
part: 5
chapter: 30
status: complete
verifiedWith:
  fsharp: "10"
  dotnetSdk: "10.0.301"
exampleIds:
  - ch11-value-restriction
  - ch16-wrong-file-order
exerciseIds:
  - ch30-exercise-01
  - ch30-exercise-02
  - ch30-exercise-03
termIds: []
sources:
  - id: microsoft-fsharp-compiler-options
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/compiler-options
    checked: "2026-08-24"
  - id: microsoft-fsharp-interactive
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/tools/fsharp-interactive/
    checked: "2026-08-24"
  - id: microsoft-managed-debuggers
    url: https://learn.microsoft.com/en-us/dotnet/core/diagnostics/managed-debuggers
    checked: "2026-08-24"
  - id: microsoft-dotnet-build
    url: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-build
    checked: "2026-08-24"
  - id: microsoft-nuget-lock-files
    url: https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files#locking-dependencies
    checked: "2026-08-24"
  - id: microsoft-local-tools
    url: https://learn.microsoft.com/en-us/dotnet/core/tools/local-tools-how-to-use
    checked: "2026-08-24"
  - id: fantomas-getting-started
    url: https://fsprojects.github.io/fantomas/docs/end-users/GettingStarted.html
    checked: "2026-08-24"
  - id: fantomas-format-check
    url: https://fsprojects.github.io/fantomas/docs/end-users/FormattingCheck.html
    checked: "2026-08-24"
---

# 第 30 章：诊断、调试、格式化与构建 {#overview}

工具只有在缩短“症状到证据”的路径时才有用。编译器诊断回答静态问题，FSI 检验小表达式，调试器暴露某一次运行，格式化器消除样式差异，锁定构建则重建约定的依赖图。混淆这些职责只会制造没有诊断价值的仪式。

本章使用仓库命令，而不绑定某个编辑器或 CI 厂商。IDE 可以在命令外包一层按钮，但项目、锁文件、工具清单和可重复命令才是共享契约。

## 学完本章你将能够做什么 {#outcomes}

学完本章后，你应该能够：

- 阅读诊断中的路径、位置、严重性、编号与消息，同时不把位置误作确定的根因；
- 从首个相关诊断开始，并识别级联错误；
- 区分语法、类型、名称解析、项目顺序、还原、运行时与断言失败；
- 在保留复现命令与环境的前提下缩小失败；
- 用 FSI 检查推断与纯实验，而不把会话状态误作项目构建；
- 用断点、局部值、监视、调用栈与异常中断检验运行时假设；
- 还原锁定的本地工具，并用不修改文件的模式运行 Fantomas；
- 把编译器警告与格式化视为不同的质量信号；
- 分离锁定还原、Release 构建与测试阶段；
- 解释 SDK、包、工具与确定性构建设置能复现什么、不能复现什么。

## 阅读首个相关诊断 {#diagnostic-anatomy}

典型 F# 编译器诊断具有如下形态：

```text
path/File.fs(12,9): error FS0039: The value or constructor 'name' is not defined.
```

路径表示构建所见的源码；行列表示编译器察觉问题的位置；`error` 是严重性；`FS0039` 是可搜索、可断言的诊断编号；余下文本提供上下文。启用警告即错误时，警告可能被提升为失败，但仍保留其诊断身份。

报告位置不承诺就是根因。缺失的结束分隔符可能在数行后才被察觉。一个未解析类型可能让后续成员查找全部失败。F# 编译顺序错误可能让某个本应在前的文件中所有名称都像不存在一样。编译器会在错误后尝试恢复，以便报告更多问题，但恢复后的解释可能制造次生噪声。

从自己源码中最早的相关诊断开始，修复或解释它，然后重新构建。不要从底向上机械编辑每一条红线。如果第一条指向生成代码或依赖，应寻找更早、导致它的还原或构建失败。

### 选择工具前先给失败分类 {#failure-classes}

| 证据 | 可能类别 | 首选工具 |
|---|---|---|
| FS0010 一类的意外标记或缩进报告 | 解析/越界规则 | 编辑器加编译器 |
| FS0001 预期一种类型却得到另一种 | 类型推断或模型错误 | 完整消息、推断签名、小型 FSI 探针 |
| FS0039 名称或命名空间未定义 | 拼写、作用域、引用或文件顺序 | 项目文件与首个缺失符号 |
| NU 前缀的还原失败 | 依赖图、源或锁不一致 | `dotnet restore --locked-mode` 输出 |
| 构建通过，但值或副作用错误 | 运行时逻辑 | 聚焦测试，必要时再用调试器 |
| 测试给出预期值/实际值失败 | 行为回归或错误预期 | 最小失败测试与领域需求 |

编号告诉你类别，不告诉你修复方案。搜索 FS0039 会列出许多成因；只有周围源码、项目顺序和引用能从中做出选择。

## 两个有意的编译器失败 {#expected-errors}

预期错误样例把文档主张变成可执行证据。样例检查器要求命令必须失败，而且每个声明的诊断编号都必须出现。对这种样例而言，成功编译反而意味着测试失败。

### FS0030：一个值不能保持含糊的泛型 {#fs0030}

第 11 章的完整夹具只有一个绑定：

<<< @/../examples/expected-errors/ch11-value-restriction.fsx{fsharp:line-numbers} [ch11-value-restriction.fsx]

直接运行它：

```console
dotnet fsi --exec examples/expected-errors/ch11-value-restriction.fsx
```

F# 10 报告 FS0030 和弱类型 `'_a list array`。`Array.create` 构造了一个元素类型未解析的可变数组值；同一个存储位置不能安全地针对无关元素类型泛化。诊断本身给出三种有意修复：用具体标注确定类型；为泛型函数显露数据参数；或在每次调用都应构造新值时添加 `()`。

最小夹具移除了无关使用，因此首条诊断就是要学习的内容。它不是生产代码，绝不应通过屏蔽 FS0030 使其变绿。

### FS0039：文件顺序是 F# 项目的一部分 {#fs0039}

无效的第 16 章项目先编译 `Workflow.fs`，再编译 `Domain.fs`：

<<< @/../examples/expected-errors/ch16-file-order/Ch16WrongOrder.fsproj{xml:line-numbers} [Ch16WrongOrder.fsproj]

F# 项目中的文件顺序是显式的。一个文件只能使用更早文件中的定义，不能使用更晚文件中的定义。`Workflow.fs` 打开 `ThinkingInFSharp.Ch16.Domain`，所以尽管两个文件都存在且各自语法有效，这个顺序仍会产生 FS0039。

修复是在有效项目中把 `Domain.fs` 放到 `Workflow.fs` 前。把领域类型复制进 `Workflow.fs`、随意增加 `open`，或清理缓存，都没有修复依赖方向。首个缺失命名空间比随后缺失的 `Capacity`、`BookingRequest` 和联合案例更有力。

调查期间只运行无效项目：

```console
dotnet build examples/expected-errors/ch16-file-order/Ch16WrongOrder.fsproj \
  --configuration Release
```

随后运行有效的第 16 章项目，最后运行完整样例门。窄命令加速反馈；宽命令发现连带的接线变化。

## 在不改变失败的前提下缩减 {#reduction-loop}

严格的诊断循环很短：

1. 记录精确命令、配置、SDK 与首个相关输出。
2. 编辑前先复现；无法复现的报告属于另一项调查。
3. 移除无关代码或筛到单项测试，同时保持同一诊断或错误行为。
4. 陈述一个能预测观察结果的假设。
5. 使用能观察它的最廉价工具。
6. 做一次最小修改，重跑窄命令，再运行完整门。

缩减不是随机删除。如果移除项目引用让 FS0039 变成还原错误，复现已被改变。如果从 Release 切到 FSI 粘贴后条件编译消失，环境已被改变。应简要记录每项有意差异。

编译器输出常包含有用的推断类型。添加标注前应读完消息：标注可以暴露错误假设，也可能抹掉有用泛化。比起强迫编译器使用你希望的类型，更应解释推断为何选择了当前类型。

## 用 FSI 回答小型静态与动态问题 {#fsi}

`dotnet fsi` 是 .NET SDK 自带的读取—求值—打印循环；`dotnet fsi --exec file.fsx` 会执行脚本后退出。它适合回答以下问题：

- 这个表达式推断出了什么类型？
- 哪个模式分支处理这个值？
- 这个纯变换是否保持预期不变量？
- 某个小型 .NET API 调用对一个受控输入返回什么？

实验需要依赖时，用 `#r` 引用程序集或包，用 `#load` 加载脚本。让实验保持确定、小巧。一旦想法对产品有意义，就把它移入编译源码与自动测试。

### FSI 不是项目编译器 {#fsi-boundary}

FSI 会话保留早先绑定和已加载程序集。过期状态可能解释成功时，应重启会话。粘贴的表达式不会自动继承项目文件顺序、全部 MSBuild 属性、目标框架资产、条件符号或精确程序集边界。

FSI 定义 `INTERACTIVE`，编译代码定义 `COMPILED`。这种差异可以是有意设计，但也再次说明“在 FSI 中工作”只是局部证据。项目仍须以真实 `.fsproj` 和警告策略完成构建。

不要把大型工作流粘入 FSI 并手工重建依赖图。聚焦单元测试可重复且保留项目上下文；FSI 最适合一眼就能看全的问题。

## 用调试器观察某一次运行 {#debugger}

当编译成功但行为与假设矛盾时，应在支持该项目的 IDE 中附加托管 .NET 调试器。界面名称各异，但证据相同：

- 断点在可执行边界暂停；
- 局部值和监视显示所选栈帧中的值；
- 单步跳过会执行调用，单步进入会跟进其实现；
- 调用栈显示执行如何到达当前函数；
- 异常设置可以在异常抛出时暂停，而不只是未处理时暂停。

应把断点放在信息发生改变的位置：领域决策前、边界转换后或外部副作用前。在大量管道的代码中，如果给重要中间结果命名能让假设可观察，就给它命名。不要散布断点，直到某处碰巧显得可疑。

面对意外的 `Rejected(requested, capacity)`，先检查进入 `decide` 前已验证的请求与容量，再看提供它们的调用方栈帧。如果两个输入都正确，就逐步执行决策；若其中一个错误，则向外追到其生产者。这样是在追踪数据来源，而不是漫游控制流。

Debug 构建通常提供最清楚的单步与局部值。Release 优化可能重排、内联或省略可观察局部变量，即使程序行为仍正确。必要时应复现仅在 Release 出现的缺陷，但要知道调试器的源码视图可能没有那么字面。

### 不要意外改变证据 {#debugger-cautions}

求值监视表达式可能调用带副作用的属性或函数。在调试器中修改值证明的是修改后状态下的行为，而不是原始运行。应记录哪些动作只是观察，哪些改变了执行。

调试器会话不是回归测试。找到原因后，编写一个没有修复时失败、有修复时通过的最小自动测试。断点消失后，测试仍会保存证据。

## 用锁定且只读的检查格式化 {#formatting}

Fantomas 是源码格式化器，不是类型检查器或 linter。本仓库把它声明为本地 .NET 工具：

```json
{
  "version": 1,
  "isRoot": true,
  "tools": {
    "fantomas": {
      "version": "7.0.5",
      "commands": ["fantomas"]
    }
  }
}
```

还原精确声明的工具，并检查全部 F# 源码：

```console
dotnet tool restore
dotnet fantomas . --check
```

Fantomas 7 从 `.editorconfig` 读取格式设置；一个不会被读取的 `fantomas.json` 只会暗示并不存在的控制。在 7.0.5 中，干净检查退出码为 0，需要格式化的文件退出码为 99。`--check` 只报告差异，不写文件。应有意运行 `dotnet fantomas .` 来应用格式化，然后评审并测试机械变化。

锁定版本很重要，因为格式化器输出可能随版本变化。升级工具时应单独变更、建立一次新基线，并尽量避免把行为修改混入格式化差异。

### 格式化与静态分析回答不同问题 {#static-analysis}

Fantomas 规范布局。F# 编译器检查解析、名称解析、类型、约束与已启用警告。`TreatWarningsAsErrors` 让已发出的警告导致构建失败；未使用绑定等可选警告仍需有意启用。空值检查和分析器同样需要显式项目配置。

不要把已格式化的文件视为正确，也不要把无警告构建视为测试充分。格式化、静态编译、性质测试、边界测试与运行时观察覆盖不同风险。

屏蔽警告时，应缩小屏蔽范围，并记录被标记条件为何安全。仅为了让门变绿而全局屏蔽，会丢弃未来证据。

## 复现工具链与依赖图 {#reproducible-builds}

“可复现”具有多个层次：

| 层次 | 仓库证据 | 仍在仓库之外的内容 |
|---|---|---|
| SDK 选择 | `global.json` 选择 10.0.301 与 `latestPatch` | 精确宿主运行时与已安装补丁可能不同 |
| 直接与传递包 | `PackageReference` 加已提交 `packages.lock.json` | 包源可用性与外部凭据 |
| 本地工具 | `.config/dotnet-tools.json` 锁定 Fantomas 7.0.5 | 能运行工具的宿主运行时 |
| 编译器输出 | 相同输入配合 `Deterministic=true` | 平台原生资产、路径、编译器控制外时间戳 |
| 行为 | 测试与样例输出断言 | 未建模的外部服务和机器状态 |

`latestPatch` 有意允许同一 SDK 功能带中更晚的服务补丁；这是安全/维护权衡，不是逐字节 SDK 身份。调查环境特有失败时应记录 `dotnet --info`。

仅有 `Version="3.4.0"` 的 PackageReference 仍可能允许解析出不止一个传递图。锁文件记录已解析版本与内容哈希。`dotnet restore --locked-mode` 要么使用该图，要么在项目依赖与锁文件不一致时失败；它不会静默改写契约。

### 分离还原、构建与测试 {#build-stages}

重视可复现性时，应使用显式阶段：

```console
dotnet tool restore
dotnet fantomas . --check
dotnet restore ThinkingInFSharp.slnx --locked-mode
dotnet build ThinkingInFSharp.slnx --configuration Release --no-restore
dotnet test ThinkingInFSharp.slnx --configuration Release --no-build
```

`dotnet build` 通常会隐式还原。`--no-restore` 证明构建使用前一项锁定还原产生的图。`--no-build` 同样防止测试隐藏构建步骤。这些标志澄清阶段归属，并非性能装饰。

当陈旧产物可能是原因时，在锁定还原与 Release 构建前运行 `dotnet clean`。不要每次诊断都先删除缓存：先保存失败输出，再把干净构建用作受控实验。

处理困难的 MSBuild 问题时，`dotnet build -bl:<path>` 产生的二进制日志会记录求值与执行细节。它可能包含绝对路径、属性和源自环境的数据，因此应把它当诊断数据检查与处理，而不是自动公开。

## 紧凑的证据检查表 {#checklist}

在宣布工具问题已修复前，询问：

1. 什么精确命令和环境复现了问题？
2. 首个相关诊断或错误值是什么？
3. 它属于哪类失败，什么观察确认了假设？
4. 修复是否处理原因，而非屏蔽证据？
5. 窄复现现在是否通过，或产生预期诊断？
6. 格式检查是否以只读方式运行？
7. 锁定还原、Release 构建与全部测试是否从干净状态通过？
8. 已发现回归是否保存为自动测试或预期错误夹具？

当另一位读者能重放证据时，工具才成为工程实践；某台工作站碰巧变绿并不算。

## 练习 {#exercises}

### 练习 1：诊断文件顺序造成的级联 {#exercise-01}

无效的第 16 章构建先报告 `Domain` 命名空间不存在，随后报告多个领域类型不存在。解释应先处理哪条消息，指出项目文件修复，并列出两个会隐藏或复制模型、而不是修复顺序的诱人编辑。

### 练习 2：选择 FSI、测试与调试器 {#exercise-02}

一个已编译的预订工作流返回 `Rejected(3, 2)`，而调用方预期接受。描述一个小型 FSI 实验、一项聚焦自动测试和一个断点计划。说明每者提供什么证据，以及诊断结束后哪项产物会留下。

### 练习 3：审计可复现构建 {#exercise-03}

一位队友修改了一个包版本却忘记锁文件，使用全局 Fantomas，并报告温热工作树中的 Debug 构建成功。给出一组有序、平台无关的命令来暴露每项不一致，并说明哪些仓库文件必须有意更新。

[阅读本章练习答案](../solutions/ch-30-diagnostics-tooling-builds)。

## 模型回顾 {#model-review}

- 从首个相关诊断开始；后续错误可能只是恢复级联。
- 诊断位置是编译器察觉问题之处，不保证就是根因。
- 预期错误夹具必须失败，并包含声明的编号。
- FSI 回答小型推断与执行问题，但不能替代项目构建。
- 调试器通过值、栈帧与异常检验一个运行时假设。
- 应把调试器中的发现保存为自动回归测试。
- Fantomas 格式化与编译器静态分析回答不同问题。
- 锁定本地工具；当门不得改写源码时使用 `--check`。
- 锁文件复现已解析包图；锁定还原会在漂移时失败。
- 分离还原、构建与测试，让各阶段输入可见。
- 干净构建是受控实验，不是每次失败的第一反应。
- 可复现性具有层次，绝不替代记录实际环境。

## 来源 {#sources}

- [Microsoft Learn：F# 编译器选项与警告](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/compiler-options)
- [Microsoft Learn：F# Interactive 与脚本](https://learn.microsoft.com/en-us/dotnet/fsharp/tools/fsharp-interactive/)
- [Microsoft Learn：托管 .NET 调试器](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/managed-debuggers)
- [Microsoft Learn：`dotnet build`、隐式还原与构建日志](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-build)
- [Microsoft Learn：NuGet 依赖锁文件与锁定模式](https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files#locking-dependencies)
- [Microsoft Learn：本地 .NET 工具清单与还原](https://learn.microsoft.com/en-us/dotnet/core/tools/local-tools-how-to-use)
- [Fantomas：安装并使用本地格式化器](https://fsprojects.github.io/fantomas/docs/end-users/GettingStarted.html)
- [Fantomas：不修改文件的格式检查](https://fsprojects.github.io/fantomas/docs/end-users/FormattingCheck.html)
