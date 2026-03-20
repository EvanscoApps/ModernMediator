using System;
using System.Collections.Concurrent;
using Polly;
using Polly.CircuitBreaker;

namespace ModernMediator;

/// <summary>
/// Default implementation of <see cref="ICircuitBreakerRegistry"/>.
/// Builds and caches Polly resilience pipelines per request type.
/// Registered as a singleton by <c>AddCircuitBreaker()</c>.
/// </summary>
public sealed class CircuitBreakerRegistry : ICircuitBreakerRegistry
{
    private readonly ConcurrentDictionary<Type, object> _pipelines = new();

    /// <inheritdoc/>
    public ResiliencePipeline<TResponse> GetOrCreate<TRequest, TResponse>(
        CircuitBreakerAttribute attribute)
    {
        return (ResiliencePipeline<TResponse>)_pipelines.GetOrAdd(
            typeof(TRequest),
            _ => BuildPipeline<TResponse>(attribute));
    }

    private static ResiliencePipeline<TResponse> BuildPipeline<TResponse>(
        CircuitBreakerAttribute attribute)
    {
        // MinimumThroughput defaults to FailureThreshold (count-based mapping).
        // If the caller set it explicitly on the attribute, that value wins.
        // See ADR-002 for rationale.
        int minimumThroughput = attribute.MinimumThroughput == -1
            ? attribute.FailureThreshold
            : attribute.MinimumThroughput;

        return new ResiliencePipelineBuilder<TResponse>()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<TResponse>
            {
                FailureRatio = 1.0,
                MinimumThroughput = minimumThroughput,
                SamplingDuration = TimeSpan.FromSeconds(attribute.SamplingDurationSeconds),
                BreakDuration = TimeSpan.FromSeconds(attribute.DurationSeconds),
                ShouldHandle = new PredicateBuilder<TResponse>()
                    .Handle<Exception>(ex => ex is not OperationCanceledException)
            })
            .Build();
    }
}
