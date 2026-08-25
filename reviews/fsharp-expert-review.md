# 无上下文 F# 专家正确性审阅 / Fresh-context F# Correctness Review

## 1. 记录身份 / Record identity

| 字段 / Field | 值 / Value |
| --- | --- |
| Scope / 范围 | F# 语言语义、惯用性、.NET 边界、异步/取消、C# 互操作、贯穿项目和 Unity 托管插件证据 |
| Review type / 类型 | F# correctness / adversarial fresh-context review |
| Reviewer / 审阅者 | Bacon `/root/r02_fresh_fsharp_review` |
| Context / 上下文 | fresh-context；只提供仓库与正确性审阅范围，不提供规格、计划、作者论证或预期结论 |
| Commit / 提交 | 初审 `47641a3b`；主要修复 `6d273c304cdd94da3dd29642928ee10948dd20f2`；最终复核 `a43ed081d8302a5e931312fafbc610180cae12db` |
| Review time / 时间 | `2026-08-25 11:09 JST` |
| Source cutoff / 来源截止 | not applicable；来源与版本由 R01 审计 |
| Locales / 语言 | both / 中英文；代码证据为两版共享 |

## 2. 环境 / Environment

```text
OS and architecture: macOS 26.3 (25D125), arm64
.NET SDK and F#: 10.0.301; FSI 15.2.301.0 for F# 10.0
Node and pnpm: 26.4.0; 11.7.0
Browser and viewport: not run — 浏览器行为由 R05 审计
Framework/editor/target: Unity 6000.3.22f1 and Avalonia 12.1.1 are review targets;
  Unity Editor absent, Avalonia native host not rerun in this review
Other material inputs: shared examples, ExampleTests, ContractTests, capstone checks,
  paired chapter/solution pages, prior manual Unity evidence record
```

## 3. 范围与抽样 / Scope and sampling

### 范围内 / In scope

- 基础类型与函数：推断、柯里化/元组参数、结构相等/比较、联合、`option`/`Result`、递归、泛型约束、度量单位与表示隐藏。
- 程序结构与效果：模块/签名、计算表达式、null/nullable、异常、资源、`Async`/`Task`、取消、清理、并发和确定性测试。
- .NET 与跨语言边界：类/结构体、装箱、委托/事件、C# 公共表面、nullability 元数据、DTO、JSON 和错误分类。
- 贯穿项目的高风险路径：RequestId → HTTP `Location`、依赖故障 → 503/500、幂等恢复与取消、Unity 值类型/IL 分配证据。
- 初审 finding 修复后的同一审阅者闭环复核。

### 范围外 / Out of scope

- 外部来源、版本与断链属于 R01；双语自然度与单语阅读属于 R03。
- 真实浏览器、键盘与 WCAG 属于 R05。
- Unity Editor/Play Mode/IL2CPP Player 和 Avalonia 原生窗口没有在本审阅中运行，不能由普通 .NET 测试替代。
- 本审阅不是对 200 个 locale 页面逐字重新证明；抽样集中在错误代价最高、可由类型/运行结果判定的边界。

### 抽样规则 / Sampling rule

审阅者从没有作者上下文的状态出发，先沿语言学习顺序抽查核心主张，再沿 null、相等/比较、CE、异步/取消、互操作与项目边界追到共享源码和测试。贯穿项目则从外部可观察结果反向追踪：可回读 URL、错误状态、取消所有权、持久阶段和异常因果链。Unity 抽样同时检查公开 CLR 形状、值类型声明与 `Step` 的托管 IL；这样不会把结构体标签或普通构建成功误当成无装箱/Player 证据。

## 4. 命令与证据 / Commands and evidence

