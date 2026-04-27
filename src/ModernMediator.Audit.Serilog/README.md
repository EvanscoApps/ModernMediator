# ModernMediator.Audit.Serilog

[Serilog](https://serilog.net/) audit writer for [ModernMediator](https://www.nuget.org/packages/ModernMediator). Provides `SerilogAuditWriter`, an `IAuditWriter` implementation that emits `AuditRecord` instances produced by ModernMediator's `AuditBehavior` as structured Serilog log events.

## Installation

```bash
dotnet add package ModernMediator.Audit.Serilog
```

The package depends on [Serilog](https://www.nuget.org/packages/Serilog) 4.2.0 and [ModernMediator](https://www.nuget.org/packages/ModernMediator), which are pulled in transitively. You will typically also install one of the Serilog hosting integration packages (for example, `Serilog.AspNetCore` or `Serilog.Extensions.Hosting`) to register `Serilog.ILogger` with the dependency-injection container.

## Setup

Configure Serilog as you normally would, then opt into the audit pipeline with `SerilogAuditWriter` as the `IAuditWriter` implementation:

```csharp
using ModernMediator;
using ModernMediator.Audit.Serilog;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddModernMediator(config =>
{
    config.RegisterServicesFromAssemblyContaining<Program>();
    config.AddAudit<SerilogAuditWriter>();
});
```

`AddAudit<TWriter>()` is defined on `MediatorConfiguration`. It registers the writer as a singleton `IAuditWriter`, registers `AuditBehavior<TRequest, TResponse>` as an open-generic pipeline behavior, and wires the background channel drainer that delivers audit records to the writer.

`SerilogAuditWriter` resolves `Serilog.ILogger` from DI when available; otherwise it falls back to the static `Log.Logger`. Most hosted setups use `builder.Host.UseSerilog()` (from `Serilog.AspNetCore`) or `services.AddSerilog(...)` (from `Serilog.Extensions.Hosting`), either of which registers `Serilog.ILogger` with the container.

## Dispatch mode

By default the audit pipeline buffers records in a `System.Threading.Channels.Channel<AuditRecord>` and writes them from a hosted background service, keeping write latency off the request path. For a structured logger like Serilog you can switch to fire-and-forget instead:

```csharp
config.AddAudit<SerilogAuditWriter>(o =>
{
    o.DispatchMode = AuditDispatchMode.FireAndForget;
});
```

See ADR-001 for the rationale behind the default.

## Opting requests out

Decorate any request type with `[NoAudit]` to skip auditing for it:

```csharp
[NoAudit]
public sealed record HealthCheckQuery() : IRequest<bool>;
```

## What gets logged

Each `AuditRecord` produces a single Serilog log event. Successful requests log at `LogEventLevel.Information`; failures log at `LogEventLevel.Warning`.

The structured properties emitted by `SerilogAuditWriter` are:

- `RequestTypeName` (string): the fully qualified type name of the dispatched request
- `UserId` (string): from `ICurrentUserAccessor.UserId` if available, otherwise `"anonymous"`
- `Succeeded` (bool): `true` if the handler completed without throwing, `false` otherwise
- `DurationMs` (double): handler execution time in milliseconds
- `TraceId` (string): the distributed trace id from `Activity.Current` if available, otherwise `"none"`
- `CorrelationId` (string): an optional correlation id from the request context, otherwise `"none"`
- `FailureReason` (string): the exception message on failure, otherwise empty

The full `AuditRecord` produced by `AuditBehavior` carries additional fields (`SerializedPayload`, `UserName`, `Timestamp`) that are not surfaced by `SerilogAuditWriter` directly. If you need those in your log output, implement a custom `IAuditWriter` that wraps Serilog with the projection you want.

## See also

- [Core ModernMediator README](https://github.com/evanscoapps/ModernMediator) for the full library reference and `AuditBehavior` documentation
- [ModernMediator.Audit.EntityFramework](https://www.nuget.org/packages/ModernMediator.Audit.EntityFramework), an alternative writer that persists audit records to a database

## License

MIT. See [LICENSE](https://github.com/evanscoapps/ModernMediator/blob/main/LICENSE) in the repository root.
