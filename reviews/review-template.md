# 审阅记录模板 / Review Record Template

复制本文件为 `reviews/YYYY-MM-DD-<scope>-<reviewer>.md` 后填写；不要覆盖模板。中英文可以择一填写，但结论必须覆盖两种语言的发布影响。

Copy this file to `reviews/YYYY-MM-DD-<scope>-<reviewer>.md` before filling it in; do not overwrite the template. The record may be written in either language, but its conclusion must cover the release impact on both editions.

## 1. 记录身份 / Record identity

| 字段 / Field | 值 / Value |
| --- | --- |
| Scope / 范围 | `<paths, chapters, feature, or release>` |
| Review type / 类型 | `<F# correctness / bilingual / source / browser / release>` |
| Reviewer / 审阅者 | `<name or agent identifier>` |
| Context / 上下文 | `<fresh-context / author-context; material supplied>` |
| Commit / 提交 | `<full git SHA>` |
| Review time / 时间 | `<YYYY-MM-DD HH:mm TZ>` |
| Source cutoff / 来源截止 | `<YYYY-MM-DD or not applicable>` |
| Locales / 语言 | `<zh / en / both>` |

## 2. 状态词汇 / Status vocabulary

每一项只能使用以下一种明确状态；不能留空：

- `passed`：执行过，并在本记录中给出命令、抽样或环境证据。
- `failed`：执行过，结果不满足验收条件；必须有 finding。
- `not run`：没有执行；说明原因和释放结论受到的限制。
- `not applicable`：边界内不适用；说明为什么。

Every item uses exactly one explicit status and is never blank:

- `passed`: performed, with command, sample, or environment evidence in this record.
- `failed`: performed but did not meet acceptance; a finding is required.
- `not run`: not performed; explain why and how that limits release claims.
- `not applicable`: outside the scope; explain why.

Finding severity is `high`, `medium`, or `low`. A release cannot pass with an open high- or medium-severity finding. A low-severity finding must still have an owner, accepted rationale, or fix.

## 3. Environment / 环境

```text
OS and architecture:
.NET SDK and F#:
Node and pnpm:
Browser and viewport, if used:
Framework/editor/target, if used:
Other material inputs:
```

Record observed values, not what the machine was expected to contain. For a version-sensitive review, compare them with `docs/version-matrix.md`.

记录实际观察值，不写预期值。版本敏感审阅还要与 `docs/version-matrix.md` 对照。

## 4. Scope and sampling / 范围与抽样

### In scope / 范围内

- `<specific path, contract, or reader journey>`

### Out of scope / 范围外

- `<specific exclusion and why>`

### Sampling rule / 抽样规则

说明抽样如何同时覆盖：中文和英文；基础、进阶与生态页；成功、预期失败和限制；正文、答案、导航和搜索。若不是抽样而是全量扫描，写出工具覆盖的文件数与它没有证明的事情。

Explain how the sample spans Chinese and English; foundational, advanced, and ecosystem pages; success, expected failure, and limitations; prose, solutions, navigation, and search. If a tool scans everything, record its file count and what it still does not prove.

## 5. Commands and evidence / 命令与证据

| ID | Status | Command, sample, or action | Observed result and artifact |
| --- | --- | --- | --- |
| E-01 | `<status>` | `<exact command/action>` | `<exit/result/path/count>` |

Preserve enough output to distinguish a real run from a planned one. Do not paste secrets, machine credentials, or an entire noisy build log; store an artifact and quote only the decisive lines when needed.

保留足够信息来区分“真实运行”和“计划运行”。不要粘贴密钥、机器凭据或整份嘈杂日志；需要时保存产物并只摘录决定性行。

## 6. Review checklist / 审阅清单

### F# and technical correctness / F# 与技术正确性

- `<status>` — Language guarantees, library behavior, project policy, and empirical results are not conflated.
- `<status>` — Public signatures, inference, equality/comparison, null/option/value option, mutation, async/task, disposal, and interop claims match the shown boundary.
- `<status>` — Examples use idiomatic F# when that improves the model; C# comparison does not define F# by translation.
- `<status>` — Success and expected-failure examples are executable or explicitly marked illustrative.
- `<status>` — Edge cases and negative conditions that change a reader decision are present.

### Bilingual independence / 双语独立性

- `<status>` — Paired routes, `translationKey`, metadata IDs, headings, anchors, examples, exercises, and links agree.
- `<status>` — Each language independently contains outcomes, prerequisites, reasoning, warnings, limitations, and sources.
- `<status>` — Terminology is consistent without forcing unnatural word-for-word translation.
- `<status>` — Navigation, search, code-copy feedback, and accessibility labels work in the page language.

### Sources and versions / 来源与版本

- `<status>` — Non-trivial and time-sensitive claims have an authoritative primary entry point.
- `<status>` — `checked` dates reflect an actual review, and version wording has a clear time/evidence boundary.
- `<status>` — Locks and `verifiedWith` values agree with the commands that were run.
- `<status>` — “latest/current/supported” is absent unless bounded by an exact date and source.

### Site and reader journey / 站点与读者路径

- `<status>` — Production build, internal links, anchors, locale mapping, and local search.
- `<status>` — Keyboard focus, skip link, heading names, landmarks, contrast, zoom/reflow, and mobile navigation.
- `<status>` — Code copying preserves the full snippet; long code scrolls without page-level overflow.
- `<status>` — Chapter → exercise → solution → chapter and cross-locale journeys reach the intended target.

## 7. Findings / 发现

| ID | Severity | Location | Claim or failure | Evidence | Required change / owner | Status and retest |
| --- | --- | --- | --- | --- | --- | --- |
| F-01 | `<high/medium/low>` | `<path:line or route>` | `<concise issue>` | `<command/source/observation>` | `<change and owner>` | `<open/fixed; retest evidence>` |

If there are no findings, write “No findings” and keep the sampling and evidence sections complete. “No findings” means the described review found none; it is not proof outside that scope.

如果没有发现，明确写“无发现”，但仍完整保留抽样和证据。“无发现”只描述本次范围，不证明范围外事项。

## 8. Conclusion / 结论

| Decision | Value |
| --- | --- |
| Review result / 审阅结果 | `<passed / failed>` |
| Release effect / 发布影响 | `<eligible / blocked / no release decision>` |
| Open high findings | `<integer>` |
| Open medium findings | `<integer>` |
| Open low findings | `<integer>` |
| Residual risk / 残余风险 | `<specific boundary, not “none” without justification>` |
| Follow-up / 后续 | `<owner, condition, and exact verification>` |

### Sign-off / 签署

`<reviewer, YYYY-MM-DD HH:mm TZ, commit SHA>`
