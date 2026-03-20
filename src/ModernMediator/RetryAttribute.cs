namespace ModernMediator;

/// <summary>
/// Marks a request for automatic retry on transient failure. The pipeline will
/// re-execute the handler up to <see cref="MaxAttempts"/> times using Polly.
/// Retryable exception types are configured globally via <c>RetryOptions</c>
/// at registration time.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class RetryAttribute : Attribute
{
    /// <summary>
    /// Gets the total number of attempts, including the initial attempt.
    /// Defaults to 3.
    /// </summary>
    public int MaxAttempts { get; }

    /// <summary>
    /// Gets or sets the delay strategy applied between attempts.
    /// Defaults to <see cref="RetryDelayStrategy.ExponentialBackoff"/>.
    /// </summary>
    public RetryDelayStrategy DelayStrategy { get; set; } = RetryDelayStrategy.ExponentialBackoff;

    /// <summary>
    /// Gets or sets the base delay in milliseconds between attempts.
    /// For exponential backoff, this is the initial delay before multiplication.
    /// Defaults to 200ms.
    /// </summary>
    public int BaseDelayMs { get; set; } = 200;

    /// <summary>
    /// Initializes a new instance of <see cref="RetryAttribute"/>.
    /// </summary>
    /// <param name="maxAttempts">
    /// The total number of attempts including the initial attempt. Defaults to 3.
    /// </param>
    public RetryAttribute(int maxAttempts = 3)
    {
        MaxAttempts = maxAttempts;
    }
}
