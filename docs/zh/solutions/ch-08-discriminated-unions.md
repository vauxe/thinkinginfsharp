---
title: "第 8 章练习答案"
description: "标志组合、联合案例、穷尽性与状态转换策略的推理答案。"
translationKey: solutions/ch-08-discriminated-unions
---

# 第 8 章练习答案 {#overview}

联合建模答案不仅要能编译，还应说明哪些组合被消除、哪些政策仍留给函数。

[返回第 8 章](../part-02/ch-08-discriminated-unions)。

## 练习 1：拆除标志组合 {#exercise-01}

三个独立布尔值产生 `2³ = 8` 种组合。若只允许邮件、短信、禁用三种互斥状态，则只有 `(true,false,false)`、`(false,true,false)`、`(false,false,true)` 合法，其他五种都要额外拒绝。

联合直接表达合法集合：

```fsharp
type NotificationTarget =
    | Email of address: string
    | Sms of phoneNumber: string
    | Disabled of reason: string
```

现在每个构造值都只选择一种目标：邮件地址、短信号码或禁用原因。智能构造函数或验证可以在保留三用例形状的同时，补充字符串格式保证。

## 练习 2：证明穷尽性 {#exercise-02}

穷尽函数为：

```fsharp
let shortLabel status =
    match status with
    | Pending -> "P"
    | Confirmed _ -> "C"
    | Cancelled _ -> "X"
```

增加 `Waitlisted of position: int` 后，这个匹配应产生 FS0025，维护者必须决定新标签，例如 `"W"`。若旧函数以 `_ -> "?"` 结尾，新增案例会静默得到 `"?"`；编译器无法区分这是有意兼容还是遗漏。

通配符并非始终错误。若函数只问“是否为 Pending”，且所有当前与未来非 Pending 案例确实同样处理，`| _ -> false` 可以准确表达剩余集合。关键是未来案例政策是否真的相同。

## 练习 3：设计转换策略 {#exercise-03}

最小函数为：

```fsharp
let cancel reason status =
    match status with
    | Pending
    | Confirmed _ -> Cancelled reason
    | Cancelled _ -> status
```

返回类型只有 `BookingStatus`，因此调用方无法区分“刚刚取消”与“先前已经取消”；也拿不到旧取消原因，无法判断重复请求是否一致。若某些转换不允许，接口可以返回 `Result<BookingStatus, string>`，成功携带新状态，失败携带原因。下一章会用领域化错误替代裸字符串，并组合这种结果。

## 应该注意什么 {#what-to-notice}

- **联合缩小表示空间：** 五种非法标志组合不再需要逐次验证。
- **案例数据仍需自身不变量：** `Email ""` 在这里仍能构造，后续要保护。
- **穷尽诊断是演进工具：** 新案例迫使显式匹配重新决定政策。
- **通配符表达一组剩余值：** 只有这组值真正共享规则时才使用。
- **合法状态不等于合法转换：** 联合保护形状，`Result` 等返回类型表达转换失败。
