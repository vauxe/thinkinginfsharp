---
title: "Chapter 28 Solutions"
description: "Select the smallest test level by risk, hand-write a double for a missing-product path, and design compatible evolution for an optional JSON field."
translationKey: solutions/ch-28-testing-boundaries
---

# Chapter 28 Solutions {#overview}

The solutions state the risk before choosing which components to run. A broader runtime scope is worthwhile only when it tests something new; adding a host, network, or mock setup does not automatically increase confidence.

[Return to Chapter 28](../part-05/ch-28-testing-boundaries).

## Exercise 1: choose a test level for three risks {#exercise-01}

### Give each test only the participants it needs {#exercise-01-selection}

| Risk | Smallest level | Real participants | Replaced participants | Key assertion |
|---|---|---|---|---|
| Incorrect discount total | Pure value example test | Discount/pricing function | None | Complete result equals a hand-written money value |
| Save still occurs after insufficient stock | Port-double unit test | Workflow and pure decision | Product lookup, clock, save | Exact error; neither save nor clock is called |
| `orderId` drifts to `OrderId` | JSON boundary contract test | DTO, actual options, `System.Text.Json` | No serializer double | Parsed output contains `orderId` and not the wrong spelling |

The discount rule needs neither workflow nor serializer; adding them only adds unrelated failure sources. The save protocol must execute the workflow, but fixed product and time already provide sufficient control. A real database would not change the evidence about whether save was requested.

The spelling risk comes from the serializer and its configuration, so both must run for real. The test can still execute entirely in memory; testing a real boundary does not require starting a server. Add an outer test only for a separate risk, such as HTTP content type or database column mapping.

First confirm that each test detects its target risk. Temporarily use the wrong total, make the failure branch save, or change the naming policy. Check that the corresponding test fails, then restore the implementation.

## Exercise 2: write a double test without locking implementation {#exercise-02}

### Record only the port protocol callers can observe {#exercise-02-double}

The shared answer creates a product lookup returning `None`, records its SKU input, and gives clock and save independent recorders:

```fsharp
let request =
    match PlaceOrderCommand.create "ORD-28" "FSP-BOOK" 2 with
    | Ok value -> value
    | Error error -> failwithf "unexpected input: %A" error

let lookups = ResizeArray<string>()
let saved = ResizeArray<PlacedOrder>()
let mutable clockCalls = 0

let ports: OrderPorts =
    { FindProduct =
        fun sku ->
            lookups.Add sku
            None
      GetUtcNow =
        fun () ->
            clockCalls <- clockCalls + 1
            DateTimeOffset.MaxValue
      SaveOrder = saved.Add }

Assert.Equal(
    Error(ProductNotFound "FSP-BOOK"),
    OrderWorkflow.place ports request
)

Assert.True(([ "FSP-BOOK" ] = (lookups |> Seq.toList)))
Assert.Equal(0, clockCalls)
Assert.Empty saved
```

The test checks four related behaviors: the workflow looks up normalized `FSP-BOOK`; it returns `ProductNotFound "FSP-BOOK"`; it does not read time; and it does not save. The last two enforce “do not run success-path side effects after a failed decision.”

It does not assert the call count of `OrderDecision.decide`, nor know whether the workflow is a pipeline or `match`. The implementation may add a pure cache, rename a helper, or change composition form; while observable result and port protocol remain the same, the test stays green.

If product lookup itself throws, this chapter's workflow lets the exception propagate and neither clock nor save runs. Mapping that exception into a domain error would be a separate product decision, driven by a separate test rather than mixing a second scenario into this one with `try/with`.

## Exercise 3: design a JSON contract evolution {#exercise-03}

### Write a compatibility matrix before changing the DTO {#exercise-03-matrix}

Assume `note` is genuinely optional and both absence and null mean “no note.” Cover at least these cases before release:

| Writer | Reader | Input/output | Expected |
|---|---|---|---|
| Old writer | New reader | No `note` | Success; domain gets no note |
| New writer | New reader | Text `note` | Success and text is preserved |
| New writer | New reader | Null `note` | No note or explicit rejection, according to documented policy |
| New writer | Old reader | Contains `note` | Depends on old reader's unknown-member policy |
| New writer | Any reader | Output JSON | Existing names and types unchanged; only `note` is added |

The current sample uses `Disallow` for unknown members, so an old reader retaining that policy rejects a new writer's `note`. “Adding an optional JSON field” is therefore not compatible for that consumer relationship. Optionality describes validation in the new DTO; it does not guarantee old parsers accept the field.

There are three viable options:

1. Deploy readers that ignore or recognize `note` before writers emit it.
2. Create a versioned message or endpoint.
3. Allow unknown fields and accept that this may hide spelling mistakes.

Choose according to deployment order and error-detection needs, not by changing one record field in isolation.

The output contract test should parse JSON and prove the existing three fields remain while `note` appears or is omitted by policy. Input tests cover the four payloads above and error types. They need not fix property order or whitespace unless canonicalization is a separate protocol requirement.

### Case leniency is a behavioral change {#exercise-03-casing}

Changing `PropertyNameCaseInsensitive` from `false` to `true` makes `OrderId`, `ORDERID`, and other casing variants match `orderId`; correct camel-case input still passes. This usually expands the accepted input set, a behavioral compatibility change rather than a binary signature change.

Expansion can help migration but can also conceal sender drift. Add tests proving required variants are accepted and inspect what happens when one object contains two fields differing only by casing. If the result depends on order or becomes ambiguous, reject that payload or specify the protocol explicitly.

## Solution review {#solution-review}

- Pure calculation, port protocol, and JSON configuration need different real participants.
- A larger test is worth its cost only when it checks an additional risk.
- Doubles record externally visible calls, not private control flow.
- Keep one scenario per test; exception mapping belongs to another product decision.
- Whether a new field is compatible depends on old readers, not only whether it is “optional.”
- Strict unknown-member policy improves spelling detection but limits forward compatibility.
- Case leniency expands the input set and is a behavioral change to record in contract tests.
