---
title: "第 16 章：模块、命名空间、项目与编译设置"
description: "把脚本转成按依赖排序的 F# 项目，区分命名空间与模块，并明确包括最小可空标注在内的编译器契约。"
translationKey: part-03/ch-16-modules-namespaces-projects
---

# 第 16 章：模块、命名空间、项目与编译设置 {#overview}

脚本可能掩盖一个有用事实：程序存在依赖方向。当领域定义、工作流和启动代码分处不同文件时，F# 要求你用编译顺序声明这个方向。后面的文件可以使用编译器已经见过的定义；前面的文件不能向前引用后面的定义。

这不是编辑器规定的形式，而是编译器可以检查的项目结构。示例中，`Domain.fs` 不知道工作流，`Workflow.fs` 依赖领域，`Program.fs` 再组合二者。项目文件准确记录这个顺序。

## 从脚本走向项目 {#script-to-project}

探索表达式和 API 时使用 `.fsx` 脚本。代码需要多个文件、包或项目引用、可复现编译设置、测试或可部署输出时，再转向项目。函数内部语法几乎不变，但项目会明确记录编译规则。

示例的目录布局如下：

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

- `Domain` 定义受保护的标识符、座位数、容量、请求和验证；
- `Workflow` 定义决策联合类型和纯决策函数；
- `Program` 包含组合逻辑与进程入口点。

限定名称会同时体现命名空间和模块两层。例如：

- `ThinkingInFSharp.Ch16.Domain.BookingId`；
- `ThinkingInFSharp.Ch16.Workflow.decide`。

文件名有助于导航，却不会自动进入限定名称。

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
`<Compile Include="Domain.fs" />` 向编译器提供一个源文件。下一项可以使用其中的定义，再下一项可以使用前两个文件的定义。重新排列编辑器标签或移动目录中的文件不会改变编译顺序；修改 `<Compile>` 顺序才会。

同样的顺序规则也适用于单个源文件：定义通常只能使用更早的定义。F# 为真正的递归提供了专用语法，但普通程序分层应从基础读向组合。

`Program.fs` 位于最后，因为它依赖另外两个模块，并且含有 `[<EntryPoint>]`。把启动组合留在最后一层，也能避免领域代码反过来依赖控制台逻辑。

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

在模块交接处，限定名称通常更清楚：

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
| `<TargetFramework>net10.0</TargetFramework>` | 项目针对哪套目标框架 API 和运行时编译？ |
| `<LangVersion>10.0</LangVersion>` | 编译器接受哪个 F# 语言版本？ |
| `<Nullable>enable</Nullable>` | F# 是否执行选择启用的空值分析？ |
| `<OutputType>Exe</OutputType>` | 项目是否打包为可执行程序，而不是默认的库？ |
| `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` | 编译器警告是否必须使构建失败？ |

这些设置分别控制不同决策。SDK 选择构建工具集，`TargetFramework` 选择 API 与运行时，`LangVersion` 选择可用的 F# 语言特性。可复现项目应记录每项选择。

共享策略可以放在 `Directory.Build.props` 中；MSBuild 会为其子目录中的项目导入它。较大的代码库可以在那里集中设置 `LangVersion`、空值检查、警告视为错误、可复现构建和锁定包还原。小型独立教学项目应保持自包含；只有多个真实项目共享策略时才值得集中配置。

应优先修复诊断，而不是全局抑制它。警告视为错误让这项纪律在本地构建和 CI 中可复现；这并不表示所有可能的可选警告都已经启用。

## 可空引用的基本规则 {#nullable-minimum}

启用 F# 空值检查后，`string` 表示非空引用，`string | null` 则允许 null。标注用于编译期分析，运行时仍是普通 .NET 引用。来自外部或未检查代码的值仍需验证。

领域构造函数有意接收可空文本：

```fsharp
let create (raw: string | null) =
    match raw with
    | null -> Error MissingBookingId
    | value when System.String.IsNullOrWhiteSpace value -> Error MissingBookingId
    | value -> Ok(BookingId(value.Trim()))
```

经过 `null` 分支之后，分析器知道剩余的 `value` 非空。边界先把缺失转换成领域错误，随后才创建受保护的 `BookingId`。内部函数因而可以使用受保护值，无须重复空值检查。

