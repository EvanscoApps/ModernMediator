# ADR-009: Dispatcher overload mismatch helpful exception

**Status:** Accepted
**Date:** 2026-04-29
**Component:** ModernMediator

---

## Context

ModernMediator's dispatcher exposes two parallel paths: `Send` for handlers implementing `IRequestHandler<TRequest, TResponse>` (Task-returning) and `SendAsync` for handlers implementing `IValueTaskRequestHandler<TRequest, TResponse>` (ValueTask-returning). The two interfaces are independent registrations in DI. A single request type can have a handler under either interface, but a given dispatcher path only resolves the matching interface: `Send` queries `IRequestHandler<,>` and never inspects `IValueTaskRequestHandler<,>`, and `SendAsync` does the inverse.

When a consumer registers a handler under one interface and invokes the dispatcher via the other path, the dispatcher's primary lookup returns null and the wrapper throws `InvalidOperationException` with the message `"No handler registered for request type {Name}. Register a handler implementing IRequestHandler<{Name}, {Response}> using AddModernMediator() with assembly scanning or manual registration."` (or the symmetric ValueTask variant). The message is technically accurate; the requested interface has no registration. It is also misleading. A handler is registered for the request type, and the user's mistake is dispatch-method choice rather than missing registration. PidStudio reported real debugging time lost to this misdirection during the Phase 4d `SaveSelectionAsStencilCommand` work, where a handler had been authored as `IValueTaskRequestHandler` but the call site invoked `Send`. The investigation pattern (re-confirming registration, re-checking assembly scanning, re-running scaffolding generators) was driven by the message's framing of the problem as a registration failure.

The investigation in advance of this ADR confirmed that the runtime fix is straightforward at the existing throw site. Both wrapper classes (`RequestHandlerWrapperImpl` and `ValueTaskHandlerWrapperImpl`) have the `IServiceProvider` in scope and the request type as a generic parameter; a secondary lookup against the alternate interface adds a single `GetService` call before the throw. The investigation also examined whether the same condition could be detected by the source generator at compile time and concluded that the generator does not currently model dispatch call sites; a compile-time diagnostic would require new infrastructure beyond the scope of v2.2.

## Decision

When a dispatcher path's primary handler interface returns no registration, the wrapper performs a secondary `IServiceProvider.GetService` call against the alternate handler interface before throwing. If a registration is found, the thrown `InvalidOperationException` carries the bracketed prefix `[MM201]` and a guiding message that names both interfaces and suggests the correct dispatch method. If no registration is found in either place, the original "No handler registered" message is thrown unchanged, without the MM201 prefix.

Concretely, in `RequestHandlerWrapperImpl<TRequest, TResponse>.Handle`, after the primary `IRequestHandler<TRequest, TResponse>` lookup returns null, the wrapper queries `IValueTaskRequestHandler<TRequest, TResponse>`. If non-null, the exception message reads:

> `[MM201] No IRequestHandler<{TRequest}, {TResponse}> is registered, but an IValueTaskRequestHandler<{TRequest}, {TResponse}> is registered. Did you mean to call SendAsync instead of Send?`

`ValueTaskHandlerWrapperImpl<TRequest, TResponse>.Handle` performs the symmetric check: secondary lookup against `IRequestHandler<,>`, and if non-null, the message suggests `Send` instead of `SendAsync`.

The new runtime code MM201 is declared as `public const string DispatcherOverloadMismatchCode` in `ModernMediator.Generators.DiagnosticDescriptors`, alongside the existing source-generator descriptors. Per ADR-008, MM2xx is the slot reserved for runtime codes.

## Rationale

Two alternative shapes for the fix were considered and rejected.

The first was to change the original "No handler registered" message unconditionally to mention both interfaces, e.g., `"No IRequestHandler or IValueTaskRequestHandler is registered for request type X"`. This would have eliminated the secondary lookup entirely and produced one message for both the genuine no-handler case and the overload-mismatch case. It was rejected because it adds noise to the genuine case (which is the more common one) for the benefit of the rarer case. A user with no handler registered does not need to be told about the alternate interface; they need to register one of the two interfaces, and the existing message points to that.

The second was to introduce a new exception type, `HandlerOverloadMismatchException`, distinct from `InvalidOperationException`. This would have given consumers a strongly-typed catch for the mismatch case. It was rejected for v2.2 because adding a new public exception type is API surface that warrants its own decision: subclass relationship, message contract, serializability, equality semantics, and downstream catch-block compatibility. The `InvalidOperationException` plus guiding message keeps the change additive only. Existing catch blocks for `InvalidOperationException` continue to function. If a strongly-typed exception is later determined to be valuable, it can be introduced as a subclass of `InvalidOperationException` without breaking the v2.2 contract.

The MM201 prefix in brackets is chosen because the bracketed prefix pattern is visually distinct from prose, easy to grep, and consistent with the way Roslyn surfaces compile-time diagnostic codes (e.g., `error CS0123:`). A user pasting an exception message into a bug report or search engine produces a query that pinpoints the condition.

The secondary lookup is performed only on the error path; the cost is one additional `IServiceProvider.GetService` call when no handler is found, which is already an exceptional condition. Successful dispatches incur no overhead.

The decision to defer source-generator detection of the same condition is driven by scope. The generator currently inspects handler declarations and emits diagnostics about handler structure (return type mismatch, abstract handlers, missing matching request types). Detecting an overload mismatch would require modeling the dispatcher's call sites, which is a different category of analysis. A future work item will introduce that analysis under the MM0xx range; the runtime fix in this ADR closes the user-facing problem without waiting for the larger generator change.

## Consequences

The two wrapper classes acquire a single conditional branch on the error path. The successful dispatch path is unchanged. No public API surface changes. No existing exception type changes; consumers catching `InvalidOperationException` continue to receive the same exception type with a more informative message in the mismatch case.

The exception message is part of the debugging contract but not part of the API contract. Future revisions to the message text are expected (e.g., to mention assembly scanning, or to reference documentation). The MM201 prefix is the stable identifier; consumers who programmatically detect the mismatch should match on the prefix rather than on the prose.

A separate work item, tracked in the v3.0 backlog, will add a compile-time diagnostic in the MM0xx range for the same condition, detecting the mismatch when both the handler and the call site are visible in the same compilation. The runtime check remains in place after the compile-time check ships, because the runtime check covers cases where the call site and handler live in different assemblies and the compile-time check cannot fire.

Documentation in the v2.2 release notes points users at MM201 as the searchable identifier for this category of problem.
