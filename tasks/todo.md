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

- [ ] **依赖：** B11。
- **主要文件（5）：** `docs/{zh,en}/part-02/ch-12-making-illegal-states-unrepresentable.md`、`docs/{zh,en}/solutions/ch-12-making-illegal-states-unrepresentable.md`、`examples/scripts/ch12-making-illegal-states-unrepresentable.fsx`。
- **验收：** 私有表示、伴生模块、智能构造函数、必要访问控制与不变量形成完整模式；解释同文件/跨文件可见性。
- **验证：** FSI 验证合法构造和预期拒绝路径；双语与内容检查；`pnpm build`。
- **规模：** L。

### K02 — 预约领域模型与不变量

- [ ] **依赖：** B12、F06。
- **主要文件（≤5）：** `examples/capstone/src/Booking.Domain/Booking.Domain.fsproj`、`Domain.fs`、`tests/ExampleTests/BookingDomainTests.fs`、`ThinkingInFSharp.slnx`、`examples/manifest.json`。
- **验收：** 建立活动、容量、请求标识、预约状态等领域类型；公开构造函数拒绝非法容量/标识/状态；领域表示不承担 JSON 或数据库契约。
- **验证：** `dotnet test ThinkingInFSharp.slnx --configuration Release --filter FullyQualifiedName~BookingDomain`；Release 构建；`pnpm check:examples`。
- **规模：** M。

### C2 — 第二部分检查点

- [ ] **依赖：** K02。
- **验收：** 类型建模路径完整；结构/哈希/身份表述一致；K02 的非法状态不可经公开 API 构造；章节和答案双语对等。
- **验证：** `pnpm test`；人工复核 B07、B11、B12 的术语和类型签名。

## 3. 第三部分：组合与程序结构

### B13 — 组合、参数顺序与管道 API / Composition, Argument Order, and Pipeline APIs

- [ ] **依赖：** C2。
- **主要文件（5）：** `docs/{zh,en}/part-03/ch-13-composition-pipeline-api.md`、`docs/{zh,en}/solutions/ch-13-composition-pipeline-api.md`、`examples/scripts/ch13-composition-pipeline-api.fsx`。
- **验收：** 从重复嵌套调用推导 `>>`/`<<` 和面向管道的参数顺序；说明何时直接调用比管道更清楚。
- **验证：** 聚焦 FSI；双语与内容检查；`pnpm build`。
- **规模：** M。

### B14 — 集合选择与求值 / Choosing Collections and Evaluation Models

- [ ] **依赖：** B13。
- **主要文件（5）：** `docs/{zh,en}/part-03/ch-14-collections-evaluation.md`、`docs/{zh,en}/solutions/ch-14-collections-evaluation.md`、`examples/scripts/ch14-collections-evaluation.fsx`。
- **验收：** 比较 `list/array/seq/Map/Set`、转换成本和求值；在问题上下文中教授 `seq {}`；用可观察计数证明 `seq` 重复枚举；准确区分有序键约束与哈希集合约束。
- **验证：** FSI 断言求值次数、顺序和集合行为；双语与内容检查；`pnpm build`。
- **规模：** L。

### B15 — 活动模式与领域匹配边界 / Active Patterns and Domain Matching Boundaries

- [ ] **依赖：** B14。
- **主要文件（5）：** `docs/{zh,en}/part-03/ch-15-active-patterns.md`、`docs/{zh,en}/solutions/ch-15-active-patterns.md`、`examples/scripts/ch15-active-patterns.fsx`。
- **验收：** 覆盖完整、部分和参数化活动模式的适用边界；避免用活动模式隐藏昂贵 I/O 或不可见失败。
- **验证：** FSI 覆盖匹配/不匹配路径；双语与内容检查；`pnpm build`。
- **规模：** M。

### E16 — 多文件模块、命名空间与编译顺序样例

- [ ] **依赖：** B15、F06。
- **主要文件（5）：** `examples/chapters/ch16/Ch16.fsproj`、`Domain.fs`、`Workflow.fs`、`Program.fs`、`tests/ExampleTests/Ch16ProjectTests.fs`。
- **验收：** 展示正确文件顺序、模块/命名空间边界、打开声明和 `<Nullable>enable</Nullable>`；交换顺序有单独预期诊断而非关闭警告。
- **验证：** 聚焦 Release build/test；`pnpm check:examples`。
- **规模：** M。

### B16 — 模块、命名空间、项目与编译设置 / Modules, Namespaces, Projects, and Compiler Settings

- [ ] **依赖：** E16。
- **主要文件（4）：** `docs/{zh,en}/part-03/ch-16-modules-namespaces-projects.md`、`docs/{zh,en}/solutions/ch-16-modules-namespaces-projects.md`。
- **验收：** 由 E16 解释文件顺序、作用域和项目结构；只教授 nullable 启用后的最小标注，不提前展开 B19 的完整空值边界。
- **验证：** E16 聚焦检查；双语与内容检查；`pnpm build`。
- **规模：** M。

### E17 — 签名文件与封装边界样例

- [ ] **依赖：** B16。
- **主要文件（≤5）：** `examples/chapters/ch17/Ch17.fsproj`、`Library.fsi`、`Library.fs`、`tests/ExampleTests/Ch17SignatureTests.fs`、`ThinkingInFSharp.slnx`。
- **验收：** `.fsi` 真实隐藏表示并公开惯用 F# API；测试只能经公共表面观察不变量；文件顺序正确。
- **验证：** 聚焦 Release build/test；`pnpm check:examples`。
- **规模：** M。

### B17 — 签名、访问控制与 F# API / Signatures, Access Control, and F#-Facing APIs

- [ ] **依赖：** E17。
- **主要文件（4）：** `docs/{zh,en}/part-03/ch-17-signatures-encapsulation.md`、`docs/{zh,en}/solutions/ch-17-signatures-encapsulation.md`。
- **验收：** 准确解释签名文件、访问修饰、抽象表示和面向 F# 的公共 API；与 B12 的智能构造模式连接而不重复整章。
- **验证：** E17 聚焦检查；双语与内容检查；`pnpm build`。
- **规模：** M。

### B18 — 显式工作流组合与验证累积 / Explicit Workflow Composition and Validation Accumulation

