# 真实浏览器、可访问性与响应式审计

## 1. 记录身份

| 字段 | 值 |
| --- | --- |
| 范围 | `docs/.vitepress/dist` 生产静态产物；根页、中英文正文、搜索、导航、复制、键盘、主题与窄屏 |
| 类型 | browser / accessibility / responsive / runtime network |
| 审阅者 | Codex `/root` |
| 上下文 | author-context；自动 Chrome 门与 Chrome DevTools 独立复验 |
| 提交 | `6db774c0e854201ba158fca78bedfbc2450d9782`；R05 实现提交 |
| 时间 | `2026-08-25 12:31 JST` |
| 来源截止 | not applicable；外部来源与版本由 R01 审计 |
| 语言 | both；两版分别执行搜索与页面语义检查 |

## 2. 环境与边界

```text
OS/architecture: macOS 26.3, arm64
Node/pnpm: 26.4.0 / 11.7.0
VitePress/Playwright Core: 1.6.4 / 1.62.1
Browser exercised: Chrome 151.0.7922.174
Desktop viewport: 1280 × 900
Narrow viewport: 360 × 800
Artifact: docs/.vitepress/dist, served from an ephemeral 127.0.0.1 port
```

自动门直接读取生产静态文件，用一个只支持 `GET`/`HEAD`、阻止路径越界、按内容类型响应并提供 gzip 的临时服务器承载；没有借用开发服务器、SSR 或应用后端。真实 DevTools 检查使用 `vitepress preview` 的同一产物。

范围外：本轮没有在 Firefox、Safari、Windows 或真实屏幕阅读器中运行，也不构成完整 WCAG 合规认证；外部来源链接没有逐个联网点击。内部链接、锚点与静态资源由全站产物门另行穷举。

在一次构建后未重启旧 preview 的诊断尝试中，新哈希资源被旧进程缓存的文件清单返回 404。该进程随后被停止，按“先构建、后预览”的真实发布顺序重启；污染结果没有计作站点通过或产品失败。锁定浏览器门每次创建新静态服务器，不受此缓存行为影响。

## 3. 路线与动作

### 自动化路线

- `/`：中立双语入口、唯一主区域/一级标题、两张语言卡与 360px 布局。
- `/en/part-01/ch-03-functions-as-values`：英文结构、跳到正文、目录锚点、代码复制、上一章/下一章、同页切换、明暗主题和键盘搜索。
- `/zh/part-01/ch-03-functions-as-values`：中文结构、同页切换与中文搜索结果。
- `/zh/part-06/ch-37-consistency-idempotency`：长中文章、移动导航、目录侧栏、Escape 关闭与目标尺寸。
- `/en/appendices/c-collections`：窄屏表格/集合参考页。
- `/zh/part-07/ch-44-unity`：窄屏生态长页。

### 每页运行时观察

Chrome 监听 console warning/error、未捕获页面异常、失败请求及 HTTP `>= 400` 响应；任一命中都会使门失败。结构检查要求语言属性、单一 `main`、单一 `h1`、无重复 ID、无无名可见导航/表单控件、图片有 `alt`，且正文标题级别不跳跃。

## 4. 命令与证据

