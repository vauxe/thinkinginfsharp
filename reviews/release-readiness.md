# 发布就绪审计 / Release Readiness Audit

## 1. 记录身份

| 字段 | 值 |
| --- | --- |
| 范围 | 从提交树导出的全新工作目录、冻结依赖安装、全部自动门、capstone、README 预览流程与最终静态产物 |
| 类型 | clean export / release reproduction / static deployment |
| 审阅者 | Codex `/root` |
| 上下文 | author-context；从不含 `.git`、`node_modules`、`dist`、`bin` 或 `obj` 的导出开始 |
| 被测提交 | `91904e2a4de036f7aa743ac86221a9d6a6c4e47b` |
| 时间 | `2026-08-25 12:43 JST` |
| 来源截止 | `2026-08-25`；事实与链接状态沿用 R01 的独立审计 |
| 语言 | both；完整门覆盖中英两版，真实预览分别抽查英文桌面与中文窄屏路线 |

本记录在被测提交之后写入；除本记录和任务状态外，没有改动书稿、示例、主题、依赖或构建配置。因而被测提交精确标识生成产物所使用的输入，而不是把审计文件自身循环纳入它要证明的构建。

## 2. 环境与干净性

```text
OS/architecture: macOS 26.3 (25D125), arm64
.NET SDK: 10.0.301
Node/pnpm: 26.4.0 / 11.7.0
Restored tools: Fable 5.13.0; Fantomas 7.0.5
Browser: Chrome 151.0.7922.174
Source: git archive of the full tested commit into an ephemeral directory
Initial generated state: no .git, node_modules, docs/.vitepress/dist, bin, or obj
```

“干净”描述源目录和生成状态，不承诺禁用机器的正常内容寻址缓存。pnpm 可以复用已校验的全局 store，NuGet 也可以复用包缓存；冻结锁文件、全新 `node_modules`、locked restore 和后续编译/执行共同约束实际依赖图。

第一次诊断导出曾在文件沙箱内尝试安装，包管理器访问缓存时收到 `EPERM`，随后被中止。为了不把续跑安装计作干净复演，最终证据来自另一个重新导出的目录：它从零生成状态直接在允许的环境中完成冻结安装。前一个权限错误既没有被记为通过，也不是产品失败。

## 3. README 复演与自动证据

| ID | Status | 命令或动作 | 观察结果 |
| --- | --- | --- | --- |
| E-01 | passed | `pnpm install --frozen-lockfile` | 全新导出中直接成功；锁图解析被跳过，安装 138 个包，退出 0 |
| E-02 | passed | `dotnet tool restore` | 精确恢复 Fable `5.13.0` 与 Fantomas `7.0.5` |
| E-03 | passed | `dotnet fantomas . --check` | 退出 0，无格式改写 |
| E-04 | passed | `env CI=true pnpm test` | 39/39 内容工具测试、双语/内容门、50 项示例检查、Fable 生产构建与 Chrome smoke、VitePress 构建、全站冒烟及浏览器 5/5 全部通过 |
| E-05 | passed | 同一完整门的静态站点输出 | 201 个书页、203 个 HTML、17,287 个内部链接/锚点、双语各 1,757 个搜索分段及 20 个代表查询通过 |
| E-06 | passed | `env CI=true pnpm check:capstone` | 真实 Kestrel 与独立 C# 客户端完成放置、精确重放、确认和读取；成功/客户端错误诊断可关联，`secrets=false`，临时资源清理 |
| E-07 | passed | README 原样执行 `pnpm preview` | `http://localhost:4173` 可用；根页、英文第 3 章和 360×800 中文第 44 章均返回 200，语言、主区域、导航和布局正确 |
| E-08 | passed | DevTools 观察 preview | 抽查路线 console warning/error、页面异常和失败/错误 HTTP 响应均为 0；首个页面没有加载搜索索引，已加载资源均来自本站 |
| E-09 | passed | 静态产物清点 | 632 个普通文件、62,020 KiB：411 JS、203 HTML、14 WOFF2、2 CSS、1 SVG、1 JSON；符号链接 0 |
| E-10 | passed | 服务器依赖与泄漏检查 | 产物中 `.dll`、`.exe`、`.fs`、`.fsx`、`package.json` 和文件名含 `server` 的项均为 0；无运行时外部资源请求 |

`pnpm test` 中 VitePress 仍提示两个原始本地搜索 chunk 大于 500 kB。R05 已用冷页面证明它们不在首屏加载、只在首次搜索时按当前语言懒加载，编码响应体分别约 316 KiB 与 446 KiB。因此这不是本轮发布阻断项；静态托管仍应开启 Brotli 或 gzip。

压缩后的 VitePress 通用主题代码包含一条 Iconify 社交图标的条件回退 URL；当前配置没有 `socialLinks`，生成 HTML 也没有对应组件，所以根页和两种语言抽查路线均未发出该请求。这里的结论是“当前站点运行不依赖外部资源”，不是“第三方 bundle 中不存在任何外部 URL 字符串”。