领域模型中的缺失使用 `option`；外部 .NET 接口确实允许 null 时，使用 `T | null`。第 19 章会把可空值类型、其他 .NET 语言的标注、`Null`/`NonNull` 模式、`option` 与运行时验证放进同一个模型。

### 包装函数必须保留可空标注 {#nullable-propagation}

`BookingRequest.create` 会把文本转交给 `BookingId.create`，所以自己的公共输入也要使用同一可空类型：

```fsharp
let create (rawId: string | null) rawSeats =
    match BookingId.create rawId with
    | Error error -> Error(InvalidBookingId error)
    | Ok bookingId ->
        // 验证座位数并组装请求
        // ...
```

若省略 `rawId` 的标注，推断会选择非空 `string` 参数。此时测试传入 `null` 会产生可空性不匹配诊断 `FS3261`。应修正包装函数的参数类型，不要关闭空值检查或散布未经检查的转换。

不要“以防万一”而把每个引用都标成可空。这会丢失有用的类型信息，并把检查推向内部。只在调用方确实可能提供 null 的位置允许它，就地验证，并让核心模型通过构造保持非空。

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

让每个项目小到只有一个变化理由，但不要只为制造更多程序集而拆分。新项目会引入真实的引用与部署边界；仅为组织代码时，新模块或新文件可能已经足够。只有独立复用、构建策略、维护归属或依赖方向需要该边界时，才拆成项目。

## 构建并运行项目 {#build-test}

在仓库根目录运行：

```console
dotnet build examples/chapters/ch16/Ch16.fsproj -c Release
dotnet run --project examples/chapters/ch16/Ch16.fsproj -c Release --no-build
```

可执行程序输出：

```text
accepted:REQ-16 remaining=1
```

仓库检查还会构建 `examples/expected-errors/ch16-file-order/Ch16WrongOrder.fsproj`，并要求它报告 `FS0039`。成功的可执行项目与预期失败夹具共同验证编译顺序规则。

## 练习 {#exercises}

### 练习 1：写出依赖顺序 {#exercise-01}

某项目含有 `Domain.fs`、`Pricing.fs` 和 `Program.fs`。`Pricing` 使用领域类型；`Program` 同时使用前两者。请按有效顺序写出三个 `<Compile>` 项。然后把 `Pricing.fs` 放在最前，预测 `FS0039` 会在哪里出现以及原因。

解释为什么只在目录之间移动文件，却不修改声明或项目项，无法修复依赖。


::: details 参考答案

#### 有效的项目顺序 {#exercise-01-order}

依赖关系是：

```text
Domain.fs  ──▶  Pricing.fs  ──▶  Program.fs
     └──────────────────────────▶
```

因此，项目项应为：

```xml
<ItemGroup>
  <Compile Include="Domain.fs" />
  <Compile Include="Pricing.fs" />
  <Compile Include="Program.fs" />
</ItemGroup>
```

`Domain.fs` 提供独立词汇。编译器已经处理它，所以 `Pricing.fs` 可以使用它。`Program.fs` 使用前两个提供者，因而位于最后。

当文件彼此独立时，可能存在多个有效的拓扑顺序。本题给出的依赖确定了三个位置。不要按字母排序项目项，除非字母顺序碰巧也满足依赖图。

#### 诊断颠倒的顺序 {#exercise-01-diagnostic}

下面的顺序无效：

```xml
<ItemGroup>
  <Compile Include="Pricing.fs" />
  <Compile Include="Domain.fs" />
  <Compile Include="Program.fs" />
</ItemGroup>
```

编译过程先到达 `Pricing.fs`，之后才会看到 `Domain.fs`。`FS0039` 会出现在 `open` 声明、首次限定使用缺失的 `Domain` 模块，或首次使用其中某个类型的位置。确切位置取决于编译器最先遇到哪个不可用名称；原因都是同一个前向引用。

目录不参与 F# 名称解析或编译顺序。把 `Domain.fs` 移进 `Core` 只会改变路径，还必须同步更新项目项。目录既不会让编译器更早看到文件，也不会自动给命名空间添加 `Core`。源码声明确定名称，`<Compile>` 项确定顺序。

