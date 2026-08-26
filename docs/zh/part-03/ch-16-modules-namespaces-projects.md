---
title: "第 16 章：模块、命名空间、项目与编译设置"
description: "把脚本转成按依赖排序的 F# 项目，区分命名空间与模块，并明确包括最小可空标注在内的编译器契约。"
translationKey: part-03/ch-16-modules-namespaces-projects
---

# 第 16 章：模块、命名空间、项目与编译设置 {#overview}

脚本可能掩盖一个有用事实：程序存在依赖方向。当领域定义、工作流和启动代码分处不同文件时，F# 要求你用编译顺序声明这个方向。后面的文件可以使用编译器已经见过的定义；前面的文件不能向前引用后面的定义。

这条规则不是编辑器要求的形式，而是可由编译器检查的项目结构。本章的可执行项目中，`Domain.fs` 不知道工作流，`Workflow.fs` 依赖领域，而 `Program.fs` 组合二者。项目文件准确记录了这个顺序。

## 学完后你能够做什么 {#outcomes}

学完本章，你应该能够：

- 把单文件实验转成小型多文件 F# 项目；
- 把 `<Compile>` 项理解为有语义的依赖顺序；
- 预测并修复前向引用产生的 `FS0039` 错误；
- 区分命名空间、模块、项目、解决方案和程序集；
- 在限定名称与有意的 `open` 声明之间选择；
- 解释常见 SDK 和编译器设置控制什么；
- 启用空值检查后，把可空引用输入标为 `T | null`；
- 让公开包装函数继续传递该标注，而不是关闭诊断；
- 安排文件顺序，使领域代码不依赖编排或启动代码。

## 从脚本走向项目 {#script-to-project}

在探索表达式和 API 时使用 `.fsx` 脚本。当代码需要多个文件、包或项目引用、可复现的编译器设置、测试或可部署输出时，再转向项目。函数内部的语法几乎不变，外围的编译契约则变得明确。

本章示例的物理布局如下：

```text
./
├── Ch16.fsproj
├── Domain.fs
├── Workflow.fs
└── Program.fs
```

目录对人很有用，却不会声明 F# 命名空间。源文件中的首个声明负责这一点，而 `Ch16.fsproj` 决定哪些文件参与编译以及它们的顺序。

## 查看三个源文件 {#project-tour}

每个源文件都以同一个命名空间开头：

```fsharp
namespace ThinkingInFSharp.Ch16
```

然后，每个文件把定义放入职责集中的模块：

- `Domain` 拥有受保护的标识符、座位数、容量、请求和验证；
- `Workflow` 拥有决策联合类型和纯决策函数；
- `Program` 拥有组合逻辑与进程入口点。

由此得到的限定名称同时体现两层，例如 `ThinkingInFSharp.Ch16.Domain.BookingId` 和 `ThinkingInFSharp.Ch16.Workflow.decide`。文件名有助于导航，却不会自动成为任一限定名称的一部分。

依赖方向足够小，可以直接看见：

```text
编译器输入： Domain.fs  ──▶  Workflow.fs  ──▶  Program.fs
可用的名称： ───────────────────────────────────────────▶
```

箭头表示“必须先被看见”，而不是“运行时调用”。没有 `Workflow.fs`，`Domain.fs` 仍然可用；反过来则不成立。

## 编译顺序是程序的一部分 {#file-order}

项目文件按依赖顺序列出源代码输入：

```xml:line-numbers [Ch16.fsproj]
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <Compile Include="Domain.fs" />
    <Compile Include="Workflow.fs" />
    <Compile Include="Program.fs" />
  </ItemGroup>
</Project>
```
`<Compile Include="Domain.fs" />` 提供一个编译器输入。下一项可以使用它的定义，再下一项可以使用前面两个文件的定义。重新排列编辑器标签或移动目录中的文件不会改变这个契约；修改 `<Compile>` 顺序才会。

同样的顺序规则也适用于一个源文件内部：定义通常使用更早的定义。F# 为真正的递归提供了显式构造，但普通程序分层应从基础读向组合。

`Program.fs` 位于最后，因为它依赖另外两个模块，并且含有 `[<EntryPoint>]`。把启动组合留在边缘，也能避免领域代码反过来依赖控制台问题。

