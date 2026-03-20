using System;

namespace ModernMediator;

/// <summary>
/// Thrown when a request is dispatched while its circuit breaker is in the open state.
/// The handler is not executed. Callers should inspect <see cref="RetryAfter"/> to
/// determine when the circuit will transition to half-open.
/// </summary>
public sealed class CircuitBreakerOpenException : Exception
{
    /// <summary>
    /// Gets the fully qualified type name of the request whose circuit is open.
    /// </summary>
    public string RequestTypeName { get; }

    /// <summary>
    /// Gets the approximate duration remaining before the circuit transitions
    /// to half-open and allows a probe request through.
    /// </summary>
    public TimeSpan RetryAfter { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="CircuitBreakerOpenException"/>.
    /// </summary>
    public CircuitBreakerOpenException(string requestTypeName, TimeSpan retryAfter)
        : base($"Circuit breaker is open for '{requestTypeName}'. Retry after {retryAfter.TotalSeconds:F0}s.")
    {
        RequestTypeName = requestTypeName;
        RetryAfter = retryAfter;
    }
}