| ID | Status | Command, sample, or action | Observed result and artifact |
| --- | --- | --- | --- |
| E-01 | passed | 无上下文专家对 `47641a3b` 做首次语义/代码审阅 | 得到 1 high、2 medium、1 low；没有以作者解释覆盖发现 |
| E-02 | passed | `dotnet test tests/ExampleTests/ExampleTests.fsproj -c Release --filter FullyQualifiedName~BookingDomain` | 9/9；空白、非法字符、`.`/`..`、长度上限和合法标识均受领域构造控制 |
| E-03 | passed | `dotnet test tests/ContractTests/ContractTests.fsproj -c Release --filter FullyQualifiedName~BookingApi` | 24/24；非法路径字符拒绝，64 字符标识创建后的 `Location` 可 GET 回读，程序异常仍安全映射为 500 |
| E-04 | passed | Release 聚焦过滤 `BookingAdapter` 与 `BookingConsistency` | 6/6 与 7/7；类型化依赖故障保留 `InnerException`，调用方取消与依赖自身取消保持不同语义 |
| E-05 | passed | `dotnet test tests/ExampleTests/ExampleTests.fsproj -c Release --filter FullyQualifiedName~SmokeTests` | 3/3；`MotionState` 为值类型，IL 解码器拒绝 `Gameplay.Step` 中的 `OpCodes.Box`，公开签名仍无 F# 专属类型 |
| E-06 | passed | 专家重跑完整 ExampleTests 与 ContractTests Release 套件 | 两个工程均 70/70；发现修复未破坏其他共享示例或契约 |
| E-07 | passed | `env CI=true pnpm check:capstone` | 真实进程放置、精确重放、确认、读取、安全诊断及有界清理通过 |
| E-08 | passed | `dotnet fantomas . --check` | 全仓 F# 格式检查通过 |
| E-09 | passed | `env CI=true pnpm check:parity && env CI=true pnpm check:content` | 双语 parity 与完整内容契约通过 |
| E-10 | passed | `env CI=true pnpm test:content` | 38/38；包含成对链接目标和共享代码引用漂移回归 |
| E-11 | passed | 同一专家对 `6d273c3` 复核，再对 `a43ed08` 的固定总数清理作最终复核 | 前三项立即 CLOSED；最后一项清理后 CLOSED；最终结论 `PASS`，未修改文件 |

## 5. 关键正确性判断 / Correctness decisions

### RequestId 同时是领域值和 URI 路径段

`RequestId.create` 现在在 trim 后只接受 1–64 个 ASCII URI unreserved 字符，并拒绝恰好为 `.` 或 `..` 的点段。API 将空白、过长与格式错误投影为稳定字段代码；契约测试既覆盖 `/`、`%`、`?`、Unicode 和点段拒绝，也覆盖最大长度标识的 `201 Location` 回读。这个收窄是当前首次发布契约；若未来存在旧快照，迁移仍需单独设计。

### 可预期依赖不可用与程序缺陷不是同一错误

支付/通知适配器只把已知可用性故障包装为 `DependencyUnavailableException`，并保留原异常为 `InnerException`。端点与幂等服务只把这个类型化信号映射为 `dependency_unavailable`；任意程序异常继续到安全外层 500，而不会伪装成可重试的 503。调用方令牌触发的 `OperationCanceledException` 继续传播；非调用方取消在幂等边界按依赖不可用处理。

### Unity 分配主张必须由具体 IL 证据限定

删除对结构体使用 `ReferenceEquals` 的检查，避免测试本身通过装箱制造误导。新的 IL 解码器遍历 `Gameplay.Step` 的托管方法体并拒绝 `OpCodes.Box`。这只证明该方法的显式托管 IL 路径没有 `box`，不证明整个 Unity Player 每帧零分配；正文继续把 Editor、IL2CPP、裁剪与 Player 结果标为未运行。

## 6. 审阅清单 / Review checklist

### F# 与技术正确性 / F# and technical correctness

- `passed` — 值、函数、推断、模式、相等/比较、泛型约束与度量单位的抽样主张与 F# 10 代码证据一致。
- `passed` — null/nullable/option、异常/Result、同步/异步释放、`Async`/`Task` 启动与取消所有权没有被混为一谈。
- `passed` — 计算表达式语法没有被描述为自带固定语义；builder、`and!` 与短路/累积选择边界明确。
- `passed` — C# 公共形状、DTO、枚举/nullability、结构体与装箱主张由真实编译、反射或 IL 检查支持。
- `passed` — 共享示例保持 F# 优先；C# 只在消费者或基础设施边界出现，没有反向定义领域模型。

