# F# 思维 / Thinking in F#

一本从 F# 语言本身出发的中英双语书：从表达式、类型和函数，讲到 .NET 工程、Web、数据、前端、桌面、云与 Unity 的适用边界。

A bilingual book that reasons from F# itself—from expressions, types, and functions to .NET engineering and the practical boundaries of web, data, frontend, desktop, cloud, and Unity work.

- [在线阅读 / Read online](https://vauxe.github.io/thinkinginfsharp/)
- [中文源文](docs/zh/index.md) · [English source](docs/en/index.md)
- [版本与复现矩阵 / Versions and reproduction](docs/version-matrix.md)

## 阅读或编辑书站 / Read or edit the site

只构建静态书站需要 Node.js 22+ 和 pnpm 11.7.0；完整 `pnpm test` 还需要本机安装 Chrome。它不需要 .NET、Aspire、Unity 或云账号。

Building the static book requires Node.js 22+ and pnpm 11.7.0; the complete `pnpm test` also expects Chrome to be installed. It does not require .NET, Aspire, Unity, or a cloud account.

```console
pnpm install --frozen-lockfile
pnpm test
pnpm preview
```

`pnpm test` 只验证书站：中英对等、内容契约、生成文件、内部链接、搜索、生产构建和浏览器导航。`pnpm preview` 在本机预览 `docs/.vitepress/dist`；不要用 `file://` 直接打开生成的 HTML。

`pnpm test` validates only the book site: bilingual parity, content contracts, generated files, internal links, search, the production build, and browser navigation. `pnpm preview` serves `docs/.vitepress/dist` locally; do not open generated HTML through `file://`.

编辑时可运行：

```console
pnpm dev
```

## 可选：验证代码示例 / Optional: verify code examples

书中的代码来自 `examples/` 中的共享源文件；中英文不会各自复制一份。验证这些示例需要 `global.json` 指定的精确 .NET SDK 10.0.301：

Book code comes from shared sources under `examples/`; the two editions do not carry separate copies. Verifying those samples requires the exact .NET SDK 10.0.301 selected by `global.json`:

```console
pnpm check:examples
```

贯穿项目另有一项较慢的本地 HTTP/C# 契约检查：

The capstone has a separate, slower local HTTP/C# contract check:

```console
pnpm check:capstone
```

这些命令不会部署云资源。Unity 自动检查只覆盖托管插件的编译和纯逻辑；不代表已运行 Unity Editor、IL2CPP 或 Player。

These commands deploy no cloud resources. Automated Unity checks cover only the managed plug-in build and pure logic; they do not claim Unity Editor, IL2CPP, or Player execution.

## 目录 / Repository map

```text
docs/       中英文书稿和静态站点 / bilingual book and static site
examples/   正文直接引用的共享代码 / shared code referenced by the book
tests/      示例契约和浏览器检查 / example contracts and browser checks
scripts/    内容生成与验证工具 / content generation and validation
```

`examples/` 和 `tests/` 不会发布到网站，但用于防止书中的代码、链接和双语结构悄然失效。一次性规格、任务清单和阶段审阅不保留在主树中；需要时可从 Git 历史查阅。

`examples/` and `tests/` are not published, but keep code, links, and bilingual structure from silently drifting. One-time specifications, task logs, and phase reviews are left in Git history rather than kept in the main tree.

贡献规则见 [CONTRIBUTING.md](CONTRIBUTING.md)。

See [CONTRIBUTING.md](CONTRIBUTING.md) for contribution rules.
