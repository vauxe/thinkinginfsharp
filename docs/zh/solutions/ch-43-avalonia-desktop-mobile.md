---
title: "第 43 章练习答案"
description: "选择合乎比例的 UI 边界，把已验证 Avalonia 切片变成桌面发布计划，并设计诚实的移动项目图与证据图。"
translationKey: solutions/ch-43-avalonia-desktop-mobile
kind: solution
part: 7
chapter: 43
status: complete
verifiedWith:
  fsharp: "10"
  dotnetSdk: "10.0.301"
exampleIds:
  - ecosystem-avalonia-desktop
exerciseIds:
  - ch43-exercise-01
  - ch43-exercise-02
  - ch43-exercise-03
termIds: []
sources:
  - id: avalonia-templates
    url: https://github.com/AvaloniaUI/Avalonia.Templates
    checked: "2026-08-25"
  - id: avalonia-supported-platforms
    url: https://docs.avaloniaui.net/docs/supported-platforms
    checked: "2026-08-25"
  - id: avalonia-cross-platform-architecture
    url: https://docs.avaloniaui.net/docs/fundamentals/cross-platform-architecture
    checked: "2026-08-25"
  - id: avalonia-cross-platform-solution
    url: https://docs.avaloniaui.net/docs/app-development/cross-platform-solution-setup
    checked: "2026-08-25"
  - id: avalonia-application-lifetimes
    url: https://docs.avaloniaui.net/docs/fundamentals/application-lifetimes
    checked: "2026-08-25"
  - id: avalonia-threading
    url: https://docs.avaloniaui.net/docs/app-development/threading
    checked: "2026-08-25"
  - id: avalonia-accessibility
    url: https://docs.avaloniaui.net/docs/app-development/accessibility
    checked: "2026-08-25"
  - id: avalonia-headless-testing
    url: https://docs.avaloniaui.net/docs/testing/setting-up-the-headless-platform
    checked: "2026-08-25"
  - id: avalonia-deploy-macos
    url: https://docs.avaloniaui.net/docs/deployment/macos
    checked: "2026-08-25"
  - id: avalonia-deploy-linux
    url: https://docs.avaloniaui.net/docs/deployment/linux
    checked: "2026-08-25"
  - id: avalonia-deploy-ios
    url: https://docs.avaloniaui.net/docs/deployment/ios
    checked: "2026-08-25"
  - id: dotnet-publishing
    url: https://learn.microsoft.com/dotnet/core/deploying/
    checked: "2026-08-25"
  - id: dotnet-wpf-migration
    url: https://learn.microsoft.com/dotnet/desktop/wpf/migration/
    checked: "2026-08-25"
  - id: dotnet-maui
    url: https://learn.microsoft.com/dotnet/maui/?view=net-maui-10.0
    checked: "2026-08-25"
---

# 第 43 章练习答案 {#overview}

以下是参考设计，不是放之四海皆准的结论。每个答案都给出首选候选、让 F# 保持价值的边界、仍需收集的证据，以及会推翻选择的条件。拥有不同控件、技能、设备、支持合同或分发渠道的团队，完全可以合理地得出不同答案。

## 练习 1：为三个产品选择 UI 边界 {#exercise-01}

决定性问题不是“哪个框架能共享最多代码”，而是“哪个边界能让这个产品拥有最小且有依据的平台表面，同时保持高成本决策可测试”。

### A. 仅限 Windows 的交易工作站 {#windows-workstation}

**首选候选：** 保留或创建 WPF/Windows UI 壳，把计算、验证、订单状态转换和服务契约放进 F# 库。

需求已经包含两个决定性约束：范围仅限 Windows，并且存在成熟 WPF 控件。用 Avalonia 重写这些控件，是为了消除产品根本不想消除的平台限制而承担风险。若必需 Windows 功能或供应商路线图要求，WinUI 可做单独现代化尖峰，但它并不会自动成为现有可用 WPF 资产的升级方案。

在 UI 壳与 F# 之间使用窄对象形边界：

- F# 拥有不可变市场快照、已验证标识符、定价函数、命令、结果和支持取消的服务端口；
- UI 适配器把联合与结果转成属性、命令、通知和可观察集合增量；
- C# 拥有 XAML 代码生成、控件供应商集成、窗口/调度器服务和安装器专用钩子；
- 序列化与线程契约在边界两侧都接受测试。