- [ ] **依赖：** B17。
- **主要文件（5）：** `docs/{zh,en}/part-03/ch-18-workflow-validation.md`、`docs/{zh,en}/solutions/ch-18-workflow-validation.md`、`examples/scripts/ch18-workflow-validation.fsx`。
- **验收：** 用普通函数分别实现 `Result` 短路和独立错误累积；不声称存在内置验证 CE；自定义 builder 与 builder 特定 `and!` 仅为标明的延伸。
- **验证：** FSI 同时断言首错短路和多错累积；双语与内容检查；`pnpm build`。
- **规模：** L。

### K03 — 预约纯工作流与验证累积

- [ ] **依赖：** B18、K02。
- **主要文件（≤5）：** `examples/capstone/src/Booking.Domain/Validation.fs`、`Workflow.fs`、`Booking.Domain.fsproj`、`tests/ExampleTests/BookingWorkflowTests.fs`、`examples/manifest.json`。
- **验收：** 纯工作流输入命令与当前状态，输出事件或错误；可并行检查的输入错误全部累积，依赖前序状态的业务失败短路。
- **验证：** `dotnet test ThinkingInFSharp.slnx --configuration Release --filter FullyQualifiedName~BookingWorkflow`；Release 构建；`pnpm check:examples`。
- **规模：** M。

### C3 — 第三部分检查点

- [ ] **依赖：** K03。
- **验收：** 集合选择、项目顺序、签名封装和工作流组合均有代码证据；未把自定义 CE 当作基础能力。
- **验证：** `pnpm test`；人工复核 `seq` 副作用示例、nullable 措辞和 B18/K03 失败语义。

## 4. 第四部分：副作用、异步与并发

### E19 — .NET 空值边界契约样例

- [ ] **依赖：** C3。
- **主要文件（≤5）：** `examples/chapters/ch19/Ch19.fsproj`、`NullBoundaries.fs`、`tests/ContractTests/Ch19NullTests.fs`、`ThinkingInFSharp.slnx`、`examples/manifest.json`。
- **验收：** nullable 开启；用真实 .NET API 验证引用 `T | null`、`Nullable<T>`、`option` 和边界转换；不把 `option` 宣称为绝对无 null。
- **验证：** 聚焦 Release build/contract tests；`pnpm check:examples`。
- **规模：** M。

### B19 — .NET API 与空值边界 / .NET APIs and Null Boundaries

- [ ] **依赖：** E19。
- **主要文件（4）：** `docs/{zh,en}/part-04/ch-19-dotnet-null-boundaries.md`、`docs/{zh,en}/solutions/ch-19-dotnet-null-boundaries.md`。
- **验收：** 在实质 I/O 前完成构造、成员、重载、接口与完整空值模型；提供边界转换决策表和 `Some null` 反例。
- **验证：** E19 契约测试；双语与内容检查；`pnpm build`。
- **规模：** L。

### B20 — 函数式核心与副作用边界 / Functional Core and Effect Boundaries

- [ ] **依赖：** B19。
- **主要文件（5）：** `docs/{zh,en}/part-04/ch-20-functional-core-effects.md`、`docs/{zh,en}/solutions/ch-20-functional-core-effects.md`、`examples/scripts/ch20-functional-core-effects.fsx`。
- **验收：** 将时间、随机数和环境显式化为数据或依赖；说明何时接口、函数参数或闭包分别合适。
- **验证：** FSI 使用固定时钟/随机源断言确定结果；双语与内容检查；`pnpm build`。
- **规模：** M。

### B21 — 异常、资源与 I/O / Exceptions, Resources, and I/O

- [ ] **依赖：** B20。
- **主要文件（5）：** `docs/{zh,en}/part-04/ch-21-exceptions-resources-io.md`、`docs/{zh,en}/solutions/ch-21-exceptions-resources-io.md`、`examples/scripts/ch21-exceptions-resources-io.fsx`。
- **验收：** 覆盖异常边界、`use`、文件 I/O 与资源释放；用决策表收束 `option/Result/验证/异常`，避免把异常包装成无信息错误。
- **验证：** FSI 在临时目录验证成功/故障均释放资源；双语与内容检查；`pnpm build`。
- **规模：** L。

### B22 — `Async<'T>` 与 `Task<'T>` / `Async<'T>` and `Task<'T>`

- [ ] **依赖：** B21。
- **主要文件（5）：** `docs/{zh,en}/part-04/ch-22-async-task.md`、`docs/{zh,en}/solutions/ch-22-async-task.md`、`examples/scripts/ch22-async-task.fsx`。
- **验收：** 在问题上下文中教授 `async {}` 与 `task {}`；由可观察门闩证明 `Async` 描述待启动工作而 `task {}` 立即开始；给出互操作和选择准则，不用任意 sleep 判定。
- **验证：** FSI 启动时机自测；双语与内容检查；`pnpm build`。
- **规模：** L。

### B23 — 取消、超时、故障与释放 / Cancellation, Timeouts, Faults, and Disposal

- [ ] **依赖：** B22。
- **主要文件（5）：** `docs/{zh,en}/part-04/ch-23-cancellation-timeouts.md`、`docs/{zh,en}/solutions/ch-23-cancellation-timeouts.md`、`examples/scripts/ch23-cancellation-timeouts.fsx`。
- **验收：** 显式传播令牌，区分取消操作与放弃等待；覆盖故障传播及成功/失败/取消下的同步和异步释放。
- **验证：** FSI 用 `TaskCompletionSource`/令牌而非计时猜测验证全部路径；双语与内容检查；`pnpm build`。
- **规模：** L。

### B24 — 并行、并发、代理与受控可变性 / Parallelism, Concurrency, Agents, and Controlled Mutation

- [ ] **依赖：** B23。
- **主要文件（5）：** `docs/{zh,en}/part-04/ch-24-concurrency-agents-state.md`、`docs/{zh,en}/solutions/ch-24-concurrency-agents-state.md`、`examples/scripts/ch24-concurrency-agents-state.fsx`。
- **验收：** 区分并行/并发；覆盖 `MailboxProcessor`、锁/原子操作、共享状态和缓存的适用边界；不宣称代理自动解决一致性。
- **验证：** FSI 使用确定性屏障验证竞争前后行为；双语与内容检查；`pnpm build`。
- **规模：** L。

### K04 — 预约异步端口与确定性替身

