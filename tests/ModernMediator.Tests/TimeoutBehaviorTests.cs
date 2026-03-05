using Xunit;

namespace ModernMediator.Tests
{
    [Timeout(500)]
    public record TimedRequest(string Value) : IRequest<string>;

    public record UntimedRequest(string Value) : IRequest<string>;

    [Timeout(50)]
    public record ShortTimedRequest(string Value) : IRequest<string>;

    public class TimeoutBehaviorTests
    {
        [Fact]
        public async Task Handle_CompletesInTime_ReturnsResult()
        {
            var behavior = new TimeoutBehavior<TimedRequest, string>();
            RequestHandlerDelegate<string> next = () => Task.FromResult("ok");

            var result = await behavior.Handle(
                new TimedRequest("test"), next, CancellationToken.None);

            Assert.Equal("ok", result);
        }

        [Fact]
        public async Task Handle_ExceedsTimeout_ThrowsOperationCanceledException()
        {
            var behavior = new TimeoutBehavior<ShortTimedRequest, string>();
            RequestHandlerDelegate<string> next = async () =>
            {
                await Task.Delay(5000);
                return "should not reach";
            };

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => behavior.Handle(
                    new ShortTimedRequest("test"), next, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_NoTimeoutAttribute_PassesThroughDirectly()
        {
            var behavior = new TimeoutBehavior<UntimedRequest, string>();
            bool nextCalled = false;
            RequestHandlerDelegate<string> next = () =>
            {
                nextCalled = true;
                return Task.FromResult("pass-through");
            };

            var result = await behavior.Handle(
                new UntimedRequest("test"), next, CancellationToken.None);

            Assert.True(nextCalled);
            Assert.Equal("pass-through", result);
        }

        [Fact]
        public void TimeoutAttribute_ZeroValue_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TimeoutAttribute(0));
        }

        [Fact]
        public void TimeoutAttribute_NegativeValue_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TimeoutAttribute(-100));
        }

        [Fact]
        public async Task Handle_ExternalTokenCancelled_ThrowsOperationCanceledException()
        {
            var behavior = new TimeoutBehavior<TimedRequest, string>();
            using var cts = new CancellationTokenSource();
            RequestHandlerDelegate<string> next = async () =>
            {
                await Task.Delay(5000);
                return "should not reach";
            };

            cts.CancelAfter(30);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => behavior.Handle(
                    new TimedRequest("test"), next, cts.Token));
        }

        [Fact]
        public async Task Handle_TimeoutFires_LinkedTokenCancelledEvenIfOriginalTokenWasNot()
        {
            var behavior = new TimeoutBehavior<ShortTimedRequest, string>();
            using var externalCts = new CancellationTokenSource();
            RequestHandlerDelegate<string> next = async () =>
            {
                await Task.Delay(5000);
                return "should not reach";
            };

            // External token is NOT cancelled — the 50ms timeout should still fire
            var ex = await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => behavior.Handle(
                    new ShortTimedRequest("test"), next, externalCts.Token));

            // The external token was never cancelled
            Assert.False(externalCts.IsCancellationRequested);
        }
    }
}
