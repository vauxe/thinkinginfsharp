# F# 思维 / Thinking in F#

一本从 F# 语言本身出发的开源双语书，从表达式、类型与函数一直讲到生产级 .NET 工程、Web、Fable、Avalonia、云端和 Unity 边界。中文与英文版内容对等、各自完整；不需要借助另一种语言才能学习。

An open-source bilingual book that reasons from F# itself, from expressions, types, and functions through production .NET engineering, web development, Fable, Avalonia, cloud boundaries, and Unity. The Chinese and English editions are equivalent and independently complete; neither requires the reader to consult the other.

- [中文版源文](docs/zh/index.md) · [English source](docs/en/index.md)
- [中文前言](docs/zh/preface/index.md) · [English preface](docs/en/preface/index.md)
- [复现版本矩阵 / Reproduction matrix](docs/version-matrix.md)

> 本仓库的静态站点和示例已有自动化门禁，但发布结论只以 `reviews/release-readiness.md` 中最后一次完整审计为准；该文件尚不存在时，就表示尚未完成发布审计。不要把“本机能构建”扩大成未执行的 Unity、移动端或云环境结论。
>
> The static site and examples have automated gates, but release status is determined only by the latest complete `reviews/release-readiness.md`; if that file does not exist, the release audit is incomplete. Do not turn “builds on this machine” into an unperformed claim about Unity, mobile, or cloud environments.

## 快速开始

### 前置条件

- .NET SDK `10.0.301` 特征带；`global.json` 允许该特征带内的后续补丁，不自动跨到新特征带。
- Node.js 22 或更高版本。
- pnpm `11.7.0`；仓库启用 `engineStrict` 和精确锁定。
- 需要执行浏览器冒烟时，安装 Google Chrome。Unity 不是自动门禁的前置条件。

在仓库根目录执行：

```console
dotnet --version
node --version
pnpm --version
pnpm install --frozen-lockfile
pnpm test
pnpm preview
```

`pnpm test` 先检查内容与双语契约，再以锁定依赖构建并运行 F#/.NET/Fable 示例，最后生成站点并扫描静态链接与搜索索引。成功后，`pnpm preview` 在本机 HTTP 服务上预览 `docs/.vitepress/dist` 中的纯静态产物。不要直接用 `file://` 打开 HTML；站点使用根相对资源和 clean URL。

日常编辑可以使用：

```console
pnpm dev
```

## 发布前的完整命令

```console
pnpm install --frozen-lockfile
dotnet tool restore
dotnet fantomas . --check
pnpm test
pnpm check:capstone
```

`pnpm check:capstone` 会在临时目录中启动真实的本地 API，通过 C# 客户端检查 JSON/HTTP 契约，然后停止子进程并删除该临时目录。它不使用付费、通知或云账号。

## 常用命令

| 命令 | 作用 |
| --- | --- |
| `pnpm dev` | 启动 VitePress 编辑服务器 |
| `pnpm test:content` | 运行内容契约、生成器和站点逻辑单元测试 |
| `pnpm check:parity` | 检查中英路径、元数据、锚点、示例和练习对等 |
| `pnpm check:content` | 检查 frontmatter、术语、链接、占位符与生成产物 |
| `pnpm check:examples` | 锁定还原、Release 构建、测试、FSI 输出、预期诊断和 Fable 浏览器冒烟 |
| `pnpm check:capstone` | 运行贯穿项目的 API/C# 客户端/持久化/诊断检查 |
| `pnpm build` | 生成 `docs/.vitepress/dist` |
| `pnpm check:site` | 检查已生成页、内部链接、语言映射和本地搜索 |
| `pnpm test` | 运行主要自动门禁，并构建/冒烟静态站点 |

`generate:glossary`、`generate:solutions-guide` 和 `generate:navigation` 会覆盖它们各自的生成文件。只有在更改了权威输入后才运行，并同时提交输入与产物。

## 仓库结构

```text
docs/                 中英书稿、术语目录和 VitePress 主题
examples/             章节脚本、工程示例、生态切片与贯穿项目
tests/                .NET 测试、契约测试和浏览器冒烟
scripts/              内容、示例、生成器和静态站点门禁
reviews/              可追溯的人工审阅与发布记录
tasks/                已批准规格、实施顺序和任务验收证据
```

详细编辑、双语、引用和版本升级规则见 [CONTRIBUTING.md](CONTRIBUTING.md)。

---

## Quick start

### Prerequisites

- The .NET SDK `10.0.301` feature band. `global.json` permits later patches in that feature band, not an automatic move to another feature band.
- Node.js 22 or later.
- pnpm `11.7.0`; the workspace uses `engineStrict` and exact versions.
- Google Chrome when running browser smoke checks. Unity is not a prerequisite for the automated gate.

From the repository root:

```console
dotnet --version
node --version
pnpm --version
pnpm install --frozen-lockfile
pnpm test
pnpm preview
```

`pnpm test` checks the content and bilingual contracts, builds and runs the locked F#/.NET/Fable examples, then produces the site and inspects its static links and search indexes. After it succeeds, `pnpm preview` serves the serverless artifact in `docs/.vitepress/dist` over local HTTP. Do not open the HTML with `file://`; the site uses root-relative assets and clean URLs.

For ordinary editing, use:

```console
pnpm dev
```

## Complete pre-release commands

```console
pnpm install --frozen-lockfile
dotnet tool restore
dotnet fantomas . --check
pnpm test
pnpm check:capstone
```

`pnpm check:capstone` starts the real local API in a temporary directory, verifies the JSON/HTTP contract through a C# client, then stops the child process and removes that exact temporary directory. It uses no payment, notification, or cloud account.

## Command reference

| Command | Purpose |
| --- | --- |
| `pnpm dev` | Start the VitePress editing server |
| `pnpm test:content` | Run unit tests for content contracts, generators, and site logic |
| `pnpm check:parity` | Check paired paths, metadata, anchors, examples, and exercises |
| `pnpm check:content` | Check frontmatter, terminology, links, placeholders, and generated output |
| `pnpm check:examples` | Locked restore, Release build, tests, FSI output, expected diagnostics, and Fable browser smoke |
| `pnpm check:capstone` | Run the capstone API/C# client/persistence/diagnostics check |
| `pnpm build` | Generate `docs/.vitepress/dist` |
| `pnpm check:site` | Inspect built pages, internal links, locale mappings, and local search |
| `pnpm test` | Run the principal automated gate and build/smoke the static site |

The `generate:glossary`, `generate:solutions-guide`, and `generate:navigation` commands overwrite their generated targets. Run one only after changing its authoritative input, and commit the input and generated output together.

## Repository map

```text
docs/                 Chinese and English book, terminology catalog, and VitePress theme
examples/             chapter scripts, compiled samples, ecosystem slices, and capstone
tests/                .NET tests, contract tests, and browser smoke checks
scripts/              content, example, generator, and static-site gates
reviews/              traceable manual reviews and release records
tasks/                approved specification, implementation order, and acceptance evidence
```

See [CONTRIBUTING.md](CONTRIBUTING.md) for editing, bilingual, citation, and version-upgrade rules.
