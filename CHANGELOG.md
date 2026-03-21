# Changelog

All notable changes to ModernMediator will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.1.0] — 2026-03-21

### Added
- **AuditBehavior**: pipeline behavior that captures per-request audit records
  (RequestTypeName, UserId, CorrelationId, duration, success/failure) and dispatches
  them to any `IAuditWriter` implementation; registered via `AddAudit()`
- **`[NoAudit]` attribute**: opt individual request types out of audit recording
- **AuditChannelDrainer** and **AuditHostedServiceExtensions**: fire-and-forget async
  drain loop via `System.Threading.Channels`; registered via `AddAuditDrainer()`
- **IdempotencyBehavior**: deduplicates requests marked with `[Idempotent]` by key
  and TTL using any `IIdempotencyStore` implementation; registered via `AddIdempotency()`
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
  (Name claim); registered via `AddAspNetCoreCurrentUser()`
- **AspNetCoreAuditExtensions** (`ModernMediator.AspNetCore`): convenience extension
  wiring `HttpContextCurrentUserAccessor` into the audit pipeline
- **ModernMediator.Audit.Serilog** (new package): `SerilogAuditWriter` — structured
  Serilog sink for audit records; logs at Information on success, Warning on failure
- **ModernMediator.Audit.EntityFramework** (new package): `EfCoreAuditWriter` and
  `AuditDbContext` — persists audit records to any EF Core-supported database
- **ModernMediator.Idempotency.EntityFramework** (new package): `EfCoreIdempotencyStore`
  and `IdempotencyDbContext` — persists idempotency entries to any EF Core-supported
  database with fingerprint-keyed deduplication and TTL expiry
- **`AddModernMediatorAspNetCore()`**: now registers `IHttpContextAccessor` automatically;
  previously a no-op placeholder

### Changed
- Total publishable packages increased from four to seven

---

## [2.0.0] — 2026-03-07

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
  with index-based recursion — replaces all reflection in the `Send` hot path
- **Closure elimination**: `RequestHandlerDelegate<TRequest, TResponse>(TRequest, CancellationToken)`
  passes request and token explicitly instead of capturing via closure — eliminates
  one allocation per pipeline step on every dispatch
- **Full ValueTask pipeline**: `IValueTaskRequestHandler`, `IValueTaskPipelineBehavior`,
  and `ISender.SendAsync` — zero-allocation path when handler completes synchronously
- Lower allocations than MediatR on every benchmark; significantly faster cold start
  and Publish; see [BENCHMARKS.md](BENCHMARKS.md) for full three-way results
  including martinothamar/Mediator
- `CreateStream` reflection eliminated via `StreamHandlerWrapperImpl<TRequest, TResponse>`
- `TryHandleException` reflection eliminated via compiled delegate cache
- `TimeoutBehavior` attribute lookup amortized via `ConcurrentDictionary<Type, int?>` cache

### Fixed
- MM002, MM003, MM100 were defined but never reported in v1.0 — now wired and tested
- EndpointGenerator duplicate route registration under incremental pipeline cache
  boundaries (string-based deduplication guard)
- Open generic behaviors incorrectly included in generated DI registration code

### Breaking Changes from v1.0
- **`RequestHandlerDelegate` signature changed**: from zero-parameter `RequestHandlerDelegate<TResponse>()`
  to two-parameter `RequestHandlerDelegate<TRequest, TResponse>(TRequest, CancellationToken)`.
  Custom `IPipelineBehavior` implementations must update `next()` calls to `next(request, cancellationToken)`.
- **`IPipelineBehavior<TRequest, TResponse>` variance removed**: `TRequest` is no longer
  declared as `in` (contravariant) due to the delegate change. This rarely affects user code.
- `ISender`, `IPublisher`, `IStreamer` are new interfaces — existing code injecting
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

[2.1.0]: https://github.com/evanscoapps/ModernMediator/compare/v2.0.0...v2.1.0
[2.0.0]: https://github.com/evanscoapps/ModernMediator/compare/v0.2.2-alpha...v2.0.0
[0.2.2-alpha]: https://github.com/EvanscoApps/ModernMediator/compare/v0.2.1-alpha...v0.2.2-alpha
[0.2.1-alpha]: https://github.com/EvanscoApps/ModernMediator/compare/v0.2.0-alpha...v0.2.1-alpha
[0.2.0-alpha]: https://github.com/EvanscoApps/ModernMediator/releases/tag/v0.2.0-alpha