**不作为首选的方案：** Avalonia 增加控件替换风险，却没有当前跨平台收益；浏览器界面可能不适合现有控件、延迟、多窗口或企业集成；直接 F# WPF UI 可以调查，但并不是获得 F# 领域的必要条件。

**证据缺口：** 代表性供应商控件、高频更新行为、UI 线程预算、无障碍、多显示器/DPI、身份验证、崩溃恢复、企业安装器、签名更新，以及从现有安装版本升级。

**反转条件：** 若 WPF 控件或受支持 Windows 版本阻碍必需路线图，或出现有预算的 macOS/Linux 需求，则使用同一个 F# 核心，对比 Avalonia 纵向重写与另一条 Windows 现代化路径。

### B. 离线跨平台现场工具 {#field-tool}

**首选候选：** 使用带共享 F# 领域/呈现核心和单一桌面宿主的 Avalonia 桌面应用，然后分别建立明确的 Windows、macOS 与指定 Linux 打包轨道。

产品恰好需要 Avalonia 桌面宿主覆盖的平台，也不需要手机生命周期或商店。键盘加触摸、本地文档符合桌面 UI，但仍需响应式控件、更大触摸目标、文件选择器适配器、耐久原子存储、冲突/恢复策略和平台测试。

保持以下边界：

- 纯 F# 状态处理文档身份、验证、编辑历史、同步状态和重试决策；
- 持久化端口拥有原子保存、备份、迁移和写入中断恢复；
- 平台适配器拥有选择器、最近文档集成、协议/文件关联、安全凭据和外部链接；
- Avalonia 视图拥有布局和输入，并使用编译绑定或显式渲染器；
- 打包项目或流水线阶段拥有各 RID、元数据、签名、安装器和更新渠道。

**不作为首选的方案：** WPF 无法覆盖 macOS/Linux；MAUI 不面向桌面 Linux，并引入产品不需要的移动式工具链；只有在离线文件、设备集成、更新和企业分发证据优于桌面安装包时，浏览器/PWA 才值得采用。

**证据缺口：** 最难的文档与列表屏幕、大数据、离线重启、文件锁、字体/区域设置、键盘与触摸、屏幕阅读器、Windows/macOS/Linux 原生启动、X11/XWayland 范围、签名安装包、干净安装、升级、回滚和现场设备性能。

**反转条件：** 若原生文档集成、Linux 后端行为、控件性能或打包成本超过尖峰预算，则保留 F# 核心，并比较原生壳或浏览器界面。

### C. 消费移动应用与配套查看器 {#consumer-mobile}

**首选候选：** 暂不选择共享 UI。在同一个 F# 核心之上，分别做两份移动纵向切片：(1) Avalonia 跨平台加薄 Android/iOS 宿主；(2) C# 移动壳——MAUI 或原生平台 UI。桌面查看器另作较小决定。

相机、推送通知、深链、后台上传、权限、进程死亡、签名和商店主导这个产品。它们是平台能力与生命周期契约，不是绘图原语。只有这些路径在设备上真正可用时，高共享 AXAML 比例才有价值。

比较应测量：

| 维度 | Avalonia 跨平台候选 | C# 移动壳候选 |
| --- | --- | --- |
| 共享视图 | 可能很高，包括桌面查看器 | 移动视图采用 MAUI/原生惯例；桌面端可独立 |
| F# 核心 | 高 | 高 |
| 原生集成 | Avalonia 宿主适配器加平台 SDK | MAUI handler/插件或直接原生 API |
| F# UI 体验 | Avalonia 有官方 F# 模板；周边样例仍常见 C# | F# 库直接；官方 UI/工具链呈 C# 形态 |
| 证据成本 | Android/iOS 宿主、所有移动路径及桌面宿主 | 两个移动宿主加独立查看器边界 |

**不作为首选的方案：** WPF 无法交付移动端；不能假设纯浏览器界面满足后台上传、推送、相机和商店；“一个代码库”不能替代设备证据。

**证据缺口：** 权限拒绝、采集与上传中断、每种生命周期状态下的通知点击、深链、离线队列、重复提交、Android Activity 重建、iOS 挂起/终止、设备无障碍、能耗/内存/启动、签名、分阶段商店发布、崩溃符号和更新兼容性。

