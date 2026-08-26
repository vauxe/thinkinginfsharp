---
title: "第 26 章：深入 .NET 边界"
description: "跨越运行时类型、委托、事件、集合、身份、相等与哈希边界，同时保留 F# 领域语义。"
translationKey: part-05/ch-26-dotnet-runtime-boundaries
---

# 第 26 章：深入 .NET 边界 {#overview}

F# 值运行在 .NET 类型系统中。记录可以装箱为 `obj`，函数可以适配为委托，F# 事件可以发布为 CLI 事件，而 `seq<'T>` 正是 `IEnumerable<'T>` 的 F# 名称。因此互操作很直接，但“同一运行时”并不等于“相同语义”。

边界必须回答：哪些静态信息被擦除了，谁拥有订阅与可变集合，以及哈希表使用哪种相等关系。本章让这些选择显式化，然后尽早转回普通 F# 值。

## 学完本章，你将能够 {#outcomes}

学完本章后，你应该能够：

- 区分编译期类型与精确的运行时 `System.Type`；
- 准确使用 `typeof<'T>`、`GetType`、装箱、类型测试模式、向上转换和向下转换；
- 把不确定的运行时值和可空性当作待解码输入；
- 在边界把 F# 函数适配为 .NET 委托；
- 以显式订阅生命周期公开和消费 CLI 事件；
- 区分实时 `IEnumerable<'T>` 视图与已经保存的快照；
- 只有确实需要其协议时才选择可变 .NET 集合；
- 有意指定字典的相等比较器；
- 区分引用身份、值相等、排序与哈希；
- 维护相等/哈希契约并避免可变哈希键。

## 静态类型与运行时类型回答不同问题 {#static-runtime-types}

编译器为每个表达式赋予静态类型。该类型决定哪些操作可以编译，并承载程序的大部分保证。运行时还把每个对象实例关联到一个精确的 `System.Type`，供反射与面向对象分派检查。

脚本比较了这两种形式：

```fsharp:line-numbers [ch26-dotnet-runtime-boundaries.fsx]
let request = { RequestId = "R-26"; Seats = 3 }

let declaredType = typeof<BookingRequest>
let boxedRequest: objnull = box request

let actualType =
    match boxedRequest with
    | null -> failwith "boxing a non-null record unexpectedly produced null"
    | value -> value.GetType()

ensureEqual "runtime type" declaredType actualType
printfn "Runtime type: declared=%s actual=%s" declaredType.Name actualType.Name
```
`typeof<BookingRequest>` 获取编译期已知类型的元数据。`value.GetType()` 获取非空运行时实例的精确类型，即使引用的静态类型是基类或 `obj`。因为启用可空检查时，`obj` 形状的 .NET 输入可能为 null，示例先匹配 `null`，只在非空分支调用 `GetType`。

反射适合框架发现、序列化基础设施、插件加载、诊断和真正的动态协议。当领域选项集合已知时，不应让它替代可辨识联合。联合在编译期保留用例；检查 `System.Type` 则把漏掉用例的错误推迟到运行时。

### 装箱会丢失原来的静态类型信息 {#boxing}

`box value` 把值转换为 `objnull`。引用值经 `System.Object` 查看；值类型则被装箱进一个含副本的新对象。`unbox<'T>` 或向下转换必须恢复兼容的运行时类型。

反射、非泛型旧 API、格式化或基于对象的协议有时要求装箱。不要把 `obj` 当成方便的通用领域容器。它会丢弃静态信息，引入运行时检查与 null 处理，而且值类型可能产生分配。

泛型 .NET API 通常保留类型实参，所以应优先 `IEnumerable<BookingRequest>` 而不是非泛型 `IEnumerable`，优先 `Dictionary<RequestId, Booking>` 而不是存储对象值。

## 只有关系真实时才转换 {#casts}

三种操作拥有不同保证：

| 操作 | 检查时机 | 失败模型 | 合适用途 |
|---|---|---|---|
| `derived :> Base` / `upcast derived` | 编译期 | 已编译的有效向上转换不会在运行时失败 | 扩宽为已知基类或接口 |
| `value :?> Derived` / `downcast value` | 运行时 | 非空值不兼容时抛出 `InvalidCastException` | 边界契约已经保证运行时类型 |
| `match` 中的 `:? Derived as value` | 运行时分支 | 不匹配时选择其他分支 | 运行时类型确实不确定 |

