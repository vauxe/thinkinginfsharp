# 贡献指南 / Contributing

## 中文

### 保持两种语言独立完整

- 同时修改 `docs/zh` 与 `docs/en` 的对应页面。
- 保持相同的相对路径、`translationKey`、章节身份、显式标题锚点、示例 ID、练习 ID 和来源结论。
- 使用自然中文和自然英文，不逐词镜像，也不让读者跳到另一种语言才能理解。

### 让正文继续从 F# 出发

- 先讲表达式、类型、数据形状和组合，再引入框架。
- C# 只用于解释互操作边界，不决定 F# 的教学顺序。
- 区分语言保证、项目策略、自动测试证据和尚未执行的平台结论。
- 删除不改变理解或决策的重复，但保留前提、失败路径和限制。

### 共享并验证代码

- 正文用 `<<< @/../examples/...` 引用共享源文件；不要在两种语言中复制实现。
- 改行为时先改 `examples/` 和相关 `tests/`，再同步两种语言的解释。
- 预期编译失败放在 `examples/expected-errors/`；说明性伪代码必须明确标记为 `illustrative`。
- 不提交 `bin`、`obj`、`dist`、Fable `generated`、秘密或本机配置。

### 引用与版本

- 语言、.NET、框架和平台事实优先引用官方文档、规范、源仓库或包元数据。
- 时间敏感的来源记录实际复核日期；版本写成精确值，不使用无边界的“最新”。
- 更新工具链时同时更新声明、锁文件、中英文版本说明和证据边界。

### 验证

文字或站点改动至少运行：

```console
pnpm test
```

改动 F#/.NET/Fable 示例时再运行：

```console
pnpm check:examples
```

改动贯穿项目的 HTTP、持久化或 C# 契约时运行：

```console
pnpm check:capstone
```

## English

### Keep each edition complete

- Update the paired pages under `docs/zh` and `docs/en` together.
- Preserve relative paths, `translationKey`, chapter identity, explicit heading anchors, example IDs, exercise IDs, and source conclusions.
- Write natural prose in each language. Never require a reader to consult the other edition.

### Keep the explanation grounded in F#

- Lead with expressions, types, data shape, and composition before frameworks.
- Use C# only to explain interop boundaries; do not let it determine the teaching order.
- Distinguish language guarantees, repository policy, automated evidence, and platform claims that were not run.
- Remove repetition that changes no understanding or decision, while retaining premises, failure paths, and limits.

### Share and verify code

- Reference shared sources with `<<< @/../examples/...`; do not duplicate implementations across editions.
- For behavior changes, update `examples/` and relevant `tests/` before changing both explanations.
- Put expected compiler failures under `examples/expected-errors/`; mark explanatory pseudocode as `illustrative`.
- Do not commit `bin`, `obj`, `dist`, Fable `generated`, secrets, or machine-local configuration.

### Sources and versions

- Prefer official documentation, specifications, source repositories, or package metadata for language, .NET, framework, and platform claims.
- Record the actual review date for time-sensitive sources and use exact versions instead of unbounded “latest” claims.
- Toolchain updates change declarations, lock files, both editions, and the stated evidence boundary together.

### Validate

Run `pnpm test` for prose or site changes, `pnpm check:examples` for F#/.NET/Fable sample changes, and `pnpm check:capstone` for capstone HTTP, persistence, or C# contract changes.
