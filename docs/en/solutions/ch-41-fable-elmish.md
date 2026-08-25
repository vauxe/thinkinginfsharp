---
title: "Chapter 41 Solutions"
description: "Choose proportional browser architectures, reject stale asynchronous results, and split a shared pricing library across honest runtime boundaries."
translationKey: solutions/ch-41-fable-elmish
---

# Chapter 41 Solutions {#overview}

These answers choose a first architecture, preserve evidence that can overturn it, and keep target-runtime claims narrower than source-language familiarity.

[Return to Chapter 41](../part-07/ch-41-fable-elmish).

## Exercise 1: choose three browser architectures {#exercise-01}

### Case A: one preference toggle on server-rendered documentation {#exercise-01-case-a}

Start with a tiny isolated enhancement. If the preference can be represented by native HTML and CSS alone, use that. If it needs stored state and event handling and the application already owns a Fable pipeline, plain Fable plus Browser.Dom is the first F# candidate.

Use one model and one typed boundary:

```fsharp
type Preference =
    | FollowSystem
    | Light
    | Dark

type Message =
    | PreferenceSelected of Preference
    | SystemPreferenceChanged of prefersDark: bool
```

The pure transition decides the effective preference. One adapter reads and validates storage, one applies a document attribute, and one owns the media-query listener. Storage failure falls back without making the control unusable. The toggle is a real button or radio group with an accessible name and visible focus.

Do not add Elmish or React yet. They add a loop, renderer, native npm packages, upgrade pairings, and lifecycle concepts without removing meaningful risk from one island.

Reversal evidence includes a second and third island that must coordinate, duplicated listener/disposal code, URL-owned state, or asynchronous work that makes the local dispatcher inconsistent. Fable itself can also lose: if the page has no Fable toolchain and a few reviewed native JavaScript lines are materially cheaper to own, F# is not a mandatory browser dependency.

The acceptance slice covers system/light/dark initialization, invalid stored input, storage denial, keyboard operation, system preference changes, cleanup, a 320-pixel layout, production bundling, and no console or HTTP failures.

### Case B: a multi-step booking client with coordinated effects {#exercise-01-case-b}

Start with Fable.Elmish core and keep the renderer provisional. The problem has long-lived coordinated state, URL ownership, several fallible effects, retries, and races; those are the conditions under which a message loop can replace custom lifecycle machinery.

Model meaningful states instead of a bag of flags:

```fsharp
type BookingFlow =
    | Editing of Draft
    | CheckingAvailability of Draft * requestId: int
    | AwaitingConfirmation of PricedDraft
    | Paying of PricedDraft * attemptId: int
    | Completed of Confirmation
    | RecoverableFailure of Draft * SafeError
    | OutcomeUnknown of PricedDraft * reconciliationId: string
```

Availability success must match the active request ID. Payment timeout is not automatically a safe retry: the server contract from Chapters 36–38 must distinguish a known failure from an unknown outcome and support idempotent reconciliation. The browser never owns the authoritative price, capacity, or booking result.

Commands cover availability, submission, payment/reconciliation, and route effects. Every command returns a success, declared failure, cancellation, or transport-unavailable message. Subscriptions are reserved for truly external streams such as network state or a server event channel, with identity and cleanup.

Start with the smallest renderer that meets the actual UI. Choose Feliz/React only if component needs, rendering ergonomics, or an existing React surface repay the additional NuGet/npm graph. Elmish and direct DOM can coexist at a narrow render adapter; the state model should not expose renderer nodes.

Reversal evidence against Elmish is a representative flow where most state is actually isolated and the loop creates mapping ceremony without simplifying effects. Evidence for a renderer change includes inaccessible or error-prone manual DOM patching, required maintained React components, or measured rendering pressure. The opposite evidence—binding lag, upgrade conflict, or bundle cost—can reject React.

Acceptance includes deep-link/reload/back navigation, invalid drafts, two out-of-order availability checks, duplicate submit, payment known failure versus unknown outcome, cancellation, retry, focus after errors, screen-reader announcements, locked production output, and a real service contract test.

### Case C: five required React components with mostly local state {#exercise-01-case-c}

Start with Feliz plus locked `react` and `react-dom`, and wrap each required component through a small typed binding. Use component-local hooks for genuinely local state. Do not introduce one application-wide Elmish model merely because the client is written in F#.

For each component, inventory:

- exact native package and peer versions;
- the F# binding version or locally owned binding surface;
- required props, events, refs, promises, null/undefined, and cleanup;
- generated module format and production bundler behavior;
- semantic DOM, focus, keyboard, localization, and error behavior;
- license, advisory, source-map, bundle, and upgrade ownership.

Introduce `Feliz.UseElmish` or a child Elmish program only where a component contains a real workflow. Introduce a root Elmish program only when cross-component transitions, effects, or routing acquire one clear owner. Fable.Elmish.React is the connector if the chosen program is Elmish and the renderer is React; it is not a substitute for React itself.

