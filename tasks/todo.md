# 执行清单：F# 思维 / Thinking in F#

状态：已批准，实施中
总体计划：[`tasks/plan.md`](./plan.md)
批准规格：[`specs/book-site.md`](../specs/book-site.md)

> 用户已于 2026-08-24 明确批准本计划；按依赖顺序执行，并在每个检查点保留可验证状态。

## 使用约定

- `Bnn` 是双语章节任务，继承总体计划 4.1 的全部章节完成条件。
- `Enn`、`Knn`、`Xnn` 是普通章节、贯穿项目、生态的代码证据任务，继承总体计划 4.2。
- `C*` 是阶段检查点；必须完成后才能进入下一阶段。
- 路径中的 `{zh,en}` 表示两个实际文件，`{page,solution}` 只用于紧凑表达，不是待创建的字面目录。
- “聚焦检查”之后仍须运行列出的全局检查；检查点统一运行完整 `pnpm test`。
- 聚焦 .NET 测试使用 `dotnet test ThinkingInFSharp.slnx --configuration Release --filter FullyQualifiedName~<筛选键>`；实现时把“筛选键”替换为任务给出的稳定测试名称。
- 每个实现任务验证通过后检查 diff 与敏感信息并创建一个原子本地提交；不推送、不打标签、不发布。
- 审计发现不直接形成无限扩张的新范围；回派给最小原任务修正，必要时先更新本清单并获得批准。

## 0. 基础与质量门

### F01 — 初始化仓库并锁定工具链

- [x] **依赖：** 计划获批。
- **主要文件（5）：** `.gitignore`、`pnpm-workspace.yaml`、`global.json`、`package.json`、`pnpm-lock.yaml`；另执行本地 `git init`。
- **验收：** 固定 .NET 10.0.3xx 特征带、pnpm 11.7.0 与 Node ≥22；依赖含 VitePress 1.6.4；只允许其构建链所需的 `esbuild` 安装脚本；不配置远端、不发布；冻结安装可复现。
- **验证：** `git status --short`；`dotnet --version`；`pnpm --version`；`pnpm install --frozen-lockfile`。
- **规模：** S。

### F02 — 建立最小 VitePress 站点与命令契约

- [x] **依赖：** F01。
- **主要文件（5）：** `docs/index.md`、`docs/.vitepress/config/index.ts`、`docs/.vitepress/config/zh.ts`、`docs/.vitepress/config/en.ts`、`package.json`。VitePress 1.6.4 原生解析模块化 `config/index.ts`。
- **验收：** `dev/build/preview` 命令可用；根页可生产构建；配置预留中英 locale 但没有虚假章节链接。
- **验证：** `pnpm build`；启动 `pnpm dev` 后检查根页返回成功且控制台无错误。
- **规模：** S。

### F03 — 双语路由、同页切换与最小可访问主题

- [x] **依赖：** F02。
- **主要文件（两个切片）：** 路由切片修改 `docs/index.md`、`docs/{zh,en}/index.md`、`docs/.vitepress/config/{index,zh,en}.ts`；主题切片新增 `docs/.vitepress/theme/index.ts` 与 `styles.css`。
- **验收：** 根页不偏向语言；`/zh/` 与 `/en/` 可达；使用 VitePress 1.6.4 内置对应路径路由保留同一相对页面与锚点，不维护重复切换组件；键盘焦点、跳至正文、对比度和窄屏布局满足基础可访问性。
- **验证：** `pnpm build`；真实浏览器检查根页、两种 locale、锚点切换、键盘、明暗主题及 320/768/1440px 布局；移动端与桌面 Lighthouse 的 Accessibility、Best Practices、SEO、Agentic Browsing 均为 100。
- **规模：** M。

### F04 — 定义页面、术语与共享示例内容契约

- [x] **依赖：** F03。
- **主要文件：** `scripts/lib/content-contract.mjs`、`scripts/content-contract.test.mjs`、`docs/terminology.json`、`docs/{zh,en}/index.md`、`docs/.vitepress/config/index.ts`。
- **验收：** frontmatter 明确定义 `translationKey`、类型、章节/练习/示例/术语编号、状态和可复核来源；术语以稳定键映射自足的中英定义；代码引用只能使用规范化的 `@/../examples/` 共享路径；VitePress 开发与构建阶段直接执行契约。
- **验证：** `node --test scripts/content-contract.test.mjs`（7 项通过，含有效、缺字段、路径错配、重复 ID、来源、代码越界及真实术语表）；`pnpm build`。
- **规模：** M。

### F05 — 实现双语与内容静态检查

- [x] **依赖：** F04。
- **主要文件（5）：** `scripts/check-parity.mjs`、`scripts/check-content.mjs`、`scripts/lib/markdown.mjs`、`scripts/content-checks.test.mjs`、`package.json`。
- **验收：** 检出缺失翻译、键/编号不一致、占位页、无效内部链接、重复锚点、未知术语键、越界代码引用；故障返回非零且信息含文件路径。
- **验证：** `pnpm test:content`（16 项通过，含故障 CLI、活性 HTML、YAML alias 与输入体积边界）；`pnpm check:parity`；`pnpm check:content`；`pnpm build`；`pnpm install --frozen-lockfile`；`pnpm audit --audit-level high` 无已知漏洞。
- **规模：** M。

### F06 — 建立 F# 解决方案与基础测试工程

- [x] **依赖：** F01。
- **主要文件（5）：** `ThinkingInFSharp.slnx`、`Directory.Build.props`、`.editorconfig`、`tests/ExampleTests/ExampleTests.fsproj`、`tests/ExampleTests/SmokeTests.fs`。
- **验收：** F# 10/`net10.0`、nullable 和警告即错误策略集中定义；xUnit 冒烟测试进入解决方案；Release 构建无警告。
- **验证：** `dotnet restore ThinkingInFSharp.slnx --locked-mode`；Release 构建 0 警告、0 错误；xUnit 冒烟测试 1 项通过；NuGet 直接与传递依赖无已知漏洞。
- **规模：** S。

### F07 — 建立契约测试与统一示例检查器

- [x] **依赖：** F05、F06。
- **主要文件（5）：** `tests/ContractTests/ContractTests.fsproj`、`tests/ContractTests/SmokeTests.fs`、`examples/manifest.json`、`scripts/check-examples.mjs`、`package.json`。
- **验收：** manifest 支持 `script/compile/test/contract/unity-plugin/expected-error/illustrative` 分类；检查器执行对应命令、拒绝未登记有效代码，并汇入 `pnpm test`。
- **验证：** 21 项 Node 测试通过（其中 5 项覆盖示例门的七类条目与失败路径）；真实 F# 示例/契约测试通过；`pnpm test` 全链路通过。
- **规模：** M。

### C0 — 基础质量门

- [x] **依赖：** F07。
- **验收：** 冻结安装、双语/内容检查、Release F# 测试和 VitePress 构建全部成功；工作区不存在产品正文占位。
- **验证：** 从提交 `29c7b4d` 的全新临时检出执行冻结安装成功（137 个包），随后 `pnpm test` 全部通过：21 项 Node 测试、双语/内容门、F# Release 构建与测试、VitePress 生产构建均为绿。

## 1. 纵向试点与第一部分

### B01 — 第一次 F# 会话 / A First F# Session

- [x] **依赖：** C0。
- **主要文件（5）：** `docs/{zh,en}/part-01/ch-01-first-session.md`、`docs/{zh,en}/solutions/ch-01-first-session.md`、`examples/scripts/ch01-first-session.fsx`。
- **验收：** 从 FSI、脚本到最小项目建立准确心智模型；覆盖字面量、字符串、`unit`、运行方式与首个迁移练习；两种语言可独立阅读。
- **验证：** FSI 脚本输出与 manifest 断言一致；`pnpm test` 全链路通过；真实浏览器验证中英同页切换、答案往返、无控制台/网络错误及窄屏无溢出；移动端亮色与暗色 Lighthouse 四项均为 100。
- **规模：** M；这是作者工作流纵向试点。

### CP — 纵向试点评审

- [x] **依赖：** B01。
- **验收：** F# 专家确认代码与解释准确；仅中文与仅英文读者均可完成练习；共享代码、答案、导航和构建链无漂移。
- **验证：** 无上下文 F# 专家给出“无阻断，可进入下一章”结论；其 3 项低优先级发现已回派 B01/F07，修正数值推断措辞和练习预泄露，并用新增失败测试驱动脚本输出有序检查；随后 `pnpm test` 全链路通过。

### B02 — 值、绑定与表达式 / Values, Bindings, and Expressions

- [x] **依赖：** CP。
- **主要文件（5）：** `docs/{zh,en}/part-01/ch-02-values-bindings-expressions.md`、`docs/{zh,en}/solutions/ch-02-values-bindings-expressions.md`、`examples/scripts/ch02-values-bindings-expressions.fsx`。
- **验收：** 清楚区分值、绑定、表达式与语句式心智；覆盖基本类型、推断、遮蔽和类型签名阅读。
- **验证：** FSI 的 4 行确定性输出按 manifest 顺序通过；`pnpm test` 全链路通过；真实浏览器验证 320px 中英同页切换、三段共享代码、答案往返、章节翻页及无控制台/网络错误。
- **规模：** M。

### B03 — 函数也是值 / Functions Are Values

- [x] **依赖：** B02。
- **主要文件（5）：** `docs/{zh,en}/part-01/ch-03-functions-as-values.md`、`docs/{zh,en}/solutions/ch-03-functions-as-values.md`、`examples/scripts/ch03-functions-as-values.fsx`。
- **验收：** 覆盖命名/匿名函数、应用、高阶函数、柯里化、元组参数和部分应用；只建立自动泛化直觉，不提前教授值限制。
- **验证：** FSI 脚本 6 行输出与 manifest 顺序一致，交互加载核对全部推断签名；`pnpm test` 的 22 项内容测试、双语/内容/示例门与生产构建全部通过。
- **规模：** M。

### B04 — 分支与基本模式 / Branching and Basic Patterns

- [x] **依赖：** B03。
- **主要文件（5）：** `docs/{zh,en}/part-01/ch-04-branching-patterns.md`、`docs/{zh,en}/solutions/ch-04-branching-patterns.md`、`examples/scripts/ch04-branching-patterns.fsx`。
- **验收：** 从布尔判断推导 `if`/`match`；覆盖元组和列表解构、守卫及基本穷尽性，不提前依赖联合类型。
- **验证：** FSI 覆盖真假分支、守卫顺序、元组与空/一项/多项列表形状，5 行输出按 manifest 通过；`pnpm test` 的内容、双语、示例和生产构建全绿。
- **规模：** M。

### B05 — 列表、管道与数据流 / Lists, Pipelines, and Data Flow

- [x] **依赖：** B04。
- **主要文件（5）：** `docs/{zh,en}/part-01/ch-05-lists-pipelines.md`、`docs/{zh,en}/solutions/ch-05-lists-pipelines.md`、`examples/scripts/ch05-lists-pipelines.fsx`。
- **验收：** 用 `map/filter/choose` 与管道表达变换，并在同一问题上诚实对比 `for`、`while` 和可变绑定。
- **验证：** FSI 证明 `filter`+`map`、`choose`、`for` 与 `while` 的结果和顺序一致，4 行确定性输出按 manifest 通过；`pnpm test` 全链路通过。
- **规模：** M。

### B06 — 递归、尾递归与折叠 / Recursion, Tail Calls, and Folds

- [x] **依赖：** B05。
- **主要文件（5）：** `docs/{zh,en}/part-01/ch-06-recursion-folds.md`、`docs/{zh,en}/solutions/ch-06-recursion-folds.md`、`examples/scripts/ch06-recursion-folds.fsx`。
- **验收：** 从结构递减推导递归，区分普通/尾递归，并以 `fold` 重写至少一个累积问题；不宣称所有递归都会优化。
- **验证：** FSI 覆盖直接/尾递归、空/单项/普通/100,000 项边界与左右折叠顺序，5 行输出按 manifest 通过；`pnpm test` 全链路通过。
- **规模：** M。

### K01 — 预约系统第一部分纯脚本切片

- [x] **依赖：** B06。
- **主要文件（≤3）：** `examples/capstone/part-01/BookingBasics.fsx`、`tests/ExampleTests/CapstonePart01Tests.fs`、`examples/manifest.json`。
- **验收：** 用已教概念解析固定输入、变换预约行并折叠容量摘要；不提前使用领域联合类型、异步或外部 I/O。
- **验证：** FSI 的 5 行确定性输出按 manifest 通过；聚焦 `CapstonePart01` xUnit 测试通过；`pnpm check:examples` 与 `pnpm test` 全链路通过。
- **规模：** S。

### C1 — 第一部分检查点

- [x] **依赖：** K01。
- **验收：** 6 章双语正文/答案完整；所有脚本独立执行；快速入门路线和 K01 可由新读者复现；无单语占位。
- **验证：** `pnpm test` 全绿；真实浏览器在 320px 逐页验证 6 章无溢出、代码块/复制控件一一对应、答案链接与语言切换有效、控制台零警告；代表章节 Lighthouse 四项 100，快速入门首页可访问性/最佳实践/SEO 100。

## 2. 第二部分：用类型建立模型

### B07 — 记录、更新、相等与比较 / Records, Updates, Equality, and Comparison

- [x] **依赖：** C1。
- **主要文件（5）：** `docs/{zh,en}/part-02/ch-07-records-equality.md`、`docs/{zh,en}/solutions/ch-07-records-equality.md`、`examples/scripts/ch07-records-equality.fsx`。
- **验收：** 覆盖元组、记录、匿名记录、不可变更新、结构相等/比较，并显式区分引用身份与哈希语义。
- **验证：** FSI 的 5 行输出固定不可变更新、匿名投影、结构相等/引用身份反例、哈希契约与结构排序；`pnpm test` 全链路通过。
- **规模：** M。

### B08 — 可辨识联合与状态建模 / Discriminated Unions and State Modeling

- [x] **依赖：** B07。
- **主要文件（5）：** `docs/{zh,en}/part-02/ch-08-discriminated-unions.md`、`docs/{zh,en}/solutions/ch-08-discriminated-unions.md`、`examples/scripts/ch08-discriminated-unions.fsx`。
- **验收：** 用联合类型替代标志组合，教授构造/解构与完整匹配；预期不穷尽示例只作诊断引用，不污染有效脚本。
- **验证：** FSI 的 5 行输出覆盖标志矛盾、三案例构造/穷尽解构、案例数据与纯转换；非穷尽版本仅作 FS0025 诊断说明；`pnpm test` 全链路通过。
- **规模：** M。

### B09 — `option` 与 `Result` / `option` and `Result`

- [x] **依赖：** B08。
- **主要文件（5）：** `docs/{zh,en}/part-02/ch-09-option-result.md`、`docs/{zh,en}/solutions/ch-09-option-result.md`、`examples/scripts/ch09-option-result.fsx`。
- **验收：** 分别用缺失与预期失败推导两种表示，覆盖组合和错误上下文；明确 `Some null` 可能存在但把完整 null 模型留给 B19。
- **验证：** FSI 的 6 行确定性输出覆盖成功、缺失、首错短路、结构化错误上下文与 `Some null` 边界；`pnpm test` 全链路通过。
- **规模：** M。

### B10 — 递归类型与结构递归 / Recursive Types and Structural Recursion

- [x] **依赖：** B09。
- **主要文件（5）：** `docs/{zh,en}/part-02/ch-10-recursive-types.md`、`docs/{zh,en}/solutions/ch-10-recursive-types.md`、`examples/scripts/ch10-recursive-types.fsx`。
- **验收：** 建模树并从类型结构推导遍历、`map` 与 `fold`；讨论深度和栈边界但不提前优化。
- **验证：** FSI 的 6 行确定性输出覆盖空树、叶子、分支、`map`、`fold`、高度与形状保持；正文示例已在 FSI 复核；`pnpm test` 全链路通过。
- **规模：** M。

### B11 — 泛型、值限制、约束与度量 / Generics, Value Restriction, Constraints, and Units

- [x] **依赖：** B10。
- **主要文件（5）：** `docs/{zh,en}/part-02/ch-11-generics-constraints.md`、`docs/{zh,en}/solutions/ch-11-generics-constraints.md`、`examples/scripts/ch11-generics-constraints.fsx`。
- **验收：** 准确解释自动泛化和值限制；覆盖相等/比较约束、组成类型约束和度量单位，避免把 SRTP 当作普通泛型前提。
- **验证：** FSI 的 5 行确定性正例覆盖泛化、安全泛型值、工厂修复、条件式相等/比较与度量算术；FS0030、组成类型 FS0001、量纲 FS0001 及正文签名均用 F# 10 交互式复核；`pnpm test` 全链路通过，E30 后续承接诊断实验。
- **规模：** L。

### B12 — 让非法状态无法表示 / Making Illegal States Unrepresentable

