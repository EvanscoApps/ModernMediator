using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ModernMediator.Tests;

public class ScopedServiceResolutionTests
{
    [Fact]
    public void Mediator_IsRegisteredAsScoped()
    {
        var services = new ServiceCollection();
        services.AddModernMediator();

        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IMediator));

        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void Mediator_IsRegisteredAsScoped_WithConfiguration()
    {
        var services = new ServiceCollection();
        services.AddModernMediator(config =>
        {
            config.RegisterServicesFromAssemblyContaining<ScopedServiceResolutionTests>();
        });

        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IMediator));

        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void Mediator_IsRegisteredAsScoped_WithAssemblyArray()
    {
        var services = new ServiceCollection();
        services.AddModernMediator(typeof(ScopedServiceResolutionTests).Assembly);

        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IMediator));

        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public async Task Handler_CanResolveScopedDependencies()
    {
        var services = new ServiceCollection();

        services.AddScoped<IScopedDependency, ScopedDependency>();
        services.AddModernMediator(config =>
        {
            config.RegisterServicesFromAssemblyContaining<ScopedServiceResolutionTests>();
        });

        var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        // Should not throw "Cannot resolve scoped service from root provider"
        var result = await mediator.Send(new ScopedDependencyRequest());

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotEqual(Guid.Empty, result.InstanceId);
    }

    [Fact]
    public async Task DifferentScopes_GetDifferentMediatorInstances()
    {
        var services = new ServiceCollection();
        services.AddModernMediator();

        var provider = services.BuildServiceProvider();

        IMediator mediator1, mediator2;

        using (var scope1 = provider.CreateScope())
        {
            mediator1 = scope1.ServiceProvider.GetRequiredService<IMediator>();
        }

        using (var scope2 = provider.CreateScope())
        {
            mediator2 = scope2.ServiceProvider.GetRequiredService<IMediator>();
        }

        Assert.NotSame(mediator1, mediator2);
    }

    [Fact]
    public async Task SameScope_GetsSameMediatorInstance()
    {
        var services = new ServiceCollection();
        services.AddModernMediator();

        var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var mediator1 = scope.ServiceProvider.GetRequiredService<IMediator>();
        var mediator2 = scope.ServiceProvider.GetRequiredService<IMediator>();

        Assert.Same(mediator1, mediator2);
    }

    [Fact]
    public async Task DifferentScopes_GetDifferentScopedDependencyInstances()
    {
        var services = new ServiceCollection();
        services.AddScoped<IScopedDependency, ScopedDependency>();
        services.AddModernMediator(config =>
        {
            config.RegisterServicesFromAssemblyContaining<ScopedServiceResolutionTests>();
        });

        var provider = services.BuildServiceProvider();

        Guid id1, id2;

        using (var scope1 = provider.CreateScope())
        {
            var mediator = scope1.ServiceProvider.GetRequiredService<IMediator>();
            var result = await mediator.Send(new ScopedDependencyRequest());
            id1 = result.InstanceId;
        }

        using (var scope2 = provider.CreateScope())
        {
            var mediator = scope2.ServiceProvider.GetRequiredService<IMediator>();
            var result = await mediator.Send(new ScopedDependencyRequest());
            id2 = result.InstanceId;
        }

        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public async Task SameScope_GetsSameScopedDependencyInstance()
    {
        var services = new ServiceCollection();
        services.AddScoped<IScopedDependency, ScopedDependency>();
        services.AddModernMediator(config =>
        {
            config.RegisterServicesFromAssemblyContaining<ScopedServiceResolutionTests>();
        });

        var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var result1 = await mediator.Send(new ScopedDependencyRequest());
        var result2 = await mediator.Send(new ScopedDependencyRequest());

        Assert.Equal(result1.InstanceId, result2.InstanceId);
    }

    [Fact]
    public async Task PipelineBehavior_CanResolveScopedDependencies()
    {
        var services = new ServiceCollection();
        services.AddScoped<IScopedDependency, ScopedDependency>();
        services.AddModernMediator(config =>
        {
            config.RegisterServicesFromAssemblyContaining<ScopedServiceResolutionTests>();
            config.AddOpenBehavior(typeof(ScopedBehavior<,>));
        });

        var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        ScopedBehavior<ScopedDependencyRequest, ScopedDependencyResponse>.LastInstanceId = Guid.Empty;

        var result = await mediator.Send(new ScopedDependencyRequest());

        // Behavior should have executed and captured the scoped dependency
        Assert.NotEqual(Guid.Empty, ScopedBehavior<ScopedDependencyRequest, ScopedDependencyResponse>.LastInstanceId);
    }

    [Fact]
    public async Task PreProcessor_CanResolveScopedDependencies()
    {
        var services = new ServiceCollection();
        services.AddScoped<IScopedDependency, ScopedDependency>();
        services.AddModernMediator(config =>
        {
            config.RegisterServicesFromAssemblyContaining<ScopedServiceResolutionTests>();
        });

        var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        ScopedPreProcessor.LastInstanceId = Guid.Empty;

        await mediator.Send(new ScopedDependencyRequest());

        Assert.NotEqual(Guid.Empty, ScopedPreProcessor.LastInstanceId);
    }

    [Fact]
    public async Task PostProcessor_CanResolveScopedDependencies()
    {
        var services = new ServiceCollection();
        services.AddScoped<IScopedDependency, ScopedDependency>();
        services.AddModernMediator(config =>
        {
            config.RegisterServicesFromAssemblyContaining<ScopedServiceResolutionTests>();
        });

        var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        ScopedPostProcessor.LastInstanceId = Guid.Empty;

        await mediator.Send(new ScopedDependencyRequest());

        Assert.NotEqual(Guid.Empty, ScopedPostProcessor.LastInstanceId);
    }

    [Fact]
    public async Task AllPipelineComponents_ShareSameScopedInstance()
    {
        var services = new ServiceCollection();
        services.AddScoped<IScopedDependency, ScopedDependency>();
        services.AddModernMediator(config =>
        {
            config.RegisterServicesFromAssemblyContaining<ScopedServiceResolutionTests>();
            config.AddOpenBehavior(typeof(ScopedBehavior<,>));
        });

        var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        // Reset tracking
        ScopedPreProcessor.LastInstanceId = Guid.Empty;
        ScopedPostProcessor.LastInstanceId = Guid.Empty;
        ScopedBehavior<ScopedDependencyRequest, ScopedDependencyResponse>.LastInstanceId = Guid.Empty;

        var result = await mediator.Send(new ScopedDependencyRequest());

        // All components should have received the same scoped instance
        var handlerId = result.InstanceId;
        var preProcessorId = ScopedPreProcessor.LastInstanceId;
        var postProcessorId = ScopedPostProcessor.LastInstanceId;
        var behaviorId = ScopedBehavior<ScopedDependencyRequest, ScopedDependencyResponse>.LastInstanceId;

        Assert.Equal(handlerId, preProcessorId);
        Assert.Equal(handlerId, postProcessorId);
        Assert.Equal(handlerId, behaviorId);
    }

    [Fact]
    public async Task StreamHandler_CanResolveScopedDependencies()
    {
        var services = new ServiceCollection();
        services.AddScoped<IScopedDependency, ScopedDependency>();
        services.AddModernMediator(config =>
        {
            config.RegisterServicesFromAssemblyContaining<ScopedServiceResolutionTests>();
        });

        var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var results = new System.Collections.Generic.List<Guid>();

        await foreach (var item in mediator.CreateStream(new ScopedStreamRequest()))
        {
            results.Add(item);
        }

        Assert.NotEmpty(results);
        Assert.All(results, id => Assert.NotEqual(Guid.Empty, id));

        // All items should have same scoped instance ID
        Assert.Single(results.Distinct());
    }
}

