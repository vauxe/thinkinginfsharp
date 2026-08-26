---
title: "第 11 章：泛型、约束与度量单位"
description: "理解自动泛化、值限制、相等与比较约束、条件式结构能力，以及度量单位。"
translationKey: part-02/ch-11-generics-constraints
---

# 第 11 章：泛型、约束与度量单位 {#overview}

第 10 章的 `mapTree` 从不检查叶子的具体类型。因此，编译器推断出的同一个实现可用于 `BookingTree<int>`、`BookingTree<string>` 及许多其他类型。相比之下，对叶子排序需要排序操作，相加数量则需要相容的数值量纲。“泛型”不表示“所有操作都可用”，而表示定义准确声明了自己需要哪些类型能力。

本章从无约束函数开始，依次讨论值限制、结构相等、比较约束和度量单位。目标不是给每个签名添加标注，而是理解类型变量为何通用、为何受限，以及编译器有时为何拒绝泛化某个绑定。

## 学完本章后你能做什么 {#outcomes}

学完本章后，你应该能够：

- 把重复的类型变量读成一致性要求；
- 解释 F# 何时会自动泛化一个定义；
- 从绑定形状与意图诊断 FS0030 值限制错误；
- 在类型标注、显式参数与接收 unit 的工厂之间选择；
- 读写 `'T : equality` 与 `'T : comparison` 约束；
- 解释记录、元组、列表和联合的能力怎样依赖组成类型；
- 区分普通泛型与静态解析参数及其内联成员约束；
- 用度量单位在编译期拒绝量纲无效的算术；
- 说明度量单位不验证什么，以及它在运行时会怎样。

## 通用性由类型独立性推断而来 {#automatic-generalization}

共享函数只是把输入的两个副本放进列表，并未以其他方式使用它：

```fsharp:line-numbers [ch11-generics-constraints.fsx]
let duplicate value = [ value; value ]

let integerCopies = duplicate 3
let attendeeCopies = duplicate "Lin"

printfn "Generalized function: ints=%A strings=%A" integerCopies attendeeCopies

let genericEmpty = []
let oneInteger = 1 :: genericEmpty
let oneAttendee = "Ada" :: genericEmpty

printfn "Simple generic value: ints=%A strings=%A" oneInteger oneAttendee
```
F# 推断出：

```fsharp
duplicate : 'T -> 'T list
```

`'T` 是泛型类型参数，并不表示“动态类型值”。每次调用中，实参与两个结果元素都具有同一个具体类型。一次调用把 `'T` 实例化为 `int`，另一次则实例化为 `string`；两次仍都经过静态检查。

这就是**自动泛化**。当带显式参数的完整函数定义不依赖某个具体类型时，编译器可以把该类型变成调用时确定的泛型参数。也存在显式语法：

```fsharp
let duplicateExplicit<'T> (value: 'T) : 'T list =
    [ value; value ]
```

显式版在这里没有增加信息，通常只会更吵。让推断公开最通用且安全的签名；只有当标注能传达公开契约或解决真实歧义时，才添加它。

一些简单的不可变数据项也能安全泛化。空列表不包含值，也没有可变元素槽，所以 `genericEmpty` 可以分别实例化为 `int list` 和 `string list`。这项例外并不允许你假定每个带未知类型的表达式都是可复用泛型值。

### 泛型类型携带同一种关系 {#generic-types}

脚本中的记录显式声明参数，因为类型定义必须命名会变化的字段类型：

```fsharp
type Envelope<'T> =
    { Label: string
      Payload: 'T }
```

`Envelope<int>` 与 `Envelope<string>` 是由一个定义构造出的不同类型。在任一值中，`Payload` 都恰好具有所提供的类型。`BookingTree<'T>`、`'T option` 和 `'T list` 使用相同思想。

只有实现确实忽略差异时，通用性才有用。若函数用 `string` 转换输入，其输入仍可能是泛型；若它把输入与 `1` 相加，所选字面量和加法操作就会引入数值要求。应从操作而非形参名称寻找约束。