- [x] **依赖：** B11。
- **主要文件（5）：** `docs/{zh,en}/part-02/ch-12-making-illegal-states-unrepresentable.md`、`docs/{zh,en}/solutions/ch-12-making-illegal-states-unrepresentable.md`、`examples/scripts/ch12-making-illegal-states-unrepresentable.fsx`。
- **验收：** 私有表示、伴生模块、智能构造函数、必要访问控制与不变量形成完整模式；解释同文件/跨文件可见性。
- **验证：** FSI 的 5 行确定性输出覆盖合法构造、规范化和三条拒绝路径；直接私有案例构造已验证为 FS1093；临时 `.fsi`/`.fs`/消费者三文件探针在 Release 零警告编译后清理；`pnpm test` 全链路通过。
- **规模：** L。

### K02 — 预约领域模型与不变量

- [x] **依赖：** B12、F06。
- **主要文件（≤5）：** `examples/capstone/src/Booking.Domain/Booking.Domain.fsproj`、`Domain.fs`、`tests/ExampleTests/BookingDomainTests.fs`、`ThinkingInFSharp.slnx`、`examples/manifest.json`。
- **验收：** 建立活动、容量、请求标识、预约状态等领域类型；公开构造函数拒绝非法容量/标识/状态；领域表示不承担 JSON 或数据库契约。
- **验证：** 先以缺失 API 获得预期编译红灯，再由 8 项 `BookingDomain` xUnit 测试覆盖标识、容量、座位、跨字段容量和状态转换；外部 FSI 直接构造 `Capacity`/`EventId` 均为 FS1093；`pnpm test` 的锁定还原、Release 构建、全测试与示例检查通过。
- **规模：** M。

### C2 — 第二部分检查点

- [x] **依赖：** K02。
- **验收：** 类型建模路径完整；结构/哈希/身份表述一致；K02 的非法状态不可经公开 API 构造；章节和答案双语对等。
- **验证：** `pnpm test` 全绿；人工复核 B07 的结构/哈希/身份条件、B11 的 `equality`/`comparison`/`int<'Measure>` 实际 FSI 签名、B12 与 K02 的同文件/跨程序集 private 边界；中英页面行数与结构检查一致。

## 3. 第三部分：组合与程序结构

### B13 — 组合、参数顺序与管道 API / Composition, Argument Order, and Pipeline APIs

- [x] **依赖：** C2。
- **主要文件（5）：** `docs/{zh,en}/part-03/ch-13-composition-pipeline-api.md`、`docs/{zh,en}/solutions/ch-13-composition-pipeline-api.md`、`examples/scripts/ch13-composition-pipeline-api.fsx`。
- **验收：** 从重复嵌套调用推导 `>>`/`<<` 和面向管道的参数顺序；说明何时直接调用比管道更清楚。
- **验证：** FSI 的 6 行确定性输出覆盖嵌套调用、等价管道、前向/后向组合、配置式部分应用和有意保持直接的谓词；`pnpm test` 的 22 项内容测试、双语/内容/示例门与生产构建全部通过。
- **规模：** M。

### B14 — 集合选择与求值 / Choosing Collections and Evaluation Models

- [x] **依赖：** B13。
- **主要文件（5）：** `docs/{zh,en}/part-03/ch-14-collections-evaluation.md`、`docs/{zh,en}/solutions/ch-14-collections-evaluation.md`、`examples/scripts/ch14-collections-evaluation.fsx`。
- **验收：** 比较 `list/array/seq/Map/Set`、转换成本和求值；在问题上下文中教授 `seq {}`；用可观察计数证明 `seq` 重复枚举；准确区分有序键约束与哈希集合约束。
- **验证：** FSI 的 8 行确定性输出和断言覆盖立即/延迟求值、重复枚举、`Seq.cache`、转换快照、有序 `Map`/`Set` 与只支持相等的哈希键；额外交互探针确认部分枚举后的增量缓存计数为 2→3，并确认该键用于 `Map` 产生 FS0001；`pnpm test` 全链路通过。
- **规模：** L。

### B15 — 活动模式与领域匹配边界 / Active Patterns and Domain Matching Boundaries

- [x] **依赖：** B14。
- **主要文件（5）：** `docs/{zh,en}/part-03/ch-15-active-patterns.md`、`docs/{zh,en}/solutions/ch-15-active-patterns.md`、`examples/scripts/ch15-active-patterns.fsx`。
- **验收：** 覆盖完整、部分和参数化活动模式的适用边界；避免用活动模式隐藏昂贵 I/O 或不可见失败。
- **验证：** FSI 的 6 行确定性输出覆盖完整模式全部分区、部分模式匹配/不匹配、保留的解析错误与参数化识别器调用次数；额外交互探针验证 F# 10 的 `bool` 部分返回及带 `[<return: Struct>]` 的 `voption` 返回；`pnpm test` 全链路通过。
- **规模：** M。

### E16 — 多文件模块、命名空间与编译顺序样例

- [x] **依赖：** B15、F06。
- **主要文件（5）：** `examples/chapters/ch16/Ch16.fsproj`、`Domain.fs`、`Workflow.fs`、`Program.fs`、`tests/ExampleTests/Ch16ProjectTests.fs`。
- **验收：** 展示正确文件顺序、模块/命名空间边界、打开声明和 `<Nullable>enable</Nullable>`；交换顺序有单独预期诊断而非关闭警告。
- **验证：** 先以缺失 API 获得 FS0039 预期红灯，再由 4 项 `Ch16ProjectTests` 覆盖可空输入、组件错误、跨文件工作流与程序组合；Release 可执行程序输出 `accepted:REQ-16 remaining=1`；反序项目稳定产生 FS0039；`pnpm check:examples` 的锁定还原、Release 构建、全测试、脚本与预期错误检查通过。
- **规模：** M。

### B16 — 模块、命名空间、项目与编译设置 / Modules, Namespaces, Projects, and Compiler Settings

- [x] **依赖：** E16。
- **主要文件（4）：** `docs/{zh,en}/part-03/ch-16-modules-namespaces-projects.md`、`docs/{zh,en}/solutions/ch-16-modules-namespaces-projects.md`。
- **验收：** 由 E16 解释文件顺序、作用域和项目结构；只教授 nullable 启用后的最小标注，不提前展开 B19 的完整空值边界。
- **验证：** E16 的 4 项 xUnit 聚焦测试覆盖可空边界、组件错误、跨文件工作流与程序组合；正文用反序项目固定 `FS0039` 诊断，并区分文件/项目/解决方案/程序集及主要编译设置；中英正文与答案行数、标题锚点和元数据严格对齐；双语、内容检查与生产构建通过。
- **规模：** M。

### E17 — 签名文件与封装边界样例

- [x] **依赖：** B16。
- **主要文件（≤5）：** `examples/chapters/ch17/Ch17.fsproj`、`Library.fsi`、`Library.fs`、`tests/ExampleTests/Ch17SignatureTests.fs`、`ThinkingInFSharp.slnx`。
- **验收：** `.fsi` 真实隐藏表示并公开惯用 F# API；测试只能经公共表面观察不变量；文件顺序正确。
- **验证：** 先以缺失项目和 `FS0039` 获得预期编译红灯，再由 4 项外部消费者测试覆盖受控构造、观察、成功分配与容量不足；`.fsi` 紧邻并先于 `.fs`，将三个表示保持抽象；独立消费者直接构造 `Capacity 0` 稳定产生 `FS0800`；Release 全解决方案构建/测试与 `pnpm check:examples` 通过。
- **规模：** M。

### B17 — 签名、访问控制与 F# API / Signatures, Access Control, and F#-Facing APIs

- [x] **依赖：** E17。
- **主要文件（4）：** `docs/{zh,en}/part-03/ch-17-signatures-encapsulation.md`、`docs/{zh,en}/solutions/ch-17-signatures-encapsulation.md`。
- **验收：** 准确解释签名文件、访问修饰、抽象表示和面向 F# 的公共 API；与 B12 的智能构造模式连接而不重复整章。
- **验证：** E17 的 4 项外部消费者测试和 `FS0800` 反例分别证明公共表面可用且隐藏表示不可构造；额外 F# 10 探针以零警告验证答案中的完整 `.fsi`/`.fs` 文件对及 `val internal`/`let internal` 语法；中英正文/答案逐行对齐，双语、内容检查与生产构建通过。
- **规模：** M。

### B18 — 显式工作流组合与验证累积 / Explicit Workflow Composition and Validation Accumulation

- [x] **依赖：** B17。
- **主要文件（5）：** `docs/{zh,en}/part-03/ch-18-workflow-validation.md`、`docs/{zh,en}/solutions/ch-18-workflow-validation.md`、`examples/scripts/ch18-workflow-validation.fsx`。
- **验收：** 用普通函数分别实现 `Result` 短路和独立错误累积；不声称存在内置验证 CE；自定义 builder 与 builder 特定 `and!` 仅为标明的延伸。
- **验证：** FSI 的 7 行确定性输出与断言覆盖首错结果、三项/两项错误累积、两种成功策略等价，以及依赖容量检查的 0/1 次调用；显式三路匹配与普通 `applyValidation` 重构结果完全一致；答案三字段探针独立执行通过；正文明确 FSharp.Core 无内置 result/validation builder 且 `and!` 语义属于特定构建器；双语、内容、示例与生产构建检查通过。
- **规模：** L。

### K03 — 预约纯工作流与验证累积

- [x] **依赖：** B18、K02。
- **主要文件（≤5）：** `examples/capstone/src/Booking.Domain/Validation.fs`、`Workflow.fs`、`Booking.Domain.fsproj`、`tests/ExampleTests/BookingWorkflowTests.fs`、`examples/manifest.json`。
- **验收：** 纯工作流输入命令与当前状态，输出事件或错误；可并行检查的输入错误全部累积，依赖前序状态的业务失败短路。
- **验证：** 先以缺失 `Validation`/`Workflow` API 获得 `FS0039` 编译红灯，再由 5 项 `BookingWorkflow` 测试覆盖双字段错误有序累积、成功事件/状态演进、容量拒绝、已有状态优先短路及无效输入优先级；外部 FSI 复核公开签名为命令/状态到事件或错误的纯函数；Release 全解决方案构建/测试与 `pnpm check:examples` 通过。
- **规模：** M。

### C3 — 第三部分检查点

- [x] **依赖：** K03。
- **验收：** 集合选择、项目顺序、签名封装和工作流组合均有代码证据；未把自定义 CE 当作基础能力。
- **验证：** `pnpm test` 全部通过（22 项内容契约测试、双语结构、内容、示例与生产构建）；人工确认第 14 章脚本以计数器证明 `seq` 的延迟执行、重复枚举与缓存语义，第 16 章只把 nullable 标注表述为可被外部/未检查代码破坏的编译期边界契约，第 18 章与 K03 分别以普通函数和 5 项测试证明独立错误累积、依赖阶段短路，并明确 FSharp.Core 不内置 `result {}` 或 `validation {}`。

## 4. 第四部分：副作用、异步与并发

### E19 — .NET 空值边界契约样例

- [x] **依赖：** C3。
- **主要文件（≤5）：** `examples/chapters/ch19/Ch19.fsproj`、`NullBoundaries.fs`、`tests/ContractTests/Ch19NullTests.fs`、`ThinkingInFSharp.slnx`、`examples/manifest.json`。
- **验收：** nullable 开启；用真实 .NET API 验证引用 `T | null`、`Nullable<T>`、`option` 和边界转换；不把 `option` 宣称为绝对无 null。
- **验证：** 先由缺失契约 API 产生 `FS0039` 编译红灯，再由 6 项 `Ch19NullTests` 覆盖 .NET 构造/成员/重载/接口、`Type.GetType` 的 `T | null` 返回、`Null`/`NonNull` 输入收窄、`Nullable<int>` 与 `option` 双向转换、引用与 `option` 双向转换及 `Some null` 反例；nullable 开启且警告视为错误，锁定还原、Release 构建/契约测试与 `pnpm check:examples` 通过。
- **规模：** M。

### B19 — .NET API 与空值边界 / .NET APIs and Null Boundaries

- [x] **依赖：** E19。
- **主要文件（4）：** `docs/{zh,en}/part-04/ch-19-dotnet-null-boundaries.md`、`docs/{zh,en}/solutions/ch-19-dotnet-null-boundaries.md`。
- **验收：** 在实质 I/O 前完成构造、成员、重载、接口与完整空值模型；提供边界转换决策表和 `Some null` 反例。
- **验证：** 中英文正文各 321 行、答案各 167 行且语义/结构对应；共享代码覆盖构造、成员、重载、接口、三种缺失表示、两组双向转换和 `Some null` 反例；答案片段在 `dotnet fsi --checknulls+` 下执行通过，6 项 E19 Release 契约测试、双语/内容检查与 `pnpm build` 通过。
- **规模：** L。

### B20 — 函数式核心与副作用边界 / Functional Core and Effect Boundaries

- [x] **依赖：** B19。
- **主要文件（5）：** `docs/{zh,en}/part-04/ch-20-functional-core-effects.md`、`docs/{zh,en}/solutions/ch-20-functional-core-effects.md`、`examples/scripts/ch20-functional-core-effects.fsx`。
- **验收：** 将时间、随机数和环境显式化为数据或依赖；说明何时接口、函数参数或闭包分别合适。
- **验证：** 中英文正文各 263 行、答案各 175 行且语义/结构对应；FSI 以固定时钟、固定抽取、映射设置和调用记录断言快照、调用顺序、后备策略、窗口边界与无额外效果的核心重放，六行输出由 manifest 精确校验；答案片段在 `--checknulls+` 下执行通过，双语/内容、全量示例与 `pnpm build` 通过。
- **规模：** M。

### B21 — 异常、资源与 I/O / Exceptions, Resources, and I/O

- [x] **依赖：** B20。
- **主要文件（5）：** `docs/{zh,en}/part-04/ch-21-exceptions-resources-io.md`、`docs/{zh,en}/solutions/ch-21-exceptions-resources-io.md`、`examples/scripts/ch21-exceptions-resources-io.fsx`。
- **验收：** 覆盖异常边界、`use`、文件 I/O 与资源释放；用决策表收束 `option/Result/验证/异常`，避免把异常包装成无信息错误。
- **验证：** 中英文正文各 278 行、答案各 187 行且语义/结构对应；FSI 在 GUID 临时目录以真实 `StreamReader` 证明成功与异常路径均释放资源、特定 I/O 异常保留结构化上下文、最终目录完成清理，五行输出由 manifest 精确校验；答案的读取/解析组合、窄异常翻译与双 reader 测试在 `--checknulls+` 下通过，双语/内容、全量示例与 `pnpm build` 通过。
- **规模：** L。

### B22 — `Async<'T>` 与 `Task<'T>` / `Async<'T>` and `Task<'T>`

- [x] **依赖：** B21。
- **主要文件（5）：** `docs/{zh,en}/part-04/ch-22-async-task.md`、`docs/{zh,en}/solutions/ch-22-async-task.md`、`examples/scripts/ch22-async-task.fsx`。
- **验收：** 在问题上下文中教授 `async {}` 与 `task {}`；由可观察门闩证明 `Async` 描述待启动工作而 `task {}` 立即开始；给出互操作和选择准则，不用任意 sleep 判定。
- **验证：** 中英文正文各 254 行、答案各 209 行且语义/结构对应；FSI 以 `TaskCompletionSource` 门闩证明 `Async<'T>` 构造后未进入、`StartAsTask` 后进入但未完成，以及 F# `task {}` 调用时立即运行到首个未完成等待，六行输出由 manifest 精确校验；答案的计数探针、Task/Async 直接组合与单航班协调器在 `--checknulls+` 下执行通过，双语/内容与 `pnpm build` 通过。
- **规模：** L。

### B23 — 取消、超时、故障与释放 / Cancellation, Timeouts, Faults, and Disposal

- [x] **依赖：** B22。
- **主要文件（5）：** `docs/{zh,en}/part-04/ch-23-cancellation-timeouts.md`、`docs/{zh,en}/solutions/ch-23-cancellation-timeouts.md`、`examples/scripts/ch23-cancellation-timeouts.fsx`。
- **验收：** 显式传播令牌，区分取消操作与放弃等待；覆盖故障传播及成功/失败/取消下的同步和异步释放。
- **验证：** 中英文正文各 260 行、答案各 258 行且语义/结构对应；FSI 用 `TaskCompletionSource`、令牌和显式截止信号证明取消操作、仅放弃等待、超时后底层仍运行、原始故障，以及同步/异步资源在成功、故障、取消三条路径上的释放，全程无计时猜测；额外编译探针验证 task `use` 会等待 `IAsyncDisposable` 三条路径，并如实记录 FSI #14454 限制；答案代码、双语/内容与 `pnpm build` 通过。
- **规模：** L。

### B24 — 并行、并发、代理与受控可变性 / Parallelism, Concurrency, Agents, and Controlled Mutation

