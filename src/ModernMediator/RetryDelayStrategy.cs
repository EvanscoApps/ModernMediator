namespace ModernMediator;

/// <summary>
/// Specifies the delay strategy used between retry attempts.
/// </summary>
public enum RetryDelayStrategy
{
    /// <summary>
    /// Each retry attempt waits the same fixed duration.
    /// </summary>
    Fixed,

    /// <summary>
    /// Each retry attempt waits for an exponentially increasing duration with jitter.
    /// </summary>
    ExponentialBackoff
}
