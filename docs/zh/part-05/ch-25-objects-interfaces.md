---
title: "第 25 章：在 F# 中定义对象"
description: "从语义而非仪式出发，在函数、记录、联合类型、类、接口、对象表达式、扩展与结构体之间作选择。"
translationKey: part-05/ch-25-objects-interfaces
---

# 第 25 章：在 F# 中定义对象 {#overview}

F# 是 .NET 语言，所以类、成员、接口、继承和值类型都是语言的原生部分，并非外来的逃生通道。它们也不是每个函数式模型最终都必须披上的成熟外壳。函数能表达行为，记录能命名数据，可辨识联合能封闭一组状态，而不需要再包一层类。

本章从类型必须保留的含义出发。只有当引用身份、隐藏状态、构造工作、生命周期、子类型分派或 .NET 契约确实有用时，才引入对象表示。目标不是“函数式对面向对象”，而是为问题选择最小且诚实的边界。

## 学完本章，你将能够 {#outcomes}

学完本章后，你应该能够：

- 在函数、记录、可辨识联合、类、接口和结构体之间作选择；
- 定义含主构造函数、初始化、附加构造函数、属性和方法的类；
- 区分无效对象配置与预期领域拒绝；
- 通过接口视图调用显式实现的接口；
- 用对象表达式提供小型局部实现；
- 用类型扩展添加派生行为，而不假装增加了存储数据；
- 解释引用身份与值复制语义；
- 识别零初始化是结构体不变量边界；
- 除非真实基类契约要求继承，否则优先组合；
- 把资源所有权与释放视为对象公开契约的一部分。

## 先选表示，再选语法 {#representation-first}

先问调用者必须知道什么，以及运行时必须保留什么：

| 需要 | 通常先考虑 | 原因 |
|---|---|---|
| 一项变换或策略 | 函数 | 输入—输出契约本身已经是抽象 |
| 具名的不可变乘积数据 | 记录 | 字段与结构化操作直接描述该值 |
| 一组封闭形状或状态中的一种 | 可辨识联合 | 用例与穷尽匹配显式呈现各种可能 |
| 一起携带的一组相关依赖 | 函数字段记录 | 调用者能组装一组小而显式的能力 |
| 身份、隐藏的演化状态或自有生命周期 | 类 | 一个引用可以封装状态与资源协议 |
| .NET 成员契约背后的多种实现 | 接口 | 有意使用运行时分派与生态消费方式 |
| 现有对象契约的一项局部实现 | 对象表达式 | 不需要可复用的具名实现 |
| 有复制语义的小型已测量值，或互操作布局要求 | 结构体 | 值类型表示本身就是需求的一部分 |

这些只是起点，并非语法禁令。记录和联合类型可以有成员、可以实现接口；类也可以不可变。有成员不等于需要类，使用类也不会自动带来更好的架构。

两个快速问题可以暴露仪式性包装：

1. 如果把 `service.Execute x` 换成 `execute x` 并不会失去身份、生命周期、分派或契约，那么函数可能是更清楚的 API。
2. 如果一个类只是保存公开构造参数，再原样暴露它们，那么不可变记录可能用更少机制表达同一模型。

## 类是 .NET 引用类型 {#classes}

经过验证的本章示例对报价计算建模。`Quote` 仍采用私有记录表示，错误仍是可辨识联合；只有计算器行为用类表示：

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

负数 `taxRate` 表示计算器本身配置错误，所以构造过程通过 `invalidArg` 抛出 `ArgumentException`。非正座位数是预期输入结果，所以 `Calculate` 返回 `Error (NonPositiveSeats actual)`。决定这种区分的是所有权与可恢复性，而不是语法。

避免让构造函数执行远程 I/O、启动无人拥有的后台工作，或在初始化完成前发布 `this`。这些操作会让创建难以取消、重试、测试和释放。获取过程异步或可能失败时，优先采用小型且经过验证的构造函数，再提供显式工厂或启动方法。

### 身份不等于领域相等 {#class-identity}

类实例是引用。两个分别构造的计算器即使配置等价，也可能是不同引用。除非类有意提供了相等语义，否则不要假定它像 F# 记录那样拥有生成的结构相等。

只有身份本身有意义时才使用 `obj.ReferenceEquals`。领域相等应比较显式稳定标识，或把值建模为记录/联合类型。覆盖 `Equals` 还必须提供一致的哈希码，并明确可变性与继承策略；不要只为方便某项测试而添加它。