- [x] **依赖：** B23。
- **主要文件（5）：** `docs/{zh,en}/part-04/ch-24-concurrency-agents-state.md`、`docs/{zh,en}/solutions/ch-24-concurrency-agents-state.md`、`examples/scripts/ch24-concurrency-agents-state.fsx`。
- **验收：** 区分并行/并发；覆盖 `MailboxProcessor`、锁/原子操作、共享状态和缓存的适用边界；不宣称代理自动解决一致性。
- **验证：** 中英文正文各 258 行、答案各 244 行且语义/结构对应；FSI 用显式门闩证明并发重叠而不臆测线程，以 `Barrier` 强制两个线程先读后写，稳定得到丢失更新 1，并验证 `lock`/`Interlocked` 修正为 2、复合容量不变量、邮箱串行所有权和 `ConcurrentDictionary`+`Lazy` 单次计算；共享脚本连续运行 20 次通过，答案代码、双语/内容与 `pnpm build` 通过。
- **规模：** L。

### K04 — 预约异步端口与确定性替身

- [x] **依赖：** B24、K03。
- **主要文件（≤5）：** `examples/capstone/src/Booking.Domain/Ports.fs`、`Fakes.fs`、`Booking.Domain.fsproj`、`tests/ExampleTests/BookingAsyncPortTests.fs`、`examples/manifest.json`。
- **验收：** 持久化、支付、通知、时钟端口显式接收取消令牌；替身能确定性模拟成功、故障、取消和延迟门闩，且纯领域层不依赖具体 I/O。
- **验证：** `BookingAsyncPort` 的 4 项 Release 测试通过：五个端口均记录精确调用方令牌，可控操作在显式完成前保持挂起，并原样传播指定故障与匹配令牌的取消；`Ports.fs` 只定义函数端口与边界数据，既有纯 `Domain`/`Validation`/`Workflow` 未引入具体 I/O；Release 构建及 `pnpm check:examples` 通过。
- **规模：** M。

### C4 — 第四部分检查点

- [x] **依赖：** K04。
- **验收：** 空值、资源、启动时机、取消、超时、故障、释放和竞争均有非计时型证据；章节选择建议互相一致。
- **验证：** `pnpm test` 全绿：22 项内容测试、双语/内容契约、全部 F# 示例与项目、VitePress 生产构建均通过；K04 的 4 项异步端口测试连续运行 20 次通过，B23 取消/释放脚本与 B24 屏障/代理/缓存脚本也分别连续运行 20 次，无 sleep、计时猜测或偶发失败。

## 5. 第五部分：.NET 互操作与工程质量

### E25 — F# 对象模型样例

- [x] **依赖：** C4。
- **主要文件（≤5）：** `examples/chapters/ch25/Ch25.fsproj`、`Types.fs`、`Program.fs`、`tests/ExampleTests/Ch25ObjectTests.fs`、`ThinkingInFSharp.slnx`。
- **验收：** 类、构造、成员、接口、对象表达式、类型扩展和结构体均由实际需求驱动；结构体示例说明复制和默认值风险。
- **验证：** Ch25 Release 构建和 6 项 `Ch25Object` 测试通过：主/次构造函数、成员、显式接口视图、对象表达式替身、类型扩展，以及结构体按值复制和 `Unchecked.defaultof` 绕过正数修订不变量均有证据；示例程序四行输出复核，`pnpm check:examples` 通过。
- **规模：** M。

### B25 — 在 F# 中定义对象 / Defining Objects in F#

- [x] **依赖：** E25。
- **主要文件（4）：** `docs/{zh,en}/part-05/ch-25-objects-interfaces.md`、`docs/{zh,en}/solutions/ch-25-objects-interfaces.md`。
- **验收：** 准确呈现对象特性与函数/记录/联合类型的选择边界；不把类包装当作默认“工程化”。
- **验证：** E25 的 6 项 `Ch25Object` 聚焦测试通过；三段答案代码由 F# 10 执行通过；实际程序集反射确认自动打开模块中的可选扩展不成为 `Quote` 属性；中英正文/答案分别严格保持 239/239 与 173/173 行，双语、内容检查及 VitePress 生产构建通过。
- **规模：** M。

### B26 — 深入 .NET 边界 / Deeper .NET Boundaries

- [x] **依赖：** B25。
- **主要文件（5）：** `docs/{zh,en}/part-05/ch-26-dotnet-runtime-boundaries.md`、`docs/{zh,en}/solutions/ch-26-dotnet-runtime-boundaries.md`、`examples/scripts/ch26-dotnet-runtime-boundaries.fsx`。
- **验收：** 覆盖运行时类型/转换、委托、事件、.NET 集合、引用身份和哈希；与 B07/B11/B14 的相等和键规则贯通。
- **验证：** 共享脚本的 8 行确定性输出在常规 FSI 与 `--checknulls+ --warnaserror+` 下通过，覆盖运行时类型/转换、委托、事件退订、实时集合/快照和两种字典键策略；三段答案探针在两种模式下零诊断执行；`pnpm check:examples`、双语/内容检查与 VitePress 生产构建通过。
- **规模：** L。

### E27 — 给 C# 调用的 F# API 契约样例

- [x] **依赖：** B26。
- **主要文件（5）：** `examples/chapters/ch27/FSharpApi/FSharpApi.fsproj`、`Library.fs`、`examples/chapters/ch27/CSharpClient/CSharpClient.csproj`、`Program.cs`、`examples/manifest.json`。
- **验收：** C# 客户端无需依赖 F# 语法知识即可消费稳定公共表面；DU/option 等内部表示经明确边界适配；XML 文档可见；编译即契约证据。
- **验证：** 先由缺失 API 获得 CS0246 红灯；随后 F# 库与 C# 客户端在 Release 警告即错误下构建，客户端运行断言接受/拒绝/验证、三项参数防卫、仅 4 个导出类型、所有公开签名无 `Microsoft.FSharp.*` 泄漏、C# 可空元数据及 XML 文档旁车；`pnpm check:examples` 通过。
- **规模：** M。

### B27 — 为 C# 设计 F# API / Designing F# APIs for C#

- [x] **依赖：** E27。
- **主要文件（4）：** `docs/{zh,en}/part-05/ch-27-fsharp-api-for-csharp.md`、`docs/{zh,en}/solutions/ch-27-fsharp-api-for-csharp.md`。
- **验收：** 解释公共表示、命名、属性、重载、文档和兼容性；明确 F# 内部模型不应直接泄露为 C#/JSON 契约。
- **验证：** E27 的 F# 库与 C# 客户端在 Release 警告即错误下以 0 警告构建并运行全部契约断言；两段答案代码由 F# 10 在 `--checknulls+ --warnaserror+` 下执行通过；中英正文与答案严格保持 264/264 和 196/196 行；22 项内容测试、双语/内容/完整示例门及 VitePress 生产构建通过。
- **规模：** L。

### E28 — 单元、替身与边界测试样例

- [x] **依赖：** B27。
- **主要文件（≤4）：** `examples/chapters/ch28/OrderWorkflow.fs`、`tests/ExampleTests/Ch28WorkflowTests.fs`、`tests/ContractTests/Ch28BoundaryTests.fs`、`examples/manifest.json`。
- **验收：** 纯逻辑用值测试，外部端口用小型替身，序列化等稳定边界用契约测试；不引入重型 mock 框架。
- **验证：** 先由共享实现空壳获得两项目预期 FS0039 编译红灯；随后精确过滤 `Ch28`，5 项 example tests 与 4 项 contract tests 全部通过，分别覆盖纯值结果、库存反例、手写记录替身的成功/库存不足/产品缺失协议、camel-case JSON 形状、DTO 解码、null/默认值和未知字段；`pnpm check:examples` 全门通过。
- **规模：** M。

### B28 — 示例测试、替身与边界测试 / Example Tests, Test Doubles, and Boundary Tests

- [x] **依赖：** E28。
- **主要文件（4）：** `docs/{zh,en}/part-05/ch-28-testing-boundaries.md`、`docs/{zh,en}/solutions/ch-28-testing-boundaries.md`。
- **验收：** 从风险选择测试层；展示状态与行为断言、确定性替身和契约边界；避免测试实现细节。
- **验证：** E28 聚焦过滤中 5 项 example tests 与 4 项 contract tests 全部通过；中英正文与答案严格保持 310/310 和 134/134 行；22 项内容测试、双语/内容检查、完整示例门及 VitePress 生产构建通过。
- **规模：** M。

### E29 — FsCheck 性质测试样例

- [x] **依赖：** B28。
- **主要文件（≤4）：** `tests/ExampleTests/Ch29Properties.fs`、`tests/ExampleTests/ExampleTests.fsproj`、`examples/chapters/ch29/Generators.fs`、`examples/manifest.json`。
- **验收：** 锁定 FsCheck 版本；性质、生成器、缩减和反例可复现；至少一个“看似合理但错误”的性质被反例推翻。
- **验证：** `FsCheck.Xunit` 与传递依赖 `FsCheck` 均锁定为 3.4.0；先由缺失性质 API 获得预期 FS0039 编译红灯；随后精确过滤 `Ch29`，三个各运行 300 个案例的性质与一个固定重放测试全部通过；错误的“接受请求构成前缀”性质稳定缩减为 `capacity = 1, requests = [2; 1]`；`pnpm check:examples` 全门通过。
- **规模：** M。

### B29 — 使用 FsCheck 进行性质测试 / Property Testing with FsCheck

- [x] **依赖：** E29。
- **主要文件（4）：** `docs/{zh,en}/part-05/ch-29-property-testing.md`、`docs/{zh,en}/solutions/ch-29-property-testing.md`。
- **验收：** 从例子推广到不变量，讲清生成、分类、缩减与重放；不把性质测试表述为单元测试替代品。
- **验证：** E29 三项通过性质与固定失败步重放测试全部通过；两段答案代码由 F# 10 在 `--checknulls+ --warnaserror+` 下执行通过；中英正文与答案严格保持 292/292 和 160/160 行；22 项内容测试、双语/内容检查、完整示例门及 VitePress 生产构建通过。
- **规模：** M。

### E30 — 诊断、格式化与可复现构建证据

- [x] **依赖：** B29。
- **主要文件（≤5）：** `.config/dotnet-tools.json`、`.editorconfig`、`examples/expected-errors/ch11-value-restriction.fsx`、`examples/expected-errors/ch16-file-order/`（≤2 文件）、`examples/manifest.json`。
- **验收：** 锁定 Fantomas；预期错误断言实际诊断编号；格式检查不改文件；干净 restore/build 可重复；不扩展为平台专属 CI 教程。
- **验证：** 依照 Fantomas 7 官方配置约定复用 `.editorconfig`，不创建不会被读取的 `fantomas.json`；本地工具锁定为稳定版 7.0.5，`dotnet tool restore` 与 `dotnet fantomas . --check` 通过；先由只读格式检查获得 99 红灯并列出 41 个历史文件，再建立机械格式基线且完整示例门通过；预期错误实际断言 FS0030、FS0039 与 FS0800；连续两轮 clean、`--locked-mode` restore、Release `--no-restore` build 均为 0 警告、0 错误。
- **规模：** M。

### B30 — 诊断、调试、格式化与构建 / Diagnostics, Debugging, Formatting, and Builds

- [x] **依赖：** E30。
- **主要文件（4）：** `docs/{zh,en}/part-05/ch-30-diagnostics-tooling-builds.md`、`docs/{zh,en}/solutions/ch-30-diagnostics-tooling-builds.md`。
- **验收：** 教读者从首个诊断定位根因、使用调试器/FSI、格式化和静态检查，并解释锁定与可复现原则。
- **验证：** 第 16 章文件顺序与第 11 章值限制样例继续实际断言 FS0039/FS0030；答案中的 FSI 诊断探针由 F# 10 在 `--checknulls+ --warnaserror+` 下执行通过；中英正文与答案严格保持 315/315 和 168/168 行；22 项内容测试、双语/内容检查、完整示例门及 VitePress 生产构建通过。
- **规模：** M。

### E31 — 测量、分配与优化前后对照

- [x] **依赖：** B30。
- **主要文件（≤5）：** `examples/chapters/ch31/Ch31.Benchmarks.fsproj`、`Benchmarks.fs`、`Program.fs`、`tests/ContentFixtures/ch31-baseline.json`、`examples/manifest.json`。
- **验收：** 锁定基准工具；记录运行时/OS/CPU/配置；比较一个集合或分配热点的基线与改进；结果只支持样例内结论；`voption`、Span/byref、裁剪/AOT 仅在测量语境识别。
- **验证：** 先由错误的上限比较获得确定性等价红灯（期望 7、实际 3），修复后固定与固定种子生成的 260 个案例全部通过；BenchmarkDotNet 0.15.8 锁定且 Dry smoke 的 4 个组合全部执行成功；Release ShortRun 在所记录环境中显示单遍版本两种规模的均值比均为 0.43，并把管道版 520 B/7504 B 的中间数组分配降为 0 B；基线明确限定为样例证据而非性能门；完整 `pnpm check:examples` 通过。
- **规模：** L。

### B31 — 先测量再优化 / Measure Before Optimizing

- [x] **依赖：** E31。
- **主要文件（4）：** `docs/{zh,en}/part-05/ch-31-measure-before-optimizing.md`、`docs/{zh,en}/solutions/ch-31-measure-before-optimizing.md`。
- **验收：** 给出基线、剖析、假设、修改、复测流程；清楚区分微基准与端到端性能；不提供脱离证据的“最快集合”清单。
- **验证：** E31 的 260 个等价案例、4 组合 Dry smoke 与环境化 ShortRun 基线均复核；答案中的 `option`/`voption` 校验和代码由 F# 10 在 `--checknulls+ --warnaserror+` 下执行通过；中英正文与答案严格保持 289/289 和 148/148 行；Fantomas 只读检查、22 项内容测试、双语/内容检查、完整示例门及 VitePress 生产构建通过。
- **规模：** M。

### E32 — 从函数到应用的最小宿主

- [x] **依赖：** B31。
- **主要文件（≤5）：** `examples/chapters/ch32/Ch32.App.fsproj`、`Ports.fs`、`Composition.fs`、`Program.fs`、`tests/ExampleTests/Ch32CompositionTests.fs`。
- **验收：** 组合根连接配置、端口和生命周期；提供结构化日志、指标/追踪概念的最小可观察证据；不引入容器框架作为前提。
- **验证：** 先在尚未接入计数器时得到预期红灯（3 项中 2 项通过，成功路径仅缺少 `booking.requests` 测量），接入低基数 `outcome` 标签后聚焦 Release 测试 3/3 通过；固定配置运行精确输出一条 JSON 日志以及结果、指标、追踪和释放证据共 5 行；F# 10、空值检查和警告即错误下的完整 `pnpm check:examples` 通过。
- **规模：** L。

### B32 — 从函数到应用 / From Functions to Applications

- [x] **依赖：** E32。
- **主要文件（4）：** `docs/{zh,en}/part-05/ch-32-functions-to-applications.md`、`docs/{zh,en}/solutions/ch-32-functions-to-applications.md`。
- **验收：** 从纯工作流推导端口、组合根、配置、生命周期与最小可观测性；保持模式轻量并说明何时需要更强宿主。
- **验证：** E32 聚焦 Release 测试 3/3 与固定 5 行运行证据复核；答案中的版本化端口与任务编排代码由 F# 10 在 `--checknulls+ --warnaserror+` 下执行通过，并由该探针发现、修复一处记录字段函数类型的无效参数标签语法；中英正文与答案严格保持 344/344 和 159/159 行；Fantomas 只读检查、22 项内容测试、双语/内容检查、完整示例门及 VitePress 生产构建通过；真实 Chrome 验证侧栏、共享代码、中英切换和 390px 移动视口，中文目标页无控制台告警且无页面级横向溢出。
- **规模：** L。

### K05 — 预约性质测试与稳定公共边界

- [x] **依赖：** B32、K04、E29。
- **主要文件（≤5）：** `tests/ExampleTests/BookingProperties.fs`、`tests/ExampleTests/ExampleTests.fsproj`、`examples/capstone/src/Booking.Domain/PublicApi.fs`、`Booking.Domain.fsproj`、`examples/manifest.json`。
- **验收：** 容量与状态转换具有性质测试；公开模块隐藏领域表示细节并为后续 DTO/C# 边界提供稳定函数；反例可重放。
- **验证：** 先由缺失 `Booking.Domain.PublicApi` 得到预期 FS0039 红灯；实现后编译器又以 FS0025 揭示共享转换错误联合的非穷尽映射，改为显式覆盖两个案例；两个性质各检查 500 个输入，另有公共表面反射契约和固定种子反例测试，聚焦 `BookingProperties` 4/4 通过；错误的“所有正请求都能容纳”主张稳定缩减为容量 1、请求 2；反射检查确认公开函数不泄漏 `Event`、`Booking`、`BookingState`、`BookingEvent`、`RequestId` 或 `SeatCount`，模型与视图均不可由外部构造；Release 完整示例门通过。
- **规模：** M。

### C5 — 第五部分检查点

- [x] **依赖：** K05。
- **验收：** 对象/.NET/C# 边界、单元/性质/契约测试、诊断、格式化、测量和应用装配均有可运行证据；公共 API 对 F# 与 C# 的取舍有明确理由。
- **验证：** Fantomas 全仓只读检查通过；真实 C# 客户端从 F# 库重新 Release 构建为 0 警告/0 错误，并运行验证接受、拒绝、参数守卫、四个公开类型、可空元数据和 XML 文档；F# 贯穿项目边界保留不透明模型、模块函数、`Result`/`option` 与 DU，跨语言 E27 边界则有意投影为普通 .NET 类、枚举、成员、可空值和异常契约；完整 `pnpm test` 的 22 项内容测试、双语/内容检查、所有示例/契约/性质测试与 VitePress 生产构建通过。

