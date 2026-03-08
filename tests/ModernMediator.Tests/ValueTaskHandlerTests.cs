using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ModernMediator.Tests;

#region Test Fixtures

public record VtPingRequest(string Message) : IRequest<VtPongResponse>;

public record VtPongResponse(string Message);

public record VtShortCircuitRequest(string Message) : IRequest<VtPongResponse>;

public class VtPingHandler : IValueTaskRequestHandler<VtPingRequest, VtPongResponse>
{
    public ValueTask<VtPongResponse> Handle(VtPingRequest request, CancellationToken cancellationToken = default)
        => new(new VtPongResponse($"Pong: {request.Message}"));
}

public class VtShortCircuitRequestHandler : IValueTaskRequestHandler<VtShortCircuitRequest, VtPongResponse>
{
    public ValueTask<VtPongResponse> Handle(VtShortCircuitRequest request, CancellationToken cancellationToken = default)
        => new(new VtPongResponse($"Pong: {request.Message}"));
}

public class VtLoggingBehavior<TRequest, TResponse> : IValueTaskPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public static int ExecutionCount;

    public async ValueTask<TResponse> Handle(TRequest request, ValueTaskRequestHandlerDelegate<TRequest, TResponse> next, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref ExecutionCount);
        return await next(request, cancellationToken);
    }
}

public class VtShortCircuitBehavior : IValueTaskPipelineBehavior<VtShortCircuitRequest, VtPongResponse>
{
    public ValueTask<VtPongResponse> Handle(VtShortCircuitRequest request, ValueTaskRequestHandlerDelegate<VtShortCircuitRequest, VtPongResponse> next, CancellationToken cancellationToken)
        => new(new VtPongResponse("Short-circuited"));
}

#endregion

public class ValueTaskHandlerTests
{
    [Fact]
    public async Task SendAsync_CallsValueTaskHandler()
    {
        var services = new ServiceCollection();
        services.AddModernMediator(cfg =>
            cfg.RegisterServicesFromAssemblyContaining<ValueTaskHandlerTests>());

        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var result = await sender.SendAsync(new VtPingRequest("hello"));

        Assert.Equal("Pong: hello", result.Message);
    }

    [Fact]
    public async Task SendAsync_WithBehavior_ExecutesBehavior()
    {
        var services = new ServiceCollection();
        services.AddModernMediator(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<ValueTaskHandlerTests>();
            cfg.AddOpenValueTaskBehavior(typeof(VtLoggingBehavior<,>));
        });

        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        VtLoggingBehavior<VtPingRequest, VtPongResponse>.ExecutionCount = 0;

        var result = await sender.SendAsync(new VtPingRequest("hello"));

        Assert.Equal("Pong: hello", result.Message);
        Assert.Equal(1, VtLoggingBehavior<VtPingRequest, VtPongResponse>.ExecutionCount);
    }

    [Fact]
    public async Task SendAsync_BehaviorCanShortCircuit()
    {
        var services = new ServiceCollection();
        services.AddModernMediator(cfg =>
            cfg.RegisterServicesFromAssemblyContaining<ValueTaskHandlerTests>());

        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        // VtShortCircuitBehavior is auto-registered for VtShortCircuitRequest via assembly scanning
        var result = await sender.SendAsync(new VtShortCircuitRequest("hello"));

        Assert.Equal("Short-circuited", result.Message);
    }

    [Fact]
    public async Task SendAsync_NoBehaviors_DirectHandlerCall()
    {
        var services = new ServiceCollection();
        services.AddModernMediator(cfg =>
            cfg.RegisterServicesFromAssemblyContaining<ValueTaskHandlerTests>());

        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var result = await sender.SendAsync(new VtPingRequest("direct"));

        Assert.Equal("Pong: direct", result.Message);
    }

    [Fact]
    public async Task SendAsync_NoHandler_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        services.AddModernMediator(); // no handlers registered

        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await sender.SendAsync(new VtPingRequest("fail")));
    }

    [Fact]
    public async Task SendAsync_CancellationRequested_ThrowsBeforeHandlerCall()
    {
        var services = new ServiceCollection();
        services.AddModernMediator(cfg =>
            cfg.RegisterServicesFromAssemblyContaining<ValueTaskHandlerTests>());

        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await sender.SendAsync(new VtPingRequest("hello"), cts.Token));
    }

    [Fact]
    public async Task SendAsync_ConcurrentCalls_AreThreadSafe()
    {
        var services = new ServiceCollection();
        services.AddModernMediator(cfg =>
            cfg.RegisterServicesFromAssemblyContaining<ValueTaskHandlerTests>());

        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var tasks = Enumerable.Range(0, 100)
            .Select(async i =>
            {
                var result = await sender.SendAsync(new VtPingRequest($"msg-{i}"));
                Assert.Equal($"Pong: msg-{i}", result.Message);
            });

        await Task.WhenAll(tasks);
    }

    [Fact]
    public async Task SendAsync_NullRequest_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();
        services.AddModernMediator();

        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await sender.SendAsync<VtPongResponse>(null!));
    }

    [Fact]
    public async Task SendAsync_ViaIMediator_AlsoWorks()
    {
        var services = new ServiceCollection();
        services.AddModernMediator(cfg =>
            cfg.RegisterServicesFromAssemblyContaining<ValueTaskHandlerTests>());

        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var result = await mediator.SendAsync(new VtPingRequest("via-mediator"));

        Assert.Equal("Pong: via-mediator", result.Message);
    }
}
