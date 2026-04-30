using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ModernMediator.Idempotency.EntityFramework;

/// <summary>
/// A durable <see cref="IIdempotencyStore"/> implementation backed by
/// Entity Framework Core. Uses a unique database constraint on the fingerprint
/// column to guarantee exactly-once execution regardless of application-layer
/// race conditions. See ADR-004.
/// </summary>
/// <remarks>
/// A new <see cref="IdempotencyDbContext"/> scope is created per operation to
/// ensure isolation from the caller's application context and from the audit
/// write path.
/// Concurrent duplicate requests are resolved at the database level: the second
/// insert is rejected by the unique constraint and the cached response from the
/// first execution is returned.
/// </remarks>
public sealed class EfCoreIdempotencyStore : IIdempotencyStore
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of <see cref="EfCoreIdempotencyStore"/>.
    /// </summary>
    public EfCoreIdempotencyStore(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    public async ValueTask<IdempotencyEntry<TResponse>?> TryGetAsync<TResponse>(
        string fingerprint,
        CancellationToken cancellationToken)
    {
        await using var scope = _serviceProvider
            .GetRequiredService<IServiceScopeFactory>()
            .CreateAsyncScope();

        var context = scope.ServiceProvider
            .GetRequiredService<IdempotencyDbContext>();

        var record = await context.IdempotencyRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Fingerprint == fingerprint, cancellationToken)
            .ConfigureAwait(false);

        if (record is null || record.ExpiresAt <= DateTimeOffset.UtcNow)
            return null;

        var response = JsonSerializer.Deserialize<TResponse>(record.SerializedResponse);

        if (response is null)
            return null;

        return new IdempotencyEntry<TResponse>
        {
            Response = response,
            CachedAt = record.CachedAt
        };
    }

    /// <inheritdoc/>
    public async ValueTask SetAsync<TResponse>(
        string fingerprint,
        TResponse response,
        TimeSpan window,
        CancellationToken cancellationToken)
    {
        await using var scope = _serviceProvider
            .GetRequiredService<IServiceScopeFactory>()
            .CreateAsyncScope();

        var context = scope.ServiceProvider
            .GetRequiredService<IdempotencyDbContext>();

        var now = DateTimeOffset.UtcNow;

        var record = new IdempotencyRecord
        {
            Fingerprint = fingerprint,
            SerializedResponse = JsonSerializer.Serialize(response),
            ResponseTypeName = typeof(TResponse).FullName ?? typeof(TResponse).Name,
            CachedAt = now,
            ExpiresAt = now.Add(window)
        };

        try
        {
            context.IdempotencyRecords.Add(record);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            // A concurrent request already inserted this fingerprint.
            // The unique constraint did its job; swallow the exception.
            // The caller will retrieve the existing entry on the next TryGetAsync.
        }
    }
}