## 6. 第六部分：活动预约系统

### K06 — 收束领域语言、命令与事件

- [x] **依赖：** C5、K02、K05。
- **主要文件（≤5）：** `examples/capstone/src/Booking.Domain/Domain.fs`、`Commands.fs`、`Events.fs`、`PublicApi.fs`、`Booking.Domain.fsproj`。
- **验收：** 名称与业务规则统一；命令表示意图、事件表示已发生事实；私有领域表示保持封装；旧教学切片迁移而非复制第二套模型。
- **验证：** `PlaceBooking`、`ConfirmBooking`、`CancelBooking` 明确表达意图，`BookingPlaced`、`BookingConfirmed`、`BookingCancelled` 明确表达已发生事实；旧 `Validation.PlaceBookingCommand` 与 `Workflow.BookingEvent` 仅保留为类型别名，不创建第二个运行时模型；新增事件案例先触发旧测试的 FS0025 非穷尽匹配，再把意外事实写成显式失败分支；领域、工作流和性质聚焦测试 17/17 通过，K05 反射契约继续证明 `PublicApi` 不泄漏领域表示；Fantomas 全仓检查和 Release 完整示例门通过。
- **规模：** M。

### B33 — 业务语言、命令、事件与模型 / Business Language, Commands, Events, and Model

- [x] **依赖：** K06。
- **主要文件（4）：** `docs/{zh,en}/part-06/ch-33-domain-language-model.md`、`docs/{zh,en}/solutions/ch-33-domain-language-model.md`。
- **验收：** 回顾前五部分切片并解释最终领域语言；区分命令、事件、状态与边界 DTO，不把事件溯源设为必需架构。
- **验证：** 领域、工作流与性质聚焦 Release 测试 17/17 通过；正文与答案严格保持 320/320 和 136/136 行，明确区分可预约活动 `Event` 与事实 `BookingEvent`、命令/已验证命令/状态/边界 DTO，并把领域事件与 .NET 事件、消息代理、CQRS、事件溯源分离；Fantomas 全仓只读检查、22 项内容测试、双语/内容检查、完整示例门与 VitePress 生产构建通过；真实 Chrome 验证第六部分侧栏、同页中英切换、正文/答案链接与代码片段，中文页无控制台告警，390×844 下无页面级横向溢出且宽表格/代码只在内部滚动。
- **规模：** M。

### K07 — 组装纯预约工作流

- [x] **依赖：** B33、K03、K06。
- **主要文件（≤5）：** `examples/capstone/src/Booking.Domain/Validation.fs`、`Workflow.fs`、`Decider.fs`、`Booking.Domain.fsproj`、`tests/ExampleTests/BookingDeciderTests.fs`。
- **验收：** 纯决策函数连接验证、状态和事件；覆盖成功、容量不足、重复或非法命令；同一规则只有一个权威实现。
- **验证：** 先由缺失 `Decider`/`BookingDecisionError` 得到预期 FS0039 红灯；扩展统一验证错误后，编译器又以 FS0025 揭示旧公共错误投影遗漏确认码与取消原因，补为显式穷尽映射；`Decider.decide` 统一接收活动、状态和三种命令，预约复用 `decidePlaceBooking`，确认/取消复用 `Booking.confirm`/`Booking.cancel`，不复制领域规则；聚焦测试 10/10 覆盖三类独立错误有序累积、成功事件与演化、容量拒绝、重复短路、缺失/不匹配预约及终态拒绝，全部预约领域回归 27/27 通过；Fantomas 全仓检查与完整锁定还原、Release 构建、全测试、脚本及预期诊断门通过。
- **规模：** M。

### B34 — 纯预约工作流与验证 / The Pure Booking Workflow and Validation

- [x] **依赖：** K07。
- **主要文件（4）：** `docs/{zh,en}/part-06/ch-34-pure-booking-workflow.md`、`docs/{zh,en}/solutions/ch-34-pure-booking-workflow.md`。
- **验收：** 从类型和规则推导纯决策管线；清楚说明验证累积与业务短路的边界，并用 K07 失败路径支撑。
- **验证：** 正文与答案严格保持 320/320 和 170/170 行，逐段说明原始/已验证命令、独立字段错误有序累积、依赖状态规则短路、错误投影、穷尽路由、`decide`/`evolve` 分工与规则权威，并明确当前模型尚未证明活动级总容量或提交原子性；答案中的三字段验证程序在 F# 10 / .NET 10 下启用空值检查与警告即错误后执行成功；K07 精确过滤测试 10/10 通过；Fantomas 全仓只读检查、22 项内容测试、双语/内容检查、完整示例执行门与 VitePress 生产构建通过；隔离的真实 Chrome 验证同页中英切换、正文到答案导航、语义标题和网络资源，390×844 下无页面级横向溢出且宽表格/代码只在内部滚动，控制台零告警，移动端 Lighthouse 可访问性、最佳实践与 SEO 均为 100。
- **规模：** M。

### K08a — 边界 DTO、JSON 映射与配置

- [x] **依赖：** B34、K07。
- **主要文件（≤5）：** `examples/capstone/src/Booking.Contracts/Booking.Contracts.fsproj`、`Dtos.fs`、`Mapping.fs`、`tests/ContractTests/BookingJsonContractTests.fs`、`ThinkingInFSharp.slnx`。
- **验收：** Web/持久化 DTO 与领域类型分离；双向映射明确失败；JSON 字段、枚举/联合表示和兼容性由固定契约测试锁定；机密值不写入仓库。
- **验证：** 先由缺失 `Booking.Contracts` 得到预期 FS0039 红灯；实现过程中 F# 10 又以 FS3261 阻止隐式混用非空字符串与 `null`，并以 FS0250 阻止跨文件拆分同名类型/伴生模块，最终用显式 `string | null` 与独立 `*Mapping` 模块解决而未压制警告；三个命令 DTO 保留原始意图，版本 1 `BookingDto` 把领域状态 DU 投影为 `pending`/`confirmed`/`cancelled` 标签与唯一关联载荷，反向映射先检查版本、缺失值、智能构造函数和合法载荷组合，再经只接收受保护值的 `Booking.restore` 重建；精确过滤契约测试 9/9 覆盖三种 JSON 形状、全状态双向往返、缺失/非法字段、未知标签、非法联合载荷、版本优先级、真实命令 JSON、精确大小写与未知成员拒绝；全解决方案 Release 构建 0 警告/0 错误，Fantomas 和完整锁定还原、示例/测试/脚本/预期诊断门通过；未新增第三方包，`.env`、私钥与证书容器继续由 `.gitignore` 排除。
- **规模：** L。

### K08b — 无外部账号的本地持久化适配器

- [x] **依赖：** K08a。
- **主要文件（≤5）：** `examples/capstone/src/Booking.Infrastructure/Booking.Infrastructure.fsproj`、`FileStore.fs`、`Configuration.fs`、`tests/ContractTests/BookingStoreContractTests.fs`、`ThinkingInFSharp.slnx`。
- **验收：** 使用本地真实持久化保存并恢复 DTO；原子替换与损坏输入有明确处理；路径可配置且测试只用临时目录。
- **验证：** 先由缺失 `Booking.Infrastructure` 得到预期 FS0039 红灯；实现以受保护的绝对文件路径配置单一快照，领域值经版本化 DTO/JSON 落盘，写入同目录唯一临时文件、刷新中间缓冲区后用覆盖移动提交，并在成功、失败或取消后清理临时文件；读取最多接受 64 KiB，严格校验 UTF-8（兼容可选 BOM），把无效编码、无效 JSON 与无法映射到领域的不可能状态分别报告，缺失文件明确为 `Ok None`；聚焦契约测试 8/8 仅使用唯一系统临时目录，覆盖配置、真实 JSON 往返、完整替换、无残留、缺失文件、三类损坏、大小上限和取消前保留旧快照；全解决方案 Release 构建 0 警告/0 错误，完整锁定示例门通过，且未新增第三方包。这里保证单个文件的同卷替换边界，不提前声称解决 K11 的跨请求容量原子性。
- **规模：** L。

### K09 — 支付、通知替身与生命周期

- [x] **依赖：** K08b、K04。
- **主要文件（≤5）：** `examples/capstone/src/Booking.Infrastructure/PaymentStub.fs`、`NotificationStub.fs`、`Composition.fs`、`Booking.Infrastructure.fsproj`、`tests/ExampleTests/BookingAdapterTests.fs`。
- **验收：** 替身确定性产生成功、拒绝、故障与取消；资源所有权和释放位置明确；不启动额外 HTTP 服务。
- **验证：** 先由缺失 `Booking.Infrastructure`/替身 API 得到预期 FS0039 红灯；支付替身由构造时固定行为确定授权、拒绝或故障，通知替身确定交付或故障，两者在记录调用前尊重调用方取消且不使用网络、随机数或延时；`Composition.start` 创建并拥有替身，把文件快照、支付、通知和注入时钟装配成领域 `AsyncPorts`，校验事件键一致性，把存储分类保留在不泄露快照内容的类型化异常中，并以幂等 `Dispose` 关闭使用边界；聚焦测试 6/6 覆盖所有替身结果、取消无副作用、真实持久化桥接、令牌传递、类型化损坏错误、重复释放和释放后拒绝调用；全解决方案 Release 构建 0 警告/0 错误，完整锁定示例门通过，未添加 HTTP 服务或第三方包。
- **规模：** M。

### B35 — 端口、持久化、配置与替身 / Ports, Persistence, Configuration, and Stubs

- [x] **依赖：** K08a、K08b、K09。
- **主要文件（4）：** `docs/{zh,en}/part-06/ch-35-ports-persistence-config.md`、`docs/{zh,en}/solutions/ch-35-ports-persistence-config.md`。
- **验收：** 解释端口到本地适配器的装配、DTO 映射、配置与替身失败；明确领域 DU/记录不直接序列化。
- **验证：** 正文严格保持 312/312 行，答案严格保持 154/154 行；逐段对齐说明向内依赖、领域/DTO 形状分离、带载荷联合的标签契约、版本优先反向映射、命令映射信任层级、严格 JSON 策略、路径与机密的区别、64 KiB 有界读取、UTF-8/BOM、同目录临时写入—刷新—移动、损坏分类、异步端口、确定性替身与组合根所有权，并明确同文件替换不等于跨效果事务，当前单快照也不等于多预约一致性存储；答案覆盖版本专属严格解析与惰性迁移、中断点/断电保证审计，以及拥有型与借用型客户端生命周期；K08a/K08b/K09 聚焦测试分别 9/9、8/8、6/6，全仓 22 项内容测试、双语/内容检查、完整锁定示例门与 VitePress 生产构建通过；真实 Chrome 验证英文正文到答案、同页切换中文、章节/答案侧栏与语义标题，390×844 下无页面级横向溢出且宽代码/表格保持内部滚动，控制台零告警，移动端 Lighthouse 可访问性、最佳实践、SEO 与可代理浏览均为 100。
- **规模：** L。

### K10 — ASP.NET Core Web API 边界

- [x] **依赖：** B35。
- **主要文件（≤5）：** `examples/capstone/src/Booking.Api/Booking.Api.fsproj`、`Program.fs`、`Endpoints.fs`、`tests/ContractTests/BookingApiTests.fs`、`ThinkingInFSharp.slnx`。
- **验收：** API 只接收/返回边界 DTO；输入验证、JSON、错误映射、异步与请求取消正确；机密配置不进入响应/日志；测试进程内运行。
- **验证：** 先由缺失 `Booking.Api` 得到预期 FS0039 红灯；实现以 `PlaceBookingDto`、`ConfirmBookingDto`、`CancelBookingDto` 和 `BookingDto` 固定四个 HTTP 路由，另用稳定 `ApiErrorDto` 映射传输、验证、领域、支付、存储与未知故障，响应不回显异常消息、支付结果或配置路径；严格 JSON 继续拒绝错误大小写与未知字段，命令体在 Kestrel 和进程内测试宿主中都限制为 16 KiB，请求的 `CancellationToken` 原样贯穿加载、支付、追加与通知；放置流程明确暴露“支付在提交前、通知在提交后”的阶段性部分失败窗口，留给 K11 解决幂等与恢复而不伪称事务；官方 `Microsoft.AspNetCore.TestHost` 10.0.9 锁定到与本机 ASP.NET Core 运行时一致的版本，精确 `BookingApi` 过滤 16/16 覆盖成功、生命周期、严格输入、有界读取、错误状态、机密脱敏、提交前后故障、依赖自身截止映射与无 sleep 客户端取消；全解决方案 Release 构建 0 警告/0 错误，真实 Kestrel 回环冒烟得到 `201`/`Location`、持久化后的 `200` 与严格 JSON `400`，最终宿主关闭 `Server` 响应头，默认日志没有请求体、支付标识或存储路径；Fantomas、示例清单单测与完整锁定 `pnpm check:examples` 通过，临时快照已删除。
- **规模：** L。

### B36 — Web API、JSON 与输入边界 / Web API, JSON, and Input Boundaries

- [x] **依赖：** K10。
- **主要文件（4）：** `docs/{zh,en}/part-06/ch-36-web-api-boundaries.md`、`docs/{zh,en}/solutions/ch-36-web-api-boundaries.md`。
- **验收：** 由 K10 解释端点、DTO、验证、取消和机密配置边界；提供可复制的本地运行/请求命令及失败响应。
- **验证：** 正文严格保持 393/393 行，答案严格保持 174/174 行；逐段对齐从 HTTP 字节到响应 DTO 的外层解释过程、四条命令路由、DTO/领域隔离、传输—存在性—领域三层验证、无 `Content-Length` 时仍有效的 16 KiB 上限、统一严格 JSON、稳定状态码/错误代码、`RequestAborted` 传播、支付—追加—通知时间线、提交前后部分失败、异常脱敏、配置/机密/日志分类、`TestServer` 与真实 Kestrel 的互补证据、本地 Bash/PowerShell 启动及 `curl` 成功/失败请求，并明确无认证/TLS/CORS/速率限制的回环样例不得原样公开；答案覆盖自动绑定下保持契约、支付歧义/发件箱式重放证据，以及边缘与受信代理拓扑的控制责任；引用当前 .NET 10 / ASP.NET Core 官方资料；K10 精确契约现为 16/16，并额外锁定依赖自身截止与客户端断连的不同语义；全解决方案 Release 构建和最终锁定示例门通过，双语/内容检查与 VitePress 生产构建通过；真实 Chrome 验证英文正文到答案、答案同页切换中文、两类侧栏与语义标题，390×844 下无页面级横向溢出且宽代码/表格保持内部滚动，控制台零告警，移动端 Lighthouse 可访问性、最佳实践、SEO 与可代理浏览均为 100。
- **规模：** L。

### K11 — 原子容量、幂等、重试与重启恢复

- [x] **依赖：** B36、K10。
- **主要文件（≤5）：** `examples/capstone/src/Booking.Infrastructure/AtomicBookingStore.fs`、`Idempotency.fs`、`Booking.Infrastructure.fsproj`、`tests/ContractTests/BookingConsistencyTests.fs`、`tests/ContentFixtures/capstone/`。
- **验收：** 两个受控并发预约不超卖；重复请求 ID 不重复副作用；失败后重试语义明确；新进程实例从持久状态恢复；测试不用不稳定 sleep。
- **验证：** 先由缺失 `AtomicBookingStore`、`IdempotentBookingService` 与一致性错误类型得到预期 FS0039 红灯；版本化聚合快照在同一次替换中保存活动容量、多预约与命令进度，待处理/已确认预约占位、取消释放座位，按规范化命令种类、请求标识和载荷指纹重放已确认结果并拒绝同键异载荷；进程内同路径工作流门把容量读取—决策—写入串行化，明确不声称跨进程并发写入安全。支付调用前持久化 `paymentStarted`，故障后重试返回结果未知且不盲目重扣；授权后的预约与待通知标记原子提交，通知故障重试只重发通知，并明确其为至少一次而非恰好一次。6 项 `BookingConsistency` 契约测试以显式门闩覆盖竞争容量、并发重复、指纹冲突、通知失败重试、支付歧义、取消释放容量，并真实启动第二个 `dotnet fsi` 进程证明重启后恢复与零副作用重放；无 `Sleep`/计时猜测，精确过滤连续 20 轮共 120 次通过。全解决方案 Release 构建 0 警告/0 错误，完整锁定 `pnpm check:examples` 通过。
- **规模：** L。

### B37 — 一致性、幂等、重试与部分失败 / Consistency, Idempotency, Retries, and Partial Failure

