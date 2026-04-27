# ADR-005: HandlerError Event Semantics

**Status:** Accepted
**Date:** 2026-04-26
**Component:** ModernMediator / IMediator, IPublisher, IModernMediator, HandlerError, ErrorPolicy

---

## Context

ModernMediator raises a `HandlerError` event when a notification handler throws an exception. The event is exposed on both `IMediator` (implemented by the `Mediator` class) and `IModernMediator` (implemented by the standalone `ModernMediator` pub/sub class), and both implementations share the same `HandlerErrorEventArgs` type. Through v2.1, the semantics of when this event fires, what it carries, and how it interacts with cancellation were not pinned down authoritatively. The runtime `Subscribe<T>` callback path raises the event and is governed by `ErrorPolicy`; the DI-resolved `INotificationHandler<T>` path on `IPublisher.Publish<TNotification>` does neither. The `HandlerErrorEventArgs` shape (`Exception`, `Message`, `MessageType`) does not carry handler identification, so consumers cannot reliably filter or attribute errors by source handler programmatically.

ADR-006 unifies error handling across notification dispatch paths, which forces the underlying event semantics to be specified once and authoritatively. This ADR is that specification.

Three questions are resolved here.

The first concerns when `HandlerError` fires relative to `ErrorPolicy`. One position is that the event is universal observation: it fires for every handler exception under every policy, and the policy governs only what happens after the event fires (swallow, propagate, aggregate). A consumer wiring a logging subscriber relies on a single observation channel that does not depend on policy configuration. The competing position is that the event is the surfacing channel for `LogAndContinue` specifically: it fires only when the policy chooses not to propagate, and other policies surface exceptions through their own canonical mechanisms (propagation from `Publish`, the `AggregateException` collected by `ContinueAndAggregate`). Each policy then has exactly one error-surfacing path with no overlap.

The universal-observation position carries the cost that under `StopOnFirstError` and `ContinueAndAggregate`, the same exception is observable through both the event and the policy mechanism, so a consumer who logs in both places double-logs. The policy-coupled position carries the cost that changing the configured policy can silently disable a consumer's logging subscriber with no compile-time signal, and that an event named `HandlerError` not firing on a handler error is counterintuitive on first read.

The second question concerns what `HandlerErrorEventArgs` exposes. A minimal surface populates `Exception`, `Message`, and `MessageType` only: the current v2.1 shape. A structured surface adds `HandlerType` of type `Type` and `HandlerInstance` of type `object?` populated symmetrically across all dispatch paths. A path-discriminated surface exposes a tagged union distinguishing DI-resolved handlers from Subscribe-callback handlers, requiring consumers to pattern-match on the source.

The minimal surface is cheapest to commit to but forecloses programmatic filtering. A consumer who wants to suppress errors from a specific handler type writes substring matching against runtime type information not directly exposed on the args. The structured surface supports `e.HandlerType == typeof(X)` filtering directly but introduces a wrinkle on the Subscribe path: lambda subscriptions produce compiler-generated closure types, which are honest but ugly in logs unless the dispatcher unwraps them. The path-discriminated surface honors the genuine difference between dispatch paths but over-models for what is in practice a logging or filtering hook.

The third question concerns `OperationCanceledException`. One position treats cancellation as just another exception: it flows through `HandlerError`, is governed by `ErrorPolicy`, and under `LogAndContinue` is swallowed alongside any other handler error. The other position treats cooperative cancellation as distinct from a handler fault: an `OperationCanceledException` raised while the publish token is cancelled bypasses `HandlerError` and bypasses policy entirely, propagating from `Publish` regardless of configuration.

The first position is consistent with treating all exceptions uniformly but produces a severe foot-gun under `LogAndContinue`: a consumer who cancels their publish token sees `Publish` return successfully, with no signal that handlers were aborted. Under `ContinueAndAggregate`, cancellation gets buried inside an `AggregateException` alongside actual handler faults, destroying the consumer's ability to distinguish "we stopped early because we were cancelled" from "stuff broke." Under `StopOnFirstError`, the propagation behavior is closer to correct, but `HandlerError` still fires for every cancellation, producing log noise on what is normally cooperative control flow. The second position aligns with universal .NET conventions: `Task.WhenAll`, `HttpClient`, ASP.NET Core, and every well-designed async API treat `OperationCanceledException` raised against the caller's cancelled token as special and propagate it regardless of error-handling configuration.

