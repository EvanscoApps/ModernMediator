# Changelog

All notable changes to ModernMediator will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.2.0] - YYYY-MM-DD

### Added

- **ISubscriberExceptionSink** (new public interface, `ModernMediator` namespace):
  observability extension point for `HandlerError` subscriber-thrown exceptions.
  Consumers register a custom implementation via dependency injection to route
  contained subscriber exceptions to a preferred observability system (structured
  logging, OpenTelemetry, Application Insights, or any custom sink). The default
  `LoggerSubscriberExceptionSink` (internal) routes through
  `Microsoft.Extensions.Logging.ILogger`.
- **`HandlerErrorEventArgs.HandlerType`** (new property, `Type?`): the concrete
  handler type that threw. On the DI-resolved `INotificationHandler<T>` path, the
  resolved handler class. On the `Subscribe`-callback path, `Method.DeclaringType`
  with compiler-generated closure types unwrapped to the enclosing user type.
- **`HandlerErrorEventArgs.HandlerInstance`** (new property, `object?`): the handler
  instance that threw. On the DI-resolved path, the resolved DI handler instance.
  On the `Subscribe`-callback path, `Delegate.Target`. Null for static delegate
  subscriptions.
- **ADR-005** documenting `HandlerError` event semantics and the cancellation
  contract; **ADR-006** documenting the DI-resolved `INotificationHandler<T>`
  dispatch path's participation in `ErrorPolicy` and `HandlerError`. Both ADRs in
  `docs/decisions/`.
- README files bundled in seven satellite NuGet packages
  (`ModernMediator.Generators`, `ModernMediator.AspNetCore`,
  `ModernMediator.AspNetCore.Generators`, `ModernMediator.FluentValidation`,
  `ModernMediator.Audit.Serilog`, `ModernMediator.Audit.EntityFramework`,
  `ModernMediator.Idempotency.EntityFramework`). Previously only the core
  `ModernMediator` package shipped a README to NuGet.

### Changed

- `HandlerError` now fires for every handler exception under every `ErrorPolicy`
  on both notification dispatch paths (the DI-resolved `INotificationHandler<T>`
  path via `IPublisher.Publish`, and the runtime `Subscribe<T>` /
  `SubscribeAsync<T>` callback paths). Previously `HandlerError` fired only on the
  `Subscribe`-callback path; the DI-resolved path was unaffected by `ErrorPolicy`
  and `HandlerError` configuration. See ADR-006.
- `ErrorPolicy` now governs the DI-resolved `IPublisher.Publish<TNotification>`
  path with the same semantics as the `Subscribe`-callback paths. Under
  `ContinueAndAggregate` (the default), all DI-registered handlers run, exceptions
  are collected, and an `AggregateException` is thrown after dispatch completes.
  Under `LogAndContinue`, exceptions are surfaced via `HandlerError` and dispatch
  continues. Under `StopOnFirstError`, the first exception fires `HandlerError`
  and propagates. See ADR-006.
- Asynchronous notification dispatch (`PublishAsyncTrue` and the async
  callback-with-response path) now fires `HandlerError` per failing handler with
  full `HandlerType` and `HandlerInstance` attribution. Previously a single
  `HandlerError` event fired with an `AggregateException` carrying the per-handler
  exceptions and null attribution. The new behavior matches the universal
  observation model in ADR-005.
- `HandlerError` subscribers that themselves throw no longer break dispatch.
  Subscriber-thrown exceptions are now contained at the dispatch site and routed
  through `ISubscriberExceptionSink`.

### Fixed

- Cooperative cancellation contract pinned: an `OperationCanceledException` raised
  by a handler while the publish token's `IsCancellationRequested` is true bypasses
  `HandlerError` and `ErrorPolicy` entirely, propagating from `Publish`
  unconditionally. Previously the cancellation contract was inconsistent across
  dispatch paths. The discriminator uses the publish token's
  `IsCancellationRequested` at the time of catch, which handles linked-token
  cancellation correctly. See ADR-005.

### Behavioral changes

