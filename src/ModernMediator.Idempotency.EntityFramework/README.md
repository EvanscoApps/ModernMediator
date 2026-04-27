# ModernMediator.Idempotency.EntityFramework

Entity Framework Core idempotency store for [ModernMediator](https://www.nuget.org/packages/ModernMediator). Provides `EfCoreIdempotencyStore`, an `IIdempotencyStore` implementation that persists request fingerprints and cached responses to a dedicated `IdempotencyDbContext`. The fingerprint column is the primary key, so duplicate inserts are rejected by the database, giving exactly-once execution guarantees that do not depend on cache retention.

## Installation

```bash
dotnet add package ModernMediator.Idempotency.EntityFramework
```

The package depends on [Microsoft.EntityFrameworkCore](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore) 8.0.2 and [ModernMediator](https://www.nuget.org/packages/ModernMediator), which are pulled in transitively. You will also install a database provider (for example, `Microsoft.EntityFrameworkCore.SqlServer`, `Npgsql.EntityFrameworkCore.PostgreSQL`, or `Microsoft.EntityFrameworkCore.Sqlite`) for the database you intend to use.

## When to use this package

The core `ModernMediator` package ships two best-effort `IIdempotencyStore` implementations:

- `InMemoryIdempotencyStore`, backed by `IMemoryCache`. Single-instance, suitable for development.
- `DistributedIdempotencyStore`, backed by `IDistributedCache`. Multi-instance, suitable for most production deployments where eventual cache eviction is acceptable.

Both can lose entries before the configured window expires (memory pressure, cache flush, node failure), in which case a duplicate request will re-execute the handler. For most idempotency use cases (deduplicating webhooks, suppressing repeated form submissions, short-circuiting redundant cache population), this is acceptable.

`EfCoreIdempotencyStore` is the right choice when handler re-execution is unacceptable: financial transaction processing, safety-critical ingestion pipelines, or any workflow where exactly-once semantics must survive cache eviction. The unique constraint on the fingerprint column is enforced at the database level, so even concurrent races between application instances are resolved by the database rather than by application-layer locking.

See [ADR-004](https://github.com/evanscoapps/ModernMediator/blob/main/docs/decisions/ADR-004-best-effort-idempotency-store.md) for the full rationale.

## Setup

Register `IdempotencyDbContext` against your idempotency database, register `EfCoreIdempotencyStore` as the `IIdempotencyStore` implementation before calling `AddIdempotency()`, then opt into the idempotency pipeline:

```csharp
using ModernMediator;
using ModernMediator.Idempotency.EntityFramework;
using Microsoft.EntityFrameworkCore;

builder.Services.AddDbContext<IdempotencyDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("IdempotencyDb")));

builder.Services.AddSingleton<IIdempotencyStore, EfCoreIdempotencyStore>();

builder.Services.AddModernMediator(config =>
{
    config.RegisterServicesFromAssemblyContaining<Program>();
    config.AddIdempotency();
});
```

`AddIdempotency()` is defined on `MediatorConfiguration`. It registers `IdempotencyBehavior<TRequest, TResponse>` as an open-generic pipeline behavior. Its `IdempotencyOptions.StoreMode` defaults to `InMemory`, which uses `TryAddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>()`. Because `IIdempotencyStore` is already registered by the explicit `AddSingleton` above, the in-memory `TryAdd` is a no-op and `EfCoreIdempotencyStore` wins.

`EfCoreIdempotencyStore` resolves a fresh `IdempotencyDbContext` from a new dependency-injection scope on every `TryGetAsync`/`SetAsync` call. This isolates idempotency persistence from the request pipeline's scope.

## Marking requests as idempotent

Decorate any request type with `[Idempotent]` to participate in the pipeline. The optional constructor argument is the cache window in seconds (default 3600):

```csharp
using ModernMediator;

[Idempotent(windowSeconds: 600)]
public sealed record SubmitPaymentCommand(string PaymentId, decimal Amount)
    : IRequest<PaymentReceipt>;
```

When the request is dispatched, `IdempotencyBehavior` computes a SHA-256 fingerprint of the JSON-serialized request (prefixed with the request type's full name), checks the store, and either returns the cached response or executes the handler and persists the response.

## Schema

`IdempotencyRecord` is mapped to a single `IdempotencyRecords` table. The columns are:

- `Fingerprint` (nchar(64), primary key) — SHA-256 hex digest of the request, fixed length. The primary-key constraint is what guarantees exactly-once execution at the database level.
- `SerializedResponse` (nvarchar(max), required) — JSON serialization of the cached response
- `ResponseTypeName` (nvarchar(512), required) — fully qualified response type name, used for deserialization
- `CachedAt` (datetimeoffset, required) — UTC timestamp at which the entry was written
- `ExpiresAt` (datetimeoffset, required) — UTC timestamp at which the entry becomes ineligible for cache retrieval; carries an index named `IX_IdempotencyRecords_ExpiresAt` to support cleanup queries

The model configuration is applied inline in `IdempotencyDbContext.OnModelCreating`. There is no separate `IEntityTypeConfiguration<IdempotencyRecord>` shipped with the package: if you want to host idempotency records in your existing context, subclass `IdempotencyDbContext` (it is unsealed) or copy the configuration into your own `OnModelCreating`.

## Migrations

`IdempotencyDbContext` is a normal EF Core `DbContext`, so the standard `dotnet ef` tooling applies:

```bash
dotnet ef migrations add InitialIdempotencySchema --context IdempotencyDbContext --output-dir Migrations/Idempotency
dotnet ef database update --context IdempotencyDbContext
```

Verify after the first migration that the generated table has `Fingerprint` declared as the primary key; this is the constraint that enforces exactly-once execution.

## Cache expiry and cleanup

`EfCoreIdempotencyStore.TryGetAsync` filters out entries whose `ExpiresAt` is in the past, so expired records do not produce false cache hits. The records remain in the table until you delete them. The `IX_IdempotencyRecords_ExpiresAt` index supports cleanup queries:

```sql
DELETE FROM IdempotencyRecords WHERE ExpiresAt <= SYSUTCDATETIME();
```

Schedule this against your maintenance windows according to retention requirements.

## Design rationale

`IdempotencyBehavior` checks a fingerprint store before dispatching a request. The decision of what guarantee that store should provide is captured in [ADR-004](https://github.com/evanscoapps/ModernMediator/blob/main/docs/decisions/ADR-004-best-effort-idempotency-store.md): the core package ships best-effort cache stores, and this package provides a durable alternative.

The decision rests on two observations:

1. Most idempotency use cases (deduplicating webhooks, avoiding redundant cache population, short-circuiting repeated form submissions) are well-served by a distributed cache. Forcing a database dependency on every caller would be inappropriate.
2. Callers who do require exactly-once execution (financial transaction processors, safety-critical ingestion pipelines) need a guarantee that cannot be defeated by cache eviction. A unique constraint on the fingerprint column in a relational database is the correct mechanism: if two concurrent requests race with the same fingerprint, the database rejects the second insert at the constraint level rather than relying on application-layer locking.

`EfCoreIdempotencyStore` follows the dedicated-context pattern established by [ADR-003](https://github.com/evanscoapps/ModernMediator/blob/main/docs/decisions/ADR-003-dedicated-audit-dbcontext.md) for `AuditDbContext`: a separate `IdempotencyDbContext` keeps the writer free of concurrency hazards from the caller's application context and avoids transactional coupling with business operations.

## See also

- [Core ModernMediator README](https://github.com/evanscoapps/ModernMediator) for the full library reference, `IdempotencyBehavior`, and the cache-backed alternatives `InMemoryIdempotencyStore` and `DistributedIdempotencyStore`
- [ADR-004: Best-effort idempotency store with durable option via separate package](https://github.com/evanscoapps/ModernMediator/blob/main/docs/decisions/ADR-004-best-effort-idempotency-store.md)
- [ADR-003: Dedicated AuditDbContext](https://github.com/evanscoapps/ModernMediator/blob/main/docs/decisions/ADR-003-dedicated-audit-dbcontext.md) for the dedicated-context pattern this package follows

## License

MIT. See [LICENSE](https://github.com/evanscoapps/ModernMediator/blob/main/LICENSE) in the repository root.