- [x] **依赖：** K11。
- **主要文件（4）：** `docs/{zh,en}/part-06/ch-37-consistency-idempotency.md`、`docs/{zh,en}/solutions/ch-37-consistency-idempotency.md`。
- **验收：** 从真实竞争场景推导原子约束、幂等和重试；诚实说明本地适配器保证及其非分布式边界。
- **验证：** 正文严格保持 388/388 行，答案严格保持 214/214 行；中英逐段对齐聚合容量拥有者、受控超售时间线、占座策略、1 MiB 版本化聚合快照、进程内同路径门闩、文件整体替换但非 ACID 的边界、操作键与规范化载荷指纹、HTTP 方法幂等与应用幂等的区别、支付/通知持久阶段、重试矩阵、支付结果未知的对账停点、至少一次通知、真实独立进程重启证据、K11 保证表、因果并发测试与生产升级条件；答案覆盖按活动键与版本条件写入的跨进程方案、冲突后重新决策、提供商幂等键与 `Authorized`/`Declined`/`NotFound` 对账，以及含租约、退避、死信和消费者原子去重的事务性发件箱，并明确不承诺跨系统恰好一次。引用 .NET 10、RFC 9110 与 Microsoft Architecture Center 官方资料；6 项 `BookingConsistency` 精确过滤连续 20 轮共 120 次通过；完整 `pnpm test` 通过 22 项内容测试、双语/内容契约、全部锁定示例构建执行与 VitePress 生产构建。真实 Chrome 验证英文正文到答案、答案同页切换中文、两类侧栏与最终中文重启证据；390×844 下无页面级横向溢出且宽代码/表格内部滚动，控制台零告警，移动端 Lighthouse 可访问性、最佳实践、SEO 与可代理浏览均为 100。
- **规模：** L。

### K12a — 集成测试与 C# 契约客户端

- [x] **依赖：** B37、K11、E27。
- **主要文件（≤5）：** `examples/capstone/clients/Booking.CSharpClient/Booking.CSharpClient.csproj`、`Program.cs`、`tests/ContractTests/BookingEndToEndTests.fs`、`tests/ContractTests/ContractTests.fsproj`、`ThinkingInFSharp.slnx`。
- **验收：** C# 客户端通过已发布 DTO/API 表面完成预约；端到端测试覆盖成功、重复、冲突和无效 JSON；不引用领域内部类型。
- **验证：** 先由缺失 `BookingEndpoints.mapConsistent` 得到预期 FS0039 红灯；保留 K10 的旧端口级 `map` 契约测试，同时新增最终 `mapConsistent` 组合路径，共享同一套 16 KiB/严格 JSON/DTO/验证/安全异常边界，并把聚合容量、幂等冲突、前序操作未完成、拒付、支付结果未知、依赖与存储故障映射为稳定且不泄密的 HTTP 结果；实际 `Program.fs` 已使用 `AtomicBookingStore`、`IdempotentBookingService` 和受控支付/通知替身。4 项 `BookingEndToEnd` 测试以真实 `TestServer`、原子快照和一致性服务覆盖首次 `201`、规范化等价重复的同结果重放、同键异载荷 `idempotency_conflict`、效果计数不重复、GET 当前状态、无效 JSON 在存储/外部效果前失败、支付故障后精确重试返回 `payment_outcome_unknown` 且不盲目重复扣款，以及相关响应头、低基数指标与内部子 Activity。C# 客户端只直接引用 `Booking.Contracts`，通过 `HttpClient` 和公开 DTO 执行放置—重放—确认—读取；Release 构建 0 警告/0 错误，并针对真实 Kestrel 独立进程输出 `Placed`、`Replay: status=201 same-body=True`、`Confirmed` 与 `Loaded: status=200 same-body=True`。完整锁定 `pnpm check:examples` 通过解决方案还原、构建、全部测试和已注册样例检查。
- **规模：** L。

### K12b — 诊断、运行说明与发布检查

- [x] **依赖：** K12a、E32。
- **主要文件（≤5）：** `examples/capstone/src/Booking.Api/Diagnostics.fs`、`Program.fs`、`examples/capstone/README.md`、`scripts/check-capstone.mjs`、`package.json`。
- **验收：** 日志含相关 ID 而不泄露机密；最小指标/追踪位置可解释；一条命令执行 capstone 构建、测试和本地冒烟；运行说明从干净状态可复现。
- **验证：** 先由 `pnpm check:capstone` 在真实 Kestrel 失败于缺失 32 位 W3C 相关 ID 得到预期红灯；`BookingRequestDiagnostics` 现把 ASP.NET Core TraceId 放入 `X-Correlation-ID`、事件 1000 的结构化完成日志与日志作用域，只记录方法、路由模板、状态、低基数结果和耗时，不记录请求/响应体、请求 ID、确认码、提供商交易文本、异常消息或快照路径。`booking.http.requests` 计数器与 `booking.http.duration` 直方图只使用有界 `outcome` 维度；`ThinkingInFSharp.Booking.Api` 的 `booking.http.request` Activity 作为服务器 Activity 的内部子活动，并正确容忍无监听器时的 `null`。第 4 项 `BookingEndToEnd` 通过 `MeterListener` 与 `ActivityListener` 证明响应相关 ID、两项测量和停止的子 Activity 对齐。`scripts/check-capstone.mjs` 无 shell 拼接地执行锁定还原、Release 构建、全部 Booking 测试、动态回环端口真实 API、独立 C# 客户端、无效 JSON 契约、成功/失败相关日志和禁用文本断言，并在 `finally` 停止进程、清除唯一临时目录；`examples/capstone/README.md` 提供一条命令及 Bash/PowerShell 手工复现、配置、端点、诊断信号、保证和明确非生产边界。最终 `pnpm check:capstone` 输出客户端四阶段成功与 `Diagnostics: success=true client-error=true ... secrets=false`；人工日志核对响应 ID 与失败日志一致，成功/失败行仅含受控字段；完整 `pnpm check:examples` 通过。
- **规模：** M。

### B38 — 集成、诊断、C# 客户端与发布 / Integration, Diagnostics, C# Client, and Release

- [x] **依赖：** K12a、K12b。
- **主要文件（4）：** `docs/{zh,en}/part-06/ch-38-integration-diagnostics-release.md`、`docs/{zh,en}/solutions/ch-38-integration-diagnostics-release.md`。
- **验收：** 把运行、集成测试、诊断、C# 契约和发布检查收束成可复现闭环；列出保证、限制和下一步，不假装样例已是完整商业系统。
- **验证：** 正文严格保持 355/355 行，答案严格保持 176/176 行；中英逐段对齐最终组合根、共享 HTTP 策略表面、纯函数—适配器—一致性—进程内 HTTP—真实进程证据阶梯、因果测试、只引用公开 Contracts 的 C# 客户端、相关 ID 的非身份边界、结构化日志脱敏、低基数计数器/直方图、可空内部子 Activity、插桩与收集的区别、单命令锁定还原/Release 构建/Booking 测试/真实 Kestrel/C# 冒烟/失败日志/有界清理、构建—发布—部署—运维区分、生产门与双向保证台账；答案把三个夸大主张改写为有拓扑和证据的窄主张，给出兼容 OpenTelemetry 的源/采样/脱敏/基数测试方案，并为依赖框架 Linux 容器设计同一摘要的发布、晋级、金丝雀、模式演进与条件回滚。引用 ASP.NET Core 集成测试、.NET 追踪/指标/日志与发布官方资料；`pnpm check:capstone` 通过真实客户端四阶段及成功/失败相关日志验证，完整 `pnpm test` 通过 22 项内容测试、双语/内容契约、锁定样例检查和 VitePress 生产构建。真实 Chrome 验证英文正文到答案、答案同页切换中文与两类侧栏；390×844 下无页面级横向溢出且宽表格内部滚动，控制台零消息，移动端 Lighthouse 可访问性、最佳实践、SEO 与可代理浏览均为 100。
- **规模：** L。

### C6 — 贯穿项目检查点

- [x] **依赖：** B38。
- **验收：** 规格要求的 JSON/C#、竞争预约、重复 ID、失败重试、取消/超时、资源释放和重启持久化场景全部有证据；无需账号或额外服务。
- **验证：** 逐项证据审计完成：9 项版本化严格 JSON 契约与 API/端到端输入测试覆盖 JSON；独立 C# 进程仅引用 `Booking.Contracts` 并经真实 HTTP 完成放置—精确重放—确认—读取；显式门闩竞争测试证明容量 3 下两个 2 座请求恰有一个成功；并发等价命令、同键异载荷冲突与效果计数证明重复 ID 语义；通知失败只重发待通知阶段，支付故障后停在结果未知且不盲目重扣；新增无计时特征测试把依赖自身截止稳定映射为 `503 dependency_unavailable`，并与客户端 `RequestAborted` 保持取消的测试区分；文件取消写入无临时残留且保留旧快照，组合根重复释放、释放后拒用和检查脚本 `finally` 精确清理证明资源边界；真实第二个 `dotnet fsi` 进程从磁盘恢复并零效果重放。最终 `BookingApi` 16/16、`pnpm check:capstone` 与完整 `pnpm test` 通过；按 README 已从唯一临时目录运行真实 Kestrel、独立 C# 客户端及无效 JSON 成功/失败流程并核对相关日志，随后停止进程并删除精确目录。除可能从公开 NuGet 源取得已锁定包外，无需账号、私有源或额外运行时服务。

## 7. 第七部分：生态地图

### X39 — ASP.NET Core/F# Web 代表性样例

- [x] **依赖：** C6。
- **主要文件（≤5）：** `examples/ecosystem/web/WebSample.fsproj`、`Program.fs`、`tests/ContractTests/WebSampleTests.fs`、`examples/ecosystem/web/README.md`、`examples/manifest.json`。
- **验收：** 锁定验证版本；用 F# Minimal API 完成一个输入/输出与错误边界；说明它和 capstone 的关系，避免复制第二个大型 Web 应用。
- **验证：** 先由 `WebSample.map` 缺失得到预期 FS0039 红灯；新增 `net10.0` F# Web SDK 项目，仅依赖 .NET 共享 Web 框架并锁定 `FSharp.Core` 10.1.301。一个 `POST /api/greetings` Minimal API 使用显式 `GreetingRequestDto`/`GreetingResponseDto`/`WebSampleErrorDto`、区分大小写且拒绝未知成员的 JSON、非空名称验证、稳定 `400 invalid_json`/`name_required`、`415 unsupported_media_type` 和不泄露异常的 `500 internal_error`，并原样传播请求取消。真实 `WebApplication` + `TestServer` 的 7 个契约用例覆盖修剪后的成功输出及六类传输/验证失败，Release 构建 0 警告/0 错误；README 给出可复制回环运行命令，明确样例只隔离平台原生 Web 选择，预约领域、持久化、一致性、诊断和 C# 客户端仍由 capstone 负责，也明确未验证套接字/TLS/认证/限流/正文上限/部署。项目、测试和源文件均进入解决方案与清单，完整锁定 `pnpm check:examples` 通过。
- **规模：** M。

### B39 — ASP.NET Core 与 F# Web 生态 / ASP.NET Core and the F# Web Ecosystem

- [x] **依赖：** X39。
- **主要文件（4）：** `docs/{zh,en}/part-07/ch-39-web-ecosystem.md`、`docs/{zh,en}/solutions/ch-39-web-ecosystem.md`。
- **验收：** 按问题类型比较平台原生 Minimal API 与主要 F# 选择；列优势、摩擦、互操作和选择条件；第三方信息含版本与官方来源。
- **验证：** 正文严格保持 377/377 行，答案严格保持 211/211 行；中英逐段对齐从共享 ASP.NET Core 平台出发，结合 X39 的显式 DTO、严格 JSON、`HttpContext -> Task`/`RequestDelegate` 适配与 7 项 `TestServer` 契约，再按真实问题形状比较 Minimal API、控制器、Giraffe、Falco、Oxpecker 与 Saturn；把序列化、OpenAPI、HTML/HTMX、认证授权、依赖注入、测试、性能、安全、供应链和部署拆成独立决策，并以功能核心隔离、三速测试与有界采用试验约束框架选择和迁移。版本表明确标注 2026-08-25 检查日，引用 Microsoft 官方 .NET 10 文档及 NuGet/GitHub 官方项目资料，区分 Giraffe 8.3.0、Falco 5.2.0、Oxpecker 2.0.1、Saturn 0.17.0 的稳定版本、包目标与计算兼容性，也明确这些第三方框架只审阅了官方元数据而未在本仓库编译执行；答案覆盖三个团队的条件式选择、保留原七项 HTTP 契约的 Falco 试验，以及含路由唯一所有权、认证/OpenAPI 比较、产物级回滚和旧包移除门槛的可逆迁移。X39 聚焦测试 7/7，通过双语/内容检查与 VitePress 生产构建；真实 Chrome 验证英文正文到答案、答案同页切换中文、两类侧栏和语义标题，390×844 下正文与答案均无页面级横向溢出，宽代码保持内部滚动，控制台零消息，移动端 Lighthouse 可访问性、最佳实践、SEO 与可代理浏览均为 100。
- **规模：** L。

### X40 — 数据、查询与类型提供器使用样例

- [x] **依赖：** B39。
- **主要文件（≤5）：** `examples/ecosystem/data/DataSample.fsproj`、`Program.fs`、`tests/ContentFixtures/data/sample.csv`、`tests/ExampleTests/DataSampleTests.fs`、`examples/manifest.json`。
- **验收：** 锁定数据/类型提供器包；固定本地样本保证编译稳定；展示查询与类型提供器消费，但不教授类型提供器创作或依赖在线 schema。
- **验证：** 依据官方 NuGet 与项目文档锁定当前稳定 `FSharp.Data` 8.2.0 及完整传递图；`CsvProvider` 只在编译期读取仓库内固定 `sample.csv`，以 `ResolutionFolder=__SOURCE_DIRECTORY__` 消除构建工作目录差异，运行期则由调用方显式传入本地路径，不读取在线 schema。先修正空可执行脚手架的 FS0988，再由缺失 `summarizeByRegion`、`highValueOrders` 和结果记录得到预期 FS0039 红灯；实现使用提供器推断的 `int`、`decimal` 与 `DateOnly` 列，先把生成行映射成普通公开记录，再分别以 `Seq.groupBy` 和 `query { where; sortByDescending; select }` 完成区域聚合与高价值订单查询。两项聚焦测试 2/2 精确断言六行夹具的订单数、单位数、收入、日期、过滤与排序；命令行先把用户相对路径规范化为绝对路径，真实执行稳定输出 South 400.00、West 390.00、North 251.00。Fantomas 7 检查通过；全解决方案 `--locked-mode` 还原后，数据项目 Release `--no-restore` 构建为 0 警告/0 错误，测试同样 `--no-restore` 通过，完整 `pnpm check:examples` 通过且无需数据库、网络 schema、账号或额外服务。
- **规模：** M。

### B40 — 数据、类型提供器、分析与机器学习 / Data, Type Providers, Analytics, and ML

- [x] **依赖：** X40。
- **主要文件（4）：** `docs/{zh,en}/part-07/ch-40-data-analytics.md`、`docs/{zh,en}/solutions/ch-40-data-analytics.md`。
- **验收：** 覆盖数据访问、查询、类型提供器、分析、可视化和 ML 的决策地图；区分稳定 schema 与动态外部数据的风险。
- **验证：** 正文严格保持 432/432 行，答案严格保持 252/252 行；中英逐段对齐从数据契约与表示选择出发，以 X40 的本地编译期 CSV 样本、私有生成行适配器、普通公开记录、`Seq` 聚合和查询表达式为可执行锚点，明确类型推断不是运行期兼容性承诺，并比较稳定样本、实时设计期来源与显式动态模式。章节按事务语义、查询翻译、数据规模与缺失值处理区分直接 ADO.NET、Dapper、EF Core、SQLProvider、普通集合和 Deedle，再把可复现 `dotnet fsi` 探索、Plotly.NET 可视化、ML.NET、ONNX Runtime、TorchSharp 与外部 Python 放进训练—产物—推理边界；版本表标注 2026-08-25，引用官方资料并明确只有 FSharp.Data 在本仓库实际编译测试，也记录 Polyglot Notebooks 与 .NET Interactive 已弃用/归档而不再推荐为新工作流默认。答案用三种真实约束给出可推翻的数据边界选择，完整设计 CSV v1/v2 漂移、货币与日期语义、隔离和删除证据，并把探索模型收束为可追溯训练、黄金向量、独立推理、受控部署与回滚。X40 聚焦测试 2/2、双语/内容检查与 VitePress 生产构建通过；真实 Chrome 验证英文正文到答案、答案同页切换中文及两类侧栏，390×844 下正文与答案均无页面级横向溢出且宽表格内部滚动，控制台零消息，移动端 Lighthouse 可访问性、最佳实践、SEO 与可代理浏览均为 100。
- **规模：** L。

### X41a — Fable 工作区与锁定依赖

