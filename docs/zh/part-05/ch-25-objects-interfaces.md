---
title: "第 25 章：在 F# 中定义对象"
description: "根据模型需要，在函数、记录、联合类型、类、接口、对象表达式、扩展与结构体之间作选择。"
translationKey: part-05/ch-25-objects-interfaces
---

# 第 25 章：在 F# 中定义对象 {#overview}

F# 是 .NET 语言，类、成员、接口、继承和值类型都是原生工具。函数表达行为，记录表示带名称的数据，可辨识联合列出有限的状态；只有模型需要类的特定运行时语义时，才引入类。

先确定类型必须保留哪些含义。只有引用身份、隐藏状态、构造逻辑、资源生命周期、运行时分派或 .NET 成员 API 确实有用时，才采用对象。无论使用函数式还是面向对象工具，都应选择能准确表达需求的最简单表示。

## 先选表示，再选语法 {#representation-first}

先问调用者必须知道什么，以及运行时必须保留什么：

| 需要 | 通常先考虑 | 原因 |
|---|---|---|
| 一项变换或策略 | 函数 | 输入与输出行为已经构成抽象 |
| 带命名字段的不可变数据 | 记录 | 字段与结构化操作直接描述该值 |
| 一组有限选项或状态中的一种 | 可辨识联合 | 用例与穷尽匹配列出全部可能 |
| 组合在一起的一组相关依赖 | 函数字段记录 | 调用者可以组装一小组清楚的操作 |
| 身份、隐藏的变化状态或受管理的生命周期 | 类 | 一个引用可以封装状态与资源处理 |
| .NET 成员 API 背后的多种实现 | 接口 | 明确需要运行时分派和 .NET 调用方式 |
| 现有接口的一项局部实现 | 对象表达式 | 不需要可复用的具名实现 |
| 经测量适合按值复制的小型值，或有互操作布局要求 | 结构体 | 值类型表示本身就是需求的一部分 |

这些只是起点：记录和联合类型可以有成员并实现接口，类也可以保持不可变。只有类的身份、状态、生命周期或分派行为能改善模型时才选择类；“需要一个成员”本身并不是充分理由。

两个问题可以发现不必要的包装：

1. 如果把 `service.Execute x` 换成 `execute x` 并不会失去身份、生命周期、分派或 API 行为，那么函数可能更清楚。
2. 如果一个类只是保存公开构造参数，再原样暴露它们，那么不可变记录可能用更少机制表达同一模型。

## 类是 .NET 引用类型 {#classes}

示例对报价计算建模。`Quote` 仍是私有记录，错误仍用可辨识联合表示；只有计算器行为使用类：

```fsharp:line-numbers [Types.fs]
namespace ThinkingInFSharp.Ch25

type QuoteRequest = { Seats: int; UnitPrice: decimal }

type QuoteError =
    | NonPositiveSeats of actual: int
    | NegativeUnitPrice of actual: decimal
    | InvalidDiscountRate of actual: decimal

type Quote =
    private
        { Seats: int
          Subtotal: decimal
          Discount: decimal
          Tax: decimal
          Total: decimal }

module Quote =
    let seats quote = quote.Seats
    let subtotal quote = quote.Subtotal
    let discount quote = quote.Discount
    let tax quote = quote.Tax
    let total quote = quote.Total

type IDiscountPolicy =
    abstract Rate: QuoteRequest -> decimal

type IQuoteService =
    abstract Quote: QuoteRequest -> Result<Quote, QuoteError>

type PriceCalculator(taxRate: decimal, discountPolicy: IDiscountPolicy) =
    do
        if taxRate < 0M then
            invalidArg (nameof taxRate) "Tax rate cannot be negative."

    new(discountPolicy: IDiscountPolicy) = PriceCalculator(0M, discountPolicy)

    member _.TaxRate = taxRate

    member _.Calculate(request: QuoteRequest) =
        if request.Seats <= 0 then
            Error(NonPositiveSeats request.Seats)
        elif request.UnitPrice < 0M then
            Error(NegativeUnitPrice request.UnitPrice)
        else
            let discountRate = discountPolicy.Rate request

            if discountRate < 0M || discountRate > 1M then
                Error(InvalidDiscountRate discountRate)
            else
                let subtotal = decimal request.Seats * request.UnitPrice
                let discount = subtotal * discountRate
                let taxable = subtotal - discount
                let tax = taxable * taxRate

                Ok
                    { Seats = request.Seats
                      Subtotal = subtotal
                      Discount = discount
                      Tax = tax
                      Total = taxable + tax }

    interface IQuoteService with
        member this.Quote request = this.Calculate request

[<AutoOpen>]
module QuoteExtensions =
    type Quote with
        member this.IsDiscounted = Quote.discount this > 0M
        member this.TotalAmount = Quote.total this

[<Struct>]
type QuoteRevision = private QuoteRevision of int

module QuoteRevision =
    let create raw =
        if raw > 0 then Ok(QuoteRevision raw) else Error raw

    let value (QuoteRevision revision) = revision
```
在 `PriceCalculator(taxRate, discountPolicy)` 中，参数列表声明主构造函数。开头的 `do` 绑定属于该构造函数，每次创建实例都会执行。`new(discountPolicy)` 成员是附加构造函数；它必须委托给主构造函数，此处补上零税率。