| ID | Status | 命令或动作 | 观察结果 |
| --- | --- | --- | --- |
| E-01 | passed | `env CI=true pnpm build` | 生产构建成功，203 个 HTML 产物；仅保留本地搜索原始 chunk 的体积提示 |
| E-02 | passed | `env CI=true pnpm check:browser` | 5/5：桌面阅读、英文搜索、中文搜索、360px 移动路线及父门全部通过；Chrome 151 |
| E-03 | passed | 根页与同页语言切换 | 根页分别进入完整 `/en/` 与 `/zh/`；英文第 3 章切到同路径中文再切回，`lang`、H1 和本地化导航随 SPA 状态完成更新 |
| E-04 | passed | 搜索、复制、锚点与 pager | 英中查询均找到同一 `#partial-application`；复制文本与代码块一致；目录锚点可达；英文第 3 章前后链接精确指向第 2/4 章 |
| E-05 | passed | 键盘与焦点 | 首次 Tab 到 “Skip to content”，3px 可见焦点轮廓；Enter 到 `#VPContent`；`/` 打开搜索、Escape 关闭；移动目录亦可 Escape 关闭 |
| E-06 | passed | 响应式与目标尺寸 | 根页、中文第 37/44 章和英文附录 C 在 360px 下 body/document 均不横向溢出；移动导航与目录关键目标至少 24×24px |
| E-07 | passed | 结构、主题与对比度抽样 | 抽样页结构问题 0；英文正文文本/链接在明暗主题下均不低于 4.5:1；复制后的中文反馈变量为“已复制” |
| E-08 | passed | DevTools Lighthouse，英文第 3 章 desktop navigation | Accessibility / Best Practices / SEO / Agentic Browsing 均 100；51 passed；原始 JSON 中 `score=0` 审计为 0 |
| E-09 | passed | DevTools Lighthouse，中文第 37 章 mobile navigation | 四类均 100；55 passed / 0 failed |
| E-10 | passed | DevTools Performance Resource Timing，冷页面后打开搜索 | 首屏两版搜索索引均为 0；英文只取英文索引，中文只取中文索引；均返回预期正文锚点 |
| E-11 | passed | `env CI=true pnpm test` | 39/39 内容测试、双语/内容门、50 项示例执行、Fable Chrome、生产构建、201 书页/203 HTML/17,287 内链、双语搜索静态检查和浏览器 5/5 全部通过 |

英文桌面 Lighthouse 的工具摘要显示 `Failed: 1`，但原始报告没有失败的二元审计；唯一非 `1.0` 数值是 CLS `0.044`（numeric score `0.99`），仍在良好阈值内。性能类别不在该 Lighthouse 工具的评分范围，因此本记录不声称拥有 Lighthouse Performance 分数。

## 5. 搜索 chunk 判断

| 路线 | 打开搜索前 | 实际加载索引 | 解码体积 | 编码响应体 | 冷传输观察 | 另一语言索引 |
| --- | ---: | --- | ---: | ---: | ---: | --- |
| English | 0 | `@localSearchIndexen.DxpsLb_8.js` | 1,944,355 B | 316,145 B | 316,445 B | 未加载 |
| 中文 | 0 | `@localSearchIndexzh.aGyfz-kv.js` | 2,232,759 B | 445,870 B | 446,170 B | 未加载 |

因此构建期 `> 500 kB` 提示对应整本书的原始本地索引，不是每页首屏脚本。索引在用户首次打开搜索时才加载，并按语言隔离；把两个大索引一起手工拆分会改变 VitePress 搜索模型，当前没有运行证据支持这种复杂度。部署端仍应启用 Brotli/gzip；若静态主机不压缩，首次搜索会承担表中的解码级原始体积。书继续增长时应复查该表，而不是调高警告阈值来隐藏变化。

## 6. 发现与修复

| ID | 严重度 | 位置 | 问题 | 证据 | 修复 | 状态与复测 |
| --- | --- | --- | --- | --- | --- | --- |
| R05-F01 | medium | VitePress 搜索按钮、`docs/.vitepress/theme/` | 按钮内部可见快捷键 `K` 被 axe 当作标签文本，但 `aria-label` 不含它，触发 WCAG 2.5.3 label-in-name 严重项 | 初次 desktop Lighthouse 明细定位 `.DocSearch-Button`；自动门新增契约先因缺属性失败 | 快捷键容器从辅助名称计算中隐藏；字面 `K` 改由视觉伪元素呈现；按钮声明 `Control+K Meta+K /`；保持本地化的“搜索本书”辅助名称 | fixed in `6db774c`; 浏览器 5/5、完整 `pnpm test`、英文 desktop 与中文 mobile Lighthouse 复测通过 |

开放 high / medium / low finding：`0 / 0 / 0`。

## 7. 结论

| Decision | Value |
| --- | --- |
| Review result | `passed` |
| Release effect | `eligible for R06; not an overall release decision` |
| Open high findings | `0` |
| Open medium findings | `0` |
| Open low findings | `0` |
| Residual risk | 自动审计和 Chrome 抽样不能替代多浏览器、VoiceOver/NVDA 人工认证；无压缩主机的首次搜索成本较高；外部链接可用性随网络变化 |
| Follow-up | R06 从提交树做干净安装、完整门与 preview 复演；部署主机启用内容压缩，内容显著增长时复测双语索引 |

### 签署

`Codex /root, 2026-08-25 12:31 JST, 6db774c0e854201ba158fca78bedfbc2450d9782`