- [x] **依赖：** B40、F01。
- **主要文件（5）：** `pnpm-workspace.yaml`、`examples/ecosystem/fable/package.json`、`FableSample.fsproj`、`pnpm-lock.yaml`、`examples/manifest.json`。
- **验收：** 锁定 Fable、.NET tool 与前端直接依赖；根冻结安装覆盖生态工作区；包脚本预留开发、生产构建和浏览器冒烟入口。
- **验证：** 根工作区现显式包含 `examples/ecosystem/fable`，样例包精确固定 Vite 6.4.3，并预留锁定还原、开发监听、Fable 生产编译、Vite 打包与浏览器冒烟脚本；根工具清单锁定 Fable 5.13.0，项目直接锁定 Fable.Core 5.2.0 与 Fable.Browser.Dom 2.20.0，完整 NuGet 传递图写入 `packages.lock.json`。先让清单因缺失项目、源文件与解决方案引用产生四项预期红灯，再补入最小 `net10.0` 项目；`CI=true pnpm install --frozen-lockfile` 覆盖两个工作区且通过供应链策略，`dotnet tool restore` 恢复 Fable/Fantomas，项目 `--locked-mode` 还原和 Release `--no-restore` 构建通过且 0 警告/0 错误。直接运行 Fable 5.13.0 将一个 F# 源文件编译为 JavaScript，整套 `pnpm check:examples` 也识别、锁定还原、构建并通过该项目。
- **规模：** M。

### X41b — Fable 浏览器构建与最小交互

- [x] **依赖：** X41a。
- **主要文件（≤4）：** `examples/ecosystem/fable/App.fs`、`index.html`、`vite.config.mjs`、`tests/site-fable-smoke.mjs`。
- **验收：** 一个最小交互由 F# 编译到浏览器并在生产模式构建；样例和说明准确区分生成 JS、浏览器 API 与可用 .NET API。
- **验证：** 先新增锁定 `playwright-core` 1.62.1、明确驱动已安装系统 Chrome 而不暗示仓库固定浏览器二进制的契约，在尚无生产产物时得到预期 `dist/index.html` ENOENT 红灯；实现把记录模型与 `Increment | Reset` 消息交给纯 `update`，只在 DOM 适配壳持有可变模型、绑定事件并渲染。Fable 5.13.0 从唯一 `App.fs` 生成 JavaScript，Vite 6.4.3 生产构建转换 15 个模块，产出约 4.32 kB HTML 与 6.74 kB JS；页面自足说明浏览器执行生成 JavaScript、`Browser.Dom` 是类型化浏览器绑定，并明确不能假定任意 .NET 库可用。首轮 Chrome 冒烟准确捕获缺失图标造成的 404 控制台错误，补内联图标后自动测试在 360×800 完成 `0 → 3 → 0`、验证禁用/恢复状态、零运行期/HTTP/请求错误与零页面溢出；根 `check:examples` 现串联原 .NET 矩阵、Fable 生产构建和该真实浏览器测试，并完整通过。独立 DevTools 验证语义标题/区域/live output/按钮可访问名称和移动布局；将单页样例声明为 MPA 并补元描述后，移动端 Lighthouse 的可访问性、最佳实践、SEO 与可代理浏览均为 100，控制台零消息。
- **规模：** M。

### B41 — Fable、Elmish 与浏览器应用 / Fable, Elmish, and Browser Applications

- [x] **依赖：** X41b。
- **主要文件（4）：** `docs/{zh,en}/part-07/ch-41-fable-elmish.md`、`docs/{zh,en}/solutions/ch-41-fable-elmish.md`。
- **验收：** 用问题/技术/优势/摩擦结构解释 Fable 与 Elmish；不把 .NET 服务端假设带入浏览器；代表例只承诺已验证的构建。
- **验证：** 正文严格保持 495/495 行，答案严格保持 284/284 行；中英逐段对齐并从 F# 源语言、Fable 编译器、生成 JavaScript 与浏览器运行时四层边界出发，分别回答“F# 能否编译”“Fable 是否支持所用能力”“目标浏览器与打包器是否支持”三个兼容性问题。章节以 X41b 的纯 `Model`/`Message`/`update` 与 DOM 效果壳为唯一已执行切片，明确数值、集合边界、日期/正则、反射/泛型、同步异步、代理和 option 的跨目标差异；再给出共享纯核心、显式 JS 适配器、NuGet/npm 双依赖图、HTTP/安全/存储/无障碍边界，以及纯 DOM、Elmish、React/Feliz/Elmish.React 和服务端岛屿的决策地图。Elmish 部分覆盖命令、订阅、请求身份与取消、URL 所有权和不可能状态，并区分状态架构与渲染器；截至 2026-08-25 的版本表只把 Fable 5.13.0、Fable.Core 5.2.0、Fable.Browser.Dom 2.20.0、Vite 6.4.3 标为仓库验证，把 Fable.Elmish、Fable.Elmish.React、Feliz 与 Fable.React 明示为仅核对官方包资料，且记录 Fable.React 对新项目推荐 Feliz。三道答案分别论证渐进岛屿、工作流状态机和 React 组件约束，设计防抖/取消/陈旧响应模型，并把共享定价核心置于服务端权威与跨目标黄金向量之下。X41b 的 Fable 生产构建与真实 Chrome 冒烟通过；双语、内容和 VitePress 生产构建通过。真实 Chrome 验证英文正文、答案与站内语言切换后的中文答案/正文及两类侧栏；390×844 下无页面级横向溢出，代码和宽表格只在自身内部滚动，控制台零消息，中文正文移动端 Lighthouse 可访问性、最佳实践、SEO 与可代理浏览均为 100。
- **规模：** L。

### X42 — 云、容器与 Aspire 的本地验证切片

- [x] **依赖：** B41。
- **主要文件（≤5）：** `examples/ecosystem/cloud/CloudService.fsproj`、`Program.fs`、`examples/ecosystem/cloud/AppHost/AppHost.csproj`、`Program.cs`、`examples/ecosystem/cloud/README.md`。
- **验收：** 实施时依据官方 .NET 10 支持矩阵锁定 Aspire/容器工具版本；用 C# 基础设施宿主编排 F# 服务且无需云账号，诚实展示生态边界；Serverless 只作带约束的决策分支。
- **验证：** 依据截至 2026-08-25 的官方 Aspire SDK、项目式 AppHost 与 NuGet 资料锁定 `Aspire.AppHost.Sdk` 13.5.2，实际环境为 .NET SDK 10.0.301；F# Web 服务锁定 `FSharp.Core` 10.1.301，C# AppHost 的完整平台编排图锁定 13.5.2，并明确保留项目资产模式、只抑制对应的 `ASPIRE010` CLI bundle 迁移提示。先由清单中的缺失项目/源文件得到预期红灯，再由 F# 泛型响应/可空性和 AppHost 缺少 `http` 端点得到编译与真实启动红灯；最终服务与 AppHost Release 构建均为 0 警告/0 错误。直接运行 F# 服务时，`/health/live`、`/health/ready` 与 `/api/runtime` 分别返回 `healthy`、`ready` 和 `standalone`；C# 编排壳显式声明动态 HTTP 端点、注入 `DEPLOYMENT_MODE=aspire-local` 并注册 `/health/ready`，无需云账号、数据库、容器 daemon 或全局 Aspire CLI。因本机没有受信任开发证书，首次动态 HTTPS 仪表板准确暴露资源服务证书校验失败；改用官方支持、严格绑定 `127.0.0.1` 的仅本地 HTTP 配置后，真实 Chrome 仪表板显示唯一 `cloud-service` 为 `Running`、Health state 为 `Healthy`、对应检查为 `Healthy`，服务端点返回 `aspire-local`，README 同时明确匿名未加密模式不得暴露到局域网或生产。使用 .NET SDK 容器目标和固定 `mcr.microsoft.com/dotnet/aspnet:10.0.11` 成功生成约 89 MB 的本地镜像归档；解析确认 `linux/arm64`、非 root UID 1654、8080 和 `dotnet /app/CloudService.dll`，Docker daemon 未运行所以不声称容器已启动。首个 `--os linux` 命令会把普通锁图污染为 `linux-arm64` 并使解决方案 `--locked-mode` 失败；最终命令不改变项目 RID，发布前后锁文件 SHA-256 一致。全解决方案锁定还原、Release 构建、测试及 Fable 生产构建/Chrome 冒烟组成的 `pnpm check:examples` 完整通过。
- **规模：** L。

### B42 — 云、容器、Serverless 与 .NET Aspire / Cloud, Containers, Serverless, and .NET Aspire

- [x] **依赖：** X42。
- **主要文件（4）：** `docs/{zh,en}/part-07/ch-42-cloud-containers-aspire.md`、`docs/{zh,en}/solutions/ch-42-cloud-containers-aspire.md`。
- **验收：** 以部署问题区分容器、托管服务、Serverless 和 Aspire；注明 F# 项目模板/生态摩擦及 C# 基础设施项目互操作选择。
- **验证：** 中英文正文均为 501 行、解答均为 234 行，覆盖从源码、制品、镜像到平台、实例与可观测发布的完整部署链，并以触发方式、生命周期、状态、伸缩、控制面与运维责任六个问题比较托管进程、托管容器、Kubernetes、Serverless 和 VM；同时纳入配置/机密/身份、进程退出、健康信号、镜像供应链、RID 锁污染、Serverless 重试与幂等、Aspire 两套健康状态、Service Defaults 的可选边界以及发布/观测/回滚证据阶梯。所有易变版本与平台能力均注明截至 2026-08-25，严格区分 X42 已执行证据、官方资料审阅和未执行的平台边界；Azure Functions 的 F# isolated worker 绑定摩擦与 AWS Lambda .NET 10 只作有来源的决策说明。`check-parity`、`check-content` 与 VitePress 生产构建通过；X42 的锁定还原、Release 构建、测试、Fable 构建/Chrome 冒烟和本地 Aspire 证据保持通过。真实 Chrome 从英文正文进入英文解答，经语言菜单切到中文解答再返回中文正文，两套导航、侧栏和链接均正确；390×844 视口无页面横向溢出，宽代码仅在代码框内滚动，控制台零错误；移动端 Lighthouse Accessibility、Best Practices、SEO 与 Agentic Browsing 均为 100。
- **规模：** L。

### X43 — Avalonia 最小桌面样例

- [x] **依赖：** B42。
- **主要文件（≤5）：** `examples/ecosystem/avalonia/AvaloniaSample.fsproj`、`Program.fs`、`App.axaml`、`MainWindow.axaml`、`MainWindow.fs`。
- **验收：** 锁定 Avalonia 版本；最小应用可构建并保持 UI 与纯更新逻辑分离；移动端只说明官方支持边界，不声称已验证未执行的平台。
- **验证：** 先把解决方案与清单指向尚不存在的项目，得到缺少 `AvaloniaSample.fsproj`、`MainWindow.fs` 和 `Program.fs` 的预期红灯；随后按截至 2026-08-25 的官方 F# 模板与平台资料实现恰好 5 个主要文件，目标为 .NET 10，并把 `Avalonia`、`Avalonia.Desktop` 与 `Avalonia.Themes.Fluent` 精确锁定到当前稳定版 12.1.1。`Counter.update` 是不接触控件的纯函数，窗口只负责把 `AddSeat`、`RemoveSeat`、`Reset` 消息派发给它并渲染状态；聚焦 xUnit 测试为 1/1，全 `ExampleTests` 为 68/68，Release 构建为 0 警告/0 错误，Fantomas 检查通过。全解决方案 locked-mode 还原、Release 构建、测试及 Fable 生产构建/Chrome 冒烟组成的 `pnpm check:examples` 完整通过。真实 macOS 启动已尝试，但当前自动化会话无法提供可用的图形显示上下文，Avalonia.Native 在创建 CoreVideo RenderTimer 时以原生代码 `-6661` 退出；因此不声称窗口运行通过，也不把该环境限制伪装成应用修复。样例仅是已构建的桌面目标；官方支持的 iOS/Android 需要独立平台项目、.NET 10 移动工作负载、签名和设备/模拟器验证，本任务未创建也未声称验证这些目标。
- **规模：** L。

### B43 — Avalonia、桌面与移动端 / Avalonia, Desktop, and Mobile

- [x] **依赖：** X43。
- **主要文件（4）：** `docs/{zh,en}/part-07/ch-43-avalonia-desktop-mobile.md`、`docs/{zh,en}/solutions/ch-43-avalonia-desktop-mobile.md`。
- **验收：** 提供桌面/移动决策地图，准确区分共享 .NET 能力、平台工具链和 F# 模板/绑定摩擦；不把桌面构建等同全平台验证。
- **验证：** 中英文正文严格保持 501/501 行、解答严格保持 300/300 行；章节从领域/持久数据、纯展示状态转换、UI 工具包、平台宿主、签名安装更新五层契约出发，区分共享逻辑、共享 UI 与共享验证证据，并比较 Avalonia、WPF/WinUI、.NET MAUI、Fable/Web 和薄原生壳。X43 的 `Counter.update`、AXAML 与桌面宿主构成唯一已编译切片；正文同时覆盖手写 MVU、MVVM 适配、code-behind、code-only/FuncUI、效果消息、编译绑定与 F# 类型适配、UI 线程/取消/陈旧结果、平台服务端口、Windows/macOS/Linux 后端、Android/iOS 项目图与生命周期、无障碍、打包、运行时交付、更新回退和验证阶梯。所有易变能力注明截至 2026-08-25，并明确 Avalonia 12.1.1/.NET 10 的仓库证据、官方资料审阅与未执行移动端/Player 证据三者边界；真实 macOS 启动的 CoreVideo RenderTimer `-6661` 环境限制也原样记录，不把桌面构建冒充窗口或全平台验证。三道双语答案分别完成既有 WPF、跨平台桌面和移动产品的边界选择，设计桌面发布切片与 Core/Desktop/Android/iOS 项目图，并给出可推翻的证据矩阵和撤回条件。双语、内容检查和 VitePress 生产构建通过；真实 Chrome 验证英文正文到解答、真实语言菜单切换中文解答并返回中文正文，两套侧栏和链接均正确；390×844 下无页面级横向溢出，宽表格与代码块只在自身容器内滚动，控制台零警告/错误，移动端 Lighthouse 的可访问性、最佳实践、SEO 与可代理浏览均为 100。
- **规模：** L。

### X44 — Unity 6.3 LTS F# 插件与 C# 适配层

- [x] **依赖：** B43。
- **主要文件（≤5）：** `examples/ecosystem/unity/FSharpGameplay/FSharpGameplay.fsproj`、`Gameplay.fs`、`UnityAdapter.cs`、`link.xml`、`README.md`。
- **验收：** F# 库目标 `netstandard2.1`；记录并验证 `FSharp.Core` 装配；C# 薄层隔离 Unity 序列化/API；文档区分编译、编辑器导入、IL2CPP/裁剪和 Player 构建。
- **验证：** 先将解决方案、清单和测试指向尚不存在的 Unity 插件，得到项目、F# 源、C# 适配器和 README 五项预期红灯；随后依据截至 2026-08-25 的 Unity 6.3 LTS、.NET profile、托管插件、序列化、IL2CPP、UnityLinker 与 F# 组件设计官方资料实现恰好 5 个主要文件。`FSharpGameplay` 目标为 `netstandard2.1`，显式把 F# SDK 的隐式 `FSharp.Core` 更新并锁到与本仓库 .NET SDK 10.0.301 对应的包 10.1.301；首次误用第二个 `Include` 触发 NU1504 重复引用红灯，最终用 `Update` 保留唯一依赖。构建后的 8,704 字节 `FSharpGameplay.dll` 声明 `FSharp.Core, Version=10.1.0.0`，2,407,760 字节的精确 `FSharp.Core.dll` 被复制到同目录，项目的后置目标会在任一文件缺失时失败。纯 `Gameplay.Step` 不引用 UnityEngine，输入夹紧、位置/速度转换与非有限值防线均在 F#；公开签名只暴露普通 CLR 值类型、属性和静态方法，聚焦测试还通过反射拒绝 `Microsoft.FSharp.*` 类型泄漏。性能复核先用 `IsValueType` 回归测试准确抓到每次 FixedUpdate 分配一个状态类的问题，再把 `MotionState` 改为私有字段、内部构造器、公开只读属性的显式 F# struct，保持 C# 读取面且消除该类分配。`UnityAdapter.cs` 只保留 `MonoBehaviour`、`SerializeField`、生命周期、Time 与 Transform，并用 `SetHorizontal(float)` 避免绑定某一输入包；该文件准确登记为必须在 Unity 中编译的说明性适配器，而不是用伪 UnityEngine 声称宿主验证。窄 `link.xml` 的 XML 格式检查通过且不粗暴保留整个 FSharp.Core。locked-mode 解决方案还原、Release 全编译均为 0 警告/0 错误，Unity 聚焦测试 1/1、ExampleTests 69/69、Fantomas 和 `pnpm check:examples`（含 Fable 生产构建及 Chrome 冒烟）均通过。目标人工矩阵锁定 Unity 6000.3.22f1；本机实际不存在 `/Applications/Unity/Hub/Editor`，因此插件导入、C# Play Mode 与 macOS ARM64 IL2CPP Player 构建/启动在 README 中逐项记为“未运行—Editor/模块缺失”，没有把 .NET 编译改写成 Unity 证据。
- **规模：** L。

### B44 — Unity 6.3 LTS 与 F# / Unity 6.3 LTS and F#

