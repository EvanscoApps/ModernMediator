using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ModernMediator.Tests;

public sealed class IdempotencyBehaviorTests
{
    // --- Test doubles ---

    private sealed class NonIdempotentRequest : IRequest<string> { }

    [Idempotent(windowSeconds: 60)]
    private sealed class IdempotentRequest : IRequest<string>
    {
        public string Value { get; init; } = string.Empty;
    }

    [Idempotent(windowSeconds: 60)]
    private sealed class IdempotentUnitRequest : IRequest<Unit> { }

    private sealed class TrackingIdempotencyStore : IIdempotencyStore
    {
        private readonly System.Collections.Generic.Dictionary<string, object?> _store = new();
        public int SetCallCount { get; private set; }
        public int GetCallCount { get; private set; }

        public ValueTask<IdempotencyEntry<TResponse>?> TryGetAsync<TResponse>(
            string fingerprint, CancellationToken cancellationToken)
        {
            GetCallCount++;
            if (_store.TryGetValue(fingerprint, out var value))
                return ValueTask.FromResult((IdempotencyEntry<TResponse>?)value);
            return ValueTask.FromResult<IdempotencyEntry<TResponse>?>(null);
        }

        public ValueTask SetAsync<TResponse>(
            string fingerprint, TResponse response, TimeSpan window,
            CancellationToken cancellationToken)
        {
            SetCallCount++;
            _store[fingerprint] = new IdempotencyEntry<TResponse>
            {
                Response = response,
                CachedAt = DateTimeOffset.UtcNow
            };
            return ValueTask.CompletedTask;
        }
    }

    // --- Helpers ---

    private sealed class CallCounter
    {
        public int Count;
    }

    private static RequestHandlerDelegate<TRequest, TResponse> HandlerReturning<TRequest, TResponse>(
        TResponse value, CallCounter counter)
        where TRequest : IRequest<TResponse>
    {
        return (_, _) =>
        {
            counter.Count++;
            return Task.FromResult(value);
        };
    }

    // --- Tests ---

    [Fact]
    public async Task Handle_NoAttribute_ExecutesHandlerWithoutStoreLookup()
    {
        var store = new TrackingIdempotencyStore();
        var behavior = new IdempotencyBehavior<NonIdempotentRequest, string>(store);
        var counter = new CallCounter();

        var result = await behavior.Handle(
            new NonIdempotentRequest(),
            HandlerReturning<NonIdempotentRequest, string>("result", counter),
            CancellationToken.None);

        Assert.Equal("result", result);
        Assert.Equal(1, counter.Count);
        Assert.Equal(0, store.GetCallCount);
        Assert.Equal(0, store.SetCallCount);
    }

    [Fact]
    public async Task Handle_AttributePresent_FirstCall_ExecutesHandlerAndStoresResult()
    {
        var store = new TrackingIdempotencyStore();
        var behavior = new IdempotencyBehavior<IdempotentRequest, string>(store);
        var counter = new CallCounter();

        var result = await behavior.Handle(
            new IdempotentRequest { Value = "test" },
            HandlerReturning<IdempotentRequest, string>("response", counter),
            CancellationToken.None);

        Assert.Equal("response", result);
        Assert.Equal(1, counter.Count);
        Assert.Equal(1, store.GetCallCount);
        Assert.Equal(1, store.SetCallCount);
    }

    [Fact]
    public async Task Handle_AttributePresent_SecondCallSameRequest_ReturnsCachedResultWithoutExecutingHandler()
    {
        var store = new TrackingIdempotencyStore();
        var behavior = new IdempotencyBehavior<IdempotentRequest, string>(store);
        var counter = new CallCounter();
        var request = new IdempotentRequest { Value = "test" };
        var handler = HandlerReturning<IdempotentRequest, string>("response", counter);

        await behavior.Handle(request, handler, CancellationToken.None);
        var secondResult = await behavior.Handle(request, handler, CancellationToken.None);

        Assert.Equal("response", secondResult);
        Assert.Equal(1, counter.Count);
        Assert.Equal(2, store.GetCallCount);
        Assert.Equal(1, store.SetCallCount);
    }

    [Fact]
    public async Task Handle_AttributePresent_DifferentPayloads_ExecutesHandlerForEach()
    {
        var store = new TrackingIdempotencyStore();
        var behavior = new IdempotencyBehavior<IdempotentRequest, string>(store);
        var counter = new CallCounter();

        await behavior.Handle(
            new IdempotentRequest { Value = "first" },
            HandlerReturning<IdempotentRequest, string>("response-a", counter),
            CancellationToken.None);

        await behavior.Handle(
            new IdempotentRequest { Value = "second" },
            HandlerReturning<IdempotentRequest, string>("response-b", counter),
            CancellationToken.None);

        Assert.Equal(2, counter.Count);
        Assert.Equal(2, store.SetCallCount);
    }

    [Fact]
    public async Task Handle_AttributePresent_UnitResponse_StoresAndReturnsCachedUnit()
    {
        var store = new TrackingIdempotencyStore();
        var behavior = new IdempotencyBehavior<IdempotentUnitRequest, Unit>(store);
        var counter = new CallCounter();
        var request = new IdempotentUnitRequest();
        var handler = HandlerReturning<IdempotentUnitRequest, Unit>(Unit.Value, counter);

        await behavior.Handle(request, handler, CancellationToken.None);
        await behavior.Handle(request, handler, CancellationToken.None);

        Assert.Equal(1, counter.Count);
    }

    [Fact]
    public async Task Handle_AttributePresent_HandlerThrows_DoesNotCacheResult()
    {
        var store = new TrackingIdempotencyStore();
        var behavior = new IdempotencyBehavior<IdempotentRequest, string>(store);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            behavior.Handle(
                new IdempotentRequest { Value = "test" },
                (_, _) => Task.FromException<string>(new InvalidOperationException("handler failed")),
                CancellationToken.None));

        Assert.Equal(0, store.SetCallCount);
    }
}
