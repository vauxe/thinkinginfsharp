---
title: "Chapter 28: Example Tests, Test Doubles, and Boundary Tests"
description: "Choose pure value tests, hand-written deterministic doubles, and real serialization contract tests by failure risk instead of testing implementation details."
translationKey: part-05/ch-28-testing-boundaries
---

# Chapter 28: Example Tests, Test Doubles, and Boundary Tests {#overview}

A test is not a second description of source structure. It is executable evidence against a risk. A wrong total calculation needs a minimal input-output example. A workflow that still saves after failure requires observing its port protocol. A drift in JSON field names or deserialization options can only be caught by a contract test that invokes the real serializer.

Ask “what must this failure prove?” before selecting a test level. If every test starts a database, feedback becomes slow and hard to localize. If every test replaces the serializer and database, nothing proves that real boundaries still agree. A good test portfolio uses cheap evidence for most logic and concentrates cost where a genuine boundary risk exists.

## What you will be able to do {#outcomes}

By the end of this chapter, you should be able to:

- choose a test level for pure computation, port protocol, library configuration, or real infrastructure risk;
- assert pure-function outcomes directly with F# values and structural equality;
- assert an exact counterexample carrying context for an error case;
- compose small deterministic test doubles from records of functions;
- distinguish result-state assertions from necessary observable-interaction assertions;
- test a DTO contract with the real `System.Text.Json` configuration;
- avoid testing private functions, internal call order, and semantically irrelevant JSON text details;
- write fast, isolated, repeatable, self-checking, clearly named xUnit tests;
- use a red-green-refactor loop whose tests constrain behavior rather than implementation.

## Choose the smallest sufficient evidence from risk {#risk-matrix}

A “unit” need not mean one class or one function. It is the unit of work this test deliberately controls. The following levels answer different questions:

| Risk | Smallest sufficient test | Real participants | Usually avoid |
|---|---|---|---|
| Calculation, branching, invariant | Pure value example test | Domain function and ordinary values | Clock, network, file, database |
| How a workflow uses ports | Unit test with hand-written doubles | Workflow; ports replaced by deterministic functions | Mock framework, real infrastructure |
| Serialization, C# surface, database mapping | Boundary contract test | Real library, options, metadata, or adapter | Whole application host |
| Whether components and infrastructure work together | Integration test | Real components that must compose | Replacing the boundary under test |
| Critical user path | A few end-to-end tests | Complete deployed-shaped path | Enumerating every domain branch |

A test's name does not determine evidence strength. A test named “integration” that replaces the real protocol still cannot prove that protocol. An in-memory JSON contract test can genuinely verify serializer configuration. Categories explain scope; they are not labels to argue over.

## Pure functions are best tested with values {#pure-value-tests}

The shared sample first represents the command, product snapshot, draft, and error as ordinary F# values. The only inputs to `decide` are a product snapshot and validated command; its only output is a `Result`:

```fsharp:line-numbers [OrderWorkflow.fs]
module OrderDecision =
    let decide (product: ProductSnapshot option) (command: PlaceOrderCommand) : Result<OrderDraft, OrderDecisionError> =
        let requestedSku = PlaceOrderCommand.sku command
        let requestedQuantity = PlaceOrderCommand.quantity command

        match product with
        | None -> Error(ProductNotFound requestedSku)
        | Some snapshot when not (StringComparer.Ordinal.Equals(snapshot.Sku, requestedSku)) ->
            Error(ProductNotFound requestedSku)
        | Some snapshot when requestedQuantity > snapshot.Available ->
            Error(InsufficientStock(requestedQuantity, snapshot.Available))
        | Some snapshot ->
            Ok
                { OrderId = PlaceOrderCommand.orderId command
                  Sku = requestedSku
                  Quantity = requestedQuantity
                  Total = decimal requestedQuantity * snapshot.UnitPrice }
```
There is no hidden clock, database, or randomness, so a test needs no object graph. Arrange the inputs, call once, then compare the complete result:

```fsharp
let private expectOk result =
    match result with
    | Ok value -> value
    | Error error -> failwithf "Expected Ok, received Error %A" error

let request =
    PlaceOrderCommand.create "ORD-28" "FSP-BOOK" 3 |> expectOk

let snapshot = ProductSnapshot.create "FSP-BOOK" 19.50M 2

[<Fact>]
let ``pure decision reports the exact stock counterexample`` () =
    Assert.Equal(
        Error(InsufficientStock(3, 2)),
        OrderDecision.decide (Some snapshot) request
    )
```

The first test compares the accepted result with one `OrderDraft` value. Structural equality for records, unions, and `Result` keeps the assertion in domain vocabulary; there is no need to call getters field by field or verify which private helper ran.

The second test chooses the smallest stock counterexample: requested 3, available 2. `InsufficientStock(3, 2)` states not only failure but the context a caller needs for diagnosis or recovery. If a future algorithm still rejects the request but swaps the numbers, the test exposes a changed contract.

### Assert output instead of copying the algorithm {#assert-output}

Expected values in tests should be small, explicit examples. Do not recalculate `decimal quantity * unitPrice`, copy the production filter, or reproduce branches with a loop in test code; the same mistake can then live in both implementation and “expected algorithm.”

One test may have several assertions when they jointly prove one behavior. The JSON shape test, for example, checks three field names and values while the failure still means one output contract. Conversely, a test that mixes pricing, save failure, and serialization is hard to localize and should be split.

Structural equality does not mean a larger assertion is always better. If a huge aggregate contains fields irrelevant to the behavior, constructing a full expected value makes unrelated evolution break the test. Assert the smallest meaningful projection that expresses the behavior.

## Workflow tests need controllable ports {#port-tests}

Outside the pure core, the sample workflow reads a product, reads time, and saves an order. Dependencies are a record of functions, and a short `match` makes effect ordering explicit:

```fsharp:line-numbers [OrderWorkflow.fs]
type PlacedOrder =
    { OrderId: string
      Sku: string
      Quantity: int
      Total: decimal
      PlacedAt: DateTimeOffset }

type OrderPorts =
    { FindProduct: string -> ProductSnapshot option
      GetUtcNow: unit -> DateTimeOffset
      SaveOrder: PlacedOrder -> unit }

module OrderWorkflow =
    let place (ports: OrderPorts) (command: PlaceOrderCommand) : Result<PlacedOrder, OrderDecisionError> =
        let product = command |> PlaceOrderCommand.sku |> ports.FindProduct

        match OrderDecision.decide product command with
        | Error error -> Error error
        | Ok draft ->
            let placed =
                { OrderId = draft.OrderId
                  Sku = draft.Sku
                  Quantity = draft.Quantity
                  Total = draft.Total
                  PlacedAt = ports.GetUtcNow() }

            ports.SaveOrder placed
            Ok placed
```
Only the success branch reads the clock and saves; a decision failure returns directly. The success test composes ports from closures: fixed product and time returns, `ResizeArray` values recording lookup and save, and a counter recording clock reads:

```fsharp
let request =
    match PlaceOrderCommand.create "ORD-28" "FSP-BOOK" 2 with
    | Ok value -> value
    | Error error -> failwithf "unexpected input: %A" error

let snapshot = ProductSnapshot.create "FSP-BOOK" 19.50M 5
let now = DateTimeOffset(2026, 8, 24, 9, 30, 0, TimeSpan.Zero)
let lookups = ResizeArray<string>()
let saved = ResizeArray<PlacedOrder>()
let mutable clockCalls = 0

let ports: OrderPorts =
    { FindProduct =
        fun sku ->
            lookups.Add sku
            Some snapshot
      GetUtcNow =
        fun () ->
            clockCalls <- clockCalls + 1
            now
      SaveOrder = saved.Add }

let expected: PlacedOrder =
    { OrderId = "ORD-28"
      Sku = "FSP-BOOK"
      Quantity = 2
      Total = 39.00M
      PlacedAt = now }

let outcome = OrderWorkflow.place ports request

Assert.Equal(Ok expected, outcome)
Assert.True(([ "FSP-BOOK" ] = (lookups |> Seq.toList)))
Assert.True(([ expected ] = (saved |> Seq.toList)))
Assert.Equal(1, clockCalls)
```