- [ ] **依赖：** B24、K03。
- **主要文件（≤5）：** `examples/capstone/src/Booking.Domain/Ports.fs`、`Fakes.fs`、`Booking.Domain.fsproj`、`tests/ExampleTests/BookingAsyncPortTests.fs`、`examples/manifest.json`。
- **验收：** 持久化、支付、通知、时钟端口显式接收取消令牌；替身能确定性模拟成功、故障、取消和延迟门闩，且纯领域层不依赖具体 I/O。
- **验证：** `dotnet test ThinkingInFSharp.slnx --configuration Release --filter FullyQualifiedName~BookingAsyncPort`；Release 构建；`pnpm check:examples`。
- **规模：** M。

### C4 — 第四部分检查点

- [ ] **依赖：** K04。
- **验收：** 空值、资源、启动时机、取消、超时、故障、释放和竞争均有非计时型证据；章节选择建议互相一致。
- **验证：** `pnpm test`；重复运行异步/并发聚焦测试至少 20 次无偶发失败。

## 5. 第五部分：.NET 互操作与工程质量

### E25 — F# 对象模型样例

- [ ] **依赖：** C4。
- **主要文件（≤5）：** `examples/chapters/ch25/Ch25.fsproj`、`Types.fs`、`Program.fs`、`tests/ExampleTests/Ch25ObjectTests.fs`、`ThinkingInFSharp.slnx`。
- **验收：** 类、构造、成员、接口、对象表达式、类型扩展和结构体均由实际需求驱动；结构体示例说明复制和默认值风险。
- **验证：** 聚焦 Release build/test；`pnpm check:examples`。
- **规模：** M。

### B25 — 在 F# 中定义对象 / Defining Objects in F#

- [ ] **依赖：** E25。
- **主要文件（4）：** `docs/{zh,en}/part-05/ch-25-objects-interfaces.md`、`docs/{zh,en}/solutions/ch-25-objects-interfaces.md`。
- **验收：** 准确呈现对象特性与函数/记录/联合类型的选择边界；不把类包装当作默认“工程化”。
- **验证：** E25 聚焦检查；双语与内容检查；`pnpm build`。
- **规模：** M。

### B26 — 深入 .NET 边界 / Deeper .NET Boundaries

- [ ] **依赖：** B25。
- **主要文件（5）：** `docs/{zh,en}/part-05/ch-26-dotnet-runtime-boundaries.md`、`docs/{zh,en}/solutions/ch-26-dotnet-runtime-boundaries.md`、`examples/scripts/ch26-dotnet-runtime-boundaries.fsx`。
- **验收：** 覆盖运行时类型/转换、委托、事件、.NET 集合、引用身份和哈希；与 B07/B11/B14 的相等和键规则贯通。
- **验证：** FSI 断言委托/事件与集合边界；双语与内容检查；`pnpm build`。
- **规模：** L。

### E27 — 给 C# 调用的 F# API 契约样例

- [ ] **依赖：** B26。
- **主要文件（5）：** `examples/chapters/ch27/FSharpApi/FSharpApi.fsproj`、`Library.fs`、`examples/chapters/ch27/CSharpClient/CSharpClient.csproj`、`Program.cs`、`examples/manifest.json`。
- **验收：** C# 客户端无需依赖 F# 语法知识即可消费稳定公共表面；DU/option 等内部表示经明确边界适配；XML 文档可见；编译即契约证据。
- **验证：** 分别 Release 构建 F# 库和 C# 客户端并运行客户端断言；`pnpm check:examples`。
- **规模：** M。

### B27 — 为 C# 设计 F# API / Designing F# APIs for C#

- [ ] **依赖：** E27。
- **主要文件（4）：** `docs/{zh,en}/part-05/ch-27-fsharp-api-for-csharp.md`、`docs/{zh,en}/solutions/ch-27-fsharp-api-for-csharp.md`。
- **验收：** 解释公共表示、命名、属性、重载、文档和兼容性；明确 F# 内部模型不应直接泄露为 C#/JSON 契约。
- **验证：** E27 客户端构建/运行；双语与内容检查；`pnpm build`。
- **规模：** L。

### E28 — 单元、替身与边界测试样例

- [ ] **依赖：** B27。
- **主要文件（≤4）：** `examples/chapters/ch28/OrderWorkflow.fs`、`tests/ExampleTests/Ch28WorkflowTests.fs`、`tests/ContractTests/Ch28BoundaryTests.fs`、`examples/manifest.json`。
- **验收：** 纯逻辑用值测试，外部端口用小型替身，序列化等稳定边界用契约测试；不引入重型 mock 框架。
- **验证：** 精确过滤 Ch28 的 example/contract tests；`pnpm check:examples`。
- **规模：** M。

### B28 — 示例测试、替身与边界测试 / Example Tests, Test Doubles, and Boundary Tests

- [ ] **依赖：** E28。
- **主要文件（4）：** `docs/{zh,en}/part-05/ch-28-testing-boundaries.md`、`docs/{zh,en}/solutions/ch-28-testing-boundaries.md`。
- **验收：** 从风险选择测试层；展示状态与行为断言、确定性替身和契约边界；避免测试实现细节。
- **验证：** E28 聚焦测试；双语与内容检查；`pnpm build`。
- **规模：** M。

### E29 — FsCheck 性质测试样例

- [ ] **依赖：** B28。
- **主要文件（≤4）：** `tests/ExampleTests/Ch29Properties.fs`、`tests/ExampleTests/ExampleTests.fsproj`、`examples/chapters/ch29/Generators.fs`、`examples/manifest.json`。
- **验收：** 锁定 FsCheck 版本；性质、生成器、缩减和反例可复现；至少一个“看似合理但错误”的性质被反例推翻。
- **验证：** 精确过滤 Ch29 tests，并用固定重放信息复现失败示例；`pnpm check:examples`。
- **规模：** M。

### B29 — 使用 FsCheck 进行性质测试 / Property Testing with FsCheck

- [ ] **依赖：** E29。
- **主要文件（4）：** `docs/{zh,en}/part-05/ch-29-property-testing.md`、`docs/{zh,en}/solutions/ch-29-property-testing.md`。
- **验收：** 从例子推广到不变量，讲清生成、分类、缩减与重放；不把性质测试表述为单元测试替代品。
- **验证：** E29 聚焦测试；双语与内容检查；`pnpm build`。
- **规模：** M。

