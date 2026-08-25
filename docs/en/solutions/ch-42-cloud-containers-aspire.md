---
title: "Chapter 42 Solutions"
description: "Choose proportional compute models, turn the local cloud sample into a release proposal, and design an idempotent event consumer with honest unknown outcomes."
translationKey: solutions/ch-42-cloud-containers-aspire
---

# Chapter 42 Solutions {#overview}

These solutions make a first decision without pretending it is permanent. Each one names what is known, what remains unproved, and which evidence would justify moving to a more or less elaborate platform.

[Return to Chapter 42](../part-07/ch-42-cloud-containers-aspire).

## Exercise 1: choose a compute model for three workloads {#exercise-01}

### Case A: one steady internal HTTP API {#exercise-01-case-a}

Start with the organization's smallest supported managed application platform. If that path accepts a locked .NET publish artifact and supplies the required runtime, routing, identity, health, logging, and scaling controls, a container adds no immediate business capability.

Choose the managed container variant instead when the organization already promotes image digests, requires an image-based security gate, needs the same artifact on more than one compatible platform, or the code-deployment build contract is too opaque. The difference is artifact and operational ownership, not the F# domain model.

Reject Serverless as the first candidate because the API is steady, continuously available, and not naturally one sparse bounded event. A function layer would add trigger/runtime limits and a second hosting contract without a demonstrated scale-to-zero or event-integration benefit.

Reject Kubernetes because there is no cluster-level requirement and no team to own the cluster abstraction. One Deployment and Service YAML file would not account for upgrades, ingress, certificates, policy, capacity, storage, telemetry, tenancy, and incident response.

The first acceptance slice includes:

- locked Release build and one immutable publish artifact or image digest;
- startup validation for database endpoint, identity, and non-secret configuration;
- managed identity access to the database with no copied credential;
- separate liveness and readiness behavior, including database loss and recovery;
- request cancellation, graceful drain, resource limits, and a representative load test;
- production telemetry query, alert path, staged rollout, rollback, and cost observation.

Move from code deployment to a container if artifact drift, native dependency, platform portability, or supply-chain policy becomes material. Move away from the chosen managed product if a verified requirement exceeds its networking, scaling, identity, runtime, or operational limits. Do not move merely because a second service appears.

### Case B: bursty image metadata events {#exercise-01-case-b}

Start with a Serverless event worker if the exact provider trigger can buffer bursts, each item stays within measured duration/memory/package limits, and scale-to-zero materially reduces idle ownership. The function is a thin adapter around an ordinary F# decision and a rate-limited media-client port.

The queue event must carry a stable item/event ID and immutable object version. Persist one processing state per semantic identity. Concurrent duplicates either observe the existing result or contend through an atomic create/compare operation. Never use a fresh invocation ID as the idempotency key.

Bound concurrency below the media API's safe rate. Automatic compute scale is not permission to multiply downstream traffic. Use provider concurrency controls plus a client limiter; treat throttling as a declared transient outcome with jittered backoff. Define maximum event age, retry exhaustion, poison validation, partial-batch response, and dead-letter replay.

Reject Kubernetes first because no cluster feature is required. Reject a continuously warm service first only if the event runtime's cold path, limits, cost, and local/deployment tooling pass the spike.

Reverse to a managed worker/container when traffic becomes steady, cold/tail latency violates the objective, provider duration or package limits constrain processing, connection reuse dominates, or per-invocation cost exceeds reserved capacity. A queue remains useful across that migration because the F# core and durable event contract do not depend on the handler runtime.

The spike measures cold and warm latency, burst drain time, duplicate rate, downstream throttling, memory, package size, retry/dead-letter behavior, telemetry, and cost. It deploys the exact F# package to a staging provider environment; an emulator alone is insufficient.

### Case C: twenty regulated services on an existing platform {#exercise-01-case-c}

Kubernetes is the first candidate because the case supplies both concrete cluster-level requirements and a staffed platform. Admission policy, private networking, approved sidecars, tenant scheduling, and common operational controls are already platform responsibilities rather than speculative features.

That does not authorize every Kubernetes feature. Each F# service should still be an ordinary independently testable process with explicit HTTP/event contracts, external state, cancellation, health, and a pinned non-root image. Domain projects do not reference Kubernetes APIs.

The platform contract should define:

