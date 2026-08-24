namespace ThinkingInFSharp.Ch17

module SeatAllocation =
    type CapacityError = NonPositiveCapacity of actual: int

    type Capacity = Capacity of int

    module Capacity =
        let create raw =
            if raw > 0 then
                Ok(Capacity raw)
            else
                Error(NonPositiveCapacity raw)

        let value (Capacity capacity) = capacity

    type SeatCountError = NonPositiveSeatCount of actual: int

    type SeatCount = SeatCount of int

    module SeatCount =
        let create raw =
            if raw > 0 then
                Ok(SeatCount raw)
            else
                Error(NonPositiveSeatCount raw)

        let value (SeatCount seats) = seats

    type AllocationError = InsufficientCapacity of requested: int * available: int

    type Allocation =
        { Capacity: Capacity
          Requested: SeatCount
          Remaining: int }

    module Allocation =
        let capacity allocation = allocation.Capacity
        let requested allocation = allocation.Requested
        let remaining allocation = allocation.Remaining

    let allocate capacity requested =
        let available = Capacity.value capacity
        let requestedSeats = SeatCount.value requested

        if requestedSeats <= available then
            Ok
                { Capacity = capacity
                  Requested = requested
                  Remaining = available - requestedSeats }
        else
            Error(InsufficientCapacity(requestedSeats, available))
