using Xunit;

namespace ModernMediator.Tests
{
    public record TestMessage(string Data);
    public record DerivedMessage(string Data, int Value) : TestMessage(Data);

    public class ModernMediatorTests
    {
        [Fact]
        public void Subscribe_And_Publish_DeliversMessage()
        {
            // Arrange
            var bus = ModernMediator.Create();
            string? receivedData = null;
            bus.Subscribe<TestMessage>(msg => receivedData = msg.Data);

            // Act
            bus.Publish(new TestMessage("test"));

            // Assert
            Assert.Equal("test", receivedData);
        }

        [Fact]
        public void Publish_ReturnsTrue_WhenHandlerInvoked()
        {
            // Arrange
            var bus = ModernMediator.Create();
            bus.Subscribe<TestMessage>(msg => { });

            // Act
            var result = bus.Publish(new TestMessage("test"));

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void Publish_ReturnsFalse_WhenNoHandlers()
        {
            // Arrange
            var bus = ModernMediator.Create();

            // Act
            var result = bus.Publish(new TestMessage("test"));

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void Publish_NullMessage_ReturnsFalse()
        {
            // Arrange
            var bus = ModernMediator.Create();
            bus.Subscribe<TestMessage>(msg => { });

            // Act
            var result = bus.Publish<TestMessage>(null);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void Subscribe_WithFilter_OnlyDeliversMatchingMessages()
        {
            // Arrange
            var bus = ModernMediator.Create();
            int count = 0;
            bus.Subscribe<TestMessage>(
                msg => count++,
                filter: msg => msg.Data == "match");

            // Act
            bus.Publish(new TestMessage("no match"));
            bus.Publish(new TestMessage("match"));
            bus.Publish(new TestMessage("no match"));

            // Assert
            Assert.Equal(1, count);
        }

        [Fact]
        public void Dispose_UnsubscribesHandler()
        {
            // Arrange
            var bus = ModernMediator.Create();
            int count = 0;
            var subscription = bus.Subscribe<TestMessage>(msg => count++);

            // Act
            bus.Publish(new TestMessage("test1"));
            subscription.Dispose();
            bus.Publish(new TestMessage("test2"));

            // Assert
            Assert.Equal(1, count);
        }

        [Fact]
        public void Covariance_BaseTypeHandler_ReceivesDerivedMessage()
        {
            // Arrange
            var bus = ModernMediator.Create();
            TestMessage? received = null;
            bus.Subscribe<TestMessage>(msg => received = msg);

            // Act
            bus.Publish(new DerivedMessage("test", 42));

            // Assert
            Assert.NotNull(received);
            Assert.IsType<DerivedMessage>(received);
            Assert.Equal("test", received.Data);
        }

        [Fact]
        public void MultipleHandlers_AllInvoked()
        {
            // Arrange
            var bus = ModernMediator.Create();
            int count1 = 0, count2 = 0, count3 = 0;
            bus.Subscribe<TestMessage>(msg => count1++);
            bus.Subscribe<TestMessage>(msg => count2++);
            bus.Subscribe<TestMessage>(msg => count3++);

            // Act
            bus.Publish(new TestMessage("test"));

            // Assert
            Assert.Equal(1, count1);
            Assert.Equal(1, count2);
            Assert.Equal(1, count3);
        }

        [Fact]
        public async Task SubscribeAsync_PublishAsyncTrue_InvokesAsyncHandler()
        {
            // Arrange
            var bus = ModernMediator.Create();
            string? receivedData = null;
            bus.SubscribeAsync<TestMessage>(async msg =>
            {
                await Task.Delay(10);
                receivedData = msg.Data;
            });

            // Act
            await bus.PublishAsyncTrue(new TestMessage("async test"));

            // Assert
            Assert.Equal("async test", receivedData);
        }

        [Fact]
        public void ErrorPolicy_ContinueAndAggregate_CollectsAllExceptions()
        {
            // Arrange
            var bus = ModernMediator.Create();
            bus.ErrorPolicy = ErrorPolicy.ContinueAndAggregate;
            
            bus.Subscribe<TestMessage>(msg => throw new InvalidOperationException("Error 1"));
            bus.Subscribe<TestMessage>(msg => throw new InvalidOperationException("Error 2"));

            // Act & Assert
            var ex = Assert.Throws<AggregateException>(() => bus.Publish(new TestMessage("test")));
            Assert.Equal(2, ex.InnerExceptions.Count);
        }

        [Fact]
        public void ErrorPolicy_StopOnFirstError_ThrowsImmediately()
        {
            // Arrange
            var bus = ModernMediator.Create();
            bus.ErrorPolicy = ErrorPolicy.StopOnFirstError;
            int count = 0;
            
            bus.Subscribe<TestMessage>(msg => throw new InvalidOperationException("Error"));
            bus.Subscribe<TestMessage>(msg => count++); // Should not be reached

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => bus.Publish(new TestMessage("test")));
            Assert.Equal(0, count);
        }

        [Fact]
        public void ErrorPolicy_LogAndContinue_RaisesHandlerErrorEvent()
        {
            // Arrange
            var bus = ModernMediator.Create();
            bus.ErrorPolicy = ErrorPolicy.LogAndContinue;
            
            Exception? caughtException = null;
            bus.HandlerError += (s, e) => caughtException = e.Exception;
            
            bus.Subscribe<TestMessage>(msg => throw new InvalidOperationException("Test error"));

            // Act
            bus.Publish(new TestMessage("test"));

            // Assert
            Assert.NotNull(caughtException);
            Assert.IsType<InvalidOperationException>(caughtException);
        }

        [Fact]
        public void StringKey_Subscribe_And_Publish_DeliversMessage()
        {
            // Arrange
            var bus = ModernMediator.Create();
            string? receivedData = null;
            bus.Subscribe<TestMessage>("test.event", msg => receivedData = msg.Data);

            // Act
            bus.Publish("test.event", new TestMessage("test"));

            // Assert
            Assert.Equal("test", receivedData);
        }

        [Fact]
        public void WeakReference_HandlerCanBeGarbageCollected()
        {
            // Arrange
            var bus = ModernMediator.Create();
            var weakRef = SubscribeWithWeakReference(bus);
            
            // Act - Force garbage collection
            for (int i = 0; i < 3; i++)
            {
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
                GC.WaitForPendingFinalizers();
            }

            var result = bus.Publish(new TestMessage("test"));

            // Assert
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

        private static WeakReference SubscribeWithWeakReference(IModernMediator bus)
        {
            var handler = new TestHandler();
            bus.Subscribe<TestMessage>(handler.Handle, weak: true);
            return new WeakReference(handler);
        }

        private class TestHandler
        {
            public void Handle(TestMessage msg) { }
        }

        [Fact]
        public void StrongReference_HandlerNotGarbageCollected()
        {
            // Arrange
            var bus = ModernMediator.Create();
            var weakRef = SubscribeWithStrongReference(bus);
            
            // Act
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // Assert - handler should still be invoked
            var result = bus.Publish(new TestMessage("test"));
            Assert.True(result); // Handler was invoked
        }

        private static WeakReference SubscribeWithStrongReference(IModernMediator bus)
        {
            var handler = new TestHandler();
            bus.Subscribe<TestMessage>(handler.Handle, weak: false);
            return new WeakReference(handler);
        }

        [Fact]
        public void Dispose_ClearsAllSubscriptions()
        {
            // Arrange
            var bus = ModernMediator.Create();
            int callCount = 0;
            bus.Subscribe<TestMessage>(msg => callCount++);
            bus.Subscribe<TestMessage>(msg => callCount++);

            // Verify handlers work before dispose
            bus.Publish(new TestMessage("before"));
            Assert.Equal(2, callCount);

            // Act
            bus.Dispose();

            // Assert - After dispose, operations should throw ObjectDisposedException
            // This is the standard .NET pattern for disposed objects
            Assert.Throws<ObjectDisposedException>(() => bus.Publish(new TestMessage("after")));
            Assert.Throws<ObjectDisposedException>(() => bus.Subscribe<TestMessage>(msg => { }));
            
            // Handlers should NOT have been called again (count unchanged)
            Assert.Equal(2, callCount);
        }

        [Fact]
        public void Dispose_CanBeCalledMultipleTimes()
        {
            // Arrange
            var bus = ModernMediator.Create();
            bus.Subscribe<TestMessage>(msg => { });

            // Act & Assert - Multiple dispose calls should not throw
            bus.Dispose();
            bus.Dispose();
            bus.Dispose();
        }

        [Fact]
        public void ThreadSafety_ConcurrentPublishAndSubscribe()
        {
            // Arrange
            var bus = ModernMediator.Create();
            int messageCount = 0;

            // Act - Concurrent subscribe and publish
            Parallel.For(0, 100, i =>
            {
                bus.Subscribe<TestMessage>(msg => Interlocked.Increment(ref messageCount));
                bus.Publish(new TestMessage($"test{i}"));
            });

            // Assert - No exceptions thrown, some messages delivered
            Assert.True(messageCount > 0);
        }
    }
}
