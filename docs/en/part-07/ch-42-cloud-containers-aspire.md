---
title: "Chapter 42: Cloud, Containers, Serverless, and .NET Aspire"
description: "Choose a deployment model from process, state, scaling, operational, security, and evidence requirements while keeping F# application boundaries explicit."
translationKey: part-07/ch-42-cloud-containers-aspire
---

# Chapter 42: Cloud, Containers, Serverless, and .NET Aspire {#overview}

An F# service runs as a .NET process in the cloud. Its domain types, functions, ports, cancellation, configuration, and wire contracts continue to define the application after deployment. Cloud products decide who supplies machines, networking, scaling, identity, and operations around those boundaries.

Start by naming the deployment problem, then choose the product and layer. A container packages a process. A compute platform such as Kubernetes runs it. Serverless changes the execution and billing contract. Aspire describes an application model and can drive local orchestration or target-specific deployment work; a separate target runtime hosts production.

Start with the process and artifact, then move outward. That order keeps the F# design visible and separates in-page design, locally verifiable facts, and evidence still required from the target platform.

This chapter uses vocabulary from three layers: records, discriminated unions, functions, and modules belong to F#; `WebApplication`, `HttpContext`, and container publishing belong to .NET/ASP.NET Core; Serverless, Kubernetes, probes, AppHost, SBOMs, and progressive rollout belong to cloud platforms or operations. The latter two groups are not F# syntax; they are boundaries that an F# application calls or encounters.

