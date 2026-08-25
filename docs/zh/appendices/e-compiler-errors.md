---
title: "附录 E：常见编译器诊断索引"
description: "依据证据、根因问题和语义正确的最小修复，诊断常见 F# 10 编译器消息。"
translationKey: appendices/e-compiler-errors
kind: appendix
appendix: E
status: complete
verifiedWith:
  fsharp: "10"
  dotnetSdk: "10.0.301"
exampleIds:
  - ch11-value-restriction
  - ch16-wrong-file-order
  - ch17-hidden-representation
exerciseIds: []
termIds: []
sources:
  - id: microsoft-fsharp-compiler-messages
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/compiler-messages/
    checked: "2026-08-25"
  - id: microsoft-fsharp-fs0001
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/compiler-messages/fs0001
    checked: "2026-08-25"
  - id: microsoft-fsharp-fs0025
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/compiler-messages/fs0025
    checked: "2026-08-25"
  - id: microsoft-fsharp-compiler-options
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/compiler-options
    checked: "2026-08-25"
  - id: microsoft-fsharp-formatting
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/style-guide/formatting
    checked: "2026-08-25"
  - id: microsoft-fsharp-automatic-generalization
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/generics/automatic-generalization
    checked: "2026-08-25"
  - id: microsoft-fsharp-signature-files
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/signature-files
    checked: "2026-08-25"
  - id: microsoft-fsharp-null-values
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/values/null-values
    checked: "2026-08-25"
---

# 附录 E：常见编译器诊断索引 {#overview}

编译器编号标识的是诊断类别，并非唯一根因。`FS0039` 可能来自拼写、作用域、缺失引用或错误的 F# 文件顺序。`FS0001` 可能出现在类型冲突终于无法成立的位置，但真正引入决定性约束的是更早的表达式。

本索引是 .NET SDK 10.0.301 下 F# 10 的分诊工具，不替代完整消息。这里列出的每个编号都在 2026-08-25 由该锁定编译器实际产生。诊断措辞、位置、默认严重性，偶尔还有编号本身都可能演进；搜索或改代码前，应先用项目实际选择的 SDK 复现。

## 三十秒阅读一条诊断 {#thirty-seconds}

1. 保留**第一条相关**诊断，包括路径、行、列、严重性、编号和完整消息。
2. 重跑范围最小的真实命令：孤立表达式用 FSI；涉及文件、引用、生成代码与编译设置时用实际项目构建。
3. 检查报告位置两侧推断或声明出的类型。下划线处是编译器证明矛盾的位置，不保证是错误假设最初出现的位置。
4. 修复一个根因后重新构建。后续消息可能只是缺失类型、分隔符或提供者文件引发的级联。
5. 只有在保持 SDK、语言/nullability 设置、引用、文件顺序和诊断编号时，缩减才有效。
6. 把有价值的负面案例变成要求以指定编号失败的 expected-error 夹具；绝不要通过屏蔽这条教训让它“通过”。

典型诊断行具有以下结构：

```text
path/File.fs(12,9): error FS0039: The value or constructor 'name' is not defined.
```

当 `--warnaserror+` 或 `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` 把警告提升为构建失败时，警告仍保留自身 `FS` 编号。格式化器可以规范有效语法，却无法判断程序意图或修复无效语法。

## 快速索引 {#fast-index}

| 编号 | 直接证据 | 首个根因问题 | 常见修复方向 |
|---|---|---|---|
| `FS0001` | 两个类型或约束无法统一 | 哪一侧最初在何处被固定为该类型？ | 修正模型/值、应用形状、分支或有意标注 |
| `FS0010` | 当前语法上下文出现意外词法单元 | 分隔符、关键字、运算符或前一表达式是否未完成？ | 修复最小语法结构，再重新解析 |
| `FS0025` | 模式匹配不穷尽 | 哪个合法输入状态尚未处理？ | 加入真实规则；只有其余状态确实同义时才用 `_` |
| `FS0027` | `<-` 的目标不是可变值 | 可变更新真的是预期状态转换吗？ | 派生新值，或有意识地把狭窄自有状态标为 `mutable` |
| `FS0030` | 一个值仍含弱泛型变量 | 这是单个存储值、泛型变换，还是新值工厂？ | 按生命周期选择具体类型、数据参数或 `unit ->` 工厂 |
| `FS0039` | 名称、类型、命名空间或模块不可用 | 它拼错、越界、未引用，还是稍后才编译？ | 正确限定/open、添加引用，或把提供者排在消费者之前 |
| `FS0041` | 重载解析仍有多个候选 | 领域需要哪种实参或结果类型？ | 标注实参/结果，或经有类型的函数调用 |
| `FS0058` | 词法单元越出布局线或缩进结构无效 | 哪个外层结构建立了当前缩进上下文？ | 完成该结构并保持一致缩进 |
| `FS0072` | 接收者类型尚未知就发生成员查找 | 这个函数边界的接收者契约是什么？ | 标注接收者，或暴露操作而非未知成员 |
| `FS0748` | `return` 出现在计算表达式之外 | 这是普通函数，还是由 builder 控制的工作流？ | 直接使用末尾表达式，或进入预期计算表达式 |
| `FS0764` | 构造记录时遗漏必需字段 | 这是完整新值，还是基于旧值的更新？ | 提供全部字段，或有意识地使用记录复制更新 |
| `FS0800` | 把类型名称当成了隐藏构造方式 | `.fsi` 或访问修饰符是否隐藏了表示？ | 调用公共构造/智能构造函数，不要穿透抽象 |
| `FS3261` | nullable 分析拒绝把 null 用作非空类型 | 这条边界真的允许 null 吗？ | 声明 `| null` 并收窄/守卫，或移除无效 null |

