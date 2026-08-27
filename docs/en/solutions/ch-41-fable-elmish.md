---
title: "Chapter 41 Solutions"
description: "Choose proportional browser architectures, reject stale asynchronous results, and split a shared pricing library across honest runtime boundaries."
translationKey: solutions/ch-41-fable-elmish
---

# Chapter 41 Solutions {#overview}

These answers choose a starting architecture, state when to reconsider it, and limit runtime claims to what was actually tested on each target.

[Return to Chapter 41](../part-07/ch-41-fable-elmish).

## Exercise 1: choose three browser architectures {#exercise-01}

### Case A: one preference toggle on server-rendered documentation {#exercise-01-case-a}

Start with a tiny isolated enhancement. If native HTML and CSS can represent the preference, use them. If it needs stored state and event handling, and the application already uses a Fable build pipeline, plain Fable plus Browser.Dom is the first F# candidate.

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

The pure transition decides the effective preference. One adapter reads and validates storage, another applies a document attribute, and a third manages the media-query listener and its cleanup. Storage failure falls back without making the control unusable. The toggle is a real button or radio group with an accessible name and visible focus.

Do not add Elmish or React yet. They add a loop, renderer, native npm packages, upgrade pairings, and lifecycle concepts without removing meaningful risk from one island.

Reconsider this choice when multiple interactive islands must coordinate, listener cleanup is duplicated, the URL becomes the source of state, or asynchronous work makes local dispatch inconsistent. Fable itself can also lose. If the page has no Fable toolchain and a few reviewed JavaScript lines cost much less to maintain, F# need not become a browser dependency.

The acceptance slice covers system/light/dark initialization, invalid stored input, storage denial, keyboard operation, system preference changes, cleanup, a 320-pixel layout, production bundling, and no console or HTTP failures.

### Case B: a multi-step booking client with coordinated effects {#exercise-01-case-b}

Start with Fable.Elmish core and keep the renderer provisional. The flow has persistent coordinated state, URL state, several fallible external operations, retries, and races. Under those conditions, a message loop can replace custom lifecycle machinery.

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

An availability result must match the active request ID. A payment timeout is not automatically safe to retry: the server contract from Chapters 36–38 must distinguish a known failure from an unknown outcome and support idempotent reconciliation. The server, not the browser, determines the final price, capacity, and booking result.

Commands cover availability, submission, payment or reconciliation, and route updates. Every command returns a success, declared failure, cancellation, or transport-unavailable message. Reserve subscriptions for external streams such as network state or a server event channel; give each one an identity and cleanup path.

Start with the smallest renderer that meets the actual UI. Choose Feliz and React only when required components, rendering ergonomics, or existing React code justify the added NuGet and npm dependencies. Elmish and direct DOM can coexist behind a narrow rendering adapter; the state model should not expose renderer nodes.

Reject Elmish if a representative flow shows that most state is isolated and the loop adds mappings without simplifying external operations. Change renderers if manual DOM updates are inaccessible or error-prone, maintained React components are required, or measurement shows rendering pressure. Binding lag, upgrade conflicts, or bundle cost can instead rule out React.

Acceptance covers navigation through deep links, reload, and Back. It also covers invalid drafts, out-of-order availability results, duplicate submission, known payment failure versus unknown outcome, cancellation, retry, focus after errors, and screen-reader announcements. The locked production output must pass a real service contract test.

### Case C: five required React components with mostly local state {#exercise-01-case-c}

Start with Feliz plus locked `react` and `react-dom`, and wrap each required component through a small typed binding. Use component-local hooks for genuinely local state. Do not introduce one application-wide Elmish model merely because the client is written in F#.

For each component, inventory:

- exact native package and peer versions;
- the F# binding version or the locally maintained binding API;
- required props, events, refs, promises, null/undefined, and cleanup;
- generated module format and production bundler behavior;
- semantic DOM, focus, keyboard, localization, and error behavior;
- licenses, advisories, source maps, bundle cost, and who handles upgrades.

Introduce `Feliz.UseElmish` or a child Elmish program only where a component contains a real workflow. Add a root Elmish program only when cross-component transitions, external operations, or routing need one coordinator. Fable.Elmish.React connects an Elmish program to React; it does not replace React itself.

Reject this choice if the binding and native-package matrix fails, accessibility output is unacceptable, errors appear only in production, bundle or runtime cost is too high, or platform HTML can replace the components. A successful storybook-like demo is insufficient without the production Fable/Vite pipeline and target browsers.

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

### Manage timers and AbortController outside the model {#exercise-02-effects}

A command adapter manages timer handles and `AbortController` instances keyed by generation or request ID. Starting newer work aborts and removes older handles. Completion and disposal also remove them. The model stores serializable IDs and state, not browser controller objects.

Abort is an optimization and lifecycle action, not the correctness rule. The identity guard still rejects a response that won the race with abort or came from a transport that cannot cancel.

A subscription can manage the debounce timer when the project consistently uses model-keyed Elmish subscriptions. Its ID includes the active generation, so changing or clearing it stops the previous subscription. Do not run both command-timer and subscription-timer designs simultaneously.

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

The browser test types both queries under controlled response gates, completes `fable` first and `fa` last, and verifies that only `fable` remains. It also checks keyboard input, focus retention, a polite live region that does not announce every keystroke, safe errors, cancellation cleanup, unmounting, console or network failures, and the production bundle.

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

### Put runtime I/O in separate adapters {#exercise-03-adapters}

The server adapter reads approved configuration, obtains a clock instant, loads/validates the JSON rule document at startup, protects secrets, and maps HTTP DTOs through an explicit serializer. It exposes validated `PricingRules` and `DateTimeOffset` to the core.

The browser adapter receives nonsecret public configuration through versioned HTTP or a build artifact. It maps Web API time only when the product contract permits client time, and uses a defined JSON codec compatible with the server wire schema. It never assumes that the reflection-based server serializer will work in the browser.

If the browser must evaluate “now,” define whether the rule uses server time, client wall time, or a monotonic duration. For expiry or authoritative pricing, use a server-issued instant and version. Client clocks are user-controlled and can drift.

The old reflection serializer remains behind the server adapter until contract tests justify replacement. The browser codec must reject malformed, missing, extra, null, number, date, currency, and version cases according to one written compatibility policy.

### Verify semantics on both targets {#exercise-03-cross-target}

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

Delete the original mixed project only after all of these conditions hold:

- every server caller uses the server adapter;
- the Fable project compiles only the neutral core;
- golden tests run on both targets;
- production no longer loads file or environment paths from core code;
- cached clients remain compatible; and
- rollback no longer requires the old layout.

## Solution review {#solution-review}

- One isolated preference control starts with platform HTML or plain Fable, not an automatic application framework.
- Coordinated booking state, external operations, routes, retries, and unknown outcomes can justify Elmish while leaving the renderer provisional.
- Required React components justify Feliz/React bindings before they justify one global Elmish model.
- Binding and native npm versions, accessibility, production output, and cleanup all need one compatibility matrix.
- Debounce generations reject old timers; request IDs reject old network completions.
- AbortController saves work, but identity matching supplies correctness.
- Visible results, pending work, empty state, safe failure, and announcements remain distinct.
- Browser controllers belong to an effect adapter, not the serializable model.
- Shared pricing receives time, rules, and inputs; it does not read target-specific environment.
- Browser pricing is a preview; the server remains authoritative.
- Decimal, dates, reflection, and serialization require cross-target checks rather than source-level confidence.
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
