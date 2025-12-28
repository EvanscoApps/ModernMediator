# Changelog

All notable changes to ModernMediator will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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

[0.2.1-alpha]: https://github.com/EvanscoApps/ModernMediator/compare/v0.2.0-alpha...v0.2.1-alpha
[0.2.0-alpha]: https://github.com/EvanscoApps/ModernMediator/releases/tag/v0.2.0-alpha