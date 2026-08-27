# F# 思维

[English](README.md) | 简体中文

一个面向 F# 初学者的中英文静态学习站点。英文是默认版本，中文使用独立路径，读者可通过站点导航切换语言。

- [在线阅读](https://vauxe.github.io/thinkinginfsharp/zh/)
- [中文书稿](docs/zh/index.md)
- [英文书稿](docs/en/index.md)

## 本地运行

运行站点只需要 Node.js 24 或更高版本；npm 随 Node.js 一起安装。

```console
npm ci
npm run dev
```

提交前运行：

```console
npm run check
```

完整检查还需要 .NET 10 SDK。它会测试阅读主题、检查双语结构、执行每个已登记的示例并核对输出或诊断，最后构建 VitePress 站点。

## 仓库结构

```text
docs/                         书稿与站点配置
examples/                     正文引用的可运行示例源码
scripts/                      书稿、示例和阅读主题的精简检查
.github/workflows/            GitHub Pages 部署
```

书页展示聚焦当前知识点的代码片段，`examples/` 保存对应的完整可运行版本。检查会防止片段与脚本漂移，并核对准确输出或正文声明的编译诊断。
