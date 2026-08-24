---
title: "附录 F：F# 中英文术语表"
description: "由双语术语目录生成的自足术语表，提供稳定锚点以及每个术语首次教学章节的链接。"
translationKey: glossary
kind: glossary
status: complete
exampleIds: []
exerciseIds: []
termIds: []
sources: []
---

# 附录 F：F# 中英文术语表 {#overview}

本术语表用中文定义全书的 F# 词汇，并记录首选英文对应词。每项定义都能只靠中文独立理解，不要求读者先会英文。条目中的稳定标识供内容元数据使用；即使日后改进显示用词，它也保持不变。

“首次讲解”指阅读顺序中最早在 frontmatter 声明该术语标识的章节；它是教学入口，并不声称这个词此前从未在一般叙述中出现。可沿链接查看动机、示例和周围概念。

条目与链接由 `docs/terminology.json` 和章节元数据生成。请修改这些源，再运行 `pnpm generate:glossary`；`pnpm check:content` 会拒绝过期的生成页面。

## 如何使用本术语表 {#how-to-use}

可以搜索可见的中文或英文术语，通过稳定锚点直接链接某项，也可以按部分阅读，以原学习顺序复习概念。定义说明本书中的用法；所链接章节提供操作细节。

## 第 1 部分 · 基础：值、函数与控制流 {#part-1}

### 表达式 · expression {#expression}

一段会被求值，并在正常完成时产生值的代码。

