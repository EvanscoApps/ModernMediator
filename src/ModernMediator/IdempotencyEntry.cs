namespace ModernMediator;

/// <summary>
/// Represents a cached idempotency result held in an <see cref="IIdempotencyStore"/>.
/// </summary>
/// <typeparam name="TResponse">The response type of the originating request.</typeparam>
public sealed class IdempotencyEntry<TResponse>
{
    /// <summary>
    /// Gets the cached response returned by the handler on the original execution.
    /// </summary>
    public required TResponse Response { get; init; }

    /// <summary>
    /// Gets the UTC timestamp at which this entry was written to the store.
    /// </summary>
    public required DateTimeOffset CachedAt { get; init; }
}
