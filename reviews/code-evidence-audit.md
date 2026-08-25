# 代码、契约、格式与确定性审计

## 1. 记录身份

| 字段 | 值 |
| --- | --- |
| 范围 | `examples/`、`tests/`、`scripts/`、解决方案/锁文件、正文代码引用、贯穿项目诊断与性能证据 |
| 类型 | code evidence / contracts / deterministic execution |
| 审阅者 | Codex `/root` |
| 上下文 | author-context；按 R04 清单做全量自动门、静态审计与高风险聚焦复演 |
| 提交 | `198e30be050d8900568c64cd14d4f261ac451c09`；R04 修复提交 |
| 时间 | `2026-08-25 11:47 JST` |
| 来源截止 | not applicable；外部来源与版本由 R01 审计 |
| 语言 | both；两版共享同一代码证据 |

## 2. 环境

```text
OS and architecture: macOS 26.3 (25D125), arm64
.NET SDK and F#: 10.0.301; FSI 15.2.301.0 for F# 10.0
Node and pnpm: 26.4.0; 11.7.0
Benchmark runtime observed by Dry job: .NET 10.0.9, Arm64 RyuJIT
Browser: Fable smoke ran in Chrome; full site browser audit belongs to R05
Other material inputs: 50-entry example manifest, 22-project solution,
  paired Markdown, benchmark baseline fixture, capstone process check
```

第一次在文件沙箱内启动 `check:examples` 时，缓冲的 .NET 子进程超过正常时间且持续无输出；该进程被中止。完全相同的命令随后按权限规则在沙箱外执行并成功，最终完整门也在同一受控环境再次成功。前一次中止不计作通过或产品失败。

## 3. 范围与审计方法

### 范围内

- 全部登记代码、项目、脚本、预期编译失败、测试、契约和唯一说明性 Unity 适配器。
- Release 锁定还原、警告、F# 格式、有效源码登记和运行输出。
- JSON/HTTP/C# 公共表面、nullability、诊断名称/编号、日志脱敏和进程清理。
- 异步/取消、共享状态、持久化、容量竞争、幂等和重启的确定性证据。
- 第 31 章性能样例的语义等价、BenchmarkDotNet Dry 执行和历史基线可追溯性。
- 机器绝对路径、凭据形态、任意 sleep、真实时间/随机和临时资源的静态扫描及逐项分类。

### 范围外

- Unity Editor/Play Mode/IL2CPP Player、Avalonia 原生窗口和真实云平台没有在本轮运行；对应记录仍为 `not run`。
- 第 31 章历史 ShortRun 数字没有重测；本轮只验证等价性与 Dry 执行，避免把另一台/另一时刻的噪声覆盖原始基线。
- 浏览器可访问性、响应式和站点搜索 chunk 的实际传输代价由 R05 判断。
- F# 教学语义由 R02 专家审阅，来源与时间性由 R01 审计。

### 方法

先运行全量门，再用清单、项目文件和 Markdown 解析结果做双向清点。对时间、随机、延迟、绝对路径和疑似机密的每个命中逐项查看调用语境，不因关键词出现就判失败。对风险最高的契约另跑精确过滤与真实消费者；对竞争测试连续运行 20 轮，不用一次绿色结果代表确定性。

## 4. 命令与证据

