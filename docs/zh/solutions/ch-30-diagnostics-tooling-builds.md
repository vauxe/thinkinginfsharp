---
title: "第 30 章练习答案"
description: "修复 F# 文件顺序导致的级联错误，为 FSI、测试和调试器分配不同问题，并审计一个被有意改动的锁定依赖图。"
translationKey: solutions/ch-30-diagnostics-tooling-builds
---

# 第 30 章练习答案 {#overview}

每份答案都先保留原始失败，再修改代码。目标不是背诵命令，而是为每个诊断问题取得能回答它的观察结果，并在仓库中留下可持续执行的检查。

[返回第 30 章](../part-05/ch-30-diagnostics-tooling-builds)。

## 练习 1：诊断文件顺序造成的级联错误 {#exercise-01}

### 修复第一个缺失依赖 {#exercise-01-order}

先处理第一个 FS0039：它指出 `ThinkingInFSharp.Ch16.Domain` 不存在。项目尚未编译 `Domain.fs`，`Workflow.fs` 就打开了这个命名空间；随后关于 `Capacity`、`BookingRequest` 和 `Accepted` 的错误，都是同一依赖缺失的后果。

无效顺序是：

```xml
<Compile Include="../../chapters/ch16/Workflow.fs" Link="Workflow.fs" />
<Compile Include="../../chapters/ch16/Domain.fs" Link="Domain.fs" />
```

有效项目必须先编译依赖：

```xml
<Compile Include="Domain.fs" />
<Compile Include="Workflow.fs" />
<Compile Include="Program.fs" />
```

先构建范围较小的有效项目，再运行完整示例检查。预期错误项目继续保留错误顺序，以确认它会产生 FS0039。

两个看似可行却并非修复的做法是：把 `Capacity` 和 `BookingRequest` 复制到 `Workflow.fs`，从而产生彼此竞争的领域模型；或添加更多 `open`，但它无法暴露尚未编译的文件。反复删除 `obj` 也是干扰：干净构建仍会复现同一错误顺序。

把全部工作流定义移入 `Domain.fs` 也许能让编译成功，但那是在改变模块边界来规避一行项目配置修复。若没有比消除诊断更充分的架构理由，就不应做这种重新设计。

## 练习 2：选择 FSI、测试与调试器 {#exercise-02}

### 为每种工具分配不同问题 {#exercise-02-tools}

按当前规则，值 `Rejected(3, 2)` 是正确的：请求三个座位无法装入容量二。在修改 `decide` 之前，应先查明调用方为何预期接受。

用 FSI 以受控值隔离纯规则：

```fsharp
let expectOk = function
    | Ok value -> value
    | Error error -> failwithf "invalid probe: %A" error

let capacity = Capacity.create 2 |> expectOk
let request = BookingRequest.create "B-30" 3 |> expectOk

Workflow.decide capacity request
// Rejected (3, 2)
```

这确认了智能构造以及纯函数对受控输入的结果。它无法说明应用实际传入了什么值，也不会在会话结束后留下回归测试。

若该策略符合意图，就添加一个聚焦的示例测试：

```fsharp
[<Fact>]
let ``three seats do not fit capacity two`` () =
    Assert.Equal(
        Rejected(3, 2),
        Workflow.decide capacity request
    )
```

测试才是持久产物。若真实需求说容量本应为四，就应在当前产生二的转换或调用方边界保留测试；不要围绕纯核心冻结错误预期。

在已编译调用方中，紧邻 `Workflow.decide` 之前设置断点。检查已验证的 `SeatCount`、`Capacity` 和调用方栈帧。若值是 3 和 2，就追踪容量来源。若此前值不同而 `decide` 收到 3 和 2，则检查边界转换。确认输入后，再单步进入函数。

调试器把一次真实执行追溯到输入；FSI 回答一个小模型问题；自动化测试保存已商定行为。让三者回答同一个问题只会增加工作，不会提高可信度。

## 练习 3：审计可复现构建 {#exercise-03}

### 让每种不一致在所属阶段失败 {#exercise-03-audit}

从示例所在目录开始，并记录所选 SDK：

```console
dotnet --info
dotnet tool restore
dotnet fantomas . --check
dotnet clean Sample.slnx --configuration Release
dotnet restore Sample.slnx --locked-mode
```

本地工具清单让 `dotnet fantomas` 使用所声明的 7.0.5 命令；同事全局安装的版本不是仓库契约。若格式不同，应有意运行已固定版本的格式化器，并审阅它只涉及源码的差异。

锁定还原应当失败，因为项目依赖已变化，相应锁定图却未更新。这个失败确认锁定检查有效。确认包变更确有意图，审阅其兼容性和来源，然后再重新生成：

```console
dotnet restore Sample.slnx --force-evaluate
git diff -- "*.fsproj" "*.csproj" "packages.lock.json"
dotnet restore Sample.slnx --locked-mode
```

shell 的通配符行为各不相同；若该审阅命令不能递归展开，请使用版本控制客户端或明确的项目路径。必须共同审阅的是项目引用与每个受影响的 `packages.lock.json`，而不是某一种 shell 写法。

锁定图一致后，在不隐式执行其他阶段的前提下验证 Release 编译和测试：

```console
dotnet build Sample.slnx --configuration Release --no-restore
dotnet test Sample.slnx --configuration Release --no-build
dotnet test Sample.slnx --configuration Release --no-build
```

应在一次有意的依赖变更中共同更新 PackageReference 与受影响的锁文件。只有格式化器升级也有意时才更新 `.config/dotnet-tools.json`；最好让其基线差异单独审阅。仅在更改样式策略时改 `.editorconfig`，仅在更改 SDK 策略时改 `global.json`。

一次使用缓存的 Debug 成功无法验证上述任何阶段。它可能重用资产、执行隐式还原、漏过 Release 专属编译，并绕过已固定的格式化器。这里清理有价值，是因为陈旧状态属于当前假设，而不是因为删除是万能修复。

## 答案回顾 {#solution-review}

- 修复最早缺失的文件依赖；后续缺失名称只是级联错误。
- 复制领域类型或添加 `open` 都不能纠正项目编译顺序。
- FSI 隔离纯问题，调试器追踪一次执行，测试则保存策略。
- 单步进入正确的决策函数前，应先检查输入。
- 还原本地工具后，全局格式化器与仓库契约无关。
- 项目依赖改变而锁定图未变时，锁定还原就应失败。
- 只有接受依赖变更后才重新生成锁文件，随后再次运行锁定模式。
- 用 `--no-restore` 和 `--no-build` 把 Release 构建及测试与还原分开。
- 包、工具、样式和 SDK 契约只能因各自对应的决策而更新。
- 当陈旧状态是明确假设时，干净构建才有价值。
