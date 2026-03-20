using System;

namespace ModernMediator;

/// <summary>
/// Enables circuit breaker protection for the decorated request using Polly.
/// When the number of failures within the sampling window reaches
/// <see cref="FailureThreshold"/>, the circuit opens and subsequent dispatches
/// fail fast with a <see cref="CircuitBreakerOpenException"/> without executing
/// the handler.
/// </summary>
/// <remarks>
/// <see cref="FailureThreshold"/> is interpreted as an absolute failure count, not
/// a ratio. Internally this maps to Polly's <c>FailureRatio = 1.0</c> with
/// <c>MinimumThroughput = FailureThreshold</c>. See ADR-002 for rationale.
/// Circuit state is scoped per request type. A tripped circuit on one request type
/// does not affect any other.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class CircuitBreakerAttribute : Attribute
{
    /// <summary>
    /// Gets the number of failures within the sampling window required to open the circuit.
    /// Defaults to 5.
    /// </summary>
    public int FailureThreshold { get; }

    /// <summary>
    /// Gets the duration in seconds the circuit remains open before transitioning
    /// to half-open. Defaults to 30.
    /// </summary>
    public int DurationSeconds { get; }

    /// <summary>
    /// Gets or sets the duration in seconds of the rolling sampling window over
    /// which failures are counted. Defaults to 10.
    /// </summary>
    public int SamplingDurationSeconds { get; set; } = 10;

    /// <summary>
    /// Gets or sets the minimum number of requests that must occur within the
    /// sampling window before the circuit can open. When set explicitly, this
    /// overrides the default count-based mapping. Defaults to the value of
    /// <see cref="FailureThreshold"/>.
    /// </summary>
    public int MinimumThroughput { get; set; } = -1;

    /// <summary>
    /// Initializes a new instance of <see cref="CircuitBreakerAttribute"/>.
    /// </summary>
    /// <param name="failureThreshold">
    /// The number of failures within the sampling window required to open the circuit. Defaults to 5.
    /// </param>
    /// <param name="durationSeconds">
    /// The duration in seconds the circuit remains open before transitioning to half-open. Defaults to 30.
    /// </param>
    public CircuitBreakerAttribute(int failureThreshold = 5, int durationSeconds = 30)
    {
        FailureThreshold = failureThreshold;
        DurationSeconds = durationSeconds;
    }
}
