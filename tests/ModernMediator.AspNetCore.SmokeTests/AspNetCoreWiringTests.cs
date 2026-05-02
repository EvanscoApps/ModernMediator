using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using ModernMediator;
using ModernMediator.AspNetCore;
using Xunit;

namespace ModernMediator.AspNetCore.SmokeTests;

/// <summary>
/// Smoke test that confirms <c>AddModernMediatorAspNetCore</c> wires correctly
/// in a real <see cref="WebApplication"/> host built against the packed nupkg.
/// </summary>
public class AspNetCoreWiringTests
{
    /// <summary>
    /// Builds a <see cref="WebApplication"/> with ModernMediator plus the ASP.NET Core
    /// integration registered, plus the documented <see cref="ICurrentUserAccessor"/>
    /// registration, and asserts both resolve from the host's service provider.
    /// </summary>
    [Fact]
    public void Mediator_and_HttpContextCurrentUserAccessor_resolve_after_AddModernMediatorAspNetCore()
    {
        var builder = WebApplication.CreateBuilder(Array.Empty<string>());

        builder.Services.AddModernMediator(c => c.RegisterServicesFromAssemblyContaining<AspNetCoreWiringTests>());
        builder.Services.AddModernMediatorAspNetCore();
        builder.Services.AddSingleton<ICurrentUserAccessor, HttpContextCurrentUserAccessor>();

        using var app = builder.Build();

        var mediator = app.Services.GetRequiredService<IMediator>();
        Assert.NotNull(mediator);

        var accessor = app.Services.GetRequiredService<ICurrentUserAccessor>();
        Assert.IsType<HttpContextCurrentUserAccessor>(accessor);
    }
}
