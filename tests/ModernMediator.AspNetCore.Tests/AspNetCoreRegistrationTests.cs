using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ModernMediator.AspNetCore.Tests;

public sealed class AspNetCoreRegistrationTests
{
    [Fact]
    public void AddModernMediatorAspNetCore_RegistersIHttpContextAccessor()
    {
        var services = new ServiceCollection();

        services.AddModernMediatorAspNetCore();

        var provider = services.BuildServiceProvider();
        var accessor = provider.GetService<IHttpContextAccessor>();

        Assert.NotNull(accessor);
    }

    [Fact]
    public void AddModernMediatorAspNetCore_CalledTwice_DoesNotThrow()
    {
        var services = new ServiceCollection();

        services.AddModernMediatorAspNetCore();
        services.AddModernMediatorAspNetCore();

        var provider = services.BuildServiceProvider();
        var accessor = provider.GetService<IHttpContextAccessor>();

        Assert.NotNull(accessor);
    }
}
