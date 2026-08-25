---
title: "Chapter 42: Cloud, Containers, Serverless, and .NET Aspire"
description: "Choose a deployment model from process, state, scaling, operational, security, and evidence requirements while keeping F# application boundaries explicit."
translationKey: part-07/ch-42-cloud-containers-aspire
---

# Chapter 42: Cloud, Containers, Serverless, and .NET Aspire {#overview}

An F# service runs as a .NET process in the cloud. Its domain types, functions, ports, cancellation, configuration, and wire contracts continue to define the application after deployment. Cloud products decide who supplies machines, networking, scaling, identity, and operations around those boundaries.

Start by naming the deployment problem, then choose the product and layer. A container packages a process. A compute platform such as Kubernetes runs it. Serverless changes the execution and billing contract. Aspire describes an application model and can drive local orchestration or target-specific deployment work; a separate target runtime hosts production.

This chapter starts with the process and artifact, then moves outward. That order keeps F# design visible and makes every cloud claim proportional to evidence.

## What you will be able to do {#outcomes}

By the end of this chapter, you should be able to:

- distinguish source, application artifact, image, running instance, platform configuration, and release, then match each claim to proportionate evidence;
- choose among managed processes, managed containers, Kubernetes, and Serverless from trigger, lifetime, state, scale, control, and operational ownership;
- define process and platform contracts for configuration, secrets, identity, storage, networking, resources, shutdown, and health;
- design durable handlers for retries, duplicate delivery, partial completion, and poison input while keeping state and idempotency outside ephemeral instances;
- connect F# services to containers and Aspire while keeping AppHost, service health, publish/deploy, and CI/CD responsibilities distinct;
- promote one immutable artifact through a reversible release with explicit cost, security, observability, and rollback evidence.