## 接口是契约，不是依赖注入装饰 {#interfaces}

接口声明一组相关抽象成员，但不存储数据。当消费者需要 .NET 形状的服务契约，或必须用运行时分派选择多种实现时，`IQuoteService` 很合适。`IDiscountPolicy` 被刻意做得很窄；但对仅使用 F# 的调用者而言，它的单一成员也可以是 `QuoteRequest -> decimal` 函数。

| 边界 | 这些情况优先函数或函数字段记录 | 这些情况优先接口 |
|---|---|---|
| 形状 | 一个操作或一小组能力已经足够 | 相关具名成员形成稳定对象契约 |
| 消费者 | 调用者主要使用 F#，组合发生在词法位置 | 框架或其他 .NET 语言期待成员/运行时分派 |
| 状态/生命周期 | 依赖只是普通值 | 实现拥有身份、状态或释放行为 |
| 演化 | 能力局部存在，易于整体替换 | 有意维护公开成员契约及兼容策略 |

F# 接口实现通常是显式的。`PriceCalculator.Calculate` 是类成员，而 `IQuoteService.Quote` 要通过接口视图调用：

```fsharp
let calculator = PriceCalculator(policy)
let service = calculator :> IQuoteService
let result = service.Quote request
```

这个向上转换提供了有用证据：使用具体类的调用者看到具体 API，使用接口的调用者只看到契约。接口应保持内聚和小巧。把每个函数都拆成一对 `IThing`/`Thing`，只会增加名称和间接层，并不一定增加真正的边界。

## 对象表达式在局部实现小型契约 {#object-expressions}

对象表达式基于接口或基类，创建编译器生成的匿名对象类型实例。示例在组合根提供团体折扣策略：

```fsharp
let groupDiscount =
    { new IDiscountPolicy with
        member _.Rate request =
            if request.Seats >= 5 then 0.10M else 0M }
```

它很适合一次性适配器、小型测试替身，或由局部值组装的策略。它仍会创建对象，也可以捕获可变状态；因此不会自动变成纯函数或线程安全实现。

当实现拥有独立不变量、多个协作者、大量行为、复用需求、生命周期或诊断需求时，应给它命名。百行对象表达式只是隐藏了类型名称，并没有消除复杂度。

## 类型扩展添加视图，不添加表示 {#type-extensions}

示例在自动打开的 `QuoteExtensions` 模块内，把 `Quote.IsDiscounted` 和 `Quote.TotalAmount` 添加为派生成员。它们是 F# 可选扩展：现有值没有新增字段，运行时表示没有改变，反射也不会把它们报告为 `Quote` 的属性。

内部扩展与被扩展类型声明在同一文件及同一命名空间/模块中，会编译为该类型的一部分并出现在反射中。可选扩展必须位于模块内；调用者要把该模块带入作用域——此处由 `[<AutoOpen>]` 自动完成——而 C# 或 Visual Basic 消费者无法调用它。类型扩展不能添加虚/抽象成员或覆盖成员，发生歧义时非扩展成员优先。

当成员调用形式能改善稳定派生操作的可发现性，尤其是无法修改原类型时，可以使用扩展。当显式依赖、管道顺序、跨语言可见性或避免依赖作用域的发现方式更重要时，优先普通模块函数。

## 结构体改变语义，而不只是分配 {#structs}

结构体是 .NET 值类型。赋值、传参和返回通常会复制其值；装箱会创建一个包含副本的对象。存储位置取决于上下文——不能保证局部值就简单地“位于栈上”，装箱结构体则位于托管堆上——所以不要根据栈与堆的口号作选择。

示例中的 `[<Struct>]` 单用例联合 `QuoteRevision` 拥有私有用例，智能构造函数只接受正整数。普通调用者无法构造 `QuoteRevision 0`，但 `Unchecked.defaultof<QuoteRevision>` 仍会产生其零初始化表示。每个结构体都有默认值；构造私有性无法消除它。

这一事实带来几项设计义务：

- 让全零表示成为有效值，或在所有可能产生默认值的边界拒绝它；
- 谨慎处理数组、序列化器、互操作和可能零初始化值的泛型 API；
- 除非明确要求且理解复制行为，否则避免可变结构体；
- 除了分配收益，还要考虑装箱和接口调用成本；
- 用测量与互操作要求证明结构体合理，而不是凭审美选择。