## 值限制防止一个值被不相容地使用 {#value-restriction}

下面这个仅用于诊断的定义有意不放入有效共享脚本：

```fsharp
let ambiguousBuckets = Array.create 2 []
```

F# 10 会报告 FS0030，并显示类似 `'_a list array` 的弱类型。表达式含有未解析的元素类型，但该绑定既不是带显式参数的完整函数，也不是能安全泛化的简单不可变数据项。

这项限制很重要，因为数组具有可变元素槽。如果同一个数组先被当作 `int list array` 使用，再被当作 `string list array` 使用，写入其中一种元素就会破坏另一种类型的假设。编译器不能让一个存储位置同时具有互不相关的类型。

该规则有意采用保守判断，也会捕获一些看似纯粹的部分应用：

```fsharp
let alwaysKeep = List.filter (fun _ -> true) // FS0030
```

尽管结果是函数值，右侧仍是一个应用，绑定自身也缺少显式参数。自动泛化遵循安全的绑定形式。带显式参数的完整函数定义属于这种形式；部分应用则可能需要类型标注或 eta 展开。

### 修复意图，而不只是诊断 {#value-restriction-fixes}

常见意图有三种：

1. **一个具体类型的单个值：** 补上缺失的标注。

   ```fsharp
   let integerBuckets: int list array = Array.create 2 []
   ```

2. **一个泛型函数：** 公开参数，而不是保存一个类型未解析的部分应用。

   ```fsharp
   let alwaysKeep values = List.filter (fun _ -> true) values
   ```

3. **按需得到一个全新的泛型值：** 让构造成为接收 `unit` 的函数。

   ```fsharp
   let makeEmptyBuckets () = Array.create 2 []
   ```

共享脚本展示第三种形式：

```fsharp:line-numbers [ch11-generics-constraints.fsx]
let makeEmptyBuckets () = Array.create 2 []

let integerBuckets: int list array = makeEmptyBuckets ()
let attendeeBuckets: string list array = makeEmptyBuckets ()

let anotherIntegerBuckets: int list array = makeEmptyBuckets ()

printfn
    "Value restriction fixes: ints=%d strings=%d fresh=%b"
    integerBuckets.Length
    attendeeBuckets.Length
    (not (LanguagePrimitives.PhysicalEquality integerBuckets anotherIntegerBuckets))
```
添加 `()` 会改变语义：每次调用都分配新数组。这适合工厂，却不适合本应共享的单例缓存。把 `alwaysKeep` 改为显式接收 `values`，会保留其纯变换含义；类型标注则把值固定为一种类型。应根据所有权与生命周期选择修复方式，而不是随便采用能让 FS0030 消失的编辑。

少数场景存在显式泛型值语法，但它不是默认修复。清楚的普通函数更容易调用，也会让求值时机可见。

## 泛型操作会引入能力约束 {#generic-constraints}

无约束的 `'T` 不承诺排序、算术、成员，甚至不承诺 F# 泛型相等。定义中使用的操作会加入最小必要能力：

```fsharp:line-numbers [ch11-generics-constraints.fsx]
type Envelope<'T> = { Label: string; Payload: 'T }

let same left right = left = right
let comesBefore left right = compare left right < 0

let first = { Label = "A"; Payload = 2 }

let firstAgain = { Label = "A"; Payload = 2 }

let second = { Label = "B"; Payload = 1 }

let sortedLabels =
    [ second; first ] |> List.sort |> List.map (fun envelope -> envelope.Label)

printfn "Constraints: equal=%b ordered=%b sorted=%A" (same first firstAgain) (comesBefore first second) sortedLabels
```
重要的推断签名在概念上是：

```fsharp
same : 'T -> 'T -> bool when 'T : equality
comesBefore : 'T -> 'T -> bool when 'T : comparison
```

`=` 引入**相等约束**。`compare`、关系运算符以及 `List.sort` 等有序操作会引入**比较约束**。公开签名确实需要时，也可以显式声明：