先结合编号阅读完整消息，再使用以下各节。不要只因能让当前行安静下来，就添加强制转换、通配符、`mutable`、`#nowarn` 或 nullable 退出设置。

## 语法与缩进：FS0010 和 FS0058 {#syntax-indentation}

`FS0010` 表示解析器遇到无法继续当前结构的符号或关键字。该词法单元可能只是暴露了紧邻其前缺少的 `)`、`]`、`}`、`then`、`with`、`=` 或表达式。先配对分隔符并检查最小外层绑定，不要全局乱改缩进。

`FS0058` 与 F# 越界规则相关。在锁定探针中，未完成的 `let answer =` 到达输入末尾时产生 `FS0058`，而 `let answer = )` 产生 `FS0010`。因此看似相近的破损源码，会根据解析器最先掌握的事实得到不同编号。

先修复结构，在它能解析后再运行 Fantomas，并保留严格缩进。为保留含糊布局而关闭严格缩进会改变语言契约，不是通用修复。

## 类型方程：FS0001 {#fs0001}

F# 推断会收集“此分支返回 `int`”“该实参必须为 `string`”一类方程。`FS0001` 表示这些方程不能同时成立。当前编译器用“标注为 `int` 却由字符串初始化”的值复现了它。

常见来源包括：

- `if` 或 `match` 各分支返回不兼容类型；
- 需要元组式调用却传成柯里化参数，或反之；
- 部分应用在需要值的位置返回了函数；
- 更早的数字/字符串操作固定了原本泛型的参数；
- 某类型不具备相等、比较、计量单位或其他约束；
- 把 `unit` 与计算结果混淆。

逐字阅读 expected 与 actual 类型，沿完整签名追踪重复的类型变量，并检查最早约束它们的使用。若期望类型才是真实契约，就修正值；若值正确，就修正契约。只有转换本身具有领域含义时，强制转换才合适。

## 重载与成员查找：FS0041 和 FS0072 {#overloads-members}

`FS0041` 表示编译器知道多个适用重载，却没有足够类型信息选出唯一项。只绑定 `System.Math.Abs` 而不提供实参时，实际复现出了 `int`、`float`、`decimal` 等候选。应在调用附近补充领域所需的实参或结果类型，不要只为通过编译任意挑一个重载。

未知类型参数立即通过 `.Length` 使用时产生了 `FS0072`。F# 通常从左向右推断，不能凭空发明“任何拥有 Length 的东西”这种结构契约。把参数标注为 `string`、数组、集合接口或真实领域类型。若多个无关类型都需要该操作，应接收投影函数或有意义的接口，而不是依赖成员名碰巧相同。

## 绑定生命周期与可变性：FS0027 和 FS0030 {#bindings}

`FS0027` 是 `<-` 指向不可变绑定的直接证据。先问预期操作是否其实是变换：`let updated = ...` 与记录复制更新往往表达得更准确。若循环、缓冲区、缓存或互操作 API 确实拥有变化状态，只让那条狭窄绑定可变。

`FS0030` 更微妙。本仓库的真实夹具创建了一个元素类型仍为弱泛型的可变数组值：

<<< @/../examples/expected-errors/ch11-value-restriction.fsx{fsharp:line-numbers} [ch11-value-restriction.fsx — 预期错误]

应按语义在三种修复中选择：

| 意图 | 修复 | 后果 |
|---|---|---|
| 一份共享、元素类型唯一的值 | 添加具体标注或约束性使用 | 所有读取者共享该存储与类型 |
| 一项泛型变换 | 把数据暴露为普通参数 | 不再存在含糊的已存储泛型值 |
| 每次请求都得到新存储 | 添加 `unit` 参数并在内部构造 | 每次调用分配不同值 |

添加 `()` 不是标点修补；它会改变生命周期与分配。[第 11 章](../part-02/ch-11-generics-constraints)会展开这一区别。

## 模式与记录：FS0025 和 FS0764 {#patterns-records}

`FS0025` 报告某个合法值未被模式匹配覆盖。警告视为错误时，新增联合案例会让不完整的决策代码先失败，而不是静默选择行为。请添加显式分支并决定其规则。只有剩余案例确实共享一项稳定策略时才适合通配符，否则它会丢掉编译器未来的帮助。

活动模式和带守卫的模式可能让覆盖分析较保守，因此要区分“编译器无法证明覆盖”与“确实遗漏领域案例”。若证明不实际，应重构复杂匹配或添加显式最终策略。

