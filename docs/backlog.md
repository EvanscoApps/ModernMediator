# Backlog

This file tracks intended work that is not in the current release scope. Items here are not commitments and may be reordered, dropped, or rescoped.

The file groups items into v3.0 strategic work (substantive features and architectural changes) and tactical cleanup (smaller consistency or housekeeping items that should land alongside whatever release is convenient). Architectural Decision Records in `docs/decisions/` document decisions made; this file documents work intended.

## v3.0 strategic work

### Authorization behavior

A new `[Authorize("PolicyName")]` attribute on request types, paired with a pipeline behavior that consults `Microsoft.AspNetCore.Authorization.IAuthorizationService` before dispatching the handler. Failed authorization should surface as a typed exception that consumers can translate into HTTP responses or logging events.

### Outbox package

A new `ModernMediator.Outbox` package providing an in-process outbox pattern with at-least-once delivery. Scope is deliberately narrow: no transport coupling (no Kafka, no RabbitMQ, no Service Bus), just a durable handoff between the application's database transaction and the eventual dispatch of a notification. Consumers who want transport-specific delivery can build on top.

### Resilience package

Migrate the existing retry and circuit-breaker pipeline behaviors from direct Polly v8 usage to `Microsoft.Extensions.Resilience`, which is Microsoft's official resilience abstraction layered on Polly. This aligns ModernMediator with the broader .NET resilience ecosystem and gives consumers a single configuration surface for resilience policies across their entire application stack.

### First-class distributed tracing

The current v2.1 `AuditRecord.TraceId` field is sourced from `Activity.Current?.TraceId.ToString()` at audit time. v3.0 should integrate distributed tracing more deeply: a pipeline behavior that creates Activities per request, attributes them with request type and outcome, and integrates with `System.Diagnostics.DiagnosticSource` so consumers using OpenTelemetry or Application Insights see ModernMediator dispatches as first-class spans.

### CreateStream reflection elimination

The current streaming-request dispatch path uses runtime reflection to invoke handlers. Replace with source-generated dispatch on the source-generated path, matching the pattern already used for `Send`. Improves performance on the hot path and eliminates a Native AOT incompatibility surface.

### Pre/post processor existence check

The current pipeline always allocates wrapper types for pre and post processors even when no processors are registered for a given request type. Add a cached boolean flag per wrapper type to short-circuit when the processor list is empty, avoiding the wrapper allocation entirely.

### TryHandleException reflection elimination

The current exception-handler dispatch path uses runtime reflection to invoke `TryHandleException` on registered handlers. Replace with source-generated dispatch on the source-generated path, matching the pattern used elsewhere.

### Dispatcher overload mismatch helpful error

When `Send` is called on a request type registered only as `IValueTaskRequestHandler` (or vice versa for `SendAsync`), the current behavior is an opaque "no handler registered" error. Improve to: check the other registry on the error path, and if a handler exists there, throw a more helpful exception suggesting "Did you mean `SendAsync`?" (or `Send`). Implement both directions.

For the source-generated path, this becomes a compile-time `MM###` diagnostic since handler registrations are known statically. Runtime cross-registry check only runs on the error path, so no hot-path impact. Requires positive and negative tests on both paths per the standard test discipline.

## Tactical cleanup

These items surfaced incidentally during other work and should land alongside whatever release is convenient. None block any specific release on their own.

### Audit vs Idempotency registration pattern asymmetry

The audit pipeline registers via `cfg.AddAudit<TWriter>()` (a generic registration that accepts the writer type as a type parameter). The idempotency pipeline registers via `cfg.AddIdempotency(Action<IdempotencyOptions>?)` (an options-based registration that switches on an `IdempotencyStoreMode` enum, with no enum value for the EF Core store).

Consumers wiring the EF idempotency store currently have to register `IIdempotencyStore` explicitly via `services.AddScoped<IIdempotencyStore, EfCoreIdempotencyStore>()` before calling `AddIdempotency()`, relying on the default `TryAddSingleton` registration becoming a no-op. This is asymmetric and not obvious.

Resolution: introduce a generic `cfg.AddIdempotency<TStore>()` overload mirroring `AddAudit<TWriter>()`, or add an `IdempotencyStoreMode.EntityFramework` value with the appropriate registration logic. The first option preserves consistency with `AddAudit<TWriter>()`; the second extends the existing options-based shape.

### Entity-type configuration class visibility

`ModernMediator.Audit.EntityFramework` exposes `AuditRecordEntityTypeConfiguration` as a public class, which lets consumers fold the audit entity into their own DbContext when they have reason to combine it with other persisted entities (despite ADR-003 recommending the dedicated context pattern by default).

`ModernMediator.Idempotency.EntityFramework` does not expose an equivalent class. The idempotency entity model configuration lives inline in `IdempotencyDbContext.OnModelCreating`. Consumers wanting to combine the entity with their own context have to subclass `IdempotencyDbContext` (it is unsealed) or copy the inline configuration.

Resolution: extract the inline configuration into a public `IdempotencyRecordEntityTypeConfiguration` class, mirroring the audit package's approach.

### Repository URL casing inconsistency

The two `*.Generators` csproj files (`src/ModernMediator.Generators/ModernMediator.Generators.csproj` and `src/ModernMediator.AspNetCore.Generators/ModernMediator.AspNetCore.Generators.csproj`) use mixed-case `EvanscoApps` in their `<RepositoryUrl>` fields. The other csproj files and the repo-root README use lowercase `evanscoapps`. GitHub treats the URLs as equivalent, but the inconsistency is small clutter.

Resolution: change the two `*.Generators` csproj `<RepositoryUrl>` fields to lowercase `evanscoapps`.

### xUnit1031 warnings in test project

`tests/ModernMediator.Tests/ComprehensiveTests.cs:298` and `tests/ModernMediator.Tests/ConcurrencyTests.cs:248` produce xUnit1031 warnings. The rule flags synchronous blocking on tasks (`.Wait()` or `.Result`) inside test methods, which can deadlock under certain xUnit runner configurations.

Both warnings are pre-existing (not introduced by recent v2.2 work) and were observed across the v2.2 test work without being addressed. Worth fixing on a future test pass: replace the blocking calls with `await` patterns and mark the test methods `async Task`.