**首次讲解:** [第 1 章：第一次 F# 会话](./part-01/ch-01-first-session#overview) · **稳定标识:** `expression`

### F# Interactive · F# Interactive {#fsharp-interactive}

随 .NET SDK 提供的 F# 交互环境；它以读取、求值、打印、循环的方式执行提交，也可以运行 F# 脚本。

**首次讲解:** [第 1 章：第一次 F# 会话](./part-01/ch-01-first-session#overview) · **稳定标识:** `fsharp-interactive`

### F# 脚本 · F# script {#fsharp-script}

扩展名为 .fsx、通常由 F# Interactive 直接执行的源文件，适合实验、自动化和小型工具。

**首次讲解:** [第 1 章：第一次 F# 会话](./part-01/ch-01-first-session#overview) · **稳定标识:** `fsharp-script`

### 字面量 · literal {#literal}

在源代码中直接写出的值表示，例如 40、true、"hello" 和 1.5m。

**首次讲解:** [第 1 章：第一次 F# 会话](./part-01/ch-01-first-session#overview) · **稳定标识:** `literal`

### unit · unit {#unit}

只有一个值 () 的类型，用于表达某个表达式没有需要传递给后续计算的特定结果。

**首次讲解:** [第 1 章：第一次 F# 会话](./part-01/ch-01-first-session#overview) · **稳定标识:** `unit`

### 值 · value {#value}

求值正常完成时得到、并可供其他表达式使用的结果；函数本身也是值。

**首次讲解:** [第 1 章：第一次 F# 会话](./part-01/ch-01-first-session#overview) · **稳定标识:** `value`

### 绑定 · binding {#binding}

由 let 等模式建立的名称与值之间的关联；它不是一个可随意改写的存储槽。

**首次讲解:** [第 2 章：值、绑定与表达式](./part-01/ch-02-values-bindings-expressions#overview) · **稳定标识:** `binding`

### 不可变性 · immutability {#immutability}

值建立后不被原地改写的性质；对绑定而言，它表示名称不会被重新赋给另一个值，但不自动保证所引用对象的内部也不可变。

**首次讲解:** [第 2 章：值、绑定与表达式](./part-01/ch-02-values-bindings-expressions#overview) · **稳定标识:** `immutability`

### 数值转换 · numeric conversion {#numeric-conversion}

显式地从一种数值类型产生另一种数值类型的值，例如用 decimal 由 int 得到 decimal。

**首次讲解:** [第 2 章：值、绑定与表达式](./part-01/ch-02-values-bindings-expressions#overview) · **稳定标识:** `numeric-conversion`

### 遮蔽 · shadowing {#shadowing}

在内层或后续作用域中建立同名的新绑定，使旧绑定在该范围内无法再由这个名称访问；它不是修改旧值。

**首次讲解:** [第 2 章：值、绑定与表达式](./part-01/ch-02-values-bindings-expressions#overview) · **稳定标识:** `shadowing`

### 类型标注 · type annotation {#type-annotation}

源码中显式写出的类型约束，用来记录意图或补足编译器无法可靠推断的上下文。

**首次讲解:** [第 2 章：值、绑定与表达式](./part-01/ch-02-values-bindings-expressions#overview) · **稳定标识:** `type-annotation`

### 类型推断 · type inference {#type-inference}

编译器根据表达式的使用方式和上下文推导静态类型，而不要求处处写出类型标注。

**首次讲解:** [第 2 章：值、绑定与表达式](./part-01/ch-02-values-bindings-expressions#overview) · **稳定标识:** `type-inference`

### 匿名函数 · anonymous function {#anonymous-function}

用 fun 参数 -> 主体 表达式直接创建、无需先给函数本身命名的函数值。

**首次讲解:** [第 3 章：函数也是值](./part-01/ch-03-functions-as-values#overview) · **稳定标识:** `anonymous-function`

### 实参 · argument {#argument}

调用函数时实际提供给参数的值或表达式。

**首次讲解:** [第 3 章：函数也是值](./part-01/ch-03-functions-as-values#overview) · **稳定标识:** `argument`

### 自动泛化 · automatic generalization {#automatic-generalization}

编译器在安全且不依赖具体类型时，把推断类型中的未知类型提升为可由多种类型实例化的类型参数。

**首次讲解:** [第 3 章：函数也是值](./part-01/ch-03-functions-as-values#overview) · **稳定标识:** `automatic-generalization`

### 闭包 · closure {#closure}

函数值连同它从定义位置捕获、并在以后调用时仍需使用的周围值。

**首次讲解:** [第 3 章：函数也是值](./part-01/ch-03-functions-as-values#overview) · **稳定标识:** `closure`

### 柯里化 · currying {#currying}

把多参数计算表示为连续接收单个参数并返回下一函数的形式；F# 的 let 绑定函数通常使用这种表示。

**首次讲解:** [第 3 章：函数也是值](./part-01/ch-03-functions-as-values#overview) · **稳定标识:** `currying`

### 函数 · function {#function}

接收输入并计算结果的值；在 F# 中，函数可以像其他值一样被绑定、传递和返回。

**首次讲解:** [第 3 章：函数也是值](./part-01/ch-03-functions-as-values#overview) · **稳定标识:** `function`

### 函数应用 · function application {#function-application}

向函数值提供实参并求值其函数主体，以产生结果。

**首次讲解:** [第 3 章：函数也是值](./part-01/ch-03-functions-as-values#overview) · **稳定标识:** `function-application`

### 高阶函数 · higher-order function {#higher-order-function}

至少接收一个函数值作为参数，或把函数值作为结果返回的函数。

**首次讲解:** [第 3 章：函数也是值](./part-01/ch-03-functions-as-values#overview) · **稳定标识:** `higher-order-function`

### 形参 · parameter {#parameter}

函数定义中用于接收调用方实参的名称或模式。

**首次讲解:** [第 3 章：函数也是值](./part-01/ch-03-functions-as-values#overview) · **稳定标识:** `parameter`

### 部分应用 · partial application {#partial-application}

只向柯里化函数提供一部分参数，从而得到一个等待其余参数的新函数。

**首次讲解:** [第 3 章：函数也是值](./part-01/ch-03-functions-as-values#overview) · **稳定标识:** `partial-application`

### 元组 · tuple {#tuple}

按固定位置组合若干值的类型；组成部分可以具有不同类型，类型签名中用星号连接。

**首次讲解:** [第 3 章：函数也是值](./part-01/ch-03-functions-as-values#overview) · **稳定标识:** `tuple`

### 穷尽性 · exhaustiveness {#exhaustiveness}

一组模式覆盖输入类型所有可能形状的性质；无法穷尽时，某些输入可能没有匹配分支。

**首次讲解:** [第 4 章：分支与基本模式](./part-01/ch-04-branching-patterns#overview) · **稳定标识:** `exhaustiveness`

### 守卫 · guard {#guard}

模式初步匹配后才求值的 when 布尔条件；条件为 false 时继续尝试后续规则。

**首次讲解:** [第 4 章：分支与基本模式](./part-01/ch-04-branching-patterns#overview) · **稳定标识:** `guard`

### 列表 · list {#list}

由同一类型元素组成的有序不可变单向链式集合；空表为 []，头与尾可用 :: 构造或解构。

**首次讲解:** [第 4 章：分支与基本模式](./part-01/ch-04-branching-patterns#overview) · **稳定标识:** `list`

### 模式 · pattern {#pattern}

用来检查输入结构、分解组成部分并可为其建立局部绑定的形状规则。

**首次讲解:** [第 4 章：分支与基本模式](./part-01/ch-04-branching-patterns#overview) · **稳定标识:** `pattern`

### 模式匹配 · pattern matching {#pattern-matching}

按值的形状选择匹配分支，并可同时为其组成部分建立绑定。

**首次讲解:** [第 4 章：分支与基本模式](./part-01/ch-04-branching-patterns#overview) · **稳定标识:** `pattern-matching`

### 通配符模式 · wildcard pattern {#wildcard-pattern}

写作 _、匹配任何输入但不为该输入建立可用名称的模式。

**首次讲解:** [第 4 章：分支与基本模式](./part-01/ch-04-branching-patterns#overview) · **稳定标识:** `wildcard-pattern`

### 立即求值 · eager evaluation {#eager-evaluation}

在操作被调用时就计算结果，而不是等到以后枚举或请求某项时才计算。

**首次讲解:** [第 5 章：列表、管道与数据流](./part-01/ch-05-lists-pipelines#overview) · **稳定标识:** `eager-evaluation`

### 效果 · effect {#effect}

求值期间发生、不能只由返回值描述的可观察行为，例如输出、写文件或修改状态。

**首次讲解:** [第 5 章：列表、管道与数据流](./part-01/ch-05-lists-pipelines#overview) · **稳定标识:** `effect`

### 可变绑定 · mutable binding {#mutable-binding}

用 let mutable 建立、其存储位置可随后通过 <- 更新的绑定。

**首次讲解:** [第 5 章：列表、管道与数据流](./part-01/ch-05-lists-pipelines#overview) · **稳定标识:** `mutable-binding`

### option · option {#option}

用 Some value 表示存在值、用 None 表示没有值的 F# 类型；完整建模规则在第 9 章展开。

**首次讲解:** [第 5 章：列表、管道与数据流](./part-01/ch-05-lists-pipelines#overview) · **稳定标识:** `option`

### 管道 · pipeline {#pipeline}

用 |> 把左侧结果作为最末参数交给右侧函数调用，从而按数据流顺序书写变换。

**首次讲解:** [第 5 章：列表、管道与数据流](./part-01/ch-05-lists-pipelines#overview) · **稳定标识:** `pipeline`

### 累加器 · accumulator {#accumulator}

在递归或折叠的每一步携带到下一步、表示截至当前已完成结果的值。

**首次讲解:** [第 6 章：递归、尾调用与折叠](./part-01/ch-06-recursion-folds#overview) · **稳定标识:** `accumulator`

### 折叠 · fold {#fold}

按确定顺序用折叠函数把集合元素逐项并入累加器，最终返回累加状态的高阶操作。

**首次讲解:** [第 6 章：递归、尾调用与折叠](./part-01/ch-06-recursion-folds#overview) · **稳定标识:** `fold`

### 递归 · recursion {#recursion}

函数直接或间接调用自身，以更小或更接近终止条件的问题继续计算。

**首次讲解:** [第 6 章：递归、尾调用与折叠](./part-01/ch-06-recursion-folds#overview) · **稳定标识:** `recursion`

### 结构递归 · structural recursion {#structural-recursion}

按数据类型的构造形状分支，并在递归分支中处理结构上更小组成部分的递归。

**首次讲解:** [第 6 章：递归、尾调用与折叠](./part-01/ch-06-recursion-folds#overview) · **稳定标识:** `structural-recursion`

### 尾调用 · tail call {#tail-call}

函数分支返回前执行的最后操作调用；调用结果无需再由当前栈帧继续加工。

**首次讲解:** [第 6 章：递归、尾调用与折叠](./part-01/ch-06-recursion-folds#overview) · **稳定标识:** `tail-call`

### 尾递归 · tail recursion {#tail-recursion}

所有递归路径都把递归调用置于尾位置的递归形式，使编译器有机会消除递归栈增长。

**首次讲解:** [第 6 章：递归、尾调用与折叠](./part-01/ch-06-recursion-folds#overview) · **稳定标识:** `tail-recursion`

## 第 2 部分 · 用类型建模 {#part-2}

### 匿名记录 · anonymous record {#anonymous-record}

无需预先声明名称、由一组精确字段标签和类型确定形状的记录值。

**首次讲解:** [第 7 章：记录、更新、相等与比较](./part-02/ch-07-records-equality#overview) · **稳定标识:** `anonymous-record`

### 哈希码 · hash code {#hash-code}

由相等语义一致地导出的整数摘要，用于哈希数据结构定位候选项；不同值仍可能产生同一码。

**首次讲解:** [第 7 章：记录、更新、相等与比较](./part-02/ch-07-records-equality#overview) · **稳定标识:** `hash-code`

### 记录 · record {#record}

由命名字段组成的乘积类型；普通 F# 记录默认不可变，并在组成字段支持时自动获得结构相等与比较。

**首次讲解:** [第 7 章：记录、更新、相等与比较](./part-02/ch-07-records-equality#overview) · **稳定标识:** `record`

### 引用身份 · reference identity {#reference-identity}

两个引用是否指向同一个运行时对象的关系，与对象内容是否结构相等分开。

**首次讲解:** [第 7 章：记录、更新、相等与比较](./part-02/ch-07-records-equality#overview) · **稳定标识:** `reference-identity`

### 结构比较 · structural comparison {#structural-comparison}

按组成部分的既定顺序递归比较复合值而得到的排序关系，要求所有相关组成类型支持比较。

**首次讲解:** [第 7 章：记录、更新、相等与比较](./part-02/ch-07-records-equality#overview) · **稳定标识:** `structural-comparison`

### 结构相等 · structural equality {#structural-equality}

递归比较复合值对应组成部分而判断内容是否相等的语义，而不是检查它们是否为同一对象。

**首次讲解:** [第 7 章：记录、更新、相等与比较](./part-02/ch-07-records-equality#overview) · **稳定标识:** `structural-equality`

### 可辨识联合 · discriminated union {#discriminated-union}

一种由若干具名用例组成的类型；一个值恰属其中一个用例，每个用例还可以携带数据。

**首次讲解:** [第 8 章：可辨识联合与状态建模](./part-02/ch-08-discriminated-unions#overview) · **稳定标识:** `discriminated-union`

### 联合案例 · union case {#union-case}

可辨识联合中一种有名称的可能形状，可不携带数据，也可携带只对该形状有意义的字段。

**首次讲解:** [第 8 章：可辨识联合与状态建模](./part-02/ch-08-discriminated-unions#overview) · **稳定标识:** `union-case`

### Result · Result {#result}

用 Ok value 表示成功、用 Error error 表示带有已建模原因的预期失败的 F# 类型。

**首次讲解:** [第 9 章：缺失与预期失败](./part-02/ch-09-option-result#overview) · **稳定标识:** `result`

### 短路 · short-circuit {#short-circuit}

组合计算时，一旦遇到无法继续的 None 或 Error，便直接保留该结果而不运行后续依赖步骤。

**首次讲解:** [第 9 章：缺失与预期失败](./part-02/ch-09-option-result#overview) · **稳定标识:** `short-circuit`

### 递归类型 · recursive type {#recursive-type}

在自身定义的组成部分中再次引用自身、从而能表示有限嵌套结构的类型。

**首次讲解:** [第 10 章：递归类型与结构递归](./part-02/ch-10-recursive-types#overview) · **稳定标识:** `recursive-type`

### 比较约束 · comparison constraint {#comparison-constraint}

写作 'T : comparison，要求类型参数支持 F# 的泛型比较与排序操作。

**首次讲解:** [第 11 章：泛型、约束与度量单位](./part-02/ch-11-generics-constraints#overview) · **稳定标识:** `comparison-constraint`

### 相等约束 · equality constraint {#equality-constraint}

写作 'T : equality，要求类型参数支持 F# 的泛型相等运算。

**首次讲解:** [第 11 章：泛型、约束与度量单位](./part-02/ch-11-generics-constraints#overview) · **稳定标识:** `equality-constraint`

### 泛型类型参数 · generic type parameter {#generic-type-parameter}

在一个定义中代表尚未指定的类型、并在每次具体使用时由一致类型实参替换的类型级参数。

**首次讲解:** [第 11 章：泛型、约束与度量单位](./part-02/ch-11-generics-constraints#overview) · **稳定标识:** `generic-type-parameter`

### 静态解析类型参数 · statically resolved type parameter {#statically-resolved-type-parameter}

写作 ^T、在内联调用点解析并可携带成员约束的 F# 类型参数；它不同于普通的 'T 泛型参数。

**首次讲解:** [第 11 章：泛型、约束与度量单位](./part-02/ch-11-generics-constraints#overview) · **稳定标识:** `statically-resolved-type-parameter`

### 度量单位 · unit of measure {#unit-of-measure}

附着在受支持数值类型上的编译期类型标注，用于静态检查量纲关系，并在运行时擦除。

**首次讲解:** [第 11 章：泛型、约束与度量单位](./part-02/ch-11-generics-constraints#overview) · **稳定标识:** `unit-of-measure`

### 值限制 · value restriction {#value-restriction}

自动泛化只允许安全形状的绑定；带未解析类型变量而不可泛化的值绑定会被拒绝，以免同一存储被不安全地当作多种类型。

**首次讲解:** [第 11 章：泛型、约束与度量单位](./part-02/ch-11-generics-constraints#overview) · **稳定标识:** `value-restriction`

### 访问控制 · access control {#access-control}

用 public、internal、private 或签名文件规定哪些代码位置能够使用某个程序实体的机制。

**首次讲解:** [第 12 章：让非法状态无法表示](./part-02/ch-12-making-illegal-states-unrepresentable#overview) · **稳定标识:** `access-control`

### 不变量 · invariant {#invariant}

对一个受保护类型的每个可公开获得值都应持续成立的条件。

**首次讲解:** [第 12 章：让非法状态无法表示](./part-02/ch-12-making-illegal-states-unrepresentable#overview) · **稳定标识:** `invariant`

### 私有表示 · private representation {#private-representation}

调用方能够使用类型本身，却不能直接使用其底层联合案例、记录字段构造或其他表示细节的设计。

**首次讲解:** [第 12 章：让非法状态无法表示](./part-02/ch-12-making-illegal-states-unrepresentable#overview) · **稳定标识:** `private-representation`

### 签名文件 · signature file {#signature-file}

扩展名为 .fsi、位于对应 .fs 实现之前并声明其他文件可见公开表面的 F# 文件。

**首次讲解:** [第 12 章：让非法状态无法表示](./part-02/ch-12-making-illegal-states-unrepresentable#overview) · **稳定标识:** `signature-file`

### 智能构造函数 · smart constructor {#smart-constructor}

在产生受保护领域值之前执行验证或规范化，并以显式返回类型报告拒绝原因的函数。

**首次讲解:** [第 12 章：让非法状态无法表示](./part-02/ch-12-making-illegal-states-unrepresentable#overview) · **稳定标识:** `smart-constructor`

## 第 3 部分 · 组合与程序结构 {#part-3}

### 函数组合 · function composition {#function-composition}

把前一个函数的输出连接到后一个函数的输入，从多个函数值得到一个新函数值。

**首次讲解:** [第 13 章：组合、参数顺序与管道 API](./part-03/ch-13-composition-pipeline-api#overview) · **稳定标识:** `function-composition`

### 数组 · array {#array}

具有固定长度、连续存储且元素可原地更新的同类型有序集合；改变长度需要创建新数组。

**首次讲解:** [第 14 章：集合选择与求值模型](./part-03/ch-14-collections-evaluation#overview) · **稳定标识:** `array`

### 延迟求值 · deferred evaluation {#deferred-evaluation}

把产生值或执行工作的时机推迟到消费者请求结果时；工作是否重复取决于具体来源与是否缓存。

**首次讲解:** [第 14 章：集合选择与求值模型](./part-03/ch-14-collections-evaluation#overview) · **稳定标识:** `deferred-evaluation`

### 枚举 · enumeration {#enumeration}

消费者通过枚举器依次请求集合元素的遍历过程；每次枚举所执行的工作由具体来源决定。

**首次讲解:** [第 14 章：集合选择与求值模型](./part-03/ch-14-collections-evaluation#overview) · **稳定标识:** `enumeration`

### 映射表（Map） · map {#map}

按键的 F# 泛型比较顺序组织键值绑定的不可变树；每个键至多对应一个值。

**首次讲解:** [第 14 章：集合选择与求值模型](./part-03/ch-14-collections-evaluation#overview) · **稳定标识:** `map`

### 序列 · sequence {#sequence}

`seq<'T>` 是 `IEnumerable<'T>` 的类型缩写，描述如何枚举同类型元素，但本身不保证缓存、纯度或可重复遍历。

**首次讲解:** [第 14 章：集合选择与求值模型](./part-03/ch-14-collections-evaluation#overview) · **稳定标识:** `sequence`

### 集合（Set） · set {#set}

按元素的 F# 泛型比较顺序组织唯一元素的不可变树。

**首次讲解:** [第 14 章：集合选择与求值模型](./part-03/ch-14-collections-evaluation#overview) · **稳定标识:** `set`

### 活动模式 · active pattern {#active-pattern}

由函数实现并以具名模式形式使用的输入视图，可在匹配时对值进行分类或解构。

**首次讲解:** [第 15 章：活动模式与领域匹配边界](./part-03/ch-15-active-patterns#overview) · **稳定标识:** `active-pattern`

### 完整活动模式 · complete active pattern {#complete-active-pattern}

为每个输入都返回某个具名案例的活动模式；多案例形式会把整个输入空间划分为若干部分。

**首次讲解:** [第 15 章：活动模式与领域匹配边界](./part-03/ch-15-active-patterns#overview) · **稳定标识:** `complete-active-pattern`

### 参数化活动模式 · parameterized active pattern {#parameterized-active-pattern}

在最终被匹配输入之前接收额外实参、从而为调用位置特化识别规则的单案例活动模式。

**首次讲解:** [第 15 章：活动模式与领域匹配边界](./part-03/ch-15-active-patterns#overview) · **稳定标识:** `parameterized-active-pattern`

### 部分活动模式 · partial active pattern {#partial-active-pattern}

只识别输入空间一部分并能以不匹配告终的单案例活动模式，名称列表以通配符案例结尾。

**首次讲解:** [第 15 章：活动模式与领域匹配边界](./part-03/ch-15-active-patterns#overview) · **稳定标识:** `partial-active-pattern`

### 程序集 · assembly {#assembly}

由 .NET 编译产生并作为部署、加载与引用单元的 .dll 或 .exe，以及其中的元数据和代码。

**首次讲解:** [第 16 章：模块、命名空间、项目与编译设置](./part-03/ch-16-modules-namespaces-projects#overview) · **稳定标识:** `assembly`

### 编译顺序 · compilation order {#compilation-order}

F# 源文件提供给编译器的先后次序；通常后面的文件可以使用前面定义，反向依赖则不可用。

**首次讲解:** [第 16 章：模块、命名空间、项目与编译设置](./part-03/ch-16-modules-namespaces-projects#overview) · **稳定标识:** `compilation-order`

### 模块 · module {#module}

把相关类型、值与函数组织在同一具名范围内的 F# 构造；模块自身可以位于命名空间或另一模块中。

**首次讲解:** [第 16 章：模块、命名空间、项目与编译设置](./part-03/ch-16-modules-namespaces-projects#overview) · **稳定标识:** `module`

### 命名空间 · namespace {#namespace}

可跨文件与程序集组织类型和模块的具名容器；它不能直接包含 F# 值或函数。

**首次讲解:** [第 16 章：模块、命名空间、项目与编译设置](./part-03/ch-16-modules-namespaces-projects#overview) · **稳定标识:** `namespace`

### 可空引用类型 · nullable reference type {#nullable-reference-type}

启用 F# 空值检查后，用 `T | null` 明确允许 null 的引用类型标注；它是编译期契约，不是运行时包装器。

**首次讲解:** [第 16 章：模块、命名空间、项目与编译设置](./part-03/ch-16-modules-namespaces-projects#overview) · **稳定标识:** `nullable-reference-type`

### open 声明 · open declaration {#open-declaration}

让某命名空间或模块中的名称在后续范围可用短名称引用的声明；它不加载代码，也不改变访问控制。

**首次讲解:** [第 16 章：模块、命名空间、项目与编译设置](./part-03/ch-16-modules-namespaces-projects#overview) · **稳定标识:** `open-declaration`

### 项目文件 · project file {#project-file}

描述目标框架、编译项顺序、引用和构建属性的 MSBuild XML 文件；F# 项目通常使用 .fsproj 扩展名。

**首次讲解:** [第 16 章：模块、命名空间、项目与编译设置](./part-03/ch-16-modules-namespaces-projects#overview) · **稳定标识:** `project-file`

### 抽象表示 · abstract representation {#abstract-representation}

签名只公开类型名称而省略联合案例、记录字段或其他实现形状，使消费者能使用该类型的值却不能依赖其底层表示。

**首次讲解:** [第 17 章：签名、访问控制与面向 F# 的 API](./part-03/ch-17-signatures-encapsulation#overview) · **稳定标识:** `abstract-representation`

### 公共 API 表面 · public API surface {#public-api-surface}

组件有意向消费者公开并承诺支持的一组类型、案例、函数、成员及其签名。

**首次讲解:** [第 17 章：签名、访问控制与面向 F# 的 API](./part-03/ch-17-signatures-encapsulation#overview) · **稳定标识:** `public-api-surface`

### 计算表达式 · computation expression {#computation-expression}

由构建器成员解释的 F# 语法，用来组合带有特定上下文或控制流的计算。

**首次讲解:** [第 18 章：显式工作流组合与验证累积](./part-03/ch-18-workflow-validation#overview) · **稳定标识:** `computation-expression`

### 验证错误累积 · validation accumulation {#validation-accumulation}

对彼此独立的检查全部求值，并把各自的失败按明确顺序合并到一个错误集合中的组合策略。

**首次讲解:** [第 18 章：显式工作流组合与验证累积](./part-03/ch-18-workflow-validation#overview) · **稳定标识:** `validation-accumulation`
