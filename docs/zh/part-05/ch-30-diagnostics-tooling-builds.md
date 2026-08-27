---
title: "第 30 章：诊断、调试、格式化与构建"
description: "阅读首个相关编译器诊断，按问题选择 FSI 或调试器，以只读方式检查格式，并复现锁定的 Release 构建。"
translationKey: part-05/ch-30-diagnostics-tooling-builds
---

# 第 30 章：诊断、调试、格式化与构建 {#overview}

工具只有在缩短“症状到原因”的路径时才有用。编译器诊断回答静态问题，FSI 检验小表达式，调试器观察某一次运行，格式化器消除样式差异，锁定构建则按锁文件重建依赖图。混淆这些职责只会制造无效流程。

示例使用项目命令，不绑定某个编辑器或 CI 厂商。IDE 可以在命令外增加按钮，但项目文件、锁文件、工具清单和可重复命令才是共同的事实来源。

## 阅读首个相关诊断 {#diagnostic-anatomy}

典型的 F# 编译器诊断如下：

```text
path/File.fs(12,9): error FS0039: The value or constructor 'name' is not defined.
```

路径表示构建读取的源码，行号与列号标出编译器发现问题的位置。`error` 表示严重级别，`FS0039` 是可搜索的诊断编号，其余文本提供上下文。启用警告即错误后，警告可能变成构建失败，但仍保留原编号。

报告位置不承诺就是根因。缺失的结束分隔符可能在数行后才被察觉。一个未解析类型可能让后续成员查找全部失败。F# 编译顺序错误可能让某个本应在前的文件中所有名称都像不存在一样。编译器会在错误后尝试恢复，以便报告更多问题，但恢复过程中可能产生许多由首个错误引起的后续诊断。

从自己源码中最早的相关诊断开始，修复或解释它，然后重新构建。不要从底向上机械编辑每一条红线。如果第一条指向生成代码或依赖，应寻找更早、导致它的还原或构建失败。

### 选择工具前先给失败分类 {#failure-classes}

| 观察到的现象 | 可能类别 | 首选工具 |
|---|---|---|
| FS0010 一类的意外标记或缩进报告 | 解析或缩进规则 | 编辑器加编译器 |
| FS0001 预期一种类型却得到另一种 | 类型推断或模型错误 | 完整消息、推断签名、小型 FSI 实验 |
| FS0039 名称或命名空间未定义 | 拼写、作用域、引用或文件顺序 | 项目文件与首个缺失符号 |
| NU 前缀的还原失败 | 依赖图、源或锁不一致 | `dotnet restore --locked-mode` 输出 |
| 构建通过，但值或副作用错误 | 运行时逻辑 | 聚焦测试，必要时再用调试器 |
| 测试给出预期值/实际值失败 | 行为回归或错误预期 | 最小失败测试与领域需求 |

编号告诉你类别，不告诉你修复方案。搜索 FS0039 会列出许多成因；只有周围源码、项目顺序和引用能从中做出选择。

## 两个故意失败的编译示例 {#expected-errors}

预期错误示例让文档中的说明可以自动验证。检查器要求命令必须失败，而且每个声明的诊断编号都必须出现。对于这类示例，成功编译反而表示测试失败。

### FS0030：一个值不能保持含糊的泛型 {#fs0030}

第 11 章的完整测试示例只有一个绑定：

```fsharp:line-numbers [ch11-value-restriction.fsx — 预期错误 FS0030]
let ambiguousBuckets = Array.create 2 []
```
直接运行它：

```console
dotnet fsi --exec examples/expected-errors/ch11-value-restriction.fsx
```

F# 10 报告 FS0030 和弱类型 `'_a list array`。`Array.create` 构造了一个元素类型尚未确定的可变数组；同一个存储位置不能安全地泛化为互不相关的元素类型。诊断给出三种合理修复：添加具体类型标注；把数据改成泛型函数的参数；或在每次调用都应构造新值时添加 `()`。

最小示例移除了无关代码，因此第一条诊断就是要说明的问题。它不是生产代码，绝不能通过屏蔽 FS0030 让检查变绿。