`FS0764` 表示具名记录构造遗漏字段；锁定探针从 `Person` 遗漏了 `Age`。记录值必须完整。新值要提供所有字段；若语义是更新，则从已知值开始使用 `{ existing with Field = value }`。不要为满足构造而加入没有意义的默认值。

## 计算表达式：FS0748 {#computation-expressions}

`return`、`return!`、`let!`、`do!` 和 `yield` 的含义由计算表达式 builder 提供。探针 `let invalid = return 1` 产生了 `FS0748`。普通函数以最后一个表达式为结果，所以应写 `let valid = 1`。若确实需要异步、task、sequence、result 或其他 builder 语义，应把操作放入相应 builder，并只使用它实现的语法。

只为消除错误而把 `return` 移入 `task {}` 会改变返回类型和执行语义。应先选择工作流。

## 名称、引用与文件顺序：FS0039 {#names-file-order}

按以下顺序检查 `FS0039`：

1. 拼写与大小写；
2. 词法作用域，以及局部值是否已在使用前定义；
3. 限定名称或正确的 `open` 声明；
4. 项目/包/程序集引用与目标框架；
5. 生成源码是否真正运行；
6. F# `<Compile>` 顺序——提供者 `.fsi`/`.fs` 必须早于消费者。

仓库中的错误顺序项目有意把 `Workflow.fs` 排在 `Domain.fs` 之前：

<<< @/../examples/expected-errors/ch16-file-order/Ch16WrongOrder.fsproj{xml:line-numbers} [Ch16WrongOrder.fsproj — 预期错误]

首个缺失命名空间会引发更多未知类型和值。应修复第一条依赖，而不是逐条修级联。`open` 只会缩短已有名称；它不会添加程序集引用，也不会让后面的文件提前可用。详见[第 16 章](../part-03/ch-16-modules-namespaces-projects)。

## 隐藏表示：FS0800 {#hidden-representation}

第 17 章的独立消费者试图构造被库的 `.fsi` 文件省略的联合案例：

<<< @/../examples/expected-errors/ch17-hidden-representation/Consumer.fs{fsharp:line-numbers} [Consumer.fs — 预期错误]

F# 10 给出简短的 `FS0800: Invalid use of a type name`。周围签名补足了缺失上下文：`Capacity` 作为抽象类型公开，但其表示不是公共构造函数。应调用 `Capacity.create` 并处理其验证结果。改变访问级别或删除签名是在破坏不变量，而不是修复消费者。

## Nullable 边界：FS3261 {#nullable-boundaries}

启用 nullable 检查后，把 `null` 传给推断为非空 `string` 的参数实际复现了 `FS3261`。应决定边界契约：

- 若 null 无效，保留 `string`，在公共边缘拒绝不受信任的调用者，并移除内部无效调用；
- 若 null 是真实 CLR 输入可能性，声明 `string | null`，再经模式匹配或守卫收窄后当作 `string` 使用；
- 若缺失在验证后成为领域值，只转换一次为 `option` 或领域联合。

不要因一条互操作边界诚实地面对 null，就关闭整个项目的 nullable 检查。应精确描述该边界。第 16、19 与 27 章提供了编译后的例子。

## 实际运行了什么 {#verification}

2026-08-25，`dotnet --version` 选中 SDK 10.0.301，FSI 报告 F# 10。小型探针以 `--warnaserror+ --checknulls+` 运行；每一个都以非零状态退出，并产生表中对应编号。仓库清单持续检查可复现的 expected-error 夹具：

```console
pnpm check:examples
```

该关卡要求第 6 章非尾递归项目以 `FS3569` 失败、第 11 章脚本以 `FS0030` 失败、第 16 章项目以 `FS0039` 失败、第 17 章外部消费者以 `FS0800` 失败。若夹具意外编译、产生错误编号、超时或未登记，检查都会失败。其余一两行探针只是临时证据，不是额外生产示例。

## 求助前应提供什么 {#before-help}

请提供仍能保留失败的最小源码、完整的第一条诊断、`dotnet --version`、精确命令、影响语言/nullability/警告的项目属性以及相关文件/引用顺序，并说明期望的类型或行为。只截取带下划线词法单元的截图，会移除区分推断、作用域、项目和工具问题所需的大多数证据。

## 资料来源 {#sources}

- [Microsoft Learn：F# 编译器消息](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/compiler-messages/)
- [Microsoft Learn：编译器错误 FS0001](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/compiler-messages/fs0001)
- [Microsoft Learn：编译器警告 FS0025](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/compiler-messages/fs0025)
- [Microsoft Learn：F# 编译器选项](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/compiler-options)
- [Microsoft Learn：F# 格式指南](https://learn.microsoft.com/en-us/dotnet/fsharp/style-guide/formatting)
- [Microsoft Learn：自动泛化](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/generics/automatic-generalization)
- [Microsoft Learn：F# 签名文件](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/signature-files)
- [Microsoft Learn：F# 的 null 值与可空检查](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/values/null-values)