```fsharp
let sameExplicit<'T when 'T : equality>
    (left: 'T)
    (right: 'T) =
    left = right
```

当函数主体已经表达要求时，推断更合适。写下实现不需要的约束只会无谓拒绝有用的调用方。

相等与比较是不同能力。一种类型可以允许相等却选择不支持比较；函数类型则两项约束都不满足。编译器支持某种顺序，也不能证明该顺序在领域中有意义。在把泛型排序用作业务策略前，应先决定预约优先级是否真的要遵循记录字段顺序。

## 外层类型的能力取决于组成部分 {#component-constraints}

`Envelope<'T>` 可以为任意负载类型构造。只有 `'T` 满足相等约束时，生成的结构相等才可用；只有 `'T` 满足比较约束时，生成的结构比较才可用。这是一项条件能力，而不是构造记录时的无条件约束。

下面这个值合法：

```fsharp
let functionEnvelope =
    { Label = "f"
      Payload = (fun value -> value) }
```

但下面这个仅用于诊断的表达式会报告 FS0001，因为函数负载不支持相等：

```fsharp
let invalid = functionEnvelope = functionEnvelope
```

同样的组合规则适用于元组、列表、option、记录和可辨识联合：外层结构操作会递归要求相关组成部分提供对应能力。类型还可以显式定制或禁止生成的相等/比较，所以不能只从表面语法推断支持情况。

这与第 7 章直接相连。相等记录具有相容哈希，是因为参与的字段相等与哈希语义能够组合。第 14 章会把比较约束用于有序 `Map` 与 `Set` 键，并把它和哈希集合的要求区分开。

## 普通泛型不是 SRTP {#ordinary-generics-vs-srtp}

本书大多数泛型 F# 代码使用写作 `'T` 的普通参数：`duplicate`、`mapTree`、`same` 和 `comesBefore`。相等与比较是能用于这些普通泛型签名的特殊 F# 约束。

泛化运算符的代码可能会出现带 `inline` 和静态成员约束的签名：

```fsharp
let inline add left right = left + right
// 推断出的签名是内联的，并携带静态 (+) 成员约束。
```

在当前 F# 中，**静态解析类型参数**（SRTP）的简化语法通常使用 `'T` 这样的撇号前缀名称；旧资料和某些复杂显式分派形式仍使用 `^T`。识别 SRTP 要同时观察 `inline`、编译期特化和 `static member (+)` 这样的成员约束。它适合少数泛型数值与成员抽象。`map`、相等检查和领域规则等普通函数通常保留普通类型参数，让签名与调用方式更简单。

共享脚本中的带度量加法有意把表示固定为 `int`，只改变度量，因此无需自定义 SRTP 机制。附录 H 会给出识别规则与高级官方入口；对领域 API 而言，具体数值类型通常更清楚。

## 度量单位约束数值量纲 {#units-of-measure}

座位数与经过分钟数都可能用数字表示，但二者相加毫无意义。F# 可以把编译期度量附着在受支持的数值类型上：

```fsharp:line-numbers [ch11-generics-constraints.fsx]
[<Measure>]
type seat

[<Measure>]
type minute

let addMeasured (left: int<'Measure>) (right: int<'Measure>) = left + right

let capacity = 40<seat>
let requested = addMeasured 2<seat> 3<seat>
let remaining = capacity - requested
let bookingRate = 12.0<seat> / 3.0<minute>

printfn "Measures: requested=%d remaining=%d rate=%.1f" requested remaining bookingRate
```
`[<Measure>] type seat` 声明一个度量，而不是运行时记录或包装器。`int<seat>` 是座位数量。加减法要求度量相容；乘除法会组合度量，因此 `bookingRate` 的类型是 `float<seat/minute>`。

`addMeasured` 中的度量变量允许任意一种度量，却要求两个实参共享它：

```fsharp
addMeasured : int<'Measure> -> int<'Measure> -> int<'Measure>
```

