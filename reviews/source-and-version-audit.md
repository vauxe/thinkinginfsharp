# 官方来源、版本与链接审计 / Source, Version, and Link Audit

## 1. 记录身份 / Record identity

| 字段 / Field | 值 / Value |
| --- | --- |
| Scope / 范围 | `docs/**/*.md`、`README.md`、`CONTRIBUTING.md`、版本与包锁、既有 Unity 人工记录 |
| Review type / 类型 | source / version / link |
| Reviewer / 审阅者 | Codex `/root` |
| Context / 上下文 | author-context；按 R01 发布审计重新全量扫描，并对易变入口人工复核 |
| Commit / 提交 | `d8704449db09ad75528401418832ac19b409ca48` |
| Review time / 时间 | `2026-08-25 10:13 JST` |
| Source cutoff / 来源截止 | `2026-08-25` |
| Locales / 语言 | both / 中英文 |

## 2. 环境 / Environment

```text
OS and architecture: macOS 26.3 (25D125), arm64
.NET SDK and F#: 10.0.301; FSI 15.2.301.0 for F# 10.0
Node and pnpm: 26.4.0; 11.7.0
Browser and viewport: not run — 浏览器行为由 R05 审计
Framework/editor/target: Unity 6000.3.22f1 is a manual target; Editor absent
Other material inputs: global.json, Directory.Build.props, package.json,
  .config/dotnet-tools.json, 24 packages.lock.json files, docs/version-matrix.md
```

实测值与 `docs/version-matrix.md` 一致。Unity 版本仍只是人工目标；本审计没有把官方网页可达或普通 .NET 构建改写成 Editor、Play Mode、IL2CPP 或 Player 证据。

## 3. 范围与抽样 / Scope and sampling

### 范围内 / In scope

- 全量解析中英 200 个 locale 页面中的 `sources`、`checked` 与 `verifiedWith`。
- 全量检查 202 个站点 Markdown 文件，加上 README、贡献指南和 Unity 人工记录，共 205 个文件中的 HTTPS 链接。
- 对照 `global.json`、项目声明、工具清单、24 份 NuGet 锁文件和版本矩阵。
- 搜索可能漂移的 “latest/current/最新版/当前版本” 等表达，并逐条按语义分类。
- 用生产构建后的站点冒烟覆盖内部页面、锚点、搜索索引和语言页面集合。

### 范围外 / Out of scope

- F# 语义与惯用性由 R02 独立审阅；这里只判断其主张是否有合适的一手入口。
- 中英文自然度与逐段语义等价由 R03 审阅。
- 浏览器交互、WCAG 与响应式布局由 R05 审阅。
- 外部页面内部的片段锚点、登录后内容以及未来可用性不能由一次 HTTP 检查保证。

### 抽样规则 / Sampling rule

结构、日期、双语来源数组、内部链接和外链 URL 是全量扫描，不是抽样。人工“主张—来源”复核选择 17 组成对页面，覆盖七部分、正文与附录：第 1、7、11、18、23、26、31、37、39–45 章及附录 C/H。样本横跨 FSI、相等/比较、泛型约束、显式验证与 CE、取消/超时/释放、.NET 边界、性能、幂等性，以及 Web、数据、Fable、云、Avalonia、Unity 和包生态。易变生态版本另外与 NuGet、上游仓库或厂商文档及本地锁文件交叉核对。

该规则证明来源入口覆盖与版本边界；它不声称逐字重新证明全书每一句话。语言正确性仍需要 R02，单语阅读体验仍需要 R03。

## 4. 命令与证据 / Commands and evidence