构造函数参数在整个类中都处于作用域内。开头的 `let` 可以让字段或辅助函数保持私有，`member` 则把方法或属性暴露到 .NET 元数据中。成员不需要当前实例时使用 `_`；只有确实需要时才给自标识符命名。

### 让每种失败进入正确通道 {#constructor-invariants}

负数 `taxRate` 表示计算器配置错误，所以构造过程通过 `invalidArg` 抛出 `ArgumentException`。非正座位数属于预期输入错误，所以 `Calculate` 返回 `Error (NonPositiveSeats actual)`。这一区分取决于谁负责处理以及能否恢复，而不是语法。

避免让构造函数执行远程 I/O、启动无人负责的后台工作，或在初始化完成前发布 `this`。这些操作会让创建过程难以取消、重试、测试和释放。如果资源获取是异步的或可能失败，应使用小型、经过参数校验的构造函数，再另行提供工厂函数或启动方法。

### 身份不等于领域相等 {#class-identity}

类实例是引用。两个分别构造的计算器即使配置相同，也是不同引用。除非类专门定义了相等语义，否则不要假定它会像 F# 记录一样自动进行结构相等比较。

只有引用身份本身有意义时才使用 `obj.ReferenceEquals`。领域相等应比较明确且稳定的标识，或把值建模为记录/联合类型。覆盖 `Equals` 时还必须提供一致的哈希码，并规定可变性与继承行为；不要只为方便某项测试而添加它。

## 接口定义基于成员的 API {#interfaces}

接口声明一组相关的抽象成员，但不存储数据。当调用方需要基于成员的 .NET API，或必须在运行时选择不同实现时，`IQuoteService` 很合适。`IDiscountPolicy` 有意保持窄小；但如果调用方只使用 F#，它的单一成员也可以改成 `QuoteRequest -> decimal` 函数。

| 考量 | 这些情况优先函数或函数字段记录 | 这些情况优先接口 |
|---|---|---|
| 操作 | 一个操作或一小组操作已经足够 | 相关的具名成员形成稳定对象 API |
| 消费者 | 调用者主要使用 F#，组合发生在局部代码中 | 框架或其他 .NET 语言期待成员/运行时分派 |
| 状态/生命周期 | 依赖只是普通值 | 实现需要管理身份、状态或释放 |
| 演化 | 操作集合只在局部使用，易于整体替换 | 公开成员 API 有明确的兼容策略 |

F# 接口实现通常是显式的。`PriceCalculator.Calculate` 是类成员，而 `IQuoteService.Quote` 要通过接口视图调用：

```fsharp
let calculator = PriceCalculator(policy)
let service = calculator :> IQuoteService
let result = service.Quote request
```

这个向上转换直接显示了区别：使用具体类的调用方看到完整 API，使用接口的调用方只看到接口成员。接口应保持内聚和小巧。把每个函数都拆成一对 `IThing`/`Thing`，只会增加名称和间接层，不一定形成有用的抽象。

## 对象表达式在局部实现小型接口 {#object-expressions}

对象表达式基于接口或基类，创建由编译器生成的匿名对象类型实例。示例在应用组装依赖的位置提供团体折扣策略：

```fsharp
let groupDiscount =
    { new IDiscountPolicy with
        member _.Rate request =
            if request.Seats >= 5 then 0.10M else 0M }
```

它很适合一次性适配器、小型测试替身，或由局部值组装的策略。它仍会创建对象，也可以捕获可变状态；因此不会自动变成纯函数或线程安全实现。

当实现存在独立不变量、多个依赖、大量行为、复用需求、生命周期或诊断需求时，应给它命名。百行对象表达式只是隐藏了类型名称，并没有消除复杂度。

## 类型扩展增加调用方式，不改变存储 {#type-extensions}

示例在自动打开的 `QuoteExtensions` 模块中，为 `Quote` 增加派生成员 `IsDiscounted` 和 `TotalAmount`。它们属于 F# 可选扩展：现有值不会新增字段，运行时表示不变，反射也不会把它们列为 `Quote` 的属性。

固有扩展（intrinsic extension）与原类型声明在同一文件及同一命名空间或模块中，会编译为该类型的一部分，并出现在反射结果中。可选扩展必须位于模块内，调用方需要把该模块引入作用域；此处由 `[<AutoOpen>]` 自动完成。C# 与 Visual Basic 无法调用 F# 可选扩展。类型扩展不能增加虚成员、抽象成员或覆盖成员；调用发生歧义时，类型原有成员优先。