- approved base images, registries, signatures, SBOM and vulnerability exceptions;
- namespace/tenant boundaries, service accounts, workload identity, secrets, and network policy;
- requests, limits, disruption budgets, autoscaling signals, quotas, and capacity ownership;
- startup, liveness, readiness, termination grace, draining, and rollout policy;
- ingress, certificates, service discovery, egress, data services, backup, and recovery;
- logs, metrics, traces, audit, alert routing, service objectives, and cost allocation;
- cluster and workload upgrade cadence, compatibility tests, and incident escalation.

Aspire may improve local topology and emit target artifacts when a current deployment integration is deliberately adopted. It does not replace platform policy, manifest review, cluster credentials, staged apply, or rollback evidence.

Serverless can still win for one isolated event edge, and a managed service can still host an independent workload. The answer is not “all twenty must use Kubernetes”; the shared platform is the default only where its controls and ownership reduce total risk.

Reverse an individual workload when the platform adds more latency, cost, coupling, or incident load than its policy value. Reverse the platform decision only with migration evidence for the shared regulated controls, not with a cheaper compute quote alone.

## Exercise 2: turn the local cloud sample into a release proposal {#exercise-02}

### Begin with the exact baseline {#exercise-02-baseline}

Before proposing a release, a copied sample must first prove:

- the F# service and C# AppHost restore from locks and build on the checked macOS arm64 environment;
- the direct service and Aspire-orchestrated service answer the three tested endpoints;
- AppHost's local resource check becomes healthy;
- the SDK produces and exposes metadata for one `linux/arm64` image archive;
- the base tag is 10.0.11, the image user is 1654, the port is 8080, and the entry point is known;
- the ordinary package lock remains unchanged after the final container command.

Those local checks still do not prove a running target platform, cloud identity, registry, production probe, telemetry export, load, security policy, rollout, or rollback. The release proposal starts at that line instead of rebranding local evidence.

### Define one immutable artifact {#exercise-02-artifact}

Choose the managed environment's supported CPU architecture. Build that architecture in CI with SDK 10.0.301, locked dependencies, and the pinned base; run the full test suite before packaging. Generate an image digest, SBOM, provenance record, license inventory, and vulnerability report.

Push once to a restricted registry. Sign or attest the digest through the organization's approved identity. Promotion records refer to that digest, never just `latest`, branch, or commit. Registry retention must preserve both the active and rollback digests.

The policy gate checks base support, non-root user, no unexpected writable or privileged requirement, port/entry point, native libraries, architecture, secrets, severity exceptions, and signature. A reproducible rebuild is useful evidence but does not replace digest promotion.

### Make runtime and security contracts explicit {#exercise-02-runtime}

Run the image locally or in CI under the target architecture, UID, read-only root filesystem, temporary writable mount, dropped capabilities, CPU/memory limits, and port 8080. Verify startup, all API responses, liveness, readiness, signal-driven drain, forced termination, and recovery after restart.

The platform service account receives only the permission required by this sample. Because the local cloud sample has no data dependency, no database or secret is invented. Add non-secret `DEPLOYMENT_MODE` through versioned configuration. If a future secret appears, use the platform secret/identity path and test rotation without printing its value.

Expose application traffic and probe paths only through intended platform routes. Protect or isolate operational endpoints. Use encrypted authenticated management and telemetry connections; the anonymous loopback dashboard exception is not copied into the deployment.

### Add only owned service defaults {#exercise-02-observability}

Choose either a small C# Service Defaults adapter callable from F#, or explicit F# registrations for OpenTelemetry and ASP.NET Core health checks. Lock every new package. State the retry and timeout policy rather than accepting template behavior unseen.

Map a cheap liveness check and a readiness check whose dependencies are genuinely required. Configure the managed platform to call them with explicit interval, timeout, threshold, startup allowance, and termination/drain behavior. Test failure and recovery, not only 200 responses.

Send safe logs, request/error/latency metrics, traces, runtime metrics, release digest, and environment identity to the real production telemetry backend from staging. Query them. Trigger a test alert and confirm ownership. Define sampling, personal-data policy, retention, access, and expected ingestion cost.

### Separate deployment generation from approval {#exercise-02-pipeline}

The simplest proposal may not need Aspire deployment at all: the CI system can deploy the one digest through the managed platform's supported declarative interface. Keep the AppHost for local orchestration.