### FS0039：文件顺序是 F# 项目的一部分 {#fs0039}

故意写错的第 16 章项目先编译 `Workflow.fs`，再编译 `Domain.fs`：

```xml:line-numbers [Ch16WrongOrder.fsproj]
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <Compile Include="../../chapters/ch16/Workflow.fs" Link="Workflow.fs" />
    <Compile Include="../../chapters/ch16/Domain.fs" Link="Domain.fs" />
  </ItemGroup>
</Project>
```
F# 项目中的文件顺序是固定的。一个文件只能使用排在它前面的定义，不能使用后面文件中的定义。`Workflow.fs` 打开了 `ThinkingInFSharp.Ch16.Domain`。因此，即使两个文件都存在且语法正确，这个顺序仍会产生 FS0039。

修复方法是在正确项目中把 `Domain.fs` 放到 `Workflow.fs` 前。把领域类型复制进 `Workflow.fs`、随意增加 `open` 或清理缓存，都没有修复依赖方向。第一个“命名空间缺失”诊断，比随后缺失的 `Capacity`、`BookingRequest` 和联合用例更有信息量。

调查期间只运行无效项目：

```console
dotnet build examples/expected-errors/ch16-file-order/Ch16WrongOrder.fsproj \
  --configuration Release
```

随后运行正确的第 16 章项目，最后运行完整示例检查。针对性命令能快速反馈，完整命令则能发现相关的项目配置变化。

## 在不改变现象的前提下隔离问题 {#reduction-loop}

聚焦的诊断循环很短：

1. 记录完整命令、配置、SDK 与首个相关输出。
2. 编辑前先复现；无法复现的报告属于另一项调查。
3. 移除无关代码或筛到单项测试，同时保持同一诊断或错误行为。
4. 陈述一个能预测观察结果的假设。
5. 使用能观察它的最廉价工具。
6. 做一次最小修改，重跑针对性命令，再运行完整验证。

隔离问题不是随机删除代码。如果移除项目引用让 FS0039 变成还原错误，复现条件就已经改变；如果从 Release 切到 FSI 粘贴后条件编译消失，环境也已经改变。应简要记录每项主动引入的差异。

编译器输出常包含有用的推断类型。添加标注前应读完消息：标注可以暴露错误假设，也可能抹掉有用泛化。比起强迫编译器使用你希望的类型，更应解释推断为何选择了当前类型。

## 用 FSI 回答小型静态与动态问题 {#fsi}

`dotnet fsi` 是 .NET SDK 自带的读取—求值—打印循环；`dotnet fsi --exec file.fsx` 会执行脚本后退出。它适合回答以下问题：

- 这个表达式推断出了什么类型？
- 哪个模式分支处理这个值？
- 这个纯变换是否保持预期不变量？
- 某个小型 .NET API 调用对一个受控输入返回什么？

实验需要依赖时，用 `#r` 引用程序集或包，用 `#load` 加载脚本。让实验保持确定、小巧。一旦想法对产品有意义，就把它移入编译源码与自动测试。

### FSI 不能替代项目构建 {#fsi-boundary}

FSI 会话会保留早先的绑定和已加载程序集。如果旧状态可能导致测试通过，应重启会话。粘贴的表达式不会自动继承项目文件顺序、全部 MSBuild 属性、目标框架资产、条件符号或真实程序集上下文。

FSI 定义 `INTERACTIVE`，编译代码定义 `COMPILED`。这种差异可以是设计的一部分，但“在 FSI 中可用”仍然只回答局部问题。项目必须继续使用真实 `.fsproj` 和警告策略构建。

不要把大型工作流粘入 FSI 并手工重建依赖图。聚焦单元测试可重复且保留项目上下文；FSI 最适合一眼就能看全的问题。

## 用调试器观察某一次运行 {#debugger}

编译成功但行为与假设矛盾时，应在支持该项目的 IDE 中附加托管 .NET 调试器。界面名称可能不同，但可以查看的信息相同：