- [x] **依赖：** X44。
- **主要文件（4）：** `docs/{zh,en}/part-07/ch-44-unity.md`、`docs/{zh,en}/solutions/ch-44-unity.md`。
- **验收：** 准确解释兼容目标、`FSharp.Core`、C# 适配、序列化、IL2CPP 和裁剪；不把 F# 类库成功编译误报为 Unity 全链成功。
- **验证：** 中英文正文严格保持 592/592 行、解答严格保持 303/303 行；章节以“F# 编译 → 依赖闭包 → Unity 导入 → Editor/Play Mode → Mono Player → IL2CPP Player → 受支持发布”的证据阶梯为主线，分别说明 .NET Standard 2.1 API profile、精确 `FSharp.Core` 装配、C# 友好 CLR 表面、Unity 序列化与重载生命周期、输入/时间/线程/逐帧分配、IL2CPP 五阶段流水线、AOT 风险、窄 `link.xml` 根、Burst/Jobs 的独立 HPC# 边界，以及可复现 Player 构建。X44 是唯一已执行样例：锁定 F# 插件构建、纯规则、值类型状态、依赖产物和公开 API 检查均通过；说明性 C# `MonoBehaviour`、Unity 6000.3.22f1 导入、Play Mode 与 macOS ARM64 IL2CPP Player 明确记录为未运行，因为本机没有 Unity Editor/模块，未把普通 .NET 构建冒充 Unity 结果。三道双语答案分别为战术游戏、Burst 主导动作游戏与 Editor 内容流水线选择不同语言边界，设计从 X44 升级到真实 IL2CPP 垂直切片的项目/证据图，并处理任务存档迁移、异步陈旧结果和 AOT 安全处理器注册。`check-parity`、`check-content` 与 VitePress 生产构建通过；真实 Chrome 验证英文正文/解答、语言菜单切换到中文解答及返回中文正文，两套侧栏与互链正确；390×844 下无页面级横向溢出，宽表格与代码块只在自身容器内滚动，控制台零警告/错误，移动端 Lighthouse 的可访问性、最佳实践、SEO 与可代理浏览均为 100。
- **规模：** L。

### B45 — 脚本、自动化、包生态与继续学习 / Scripting, Automation, Packages, and What Comes Next

- [x] **依赖：** B44。
- **主要文件（5）：** `docs/{zh,en}/part-07/ch-45-scripting-packages-next.md`、`docs/{zh,en}/solutions/ch-45-scripting-packages-next.md`、`examples/scripts/ch45-scripting-packages-next.fsx`。
- **验收：** 展示一个真实本地自动化脚本、包选择/锁定原则和继续学习地图；quotations、SRTP、灵活类型、byref/Span 只做识别并链接 A08。
- **验证：** 先在示例 manifest 登记尚不存在的 `ch45-scripting-packages-next.fsx`，得到精确缺文件红灯；首版脚本又因 .NET 现代 `Path.GetDirectoryName` 的 `string`/`ReadOnlySpan<char>` 重载无法唯一推断而真实编译失败，补清边界类型并消除保留字警告后通过 Fantomas。最终 X45 只用 BCL 建立版本 1 SHA-256 JSON 产物清单：解析完整路径、拒绝链接根、递归跳过 reparse point、排除输出自身、统一 `/`、ordinal 排序、固定 JSON 属性/UTF-8/换行，并先生成完整期望文本；`write` 仅在字节不同后以同目录临时文件替换，`check` 只读且用退出码区分 stale 与故障。无参数 FSI 在唯一临时夹具上严格观察首次 `Updated`、第二次 `Unchanged`、只读 `Current`、2 个规范化路径、哨兵时间戳未变化和最终清理；统一示例门完整通过。另用真实 CLI 参数对 23 个脚本文件执行 `write → check → write`，依次得到 updated/current/unchanged，随后删除任务专属临时目录。中英文正文严格保持 499/499 行、解答严格保持 280/280 行，覆盖 REPL/脚本/项目/本地工具/FAKE 的升级判断、FSI 参数与指令、确定性和幂等自动化、路径/进程/安全边界、包适配评估、`#r "nuget:"` 与完整锁图的差异、PackageReference locked mode、源映射/审计、Paket/FAKE 分工及项目式继续学习地图；高级 quotations、SRTP、灵活类型、byref/Span 只作识别并链接 167/167 行的附录 H 评审版。官方当前 SRTP 资料还揭示并修正了第 11 章旧式“只看 `^T`”表述：F# 7+ 常用简化语法可用 `'T`，应由 `inline` 加成员约束共同识别。解答以截至 2026-08-25 的 NuGet 官方页比较 Argu 6.2.5、System.CommandLine 2.0.11 与手写解析，并明确这些是资料审阅而非仓库运行证据。双语、内容与 VitePress 生产构建通过；真实 Chrome 走通英文正文、附录 H、英文解答、语言菜单中文解答及返回中文正文，两套第 45 章侧栏和互链正确；390×844 无页面级横向溢出，宽表格与代码只在自身容器滚动，控制台零警告/错误，移动端 Lighthouse 的可访问性、最佳实践、SEO 与可代理浏览均为 100。
- **规模：** M。

### C7 — 生态地图检查点

- [x] **依赖：** B45。
- **验收：** 每章均含问题、选择、优势、摩擦、代表例和继续学习；版本、官方来源、自动/人工证据和未验证边界清楚；无专有账号依赖。
- **验证：** 逐章审查中英文第 39–45 章：每章都从问题/边界出发，给出候选选择、强项或适用条件、摩擦/取舍、唯一已验证切片、有界采用试验与后续章节/项目学习路径；所有易变版本和官方来源均带 2026-08-25 审阅日，表格与正文把仓库已执行、官方资料审阅和未验证平台明确分开。聚焦验证分别重跑：X39 Release 构建 0 警告/0 错误且 `WebSampleTests` 7/7；X40 同样无警告且 `DataSampleTests` 2/2；X41a/b 重新锁定还原，Fable 5.13.0 编译、Vite 6.4.3 转换 15 个模块，真实 Chrome 完成 `0 → 3 → 0` 且无运行错误/溢出；X42 的 F# 服务与 C# AppHost 均 0 警告/0 错误；X43 Avalonia 构建无警告且纯转换测试 1/1；X44 Unity 插件构建无警告且 CLR 边界/值类型测试 1/1。生态样例源码只使用本地夹具、公开包和回环/本机边界，无云、数据库或专有账号才能通过的验收项。Unity 目录现场检查再次证实 `/Applications/Unity/Hub/Editor` 不存在；README 的导入、Play Mode 和 IL2CPP Player 三项均是带精确目标的 `Not run — Editor/module absent`，而非占位通过。该复核还发现 struct 优化后插件实际为 8,704 字节，已同步更正第 44 章中英文和 X44 旧的 8,192 字节记录。最终 `pnpm test` 全量通过：22 项内容工具测试、双语一致性、内容契约、全部样例与 Fable Chrome 冒烟、VitePress 生产构建均为绿色。

## 8. 前言、附录与全站装配

### A01 — 附录 A：跨平台环境配置

- [x] **依赖：** C7。
- **主要文件（2）：** `docs/zh/appendices/a-setup.md`、`docs/en/appendices/a-setup.md`。
- **验收：** Windows/macOS/Linux 安装、版本检查、编辑器和故障排查准确；区分必需工具与可选工具。
- **验证：** 中英文严格保持 279/279 行与同锨结构，把“只阅读静态书”、“运行核心 F#”、“维护本仓库”与“面向移动/Unity/云平台”拆成不同工具合同，明确 SDK 已包含 F# Interactive 和对应运行时，核心学习不需额外 workload、云账号或特定编辑器。Windows 覆盖官方安装器/WinGet、架构与 Visual Studio 边界；macOS 覆盖 Arm64/x64、安装根冲突及 Visual Studio for Mac 退役；Linux 按发行版分流，只把 `dotnet-sdk-10.0` 写为 Ubuntu 条件示例。还区分 VS Code/Ionide、Windows Visual Studio 与商业 Rider，并以命令发现、SDK 选择、架构、锁定还原、编辑器状态、workload/证书六层故障树收尾。安装与工具资料全部来自 Microsoft 或 JetBrains 官方页并带 2026-08-25 复核日。当前 macOS 26.3 Arm64 逐命令验证 `dotnet --version/--list-sdks/--info`、FSI help、Git、Node 与 pnpm；实际选中 SDK 10.0.301/F# 10，无额外 workload，且全新临时 `dotnet new console --language F#` 项目真实运行并输出 `Hello from F#`，随后清理。Windows/Linux 命令与 Visual Studio/Rider 只标记官方资料审阅，未伪称本机执行。双语、内容与 VitePress 生产构建通过。
- **规模：** M。

### A02 — 附录 B：语法与运算符速查

- [x] **依赖：** A01。
- **主要文件（2）：** `docs/zh/appendices/b-syntax-reference.md`、`docs/en/appendices/b-syntax-reference.md`。
- **验收：** 只汇总正文已解释语法；优先类型和小例，不把速查表变成第二本语言规范。
- **验证：** 中英文严格保持 322/322 行与同键结构，以“从外向内读类型”和 `List.fold` 签名拆读开场，再索引绑定/表达式、函数应用、分支/模式、记录/联合、集合表层、计算表达式、.NET 互操作、运算符/优先级和文件级声明；特别区分柯里化与元组参数、模式中小写名的“绑定”而非比较、`=` 与 `<-`、管道与组合、`unit` 与缺失、builder 赋予 `let!`/`return` 的语义，并建议在可能有两种读法时用括号或中间名称，而非背完整规范。四组完整 F# 代码只通过 VitePress region 引用清单中已执行的 `ch02`、`ch03`、`ch04`、`ch08` 源，没有复制出独立未测版本。官方 F# 类型、推断、函数、模式、联合、计算表达式、符号/优先级与格式指南全部带 2026-08-25 复核日。完整示例门、双语/内容检查与 VitePress 生产构建通过。
- **规模：** M。

### A03 — 附录 C：集合选择与复杂度

- [x] **依赖：** A02、B14、B26。
- **主要文件（2）：** `docs/zh/appendices/c-collections.md`、`docs/en/appendices/c-collections.md`。
- **验收：** 汇总集合求值、更新、查找、顺序、键约束和典型复杂度；复杂度声明注明条件且与官方实现文档一致。
- **验证：** 中英文严格保持 276/276 行与同键结构，先按产生时机、更新所有权、访问形状、可观察顺序和键身份五份契约区分 list、array、`seq`、`ResizeArray`、Map/Set、Dictionary/HashSet。逐行复核 Microsoft F# 集合指南、FSharp.Core List/Seq/Map/Set API 与 .NET 10 集合复杂度/API：列表前置 O(1)、索引 O(k)、连接 O(左长)；数组索引/长度 O(1)；`List<T>.Add` 均摊 O(1)/扩容最坏 O(n)；有限序列完整消费 O(n) 但生产器和重复枚举另计；Map/Set 查找/更新 O(log n)、普通构建 O(n log n)、当前文档中 `count` O(n)；哈希查找/添加只在稳定且分布良好的相等/哈希契约下期望/均摊 O(1)，最坏 O(n)。顺序表明 List/Array 保持索引/源顺序，Map/Set 按 F# 泛型比较，HashSet 无特定顺序，且不把当前 Dictionary 枚举行为当作领域 API 承诺。键表区分 Map/Set 的编译期 `comparison` 约束与 Dictionary/HashSet 的运行期 `IEqualityComparer`/稳定哈希责任。三个完整代码区只引用已执行的第 14 章源，实际展示重复枚举、比较有序集合与仅相等哈希键。完整示例门、双语/内容检查与 VitePress 生产构建通过。
- **规模：** M。

### A04 — 附录 D：C# 到 F# 迁移与互操作表

- [x] **依赖：** A03、B27。
- **主要文件（2）：** `docs/zh/appendices/d-csharp-migration.md`、`docs/en/appendices/d-csharp-migration.md`。
- **验收：** 对比值/表达式、数据建模、失败、异步、集合和 API 边界；避免“一一替换语法”的误导。
- **验证：** 中英文严格保持 232/232 行与同锚点结构，先用“熟悉的 C# 形式 / 可能的 F# 形式 / 真正决定选择的问题”拆除逐句转写假设，再分别按值与表达式、乘积/选择/身份、可恢复动作驱动的失败分类、`Async`/`Task` 启动与取消语义、集合求值/顺序/所有权以及 F# 专用与广泛 .NET 公共表层作决策。迁移流程以契约盘点、消费者行为冻结、窄接缝、地道 F# 核心、兼容适配器、真实调用者编译、证据扩展与有版本退役为序，并明确混合 F#/C# 可以是稳定终点。官方 F# 组件、null/nullable、async/task 与 .NET 集合/兼容性资料均带 2026-08-25 复核日。三段完整源码直接引用 E27 已执行区域；重新以 Release、`--no-restore` 构建 F# 库和 C# 客户端，结果 0 警告/0 错误，实际运行同时验证 accepted/rejected/invalid、三类参数守卫、只导出 4 个普通 CLR 类型、无 `Microsoft.FSharp.*` 公共签名、nullability 元数据与 XML 文档。双语、内容检查和 VitePress 生产构建通过。
- **规模：** M。

### A05 — 附录 E：常见编译错误索引

- [x] **依赖：** A04、E30。
- **主要文件（2）：** `docs/zh/appendices/e-compiler-errors.md`、`docs/en/appendices/e-compiler-errors.md`。
- **验收：** 按实际诊断编号与根因索引值限制、文件顺序、类型不匹配、非穷尽等；所有诊断来自锁定编译器运行。
- **验证：** 中英文严格保持 207/207 行与同锚点结构，以“编号是类别而非唯一修复”为入口，先给第一条相关消息、真实命令、两侧类型、级联消除和保留环境的六步分诊法，再索引 `FS0001`、`FS0010`、`FS0025`、`FS0027`、`FS0030`、`FS0039`、`FS0041`、`FS0058`、`FS0072`、`FS0748`、`FS0764`、`FS0800` 与 `FS3261`。每项都区分立即证据、首个根因问题和语义修复方向，特别把值限制的共享值/泛型变换/新值工厂，名称解析的拼写/作用域/引用/生成/文件顺序，及 nullable 的无效 null/真实 CLR null/验证后领域缺失拆开。全部编号在 SDK 10.0.301/F# 10 下以 `--warnaserror+ --checknulls+` 的一两行临时探针或仓库夹具实际复现；探针揭示未完成绑定当前报告 FS0058，而意外 `)` 报告 FS0010，随后已清理。三个完整负例只引用 E30 已登记源码；完整 `pnpm check:examples` 在锁定还原后验证 FS0030、FS0039、FS0800，并通过全部解决方案构建/测试、脚本与 Fable Chrome 冒烟。首次受限运行的还原达到五分钟上限后，以 1 秒只读堆栈确认工作线程阻塞于网络 `connect`，在获批网络环境重跑原命令即通过，未放宽超时或检查。官方编译器消息/选项、格式、泛化、签名与 null 资料均带 2026-08-25 复核日；双语、内容和 VitePress 生产构建通过。
- **规模：** M。

### A06 — 附录 F：中英文术语表

- [x] **依赖：** A05、F04。
- **主要文件（2）：** `docs/zh/glossary.md`、`docs/en/glossary.md`。
- **验收：** 从 `terminology.json` 生成或验证一致的术语、定义、首次出现和交叉链接；两种语言均可独立理解。
- **验证：** `scripts/generate-glossary.mjs` 以 `docs/terminology.json` 为唯一显示词与定义源，按章节阅读顺序从两种语言 frontmatter 分别求出最早声明页，并要求每个键都有中英文首现、两页 `translationKey`/部分/章号相同；任何孤立键、跨语言首现漂移、缺失页或生成差异均失败。生成的中英文页严格保持 533/533 行、84/84 个同名稳定锚点和三个学习阶段分组；每项以本语言完整定义为主体，同时显示另一语言首选词、稳定键和真实首教章节链接，所以只会中文或只会英文都可独立使用。首现定义明确为“阅读顺序中最早声明该键的教学章节”，不假装普通叙述此前绝未出现该词。新增 `generate:glossary`/`check:glossary`，并把后者接入 `check:content`；三项生成器测试覆盖双语渲染与锚点/链接、缺失/过期检测以及无首现术语拒绝，完整内容测试 25/25 通过。`pnpm check:glossary`、双语、内容和 VitePress 生产构建全部通过；安装期意外中断造成的 pnpm 状态缓存/链接不一致已只通过删除生成状态并以 `CI=true pnpm install --frozen-lockfile` 从既有锁文件恢复，未修改依赖或锁文件。
- **规模：** M。

### A07 — 附录 G：答案与开放题评审标准