:::

### 练习 2：修复作用域并选择限定方式 {#exercise-02}

修复下面的无效文件，同时保持公开限定名称为 `Booking.Text.normalize`：

```fsharp
namespace Booking

let normalize (raw: string) = raw.Trim()
```

在消费者模块中，分别展示一次使用完整名称的调用，以及一次先写 `open` 声明再调用的代码。准确解释 `open` 改变什么，又不改变什么。


::: details 参考答案

#### 把值放入模块 {#exercise-02-fix}

把 `Text` 放在 `Booking` 命名空间下，即可得到所要求的公开名称：

```fsharp
namespace Booking

module Text =
    let normalize (raw: string) = raw.Trim()
```

命名空间可以包含模块，而模块可以包含 `let` 绑定函数。只有缩进 `let`，却没有 `module Text =` 声明，并不会产生这种结构。

#### 限定调用与打开后的调用 {#exercise-02-open}

调用方可以在调用点使用完整限定名称：

```fsharp
module Booking.Consumer

let normalizeQualified raw =
    Booking.Text.normalize raw
```

它也可以先打开模块，再在后续位置使用：

```fsharp
module Booking.Consumer

open Booking.Text

let normalizeOpened raw =
    normalize raw
```

`open Booking.Text` 把可访问成员加入后续作用域的短名称查找。原始名称、定义、文件顺序、程序集引用和访问级别都保持原样。因此，两种写法都要求定义文件位于本项目中的更早位置，或项目已经引用定义所在的程序集。

当短名称有歧义或只使用一次时，限定名称是更好的默认选择。当调用方反复使用某个职责集中的模块词汇时，可以只 `open` 该模块。

:::

### 练习 3：传递一个可空参数 {#exercise-03}

假设 `BookingId.create : (string | null) -> Result<BookingId, BookingIdError>`。编写 `BookingRequest.create`，让它接收同一种可空文本，转交输入，并把错误映射为 `InvalidBookingId`。

分别测试 `null` 和非空标识符。解释参数标注为何必须出现在包装函数上，以及为什么这种输入类型不能替代领域模型中的 `option`。


::: details 参考答案

#### 声明包装函数的真实契约 {#exercise-03-contract}

下面这个紧凑模型明确标注了内外两层函数的参数：

```fsharp
open System

type BookingIdError =
    | MissingBookingId

type BookingId = private BookingId of string

module BookingId =
    let create (raw: string | null) =
        match raw with
        | null -> Error MissingBookingId
        | value when String.IsNullOrWhiteSpace value -> Error MissingBookingId
        | value -> Ok(BookingId(value.Trim()))

type BookingRequestError =
    | InvalidBookingId of BookingIdError

type BookingRequest =
    private
        { Id: BookingId
          Seats: int }

module BookingRequest =
    let create (rawId: string | null) seats =
        match BookingId.create rawId with
        | Error error -> Error(InvalidBookingId error)
        | Ok bookingId -> Ok { Id = bookingId; Seats = seats }
```

`BookingRequest.create` 承诺调用方可以提供 `null`，随后立即委托验证并保留错误上下文。章内的正式示例还会验证 `SeatCount`；这个独立不变量不会改变可空引用的推理。

#### 测试边界两侧 {#exercise-03-tests}

```fsharp
match BookingRequest.create null 2 with
| Error(InvalidBookingId MissingBookingId) -> ()
| other -> failwithf "Unexpected nullable result: %A" other

match BookingRequest.create "REQ-16" 2 with
| Ok _ -> ()
| other -> failwithf "Unexpected valid result: %A" other
```

若没有 `(rawId: string | null)`，即便被调用函数接受更宽的输入，推断也会让包装函数只接受非空 `string`。传入 `null` 的测试随后会与包装函数推断出的契约冲突。标注包装函数记录的是它的调用方实际可以提供什么。

`string | null` 建模可能含有 null 的 CLR 引用，应在边界检查并规范化。`option<string>` 是 F# 领域值，提供 `Some`、`None`、模式匹配和组合函数。二者不会自动互换；跨越边界时要主动转换。

:::


第 17 章会用签名文件限制公共 API：调用方只能看到组件有意公开的类型和操作。

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
