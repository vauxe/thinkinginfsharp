namespace ThinkingInFSharp.Ecosystem.Cloud

open System
open System.Text.Json.Serialization
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http

[<CLIMutable>]
type HealthResponse =
    { [<JsonPropertyName("status")>]
      Status: string }

[<CLIMutable>]
type RuntimeResponse =
    { [<JsonPropertyName("service")>]
      Service: string
      [<JsonPropertyName("deploymentMode")>]
      DeploymentMode: string }

[<RequireQualifiedAccess>]
module CloudService =
    let private writeJson (context: HttpContext) (value: 'value) : Task =
        context.Response.WriteAsJsonAsync<'value>(value, context.RequestAborted)

    let private live context =
        writeJson context { Status = "healthy" }

    let private ready context =
        // This sample has no required external dependency. A real readiness probe
        // must test only dependencies that should stop this instance receiving traffic.
        writeJson context { Status = "ready" }

    let private runtime context =
        let deploymentMode =
            match Environment.GetEnvironmentVariable "DEPLOYMENT_MODE" with
            | null -> "standalone"
            | value when String.IsNullOrWhiteSpace value -> "standalone"
            | value -> value

        writeJson
            context
            { Service = "cloud-service"
              DeploymentMode = deploymentMode }

    let map (application: WebApplication) =
        ArgumentNullException.ThrowIfNull(application, nameof application)

        application.MapGet("/health/live", RequestDelegate live) |> ignore
        application.MapGet("/health/ready", RequestDelegate ready) |> ignore
        application.MapGet("/api/runtime", RequestDelegate runtime) |> ignore

module Program =
    [<EntryPoint>]
    let main arguments =
        let builder = WebApplication.CreateBuilder arguments
        use application = builder.Build()
        CloudService.map application
        application.Run()
        0
