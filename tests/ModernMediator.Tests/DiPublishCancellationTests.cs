using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ModernMediator.Tests
{
    public class DiPublishCancellationTests
    {
        [Fact]
        public async Task CancellationRequestedBeforePublish_PropagatesImmediately_NoHandlerInvoked()
        {
            var handler = new RecordingNotificationHandler<TestNotification>();

            var mediator = new MediatorTestHarness()
                .WithErrorPolicy(ErrorPolicy.LogAndContinue)
                .WithHandler(handler)
                .Build();

            var captured = new List<HandlerErrorEventArgs>();
            mediator.HandlerError += (_, args) => captured.Add(args);

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(
                () => mediator.Publish(new TestNotification("p"), cts.Token));

            Assert.Equal(0, handler.InvocationCount);
            Assert.Empty(captured);
        }

        [Fact]
        public async Task CancellationDuringDispatch_SecondHandlerObservesCancellation_PropagatesAsCancellation()
        {
            using var cts = new CancellationTokenSource();
            var canceller = new CancellingNotificationHandler<TestNotification>(cts);
            var handler2 = new RecordingNotificationHandler<TestNotification> { ObserveCancellation = true };
            var handler3 = new RecordingNotificationHandler<TestNotification>();

            var sink = default(RecordingSubscriberExceptionSink);
            var handlerErrorFireCount = 0;

            var mediator = new MediatorTestHarness()
                .WithErrorPolicy(ErrorPolicy.LogAndContinue)
                .WithRecordingSink(out sink!)
                .WithHandler(canceller)
                .WithHandler(handler2)
                .WithHandler(handler3)
                .Build();

            mediator.HandlerError += (_, _) => handlerErrorFireCount++;

            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                mediator.Publish(new TestNotification("p"), cts.Token));

            // canceller ran (handler 1)
            Assert.Equal(1, canceller.InvocationCount);
            // handler2 was reached, observed cancellation, threw OCE
            Assert.Equal(1, handler2.InvocationCount);
            // handler3 was never reached
            Assert.Equal(0, handler3.InvocationCount);
            // HandlerError did NOT fire;the OCE under cancelled token is cooperative cancellation
            Assert.Equal(0, handlerErrorFireCount);
        }

        [Fact]
        public async Task CooperativeCancellation_BypassesContinueAndAggregate_NoPartialAggregate()
        {
            using var cts = new CancellationTokenSource();
            var failingHandler = new RecordingNotificationHandler<TestNotification>
            {
                AlwaysThrow = new InvalidOperationException("h1 fault")
            };
            var canceller = new CancellingNotificationHandler<TestNotification>(cts);
            var handler3 = new RecordingNotificationHandler<TestNotification> { ObserveCancellation = true };
            var handler4 = new RecordingNotificationHandler<TestNotification>();

            var handlerErrorEventArgs = new System.Collections.Generic.List<HandlerErrorEventArgs>();

            var mediator = new MediatorTestHarness()
                .WithErrorPolicy(ErrorPolicy.ContinueAndAggregate)
                .WithHandler(failingHandler)
                .WithHandler(canceller)
                .WithHandler(handler3)
                .WithHandler(handler4)
                .Build();

            mediator.HandlerError += (_, args) => handlerErrorEventArgs.Add(args);

            // Expect OperationCanceledException, NOT AggregateException;the cancellation
            // discards any partial aggregate per ADR-006.
            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                mediator.Publish(new TestNotification("p"), cts.Token));

            // failingHandler ran and threw;HandlerError fired for it
            Assert.Equal(1, failingHandler.InvocationCount);
            // canceller ran and cancelled the token
            Assert.Equal(1, canceller.InvocationCount);
            // handler3 was reached, observed cancellation, threw OCE
            Assert.Equal(1, handler3.InvocationCount);
            // handler4 was never reached
            Assert.Equal(0, handler4.InvocationCount);
            // HandlerError fired exactly once;for failingHandler's InvalidOperationException, NOT for the OCE
            Assert.Single(handlerErrorEventArgs);
            Assert.IsType<InvalidOperationException>(handlerErrorEventArgs[0].Exception);
        }

        [Fact]
        public async Task OperationCanceledExceptionWithUnrelatedToken_TreatedAsHandlerFault()
        {
            using var unrelatedCts = new CancellationTokenSource();
            var oce = new OperationCanceledException(unrelatedCts.Token);

            var handler = new RecordingNotificationHandler<TestNotification>
            {
                AlwaysThrow = oce,
                ObserveCancellation = false
            };

            var mediator = new MediatorTestHarness()
                .WithErrorPolicy(ErrorPolicy.LogAndContinue)
                .WithHandler(handler)
                .Build();

            var captured = new List<HandlerErrorEventArgs>();
            mediator.HandlerError += (_, args) => captured.Add(args);

            await mediator.Publish(new TestNotification("p"), CancellationToken.None);

            Assert.Single(captured);
            Assert.Same(oce, captured[0].Exception);
        }
    }
}