::: tip Two reading passes
For a first pass, follow the [deployment stack](#deployment-contracts), [decision map](#compute-decision-map), and [verified local slice](#verified-slice). Return to the [release plan](#release-observe-rollback), [evidence ladder](#evidence-ladder), and [adoption spike](#adoption-spike) when preparing a real deployment.
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
| VM or directly managed host | Legacy/native dependency, unusual OS control, stable load, or migration constraint makes host ownership worthwhile | Patching, process supervision, capacity, failover, certificates, deployment, telemetry, backup, hardening |

These are starting points, not a ranking. A managed container product can scale to zero; a Serverless product may accept container images; Kubernetes may be managed; a VM can run containers. Decide from the contract you must own, not the marketing category.

## The local cloud sample: one verified local slice {#verified-slice}

The local cloud sample deliberately contains one F# HTTP service, one C# project-based AppHost, and no cloud account or external service. It demonstrates the boundary between application code, development orchestration, and container packaging without pretending to validate a provider deployment.

### The F# service and pinned image base {#fsharp-service}

```xml:line-numbers [CloudService.fsproj]
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ContainerRepository>thinking-in-fsharp-cloud-service</ContainerRepository>
    <ContainerBaseImage>mcr.microsoft.com/dotnet/aspnet:10.0.11</ContainerBaseImage>
  </PropertyGroup>

  <ItemGroup>
    <Compile Include="Program.fs" />
  </ItemGroup>
</Project>
```
The Web SDK targets `net10.0`; a copied sample can pin FSharp.Core 10.1.301 in its project file. The image base is explicitly `mcr.microsoft.com/dotnet/aspnet:10.0.11`; a floating `10.0` tag would silently move the runtime beneath an unchanged commit. .NET 10 unqualified Microsoft image tags use Ubuntu rather than the Debian base used by earlier releases, so OS assumptions need testing.

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

The project opts into the Aspire CLI bundle and selects `DnxPinned`. The SDK therefore resolves the matching `Aspire.Cli@13.5.2` instead of adding host-specific Dashboard and orchestration packages to the AppHost lock file. No global CLI install is required, but first use needs the package source or a populated cache. This is an optional ecosystem sample, not a dependency of the book site.

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
The AppHost names the resource, explicitly declares an `http` endpoint, injects `DEPLOYMENT_MODE=aspire-local`, and attaches an HTTP health check. The explicit endpoint matters: without launch-profile endpoint metadata, the first real run failed before orchestration because the health check had no `http` or `https` endpoint to select.

This is a good inter-language boundary. F# owns the application and its typed behavior. C# owns a small infrastructure DSL whose current templates, generated metadata, and examples are C#-first. No domain type crosses the boundary, so replacing the orchestration tool does not rewrite the service.

### What was actually executed {#executed-evidence}

The verified sequence established the following:

- .NET SDK 10.0.301 restored the F# and AppHost projects from lock files;
- both projects built in Release with zero warnings and zero errors;
- direct service requests returned `healthy`, `ready`, and `standalone`;
- Aspire 13.5.2 started the F# child process and injected `aspire-local`;
- a loopback-only HTTP dashboard showed the resource as `Running`, health state `Healthy`, and the `/health/ready` check as `Healthy`;
- the service reached through the Aspire-assigned port returned `aspire-local`;
- the .NET container target produced an approximately 89 MB `linux/arm64` archive from the pinned ASP.NET Core 10.0.11 base;
- archive metadata showed non-root UID 1654, port 8080, and entry point `dotnet /app/CloudService.dll`;
- the final publish command left the ordinary `net10.0` lock-file hash unchanged;
- the full locked solution build, tests, Fable production build, and browser smoke still passed.

The machine had Docker CLI but no running daemon. The archive was inspected and then deleted; no container start is claimed. No registry push, signature, SBOM attestation, vulnerability decision, cloud identity, external dependency, load test, production telemetry export, deployment, or rollback occurred.

### Local HTTP was an explicit test exception {#local-http-exception}

The machine also lacked a trusted Aspire development certificate. A first HTTPS dashboard started the service but could not validate its own resource-service certificate, so the UI remained disconnected. The final check used fixed, anonymous HTTP endpoints bound strictly to `127.0.0.1`, as permitted by Aspire's local configuration.

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

Kubernetes explicitly gives startup, liveness, and readiness probes different effects. Aspire also distinguishes AppHost resource checks from service endpoint checks. The local cloud sample connects the AppHost check to the service's readiness URL, but one green local check does not configure a production load balancer.

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

### The lock-file lesson from the local cloud sample {#lock-file-lesson}

The first container command added `--os linux`. Restore then rewrote the project's lock graph with runtime identifier `linux-arm64`, and the next ordinary solution `--locked-mode` restore failed because the project itself declared no RID.

The final checked command lets the container target choose the Linux image platform without changing the application project's runtime identifier. The lock hash remains stable. When a self-contained or cross-architecture release truly needs a RID-specific graph, isolate and commit that release contract deliberately; do not let an ad hoc publish mutate the development lock unnoticed.

## Serverless is an invocation contract {#serverless-contract}

“Serverless” means the provider owns more of the execution fleet and exposes a higher-level invocation, scaling, and billing model. Servers still exist, and application responsibilities remain.

Choose a function or event runtime when work is naturally bounded, sparse or bursty, provider triggers remove meaningful infrastructure, scale-to-zero is acceptable, and the team can live within the runtime's limits. A continuously busy low-latency API, long-lived socket, heavy native process, stable high-throughput worker, or workflow with complex in-memory coordination may fit a service or container better.

### Put a thin handler around a pure decision {#thin-handler}

Use this shape:

```text
provider event/binding
  -> validate and map public input
  -> pure F# decision or workflow
  -> explicit ports for durable effects
  -> provider response, acknowledgement, retry, or dead-letter outcome
```

Keep provider attributes, trigger types, contexts, and SDK clients at the edge. The core should accept ordinary records and unions and return declared decisions. That makes the business behavior runnable without an emulator and keeps migration between a function and a worker possible.

Bindings save code only when their failure semantics are understood. Ask who owns serialization, batching, checkpointing, acknowledgement, retries, poison messages, partial batch success, concurrency, cancellation, and telemetry. If an output binding hides the error you need to classify, a direct client SDK behind a port may be safer.

### Assume retries and duplicate delivery {#retries-duplicates}

Provider behavior varies by trigger and invocation mode, but duplicate delivery is a normal possibility. AWS documents at-least-once event source mappings; Azure's event and retry guidance likewise requires considering duplicate processing. “The function ran once” is not a domain guarantee.

Give each command or event a stable identity. Validate that the same identity has the same semantic payload. Make the effect and deduplication record atomic when possible. Record success only after the authoritative effect commits. Define retention, conflict, replay, partial-batch, retry exhaustion, and dead-letter behavior.

A payment timeout is not proof of failure. It may be an unknown outcome requiring lookup or reconciliation under the same idempotency key. Retrying with a new identity can duplicate the charge regardless of how quickly the function scales.

### Cold start, concurrency, and limits are design inputs {#cold-start-concurrency-limits}

Measure package size, initialization, dependency connection setup, first invocation, warm invocation, scale-out, and tail latency under the chosen plan and region. Do not promise a cold-start number copied from another language, package, plan, or date.

Concurrency can occur within an instance, across instances, or both. Static mutable values and local caches are shared according to the provider worker model, not per logical event. Bound downstream connections and request rates; automatic compute scaling can overwhelm a database or paid API faster than it helps users.

Every provider defines supported .NET versions, CPU architectures, duration, memory, temporary storage, payload, networking, concurrency, retry, and deployment-package limits. Treat that matrix as a dated dependency and test the exact trigger and hosting plan.

### F# on provider .NET workers {#fsharp-provider-workers}

A provider advertising “.NET” does not prove first-class F# templates, analyzers, generated bindings, local tools, Native AOT behavior, or documentation. F# can consume ordinary .NET libraries, but code-generation and tooling surfaces may be language-shaped.

As of 2026-08-25, Azure Functions 4.x isolated worker documentation lists .NET 10 and notes that F# applications may need explicit registration for some binding extensions. It also records plan-specific restrictions and minimum worker package versions. This chapter reviewed that documentation but did not build or deploy an Azure Function.

AWS currently documents a .NET 10 Lambda base image and .NET packaging paths, mostly using C# terminology and examples. A compiled F# handler can be a candidate only after an F# project spike verifies handler discovery, serializer behavior, packages, architecture, local invocation, cold path, deployment, and telemetry. The local cloud sample did none of those steps.

Do not choose Serverless to avoid learning deployment. It adds a provider runtime, trigger contract, identity, limits, pricing dimensions, local emulator/tooling, and event failure semantics. Choose it when those additions remove more owned infrastructure than they create application risk.

## Aspire models topology; it does not erase it {#aspire-model}

An Aspire AppHost is code that declares resources and relationships. In run mode, the Developer Control Plane starts and monitors local processes or containers, assigns endpoints, injects configuration, and feeds the dashboard. Official architecture guidance explicitly says the AppHost is not a production runtime.

### Resources, references, and ordering {#resources-references-ordering}

`AddProject`, `AddContainer`, and hosting integrations add resources to an application model. `WithReference` expresses a relationship and can inject connection or endpoint information. `WaitFor` controls startup readiness. These operations solve different problems: a reference is not automatically a wait, and either one is not a production authorization policy.

Aspire integrations are packages that teach the AppHost how to represent and connect resources. Adding a database integration may start a local container, connect to an existing service, or participate in deployment. It does not decide schema ownership, transaction boundaries, backup, capacity, failover, data classification, or deletion.

Name resources as stable operational concepts. Treat generated connection data as configuration at the service boundary. Keep domain code unaware of Aspire resource types, so tests and alternative hosts can construct the same ports directly.

### Two health systems {#two-health-systems}

AppHost resource health answers whether orchestration considers a resource ready, including whether a dependent `WaitFor` may proceed. Service endpoint health answers whether a running application instance should receive traffic or restart under its production platform.

The dashboard can display an HTTP resource check, as the local cloud sample verified. Production still needs platform probe configuration aimed at the right service path, port, timeout, threshold, and security boundary. Copying a green dashboard screenshot into a runbook does not create that configuration.

### Service Defaults are source code, not magic {#service-defaults}

The current C# Service Defaults template composes OpenTelemetry, health checks, service discovery, and standard `HttpClient` resilience. It is a customizable shared project. Calling `AddServiceDefaults` and `MapDefaultEndpoints` is what installs those behaviors; merely running under AppHost does not instrument an application.

The local cloud sample intentionally omits Service Defaults. The AppHost injects OTLP-related environment variables, but the F# service has no OpenTelemetry SDK/exporter packages, so the chapter claims no traces or metrics. Its health endpoints are explicit teaching handlers, not ASP.NET Core `IHealthCheck` registrations.

For a real F# solution, choose one of three honest paths:

1. reference a small C# Service Defaults adapter and call its public extensions from the F# composition root;
2. reproduce only the required registrations directly in F#, with locked packages and tests;
3. create a language-neutral shared library whose public API is deliberately F#- and C#-friendly.

Do not copy a template once and forget it. Own retry policy, timeouts, endpoint exposure, instrumentation sources, exporter behavior, sampling, package upgrades, and production backend verification.

## Local orchestration and deployment are separate modes {#local-versus-deployment}

Current Aspire deployment is pipeline-based. A deployment target or compute environment contributes target-specific steps to the application model.

- `aspire publish` evaluates the AppHost and emits artifacts for a later tool or human to apply; it is a one-way handoff.
- `aspire deploy` evaluates the model, resolves parameters, generates target output, and applies it directly.
- `aspire do <step>` invokes named pipeline steps when CI/CD needs a split flow.

These commands require an appropriate CLI and target integrations. The local cloud sample uses the pinned CLI bundle for development orchestration, but configures no deployment target and runs no publish or deploy command. Its AppHost build is only a local application-model and orchestration check.

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

1. pure F# tests for decisions, idempotency, and state transitions;
2. adapter tests for configuration, serialization, provider events, cancellation, and failure mapping;
3. locked Release build and ordinary publish for every target framework/RID;
4. image metadata and policy checks for user, port, entry point, base, architecture, SBOM, signature, and vulnerabilities;
5. container start under read-only/non-root/resource-limit settings, then probe and shutdown tests;
6. AppHost or emulator test for resource wiring and the representative dependency path;
7. target-platform staging deployment with real identity, network, data, probes, telemetry, scaling, and failure injection;
8. progressive production rollout with user, reliability, security, and cost gates;
9. rollback or forward-fix rehearsal with evidence that data remains compatible.

Emulators and local orchestrators are useful but not authoritative. Provider control planes, identities, quotas, networking, retries, and managed data services must be tested in a production-like environment. Conversely, do not move pure domain testing into expensive cloud fixtures.

## Date every compatibility claim {#version-evidence}

| Surface | Checked version or statement | What an adopting application must verify |
| --- | --- | --- |
| .NET SDK | 10.0.301 | Locked restore, Release build, tests, and publish |
| FSharp.Core | 10.1.301 | Resolved graph and runtime compatibility |
| Aspire.AppHost.Sdk | 13.5.2, published 2026-08-21 | Use only if local multi-service orchestration repays its cost; then test startup and health |
| ASP.NET Core base image | 10.0.11 | Image metadata, operating system, architecture, vulnerabilities, and container startup |
| Aspire CLI bundle/deployment targets | CLI 13.5.2 | Bundle output and the chosen deployment target, if used |
| Azure Functions isolated worker | Docs list .NET 10 and F# binding caveats | Package, emulate, and deploy the actual trigger path |
| AWS Lambda .NET 10 image/runtime path | Current official docs reviewed | Package, invoke, and deploy the actual handler |
| Kubernetes probes/deployment | Current official semantics reviewed | Manifest, cluster behavior, and real probe execution |

Versions answer “what was considered,” not “what your application supports.” Keep the provider plan, region, architecture, trigger, integration package, CLI, base digest, and test date with the evidence.

## Run a bounded adoption spike {#adoption-spike}

Choose one representative path rather than scaffolding the intended final estate:

- one F# service or handler with a real public/event contract;
- one durable effect with duplicate and unknown-outcome handling;
- one identity and secret flow without copied long-lived credentials;
- one image or deployment package for the actual architecture;
- one readiness, liveness, shutdown, and dependency-failure sequence;
- one telemetry path queried in the target backend;
- one measured scale or cold-capacity scenario;
- one deployment, partial rollout, rollback or forward-fix, and cleanup;
- one cost estimate reconciled with observed usage.

Compare the smallest managed service, managed container, Serverless candidate, and Kubernetes only when Kubernetes is genuinely plausible. Count code, manifests, packages, control-plane objects, permissions, pipeline steps, alerts, upgrade duties, incident paths, and deletion work.

The spike should be cheap to remove. Keep provider types outside the domain core, preserve a normal host path, and record the condition that would reverse the choice.

## Avoid common cloud mistakes {#common-mistakes}

- Treating a container as an operating model or a security boundary by itself.
- Selecting Kubernetes before identifying a cluster-level requirement and owner.
- Calling Serverless stateless while retaining business truth in memory or `/tmp`.
- Assuming an event executes once, in order, on one instance.
- Retrying a payment or write with a new identity after an unknown outcome.
- Putting every downstream dependency into liveness and creating restart storms.
- Exposing health details, dashboards, OTLP, or admin endpoints without access control.
- Shipping secrets, environment values, certificates, or provider IDs inside an image.
- Promoting mutable tags or rebuilding separately for each environment.
- Ignoring OS, CPU architecture, non-root user, filesystem, signal, and memory differences.
- Adding Aspire resources without understanding connection, ordering, health, and production ownership.
- Assuming AppHost environment automatically becomes the child application's environment.
- Assuming OTLP variables mean the F# service is instrumented or telemetry reached a backend.
- Treating a green local dashboard as a production deployment or probe configuration.
- Suppressing broad warning sets instead of documenting one dated migration warning.
- Letting a publish command silently rewrite a shared lock file.
- Claiming “.NET support” proves first-class F# templates and bindings.
- Using a local emulator as evidence for provider identity, retries, quotas, networking, or cost.
- Deploying a code rollback without checking schema, messages, payments, and other irreversible effects.
- Measuring cloud cost without engineering and incident ownership.

## Exercises {#exercises}

### Exercise 1: choose a compute model for three workloads {#exercise-01}

Evaluate these workloads separately:

1. One team owns a steady internal HTTP API with a managed database, moderate traffic, no custom network or sidecar requirement, and no platform team.
2. Image metadata processing arrives in sharp bursts. Each item finishes within seconds, duplicate delivery is possible, and the downstream media API is rate-limited.
3. Twenty regulated services need common admission policy, private networking, sidecars, controlled multi-tenant scheduling, and an existing staffed Kubernetes platform.

For each workload, record the first candidate, rejected alternatives, evidence gap, and reversal condition. Compare managed processes or containers, Serverless, and Kubernetes; each workload may lead to a different choice.

### Exercise 2: turn the local cloud sample into a release proposal {#exercise-02}

Design the minimum work required to deploy the F# service from the local cloud sample to a managed container environment. Organize the proposal into four parts:

- **Artifact and supply chain:** architecture, immutable image identity, registry, SBOM, signing, and vulnerability policy.
- **Runtime contract:** configuration and secret identity, Service Defaults or alternative telemetry, production probes, non-root/read-only execution, resource limits, and shutdown.
- **Release path:** staging smoke, representative load, progressive rollout, rollback, and data compatibility.
- **Ownership:** cost, cleanup, and the team responsible for each operational response.

Label each claim as either illustrated by this chapter or awaiting evidence from the target environment.

### Exercise 3: design an idempotent Serverless booking consumer {#exercise-03}

A provider event delivers `BookingConfirmed` at least once. The handler must reserve a notification identity, call an email provider, record the outcome, retry transient faults, isolate poison input, and tolerate a crash after the provider accepted the email but before the handler recorded success.

Show four parts in the design:

- **Core state:** F# types, persistent transitions, the atomic boundary, and concurrency control.
- **Provider boundary:** the email adapter and reconciliation of an unknown outcome.
- **Operations:** retry and dead-letter policy, telemetry, deployment, and rollback.
- **Proof:** tests for duplicate delivery, partial completion, poison input, and recovery.

Finish by naming the guarantee that requires cooperation from the email provider.

[Read the chapter solutions](../solutions/ch-42-cloud-containers-aspire).

## Chapter review {#chapter-review}

- F# cloud code remains ordinary .NET application code; deployment changes external contracts, not the value of types and functions.
- Separate compilation, publish layout, image, platform configuration, running instance, and observable release evidence.
- Choose compute from trigger, lifetime, state, scale, control, and operational ownership.
- A container packages a process; a platform runs it; Serverless defines an invocation model; Aspire declares an application model.
- Keep configuration external, secrets out of artifacts, identity narrow, and durable state outside ephemeral instances.
- Treat shutdown as best effort and propagate cancellation; durable work needs idempotency and recovery.
- Liveness, readiness, startup, resource readiness, synthetic journeys, and business health have different consumers and reactions.
- Pin and inspect base image, architecture, user, port, entry point, supply-chain evidence, and immutable digest.
- Serverless handlers need thin provider adapters, explicit retry semantics, duplicate handling, concurrency bounds, and measured cold paths.
- Provider “.NET support” is not enough evidence for F# templates, bindings, code generation, or tooling.
- A C# AppHost can be an honest narrow infrastructure adapter around an F# service.
- AppHost is a development orchestrator, not the production runtime; local resource health is not production probe configuration.
- Service Defaults are optional owned source code; environment injection alone does not instrument a service.
- `aspire publish` emits a handoff, `aspire deploy` applies a target pipeline, and CI/CD still owns governance.
- Promote one immutable artifact, design compatible data changes, observe progressive rollout, and rehearse rollback or forward fix.
- The local cloud sample verifies a local F# service, C# AppHost, dashboard health, and image archive only; all provider paths remain unexecuted.

Chapter 43 returns from cloud topology to a user-facing .NET runtime: Avalonia desktop applications, platform packaging, and the honest boundary of mobile support.
