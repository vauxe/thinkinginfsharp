---
title: "附录 F：F# 术语表"
description: "自包含的 F# 术语表，并提供每个术语首次讲解章节的链接。"
translationKey: glossary
---

# 附录 F：F# 术语表 {#overview}

本术语表定义中文版使用的 F# 术语。

“首次讲解”指阅读顺序中最早直接教授该概念的章节。可沿链接查看动机、示例和相关概念。

## 如何使用本术语表 {#how-to-use}

你可以搜索术语、直接打开某个锚点，也可以按章节顺序复习概念。这里给出本书采用的定义；链接章节解释具体用法。

## 第 1 部分 · 基础：值、函数与控制流 {#part-1}

### 表达式 {#expression}

一段会被求值，并在正常完成时产生值的代码。

**首次讲解:** [第 1 章：第一次 F# 会话](./part-01/ch-01-first-session#overview)

### F# Interactive {#fsharp-interactive}

随 .NET SDK 提供的 F# 交互环境；它通过“读取—求值—输出”循环（REPL）执行输入，也可以运行 F# 脚本。

**首次讲解:** [第 1 章：第一次 F# 会话](./part-01/ch-01-first-session#overview)

### F# 脚本 {#fsharp-script}

扩展名为 .fsx、通常由 F# Interactive 直接执行的源文件，适合实验、自动化和小型工具。

**首次讲解:** [第 1 章：第一次 F# 会话](./part-01/ch-01-first-session#overview)

### 字面量 {#literal}

在源代码中直接写出的值表示，例如 40、true、"hello" 和 1.5m。

**首次讲解:** [第 1 章：第一次 F# 会话](./part-01/ch-01-first-session#overview)

### unit {#unit}

只有一个值 `()` 的类型，表示表达式没有需要传给后续计算的具体结果。

**首次讲解:** [第 1 章：第一次 F# 会话](./part-01/ch-01-first-session#overview)

### 值 {#value}

求值正常完成时得到、并可供其他表达式使用的结果；函数本身也是值。

**首次讲解:** [第 1 章：第一次 F# 会话](./part-01/ch-01-first-session#overview)

### 绑定 {#binding}

名称与值之间的关联，通常由 `let` 和模式建立。绑定不是可以随意改写的存储槽。

**首次讲解:** [第 2 章：值、绑定与表达式](./part-01/ch-02-values-bindings-expressions#overview)

### 不可变性 {#immutability}

保持既有值不变的性质。绑定会维持名称与值的关联；如果该值引用对象，对象内部能否改变则由其类型另行决定。

**首次讲解:** [第 2 章：值、绑定与表达式](./part-01/ch-02-values-bindings-expressions#overview)

### 数值转换 {#numeric-conversion}

显式地从一种数值类型产生另一种数值类型的值，例如用 decimal 由 int 得到 decimal。

**首次讲解:** [第 2 章：值、绑定与表达式](./part-01/ch-02-values-bindings-expressions#overview)

### 遮蔽 {#shadowing}

在内层或后续作用域中建立同名的新绑定，使旧绑定在该范围内无法再由这个名称访问；它不是修改旧值。

**首次讲解:** [第 2 章：值、绑定与表达式](./part-01/ch-02-values-bindings-expressions#overview)

### 类型标注 {#type-annotation}

源码中明确写出的类型约束，用来记录意图，或补充编译器无法可靠推断的上下文。

**首次讲解:** [第 2 章：值、绑定与表达式](./part-01/ch-02-values-bindings-expressions#overview)

### 类型推断 {#type-inference}

编译器根据表达式的使用方式和上下文推导静态类型，而不要求处处写出类型标注。

**首次讲解:** [第 2 章：值、绑定与表达式](./part-01/ch-02-values-bindings-expressions#overview)

### 匿名函数 {#anonymous-function}

用 fun 参数 -> 主体 表达式直接创建、无需先给函数本身命名的函数值。

**首次讲解:** [第 3 章：函数也是值](./part-01/ch-03-functions-as-values#overview)

### 实参 {#argument}

调用函数时实际提供给参数的值或表达式。

**首次讲解:** [第 3 章：函数也是值](./part-01/ch-03-functions-as-values#overview)

### 自动泛化 {#automatic-generalization}

在安全且定义不依赖具体类型时，编译器会把推断出的未知类型变成类型参数，使其能用于多种具体类型。

**首次讲解:** [第 3 章：函数也是值](./part-01/ch-03-functions-as-values#overview)

### 闭包 {#closure}

一个函数值，以及它在定义处捕获并供以后调用使用的外部值。

**首次讲解:** [第 3 章：函数也是值](./part-01/ch-03-functions-as-values#overview)

### 柯里化 {#currying}

把多参数计算表示为连续接收单个参数并返回下一函数的形式；F# 的 let 绑定函数通常使用这种表示。

**首次讲解:** [第 3 章：函数也是值](./part-01/ch-03-functions-as-values#overview)

### 函数 {#function}

接收输入并计算结果的值；在 F# 中，函数可以像其他值一样被绑定、传递和返回。

**首次讲解:** [第 3 章：函数也是值](./part-01/ch-03-functions-as-values#overview)

### 函数应用 {#function-application}

向函数值提供实参并执行函数体，从而产生结果。

**首次讲解:** [第 3 章：函数也是值](./part-01/ch-03-functions-as-values#overview)

### 高阶函数 {#higher-order-function}

至少接收一个函数值作为参数，或把函数值作为结果返回的函数。

**首次讲解:** [第 3 章：函数也是值](./part-01/ch-03-functions-as-values#overview)

### 形参 {#parameter}

函数定义中用于接收调用方实参的名称或模式。

**首次讲解:** [第 3 章：函数也是值](./part-01/ch-03-functions-as-values#overview)

### 部分应用 {#partial-application}

只向柯里化函数提供一部分参数，从而得到一个等待其余参数的新函数。

**首次讲解:** [第 3 章：函数也是值](./part-01/ch-03-functions-as-values#overview)

### 元组 {#tuple}

按固定位置组合若干值的类型；组成部分可以具有不同类型，类型签名中用星号连接。

**首次讲解:** [第 3 章：函数也是值](./part-01/ch-03-functions-as-values#overview)

### 穷尽性 {#exhaustiveness}

一组模式覆盖输入类型的所有可能形式。若无法穷尽，某些输入就可能没有匹配分支。

**首次讲解:** [第 4 章：分支与基本模式](./part-01/ch-04-branching-patterns#overview)

### 守卫 {#guard}

模式初步匹配后才求值的 `when` 布尔条件；条件为 `false` 时继续尝试后续规则。

**首次讲解:** [第 4 章：分支与基本模式](./part-01/ch-04-branching-patterns#overview)

### 列表 {#list}

由同一类型元素组成的有序不可变单向链式集合；空表为 []，头与尾可用 :: 构造或解构。

**首次讲解:** [第 4 章：分支与基本模式](./part-01/ch-04-branching-patterns#overview)

### 模式 {#pattern}

用来检查输入结构、分解组成部分，并可为其建立局部绑定的规则。

**首次讲解:** [第 4 章：分支与基本模式](./part-01/ch-04-branching-patterns#overview)

### 模式匹配 {#pattern-matching}

根据值的结构选择匹配分支，并可同时为组成部分建立绑定。

**首次讲解:** [第 4 章：分支与基本模式](./part-01/ch-04-branching-patterns#overview)

### 通配符模式 {#wildcard-pattern}

写作 `_`，可以匹配任何输入，但不会为该输入建立名称。

**首次讲解:** [第 4 章：分支与基本模式](./part-01/ch-04-branching-patterns#overview)

### 立即求值 {#eager-evaluation}

在操作被调用时就计算结果，而不是等到以后枚举或请求某项时才计算。

**首次讲解:** [第 5 章：列表、管道与数据流](./part-01/ch-05-lists-pipelines#overview)

### 副作用 {#effect}

求值期间发生、不能只由返回值描述的可观察行为，例如输出、写文件或修改状态。

**首次讲解:** [第 5 章：列表、管道与数据流](./part-01/ch-05-lists-pipelines#overview)

### 可变绑定 {#mutable-binding}

用 let mutable 建立、其存储位置可随后通过 <- 更新的绑定。

**首次讲解:** [第 5 章：列表、管道与数据流](./part-01/ch-05-lists-pipelines#overview)

### option {#option}

用 Some value 表示存在值、用 None 表示没有值的 F# 类型；完整建模规则在第 9 章展开。

**首次讲解:** [第 5 章：列表、管道与数据流](./part-01/ch-05-lists-pipelines#overview)

### 管道 {#pipeline}

用 |> 把左侧结果作为最末参数交给右侧函数调用，从而按数据流顺序书写变换。

**首次讲解:** [第 5 章：列表、管道与数据流](./part-01/ch-05-lists-pipelines#overview)

### 累加器 {#accumulator}

递归或折叠每一步都会传给下一步的值，表示截至当前的计算结果。

**首次讲解:** [第 6 章：递归、尾调用与折叠](./part-01/ch-06-recursion-folds#overview)

### 折叠 {#fold}

按确定顺序用组合函数把集合元素逐项并入累加器，最后返回累加器的高阶操作。

**首次讲解:** [第 6 章：递归、尾调用与折叠](./part-01/ch-06-recursion-folds#overview)

### 递归 {#recursion}

函数直接或间接调用自身，并把更小的问题或更接近终止条件的输入交给下一次调用。

**首次讲解:** [第 6 章：递归、尾调用与折叠](./part-01/ch-06-recursion-folds#overview)

### 结构递归 {#structural-recursion}

按数据类型的构造方式分支，并在递归分支中处理结构上更小组成部分的递归。

**首次讲解:** [第 6 章：递归、尾调用与折叠](./part-01/ch-06-recursion-folds#overview)

### 尾调用 {#tail-call}

函数分支返回前执行的最后操作调用；调用结果无需再由当前栈帧继续加工。

**首次讲解:** [第 6 章：递归、尾调用与折叠](./part-01/ch-06-recursion-folds#overview)

### 尾递归 {#tail-recursion}

所有递归路径都把递归调用置于尾位置的递归形式，使编译器有机会消除递归栈增长。

**首次讲解:** [第 6 章：递归、尾调用与折叠](./part-01/ch-06-recursion-folds#overview)

## 第 2 部分 · 用类型建模 {#part-2}

### 匿名记录 {#anonymous-record}

无需单独声明类型名称，其具体类型由字段名和字段类型共同确定的记录值。

**首次讲解:** [第 7 章：记录、更新、相等与比较](./part-02/ch-07-records-equality#overview)

### 哈希码 {#hash-code}

用于在哈希数据结构中定位候选项的整数摘要。相等的值必须产生相同哈希码，不同值也可能产生同一码。

**首次讲解:** [第 7 章：记录、更新、相等与比较](./part-02/ch-07-records-equality#overview)

### 记录 {#record}

由命名字段组成的乘积类型；普通 F# 记录默认不可变，并在组成字段支持时自动获得结构相等与比较。

**首次讲解:** [第 7 章：记录、更新、相等与比较](./part-02/ch-07-records-equality#overview)

### 引用身份 {#reference-identity}

判断两个引用是否指向同一个运行时对象；这与对象内容是否结构相等是两回事。

**首次讲解:** [第 7 章：记录、更新、相等与比较](./part-02/ch-07-records-equality#overview)

### 结构比较 {#structural-comparison}

按组成部分的既定顺序递归比较复合值而得到的排序关系，要求所有相关组成类型支持比较。

**首次讲解:** [第 7 章：记录、更新、相等与比较](./part-02/ch-07-records-equality#overview)

### 结构相等 {#structural-equality}

递归比较复合值对应组成部分而判断内容是否相等的语义，而不是检查它们是否为同一对象。

**首次讲解:** [第 7 章：记录、更新、相等与比较](./part-02/ch-07-records-equality#overview)

### 可辨识联合 {#discriminated-union}

一种由若干具名用例组成的类型；一个值恰属其中一个用例，每个用例还可以携带数据。

**首次讲解:** [第 8 章：可辨识联合与状态建模](./part-02/ch-08-discriminated-unions#overview)

### 联合案例 {#union-case}

可辨识联合中的一种具名选项。它可以不携带数据，也可以携带只对该选项有意义的字段。

**首次讲解:** [第 8 章：可辨识联合与状态建模](./part-02/ch-08-discriminated-unions#overview)

### Result {#result}

用 Ok value 表示成功、用 Error error 表示带有已建模原因的预期失败的 F# 类型。

**首次讲解:** [第 9 章：缺失与预期失败](./part-02/ch-09-option-result#overview)

### 短路 {#short-circuit}

组合计算时，一旦遇到无法继续的 None 或 Error，便直接保留该结果而不运行后续依赖步骤。

**首次讲解:** [第 9 章：缺失与预期失败](./part-02/ch-09-option-result#overview)

### 递归类型 {#recursive-type}

在自身定义的组成部分中再次引用自身、从而能表示有限嵌套结构的类型。

**首次讲解:** [第 10 章：递归类型与结构递归](./part-02/ch-10-recursive-types#overview)

### 比较约束 {#comparison-constraint}

写作 'T : comparison，要求类型参数支持 F# 的泛型比较与排序操作。

**首次讲解:** [第 11 章：泛型、约束与度量单位](./part-02/ch-11-generics-constraints#overview)

### 相等约束 {#equality-constraint}

写作 'T : equality，要求类型参数支持 F# 的泛型相等运算。

**首次讲解:** [第 11 章：泛型、约束与度量单位](./part-02/ch-11-generics-constraints#overview)

### 泛型类型参数 {#generic-type-parameter}

定义中尚未指定具体类型的占位参数；每次具体使用时，同一个参数都会由同一种类型替换。

**首次讲解:** [第 11 章：泛型、约束与度量单位](./part-02/ch-11-generics-constraints#overview)

### 静态解析类型参数 {#statically-resolved-type-parameter}

写作 ^T、在内联调用点解析并可携带成员约束的 F# 类型参数；它不同于普通的 'T 泛型参数。

**首次讲解:** [第 11 章：泛型、约束与度量单位](./part-02/ch-11-generics-constraints#overview)

### 度量单位 {#unit-of-measure}

附着在受支持数值类型上的编译期类型标注，用于静态检查量纲关系，并在运行时擦除。

**首次讲解:** [第 11 章：泛型、约束与度量单位](./part-02/ch-11-generics-constraints#overview)

### 值限制 {#value-restriction}

自动泛化只适用于安全的绑定形式。若值包含未解析的类型变量，而且泛化可能让同一存储被当作多种类型使用，编译器就会拒绝该绑定。

**首次讲解:** [第 11 章：泛型、约束与度量单位](./part-02/ch-11-generics-constraints#overview)

### 访问控制 {#access-control}

决定哪些代码可以使用某个程序实体的规则。F# 通过 `public`、`internal`、`private` 和签名文件中公开的声明来表达这些规则。

**首次讲解:** [第 12 章：让非法状态无法表示](./part-02/ch-12-making-illegal-states-unrepresentable#overview)

### 不变量 {#invariant}

对一个受保护类型的每个可公开获得值都应持续成立的条件。

**首次讲解:** [第 12 章：让非法状态无法表示](./part-02/ch-12-making-illegal-states-unrepresentable#overview)

### 私有表示 {#private-representation}

调用方能够使用类型本身，却不能直接使用其底层联合案例、记录字段构造或其他表示细节的设计。

**首次讲解:** [第 12 章：让非法状态无法表示](./part-02/ch-12-making-illegal-states-unrepresentable#overview)

### 签名文件 {#signature-file}

扩展名为 `.fsi`、位于对应 `.fs` 实现之前的 F# 文件。它声明其他文件可以看到的 API。

**首次讲解:** [第 12 章：让非法状态无法表示](./part-02/ch-12-making-illegal-states-unrepresentable#overview)

### 智能构造函数 {#smart-constructor}

先验证或规范化输入，再产生满足约束的领域值；输入不合法时通过返回类型报告原因的函数。

**首次讲解:** [第 12 章：让非法状态无法表示](./part-02/ch-12-making-illegal-states-unrepresentable#overview)

## 第 3 部分 · 组合与程序结构 {#part-3}

### 函数组合 {#function-composition}

把前一个函数的输出连接到后一个函数的输入，从多个函数值得到一个新函数值。

**首次讲解:** [第 13 章：组合、参数顺序与管道 API](./part-03/ch-13-composition-pipeline-api#overview)

### 数组 {#array}

具有固定长度、连续存储且元素可原地更新的同类型有序集合；改变长度需要创建新数组。

**首次讲解:** [第 14 章：集合选择与求值模型](./part-03/ch-14-collections-evaluation#overview)

### 延迟求值 {#deferred-evaluation}

把产生值或执行工作的时机推迟到消费者请求结果时；工作是否重复取决于具体来源与是否缓存。

**首次讲解:** [第 14 章：集合选择与求值模型](./part-03/ch-14-collections-evaluation#overview)

### 枚举 {#enumeration}

消费者通过枚举器依次请求集合元素的遍历过程；每次枚举所执行的工作由具体来源决定。

**首次讲解:** [第 14 章：集合选择与求值模型](./part-03/ch-14-collections-evaluation#overview)

### 映射表（Map） {#map}

按键的 F# 泛型比较顺序组织键值绑定的不可变树；每个键至多对应一个值。

**首次讲解:** [第 14 章：集合选择与求值模型](./part-03/ch-14-collections-evaluation#overview)

### 序列 {#sequence}

`seq<'T>` 是 `IEnumerable<'T>` 的类型缩写，描述如何枚举同类型元素，但本身不保证缓存、纯度或可重复遍历。

**首次讲解:** [第 14 章：集合选择与求值模型](./part-03/ch-14-collections-evaluation#overview)

### 集合（Set） {#set}

按元素的 F# 泛型比较顺序组织唯一元素的不可变树。

**首次讲解:** [第 14 章：集合选择与求值模型](./part-03/ch-14-collections-evaluation#overview)

### 活动模式 {#active-pattern}

由函数实现并以具名模式形式使用的输入视图，可在匹配时对值进行分类或解构。

**首次讲解:** [第 15 章：活动模式与领域匹配边界](./part-03/ch-15-active-patterns#overview)

### 完整活动模式 {#complete-active-pattern}

为每个输入都返回某个具名案例的活动模式；多案例形式会把整个输入空间划分为若干部分。

**首次讲解:** [第 15 章：活动模式与领域匹配边界](./part-03/ch-15-active-patterns#overview)

### 参数化活动模式 {#parameterized-active-pattern}

在最终被匹配输入之前接收额外实参、从而为调用位置特化识别规则的单案例活动模式。

**首次讲解:** [第 15 章：活动模式与领域匹配边界](./part-03/ch-15-active-patterns#overview)

### 部分活动模式 {#partial-active-pattern}

只识别输入空间一部分并能以不匹配告终的单案例活动模式，名称列表以通配符案例结尾。

**首次讲解:** [第 15 章：活动模式与领域匹配边界](./part-03/ch-15-active-patterns#overview)

### 程序集 {#assembly}

由 .NET 编译产生并作为部署、加载与引用单元的 .dll 或 .exe，以及其中的元数据和代码。

**首次讲解:** [第 16 章：模块、命名空间、项目与编译设置](./part-03/ch-16-modules-namespaces-projects#overview)

### 编译顺序 {#compilation-order}

F# 源文件提供给编译器的先后次序。通常，后面的文件可以使用前面的定义，前面的文件不能使用后面的定义。

**首次讲解:** [第 16 章：模块、命名空间、项目与编译设置](./part-03/ch-16-modules-namespaces-projects#overview)

### 模块 {#module}

把相关类型、值与函数组织在同一具名范围内的 F# 构造；模块自身可以位于命名空间或另一模块中。

**首次讲解:** [第 16 章：模块、命名空间、项目与编译设置](./part-03/ch-16-modules-namespaces-projects#overview)

### 命名空间 {#namespace}

可跨文件与程序集组织类型和模块的具名容器；它不能直接包含 F# 值或函数。

**首次讲解:** [第 16 章：模块、命名空间、项目与编译设置](./part-03/ch-16-modules-namespaces-projects#overview)

### 可空引用类型 {#nullable-reference-type}

启用 F# 空值检查后，`T | null` 表示引用类型允许为 `null`。它只供编译器检查，不会在运行时增加包装。

**首次讲解:** [第 16 章：模块、命名空间、项目与编译设置](./part-03/ch-16-modules-namespaces-projects#overview)

### open 声明 {#open-declaration}

让某命名空间或模块中的名称在后续范围可用短名称引用的声明；它不加载代码，也不改变访问控制。

**首次讲解:** [第 16 章：模块、命名空间、项目与编译设置](./part-03/ch-16-modules-namespaces-projects#overview)

### 项目文件 {#project-file}

描述目标框架、编译项顺序、引用和构建属性的 MSBuild XML 文件；F# 项目通常使用 .fsproj 扩展名。

**首次讲解:** [第 16 章：模块、命名空间、项目与编译设置](./part-03/ch-16-modules-namespaces-projects#overview)

### 抽象表示 {#abstract-representation}

签名只公开类型名称，不公开联合案例、记录字段或其他实现细节。调用方可以使用该类型的值，却不能依赖其底层表示。

**首次讲解:** [第 17 章：签名、访问控制与面向 F# 的 API](./part-03/ch-17-signatures-encapsulation#overview)

### 公开 API {#public-api-surface}

组件主动向调用方公开并承诺支持的类型、案例、函数、成员及其签名。

**首次讲解:** [第 17 章：签名、访问控制与面向 F# 的 API](./part-03/ch-17-signatures-encapsulation#overview)

### 计算表达式 {#computation-expression}

由构建器成员解释的 F# 语法，用来组合带有特定上下文或控制流的计算。

**首次讲解:** [第 18 章：显式工作流组合与验证累积](./part-03/ch-18-workflow-validation#overview)

### 验证错误累积 {#validation-accumulation}

对彼此独立的检查全部求值，并把各自的失败按明确顺序合并到一个错误集合中的组合策略。

**首次讲解:** [第 18 章：显式工作流组合与验证累积](./part-03/ch-18-workflow-validation#overview)
