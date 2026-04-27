# ModernMediator.Audit.EntityFramework

Entity Framework Core audit writer for [ModernMediator](https://www.nuget.org/packages/ModernMediator). Provides `EfCoreAuditWriter`, an `IAuditWriter` implementation that persists `AuditRecord` instances produced by ModernMediator's `AuditBehavior` to a dedicated `AuditDbContext`.

## Installation

```bash
dotnet add package ModernMediator.Audit.EntityFramework
```

The package depends on [Microsoft.EntityFrameworkCore](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore) 8.0.2 and [ModernMediator](https://www.nuget.org/packages/ModernMediator), which are pulled in transitively. You will also install a database provider (for example, `Microsoft.EntityFrameworkCore.SqlServer`, `Npgsql.EntityFrameworkCore.PostgreSQL`, or `Microsoft.EntityFrameworkCore.Sqlite`) for the database you intend to use.

## Setup

Register `AuditDbContext` against your audit database, then opt into the audit pipeline with `EfCoreAuditWriter` as the `IAuditWriter` implementation:

```csharp
using ModernMediator;
using ModernMediator.Audit.EntityFramework;
using Microsoft.EntityFrameworkCore;

builder.Services.AddDbContext<AuditDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("AuditDb")));

builder.Services.AddModernMediator(config =>
{
    config.RegisterServicesFromAssemblyContaining<Program>();
    config.AddAudit<EfCoreAuditWriter>();
});
```

`AddAudit<TWriter>()` is defined on `MediatorConfiguration`. It registers the writer as a singleton `IAuditWriter`, registers `AuditBehavior<TRequest, TResponse>` as an open-generic pipeline behavior, and wires the background channel drainer that delivers audit records to the writer.

`EfCoreAuditWriter` resolves a fresh `AuditDbContext` from a new dependency-injection scope on every write. This isolates audit persistence from the request pipeline's scope and from any other concurrent audit write.

## Migrations

`AuditDbContext` is a normal EF Core `DbContext`, so the standard `dotnet ef` tooling applies:

```bash
dotnet ef migrations add InitialAuditSchema --context AuditDbContext --output-dir Migrations/Audit
dotnet ef database update --context AuditDbContext
```

If you do not want a separate audit database, the package ships an `AuditRecordEntityTypeConfiguration` you can apply inside your existing `DbContext`:

```csharp
public class ApplicationDbContext : DbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new AuditRecordEntityTypeConfiguration());
        // ...
    }
}
```

In that case, you would implement a custom `IAuditWriter` that targets your application context directly. See "Design rationale" below for the tradeoffs involved.

## Schema

`AuditRecord` is mapped to a single `AuditRecords` table configured with `HasNoKey()` (audit records are append-only and addressed by query, not by primary key). The columns are:

- `RequestTypeName` (nvarchar(512), required): the fully qualified type name of the dispatched request
- `SerializedPayload` (nvarchar(max), required): JSON serialization of the request
- `UserId` (nvarchar(256), nullable): from `ICurrentUserAccessor.UserId` if available
- `UserName` (nvarchar(256), nullable): from `ICurrentUserAccessor.UserName` if available
- `Timestamp` (datetime, required): when the audit record was produced
- `Succeeded` (bit, required): `true` if the handler completed without throwing, `false` otherwise
- `FailureReason` (nvarchar(2048), nullable): the exception message on failure
- `Duration` (bigint, required): handler execution time stored as `TimeSpan.Ticks` via a value converter
- `CorrelationId` (nvarchar(256), nullable): an optional correlation id from the request context
- `TraceId` (varchar(32), nullable): the distributed trace id from `Activity.Current` if available

## Opting requests out

Decorate any request type with `[NoAudit]` to skip auditing for it:

```csharp
using ModernMediator;

[NoAudit]
public sealed record HealthCheckQuery() : IRequest<bool>;
```

## Design rationale

`EfCoreAuditWriter` uses a dedicated `AuditDbContext`; the caller's application `DbContext` is never injected into or accessed by the writer. This is deliberate, and the reasoning is captured in [ADR-003](https://github.com/evanscoapps/ModernMediator/blob/main/docs/decisions/ADR-003-dedicated-audit-dbcontext.md).

Two problems would arise from sharing the application `DbContext`:

1. **Concurrency.** EF Core `DbContext` instances are not thread-safe. The audit pipeline writes records from a channel-backed background service running on a separate thread. Sharing a `DbContext` between that background service and the request pipeline guarantees concurrent access under non-trivial load, and the resulting behavior is undefined.
2. **Transactional coupling.** An audit write must succeed or fail independently of the business operation. Enlisting the audit write in the business transaction means a rollback discards the audit record (defeating the audit) and a failed audit write rolls back a successful business operation (worse).

A dedicated `AuditDbContext` has its own connection, transaction scope, and lifetime, so the audit write succeeds or fails on its own terms.

Callers who want audit records transactionally consistent with their business data can implement a custom `IAuditWriter` using their own `DbContext` and accept the concurrency responsibility explicitly. The shipped `AuditRecordEntityTypeConfiguration` makes that path practical.

## See also

- [Core ModernMediator README](https://github.com/evanscoapps/ModernMediator) for the full library reference and `AuditBehavior` documentation
- [ADR-003: Dedicated AuditDbContext](https://github.com/evanscoapps/ModernMediator/blob/main/docs/decisions/ADR-003-dedicated-audit-dbcontext.md) for the full design rationale
- [ModernMediator.Audit.Serilog](https://www.nuget.org/packages/ModernMediator.Audit.Serilog), an alternative writer that emits audit records as structured Serilog log events

## License

MIT. See [LICENSE](https://github.com/evanscoapps/ModernMediator/blob/main/LICENSE) in the repository root.