| ID | Status | 命令、抽样或动作 | 观察结果 |
| --- | --- | --- | --- |
| E-01 | passed | `dotnet fantomas . --check` | 全仓 F# 格式检查退出 0，未改文件 |
| E-02 | passed | `dotnet restore ThinkingInFSharp.slnx --locked-mode` | 22 项目锁图一致；最终示例门再次执行成功 |
| E-03 | passed | `dotnet build ThinkingInFSharp.slnx -c Release --no-restore --nologo` | `Build succeeded`，0 warning / 0 error；仓库同时设置 `TreatWarningsAsErrors=true` 与 deterministic build |
| E-04 | passed | `env CI=true pnpm run check:examples` | 50 项清单的构建与执行检查通过；Fable 5.13.0 生产构建及 Chrome `0 → 3 → 0` smoke 通过，无运行错误/溢出 |
| E-05 | passed | 清单全量统计与代码发现 | 24 script、19 compile、3 expected-error、1 test、1 contract、1 Unity plugin、1 illustrative；50 个唯一路径、75 条登记源码引用、138 个有序输出片段、FS0030/FS0039/FS0800 三项真实诊断；无未登记代码 |
| E-06 | passed | locale `exampleIds` ↔ manifest 双向清点 | 50/50 清单 ID 被正文引用，未知 ID 0、未使用 ID 0；每个含 F#/C# 围栏片段的页面都有登记证据 ID |
| E-07 | passed | Markdown 共享代码引用复算 | 每版 239 个共享代码引用，100/100 对应页同序；目标文件、region、扩展名和 `examples/` 边界均通过内容门 |
| E-08 | passed | `dotnet test ThinkingInFSharp.slnx -c Release --no-build` | ExampleTests 70/70、ContractTests 70/70，失败 0、跳过 0 |
| E-09 | passed | `BookingJsonContract` 与 `BookingEndToEnd` 精确过滤 | 9/9 与 4/4；严格 schema、联合状态形状、缺失/未知数据、HTTP 副作用、低基数诊断和关联 Activity 通过 |
| E-10 | passed | 运行第 27 章真实 C# 客户端 | 接受/拒绝/无效路径、三个运行时 guard、仅 4 个公开类型、无 F# 专属签名、NotNull/Nullable 元数据及 XML 文档均通过 |
| E-11 | passed | `env CI=true pnpm run check:capstone` | 真实 Kestrel 与独立 C# 客户端完成放置、精确重放、确认、读取；成功/客户端错误关联日志匹配，`secrets=false`，临时目录在 `finally` 清理 |
| E-12 | passed | `BookingConsistency` 精确过滤连续 20 轮 | 每轮 7/7，共 140/140，失败/跳过 0；覆盖受控容量竞争、并发等价命令、通知重试、支付歧义、程序缺陷分类、取消释放容量和独立 FSI 进程重启 |
| E-13 | passed | `--verify-only` 与 BenchmarkDotNet `--smoke` | 260 个固定/固定种子等价案例通过；Dry 发现并执行 4 个参数组合。高优先级权限与最短迭代提示符合 Dry 的非基线定位 |
| E-14 | passed | `env CI=true pnpm test` | 39/39 内容工具测试、双语/内容、增强后的示例执行、Fable smoke、VitePress 生产构建和站点冒烟全部通过；201 书页、203 HTML、17,287 内链、1,757/1,757 搜索分段、20 个查询 |
| E-15 | passed | 凭据与绝对路径扫描 | 无私钥头、常见云/Git/API token 形态、赋值型密码/密钥，亦无 `/Users/...`、`/home/<user>/...` 或 `C:\\Users\\...` 泄漏；平台文档中的 `/Applications/Unity/...` 是有意的 macOS 安装目标 |
| E-16 | passed | `Thread.Sleep`、`Task.Delay`、`Async.Sleep`、真实时间/随机、timer、临时文件扫描 | 无任意 sleep。命中项分别是可注入的系统适配器、取消令牌驱动的无限等待、关联 ID 生成、诊断 Stopwatch、进程启动/终止安全超时和唯一临时名；没有用经过时间猜测业务结果 |

## 5. 关键审计判断

### 有效代码与页面证据能双向追踪

清单不仅检查“文件存在”。项目源码必须真正出现在项目编译项中，解决方案项目必须登记，脚本输出按顺序匹配，预期失败必须以指定诊断真正失败，未登记代码会使门禁失败。正文中 50 个 `exampleIds` 与 50 个清单 ID 双向无遗漏；共享长代码从 `examples/` 单源引用。围栏中的小型类型/边界片段不是独立程序，但其所在页面都指向同一登记证据，不把另一版复制品当成第二份实现。

唯一 `illustrative` 项是依赖 `UnityEngine` 的 C# adapter；清单和 README 都明确它只在 Unity 中编译，因此没有把普通 .NET 构建误报为 Unity 宿主证据。

### 诊断编号、传输契约与 C# 表面各有独立证据

`BookingRequestCompleted` 的源码事件号是 `1000`，名称、计数器、直方图和 ActivitySource 均为稳定常量。端到端监听器证明受限 `outcome` 测量与停止的子 Activity；真实进程门证明 32 位十六进制关联 ID 能连接响应与成功/失败日志，并拒绝敏感夹具文本。console formatter 不显示事件号，因此本轮只把 `1000` 记作源码契约，不声称从真实日志文本反推了编号。

JSON 9/9 检查三种状态的精确字段、schema 版本优先、缺失/未知/不可能数据和严格反序列化。第 27 章 C# 进程直接反射公开程序集，贯穿项目的独立 C# 进程只引用 Contracts 并穿过真实 HTTP；“能从 C# 调用”和“稳定二进制兼容所有旧版本”没有混为一谈。

### 并发门由因果信号控制

