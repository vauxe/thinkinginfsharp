namespace ThinkingInFSharp.ContractTests

open System.Text.Json
open ThinkingInFSharp.Ch28
open Xunit

module Ch28BoundaryTests =
    // #region json-shape-contract
    [<Fact>]
    let ``json output keeps the documented camel-case shape`` () =
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
    // #endregion json-shape-contract

    // #region json-input-contract
    [<Fact>]
    let ``json input crosses validation before becoming a command`` () =
        let dto =
            PlaceOrderJson.deserialize
                """{"orderId":"ORD-28","sku":"FSP-BOOK","quantity":2}"""

        match PlaceOrderDto.toCommand dto with
        | Error error -> failwithf "Expected a command, received %A" error
        | Ok command ->
            Assert.Equal("ORD-28", PlaceOrderCommand.orderId command)
            Assert.Equal("FSP-BOOK", PlaceOrderCommand.sku command)
            Assert.Equal(2, PlaceOrderCommand.quantity command)

    [<Fact>]
    let ``null missing and default json values remain boundary errors`` () =
        let nullBody = PlaceOrderJson.deserialize "null"
        let missingId = PlaceOrderJson.deserialize "{}"

        let defaultQuantity =
            PlaceOrderJson.deserialize
                """{"orderId":"ORD-28","sku":"FSP-BOOK"}"""

        Assert.Equal(Error MissingBody, PlaceOrderDto.toCommand nullBody)
        Assert.Equal(
            Error(InvalidCommand MissingOrderId),
            PlaceOrderDto.toCommand missingId
        )

        Assert.Equal(
            Error(InvalidCommand(NonPositiveQuantity 0)),
            PlaceOrderDto.toCommand defaultQuantity
        )

    [<Fact>]
    let ``unknown json members fail instead of disappearing silently`` () =
        Assert.Throws<JsonException>(fun () ->
            PlaceOrderJson.deserialize
                """{"orderId":"ORD-28","sku":"FSP-BOOK","quantity":2,"priority":true}"""
            |> ignore)
    // #endregion json-input-contract
