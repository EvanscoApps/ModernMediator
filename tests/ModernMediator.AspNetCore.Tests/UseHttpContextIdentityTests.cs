using System.Security.Claims;
using ModernMediator;

namespace ModernMediator.AspNetCore.Tests;

public sealed class UseHttpContextIdentityTests
{
    private static ClaimsPrincipal AuthenticatedUser(string? nameIdentifier = null, string? name = null)
    {
        var claims = new List<Claim>();
        if (nameIdentifier is not null)
            claims.Add(new Claim(ClaimTypes.NameIdentifier, nameIdentifier));
        if (name is not null)
            claims.Add(new Claim(ClaimTypes.Name, name));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "TestScheme"));
    }

    [Fact]
    public void UseHttpContextIdentity_WithAuthenticatedUser_AccessorResolvesUserName()
    {
        var services = new ServiceCollection();
        var options = new AuditOptions();

        options.UseHttpContextIdentity(services);

        var provider = services.BuildServiceProvider();
        var httpContextAccessor = provider.GetRequiredService<IHttpContextAccessor>();
        httpContextAccessor.HttpContext = new DefaultHttpContext
        {
            User = AuthenticatedUser(nameIdentifier: "user-42", name: "alice@example.com")
        };

        var userAccessor = provider.GetRequiredService<ICurrentUserAccessor>();

        Assert.Equal("user-42", userAccessor.UserId);
        Assert.Equal("alice@example.com", userAccessor.UserName);
    }

    [Fact]
    public void UseHttpContextIdentity_WithAnonymousUser_AccessorReturnsNullIdentity()
    {
        var services = new ServiceCollection();
        var options = new AuditOptions();

        options.UseHttpContextIdentity(services);

        var provider = services.BuildServiceProvider();
        var httpContextAccessor = provider.GetRequiredService<IHttpContextAccessor>();
        httpContextAccessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity())
        };

        var userAccessor = provider.GetRequiredService<ICurrentUserAccessor>();

        Assert.Null(userAccessor.UserId);
        Assert.Null(userAccessor.UserName);
    }

    [Fact]
    public void UseHttpContextIdentity_WithoutHttpContextAvailable_AccessorReturnsNullIdentity()
    {
        var services = new ServiceCollection();
        var options = new AuditOptions();

        options.UseHttpContextIdentity(services);

        var provider = services.BuildServiceProvider();
        var httpContextAccessor = provider.GetRequiredService<IHttpContextAccessor>();
        httpContextAccessor.HttpContext = null;

        var userAccessor = provider.GetRequiredService<ICurrentUserAccessor>();

        Assert.Null(userAccessor.UserId);
        Assert.Null(userAccessor.UserName);
    }

    [Fact]
    public void UseHttpContextIdentity_RegistersIHttpContextAccessorAndCurrentUserAccessor()
    {
        var services = new ServiceCollection();
        var options = new AuditOptions();

        options.UseHttpContextIdentity(services);

        var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<IHttpContextAccessor>());
        var userAccessor = provider.GetService<ICurrentUserAccessor>();
        Assert.NotNull(userAccessor);
        Assert.IsType<HttpContextCurrentUserAccessor>(userAccessor);
    }

    [Fact]
    public void UseHttpContextIdentity_ReturnsSameAuditOptionsForChaining()
    {
        var services = new ServiceCollection();
        var options = new AuditOptions();

        var result = options.UseHttpContextIdentity(services);

        Assert.Same(options, result);
    }
}
