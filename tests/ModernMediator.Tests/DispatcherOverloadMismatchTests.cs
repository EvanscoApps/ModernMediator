using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ModernMediator.Tests;

public class DispatcherOverloadMismatchTests
{
    public record OmRequest(string Message) : IRequest<OmResponse>;

    public record OmResponse(string Message);

    private sealed class OmTaskHandler : IRequestHandler<OmRequest, OmResponse>
    {
        public Task<OmResponse> Handle(OmRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new OmResponse($"task: {request.Message}"));
    }

    private sealed class OmValueTaskHandler : IValueTaskRequestHandler<OmRequest, OmResponse>
    {
        public ValueTask<OmResponse> Handle(OmRequest request, CancellationToken cancellationToken = default)
            => new(new OmResponse($"valuetask: {request.Message}"));
    }

    [Fact]
    public async Task Send_WithIRequestHandlerRegistered_Succeeds()
    {
        var services = new ServiceCollection();
        services.AddModernMediator();
        services.AddTransient<IRequestHandler<OmRequest, OmResponse>, OmTaskHandler>();

        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var result = await mediator.Send(new OmRequest("hi"));

        Assert.Equal("task: hi", result.Message);
    }

    [Fact]
    public async Task Send_WithOnlyIValueTaskRequestHandlerRegistered_ThrowsHelpfulException()
    {
        var services = new ServiceCollection();
        services.AddModernMediator();
        services.AddTransient<IValueTaskRequestHandler<OmRequest, OmResponse>, OmValueTaskHandler>();

        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await mediator.Send(new OmRequest("hi")));

        Assert.Contains("MM201", ex.Message);
        Assert.Contains("Did you mean to call SendAsync", ex.Message);
    }

    [Fact]
    public async Task SendAsync_WithIValueTaskRequestHandlerRegistered_Succeeds()
    {
        var services = new ServiceCollection();
        services.AddModernMediator();
        services.AddTransient<IValueTaskRequestHandler<OmRequest, OmResponse>, OmValueTaskHandler>();

        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var result = await sender.SendAsync(new OmRequest("hi"));

        Assert.Equal("valuetask: hi", result.Message);
    }

    [Fact]
    public async Task SendAsync_WithOnlyIRequestHandlerRegistered_ThrowsHelpfulException()
    {
        var services = new ServiceCollection();
        services.AddModernMediator();
        services.AddTransient<IRequestHandler<OmRequest, OmResponse>, OmTaskHandler>();

        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await sender.SendAsync(new OmRequest("hi")));

        Assert.Contains("MM201", ex.Message);
        Assert.Contains("Did you mean to call Send", ex.Message);
    }

    [Fact]
    public async Task Send_WithNoHandlerRegistered_ThrowsOriginalMessage()
    {
        var services = new ServiceCollection();
        services.AddModernMediator();

        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await mediator.Send(new OmRequest("hi")));

        Assert.DoesNotContain("MM201", ex.Message);
        Assert.Contains("No handler registered for request type", ex.Message);
    }
}
