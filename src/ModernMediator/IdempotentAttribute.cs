namespace ModernMediator;

/// <summary>
/// Marks a request as idempotent. The pipeline will compute a fingerprint of the
/// request and return a cached result if the same fingerprint has been seen within
/// the configured window, without re-executing the handler.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class IdempotentAttribute : Attribute
{
    /// <summary>
    /// Gets the duration in seconds for which a fingerprint entry remains valid.
    /// Defaults to 3600 (one hour).
    /// </summary>
    public int WindowSeconds { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="IdempotentAttribute"/>.
    /// </summary>
    /// <param name="windowSeconds">
    /// The duration in seconds for which a fingerprint entry remains valid. Defaults to 3600.
    /// </param>
    public IdempotentAttribute(int windowSeconds = 3600)
    {
        WindowSeconds = windowSeconds;
    }
}
