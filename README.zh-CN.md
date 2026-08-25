# F# 思维

[English](README.md) | 简体中文

一个面向 F# 初学者的中英文静态学习站点。英文是默认版本，中文使用独立路径，读者可通过站点导航切换语言。

- [在线阅读](https://vauxe.github.io/thinkinginfsharp/zh/)
- [中文书稿](docs/zh/index.md)
- [英文书稿](docs/en/index.md)

## 本地运行

只需要 Node.js 24 或更高版本；npm 随 Node.js 一起安装。

```console
npm ci
npm run dev
```

提交前运行：

```console
npm run check
```

`npm run check` 检查中英文页面、标题锚点和代码块是否对应，然后构建 VitePress 站点。

## 仓库结构

```text
docs/                         书稿与站点配置
scripts/check-book.mjs        最小双语一致性检查
.github/workflows/            GitHub Pages 部署
```

代码示例直接写在书页中，因此仓库不包含独立的 .NET 解决方案、Aspire 工程、测试矩阵或内容生成器。
