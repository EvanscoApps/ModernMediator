using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ModernMediator.Tests
{
    public class DiPublishHandlerErrorEventTests
    {
        [Fact]
        public async Task HandlerError_FiresUnderStopOnFirstError_BeforePropagation()
        {
            var thrown = new InvalidOperationException("h1");
            var handler1 = new RecordingNotificationHandler<TestNotification> { AlwaysThrow = thrown };
            var handler2 = new RecordingNotificationHandler<TestNotification>();

            var mediator = new MediatorTestHarness()
                .WithErrorPolicy(ErrorPolicy.StopOnFirstError)
                .WithHandler(handler1)
                .WithHandler(handler2)
                .Build();

            var captured = new List<HandlerErrorEventArgs>();
            mediator.HandlerError += (_, args) => captured.Add(args);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => mediator.Publish(new TestNotification("p"), CancellationToken.None));

            Assert.Single(captured);
            Assert.Same(thrown, captured[0].Exception);
            Assert.Equal(0, handler2.InvocationCount);
        }

        [Fact]
        public async Task HandlerError_FiresUnderLogAndContinue_PerFailure()
        {
            var handler1 = new RecordingNotificationHandler<TestNotification> { AlwaysThrow = new InvalidOperationException("h1") };
            var handler2 = new RecordingNotificationHandler<TestNotification> { AlwaysThrow = new ArgumentException("h2") };
            var handler3 = new RecordingNotificationHandler<TestNotification> { AlwaysThrow = new NotSupportedException("h3") };

            var mediator = new MediatorTestHarness()
                .WithErrorPolicy(ErrorPolicy.LogAndContinue)
                .WithHandler(handler1)
                .WithHandler(handler2)
                .WithHandler(handler3)
                .Build();

            var captured = new List<HandlerErrorEventArgs>();
            mediator.HandlerError += (_, args) => captured.Add(args);

            await mediator.Publish(new TestNotification("p"), CancellationToken.None);

            Assert.Equal(3, captured.Count);
        }

        [Fact]
        public async Task HandlerError_FiresUnderContinueAndAggregate_PerFailure_AndExceptionsAlsoInAggregate()
        {
            var ex1 = new InvalidOperationException("h1");
            var ex2 = new ArgumentException("h2");
            var ex3 = new NotSupportedException("h3");
            var handler1 = new RecordingNotificationHandler<TestNotification> { AlwaysThrow = ex1 };
            var handler2 = new RecordingNotificationHandler<TestNotification> { AlwaysThrow = ex2 };
            var handler3 = new RecordingNotificationHandler<TestNotification> { AlwaysThrow = ex3 };

            var mediator = new MediatorTestHarness()
                .WithErrorPolicy(ErrorPolicy.ContinueAndAggregate)
                .WithHandler(handler1)
                .WithHandler(handler2)
                .WithHandler(handler3)
                .Build();

            var captured = new List<HandlerErrorEventArgs>();
            mediator.HandlerError += (_, args) => captured.Add(args);

            var aggregate = await Assert.ThrowsAsync<AggregateException>(
                () => mediator.Publish(new TestNotification("p"), CancellationToken.None));

            Assert.Equal(3, captured.Count);
            Assert.Equal(3, aggregate.InnerExceptions.Count);
            Assert.Contains(ex1, aggregate.InnerExceptions);
            Assert.Contains(ex2, aggregate.InnerExceptions);
            Assert.Contains(ex3, aggregate.InnerExceptions);
        }

        [Fact]
        public async Task HandlerErrorArgs_HandlerType_PopulatedFromConcreteHandlerType()
        {
            var handler = new RecordingNotificationHandler<TestNotification>
            {
                AlwaysThrow = new InvalidOperationException("boom")
            };

            var mediator = new MediatorTestHarness()
                .WithErrorPolicy(ErrorPolicy.LogAndContinue)
                .WithHandler(handler)
                .Build();

            HandlerErrorEventArgs? captured = null;
            mediator.HandlerError += (_, args) => captured = args;

            await mediator.Publish(new TestNotification("p"), CancellationToken.None);

            Assert.NotNull(captured);
            Assert.Equal(typeof(RecordingNotificationHandler<TestNotification>), captured!.HandlerType);
        }

        [Fact]
        public async Task HandlerErrorArgs_HandlerInstance_PopulatedFromResolvedInstance()
        {
            var handler = new RecordingNotificationHandler<TestNotification>
            {
                AlwaysThrow = new InvalidOperationException("boom")
            };

            var mediator = new MediatorTestHarness()
                .WithErrorPolicy(ErrorPolicy.LogAndContinue)
                .WithHandler(handler)
                .Build();

            HandlerErrorEventArgs? captured = null;
            mediator.HandlerError += (_, args) => captured = args;

            await mediator.Publish(new TestNotification("p"), CancellationToken.None);

            Assert.NotNull(captured);
            Assert.Same(handler, captured!.HandlerInstance);
        }

        [Fact]
        public async Task HandlerErrorArgs_NotificationTypeAndMessage_PopulatedCorrectly()
        {
            var handler = new RecordingNotificationHandler<TestNotification>
            {
                AlwaysThrow = new InvalidOperationException("boom")
            };

            var mediator = new MediatorTestHarness()
                .WithErrorPolicy(ErrorPolicy.LogAndContinue)
                .WithHandler(handler)
                .Build();

            HandlerErrorEventArgs? captured = null;
            mediator.HandlerError += (_, args) => captured = args;

            var published = new TestNotification("p");
            await mediator.Publish(published, CancellationToken.None);

            Assert.NotNull(captured);
            Assert.Equal(typeof(TestNotification), captured!.MessageType);
            Assert.Same(published, captured.Message);
        }

        [Fact]
        public async Task SubscriberThrowsException_ContainedAndRoutedToSink_DispatchContinues()
        {
            var handler1 = new RecordingNotificationHandler<TestNotification> { AlwaysThrow = new InvalidOperationException("h1") };
            var handler2 = new RecordingNotificationHandler<TestNotification> { AlwaysThrow = new ArgumentException("h2") };

            var mediator = new MediatorTestHarness()
                .WithErrorPolicy(ErrorPolicy.LogAndContinue)
                .WithRecordingSink(out var sink)
                .WithHandler(handler1)
                .WithHandler(handler2)
                .Build();

            mediator.HandlerError += (_, _) => throw new Exception("subscriber boom");

            await mediator.Publish(new TestNotification("p"), CancellationToken.None);

            Assert.Equal(1, handler1.InvocationCount);
            Assert.Equal(1, handler2.InvocationCount);
            Assert.Equal(2, sink.ReceivedCount);
        }

        [Fact]
        public async Task MultipleSubscribers_OneThrows_OthersStillInvoked()
        {
            var handler = new RecordingNotificationHandler<TestNotification>
            {
                AlwaysThrow = new InvalidOperationException("boom")
            };

            var mediator = new MediatorTestHarness()
                .WithErrorPolicy(ErrorPolicy.LogAndContinue)
                .WithRecordingSink(out var sink)
                .WithHandler(handler)
                .Build();

            int subA = 0, subC = 0;
            mediator.HandlerError += (_, _) => Interlocked.Increment(ref subA);
            mediator.HandlerError += (_, _) => throw new Exception("sub B boom");
            mediator.HandlerError += (_, _) => Interlocked.Increment(ref subC);

            await mediator.Publish(new TestNotification("p"), CancellationToken.None);

            Assert.Equal(1, subA);
            Assert.Equal(1, subC);
            Assert.Equal(1, sink.ReceivedCount);
        }
    }
}