### E30 — 诊断、格式化与可复现构建证据

- [ ] **依赖：** B29。
- **主要文件（≤5）：** `.config/dotnet-tools.json`、`fantomas.json`、`examples/expected-errors/ch11-value-restriction.fsx`、`examples/expected-errors/ch16-file-order/`（≤2 文件）、`examples/manifest.json`。
- **验收：** 锁定 Fantomas；预期错误断言实际诊断编号；格式检查不改文件；干净 restore/build 可重复；不扩展为平台专属 CI 教程。
- **验证：** `dotnet tool restore`；`dotnet fantomas . --check`；`pnpm check:examples`；干净 Release build。
- **规模：** M。

### B30 — 诊断、调试、格式化与构建 / Diagnostics, Debugging, Formatting, and Builds

- [ ] **依赖：** E30。
- **主要文件（4）：** `docs/{zh,en}/part-05/ch-30-diagnostics-tooling-builds.md`、`docs/{zh,en}/solutions/ch-30-diagnostics-tooling-builds.md`。
- **验收：** 教读者从首个诊断定位根因、使用调试器/FSI、格式化和静态检查，并解释锁定与可复现原则。
- **验证：** E30 全部检查；双语与内容检查；`pnpm build`。
- **规模：** M。

### E31 — 测量、分配与优化前后对照

- [ ] **依赖：** B30。
- **主要文件（≤5）：** `examples/chapters/ch31/Ch31.Benchmarks.fsproj`、`Benchmarks.fs`、`Program.fs`、`tests/ContentFixtures/ch31-baseline.json`、`examples/manifest.json`。
- **验收：** 锁定基准工具；记录运行时/OS/CPU/配置；比较一个集合或分配热点的基线与改进；结果只支持样例内结论；`voption`、Span/byref、裁剪/AOT 仅在测量语境识别。
- **验证：** Release 基准 smoke job 与功能等价测试；`pnpm check:examples`。
- **规模：** L。

### B31 — 先测量再优化 / Measure Before Optimizing

- [ ] **依赖：** E31。
- **主要文件（4）：** `docs/{zh,en}/part-05/ch-31-measure-before-optimizing.md`、`docs/{zh,en}/solutions/ch-31-measure-before-optimizing.md`。
- **验收：** 给出基线、剖析、假设、修改、复测流程；清楚区分微基准与端到端性能；不提供脱离证据的“最快集合”清单。
- **验证：** E31 基准 smoke；双语与内容检查；`pnpm build`。
- **规模：** M。

### E32 — 从函数到应用的最小宿主

- [ ] **依赖：** B31。
- **主要文件（≤5）：** `examples/chapters/ch32/Ch32.App.fsproj`、`Ports.fs`、`Composition.fs`、`Program.fs`、`tests/ExampleTests/Ch32CompositionTests.fs`。
- **验收：** 组合根连接配置、端口和生命周期；提供结构化日志、指标/追踪概念的最小可观察证据；不引入容器框架作为前提。
- **验证：** 聚焦 Release build/test；以固定配置运行并检查结构化输出；`pnpm check:examples`。
- **规模：** L。

### B32 — 从函数到应用 / From Functions to Applications

- [ ] **依赖：** E32。
- **主要文件（4）：** `docs/{zh,en}/part-05/ch-32-functions-to-applications.md`、`docs/{zh,en}/solutions/ch-32-functions-to-applications.md`。
- **验收：** 从纯工作流推导端口、组合根、配置、生命周期与最小可观测性；保持模式轻量并说明何时需要更强宿主。
- **验证：** E32 聚焦检查；双语与内容检查；`pnpm build`。
- **规模：** L。

### K05 — 预约性质测试与稳定公共边界

- [ ] **依赖：** B32、K04、E29。
- **主要文件（≤5）：** `tests/ExampleTests/BookingProperties.fs`、`tests/ExampleTests/ExampleTests.fsproj`、`examples/capstone/src/Booking.Domain/PublicApi.fs`、`Booking.Domain.fsproj`、`examples/manifest.json`。
- **验收：** 容量与状态转换具有性质测试；公开模块隐藏领域表示细节并为后续 DTO/C# 边界提供稳定函数；反例可重放。
- **验证：** 精确过滤 BookingProperties；Release build；`pnpm check:examples`。
- **规模：** M。

### C5 — 第五部分检查点

- [ ] **依赖：** K05。
- **验收：** 对象/.NET/C# 边界、单元/性质/契约测试、诊断、格式化、测量和应用装配均有可运行证据；公共 API 对 F# 与 C# 的取舍有明确理由。
- **验证：** `dotnet fantomas . --check`；`pnpm test`；从 C# 客户端重新构建验证。

## 6. 第六部分：活动预约系统

### K06 — 收束领域语言、命令与事件

- [ ] **依赖：** C5、K02、K05。
- **主要文件（≤5）：** `examples/capstone/src/Booking.Domain/Domain.fs`、`Commands.fs`、`Events.fs`、`PublicApi.fs`、`Booking.Domain.fsproj`。
- **验收：** 名称与业务规则统一；命令表示意图、事件表示已发生事实；私有领域表示保持封装；旧教学切片迁移而非复制第二套模型。
- **验证：** 领域与性质测试；Release build；`pnpm check:examples`。
- **规模：** M。

### B33 — 业务语言、命令、事件与模型 / Business Language, Commands, Events, and Model

- [ ] **依赖：** K06。
- **主要文件（4）：** `docs/{zh,en}/part-06/ch-33-domain-language-model.md`、`docs/{zh,en}/solutions/ch-33-domain-language-model.md`。
- **验收：** 回顾前五部分切片并解释最终领域语言；区分命令、事件、状态与边界 DTO，不把事件溯源设为必需架构。
- **验证：** K06 聚焦检查；双语与内容检查；`pnpm build`。
- **规模：** M。

### K07 — 组装纯预约工作流

- [ ] **依赖：** B33、K03、K06。
- **主要文件（≤5）：** `examples/capstone/src/Booking.Domain/Validation.fs`、`Workflow.fs`、`Decider.fs`、`Booking.Domain.fsproj`、`tests/ExampleTests/BookingDeciderTests.fs`。
- **验收：** 纯决策函数连接验证、状态和事件；覆盖成功、容量不足、重复或非法命令；同一规则只有一个权威实现。
- **验证：** 精确过滤 BookingDecider；Release build；`pnpm check:examples`。
- **规模：** M。

