namespace ThinkingInFSharp.Ch17

module SeatAllocation =
    type CapacityError =
        | NonPositiveCapacity of actual: int

    type Capacity

    module Capacity =
        val create: raw: int -> Result<Capacity, CapacityError>
        val value: capacity: Capacity -> int

    type SeatCountError =
        | NonPositiveSeatCount of actual: int

    type SeatCount

    module SeatCount =
        val create: raw: int -> Result<SeatCount, SeatCountError>
        val value: seats: SeatCount -> int

    type AllocationError =
        | InsufficientCapacity of requested: int * available: int

    type Allocation

    module Allocation =
        val capacity: allocation: Allocation -> Capacity
        val requested: allocation: Allocation -> SeatCount
        val remaining: allocation: Allocation -> int

    val allocate: capacity: Capacity -> requested: SeatCount -> Result<Allocation, AllocationError>
