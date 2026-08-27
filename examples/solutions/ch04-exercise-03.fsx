// #region solution
let classifyRequest (remaining, requested) =
    match remaining, requested with
    | _, requested when requested <= 0 -> "invalid"
    | remaining, requested when requested <= remaining -> "accepted"
    | _ -> "too large"

printfn "Requests: %s, %s, %s" (classifyRequest (5, 0)) (classifyRequest (5, 3)) (classifyRequest (2, 3))
// #endregion solution
