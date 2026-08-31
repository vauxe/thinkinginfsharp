---
title: "附录 B：语法与运算符速查"
description: "快速阅读常见 F# 类型、表达式、模式、声明、计算表达式与运算符，而不把速查表变成第二本语言规范。"
translationKey: appendices/b-syntax-reference
---

# 附录 B：语法与运算符速查 {#overview}

本附录用于快速查找正文已经讲过的语法，不是第二份教程，也不能替代编译器和官方语言参考。陌生代码看起来很密集时，先看类型，再确认数据从哪里流向哪里，最后再解释标点。

下面的完整 F# 代码块都是可复制到 FSI 或小型项目中的自包含示例。表格里的简短条目只展示语法写法，并非完整程序。

## 从外向内读类型 {#read-types}

| 形式 | 读法 | 重要区别 |
|---|---|---|
| `int`、`decimal`、`bool`、`string` | 一个命名类型 | 数字字面量与转换会约束推断 |
| `'T` | 普通泛型类型参数 | 编译器可以推断并自动泛化它 |
| `unit` | 只有普通值 `()` 的类型 | 表示完成，不是 null 或缺失数据 |
| `'T option` | `Some value` 或 `None` | 表示可信 F# 数据中的可能缺失 |
| `Result<'T, 'Error>` | `Ok value` 或 `Error error` | 表示预期的成功/失败结果 |
| `'T list` | 不可变链表 | 不同于数组和一般序列 |
| `'T array` 或 `'T[]` | 可变索引数组 | 同一种运行时数组表示 |
| `seq<'T>` | 可枚举源 | 不承诺立即求值、缓存结果或只枚举一次 |
| `'A * 'B` | 含两个字段的元组 | 类型中的 `*` 分隔元组部分 |
| `Name: string` | 命名字段/成员/参数注解 | 此处 `:` 是注解分隔符 |
| `Input -> Output` | 函数类型 | 箭头分隔输入与结果 |
| `Type<Arg>` | 泛型 .NET/F# 类型应用 | 例如 `Task<int>` 或 `Map<string, int>` |

函数箭头向右结合：

```text
decimal -> int -> decimal
= decimal -> (int -> decimal)
```

这是柯里化函数：给它 `decimal` 后，会返回一个等待 `int` 的函数。元组参数函数有不同类型：`decimal * int -> decimal`。

函数应用向左结合：

```text
lineTotal 19.50m 3
= (lineTotal 19.50m) 3
```

括号组合一个值，并不定义调用语法。`f (x, y)` 传入一个元组；`f x y` 连续应用两次。

### 拆读高阶签名 {#higher-order-signature}

逐个箭头阅读这个常见签名：

```text
List.fold : ('State -> 'T -> 'State) -> 'State -> 'T list -> 'State
```

它接收三个参数：

1. 从当前状态与一个元素得到下一状态的折叠函数；
2. 初始状态；
3. 元素列表。

返回值是最终状态。

折叠函数外的括号很重要，因为整个函数是第一个参数。`'State` 与 `'T` 等类型名描述关系：初始、中间与最终状态必须一致；所有列表元素具有同一元素类型。

## 绑定值并返回表达式 {#bindings-expressions}

`let` 把名称绑定到表达式的值。它不是语句结束符，也不会让值自动变成可变。

```fsharp:line-numbers
let eventName = "Functional Foundations"
let capacity = 40
let fillRatio = 0.45
let ticketPrice = 19.50m
let eventCode = 'F'
let registrationOpen = true
let noFurtherResult = ()

printfn "%s (%c): capacity=%d, fill=%.2f, open=%b" eventName eventCode capacity fillRatio registrationOpen
```
| 形式 | 含义 |
|---|---|
| `let name = expression` | 不可变绑定 |
| `let name: Type = expression` | 带注解的绑定 |
| `let mutable name = expression` | 作用域内的可变存储 |
| `name <- expression` | 向可变存储或可设置属性赋值 |
| `let rec f x = ...` | 递归绑定；互递归再加 `and` |
| `let private name = ...` | 受可访问性限制的声明 |
| `use resource = expression` | 绑定并在词法作用域结束时释放 |

多行构造返回最后一个表达式。`printfn`、赋值、循环和许多副作用调用返回 `unit`；它们不会因为位于最后就变成有用结果。