Reversal evidence is a failed binding/native compatibility matrix, unacceptable accessibility output, production-only errors, prohibitive bundle/runtime cost, or components that can be replaced with platform HTML. A successful storybook-like demo is insufficient without the production Fable/Vite pipeline and target browsers.

## Exercise 2: model stale search results {#exercise-02}

### Separate input, pending work, and visible results {#exercise-02-model}

Use monotonically increasing generations for debounce timers and request IDs for network work:

```fsharp
type Pending =
    | NoPending
    | Debouncing of generation: int * query: string
    | Requesting of requestId: int * query: string

type VisibleResults =
    | NeverSearched
    | Showing of query: string * SearchItem list
    | Empty of query: string
    | Failed of query: string * safeMessage: string

type Model =
    { Input: string
      Pending: Pending
      Visible: VisibleResults
      NextGeneration: int
      NextRequestId: int
      Announcement: string option }
```

Keeping `Visible` separate lets the previous successful result remain on screen during a refresh. The view can show a non-blocking “Updating results” status instead of replacing useful content with a spinner.

Messages carry the identities needed to reject stale work:

```fsharp
type Msg =
    | InputChanged of string
    | DebounceElapsed of generation: int * query: string
    | SearchSucceeded of requestId: int * query: string * SearchItem list
    | SearchFailed of requestId: int * query: string * safeMessage: string
    | SearchCancelled of requestId: int
```

### Keep update deterministic {#exercise-02-update}

`InputChanged text` trims only according to the declared search contract, allocates a new debounce generation, clears any old announcement, and returns commands to cancel the active request/timer and start a 250 ms timer. Blank input moves to `NoPending` and `NeverSearched` without starting HTTP.

`DebounceElapsed(generation, query)` starts a request only if `Pending` is exactly `Debouncing(generation, query)` and the current input still denotes that query. It allocates `requestId`, moves to `Requesting(requestId, query)`, preserves `Visible`, and returns a start-search command.

Success, failure, and cancellation are accepted only when both request ID and query match the active `Requesting` case. Any completion for `fa` after `fable` has become active returns the unchanged model and no command. Logging an aggregate stale-completion counter is an adapter concern, not a model transition that exposes user input.

Successful nonempty results become `Showing`; zero results become `Empty`; declared failures become `Failed` with a safe message. Cancellation caused by newer input does not overwrite visible content or announce an error. A real user-requested stop may use a distinct message and notice.

### Own timers and AbortController outside the model {#exercise-02-effects}

A command adapter owns timer handles and `AbortController` instances keyed by generation/request ID. Starting newer work aborts and removes older handles. Completion and disposal also remove them. The model stores serializable identity and state, not browser controller objects.

Abort is an optimization and lifecycle action, not the correctness rule. The identity guard still rejects a response that won the race with abort or came from a transport that cannot cancel.

A subscription can own the debounce timer if the project consistently uses model-keyed Elmish subscriptions. Its subscription ID includes the active generation; changing or clearing it stops the previous subscription. Do not run both command-timer and subscription-timer designs simultaneously.

### Test the transitions and accessible output {#exercise-02-tests}

Pure tests cover:

- blank input starts nothing;
- `fa` allocates generation 1, then `fable` allocates generation 2 and cancels generation 1;
- a generation-1 tick is ignored, while generation 2 starts request 1;
- a later edit starts request 2 and request-1 success/failure/cancellation is ignored;
- request-2 nonempty, empty, and safe failure results create distinct visible states;
- retry allocates a new request ID rather than reusing an ambiguous completion identity;
- accepted completion clears pending work and produces one bounded announcement;
- ignored stale work does not change content, focus, or announcement.

The browser test types both queries under controlled response gates, completes `fable` first and `fa` last, and proves only `fable` remains. It also checks keyboard input, focus retention, a polite live status that does not announce every keystroke, safe errors, cancellation cleanup, unmount, console/network failures, and the production bundle.

## Exercise 3: audit a shared library and release {#exercise-03}

### Extract a target-neutral pricing core {#exercise-03-core}

Create a small project containing only pricing contracts and deterministic decisions:

```fsharp
type Money =
    private
    | Money of currency: Currency * amount: decimal

type PricingInput =
    { At: DateTimeOffset
      Basket: Basket
      Rules: PricingRules }

val price: PricingInput -> Result<PricedBasket, PricingError list>
```

Pass time and rules as data. Keep currency explicit and centralize scale/rounding rules. The core does not read a clock, environment, file, browser storage, or serializer. It does not decide how configuration arrived.

The server remains authoritative. The browser may calculate a preview for responsiveness, but checkout sends a versioned request and displays the server's priced result. A client bundle can be modified, run with stale rules, or have different numeric behavior.

### Put target effects in separate adapters {#exercise-03-adapters}