**反转条件：** 只有当共享 UI 实质降低总成本且所有关键原生路径仍可维护时才选 Avalonia。若原生集成、工具链或平台体验明显更安全，则选择 C# 壳。无论结果如何，F# 核心都保留。

## 练习 2：把 X43 变成桌面发布 {#exercise-02}

先建立证据账本。X43 当前证明：锁定 Avalonia 12.1.1 依赖图、`net10.0` Release 编译、AXAML 编译、一个纯状态测试、68 项通过的示例测试，以及仓库集成。它没有证明窗口已显示：自动化 macOS 尝试在创建窗口前因 RenderTimer 错误 `-6661` 停止。Windows、Linux、发布输出、安装包、签名、安装、更新和无障碍都未执行。

### 在不丢掉小核心的前提下重构 {#desktop-structure}

一套合乎比例的目标结构是：

```text
DesktopApp.Domain        不可变规则与已验证值
DesktopApp.Presentation  Model、Msg、update、Effect 描述
DesktopApp.Core          共享 Avalonia 视图与 UI 适配器
DesktopApp.Desktop       AppBuilder、生命周期、平台组合
DesktopApp.Tests         纯测试与适配器测试
DesktopApp.UiTests       无头控件/布局/输入测试
packaging/               每个受支持 OS/包格式一条自有轨道
```

不必立即变成七个项目。物理拆分之前，边界就已经重要。先把 `Model`、`Message` 和 `Counter.update` 从窗口文件移出；只有依赖方向或平台差异证明值得时，才引入更多模块或项目。

### 加入受监督的副作用循环 {#desktop-effects}

用应用 store 取代局部可变计数器，由它拥有当前模型、串行消息处理、副作用执行和视图订阅。每个副作用都带取消令牌，并把成功、失败或取消报告为消息。每项请求带身份，只有匹配活动模型状态的完成结果才会被接受。

为文档、设置、安全凭据、对话框、外部链接、更新检查和崩溃报告使用端口，把实现细节留在桌面组合根。只持久化耐久应用数据；重新创建视图对象和派生呈现值。

对于本地文档：

1. 验证并序列化到目标文件系统中的新临时文件；
2. 按耐久契约需要进行 flush；
3. 在平台/文件系统支持时原子替换旧文件；
4. 对不可替代数据保留可恢复备份或日志；
5. 记录 schema 版本并测试每条受支持迁移；
6. 分别暴露权限、冲突、磁盘满、取消和损坏数据结果。

### 补齐视图与无障碍缺口 {#desktop-view-quality}

若应用从 X43 渲染器转向 MVVM，应采用带显式 `x:DataType` 的编译绑定。加入稳定自动化 ID 与标签、键盘导航与快捷键、焦点恢复、错误/实时播报、对比度、大字号、高 DPI、适用时的减少动态效果，以及屏幕阅读器检查。

外置字符串并测试英文、中文、长翻译、缺失字形、数字/日期格式和窄布局。不能从指针点击推断触摸支持；应在代表性硬件上测试触摸目标、滚动、选择、拖拽和软件键盘。

### 逐层完成测试与发布矩阵 {#desktop-release-matrix}

| 层次 | Windows | macOS | 指定 Linux 目标 |
| --- | --- | --- | --- |
| 锁定构建 | `net10.0` 加所选 RID | `net10.0` 加受支持的 `osx-arm64`/`osx-x64` | 按需 `linux-x64`/`linux-arm64` |
| 无头 | 绑定、布局、输入、自动化树 | 同一共享测试套件 | 同一共享测试套件 |
| 原生冒烟 | Win32、DPI、键盘、对话框、关闭 | 已解锁原生后端、菜单、快捷键、对话框 | X11/XWayland 或显式选择后端、桌面环境 |
| 安装包 | 所选签名安装器 | `.app` 包、身份、签名、公证、所选归档 | 明确命名的 `.deb`/RPM/其他格式及原生依赖 |
| 生命周期 | 安装、首次运行、更新、回滚、卸载 | 隔离/Gatekeeper、安装、更新、回滚、卸载 | 干净发行版镜像、安装、更新、回滚、移除 |
| 无障碍 | Windows 屏幕阅读器与键盘 | VoiceOver 与键盘 | AT-SPI 屏幕阅读器与键盘 |