### 错误顺序会产生真实的编译错误 {#wrong-order}

下面这个最小预期错误项目故意把 `Workflow.fs` 列在最前：

```xml:line-numbers [Ch16WrongOrder.fsproj]
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <Compile Include="Workflow.fs" />
    <Compile Include="Domain.fs" />
  </ItemGroup>
</Project>
```
当编译器到达 `Workflow.fs` 中这一行时：

```fsharp
open ThinkingInFSharp.Ch16.Domain
```

它还没有处理 `Domain.fs`，所以不知道 `Domain`，编译会报告 `FS0039`。这不是应该抑制的警告。应当把提供者放在消费者之前。

如果两个文件看起来必须互相依赖，先检查设计。通常，共享类型应进入更早的领域文件，而协调两边的操作应进入更晚的工作流文件。循环依赖通常说明分层可能有问题。递归模块和 `namespace rec` 确实为真正的相互递归而存在，但不应成为绕过普通分层的捷径。

## 命名空间与模块解决不同问题 {#namespace-vs-module}

命名空间把类型和模块组织在稳定的限定名称下。它可以跨文件延续，甚至可以跨程序集。它不能直接包含 F# 值或函数：

```fsharp
namespace Booking

let normalize raw = raw.Trim() // 无效：命名空间不能包含这个值
```

应改为把绑定放进模块：

```fsharp
namespace Booking

module Text =
    let normalize (raw: string) = raw.Trim()
```

模块把 F# 类型、值、函数和嵌套模块组织在同一个具名作用域中。文件可以用顶层 `module Booking.Text` 声明覆盖全部内容，也可以像本章示例一样，在命名空间下使用局部 `module Text =` 声明。在多文件项目中，让每个文件都以显式的命名空间或模块声明开始；不要依赖单文件应用中的隐式模块行为。

一种实用的划分如下：

| 构造 | 可包含 | 能跨文件吗？ | 典型职责 |
|---|---|---:|---|
| 命名空间 | 类型和模块 | 能 | 稳定的产品或库命名 |
| 模块 | 类型、值、函数、嵌套模块 | 不能作为同一个声明延续 | 内聚行为与 F# API |
| 文件 | 位于一个编译位置的源文本 | 不能 | 一个依赖步骤与评审单元 |

不要机械地为每个类型创建一个模块。应把一同变化的定义放在一起，并公开内聚的词汇。第 17 章将用签名文件明确这套公开词汇。

## `open` 缩短名称，但不会创建依赖 {#open}

`Workflow.fs` 包含：

```fsharp
open ThinkingInFSharp.Ch16.Domain
```

在该声明之后，可以把 `Domain` 中可访问的名称写成 `Capacity`、`BookingRequest` 和 `BookingId`，省去完整路径。`open` 只改变后续作用域中的名称查找。项目成员或程序集引用提供定义，编译顺序决定可见时机，访问修饰符继续控制可用名称。

在边界处，限定名称通常更清楚：

```fsharp
let requested = request |> Domain.BookingRequest.seats |> Domain.SeatCount.value
```

在始终使用某个领域模块的工作流内部，打开这个职责集中的模块通常更清楚。不要只为少写字符而打开包含常见名称的宽泛模块；后续 `open` 声明可能影响未限定名称最终指向哪一个定义。当 F# 模块或联合类型的消费者应该保留所属者名称时，也可以加上 `[<RequireQualifiedAccess>]`。

## 项目、解决方案和程序集处在不同层级 {#project-contract}

项目文件是一份 MSBuild XML 文档。对于普通的 SDK 风格 F# 构建，它定义一次编译并产出一个程序集——通常是 `.dll`；当 `OutputType` 为 `Exe` 时，还会有可执行宿主。解决方案把多个项目组合起来以执行还原、构建和测试；它不是另一个命名空间，也不会把这些项目的源文件合并成一次编译。

因此，两种依赖机制工作在不同层级：

| 机制 | 范围 | 含义 |
|---|---|---|
| `<Compile Include="..." />` | 一个 F# 项目内部 | 按编译器顺序加入源文件 |
| `<ProjectReference Include="..." />` | 项目之间 | 构建并引用另一个项目的输出 |

