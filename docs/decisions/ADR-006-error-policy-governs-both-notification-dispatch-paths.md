# ADR-006: ErrorPolicy Governs the DI-Resolved INotificationHandler Dispatch Path

**Status:** Accepted
**Date:** 2026-04-26
**Component:** ModernMediator / Mediator, IPublisher, INotificationHandler, ErrorPolicy, HandlerError

---

## Context

ModernMediator exposes notification dispatch through several overloads on the `Mediator` class. The runtime `Subscribe<T>(callback)` family (`Publish<T>(T?)`, `Publish<T>(string key, T?)`, `PublishAsync<T>`, `PublishAsyncTrue<T>`, and the callback-with-response variants `Publish<TMessage,TResponse>` and `PublishAsync<TMessage,TResponse>`) all participate in `ErrorPolicy` and raise `HandlerError` when subscribed callbacks throw. The DI-resolved `IPublisher.Publish<TNotification>` overload, which dispatches to registered `INotificationHandler<TNotification>` implementations, does not. Its implementation in v2.1 is a sequential `foreach` over resolved handlers with no try/catch, no `ErrorPolicy` consultation, and no `HandlerError` invocation.

Empirical testing against v2.1.0 confirms the asymmetry. With `ErrorPolicy = LogAndContinue` configured and a `HandlerError` subscriber wired:

| Dispatch path                                                                  | HandlerError fires? | Exception propagates from Publish? |
|--------------------------------------------------------------------------------|---------------------|-------------------------------------|
| IPublisher.Publish(notification) → DI-resolved INotificationHandler<T>.Handle  | No                  | Yes                                 |
| IMediator.Publish(notification) → runtime Subscribe<T>(callback)               | Yes                 | No                                  |

The README presents `IPublisher.Publish` and the runtime `Subscribe<T>` paths as parallel notification mechanisms, with the DI-resolved path positioned as the more idiomatic option for CQRS-style codebases. The `ErrorPolicy` and `HandlerError` documentation describes them as the cross-cutting error-handling story. A consumer reading the documentation reasonably concludes the policy and event apply to either path. In the DI case, they do not.

The integration testbed for ModernMediator surfaced this through real-world usage: a consumer codebase with sixteen `IPublisher.Publish` call sites, twelve `INotificationHandler<T>` implementations, and zero Subscribe-callback usage. Setting `ErrorPolicy = LogAndContinue` and wiring `HandlerError` had zero observable effect on the application's actual notification flow despite the wiring being correct per the documented surface.

Two positions on the appropriate response were considered.

The first treats the asymmetry as documented behavior to be preserved. The DI-resolved path's exception-propagation behavior is independent of `ErrorPolicy` by design; consumers who want policy governance use the Subscribe-callback path, and the documentation should be updated to clarify the divergence rather than the implementation changed to unify it. This position preserves byte-for-byte behavioral compatibility for any consumer who depended on exceptions propagating from `IPublisher.Publish` regardless of policy.

The second treats the asymmetry as a defect introduced by implementation legacy rather than design intent. The README presents the paths as parallel; consumers reasonably expect parallel behavior; the absence of any ADR or design note documenting the divergence indicates it was not an intentional choice. The implementation should be brought into line with the documented mental model.

A separate question concerns dispatch ordering on the DI path. The runtime async Subscribe-callback path uses `Task.WhenAll` and dispatches handlers concurrently. The DI-resolved path uses sequential `await` in a `foreach` loop. The README's general "notifications execute handlers concurrently" framing does not match the DI path's actual behavior. Two options were considered: bring the DI path into alignment by switching to `Task.WhenAll`, or pin the existing sequential behavior as canonical and document it. The first changes ordering for any consumer whose DI handlers depend on serial dispatch (for example, handler A mutates state that handler B reads); the second preserves existing ordering and limits the v2.2 scope to error-handling participation.

## Decision

The DI-resolved `IPublisher.Publish<TNotification>` path joins the participation pattern already established by the other notification dispatch overloads on `Mediator`. Exceptions thrown by `INotificationHandler<TNotification>.Handle` are caught, surfaced via `HandlerError`, and governed by `ErrorPolicy`. The semantics of `HandlerError`, including the structured event args shape and the cancellation contract, are specified in ADR-005 and apply uniformly across this path and the existing participating overloads.

