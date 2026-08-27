---
title: "Chapter 28: Example Tests, Test Doubles, and Contract Tests"
description: "Choose pure value tests, hand-written deterministic doubles, and real serialization contract tests by failure risk instead of testing implementation details."
translationKey: part-05/ch-28-testing-boundaries
---

# Chapter 28: Example Tests, Test Doubles, and Contract Tests {#overview}

A test should demonstrate behavior that matters, not mirror source structure. A wrong total needs a minimal input-output example. Saving after a failed decision requires observing calls to dependencies. A changed JSON field name or deserialization option requires a contract test that invokes the real serializer.

Before choosing a test level, ask what failure it must detect. If every test starts a database, feedback is slow and failures are hard to locate. If every test replaces the serializer and database, no test checks that the real integrations still agree. Use inexpensive tests for most logic and pay the extra cost only for genuine integration risks.

## Choose the cheapest test that covers the risk {#risk-matrix}

A “unit” need not be one class or function. It is the amount of work that one test deliberately controls. The following levels answer different questions:

| Risk | Smallest useful test | Real participants | Usually avoid |
|---|---|---|---|
| Calculation, branching, invariant | Pure value example test | Domain function and ordinary values | Clock, network, file, database |
| How a workflow uses dependencies | Unit test with hand-written doubles | Workflow; dependencies replaced by deterministic functions | Mock framework, real infrastructure |
| Serialization, C# API, database mapping | Integration contract test | Real library, options, metadata, or adapter | Whole application host |
| Whether components and infrastructure work together | Integration test | Real components that must compose | Replacing the boundary under test |
| Critical user path | A few end-to-end tests | Complete deployment-like path | Enumerating every domain branch |

A test's name does not determine what it checks. An “integration” test that replaces the real protocol cannot verify that protocol, while an in-memory JSON contract test can verify actual serializer configuration. Categories describe scope; they are not labels worth arguing over.

## Pure functions are best tested with values {#pure-value-tests}

The sample first represents the command, product snapshot, draft, and error as normal F# values. `decide` receives only a product snapshot and validated command, and returns one `Result`:

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

The second test chooses the smallest stock counterexample: requested 3, available 2. `InsufficientStock(3, 2)` records both failure and the context needed for diagnosis or recovery. If a future algorithm still rejects the request but swaps the numbers, the test exposes changed output behavior.

### Assert output instead of copying the algorithm {#assert-output}

Expected values in tests should be small, concrete examples. Do not recalculate `decimal quantity * unitPrice`, copy the production filter, or reproduce branches with a loop in test code; the same mistake can then exist in both implementation and “expected algorithm.”

One test may have several assertions when together they check one behavior. The JSON output test, for example, checks three field names and values while still describing one API result. A test that mixes pricing, save failure, and serialization is harder to diagnose and should be split.

Structural equality does not mean a larger assertion is always better. If a huge aggregate contains fields irrelevant to the behavior, constructing a full expected value makes unrelated evolution break the test. Assert the smallest meaningful projection that expresses the behavior.

## Workflow tests need controllable dependencies {#port-tests}

Outside the pure core, the sample workflow reads a product, reads time, and saves an order. Its dependencies form a record of functions, and a short `match` makes side-effect order clear:

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
Only the success branch reads the clock and saves; a decision failure returns directly. The success test builds dependencies from closures: fixed product and time results, `ResizeArray` values that record lookup and save, and a counter for clock reads:

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

These values combine several test-double roles. A fixed-return function is a stub; a call-recording list is a spy; a simplified in-memory implementation is usually a fake. “Mock” commonly means a double with predefined interaction expectations. Terms vary by team and tool, so make the supplied values and recorded calls clear in code.

Only functions and values are needed here, so a dynamic proxy or heavy mock framework would add no confidence. A framework can help with a large interface, a cross-language proxy, or an established team convention; it still does not justify asserting every internal call.

### State and behavior assertions each have a place {#state-behavior}

The success test first asserts the returned `PlacedOrder`, which is visible to the caller. It then checks the looked-up SKU, saved order, and single clock read—the workflow's calls to dependencies. The failure test checks the error and confirms that neither clock nor save was called, because “failure has no side effects” is a real workflow promise.

Do not assert that `decide` was called once, which pipeline operator ran first, or whether the implementation uses `Result.map` or `match`. Those are implementation choices, so an equivalent refactor should keep tests green. Test order only when it changes external meaning—for example, committing a database transaction before publishing a message.

### Determinism comes from controlled inputs {#determinism}

The test supplies `2026-08-24T09:30Z` as the fixed `GetUtcNow` result. It does not read `DateTimeOffset.UtcNow`, `Sleep`, depend on the current culture, or connect to a shared service. The same source and inputs should produce the same result in any order and on any machine.

If time, randomness, environment variables, or I/O are hard to replace, return to Chapter 20: pass the side-effecting operation as a parameter or small interface. Clear dependencies make code testable; private implementation members do not need to become public.

Parallel tests must especially avoid shared mutable global state. Each test creates its own recording lists and counter; resource tests acquire and release resources inside the test with `use` or `use!`. Retrying flaky tests hides the problem—find the timing, ordering, or external-state dependency instead.

## Contract tests must invoke the real integration code {#contract-tests}

Chapter 27 separated DTOs from domain commands. Here the real `System.Text.Json` library and actual options test camel-case output, case-sensitive input, rejection of unknown fields, and conversion from a DTO to a smart-constructed command.

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

It does not compare a whole JSON string because property order and whitespace normally carry no meaning for JSON consumers. If a business protocol requires canonical bytes for signing or hashing, treat that as a separate risk and add a canonicalization test.