- 断点在可执行位置暂停；
- 局部值和监视显示所选栈帧中的值；
- 单步跳过会执行调用，单步进入会跟进其实现；
- 调用栈显示执行如何到达当前函数；
- 异常设置可以在异常抛出时暂停，而不只是未处理时暂停。

应把断点放在信息发生改变的位置：领域决策前、外部输入转换后或外部副作用前。在大量使用管道的代码中，如果给重要中间结果命名有助于观察假设，就给它命名。不要到处添加断点，等待某处碰巧显得可疑。

面对意外的 `Rejected(requested, capacity)`，先检查进入 `decide` 前已验证的请求与容量，再看提供它们的调用方栈帧。如果两个输入都正确，就逐步执行决策；若其中一个错误，则向外追到其生产者。这样是在追踪数据来源，而不是漫游控制流。

Debug 构建通常提供最清楚的单步与局部值。Release 优化可能重排、内联或省略可观察局部变量，即使程序行为仍正确。必要时应复现仅在 Release 出现的缺陷，但要知道调试器的源码视图可能没有那么字面。

### 不要意外改变执行过程 {#debugger-cautions}

计算监视表达式可能调用带副作用的属性或函数。在调试器中修改值后，看到的是修改状态下的行为，不再是原始运行。应记录哪些动作只是观察，哪些动作改变了执行。

调试器会话不是回归测试。找到原因后，编写一个修复前失败、修复后通过的最小自动测试。断点消失后，测试仍能保留这个行为检查。

## 用锁定版本的工具检查格式 {#formatting}

Fantomas 是源码格式化器，不是类型检查器或 linter。项目可以把它声明为本地 .NET 工具：

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

还原清单中声明的工具版本，并检查全部 F# 源码：

```console
dotnet tool restore
dotnet fantomas . --check
```

Fantomas 7 从 `.editorconfig` 读取格式设置；放置一个不会被读取的 `fantomas.json`，只会制造已经配置的错觉。在 7.0.5 中，格式正确时退出码为 0，需要格式化时为 99。`--check` 只报告差异，不写文件。只有准备应用格式化时才运行 `dotnet fantomas .`，随后评审并测试这项机械修改。

格式化器输出可能随版本变化，因此必须锁定版本。升级工具时应单独提交变更、建立新基线，并尽量不要把行为修改混入格式化差异。

### 格式化与静态分析回答不同问题 {#static-analysis}

Fantomas 统一代码布局。F# 编译器检查语法、名称解析、类型、约束与已启用的警告。`TreatWarningsAsErrors` 会让已经发出的警告导致构建失败；未使用绑定等可选警告仍需另行启用。空值检查和分析器同样需要项目配置。

格式化、静态编译、属性测试、契约测试与运行时观察覆盖不同风险。发布判断应综合相关结果，不能把格式统一或无警告构建当作完整结论。

屏蔽警告时，应尽量缩小范围，并记录被标记情况为何安全。只为让检查通过而全局屏蔽，会丢失未来的诊断信息。

## 复现工具链与依赖图 {#reproducible-builds}

可复现性包含多个层次：

| 层次 | 仓库输入 | 仍在仓库之外的内容 |
|---|---|---|
| SDK 选择 | `global.json` 选择 10.0.301 | 宿主运行时与操作系统仍可能不同 |
| 直接与传递包 | `PackageReference` 加已提交 `packages.lock.json` | 包源可用性与外部凭据 |
| 本地工具 | `.config/dotnet-tools.json` 锁定 Fantomas 7.0.5 | 能运行工具的宿主运行时 |
| 编译器输出 | 相同输入配合 `Deterministic=true` | 平台原生资产、路径、编译器控制外时间戳 |
| 行为 | 测试与样例输出断言 | 未建模的外部服务和机器状态 |

固定 SDK 版本可以让 SDK 自带依赖与包锁保持一致，但不会固定操作系统、部署环境的运行时、包源或本地缓存。升级 SDK 时应主动更新相关配置；调查环境特有故障时，要记录 `dotnet --info`。