共享解码器使用类型测试模式：

```fsharp:line-numbers [ch26-dotnet-runtime-boundaries.fsx]
let describeObject (value: objnull) =
    match value with
    | null -> "null"
    | :? string as text -> $"text:{text.ToUpperInvariant()}"
    | :? BookingRequest as booking -> $"request:{booking.RequestId}/{booking.Seats}"
    | :? int as number -> $"int:{number}"
    | _ -> "other"

let descriptions = [ box "lin"; box request; box 42 ] |> List.map describeObject

ensureEqual "pattern casts" [ "text:LIN"; "request:R-26/3"; "int:42" ] descriptions

printfn "Pattern casts: %A" descriptions

let failedDowncast =
    try
        let _: string | null = (box 42 :?> (string | null))
        "no-error"
    with :? InvalidCastException as error ->
        error.GetType().Name

ensureEqual "failed downcast" "InvalidCastException" failedDowncast
printfn "Failed downcast: %s" failedDowncast
```
尽管固定输入都非空，它仍先处理 null。每个成功的 `:?` 分支都会收窄该值并绑定类型化载荷。刻意错误的 `:?> string` 证明向下转换不会改变值的内容：装箱整数 `42` 不会变成文本，而会抛出 `InvalidCastException`。

`int64`、`decimal` 或已检查运算符等数值转换函数会改变表示，也可能带来溢出/舍入策略。向上/向下转换则在兼容对象类型之间移动。解析文本又是另一种操作，应进入 `Result`/`TryParse` 风格的验证。

动态输入只解码一次。返回类型化联合或 `Result`，随后让反射和转换离开领域核心。业务逻辑中反复出现 `:?` 检查，通常说明某个边界尚未建模。

## 委托是可调用的 .NET 对象 {#delegates}

F# 函数值和 .NET 委托都表示可调用行为，但它们的运行时类型与消费约定不同。在 F# 内部，优先函数值，以便柯里化、部分应用与组合。只有 .NET API 或跨语言公开契约要求时才使用委托。

脚本显式构造 `Func<int,int,int>` 和 `Converter<int,string>`：

```fsharp:line-numbers [ch26-dotnet-runtime-boundaries.fsx]
let add = Func<int, int, int>(fun left right -> left + right)

let labels =
    Array.ConvertAll([| 1; 2; 3 |], Converter<int, string>(fun number -> string (number * 2)))

ensureEqual "delegate invocation" 7 (add.Invoke(3, 4))
ensureEqual "delegate conversion" [| "2"; "4"; "6" |] labels
printfn "Delegates: add=%d labels=%A" (add.Invoke(3, 4)) labels
```
委托的 `Invoke` 方法执行调用。当参数类型已知为委托时，F# 常能自动适配兼容 lambda；但在重载解析有歧义、稍后必须保存/移除委托，或公开类型本身很重要时，显式构造函数更有用。

不要意外向期待 `Func<_,_>`/`Action<_>` 或具名委托的语言公开 `FSharpFunc<_,_>`；第 27 章会设计该公共 API。反过来，也不要只因应用运行在 .NET 上，就把所有内部函数换成委托。只在系统边界转换一次。

## 事件是一种订阅协议 {#events}

事件把可能触发通知的发布者与订阅观察者分开。本章发布者保存私有的 `Event<EventHandler<SeatsChangedEventArgs>, SeatsChangedEventArgs>`，只通过带 `[<CLIEvent>]` 的成员暴露 `Publish`：