### Leniency or strictness must be deliberate {#json-input}

By default, `System.Text.Json` ignores input fields without corresponding DTO members. The sample sets `UnmappedMemberHandling` to `Disallow`, so unknown `priority` throws `JsonException`. Not every API should be strict; this test simply records the chosen behavior.

The input tests also show that valid JSON passes through the smart constructor, JSON `null` remains `MissingBody`, a missing reference stays a missing-field error, and a missing `int` becomes zero and is rejected:

```fsharp
[<Fact>]
let ``unknown json members fail instead of disappearing silently`` () =
    Assert.Throws<JsonException>(fun () ->
        PlaceOrderJson.deserialize
            """{"orderId":"ORD-28","sku":"FSP-BOOK","quantity":2,"priority":true}"""
        |> ignore)
```

If a protocol chooses forward compatibility and ignores unknown fields, configure leniency and use the same real test to show that unknown fields do not change known values. The test records a product decision rather than a documentation default.

Contract tests also apply to Chapter 27's C#-visible signatures, database column mappings, message headers, and HTTP status codes. The real library or adapter responsible for conversion must participate. Replacing it with a fake shows only that the fake matches its setup.

## Write tests that survive useful change {#durable-tests}

### Let names describe scenario and result first {#test-names}

Names such as `pure decision reports the stock counterexample` and `failed decision does not read the clock or save` explain behavior without exposing implementation details. F# backtick names allow readable sentences. `[<Fact>]` marks a parameterless test, while `[<Theory>]` suits several concrete data rows.

Arrange-Act-Assert is a reading convention, not a demand for mechanical comments. A short test can separate setup, one action, and assertions with blank lines. If setup overwhelms behavior, extract a helper that only creates valid values; do not hide assertions or branches inside a general test framework.

### Verify that the red light is trustworthy {#red-green-refactor}

The test-driven loop is: write the smallest failing test and confirm that it fails for the expected reason; write the smallest implementation that passes; then improve names, duplication, and structure under a green suite. A test never observed failing may not exercise its intended path.

The sample first produced an FS0039 compile failure for missing types, then implemented the shared API, and finally separated DTO errors from domain errors while focused tests stayed green. A compilation failure is a valid red test when it directly shows that a required API does not exist yet.

### Test public behavior without locking private implementation {#implementation-details}

Common signs of excessive coupling include:

- renaming a private helper fails a test;
- replacing a pipeline with an equivalent `match` fails;
- adding a harmless cache fails because call counts changed;
- mock setup is longer than the business example;
- an otherwise private member was published only for a test.

By contrast, a serialized field name, exactly-once charge, suppressed save after failure, event order, or idempotency key can be public behavior. Test an interaction when a caller or external system can observe and rely on it.

Code coverage reveals which locations executed; risk and invariant analysis determines which scenarios and assertions matter. Use coverage afterward to find blind spots. Focus on behavior, not trivial getters, framework code, or a target percentage.

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
2. If side effects exist, which dependency results must be controlled and which calls are externally observable behavior?
3. Is a short record of functions sufficient, or is a reusable fake or framework genuinely needed?
4. Does the risk come from a serializer, database driver, HTTP stack, runtime metadata, or another real integration?
5. Does the test control time, randomness, culture, environment, concurrency, and resource cleanup?
6. Can an equivalent refactor keep it passing?
7. Will a failure message expose the scenario, expectation, and actual counterexample?

If the first two questions cover the behavior, there is no need for an end-to-end test. If the fourth is true, do not replace the real integration with more mocks.

## Exercises {#exercises}

### Exercise 1: choose a test level for three risks {#exercise-01}

Choose the smallest test level for three risks: an incorrect discount total, saving after insufficient stock, and a JSON field changing from `orderId` to `OrderId`. For each, name the real and replaced participants and the key assertion. Explain why a larger test adds no useful coverage.

### Exercise 2: write a double test without locking implementation {#exercise-02}

Write a test for the `ProductNotFound` path. Hand-write dependencies that record the queried SKU and confirm that clock and save are not called. Do not assert private function names, pipeline form, or an internal call count with no external meaning.

### Exercise 3: design a JSON schema change {#exercise-03}

The product will add an optional `note` field. Decide behavior for old readers, old writers, and unknown fields; list input and output contract tests required before release. Explain which inputs become accepted if `PropertyNameCaseInsensitive` changes to `true` and what kind of behavioral change that is.

[Read the chapter solutions](../solutions/ch-28-testing-boundaries).

## Model review {#model-review}

- Test levels follow risk, not directory names or frameworks.
- Pure functions provide the fastest and clearest feedback through small values and structural equality.
- Specific error values should preserve the counterexample context needed by callers.
- Small functions and records are often sufficient as deterministic dependency doubles.
- Prefer state assertions; assert interactions only when they are externally observable.
- Contract tests include the real conversion library, options, and adapter.
- JSON property order is usually not a contract; field names, types, absence, and unknown-member policy can be.
- Time, randomness, shared state, sleeping, and real services damage repeatability.
- Confirm a trustworthy red light, implement the green result, and refactor under behavioral protection.
- Tests should permit equivalent refactoring while preventing public behavior and integration contracts from drifting.

## Sources {#sources}

- [Microsoft Learn: test types, tools, and execution in .NET](https://learn.microsoft.com/en-us/dotnet/core/testing/)
- [Microsoft Learn: unit testing F# with dotnet test and xUnit](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-fsharp-with-xunit)
- [Microsoft Learn: unit testing best practices](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices)
- [Microsoft Learn: handle unmapped JSON members during deserialization](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/missing-members)
- [Microsoft Learn: `System.Text.Json` property-name casing](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/character-casing)