If Aspire deployment is adopted, pin and install the CLI, add the exact target integration, generate target output with `aspire publish`, review it, and let a protected deployment stage apply it. Use `aspire deploy` only when the stage is intentionally granting Aspire direct apply authority. In either case CI/CD owns approvals, identities, environment protection, logs, and retention.

Use separate staging and production configuration, but the same digest. A staging gate performs socket/TLS, identity, health, telemetry, restart, resource-limit, and representative load tests. Capture the platform revision and target configuration with the digest.

### Roll out and reverse safely {#exercise-02-rollout}

Release to a small traffic slice or revision. Gate expansion on error rate, tail latency, readiness churn, restarts, resource pressure, one synthetic request, and cost. Define numbers and observation windows before deployment.

Rollback routes traffic to the retained previous digest and compatible configuration. The local cloud sample has no data migration, so reversal is simple; the first persistent dependency must add schema compatibility and forward-fix analysis. Rehearse rollback from a deliberately unhealthy candidate.

After the observation window, retain evidence, remove failed revisions and unused temporary resources, and reconcile registry, telemetry, egress, and compute cost. Cleanup is part of the proposal because abandoned environments are both expense and attack surface.

## Exercise 3: design an idempotent Serverless booking consumer {#exercise-03}

### Model facts, attempts, and uncertainty separately {#exercise-03-model}

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

The pure core decides among `IgnoreAccepted`, `RejectConflict`, `AcquireAttempt`, `ReconcileUnknown`, `RetryTransient`, and `RejectPermanent`. Provider event, clock, storage version, and email response are explicit inputs; storage and email calls remain ports.

### Persist before and after the external effect {#exercise-03-persistence}

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

Atomic create/compare prevents concurrent handlers from both owning the same attempt. Lease expiry permits recovery after a crash; fencing prevents a late old worker from overwriting a newer result. Retention must cover the maximum source replay and business audit window.

There is still a dual-write gap between the email provider and local storage. If the provider accepts the message and the process dies before `Accepted` commits, local state is uncertain.

Exactly-once email cannot be manufactured by an F# type or local transaction. It requires provider cooperation: a stable idempotency key with durable deduplication, or a status lookup by that key/message ID. Without either, reconciliation can only choose between possible duplicate send and possible missed notification according to an explicit business policy.

### Classify retries and poison input {#exercise-03-retries}

Malformed schema, unsupported version, invalid recipient, and identity/payload conflict are permanent or poison outcomes. Record safe diagnostics and route them to a dead-letter/quarantine path without endless retry.

Provider timeout, throttling, and selected 5xx responses are transient candidates, but only after checking the provider contract. Use bounded exponential backoff with jitter, maximum event age, attempt count, and concurrency. A timeout after sending becomes `OutcomeUnknown`, not an automatic new send.

Partial-batch runtimes report only failed items when supported, so one poison event does not replay successful siblings. Dead-letter replay is an audited command that preserves the original identity and payload, not a copy with a new key.

Bound function concurrency and provider calls together. Autoscaling cannot exceed provider quota or storage capacity. Emit queue age and throttling signals so delayed work is visible before retries expire.

### Verify from core to provider {#exercise-03-evidence}

Pure tests cover first event, duplicate accepted event, conflicting hash, concurrent lease decisions, transient retry, permanent rejection, stale worker completion, and unknown-outcome reconciliation.

Storage contract tests prove atomic create, conditional update, lease expiry, fencing, and retention. Adapter tests cover exact provider request, idempotency header, status/error mapping, cancellation, timeout after acceptance, and redacted diagnostics. Event fixtures cover missing, extra, null, oversized, old, and future-version input.

A target-provider staging test sends duplicate and concurrent events, kills a handler around the external call, observes retry/dead-letter behavior, exercises provider lookup/idempotency, and queries telemetry. Measure cold/warm latency, queue age, scale, downstream rate, and cost.

Deploy one immutable package with locked worker/binding versions, least-privilege identity, encrypted configuration, concurrency and retry policy, alarms, and a disabled or zero-concurrency emergency stop. Roll out by event-source partition, alias, version, or provider-supported traffic control.

Rollback must keep reading states written by the new version and must not reset notification identities. If a schema or state transition is not backward-compatible, pause consumption and use a forward-compatible repair rather than blindly activating old code.

The final guarantee is deliberately narrow: each semantic notification reaches a terminal recorded state, duplicates are suppressed when the provider contract permits, and uncertainty is visible and reconcilable. Human delivery and exactly-once external effect remain outside the consumer's unilateral control.
