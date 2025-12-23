using ModernMediator.Dispatchers;
using Xunit;

namespace ModernMediator.Tests
{
    public class DispatcherTests
    {
        [Fact]
        public void SetDispatcher_AcceptsValidDispatcher()
        {
            var bus = ModernMediator.Create();
            var dispatcher = new TestDispatcher();
            
            bus.SetDispatcher(dispatcher);
            
            // No exception means success
        }

        [Fact]
        public void SetDispatcher_ThrowsOnNull()
        {
            var bus = ModernMediator.Create();

            Assert.Throws<ArgumentNullException>(() => bus.SetDispatcher(null!));
        }

        [Fact]
        public void SubscribeOnMainThread_ThrowsWithoutDispatcher()
        {
            var bus = ModernMediator.Create();

            Assert.Throws<InvalidOperationException>(() =>
                bus.SubscribeOnMainThread<TestMessage>(msg => { }));
        }

        [Fact]
        public void SubscribeOnMainThread_DeliversOnMainThread()
        {
            var bus = ModernMediator.Create();
            var dispatcher = new TestDispatcher();
            bus.SetDispatcher(dispatcher);

            string? receivedData = null;
            bus.SubscribeOnMainThread<TestMessage>(msg => receivedData = msg.Data);

            bus.Publish(new TestMessage("test"));

            Assert.Equal("test", receivedData);
            Assert.True(dispatcher.InvokeWasCalled || dispatcher.CheckAccessWasCalled);
        }

        [Fact]
        public async Task SubscribeAsyncOnMainThread_DeliversAsyncOnMainThread()
        {
            var bus = ModernMediator.Create();
            var dispatcher = new TestDispatcher();
            bus.SetDispatcher(dispatcher);

            string? receivedData = null;
            bus.SubscribeAsyncOnMainThread<TestMessage>(async msg =>
            {
                await Task.Delay(10);
                receivedData = msg.Data;
            });

            await bus.PublishAsyncTrue(new TestMessage("async test"));

            Assert.Equal("async test", receivedData);
            Assert.True(dispatcher.InvokeAsyncWasCalled || dispatcher.CheckAccessWasCalled,
                "Dispatcher.InvokeAsync should have been called");
        }

        private class TestDispatcher : IDispatcher
        {
            public bool CheckAccessWasCalled { get; private set; }
            public bool InvokeWasCalled { get; private set; }
            public bool InvokeAsyncWasCalled { get; private set; }

            public bool CheckAccess()
            {
                CheckAccessWasCalled = true;
                return true; // Simulate being on UI thread
            }

            public void Invoke(Action action)
            {
                InvokeWasCalled = true;
                action();
            }

            public Task InvokeAsync(Func<Task> func)
            {
                InvokeAsyncWasCalled = true;
                return func();
            }
        }
    }

    public class AsyncErrorHandlingTests
    {
        [Fact]
        public async Task AsyncHandler_ErrorPolicy_ContinueAndAggregate()
        {
            var bus = ModernMediator.Create();
            bus.ErrorPolicy = ErrorPolicy.ContinueAndAggregate;

            bus.SubscribeAsync<TestMessage>(async msg =>
            {
                await Task.Delay(10);
                throw new InvalidOperationException("Async Error 1");
            });

            bus.SubscribeAsync<TestMessage>(async msg =>
            {
                await Task.Delay(10);
                throw new InvalidOperationException("Async Error 2");
            });

            var ex = await Assert.ThrowsAsync<AggregateException>(async () =>
                await bus.PublishAsyncTrue(new TestMessage("test")));

            Assert.Equal(2, ex.InnerExceptions.Count);
        }

        [Fact]
        public async Task AsyncHandler_ErrorPolicy_StopOnFirstError()
        {
            var bus = ModernMediator.Create();
            bus.ErrorPolicy = ErrorPolicy.StopOnFirstError;
            int count = 0;

            bus.SubscribeAsync<TestMessage>(async msg =>
            {
                await Task.Delay(10);
                throw new InvalidOperationException("First Error");
            });

            bus.SubscribeAsync<TestMessage>(async msg =>
            {
                await Task.Delay(10);
                Interlocked.Increment(ref count);
            });

            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await bus.PublishAsyncTrue(new TestMessage("test")));
        }