::: tip Two reading passes
For a first pass, follow the [deployment stack](#deployment-contracts), [decision map](#compute-decision-map), and [in-page project template](#verified-slice). Return to the [release plan](#release-observe-rollback), [evidence ladder](#evidence-ladder), and [adoption spike](#adoption-spike) when preparing a real deployment.
:::

## Deployment is a stack of contracts {#deployment-contracts}

Treat a release as several related but non-interchangeable objects:

```text
F# source + locked dependencies
  -> compiled/published application artifact
  -> optional container image for one OS/architecture
  -> platform configuration and external resources
  -> running instances, routes, identity, and data
  -> observable release with rollout and rollback state
```

A successful `dotnet build` proves compilation for the selected target framework. `dotnet publish` proves a publish layout. A generated image archive proves that container packaging completed. A local process answering a health URL proves one runtime path. None alone proves registry integrity, target architecture, production identity, network policy, durable storage, load behavior, managed-service compatibility, rollout safety, or rollback.

### Ask six questions before naming a platform {#six-questions}

1. **Trigger:** Is work driven by HTTP, a queue, a schedule, a stream, or a long-lived connection?
2. **Lifetime:** Is it a continuous process, a bounded invocation, a batch, or a durable workflow spanning waits?
3. **State:** What must survive restart, duplication, scale-out, region loss, and deployment?
4. **Scale:** What concurrency, latency, burst, warm-capacity, and geographic behavior is required?
5. **Control:** Which runtime, network, filesystem, accelerator, sidecar, or policy details must the team own?
6. **Operations:** Who patches, monitors, responds, controls cost, approves changes, and rehearses recovery?

The answer may be one small managed Web service. Distributed topology is not a maturity badge. Every new process boundary replaces a function call with serialization, networking, partial failure, authentication, versioning, telemetry, and operational ownership.

## A proportional compute decision map {#compute-decision-map}

| First candidate | Strong fit | Friction that must be justified |
| --- | --- | --- |
| Managed application/process platform | One ordinary Web or worker service; team wants provider-managed hosts, routing, patching, and basic scaling | Platform build contract, supported .NET versions, restricted host controls, provider configuration and diagnostics |
| Managed container platform | A portable image is already a release contract; moderate scaling and networking needs; no cluster API requirement | Registry, image lifecycle, ingress, identity, volumes, probes, scaling limits, cold capacity |
| Kubernetes | Many workloads need common cluster scheduling, custom controllers, network policy, sidecars, or a platform team already operates it | Cluster upgrades, policy, capacity, ingress, certificates, storage, tenancy, observability, incident load |
| Serverless function/event runtime | Bursty or sparse bounded work maps naturally to provider triggers and can tolerate the invocation contract | Binding/runtime compatibility, cold start, duration and payload limits, retries, concurrency, ephemeral state, local fidelity, provider coupling |
| VM or directly managed host | Legacy/native dependency, unusual OS control, stable load, or migration constraint justifies managing the host directly | Patching, process supervision, capacity, failover, certificates, deployment, telemetry, backup, hardening |

These are starting points, not a ranking. A managed container product can scale to zero; a Serverless product may accept container images; Kubernetes may be managed; a VM can run containers. Decide from the responsibilities the team must retain, not the marketing category.

## In-page project template: an F# service and Aspire AppHost {#verified-slice}

The current repository no longer contains the former `examples/ecosystem/cloud` project. This section preserves a reconstructable template: one F# HTTP service, one C# project-based AppHost, and no cloud account or external service. It explains the boundary between application code, development orchestration, and container packaging; it is not a currently executable repository project and proves no provider deployment.

Use these relative locations when reconstructing it; this layout makes the AppHost project reference and the generated type used below valid:

```text
CloudTemplate/
  CloudService.fsproj
  Program.fs
  AppHost/
    AppHost.csproj
    Program.cs
```

`CloudService.fsproj` compiles only the root `Program.fs`. `AppHost.csproj` references the F# project one level above, allowing the Aspire SDK to generate `Projects.CloudService` for the C# code. After the first restore, generate and keep a dependency lock for each project, then verify with locked mode.

### The F# service and pinned image base {#fsharp-service}

```xml:line-numbers [CloudService.fsproj]
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ContainerRepository>thinking-in-fsharp-cloud-service</ContainerRepository>
    <ContainerBaseImage>mcr.microsoft.com/dotnet/aspnet:10.0.10</ContainerBaseImage>
  </PropertyGroup>

  <ItemGroup>
    <Compile Include="Program.fs" />
  </ItemGroup>
</Project>
```
The Web SDK targets `net10.0`. There is no explicit `FSharp.Core` package reference here; its default version comes from the selected SDK, so inspect the resolved lock rather than guessing a version from this text. The image base is explicitly `mcr.microsoft.com/dotnet/aspnet:10.0.10`; check its support status before adoption and preferably pin a digest as well. A floating `10.0` tag can move the runtime beneath an unchanged commit, and both OS and architecture assumptions require target-environment testing.

```fsharp:line-numbers [Program.fs]
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
```
`CloudService.map` owns three narrow HTTP endpoints. `/health/live` says the process can respond. `/health/ready` says this dependency-free sample can accept traffic. `/api/runtime` exposes one controlled teaching value and defaults to `standalone`; it does not dump environment variables.

The two probe paths are separate even though their current implementations are both immediate. That preserves the contract when a real readiness condition later appears. A database outage may justify removing an instance from traffic, but making the same transient outage fail liveness can restart every replica and amplify the incident.

### A C# infrastructure shell around an F# project {#csharp-apphost}

```xml:line-numbers [AppHost.csproj]
<Project Sdk="Aspire.AppHost.Sdk/13.5.2">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <AspireUseCliBundle>true</AspireUseCliBundle>
    <AspireCliInvocationMode>DnxPinned</AspireCliInvocationMode>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="../CloudService.fsproj" />
  </ItemGroup>
</Project>
```
The AppHost SDK is pinned to 13.5.2 and targets .NET 10. Its project reference points to the F# service. Aspire generates the `Projects.CloudService` metadata type from that reference; the service does not become C# and does not reference the AppHost.

The project opts into the Aspire CLI bundle and selects `DnxPinned`. When this version is reconstructed and restored, the SDK should resolve the matching `Aspire.Cli@13.5.2` instead of requiring a global CLI install; first use still needs the package source or a populated cache. This is an optional in-page ecosystem template, not a dependency of the book site.

```csharp:line-numbers [AppHost Program.cs]
using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

builder
    .AddProject<Projects.CloudService>("cloud-service")
    .WithHttpEndpoint(name: "http")
    .WithEnvironment("DEPLOYMENT_MODE", "aspire-local")
    .WithHttpHealthCheck("/health/ready");

builder.Build().Run();
```
The AppHost names the resource, explicitly declares an `http` endpoint, injects `DEPLOYMENT_MODE=aspire-local`, and attaches an HTTP health check. The explicit endpoint matters: this template has no `launchSettings.json` supplying endpoint metadata; without `.WithHttpEndpoint`, the health check has no eligible `http` or `https` endpoint to reference.

This is a good inter-language boundary. F# implements the application and its typed behavior. C# contains a small infrastructure DSL whose current templates, generated metadata, and examples are C#-first. No domain type crosses the boundary, so replacing the orchestration tool does not rewrite the service.

### Evidence required after reconstruction {#executed-evidence}

Readable code is not evidence that the template has run. After reconstruction, complete at least these checks:

- generate locks with the selected SDK, then restore in locked mode and build Release;
- start the F# service directly and confirm the three endpoints return `healthy`, `ready`, and `standalone` respectively;
- start through AppHost, confirm the resource health check completes, and confirm `/api/runtime` returns `aspire-local`;
- publish a container archive for the target OS/architecture and inspect its base, digest, non-root user, port, entry point, and architecture;
- compare the ordinary `net10.0` lock before and after publishing so container arguments cannot silently change the graph;
- actually start the image in a constrained container and verify probes, cancellation, shutdown, and resource limits.

Even if every local check passes, it does not prove registry push, signing, SBOM attestation, vulnerability decisions, cloud identity, external dependencies, load, production telemetry, deployment, or rollback. This chapter did not perform those operations.

### Local HTTP was an explicit test exception {#local-http-exception}

If a development machine lacks a trusted Aspire development certificate, a controlled experiment can explicitly enable unsecured transport while binding the Dashboard, resource service, and OTLP endpoints strictly to `127.0.0.1`. Record the environment variables, ports, and cleanup steps in the local run instructions; trusted development HTTPS remains the better team default.

That mode is not production guidance. Other local processes can read or submit dashboard telemetry. Never bind an anonymous unsecured dashboard or OTLP receiver to a LAN or public address. Normal team use should establish trusted development HTTPS; remote and production endpoints require authentication, encryption, network policy, and secret handling appropriate to the environment.

## Design the process before the container {#process-contract}

A container cannot repair an application that assumes one machine, immortal memory, local secrets, or graceful completion without interruption. Make the process contract explicit first.

### Configuration, secrets, and identity {#configuration-secrets-identity}

Configuration names and validation belong in source; environment-specific values do not. Validate required values at startup, distinguish secret from non-secret configuration, and fail safely before receiving traffic. Avoid logging raw configuration objects or returning process environment as diagnostics.

Environment variables are a transport, not a secret manager. A platform identity with narrow permissions is usually preferable to long-lived credentials copied into files or variables. Define who can read each secret, how rotation works, whether an instance can refresh it, and what rollback does when old and new credentials overlap.

Do not bake environment names, connection strings, certificates, tokens, or provider account IDs into the image. Build once and inject values at deployment. If configuration changes application behavior materially, version and test the configuration contract alongside code.

### State, storage, and filesystem {#state-storage-filesystem}

Assume an instance can disappear between any two effects. Memory is a cache unless the business can tolerate losing it. A writable container layer is normally ephemeral and instance-local; two replicas do not share it, and replacement discards it.

Durable truth belongs in an explicit data service, object store, queue, or attached volume whose consistency, concurrency, backup, restore, encryption, retention, region, and migration semantics are understood. “The platform persists it” is not a data contract.

Keep caches reconstructible. Give temporary files bounded size and cleanup ownership. Test a read-only root filesystem when the target supports it. Never rely on a local clock, hostname, instance count, or sequential request arrival for a domain invariant.

### Startup, shutdown, and cancellation {#startup-shutdown-cancellation}

ASP.NET Core's Generic Host responds to process shutdown signals and attempts graceful stopping. Application code must propagate request cancellation, stop accepting new work when draining begins, bound cleanup, and avoid claiming completion before durable effects commit.

Shutdown is a best effort, not a transaction. A crash, kill, node loss, or timeout may bypass cleanup. Durable work therefore needs idempotency, leases, checkpoints, or transactional outbox/inbox boundaries rather than faith in `finally` or `StopAsync`.

Measure startup and shutdown under the real image and platform. Probe delays, termination grace, load-balancer drain, long requests, background work, and storage visibility interact. A locally graceful Ctrl+C is only the first rung of evidence.

## Health is a control signal, not a status page {#health-signals}

Different consumers need different answers:

| Signal | Question | Typical reaction |
| --- | --- | --- |
| Startup | Has initialization completed enough to begin other probes? | Keep waiting or terminate a failed startup |
| Liveness | Is this process irrecoverably stuck? | Restart this instance |
| Readiness | Should new traffic reach this instance now? | Remove or add the instance to routing |
| Dependency/resource readiness | May a dependent resource start or proceed? | Hold orchestration ordering |
| Synthetic/user journey | Can an important operation succeed through real boundaries? | Alert, halt rollout, or investigate |
| Business health | Are domain outcomes, queues, latency, errors, and cost within objectives? | Operational or product response |

Kubernetes explicitly gives startup, liveness, and readiness probes different effects. Aspire also distinguishes AppHost resource checks from service endpoint checks. The in-page template connects the AppHost check to the service's readiness URL, but that declaration does not configure a production load balancer.

Keep liveness cheap and independent of transient downstream systems. Readiness may include required dependencies, but an unbounded dependency chain can make every instance unready during a shared outage. Set timeouts, avoid leaking internals, control caching, restrict exposure, and test both failure and recovery.

## An image is a release input {#image-contract}

.NET SDK 8.0.200 and later can publish supported projects as containers without a separate container-build package. That removes one Dockerfile for simple cases; it does not remove image design.

Record and verify:

- SDK, target framework, dependency lock, publish mode, and container tooling version;
- base image repository, exact patch or digest, OS distribution, architecture, and support lifetime;
- non-root user, writable paths, capabilities, environment, exposed ports, entry point, and working directory;
- globalization, certificates, time-zone data, native libraries, diagnostics, and memory/CPU behavior;
- labels, license inventory, SBOM, provenance, signature, vulnerability policy, and exception ownership;
- registry destination, immutable digest, retention, replication, access, promotion, and rollback.

Tags are convenient names; a digest identifies image content. Promote the same digest through test, staging, and production. Rebuilding “the same commit” later can select a changed base image, package feed, clock, or tool and produce a different artifact.

Multi-architecture indexes do not prove identical behavior. Native libraries, globalization, JIT behavior, available images, and performance can differ between `amd64` and `arm64`. Build and smoke each deployed architecture, then test under the target's security context and resource limits.

### Keep publish commands from silently changing locks {#lock-file-lesson}

Some cross-OS or cross-architecture publish arguments trigger a RID-specific restore and can change the ordinary project lock graph. Do not assume that a command “only packages” because it looks that way; compare the lock before and after publishing, then repeat the ordinary `--locked-mode` restore.

When a self-contained or cross-architecture release genuinely needs a RID-specific graph, declare, lock, and test it as a separate release contract. If only the container target must select a Linux image platform, choose a command that does not accidentally rewrite the development graph and prove that with a clean-worktree check.

## Serverless is an invocation contract {#serverless-contract}

“Serverless” means the provider manages more of the execution fleet and exposes a higher-level invocation, scaling, and billing model. Servers still exist, and application responsibilities remain.

Choose a function or event runtime when work is naturally bounded, sparse or bursty, provider triggers remove meaningful infrastructure, scale-to-zero is acceptable, and the team can live within the runtime's limits. A continuously busy low-latency API, long-lived socket, heavy native process, stable high-throughput worker, or workflow with complex in-memory coordination may fit a service or container better.

### Put a thin handler around a pure decision {#thin-handler}

Use this flow:

```text
provider event/binding
  -> validate and map public input
  -> pure F# decision or workflow
  -> explicit ports for durable effects
  -> provider response, acknowledgement, retry, or dead-letter outcome
```

Keep provider attributes, trigger types, contexts, and SDK clients at the edge. The core should accept ordinary records and unions and return declared decisions. That makes the business behavior runnable without an emulator and keeps migration between a function and a worker possible.

Bindings save code only when their failure semantics are understood. Ask which component handles serialization, batching, checkpointing, acknowledgement, retries, poison messages, partial batch success, concurrency, cancellation, and telemetry. If an output binding hides the error you need to classify, a direct client SDK behind a port may be safer.

### Assume retries and duplicate delivery {#retries-duplicates}

Provider behavior varies by trigger and invocation mode, but duplicate delivery is a normal possibility. AWS documents at-least-once event source mappings; Azure's event and retry guidance likewise requires considering duplicate processing. “The function ran once” is not a domain guarantee.

Give each command or event a stable identity. Validate that the same identity has the same semantic payload. Make the effect and deduplication record atomic when possible. Record success only after the authoritative effect commits. Define retention, conflict, replay, partial-batch, retry exhaustion, and dead-letter behavior.

A payment timeout is not proof of failure. It may be an unknown outcome requiring lookup or reconciliation under the same idempotency key. Retrying with a new identity can duplicate the charge regardless of how quickly the function scales.

### Cold start, concurrency, and limits are design inputs {#cold-start-concurrency-limits}

Measure package size, initialization, dependency connection setup, first invocation, warm invocation, scale-out, and tail latency under the chosen plan and region. Do not promise a cold-start number copied from another language, package, plan, or date.

Concurrency can occur within an instance, across instances, or both. Static mutable values and local caches are shared according to the provider worker model, not per logical event. Bound downstream connections and request rates; automatic compute scaling can overwhelm a database or paid API faster than it helps users.

Every provider defines supported .NET versions, CPU architectures, duration, memory, temporary storage, payload, networking, concurrency, retry, and deployment-package limits. Treat that matrix as a dated dependency and test the exact trigger and hosting plan.

### F# on provider .NET workers {#fsharp-provider-workers}

A provider advertising “.NET” does not prove first-class F# templates, analyzers, generated bindings, local tools, Native AOT behavior, or documentation. F# can consume ordinary .NET libraries, but code generation and tooling APIs may favor one language.

As of 2026-08-31, Azure Functions 4.x isolated worker documentation lists .NET 10 and notes that F# applications may need explicit registration for some binding extensions. It also records plan-specific restrictions and minimum worker package versions. This chapter reviewed that documentation but did not build or deploy an Azure Function.

AWS currently documents a .NET 10 Lambda base image and .NET packaging paths, mostly using C# terminology and examples. A compiled F# handler can be a candidate only after an F# project spike verifies handler discovery, serializer behavior, packages, architecture, local invocation, cold path, deployment, and telemetry. The in-page template implements no Lambda handler and performs none of those steps.

Do not choose Serverless to avoid learning deployment. It adds a provider runtime, trigger contract, identity, limits, pricing dimensions, local emulator/tooling, and event failure semantics. Choose it when those additions remove more owned infrastructure than they create application risk.

## Aspire models topology; it does not erase it {#aspire-model}

An Aspire AppHost is code that declares resources and relationships. In run mode, the Developer Control Plane starts and monitors local processes or containers, assigns endpoints, injects configuration, and feeds the dashboard. Official architecture guidance explicitly says the AppHost is not a production runtime.

### Resources, references, and ordering {#resources-references-ordering}

`AddProject`, `AddContainer`, and hosting integrations add resources to an application model. `WithReference` expresses a relationship and can inject connection or endpoint information. `WaitFor` controls startup readiness. These operations solve different problems: a reference is not automatically a wait, and either one is not a production authorization policy.

Aspire integrations are packages that teach the AppHost how to represent and connect resources. Adding a database integration may start a local container, connect to an existing service, or participate in deployment. It does not decide who governs the schema, where transactions end, or how backup, capacity, failover, data classification, and deletion work.

Name resources as stable operational concepts. Treat generated connection data as configuration at the service boundary. Keep domain code unaware of Aspire resource types, so tests and alternative hosts can construct the same ports directly.

### Two health systems {#two-health-systems}

AppHost resource health answers whether orchestration considers a resource ready, including whether a dependent `WaitFor` may proceed. Service endpoint health answers whether a running application instance should receive traffic or restart under its production platform.

The Aspire dashboard can display an HTTP resource check declared by AppHost; verify that wiring when reconstructing the template. Production still needs platform probe configuration aimed at the right service path, port, timeout, threshold, and security boundary. Copying a green dashboard screenshot into a runbook does not create that configuration.

### Service Defaults are source code, not magic {#service-defaults}

The current C# Service Defaults template composes OpenTelemetry, health checks, service discovery, and standard `HttpClient` resilience. It is a customizable shared project. Calling `AddServiceDefaults` and `MapDefaultEndpoints` is what installs those behaviors; merely running under AppHost does not instrument an application.

The in-page template intentionally omits Service Defaults. Even if AppHost injects OTLP-related environment variables, the F# service has no OpenTelemetry SDK/exporter packages and therefore does not gain traces or metrics automatically. Its health endpoints are explicit teaching handlers, not ASP.NET Core `IHealthCheck` registrations.

For a real F# solution, choose one of three honest paths:

1. reference a small C# Service Defaults adapter and call its public extensions from the F# composition root;
2. reproduce only the required registrations directly in F#, with locked packages and tests;
3. create a language-neutral shared library whose public API is deliberately F#- and C#-friendly.

Do not copy a template once and forget it. The team must define and maintain retry policy, timeouts, endpoint exposure, instrumentation sources, exporter behavior, sampling, package upgrades, and production-backend verification.

## Local orchestration and deployment are separate modes {#local-versus-deployment}

Current Aspire deployment is pipeline-based. A deployment target or compute environment contributes target-specific steps to the application model.

- `aspire publish` evaluates the AppHost and emits artifacts for a later tool or human to apply; it is a one-way handoff.
- `aspire deploy` evaluates the model, resolves parameters, generates target output, and applies it directly.
- `aspire do <step>` invokes named pipeline steps when CI/CD needs a split flow.

These commands require an appropriate CLI and target integrations. The in-page template declares only a pinned CLI bundle and development orchestration; it configures no deployment target and supplies no publish or deploy flow. Even a successful AppHost build is only local application-model evidence.

### Environment is not execution mode {#environment-execution-mode}

Aspire distinguishes an environment name such as Development or Production from execution context such as run or publish. Deployment commands default differently from development commands, and an Aspire environment does not automatically set `DOTNET_ENVIRONMENT` or another child runtime variable.

Pass child environment explicitly when behavior depends on it. Keep topology branches small and test each branch. A conditional resource that exists only in Production still needs validation; a branch that runs only during deployment is executable infrastructure code, not harmless configuration.

### CI/CD retains governance {#cicd-governance}

Aspire can define application-specific build, publish, push, and deploy steps. CI/CD still owns checkout, test gates, identities, approvals, artifact retention, environment protection, concurrency, audit, promotion, and emergency controls.

Prefer workload identity or another short-lived credential mechanism. Separate plan/publish evidence from apply authority. Require review for destructive data or network changes. Capture target output, parameters without secret values, image digests, tool versions, logs, and deployment result.

## Release, observe, and roll back {#release-observe-rollback}

A credible release record names:

- the immutable application/image digest and its provenance;
- database or message-schema compatibility and migration order;
- target environment, identity, configuration version, routes, and feature flags;
- startup, readiness, smoke, contract, security, and performance gates;
- telemetry queries, service-level indicators, cost signals, and alert ownership;
- rollout stages, pause criteria, abort threshold, and maximum observation window;
- rollback artifact and configuration, data forward-fix strategy, and responsible operator.

Rollback is not always “deploy the old image.” A destructive migration, emitted event, charged payment, sent notification, or incompatible cache entry may outlive code. Prefer expand-and-contract schemas, version-tolerant consumers, idempotent effects, and a tested forward fix where reversal is impossible.

Observability is also a boundary. Logs, metrics, and traces must carry safe correlation and release identity without secrets or personal data. Local dashboard visibility proves neither production export nor retention, sampling, backend ingestion, query correctness, alert delivery, or incident response.

Cost is an operational signal, not an afterthought. Record requests, duration, CPU, memory, egress, storage, managed-resource units, idle capacity, build minutes, log volume, and support labor. Serverless can be economical for sparse bursts and expensive for steady or chatty work; Kubernetes can reduce per-unit compute cost while increasing platform labor.

## Build an evidence ladder {#evidence-ladder}

Move outward only as risk requires:

1. pure decision/idempotency tests, then adapter tests for configuration, events, cancellation, and failure mapping;
2. locked Release build/publish plus image metadata, architecture, SBOM, signature, and vulnerability checks;
3. restricted container startup, probes, shutdown, and AppHost/emulator wiring;
4. target-platform staging with real identity, network, data, telemetry, scaling, and failure injection;
5. progressive production gates followed by rollback or forward-fix rehearsal with compatible data.

Emulators are not evidence for provider control planes, identities, quotas, networking, retries, or managed data; test those in a production-like environment. Keep pure domain tests out of expensive cloud fixtures.

## Date every compatibility claim {#version-evidence}

| Surface | Checked version or statement | What an adopting application must verify |
| --- | --- | --- |
| .NET SDK | local editing environment is 10.0.302; template targets `net10.0` | Select and pin an SDK, then perform locked restore, Release build, tests, and publish |
| FSharp.Core | not explicitly pinned by the template | Inspect the resolved lock version and runtime compatibility |
| Aspire.AppHost.Sdk | template pins 13.5.2; NuGet lists 13.5.3 on 2026-08-31 | Use only if local multi-service orchestration repays its cost; then test startup and health |
| ASP.NET Core base image | template pins tag 10.0.10 | Check support and digest, then verify metadata, operating system, architecture, vulnerabilities, and container startup |
| Aspire CLI bundle/deployment targets | template requests CLI 13.5.2; not executed | Bundle output and the chosen deployment target, if used |
| Azure Functions isolated worker | Docs list .NET 10 and F# binding caveats | Package, emulate, and deploy the actual trigger path |
| AWS Lambda .NET 10 image/runtime path | Current official docs reviewed | Package, invoke, and deploy the actual handler |
| Kubernetes probes/deployment | Current official semantics reviewed | Manifest, cluster behavior, and real probe execution |

The template remains on 13.5.2 so `AppHost.csproj` and its CLI bundle agree; that is not current execution evidence. Adopting NuGet's listed 13.5.3 requires updating the SDK and matching CLI together, then repeating restore through deployment. Versions record what was considered, not application support; keep provider plan, region, architecture, trigger, packages, CLI, base digest, and date with the evidence.

## Run a bounded adoption spike {#adoption-spike}

Choose one removable representative path covering:

- a real service/event contract and a durable effect with duplicate/unknown outcomes;
- identity and secrets without copied credentials, plus the actual deployment package;
- readiness, liveness, shutdown, dependency failure, and queried telemetry;
- measured capacity and cost reconciled with observed use;
- deployment, partial rollout, rollback or forward-fix, and cleanup.

Compare the smallest managed service, managed container, Serverless candidate, and Kubernetes only when plausible. Count code, infrastructure, permissions, pipelines, alerts, upgrades, incidents, and deletion work. Keep provider types outside the core, preserve a normal host path, and record the reversal condition.

## Exercises {#exercises}

### Exercise 1: choose a compute model for three workloads {#exercise-01}

Evaluate these workloads separately:

1. One team owns a steady internal HTTP API with a managed database, moderate traffic, no custom network or sidecar requirement, and no platform team.
2. Image metadata processing arrives in sharp bursts. Each item finishes within seconds, duplicate delivery is possible, and the downstream media API is rate-limited.
3. Twenty regulated services need common admission policy, private networking, sidecars, controlled multi-tenant scheduling, and an existing staffed Kubernetes platform.

For each workload, record the first candidate, rejected alternatives, evidence gap, and reversal condition. Compare managed processes or containers, Serverless, and Kubernetes; each workload may lead to a different choice.


::: details Answer

#### Case A: one steady internal HTTP API {#exercise-01-case-a}

Start with the organization's smallest supported managed application platform. If that path accepts a locked .NET publish artifact and supplies the required runtime, routing, identity, health, logging, and scaling controls, a container adds no immediate business capability.

Choose the managed container variant when the organization already promotes image digests, requires an image-based security gate, needs the same artifact on several compatible platforms, or finds the code-deployment build contract too opaque. The difference lies in artifact and operational responsibilities, not the F# domain model.

Reject Serverless as the first candidate because the API is steady, continuously available, and not naturally one sparse bounded event. A function layer would add trigger/runtime limits and a second hosting contract without a demonstrated scale-to-zero or event-integration benefit.

Reject Kubernetes because there is no cluster-level requirement and no team to operate the platform. One Deployment and Service YAML file would not cover upgrades, ingress, certificates, policy, capacity, storage, telemetry, tenancy, and incident response.

The first acceptance slice includes:

- locked Release build and one immutable publish artifact or image digest;
- startup validation for database endpoint, identity, and non-secret configuration;
- managed identity access to the database with no copied credential;
- separate liveness and readiness behavior, including database loss and recovery;
- request cancellation, graceful drain, resource limits, and a representative load test;
- production telemetry query, alert path, staged rollout, rollback, and cost observation.

Move from code deployment to a container if artifact drift, native dependency, platform portability, or supply-chain policy becomes material. Move away from the chosen managed product if a verified requirement exceeds its networking, scaling, identity, runtime, or operational limits. Do not move merely because a second service appears.

#### Case B: bursty image metadata events {#exercise-01-case-b}

Start with a Serverless event worker if the actual provider trigger can buffer bursts, each item stays within measured duration, memory, and package limits, and scale-to-zero materially reduces idle operating cost. The function is a thin adapter around a standalone F# decision and a rate-limited media-client port.

The queue event must carry a stable item/event ID and immutable object version. Persist one processing state per semantic identity. Concurrent duplicates either observe the existing result or contend through an atomic create/compare operation. Never use a fresh invocation ID as the idempotency key.

Bound concurrency below the media API's safe rate. Automatic compute scale is not permission to multiply downstream traffic. Use provider concurrency controls plus a client limiter; treat throttling as a declared transient outcome with jittered backoff. Define maximum event age, retry exhaustion, poison validation, partial-batch response, and dead-letter replay.

Reject Kubernetes first because no cluster feature is required. Reject a continuously warm service first only if the event runtime's cold path, limits, cost, and local/deployment tooling pass the spike.

Reverse to a managed worker/container when traffic becomes steady, cold/tail latency violates the objective, provider duration or package limits constrain processing, connection reuse dominates, or per-invocation cost exceeds reserved capacity. A queue remains useful across that migration because the F# core and durable event contract do not depend on the handler runtime.

The spike measures cold and warm latency, burst drain time, duplicate rate, downstream throttling, memory, package size, retry/dead-letter behavior, telemetry, and cost. It deploys the exact F# package to a staging provider environment; an emulator alone is insufficient.

#### Case C: twenty regulated services on an existing platform {#exercise-01-case-c}

Kubernetes is the first candidate because the case supplies both concrete cluster-level requirements and a staffed platform. Admission policy, private networking, approved sidecars, tenant scheduling, and common operational controls are already platform responsibilities rather than speculative features.

That does not authorize every Kubernetes feature. Each F# service should still be an ordinary independently testable process with explicit HTTP/event contracts, external state, cancellation, health, and a pinned non-root image. Domain projects do not reference Kubernetes APIs.

The platform contract should define:

- approved base images, registries, signatures, SBOM and vulnerability exceptions;
- namespace/tenant boundaries, service accounts, workload identity, secrets, and network policy;
- requests, limits, disruption budgets, autoscaling signals, quotas, and responsibility for capacity;
- startup, liveness, readiness, termination grace, draining, and rollout policy;
- ingress, certificates, service discovery, egress, data services, backup, and recovery;
- logs, metrics, traces, audit, alert routing, service objectives, and cost allocation;
- cluster and workload upgrade cadence, compatibility tests, and incident escalation.

Aspire may improve local topology and emit target artifacts when a current deployment integration is deliberately adopted. It does not replace platform policy, manifest review, cluster credentials, staged apply, or rollback verification.

Serverless can still win for one isolated event edge, and a managed service can still host an independent workload. The answer is not “all twenty must use Kubernetes.” The shared platform is the default only where its controls and clear responsibilities reduce total risk.

Reverse an individual workload when the platform adds more latency, cost, coupling, or incident load than its policy value. Reverse the platform decision only with migration evidence for the shared regulated controls, not with a cheaper compute quote alone.

:::

### Exercise 2: turn the in-page cloud template into a release proposal {#exercise-02}

Design the minimum work required to deploy the F# service from the in-page template to a managed container environment. Organize the proposal into four parts:

- **Artifact and supply chain:** architecture, immutable image identity, registry, SBOM, signing, and vulnerability policy.
- **Runtime contract:** configuration and secret identity, Service Defaults or alternative telemetry, production probes, non-root/read-only execution, resource limits, and shutdown.
- **Release path:** staging smoke, representative load, progressive rollout, rollback, and data compatibility.
- **Responsibility:** cost, cleanup, and the team responsible for each operational response.

Label each claim as coming from the in-page code design, a local reconstruction check, or evidence still required from the target environment.


::: details Answer

#### Establish a repeatable local baseline first {#exercise-02-baseline}

Before proposing a release, the reconstructed template must first verify:

- the F# service and C# AppHost restore from locks and build in a team-supported environment;
- the direct service and Aspire-orchestrated service answer the three endpoints;
- AppHost's local resource check becomes healthy;
- the SDK produces an image archive for the target architecture whose metadata can be inspected;
- the base digest, image user, port, architecture, and entry point meet a written contract;
- the ordinary package lock remains unchanged after the container command.

Those local checks do not verify the target platform, cloud identity, registry, production probes, telemetry export, load, security policy, rollout, or rollback. The release proposal starts at that boundary instead of relabeling local results.

#### Define one immutable artifact {#exercise-02-artifact}

Choose the managed environment's supported CPU architecture. Build that architecture in CI with the team's pinned .NET 10 SDK, locked dependencies, and pinned base digest; run the full test suite before packaging. Generate an image digest, SBOM, provenance record, license inventory, and vulnerability report.

Push once to a restricted registry. Sign or attest the digest through the organization's approved identity. Promotion records refer to that digest, never just `latest`, branch, or commit. Registry retention must preserve both the active and rollback digests.

The policy gate checks base support, non-root user, unexpected writable or privileged requirements, port and entry point, native libraries, architecture, secrets, severity exceptions, and signature. A reproducible rebuild is useful, but it does not replace promotion by digest.

#### Make runtime and security contracts explicit {#exercise-02-runtime}

Run the image locally or in CI under the target architecture, UID, read-only root filesystem, temporary writable mount, dropped capabilities, CPU/memory limits, and port 8080. Verify startup, all API responses, liveness, readiness, signal-driven drain, forced termination, and recovery after restart.

The platform service account receives only the permission required by the template. Because the template has no data dependency, no database or secret is invented. Add non-secret `DEPLOYMENT_MODE` through versioned configuration. If a future secret appears, use the platform secret/identity path and test rotation without printing its value.

Expose application traffic and probe paths only through intended platform routes. Protect or isolate operational endpoints. Use encrypted authenticated management and telemetry connections; the anonymous loopback dashboard exception is not copied into the deployment.

#### Add only service defaults the team can support {#exercise-02-observability}

Choose either a small C# Service Defaults adapter callable from F#, or explicit F# registrations for OpenTelemetry and ASP.NET Core health checks. Lock every new package. State the retry and timeout policy rather than accepting template behavior unseen.

Map a cheap liveness check and a readiness check whose dependencies are genuinely required. Configure the managed platform to call them with explicit interval, timeout, threshold, startup allowance, and termination/drain behavior. Test failure and recovery, not only 200 responses.

From staging, send safe logs, request, error, and latency metrics, traces, runtime metrics, the release digest, and environment identity to the real production telemetry backend. Query them. Trigger a test alert and confirm who responds. Define sampling, personal-data policy, retention, access, and expected ingestion cost.

#### Separate deployment generation from approval {#exercise-02-pipeline}

The simplest proposal may not need Aspire deployment at all: the CI system can deploy the one digest through the managed platform's supported declarative interface. Keep the AppHost for local orchestration.

If Aspire deployment is adopted, pin and install the CLI, add the selected target integration, generate target output with `aspire publish`, review it, and let a protected deployment stage apply it. Use `aspire deploy` only when that stage intentionally grants Aspire direct apply permission. In either case, CI/CD manages approvals, identities, environment protection, logs, and retention.

Use separate staging and production configuration, but the same digest. A staging gate performs socket/TLS, identity, health, telemetry, restart, resource-limit, and representative load tests. Capture the platform revision and target configuration with the digest.

#### Roll out and reverse safely {#exercise-02-rollout}

Release to a small traffic slice or revision. Gate expansion on error rate, tail latency, readiness churn, restarts, resource pressure, one synthetic request, and cost. Define numbers and observation windows before deployment.

Rollback routes traffic to the retained previous digest and compatible configuration. This template has no data migration, so reversal in the exercise is comparatively simple; the first persistent dependency must add schema compatibility and forward-fix analysis. Rehearse rollback from a deliberately unhealthy candidate.

After the observation window, retain the release records, remove failed revisions and unused temporary resources, and reconcile registry, telemetry, egress, and compute cost. Cleanup is part of the proposal because abandoned environments are both an expense and an attack surface.

:::

### Exercise 3: design an idempotent Serverless booking consumer {#exercise-03}

A provider event delivers `BookingConfirmed` at least once. The handler must reserve a notification identity, call an email provider, record the outcome, retry transient faults, and isolate poison input. It must also recover when the provider accepted an email but the handler crashed before recording success.

Show four parts in the design:

- **Core state:** F# types, persistent transitions, the atomic boundary, and concurrency control.
- **Provider boundary:** the email adapter and reconciliation of an unknown outcome.
- **Operations:** retry and dead-letter policy, telemetry, deployment, and rollback.
- **Verification:** tests for duplicate delivery, partial completion, poison input, and recovery.

Finish by naming the guarantee that requires cooperation from the email provider.


::: details Answer

#### Model facts, attempts, and uncertainty separately {#exercise-03-model}

Use a stable notification identity derived from the business event, channel, recipient, and template version—not from the provider invocation:

```fsharp
type NotificationId = private NotificationId of string
type PayloadHash = private PayloadHash of string

type DeliveryState =
    | Reserved of payloadHash: PayloadHash
    | Sending of payloadHash: PayloadHash * attempt: int * lease: string
    | Accepted of payloadHash: PayloadHash * providerMessageId: string
    | OutcomeUnknown of payloadHash: PayloadHash * attempt: int
    | Rejected of payloadHash: PayloadHash * safeReason: string
```

`Accepted` means the provider acknowledged the message. It does not prove human delivery or reading. `OutcomeUnknown` is not `Rejected`. The payload hash prevents the same identity from silently carrying different recipient or content semantics.

The pure core returns one of six decisions:

- `IgnoreAccepted`;
- `RejectConflict`;
- `AcquireAttempt`;
- `ReconcileUnknown`;
- `RetryTransient`;
- `RejectPermanent`.

Provider events, the clock, storage versions, and email responses are explicit inputs. Storage and email calls remain behind ports.

#### Persist before and after the external effect {#exercise-03-persistence}

On receipt:

1. validate schema, event type, booking identity, recipient policy, and payload size;
2. derive `NotificationId` and `PayloadHash`;
3. atomically create `Reserved`, or load the existing row;
4. return success immediately for identical `Accepted` state;
5. reject and alert when the identity exists with another payload hash;
6. atomically acquire a bounded lease/fencing token and move to `Sending`;
7. call the provider with the notification ID as its idempotency key when supported;
8. conditionally write `Accepted`, `Rejected`, or `OutcomeUnknown` using the lease/version;
9. acknowledge the source event only after the durable state permits it.

Atomic create or compare prevents two concurrent handlers from acquiring the same attempt. Lease expiry permits recovery after a crash; fencing prevents a late old worker from overwriting a newer result. Retention must cover the maximum source replay and business audit window.

There is still a dual-write gap between the email provider and local storage. If the provider accepts the message and the process dies before `Accepted` commits, local state is uncertain.

Exactly-once email cannot be manufactured by an F# type or local transaction. It requires provider cooperation: a stable idempotency key with durable deduplication, or a status lookup by that key/message ID. Without either, reconciliation can only choose between possible duplicate send and possible missed notification according to an explicit business policy.

#### Classify retries and poison input {#exercise-03-retries}

Malformed schema, unsupported version, invalid recipient, and identity/payload conflict are permanent or poison outcomes. Record safe diagnostics and route them to a dead-letter/quarantine path without endless retry.

Provider timeout, throttling, and selected 5xx responses are transient candidates, but only after checking the provider contract. Use bounded exponential backoff with jitter, maximum event age, attempt count, and concurrency. A timeout after sending becomes `OutcomeUnknown`, not an automatic new send.

Partial-batch runtimes report only failed items when supported, so one poison event does not replay successful siblings. Dead-letter replay is an audited command that preserves the original identity and payload, not a copy with a new key.

Bound function concurrency and provider calls together. Autoscaling cannot exceed provider quota or storage capacity. Emit queue age and throttling signals so delayed work is visible before retries expire.

#### Verify every layer from core to provider {#exercise-03-evidence}

Pure tests cover first event, duplicate accepted event, conflicting hash, concurrent lease decisions, transient retry, permanent rejection, stale worker completion, and unknown-outcome reconciliation.

Storage contract tests verify atomic creation, conditional update, lease expiry, fencing, and retention. Adapter tests cover the specified provider request, idempotency header, status and error mapping, cancellation, timeout after acceptance, and redacted diagnostics. Event fixtures cover missing, extra, null, oversized, old, and future-version input.

A target-provider staging test sends duplicate and concurrent events, kills a handler around the external call, observes retry/dead-letter behavior, exercises provider lookup/idempotency, and queries telemetry. Measure cold/warm latency, queue age, scale, downstream rate, and cost.

Deploy one immutable package with locked worker/binding versions, least-privilege identity, encrypted configuration, concurrency and retry policy, alarms, and a disabled or zero-concurrency emergency stop. Roll out by event-source partition, alias, version, or provider-supported traffic control.

Rollback must keep reading states written by the new version and must not reset notification identities. If a schema or state transition is not backward-compatible, pause consumption and use a forward-compatible repair rather than blindly activating old code.

The final guarantee is deliberately narrow: each semantic notification reaches a recorded terminal state, duplicates are suppressed when the provider contract permits, and uncertainty remains visible and reconcilable. Human delivery and exactly-once external delivery remain outside the consumer's unilateral control.

:::


## Sources {#sources}

- [Microsoft Learn: Aspire architecture and development-time orchestration](https://learn.microsoft.com/en-us/dotnet/aspire/architecture/overview)
- [Microsoft Learn: AppHost configuration](https://learn.microsoft.com/en-us/dotnet/aspire/app-host/configuration)
- [NuGet: Aspire.AppHost.Sdk versions](https://www.nuget.org/packages/Aspire.AppHost.Sdk)
- [Microsoft Learn: .NET container fundamentals](https://learn.microsoft.com/en-us/dotnet/core/containers/overview)
- [Microsoft Learn: .NET isolated worker guide for Azure Functions](https://learn.microsoft.com/en-us/azure/azure-functions/dotnet-isolated-process-guide)
- [Kubernetes: liveness, readiness, and startup probes](https://kubernetes.io/docs/concepts/configuration/liveness-readiness-startup-probes/)

Chapter 43 returns from cloud topology to a user-facing .NET runtime: Avalonia desktop applications, platform packaging, and the honest boundary of mobile support.