### 双语独立性 / Bilingual independence

- `passed` — 修复同步进入中英文正文/答案，稳定锚点、链接目标与共享源码引用保持一致。
- `not applicable` — 全书表达自然度与三条单语阅读路线由 R03 给出独立结论。

### 来源与版本 / Sources and versions

- `not applicable` — 一手来源与版本截止由 R01 审计；本审阅只判断所示代码和证据是否支撑主张。

### 站点与读者路径 / Site and reader journey

- `passed` — 静态双语、内容、示例引用和答案契约在修复后通过。
- `not run` — 真实浏览器交互与平台原生宿主不属于 R02。

## 7. 发现 / Findings

| ID | Severity | Location | Claim or failure | Evidence | Required change / owner | Status and retest |
| --- | --- | --- | --- | --- | --- | --- |
| R02-F01 | high | `Booking.Domain/Domain.fs`、`Booking.Api/Endpoints.fs` | RequestId 接受 `/` 等路径分隔/保留字符，成功响应的 `Location` 可能无法按同一路由回读 | 初审构造与路由追踪 | 将路径段契约放进智能构造；限制长度/字符/点段；增加 HTTP 回读测试 / `/root` | fixed in `6d273c3`; 领域 9/9、API 24/24；专家最终 CLOSED |
| R02-F02 | medium | API 与一致性边界的外部调用捕获 | catch-all 把任意程序缺陷降格为可重试 503，隐藏真实 500 | 初审异常分支与契约测试 | 引入类型化依赖不可用异常，保留 cause，只捕获已声明类别并区分取消 / `/root` | fixed in `6d273c3`; adapter 6/6、consistency 7/7、API 24/24；专家最终 CLOSED |
| R02-F03 | medium | Unity `MotionState` 回归测试 | 对 struct 使用 `ReferenceEquals` 会装箱，使“无该项分配”的证据自相矛盾 | 初审测试源与 CLR 语义 | 移除身份检查，解码 `Step` IL 并拒绝 `box`；收窄文档主张 / `/root` | fixed in `6d273c3`; SmokeTests 3/3；专家最终 CLOSED |
| R02-F04 | low | Avalonia 双语正文/解答与 Unity 证据表 | 读者正文保留 68/70 固定总数，新增测试后发生漂移 | 专家首次复核发现一处旧 68，仓库搜索发现同类固定总数 | 保留聚焦 1/1，完整套件只陈述通过；精确总数留在可执行/审计输出 / `/root` | fixed in `a43ed08`; parity/content 及 38/38 内容测试；专家最终 CLOSED |

开放 finding：0。初审的 1 个 high、2 个 medium、1 个 low 均由原边界中的最小修复与聚焦回归关闭，没有以文档免责声明替代代码修复。

## 8. 结论 / Conclusion

| Decision | Value |
| --- | --- |
| Review result / 审阅结果 | `passed` |
| Release effect / 发布影响 | `eligible for R03–R06; not an overall release decision` |
| Open high findings | `0` |
| Open medium findings | `0` |
| Open low findings | `0` |
| Residual risk / 残余风险 | Unity Editor/IL2CPP Player 与 Avalonia 原生图形宿主未运行；若未来需要读取旧版持久快照，RequestId 收窄需要版本化迁移；`InnerException` 已保留，但生产适配器仍须用受控日志/追踪让内部原因可诊断 |
| Follow-up / 后续 | R04 重跑完整代码/确定性门；R05 复核真实浏览器；只有新增旧数据兼容需求时才创建 RequestId 迁移任务 |

English summary: the fresh-context expert review passes after closing one high-, two medium-, and one low-severity finding. Request IDs now form safe dereferenceable route segments, dependency outages no longer hide arbitrary defects as retryable 503 responses, and the Unity managed no-box claim has an IL regression test. No high, medium, or low finding remains open; native Unity/Avalonia host execution remains explicitly outside this evidence.

### Sign-off / 签署

`Bacon /root/r02_fresh_fsharp_review, final PASS relayed 2026-08-25; recorded by Codex /root at a43ed081d8302a5e931312fafbc610180cae12db`