For consumers using the DI-resolved `IPublisher.Publish<TNotification>` path with
multiple `INotificationHandler<T>` implementations, behavior under the default
`ContinueAndAggregate` policy has shifted. Previously the first thrown exception
propagated immediately and remaining handlers did not run. After this release,
all handlers run, exceptions are collected, and `AggregateException` is thrown
after dispatch. Consumers who depended on the prior short-circuit behavior were
depending on a defect that contradicted the policy name (`ContinueAndAggregate`).
The fix is consistent with the documented contract; the CHANGELOG calls out the
change so any affected consumer can audit.

Consumers using `LogAndContinue` with DI-resolved handlers see exceptions caught
and surfaced via `HandlerError` instead of propagating. Previously the policy was
effectively a no-op on the DI-resolved path. The fix brings the documented
behavior into effect on both dispatch paths.

Consumers using `StopOnFirstError` with DI-resolved handlers see no change in
propagation behavior. They see `HandlerError` fire before propagation, which is
new but additive. A consumer who never subscribed to `HandlerError` is
unaffected.

---

## [2.1.0] - 2026-03-21

### Added
- **AuditBehavior**: pipeline behavior that captures per-request audit records
  (RequestTypeName, UserId, UserName, CorrelationId, Duration, Succeeded, FailureReason,
  SerializedPayload, Timestamp, TraceId) and dispatches them to any `IAuditWriter`
  implementation. Registered via `config.AddAudit<TWriter>()` inside `AddModernMediator`,
  where `TWriter` is the `IAuditWriter` implementation (`SerilogAuditWriter`,
  `EfCoreAuditWriter`, or a custom implementation).
- **`[NoAudit]` attribute**: opt individual request types out of audit recording
- **AuditChannelDrainer** and **AuditHostedServiceExtensions**: fire-and-forget async
  drain loop via `System.Threading.Channels`, registered automatically when
  `config.AddAudit<TWriter>()` is configured for channel-based dispatch (the default
  mode per ADR-001).
- **IdempotencyBehavior**: deduplicates requests marked with `[Idempotent]` by key
  and TTL using any `IIdempotencyStore` implementation. Registered via
  `config.AddIdempotency(Action<IdempotencyOptions>?)` on `MediatorConfiguration`
  with mode-based store selection (`InMemory` default, `Distributed` for
  `IDistributedCache`-backed; for `EfCoreIdempotencyStore`, register the store explicitly
  via `services.AddSingleton<IIdempotencyStore, EfCoreIdempotencyStore>()` before
  calling `AddIdempotency()`).
- **InMemoryIdempotencyStore**: built-in in-process idempotency store
- **DistributedIdempotencyStore**: `IDistributedCache`-backed idempotency store
- **CircuitBreakerBehavior**: per-request-type circuit breaker enforced via
  `[CircuitBreaker]` attribute; open circuit throws `CircuitBreakerOpenException`;
  registered via `AddCircuitBreaker()`
- **RetryBehavior**: automatic retry with configurable count and delay strategy
  (None, Fixed, Linear, Exponential) via `[Retry]` attribute;
  registered via `AddRetry()`
- **ICurrentUserAccessor**: abstraction for resolving the current user identity
  within pipeline behaviors
- **HttpContextCurrentUserAccessor** (`ModernMediator.AspNetCore`): `IHttpContextAccessor`-
  backed implementation exposing `UserId` (NameIdentifier claim) and `UserName`
  (Name claim). Auto-registered when the audit pipeline opts in via
  `config.AddAudit<TWriter>(o => o.UseHttpContextIdentity(services))`, or registered
  manually via `services.AddSingleton<ICurrentUserAccessor, HttpContextCurrentUserAccessor>()`.
- **AspNetCoreAuditExtensions** (`ModernMediator.AspNetCore`): provides the
  `UseHttpContextIdentity` audit option that auto-registers `HttpContextCurrentUserAccessor`
  when invoked inside `config.AddAudit<TWriter>(o => o.UseHttpContextIdentity(services))`.
- **ModernMediator.Audit.Serilog** (new package): `SerilogAuditWriter`, a structured
  Serilog sink for audit records; logs at Information on success, Warning on failure
