namespace Booking.Api

open System
open System.Collections.Generic
open System.Diagnostics
open System.Diagnostics.Metrics
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging

// #region booking-diagnostic-names
[<RequireQualifiedAccess>]
module BookingDiagnosticNames =
    [<Literal>]
    let ActivitySourceName = "ThinkingInFSharp.Booking.Api"

    [<Literal>]
    let MeterName = "ThinkingInFSharp.Booking.Api"

    [<Literal>]
    let RequestActivityName = "booking.http.request"

    [<Literal>]
    let RequestCounterName = "booking.http.requests"

    [<Literal>]
    let RequestDurationName = "booking.http.duration"
// #endregion booking-diagnostic-names

// #region booking-request-diagnostics
[<Sealed>]
type BookingRequestDiagnostics(meterFactory: IMeterFactory, logger: ILogger<BookingRequestDiagnostics>) =
    let meter = meterFactory.Create(BookingDiagnosticNames.MeterName, "1.0.0")
    let activities = new ActivitySource(BookingDiagnosticNames.ActivitySourceName, "1.0.0")

    let requestCounter =
        meter.CreateCounter<int64>(
            BookingDiagnosticNames.RequestCounterName,
            "{request}",
            "Completed booking HTTP requests"
        )

    let requestDuration =
        meter.CreateHistogram<double>(
            BookingDiagnosticNames.RequestDurationName,
            "ms",
            "Booking HTTP request duration"
        )

    let requestCompleted =
        LoggerMessage.Define<string, string, string, int, string, double>(
            LogLevel.Information,
            EventId(1000, "BookingRequestCompleted"),
            "Booking request completed correlationId={CorrelationId} method={Method} endpoint={Endpoint} statusCode={StatusCode} outcome={Outcome} elapsedMs={ElapsedMilliseconds}"
        )

    let classify statusCode =
        if statusCode >= 200 && statusCode < 400 then
            "success"
        elif statusCode >= 400 && statusCode < 500 then
            "client_error"
        else
            "server_error"

    let correlationId () =
        match Activity.Current with
        | null -> ActivityTraceId.CreateRandom().ToString()
        | current -> current.TraceId.ToString()

    let setTag (activity: Activity | null) name value =
        match activity with
        | null -> ()
        | current -> current.SetTag(name, value) |> ignore

    member _.InvokeAsync(context: HttpContext, next: RequestDelegate) : Task =
        task {
            let stopwatch = Stopwatch.StartNew()
            let activity = activities.StartActivity(BookingDiagnosticNames.RequestActivityName, ActivityKind.Internal)
            let correlation = correlationId ()
            let scopeValues = Dictionary<string, obj>()
            scopeValues.Add("CorrelationId", correlation)
            use _scope = logger.BeginScope scopeValues

            context.Response.Headers["X-Correlation-ID"] <- correlation
            setTag activity "booking.correlation_id" correlation
            setTag activity "http.request.method" context.Request.Method

            let mutable statusCode = StatusCodes.Status500InternalServerError
            let mutable outcome = "server_error"

            try
                try
                    do! next.Invoke context
                    statusCode <- context.Response.StatusCode
                    outcome <- classify statusCode
                with
                | :? OperationCanceledException as error when context.RequestAborted.IsCancellationRequested ->
                    statusCode <- 0
                    outcome <- "canceled"
                    return raise error
                | error ->
                    statusCode <-
                        if context.Response.HasStarted then
                            context.Response.StatusCode
                        else
                            StatusCodes.Status500InternalServerError

                    outcome <- "server_error"
                    return raise error
            finally
                stopwatch.Stop()

                let endpoint =
                    match context.GetEndpoint() with
                    | null -> "unmatched"
                    | value ->
                        match value.DisplayName with
                        | null -> "unmatched"
                        | displayName -> displayName

                setTag activity "http.route" endpoint
                setTag activity "http.response.status_code" statusCode
                setTag activity "booking.outcome" outcome

                match activity with
                | null -> ()
                | current ->
                    if outcome = "server_error" || outcome = "canceled" then
                        current.SetStatus(ActivityStatusCode.Error, outcome) |> ignore
                    else
                        current.SetStatus(ActivityStatusCode.Ok) |> ignore

                let outcomeTag = KeyValuePair<string, obj | null>("outcome", box outcome)
                requestCounter.Add(1L, outcomeTag)
                requestDuration.Record(stopwatch.Elapsed.TotalMilliseconds, outcomeTag)

                requestCompleted.Invoke(
                    logger,
                    correlation,
                    context.Request.Method,
                    endpoint,
                    statusCode,
                    outcome,
                    stopwatch.Elapsed.TotalMilliseconds,
                    null
                )

                match activity with
                | null -> ()
                | current -> current.Dispose()
        }

    interface IDisposable with
        member _.Dispose() = activities.Dispose()

[<Sealed>]
type BookingDiagnosticsMiddleware(next: RequestDelegate, diagnostics: BookingRequestDiagnostics) =
    member _.InvokeAsync(context: HttpContext) = diagnostics.InvokeAsync(context, next)
// #endregion booking-request-diagnostics

// #region booking-diagnostics-registration
[<RequireQualifiedAccess>]
module BookingDiagnostics =
    let add (services: IServiceCollection) =
        ArgumentNullException.ThrowIfNull(services, nameof services)
        services.AddMetrics() |> ignore
        services.AddSingleton<BookingRequestDiagnostics>() |> ignore

    let useMiddleware (application: WebApplication) =
        ArgumentNullException.ThrowIfNull(application, nameof application)
        application.UseMiddleware<BookingDiagnosticsMiddleware>() |> ignore
// #endregion booking-diagnostics-registration