These combine test-double roles. A function that returns a fixed product acts like a stub, a list collecting calls acts like a spy, and a complete simplified in-memory implementation is often called a fake; “mock” commonly means a double with prearranged interaction expectations. Terminology varies by team and tool, so code should make clear which values it supplies and which facts it records.

Only ordinary functions and values are needed here, so a dynamic proxy or heavy mock framework would add no evidence. A framework can be useful for a large interface, a cross-language proxy, or an established team convention; it still should not justify writing every internal call into a test.

### State and behavior assertions each have a place {#state-behavior}

The success test first asserts the returned `PlacedOrder`, which is caller-visible state. It then asserts the looked-up SKU, saved order, and one clock read, which form the port protocol. The failure test asserts the error and proves that no clock read or save occurred, because “failure has no effects” is a real workflow promise.

Do not assert that `decide` was called once, which pipeline operator ran first, or whether the implementation uses `Result.map` or `match`. Those are implementation choices; a behavior-preserving refactor should keep tests green. Only promote order to a tested contract when order itself changes external meaning—for example, committing a database transaction before publishing a message.

### Determinism comes from explicit inputs {#determinism}

The test supplies `2026-08-24T09:30Z` as the fixed `GetUtcNow` result. It does not read `DateTimeOffset.UtcNow`, `Sleep`, depend on the current culture, or connect to a shared service. The same source and inputs should produce the same result in any order and on any machine.

If time, randomness, environment variables, or I/O are hard to replace, return to Chapter 20: capture the effect as a parameter or small port. Testability follows from explicit dependencies; it does not require publishing otherwise-private implementation members.

Parallel tests especially require avoiding shared mutable global state. Each test creates its own recording lists and counter; resource tests acquire and release ownership inside the test with `use`/`use!`. Retrying flaky tests hides lost evidence—find the timing, ordering, or external-state dependency instead.

## Contract tests must invoke the real boundary {#contract-tests}

Chapter 27 separated DTOs from domain commands. This chapter uses real `System.Text.Json` behavior and actual options to prove that boundary: camel-case output, case-sensitive input, rejection of unknown fields, and conversion from DTO to a smart-constructed command.

```fsharp:line-numbers [OrderWorkflow.fs]
[<CLIMutable>]
type PlaceOrderDto =
    { OrderId: string | null
      Sku: string | null
      Quantity: int }

type DtoError =
    | MissingBody
    | InvalidCommand of CommandError

module PlaceOrderDto =
    let toCommand (dto: PlaceOrderDto | null) =
        match dto with
        | null -> Error MissingBody
        | value ->
            PlaceOrderCommand.create value.OrderId value.Sku value.Quantity
            |> Result.mapError InvalidCommand

module PlaceOrderJson =
    let private options =
        let settings = JsonSerializerOptions()
        settings.PropertyNamingPolicy <- JsonNamingPolicy.CamelCase
        settings.PropertyNameCaseInsensitive <- false
        settings.UnmappedMemberHandling <- JsonUnmappedMemberHandling.Disallow
        settings

    let serialize (dto: PlaceOrderDto) =
        ArgumentNullException.ThrowIfNull(dto, nameof dto)
        JsonSerializer.Serialize(dto, options)

    let deserialize (json: string) : PlaceOrderDto | null =
        ArgumentNullException.ThrowIfNull(json, nameof json)
        JsonSerializer.Deserialize<PlaceOrderDto>(json, options)
```
The `CLIMutable` DTO can temporarily contain null and zero. `PlaceOrderDto.toCommand` keeps a null body distinct from a domain command error. The domain workflow never receives the DTO.