### B34 — 纯预约工作流与验证 / The Pure Booking Workflow and Validation

- [ ] **依赖：** K07。
- **主要文件（4）：** `docs/{zh,en}/part-06/ch-34-pure-booking-workflow.md`、`docs/{zh,en}/solutions/ch-34-pure-booking-workflow.md`。
- **验收：** 从类型和规则推导纯决策管线；清楚说明验证累积与业务短路的边界，并用 K07 失败路径支撑。
- **验证：** K07 聚焦测试；双语与内容检查；`pnpm build`。
- **规模：** M。

### K08a — 边界 DTO、JSON 映射与配置

- [ ] **依赖：** B34、K07。
- **主要文件（≤5）：** `examples/capstone/src/Booking.Contracts/Booking.Contracts.fsproj`、`Dtos.fs`、`Mapping.fs`、`tests/ContractTests/BookingJsonContractTests.fs`、`ThinkingInFSharp.slnx`。
- **验收：** Web/持久化 DTO 与领域类型分离；双向映射明确失败；JSON 字段、枚举/联合表示和兼容性由固定契约测试锁定；机密值不写入仓库。
- **验证：** 精确过滤 BookingJsonContract；Release build；`pnpm check:examples`。
- **规模：** L。

### K08b — 无外部账号的本地持久化适配器

- [ ] **依赖：** K08a。
- **主要文件（≤5）：** `examples/capstone/src/Booking.Infrastructure/Booking.Infrastructure.fsproj`、`FileStore.fs`、`Configuration.fs`、`tests/ContractTests/BookingStoreContractTests.fs`、`ThinkingInFSharp.slnx`。
- **验收：** 使用本地真实持久化保存并恢复 DTO；原子替换与损坏输入有明确处理；路径可配置且测试只用临时目录。
- **验证：** 精确过滤 BookingStoreContract；Release build；`pnpm check:examples`。
- **规模：** L。

### K09 — 支付、通知替身与生命周期

- [ ] **依赖：** K08b、K04。
- **主要文件（≤5）：** `examples/capstone/src/Booking.Infrastructure/PaymentStub.fs`、`NotificationStub.fs`、`Composition.fs`、`Booking.Infrastructure.fsproj`、`tests/ExampleTests/BookingAdapterTests.fs`。
- **验收：** 替身确定性产生成功、拒绝、故障与取消；资源所有权和释放位置明确；不启动额外 HTTP 服务。
- **验证：** 精确过滤 BookingAdapter；Release build；`pnpm check:examples`。
- **规模：** M。

### B35 — 端口、持久化、配置与替身 / Ports, Persistence, Configuration, and Stubs

- [ ] **依赖：** K08a、K08b、K09。
- **主要文件（4）：** `docs/{zh,en}/part-06/ch-35-ports-persistence-config.md`、`docs/{zh,en}/solutions/ch-35-ports-persistence-config.md`。
- **验收：** 解释端口到本地适配器的装配、DTO 映射、配置与替身失败；明确领域 DU/记录不直接序列化。
- **验证：** K08a/K08b/K09 聚焦测试；双语与内容检查；`pnpm build`。
- **规模：** L。

### K10 — ASP.NET Core Web API 边界

- [ ] **依赖：** B35。
- **主要文件（≤5）：** `examples/capstone/src/Booking.Api/Booking.Api.fsproj`、`Program.fs`、`Endpoints.fs`、`tests/ContractTests/BookingApiTests.fs`、`ThinkingInFSharp.slnx`。
- **验收：** API 只接收/返回边界 DTO；输入验证、JSON、错误映射、异步与请求取消正确；机密配置不进入响应/日志；测试进程内运行。
- **验证：** 精确过滤 BookingApi；Release build；本地启动后最小 HTTP 冒烟；`pnpm check:examples`。
- **规模：** L。

### B36 — Web API、JSON 与输入边界 / Web API, JSON, and Input Boundaries

- [ ] **依赖：** K10。
- **主要文件（4）：** `docs/{zh,en}/part-06/ch-36-web-api-boundaries.md`、`docs/{zh,en}/solutions/ch-36-web-api-boundaries.md`。
- **验收：** 由 K10 解释端点、DTO、验证、取消和机密配置边界；提供可复制的本地运行/请求命令及失败响应。
- **验证：** K10 契约与 HTTP 冒烟；双语与内容检查；`pnpm build`。
- **规模：** L。

### K11 — 原子容量、幂等、重试与重启恢复

- [ ] **依赖：** B36、K10。
- **主要文件（≤5）：** `examples/capstone/src/Booking.Infrastructure/AtomicBookingStore.fs`、`Idempotency.fs`、`Booking.Infrastructure.fsproj`、`tests/ContractTests/BookingConsistencyTests.fs`、`tests/ContentFixtures/capstone/`。
- **验收：** 两个受控并发预约不超卖；重复请求 ID 不重复副作用；失败后重试语义明确；新进程实例从持久状态恢复；测试不用不稳定 sleep。
- **验证：** 精确过滤 BookingConsistency 并重复运行 20 次；Release build；`pnpm check:examples`。
- **规模：** L。

### B37 — 一致性、幂等、重试与部分失败 / Consistency, Idempotency, Retries, and Partial Failure

- [ ] **依赖：** K11。
- **主要文件（4）：** `docs/{zh,en}/part-06/ch-37-consistency-idempotency.md`、`docs/{zh,en}/solutions/ch-37-consistency-idempotency.md`。
- **验收：** 从真实竞争场景推导原子约束、幂等和重试；诚实说明本地适配器保证及其非分布式边界。
- **验证：** K11 重复测试；双语与内容检查；`pnpm build`。
- **规模：** L。

### K12a — 集成测试与 C# 契约客户端

