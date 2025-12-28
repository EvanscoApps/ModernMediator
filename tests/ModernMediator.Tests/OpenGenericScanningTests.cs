using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ModernMediator.Tests;

public class OpenGenericScanningTests
{
    [Fact]
    public void RegisterServicesFromAssemblies_SkipsOpenGenericBehaviors()
    {
        var services = new ServiceCollection();

        services.AddModernMediator(config =>
        {
            config.RegisterServicesFromAssemblyContaining<OpenGenericScanningTests>();
        });

        // Open generic behaviors should NOT be registered during assembly scanning
        // They would fail at runtime if registered incorrectly
        var behaviorDescriptors = services
            .Where(d => d.ServiceType.IsGenericType &&
                        d.ServiceType.GetGenericTypeDefinition() == typeof(IPipelineBehavior<,>))
            .ToList();

        // Verify no open generic implementations were registered as closed generics
        Assert.DoesNotContain(behaviorDescriptors,
            d => d.ImplementationType?.IsGenericTypeDefinition == true);
    }

    [Fact]
    public void RegisterServicesFromAssemblies_SkipsOpenGenericExceptionHandlers()
    {
        var services = new ServiceCollection();

        services.AddModernMediator(config =>
        {
            config.RegisterServicesFromAssemblyContaining<OpenGenericScanningTests>();
        });

        var exceptionHandlerDescriptors = services
            .Where(d => d.ServiceType.IsGenericType &&
                        d.ServiceType.GetGenericTypeDefinition() == typeof(IRequestExceptionHandler<,,>))
            .ToList();

        Assert.DoesNotContain(exceptionHandlerDescriptors,
            d => d.ImplementationType?.IsGenericTypeDefinition == true);
    }

    [Fact]
    public void RegisterServicesFromAssemblies_RegistersClosedGenericBehaviors()
    {
        var services = new ServiceCollection();

        services.AddModernMediator(config =>
        {
            config.RegisterServicesFromAssemblyContaining<OpenGenericScanningTests>();
        });

        // Closed generic behaviors SHOULD be registered
        var hasClosedBehavior = services.Any(d =>
            d.ServiceType == typeof(IPipelineBehavior<TestRequest, TestResponse>) &&
            d.ImplementationType == typeof(ClosedGenericBehavior));

        Assert.True(hasClosedBehavior);
    }

    [Fact]
    public void AddOpenBehavior_RegistersCorrectly()
    {
        var services = new ServiceCollection();

        services.AddModernMediator(config =>
        {
            config.AddOpenBehavior(typeof(TestLoggingBehavior<,>));
        });

        var descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(IPipelineBehavior<,>));

        Assert.NotNull(descriptor);
        Assert.Equal(typeof(TestLoggingBehavior<,>), descriptor.ImplementationType);
    }

    [Fact]
    public void AddOpenBehavior_RespectsLifetime()
    {
        var services = new ServiceCollection();

        services.AddModernMediator(config =>
        {
            config.BehaviorLifetime = ServiceLifetime.Singleton;
            config.AddOpenBehavior(typeof(TestLoggingBehavior<,>));
        });

        var descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(IPipelineBehavior<,>));

        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void AddOpenBehavior_ThrowsForNonGenericType()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<ArgumentException>(() =>
        {
            services.AddModernMediator(config =>
            {
                config.AddOpenBehavior(typeof(string));
            });
        });

        Assert.Contains("open generic", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddOpenBehavior_ThrowsForClosedGenericType()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<ArgumentException>(() =>
        {
            services.AddModernMediator(config =>
            {
                config.AddOpenBehavior(typeof(ClosedGenericBehavior));
            });
        });

        Assert.Contains("open generic", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddOpenBehavior_ThrowsForTypeThatDoesNotImplementInterface()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<ArgumentException>(() =>
        {
            services.AddModernMediator(config =>
            {
                config.AddOpenBehavior(typeof(NotABehavior<,>));
            });
        });

        Assert.Contains("IPipelineBehavior", exception.Message);
    }

    [Fact]
    public void AddOpenExceptionHandler_RegistersCorrectly()
    {
        var services = new ServiceCollection();

        services.AddModernMediator(config =>
        {
            config.AddOpenExceptionHandler(typeof(TestExceptionHandler<,,>));
        });

        var descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(IRequestExceptionHandler<,,>));

        Assert.NotNull(descriptor);
        Assert.Equal(typeof(TestExceptionHandler<,,>), descriptor.ImplementationType);
    }

    [Fact]
    public void AddOpenExceptionHandler_ThrowsForNonGenericType()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<ArgumentException>(() =>
        {
            services.AddModernMediator(config =>
            {
                config.AddOpenExceptionHandler(typeof(string));
            });
        });

        Assert.Contains("open generic", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OpenBehavior_IsResolvedAndExecuted()
    {
        var services = new ServiceCollection();

        services.AddModernMediator(config =>
        {
            config.RegisterServicesFromAssemblyContaining<OpenGenericScanningTests>();
            config.AddOpenBehavior(typeof(TestLoggingBehavior<,>));
        });

        var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        TestLoggingBehavior<TestRequest, TestResponse>.ExecutionCount = 0;

        var result = await mediator.Send(new TestRequest("test"));

        Assert.Equal(1, TestLoggingBehavior<TestRequest, TestResponse>.ExecutionCount);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task MultipleOpenBehaviors_AreAllExecuted()
    {
        var services = new ServiceCollection();

        services.AddModernMediator(config =>
        {
            config.RegisterServicesFromAssemblyContaining<OpenGenericScanningTests>();
            config.AddOpenBehavior(typeof(TestLoggingBehavior<,>));
            config.AddOpenBehavior(typeof(TestValidationBehavior<,>));
        });

        var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        TestLoggingBehavior<TestRequest, TestResponse>.ExecutionCount = 0;
        TestValidationBehavior<TestRequest, TestResponse>.ExecutionCount = 0;

        await mediator.Send(new TestRequest("test"));

        Assert.Equal(1, TestLoggingBehavior<TestRequest, TestResponse>.ExecutionCount);
        Assert.Equal(1, TestValidationBehavior<TestRequest, TestResponse>.ExecutionCount);
    }
}

#region Test Fixtures

public record TestRequest(string Value) : IRequest<TestResponse>;

public record TestResponse(string Value);

public class TestRequestHandler : IRequestHandler<TestRequest, TestResponse>
{
    public Task<TestResponse> Handle(TestRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new TestResponse(request.Value));
    }
}

public class TestLoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public static int ExecutionCount;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref ExecutionCount);
        return await next();
    }
}

public class TestValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public static int ExecutionCount;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref ExecutionCount);
        return await next();
    }
}

public class ClosedGenericBehavior : IPipelineBehavior<TestRequest, TestResponse>
{
    public Task<TestResponse> Handle(TestRequest request, RequestHandlerDelegate<TestResponse> next, CancellationToken cancellationToken)
    {
        return next();
    }
}

public class NotABehavior<TRequest, TResponse>
{
    // Does not implement IPipelineBehavior
}

public class TestExceptionHandler<TRequest, TResponse, TException> : IRequestExceptionHandler<TRequest, TResponse, TException>
    where TRequest : IRequest<TResponse>
    where TException : Exception
{
    public Task<ExceptionHandlingResult<TResponse>> Handle(TRequest request, TException exception, CancellationToken cancellationToken)
    {
        return Task.FromResult(ExceptionHandlingResult<TResponse>.NotHandled());
    }
}

#endregion