如果成员调用形式能让稳定的派生操作更容易找到，尤其是无法修改原类型时，可以使用扩展。如果更看重依赖是否清楚、管道顺序、跨语言调用，或不希望行为依赖模块作用域，则优先使用普通模块函数。

## 结构体改变语义，而不只是分配 {#structs}

结构体是 .NET 值类型。赋值、传参和返回通常会复制其值；装箱会创建一个包含副本的对象。存储位置取决于上下文——不能保证局部值就简单地“位于栈上”，装箱结构体则位于托管堆上——所以不要根据栈与堆的口号作选择。

示例中的 `[<Struct>]` 单用例联合 `QuoteRevision` 使用私有用例，智能构造函数只接受正整数。一般调用方无法构造 `QuoteRevision 0`，但 `Unchecked.defaultof<QuoteRevision>` 仍会产生其零初始化表示。每个结构体都有默认值；私有构造方式无法消除它。

这一事实带来几项设计义务：

- 让全零表示成为有效值，或在所有可能产生默认值的入口拒绝它；
- 谨慎处理数组、序列化器、互操作和可能零初始化值的泛型 API；
- 除非明确要求且理解复制行为，否则避免可变结构体；
- 除了分配收益，还要考虑装箱和接口调用成本；
- 用测量与互操作要求证明结构体合理，而不是凭审美选择。

在经过证明的热点中，小型不可变结构体记录或联合可能很有价值。普通领域数据更安全的默认选择是引用记录，因为其值语义更清楚，也不会悄然获得全零表示。

## 继承、组合与生命周期 {#inheritance-lifetime}

F# 类可以继承一个直接基类，并实现多个接口。只有框架本身要求基类时才使用继承，例如需要覆盖 UI 或宿主类型；不要只为共享辅助逻辑而继承。模块、函数组合、函数字段记录与内部对象通常能更清楚地显示依赖，也能避开脆弱的基类状态。

如果对象管理 `IDisposable` 或 `IAsyncDisposable` 资源，其 API 必须说明由谁、在何时释放，以及方法从何时起失效。第 21、23 章的 `use`/`use!` 规则仍然适用。把句柄藏进类不会消除生命周期，只会把责任转交给该类及其调用方。

带可变状态的类还需要并发策略。“私有”只能阻止直接访问字段，不能阻止同时调用成员。应依据第 24 章的规则发布不可变快照、串行处理访问，或同步整个不变量。

## 评审面向对象的公开 API {#api-review}

发布基于对象的 API 前，逐项检查：

- 函数、记录或联合类型是否能更直接地表达相同语义？
- 引用身份是否重要，相等行为是否明确？
- 哪些构造参数错误属于配置问题，哪些方法输入应产生类型化领域错误？
- 每个接口成员是否属于同一个内聚 API，而不是只为将来可能扩展而添加？
- 对象表达式是否保持局部且小巧？
- 类型扩展在需要它的作用域和语言中是否可发现？
- 结构体的零值是否有效，复制/装箱是否经过测量？
- 由谁处理释放、可变状态、取消和线程安全？
- 公开表示是否适合当前调用者与未来兼容约束？

第 27 章会从 C# 调用方的角度重新讨论最后一个问题。第 31 章会先提供测量结果，再讨论表示层面的优化。

## 运行共享示例 {#run-example}

在仓库根目录执行：

```console
dotnet run --project examples/chapters/ch25/Ch25.fsproj --configuration Release
```

程序用四行确定性输出覆盖类、接口、扩展和结构体；仓库检查会逐行核对结果。

## 练习 {#exercises}

### 练习 1：移除不必要的类 {#exercise-01}

某个 `SeatRequest` 类只通过只读属性保存标识与座位数。把它改成不可变记录；在模块函数中完成预期验证并返回 `Result`，再解释什么需求会证明保留类是合理的。

### 练习 2：选择策略的表示方式 {#exercise-02}

把同一折扣规则分别实现为函数和 `IDiscountPolicy` 对象表达式，并在计算中使用两者。然后说明：仅供 F# 使用的库会保留哪种公开 API，什么需求可以支持使用接口。

### 练习 3：审计结构体不变量 {#exercise-03}

通过智能构造函数创建正数修订号结构体，复制它，分别装箱两个副本，并观察其默认值。然后重新设计该类型，让零初始化表示一个有名称的有效状态；或者记录并测试所有可能产生默认值的来源都会拒绝它。

[阅读本章答案](../solutions/ch-25-objects-interfaces)。

## 资料来源 {#sources}

- [Microsoft Learn：F# 类](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/classes)
- [Microsoft Learn：F# 构造函数](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/members/constructors)
- [Microsoft Learn：F# 接口](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/interfaces)
- [Microsoft Learn：F# 对象表达式](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/object-expressions)
- [Microsoft Learn：F# 类型扩展](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/type-extensions)
- [Microsoft Learn：F# 结构体](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/structs)