测试项目通过 `ProjectReference` 使用本章项目。它自己的测试文件仍有自己的 `<Compile>` 顺序。相同名称的命名空间可以出现在两个程序集中，但让外部定义变得可用的是引用，而不是命名空间的拼写。

## 编译器设置会改变构建结果 {#settings}

按照每项设置回答的问题来理解它：

| 设置 | 回答的问题 |
|---|---|
| `global.json` 的 `sdk.version` 与 `rollForward` | 哪个已安装的 .NET SDK 可以运行 CLI/构建工具？ |
| `<TargetFramework>net10.0</TargetFramework>` | 项目针对哪套目标框架 API 和运行时契约编译？ |
| `<LangVersion>10.0</LangVersion>` | 编译器接受哪个 F# 语言版本？ |
| `<Nullable>enable</Nullable>` | F# 是否执行选择启用的空值分析？ |
| `<OutputType>Exe</OutputType>` | 项目是否打包为可执行程序，而不是默认的库？ |
| `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` | 编译器警告是否必须使构建失败？ |

这些控制项分别负责不同决策：SDK 选择构建工具集，`TargetFramework` 选择 API 与运行时契约，`LangVersion` 选择编译器接受的 F# 语言特性。可复现项目应显式声明每项策略。

共享策略可以放在 `Directory.Build.props` 中；MSBuild 会为其子目录中的项目导入它。较大的代码库可以在那里集中设置 `LangVersion`、空值检查、警告视为错误、可复现构建和锁定包还原。小型独立教学项目应保持自包含；只有多个真实项目共享策略时才值得集中配置。

应优先修复诊断，而不是全局抑制它。警告视为错误让这项纪律在本地构建和 CI 中可复现；这并不表示所有可能的可选警告都已经启用。

## 可空引用的基本规则 {#nullable-minimum}

启用 F# 空值检查后，`string` 表达非空引用契约，`string | null` 则显式允许 null。该标注指导编译期分析，运行时仍使用普通 .NET 引用表示。来自外部或未检查代码的值仍需在边界验证。

领域边界有意接收可空文本：

```fsharp
let create (raw: string | null) =
    match raw with
    | null -> Error MissingBookingId
    | value when System.String.IsNullOrWhiteSpace value -> Error MissingBookingId
    | value -> Ok(BookingId(value.Trim()))
```

经过 `null` 分支之后，分析器知道剩余的 `value` 非空。边界先把缺失转换成领域错误，随后才创建受保护的 `BookingId`。内部函数因而可以使用受保护值，无须重复空值检查。

当缺失属于 F# 领域模型时，使用 `option`。当实际的引用契约允许 null 时，尤其在 .NET 互操作边界上，使用 `T | null`。第 19 章会把可空值类型、来自其他 .NET 语言的标注、`Null`/`NonNull` 模式、`option` 与运行时边界验证放在一个完整模型中讨论。

### 包装函数必须继续传递可空契约 {#nullable-propagation}

`BookingRequest.create` 会把文本转交给 `BookingId.create`，所以它自己的公开输入也要声明同一契约：

```fsharp
let create (rawId: string | null) rawSeats =
    match BookingId.create rawId with
    | Error error -> Error(InvalidBookingId error)
    | Ok bookingId ->
        // 验证座位数并组装请求
        // ...
```

若省略 `rawId` 的标注，推断会选择非空 `string` 参数。此时在聚焦测试中传入 `null` 会产生可空性不匹配诊断 `FS3261`。修复方法是描述包装函数的真实边界，而不是关闭空值检查，也不是散布未经检查的转换。

不要“以防万一”而把每个引用都标成可空。这会削弱契约并把检查向内部推移。只在调用方确实可能提供 null 的地方允许它，在那里完成验证，并让核心模型通过构造保持非空。

## 用依赖方向引导架构 {#dependency-shape}

编译器只会强制“先前再后来”，不会替你选择好的分层。应利用顺序，让架构意图可以被评审：

```text
稳定的领域类型与不变量
           ↓
纯策略与工作流
           ↓
I/O 适配器与应用组合
```

更早的文件通常应该更稳定，并且更少了解基础设施。后面的文件可以依赖它们并负责组合。如果 `Domain.fs` 需要 `Program.fs`，把 `Program.fs` 移到前面也许能消除一个错误，却颠倒了预期架构。此时应移动放错位置的抽象。

