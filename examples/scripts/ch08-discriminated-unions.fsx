// #region flag-contradiction
type BookingFlags =
    { IsPending: bool
      IsConfirmed: bool
      IsCancelled: bool }

let contradictoryFlags =
    { IsPending = true
      IsConfirmed = true
      IsCancelled = false }

printfn
    "Flag model contradiction: pending=%b confirmed=%b cancelled=%b"
    contradictoryFlags.IsPending
    contradictoryFlags.IsConfirmed
    contradictoryFlags.IsCancelled
// #endregion flag-contradiction

// #region union-definition
type BookingStatus =
    | Pending
    | Confirmed of confirmationCode: string
    | Cancelled of reason: string
// #endregion union-definition

// #region exhaustive-match
let describeStatus status =
    match status with
    | Pending -> "pending"
    | Confirmed confirmationCode -> $"confirmed:{confirmationCode}"
    | Cancelled reason -> $"cancelled:{reason}"

let statuses =
    [ Pending
      Confirmed "C-42"
      Cancelled "duplicate" ]

let descriptions = statuses |> List.map describeStatus

printfn "Statuses: %A" descriptions
// #endregion exhaustive-match

// #region case-data
let confirmationCode status =
    match status with
    | Confirmed code -> Some code
    | Pending
    | Cancelled _ -> None

printfn "Confirmed case carries code: %s" (confirmationCode (Confirmed "C-42") |> Option.defaultValue "none")
// #endregion case-data

// #region transition
let confirm code status =
    match status with
    | Pending -> Confirmed code
    | Confirmed _
    | Cancelled _ -> status

let transitioned = Pending |> confirm "C-99"

printfn "Transition: pending -> %s" (describeStatus transitioned)
printfn "All descriptions: %d" (List.length descriptions)
// #endregion transition
