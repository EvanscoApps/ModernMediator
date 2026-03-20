using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ModernMediator.Tests;

public sealed class RetryBehaviorTests
{
    // --- Test doubles ---

    private sealed class NonRetryableRequest : IRequest<string> { }

    [Retry(maxAttempts: 3, DelayStrategy = RetryDelayStrategy.Fixed, BaseDelayMs = 0)]
    private sealed class RetryableRequest : IRequest<string> { }

    [Retry(maxAttempts: 1, DelayStrategy = RetryDelayStrategy.Fixed, BaseDelayMs = 0)]
    private sealed class SingleAttemptRequest : IRequest<string> { }

    [Retry(maxAttempts: 3, DelayStrategy = RetryDelayStrategy.Fixed, BaseDelayMs = 0)]
    private sealed class RetryableUnitRequest : IRequest<Unit> { }

    private sealed class TransientException : Exception
    {
        public TransientException(string message) : base(message) { }
    }

    private sealed class NonTransientException : Exception
    {
        public NonTransientException(string message) : base(message) { }
    }

    // --- Helpers ---

    private static RetryOptions BuildOptions() =>
        new RetryOptions().RetryOn<TransientException>();

    private static RetryBehavior<TRequest, TResponse> BuildBehavior<TRequest, TResponse>()
        where TRequest : IRequest<TResponse> =>
        new RetryBehavior<TRequest, TResponse>(BuildOptions());

    // --- Tests ---

    [Fact]
    public async Task Handle_NoAttribute_ExecutesHandlerOnce()
    {
        var behavior = BuildBehavior<NonRetryableRequest, string>();
        int callCount = 0;

        var result = await behavior.Handle(
            new NonRetryableRequest(),
            (_, _) => { callCount++; return Task.FromResult("ok"); },
            CancellationToken.None);

        Assert.Equal("ok", result);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task Handle_AttributePresent_SuccessOnFirstAttempt_ExecutesHandlerOnce()
    {
        var behavior = BuildBehavior<RetryableRequest, string>();
        int callCount = 0;

        var result = await behavior.Handle(
            new RetryableRequest(),
            (_, _) => { callCount++; return Task.FromResult("ok"); },
            CancellationToken.None);

        Assert.Equal("ok", result);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task Handle_AttributePresent_TransientFailureThenSuccess_RetriesUntilSuccess()
    {
        var behavior = BuildBehavior<RetryableRequest, string>();
        int callCount = 0;

        var result = await behavior.Handle(
            new RetryableRequest(),
            (_, _) =>
            {
                callCount++;
                if (callCount < 3)
                    throw new TransientException("transient");
                return Task.FromResult("ok");
            },
            CancellationToken.None);

        Assert.Equal("ok", result);
        Assert.Equal(3, callCount);
    }

    [Fact]
    public async Task Handle_AttributePresent_ExceedsMaxAttempts_ThrowsAfterAllRetries()
    {
        var behavior = BuildBehavior<RetryableRequest, string>();
        int callCount = 0;

        await Assert.ThrowsAsync<TransientException>(() =>
            behavior.Handle(
                new RetryableRequest(),
                (_, _) =>
                {
                    callCount++;
                    throw new TransientException("always fails");
                },
                CancellationToken.None));

        Assert.Equal(3, callCount);
    }

    [Fact]
    public async Task Handle_AttributePresent_NonRetryableException_DoesNotRetry()
    {
        var behavior = BuildBehavior<RetryableRequest, string>();
        int callCount = 0;

        await Assert.ThrowsAsync<NonTransientException>(() =>
            behavior.Handle(
                new RetryableRequest(),
                (_, _) =>
                {
                    callCount++;
                    throw new NonTransientException("non-retryable");
                },
                CancellationToken.None));

        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task Handle_AttributePresent_OperationCanceledException_DoesNotRetry()
    {
        var behavior = BuildBehavior<RetryableRequest, string>();
        int callCount = 0;
        using var cts = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            behavior.Handle(
                new RetryableRequest(),
                (_, _) =>
                {
                    callCount++;
                    throw new OperationCanceledException("cancelled");
                },
                cts.Token));

        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task Handle_MaxAttemptsOne_NeverRetries()
    {
        var behavior = BuildBehavior<SingleAttemptRequest, string>();
        int callCount = 0;

        await Assert.ThrowsAsync<TransientException>(() =>
            behavior.Handle(
                new SingleAttemptRequest(),
                (_, _) =>
                {
                    callCount++;
                    throw new TransientException("fails");
                },
                CancellationToken.None));

        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task Handle_AttributePresent_UnitResponse_RetriesCorrectly()
    {
        var behavior = BuildBehavior<RetryableUnitRequest, Unit>();
        int callCount = 0;

        var result = await behavior.Handle(
            new RetryableUnitRequest(),
            (_, _) =>
            {
                callCount++;
                if (callCount < 2)
                    throw new TransientException("transient");
                return Task.FromResult(Unit.Value);
            },
            CancellationToken.None);

        Assert.Equal(Unit.Value, result);
        Assert.Equal(2, callCount);
    }
}