```fsharp:line-numbers [ch26-dotnet-runtime-boundaries.fsx]
type SeatsChangedEventArgs(previous: int, current: int) =
    inherit EventArgs()

    member _.Previous = previous
    member _.Current = current

type CapacityPublisher(initial: int) =
    let changed = Event<EventHandler<SeatsChangedEventArgs>, SeatsChangedEventArgs>()
    let mutable current = initial

    [<CLIEvent>]
    member _.SeatsChanged = changed.Publish

    member this.SetSeats(next: int) =
        let previous = current
        current <- next
        changed.Trigger(this, SeatsChangedEventArgs(previous, next))

let publisher = CapacityPublisher(4)
let observations = ResizeArray<string>()

let handler =
    EventHandler<SeatsChangedEventArgs>(fun sender args ->
        assert (obj.ReferenceEquals(sender, publisher))
        observations.Add($"{args.Previous}->{args.Current}"))

publisher.SeatsChanged.AddHandler handler
publisher.SetSeats 2
publisher.SeatsChanged.RemoveHandler handler
publisher.SetSeats 1

let observedChanges = observations |> Seq.toList
ensureEqual "removed handler" [ "4->2" ] observedChanges
printfn "Event: observed=%A after-remove=%d" observedChanges observations.Count
```
`AddHandler` 与 `RemoveHandler` 使用同一个已保存委托实例。第一次更新被观察到；移除后第二次不再被观察。这不仅断言事件值，也断言生命周期。

对 F# 形状的事件，`.Subscribe` 返回 `IDisposable`；应以 `use` 绑定该订阅，或显式转移所有权。便捷的 `.Add` 会安装处理器但不返回移除令牌，所以当生命周期必须早于事件源结束时不要使用它。

订阅通常会让发布者保留处理器及其捕获对象的引用。因此忘记从寿命更长的发布者退订，可能留住本该死亡的订阅者。UI 拆卸、测试清理与应用关闭都需要明确所有者。

进程内事件不是持久消息总线、事务、重放日志、背压机制或错误隔离边界。处理器代码作为通知过程的一部分运行；应保持小巧，而且当正确性依赖多个处理器时，必须明确顺序/错误策略。

## .NET 集合暴露可变协议 {#dotnet-collections}

第 14 章按查找、更新、顺序与求值需求选择集合。引入 .NET 类型后仍要问同样问题：

| 类型/视图 | 重要语义 | 不应推断 |
|---|---|---|
| F# `list<'T>` | 不可变链式序列；元素支持时有结构相等/比较 | 低成本索引或原地增长 |
| `ResizeArray<'T>` / `List<T>` | 可变、可增长、可索引集合 | 不可变性、线程安全或稳定快照 |
| `seq<'T>` / `IEnumerable<T>` | 枚举协议；由来源决定何时/如何工作 | 可重复、有限、低成本或所有权 |
| `IReadOnlyList<T>` / 只读包装 | 不能通过该视图修改 | 底层存储不会改变 |
| `Dictionary<TKey,TValue>` | 使用一个 `IEqualityComparer<TKey>` 的可变哈希查找 | 已排序、领域相等或复合线程安全 |
| F# `Map<'K,'V>` | 使用 F# 比较的不可变有序映射 | 仅支持哈希的键或常数时间更新 |

脚本让实时视图与快照变得可观察：

```fsharp:line-numbers [ch26-dotnet-runtime-boundaries.fsx]
let mutableNumbers = ResizeArray<int>([ 1; 2 ])
let liveView: IEnumerable<int> = mutableNumbers
let snapshot = liveView |> Seq.toList
mutableNumbers.Add 3
let liveValues = liveView |> Seq.toList

ensureEqual "live enumerable" [ 1; 2; 3 ] liveValues
ensureEqual "list snapshot" [ 1; 2 ] snapshot
printfn ".NET list: live=%A snapshot=%A" liveValues snapshot

let bookingByEmail = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
bookingByEmail["lin@example.com"] <- "first"
bookingByEmail["LIN@EXAMPLE.COM"] <- "second"
let found, emailValue = bookingByEmail.TryGetValue "Lin@Example.com"

ensureEqual "case-insensitive key count" 1 bookingByEmail.Count
ensureEqual "case-insensitive lookup" (true, "second") (found, emailValue)

printfn "String comparer: count=%d found=%b value=%s" bookingByEmail.Count found emailValue
```
`liveView` 是同一个可变 `List<T>` 的 `IEnumerable<T>` 视图；在 `Add` 后枚举会看到新元素。`Seq.toList` 会在调用时保存一份独立的 F# 列表。只读接口会限制可用操作，但其他引用对底层所做的后续修改仍可能从该视图看到。

