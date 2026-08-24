// #region if-expression
let availability remaining =
    if remaining > 0 then
        "available"
    else
        "full"

printfn "Availability: %s" (availability 3)
// #endregion if-expression

// #region guarded-match
let capacityBand remaining =
    match remaining with
    | value when value <= 0 -> "full"
    | 1 -> "last seat"
    | value when value <= 5 -> "limited"
    | _ -> "available"

printfn "Capacity bands: %s, %s, %s, %s" (capacityBand 0) (capacityBand 1) (capacityBand 4) (capacityBand 8)
// #endregion guarded-match

// #region tuple-pattern
let bookingSummary (guest, seats) =
    let noun = if seats = 1 then "seat" else "seats"
    $"{guest} requested {seats} {noun}"

printfn "Booking: %s" (bookingSummary ("Lin", 3))
// #endregion tuple-pattern

// #region list-pattern
let describeQueue queue =
    match queue with
    | [] -> "empty"
    | [ only ] -> $"one: {only}"
    | first :: second :: _ -> $"next: {first}, then {second}"

printfn "Queues: %s | %s | %s" (describeQueue []) (describeQueue [ "Lin" ]) (describeQueue [ "Lin"; "Ada"; "Sam" ])
// #endregion list-pattern

// #region exercise-03
let classifyRequest (remaining, requested) =
    match remaining, requested with
    | _, requested when requested <= 0 -> "invalid"
    | remaining, requested when requested <= remaining -> "accepted"
    | _ -> "too large"

printfn "Requests: %s, %s, %s" (classifyRequest (5, 0)) (classifyRequest (5, 3)) (classifyRequest (2, 3))
// #endregion exercise-03
