using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ModernMediator.Tests;

public sealed class CircuitBreakerBehaviorTests
{
    // --- Test doubles ---

    private sealed class NonProtectedRequest : IRequest<string> { }

    [CircuitBreaker(failureThreshold: 3, durationSeconds: 30, SamplingDurationSeconds = 10)]
    private sealed class ProtectedRequest : IRequest<string> { }

    [CircuitBreaker(failureThreshold: 2, durationSeconds: 5, SamplingDurationSeconds = 10)]
    private sealed class QuickTripRequest : IRequest<string> { }

    [CircuitBreaker(failureThreshold: 3, durationSeconds: 30, SamplingDurationSeconds = 10)]
    private sealed class ProtectedUnitRequest : IRequest<Unit> { }

    // --- Helpers ---

    private static CircuitBreakerBehavior<TRequest, TResponse>
        BuildBehavior<TRequest, TResponse>()
        where TRequest : IRequest<TResponse> =>
        new CircuitBreakerBehavior<TRequest, TResponse>(new CircuitBreakerRegistry());

    private static RequestHandlerDelegate<TRequest, TResponse>
        AlwaysSucceeds<TRequest, TResponse>(TResponse value) =>
        (_, _) => Task.FromResult(value);

    private static RequestHandlerDelegate<TRequest, TResponse>
        AlwaysThrows<TRequest, TResponse>(Exception ex) =>
        (_, _) => Task.FromException<TResponse>(ex);

    // --- Tests ---

    [Fact]
    public async Task Handle_NoAttribute_ExecutesHandlerWithoutCircuitBreaker()
    {
        var behavior = BuildBehavior<NonProtectedRequest, string>();

        var result = await behavior.Handle(
            new NonProtectedRequest(),
            AlwaysSucceeds<NonProtectedRequest, string>("ok"),
            CancellationToken.None);

        Assert.Equal("ok", result);
    }

    [Fact]
    public async Task Handle_AttributePresent_SuccessfulHandler_ReturnsResult()
    {
        var behavior = BuildBehavior<ProtectedRequest, string>();

        var result = await behavior.Handle(
            new ProtectedRequest(),
            AlwaysSucceeds<ProtectedRequest, string>("ok"),
            CancellationToken.None);

        Assert.Equal("ok", result);
    }

    [Fact]
    public async Task Handle_AttributePresent_FailingHandler_ThrowsOriginalException()
    {
        var behavior = BuildBehavior<ProtectedRequest, string>();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            behavior.Handle(
                new ProtectedRequest(),
                AlwaysThrows<ProtectedRequest, string>(new InvalidOperationException("fail")),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_AttributePresent_ExceedsFailureThreshold_CircuitOpens()
    {
        // Use a fresh registry so circuit state is isolated to this test
        var registry = new CircuitBreakerRegistry();
        var behavior = new CircuitBreakerBehavior<QuickTripRequest, string>(registry);

        // Trip the circuit by hitting the failure threshold
        for (int i = 0; i < 2; i++)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                behavior.Handle(
                    new QuickTripRequest(),
                    AlwaysThrows<QuickTripRequest, string>(
                        new InvalidOperationException("fail")),
                    CancellationToken.None));
        }

        // Next call should fail fast with CircuitBreakerOpenException
        var ex = await Assert.ThrowsAsync<CircuitBreakerOpenException>(() =>
            behavior.Handle(
                new QuickTripRequest(),
                AlwaysSucceeds<QuickTripRequest, string>("ok"),
                CancellationToken.None));

        Assert.Contains("QuickTripRequest", ex.RequestTypeName);
        Assert.True(ex.RetryAfter > TimeSpan.Zero);
    }

    [Fact]
    public async Task Handle_AttributePresent_CircuitOpen_DoesNotExecuteHandler()
    {
        var registry = new CircuitBreakerRegistry();
        var behavior = new CircuitBreakerBehavior<QuickTripRequest, string>(registry);
        int handlerCallCount = 0;

        // Trip the circuit
        for (int i = 0; i < 2; i++)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                behavior.Handle(
                    new QuickTripRequest(),
                    AlwaysThrows<QuickTripRequest, string>(
                        new InvalidOperationException("fail")),
                    CancellationToken.None));
        }

        // Attempt while open — handler must not execute
        await Assert.ThrowsAsync<CircuitBreakerOpenException>(() =>
            behavior.Handle(
                new QuickTripRequest(),
                (_, _) =>
                {
                    handlerCallCount++;
                    return Task.FromResult("ok");
                },
                CancellationToken.None));

        Assert.Equal(0, handlerCallCount);
    }

    [Fact]
    public async Task Handle_AttributePresent_OperationCanceledException_DoesNotTripCircuit()
    {
        var registry = new CircuitBreakerRegistry();
        var behavior = new CircuitBreakerBehavior<QuickTripRequest, string>(registry);

        // OperationCanceledException should not count as a failure
        for (int i = 0; i < 5; i++)
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                behavior.Handle(
                    new QuickTripRequest(),
                    AlwaysThrows<QuickTripRequest, string>(
                        new OperationCanceledException("cancelled")),
                    CancellationToken.None));
        }

        // Circuit should still be closed — next call with a real handler should succeed
        var result = await behavior.Handle(
            new QuickTripRequest(),
            AlwaysSucceeds<QuickTripRequest, string>("ok"),
            CancellationToken.None);

        Assert.Equal("ok", result);
    }

    [Fact]
    public async Task Handle_AttributePresent_UnitResponse_CircuitBreakerEngages()
    {
        var registry = new CircuitBreakerRegistry();
        var behavior = new CircuitBreakerBehavior<ProtectedUnitRequest, Unit>(registry);

        var result = await behavior.Handle(
            new ProtectedUnitRequest(),
            AlwaysSucceeds<ProtectedUnitRequest, Unit>(Unit.Value),
            CancellationToken.None);

        Assert.Equal(Unit.Value, result);
    }

    [Fact]
    public async Task Handle_DifferentRequestTypes_IndependentCircuitState()
    {
        // Both behaviors share the same registry but different request types
        var registry = new CircuitBreakerRegistry();
        var quickBehavior = new CircuitBreakerBehavior<QuickTripRequest, string>(registry);
        var protectedBehavior = new CircuitBreakerBehavior<ProtectedRequest, string>(registry);

        // Trip the QuickTripRequest circuit
        for (int i = 0; i < 2; i++)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                quickBehavior.Handle(
                    new QuickTripRequest(),
                    AlwaysThrows<QuickTripRequest, string>(
                        new InvalidOperationException("fail")),
                    CancellationToken.None));
        }

        await Assert.ThrowsAsync<CircuitBreakerOpenException>(() =>
            quickBehavior.Handle(
                new QuickTripRequest(),
                AlwaysSucceeds<QuickTripRequest, string>("ok"),
                CancellationToken.None));

        // ProtectedRequest circuit must still be closed
        var result = await protectedBehavior.Handle(
            new ProtectedRequest(),
            AlwaysSucceeds<ProtectedRequest, string>("ok"),
            CancellationToken.None);

        Assert.Equal("ok", result);
    }
}
