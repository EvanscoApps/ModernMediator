using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;

namespace ModernMediator;

/// <summary>
/// An <see cref="IIdempotencyStore"/> implementation backed by <see cref="IMemoryCache"/>.
/// Suitable for development and single-instance deployments.
/// </summary>
/// <remarks>
/// This implementation is best-effort. Entries may be evicted under memory pressure
/// before their configured window expires, which can result in handler re-execution
/// for a request whose fingerprint was evicted. Callers in environments where
/// exactly-once execution is required should use a durable store. See ADR-004.
/// </remarks>
public sealed class InMemoryIdempotencyStore : IIdempotencyStore
{
    private readonly IMemoryCache _cache;

    /// <summary>
    /// Initializes a new instance of <see cref="InMemoryIdempotencyStore"/>.
    /// </summary>
    public InMemoryIdempotencyStore(IMemoryCache cache)
    {
        _cache = cache;
    }

    /// <inheritdoc/>
    public ValueTask<IdempotencyEntry<TResponse>?> TryGetAsync<TResponse>(
        string fingerprint,
        CancellationToken cancellationToken)
    {
        _cache.TryGetValue(fingerprint, out IdempotencyEntry<TResponse>? entry);
        return ValueTask.FromResult(entry);
    }

    /// <inheritdoc/>
    public ValueTask SetAsync<TResponse>(
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

        _cache.Set(fingerprint, entry, window);
        return ValueTask.CompletedTask;
    }
}