竞争路径使用 `TaskCompletionSource`、`CountdownEvent`、取消令牌和独立进程退出码；20 轮均没有靠固定毫秒数判业务完成。源码中的 `Task.Delay(Timeout.Infinite, cancellationToken)` 只让端口在调用者取消前保持未完成，测试先观察进入信号再取消。`check-capstone` 的 30 秒启动上限和 5 秒强制终止是防止 CI 永久挂起的安全边界，不决定功能断言。

### 性能记录保留历史，而长期门只验证确定行为

审计先直接复演 260 个等价案例和 4 组合 Dry smoke，确认实现可运行。历史 ShortRun fixture 保留原数值、环境和局限，本轮补入创建它的源码提交 `fd009c2ffe664d0c7dad12416ace00d6e9e9fad2` 及精确命令，没有用 Dry 数字覆盖基线。完整示例门过去只编译该项目；现在清单以无 shell 的 `runArguments` 启动 `--verify-only` 并按序检查输出，因此性能候选以后若失去语义等价，常规发布门会失败。

## 6. 审阅清单

### 代码、契约与证据

- `passed` — 所有发现的示例/测试代码均在 manifest 登记；项目编译项、解决方案、页面 ID 和共享引用一致。
- `passed` — Release 构建零警告零错误，Fantomas 只读检查通过，三项预期诊断真正失败。
- `passed` — JSON、HTTP、C#、nullability、XML 文档、日志/指标/追踪和错误分类有运行证据。
- `passed` — 性能表只保留有环境边界的历史主张；语义等价和 Dry 可执行性当前通过。
- `passed` — 临时资源使用唯一目录并由 `IDisposable`/`finally` 精确清理；无仓库内机器路径或凭据。

### F# 与技术正确性

- `passed` — 本轮执行的异步、取消、异常、资源和共享状态行为与代码主张相符。
- `not applicable` — 全书语言/惯用性结论由 R02 的无上下文专家记录。

### 双语独立性

- `passed` — 两版引用同一 manifest ID 和共享源码序列；完整 parity/content 门通过。
- `not applicable` — 自然度与单语学习路线由 R03 记录。

### 来源、站点与平台

- `not applicable` — 外部来源/版本由 R01 记录。
- `passed` — 静态生产构建和全站产物冒烟通过。
- `not run` — VitePress 报告两个本地搜索索引 chunk 超过 500 kB；是否影响实际导航加载、缓存或可访问性转交 R05 的真实浏览器/网络审计。
- `not run` — Unity Editor、IL2CPP 与 Avalonia 原生图形宿主不属于本轮环境。

## 7. 发现

| ID | 严重度 | 位置 | 问题 | 证据 | 修复 | 状态与复测 |
| --- | --- | --- | --- | --- | --- | --- |
| R04-F01 | medium | `examples/manifest.json`、`scripts/check-examples.mjs`、第 31 章项目 | 完整示例门只编译基准项目，没有运行其 260 个等价案例；候选实现可在语义回归后继续“构建通过” | 直接 `--verify-only` 当前为 260，但旧清单没有项目执行动作 | 为 executable `compile` 增加非空参数数组与有序输出契约；无 shell 启动；增加正反单测；Ch31 登记 `--verify-only` | fixed in `198e30b`; 39/39 内容测试、`check:examples` 与完整 `pnpm test` passed |
| R04-F02 | low | `tests/ContentFixtures/ch31-baseline.json` | 历史 ShortRun 记录有日期/环境，却没有把数值绑定到源码修订和复现命令 | Git 历史显示 fixture 与实现由 `fd009c2` 同一提交引入 | 补 `sourceCommit` 与精确 Release 命令，不改原测量数值 | fixed in `198e30b`; JSON 解析、Dry 复演和完整门 passed |

开放 high / medium / low finding：`0 / 0 / 0`。

## 8. 结论

| Decision | Value |
| --- | --- |
| Review result | `passed` |
| Release effect | `eligible for R05–R06; not an overall release decision` |
| Open high findings | `0` |
| Open medium findings | `0` |
| Open low findings | `0` |
| Residual risk | 历史微基准仍只代表所记录工作站/工作负载；20 轮进程内/本机契约不是分布式生产证明；Unity/Avalonia 原生宿主未运行；搜索索引大 chunk 尚待 R05 判断实际影响 |
| Follow-up | R05 用真实浏览器检查网络、搜索与长页；R06 在干净检出中冻结安装并重跑全部门。只有重新采集可比 ShortRun 时才更新历史性能数值 |

### 签署

`Codex /root, 2026-08-25 11:47 JST, 198e30be050d8900568c64cd14d4f261ac451c09`
