# 贡献指南 / Contributing

本项目同时是一本书、一组可执行证据和一个纯静态站点。改动的最小完整单位不是“一段中文”或“一个能编译的文件”，而是一项读者能在两种语言中理解、在锁定环境中重现、并能说清证据边界的改动。

This project is simultaneously a book, a body of executable evidence, and a serverless static site. The smallest complete change is not “one Chinese paragraph” or “one file that compiles,” but a change that readers can understand in either language, reproduce under locked inputs, and evaluate within an explicit evidence boundary.

## 中文贡献流程

### 1. 先判断改动属于哪一层

- **只改文字：** 同时更新 `docs/zh` 和 `docs/en` 的对应页，然后运行双语与内容门禁。
- **改示例或行为：** 先修改 `examples`/`tests` 中的权威实现和失败证据，再更新两种语言的解释。
- **改术语：** 修改 `docs/terminology.json`，确认首次引入位置，然后运行 `pnpm generate:glossary`。
- **改标题、章序或页面：** 保持稳定锚点和同页语言映射，然后运行 `pnpm generate:navigation`。
- **改练习：** 主文和答案使用相同 `exerciseIds`和锚点，然后运行 `pnpm generate:solutions-guide`。
- **改版本或工具链：** 按 [版本矩阵](docs/version-matrix.md) 执行边界审计，不要只改一个版本号。

### 2. 保持双语独立完整

`docs/zh` 与 `docs/en` 中的对应页必须具有相同的相对路径和 `translationKey`。一名只会中文或只会英文的读者应当能独立获得：

- 相同的学习结果、前置概念与适用边界；
- 相同的有效代码、预期失败、输出、练习与答案；
- 相同的警告、局限、版本和来源结论；
- 在自己语言中自然的表达，而不是逐词镜像。

可以因语序和惯用法使行数不同，但不能把概念深度、否定条件或安全边界只放在一种语言中。不要写“请查看英文版”或 “see the Chinese edition” 来代替内容。

### 3. 遵守页面契约

中英页的 frontmatter 必须按页面类型包含 `title`、`description`、`translationKey`、`kind`、`status`、`exampleIds`、`exerciseIds`、`termIds` 和 `sources`。章节/答案还需 `part`、`chapter`、`verifiedWith`；附录需稳定字母。两种语言的以下结构必须一致：

- `translationKey`、`kind`、章/部分/附录身份和 `status`；
- `exampleIds`、`exerciseIds`、`termIds` 及其顺序；
- H1–H3 层级、显式 `{#stable-id}` 锚点和围栏代码引用；
- 主文到答案、答案回主文的链接。

具体验证以 `scripts/lib/content-contract.mjs`、`check-parity.mjs` 和 `check-content.mjs` 为准；不要绕过检查器去“修复”内容。

### 4. 从 F# 本身组织教学

- 先让读者看表达式、类型签名、数据形状和组合，再说工具或框架。
- 可以用 C# 建立互操直觉，但不把 F# 写成 C# 语法的翻译。
- 优先小型、可运行、有类型与输出证据的例子；把大框架放在明确边界后。
- 区分“语言保证”、“此实现的策略”、“测试证明”与“尚未执行的环境结论”。
- 删去不改变决策的重复；不用缩写省略必要的前提、失败路径或限制。

### 5. 用可执行证据支撑代码

正文中的共享代码必须使用 `<<< @/../examples/...` 引用，而不是在两种语言里各复制一份。所有 `examples`/`tests` 代码都必须在 `examples/manifest.json` 登记：

- 可执行脚本列出有顺序的 `expectedOutput`；
- 编译或测试项目列出真正参与编译的源文件；
- 需要行为证据的可执行 `compile` 项目列出非空 `runArguments` 和有顺序的 `expectedOutput`；参数直接传给进程，不经过 shell；
- 预期失败必须声明诊断号，且命令必须真正失败；
- “仅用于说明”的代码必须显式登记为 `illustrative`，不冒充编译证据。

不提交 `bin`、`obj`、`dist`、Fable `generated` 或私密/环境文件。本地服务默认绑定 `127.0.0.1`；不要为教学演示关闭认证后把端点暴露到局域网或公网。

### 6. 引用和时间性规则

- 语言语义、.NET/API 行为、框架支持、版本与平台兼容性优先使用官方文档、规范、源仓库或包元数据。
- frontmatter `sources` 使用稳定 kebab-case `id`、HTTPS URL 和实际复核日期 `checked: YYYY-MM-DD`。
- 正文末尾的“来源/Sources”与 frontmatter 的关键事实入口保持一致。
- 使用确切版本与复核日期，不写无时间边界的“最新”、“当前”或“现在都支持”。
- 二手教程可用于找线索，但不应是非平凡事实的唯一发布证据。

### 7. 本地验证和提交

先运行与改动相关的最小命令，提交前再扩大：

