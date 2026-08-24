namespace ThinkingInFSharp.Ch25

type QuoteRequest = { Seats: int; UnitPrice: decimal }

type QuoteError =
    | NonPositiveSeats of actual: int
    | NegativeUnitPrice of actual: decimal
    | InvalidDiscountRate of actual: decimal

type Quote =
    private
        { Seats: int
          Subtotal: decimal
          Discount: decimal
          Tax: decimal
          Total: decimal }

module Quote =
    let seats quote = quote.Seats
    let subtotal quote = quote.Subtotal
    let discount quote = quote.Discount
    let tax quote = quote.Tax
    let total quote = quote.Total

type IDiscountPolicy =
    abstract Rate: QuoteRequest -> decimal

type IQuoteService =
    abstract Quote: QuoteRequest -> Result<Quote, QuoteError>

type PriceCalculator(taxRate: decimal, discountPolicy: IDiscountPolicy) =
    do
        if taxRate < 0M then
            invalidArg (nameof taxRate) "Tax rate cannot be negative."

    new(discountPolicy: IDiscountPolicy) = PriceCalculator(0M, discountPolicy)

    member _.TaxRate = taxRate

    member _.Calculate(request: QuoteRequest) =
        if request.Seats <= 0 then
            Error(NonPositiveSeats request.Seats)
        elif request.UnitPrice < 0M then
            Error(NegativeUnitPrice request.UnitPrice)
        else
            let discountRate = discountPolicy.Rate request

            if discountRate < 0M || discountRate > 1M then
                Error(InvalidDiscountRate discountRate)
            else
                let subtotal = decimal request.Seats * request.UnitPrice
                let discount = subtotal * discountRate
                let taxable = subtotal - discount
                let tax = taxable * taxRate

                Ok
                    { Seats = request.Seats
                      Subtotal = subtotal
                      Discount = discount
                      Tax = tax
                      Total = taxable + tax }

    interface IQuoteService with
        member this.Quote request = this.Calculate request

[<AutoOpen>]
module QuoteExtensions =
    type Quote with
        member this.IsDiscounted = Quote.discount this > 0M
        member this.TotalAmount = Quote.total this

[<Struct>]
type QuoteRevision = private QuoteRevision of int

module QuoteRevision =
    let create raw =
        if raw > 0 then Ok(QuoteRevision raw) else Error raw

    let value (QuoteRevision revision) = revision
