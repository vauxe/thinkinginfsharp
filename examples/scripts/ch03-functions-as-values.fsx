// #region curried-function
let lineTotal unitPrice seats = unitPrice * decimal seats
let standardLineTotal = lineTotal 19.50m
let totalForThree = standardLineTotal 3

printfn "Curried total: %M" totalForThree
// #endregion curried-function

// #region tupled-function
let lineTotalTupled (unitPrice, seats) = unitPrice * decimal seats
let tupledTotal = lineTotalTupled (19.50m, 3)

printfn "Tupled total: %M" tupledTotal
// #endregion tupled-function

// #region named-and-anonymous
let increment seats = seats + 1
let incrementAnonymous = fun seats -> seats + 1

printfn "Named and anonymous: %d, %d" (increment 3) (incrementAnonymous 3)
// #endregion named-and-anonymous

// #region higher-order
let applyTwice transform value = transform (transform value)
let incrementedTwice = applyTwice increment 3

printfn "Applied twice: %d" incrementedTwice
// #endregion higher-order

// #region returned-function
let addFee fee subtotal = subtotal + fee
let addServiceFee = addFee 2.00m
let finalTotal = addServiceFee totalForThree

printfn "With service fee: %M" finalTotal
// #endregion returned-function

// #region generic-identity
let identity value = value
let unchangedNumber = identity 42
let unchangedText = identity "F#"

printfn "Identity values: %d, %s" unchangedNumber unchangedText
// #endregion generic-identity
