namespace ModernMediator;

/// <summary>
/// Defines the contract for a store that backs the idempotency pipeline behavior.
/// Implementations may be cache-backed (best-effort) or database-backed (durable).
/// </summary>
/// <remarks>
/// Cache-backed implementations cannot guarantee exactly-once execution if an entry
/// is evicted before its window expires. For exactly-once semantics, use a durable
/// implementation backed by a database with a unique constraint on the fingerprint
/// column. See ADR-004 for rationale.
/// </remarks>
public interface IIdempotencyStore
{
    /// <summary>
    /// Attempts to retrieve a cached response for the given fingerprint.
    /// Returns <c>null</c> if no entry exists or the entry has expired.
    /// </summary>
    ValueTask<IdempotencyEntry<TResponse>?> TryGetAsync<TResponse>(
        string fingerprint,
        CancellationToken cancellationToken);

    /// <summary>
    /// Stores a response for the given fingerprint with the specified expiry window.
    /// </summary>
    ValueTask SetAsync<TResponse>(
        string fingerprint,
        TResponse response,
        TimeSpan window,
        CancellationToken cancellationToken);
}
