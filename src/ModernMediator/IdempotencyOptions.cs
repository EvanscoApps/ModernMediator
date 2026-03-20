namespace ModernMediator;

/// <summary>
/// Configuration options for <see cref="IdempotencyBehavior{TRequest,TResponse}"/>.
/// Configure via <c>AddIdempotency()</c> at registration time.
/// </summary>
public sealed class IdempotencyOptions
{
    /// <summary>
    /// Gets or sets the backing store mode for idempotency fingerprints.
    /// Defaults to <see cref="IdempotencyStoreMode.InMemory"/>.
    /// </summary>
    public IdempotencyStoreMode StoreMode { get; set; } = IdempotencyStoreMode.InMemory;
}