- **ModernMediator.Audit.EntityFramework** (new package): `EfCoreAuditWriter` and
  `AuditDbContext`, which persist audit records to any EF Core-supported database
- **ModernMediator.Idempotency.EntityFramework** (new package): `EfCoreIdempotencyStore`
  and `IdempotencyDbContext`, which persist idempotency entries to any EF Core-supported
  database with fingerprint-keyed deduplication and TTL expiry
- **`AddModernMediatorAspNetCore()`**: now registers `IHttpContextAccessor` automatically;
  previously a no-op placeholder
- **`AuditRecord.TraceId`**: automatically populated from
  `Activity.Current` when an active distributed trace is present;
  null when no trace context exists

### Changed
- Total publishable packages increased from four to seven

---

## [2.0.0] - 2026-03-07

### Added
- **Compile-time diagnostics** (MM001–MM008, MM100): nine diagnostic rules enforced
  at build time with positive and negative test coverage for each
- **LoggingBehavior**: built-in request/response logging with configurable levels and
  optional payload serialization; registered via `AddLogging()`
- **Result<T> pattern**: `readonly struct` with implicit conversions, `Map`,
  `GetValueOrDefault`, and `ResultExtensions`
- **ModernMediator.AspNetCore**: Minimal API endpoint generation via `[Endpoint]`
  attribute and `MapMediatorEndpoints()`; MM200 diagnostic for invalid HTTP methods
- **OpenTelemetry observability**: `MediatorTelemetry` static class with
  `ActivitySource`, `Meter`, `RequestCounter`, and `RequestDuration` emitted into
  generated dispatch code; registered via `AddTelemetry()`
- **TimeoutBehavior**: per-request timeout enforcement via `[Timeout(ms)]` attribute
  using `CancellationTokenSource.CreateLinkedTokenSource`; registered via `AddTimeout()`
- **ModernMediator.FluentValidation**: `ValidationBehavior` pipeline integration;
  registered via `AddModernMediatorValidation()`
- **Interface segregation**: `ISender`, `IPublisher`, and `IStreamer` interfaces;
  `IMediator` composes all three; all three registered in DI as forwarding aliases
- **DI-based notification dispatch**: `IPublisher.Publish<TNotification>` resolves
  `INotificationHandler<T>` from DI and dispatches sequentially
- **13 cross-platform samples**: Console, WPF, MAUI, Avalonia, Blazor Server/WASM,
  Worker Service, WebApi, and WebApi.Advanced
- **dotnet new template**: `modernmediator` shortname scaffolds a pre-wired starter API
- **NuGet metadata**: all four packable packages include MIT license expression,
  RepositoryUrl, package descriptions, and tags

### Performance
- Typed wrapper dispatch (`RequestHandlerWrapperImpl<TRequest, TResponse>`)
  with index-based recursion. Replaces all reflection in the `Send` hot path
- **Closure elimination**: `RequestHandlerDelegate<TRequest, TResponse>(TRequest, CancellationToken)`
  passes request and token explicitly instead of capturing via closure, eliminating
  one allocation per pipeline step on every dispatch
- **Full ValueTask pipeline**: `IValueTaskRequestHandler`, `IValueTaskPipelineBehavior`,
  and `ISender.SendAsync`. Zero-allocation path when handler completes synchronously
- Lower allocations than MediatR on every benchmark; significantly faster cold start
  and Publish; see [BENCHMARKS.md](BENCHMARKS.md) for full three-way results
  including martinothamar/Mediator
- `CreateStream` reflection eliminated via `StreamHandlerWrapperImpl<TRequest, TResponse>`
- `TryHandleException` reflection eliminated via compiled delegate cache
- `TimeoutBehavior` attribute lookup amortized via `ConcurrentDictionary<Type, int?>` cache

### Fixed
- MM002, MM003, MM100 were defined but never reported in v1.0; now wired and tested
- EndpointGenerator duplicate route registration under incremental pipeline cache
  boundaries (string-based deduplication guard)
- Open generic behaviors incorrectly included in generated DI registration code

