namespace Booking.Api

open System
open System.Globalization
open System.IO
open System.Text.Json
open System.Text.Json.Serialization
open System.Threading.Tasks
open Booking.Contracts
open Booking.Domain
open Booking.Domain.Ports
open Booking.Domain.Validation
open Booking.Domain.Workflow
open Booking.Infrastructure
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http

// #region api-error-contract
[<CLIMutable>]
type ApiFieldErrorDto =
    { [<JsonPropertyName("field")>]
      Field: string
      [<JsonPropertyName("code")>]
      Code: string }

[<CLIMutable>]
type ApiErrorDto =
    { [<JsonPropertyName("code")>]
      Code: string
      [<JsonPropertyName("message")>]
      Message: string
      [<JsonPropertyName("errors")>]
      Errors: ApiFieldErrorDto array }
// #endregion api-error-contract

type BookingApiDependencies = { Activity: Event; Ports: AsyncPorts }

[<RequireQualifiedAccess>]
module BookingEndpoints =
    [<Literal>]
    let MaxRequestBodyBytes = 16384

    let private jsonOptions =
        let options = JsonSerializerOptions()
        BookingJson.configure options
        options

    type private BodyError =
        | UnsupportedMediaType
        | TooLarge
        | InvalidJson

    type private PreparedCommand =
        { Command: BookingCommand
          RequestId: RequestId
          Payment: PaymentRequest option
          SuccessStatusCode: int }

    type private PaymentStep =
        | PaymentAccepted
        | PaymentRefused
        | PaymentUnavailable

    let private writeJson (context: HttpContext) statusCode value =
        task {
            context.Response.StatusCode <- statusCode
            context.Response.ContentType <- "application/json; charset=utf-8"

            do! JsonSerializer.SerializeAsync(context.Response.Body, value, jsonOptions, context.RequestAborted)
        }

    let private writeError context statusCode code message errors =
        writeJson
            context
            statusCode
            { Code = code
              Message = message
              Errors = errors }

    let private fieldError field code = { Field = field; Code = code }

    let private dtoErrorDetail error =
        match error with
        | DtoMappingError.MissingBody -> None
        | DtoMappingError.UnsupportedSchemaVersion _ -> Some(fieldError "schemaVersion" "unsupported")
        | DtoMappingError.MissingRequestId -> Some(fieldError "requestId" "missing")
        | DtoMappingError.MissingEventId -> Some(fieldError "eventId" "missing")
        | DtoMappingError.MissingSeats -> Some(fieldError "seats" "missing")
        | DtoMappingError.MissingStatus -> Some(fieldError "status" "missing")
        | DtoMappingError.MissingConfirmationCode -> Some(fieldError "confirmationCode" "missing")
        | DtoMappingError.MissingCancellationReason -> Some(fieldError "reason" "missing")
        | DtoMappingError.InvalidRequestId _ -> Some(fieldError "requestId" "blank")
        | DtoMappingError.InvalidEventId _ -> Some(fieldError "eventId" "blank")
        | DtoMappingError.InvalidSeatCount _ -> Some(fieldError "seats" "non_positive")
        | DtoMappingError.InvalidConfirmationCode _ -> Some(fieldError "confirmationCode" "blank")
        | DtoMappingError.InvalidCancellationReason _ -> Some(fieldError "reason" "blank")
        | DtoMappingError.UnknownStatus _ -> Some(fieldError "status" "unknown")
        | DtoMappingError.UnexpectedConfirmationCode _ -> Some(fieldError "confirmationCode" "unexpected")
        | DtoMappingError.UnexpectedCancellationReason _ -> Some(fieldError "reason" "unexpected")

    let private commandErrorDetail error =
        match error with
        | CommandValidationError.InvalidRequestId _ -> fieldError "requestId" "blank"
        | CommandValidationError.InvalidSeatCount _ -> fieldError "seats" "non_positive"
        | CommandValidationError.InvalidConfirmationCode _ -> fieldError "confirmationCode" "blank"
        | CommandValidationError.InvalidCancellationReason _ -> fieldError "reason" "blank"

    let private writeDtoError context error =
        let errors = dtoErrorDetail error |> Option.toArray
        writeError context StatusCodes.Status400BadRequest "invalid_request" "The request is invalid." errors

    let private writeValidationErrors context errors =
        writeError
            context
            StatusCodes.Status400BadRequest
            "validation_failed"
            "One or more fields are invalid."
            (errors |> List.map commandErrorDetail |> List.toArray)

    let private writeDecisionError context error =
        match error with
        | BookingDecisionError.InvalidCommand errors -> writeValidationErrors context errors
        | BookingDecisionError.BookingAlreadyExists _ ->
            writeError
                context
                StatusCodes.Status409Conflict
                "booking_already_exists"
                "A booking already exists for this request ID."
                [||]
        | BookingDecisionError.BookingDoesNotExist ->
            writeError
                context
                StatusCodes.Status404NotFound
                "booking_not_found"
                "No booking exists for this request ID."
                [||]
        | BookingDecisionError.BookingCreationFailed _ ->
            writeError
                context
                StatusCodes.Status409Conflict
                "capacity_exceeded"
                "The requested seats exceed this activity's capacity."
                [||]
        | BookingDecisionError.BookingTransitionFailed _ ->
            writeError
                context
                StatusCodes.Status409Conflict
                "invalid_transition"
                "The requested booking transition is not allowed."
                [||]

    let private writeBodyError context error =
        match error with
        | UnsupportedMediaType ->
            writeError
                context
                StatusCodes.Status415UnsupportedMediaType
                "unsupported_media_type"
                "Content-Type must be a JSON media type."
                [||]
        | TooLarge ->
            writeError
                context
                StatusCodes.Status413PayloadTooLarge
                "request_too_large"
                "The request body is too large."
                [||]
        | InvalidJson ->
            writeError
                context
                StatusCodes.Status400BadRequest
                "invalid_json"
                "The request body is not valid JSON for this endpoint."
                [||]

    // #region bounded-json-body
    // The small command contract is buffered only up to the documented limit. This also
    // enforces the limit under TestServer, where Kestrel-specific limits do not run.
    let private readBody (context: HttpContext) =
        task {
            if not (context.Request.HasJsonContentType()) then
                return Error UnsupportedMediaType
            elif
                context.Request.ContentLength.HasValue
                && context.Request.ContentLength.Value > int64 MaxRequestBodyBytes
            then
                return Error TooLarge
            else
                use body = new MemoryStream(MaxRequestBodyBytes)
                let chunk = Array.zeroCreate<byte> 4096
                let mutable finished = false
                let mutable tooLarge = false

                while not finished && not tooLarge do
                    let remaining = MaxRequestBodyBytes - int body.Length
                    let requested = min chunk.Length (remaining + 1)

                    let! count = context.Request.Body.ReadAsync(chunk.AsMemory(0, requested), context.RequestAborted)

                    if count = 0 then
                        finished <- true
                    elif body.Length + int64 count > int64 MaxRequestBodyBytes then
                        tooLarge <- true
                    else
                        body.Write(chunk, 0, count)

                if tooLarge then
                    return Error TooLarge
                else
                    return Ok(body.ToArray())
        }

    let private deserialize<'dto when 'dto: not struct and 'dto: not null>
        (bytes: byte array)
        : Result<'dto | null, BodyError> =
        try
            let span = ReadOnlySpan<byte>(bytes)
            Ok(JsonSerializer.Deserialize<'dto>(span, jsonOptions))
        with :? JsonException ->
            Error InvalidJson
    // #endregion bounded-json-body

    let private preparePlace command =
        Validation.validatePlaceBooking command
        |> Result.map (fun valid ->
            { Command = BookingCommand.Place command
              RequestId = ValidPlaceBooking.requestId valid
              Payment =
                Some
                    { RequestId = ValidPlaceBooking.requestId valid
                      Seats = ValidPlaceBooking.seats valid }
              SuccessStatusCode = StatusCodes.Status201Created })

    let private prepareConfirm command =
        Validation.validateConfirmBooking command
        |> Result.map (fun valid ->
            { Command = BookingCommand.Confirm command
              RequestId = ValidConfirmBooking.requestId valid
              Payment = None
              SuccessStatusCode = StatusCodes.Status200OK })

    let private prepareCancel command =
        Validation.validateCancelBooking command
        |> Result.map (fun valid ->
            { Command = BookingCommand.Cancel command
              RequestId = ValidCancelBooking.requestId valid
              Payment = None
              SuccessStatusCode = StatusCodes.Status200OK })

    let private tryExternal start =
        task {
            try
                let! value = start ()
                return Ok value
            with
            | :? OperationCanceledException as error -> return raise error
            | _ -> return Error()
        }

    let private authorize ports payment cancellationToken =
        task {
            match payment with
            | None -> return PaymentAccepted
            | Some request ->
                let! outcome = tryExternal (fun () -> ports.Charge request cancellationToken)

                return
                    match outcome with
                    | Ok(PaymentOutcome.Authorized _) -> PaymentAccepted
                    | Ok(PaymentOutcome.Declined _) -> PaymentRefused
                    | Error() -> PaymentUnavailable
        }

    let private notificationFor bookingEvent =
        let message =
            match bookingEvent with
            | BookingPlaced _ -> "booking placed"
            | BookingConfirmed _ -> "booking confirmed"
            | BookingCancelled _ -> "booking cancelled"

        { RequestId = BookingEvent.requestId bookingEvent
          Message = message }

    // #region endpoint-workflow
    let private executeCommand dependencies prepared (context: HttpContext) =
        task {
            let cancellationToken = context.RequestAborted
            cancellationToken.ThrowIfCancellationRequested()
            let! state = dependencies.Ports.LoadBooking prepared.RequestId cancellationToken

            match Decider.decide dependencies.Activity state prepared.Command with
            | Error error -> return! writeDecisionError context error
            | Ok bookingEvent ->
                let! payment = authorize dependencies.Ports prepared.Payment cancellationToken

                match payment with
                | PaymentRefused ->
                    return!
                        writeError
                            context
                            StatusCodes.Status422UnprocessableEntity
                            "payment_declined"
                            "Payment was declined."
                            [||]
                | PaymentUnavailable ->
                    return!
                        writeError
                            context
                            StatusCodes.Status503ServiceUnavailable
                            "dependency_unavailable"
                            "An external dependency is unavailable."
                            [||]
                | PaymentAccepted ->
                    do! dependencies.Ports.AppendEvent prepared.RequestId bookingEvent cancellationToken

                    let! notified =
                        tryExternal (fun () ->
                            dependencies.Ports.Notify (notificationFor bookingEvent) cancellationToken)

                    match notified with
                    | Error() ->
                        return!
                            writeError
                                context
                                StatusCodes.Status503ServiceUnavailable
                                "dependency_unavailable"
                                "An external dependency is unavailable."
                                [||]
                    | Ok() ->
                        let booking = BookingEvent.booking bookingEvent

                        if prepared.SuccessStatusCode = StatusCodes.Status201Created then
                            let requestId =
                                booking |> Booking.requestId |> RequestId.value |> Uri.EscapeDataString

                            context.Response.Headers.Location <- $"/api/bookings/{requestId}"

                        return! writeJson context prepared.SuccessStatusCode (BookingMapping.ofDomain booking)
        }
    // #endregion endpoint-workflow

    let private processCommand<'dto, 'command when 'dto: not struct and 'dto: not null>
        (deserializeDto: byte array -> Result<'dto | null, BodyError>)
        (mapDto: ('dto | null) -> Result<'command, DtoMappingError>)
        (prepare: 'command -> Result<PreparedCommand, CommandValidationError list>)
        dependencies
        context
        =
        task {
            let! body = readBody context

            match body with
            | Error error -> return! writeBodyError context error
            | Ok bytes ->
                match deserializeDto bytes with
                | Error error -> return! writeBodyError context error
                | Ok dto ->
                    match mapDto dto with
                    | Error error -> return! writeDtoError context error
                    | Ok command ->
                        match prepare command with
                        | Error errors -> return! writeValidationErrors context errors
                        | Ok prepared -> return! executeCommand dependencies prepared context
        }

    let private handlePlace dependencies context =
        processCommand (deserialize<PlaceBookingDto>) PlaceBookingMapping.toDomain preparePlace dependencies context

    let private handleConfirm dependencies context =
        processCommand
            (deserialize<ConfirmBookingDto>)
            ConfirmBookingMapping.toDomain
            prepareConfirm
            dependencies
            context

    let private handleCancel dependencies context =
        processCommand (deserialize<CancelBookingDto>) CancelBookingMapping.toDomain prepareCancel dependencies context

    let private handleGet dependencies (context: HttpContext) =
        task {
            let rawRequestId =
                match context.Request.RouteValues.TryGetValue "requestId" with
                | true, value ->
                    match Convert.ToString(value, CultureInfo.InvariantCulture) with
                    | null -> String.Empty
                    | converted -> converted
                | false, _ -> String.Empty

            match RequestId.create rawRequestId with
            | Error _ ->
                return!
                    writeError
                        context
                        StatusCodes.Status400BadRequest
                        "validation_failed"
                        "One or more fields are invalid."
                        [| fieldError "requestId" "blank" |]
            | Ok requestId ->
                let! state = dependencies.Ports.LoadBooking requestId context.RequestAborted

                match state with
                | Booked booking when Booking.requestId booking = requestId ->
                    return! writeJson context StatusCodes.Status200OK (BookingMapping.ofDomain booking)
                | Booked _
                | NotBooked ->
                    return!
                        writeError
                            context
                            StatusCodes.Status404NotFound
                            "booking_not_found"
                            "No booking exists for this request ID."
                            [||]
        }

    // #region safe-error-boundary
    let private safely handler (context: HttpContext) =
        task {
            try
                return! handler context
            with
            | :? OperationCanceledException as error when context.RequestAborted.IsCancellationRequested ->
                return raise error
            | :? OperationCanceledException ->
                return!
                    writeError
                        context
                        StatusCodes.Status503ServiceUnavailable
                        "dependency_unavailable"
                        "An external dependency is unavailable."
                        [||]
            | :? BadHttpRequestException as error when error.StatusCode = StatusCodes.Status413PayloadTooLarge ->
                return! writeBodyError context TooLarge
            | :? BookingStoreAdapterException ->
                return!
                    writeError
                        context
                        StatusCodes.Status503ServiceUnavailable
                        "storage_unavailable"
                        "Booking storage is unavailable."
                        [||]
            | _ when context.Response.HasStarted -> context.Abort()
            | _ ->
                return!
                    writeError
                        context
                        StatusCodes.Status500InternalServerError
                        "internal_error"
                        "The request could not be completed."
                        [||]
        }
    // #endregion safe-error-boundary

    // #region endpoint-map
    let map (application: WebApplication) dependencies =
        ArgumentNullException.ThrowIfNull(application, nameof application)

        let protectedHandler handler =
            RequestDelegate(fun context -> safely handler context)

        application.MapPost("/api/bookings/place", protectedHandler (handlePlace dependencies))
        |> ignore

        application.MapPost("/api/bookings/confirm", protectedHandler (handleConfirm dependencies))
        |> ignore

        application.MapPost("/api/bookings/cancel", protectedHandler (handleCancel dependencies))
        |> ignore

        application.MapGet("/api/bookings/{requestId}", protectedHandler (handleGet dependencies))
        |> ignore
// #endregion endpoint-map
