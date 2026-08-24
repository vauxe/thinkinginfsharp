---
title: "第 16 章答案"
description: "排列多文件项目，修复命名空间级绑定，并让显式可空引用契约通过包装函数继续传递。"
translationKey: solutions/ch-16-modules-namespaces-projects
kind: solution
part: 3
chapter: 16
status: complete
verifiedWith:
  fsharp: "10"
  dotnetSdk: "10.0.301"
exampleIds:
  - ch16-multifile-project
  - ch16-wrong-file-order
exerciseIds:
  - ch16-exercise-01
  - ch16-exercise-02
  - ch16-exercise-03
termIds: []
sources:
  - id: microsoft-modules
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/modules
    checked: "2026-08-24"
  - id: microsoft-namespaces
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/namespaces
    checked: "2026-08-24"
  - id: microsoft-open
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/import-declarations-the-open-keyword
    checked: "2026-08-24"
  - id: microsoft-null-values
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/values/null-values
    checked: "2026-08-24"
---

# 第 16 章答案 {#overview}

解决结构问题时，应先写出依赖图，再编辑项目。然后让声明表达名称，让编译器设置表达边界契约。

[返回第 16 章](../part-03/ch-16-modules-namespaces-projects)。

## 练习 1：写出依赖顺序 {#exercise-01}

### 有效的项目顺序 {#exercise-01-order}

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

### 诊断颠倒的顺序 {#exercise-01-diagnostic}

下面的顺序无效：

```xml
<ItemGroup>
  <Compile Include="Pricing.fs" />
  <Compile Include="Domain.fs" />
  <Compile Include="Program.fs" />
</ItemGroup>
```

编译过程先到达 `Pricing.fs`，之后才会看到 `Domain.fs`。`FS0039` 会出现在 `open` 声明、首次限定使用缺失的 `Domain` 模块，或首次使用其中某个类型的位置。确切位置取决于编译器最先遇到哪个不可用名称；原因都是同一个前向引用。

目录不参与 F# 名称解析或编译器输入顺序。把 `Domain.fs` 移进 `Core` 目录只会改变路径，直到你同步更新项目项；它不会让编译器更早看到该文件，也不会给命名空间添加 `Core`。源代码声明确定名称，`<Compile>` 项确定顺序。

## 练习 2：修复作用域并选择限定方式 {#exercise-02}

### 把值放入模块 {#exercise-02-fix}

把 `Text` 放在 `Booking` 命名空间下，即可得到所要求的公开名称：

```fsharp
namespace Booking

module Text =
    let normalize (raw: string) = raw.Trim()
```

命名空间可以包含模块，而模块可以包含 `let` 绑定函数。只有缩进 `let`，却没有 `module Text =` 声明，并不会产生这种结构。

### 限定调用与打开后的调用 {#exercise-02-open}

消费者可以在调用点保留完整的所属者：

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

`open Booking.Text` 让可访问成员可以在后续作用域中使用短名称。它不会重命名 `normalize`，不会加载或引用程序集，不会改变文件顺序，不会复制函数，也不会把私有函数变成公开函数。如果定义文件没有位于本项目中的更早位置，或者来自其他项目的定义程序集没有被引用，那么两种写法都会失败。

当短名称有歧义或只使用一次时，限定名称是更好的默认选择。当消费者反复使用某个职责集中的模块词汇时，精确的 `open` 是合理选择。

## 练习 3：传递一个可空边界 {#exercise-03}

### 声明包装函数的真实契约 {#exercise-03-contract}

下面这个紧凑模型显式标注了内外两层参数：

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

### 测试边界两侧 {#exercise-03-tests}

```fsharp
match BookingRequest.create null 2 with
| Error(InvalidBookingId MissingBookingId) -> ()
| other -> failwithf "Unexpected nullable result: %A" other

match BookingRequest.create "REQ-16" 2 with
| Ok _ -> ()
| other -> failwithf "Unexpected valid result: %A" other
```

若没有 `(rawId: string | null)`，即便被调用函数接受更宽的输入，推断也会让包装函数只接受非空 `string`。传入 `null` 的测试随后会与包装函数推断出的契约冲突。标注包装函数记录的是它的调用方实际可以提供什么。

`string | null` 建模的是可能含有 null 的 CLR 引用边界，应在该边界检查并规范化它。`option<string>` 是显式的 F# 领域值，拥有 `Some`、`None`、模式匹配和组合函数。二者不会悄悄互相替代；跨越边界时应有意转换。

## 要点 {#what-to-notice}

- 按“提供者先于消费者”的顺序编写源文件。
- 把前向引用导致的 `FS0039` 视为依赖证据，而不是警告策略问题。
- 命名空间提供路径；模块拥有值和函数。
- `open` 改变的是后续引用的写法，而不改变存在哪些代码或哪些代码可访问。
- 即使包装函数立即委托，其参数类型仍是自己的公开契约。
- 可空引用标注属于真实的可空边界；验证后的领域值保持非空。