从锁定输入发布每个 RID。显式决定框架依赖还是自包含输出。若自包含，要为 .NET 安全补丁建立重建节奏。只有实测启动/体积目标足以证明时，才测试单文件、裁剪、ReadyToRun 或 AOT；兼容性警告应使构建失败，并实际运行打包制品。

### 打包、更新、观察与恢复 {#desktop-operations}

给应用稳定 ID、语义显示版本与单调递增构建版本、确定的数据/日志/缓存位置、签名更新元数据和渠道策略。在平台要求处签名和公证。生成并保留校验和、依赖清单、符号以及源码/提交来源。

为启动阶段、已处理/未处理故障、更新状态、迁移版本、性能和功能结果加入检测，但不收集文档内容或机密。明确崩溃报告的同意与隐私行为。

测试干净安装；从每个受支持前序版本升级；下载、安装和迁移中断；不兼容降级；回滚或前向修复；以及适用时同时覆盖“保留用户数据”和“移除数据”策略的卸载。只有当旧数据格式仍能打开用户状态时，最后已知良好包才有意义。

### 保留原生证据上限 {#desktop-evidence-limit}

在已解锁交互式会话中重跑 macOS 冒烟，记录操作系统、CPU、显示器、区域设置、显示比例、提交和结果。若通过，证据应写成“此构建在此 macOS 目标显示并完成交互”，而不是“所有 macOS 都能用”。若 `-6661` 在有效显示会话中重复出现，则先缩减到官方模板，再用最小复现调查配置、依赖和框架问题。

不能用 macOS 结果填满 Windows 或 Linux 行。只发布团队准备支持、诊断、修补和退役的矩阵行。

## 练习 3：把架构扩展到移动端 {#exercise-03}

使用官方多项目形态，并让决策点保持可见：

```text
Booking.Client.Domain
Booking.Client.Presentation
Booking.Client.Core          共享 Avalonia 视图与适配器
Booking.Client.Desktop       经典桌面生命周期
Booking.Client.Android       .NET Android + Activity/视图工厂
Booking.Client.iOS           .NET iOS + app delegate/scenes
Booking.Client.Tests         纯工作流、持久化、端口契约
Booking.Client.UiTests       共享无头视图行为
```

仓库应固定 .NET SDK、Avalonia 包、NuGet 锁、工作负载清单/版本集合、Android SDK/JDK 要求和 Xcode 兼容性。平台 CI 镜像是工具链的一部分，不是不可见基础设施。

### 建模草稿与提交状态 {#mobile-state}

一套紧凑共享模型可以区分：

```fsharp
type Submission =
    | Editing
    | Submitting of operationId: Guid
    | OutcomeUnknown of operationId: Guid
    | Confirmed of bookingId: string
    | Rejected of message: string

type Msg =
    | DraftChanged of DraftChange
    | SubmitRequested
    | SubmitFinished of operationId: Guid * Result<string, SubmitError>
    | AppSuspending
    | AppResumed
    | ConfirmationLinkOpened of Uri
    | ReceiptExportRequested
    | ReceiptExportFinished of Result<unit, ExportError>
```

`update` 验证草稿、分配一个稳定操作 ID、忽略旧 ID 的完成，并描述保存/提交/导出副作用。服务器必须尊重操作 ID 才能实现幂等；网络结果未知后，UI 无法凭空制造恰好一次提交。

在有意义编辑后用防抖持久化草稿，并在挂起/导航检查点保存。发送前持久化操作 ID。恢复或冷启动时，加载耐久状态，并先与服务器核对 `Submitting`/`OutcomeUnknown`，再允许产生新身份。绝不序列化控件、Activity 实例、取消令牌或打开流。

### 定义平台端口与结果 {#mobile-ports}

共享项目可以拥有以下端口：

- 带原子替换、迁移、损坏恢复和测试替身的草稿存储；
- 带取消和幂等身份的已认证预约提交与状态查询；
- 返回已完成、用户取消、权限拒绝、不可用或失败的收据导出；
- 把深链解析写成纯函数，把宿主注册留在外部；
- 能改善体验但绝不声称请求成功的连接状态提示；
- 带同意、脱敏、关联和离线缓冲策略的遥测。

Android 实现 Activity 入口、Intent、运行时权限、文档选择器、安全存储、通知渠道和后台调度。iOS 实现 scene/app delegate、URL 处理、权限说明、文档/分享 UI、Keychain 访问、通知和允许的后台模式。每个适配器把原生回调转换成共享消息。