### Check JSON meaning, not irrelevant bytes {#json-shape}

The output test parses the real serialized result with `JsonDocument`, sorts field names, then checks `orderId`, `sku`, `quantity`, and their values:

```fsharp
let dto: PlaceOrderDto =
    { OrderId = "ORD-28"
      Sku = "FSP-BOOK"
      Quantity = 2 }

use document = JsonDocument.Parse(PlaceOrderJson.serialize dto)
let root = document.RootElement

let propertyNames =
    root.EnumerateObject()
    |> Seq.map (fun property -> property.Name)
    |> Seq.sort
    |> Seq.toArray

Assert.True((propertyNames = [| "orderId"; "quantity"; "sku" |]))
Assert.Equal("ORD-28", root.GetProperty("orderId").GetString())
Assert.Equal("FSP-BOOK", root.GetProperty("sku").GetString())
Assert.Equal(2, root.GetProperty("quantity").GetInt32())
```

It does not compare a whole JSON string because object-property order and whitespace are normally not semantic contracts for JSON consumers. If a business protocol truly requires canonical bytes for signing or hashing, that is a separate explicit risk and deserves a canonicalization test.

### Leniency or strictness must be deliberate {#json-input}

By default, `System.Text.Json` ignores input fields without corresponding DTO members. The sample sets `UnmappedMemberHandling` to `Disallow`, so unknown `priority` throws `JsonException`. This is not a claim that every API should be strict; it puts the actual choice under test.

The input contract also proves that valid JSON crosses the smart constructor, JSON `null` remains `MissingBody`, a missing reference field remains a missing-field error, and a missing `int` becomes default zero and is rejected:

```fsharp
[<Fact>]
let ``unknown json members fail instead of disappearing silently`` () =
    Assert.Throws<JsonException>(fun () ->
        PlaceOrderJson.deserialize
            """{"orderId":"ORD-28","sku":"FSP-BOOK","quantity":2,"priority":true}"""
        |> ignore)
```

If a protocol chooses forward compatibility and ignores unknown fields, configure leniency and use the same real test to prove that an unknown field does not change known values. The test fixes a product decision, not a documentation page's default.

Contract tests also apply to Chapter 27's C#-visible signatures, database column mappings, message headers, or HTTP status. The real library or adapter responsible for conversion must participate. Replacing it with a fake proves only that the fake agrees with its setup.

## Write tests that survive useful change {#durable-tests}

### Let names describe scenario and result first {#test-names}

`pure decision reports the exact stock counterexample` and `failed decision does not read the clock or save` explain behavior without opening the implementation. F# backtick names make readable sentences; `[<Fact>]` lets xUnit discover a parameterless fact, while `[<Theory>]` fits a set of explicit data rows.

Arrange-Act-Assert is a reading boundary, not a demand for mechanical comments. A short test can separate setup, its single action, and assertions with blank lines. If setup overwhelms behavior, extract a helper that only creates valid values; do not hide assertions or branches inside a general test framework.

### Verify that the red light is trustworthy {#red-green-refactor}

The test-driven loop is: write the smallest failing test and run it to confirm the expected reason; write the smallest implementation that passes; improve names, duplication, and boundaries under a green suite. A green test never observed failing may not exercise its intended path.

This chapter's sample first produced an FS0039 compile failure for missing types, then implemented the shared API, and finally separated DTO errors from domain errors while the focused suite stayed green. A compilation failure can be a valid red light when it precisely proves the required contract does not exist yet.

### Test public behavior without locking private implementation {#implementation-details}