The server adapter reads approved configuration, obtains a clock instant, loads/validates the JSON rule document at startup, protects secrets, and maps HTTP DTOs through an explicit serializer. It exposes validated `PricingRules` and `DateTimeOffset` to the core.

The browser adapter receives nonsecret public configuration through a versioned HTTP or build artifact, maps Web API time only when the product contract allows client time, and uses an explicit JSON codec compatible with the server wire schema. It never imports the reflection-based server serializer by hope.

If the browser must evaluate “now,” define whether server time, client wall time, or a monotonic duration owns the rule. For expiry or price authority, use a server-issued instant/version. Client clocks are user-controlled and can drift.

The old reflection serializer remains behind the server adapter until contract tests justify replacement. The browser codec must reject malformed, missing, extra, null, number, date, currency, and version cases according to one written compatibility policy.

### Build cross-target semantic evidence {#exercise-03-cross-target}

Run the same golden vectors through .NET and Fable-generated JavaScript. Include every currency scale, midpoint rounding, negative/zero/high amounts, quantity boundaries, discounts, tax order, date cutoffs, Unicode identifiers, and invalid combinations. Compare declared money values, errors, and rule versions—not internal object representations.

Add generated boundary/property cases where the two test frameworks can share deterministic seeds or serialized fixtures. A mismatch blocks client preview release; it must not be rounded away without a domain decision.

Decimal use is not proof of identical runtime behavior. Fable documents target-specific numeric representations. Until cross-target tests and representative browser measurements pass, claim only that both targets compile. Even after they pass, the server recalculates authoritative price.

Compile-time success also cannot prove reflection, time-zone, serializer, or filesystem equivalence; those features were deliberately removed from the shared core rather than emulated.

### Lock, build, secure, and release {#exercise-03-release}

The release pipeline performs:

1. locked SDK, tool, NuGet, and JavaScript dependency restore;
2. .NET unit/property tests and server serializer contracts;
3. Fable compilation plus JavaScript execution of shared golden vectors;
4. Vite production build and bundle/license/advisory review;
5. real server/browser HTTP contract and checkout smoke;
6. accessibility, CSP, storage, cache, source-map, and secret scans;
7. static-host tests for MIME, base path, compression, caching, direct routes, and error documents;
8. artifact digest, rule/API compatibility, rollout, monitoring, and rollback recording.

Deploy server support for the new DTO/rule version before publishing a client that needs it. Keep old server compatibility through the browser-cache and rollback window. Static assets use immutable hashes; the HTML entry revalidates and can be rolled back to a prior compatible asset set.

Observe bounded counts for rule versions, client/server price disagreement, rejected DTO versions, checkout errors, and rollback selection without logging baskets, tokens, or personal data. A disagreement prevents submission or shows the authoritative server price; it never silently charges the client preview.

Delete the original mixed project only after every server caller uses the server adapter, the Fable project compiles only the neutral core, golden tests run on both targets, production no longer loads its file/environment paths from core code, cached clients remain compatible, and rollback no longer requires the old layout.

## Solution review {#solution-review}

- One isolated preference control starts with platform HTML or plain Fable, not an automatic application framework.
- Coordinated booking state, effects, routes, retries, and unknown outcomes can justify Elmish while leaving the renderer provisional.
- Required React components justify Feliz/React bindings before they justify one global Elmish model.
- Binding and native npm versions, accessibility, production output, and cleanup all need one compatibility matrix.
- Debounce generations reject old timers; request IDs reject old network completions.
- AbortController saves work, but identity matching supplies correctness.
- Visible results, pending work, empty state, safe failure, and announcements remain distinct.
- Browser controllers belong to an effect adapter, not the serializable model.
- Shared pricing receives time, rules, and inputs; it does not read target-specific environment.
- Browser pricing is a preview; the server remains authoritative.
- Decimal, dates, reflection, and serialization require cross-target evidence rather than source-level confidence.
- Lock both dependency graphs and execute golden vectors under both CLR and generated JavaScript.
- Release the server compatibility boundary before the cached static client that consumes it.
- Rollback includes HTML/assets, API/rule versions, caches, and old-client compatibility.

## Sources {#sources}

- [Fable: Build and Run](https://fable.io/docs/javascript/build-and-run.html)
- [Fable: .NET and F# compatibility](https://fable.io/docs/javascript/compatibility.html)
- [Fable: JavaScript features and interoperability](https://fable.io/docs/javascript/features.html)
- [Elmish overview](https://elmish.github.io/elmish/)
- [Elmish subscriptions](https://elmish.github.io/elmish/docs/subscription.html)
- [NuGet: Fable.Elmish 5.0.2](https://www.nuget.org/packages/Fable.Elmish/5.0.2)
- [NuGet: Fable.Elmish.React 5.6.0](https://www.nuget.org/packages/Fable.Elmish.React/5.6.0)
- [NuGet: Feliz 3.3.3](https://www.nuget.org/packages/Feliz/3.3.3)
