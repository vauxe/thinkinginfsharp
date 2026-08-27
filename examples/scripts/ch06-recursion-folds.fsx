let seatCounts = [ 3; 0; 4; 2 ]

// #region direct-recursion
let rec sumRecursive values =
    match values with
    | [] -> 0
    | head :: tail -> head + sumRecursive tail
// #endregion direct-recursion

// #region tail-recursion
[<TailCall>]
let rec sumLoop accumulator values =
    match values with
    | [] -> accumulator
    | head :: tail -> sumLoop (accumulator + head) tail

let sumTailRecursive values = sumLoop 0 values
// #endregion tail-recursion

// #region fold-sum
let sumWithFold values =
    values |> List.fold (fun accumulator value -> accumulator + value) 0
// #endregion fold-sum

let recursiveTotal = sumRecursive seatCounts
let tailRecursiveTotal = sumTailRecursive seatCounts
let foldedTotal = sumWithFold seatCounts

printfn "Sums: recursive=%d tail=%d fold=%d" recursiveTotal tailRecursiveTotal foldedTotal
printfn "Empty sums: %d, %d, %d" (sumRecursive []) (sumTailRecursive []) (sumWithFold [])
printfn "Singleton sums: %d, %d, %d" (sumRecursive [ 5 ]) (sumTailRecursive [ 5 ]) (sumWithFold [ 5 ])

// #region tail-count
[<TailCall>]
let rec countLoop accumulator values =
    match values with
    | [] -> accumulator
    | _ :: tail -> countLoop (accumulator + 1) tail

let countTailRecursive values = countLoop 0 values
let largeCount = countTailRecursive [ 1..100_000 ]

printfn "Tail-recursive count: %d" largeCount
// #endregion tail-count

// #region fold-order
let leftAssociated = List.fold (fun state value -> state - value) 0 [ 1; 2; 3 ]
let rightAssociated = List.foldBack (fun value state -> value - state) [ 1; 2; 3 ] 0

printfn "Fold order: left=%d right=%d" leftAssociated rightAssociated
// #endregion fold-order
