using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ModernMediator.Tests;

internal sealed class CapturingAuditWriter : IAuditWriter
{
    private readonly SemaphoreSlim _writeSignal = new(0);

    public List<AuditRecord> Records { get; } = new();

    public ValueTask WriteAsync(AuditRecord record, CancellationToken cancellationToken = default)
    {
        Records.Add(record);
        _writeSignal.Release();
        return ValueTask.CompletedTask;
    }

    public async Task WaitForWriteAsync(TimeSpan? timeout = null)
    {
        var actualTimeout = timeout ?? TimeSpan.FromSeconds(5);
        if (!await _writeSignal.WaitAsync(actualTimeout))
        {
            throw new TimeoutException(
                $"CapturingAuditWriter.WaitForWriteAsync timed out after {actualTimeout.TotalSeconds:F1}s waiting for an audit write. " +
                $"Records currently captured: {Records.Count}.");
        }
    }
}
