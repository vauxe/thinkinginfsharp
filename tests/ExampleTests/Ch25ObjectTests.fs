namespace ThinkingInFSharp.ExampleTests

open System
open ThinkingInFSharp.Ch25
open Xunit

module Ch25ObjectTests =
    let private expectOk result =
        match result with
        | Ok value -> value
        | Error error -> failwithf "Expected Ok, received Error %A" error

    let private noDiscount =
        { new IDiscountPolicy with
            member _.Rate _ = 0M }

    [<Fact>]
    let ``constructor enforces configuration and secondary constructor supplies zero tax`` () =
        Assert.Throws<ArgumentException>(fun () -> PriceCalculator(-0.01M, noDiscount) |> ignore)
        |> ignore

        let calculator = PriceCalculator(noDiscount)
        Assert.Equal(0M, calculator.TaxRate)

    [<Fact>]
    let ``class member calculates a validated quote`` () =
        let groupDiscount =
            { new IDiscountPolicy with
                member _.Rate request = if request.Seats >= 5 then 0.10M else 0M }

        let calculator = PriceCalculator(0.20M, groupDiscount)
        let quote = calculator.Calculate { Seats = 5; UnitPrice = 10M } |> expectOk

        Assert.Equal(5, Quote.seats quote)
        Assert.Equal(50M, Quote.subtotal quote)
        Assert.Equal(5M, Quote.discount quote)
        Assert.Equal(9M, Quote.tax quote)
        Assert.Equal(54M, Quote.total quote)

    [<Fact>]
    let ``interface view delegates to the same class behavior`` () =
        let service = PriceCalculator(noDiscount) :> IQuoteService

        Assert.Equal(
            Error(NonPositiveSeats 0),
            service.Quote { Seats = 0; UnitPrice = 10M }
        )

        let quote = service.Quote { Seats = 2; UnitPrice = 10M } |> expectOk
        Assert.Equal(20M, Quote.total quote)

    [<Fact>]
    let ``object expression is a small deterministic policy substitute`` () =
        let calls = ResizeArray<QuoteRequest>()

        let recordingPolicy =
            { new IDiscountPolicy with
                member _.Rate request =
                    calls.Add request
                    0.25M }

        let calculator = PriceCalculator(recordingPolicy)
        let request = { Seats = 4; UnitPrice = 8M }
        let quote = calculator.Calculate request |> expectOk

        Assert.Equal<QuoteRequest list>([ request ], calls |> Seq.toList)
        Assert.Equal(8M, Quote.discount quote)

    [<Fact>]
    let ``type extension offers a derived view without storing another field`` () =
        let quote =
            PriceCalculator(noDiscount).Calculate { Seats = 2; UnitPrice = 7M }
            |> expectOk

        Assert.False(quote.IsDiscounted)
        Assert.Equal(14M, quote.TotalAmount)

    [<Fact>]
    let ``struct copies by value and default initialization can bypass creation`` () =
        let revision = QuoteRevision.create 2 |> expectOk
        let copy = revision
        let invalidDefault = Unchecked.defaultof<QuoteRevision>

        Assert.Equal(2, QuoteRevision.value revision)
        Assert.Equal(2, QuoteRevision.value copy)
        Assert.False(obj.ReferenceEquals(box revision, box copy))
        Assert.Equal(0, QuoteRevision.value invalidDefault)
