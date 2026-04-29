using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;
using Xunit;

namespace ModernMediator.Tests;

public sealed class AuditRegistrationTests
{
    // --- Test doubles ---

    private sealed class TestCommand : IRequest<Unit> { }

    [NoAudit]
    private sealed class TestQuery : IRequest<string> { }

    private sealed class TestCommandHandler : IRequestHandler<TestCommand, Unit>
    {
        public Task<Unit> Handle(TestCommand request, CancellationToken cancellationToken)
            => Task.FromResult(Unit.Value);
    }

    private sealed class TestQueryHandler : IRequestHandler<TestQuery, string>
    {
        public Task<string> Handle(TestQuery request, CancellationToken cancellationToken)
            => Task.FromResult("result");
    }

    // --- Helpers ---

    private static (IServiceProvider Provider, CapturingAuditWriter Writer) BuildProvider()
    {
        var writer = new CapturingAuditWriter();
        var services = new ServiceCollection();

        services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));
        services.AddSingleton<IAuditWriter>(writer);
        services.AddModernMediator(cfg => cfg
            .RegisterServicesFromAssemblyContaining<TestCommandHandler>()
            .AddAudit<CapturingAuditWriter>());

        // Replace the registered writer with our capturing instance
        // so we can inspect records written through the channel
        var provider = services.BuildServiceProvider();
        return (provider, writer);
    }

    // --- Tests ---

    [Fact]
    public async Task AddAudit_ChannelMode_AuditBehaviorResolvesWithoutError()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));
        services.AddModernMediator(cfg => cfg
            .RegisterServicesFromAssemblyContaining<TestCommandHandler>()
            .AddAudit<CapturingAuditWriter>());

        var provider = services.BuildServiceProvider();

        // Resolving the mediator should not throw
        var mediator = provider.GetRequiredService<IMediator>();
        Assert.NotNull(mediator);
    }

    [Fact]
    public async Task AddAudit_ChannelMode_ChannelSingletonIsRegistered()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));
        services.AddModernMediator(cfg => cfg
            .RegisterServicesFromAssemblyContaining<TestCommandHandler>()
            .AddAudit<CapturingAuditWriter>());

        var provider = services.BuildServiceProvider();

        var channel = provider.GetService<Channel<AuditRecord>>();
        Assert.NotNull(channel);
    }

    [Fact]
    public async Task AddAudit_FireAndForgetMode_NoChannelRegistered()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));
        services.AddModernMediator(cfg => cfg
            .RegisterServicesFromAssemblyContaining<TestCommandHandler>()
            .AddAudit<CapturingAuditWriter>(o =>
                o.DispatchMode = AuditDispatchMode.FireAndForget));

        var provider = services.BuildServiceProvider();

        var channel = provider.GetService<Channel<AuditRecord>>();
        Assert.Null(channel);
    }

    [Fact]
    public async Task AddAudit_NoAuditAttribute_RequestIsNotAudited()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));
        services.AddModernMediator(cfg => cfg
            .RegisterServicesFromAssemblyContaining<TestQueryHandler>()
            .AddAudit<CapturingAuditWriter>());

        var provider = services.BuildServiceProvider();
        var channel = provider.GetRequiredService<Channel<AuditRecord>>();
        var mediator = provider.GetRequiredService<IMediator>();

        await mediator.Send(new TestQuery(), CancellationToken.None);

        Assert.Equal(0, channel.Reader.Count);
    }
}
