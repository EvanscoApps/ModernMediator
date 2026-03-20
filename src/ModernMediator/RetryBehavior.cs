using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Polly;
using Polly.Retry;

namespace ModernMediator;

/// <summary>
/// Pipeline behavior that automatically retries failed requests decorated with
/// <see cref="RetryAttribute"/> using Polly. Retryable exception types are
/// configured globally via <c>RetryOptions</c> at registration time.
/// </summary>
/// <remarks>
/// When composed with <see cref="TimeoutBehavior"/>, timeout applies per attempt
/// rather than across all attempts, provided <c>RetryBehavior</c> is registered
/// before <c>TimeoutBehavior</c> in the pipeline. See the registration order
/// reference in the documentation.
/// </remarks>
#pragma warning disable MM006
public sealed class RetryBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
#pragma warning restore MM006

    private static readonly ConcurrentDictionary<RetryPipelineKey, ResiliencePipeline<TResponse>>
        _pipelines = new();

    private readonly RetryOptions _options;

    /// <summary>
    /// Initializes a new instance of <see cref="RetryBehavior{TRequest, TResponse}"/>.
    /// </summary>
    public RetryBehavior(RetryOptions options)
    {
        _options = options;
    }

    /// <inheritdoc/>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TRequest, TResponse> next,
        CancellationToken cancellationToken)
    {
        var attribute = GetAttribute();

        if (attribute is null || attribute.MaxAttempts <= 1)
            return await next(request, cancellationToken).ConfigureAwait(false);

        var pipeline = GetOrCreatePipeline(attribute);

        return await pipeline.ExecuteAsync(
            async ct => await next(request, ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);
    }

    private static RetryAttribute? GetAttribute() =>
        (RetryAttribute?)Attribute.GetCustomAttribute(
            typeof(TRequest), typeof(RetryAttribute));

    private ResiliencePipeline<TResponse> GetOrCreatePipeline(RetryAttribute attribute)
    {
        var key = new RetryPipelineKey(
            attribute.MaxAttempts,
            attribute.DelayStrategy,
            attribute.BaseDelayMs);

        return _pipelines.GetOrAdd(key, _ => BuildPipeline(attribute));
    }

    private ResiliencePipeline<TResponse> BuildPipeline(RetryAttribute attribute)
    {
        var retryableTypes = _options.RetryableExceptionTypes;

        return new ResiliencePipelineBuilder<TResponse>()
            .AddRetry(new RetryStrategyOptions<TResponse>
            {
                MaxRetryAttempts = attribute.MaxAttempts - 1,
                Delay = TimeSpan.FromMilliseconds(attribute.BaseDelayMs),
                BackoffType = attribute.DelayStrategy == RetryDelayStrategy.ExponentialBackoff
                    ? DelayBackoffType.Exponential
                    : DelayBackoffType.Constant,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder<TResponse>()
                    .Handle<Exception>(ex => IsRetryable(ex, retryableTypes))
            })
            .Build();
    }

    private static bool IsRetryable(Exception ex, IReadOnlyList<Type> retryableTypes)
    {
        if (ex is OperationCanceledException)
            return false;

        foreach (var type in retryableTypes)
        {
            if (type.IsInstanceOfType(ex))
                return true;
        }

        return false;
    }

    private readonly record struct RetryPipelineKey(
        int MaxAttempts,
        RetryDelayStrategy DelayStrategy,
        int BaseDelayMs);
}
