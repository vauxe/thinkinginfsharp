namespace ThinkingInFSharp.ContractTests

open System
open System.Net
open System.Net.Http
open System.Text
open System.Text.Json
open System.Threading
open System.Threading.Tasks
open Booking.Api
open Booking.Contracts
open Booking.Domain
open Booking.Domain.Ports
open Booking.Domain.Workflow
open Booking.Infrastructure
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.TestHost
open Xunit

module BookingApiTests =
    let private complete (operation: Task<'value>) = operation.GetAwaiter().GetResult()
    let private completeUnit (operation: Task) = operation.GetAwaiter().GetResult()

    let private expectOk result =
        match result with
        | Ok value -> value
        | Error error -> failwithf "Expected Ok, received Error %A" error

    let private activity =
        let eventId = EventId.create "EVT-K10" |> expectOk
        let capacity = Capacity.create 4 |> expectOk
        Event.create eventId capacity

    let private pendingBooking requestId seats =
        let validRequestId = RequestId.create requestId |> expectOk
        let validSeats = SeatCount.create seats |> expectOk
        Booking.create activity validRequestId validSeats |> expectOk

    type private RecordedPorts =
        { Ports: AsyncPorts
          Calls: ResizeArray<string>
          Tokens: ResizeArray<CancellationToken>
          State: unit -> BookingState }

    let private recordingPorts initialState =
        let calls = ResizeArray<string>()
        let tokens = ResizeArray<CancellationToken>()
        let mutable state = initialState

        let record name cancellationToken =
            calls.Add name
            tokens.Add cancellationToken

        let ports =
            { LoadBooking =
                fun _ cancellationToken ->
                    record "load" cancellationToken
                    Task.FromResult state
              AppendEvent =
                fun _ bookingEvent cancellationToken ->
                    record "append" cancellationToken
                    state <- Workflow.evolve state bookingEvent
                    Task.FromResult()
              Charge =
                fun _ cancellationToken ->
                    record "charge" cancellationToken
                    Task.FromResult(PaymentOutcome.Authorized "TX-INTERNAL-SECRET")
              Notify =
                fun _ cancellationToken ->
                    record "notify" cancellationToken
                    Task.FromResult()
              GetUtcNow =
                fun cancellationToken ->
                    record "clock" cancellationToken
                    Task.FromResult DateTimeOffset.UnixEpoch }

        { Ports = ports
          Calls = calls
          Tokens = tokens
          State = fun () -> state }

    type private TestApi(ports: AsyncPorts) =
        let builder = WebApplication.CreateBuilder([||])

        do builder.WebHost.UseTestServer() |> ignore

        let application = builder.Build()

        do BookingEndpoints.map application { Activity = activity; Ports = ports }
        do application.StartAsync() |> completeUnit

        let client = application.GetTestClient()

        member _.Client = client

        interface IDisposable with
            member _.Dispose() =
                client.Dispose()
                application.DisposeAsync().AsTask() |> completeUnit

    let private sendJson (client: HttpClient) (method: HttpMethod) (path: string) (contentType: string) (json: string) =
        use request = new HttpRequestMessage(method, path)
        request.Content <- new StringContent(json, Encoding.UTF8, contentType)
        client.SendAsync request |> complete

    let private readText (response: HttpResponseMessage) =
        response.Content.ReadAsStringAsync() |> complete

    let private readError response =
        let json = readText response
        let parsed: ApiErrorDto | null = JsonSerializer.Deserialize<ApiErrorDto>(json)

        match parsed with
        | null -> failwithf "Expected an API error DTO, received %s" json
        | value -> value

    [<Fact>]
    let ``place uses boundary dtos and propagates one request token through every effect`` () =
        let recorded = recordingPorts NotBooked
        use api = new TestApi(recorded.Ports)

        use response =
            sendJson
                api.Client
                HttpMethod.Post
                "/api/bookings/place"
                "application/json"
                """{"requestId":" REQ-HTTP ","seats":2}"""

        Assert.Equal(HttpStatusCode.Created, response.StatusCode)
        Assert.Equal("/api/bookings/REQ-HTTP", string response.Headers.Location)

        Assert.Equal(
            """{"schemaVersion":1,"requestId":"REQ-HTTP","eventId":"EVT-K10","seats":2,"status":"pending"}""",
            readText response
        )

        Assert.DoesNotContain("TX-INTERNAL-SECRET", readText response)
        Assert.Equal<string>([| "load"; "charge"; "append"; "notify" |], recorded.Calls)
        Assert.Equal(4, recorded.Tokens.Count)
        Assert.All(recorded.Tokens, fun token -> Assert.Equal(recorded.Tokens[0], token))

        use loaded = api.Client.GetAsync("/api/bookings/REQ-HTTP") |> complete
        Assert.Equal(HttpStatusCode.OK, loaded.StatusCode)
        Assert.Equal(readText response, readText loaded)

    [<Fact>]
    let ``confirm and cancel expose lifecycle results without charging again`` () =
        let recorded = recordingPorts (Booked(pendingBooking "REQ-LIFECYCLE" 2))
        use api = new TestApi(recorded.Ports)

        use confirmed =
            sendJson
                api.Client
                HttpMethod.Post
                "/api/bookings/confirm"
                "application/json"
                """{"requestId":"REQ-LIFECYCLE","confirmationCode":"CONF-10"}"""

        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode)
        Assert.Contains("\"status\":\"confirmed\"", readText confirmed)
        Assert.Contains("\"confirmationCode\":\"CONF-10\"", readText confirmed)

        use cancelled =
            sendJson
                api.Client
                HttpMethod.Post
                "/api/bookings/cancel"
                "application/json"
                """{"requestId":"REQ-LIFECYCLE","reason":"customer request"}"""

        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode)
        Assert.Contains("\"status\":\"cancelled\"", readText cancelled)
        Assert.Contains("\"cancellationReason\":\"customer request\"", readText cancelled)

        Assert.Equal<string>([| "load"; "append"; "notify"; "load"; "append"; "notify" |], recorded.Calls)

    [<Theory>]
    [<InlineData("{not-json", "application/json", 400, "invalid_json")>]
    [<InlineData("{\"requestId\":\"REQ-STRICT\",\"Seats\":2}", "application/json", 400, "invalid_json")>]
    [<InlineData("{\"requestId\":\"REQ-STRICT\",\"seats\":2,\"extra\":true}", "application/json", 400, "invalid_json")>]
    [<InlineData("{\"requestId\":\"REQ-STRICT\"}", "application/json", 400, "invalid_request")>]
    [<InlineData("null", "application/json", 400, "invalid_request")>]
    [<InlineData("{\"requestId\":\"REQ-TEXT\",\"seats\":2}", "text/plain", 415, "unsupported_media_type")>]
    let ``invalid transport input has a stable safe error`` json contentType expectedStatus expectedCode =
        let recorded = recordingPorts NotBooked
        use api = new TestApi(recorded.Ports)

        use response =
            sendJson api.Client HttpMethod.Post "/api/bookings/place" contentType json

        let error = readError response

        Assert.Equal(enum<HttpStatusCode> expectedStatus, response.StatusCode)
        Assert.Equal(expectedCode, error.Code)
        Assert.Empty recorded.Calls

    [<Fact>]
    let ``domain validation accumulates safe field errors before effects`` () =
        let recorded = recordingPorts NotBooked
        use api = new TestApi(recorded.Ports)

        use response =
            sendJson
                api.Client
                HttpMethod.Post
                "/api/bookings/place"
                "application/json"
                """{"requestId":"   ","seats":0}"""

        let error = readError response
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode)
        Assert.Equal("validation_failed", error.Code)

        Assert.Equal<ApiFieldErrorDto>(
            [| { Field = "requestId"; Code = "blank" }
               { Field = "seats"
                 Code = "non_positive" } |],
            error.Errors
        )

        Assert.Empty recorded.Calls

    [<Fact>]
    let ``request bodies are bounded even before json parsing`` () =
        let recorded = recordingPorts NotBooked
        use api = new TestApi(recorded.Ports)
        let oversized = String('x', BookingEndpoints.MaxRequestBodyBytes + 1)

        use response =
            sendJson api.Client HttpMethod.Post "/api/bookings/place" "application/json" oversized

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode)
        Assert.Equal("request_too_large", (readError response).Code)
        Assert.Empty recorded.Calls

    [<Fact>]
    let ``domain refusals map to conflict or not found without side effects`` () =
        let duplicatePorts = recordingPorts (Booked(pendingBooking "REQ-DUPLICATE" 1))
        use duplicateApi = new TestApi(duplicatePorts.Ports)

        use duplicate =
            sendJson
                duplicateApi.Client
                HttpMethod.Post
                "/api/bookings/place"
                "application/json"
                """{"requestId":"REQ-DUPLICATE","seats":1}"""

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode)
        Assert.Equal("booking_already_exists", (readError duplicate).Code)
        Assert.Equal<string>([| "load" |], duplicatePorts.Calls)

        let missingPorts = recordingPorts NotBooked
        use missingApi = new TestApi(missingPorts.Ports)

        use missing =
            sendJson
                missingApi.Client
                HttpMethod.Post
                "/api/bookings/confirm"
                "application/json"
                """{"requestId":"REQ-MISSING","confirmationCode":"CONF-10"}"""

        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode)
        Assert.Equal("booking_not_found", (readError missing).Code)
        Assert.Equal<string>([| "load" |], missingPorts.Calls)

        let capacityPorts = recordingPorts NotBooked
        use capacityApi = new TestApi(capacityPorts.Ports)

        use capacity =
            sendJson
                capacityApi.Client
                HttpMethod.Post
                "/api/bookings/place"
                "application/json"
                """{"requestId":"REQ-CAPACITY","seats":5}"""

        Assert.Equal(HttpStatusCode.Conflict, capacity.StatusCode)
        Assert.Equal("capacity_exceeded", (readError capacity).Code)
        Assert.Equal<string>([| "load" |], capacityPorts.Calls)

    [<Fact>]
    let ``payment refusal is expected and does not expose provider details`` () =
        let recorded = recordingPorts NotBooked

        let ports =
            { recorded.Ports with
                Charge =
                    fun _ cancellationToken ->
                        recorded.Calls.Add "charge"
                        recorded.Tokens.Add cancellationToken
                        Task.FromResult(PaymentOutcome.Declined "CARD-DETAIL-SECRET") }

        use api = new TestApi(ports)

        use response =
            sendJson
                api.Client
                HttpMethod.Post
                "/api/bookings/place"
                "application/json"
                """{"requestId":"REQ-DECLINED","seats":2}"""

        let responseText = readText response
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode)
        Assert.Equal("payment_declined", (readError response).Code)
        Assert.DoesNotContain("CARD-DETAIL-SECRET", responseText)
        Assert.Equal<string>([| "load"; "charge" |], recorded.Calls)
        Assert.Equal(NotBooked, recorded.State())

    [<Fact>]
    let ``dependency failures are safe and reveal the post-commit notification window`` () =
        let paymentFailure = recordingPorts NotBooked

        let paymentPorts =
            { paymentFailure.Ports with
                Charge =
                    fun _ cancellationToken ->
                        paymentFailure.Calls.Add "charge"
                        paymentFailure.Tokens.Add cancellationToken
                        Task.FromException<PaymentOutcome>(InvalidOperationException "PAYMENT-SECRET") }

        use paymentApi = new TestApi(paymentPorts)

        use beforeCommit =
            sendJson
                paymentApi.Client
                HttpMethod.Post
                "/api/bookings/place"
                "application/json"
                """{"requestId":"REQ-PAYMENT-FAULT","seats":2}"""

        Assert.Equal(HttpStatusCode.ServiceUnavailable, beforeCommit.StatusCode)
        Assert.DoesNotContain("PAYMENT-SECRET", readText beforeCommit)
        Assert.Equal(NotBooked, paymentFailure.State())

        let notificationFailure = recordingPorts NotBooked

        let notificationPorts =
            { notificationFailure.Ports with
                Notify =
                    fun _ cancellationToken ->
                        notificationFailure.Calls.Add "notify"
                        notificationFailure.Tokens.Add cancellationToken
                        Task.FromException<unit>(InvalidOperationException "NOTIFICATION-SECRET") }

        use notificationApi = new TestApi(notificationPorts)

        use afterCommit =
            sendJson
                notificationApi.Client
                HttpMethod.Post
                "/api/bookings/place"
                "application/json"
                """{"requestId":"REQ-NOTIFICATION-FAULT","seats":2}"""

        Assert.Equal(HttpStatusCode.ServiceUnavailable, afterCommit.StatusCode)
        Assert.Equal("dependency_unavailable", (readError afterCommit).Code)
        Assert.DoesNotContain("NOTIFICATION-SECRET", readText afterCommit)

        match notificationFailure.State() with
        | Booked booking -> Assert.Equal("REQ-NOTIFICATION-FAULT", booking |> Booking.requestId |> RequestId.value)
        | NotBooked -> failwith "The booking must already be committed before notification."

    [<Fact>]
    let ``storage and unexpected faults never expose exception or configuration details`` () =
        let recorded = recordingPorts NotBooked

        let storeFailure =
            { recorded.Ports with
                LoadBooking =
                    fun _ _ ->
                        Task.FromException<BookingState>(
                            BookingStoreAdapterException BookingStoreError.CannotReadSnapshot
                        ) }

        use storeApi = new TestApi(storeFailure)
        use unavailable = storeApi.Client.GetAsync("/api/bookings/REQ-STORE") |> complete
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode)
        Assert.Equal("storage_unavailable", (readError unavailable).Code)

        let unexpectedFailure =
            { recorded.Ports with
                LoadBooking = fun _ _ -> Task.FromException<BookingState>(Exception "PATH-OR-SECRET") }

        use unexpectedApi = new TestApi(unexpectedFailure)

        use unexpected =
            unexpectedApi.Client.GetAsync("/api/bookings/REQ-FAULT") |> complete

        let responseText = readText unexpected
        Assert.Equal(HttpStatusCode.InternalServerError, unexpected.StatusCode)
        Assert.Equal("internal_error", (readError unexpected).Code)
        Assert.DoesNotContain("PATH-OR-SECRET", responseText)

    [<Fact>]
    let ``client cancellation reaches the blocked port and remains cancellation`` () =
        let recorded = recordingPorts NotBooked

        let entered =
            TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)

        let mutable observedToken = CancellationToken.None

        let blockingPorts =
            { recorded.Ports with
                LoadBooking =
                    fun _ cancellationToken ->
                        observedToken <- cancellationToken
                        entered.TrySetResult() |> ignore

                        task {
                            do! Task.Delay(Timeout.Infinite, cancellationToken)
                            return NotBooked
                        } }

        use api = new TestApi(blockingPorts)
        use owner = new CancellationTokenSource()
        let response = api.Client.GetAsync("/api/bookings/REQ-CANCEL", owner.Token)
        entered.Task |> completeUnit
        owner.Cancel()

        Assert.ThrowsAny<OperationCanceledException>(fun () -> response |> complete |> ignore)
        |> ignore

        Assert.True(observedToken.CanBeCanceled)
        Assert.True(observedToken.IsCancellationRequested)
