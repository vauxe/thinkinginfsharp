namespace ThinkingInFSharp.ExampleTests

open System
open System.Reflection
open Booking.Domain
open Booking.Domain.PublicApi
open global.FsCheck
open global.FsCheck.Xunit
open global.Xunit

type private BookingPropertyRunner() =
    let mutable result = None

    member _.Result = result

    interface IRunner with
        member _.OnStartFixture _ = ()
        member _.OnArguments(_, _, _) = ()
        member _.OnShrink(_, _) = ()
        member _.OnFinished(_, testResult) = result <- Some testResult

[<Properties(QuietOnSuccess = true)>]
module BookingProperties =
    let private expectOk result =
        match result with
        | Ok value -> value
        | Error errors -> failwithf "expected Ok, received Error %A" errors

    let private requireBooking model =
        match tryBooking model with
        | Some booking -> booking
        | None -> failwith "expected a booking view"

    let private createPlaced capacityValue seatsValue suffix =
        start $"EVT-{suffix}" capacityValue
        |> expectOk
        |> place $"REQ-{suffix}" seatsValue
        |> expectOk

    [<Property(MaxTest = 500)>]
    let ``placement respects the capacity boundary`` (PositiveInt capacityValue) (PositiveInt seatsValue) =
        let initial = start "EVT-PROPERTY" capacityValue |> expectOk

        match place "REQ-PROPERTY" seatsValue initial with
        | Ok placed ->
            let booking = requireBooking placed

            seatsValue <= capacityValue
            && BookingView.seats booking = seatsValue
            && BookingView.phase booking = BookingPhase.Pending
            && tryBooking initial = None
        | Error [ BookingError.RequestedSeatsExceedCapacity(requested, capacity) ] ->
            seatsValue > capacityValue && requested = seatsValue && capacity = capacityValue
        | Error _ -> false

    [<Property(MaxTest = 500)>]
    let ``cancellation is final after every legal placement``
        (PositiveInt capacityValue)
        (PositiveInt seatSeed)
        (PositiveInt suffix)
        (confirmFirst: bool)
        =
        let seatsValue = 1 + ((seatSeed - 1) % capacityValue)
        let confirmationCode = $"CONF-{suffix}"
        let reason = $"cancel-{suffix}"
        let placed = createPlaced capacityValue seatsValue suffix

        let beforeCancellation =
            if confirmFirst then
                confirm confirmationCode placed |> expectOk
            else
                placed

        let cancelled = cancel reason beforeCancellation |> expectOk
        let cancelledView = requireBooking cancelled
        let originalView = requireBooking placed

        let confirmAgain = confirm $"OTHER-{suffix}" cancelled
        let cancelAgain = cancel $"again-{suffix}" cancelled

        BookingView.phase originalView = BookingPhase.Pending
        && BookingView.phase cancelledView = BookingPhase.Cancelled reason
        && BookingView.requestId cancelledView = $"REQ-{suffix}"
        && BookingView.seats cancelledView = seatsValue
        && confirmAgain = Error [ BookingError.CannotConfirmFrom(BookingPhase.Cancelled reason) ]
        && cancelAgain = Error [ BookingError.CannotCancelFrom(BookingPhase.Cancelled reason) ]

    [<Fact>]
    let ``public functions do not expose domain representation types`` () =
        let forbidden =
            [| typeof<Event>
               typeof<RequestId>
               typeof<SeatCount>
               typeof<Booking>
               typeof<Workflow.BookingState>
               typeof<Workflow.BookingEvent> |]

        let rec mentionsForbidden (candidate: Type) =
            (forbidden |> Array.contains candidate)
            || (candidate.IsArray
                && match candidate.GetElementType() with
                   | null -> false
                   | elementType -> mentionsForbidden elementType)
            || (candidate.IsGenericType
                && (candidate.GetGenericArguments() |> Array.exists mentionsForbidden))

        let apiType =
            match typeof<BookingModel>.Assembly.GetType("Booking.Domain.PublicApi") with
            | null -> failwith "compiled PublicApi module was not found"
            | value -> value

        let publicMethods = apiType.GetMethods(BindingFlags.Public ||| BindingFlags.Static)
        Assert.NotEmpty publicMethods

        for methodInfo in publicMethods do
            Assert.False(mentionsForbidden methodInfo.ReturnType, $"{methodInfo.Name} leaks {methodInfo.ReturnType}")

            for parameter in methodInfo.GetParameters() do
                Assert.False(
                    mentionsForbidden parameter.ParameterType,
                    $"{methodInfo.Name} leaks {parameter.ParameterType}"
                )

        Assert.Empty(typeof<BookingModel>.GetConstructors())
        Assert.Empty(typeof<BookingModel>.GetProperties())
        Assert.Empty(typeof<BookingView>.GetConstructors())
        Assert.Empty(typeof<BookingView>.GetProperties())

    [<Fact>]
    let ``fixed replay shrinks the false universal fit claim`` () =
        let runner = BookingPropertyRunner()

        let config =
            Config.Quick.WithMaxTest(300).WithReplay(13285693176119930639UL, 18364232908344279255UL).WithRunner(runner)

        let everyPositiveRequestFits (PositiveInt capacityValue) (PositiveInt seatsValue) =
            start "EVT-REPLAY" capacityValue
            |> expectOk
            |> place "REQ-REPLAY" seatsValue
            |> Result.isOk

        Check.One("every positive request fits", config, everyPositiveRequestFits)

        match runner.Result with
        | Some(TestResult.Failed(data, _, shrunkArguments, _, _, _, _)) ->
            let values =
                shrunkArguments |> List.map (unbox<PositiveInt> >> fun value -> value.Get)

            Assert.True(data.NumberOfShrinks > 0, "the counterexample should be shrunk")
            Assert.Equal<int list>([ 1; 2 ], values)
        | Some result ->
            let report = Runner.onFinishedToString "every positive request fits" result
            Assert.Fail($"expected a falsified property, got: {report}")
        | None -> Assert.Fail("FsCheck did not report a result")