## 4. 能力覆盖与可运行证据

下表逐项复核规格中的能力矩阵。章节与答案负责教学叙事；右栏给出不依赖“读过正文就算完成”的独立运行证据。

| 能力 | 章节 | 发布证据 |
| --- | --- | --- |
| 阅读类型、编写函数与变换集合 | 1–6、13–14 | manifest 中 `ch01`–`ch06`、`ch13`–`ch14` 的 FSI 脚本及有序输出；基础 capstone 脚本；`check:examples` |
| 用类型建模并保护不变量 | 7–12、17 | `ch07`–`ch12` 脚本、Ch17 签名项目、`Booking.Domain`；RequestId/SeatCount 智能构造、状态转换与性质测试 |
| 选择失败表示并隔离副作用 | 9、18、20–21 | `option`/`Result`、验证累积、functional core 和资源脚本；Booking workflow/adapter 测试与类型化依赖故障测试 |
| 正确处理异步、取消与并发 | 22–24 | 三章 FSI 脚本、受控异步端口测试、容量竞争/取消释放/故障分类/独立进程重启契约；测试用因果信号而非任意 sleep |
| 与 .NET/C# 互操作并维持工程质量 | 19、25–32 | null 边界、对象、运行时、F# API 与真实 C# client 项目；示例/性质/契约测试、Release 零警告和 Fantomas 门 |
| 组装可运行应用 | 33–38 | `Booking.Domain`、Contracts、Infrastructure、API 与独立 C# client；`check:capstone` 覆盖 HTTP/JSON、幂等重放、持久化、诊断与清理 |
| 根据测量选择优化手段 | 31 | Ch31 BenchmarkDotNet 项目、260 个固定/固定种子等价案例、4 组合 Dry smoke，以及绑定源码提交与命令的历史 ShortRun fixture |
| 判断生态方向是否适合 F# | 39–45 | 锁定并编译/测试 Web、FSharp.Data、Fable、Aspire、Avalonia 与 Unity managed plug-in 切片；Ch45 脚本；平台限制与 Unity 人工状态单独记录 |

结果：`8 / 8` 能力都有脚本、测试、契约、可编译项目或明确的人工边界，未用目录存在或文字主张代替证据。

## 5. 发布清单

| 项目 | 结果 | 依据 |
| --- | --- | --- |
| 45 个章节 | passed | `B01`–`B45` 为 45/45；两版正文、练习与答案由 parity/content 门成对检查 |
| 8 个附录及全站页面 | passed | `A01`–`A08` 为 8/8；首页、前言、84 项术语表、45 章答案索引与导航均由内容门检查 |
| 检查点 | passed | `C0`–`C8` 为 9/9，每项在 `tasks/todo.md` 保留命令和结果 |
| 发布审阅 | passed | R01–R06 全部完成；来源、专家、两类单语读者、代码证据、浏览器与本轮均无开放 high/medium/low finding |
| Unity 证据边界 | passed | managed plug-in 自动证据通过；Editor、adapter、Play Mode、IL2CPP 与 Player 因环境缺失继续准确标为 `not run` |
| 无应用服务器部署 | passed | 输出是自包含静态文件；任意支持 clean URL/fallback 的静态主机即可提供 GET/HEAD，预览不依赖 API、数据库或 Node 服务 |
| 用户最终确认 | pending | 技术发布门不能替代用户对内容、双语一致性、正确性与简洁性的最终验收 |

## 6. 发现、限制与部署要求

本轮没有新增产品发现。开放 high / medium / low finding：`0 / 0 / 0`。

以下是已经诚实标注的证据边界，不是被隐藏的通过项：

- 没有 Unity Editor/Player、Avalonia 原生窗口、真实移动设备、云账号或托管平台运行证据；对应章节只承诺已验证的最小切片与决策边界。
- 浏览器自动门和人工抽查使用 Chrome；没有执行 Firefox、Safari 或真实 VoiceOver/NVDA 认证。
- 外部来源可用性会随时间变化；本轮不重复冒充 R01 的联网审计。
- 部署主机必须支持根相对资源、clean URL 和 404 fallback，并应启用 Brotli/gzip；`file://` 不是受支持的打开方式。

## 7. 结论

| Decision | Value |
| --- | --- |
| Review result | `passed` |
| Release effect | `technically release-ready; awaiting user confirmation` |
| Open high findings | `0` |
| Open medium findings | `0` |
| Open low findings | `0` |
| Static artifact | `docs/.vitepress/dist`；无需应用服务器 |
| Residual risk | 未执行的平台/多浏览器边界如上；部署压缩和后续来源漂移需由实际主机与维护周期管理 |
| Follow-up | 用户确认发布标准；选定静态主机后按其 clean URL/fallback/压缩配置部署，不改写书的证据状态 |

### 签署

`Codex /root, 2026-08-25 12:43 JST, tested 91904e2a4de036f7aa743ac86221a9d6a6c4e47b`