## Decision

`HandlerError` is a universal observation channel. The event fires for every handler exception under every `ErrorPolicy`. The policy governs control flow (propagation, aggregation, or suppression) but does not gate event invocation.

`HandlerErrorEventArgs` is extended to a structured symmetric surface populated identically across all dispatch paths that raise the event. The existing properties `Exception`, `Message`, and `MessageType` are preserved. Two additional properties are added: `HandlerType` of type `Type` and `HandlerInstance` of type `object?`. On the DI-resolved path, `HandlerType` is the concrete handler type from DI resolution and `HandlerInstance` is the resolved instance. On Subscribe-callback paths, `HandlerType` is `Method.DeclaringType` with compiler-generated closure types unwrapped to the enclosing user type, and `HandlerInstance` is `Delegate.Target`. For static delegate subscriptions, `HandlerInstance` is null.

`OperationCanceledException` is treated as cooperative cancellation, not a handler error, when the publish-path `CancellationToken.IsCancellationRequested` is true at the time of catch. Cooperative cancellation does not fire `HandlerError` and does not flow through `ErrorPolicy`. It propagates from `Publish` unconditionally.

## Rationale

The universal-observation model serves the consumer mental model better than the policy-coupled alternative. The pain point that motivated this work in the first place was a consumer wiring a documented error-handling pattern and getting silent failure because the documentation and the code path diverged. Designing the event semantics around "the event sometimes fires depending on policy" reproduces that failure mode in a different shape: a consumer who configures a subscriber, then later changes policy, sees their subscriber silently stop firing with no compile-time or runtime signal. Universal observation is robust to policy changes by design.

The double-surfacing concern under universal observation is real but narrow. Under `StopOnFirstError` and `ContinueAndAggregate`, the same exception is observable through both `HandlerError` and the policy's surfacing mechanism, so a consumer who logs in both places will log twice. The mitigation is documentation rather than design: consumers are directed to subscribe to `HandlerError` for observation concerns (logging, metrics, diagnostics) and to use the policy mechanism for control flow concerns (catching the propagating exception, inspecting the aggregate). A consumer who follows this rule never double-logs. The general .NET convention (`DiagnosticListener` events firing regardless of whether downstream code catches the underlying exception, `ILogger` calls not gated on rethrow decisions) supports this separation.

The structured event args shape supports the realistic consumer scenario. A plugin host that wants to suppress noise from a specific handler implementation writes `if (e.HandlerType == typeof(LegacyHandler)) return;` rather than substring matching against runtime type lookups. A diagnostics subscriber that wants to attribute metrics by handler type uses `HandlerType.FullName` directly. The `HandlerInstance` field supports rarer but legitimate scenarios where the consumer needs the resolved object. For example, a handler that exposes a correlation ID via a marker interface, retrievable as `(e.HandlerInstance as ICorrelatable)?.CorrelationId`. The path-discriminated alternative was rejected because the genuine difference between dispatch paths is not load-bearing for consumers handling errors: they care which code threw, not which mechanism produced the error.

The closure-type wrinkle on Subscribe-callback paths is handled by detecting `[CompilerGenerated]` on the declaring type and walking to the enclosing user type. This produces clean identification for lambda subscriptions in the common case where the lambda is declared inside a user-authored class. Named-method subscriptions produce clean identification directly without unwrapping. Static delegate subscriptions produce a null `HandlerInstance` because there is no instance to expose.

The cancellation contract aligns with universal .NET conventions and avoids three concrete foot-guns. Under `LogAndContinue` without the carve-out, a cancelled publish would return as if successful, which contradicts the explicit signal of token cancellation. Under `ContinueAndAggregate` without the carve-out, cancellation would be buried in an aggregate alongside genuine faults, which destroys the diagnostic value of the aggregate. Under `StopOnFirstError` without the carve-out, every cancelled publish would fire `HandlerError`, polluting logs with what is normally routine control flow. Treating cooperative cancellation as distinct from a handler fault eliminates all three concerns at the cost of a single conditional in the dispatcher's catch clause.

