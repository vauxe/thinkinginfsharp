open System

// #region model
type ValidationError =
    | MissingRequestId
    | MissingAttendee
    | SeatsNotInteger of raw: string
    | NonPositiveSeats of actual: int
    | ExceedsCapacity of requested: int * available: int

type RequestId = RequestId of string
type Attendee = Attendee of string
type SeatCount = SeatCount of int

type RawBooking =
    { RequestId: string
      Attendee: string
      Seats: string }

type ValidBooking =
    { RequestId: RequestId
      Attendee: Attendee
      Seats: SeatCount }
// #endregion model

// #region field-validation
let validateRequestId raw =
    if String.IsNullOrWhiteSpace raw then
        Error [ MissingRequestId ]
    else
        Ok(RequestId(raw.Trim()))

let validateAttendee raw =
    if String.IsNullOrWhiteSpace raw then
        Error [ MissingAttendee ]
    else
        Ok(Attendee(raw.Trim()))

let validateSeats (raw: string) =
    match Int32.TryParse raw with
    | true, value when value > 0 -> Ok(SeatCount value)
    | true, value -> Error [ NonPositiveSeats value ]
    | false, _ -> Error [ SeatsNotInteger raw ]
// #endregion field-validation

// #region first-error
let validateFirstError (raw: RawBooking) =
    validateRequestId raw.RequestId
    |> Result.bind (fun requestId ->
        validateAttendee raw.Attendee
        |> Result.bind (fun attendee ->
            validateSeats raw.Seats
            |> Result.map (fun seats ->
                { RequestId = requestId
                  Attendee = attendee
                  Seats = seats })))
// #endregion first-error

// #region accumulation
let errorsOf result =
    match result with
    | Ok _ -> []
    | Error errors -> errors

let validateAccumulating (raw: RawBooking) =
    let requestIdResult = validateRequestId raw.RequestId
    let attendeeResult = validateAttendee raw.Attendee
    let seatsResult = validateSeats raw.Seats

    match requestIdResult, attendeeResult, seatsResult with
    | Ok requestId, Ok attendee, Ok seats ->
        Ok
            { RequestId = requestId
              Attendee = attendee
              Seats = seats }
    | _ ->
        [ yield! errorsOf requestIdResult
          yield! errorsOf attendeeResult
          yield! errorsOf seatsResult ]
        |> Error
// #endregion accumulation

// #region reusable-accumulation
let applyValidation valueResult functionResult =
    match functionResult, valueResult with
    | Ok mapping, Ok value -> Ok(mapping value)
    | Error functionErrors, Error valueErrors -> Error(functionErrors @ valueErrors)
    | Error errors, Ok _
    | Ok _, Error errors -> Error errors

let createBooking requestId attendee seats : ValidBooking =
    { RequestId = requestId
      Attendee = attendee
      Seats = seats }

let validateAccumulatingWithApply (raw: RawBooking) =
    Ok createBooking
    |> applyValidation (validateRequestId raw.RequestId)
    |> applyValidation (validateAttendee raw.Attendee)
    |> applyValidation (validateSeats raw.Seats)
// #endregion reusable-accumulation

// #region dependent-workflow
let ensureWithin capacity (SeatCount requested as seats) =
    if requested <= capacity then
        Ok seats
    else
        Error [ ExceedsCapacity(requested, capacity) ]

let validateSeatsThenCapacity checkCapacity rawSeats =
    validateSeats rawSeats |> Result.bind checkCapacity

let observeDependentValidation rawSeats =
    let mutable capacityChecks = 0

    let observedCheck seats =
        capacityChecks <- capacityChecks + 1
        ensureWithin 4 seats

    validateSeatsThenCapacity observedCheck rawSeats, capacityChecks
// #endregion dependent-workflow

// #region evidence
let renderError error =
    match error with
    | MissingRequestId -> "missing-request-id"
    | MissingAttendee -> "missing-attendee"
    | SeatsNotInteger raw -> $"seats-not-integer:{raw}"
    | NonPositiveSeats actual -> $"non-positive-seats:{actual}"
    | ExceedsCapacity(requested, available) -> $"exceeds-capacity:{requested}/{available}"

let renderErrors errors =
    errors |> List.map renderError |> String.concat "; " |> sprintf "[%s]"

let renderBooking (booking: ValidBooking) =
    let (RequestId requestId) = booking.RequestId
    let (Attendee attendee) = booking.Attendee
    let (SeatCount seats) = booking.Seats
    $"{requestId}|{attendee}|{seats}"

let renderResult renderOk result =
    match result with
    | Ok value -> renderOk value
    | Error errors -> renderErrors errors

let invalid: RawBooking =
    { RequestId = " "
      Attendee = ""
      Seats = "oops" }

let mixed: RawBooking =
    { RequestId = "REQ-18"
      Attendee = " "
      Seats = "0" }

let valid: RawBooking =
    { RequestId = " REQ-18 "
      Attendee = " Lin "
      Seats = "3" }

let firstError = validateFirstError invalid
let allErrors = validateAccumulating invalid
let mixedErrors = validateAccumulating mixed
let validFirst = validateFirstError valid
let validAccumulated = validateAccumulating valid

assert (firstError = Error [ MissingRequestId ])

assert (allErrors = Error [ MissingRequestId; MissingAttendee; SeatsNotInteger "oops" ])

assert (mixedErrors = Error [ MissingAttendee; NonPositiveSeats 0 ])
assert (validFirst = validAccumulated)
assert (validateAccumulatingWithApply invalid = allErrors)
assert (validateAccumulatingWithApply mixed = mixedErrors)
assert (validateAccumulatingWithApply valid = validAccumulated)

let invalidSeatsResult, invalidSeatsChecks = observeDependentValidation "oops"
let excessiveSeatsResult, excessiveSeatsChecks = observeDependentValidation "5"
let acceptedSeatsResult, acceptedSeatsChecks = observeDependentValidation "3"

assert (invalidSeatsResult = Error [ SeatsNotInteger "oops" ])
assert (invalidSeatsChecks = 0)
assert (excessiveSeatsResult = Error [ ExceedsCapacity(5, 4) ])
assert (excessiveSeatsChecks = 1)
assert (acceptedSeatsResult = Ok(SeatCount 3))
assert (acceptedSeatsChecks = 1)

printfn "First error: %s" (renderResult renderBooking firstError)
printfn "Accumulated invalid: %s" (renderResult renderBooking allErrors)
printfn "Accumulated mixed: %s" (renderResult renderBooking mixedErrors)
printfn "Valid strategies agree: %b (%s)" (validFirst = validAccumulated) (renderResult renderBooking validAccumulated)

printfn
    "Dependent invalid: %s capacity-checks=%d"
    (renderResult (fun (SeatCount seats) -> $"ok:{seats}") invalidSeatsResult)
    invalidSeatsChecks

printfn
    "Dependent excessive: %s capacity-checks=%d"
    (renderResult (fun (SeatCount seats) -> $"ok:{seats}") excessiveSeatsResult)
    excessiveSeatsChecks

printfn
    "Dependent accepted: %s capacity-checks=%d"
    (renderResult (fun (SeatCount seats) -> $"ok:{seats}") acceptedSeatsResult)
    acceptedSeatsChecks
// #endregion evidence