```console
pnpm test:content
pnpm check:parity
pnpm check:content
pnpm check:examples
pnpm build
pnpm check:site
```

贯穿项目或发布改动再运行 `pnpm check:capstone`。F# 格式改动先执行 `dotnet tool restore`，然后用 `dotnet fantomas . --check` 检查。提交说明要写“改变了什么可观察契约”，不要只写“优化文档”。

手工审阅使用 [reviews/review-template.md](reviews/review-template.md)。`passed` 必须附带命令、抽样或环境证据；没有执行就写 `not run`，不用空白或“应该可以”代替。

---

## English contribution workflow

### 1. Classify the change before editing

- **Prose only:** update the paired page under both `docs/zh` and `docs/en`, then run bilingual and content gates.
- **Example or behavior:** change the authoritative implementation and failing evidence in `examples`/`tests` first, then update both explanations.
- **Terminology:** edit `docs/terminology.json`, confirm the first-introduction route, then run `pnpm generate:glossary`.
- **Heading, chapter order, or page:** preserve stable anchors and same-page locale mapping, then run `pnpm generate:navigation`.
- **Exercise:** use the same `exerciseIds` and anchors in chapter and solution, then run `pnpm generate:solutions-guide`.
- **Version or toolchain:** follow the boundary audit in the [version matrix](docs/version-matrix.md); do not update only one version string.

### 2. Keep each edition independently complete

Paired pages under `docs/zh` and `docs/en` must have the same relative path and `translationKey`. A reader who knows only one language must receive the same outcomes, prerequisites, applicable boundaries, executable/expected-failure examples, output, exercises, answers, warnings, limitations, version conclusions, and source conclusions.

Natural sentence order and idiom may produce different line counts. Conceptual depth, negative conditions, and safety boundaries may not exist in only one edition. Never use “see the Chinese edition” or “请查看英文版” as a substitute for content.

### 3. Preserve the page contract

Frontmatter must supply the fields required by the page kind, including `title`, `description`, `translationKey`, `kind`, `status`, `exampleIds`, `exerciseIds`, `termIds`, and `sources`. Chapters and solutions also carry `part`, `chapter`, and `verifiedWith`; appendices carry a stable letter. Paired pages must agree on identity/status, ordered ID lists, H1–H3 structure, explicit `{#stable-id}` anchors, fenced-code references, and chapter/solution return links.

The executable contract lives in `scripts/lib/content-contract.mjs`, `check-parity.mjs`, and `check-content.mjs`. Fix the content rather than weakening a gate to admit it.

### 4. Teach from F# itself

- Lead with expressions, type signatures, data shape, and composition before tools or frameworks.
- Use C# to establish an interop intuition where useful, but do not present F# as translated C# syntax.
- Prefer small runnable examples with type and output evidence; place frameworks after a clear boundary.
- Distinguish a language guarantee, this implementation's policy, what a test proves, and an environment claim that has not been run.
- Remove repetition that does not change a decision, without omitting premises, failure paths, or limitations.

### 5. Back code with executable evidence

Shared prose code uses `<<< @/../examples/...`; do not copy separate implementations into the two editions. Register all code under `examples` and `tests` in `examples/manifest.json`. Scripts declare ordered output; projects name sources actually compiled; executable `compile` entries that need behavioral evidence declare non-empty `runArguments` and ordered `expectedOutput`, passed directly without a shell; expected errors declare diagnostics and really fail; explanatory-only code is explicitly `illustrative` rather than presented as compiled evidence.

Do not commit `bin`, `obj`, `dist`, Fable `generated`, secrets, or local environment files. Local services bind to `127.0.0.1` by default. Never expose an authentication-disabled teaching endpoint to a LAN or public interface.

### 6. Citation and time-sensitive claims

- Prefer official documentation, specifications, source repositories, or package metadata for language semantics, .NET/API behavior, framework support, versions, and platform compatibility.
- Each frontmatter source has a stable kebab-case `id`, an HTTPS URL, and the actual `checked: YYYY-MM-DD` review date.
- Keep the prose Sources section aligned with the primary entry points used for non-trivial claims.
- Write exact versions and review dates. Avoid unbounded words such as “latest,” “current,” or “now supported.”
- Secondary tutorials can help discovery, but cannot be the sole release evidence for a non-trivial fact.

### 7. Validate and submit

Start with the smallest relevant command, then expand before submission:

```console
pnpm test:content
pnpm check:parity
pnpm check:content
pnpm check:examples
pnpm build
pnpm check:site
```

Run `pnpm check:capstone` for capstone or release changes. For F# formatting work, run `dotnet tool restore` and `dotnet fantomas . --check`. A commit message should name the observable contract changed, not merely “improve docs.”

Use [reviews/review-template.md](reviews/review-template.md) for manual work. A `passed` result needs command, sample, or environment evidence. If it was not performed, record `not run`; never replace that with a blank or “should work.”