| ID | Status | Command, sample, or action | Observed result and artifact |
| --- | --- | --- | --- |
| E-01 | passed | 用 `collectMarkdownPages('docs', { localesOnly: true })` 全量统计 frontmatter | 200 页；196 页含来源，4 页为确定性生成的术语表/答案索引；1,186 条来源出现；有来源的页面缺 `verifiedWith` 为 0 |
| E-02 | passed | 统计全部 `checked` 日期 | `2026-08-24` 为 590 条，`2026-08-25` 为 596 条；没有未来日期、无效日期或其他漂移日期 |
| E-03 | passed | `env CI=true pnpm check:parity` | `Bilingual parity check passed.`；成对页面的来源 ID、URL、日期与验证基线一致 |
| E-04 | passed | `env CI=true pnpm check:content` | glossary、solutions guide、navigation 与 content 四层检查全部通过；HTTPS、日期、内部链接和内容契约有效 |
| E-05 | passed | 一次性 Node 26 外链审计：提取 205 个 Markdown 中的 HTTPS URL、去片段、跟随重定向、20 秒超时、两次尝试 | 1,920 次出现、306 个唯一 URL；修复后 299 个直接成功；7 个 GitHub URL 在高并发下超时，随后逐页低并发/浏览器复核全部存在 |
| E-06 | passed | 人工复核 7 个超时目标 | Avalonia.Templates、Oxpecker、Saturn、Giraffe、`dotnet/fsharp#14454`、`dotnet/interactive#4163` 与 MAUI templates 目录均显示目标仓库/问题/目录内容，不是 404 |
| E-07 | passed | 官方页面抽查 F# 10、null、task expressions、`global.json`、Unity 自动测试及关键包版本 | 页面标题、版本与书中边界一致；Unity 新 URL 明示 Unity 6.0 Automated testing；NuGet 元数据与锁定版本一致 |
| E-08 | passed | `dotnet restore ThinkingInFSharp.slnx --locked-mode` | exit 0；22 个解决方案项目全部满足锁文件，21 个已是最新状态，1 个成功还原 |
| E-09 | passed | `rg` 搜索无界版本词并逐项判定 | 没有把依赖或平台写成无日期的“最新版”；命中项是业务“当前状态”、反例 `latest.csv`/容器标签、AWS 官方 URL 路径或明确的升级警告 |
| E-10 | passed | `env CI=true pnpm build` | VitePress 1.6.4 生产构建成功；既有大 chunk 警告留给 R05 性能审计，不影响本次链接结论 |
| E-11 | passed | `env CI=true pnpm check:site` | 201 个书页、203 个 HTML、17,287 个内部链接/锚点、1,757/1,757 个中英搜索分段与 20 个代表查询全部通过 |

外链审计第一次运行曾直接发现旧 Unity 测试 URL 为 HTTP 404；这说明检查能够区分真实断链与随后出现的 GitHub 瞬时超时。批量抓取的超时没有被静默当作通过，而是全部进入二次复核。

## 5. 来源与版本判断 / Source and version decisions

### 来源覆盖

196 个含技术或版本主张的 locale 页面都同时拥有一手来源和明确验证基线。没有独立来源的四页恰好是中英术语表与答案评审索引；它们由已审阅页面和 `terminology.json` 确定性生成，不新增语言或生态事实。

来源域均是内容所有者或规范入口：Microsoft Learn、FSharp.Core 文档、NuGet 包元数据、Unity、Avalonia、Aspire、Fable、FsProjects、BenchmarkDotNet、AWS、Kubernetes、RFC Editor、ONNX Runtime、JetBrains 及上游 GitHub 仓库/问题。没有用搜索摘要、聚合文章或问答站替代语言规范、平台文档或包元数据。

### 版本边界

- SDK 由 `global.json` 固定为 `10.0.301`，仅以 `latestPatch` 在同一特征带滚动；`allowPrerelease` 为 `false`。
- F# 项目由 `Directory.Build.props` 固定 `LangVersion` 10.0；所有含来源页面现在都声明同一 `verifiedWith`。
- pnpm 11.7.0、VitePress 1.6.4、Fable CLI 5.13.0 与 Fantomas 7.0.5 分别由 package/tool manifest 和锁文件固定。
- FSharp.Core 10.1.301、Avalonia 12.1.1、Aspire 13.5.2、FSharp.Data 8.2.0、BenchmarkDotNet 0.15.8、Fable.Core 5.2.0 与 Browser.Dom 2.20.0 均与项目声明、锁文件和版本矩阵一致。
- Unity 6000.3.22f1 仍明确标记为人工目标；插件自动证据只覆盖 `netstandard2.1`、依赖闭包和纯逻辑/API 测试。

### 漂移用语