Common signs of excess coupling include: renaming a private helper fails a test; changing a pipeline to an equivalent `match` fails; adding a harmless cache fails because call counts changed; mock setup is longer than the business example; an otherwise-private member was published only for a test.

By contrast, a boundary field name, exactly-once charge, suppressed save after failure, event order, or idempotency key can be public behavior. Test an interaction when a caller or external system can observe and rely on it.

Code coverage reveals which locations executed. Risk and invariant analysis establishes which scenarios and assertions matter. Use coverage afterward to find blind spots, with attention on behavior rather than trivial getters, framework code, or a target percentage.

## Run focused and complete tests {#running-tests}

In a solution containing these tests, use a filter for quick feedback:

```console
dotnet test Sample.slnx --configuration Release --filter FullyQualifiedName~Ch28
```

The filter selects names containing `Ch28`. Before committing an application change, run the same solution without the filter so cross-project wiring is checked too:

```console
dotnet test Sample.slnx --configuration Release
```

## A practical selection checklist {#selection-checklist}

When adding a test for one behavior, ask in order:

1. Can the rule become a pure function whose input and output values are compared directly?
2. If effects exist, which port results must be controlled and which calls are public protocol?
3. Is a short record of functions sufficient, or is a reusable fake or framework genuinely needed?
4. Does the risk come from a serializer, database driver, HTTP stack, runtime metadata, or another real boundary?
5. Does the test control time, randomness, culture, environment, concurrency, and resource ownership?
6. Can an equivalent refactor keep it passing?
7. Will a failure message expose the scenario, expectation, and actual counterexample?

If the first two questions provide sufficient evidence, there is no need to escalate to an end-to-end test. If the fourth is true, do not evade the real contract with more mocks.

## Exercises {#exercises}

### Exercise 1: choose a test level for three risks {#exercise-01}

Choose the smallest level for “discount total is calculated incorrectly,” “an order is still saved after insufficient stock,” and “the JSON field drifts from `orderId` to `OrderId`.” State real participants, replaced participants, and the key assertion for each, and explain why a larger test adds no necessary evidence.

### Exercise 2: write a double test without locking implementation {#exercise-02}

Write a test for the `ProductNotFound` path. Hand-write ports that record the queried SKU and prove that clock and save do not occur. Do not assert private function names, pipeline shape, or an internal call count with no external meaning.

### Exercise 3: design a JSON contract evolution {#exercise-03}

The product will add an optional `note` field. Decide behavior for old readers, old writers, and unknown fields; list input and output contract tests required before release. Explain which inputs become accepted if `PropertyNameCaseInsensitive` changes to `true` and what kind of behavioral change that is.

[Read the chapter solutions](../solutions/ch-28-testing-boundaries).

## Model review {#model-review}

- Test levels follow risk, not directory names or frameworks.
- Pure functions provide the fastest and clearest evidence through small values and structural equality.
- Exact error values should preserve counterexample context needed by callers.
- Small functions and records are often sufficient deterministic port doubles.
- Prefer state assertions; assert behavior only when it is an observable protocol.
- Contract tests include the real conversion library, options, and adapter.
- JSON property order is usually not a contract; field names, types, absence, and unknown-member policy can be.
- Time, randomness, shared state, sleeping, and real services damage repeatability.
- Confirm a trustworthy red light, implement the green result, and refactor under behavioral protection.
- Tests should permit equivalent refactoring while preventing public behavior and boundary contracts from drifting.

## Sources {#sources}

- [Microsoft Learn: test types, tools, and execution in .NET](https://learn.microsoft.com/en-us/dotnet/core/testing/)
- [Microsoft Learn: unit testing F# with dotnet test and xUnit](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-fsharp-with-xunit)
- [Microsoft Learn: unit testing best practices](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices)
- [Microsoft Learn: handle unmapped JSON members during deserialization](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/missing-members)
- [Microsoft Learn: `System.Text.Json` property-name casing](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/character-casing)
