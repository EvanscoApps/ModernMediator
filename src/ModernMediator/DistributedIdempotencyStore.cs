using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;

namespace ModernMediator;

/// <summary>
/// An <see cref="IIdempotencyStore"/> implementation backed by <see cref="IDistributedCache"/>.
/// Suitable for multi-instance and production deployments using Redis or SQL Server.
/// </summary>
/// <remarks>
/// This implementation is best-effort. Entries may be lost if the distributed cache
/// is flushed or a node fails before the configured window expires, which can result
/// in handler re-execution for a request whose fingerprint was lost. Callers requiring
/// exactly-once execution guarantees should use a durable store. See ADR-004.
/// Request and response types must be serializable by <see cref="System.Text.Json.JsonSerializer"/>.
/// </remarks>
public sealed class DistributedIdempotencyStore : IIdempotencyStore
{
    private readonly IDistributedCache _cache;

    /// <summary>
    /// Initializes a new instance of <see cref="DistributedIdempotencyStore"/>.
    /// </summary>
    public DistributedIdempotencyStore(IDistributedCache cache)
    {
        _cache = cache;
    }

    /// <inheritdoc/>
    public async ValueTask<IdempotencyEntry<TResponse>?> TryGetAsync<TResponse>(
        string fingerprint,
        CancellationToken cancellationToken)
    {
        var bytes = await _cache.GetAsync(fingerprint, cancellationToken).ConfigureAwait(false);

        if (bytes is null)
            return null;

        return JsonSerializer.Deserialize<IdempotencyEntry<TResponse>>(bytes);
    }

    /// <inheritdoc/>
    public async ValueTask SetAsync<TResponse>(
        string fingerprint,
        TResponse response,
        TimeSpan window,
        CancellationToken cancellationToken)
    {
        var entry = new IdempotencyEntry<TResponse>
        {
            Response = response,
            CachedAt = DateTimeOffset.UtcNow
        };

        var bytes = JsonSerializer.SerializeToUtf8Bytes(entry);

        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = window
        };

        await _cache.SetAsync(fingerprint, bytes, options, cancellationToken).ConfigureAwait(false);
    }
}
