using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ModernMediator.Tests
{
    public class DiPublishPolicyTests
    {
        [Fact]
        public async Task StopOnFirstError_FirstHandlerThrows_PropagatesAndSkipsRemainingHandlers()
        {
            var handler1 = new RecordingNotificationHandler<TestNotification>();
            var handler2 = new RecordingNotificationHandler<TestNotification>
            {
                AlwaysThrow = new InvalidOperationException("h2")
            };
            var handler3 = new RecordingNotificationHandler<TestNotification>();

            var mediator = new MediatorTestHarness()
                .WithErrorPolicy(ErrorPolicy.StopOnFirstError)
                .WithHandler(handler1)
                .WithHandler(handler2)
                .WithHandler(handler3)
                .Build();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => mediator.Publish(new TestNotification("p"), CancellationToken.None));

            Assert.Equal(1, handler1.InvocationCount);
            Assert.Equal(1, handler2.InvocationCount);
            Assert.Equal(0, handler3.InvocationCount);
        }

        [Fact]
        public async Task LogAndContinue_HandlerThrows_DoesNotPropagate_AndContinuesToNextHandler()
        {
            var handler1 = new RecordingNotificationHandler<TestNotification>();
            var handler2 = new RecordingNotificationHandler<TestNotification>
            {
                AlwaysThrow = new InvalidOperationException("h2")
            };
            var handler3 = new RecordingNotificationHandler<TestNotification>();

            var mediator = new MediatorTestHarness()
                .WithErrorPolicy(ErrorPolicy.LogAndContinue)
                .WithHandler(handler1)
                .WithHandler(handler2)
                .WithHandler(handler3)
                .Build();

            await mediator.Publish(new TestNotification("p"), CancellationToken.None);

            Assert.Equal(1, handler1.InvocationCount);
            Assert.Equal(1, handler2.InvocationCount);
            Assert.Equal(1, handler3.InvocationCount);
        }

        [Fact]
        public async Task LogAndContinue_AllHandlersThrow_DoesNotPropagate_AllHandlersRan()
        {
            var handler1 = new RecordingNotificationHandler<TestNotification>
            {
                AlwaysThrow = new InvalidOperationException("h1")
            };
            var handler2 = new RecordingNotificationHandler<TestNotification>
            {
                AlwaysThrow = new ArgumentException("h2")
            };
            var handler3 = new RecordingNotificationHandler<TestNotification>
            {
                AlwaysThrow = new NotSupportedException("h3")
            };

            var mediator = new MediatorTestHarness()
                .WithErrorPolicy(ErrorPolicy.LogAndContinue)
                .WithHandler(handler1)
                .WithHandler(handler2)
                .WithHandler(handler3)
                .Build();

            await mediator.Publish(new TestNotification("p"), CancellationToken.None);

            Assert.Equal(1, handler1.InvocationCount);
            Assert.Equal(1, handler2.InvocationCount);
            Assert.Equal(1, handler3.InvocationCount);
        }

        [Fact]
        public async Task ContinueAndAggregate_OneHandlerThrows_AggregateExceptionThrownAfterAllHandlersRan()
        {
            var thrown = new InvalidOperationException("h2");
            var handler1 = new RecordingNotificationHandler<TestNotification>();
            var handler2 = new RecordingNotificationHandler<TestNotification> { AlwaysThrow = thrown };
            var handler3 = new RecordingNotificationHandler<TestNotification>();

            var mediator = new MediatorTestHarness()
                .WithErrorPolicy(ErrorPolicy.ContinueAndAggregate)
                .WithHandler(handler1)
                .WithHandler(handler2)
                .WithHandler(handler3)
                .Build();

            var ex = await Assert.ThrowsAsync<AggregateException>(
                () => mediator.Publish(new TestNotification("p"), CancellationToken.None));

            Assert.Equal(1, handler1.InvocationCount);
            Assert.Equal(1, handler2.InvocationCount);
            Assert.Equal(1, handler3.InvocationCount);
            Assert.Single(ex.InnerExceptions);
            Assert.Same(thrown, ex.InnerExceptions[0]);
        }

        [Fact]
        public async Task ContinueAndAggregate_MultipleHandlersThrow_AggregateContainsAllExceptions()
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

            var aggregate = await Assert.ThrowsAsync<AggregateException>(
                () => mediator.Publish(new TestNotification("p"), CancellationToken.None));

            Assert.Equal(1, handler1.InvocationCount);
            Assert.Equal(1, handler2.InvocationCount);
            Assert.Equal(1, handler3.InvocationCount);
            Assert.Equal(3, aggregate.InnerExceptions.Count);
            Assert.Contains(aggregate.InnerExceptions, e => e is InvalidOperationException);
            Assert.Contains(aggregate.InnerExceptions, e => e is ArgumentException);
            Assert.Contains(aggregate.InnerExceptions, e => e is NotSupportedException);
        }

        [Fact]
        public async Task NoHandlersFail_NoExceptionThrown_AllHandlersRan()
        {
            var handler1 = new RecordingNotificationHandler<TestNotification>();
            var handler2 = new RecordingNotificationHandler<TestNotification>();
            var handler3 = new RecordingNotificationHandler<TestNotification>();

            var mediator = new MediatorTestHarness()
                .WithErrorPolicy(ErrorPolicy.ContinueAndAggregate)
                .WithHandler(handler1)
                .WithHandler(handler2)
                .WithHandler(handler3)
                .Build();

            await mediator.Publish(new TestNotification("p"), CancellationToken.None);

            Assert.Equal(1, handler1.InvocationCount);
            Assert.Equal(1, handler2.InvocationCount);
            Assert.Equal(1, handler3.InvocationCount);
        }

        [Fact]
        public async Task NoHandlersRegistered_PublishCompletesSilently()
        {
            var mediator = new MediatorTestHarness()
                .WithErrorPolicy(ErrorPolicy.ContinueAndAggregate)
                .Build();

            await mediator.Publish(new TestNotification("p"), CancellationToken.None);
        }
    }
}
