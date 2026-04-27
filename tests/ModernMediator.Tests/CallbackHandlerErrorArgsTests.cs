using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Xunit;

namespace ModernMediator.Tests
{
    public sealed class TestSubscriberFixture
    {
        public void ThrowingHandler(TestNotification notification)
        {
            throw new InvalidOperationException("instance method threw");
        }

        public static void StaticThrowingHandler(TestNotification notification)
        {
            throw new InvalidOperationException("static method threw");
        }
    }

    public class CallbackHandlerErrorArgsTests
    {
        [Fact]
        public void LambdaSubscriber_HandlerErrorArgs_HandlerType_IsEnclosingUserType()
        {
            var capturedArgs = new List<HandlerErrorEventArgs>();
            var mediator = new CallbackTestHarness()
                .WithErrorPolicy(ErrorPolicy.LogAndContinue)
                .Build();
            mediator.HandlerError += (_, args) => capturedArgs.Add(args);

            using var token = mediator.Subscribe<TestNotification>(
                n => throw new InvalidOperationException("lambda"));

            mediator.Publish(new TestNotification("p"));

            Assert.Single(capturedArgs);
            Assert.NotNull(capturedArgs[0].HandlerType);
            Assert.Equal(typeof(CallbackHandlerErrorArgsTests), capturedArgs[0].HandlerType);
            Assert.Null(capturedArgs[0].HandlerType!.GetCustomAttribute<CompilerGeneratedAttribute>());
        }

        [Fact]
        public void LambdaSubscriber_HandlerErrorArgs_HandlerInstance_IsClosureTarget()
        {
            var capturedArgs = new List<HandlerErrorEventArgs>();
            var mediator = new CallbackTestHarness()
                .WithErrorPolicy(ErrorPolicy.LogAndContinue)
                .Build();
            mediator.HandlerError += (_, args) => capturedArgs.Add(args);

            using var token = mediator.Subscribe<TestNotification>(
                n => throw new InvalidOperationException("lambda"));

            mediator.Publish(new TestNotification("p"));

            Assert.Single(capturedArgs);
            Assert.NotNull(capturedArgs[0].HandlerInstance);
        }

        [Fact]
        public void NamedInstanceMethodSubscriber_HandlerErrorArgs_HandlerType_IsDeclaringType()
        {
            var fixture = new TestSubscriberFixture();
            var capturedArgs = new List<HandlerErrorEventArgs>();
            var mediator = new CallbackTestHarness()
                .WithErrorPolicy(ErrorPolicy.LogAndContinue)
                .Build();
            mediator.HandlerError += (_, args) => capturedArgs.Add(args);

            using var token = mediator.Subscribe<TestNotification>(fixture.ThrowingHandler);

            mediator.Publish(new TestNotification("p"));

            Assert.Single(capturedArgs);
            Assert.Equal(typeof(TestSubscriberFixture), capturedArgs[0].HandlerType);
            Assert.Same(fixture, capturedArgs[0].HandlerInstance);
        }

        [Fact]
        public void NamedStaticMethodSubscriber_HandlerErrorArgs_HandlerInstance_IsNull()
        {
            var capturedArgs = new List<HandlerErrorEventArgs>();
            var mediator = new CallbackTestHarness()
                .WithErrorPolicy(ErrorPolicy.LogAndContinue)
                .Build();
            mediator.HandlerError += (_, args) => capturedArgs.Add(args);

            using var token = mediator.Subscribe<TestNotification>(TestSubscriberFixture.StaticThrowingHandler);

            mediator.Publish(new TestNotification("p"));

            Assert.Single(capturedArgs);
            Assert.Equal(typeof(TestSubscriberFixture), capturedArgs[0].HandlerType);
            Assert.Null(capturedArgs[0].HandlerInstance);
        }

        [Fact]
        public async Task AsyncLambdaSubscriber_HandlerErrorArgs_HandlerType_IsEnclosingUserType()
        {
            var capturedArgs = new List<HandlerErrorEventArgs>();
            var mediator = new CallbackTestHarness()
                .WithErrorPolicy(ErrorPolicy.LogAndContinue)
                .Build();
            mediator.HandlerError += (_, args) => capturedArgs.Add(args);

            using var token = mediator.SubscribeAsync<TestNotification>(
                async n =>
                {
                    await Task.Yield();
                    throw new InvalidOperationException("async lambda");
                });

            await mediator.PublishAsyncTrue(new TestNotification("p"));

            Assert.NotEmpty(capturedArgs);
            Assert.Equal(typeof(CallbackHandlerErrorArgsTests), capturedArgs[0].HandlerType);
            Assert.Null(capturedArgs[0].HandlerType!.GetCustomAttribute<CompilerGeneratedAttribute>());
        }

        [Fact]
        public void WeakSubscription_HandlerErrorArgs_PopulatedSameAsStrongSubscription()
        {
            var fixture = new TestSubscriberFixture();
            var capturedArgs = new List<HandlerErrorEventArgs>();
            var mediator = new CallbackTestHarness()
                .WithErrorPolicy(ErrorPolicy.LogAndContinue)
                .Build();
            mediator.HandlerError += (_, args) => capturedArgs.Add(args);

            using var token = mediator.Subscribe<TestNotification>(fixture.ThrowingHandler, weak: true);

            mediator.Publish(new TestNotification("p"));

            Assert.Single(capturedArgs);
            Assert.Equal(typeof(TestSubscriberFixture), capturedArgs[0].HandlerType);
            Assert.Same(fixture, capturedArgs[0].HandlerInstance);
        }

        [Fact]
        public void StrongSubscription_HandlerErrorArgs_PopulatedSameAsWeakSubscription()
        {
            var fixture = new TestSubscriberFixture();
            var capturedArgs = new List<HandlerErrorEventArgs>();
            var mediator = new CallbackTestHarness()
                .WithErrorPolicy(ErrorPolicy.LogAndContinue)
                .Build();
            mediator.HandlerError += (_, args) => capturedArgs.Add(args);

            using var token = mediator.Subscribe<TestNotification>(fixture.ThrowingHandler, weak: false);

            mediator.Publish(new TestNotification("p"));

            Assert.Single(capturedArgs);
            Assert.Equal(typeof(TestSubscriberFixture), capturedArgs[0].HandlerType);
            Assert.Same(fixture, capturedArgs[0].HandlerInstance);
        }
    }
}
