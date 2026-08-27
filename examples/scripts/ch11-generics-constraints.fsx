// #region automatic-generalization
let duplicate value = [ value; value ]

let integerCopies = duplicate 3
let attendeeCopies = duplicate "Lin"

printfn "Generalized function: ints=%A strings=%A" integerCopies attendeeCopies

let genericEmpty = []
let oneInteger = 1 :: genericEmpty
let oneAttendee = "Ada" :: genericEmpty

printfn "Simple generic value: ints=%A strings=%A" oneInteger oneAttendee
// #endregion automatic-generalization

// #region value-restriction-fixes
let makeEmptyBuckets () = Array.create 2 []

let integerBuckets: int list array = makeEmptyBuckets ()
let attendeeBuckets: string list array = makeEmptyBuckets ()

let anotherIntegerBuckets: int list array = makeEmptyBuckets ()

printfn
    "Value restriction fixes: ints=%d strings=%d fresh=%b"
    integerBuckets.Length
    attendeeBuckets.Length
    (not (LanguagePrimitives.PhysicalEquality integerBuckets anotherIntegerBuckets))
// #endregion value-restriction-fixes

// #region equality-comparison
type Envelope<'T> = { Label: string; Payload: 'T }

let same left right = left = right
let comesBefore left right = compare left right < 0

let first = { Label = "A"; Payload = 2 }

let firstAgain = { Label = "A"; Payload = 2 }

let second = { Label = "B"; Payload = 1 }

let sortedLabels =
    [ second; first ] |> List.sort |> List.map (fun envelope -> envelope.Label)

printfn "Constraints: equal=%b ordered=%b sorted=%A" (same first firstAgain) (comesBefore first second) sortedLabels
// #endregion equality-comparison

// #region units-of-measure
[<Measure>]
type seat

[<Measure>]
type minute

let addMeasured (left: int<'Measure>) (right: int<'Measure>) = left + right

let capacity = 40<seat>
let requested = addMeasured 2<seat> 3<seat>
let remaining = capacity - requested
let bookingRate = 12.0<seat> / 3.0<minute>

printfn "Measures: requested=%d remaining=%d rate=%.1f" requested remaining bookingRate
// #endregion units-of-measure
