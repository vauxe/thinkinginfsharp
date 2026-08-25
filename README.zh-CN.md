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

`npm run check` 测试阅读主题，检查中英文页面、标题锚点和代码块是否对应，然后构建 VitePress 站点。

## 行文原则

先说明概念是什么、保证什么、读者应怎么做。边界条件另起一句，每句只表达一个限制。类型不变量、失败状态、安全规则，以及改成肯定句反而会失真的精确对比，继续使用直接否定。

## 仓库结构

```text
docs/                         书稿与站点配置
scripts/check-book.mjs        最小双语一致性检查
.github/workflows/            GitHub Pages 部署
```

代码示例直接写在书页中。Markdown 书稿及其内嵌示例是事实源，Node 检查负责验证这套自包含的书稿结构。
