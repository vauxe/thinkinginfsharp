namespace ThinkingInFSharp.ContractTests

open System
open System.Diagnostics
open System.Diagnostics.Metrics
open System.IO
open System.Net
open System.Net.Http
open System.Text
open System.Text.Json
open System.Threading
open System.Threading.Tasks
open Booking.Api
open Booking.Domain
open Booking.Domain.Ports
open Booking.Infrastructure
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.TestHost
open Xunit

module BookingEndToEndTests =
    type private Counter() =
        let mutable value = 0

        member _.Increment() = Interlocked.Increment(&value) |> ignore
        member _.Value = Volatile.Read(&value)

    type private TemporaryDirectory() =
        let directoryPath =
            Path.Combine(Path.GetTempPath(), "thinking-in-fsharp", Guid.NewGuid().ToString("N"))

        do Directory.CreateDirectory(directoryPath) |> ignore

        member _.Path = directoryPath

        interface IDisposable with
            member _.Dispose() =
                if Directory.Exists directoryPath then
                    Directory.Delete(directoryPath, true)

    let private complete (operation: Task<'value>) = operation.GetAwaiter().GetResult()
    let private completeUnit (operation: Task) = operation.GetAwaiter().GetResult()

    let private expectOk result =
        match result with
        | Ok value -> value
        | Error error -> failwithf "Expected Ok, received Error %A" error

    let private activity =
        let eventId = EventId.create "EVT-E2E" |> expectOk
        let capacity = Capacity.create 4 |> expectOk
        Event.create eventId capacity

    type private TestApi
        (snapshotPath: string, chargeBehavior: PaymentRequest -> CancellationToken -> Task<PaymentOutcome>) =
        let paymentCalls = Counter()
        let notificationCalls = Counter()
        let configuration = BookingStoreConfiguration.create snapshotPath |> expectOk
        let store = AtomicBookingStore configuration

        let charge (request: PaymentRequest) (cancellationToken: CancellationToken) =
            cancellationToken.ThrowIfCancellationRequested()
            paymentCalls.Increment()
            chargeBehavior request cancellationToken

        let notify (_: NotificationRequest) (cancellationToken: CancellationToken) =
            cancellationToken.ThrowIfCancellationRequested()
            notificationCalls.Increment()
            Task.FromResult()

        let service = IdempotentBookingService(activity, store, charge, notify)
        let builder = WebApplication.CreateBuilder([||])

        do
            builder.WebHost.UseTestServer() |> ignore
            BookingDiagnostics.add builder.Services

        let application = builder.Build()

        do
            BookingDiagnostics.useMiddleware application

            BookingEndpoints.mapConsistent
                application
                { Execute = fun command token -> service.Execute(command, token)
                  Load = fun requestId token -> service.Load(requestId, token) }

            application.StartAsync() |> completeUnit

        let client = application.GetTestClient()

        member _.Client = client
        member _.PaymentCalls = paymentCalls.Value
        member _.NotificationCalls = notificationCalls.Value

        interface IDisposable with
            member _.Dispose() =
                client.Dispose()
                application.DisposeAsync().AsTask() |> completeUnit

    let private sendJson (client: HttpClient) (path: string) (json: string) =
        use request = new HttpRequestMessage(HttpMethod.Post, path)
        request.Content <- new StringContent(json, Encoding.UTF8, "application/json")
        client.SendAsync request |> complete

    let private readText (response: HttpResponseMessage) =
        response.Content.ReadAsStringAsync() |> complete

    let private readError response =
        let json = readText response
        let parsed: ApiErrorDto | null = JsonSerializer.Deserialize<ApiErrorDto>(json)

        match parsed with
        | null -> failwithf "Expected an API error DTO, received %s" json
        | value -> value

    let private authorize (_: PaymentRequest) (_: CancellationToken) =
        Task.FromResult(PaymentOutcome.Authorized "TX-E2E-INTERNAL")

    [<Fact>]
    let ``completed command replays while changed payload conflicts without repeating effects`` () =
        use temporary = new TemporaryDirectory()
        use api = new TestApi(Path.Combine(temporary.Path, "bookings.json"), authorize)

        use created =
            sendJson api.Client "/api/bookings/place" """{"requestId":" REQ-E2E ","seats":2}"""

        let createdBody = readText created
        Assert.Equal(HttpStatusCode.Created, created.StatusCode)
        Assert.Equal("/api/bookings/REQ-E2E", string created.Headers.Location)

        Assert.Equal(
            """{"schemaVersion":1,"requestId":"REQ-E2E","eventId":"EVT-E2E","seats":2,"status":"pending"}""",
            createdBody
        )

        Assert.DoesNotContain("TX-E2E-INTERNAL", createdBody)

        use replayed =
            sendJson api.Client "/api/bookings/place" """{"requestId":"REQ-E2E","seats":2}"""

        Assert.Equal(HttpStatusCode.Created, replayed.StatusCode)
        Assert.Equal(createdBody, readText replayed)
        Assert.Equal("/api/bookings/REQ-E2E", string replayed.Headers.Location)

        use conflict =
            sendJson api.Client "/api/bookings/place" """{"requestId":"REQ-E2E","seats":1}"""

        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode)
        Assert.Equal("idempotency_conflict", (readError conflict).Code)
        Assert.Equal(1, api.PaymentCalls)
        Assert.Equal(1, api.NotificationCalls)

        use loaded = api.Client.GetAsync("/api/bookings/REQ-E2E") |> complete
        Assert.Equal(HttpStatusCode.OK, loaded.StatusCode)
        Assert.Equal(createdBody, readText loaded)

    [<Fact>]
    let ``invalid json is rejected before storage or external effects`` () =
        use temporary = new TemporaryDirectory()
        let snapshotPath = Path.Combine(temporary.Path, "bookings.json")
        use api = new TestApi(snapshotPath, authorize)

        use response = sendJson api.Client "/api/bookings/place" "{not-json"

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode)
        Assert.Equal("invalid_json", (readError response).Code)
        Assert.Equal(0, api.PaymentCalls)
        Assert.Equal(0, api.NotificationCalls)
        Assert.False(File.Exists snapshotPath)

    [<Fact>]
    let ``ambiguous payment is not charged again through http`` () =
        use temporary = new TemporaryDirectory()

        let failPayment (_: PaymentRequest) (_: CancellationToken) =
            Task.FromException<PaymentOutcome>(
                DependencyUnavailableException(
                    "Payment dependency is unavailable.",
                    InvalidOperationException "controlled payment fault"
                )
            )

        use api = new TestApi(Path.Combine(temporary.Path, "bookings.json"), failPayment)

        let body = """{"requestId":"REQ-E2E-PAYMENT","seats":2}"""
        use first = sendJson api.Client "/api/bookings/place" body

        Assert.Equal(HttpStatusCode.ServiceUnavailable, first.StatusCode)
        Assert.Equal("dependency_unavailable", (readError first).Code)
        Assert.DoesNotContain("controlled payment fault", readText first)

        use retry = sendJson api.Client "/api/bookings/place" body

        Assert.Equal(HttpStatusCode.Conflict, retry.StatusCode)
        Assert.Equal("payment_outcome_unknown", (readError retry).Code)
        Assert.Equal(1, api.PaymentCalls)
        Assert.Equal(0, api.NotificationCalls)

    [<Fact>]
    let ``request diagnostics emit bounded metrics and one correlated child activity`` () =
        let measurements = ResizeArray<string * string>()
        let completedActivities = ResizeArray<Activity>()

        use meterListener = new MeterListener()

        meterListener.InstrumentPublished <-
            fun instrument listener ->
                if instrument.Meter.Name = BookingDiagnosticNames.MeterName then
                    listener.EnableMeasurementEvents instrument

        meterListener.SetMeasurementEventCallback<int64>(fun instrument _ tags _ ->
            let outcome =
                tags.ToArray()
                |> Array.tryPick (fun tag -> if tag.Key = "outcome" then Some(string tag.Value) else None)
                |> Option.defaultValue "missing"

            measurements.Add(instrument.Name, outcome))

        meterListener.SetMeasurementEventCallback<double>(fun instrument _ tags _ ->
            let outcome =
                tags.ToArray()
                |> Array.tryPick (fun tag -> if tag.Key = "outcome" then Some(string tag.Value) else None)
                |> Option.defaultValue "missing"

            measurements.Add(instrument.Name, outcome))

        meterListener.Start()

        use activityListener = new ActivityListener()
        activityListener.ShouldListenTo <- fun source -> source.Name = BookingDiagnosticNames.ActivitySourceName
        activityListener.Sample <- fun _ -> ActivitySamplingResult.AllDataAndRecorded
        activityListener.ActivityStopped <- Action<Activity>(completedActivities.Add)
        ActivitySource.AddActivityListener activityListener

        use temporary = new TemporaryDirectory()
        use api = new TestApi(Path.Combine(temporary.Path, "bookings.json"), authorize)
        use response = sendJson api.Client "/api/bookings/place" "{not-json"

        let correlation = response.Headers.GetValues("X-Correlation-ID") |> Seq.exactlyOne
        Assert.Matches("^[0-9a-f]{32}$", correlation)

        Assert.Contains((BookingDiagnosticNames.RequestCounterName, "client_error"), measurements)

        Assert.Contains((BookingDiagnosticNames.RequestDurationName, "client_error"), measurements)

        let activity = Assert.Single completedActivities
        Assert.Equal(BookingDiagnosticNames.RequestActivityName, activity.DisplayName)
        Assert.Equal(correlation, string (activity.GetTagItem "booking.correlation_id"))
        Assert.Equal("HTTP: POST /api/bookings/place", string (activity.GetTagItem "http.route"))
        Assert.Equal("client_error", string (activity.GetTagItem "booking.outcome"))
