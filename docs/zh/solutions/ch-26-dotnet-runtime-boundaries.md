---
title: "第 26 章答案"
description: "只解码一次对象输入、拥有事件订阅，并证明自定义字典比较器遵守相等与哈希契约。"
translationKey: solutions/ch-26-dotnet-runtime-boundaries
---

# 第 26 章答案 {#overview}

每项答案都把运行时协议限制在一个边缘。由此，领域代码只会看到联合类型、有明确所有者的订阅，或在构造时固定键策略的字典。

[返回第 26 章](../part-05/ch-26-dotnet-runtime-boundaries)。

## 练习 1：只解码一次对象边界 {#exercise-01}

### 把运行时选项转换为封闭联合 {#exercise-01-decoder}

```fsharp
open System

type BookingRequest =
    { RequestId: string
      Seats: int }

type BoundaryValue =
    | Text of string
    | Count of int
    | Request of BookingRequest

type DecodeError =
    | NullValue
    | UnsupportedType of Type

let decode (input: objnull) =
    match input with
    | null -> Error NullValue
    | :? string as text -> Ok(Text text)
    | :? int as count -> Ok(Count count)
    | :? BookingRequest as request -> Ok(Request request)
    | value -> Error(UnsupportedType(value.GetType()))

let request = { RequestId = "R-26"; Seats = 2 }

let decoded =
    [ box "Lin"; box 3; box request ]
    |> List.map decode

assert (decoded = [ Ok(Text "Lin"); Ok(Count 3); Ok(Request request) ])
assert (decode null = Error NullValue)

match decode (box 1.5M) with
| Error(UnsupportedType runtimeType) -> assert (runtimeType = typeof<decimal>)
| outcome -> failwithf "unexpected outcome: %A" outcome
```

只有 `decode` 知道 `objnull`、`:?` 和 `GetType`。下游函数可以穷尽匹配 `Text`、`Count` 与 `Request`；不支持的运行时类型无法以未检查转换形式泄漏。

null 与不支持类型应该属于一个还是两个错误用例，是领域策略。在边界诊断中保留 `System.Type` 很有用；在适配器之后仍让反射决定业务行为则不合适。

## 练习 2：拥有事件订阅 {#exercise-02}

### 让释放可观察 {#exercise-02-subscription}

```fsharp
open System

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

let publisher = CapacityPublisher(5)
let observed = ResizeArray<int * int>()

let subscription =
    publisher.SeatsChanged.Subscribe(fun args ->
        observed.Add(args.Previous, args.Current))

publisher.SetSeats 3
subscription.Dispose()
publisher.SetSeats 1

assert (observed |> Seq.toList = [ (5, 3) ])
```

创建 `subscription` 的组合作用域拥有它。在应用中，该作用域应以 `use` 绑定它、把它保存在实现释放的所有者组件中，或显式转移责任。测试只为证明生命周期边界，才在中途释放。

发布者拥有事件触发和当前容量，但不拥有任意订阅者的生命周期。寿命更长的发布者留住未移除处理器，正是泄漏风险。

## 练习 3：定义字典键含义 {#exercise-03}

### 用同一个不可变投影完成相等与哈希 {#exercise-03-comparer}

```fsharp
open System
open System.Collections.Generic

type Customer(customerId: string, displayName: string) =
    member _.CustomerId = customerId
    member _.DisplayName = displayName

let customerIdIdentity: IEqualityComparer<Customer> =
    HashIdentity.FromFunctions
        (fun customer ->
            StringComparer.OrdinalIgnoreCase.GetHashCode(customer.CustomerId))
        (fun left right ->
            StringComparer.OrdinalIgnoreCase.Equals(
                left.CustomerId,
                right.CustomerId
            ))

let first = Customer("customer-26", "Lin")
let second = Customer("CUSTOMER-26", "Ada")
let third = Customer("Customer-26", "Mira")

let equal left right = customerIdIdentity.Equals(left, right)
let hashOf value = customerIdIdentity.GetHashCode value

assert (equal first first)
assert (equal first second = equal second first)
assert (equal first second && equal second third && equal first third)
assert (hashOf first = hashOf second && hashOf second = hashOf third)

let byCustomer = Dictionary<Customer, string>(customerIdIdentity)
byCustomer[first] <- "first"
byCustomer[second] <- "second"

assert (byCustomer.Count = 1)
assert (byCustomer[third] = "second")
```

显示名称不参与键含义，所有 ID 操作都使用同一种序号不区分大小写规则。因此相等 ID 产生相等哈希，并只占一个字典条目。

如果 `CustomerId` 在插入后发生变化，比较器可能把查找导向不同于插入位置的哈希桶。该条目可能无法再找到，移除也可能失败。应保持键投影不可变；要重命名键，就在显式操作中移除旧键，再用新的不可变值插入。

这些断言抽样检查规律，却不能证明所有字符串都成立。第 29 章会把对称性、相等值哈希一致等规律变成生成性质。

## 答案复盘 {#solution-review}

- 只解码一次运行时选项，并返回封闭的类型化结果。
- 把 `System.Type` 留给诊断，不要让它反复参与领域分派。
- 把事件订阅释放视为所有权断言。
- 订阅创建者必须释放它，或转移这项义务。
- 从同一个不可变投影与比较规则构建字典相等和哈希。
- 相等键必须拥有相等哈希；哈希碰撞不代表相等。
- 键存储期间绝不能修改相等/哈希投影。