The discriminator between cooperative cancellation and a handler-thrown `OperationCanceledException` carrying an unrelated token uses a single check: `publishToken.IsCancellationRequested` at the time of catch. If the publish token is cancelled, any `OperationCanceledException` arriving from a handler is treated as cooperative cancellation regardless of which token the exception carries. This handles linked-token cancellation correctly. A handler that creates a linked source via `CancellationTokenSource.CreateLinkedTokenSource(publishToken, ...)` will throw an `OperationCanceledException` carrying the linked token rather than the publish token, but `publishToken.IsCancellationRequested` is still true. The single-check approach matches the pattern already in use elsewhere in the codebase (Mediator.cs PublishAsyncTrue) and matches ASP.NET Core's request-abortion handling. The over-counting case (a handler throws `OperationCanceledException` for unrelated reasons coincidentally during a publish whose token happens to be cancelled) is rare enough that the diagnostic loss is acceptable.

An `OperationCanceledException` thrown while the publish token is not cancelled is treated as a handler fault. It fires `HandlerError` and is governed by policy. This is the correct fallback when no signal of cooperative cancellation is present.

## Consequences

- `HandlerError` fires for every handler exception under every `ErrorPolicy`. Consumers may rely on the event as a single observation channel that does not depend on policy configuration.

- Under `StopOnFirstError`, `HandlerError` fires before the exception propagates from `Publish`. Ordering is observable and committed: consumers can rely on the event having been raised by the time the exception surfaces at the call site.

- Under `ContinueAndAggregate`, `HandlerError` fires once per failing handler during dispatch, and the same exceptions appear in the `AggregateException` thrown after dispatch completes. Consumers are expected to choose one mechanism per concern: subscribe to `HandlerError` for observation, inspect the aggregate for control flow, do not handle the same exception in both paths.

- `HandlerErrorEventArgs` carries `Exception`, `Message`, `MessageType`, `HandlerType`, and `HandlerInstance`. The first three are preserved from the v2.1 shape; the last two are added in v2.2. All five are populated symmetrically on every dispatch path that raises the event, including paths on the standalone `ModernMediator` pub/sub class.

- Adding properties to `HandlerErrorEventArgs` is a non-breaking source and binary change for consumers. Existing subscribers continue to work without modification.

- On Subscribe-callback paths, `HandlerType` for lambda subscriptions is the enclosing user type, not the compiler-generated closure type. The dispatcher detects `[CompilerGenerated]` on the declaring type and walks to the enclosing type. Named-method subscriptions produce `Method.DeclaringType` directly.

- `HandlerInstance` is the resolved DI instance on the DI-resolved path and `Delegate.Target` on Subscribe-callback paths. For static delegate subscriptions, the property is null.

- An `OperationCanceledException` is treated as cooperative cancellation when the publish token's `IsCancellationRequested` is true at the time of catch. Cooperative cancellation does not fire `HandlerError` and does not flow through `ErrorPolicy`. It propagates from `Publish` regardless of policy.

- An `OperationCanceledException` thrown while the publish token is not cancelled is treated as a handler fault. It fires `HandlerError` and is governed by `ErrorPolicy`.

- `Publish` performs `publishToken.ThrowIfCancellationRequested()` at entry. If the publish token is already cancelled before any handler is invoked, `OperationCanceledException` propagates without firing `HandlerError`. This matches the existing entry-guard behavior on the publish overloads.

- A subscriber to `HandlerError` that itself throws does not break dispatch. The dispatcher contains exceptions raised by `HandlerError` subscribers and does not propagate them. Subscriber-thrown exceptions are logged via the framework's `ILogger` infrastructure where available.

- These semantics apply to both the `Mediator` class and the standalone `ModernMediator` class. Both classes raise `HandlerError` against the same `HandlerErrorEventArgs` type and share the cancellation contract specified here.

- These semantics are foundational. ADR-006 references this ADR as the authoritative specification for `HandlerError` behavior across notification dispatch paths. Future work that extends error-handling semantics to additional pipeline mechanisms (for example, send-path behaviors or stream dispatch) is expected to reference this ADR rather than re-litigate the underlying questions.
