namespace ThinkingInFSharp.Ch16

open ThinkingInFSharp.Ch16.Domain

module Workflow =
    type Decision =
        | Accepted of bookingId: BookingId * remaining: int
        | Rejected of requested: int * capacity: int

    let decide capacity request =
        let available = Capacity.value capacity
        let requested = request |> BookingRequest.seats |> SeatCount.value

        if requested <= available then
            Accepted(BookingRequest.id request, available - requested)
        else
            Rejected(requested, available)

    let describe decision =
        match decision with
        | Accepted(bookingId, remaining) ->
            $"accepted:{BookingId.value bookingId} remaining={remaining}"
        | Rejected(requested, capacity) ->
            $"rejected:requested={requested} capacity={capacity}"
