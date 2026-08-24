---
title: "第 7 章练习答案"
description: "元组迁移、不可变更新、结构相等、引用身份、哈希契约与业务排序的推理答案。"
translationKey: solutions/ch-07-records-equality
kind: solution
part: 2
chapter: 7
status: complete
verifiedWith:
  fsharp: "10"
  dotnetSdk: "10.0.301"
exampleIds:
  - ch07-records-equality
exerciseIds:
  - ch07-exercise-01
  - ch07-exercise-02
  - ch07-exercise-03
termIds: []
sources:
  - id: microsoft-records
    url: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/records
    checked: "2026-08-24"
  - id: fsharp-core-language-primitives
    url: https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-languageprimitives.html
    checked: "2026-08-24"
---

# 第 7 章练习答案 {#overview}

这些答案分开讨论类型、内容、对象和领域身份。笼统说“它们相同”无法说明程序应使用哪一种规则。

[返回第 7 章](../part-02/ch-07-records-equality)。

## 练习 1：从元组迁移到记录 {#exercise-01}

一种完整改写是：

```fsharp
type BookingDraft =
    { EventId: string
      Attendee: string
      Seats: int }

let draft =
    { EventId = "A-1"
      Attendee = "Lin"
      Seats = 2 }

let format { EventId = eventId; Attendee = attendee; Seats = seats } =
    $"{eventId}:{attendee}:{seats}"

let attendee = draft.Attendee
```

记录消除了把两个 `string` 位置交换却仍通过类型检查的风险，也让调用方不必记忆第三个位置。它没有保证字符串非空、座位数为正、活动存在或容量足够；这些是不变量与工作流规则，第 12 章和贯穿项目会处理。

## 练习 2：追踪复制与身份 {#exercise-02}

共享定义可以直接用于预测：

<<< @/../examples/scripts/ch07-records-equality.fsx#equality-identity-hash{fsharp:line-numbers} [ch07-records-equality.fsx]

设 `updated = { original with Seats = 3 }`、`equalCopy` 与原字段完全相同、`alias = original`：

| 比较 | 结果 | 原因 |
| --- | --- | --- |
| `original = equalCopy` | `true` | 三个字段结构相等 |
| `PhysicalEquality original equalCopy` | `false` | 分别构造的引用对象 |
| `PhysicalEquality original alias` | `true` | 两个名称指向同一对象 |
| `original = updated` | `false` | `Seats` 不同 |

复制更新沿用未改变字段的值。若一个字段是数组，新旧记录可指向同一数组；随后改写数组元素会从两条路径都可见。问题在嵌套对象的可变性，不在记录字段被重新赋值。

## 练习 3：设计相等、哈希与顺序 {#exercise-03}

- 去除内容相同的草稿用结构相等，因为字段内容就是本需求的等价关系。
- 确认两个变量是否指向同一缓存对象用引用身份；若缓存契约以键定义对象，则更常直接比较显式缓存键。
- 真实预约是否为同一业务实体应比较明确的预约或请求 ID，不能从内容或对象身份猜测。
- 展示顺序应显式写成 `bookings |> List.sortByDescending (fun booking -> booking.Seats)`。若座位数相同时还要稳定按姓名升序，可用 `List.sortWith` 写出两级规则，而不是依赖记录字段声明顺序。

`hash x = hash y` 对结构相等是必要条件，不是充分条件。不同值可以碰撞，因此哈希相同不能替代 `x = y`；哈希也不说明两个引用是否为同一对象。

## 应该注意什么 {#what-to-notice}

- **名称修复可读性，不自动建立不变量：** 从元组迁移到记录只是建模的第一步。
- **不可变更新与深复制不同：** 记录本身未被改写，嵌套引用仍可能共享。
- **相等规则必须来自需求：** 内容、对象身份和业务身份可能各自合理，但不能混用。
- **哈希是索引机制：** 不持久化、不当 ID、不单独用来断言相等。
- **业务排序应显式：** 默认结构比较是语言提供的顺序，不一定是产品规则。
