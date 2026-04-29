using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ModernMediator.Tests;

internal sealed class CapturingAuditWriter : IAuditWriter
{
    public List<AuditRecord> Records { get; } = new();

    public ValueTask WriteAsync(AuditRecord record, CancellationToken cancellationToken)
    {
        Records.Add(record);
        return ValueTask.CompletedTask;
    }
}
