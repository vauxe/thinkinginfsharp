open System

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

let invalid: RawBooking =
    { RequestId = " "
      Attendee = ""
      Seats = "oops" }

let valid: RawBooking =
    { RequestId = " REQ-18 "
      Attendee = " Lin "
      Seats = "3" }

let expectedErrors =
    Error [ MissingRequestId; MissingAttendee; SeatsNotInteger "oops" ]

let expectedValid =
    Ok
        { RequestId = RequestId "REQ-18"
          Attendee = Attendee "Lin"
          Seats = SeatCount 3 }

printfn "first-error: %b" (validateFirstError invalid = Error [ MissingRequestId ])
printfn "all-errors: %b" (validateAccumulating invalid = expectedErrors)
printfn "apply-agrees: %b" (validateAccumulatingWithApply invalid = expectedErrors)
printfn "valid-booking: %b" (validateAccumulating valid = expectedValid)

printfn
    "dependent: parse=%A over=%A fit=%A"
    (observeDependentValidation "oops")
    (observeDependentValidation "5")
    (observeDependentValidation "3")