不要在枚举普通 `List<T>`/`Dictionary<TKey,TValue>` 时修改它们，也不要假定它们支持并发写入。在所有权边界只转换一次：如果核心需要稳定性，就复制外部可变输入；如果必须持续看到更新，就返回显式只读/实时契约。

F# 会把 .NET 的 `bool TryGetValue(key, out value)` 模式适配为元组形状调用，因此 `let found, value = dictionary.TryGetValue key` 可保持单次查找。如果 `found` 为 false，应把 out 值视为未指定/默认值，而不是有意义的领域数据。

## 哈希表需要相等策略 {#hash-equality}

`Dictionary<TKey,TValue>` 使用构造函数收到的比较器；若未提供，则使用 `EqualityComparer<TKey>.Default`。比较器同时回答“这些键是否相等”以及“应该搜索哪个哈希桶”。

邮件地址字典使用 `StringComparer.OrdinalIgnoreCase`，所以三个大小写变体命名同一个条目。该策略是显式、与区域性无关且局限于该字典的，并不会全局改变字符串相等。

必须分开四个相关概念：

| 概念 | 问题 | 代表操作 |
|---|---|---|
| 引用身份 | 它们是否是同一个对象实例？ | `obj.ReferenceEquals(a, b)` |
| 值/领域相等 | 这些值是否表达相同含义？ | F# `=`、`Equals` 或比较器 `Equals` |
| 哈希 | 哪个候选桶应包含这个键？ | `hash`、`GetHashCode`、比较器 `GetHashCode` |
| 排序 | 哪个值应排在另一个之前？ | F# `compare` 或 `IComparer<T>` |

引用身份无法被覆盖。对值类型调用 `ReferenceEquals` 会装箱每个实参，所以它不是值相等操作。字符串驻留也会让它不适合询问文本值是否相等。只有真正需要对象身份时才使用它。

### 默认类键与领域键 {#class-keys}

脚本创建两个携带相同 ID 的 `Customer` 实例。该类没有覆盖相等，因此默认字典把两个引用视为不同键。第二个字典接收由 `HashIdentity.FromFunctions` 构建的显式比较器：

```fsharp:line-numbers [ch26-dotnet-runtime-boundaries.fsx]
type Customer(customerId: string) =
    member _.CustomerId = customerId

let customerIdComparer: IEqualityComparer<Customer> =
    HashIdentity.FromFunctions
        (fun customer -> StringComparer.Ordinal.GetHashCode(customer.CustomerId))
        (fun left right -> StringComparer.Ordinal.Equals(left.CustomerId, right.CustomerId))

let firstCustomer = Customer("C-26")
let secondCustomer = Customer("C-26")
let sameReference = obj.ReferenceEquals(firstCustomer, secondCustomer)

let defaultKeys = Dictionary<Customer, string>()
defaultKeys[firstCustomer] <- "first"
defaultKeys[secondCustomer] <- "second"

let domainKeys = Dictionary<Customer, string>(customerIdComparer)
domainKeys[firstCustomer] <- "first"
domainKeys[secondCustomer] <- "second"

ensureEqual "separate references" false sameReference
ensureEqual "default class keys" 2 defaultKeys.Count
ensureEqual "domain class keys" 1 domainKeys.Count
ensureEqual "domain lookup" "second" domainKeys[firstCustomer]

printfn
    "Class keys: same-reference=%b default=%d domain=%d value=%s"
    sameReference
    defaultKeys.Count
    domainKeys.Count
    domainKeys[firstCustomer]
```
两个比较器函数都投影同一个不可变 `CustomerId`。因此第二次插入会替换第一次，字典只有一个领域键。在构造时选择比较器，让集合含义可供评审，而不必改变每个 `Customer` 用途的相等语义。

每个相等比较器都必须遵守这些规律：

- 相等具有自反性、对称性和传递性；
- 若 `Equals(a, b)` 为 true，两者必须产生相同哈希码；
- 不相等的值可以拥有相同哈希码——允许碰撞，随后由相等判断消解；
- 用于相等和哈希的字段在键存储期间不得变化；
- 哈希码是进程实现细节，不是持久 ID、签名或稳定顺序。

