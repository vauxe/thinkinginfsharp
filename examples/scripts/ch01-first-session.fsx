// #region first-session
let eventName = "Functional Foundations"
let capacity = 40
let booked = 18
let remaining = capacity - booked
let hasSeats = remaining > 0
let summary = $"{eventName}: {remaining} seats remaining"

let printResult = printfn "%s" summary
printfn "Seats available: %b" hasSeats
printfn "Printing returned: %A" printResult
// #endregion first-session

// #region exercise-02
let guest = "Lin"
let requestedSeats = 3
let confirmation = $"{guest} booked {requestedSeats} seats."

printfn "%s" confirmation
// #endregion exercise-02