### Breaking Changes from v1.0
- **`RequestHandlerDelegate` signature changed**: from zero-parameter `RequestHandlerDelegate<TResponse>()`
  to two-parameter `RequestHandlerDelegate<TRequest, TResponse>(TRequest, CancellationToken)`.
  Custom `IPipelineBehavior` implementations must update `next()` calls to `next(request, cancellationToken)`.
- **`IPipelineBehavior<TRequest, TResponse>` variance removed**: `TRequest` is no longer
  declared as `in` (contravariant) due to the delegate change. This rarely affects user code.
- `ISender`, `IPublisher`, `IStreamer` are new interfaces; existing code injecting
  `IMediator` is unaffected

---

## [0.2.2-alpha] - 2026-01-06

### Added

- **Avalonia dispatcher support**: Added `AvaloniaDispatcher` for UI thread marshalling in Avalonia applications. Available as a drop-in file in the `docs/` folder since Avalonia requires a separate package reference. Community-tested.

- **Source generator configuration overload**: `AddModernMediatorGenerated()` now accepts an optional configuration action for `ErrorPolicy`, `CachingMode`, and `IDispatcher`.

### Fixed

- **Source generator scoped registration**: `AddModernMediatorGenerated()` now registers `IMediator` as Scoped (was Singleton), matching the assembly scanning behavior from 0.2.1.

## [0.2.1-alpha] - 2024-12-28

### Fixed

- **Scoped service resolution**: Handlers can now resolve scoped dependencies (e.g., `DbContext`). Previously, `IMediator` was registered as a Singleton which captured the root `IServiceProvider`, causing "Cannot resolve scoped service from root provider" errors.

- **Open generic assembly scanning**: Assembly scanning now correctly skips open generic types (e.g., `ValidationBehavior<,>`). These must be registered explicitly via `AddOpenBehavior()`. Previously, scanning attempted invalid registrations that would fail at runtime.

### Changed

- **Breaking**: `IMediator` is now registered as **Scoped** instead of Singleton. This means:
  - Each DI scope gets its own `IMediator` instance
  - Pub/Sub subscriptions made via DI-injected `IMediator` are per-scope and not shared
  - For shared Pub/Sub subscriptions across scopes, use `Mediator.Instance` (the static singleton)

### Added

- Public constructor `Mediator(IServiceProvider)` for DI integration
- `AddOpenBehavior()` and `AddOpenExceptionHandler()` methods for registering open generic pipeline components
- 24 new tests covering scoped resolution and open generic scanning

## [0.2.0-alpha] - 2024-12-20

### Added

- Initial alpha release
- Request/Response pattern with `IRequest<TResponse>` and `IRequestHandler<TRequest, TResponse>`
- Streaming with `IStreamRequest<TResponse>` and `IAsyncEnumerable`
- Pub/Sub notifications with `Publish()` and `Subscribe()`
- Pub/Sub with callbacks for collecting responses from multiple subscribers
- Pipeline behaviors with `IPipelineBehavior<TRequest, TResponse>`
- Pre-processors and post-processors
- Exception handlers with typed exception handling
- Source generators for Native AOT compatibility
- Weak and strong reference support
- UI thread dispatching for WPF, WinForms, and MAUI
- Assembly scanning for automatic handler discovery
- String key routing for topic-based subscriptions
- Predicate filters for subscriptions
- Covariant message dispatch

[2.2.0]: https://github.com/EvanscoApps/ModernMediator/compare/v2.1.0...v2.2.0
[2.1.0]: https://github.com/evanscoapps/ModernMediator/compare/v2.0.0...v2.1.0
[2.0.0]: https://github.com/evanscoapps/ModernMediator/compare/v0.2.2-alpha...v2.0.0
[0.2.2-alpha]: https://github.com/EvanscoApps/ModernMediator/compare/v0.2.1-alpha...v0.2.2-alpha
[0.2.1-alpha]: https://github.com/EvanscoApps/ModernMediator/compare/v0.2.0-alpha...v0.2.1-alpha
[0.2.0-alpha]: https://github.com/EvanscoApps/ModernMediator/releases/tag/v0.2.0-alpha