Sequential dispatch ordering on the DI path is preserved. Handlers continue to be invoked one at a time in the order returned by the DI container, with each handler's task awaited before the next handler is invoked. This ADR does not change dispatch concurrency on this path.

## Rationale

The asymmetry is a defect, not a feature. The README documents `ErrorPolicy` and `HandlerError` as the cross-cutting error-handling story without qualifying the description by dispatch path. No ADR through v2.1.0 documents an intent to diverge. The most likely cause of the divergence is that the runtime callback paths wrap delegate invocation in try/catch at the invocation site (via the `InvokeHandlers` and `HandleError` helpers on `Mediator`) while the DI-resolved path directly awaits handler invocations without that wrapping, and the combination was never exercised from the DI side under a non-default policy. The first integration to test it surfaced the gap.

Preserving the asymmetry as documented behavior was rejected for two reasons. The first is that it offers no consumer benefit: a consumer who wants exceptions to propagate from `Publish` under `LogAndContinue` is asking for a contradiction in terms, since the policy name itself states the intended behavior. The second is that it codifies a documentation update that would itself be a foot-gun: a consumer reading "set `LogAndContinue` to log and continue, except when using `IPublisher.Publish` with DI-resolved handlers, in which case exceptions propagate" is more likely to misconfigure or work around the limitation than to internalize it correctly.

Sequential dispatch is preserved on the DI path because changing dispatch ordering would be a silent behavioral break with no opt-in. The existing v2.1 implementation guarantees that handler N completes before handler N+1 begins. Some consumers may have written DI handlers that depend on this ordering. For example, an audit-logging handler that reads state mutated by a state-update handler registered earlier. Switching to `Task.WhenAll` removes that guarantee with no compile-time signal. The cost of preserving sequential dispatch is documentation work to clarify that the DI path is sequential and the runtime async Subscribe-callback path is concurrent. The README is updated accordingly. Consumers who want concurrent dispatch with DI handlers can fan out within their own handler implementations, or use the Subscribe-callback async path. Future work may introduce concurrent DI dispatch as an opt-in (per-call or per-registration), but that is out of scope for v2.2.

The per-policy semantics on the sequential DI path are simpler than they would be on a concurrent path. Under `StopOnFirstError`, the dispatcher throws on the first handler exception; subsequent handlers are not invoked. There is no in-flight-handler problem to resolve because at any moment only one handler is running. Under `LogAndContinue`, each handler is wrapped in try/catch independently; failing handlers fire `HandlerError` and the dispatcher continues to the next handler. Under `ContinueAndAggregate`, exceptions are collected as each handler runs; after all handlers have completed, an `AggregateException` is thrown if any exceptions were collected, with `HandlerError` having fired once per exception during dispatch.

The default policy is `ContinueAndAggregate`, configured at field initialization on `Mediator`. The behavioral effect of this ADR on default-policy consumers needs to be stated clearly. Prior to this change, a default-policy consumer with multiple DI handlers where one threw saw the first exception propagate immediately and remaining handlers not run. After this change, all handlers run, exceptions are collected, and an `AggregateException` is thrown after dispatch completes. A consumer who wraps `Publish` in `try { } catch (Exception)` and inspects only the root cause sees substantially the same diagnostic information; a consumer who depended on later handlers not running after a failure sees different runtime behavior. The judgment is that consumers depending on the prior short-circuit behavior were depending on a defect that contradicted the policy name (`ContinueAndAggregate`), and the fix is consistent with the documented contract. The CHANGELOG entry for v2.2 calls out the behavioral change explicitly so any such consumer can audit.

The unified implementation is a single `try/catch` wrapper inside the DI-path `foreach` that delegates to the existing `HandleError` helper pattern (or an equivalent helper extended to accept the additional `HandlerType` and `HandlerInstance` properties from ADR-005). Existing inline `HandlerError` raising sites in `Mediator.cs` and `ModernMediator.cs` are consolidated through the same helper to ensure consistent population of the structured event args. The standalone `ModernMediator` class does not implement `IPublisher` and is not affected by the DI-path unification, but its callback paths raise the same `HandlerErrorEventArgs` type and must populate the structured properties consistently per ADR-005.

## Consequences