缩进属于 F# 语法。对齐的行通常属于同一个代码块，减少缩进会结束该块。优先使用格式化器能够稳定保持的布局，不要手工制造复杂对齐。`;` 只在少数紧凑写法中分隔元素或表达式，不需要放在每行末尾。`;;` 用来结束 FSI 中的一次交互提交，通常不应写进 `.fs` 或 `.fsx` 文件。

遮蔽会创建同名新绑定，不会修改先前值。只在很小的作用域内、每一步都明显是在细化同一个值时使用。

## 定义并应用函数 {#functions}

```fsharp:line-numbers
let lineTotal unitPrice seats = unitPrice * decimal seats
let standardLineTotal = lineTotal 19.50m
let totalForThree = standardLineTotal 3

printfn "Curried total: %M" totalForThree
```
| 形式 | 含义 |
|---|---|
| `let f x y = body` | 命名柯里化函数 |
| `let f (x, y) = body` | 接收一个元组的命名函数 |
| `fun x -> body` | 匿名函数 |
| `let f (x: Type): Result = body` | 参数与返回注解 |
| `let partiallyApplied = f first` | 等待其余参数的函数 |
| `value |> f` | `f value` |
| `(a, b) ||> f` | `f a b` |
| `f >> g` | 先应用 `f`，再应用 `g` 的函数 |
| `f << g` | 先应用 `g`，再应用 `f` 的函数 |

安排参数顺序时，把较固定的配置参数放在前面，把经常变化的数据放在最后，以便通过 `|>` 传入。管道只改变表达式的分组方式，不会改变求值方式、错误处理、可空性或性能。

`ignore` 消费一个值并返回 `unit`。只在有意丢弃时使用；不要用它隐藏本应处理的成功或失败结果。

## 用表达式与模式分支 {#branching-patterns}

`if/then/else` 是表达式。两个分支必须具有兼容类型。只有 `then` 分支为 `unit` 时才允许省略 `else`。

```fsharp:line-numbers
let capacityBand remaining =
    match remaining with
    | value when value <= 0 -> "full"
    | 1 -> "last seat"
    | value when value <= 5 -> "limited"
    | _ -> "available"

printfn "Capacity bands: %s, %s, %s, %s" (capacityBand 0) (capacityBand 1) (capacityBand 4) (capacityBand 8)
```
匹配分支从上到下运行。优先写具体结构分支，最后再放通配符；利用编译器的穷尽性警告发现新增领域状态。

| 模式 | 含义 |
|---|---|
| `_` | 接受并丢弃任意值 |
| `name` | 接受任意值并绑定到新名称 |
| `42`、`"open"`、`true` | 匹配字面量 |
| `(left, right)` | 分解元组 |
| `{ Name = name }` | 从记录选择字段 |
| `Some value`、`None` | 分解 option |
| `Ok value`、`Error error` | 分解 result |
| `head :: tail`、`[]` | 分解列表 |
| `case as whole` | 同时保留分解结果与完整值 |
| `p1 | p2` | 或模式；两边必须绑定兼容名称 |
| `:? Type as value` | 在 .NET 边界运行时类型测试并绑定 |
| `pattern when condition` | 结构匹配后的守卫 |

模式中的小写标识符通常会**绑定**，不会同先前的同名值比较。需要相等判断时，应使用字面量、联合案例或显式守卫。

`function | pattern -> result | ...` 是 `fun value -> match value with ...` 的简写。只有省略参数名能改善而非掩盖代码时才使用。

## 用记录与联合建立数据模型 {#records-unions}

| 形式 | 用途 |
|---|---|
| `type Person = { Name: string; Age: int }` | 命名积：所有字段同时存在 |
| `{ old with Age = old.Age + 1 }` | 记录复制更新；创建新记录 |
| `{| Name = "Ada"; Age = 36 |}` | 匿名记录，常用于局部值或 API/序列化边界 |
| `type Status = Pending | Confirmed of string` | 带案例专属数据的命名替代项 |
| `type UserId = private UserId of string` | 隐藏未检查构造的单案例联合 |
| `type Alias = string` | 仅为缩写；不是独立领域类型 |

联合案例以大写标识符开头。可以命名案例字段，以改善生成签名与互操作。

```fsharp:line-numbers
type BookingStatus =
    | Pending
    | Confirmed of confirmationCode: string
    | Cancelled of reason: string
```
```fsharp:line-numbers
let describeStatus status =
    match status with
    | Pending -> "pending"
    | Confirmed confirmationCode -> $"confirmed:{confirmationCode}"
    | Cancelled reason -> $"cancelled:{reason}"

let statuses = [ Pending; Confirmed "C-42"; Cancelled "duplicate" ]

let descriptions = statuses |> List.map describeStatus

printfn "Statuses: %A" descriptions
```
用记录表示同时成立的事实，用联合表示互斥情况。除非矛盾组合确实有效，否则不要用互相独立的布尔标志模拟联合。