- [ ] **依赖：** B37、K11、E27。
- **主要文件（≤5）：** `examples/capstone/clients/Booking.CSharpClient/Booking.CSharpClient.csproj`、`Program.cs`、`tests/ContractTests/BookingEndToEndTests.fs`、`tests/ContractTests/ContractTests.fsproj`、`ThinkingInFSharp.slnx`。
- **验收：** C# 客户端通过已发布 DTO/API 表面完成预约；端到端测试覆盖成功、重复、冲突和无效 JSON；不引用领域内部类型。
- **验证：** 构建并运行 C# 客户端；精确过滤 BookingEndToEnd；`pnpm check:examples`。
- **规模：** L。

### K12b — 诊断、运行说明与发布检查

- [ ] **依赖：** K12a、E32。
- **主要文件（≤5）：** `examples/capstone/src/Booking.Api/Diagnostics.fs`、`Program.fs`、`examples/capstone/README.md`、`scripts/check-capstone.mjs`、`package.json`。
- **验收：** 日志含相关 ID 而不泄露机密；最小指标/追踪位置可解释；一条命令执行 capstone 构建、测试和本地冒烟；运行说明从干净状态可复现。
- **验证：** `pnpm check:capstone`；人工检查日志的成功与失败路径。
- **规模：** M。

### B38 — 集成、诊断、C# 客户端与发布 / Integration, Diagnostics, C# Client, and Release

- [ ] **依赖：** K12a、K12b。
- **主要文件（4）：** `docs/{zh,en}/part-06/ch-38-integration-diagnostics-release.md`、`docs/{zh,en}/solutions/ch-38-integration-diagnostics-release.md`。
- **验收：** 把运行、集成测试、诊断、C# 契约和发布检查收束成可复现闭环；列出保证、限制和下一步，不假装样例已是完整商业系统。
- **验证：** `pnpm check:capstone`；双语与内容检查；`pnpm build`。
- **规模：** L。

### C6 — 贯穿项目检查点

- [ ] **依赖：** B38。
- **验收：** 规格要求的 JSON/C#、竞争预约、重复 ID、失败重试、取消/超时、资源释放和重启持久化场景全部有证据；无需账号或额外服务。
- **验证：** `pnpm check:capstone`；`pnpm test`；按 README 从临时目录手工完成一次 API + C# 客户端流程。

## 7. 第七部分：生态地图

### X39 — ASP.NET Core/F# Web 代表性样例

- [ ] **依赖：** C6。
- **主要文件（≤5）：** `examples/ecosystem/web/WebSample.fsproj`、`Program.fs`、`tests/ContractTests/WebSampleTests.fs`、`examples/ecosystem/web/README.md`、`examples/manifest.json`。
- **验收：** 锁定验证版本；用 F# Minimal API 完成一个输入/输出与错误边界；说明它和 capstone 的关系，避免复制第二个大型 Web 应用。
- **验证：** Release build、进程内 HTTP 契约测试、`pnpm check:examples`。
- **规模：** M。

### B39 — ASP.NET Core 与 F# Web 生态 / ASP.NET Core and the F# Web Ecosystem

- [ ] **依赖：** X39。
- **主要文件（4）：** `docs/{zh,en}/part-07/ch-39-web-ecosystem.md`、`docs/{zh,en}/solutions/ch-39-web-ecosystem.md`。
- **验收：** 按问题类型比较平台原生 Minimal API 与主要 F# 选择；列优势、摩擦、互操作和选择条件；第三方信息含版本与官方来源。
- **验证：** X39 聚焦检查；双语与内容检查；`pnpm build`。
- **规模：** L。

### X40 — 数据、查询与类型提供器使用样例

- [ ] **依赖：** B39。
- **主要文件（≤5）：** `examples/ecosystem/data/DataSample.fsproj`、`Program.fs`、`tests/ContentFixtures/data/sample.csv`、`tests/ExampleTests/DataSampleTests.fs`、`examples/manifest.json`。
- **验收：** 锁定数据/类型提供器包；固定本地样本保证编译稳定；展示查询与类型提供器消费，但不教授类型提供器创作或依赖在线 schema。
- **验证：** 离线可重复 Release build/test；`pnpm check:examples`。
- **规模：** M。

### B40 — 数据、类型提供器、分析与机器学习 / Data, Type Providers, Analytics, and ML

- [ ] **依赖：** X40。
- **主要文件（4）：** `docs/{zh,en}/part-07/ch-40-data-analytics.md`、`docs/{zh,en}/solutions/ch-40-data-analytics.md`。
- **验收：** 覆盖数据访问、查询、类型提供器、分析、可视化和 ML 的决策地图；区分稳定 schema 与动态外部数据的风险。
- **验证：** X40 聚焦检查；双语与内容检查；`pnpm build`。
- **规模：** L。

### X41a — Fable 工作区与锁定依赖

- [ ] **依赖：** B40、F01。
- **主要文件（5）：** `pnpm-workspace.yaml`、`examples/ecosystem/fable/package.json`、`FableSample.fsproj`、`pnpm-lock.yaml`、`examples/manifest.json`。
- **验收：** 锁定 Fable、.NET tool 与前端直接依赖；根冻结安装覆盖生态工作区；包脚本预留开发、生产构建和浏览器冒烟入口。
- **验证：** `pnpm install --frozen-lockfile`；Fable 项目 restore；`pnpm check:examples` 能识别该样例。
- **规模：** M。

### X41b — Fable 浏览器构建与最小交互

- [ ] **依赖：** X41a。
- **主要文件（≤4）：** `examples/ecosystem/fable/App.fs`、`index.html`、`vite.config.mjs`、`tests/site-fable-smoke.mjs`。
- **验收：** 一个最小交互由 F# 编译到浏览器并在生产模式构建；样例和说明准确区分生成 JS、浏览器 API 与可用 .NET API。
- **验证：** 生产构建；锁定浏览器 smoke；`pnpm check:examples`。
- **规模：** M。

### B41 — Fable、Elmish 与浏览器应用 / Fable, Elmish, and Browser Applications

- [ ] **依赖：** X41b。
- **主要文件（4）：** `docs/{zh,en}/part-07/ch-41-fable-elmish.md`、`docs/{zh,en}/solutions/ch-41-fable-elmish.md`。
- **验收：** 用问题/技术/优势/摩擦结构解释 Fable 与 Elmish；不把 .NET 服务端假设带入浏览器；代表例只承诺已验证的构建。
- **验证：** X41b 构建与浏览器冒烟；双语与内容检查；`pnpm build`。
- **规模：** L。

### X42 — 云、容器与 Aspire 的本地验证切片