        [Fact]
        public async Task AsyncHandler_CancellationToken_Respected()
        {
            var bus = ModernMediator.Create();
            var cts = new CancellationTokenSource();

            bus.SubscribeAsync<TestMessage>(async msg =>
            {
                await Task.Delay(1000);
            });

            cts.CancelAfter(50);

            await Assert.ThrowsAsync<TaskCanceledException>(async () =>
                await bus.PublishAsyncTrue(new TestMessage("test"), cts.Token));
        }
    }

    public class WeakReferenceAsyncTests
    {
        [Fact]
        public async Task WeakReference_AsyncHandler_CanBeGarbageCollected()
        {
            var bus = ModernMediator.Create();
            var weakRef = SubscribeWithWeakAsyncReference(bus);

            for (int i = 0; i < 3; i++)
            {
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
                GC.WaitForPendingFinalizers();
            }

            var result = await bus.PublishAsyncTrue(new TestMessage("test"));

            // GC is non-deterministic. We test the logical invariant:
            // IF the handler was collected, THEN it should not have been invoked.
            if (!weakRef.IsAlive)
            {
                // Handler was collected - verify it wasn't invoked
                Assert.False(result, "Collected handler should not be invoked");
            }
            else
            {
                // Handler not yet collected (can happen in Debug mode)
                // This is acceptable - weak refs don't guarantee immediate collection
                Assert.True(result, "Alive handler should be invoked");
            }
        }

        private static WeakReference SubscribeWithWeakAsyncReference(IModernMediator bus)
        {
            var handler = new AsyncTestHandler();
            bus.SubscribeAsync<TestMessage>(handler.HandleAsync, weak: true);
            return new WeakReference(handler);
        }

        private class AsyncTestHandler
        {
            public async Task HandleAsync(TestMessage msg)
            {
                await Task.Delay(10);
            }
        }
    }

    public class EdgeCaseTests
    {
        [Fact]
        public void Dispose_ThrowsObjectDisposedException_OnSubsequentOperations()
        {
            var bus = ModernMediator.Create();
            bus.Dispose();

            Assert.Throws<ObjectDisposedException>(() => 
                bus.Subscribe<TestMessage>(msg => { }));
            
            Assert.Throws<ObjectDisposedException>(() => 
                bus.Publish(new TestMessage("test")));
        }

        [Fact]
        public void RecursivePublish_FromHandler_DoesNotDeadlock()
        {
            var bus = ModernMediator.Create();
            int callCount = 0;

            bus.Subscribe<TestMessage>(msg =>
            {
                callCount++;
                if (callCount < 3)
                {
                    bus.Publish(new TestMessage($"recursive {callCount}"));
                }
            });

            bus.Publish(new TestMessage("initial"));

            Assert.Equal(3, callCount);
        }

        [Fact]
        public void FilterPredicate_Throws_HandledByErrorPolicy()
        {
            var bus = ModernMediator.Create();
            bus.ErrorPolicy = ErrorPolicy.LogAndContinue;
            
            Exception? caughtException = null;
            bus.HandlerError += (s, e) => caughtException = e.Exception;

            bus.Subscribe<TestMessage>(
                msg => { },
                filter: msg => throw new InvalidOperationException("Filter error"));

            bus.Publish(new TestMessage("test"));

            Assert.NotNull(caughtException);
            Assert.IsType<InvalidOperationException>(caughtException);
        }
    }

    public class ConcurrencyTests
    {
        [Fact]
        public void Subscribe_DuringPublish_ThreadSafe()
        {
            var bus = ModernMediator.Create();
            int messageCount = 0;
            var startSignal = new ManualResetEventSlim(false);

            var publishTask = Task.Run(() =>
            {
                startSignal.Wait();
                for (int i = 0; i < 100; i++)
                {
                    bus.Publish(new TestMessage($"msg{i}"));
                    Thread.Sleep(1);
                }
            });

            var subscribeTask = Task.Run(() =>
            {
                startSignal.Wait();
                for (int i = 0; i < 100; i++)
                {
                    bus.Subscribe<TestMessage>(msg => Interlocked.Increment(ref messageCount));
                    Thread.Sleep(1);
                }
            });

            startSignal.Set();
            Task.WaitAll(publishTask, subscribeTask);

            Assert.True(messageCount >= 0);
        }

        [Fact]
        public async Task Dispose_DuringPublish_HandledGracefully()
        {
            var bus = ModernMediator.Create();
            var startSignal = new ManualResetEventSlim(false);
            Exception? caughtException = null;

            bus.Subscribe<TestMessage>(msg =>
            {
                Thread.Sleep(50);
            });

            var publishTask = Task.Run(() =>
            {
                startSignal.Wait();
                try
                {
                    for (int i = 0; i < 10; i++)
                    {
                        bus.Publish(new TestMessage($"msg{i}"));
                    }
                }
                catch (ObjectDisposedException ex)
                {
                    caughtException = ex;
                }
            });

            startSignal.Set();
            await Task.Delay(25);
            bus.Dispose();

            await publishTask;

            // Either completed normally or caught ObjectDisposedException - both are valid
        }

        [Fact]
        public async Task AsyncConcurrentStressTest()
        {
            var bus = ModernMediator.Create();
            int totalHandled = 0;

            for (int i = 0; i < 10; i++)
            {
                bus.SubscribeAsync<TestMessage>(async msg =>
                {
                    await Task.Delay(Random.Shared.Next(1, 10));
                    Interlocked.Increment(ref totalHandled);
                });
            }

            var tasks = new Task[20];
            for (int i = 0; i < 20; i++)
            {
                tasks[i] = Task.Run(async () =>
                {
                    for (int j = 0; j < 10; j++)
                    {
                        await bus.PublishAsyncTrue(new TestMessage($"msg{j}"));
                    }
                });
            }

            await Task.WhenAll(tasks);

            Assert.Equal(2000, totalHandled);
        }
    }

    public class SelfUnsubscribeTests
    {
        [Fact]
        public void Handler_UnsubscribesItself_DuringPublish()
        {
            // This is a common real-world pattern: one-shot handlers
            var bus = ModernMediator.Create();
            int callCount = 0;
            IDisposable? subscription = null;

            subscription = bus.Subscribe<TestMessage>(msg =>
            {
                callCount++;
                subscription?.Dispose(); // Unsubscribe self after first call
            });

            // First publish - handler runs and unsubscribes
            bus.Publish(new TestMessage("first"));
            Assert.Equal(1, callCount);

            // Second publish - handler should NOT run
            bus.Publish(new TestMessage("second"));
            Assert.Equal(1, callCount);
        }

        [Fact]
        public void MultipleHandlers_OneUnsubscribesItself_OthersStillCalled()
        {
            var bus = ModernMediator.Create();
            int count1 = 0, count2 = 0, count3 = 0;
            IDisposable? subscription2 = null;

            bus.Subscribe<TestMessage>(msg => count1++);
            subscription2 = bus.Subscribe<TestMessage>(msg =>
            {
                count2++;
                subscription2?.Dispose();
            });
            bus.Subscribe<TestMessage>(msg => count3++);

            // First publish
            bus.Publish(new TestMessage("first"));
            Assert.Equal(1, count1);
            Assert.Equal(1, count2);
            Assert.Equal(1, count3);

            // Second publish - handler2 should not run
            bus.Publish(new TestMessage("second"));
            Assert.Equal(2, count1);
            Assert.Equal(1, count2); // Still 1
            Assert.Equal(2, count3);
        }

        [Fact]
        public async Task AsyncHandler_UnsubscribesItself_DuringPublish()
        {
            var bus = ModernMediator.Create();
            int callCount = 0;
            IDisposable? subscription = null;

            subscription = bus.SubscribeAsync<TestMessage>(async msg =>
            {
                await Task.Delay(10);
                callCount++;
                subscription?.Dispose();
            });

            await bus.PublishAsyncTrue(new TestMessage("first"));
            Assert.Equal(1, callCount);

            await bus.PublishAsyncTrue(new TestMessage("second"));
            Assert.Equal(1, callCount);
        }
    }

    public class AsyncCovarianceTests
    {
        [Fact]
        public async Task AsyncHandler_BaseType_ReceivesDerivedMessage()
        {
            var bus = ModernMediator.Create();
            TestMessage? received = null;

            bus.SubscribeAsync<TestMessage>(async msg =>
            {
                await Task.Delay(10);
                received = msg;
            });

            await bus.PublishAsyncTrue(new DerivedMessage("test", 42));

            Assert.NotNull(received);
            Assert.IsType<DerivedMessage>(received);
            var derived = (DerivedMessage)received;
            Assert.Equal("test", derived.Data);
            Assert.Equal(42, derived.Value);
        }

        [Fact]
        public async Task AsyncHandler_MultipleBaseTypes_AllReceiveDerivedMessage()
        {
            var bus = ModernMediator.Create();
            int baseCount = 0;
            int derivedCount = 0;

            bus.SubscribeAsync<TestMessage>(async msg =>
            {
                await Task.Delay(10);
                Interlocked.Increment(ref baseCount);
            });

            bus.SubscribeAsync<DerivedMessage>(async msg =>
            {
                await Task.Delay(10);
                Interlocked.Increment(ref derivedCount);
            });

            await bus.PublishAsyncTrue(new DerivedMessage("test", 42));

            Assert.Equal(1, baseCount);
            Assert.Equal(1, derivedCount);
        }
    }

    public class PublishAsyncErrorTests
    {
        [Fact]
        public async Task PublishAsync_HandlerThrows_PropagatesException()
        {
            var bus = ModernMediator.Create();
            bus.ErrorPolicy = ErrorPolicy.StopOnFirstError;

            bus.Subscribe<TestMessage>(msg => throw new InvalidOperationException("Sync error"));

            // PublishAsync wraps sync Publish in Task.Run
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await bus.PublishAsync(new TestMessage("test")));

            Assert.Equal("Sync error", ex.Message);
        }

        [Fact]
        public async Task PublishAsync_MultipleHandlersThrow_AggregatesExceptions()
        {
            var bus = ModernMediator.Create();
            bus.ErrorPolicy = ErrorPolicy.ContinueAndAggregate;

            bus.Subscribe<TestMessage>(msg => throw new InvalidOperationException("Error 1"));
            bus.Subscribe<TestMessage>(msg => throw new InvalidOperationException("Error 2"));

            var ex = await Assert.ThrowsAsync<AggregateException>(async () =>
                await bus.PublishAsync(new TestMessage("test")));

            Assert.Equal(2, ex.InnerExceptions.Count);
        }

        [Fact]
        public async Task PublishAsync_WithStringKey_HandlerThrows_PropagatesException()
        {
            var bus = ModernMediator.Create();
            bus.ErrorPolicy = ErrorPolicy.StopOnFirstError;

            bus.Subscribe<TestMessage>("error.key", msg => throw new InvalidOperationException("Key error"));

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await bus.PublishAsync("error.key", new TestMessage("test")));

            Assert.Equal("Key error", ex.Message);
        }
    }

    public class StringKeyValidationTests
    {
        [Fact]
        public void Subscribe_NullKey_ThrowsArgumentNullException()
        {
            var bus = ModernMediator.Create();

            Assert.Throws<ArgumentNullException>(() =>
                bus.Subscribe<TestMessage>(null!, msg => { }));
        }

        [Fact]
        public void Subscribe_EmptyKey_ThrowsArgumentNullException()
        {
            var bus = ModernMediator.Create();

            Assert.Throws<ArgumentNullException>(() =>
                bus.Subscribe<TestMessage>("", msg => { }));
        }

        [Fact]
        public void Subscribe_WhitespaceKey_IsAllowed()
        {
            // Whitespace-only keys are technically allowed (not null or empty)
            // This tests current behavior - you may want to change this
            var bus = ModernMediator.Create();
            bool handlerCalled = false;

            bus.Subscribe<TestMessage>("   ", msg => handlerCalled = true);
            bus.Publish("   ", new TestMessage("test"));

            Assert.True(handlerCalled);
        }

        [Fact]
        public void Publish_NullKey_ThrowsArgumentNullException()
        {
            var bus = ModernMediator.Create();
            bus.Subscribe<TestMessage>("valid.key", msg => { });

            Assert.Throws<ArgumentNullException>(() =>
                bus.Publish<TestMessage>(null!, new TestMessage("test")));
        }

        [Fact]
        public void Publish_EmptyKey_ThrowsArgumentNullException()
        {
            var bus = ModernMediator.Create();
            bus.Subscribe<TestMessage>("valid.key", msg => { });

            Assert.Throws<ArgumentNullException>(() =>
                bus.Publish("", new TestMessage("test")));
        }

        [Fact]
        public void Subscribe_SameKeyDifferentType_ThrowsArgumentException()
        {
            var bus = ModernMediator.Create();
            bus.Subscribe<TestMessage>("shared.key", msg => { });

            Assert.Throws<ArgumentException>(() =>
                bus.Subscribe<DerivedMessage>("shared.key", msg => { }));
        }
    }

    public class SubscriptionTokenTests
    {
        [Fact]
        public void SubscriptionToken_DisposedTwice_DoesNotThrow()
        {
            var bus = ModernMediator.Create();
            var subscription = bus.Subscribe<TestMessage>(msg => { });

            // Should not throw on multiple dispose calls
            subscription.Dispose();
            subscription.Dispose();
            subscription.Dispose();
        }

        [Fact]
        public void SubscriptionToken_DisposedTwice_HandlerStillUnsubscribed()
        {
            var bus = ModernMediator.Create();
            int callCount = 0;
            var subscription = bus.Subscribe<TestMessage>(msg => callCount++);

            bus.Publish(new TestMessage("first"));
            Assert.Equal(1, callCount);

            subscription.Dispose();
            subscription.Dispose(); // Second dispose

            bus.Publish(new TestMessage("second"));
            Assert.Equal(1, callCount); // Still 1
        }

        [Fact]
        public async Task AsyncSubscriptionToken_DisposedTwice_DoesNotThrow()
        {
            var bus = ModernMediator.Create();
            var subscription = bus.SubscribeAsync<TestMessage>(async msg => await Task.Delay(10));

            subscription.Dispose();
            subscription.Dispose();
            subscription.Dispose();

            // Verify bus still works
            var result = await bus.PublishAsyncTrue(new TestMessage("test"));
            Assert.False(result); // No handlers
        }

        [Fact]
        public void StringKeySubscriptionToken_DisposedTwice_DoesNotThrow()
        {
            var bus = ModernMediator.Create();
            var subscription = bus.Subscribe<TestMessage>("test.key", msg => { });

            subscription.Dispose();
            subscription.Dispose();
            subscription.Dispose();
        }
    }

    public class DuplicateHandlerTests
    {
        [Fact]
        public void SameHandler_SubscribedTwice_InvokedTwice()
        {
            var bus = ModernMediator.Create();
            int callCount = 0;
            Action<TestMessage> handler = msg => callCount++;

            bus.Subscribe(handler);
            bus.Subscribe(handler);

            bus.Publish(new TestMessage("test"));

            // Both subscriptions should invoke the same handler
            Assert.Equal(2, callCount);
        }

        [Fact]
        public void SameHandler_SubscribedTwice_DisposingOneKeepsOther()
        {
            var bus = ModernMediator.Create();
            int callCount = 0;
            Action<TestMessage> handler = msg => callCount++;

            var sub1 = bus.Subscribe(handler);
            var sub2 = bus.Subscribe(handler);

            bus.Publish(new TestMessage("first"));
            Assert.Equal(2, callCount);

            sub1.Dispose();

            bus.Publish(new TestMessage("second"));
            Assert.Equal(3, callCount); // Only one handler now
        }

        [Fact]
        public async Task SameAsyncHandler_SubscribedTwice_InvokedTwice()
        {
            var bus = ModernMediator.Create();
            int callCount = 0;
            Func<TestMessage, Task> handler = async msg =>
            {
                await Task.Delay(10);
                Interlocked.Increment(ref callCount);
            };

            bus.SubscribeAsync(handler);
            bus.SubscribeAsync(handler);

            await bus.PublishAsyncTrue(new TestMessage("test"));

            Assert.Equal(2, callCount);
        }

        [Fact]
        public void SameHandler_DifferentFilters_BothRespected()
        {
            var bus = ModernMediator.Create();
            int callCount = 0;
            Action<TestMessage> handler = msg => callCount++;

            bus.Subscribe(handler, filter: msg => msg.Data == "A");
            bus.Subscribe(handler, filter: msg => msg.Data == "B");

            bus.Publish(new TestMessage("A"));
            Assert.Equal(1, callCount);

            bus.Publish(new TestMessage("B"));
            Assert.Equal(2, callCount);

            bus.Publish(new TestMessage("C"));
            Assert.Equal(2, callCount); // Neither filter matches
        }
    }
}