下面这个仅用于诊断的表达式因量纲不一致而失败：

```fsharp
let invalid = 2<seat> + 3<minute> // FS0001
```

度量属于编译期信息，并会在运行时擦除。底层数值表示和反射看到的运行时值保持原样；序列化与非 F# 边界携带的是普通数字。因此，F# 编译器检查度量后，示例输出仍是普通数字。

在输入边界，先解析并验证原始数字，再显式恢复可信度量：

```fsharp
let seatsFromInt raw : int<seat> =
    LanguagePrimitives.Int32WithMeasure raw
```

`Int32WithMeasure` 只附加编译期度量，并不检查正数或容量。`-3<seat>` 在量纲上仍是座位数，却很可能违反本领域不变量。第 12 章会把带度量表示与私有构造、验证结合起来。

不要混淆只有一个值 `()` 的 `unit` 类型与 `seat` 这样的**度量单位**。它们的词语重叠，作用却完全不同。

## 运行共享示例 {#run-example}

在示例所在目录执行：

```console
dotnet fsi --exec ch11-generics-constraints.fsx
```

五行输出依次展示：同一个泛型函数用于两种类型、可安全泛化的简单值、每次生成新值的泛型工厂、泛型记录带来的相等与比较约束，以及经过量纲检查的算术。

无效的 FS0030 与 FS0001 示例只作诊断说明，因此共享脚本保持零警告。附录 E 会集中收录编译器诊断实验。

## 练习 {#exercises}

### 练习 1：推断通用性与约束 {#exercise-01}

为每个定义写出最通用签名，并解释哪个操作引入了约束：

```fsharp
let pair left right = left, right
let contains value values = List.contains value values
let orderedPair left right = if left <= right then left, right else right, left
let wrap value = { Label = "value"; Payload = value }
```

然后判断哪些定义能够接收函数值作为实参。

### 练习 2：修复两项值限制 {#exercise-02}

下面两个仅用于诊断的绑定都会报告 FS0030：

```fsharp
let buckets = Array.create 2 []
let keepAll = List.filter (fun _ -> true)
```

给出三种有明确意图的修复：一个共享的 `BookingRequest list array`、每次调用产生的新泛型数组，以及一个泛型 `keepAll` 函数。分别说明构造发生一次还是每次调用发生。

### 练习 3：跨边界保留量纲 {#exercise-03}

定义 `seat` 与 `minute`，然后编写：

- `throughput : float<seat> -> float<minute> -> float<seat/minute>`；
- 一个把已经验证的普通 `int` 转换为 `int<seat>` 的边界函数；
- 一个因为把座位与分钟相加而应失败的表达式。

解释序列化后还会保留哪些度量信息，并说出一条仅靠度量无法强制的预约不变量。

[查看本章练习答案](../solutions/ch-11-generics-constraints)。

## 模型复盘 {#model-review}

- 只有当定义安全地不依赖具体类型时，自动泛化才会量化类型变量。
- 值限制防止一个不可泛化的值被用成彼此不相容的构造类型。
- 类型标注会专门化一个值；显式参数会公开泛型函数；`()` 可以建立每次生成新值的工厂。
- 相等与比较约束来自操作，并通过结构字段组合。
- 普通泛型不需要 SRTP；应通过 `inline` 加成员约束来识别 SRTP，而不是只看 `'T` 与 `^T` 标点。
- 度量单位在编译期拒绝量纲错误，在运行时擦除，而且不会强制数值范围不变量。

第 12 章会有意运用这些类型能力：私有表示与智能构造函数将阻止调用方构造无效领域值。

## 资料来源 {#sources}

- [Microsoft Learn：泛型](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/generics/)
- [Microsoft Learn：自动泛化](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/generics/automatic-generalization)
- [Microsoft Learn：泛型约束](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/generics/constraints)
- [Microsoft Learn：度量单位](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/units-of-measure)
- [FSharp.Core：LanguagePrimitives](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-languageprimitives.html)
