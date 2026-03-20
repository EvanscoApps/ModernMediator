using System.Threading;
using System.Threading.Tasks;

namespace ModernMediator;

/// <summary>
/// Defines the contract for writing audit records produced by <c>AuditBehavior</c>.
/// Implement this interface to direct audit output to a database, structured log,
/// event stream, or any other destination.
/// </summary>
/// <remarks>
/// Implementations are invoked from a background channel drainer, not from within
/// the request pipeline. A failure in <see cref="WriteAsync"/> will not propagate
/// to the caller. See ADR-001 for rationale.
/// </remarks>
public interface IAuditWriter
{
    /// <summary>
    /// Writes the given audit record to the underlying destination.
    /// </summary>
    ValueTask WriteAsync(AuditRecord record, CancellationToken cancellationToken);
}
