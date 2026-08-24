namespace ThinkingInFSharp.ExampleTests

open System
open ThinkingInFSharp.Ch28
open Xunit

module Ch28WorkflowTests =
    // #region pure-value-tests
    let private expectOk result =
        match result with
        | Ok value -> value
        | Error error -> failwithf "Expected Ok, received Error %A" error

    let private command orderId sku quantity =
        PlaceOrderCommand.create orderId sku quantity |> expectOk

    let private product sku unitPrice available =
        ProductSnapshot.create sku unitPrice available

    [<Fact>]
    let ``pure decision calculates an order from values`` () =
        let request = command "ORD-28" "FSP-BOOK" 2
        let snapshot = product "FSP-BOOK" 19.50M 5

        let expected: OrderDraft =
            { OrderId = "ORD-28"
              Sku = "FSP-BOOK"
              Quantity = 2
              Total = 39.00M }

        Assert.Equal(Ok expected, OrderDecision.decide (Some snapshot) request)

    [<Fact>]
    let ``pure decision reports the exact stock counterexample`` () =
        let request = command "ORD-28" "FSP-BOOK" 3
        let snapshot = product "FSP-BOOK" 19.50M 2

        Assert.Equal(Error(InsufficientStock(3, 2)), OrderDecision.decide (Some snapshot) request)
    // #endregion pure-value-tests

    // #region port-double-tests
    [<Fact>]
    let ``small fake records the successful boundary protocol`` () =
        let request = command "ORD-28" "FSP-BOOK" 2
        let snapshot = product "FSP-BOOK" 19.50M 5
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

        Assert.Equal(Ok expected, OrderWorkflow.place ports request)
        Assert.True(([ "FSP-BOOK" ] = (lookups |> Seq.toList)))
        Assert.True(([ expected ] = (saved |> Seq.toList)))
        Assert.Equal(1, clockCalls)

    [<Fact>]
    let ``failed decision does not read the clock or save`` () =
        let request = command "ORD-28" "FSP-BOOK" 2
        let snapshot = product "FSP-BOOK" 19.50M 1
        let saved = ResizeArray<PlacedOrder>()
        let mutable clockCalls = 0

        let ports: OrderPorts =
            { FindProduct = fun _ -> Some snapshot
              GetUtcNow =
                fun () ->
                    clockCalls <- clockCalls + 1
                    DateTimeOffset.MaxValue
              SaveOrder = saved.Add }

        Assert.Equal(Error(InsufficientStock(2, 1)), OrderWorkflow.place ports request)

        Assert.Equal(0, clockCalls)
        Assert.Empty saved
    // #endregion port-double-tests

    // #region missing-product-test
    [<Fact>]
    let ``missing product queries the sku without later effects`` () =
        let request = command "ORD-28" "FSP-BOOK" 2
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

        Assert.Equal(Error(ProductNotFound "FSP-BOOK"), OrderWorkflow.place ports request)

        Assert.True(([ "FSP-BOOK" ] = (lookups |> Seq.toList)))
        Assert.Equal(0, clockCalls)
        Assert.Empty saved
// #endregion missing-product-test
