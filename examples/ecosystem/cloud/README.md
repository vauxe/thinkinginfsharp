# F# cloud and Aspire local slice

This sample keeps application code in a small F# ASP.NET Core service and puts local orchestration in a C# project-based Aspire AppHost. The language boundary is deliberate: Aspire can orchestrate an F# project, while the current project templates and generated `Projects` API make C# the low-friction infrastructure host.

The AppHost SDK is pinned to 13.5.2 and targets .NET 10. It needs no cloud account, database, container engine, or globally installed Aspire CLI. `AspireUseCliBundle` with `DnxPinned` resolves the matching `Aspire.Cli@13.5.2` on first build or run, so the committed AppHost package lock remains independent of the host operating system. That first use needs a configured NuGet source or an existing cache.

## Run the service directly

From the repository root:

```console
dotnet run \
  --project examples/ecosystem/cloud/CloudService.fsproj \
  --configuration Release \
  -- \
  --urls http://127.0.0.1:5092
```

In another terminal:

```console
curl --fail http://127.0.0.1:5092/health/live
curl --fail http://127.0.0.1:5092/health/ready
curl --fail http://127.0.0.1:5092/api/runtime
```

The last response reports `"deploymentMode":"standalone"`. The two health endpoints are separate even though this dependency-free sample can answer both immediately. Production readiness must reflect only required dependencies whose failure should remove an instance from traffic; liveness must not turn a transient downstream failure into a restart loop.

## Run through Aspire

If a trusted Aspire development certificate is configured, an ordinary `dotnet run` is sufficient. The following POSIX-shell command is the exact certificate-free local path verified for this sample:

```console
env \
  ASPIRE_ALLOW_UNSECURED_TRANSPORT=true \
  ASPIRE_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true \
  ASPIRE_VERSION_CHECK_DISABLED=true \
  ASPNETCORE_URLS=http://127.0.0.1:5192 \
  ASPIRE_RESOURCE_SERVICE_ENDPOINT_URL=http://127.0.0.1:5193 \
  ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL=http://127.0.0.1:5194 \
  dotnet run \
  --project examples/ecosystem/cloud/AppHost/AppHost.csproj
```

All three fixed endpoints bind only to `127.0.0.1`. The anonymous, unencrypted dashboard is suitable only for this controlled local check: other local processes can read its telemetry and submit data. Do not change those addresses to a LAN or public interface. Prefer trusted HTTPS development certificates for normal team use, and use authenticated encrypted endpoints outside a developer machine.

The AppHost starts the F# project on a dynamically allocated service port, injects `DEPLOYMENT_MODE=aspire-local`, registers `/health/ready` as its HTTP health check, and exposes local resource state at `http://127.0.0.1:5192`. The verified dashboard showed one `cloud-service` resource as `Running`, a `Healthy` health state, and one healthy `/health/ready` check. The service's `/api/runtime` response reported `"deploymentMode":"aspire-local"`.

The AppHost is a development orchestrator and application-model declaration. Building or running it does not prove a production deployment, secret store, managed identity, autoscaling policy, network policy, data durability, disaster recovery, cost model, or cloud-provider integration.

## Publish a local container archive

.NET SDK 8.0.200 and later include container publishing for Web projects, so this sample does not add a container-build package or Dockerfile. The project pins the ASP.NET Core base image tag to `10.0.11`. To generate a Linux image archive without pushing to a registry:

```console
dotnet publish examples/ecosystem/cloud/CloudService.fsproj \
  --configuration Release \
  /t:PublishContainer \
  -p:ContainerArchiveOutputPath=/tmp/thinking-in-fsharp-cloud-service.tar.gz
```

On the verified arm64 host, the archive described a `linux/arm64` image running as non-root UID 1654, exposing port 8080, and entering through `dotnet /app/CloudService.dll`. The checked command intentionally leaves the project runtime identifier unchanged, so it does not rewrite the repository's ordinary `net10.0` lock graph. When the deployment architecture differs, declare and lock that RID in an isolated release pipeline instead of silently changing the development lock file.

Inspect or scan the archive before loading or promoting it. Creating an archive proves packaging only; it does not prove that an image starts under a target platform's identity, filesystem, CPU architecture, memory limit, ingress, probes, or shutdown policy. Docker CLI was present during this check, but its daemon was not running, so no container execution is claimed.

## What this slice intentionally omits

- No cloud SDK or Serverless package is needed to demonstrate process and orchestration boundaries.
- No database or message broker is added merely to make the topology look distributed.
- No secret values are returned; `/api/runtime` exposes one controlled teaching value only.
- Authentication, authorization, rate limiting, TLS, telemetry export, deployment manifests, registry signing, SBOM attestation, vulnerability policy, rollout, and rollback need environment-specific verification.

Current reference points: [Aspire SDK](https://aspire.dev/get-started/aspire-sdk/), [project-based AppHost setup](https://aspire.dev/get-started/add-aspire-existing-app/), and [.NET SDK container publishing](https://learn.microsoft.com/dotnet/core/containers/overview).
