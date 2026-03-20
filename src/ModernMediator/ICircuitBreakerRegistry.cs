using System;
using Polly;

namespace ModernMediator;

/// <summary>
/// Manages Polly resilience pipelines for circuit breaker-protected request types.
/// Pipelines are created on first access using the <see cref="CircuitBreakerAttribute"/>
/// values declared on the request type, and cached for the lifetime of the registry.
/// </summary>
/// <remarks>
/// Circuit state is scoped per request type. A tripped circuit on one request type
/// does not affect any other request type.
/// </remarks>
public interface ICircuitBreakerRegistry
{
    /// <summary>
    /// Returns the cached resilience pipeline for <typeparamref name="TRequest"/>,
    /// creating and caching it on first call using the attribute values declared
    /// on <typeparamref name="TRequest"/>.
    /// </summary>
    ResiliencePipeline<TResponse> GetOrCreate<TRequest, TResponse>(
        CircuitBreakerAttribute attribute);
}