- [ ] **依赖：** B41。
- **主要文件（≤5）：** `examples/ecosystem/cloud/CloudService.fsproj`、`Program.fs`、`examples/ecosystem/cloud/AppHost/AppHost.csproj`、`Program.cs`、`examples/ecosystem/cloud/README.md`。
- **验收：** 实施时依据官方 .NET 10 支持矩阵锁定 Aspire/容器工具版本；用 C# 基础设施宿主编排 F# 服务且无需云账号，诚实展示生态边界；Serverless 只作带约束的决策分支。
- **验证：** Release build；本地 AppHost/服务健康检查；可用时验证容器发布；`pnpm check:examples`。
- **规模：** L。

### B42 — 云、容器、Serverless 与 .NET Aspire / Cloud, Containers, Serverless, and .NET Aspire

- [ ] **依赖：** X42。
- **主要文件（4）：** `docs/{zh,en}/part-07/ch-42-cloud-containers-aspire.md`、`docs/{zh,en}/solutions/ch-42-cloud-containers-aspire.md`。
- **验收：** 以部署问题区分容器、托管服务、Serverless 和 Aspire；注明 F# 项目模板/生态摩擦及 C# 基础设施项目互操作选择。
- **验证：** X42 聚焦检查；双语与内容检查；`pnpm build`。
- **规模：** L。

### X43 — Avalonia 最小桌面样例

- [ ] **依赖：** B42。
- **主要文件（≤5）：** `examples/ecosystem/avalonia/AvaloniaSample.fsproj`、`Program.fs`、`App.axaml`、`MainWindow.axaml`、`MainWindow.fs`。
- **验收：** 锁定 Avalonia 版本；最小应用可构建并保持 UI 与纯更新逻辑分离；移动端只说明官方支持边界，不声称已验证未执行的平台。
- **验证：** Release build；可用桌面环境人工启动；纯更新逻辑聚焦测试；`pnpm check:examples`。
- **规模：** L。

### B43 — Avalonia、桌面与移动端 / Avalonia, Desktop, and Mobile

- [ ] **依赖：** X43。
- **主要文件（4）：** `docs/{zh,en}/part-07/ch-43-avalonia-desktop-mobile.md`、`docs/{zh,en}/solutions/ch-43-avalonia-desktop-mobile.md`。
- **验收：** 提供桌面/移动决策地图，准确区分共享 .NET 能力、平台工具链和 F# 模板/绑定摩擦；不把桌面构建等同全平台验证。
- **验证：** X43 构建；双语与内容检查；`pnpm build`。
- **规模：** L。

### X44 — Unity 6.3 LTS F# 插件与 C# 适配层

- [ ] **依赖：** B43。
- **主要文件（≤5）：** `examples/ecosystem/unity/FSharpGameplay/FSharpGameplay.fsproj`、`Gameplay.fs`、`UnityAdapter.cs`、`link.xml`、`README.md`。
- **验收：** F# 库目标 `netstandard2.1`；记录并验证 `FSharp.Core` 装配；C# 薄层隔离 Unity 序列化/API；文档区分编译、编辑器导入、IL2CPP/裁剪和 Player 构建。
- **验证：** Release 编译插件；检查产物与依赖；`pnpm check:examples`；Unity 6.3 指定补丁版本人工导入和代表性 Player 构建另行记录。
- **规模：** L。

### B44 — Unity 6.3 LTS 与 F# / Unity 6.3 LTS and F#

- [ ] **依赖：** X44。
- **主要文件（4）：** `docs/{zh,en}/part-07/ch-44-unity.md`、`docs/{zh,en}/solutions/ch-44-unity.md`。
- **验收：** 准确解释兼容目标、`FSharp.Core`、C# 适配、序列化、IL2CPP 和裁剪；不把 F# 类库成功编译误报为 Unity 全链成功。
- **验证：** X44 自动证据 + 独立人工记录；双语与内容检查；`pnpm build`。
- **规模：** L。

### B45 — 脚本、自动化、包生态与继续学习 / Scripting, Automation, Packages, and What Comes Next

- [ ] **依赖：** B44。
- **主要文件（5）：** `docs/{zh,en}/part-07/ch-45-scripting-packages-next.md`、`docs/{zh,en}/solutions/ch-45-scripting-packages-next.md`、`examples/scripts/ch45-scripting-packages-next.fsx`。
- **验收：** 展示一个真实本地自动化脚本、包选择/锁定原则和继续学习地图；quotations、SRTP、灵活类型、byref/Span 只做识别并链接 A08。
- **验证：** FSI 在临时夹具上执行自动化且幂等；双语与内容检查；`pnpm build`。
- **规模：** M。

### C7 — 生态地图检查点

- [ ] **依赖：** B45。
- **验收：** 每章均含问题、选择、优势、摩擦、代表例和继续学习；版本、官方来源、自动/人工证据和未验证边界清楚；无专有账号依赖。
- **验证：** `pnpm test`；分别执行 X39、X40、X41a/b、X42、X43、X44 的聚焦构建；审查 Unity 人工记录是否真实完成而非占位。

## 8. 前言、附录与全站装配

### A01 — 附录 A：跨平台环境配置

- [ ] **依赖：** C7。
- **主要文件（2）：** `docs/zh/appendices/a-setup.md`、`docs/en/appendices/a-setup.md`。
- **验收：** Windows/macOS/Linux 安装、版本检查、编辑器和故障排查准确；区分必需工具与可选工具。
- **验证：** 双语与内容检查；在当前平台逐命令复核，其他平台依据官方文档审阅；`pnpm build`。
- **规模：** M。

### A02 — 附录 B：语法与运算符速查

- [ ] **依赖：** A01。
- **主要文件（2）：** `docs/zh/appendices/b-syntax-reference.md`、`docs/en/appendices/b-syntax-reference.md`。
- **验收：** 只汇总正文已解释语法；优先类型和小例，不把速查表变成第二本语言规范。
- **验证：** 代码片段引用已验证源；双语与内容检查；`pnpm build`。
- **规模：** M。

### A03 — 附录 C：集合选择与复杂度

