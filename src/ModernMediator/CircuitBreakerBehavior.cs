using System;
using System.Threading;
using System.Threading.Tasks;
using Polly;
using Polly.CircuitBreaker;

namespace ModernMediator;

/// <summary>
/// Pipeline behavior that applies circuit breaker protection to requests decorated
/// with <see cref="CircuitBreakerAttribute"/> using Polly. When the failure count
/// within the sampling window reaches the configured threshold, the circuit opens
/// and subsequent dispatches fail fast with <see cref="CircuitBreakerOpenException"/>
/// without executing the handler.
/// </summary>
/// <remarks>
/// Circuit state is scoped per request type via <see cref="ICircuitBreakerRegistry"/>.
/// A tripped circuit on one request type does not affect any other request type.
/// Register this behavior as the outermost behavior in the pipeline so that
/// fast-fail occurs before retry or timeout logic is invoked. See the registration
/// order reference in the documentation.
/// </remarks>
#pragma warning disable MM006
public sealed class CircuitBreakerBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
#pragma warning restore MM006

    private readonly ICircuitBreakerRegistry _registry;

    /// <summary>
    /// Initializes a new instance of
    /// <see cref="CircuitBreakerBehavior{TRequest, TResponse}"/>.
    /// </summary>
    public CircuitBreakerBehavior(ICircuitBreakerRegistry registry)
    {
        _registry = registry;
    }

    /// <inheritdoc/>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TRequest, TResponse> next,
        CancellationToken cancellationToken)
    {
        var attribute = GetAttribute();

        if (attribute is null)
            return await next(request, cancellationToken).ConfigureAwait(false);

        var pipeline = _registry.GetOrCreate<TRequest, TResponse>(attribute);

        try
        {
            return await pipeline.ExecuteAsync(
                async ct => await next(request, ct).ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false);
        }
        catch (BrokenCircuitException)
        {
            throw new CircuitBreakerOpenException(
                typeof(TRequest).FullName ?? typeof(TRequest).Name,
                TimeSpan.FromSeconds(attribute.DurationSeconds));
        }
    }

    private static CircuitBreakerAttribute? GetAttribute() =>
        (CircuitBreakerAttribute?)Attribute.GetCustomAttribute(
            typeof(TRequest), typeof(CircuitBreakerAttribute));
}
