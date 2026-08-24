namespace ThinkingInFSharp.Ecosystem.Web

open System
open System.Text.Json
open System.Text.Json.Serialization
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http

// #region web-sample-contract
[<CLIMutable>]
type GreetingRequestDto =
    { [<JsonPropertyName("name")>]
      Name: string | null }

[<CLIMutable>]
type GreetingResponseDto =
    { [<JsonPropertyName("message")>]
      Message: string }

[<CLIMutable>]
type WebSampleErrorDto =
    { [<JsonPropertyName("code")>]
      Code: string
      [<JsonPropertyName("message")>]
      Message: string }
// #endregion web-sample-contract

[<RequireQualifiedAccess>]
module WebSample =
    let private jsonOptions =
        let options = JsonSerializerOptions(JsonSerializerDefaults.Web)
        options.PropertyNameCaseInsensitive <- false
        options.UnmappedMemberHandling <- JsonUnmappedMemberHandling.Disallow
        options

    let private writeJson (context: HttpContext) statusCode value =
        task {
            context.Response.StatusCode <- statusCode
            context.Response.ContentType <- "application/json; charset=utf-8"

            do! JsonSerializer.SerializeAsync(context.Response.Body, value, jsonOptions, context.RequestAborted)
        }

    let private writeError context statusCode code message =
        writeJson context statusCode { Code = code; Message = message }

    // #region web-sample-handler
    let private greet (context: HttpContext) : Task =
        task {
            if not (context.Request.HasJsonContentType()) then
                return!
                    writeError
                        context
                        StatusCodes.Status415UnsupportedMediaType
                        "unsupported_media_type"
                        "Content-Type must be a JSON media type."
            else
                try
                    let! request =
                        JsonSerializer.DeserializeAsync<GreetingRequestDto>(
                            context.Request.Body,
                            jsonOptions,
                            context.RequestAborted
                        )

                    match request with
                    | null ->
                        return! writeError context StatusCodes.Status400BadRequest "name_required" "Name is required."
                    | value ->
                        match value.Name with
                        | null ->
                            return!
                                writeError context StatusCodes.Status400BadRequest "name_required" "Name is required."
                        | name when String.IsNullOrWhiteSpace name ->
                            return!
                                writeError context StatusCodes.Status400BadRequest "name_required" "Name is required."
                        | name ->
                            return! writeJson context StatusCodes.Status200OK { Message = $"Hello, {name.Trim()}!" }
                with
                | :? JsonException ->
                    return!
                        writeError
                            context
                            StatusCodes.Status400BadRequest
                            "invalid_json"
                            "The request body is not valid for this endpoint."
                | :? OperationCanceledException as error when context.RequestAborted.IsCancellationRequested ->
                    return raise error
                | _ when context.Response.HasStarted -> context.Abort()
                | _ ->
                    return!
                        writeError
                            context
                            StatusCodes.Status500InternalServerError
                            "internal_error"
                            "The request could not be completed."
        }
    // #endregion web-sample-handler

    // #region web-sample-map
    let map (application: WebApplication) =
        ArgumentNullException.ThrowIfNull(application, nameof application)

        application.MapPost("/api/greetings", RequestDelegate greet) |> ignore
// #endregion web-sample-map

module Program =
    // #region web-sample-host
    [<EntryPoint>]
    let main arguments =
        let builder = WebApplication.CreateBuilder arguments
        use application = builder.Build()
        WebSample.map application
        application.Run()
        0
// #endregion web-sample-host