- [x] **依赖：** A06、B45。
- **主要文件（2）：** `docs/zh/appendices/g-solutions-guide.md`、`docs/en/appendices/g-solutions-guide.md`。
- **验收：** 索引 45 章答案；开放设计题给约束、评审维度和可接受变体，不伪造唯一答案。
- **验证：** 新增确定性生成器 `scripts/generate-solutions-guide.mjs`，从两种语言的章节与答案 frontmatter 生成严格对称的 452/452 行指南，并按七个部分索引 45/45 章与 135/135 个练习答案深链接。生成前同时验证章号与部分、slug、`exerciseIds` 次序、中英文元数据、章节到答案及答案回章节的双向链接，以及每个精确答案锚点恰好出现一次且没有孤立练习锚点；缺页、陈旧输出、编号漂移或额外答案都会失败。指南先规定“作答—比较—修订”的使用方式，再区分封闭题、诊断题与开放设计题；开放题按契约、模型、副作用、API、证据与清晰度六个维度评审，明确可接受变体和硬失败，不把示例解答冒充唯一答案，也不把设计文字冒充可执行证据。新增 `generate:solutions-guide`/`check:solutions-guide` 并接入 `check:content`；四项生成器测试及完整内容测试 29/29 通过，双语、内容与 VitePress 生产构建全部通过。
- **规模：** M。

### A08 — 附录 H：高级特性识别索引

- [x] **依赖：** A07、B31、B40、B45。
- **主要文件（2）：** `docs/zh/appendices/h-advanced-index.md`、`docs/en/appendices/h-advanced-index.md`。
- **验收：** quotations、SRTP、灵活类型、byref/Span 各说明识别信号、适用边界与官方入口；明确首版不教类型提供器创作或编译器服务。
- **验证：** 中英文严格保持 167/167 行与同锚点结构，以四列表先给 quotations、SRTP、灵活类型与 byref/Span 的表面信号、核心含义和第一个停止问题，再分别说明识别组合、真正适用边界、常见误用、评审问题及正文回链。2026-08-25 逐项复核 Microsoft Learn 的 F# quotations、SRTP、灵活类型、byrefs，以及 .NET Memory/Span 生命周期指南和 FSharp.Core quotation API：明确 typed/untyped quotation 是表达式对象而非自动执行；F# 7+ SRTP 简化语法可显示 `'T`，旧式/复杂分派仍可能出现 `^T`；`#BaseType` 等价于带子类型约束的新泛型参数且主要服务嵌套类型位置；`inref` 只限制当前引用持有者，Span/byref-like 值受逸出和捕获限制，异步/长期所有权应转向 `Memory<T>` 等合适表示。范围边界明确不教授类型提供器创作、FSharp.Compiler.Service、通用 quotation 求值器、高级 SRTP 分派和自定义 byref-like 类型；它只是一份识别与路由索引。双语、内容与 VitePress 生产构建通过。
- **规模：** M。

### S01 — 双语前言、阅读路线与中立首页

- [x] **依赖：** C7。
- **主要文件（5）：** `docs/index.md`、`docs/zh/index.md`、`docs/en/index.md`、`docs/zh/preface/index.md`、`docs/en/preface/index.md`。
- **验收：** 说明适合/不适合读者、三条路线、运行示例、类型签名阅读、F# 10/.NET 10 范围；根页提供等权语言入口。
- **验证：** 新增 141/141 行、同结构且可单语独立阅读的中英文前言，明确适合/不适合读者与“从 F# 本身思考”的学习合同；快速入门、系统学习和 C#/.NET 转向三条路线各有顺序与继续前进的可观察证据。章节循环要求先读类型与预测，再运行最小证据、独立练习、比较契约；示例入口区分纯静态阅读、单个 FSI 脚本、集成切片、全量门禁和预期失败夹具。类型签名表覆盖箭头右结合、柯里化与元组、泛型、`option`、`Result` 和 `Task` 的最小阅读规则。版本说明把 F# 10、通常的 `net10.0`、SDK 10.0.301 复现基线、`latestPatch` 同特征带选择、生产安全补丁与 Unity 的 `netstandard2.1` 例外分开。62/62 行语言首页新增前言入口；33 行根页保留同权中英文入口、说明两版各自完整，并为双语标题补充不会连读的可访问名称。官方 F# 入门/F# 10、.NET 10 下载与 `global.json` 资料于 2026-08-25 复核。双语、内容与 VitePress 生产构建通过；真实 Chrome 首次访问保持在中立根页，没有自动语言偏置，两张语言卡分别进入 `/zh/` 与 `/en/`，再可进入对应前言，控制台无警告或错误。
- **规模：** M。

### S02 — 完整目录、侧栏与同页语言映射

- [x] **依赖：** A08、S01。
- **主要文件（≤5）：** `docs/.vitepress/config/index.ts`、`zh.ts`、`en.ts`、`scripts/generate-navigation.mjs`、`package.json`。
- **验收：** 45 章、前言、8 附录、术语和答案进入对称导航；前后章顺序正确；语言切换始终映射相同 `translationKey`。
- **验证：** 新增 `scripts/generate-navigation.mjs`，以 100 对完整双语页面的已验证 frontmatter 为唯一导航源，生成复用共享侧栏常量的 `navigation.generated.ts`；手写中英文配置只保留本地化 UI 文本并导入生成结果。生成器要求 1 个首页、1 个前言、45 章、45 个答案、A–E/G/H 七个普通附录与作为附录 F 的术语表全部为 `complete`，章号严格连续且落在规定七部分，章节/答案 slug 相同；任何缺翻译、`translationKey`/kind 漂移、孤立页、重复/缺失章节或附录都会失败。主书侧栏按首页→前言→第 1–45 章→附录 A–H 排序并跨部分连续；答案侧栏含评审指南和 45 个答案；顶部参考菜单含完整 A–H。新增 `generate:navigation`/`check:navigation` 并接入内容门禁，四项导航测试及完整内容测试 33/33 通过；双语、内容与 VitePress 生产构建通过。真实 Chrome 对七部分各首尾共 14 页逐页比较前后链接，14/14 正确，包含 6→7、12→13、18→19、24→25、32→33、38→39 与 45→附录 A；深页 `/en/part-01/ch-06-recursion-folds` 的语言菜单精确指向中文同章，答案侧栏实际呈现评审指南加 45 章共 46 个链接。
- **规模：** L。

### S03 — 本地搜索、答案索引与交互可用性

- [x] **依赖：** S02、A06、A07。
- **主要文件（≤5）：** `docs/.vitepress/config/{zh,en}.ts`、`docs/.vitepress/theme/index.ts`、`styles.css`、`scripts/site-smoke.mjs`。
- **验收：** 本地搜索中英 UI 文本正确并可找到术语/章节；代码复制、键盘焦点、移动导航和答案返回链接可用。
- **验证：** 启用 VitePress 1.6.4 离线搜索，在中英 locale 中分别提供按钮、对话框和键盘提示文案。生产构建首次揭示 `permalink: false` 会让默认分段器生成 0 文档索引；已保留搜索所需的不可见标题锚点，并用自定义 permalink 去掉辅助名称中的 `{#id}`、按页面语言生成中英文案。新增单元测试后内容测试 36/36 通过；`pnpm build` 通过。`node scripts/site-smoke.mjs` 从静态产物证明 201 个书页、202 个 HTML、17,270 个去重内部链接及锚点全部可达，中英索引各含 1,757 个分段，`discriminated unions`/“可辨识联合”与 `statically resolved type parameters`/“静态解析类型参数”四个查询均命中预期章节或术语入口。真实 Chrome 中中英搜索结果可打开，答案“返回本章”到达同章；复制控件写入与代码块完全一致的文本并显示“已复制”。键盘首个焦点是可见的“跳到正文”链接，3px 焦点环可见；360×800 视口无页面级横向溢出，代码块独立滚动，移动导航与全书目录可展开。主导航、全书目录、翻页、移动导航和分组开关的辅助名称已按 locale 本地化；控制台无警告/错误，页面及资源请求全部 200。
- **规模：** M。

### S04 — 贡献、版本矩阵与人工验证模板

- [x] **依赖：** S03。
- **主要文件（≤5）：** `README.md`、`CONTRIBUTING.md`、`docs/version-matrix.md`、`reviews/review-template.md`、`reviews/unity-validation.md`。
- **验收：** 从零运行、内容编辑、双语规则、来源复核、版本升级和人工检查步骤可复现；Unity 记录只填真实结果，未执行状态明确。
- **验证：** 新增五份各自可由单语读者使用的双语文档：README 给出从零冻结安装、日常编辑、完整发布命令和仓库地图；贡献指南把中英独立完整、frontmatter/锚点/示例合同、F# 优先教学、引用、版本与安全边界写成可执行规则；版本矩阵严格区分锁定基线、最低要求、2026-08-25 实测环境和人工目标，并给出升级、全量验证与成对回退步骤；审阅模板要求所有项目显式使用 `passed`、`failed`、`not run` 或 `not applicable`，记录范围、抽样、证据、严重度和残余风险。Unity 记录中 .NET Release 构建、纯逻辑/API 测试和相邻 DLL 检查来自实际退出码 0 的示例门禁；由于本机不存在 `/Applications/Unity/Hub/Editor`，Editor 导入、C# adapter、Play Mode、macOS ARM64 IL2CPP Player 构建与启动五项保持 `not run`，并附精确人工复现协议。按 README 实际执行 `pnpm install --frozen-lockfile`（pnpm 11.7.0，锁文件未变）、`dotnet tool restore`、`env CI=true pnpm test`、`dotnet fantomas . --check` 与 `pnpm check:capstone`；完整测试通过 36/36 内容单测、双语/内容/示例/Fable 门禁和生产构建，capstone 的放置、重放、确认、读取与无密钥诊断通过。Fantomas 首次发现的三个既有格式问题已在 `84bb138` 机械修复，随后全仓检查通过。最终站点冒烟覆盖 201 个书页、203 个 HTML、17,287 个内部链接/锚点、中英各 1,757 个搜索分段及四个代表查询；仓库文档 11 个相对链接目标通过。`pnpm preview --host 127.0.0.1 --port 4173` 启动后，根页、`/zh/`、`/en/` 和 `/version-matrix` 均返回 HTTP 200，随后关闭服务。
- **规模：** M。

### C8 — 全站装配检查点

- [x] **依赖：** S04、A08。
- **验收：** 前言、45 章、答案、8 附录、术语、目录、搜索和语言切换完整；无草稿/占位/孤儿页；桌面和移动阅读闭环。
- **验证：** 全量装配清点证明中英各 100 页，均为 `complete` 且全部出现在确定性生成导航中：每种语言包含首页、前言、45 章、45 份答案及包含术语表 F 在内的 8 个附录，孤儿页为 0；内容门禁同时拒绝草稿、占位、断链、重复锚点和陈旧生成物。`env CI=true pnpm test` 在当前格式化源码上通过 37/37 内容单测、双语与内容契约、锁定 Release 示例/预期诊断、Fable 构建与真实浏览器冒烟，以及 VitePress 生产构建。站点冒烟逐页检查 201 个书页、203 个 HTML 与 17,287 个内部链接/锚点，确认 1,757/1,757 个中英搜索分段覆盖所有书页。代表搜索扩为 10 组永久双语契约、共 20 个查询：部分应用、可辨识联合、活动模式、计算表达式、取消/超时、性质测试、幂等冲突、类型提供器、Unity/IL2CPP 与静态解析类型参数，覆盖七部分及附录 H，全部命中预期页面。真实 Chrome 从七部分分别抽取第 3、9、16、22、28、35、42 章，逐次从英文页点击语言菜单，7/7 到达相同 slug 的中文页，`lang` 从 `en` 变为 `zh-Hans` 且 H1 章号正确；中文搜索 UI 输入“性质测试”显示并打开第 29 章。360×800 移动视口的 document width 为 360，无页面级横向溢出，抽查页 10 个长代码块均在块内滚动；“移动导航”和“目录”可展开，侧栏出现可见书页链接。连续浏览与搜索期间控制台无 warning/error/issue，89 个网络请求全部为 200/304。

## 9. 发布审计

### R01 — 官方来源、版本与链接审计

- [x] **依赖：** C8。
- **主要文件（1）：** `reviews/source-and-version-audit.md`；修复回派原任务。
- **验收：** 所有非平凡语言/.NET/生态事实有一手来源；版本与复核日期一致；无“最新版”等漂移表达；内部/外部链接有效。
- **验证：** 对中英 200 个 locale 页面全量解析得到 196 个有来源页面、1,186 条成对来源记录和 0 个“有来源但无 `verifiedWith`”页面；590/596 条来源日期分别为 2026-08-24/25，全部有效。一次性 Node 外链审计扫描 205 个 Markdown、1,920 次 URL 出现和 306 个唯一 HTTPS 目标：修复 Unity 未固定版本测试页的真实 404 后，299 个直接成功；7 个 GitHub 高并发超时目标逐页二次复核均存在，没有把超时静默算作通过。版本声明与 `global.json`、工具清单、项目文件、24 份锁文件及官方包元数据一致，`dotnet restore ThinkingInFSharp.slnx --locked-mode` 退出 0；无界版本词扫描只剩业务状态、反例、升级警告或上游稳定 URL 路径。`pnpm check:parity`、`pnpm check:content`、VitePress 生产构建与站点冒烟通过，后者覆盖 201 个书页、203 个 HTML、17,287 个内部链接/锚点、1,757/1,757 个搜索分段和 20 个双语查询。审计记录为 `reviews/source-and-version-audit.md`；一个中等断链和一个低优先级元数据问题均已在原双语页面修复，开放高/中/低发现均为 0。
- **规模：** L。

### R02 — 无上下文 F# 专家正确性审阅

- [x] **依赖：** C8。
- **主要文件（1）：** `reviews/fsharp-expert-review.md`；修复回派原任务。
- **验收：** 新专家不依赖规划上下文审阅语言语义、惯用性、null、相等/比较、CE、异步/取消、互操作和项目；所有高/中问题闭环。
- **验证：** 无上下文专家 Bacon 初审得到 1 个高、2 个中、1 个低问题：RequestId 可产生不可回读 `Location`、catch-all 把程序缺陷伪装为可重试 503、Unity struct 测试自身装箱，以及正文固定测试总数漂移。`6d273c3` 用 URI unreserved/长度/点段领域契约、可保留 `InnerException` 的类型化依赖故障、精确取消分类和 `OpCodes.Box` IL 回归关闭前三项；`a43ed08` 从读者正文移除易漂移总数并保留聚焦 1/1。领域/API/适配器/一致性/Unity 聚焦测试分别 9/9、24/24、6/6、7/7、3/3，完整 ExampleTests 与 ContractTests 均 70/70；capstone、Fantomas、双语/内容门和 38/38 内容测试通过。同一专家两轮复核后给出最终 PASS，4 项全部 CLOSED，开放高/中/低均为 0；完整记录见 `reviews/fsharp-expert-review.md`。
- **规模：** L。

### R03 — 中英文独立读者与语义对等审阅

- [ ] **依赖：** C8。
- **主要文件（≤2）：** `reviews/zh-reader-review.md`、`reviews/en-reader-review.md`；修复回派原任务。
- **验收：** 仅中文或仅英文读者不依赖另一语言即可学习；概念深度、示例、练习、答案与限制语义一致；表达自然。
- **验证：** `pnpm check:parity`；逐项关闭抽查发现；复核三条阅读路线。
- **规模：** L。

### R04 — 代码、契约、格式与确定性审计

- [ ] **依赖：** C8。
- **主要文件（1）：** `reviews/code-evidence-audit.md`；修复回派原任务。
- **验收：** 每个有效代码块可追溯；Release 无警告；诊断编号、JSON/C# 契约、异步并发和性能证据真实；无泄露、绝对路径或偶发测试。
- **验证：** `dotnet fantomas . --check`；`pnpm check:examples`；`pnpm check:capstone`；`pnpm test`；关键并发测试重复运行。
- **规模：** L。

### R05 — 真实浏览器、可访问性与响应式审计

- [ ] **依赖：** C8。
- **主要文件（≤3）：** `tests/site-browser.test.mjs`、`package.json`、`reviews/browser-accessibility-audit.md`；修复回派原任务。
- **验收：** 根页、中英路线、同页切换、搜索、复制、锚点、前后章、键盘和窄屏可用；控制台/网络无异常；关键页面无明显 WCAG 2.2 AA 问题。
- **验证：** 锁定浏览器自动化 smoke；真实浏览器检查桌面与 360px；`pnpm build`。
- **规模：** L。

### R06 — 干净检出发布复演

- [ ] **依赖：** R01、R02、R03、R04、R05。
- **主要文件（≤2）：** `reviews/release-readiness.md`、必要的静态托管配置文件。
- **验收：** 临时干净检出仅按 README 即可冻结安装、运行全部质量门并生成静态产物；产物无服务器依赖；所有检查点和人工记录已完成。
- **验证：** `pnpm install --frozen-lockfile`；`pnpm test`；`pnpm preview` 浏览器冒烟；检查静态产物链接与资源。
- **规模：** M。

## 10. 最终完成门

- [ ] 45 个 `B*` 章节任务全部完成。
- [ ] 8 个 `A*` 附录任务、前言、首页、术语和答案索引全部完成。
- [ ] C0–C8 全部检查点有可追溯通过记录。
- [ ] R01–R06 全部审计完成且无未关闭的高/中问题。
- [ ] 能力证据矩阵逐项映射到可执行脚本、测试、契约、项目或明确人工证据。
- [ ] 用户确认静态站点内容、双语一致性、正确性和简洁性达到发布标准。