- [ ] **依赖：** A02、B14、B26。
- **主要文件（2）：** `docs/zh/appendices/c-collections.md`、`docs/en/appendices/c-collections.md`。
- **验收：** 汇总集合求值、更新、查找、顺序、键约束和典型复杂度；复杂度声明注明条件且与官方实现文档一致。
- **验证：** 双语与内容检查；对表中每行执行来源复核；`pnpm build`。
- **规模：** M。

### A04 — 附录 D：C# 到 F# 迁移与互操作表

- [ ] **依赖：** A03、B27。
- **主要文件（2）：** `docs/zh/appendices/d-csharp-migration.md`、`docs/en/appendices/d-csharp-migration.md`。
- **验收：** 对比值/表达式、数据建模、失败、异步、集合和 API 边界；避免“一一替换语法”的误导。
- **验证：** 与 E27/C# 客户端交叉核对；双语与内容检查；`pnpm build`。
- **规模：** M。

### A05 — 附录 E：常见编译错误索引

- [ ] **依赖：** A04、E30。
- **主要文件（2）：** `docs/zh/appendices/e-compiler-errors.md`、`docs/en/appendices/e-compiler-errors.md`。
- **验收：** 按实际诊断编号与根因索引值限制、文件顺序、类型不匹配、非穷尽等；所有诊断来自锁定编译器运行。
- **验证：** `pnpm check:examples` 重跑 expected-error；双语与内容检查；`pnpm build`。
- **规模：** M。

### A06 — 附录 F：中英文术语表

- [ ] **依赖：** A05、F04。
- **主要文件（2）：** `docs/zh/glossary.md`、`docs/en/glossary.md`。
- **验收：** 从 `terminology.json` 生成或验证一致的术语、定义、首次出现和交叉链接；两种语言均可独立理解。
- **验证：** `pnpm check:content`；`pnpm check:parity`；`pnpm build`。
- **规模：** M。

### A07 — 附录 G：答案与开放题评审标准

- [ ] **依赖：** A06、B45。
- **主要文件（2）：** `docs/zh/appendices/g-solutions-guide.md`、`docs/en/appendices/g-solutions-guide.md`。
- **验收：** 索引 45 章答案；开放设计题给约束、评审维度和可接受变体，不伪造唯一答案。
- **验证：** 内容检查确保所有练习/答案编号可达且无孤儿；`pnpm build`。
- **规模：** M。

### A08 — 附录 H：高级特性识别索引

- [ ] **依赖：** A07、B31、B40、B45。
- **主要文件（2）：** `docs/zh/appendices/h-advanced-index.md`、`docs/en/appendices/h-advanced-index.md`。
- **验收：** quotations、SRTP、灵活类型、byref/Span 各说明识别信号、适用边界与官方入口；明确首版不教类型提供器创作或编译器服务。
- **验证：** 官方来源复核；双语与内容检查；`pnpm build`。
- **规模：** M。

### S01 — 双语前言、阅读路线与中立首页

- [ ] **依赖：** C7。
- **主要文件（5）：** `docs/index.md`、`docs/zh/index.md`、`docs/en/index.md`、`docs/zh/preface/index.md`、`docs/en/preface/index.md`。
- **验收：** 说明适合/不适合读者、三条路线、运行示例、类型签名阅读、F# 10/.NET 10 范围；根页提供等权语言入口。
- **验证：** 双语与内容检查；`pnpm build`；浏览器验证首次访问和语言选择。
- **规模：** M。

### S02 — 完整目录、侧栏与同页语言映射

- [ ] **依赖：** A08、S01。
- **主要文件（≤5）：** `docs/.vitepress/config/index.ts`、`zh.ts`、`en.ts`、`scripts/generate-navigation.mjs`、`package.json`。
- **验收：** 45 章、前言、8 附录、术语和答案进入对称导航；前后章顺序正确；语言切换始终映射相同 `translationKey`。
- **验证：** 导航生成检查；`pnpm check:parity`；`pnpm build`；随机抽查每部分首尾页。
- **规模：** L。

### S03 — 本地搜索、答案索引与交互可用性

- [ ] **依赖：** S02、A06、A07。
- **主要文件（≤5）：** `docs/.vitepress/config/{zh,en}.ts`、`docs/.vitepress/theme/index.ts`、`styles.css`、`scripts/site-smoke.mjs`。
- **验收：** 本地搜索中英 UI 文本正确并可找到术语/章节；代码复制、键盘焦点、移动导航和答案返回链接可用。
- **验证：** `node scripts/site-smoke.mjs`；`pnpm build`；真实浏览器桌面/360px/键盘人工检查。
- **规模：** M。

### S04 — 贡献、版本矩阵与人工验证模板

- [ ] **依赖：** S03。
- **主要文件（≤5）：** `README.md`、`CONTRIBUTING.md`、`docs/version-matrix.md`、`reviews/review-template.md`、`reviews/unity-validation.md`。
- **验收：** 从零运行、内容编辑、双语规则、来源复核、版本升级和人工检查步骤可复现；Unity 记录只填真实结果，未执行状态明确。
- **验证：** 新读者按 README 完成冻结安装、检查和预览；链接/命令检查通过。
- **规模：** M。

### C8 — 全站装配检查点

- [ ] **依赖：** S04、A08。
- **验收：** 前言、45 章、答案、8 附录、术语、目录、搜索和语言切换完整；无草稿/占位/孤儿页；桌面和移动阅读闭环。
- **验证：** `pnpm test`；从每部分随机一章执行同页切换；搜索 10 个中英术语；跑完整站点冒烟。

## 9. 发布审计

### R01 — 官方来源、版本与链接审计

- [ ] **依赖：** C8。
- **主要文件（1）：** `reviews/source-and-version-audit.md`；修复回派原任务。
- **验收：** 所有非平凡语言/.NET/生态事实有一手来源；版本与复核日期一致；无“最新版”等漂移表达；内部/外部链接有效。
- **验证：** `pnpm check:content`；自动链接检查；逐项关闭报告问题。
- **规模：** L。

### R02 — 无上下文 F# 专家正确性审阅

- [ ] **依赖：** C8。
- **主要文件（1）：** `reviews/fsharp-expert-review.md`；修复回派原任务。
- **验收：** 新专家不依赖规划上下文审阅语言语义、惯用性、null、相等/比较、CE、异步/取消、互操作和项目；所有高/中问题闭环。
- **验证：** 修复后 `pnpm test`，并由审阅者复核关闭状态。
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
