// #region basic-values
let eventName = "Functional Foundations"
let capacity = 40
let fillRatio = 0.45
let ticketPrice = 19.50m
let eventCode = 'F'
let registrationOpen = true
let noFurtherResult = ()

printfn "%s (%c): capacity=%d, fill=%.2f, open=%b" eventName eventCode capacity fillRatio registrationOpen
// #endregion basic-values

// #region annotations-and-conversion
let requestedSeats: int = 3
let pricePerSeat: decimal = 19.50m
let totalPrice = decimal requestedSeats * pricePerSeat

printfn "Ticket total: %M" totalPrice
// #endregion annotations-and-conversion

// #region local-shadowing
let normalizedCapacity =
    let capacity = 20
    let capacity = capacity + 4
    capacity

printfn "Normalized capacity: %d; outer capacity: %d" normalizedCapacity capacity
// #endregion local-shadowing

// #region exercise-02
let rawAttendeeCount = "24"
let attendeeCount = int rawAttendeeCount
let nextAttendeeCount = attendeeCount + 1

printfn "Next attendee count: %d" nextAttendeeCount
// #endregion exercise-02