在经过证明的热点中，小型不可变结构体记录或联合可能很有价值。普通领域数据更安全的默认选择是引用记录，因为其值语义更清楚，也不会悄然获得全零表示。

## 继承、组合与生命周期 {#inheritance-lifetime}

F# 类可以继承一个直接基类，并实现多个接口。当框架契约本来就是基类时才使用继承，例如覆盖 UI 或宿主类型；不要只为共享辅助逻辑而继承。模块、函数组合、能力记录与被包含对象通常能让依赖更可见，也能避开脆弱的基类状态。

如果对象拥有 `IDisposable` 或 `IAsyncDisposable` 资源，它的 API 必须说明由谁、在何时释放，以及方法从何时起不再有效。第 21、23 章的 `use`/`use!` 规则仍然适用。把句柄藏进类并不会消除其生命周期；只是把责任移交给该类及其调用契约。

带可变状态的类还需要并发策略。“私有”只能阻止直接访问字段，不能阻止同时调用成员。应依据第 24 章规则发布不可变值、序列化所有权，或同步整个不变量。

## 精简的公开 API 复核表 {#api-review}

发布对象形状的 API 前，逐项询问：

- 函数、记录或联合类型是否能更直接地表达相同语义？
- 引用身份是否重要，相等行为是否明确？
- 哪些构造参数错误属于配置问题，哪些方法输入应产生类型化领域错误？
- 每个接口成员是否属于同一个内聚契约，而不是臆测的扩展性？
- 对象表达式是否保持局部且小巧？
- 类型扩展在需要它的作用域和语言中是否可发现？
- 结构体的零值是否有效，复制/装箱是否经过测量？
- 谁拥有释放、可变性、取消和线程安全责任？
- 公开表示是否适合当前调用者与未来兼容约束？

第 27 章会从 C# 消费者角度重访最后一个问题。第 31 章会先提供测量，再讨论表示级优化。

## 运行已验证示例 {#run-example}

在示例所在目录执行：

```console
dotnet run --project Ch25.fsproj --configuration Release
dotnet test Sample.slnx --configuration Release --filter FullyQualifiedName~Ch25Object
```

程序打印类、接口、扩展与结构体观察结果。聚焦测试覆盖两个构造函数、成员验证、显式接口视图、对象表达式替身、扩展派生成员、值复制、不同装箱对象，以及无效的零初始化修订号。

## 练习 {#exercises}

### 练习 1：移除仪式性类 {#exercise-01}

某个 `SeatRequest` 类只通过只读属性保存标识与座位数。把它改成不可变记录；在模块函数中完成预期验证并返回 `Result`，再解释什么需求会证明保留类是合理的。

### 练习 2：选择策略边界 {#exercise-02}

把同一折扣规则分别实现为函数和 `IDiscountPolicy` 对象表达式，并在计算中使用两者。然后说明：仅供 F# 使用的库会保留哪种公开边界，什么条件可能证明接口合理。

### 练习 3：审计结构体不变量 {#exercise-03}

通过智能构造函数创建正数修订号结构体，复制它，分别装箱两个副本，并观察其默认值。然后重新设计该类型，让零初始化表示一个显式有效状态；或者记录并测试所有可能产生默认值的边界都会拒绝它。

[阅读本章答案](../solutions/ch-25-objects-interfaces)。

## 模型复盘 {#model-review}

- F# 对象特性是原生工具，并非每个模型的必然终点。
- 函数表达行为，记录表达乘积，联合表达选项；类增加引用身份与对象协议。
- 构造失败与预期方法拒绝需要不同策略。
- 显式接口实现通过接口视图消费。
- 对象表达式适合小型局部实现；规模与生命周期会证明具名类型合理。
- 扩展添加可调用行为，但不添加存储状态或改变表示。
- 结构体按值复制，而且始终具有零初始化的默认表示。
- 默认选择组合；只有真实基类契约才使用继承。
- 封装会转移生命周期与并发责任，而不是消除它们。

## 资料来源 {#sources}

- [Microsoft Learn：F# 类](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/classes)
- [Microsoft Learn：F# 构造函数](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/members/constructors)
- [Microsoft Learn：F# 接口](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/interfaces)
- [Microsoft Learn：F# 对象表达式](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/object-expressions)
- [Microsoft Learn：F# 类型扩展](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/type-extensions)
- [Microsoft Learn：F# 结构体](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/structs)
