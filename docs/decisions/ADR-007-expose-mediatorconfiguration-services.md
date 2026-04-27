# ADR-007: Expose MediatorConfiguration.Services for satellite package integrations

**Status:** Accepted
**Date:** 2026-04-27
**Component:** ModernMediator, ModernMediator.FluentValidation

---

## Context

ModernMediator's `MediatorConfiguration` exposes a fluent surface for registering pipeline behaviors at specific positions: `AddLogging`, `AddTimeout`, `AddIdempotency`, `AddRetry`, `AddAudit`, `AddCircuitBreaker`, and the open-generic `AddOpenBehavior`. Each call writes immediately to the underlying `IServiceCollection` and lands at the current point in the registration sequence. Because the dispatcher resolves `IPipelineBehavior<TRequest, TResponse>` instances in DI registration order and folds them forward (index zero outermost), textual call order in the configuration lambda equals pipeline order from outermost to innermost.

Satellite packages that contribute pipeline behaviors have historically been forced into a different shape. The FluentValidation integration ships an `AddModernMediatorValidation` extension on `IServiceCollection`, not on `MediatorConfiguration`. Callers register validation by calling `services.AddModernMediator(config => { ... })` with their logging, timeout, and other behaviors inside the lambda, then calling `services.AddModernMediatorValidation(typeof(Program).Assembly)` afterward. This places `ValidationBehavior<,>` after every behavior registered inside the `AddModernMediator` lambda, regardless of the caller's intended pipeline position. A user who wants `Logging` then `Validation` then `Timeout` then `Handler` cannot express that ordering through the convenience method. The workaround is to drop to `config.AddOpenBehavior(typeof(ValidationBehavior<,>))` and register validators manually via `services.AddTransient<IValidator<T>, TValidator>()`, bypassing the package's discovery helper. The advanced sample in the repository documents this workaround explicitly, which is a signal that the public surface is incomplete.

The root cause is access. `MediatorConfiguration.Services` is declared `private`, so extension methods authored outside the core assembly cannot reach it. Each satellite package must either duplicate the configuration's responsibility or accept tail-end behavior placement.

## Decision

`MediatorConfiguration.Services` is changed from `private` to `public`. The property's contract is preserved verbatim: it returns the captured `IServiceCollection` when the configuration was constructed via the `IServiceCollection`-accepting internal constructor, and throws `InvalidOperationException` when the configuration was constructed via the parameterless constructor used in source-generated registration scenarios. XML documentation describes the intended use case (satellite package integrations) and the throwing contract.

Satellite packages may now expose `MediatorConfiguration` extension methods that perform direct registration via `config.Services.Add(...)`, allowing callers to interleave package-contributed behaviors with core behaviors at any point in the configuration sequence.

## Rationale

The visibility change is the minimum surface area necessary to unblock satellite package integration patterns. Three alternatives were considered and rejected.

The first alternative was an internal-only API exposed via `InternalsVisibleTo` to specific first-party satellite packages. This would have closed the gap for the in-tree packages but left third-party extension authors without a path. ModernMediator's positioning is open ecosystem, and a pattern that works only for in-tree code contradicts that positioning.

The second alternative was a deferred-behavior model inside `MediatorConfiguration` that buffered registrations into an internal list and flushed them to the service collection when `AddModernMediator` returned. This would have allowed a `MediatorConfiguration` extension surface without exposing the underlying collection. The cost is significant: the entire existing implementation registers immediately, so a deferred model would require reworking every existing extension method, breaking the invariant that `Services.Add` ordering equals pipeline ordering, and introducing a new failure mode (configuration referenced after flush). The benefit is encapsulation of a property that is already narrowly scoped to behavior-and-supporting-service registration. The trade did not justify the disruption.

The third alternative was leaving the gap open and documenting the workaround. This was rejected on the project's quality bar: known gaps are fixed before the release that would otherwise expose them, and v2.2 has not yet shipped. Adopters of v2.2 should encounter the cleaner API on first contact rather than the workaround documented in the advanced sample.

The chosen approach is mechanically the smallest change that resolves the gap. No behavioral semantics change. No existing caller is affected, because no caller outside `MediatorConfiguration` itself touched the property under its prior `private` access. The throw-on-source-gen-mode contract is documented rather than introduced; it was already the runtime behavior. The only new commitment is that future versions will not silently change the property's null-handling semantics, which is appropriate for a property used by external extension authors.

## Consequences

Satellite packages and third-party extensions can author `MediatorConfiguration` extension methods that register pipeline behaviors at the caller's chosen position in the configuration sequence. The FluentValidation package adds such an overload in v2.2 alongside this change.

The `Services` property's null-throw behavior is now part of the public contract. Code paths that construct `MediatorConfiguration` via the parameterless constructor (source-generated registration) and then access `Services` will throw `InvalidOperationException` with a message directing the caller to `AddModernMediator()`. This was already the behavior internally; documenting it as a public contract means future changes to the source-generated registration path must preserve a working `Services` accessor or accept the breaking change explicitly.

No existing public API is changed, removed, or renamed. The change is purely additive at the access-modifier level. Existing consumers compile and run without modification.
