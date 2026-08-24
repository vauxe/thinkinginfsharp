namespace ThinkingInFSharp.ContractTests

open System
open System.Text.Json
open Booking.Contracts
open Booking.Domain
open Xunit

module BookingJsonContractTests =
    let private expectOk result =
        match result with
        | Ok value -> value
        | Error error -> failwithf "Expected Ok, received Error %A" error

    let private createPending requestId =
        let eventId = EventId.create "EVT-K08" |> expectOk
        let capacity = Capacity.create 8 |> expectOk
        let seats = SeatCount.create 3 |> expectOk
        let validRequestId = RequestId.create requestId |> expectOk
        let activity = Event.create eventId capacity
        Booking.create activity validRequestId seats |> expectOk

    let private confirm booking =
        let code = ConfirmationCode.create "CONF-K08" |> expectOk
        Booking.confirm code booking |> expectOk

    let private cancel booking =
        let reason = CancellationReason.create "duplicate request" |> expectOk
        Booking.cancel reason booking |> expectOk

    let private propertyNames (json: string) : Set<string> =
        use document = JsonDocument.Parse(json)

        document.RootElement.EnumerateObject()
        |> Seq.map (fun property -> property.Name)
        |> Set.ofSeq

    let private validPendingDto () : BookingDto =
        { SchemaVersion = BookingContract.CurrentSchemaVersion
          RequestId = "REQ-K08"
          EventId = "EVT-K08"
          Seats = Nullable 3
          Status = "pending"
          ConfirmationCode = null
          CancellationReason = null }

    [<Fact>]
    let ``booking statuses use a stable tagged json shape`` () =
        let pending =
            createPending "REQ-PENDING"
            |> BookingMapping.ofDomain
            |> BookingJson.serializeBooking

        let confirmed =
            createPending "REQ-CONFIRMED"
            |> confirm
            |> BookingMapping.ofDomain
            |> BookingJson.serializeBooking

        let cancelled =
            createPending "REQ-CANCELLED"
            |> cancel
            |> BookingMapping.ofDomain
            |> BookingJson.serializeBooking

        let pendingProperties =
            Set.ofList [ "schemaVersion"; "requestId"; "eventId"; "seats"; "status" ]

        let confirmedProperties =
            Set.ofList
                [ "schemaVersion"
                  "requestId"
                  "eventId"
                  "seats"
                  "status"
                  "confirmationCode" ]

        let cancelledProperties =
            Set.ofList
                [ "schemaVersion"
                  "requestId"
                  "eventId"
                  "seats"
                  "status"
                  "cancellationReason" ]

        Assert.True(propertyNames pending = pendingProperties)
        Assert.True(propertyNames confirmed = confirmedProperties)
        Assert.True(propertyNames cancelled = cancelledProperties)

        use pendingDocument = JsonDocument.Parse(pending)
        use confirmedDocument = JsonDocument.Parse(confirmed)
        use cancelledDocument = JsonDocument.Parse(cancelled)

        Assert.Equal(1, pendingDocument.RootElement.GetProperty("schemaVersion").GetInt32())
        Assert.Equal("pending", pendingDocument.RootElement.GetProperty("status").GetString())
        Assert.Equal("confirmed", confirmedDocument.RootElement.GetProperty("status").GetString())
        Assert.Equal("CONF-K08", confirmedDocument.RootElement.GetProperty("confirmationCode").GetString())
        Assert.Equal("cancelled", cancelledDocument.RootElement.GetProperty("status").GetString())
        Assert.Equal("duplicate request", cancelledDocument.RootElement.GetProperty("cancellationReason").GetString())

    [<Fact>]
    let ``every protected booking status round trips through version one dto`` () =
        let pending = createPending "REQ-PENDING"
        let confirmed = createPending "REQ-CONFIRMED" |> confirm
        let cancelled = createPending "REQ-CANCELLED" |> cancel

        for booking in [ pending; confirmed; cancelled ] do
            let restored =
                booking |> BookingMapping.ofDomain |> BookingMapping.toDomain |> expectOk

            Assert.Equal(booking, restored)

    [<Fact>]
    let ``snapshot mapping reports common representation failures explicitly`` () =
        let missingId =
            { validPendingDto () with
                RequestId = null }

        let blankEventId =
            { validPendingDto () with
                EventId = " " }

        let missingSeats =
            { validPendingDto () with
                Seats = Nullable() }

        let zeroSeats =
            { validPendingDto () with
                Seats = Nullable 0 }

        Assert.Equal(Error DtoMappingError.MissingBody, BookingMapping.toDomain null)
        Assert.Equal(Error DtoMappingError.MissingRequestId, BookingMapping.toDomain missingId)

        Assert.Equal(Error(DtoMappingError.InvalidEventId BlankEventId), BookingMapping.toDomain blankEventId)

        Assert.Equal(Error DtoMappingError.MissingSeats, BookingMapping.toDomain missingSeats)

        Assert.Equal(Error(DtoMappingError.InvalidSeatCount(NonPositiveSeatCount 0)), BookingMapping.toDomain zeroSeats)

    [<Fact>]
    let ``snapshot status tag and payload must describe one possible union case`` () =
        let pendingWithCode =
            { validPendingDto () with
                ConfirmationCode = "CONF-K08" }

        let confirmedWithoutCode =
            { validPendingDto () with
                Status = "confirmed" }

        let cancelledWithoutReason =
            { validPendingDto () with
                Status = "cancelled" }

        let cancelledWithOldCode =
            { validPendingDto () with
                Status = "cancelled"
                ConfirmationCode = "CONF-K08"
                CancellationReason = "duplicate request" }

        let unknownStatus =
            { validPendingDto () with
                Status = "Confirmed" }

        Assert.Equal(
            Error(DtoMappingError.UnexpectedConfirmationCode "pending"),
            BookingMapping.toDomain pendingWithCode
        )

        Assert.Equal(Error DtoMappingError.MissingConfirmationCode, BookingMapping.toDomain confirmedWithoutCode)
        Assert.Equal(Error DtoMappingError.MissingCancellationReason, BookingMapping.toDomain cancelledWithoutReason)

        Assert.Equal(
            Error(DtoMappingError.UnexpectedConfirmationCode "cancelled"),
            BookingMapping.toDomain cancelledWithOldCode
        )

        Assert.Equal(Error(DtoMappingError.UnknownStatus "Confirmed"), BookingMapping.toDomain unknownStatus)

    [<Fact>]
    let ``unsupported snapshot version wins before payload interpretation`` () =
        let incompatible =
            { validPendingDto () with
                SchemaVersion = 2
                RequestId = null
                Status = "future-status" }

        Assert.Equal(Error(DtoMappingError.UnsupportedSchemaVersion 2), BookingMapping.toDomain incompatible)

    [<Fact>]
    let ``command dtos round trip raw intent without duplicating domain validation`` () =
        let placeDto: PlaceBookingDto = { RequestId = " "; Seats = Nullable 0 }

        let confirmDto: ConfirmBookingDto =
            { RequestId = "REQ-K08"
              ConfirmationCode = " " }

        let cancelDto: CancelBookingDto = { RequestId = "REQ-K08"; Reason = " " }

        let place = PlaceBookingMapping.toDomain placeDto |> expectOk
        let confirmation = ConfirmBookingMapping.toDomain confirmDto |> expectOk
        let cancellation = CancelBookingMapping.toDomain cancelDto |> expectOk

        Assert.Equal(" ", place.RequestId)
        Assert.Equal(0, place.Seats)
        Assert.Equal(" ", confirmation.ConfirmationCode)
        Assert.Equal(" ", cancellation.Reason)
        Assert.Equal(placeDto, PlaceBookingMapping.ofDomain place)
        Assert.Equal(confirmDto, ConfirmBookingMapping.ofDomain confirmation)
        Assert.Equal(cancelDto, CancelBookingMapping.ofDomain cancellation)

    [<Fact>]
    let ``command dto mapping distinguishes missing transport data`` () =
        let missingRequestId: PlaceBookingDto = { RequestId = null; Seats = Nullable 2 }

        let missingSeats: PlaceBookingDto =
            { RequestId = "REQ-K08"
              Seats = Nullable() }

        let missingCode: ConfirmBookingDto =
            { RequestId = "REQ-K08"
              ConfirmationCode = null }

        let missingReason: CancelBookingDto = { RequestId = "REQ-K08"; Reason = null }

        Assert.Equal(Error DtoMappingError.MissingBody, PlaceBookingMapping.toDomain null)
        Assert.Equal(Error DtoMappingError.MissingRequestId, PlaceBookingMapping.toDomain missingRequestId)
        Assert.Equal(Error DtoMappingError.MissingSeats, PlaceBookingMapping.toDomain missingSeats)
        Assert.Equal(Error DtoMappingError.MissingConfirmationCode, ConfirmBookingMapping.toDomain missingCode)
        Assert.Equal(Error DtoMappingError.MissingCancellationReason, CancelBookingMapping.toDomain missingReason)

    [<Fact>]
    let ``command json crosses dto mapping before domain validation`` () =
        let place =
            BookingJson.deserializePlaceBooking """{"requestId":" REQ-K08 ","seats":0}"""
            |> PlaceBookingMapping.toDomain
            |> expectOk

        let confirmation =
            BookingJson.deserializeConfirmBooking """{"requestId":"REQ-K08","confirmationCode":" "}"""
            |> ConfirmBookingMapping.toDomain
            |> expectOk

        let cancellation =
            BookingJson.deserializeCancelBooking """{"requestId":"REQ-K08","reason":"duplicate request"}"""
            |> CancelBookingMapping.toDomain
            |> expectOk

        let missingSeats =
            BookingJson.deserializePlaceBooking """{"requestId":"REQ-K08"}"""
            |> PlaceBookingMapping.toDomain

        Assert.Equal(" REQ-K08 ", place.RequestId)
        Assert.Equal(0, place.Seats)
        Assert.Equal(" ", confirmation.ConfirmationCode)
        Assert.Equal("duplicate request", cancellation.Reason)
        Assert.Equal(Error DtoMappingError.MissingSeats, missingSeats)

    [<Fact>]
    let ``json member names are exact and unknown members fail closed`` () =
        let unknownMember =
            """{"schemaVersion":1,"requestId":"REQ-K08","eventId":"EVT-K08","seats":3,"status":"pending","priority":true}"""

        let wrongCase =
            """{"schemaVersion":1,"RequestId":"REQ-K08","eventId":"EVT-K08","seats":3,"status":"pending"}"""

        Assert.Throws<JsonException>(fun () -> BookingJson.deserializeBooking unknownMember |> ignore)
        |> ignore

        Assert.Throws<JsonException>(fun () -> BookingJson.deserializeBooking wrongCase |> ignore)
        |> ignore

        let nullBody = BookingJson.deserializeBooking "null"
        Assert.Equal(Error DtoMappingError.MissingBody, BookingMapping.toDomain nullBody)