让每个项目小到只有一个变化理由，但不要只为制造更多程序集而拆分。新项目会引入真实的引用与部署边界；组织代码时，新模块或新文件可能已经足够。只有独立复用、构建策略、所有权或依赖方向需要该边界时，才使用项目。

## 构建、运行并测试项目 {#build-test}

在示例所在目录运行：

```console
dotnet build Ch16.fsproj -c Release --locked-mode
dotnet run --project Ch16.fsproj -c Release --no-build
dotnet test ExampleTests.fsproj -c Release --no-restore --filter FullyQualifiedName~Ch16ProjectTests
```

可执行程序输出：

```text
accepted:REQ-16 remaining=1
```

聚焦测试覆盖可空和空白标识符、组件验证错误、跨文件工作流与最终程序组合。另一个故意颠倒构建顺序的项目必须以 `FS0039` 失败，用来确认编译顺序确实具有上述作用。

## 练习 {#exercises}

### 练习 1：写出依赖顺序 {#exercise-01}

某项目含有 `Domain.fs`、`Pricing.fs` 和 `Program.fs`。`Pricing` 使用领域类型；`Program` 同时使用前两者。请按有效顺序写出三个 `<Compile>` 项。然后把 `Pricing.fs` 放在最前，预测 `FS0039` 会在哪里出现以及原因。

解释为什么只在目录之间移动文件，却不修改声明或项目项，无法修复依赖。

### 练习 2：修复作用域并选择限定方式 {#exercise-02}

修复下面的无效文件，同时保持公开限定名称为 `Booking.Text.normalize`：

```fsharp
namespace Booking

let normalize (raw: string) = raw.Trim()
```

在消费者模块中，分别展示一次使用完整名称的调用，以及一次先写 `open` 声明再调用的代码。准确解释 `open` 改变什么，又不改变什么。

### 练习 3：传递一个可空边界 {#exercise-03}

假设 `BookingId.create : (string | null) -> Result<BookingId, BookingIdError>`。编写 `BookingRequest.create`，让它接收同一个可空文本契约，转交输入，并把错误映射为 `InvalidBookingId`。

分别测试 `null` 和非空标识符。解释参数标注为何属于包装函数，以及为什么这种边界类型不能替代领域模型中的 `option`。

[阅读本章答案](../solutions/ch-16-modules-namespaces-projects)。

## 模型回顾 {#model-review}

- F# 按依赖顺序编译普通源文件：提供者先于消费者。
- `<Compile>` 顺序是程序语义；目录顺序和编辑器顺序不是。
- 命名空间组织类型和模块；模块容纳 F# 值和函数。
- `open` 允许后续引用使用短名称，但既不加载代码，也不改变可访问性。
- 项目定义一次编译；解决方案组合项目；普通构建产出程序集。
- SDK、目标框架、语言版本、空值分析、输出种类与警告策略分别回答不同问题。
- 启用空值检查后，用 `T | null` 显式允许可空引用，并让包装函数继续传递契约。
- 应用文件顺序揭示单向架构，而不是只把文件重排到构建通过为止。

第 17 章会用签名文件强化这些边界：消费者只能看到组件有意公开的类型和操作。

## 资料来源 {#sources}

- [Microsoft Learn：F# 模块](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/modules)
- [Microsoft Learn：F# 命名空间](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/namespaces)
- [Microsoft Learn：`open` 声明](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/import-declarations-the-open-keyword)
- [Microsoft Learn：按依赖排序的 F# 项目示例](https://learn.microsoft.com/en-us/odata/webapi-8/tutorials/basic-crud-in-fsharp)
- [Microsoft Learn：F# 编译器选项](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/compiler-options)
- [Microsoft Learn：F# 空值与空值检查](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/values/null-values)
- [Microsoft Learn：`global.json` 概览](https://learn.microsoft.com/en-us/dotnet/core/tools/global-json)
- [Microsoft Learn：常见 MSBuild 项目项](https://learn.microsoft.com/en-us/visualstudio/msbuild/common-msbuild-project-items)
- [Microsoft Learn：F# 组件设计指南](https://learn.microsoft.com/en-us/dotnet/fsharp/style-guide/component-design-guidelines)
