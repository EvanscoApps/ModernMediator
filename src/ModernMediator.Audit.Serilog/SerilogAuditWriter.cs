using System.Threading;
using System.Threading.Tasks;
using Serilog;
using Serilog.Events;

namespace ModernMediator.Audit.Serilog;

/// <summary>
/// An <see cref="IAuditWriter"/> implementation that writes <see cref="AuditRecord"/>
/// instances as structured log events via Serilog.
/// Successful requests are logged at <see cref="LogEventLevel.Information"/>.
/// Failed requests are logged at <see cref="LogEventLevel.Warning"/>.
/// </summary>
public sealed class SerilogAuditWriter : IAuditWriter
{
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="SerilogAuditWriter"/> using the
    /// provided Serilog logger instance.
    /// </summary>
    public SerilogAuditWriter(ILogger logger)
    {
        _logger = logger.ForContext<SerilogAuditWriter>();
    }

    /// <summary>
    /// Initializes a new instance of <see cref="SerilogAuditWriter"/> using the
    /// global <see cref="Log.Logger"/>.
    /// </summary>
    public SerilogAuditWriter() : this(Log.Logger) { }

    /// <inheritdoc/>
    public ValueTask WriteAsync(AuditRecord record, CancellationToken cancellationToken)
    {
        var level = record.Succeeded
            ? LogEventLevel.Information
            : LogEventLevel.Warning;

        _logger.Write(level,
            "Audit | {RequestTypeName} | User: {UserId} | Succeeded: {Succeeded} | " +
            "Duration: {DurationMs}ms | TraceId: {TraceId} | " +
            "CorrelationId: {CorrelationId} | Failure: {FailureReason}",
            record.RequestTypeName,
            record.UserId ?? "anonymous",
            record.Succeeded,
            record.Duration.TotalMilliseconds,
            record.TraceId ?? "none",
            record.CorrelationId ?? "none",
            record.FailureReason ?? string.Empty);

        return ValueTask.CompletedTask;
    }
}
