namespace ModernMediator;

/// <summary>
/// Specifies the backing store used by the idempotency pipeline behavior.
/// </summary>
public enum IdempotencyStoreMode
{
    /// <summary>
    /// Uses an in-memory cache. Suitable for development and single-instance deployments.
    /// Best-effort only — see ADR-004.
    /// </summary>
    InMemory,

    /// <summary>
    /// Uses a distributed cache (e.g. Redis). Suitable for multi-instance deployments.
    /// Best-effort only — see ADR-004.
    /// </summary>
    DistributedCache
}
