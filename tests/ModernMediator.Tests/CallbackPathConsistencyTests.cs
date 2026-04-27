using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ModernMediator.Tests
{
    public class CallbackPathConsistencyTests
    {
        [Fact]
        public void HandlerError_FiresUnderStopOnFirstError_OnCallbackPath()
        {
            var capturedArgs = new List<HandlerErrorEventArgs>();
            var secondInvoked = false;

            var mediator = new CallbackTestHarness()
                .WithErrorPolicy(ErrorPolicy.StopOnFirstError)
                .Build();
            mediator.HandlerError += (_, args) => capturedArgs.Add(args);

            using var token1 = mediator.Subscribe<TestNotification>(
                n => throw new InvalidOperationException("sub1"));
            using var token2 = mediator.Subscribe<TestNotification>(
                n => secondInvoked = true);

            // StopOnFirstError causes HandleError to rethrow the actual exception,
            // which propagates out of Publish<T>.
            Assert.Throws<InvalidOperationException>(
                () => mediator.Publish(new TestNotification("p")));

            Assert.Single(capturedArgs);
            Assert.False(secondInvoked);
        }

        [Fact]
        public void HandlerError_FiresUnderLogAndContinue_OnCallbackPath()
        {
            var capturedArgs = new List<HandlerErrorEventArgs>();

            var mediator = new CallbackTestHarness()
                .WithErrorPolicy(ErrorPolicy.LogAndContinue)
                .Build();
            mediator.HandlerError += (_, args) => capturedArgs.Add(args);

            using var t1 = mediator.Subscribe<TestNotification>(
                n => throw new InvalidOperationException("sub1"));
            using var t2 = mediator.Subscribe<TestNotification>(
                n => throw new ArgumentException("sub2"));
            using var t3 = mediator.Subscribe<TestNotification>(
                n => throw new NotSupportedException("sub3"));

            mediator.Publish(new TestNotification("p"));

            Assert.Equal(3, capturedArgs.Count);
        }

        [Fact]
        public void SubscriberThrowsException_ContainedAndRoutedToSink_OnCallbackPath()
        {
            var mediator = new CallbackTestHarness()
                .WithErrorPolicy(ErrorPolicy.LogAndContinue)
                .WithRecordingSink(out var sink)
                .Build();

            mediator.HandlerError += (_, _) => throw new Exception("subscriber boom");

            using var token = mediator.Subscribe<TestNotification>(
                n => throw new InvalidOperationException("handler boom"));

            mediator.Publish(new TestNotification("p"));

            Assert.Equal(1, sink.ReceivedCount);
        }

        [Fact]
        public async Task CooperativeCancellation_DoesNotFireHandlerError_OnCallbackPath()
        {
            var capturedArgs = new List<HandlerErrorEventArgs>();
            var mediator = new CallbackTestHarness()
                .WithErrorPolicy(ErrorPolicy.LogAndContinue)
                .Build();
            mediator.HandlerError += (_, args) => capturedArgs.Add(args);

            using var cts = new CancellationTokenSource();

            using var token = mediator.SubscribeAsync<TestNotification>(async n =>
            {
                cts.Cancel();
                await Task.Yield();
                cts.Token.ThrowIfCancellationRequested();
            });

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => mediator.PublishAsyncTrue(new TestNotification("p"), cts.Token));

            // OCE propagated due to cooperative cancellation — HandlerError must NOT fire.
            Assert.Empty(capturedArgs);
        }
    }
}