发布内容没有用无界的 “latest/current/最新版” 指代应固定的 SDK、库或宿主。保留的 `latest` 只出现在以下有意位置：禁止 `latest.csv` 或容器 `latest` 的反例、提醒“Latest 是查询结果而非评审策略”、以及 AWS 自己维护的稳定文档路径。业务语境中的 current/latest 指当前领域状态，不是工具版本承诺。

## 6. 审阅清单 / Review checklist

### F# 与技术正确性 / F# and technical correctness

- `not applicable` — 独立语义正确性结论属于 R02；本记录不替代专家审阅。
- `passed` — 抽样主张都有对应的一手语言、BCL、平台或上游项目入口；运行事实与官方能力说明没有混写。
- `passed` — 成功、预期失败、未运行平台边界的证据标签没有被来源审阅升级为执行通过。

### 双语独立性 / Bilingual independence

- `passed` — 所有成对页面的来源、日期和 `verifiedWith` 由 parity 门全量比较。
- `not applicable` — 译文自然度、完整推理和单语读者体验由 R03 判断。

### 来源与版本 / Sources and versions

- `passed` — 非平凡与易变主张具有权威一手入口；人工样本覆盖基础、进阶与七个生态章节。
- `passed` — 1,186 条日期均为真实实施/本次复核所在的 2026-08-24 或 2026-08-25。
- `passed` — 锁文件、声明、`verifiedWith`、实测 SDK 与版本矩阵一致。
- `passed` — 无界版本词扫描没有发现依赖或平台漂移承诺。

### 站点与读者路径 / Site and reader journey

- `passed` — 生产构建、内部链接、锚点、locale 集合与本地搜索静态检查通过。
- `not run` — 键盘、对比度、复制和响应式浏览器行为不在 R01 范围，由 R05 复演。

## 7. 发现 / Findings

| ID | Severity | Location | Claim or failure | Evidence | Required change / owner | Status and retest |
| --- | --- | --- | --- | --- | --- | --- |
| R01-F01 | medium | `docs/{zh,en}/{part-07,solutions}/ch-44-unity.md` | Unity 未固定版本的自动测试来源返回 404，四个成对来源入口失效 | 首次外链扫描得到 HTTP 404；Unity 6.0 同页存在 | 改用固定 Unity 6.0 文档路径 / `/root` | fixed in `3a90746335c0ece46a5e9dd79180c3b031f6a422`；新 URL 直接检查成功，parity/content/build/site smoke 通过 |
| R01-F02 | low | `docs/{zh,en}/preface/index.md`、`appendices/h-advanced-index.md` | 页面已有 F# 10/.NET 10 来源，但缺少其他技术页共有的验证基线元数据 | 全量统计显示 4 个 sourced page 无 `verifiedWith` | 成对补 `fsharp: "10"` 与 `dotnetSdk: "10.0.301"` / `/root` | fixed in `d8704449db09ad75528401418832ac19b409ca48`；现在 sourced pages without `verifiedWith` = 0，parity/content 通过 |

开放 finding：0。初次发现均已在双语原页面修复，没有通过忽略规则掩盖。

## 8. 结论 / Conclusion

| Decision | Value |
| --- | --- |
| Review result / 审阅结果 | `passed` |
| Release effect / 发布影响 | `eligible for R02–R06; not an overall release decision` |
| Open high findings | `0` |
| Open medium findings | `0` |
| Open low findings | `0` |
| Residual risk / 残余风险 | 外站可能在截止日后变化；片段锚点和受登录保护内容未作全量语义验证；Unity 宿主验证仍为 `not run` |
| Follow-up / 后续 | R02 复核语言正确性，R03 复核双语独立性，R05 复核浏览器；发布前 R06 重跑静态门禁 |

English summary: the source/version/link scope passes for both editions. One real Unity 404 and one verification-metadata inconsistency were fixed in the original paired pages. All 306 unique external targets were either reached directly or individually resolved after transient GitHub timeouts. This decision does not certify F# semantics, prose equivalence, browser accessibility, or Unity Editor/Player execution; those remain separate audits.

### Sign-off / 签署

`Codex /root, 2026-08-25 10:13 JST, d8704449db09ad75528401418832ac19b409ca48`