#region Test Fixtures

public interface IScopedDependency
{
    Guid InstanceId { get; }
}

public class ScopedDependency : IScopedDependency
{
    public Guid InstanceId { get; } = Guid.NewGuid();
}

public record ScopedDependencyRequest : IRequest<ScopedDependencyResponse>;

public record ScopedDependencyResponse(bool Success, Guid InstanceId);

public class ScopedDependencyHandler : IRequestHandler<ScopedDependencyRequest, ScopedDependencyResponse>
{
    private readonly IScopedDependency _dependency;

    public ScopedDependencyHandler(IScopedDependency dependency)
    {
        _dependency = dependency;
    }

    public Task<ScopedDependencyResponse> Handle(ScopedDependencyRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new ScopedDependencyResponse(true, _dependency.InstanceId));
    }
}

public class ScopedBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public static Guid LastInstanceId;

    private readonly IScopedDependency _dependency;

    public ScopedBehavior(IScopedDependency dependency)
    {
        _dependency = dependency;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        LastInstanceId = _dependency.InstanceId;
        return await next();
    }
}

public class ScopedPreProcessor : IRequestPreProcessor<ScopedDependencyRequest>
{
    public static Guid LastInstanceId;

    private readonly IScopedDependency _dependency;

    public ScopedPreProcessor(IScopedDependency dependency)
    {
        _dependency = dependency;
    }

    public Task Process(ScopedDependencyRequest request, CancellationToken cancellationToken)
    {
        LastInstanceId = _dependency.InstanceId;
        return Task.CompletedTask;
    }
}

public class ScopedPostProcessor : IRequestPostProcessor<ScopedDependencyRequest, ScopedDependencyResponse>
{
    public static Guid LastInstanceId;

    private readonly IScopedDependency _dependency;

    public ScopedPostProcessor(IScopedDependency dependency)
    {
        _dependency = dependency;
    }

    public Task Process(ScopedDependencyRequest request, ScopedDependencyResponse response, CancellationToken cancellationToken)
    {
        LastInstanceId = _dependency.InstanceId;
        return Task.CompletedTask;
    }
}

public record ScopedStreamRequest : IStreamRequest<Guid>;

public class ScopedStreamHandler : IStreamRequestHandler<ScopedStreamRequest, Guid>
{
    private readonly IScopedDependency _dependency;

    public ScopedStreamHandler(IScopedDependency dependency)
    {
        _dependency = dependency;
    }

    public async System.Collections.Generic.IAsyncEnumerable<Guid> Handle(
        ScopedStreamRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        for (int i = 0; i < 3; i++)
        {
            yield return _dependency.InstanceId;
            await Task.Delay(10, cancellationToken);
        }
    }
}

#endregion