using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ModernMediator.Audit.EntityFramework;

/// <summary>
/// An <see cref="IAuditWriter"/> implementation that persists
/// <see cref="AuditRecord"/> instances to a relational database via
/// Entity Framework Core using a dedicated <see cref="AuditDbContext"/>.
/// </summary>
/// <remarks>
/// Uses a dedicated <see cref="AuditDbContext"/> to avoid transactional coupling
/// with the caller's application data and to prevent thread-safety issues when
/// writing from the channel background drainer. See ADR-003.
/// A new <see cref="AuditDbContext"/> scope is created per write to ensure
/// isolation.
/// </remarks>
public sealed class EfCoreAuditWriter : IAuditWriter
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of <see cref="EfCoreAuditWriter"/>.
    /// </summary>
    public EfCoreAuditWriter(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    public async ValueTask WriteAsync(AuditRecord record, CancellationToken cancellationToken)
    {
        await using var scope = _serviceProvider
            .GetRequiredService<IServiceScopeFactory>()
            .CreateAsyncScope();

        var context = scope.ServiceProvider
            .GetRequiredService<AuditDbContext>();

        context.AuditRecords.Add(record);

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
