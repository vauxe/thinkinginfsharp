open System

type BookingStatus =
    | Pending
    | Confirmed of confirmationCode: string
    | Cancelled of reason: string

// #region complete-active-pattern
let (|Open|Closed|) status =
    match status with
    | Pending -> Open "pending"
    | Confirmed code -> Open $"confirmed:{code}"
    | Cancelled reason -> Closed reason

let describeStatus status =
    match status with
    | Open detail -> $"open:{detail}"
    | Closed reason -> $"closed:{reason}"

printfn "Complete: pending=%s" (describeStatus Pending)
printfn "Complete: confirmed=%s" (describeStatus (Confirmed "C-42"))
printfn "Complete: cancelled=%s" (describeStatus (Cancelled "duplicate"))
// #endregion complete-active-pattern

type SeatCountError =
    | NotAnInteger of raw: string
    | NotPositive of actual: int

let parseSeatCount (raw: string) =
    match Int32.TryParse raw with
    | true, value when value > 0 -> Ok value
    | true, value -> Error(NotPositive value)
    | false, _ -> Error(NotAnInteger raw)

// #region partial-active-pattern
let (|SeatCount|_|) raw =
    match parseSeatCount raw with
    | Ok value -> Some value
    | Error _ -> None

let describeRawSeatCount raw =
    match raw with
    | SeatCount value -> $"matched:{value}"
    | _ -> "not-matched"

printfn
    "Partial: three=%s zero=%s text=%s"
    (describeRawSeatCount "3")
    (describeRawSeatCount "0")
    (describeRawSeatCount "oops")
// #endregion partial-active-pattern

let describeSeatCountError raw =
    match parseSeatCount raw with
    | Ok value -> $"ok:{value}"
    | Error(NotPositive actual) -> $"not-positive:{actual}"
    | Error(NotAnInteger invalid) -> $"not-an-integer:{invalid}"

printfn
    "Explicit errors: zero=%s text=%s"
    (describeSeatCountError "0")
    (describeSeatCountError "oops")

// #region parameterized-active-pattern
let mutable thresholdChecks = 0

let (|AtLeast|_|) minimum value =
    thresholdChecks <- thresholdChecks + 1

    if value >= minimum then
        Some value
    else
        None

let classifyParty seats =
    match seats with
    | AtLeast 5 actual -> $"large:{actual}"
    | AtLeast 2 actual -> $"group:{actual}"
    | actual -> $"single:{actual}"

let classifyWithCount seats =
    thresholdChecks <- 0
    let label = classifyParty seats
    label, thresholdChecks

let largeLabel, largeChecks = classifyWithCount 6
let groupLabel, groupChecks = classifyWithCount 3
let singleLabel, singleChecks = classifyWithCount 1

printfn
    "Parameterized: six=%s/%d three=%s/%d one=%s/%d"
    largeLabel
    largeChecks
    groupLabel
    groupChecks
    singleLabel
    singleChecks
// #endregion parameterized-active-pattern