### 务实选择宿主语言 {#mobile-host-languages}

从官方 F# Avalonia 跨平台模板开始，并从 CLI 构建两个宿主。若生成项目、IDE、工作负载、绑定、原生回调、签名和设备路径都保持常规，就保留 F# 宿主；若平台源生成、样例或 SDK 约定让极小 C# 宿主明显更安全，就使用它。两种选择都不改变 F# 领域与呈现状态的所有权。

不要把原生 SDK 类型重写成复杂的语言中立框架。保持适配器薄、测试其契约，并在用户期望处允许平台专用行为。

### 分别验证生命周期与分发 {#mobile-evidence-matrix}

| 场景 | Android 证据 | iOS 证据 |
| --- | --- | --- |
| 构建 | 锁定 `net10.0-android` 工作负载与目标 SDK | 锁定 `net10.0-ios` 工作负载与兼容 Xcode |
| 基础运行 | 受支持模拟器加代表性真机/架构 | 当前模拟器加代表性 iPhone/iPad 真机 |
| 重建 | 旋转/配置变化与 Activity 重建 | scene 转换及适用时的视图重建 |
| 进程丢失 | 后台杀进程、冷恢复、核对进行中操作 | 终止/挂起、冷恢复、核对进行中操作 |
| 链接/通知 | 冷启动、后台、前台 Intent 与通知点击 | 冷启动、后台、前台链接与通知响应 |
| 导出/权限 | 允许、拒绝、永久拒绝、取消、provider 不可用 | 允许、拒绝、取消、分享/文档目标不可用 |
| 无障碍/输入 | TalkBack、开关/键盘/触摸、大字体 | VoiceOver、开关/键盘/触摸、Dynamic Type |
| 分发 | 签名内部轨道、分阶段发布、升级、回滚计划 | provisioning 归档、TestFlight/分阶段发布、升级、回滚计划 |

还要加入离线、慢网与切网；重复点击；服务器接受后超时；时钟变化；低存储；本地化；内存压力；启动与交互预算；崩溃符号上传；隐私披露与遥测查询。商店审核通过是分发证据，不是业务正确性证明。

发布一组不可变后端契约和兼容客户端序列。移动客户端更新缓慢，因此服务器必须在声明窗口内支持旧应用版本。功能开关和最低版本门槛需要离线与失败策略，且不能破坏草稿。

### 陈述桌面推论的上限 {#mobile-inference-limit}

X43 桌面构建只证明共享编译器能构建当前桌面项目，以及纯计数器转换通过。把移动无关的 Domain/Presentation 项目抽出后，那些纯测试可以成为共享逻辑证据。

它不能证明 `net10.0-android` 或 `net10.0-ios` 还原、工作负载兼容性、宿主启动、Activity/scene、这些目标上的 AXAML、权限、原生服务、触摸、无障碍、包元数据、签名、真机、商店或生命周期恢复。其中每一项都需要自己的矩阵行。

**反转条件：** 若关键相机/后台/通知集成缺乏可维护路径、设备体验或无障碍未达产品阈值、平台回归主导交付、打包/商店工作超过预算，或团队无法诊断原生故障，则放弃共享 Avalonia UI——而不是 F# 核心。薄 C# 或原生 UI 壳是预设出口，不是重写业务规则。

## 解答要点 {#solution-takeaways}

- 在框架实验之间保留 F# 核心；不要让产品决定依赖于强迫每个宿主使用同一种语言。
- 当跨平台范围没有产品价值时，复用现有 Windows UI。
- 对明确命名的跨平台桌面范围，Avalonia 是有力首选，但要以原生和安装包证据为条件。
- 移动能力与生命周期路径应决定移动壳；共享标记比例排在其后。
- 让 X43 逐步拥有受监督副作用、持久化、无障碍、无头测试、原生冒烟、按 RID 打包、签名、更新和恢复。
- 在交互式 macOS 运行产生新证据前，把 `-6661` 尝试保留为失败原生矩阵行。
- 移动架构需要共享 Core、独立 Android/iOS 宿主、耐久检查点、过期结果保护和服务器幂等协作。
- 桌面构建不能证明任何移动工作负载、设备、签名或商店路径。

[返回第 43 章](../part-07/ch-43-avalonia-desktop-mobile)。