- `IPublisher.Publish<TNotification>` catches exceptions thrown by `INotificationHandler<TNotification>.Handle` invocations and routes them through `ErrorPolicy` and `HandlerError` per ADR-005. The DI-resolved path is no longer unaffected by policy configuration.

- The default `ErrorPolicy` on `Mediator` is `ContinueAndAggregate`. Existing default-policy consumers using DI handlers see a behavioral change: where the first exception previously propagated immediately and remaining handlers did not run, all handlers now run, exceptions are collected, and an `AggregateException` is thrown after dispatch completes. This is the intended fix. The CHANGELOG calls out the change explicitly.

- Existing consumers using `LogAndContinue` with DI handlers see the policy operate as documented for the first time. Exceptions are caught, surfaced via `HandlerError`, and not propagated.

- Existing consumers using `StopOnFirstError` with DI handlers see no change in propagation behavior: the first exception still propagates and remaining handlers are not invoked. They see `HandlerError` fire before propagation, which is new but additive. A consumer who never subscribed to `HandlerError` is unaffected.

- Dispatch on the DI-resolved path remains sequential. Handler N completes before handler N+1 begins. This ADR does not change dispatch concurrency on this path. Consumers requiring concurrent dispatch should use the runtime async Subscribe-callback path or fan out within their own handler implementations.

- The runtime `Subscribe<T>` async paths (`PublishAsyncTrue<T>` and the callback-with-response async variant) continue to dispatch concurrently via `Task.WhenAll`. This ADR does not change concurrency behavior on those paths.

- Under `LogAndContinue` on the DI path, the dispatcher catches each handler's exception independently and continues to the next handler. `HandlerError` fires per failure. None propagate.

- Under `StopOnFirstError` on the DI path, the dispatcher throws on the first handler exception. Subsequent handlers are not invoked. `HandlerError` fires once, before propagation.

- Under `ContinueAndAggregate` on the DI path, the dispatcher collects exceptions as handlers run sequentially. After all handlers complete, an `AggregateException` is thrown if any exceptions were collected. `HandlerError` fires once per exception during dispatch.

- If the publish token is cancelled mid-dispatch on the DI path, the currently-executing handler observes the token cooperatively and throws `OperationCanceledException`. Per ADR-005, this is treated as cooperative cancellation when the publish token's `IsCancellationRequested` is true: the exception bypasses `HandlerError` and `ErrorPolicy` and propagates from `Publish`. Already-collected handler exceptions from prior handlers under `ContinueAndAggregate` are discarded from any returned aggregate; the operation propagates `OperationCanceledException` rather than throwing a partial aggregate. Already-fired `HandlerError` invocations from prior handlers stand: they fired during dispatch and are not retroactively suppressed.

- The DI-resolved path and the runtime Subscribe-callback paths use a shared error-handling implementation. The `HandleError` helper pattern on `Mediator` (and the equivalent on `ModernMediator` for its callback paths) is the single point of `HandlerErrorEventArgs` construction and `HandlerError` invocation. Consolidating existing inline raising sites through this helper is part of the v2.2 work.

- `HandlerErrorEventArgs.HandlerType` on the DI-resolved path is the concrete handler type from DI resolution. `HandlerInstance` is the resolved instance. These follow ADR-005's specification.

- The standalone `ModernMediator` pub/sub class does not implement `IPublisher` and is not directly affected by the DI-path unification. Its existing callback-dispatch paths raise the same `HandlerErrorEventArgs` type as `Mediator` and are updated to populate the structured `HandlerType` and `HandlerInstance` properties per ADR-005 for consistency across the public event surface.

- The README's pub/sub error-handling section is updated to state explicitly that `ErrorPolicy` and `HandlerError` govern `IPublisher.Publish` and the runtime `Subscribe<T>` paths uniformly. The README also clarifies that the DI-resolved path dispatches handlers sequentially and that the runtime async Subscribe-callback path dispatches concurrently. The CHANGELOG entry for v2.2 calls out the prior-behavior divergence on the DI path so consumers reading it understand why their existing wiring may have been silently uncovered before this release.

- This ADR depends on ADR-005 for the underlying event semantics, the structured event args shape, and the cancellation contract. ADR-005 is the authoritative reference for those concerns; this ADR is the authoritative reference for the DI-resolved path's participation in them and for the per-policy dispatch behaviors on that path.
