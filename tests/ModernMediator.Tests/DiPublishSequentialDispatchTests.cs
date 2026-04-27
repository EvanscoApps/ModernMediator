using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ModernMediator.Tests
{
    public class DiPublishSequentialDispatchTests
    {
        [Fact]
        public async Task Handlers_DispatchedInRegistrationOrder_NotConcurrently()
        {
            RecordingNotificationHandler<TestNotification>.ResetGlobalSequence();

            var handler1 = new RecordingNotificationHandler<TestNotification> { Delay = TimeSpan.FromMilliseconds(50) };
            var handler2 = new RecordingNotificationHandler<TestNotification> { Delay = TimeSpan.FromMilliseconds(50) };
            var handler3 = new RecordingNotificationHandler<TestNotification> { Delay = TimeSpan.FromMilliseconds(50) };

            var mediator = new MediatorTestHarness()
                .WithErrorPolicy(ErrorPolicy.ContinueAndAggregate)
                .WithHandler(handler1)
                .WithHandler(handler2)
                .WithHandler(handler3)
                .Build();

            await mediator.Publish(new TestNotification("p"), CancellationToken.None);

            Assert.Single(handler1.InvocationOrder);
            Assert.Single(handler2.InvocationOrder);
            Assert.Single(handler3.InvocationOrder);

            Assert.True(handler1.InvocationOrder[0] < handler2.InvocationOrder[0],
                $"handler1 sequence ({handler1.InvocationOrder[0]}) should precede handler2 ({handler2.InvocationOrder[0]})");
            Assert.True(handler2.InvocationOrder[0] < handler3.InvocationOrder[0],
                $"handler2 sequence ({handler2.InvocationOrder[0]}) should precede handler3 ({handler3.InvocationOrder[0]})");
        }

        [Fact]
        public async Task SecondHandlerCompletesBeforeThirdInvoked()
        {
            var sharedLog = new List<string>();
            var sharedLock = new object();

            var services = new ServiceCollection();
            services.AddSingleton<INotificationHandler<TestNotification>>(
                new SideEffectHandler("h1-end", sharedLog, sharedLock, requirePresent: Array.Empty<string>()));
            services.AddSingleton<INotificationHandler<TestNotification>>(
                new SideEffectHandler("h2-end", sharedLog, sharedLock, requirePresent: new[] { "h1-end" }));
            services.AddSingleton<INotificationHandler<TestNotification>>(
                new SideEffectHandler("h3-end", sharedLog, sharedLock, requirePresent: new[] { "h1-end", "h2-end" }));

            services.AddModernMediator(config => { config.ErrorPolicy = ErrorPolicy.ContinueAndAggregate; });

            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            await mediator.Publish(new TestNotification("p"), CancellationToken.None);

            Assert.Equal(new[] { "h1-end", "h2-end", "h3-end" }, sharedLog);
        }

        private sealed class SideEffectHandler : INotificationHandler<TestNotification>
        {
            private readonly string _marker;
            private readonly List<string> _log;
            private readonly object _lock;
            private readonly string[] _requirePresent;

            public SideEffectHandler(string marker, List<string> log, object lockObj, string[] requirePresent)
            {
                _marker = marker;
                _log = log;
                _lock = lockObj;
                _requirePresent = requirePresent;
            }

            public Task Handle(TestNotification notification, CancellationToken cancellationToken)
            {
                lock (_lock)
                {
                    foreach (var required in _requirePresent)
                    {
                        if (!_log.Contains(required))
                        {
                            throw new InvalidOperationException(
                                $"Handler {_marker} expected prior marker '{required}' but log was [{string.Join(",", _log)}]");
                        }
                    }
                    _log.Add(_marker);
                }
                return Task.CompletedTask;
            }
        }
    }
}