## 识别集合语法 {#collections}

| 语法 | 表示的值或求值方式 |
|---|---|
| `[ 1; 2; 3 ]` | 列表 |
| `1 :: rest` | 在头部添加一个元素的新列表 |
| `left @ right` | 列表连接；复制左侧链 |
| `[| 1; 2; 3 |]` | 数组 |
| `array[index]` | 索引查找 |
| `source[start..finish]` | 切片，遵循源本身的规则 |
| `seq { yield 1; yield 2 }` | 通常延迟的序列表达式 |
| `[ for x in source do yield f x ]` | 列表推导式 |
| `[ start..finish ]` | 按元素类型范围规则生成的闭区间 |

`List`、`Array` 与 `Seq` 模块提供许多同名函数，但各自的存储方式和求值行为并不相同。附录 C 比较这些差异；此表只用于识别语法。

## 识别副作用与计算表达式 {#effects-computation-expressions}

| 形式 | 含义 |
|---|---|
| `try expression with | pattern -> handler` | 翻译选定异常 |
| `try expression finally cleanup` | 始终执行同步清理 |
| `use x = acquire ()` | 离开词法作用域时释放 `IDisposable` |
| `raise exception` | 抛出异常表达式 |
| `async { ... }` | 由 `async` 构建的 F# 异步工作流 |
| `task { ... }` | 由 `task` 构建的 .NET task 工作流 |
| `let! x = operation` | 计算表达式中由构建器定义的绑定 |
| `do! operation` | 绑定并丢弃类似 unit 的结果 |
| `return value` / `return! work` | 构建器定义的返回/委托返回 |
| `yield value` / `yield! values` | 按构建器规则产生一个值/转交多个值 |
| `use! x = operation` | 构建器定义的异步获取加释放作用域 |

`{ ... }` 前的标识符选择构建器。因此，带 `!` 关键字的含义来自该构建器；花括号本身不保证并发、延迟、取消、异常转换或回滚。要判断实际行为，应查看结果类型和构建器规则。

在计算表达式之外，F# 函数返回最后一个表达式——不存在通用 `return` 语句。

## 阅读 .NET 对象与互操作形式 {#dotnet-interop}

| 形式 | 含义 |
|---|---|
| `Type(arguments)` | 构造 .NET/F# 对象；`new` 通常可省略 |
| `value.Member` | 查找属性、字段或方法 |
| `value.Method(argument)` | 元组/CLI 风格成员调用 |
| `object.Property <- value` | 可设置属性赋值 |
| `value :> BaseType` | 静态检查的向上转换 |
| `value :?> DerivedType` | 运行时检查的向下转换；可能抛异常 |
| `value :? Type` | 运行时类型测试 |
| `null` / `Type | null` | 启用空值检查时允许为 null 的 .NET 引用 |
| `Nullable<'T>` | .NET 可空值包装；不同于 `'T option` |

成员重载、可选参数、委托、事件、特性与 null 注解都由 .NET API 的声明决定。推断无法选择目标重载时，只在该调用处加入类型注解；不要把注解扩散到本来清晰的纯代码。

## 拆读常见运算符与符号 {#operators-symbols}

| 符号 | 读法 | 不要混同为 |
|---|---|---|
| `=` / `<>` | 类型支持时的结构相等/不等 | 赋值（`<-`） |
| `<`、`<=`、`>`、`>=` | 类型支持时的结构比较 | 领域顺序正确性的证明 |
| `&&`、`||`、`not` | 短路布尔运算 | 位运算 `&&&`、`|||`、`~~~` |
| `+`、`-`、`*`、`/`、`%`、`**` | 重载算术 | 所有数字类型都有相同行为 |
| `|>` / `<|` | 正向/反向应用 | 组合 |
| `>>` / `<<` | 正向/反向组合 | 立即执行任一函数 |
| `::` / `@` | 列表前置/列表连接 | 数组修改 |
| `^` | 普通表达式中的 F# 字符串连接 | 旧式/显式 SRTP 语法中的插入符 |
| `->` | 函数/结果箭头或匹配分支分隔 | 修改 |
| `<-` | 赋值或属性设置 | 相等 |
| `:` / `:>` / `:?>` / `:?` | 注解/向上转换/向下转换/类型测试 | 联合案例载荷语法 |
| `|` | 联合或匹配分支分隔符 | 正向管道 `|>` |
| 关键字后的 `!` | `let!` 等计算表达式变体 | 已弃用的引用单元解引用语法 |
| `[<Attribute>]` | 特性 | 列表语法 |
| `#load`、`#r`、`#if` | 脚本/编译器指令 | 灵活类型语法 `#Base` |
| `<@ expression @>` | 有类型引号表达式（quotation） | 普通执行 |