只有 `Version="3.4.0"` 的 PackageReference，仍可能解析出不同的传递依赖图。锁文件会记录实际版本与内容哈希。`dotnet restore --locked-mode` 要么使用这个图，要么在项目依赖与锁文件不一致时失败；它不会静默改写锁文件。

### 分离还原、构建与测试 {#build-stages}

重视可复现性时，应分开执行各阶段：

```console
dotnet tool restore
dotnet fantomas . --check
dotnet restore path/to/YourSolution.slnx --locked-mode
dotnet build path/to/YourSolution.slnx --configuration Release --no-restore
dotnet test path/to/YourSolution.slnx --configuration Release --no-build
```

`dotnet build` 通常会隐式还原。`--no-restore` 可以确认构建使用前一步锁定还原产生的依赖图；`--no-build` 同样防止测试悄悄执行构建。这些标志让各阶段的责任清楚可见，并不只是性能选项。

当陈旧产物可能是原因时，在锁定还原与 Release 构建前运行 `dotnet clean`。不要每次诊断都先删除缓存：先保存失败输出，再把干净构建用作受控实验。

处理困难的 MSBuild 问题时，`dotnet build -bl:<path>` 产生的二进制日志会记录求值与执行细节。它可能包含绝对路径、属性和源自环境的数据，因此应把它当诊断数据检查与处理，而不是自动公开。

## 精简的诊断检查表 {#checklist}

在宣布工具问题已修复前，询问：

1. 哪条完整命令、哪个环境复现了问题？
2. 首个相关诊断或错误值是什么？
3. 它属于哪类失败，什么观察确认了假设？
4. 修复是否处理了原因，而不是隐藏症状？
5. 窄复现现在是否通过，或产生预期诊断？
6. 格式检查是否以只读方式运行？
7. 锁定还原、Release 构建与全部测试是否从干净状态通过？
8. 已发现回归是否保存为自动测试或预期错误示例？

只有另一位读者也能复现结果，工具才真正形成工程实践；某台工作站碰巧变绿并不算。

## 练习 {#exercises}

### 练习 1：诊断文件顺序造成的级联 {#exercise-01}

错误的第 16 章构建先报告 `Domain` 命名空间不存在，随后报告多个领域类型不存在。解释应先处理哪条消息，指出项目文件需要怎样修改，并列出两个看似可行、实际上只会隐藏或复制模型的错误做法。

### 练习 2：选择 FSI、测试与调试器 {#exercise-02}

一个已编译的预订工作流返回 `Rejected(3, 2)`，而调用方预期接受。描述一个小型 FSI 实验、一项聚焦自动测试和一个断点计划。说明每项方法能发现什么，以及诊断结束后会保留哪项产物。

### 练习 3：审计可复现构建 {#exercise-03}

一位队友修改了一个包版本却忘记锁文件，使用全局 Fantomas，并报告 Debug 构建在已有构建缓存和产物时成功。给出一组有序、平台无关的命令来暴露每项不一致，并说明哪些仓库文件必须有意更新。

[阅读本章练习答案](../solutions/ch-30-diagnostics-tooling-builds)。

## 来源 {#sources}

- [Microsoft Learn：F# 编译器选项与警告](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/compiler-options)
- [Microsoft Learn：F# Interactive 与脚本](https://learn.microsoft.com/en-us/dotnet/fsharp/tools/fsharp-interactive/)
- [Microsoft Learn：托管 .NET 调试器](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/managed-debuggers)
- [Microsoft Learn：`dotnet build`、隐式还原与构建日志](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-build)
- [Microsoft Learn：NuGet 依赖锁文件与锁定模式](https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files#locking-dependencies)
- [Microsoft Learn：本地 .NET 工具清单与还原](https://learn.microsoft.com/en-us/dotnet/core/tools/local-tools-how-to-use)
- [Fantomas：安装并使用本地格式化器](https://fsprojects.github.io/fantomas/docs/end-users/GettingStarted.html)
- [Fantomas：不修改文件的格式检查](https://fsprojects.github.io/fantomas/docs/end-users/FormattingCheck.html)
