# F# 思维

一个面向 F# 初学者的中英文静态学习站点。中文与英文内容分别组织，根页面只用于选择语言。

- [在线阅读](https://vauxe.github.io/thinkinginfsharp/)
- [中文书稿](docs/zh/index.md)
- [English edition](docs/en/index.md)

## 本地运行

只需要 Node.js 22+（npm 随 Node 一起安装）：

```console
npm ci
npm run dev
```

提交前运行：

```console
npm run check
```

`npm run check` 只做两件事：检查中英文页面、标题锚点和代码块是否对应，然后构建 VitePress 站点。

## 目录

```text
docs/                         书稿与站点配置
scripts/check-book.mjs        最小双语一致性检查
.github/workflows/            GitHub Pages 部署
```

代码示例直接写在书页中，因此仓库不再包含独立的 .NET 解决方案、Aspire 工程、测试矩阵或内容生成器。
