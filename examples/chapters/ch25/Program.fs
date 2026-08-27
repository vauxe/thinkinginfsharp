namespace ThinkingInFSharp.Ch25

module Program =
    let private expectOk result =
        match result with
        | Ok value -> value
        | Error error -> failwithf "Expected Ok, received Error %A" error

    [<EntryPoint>]
    let main _ =
        let groupDiscount =
            { new IDiscountPolicy with
                member _.Rate request =
                    if request.Seats >= 5 then 0.10M else 0M }

        let calculator = PriceCalculator(0.20M, groupDiscount)
        let quote = calculator.Calculate { Seats = 5; UnitPrice = 10M } |> expectOk
        let service = calculator :> IQuoteService
        let interfaceQuote = service.Quote { Seats = 1; UnitPrice = 10M } |> expectOk
        let revision = QuoteRevision.create 2 |> expectOk
        let revisionCopy = revision
        let zeroRevision = Unchecked.defaultof<QuoteRevision>

        printfn "Class: tax-rate=%.2f total=%.2f" calculator.TaxRate quote.TotalAmount
        printfn "Interface: total=%.2f" interfaceQuote.TotalAmount
        printfn "Extension: discounted=%b" quote.IsDiscounted

        printfn
            "Struct: value=%d copy=%d default=%d"
            (QuoteRevision.value revision)
            (QuoteRevision.value revisionCopy)
            (QuoteRevision.value zeroRevision)

        0