引用单元的 `!cell` 与 `cell := value` 在当前 F# 中会产生弃用提示。有意使用引用单元保存状态时，应优先写 `cell.Value` 与 `cell.Value <- value`。

## 把优先级当作警示，而不是记忆比赛 {#precedence}

函数和成员调用的优先级很高。乘法先于加法，算术先于比较，布尔运算再组合比较结果。管道的优先级较低，因此前面的表达式通常会先算完再传入管道。`::` 和函数类型中的 `->` 都向右结合；自定义运算符的优先级由开头符号决定。

两种合理读法会产生不同行为时，应加入括号或命名中间值。尤其要澄清混合算术与比较、包含 `if`/`match` 的管道参数、嵌套函数值、转换和自定义运算符。格式可以传达读法，却不能覆盖语法规则。

空白也很重要。二元减法写作 `x - 1`，一元负号写作 `-x`。用空格分隔的函数调用，与带括号的 CLI 风格成员调用是两种不同写法。

## 识别声明与文件级形式 {#declarations-files}

| 形式 | 角色 |
|---|---|
| `namespace Company.Product` | CLR 命名空间；不能直接包含普通值绑定 |
| `module Name = ...` | 包含类型和值的命名模块 |
| 文件顶部的 `module Name` | 顶层模块形式 |
| `open Namespace.Or.Module` | 让其中的名称可直接使用；不会导入文件 |
| `type Name = ...` | 记录、联合、类、接口、枚举、别名等类型形式 |
| `member this.Name ...` | 实例成员 |
| `static member Name ...` | 静态成员 |
| `abstract member Name: ...` | 抽象/接口契约 |
| `interface IName with ...` | 显式接口实现 |
| 位于配对 `.fs` 前的 `.fsi` | 限制对外可见 F# API 的签名 |
| 项目 `<Compile Include="..." />` 顺序 | 编译顺序；定义先于消费者 |

`open` 只让名称可以写得更短。项目引用使程序集可用，文件项把源码加入编译，`#load` 则供 FSI 和脚本加载文件；三者解决不同问题。

## 找到对应章节 {#chapter-map}

| 如果不理解的是…… | 返回…… |
|---|---|
| 值、注解、转换、遮蔽 | [第 2 章](../part-01/ch-02-values-bindings-expressions) |
| 柯里化、部分应用、高阶函数 | [第 3 章](../part-01/ch-03-functions-as-values) |
| `if`、`match`、守卫、元组/列表模式 | [第 4 章](../part-01/ch-04-branching-patterns) |
| 列表管道或折叠 | [第 5–6 章](../part-01/ch-05-lists-pipelines) |
| 记录、相等、比较 | [第 7 章](../part-02/ch-07-records-equality) |
| 联合与穷尽状态建模 | [第 8 章](../part-02/ch-08-discriminated-unions) |
| option 与 result 组合 | [第 9 章](../part-02/ch-09-option-result) |
| 泛型、约束、度量单位 | [第 11 章](../part-02/ch-11-generics-constraints) |
| 模块、命名空间、文件顺序、项目 | [第 16 章](../part-03/ch-16-modules-namespaces-projects) |
| 异常、资源、async/task、取消 | [第 21–23 章](../part-04/ch-21-exceptions-resources-io) |
| 对象与 .NET 边界语法 | [第 25–27 章](../part-05/ch-25-objects-interfaces) |
| 代码引用、SRTP、灵活类型、byref | [附录 H](h-advanced-index) |

## 官方入口 {#official-entry-points}

- [F# 语言参考](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/)
- [F# 类型](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/fsharp-types)
- [类型推断](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/type-inference)
- [函数](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/functions/)
- [模式匹配](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/pattern-matching)
- [可区分联合](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/discriminated-unions)
- [计算表达式](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/computation-expressions)
- [符号与运算符参考](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/symbol-and-operator-reference/)
- [F# 代码格式指南](https://learn.microsoft.com/en-us/dotnet/fsharp/style-guide/formatting)
