namespace ThinkingInFSharp.ExampleTests

open ThinkingInFSharp.Ch16.Domain
open ThinkingInFSharp.Ch16.Workflow
open Xunit

module Ch16ProjectTests =
    let private expectOk result =
        match result with
        | Ok value -> value
        | Error error -> failwithf "Expected Ok, received Error %A" error

    [<Fact>]
    let ``booking identifiers make nullable input explicit and normalize text`` () =
        Assert.Equal(Error MissingBookingId, BookingId.create null)
        Assert.Equal(Error MissingBookingId, BookingId.create "   ")

        let bookingId = BookingId.create "  REQ-16  " |> expectOk
        Assert.Equal("REQ-16", BookingId.value bookingId)

    [<Fact>]
    let ``request construction preserves component validation errors`` () =
        Assert.Equal(Error(InvalidBookingId MissingBookingId), BookingRequest.create null 2)
        Assert.Equal(Error(InvalidSeatCount(NonPositiveSeatCount 0)), BookingRequest.create "REQ-16" 0)

        let request = BookingRequest.create "REQ-16" 3 |> expectOk
        Assert.Equal("REQ-16", request |> BookingRequest.id |> BookingId.value)
        Assert.Equal(3, request |> BookingRequest.seats |> SeatCount.value)

    [<Fact>]
    let ``workflow uses domain definitions from the preceding file`` () =
        let capacity = Capacity.create 4 |> expectOk
        let acceptedRequest = BookingRequest.create "REQ-16" 3 |> expectOk
        let rejectedRequest = BookingRequest.create "REQ-17" 5 |> expectOk

        let acceptedId = acceptedRequest |> BookingRequest.id

        Assert.Equal(Accepted(acceptedId, 1), decide capacity acceptedRequest)
        Assert.Equal(Rejected(5, 4), decide capacity rejectedRequest)

    [<Fact>]
    let ``program composes the files in dependency order`` () =
        Assert.Equal("accepted:REQ-16 remaining=1", ThinkingInFSharp.Ch16.Program.summary ())
