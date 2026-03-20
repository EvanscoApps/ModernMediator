using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ModernMediator;

/// <summary>
/// Hosted background service that drains the audit record channel and forwards
/// each record to the registered <see cref="IAuditWriter"/>. Runs for the
/// lifetime of the application and completes in-flight writes on graceful shutdown.
/// </summary>
internal sealed class AuditChannelDrainer : BackgroundService
{
    private readonly Channel<AuditRecord> _channel;
    private readonly IAuditWriter _writer;
    private readonly ILogger<AuditChannelDrainer> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="AuditChannelDrainer"/>.
    /// </summary>
    public AuditChannelDrainer(
        Channel<AuditRecord> channel,
        IAuditWriter writer,
        ILogger<AuditChannelDrainer> logger)
    {
        _channel = channel;
        _writer = writer;
        _logger = logger;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var record in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await _writer.WriteAsync(record, stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Audit write failed for request type {RequestType}.",
                    record.RequestTypeName);
            }
        }
    }
}