第 7 章为不可变记录建立了结构相等与哈希一致；第 11 章解释相等/比较约束；第 14 章区分有序 `Map`/`Set` 与哈希集合。在 .NET 边界，要明确接收 API 使用其中哪种契约。

## 保持类型化的边界工作流 {#boundary-workflow}

集成对象形状的 API 时，采用以下顺序：

1. 阅读其精确可空标注、重载、委托类型、集合接口和比较器规则。
2. 在薄适配器中解码 `objnull`、运行时变体、异常和 `Try` 模式。
3. 如果核心需要稳定快照，复制可变/实时数据。
4. 把回调和事件转换为有所有者的函数、任务、消息或可释放订阅。
5. 在构造集合时选择身份/相等/哈希策略；让键投影保持不可变。
6. 向 F# 核心返回记录、联合、`option`、`Result` 和函数。
7. 同时测试值结果与边界生命周期：处理器移除、枚举时机、比较器行为和失败类型。

这不是隔离 .NET，而是明确转换：适配器把宽泛的运行时协议翻译成领域真正需要的少量类型和操作。

## 运行共享示例 {#run-example}

在示例所在目录执行：

```console
dotnet fsi --checknulls+ --warnaserror+ --exec ch26-dotnet-runtime-boundaries.fsx
```

八行输出覆盖精确运行时类型、安全与失败转换、委托、事件移除、实时与复制集合、不区分大小写查找，以及默认引用键与领域键身份。先用上面的参数运行一次；若想比较未启用可空检查时的编译器行为，再去掉 `--checknulls+`。

## 练习 {#exercises}

### 练习 1：只解码一次对象边界 {#exercise-01}

为 `string`、`int` 和 `BookingRequest` 编写 `decode : objnull -> Result<BoundaryValue, DecodeError>`。显式处理 null 和不支持的运行时类型。程序其余部分只能匹配 `BoundaryValue`，不再执行转换。

### 练习 2：拥有事件订阅 {#exercise-02}

创建含 CLI 事件的容量发布者。订阅，触发一次变化，释放或移除订阅，再触发另一次变化。断言只观察到第一次，并指出由谁负责清理。

### 练习 3：定义字典键含义 {#exercise-03}

存储两个 ID 仅大小写不同的客户对象。用 `HashIdentity.FromFunctions` 构建序号不区分大小写比较器，在三个代表对象上验证相等/哈希规律，并证明第二次插入会替换第一次。解释可变客户 ID 为什么会破坏键协议。

[阅读本章答案](../solutions/ch-26-dotnet-runtime-boundaries)。

## 模型复盘 {#model-review}

- 静态类型在执行前防止错误；运行时类型支持真正的动态协议。
- 装箱擦除类型化视图，并可能为值类型产生分配。
- 向上转换静态有效；不确定的向下转换应进入类型测试分支。
- 运行时转换不是数值转换或解析。
- 函数是 F# 默认选择；委托是显式 .NET 适配器。
- 事件正确性包括订阅生命周期，而不只有交付值。
- `IEnumerable<T>` 可以是实时或延迟视图；立即枚举并保存结果会创建快照。
- 只读视图不能证明底层存储不可变。
- 字典语义来自比较器，而不是键字段名称。
- 身份、相等、哈希与排序是四项不同契约。

## 资料来源 {#sources}

- [Microsoft Learn：F# 转换](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/casting-and-conversions)
- [Microsoft Learn：`Object.GetType`](https://learn.microsoft.com/en-us/dotnet/api/system.object.gettype?view=net-10.0)
- [Microsoft Learn：F# 委托](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/delegates)
- [Microsoft Learn：F# 事件](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/members/events)
- [Microsoft Learn：.NET 集合与数据结构](https://learn.microsoft.com/en-us/dotnet/standard/collections/)
- [Microsoft Learn：`Dictionary<TKey,TValue>.Comparer`](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2.comparer?view=net-10.0)
- [FSharp.Core 参考：`HashIdentity`](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-collections-hashidentity.html)
- [Microsoft Learn：`Object.ReferenceEquals`](https://learn.microsoft.com/en-us/dotnet/api/system.object.referenceequals?view=net-10.0)
