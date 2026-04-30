using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ModernMediator;
using Xunit;

namespace ModernMediator.Tests
{
    #region Test Types for Concurrency

    public record ConcurrentPingRequest(int Id) : IRequest<ConcurrentPingResponse>;
    public record ConcurrentPingResponse(int Id, string Message);

    public class ConcurrentPingHandler : IRequestHandler<ConcurrentPingRequest, ConcurrentPingResponse>
    {
        public Task<ConcurrentPingResponse> Handle(ConcurrentPingRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ConcurrentPingResponse(request.Id, $"Pong {request.Id}"));
        }
    }

    public record ConcurrentNotification(int Id) : INotification;

    public class ConcurrentNotificationHandler1 : INotificationHandler<ConcurrentNotification>
    {
        public static int InvocationCount;
        public Task Handle(ConcurrentNotification notification, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref InvocationCount);
            return Task.CompletedTask;
        }
    }

    public class ConcurrentNotificationHandler2 : INotificationHandler<ConcurrentNotification>
    {
        public static int InvocationCount;
        public Task Handle(ConcurrentNotification notification, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref InvocationCount);
            return Task.CompletedTask;
        }
    }

    // 20 different request types for wrapper cache stress test
    public record StressRequest1(int Id) : IRequest<int>;
    public record StressRequest2(int Id) : IRequest<int>;
    public record StressRequest3(int Id) : IRequest<int>;
    public record StressRequest4(int Id) : IRequest<int>;
    public record StressRequest5(int Id) : IRequest<int>;
    public record StressRequest6(int Id) : IRequest<int>;
    public record StressRequest7(int Id) : IRequest<int>;
    public record StressRequest8(int Id) : IRequest<int>;
    public record StressRequest9(int Id) : IRequest<int>;
    public record StressRequest10(int Id) : IRequest<int>;
    public record StressRequest11(int Id) : IRequest<int>;
    public record StressRequest12(int Id) : IRequest<int>;
    public record StressRequest13(int Id) : IRequest<int>;
    public record StressRequest14(int Id) : IRequest<int>;
    public record StressRequest15(int Id) : IRequest<int>;
    public record StressRequest16(int Id) : IRequest<int>;
    public record StressRequest17(int Id) : IRequest<int>;
    public record StressRequest18(int Id) : IRequest<int>;
    public record StressRequest19(int Id) : IRequest<int>;
    public record StressRequest20(int Id) : IRequest<int>;

    public class StressHandler1 : IRequestHandler<StressRequest1, int> { public Task<int> Handle(StressRequest1 r, CancellationToken ct = default) => Task.FromResult(r.Id); }
    public class StressHandler2 : IRequestHandler<StressRequest2, int> { public Task<int> Handle(StressRequest2 r, CancellationToken ct = default) => Task.FromResult(r.Id); }
    public class StressHandler3 : IRequestHandler<StressRequest3, int> { public Task<int> Handle(StressRequest3 r, CancellationToken ct = default) => Task.FromResult(r.Id); }
    public class StressHandler4 : IRequestHandler<StressRequest4, int> { public Task<int> Handle(StressRequest4 r, CancellationToken ct = default) => Task.FromResult(r.Id); }
    public class StressHandler5 : IRequestHandler<StressRequest5, int> { public Task<int> Handle(StressRequest5 r, CancellationToken ct = default) => Task.FromResult(r.Id); }
    public class StressHandler6 : IRequestHandler<StressRequest6, int> { public Task<int> Handle(StressRequest6 r, CancellationToken ct = default) => Task.FromResult(r.Id); }
    public class StressHandler7 : IRequestHandler<StressRequest7, int> { public Task<int> Handle(StressRequest7 r, CancellationToken ct = default) => Task.FromResult(r.Id); }
    public class StressHandler8 : IRequestHandler<StressRequest8, int> { public Task<int> Handle(StressRequest8 r, CancellationToken ct = default) => Task.FromResult(r.Id); }
    public class StressHandler9 : IRequestHandler<StressRequest9, int> { public Task<int> Handle(StressRequest9 r, CancellationToken ct = default) => Task.FromResult(r.Id); }
    public class StressHandler10 : IRequestHandler<StressRequest10, int> { public Task<int> Handle(StressRequest10 r, CancellationToken ct = default) => Task.FromResult(r.Id); }
    public class StressHandler11 : IRequestHandler<StressRequest11, int> { public Task<int> Handle(StressRequest11 r, CancellationToken ct = default) => Task.FromResult(r.Id); }
    public class StressHandler12 : IRequestHandler<StressRequest12, int> { public Task<int> Handle(StressRequest12 r, CancellationToken ct = default) => Task.FromResult(r.Id); }
    public class StressHandler13 : IRequestHandler<StressRequest13, int> { public Task<int> Handle(StressRequest13 r, CancellationToken ct = default) => Task.FromResult(r.Id); }
    public class StressHandler14 : IRequestHandler<StressRequest14, int> { public Task<int> Handle(StressRequest14 r, CancellationToken ct = default) => Task.FromResult(r.Id); }
    public class StressHandler15 : IRequestHandler<StressRequest15, int> { public Task<int> Handle(StressRequest15 r, CancellationToken ct = default) => Task.FromResult(r.Id); }
    public class StressHandler16 : IRequestHandler<StressRequest16, int> { public Task<int> Handle(StressRequest16 r, CancellationToken ct = default) => Task.FromResult(r.Id); }
    public class StressHandler17 : IRequestHandler<StressRequest17, int> { public Task<int> Handle(StressRequest17 r, CancellationToken ct = default) => Task.FromResult(r.Id); }
    public class StressHandler18 : IRequestHandler<StressRequest18, int> { public Task<int> Handle(StressRequest18 r, CancellationToken ct = default) => Task.FromResult(r.Id); }
    public class StressHandler19 : IRequestHandler<StressRequest19, int> { public Task<int> Handle(StressRequest19 r, CancellationToken ct = default) => Task.FromResult(r.Id); }
    public class StressHandler20 : IRequestHandler<StressRequest20, int> { public Task<int> Handle(StressRequest20 r, CancellationToken ct = default) => Task.FromResult(r.Id); }

    #endregion

    public class ThreadSafetyConcurrencyTests
    {
        [Fact]
        public async Task ConcurrentSend_100Calls_AllCompleteCorrectly()
        {
            var services = new ServiceCollection();
            services.AddModernMediator(config =>
            {
                config.RegisterHandler<ConcurrentPingHandler>();
            });
            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            var tasks = Enumerable.Range(1, 100)
                .Select(i => mediator.Send(new ConcurrentPingRequest(i)))
                .ToArray();

            var results = await Task.WhenAll(tasks);

            Assert.Equal(100, results.Length);
            for (int i = 0; i < 100; i++)
            {
                Assert.Equal(i + 1, results[i].Id);
                Assert.Equal($"Pong {i + 1}", results[i].Message);
            }
        }

        [Fact]
        public async Task ConcurrentPublish_50Calls_AllHandlersFire()
        {
            ConcurrentNotificationHandler1.InvocationCount = 0;
            ConcurrentNotificationHandler2.InvocationCount = 0;

            var services = new ServiceCollection();
            services.AddModernMediator(config =>
            {
                config.RegisterServicesFromAssemblyContaining<ConcurrentNotificationHandler1>();
            });
            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            var tasks = Enumerable.Range(1, 50)
                .Select(i => mediator.Publish(new ConcurrentNotification(i), CancellationToken.None))
                .ToArray();

            await Task.WhenAll(tasks);

            Assert.Equal(50, ConcurrentNotificationHandler1.InvocationCount);
            Assert.Equal(50, ConcurrentNotificationHandler2.InvocationCount);
        }

        [Fact]
        public async Task ConcurrentSendAndPublish_NoContamination()
        {
            ConcurrentNotificationHandler1.InvocationCount = 0;
            ConcurrentNotificationHandler2.InvocationCount = 0;

            var services = new ServiceCollection();
            services.AddModernMediator(config =>
            {
                config.RegisterHandler<ConcurrentPingHandler>();
                config.RegisterServicesFromAssemblyContaining<ConcurrentNotificationHandler1>();
            });
            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            var sendTasks = Enumerable.Range(1, 50)
                .Select(i => mediator.Send(new ConcurrentPingRequest(i)));
            var publishTasks = Enumerable.Range(1, 50)
                .Select(i => mediator.Publish(new ConcurrentNotification(i), CancellationToken.None));

            var allTasks = sendTasks.Cast<Task>().Concat(publishTasks).ToArray();
            await Task.WhenAll(allTasks);

            Assert.Equal(50, ConcurrentNotificationHandler1.InvocationCount);
            Assert.Equal(50, ConcurrentNotificationHandler2.InvocationCount);
        }

        [Fact]
        public async Task WrapperCacheStress_20RequestTypes_ConcurrentFirstCall()
        {
            var services = new ServiceCollection();
            services.AddModernMediator(config =>
            {
                config.RegisterServicesFromAssemblyContaining<StressHandler1>();
            });
            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            // All 20 request types dispatched concurrently; first call for each
            var tasks = new Task<int>[]
            {
                mediator.Send(new StressRequest1(1)),
                mediator.Send(new StressRequest2(2)),
                mediator.Send(new StressRequest3(3)),
                mediator.Send(new StressRequest4(4)),
                mediator.Send(new StressRequest5(5)),
                mediator.Send(new StressRequest6(6)),
                mediator.Send(new StressRequest7(7)),
                mediator.Send(new StressRequest8(8)),
                mediator.Send(new StressRequest9(9)),
                mediator.Send(new StressRequest10(10)),
                mediator.Send(new StressRequest11(11)),
                mediator.Send(new StressRequest12(12)),
                mediator.Send(new StressRequest13(13)),
                mediator.Send(new StressRequest14(14)),
                mediator.Send(new StressRequest15(15)),
                mediator.Send(new StressRequest16(16)),
                mediator.Send(new StressRequest17(17)),
                mediator.Send(new StressRequest18(18)),
                mediator.Send(new StressRequest19(19)),
                mediator.Send(new StressRequest20(20)),
            };

            var results = await Task.WhenAll(tasks);

            for (int i = 0; i < 20; i++)
            {
                Assert.Equal(i + 1, results[i]);
            }
        }

        [Fact]
        public async Task TimeoutBehavior_SimultaneousCancellation_NoDeadlock()
        {
            var services = new ServiceCollection();
            services.AddModernMediator(config =>
            {
                config.RegisterHandler<ConcurrentPingHandler>();
                config.AddTimeout();
            });
            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            // Fire multiple cancellations simultaneously
            var tasks = Enumerable.Range(1, 10).Select(async i =>
            {
                var cts = new CancellationTokenSource();
                cts.CancelAfter(TimeSpan.FromMilliseconds(1));

                try
                {
                    // Small delay to let cancellation fire
                    await Task.Delay(5);
                    var result = await mediator.Send(new ConcurrentPingRequest(i), cts.Token);
                    // Either succeeds (if not yet cancelled) or throws
                    return true;
                }
                catch (OperationCanceledException)
                {
                    return true; // Expected
                }
            }).ToArray();

            // Must complete without deadlock within reasonable time
            var completedInTime = Task.WaitAll(tasks, TimeSpan.FromSeconds(10));
            Assert.True(completedInTime, "Concurrent cancellation caused deadlock");

            foreach (var task in tasks)
            {
                Assert.True(await task);
            }
        }
    }
}
