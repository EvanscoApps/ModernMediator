using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace ModernMediator.AspNetCore.Tests;

public sealed class HttpContextCurrentUserAccessorTests
{
    private static IHttpContextAccessor WithHttpContext(ClaimsPrincipal? user = null)
    {
        var context = new DefaultHttpContext();
        if (user is not null)
            context.User = user;
        return new HttpContextAccessor { HttpContext = context };
    }

    private static IHttpContextAccessor WithNullHttpContext()
    {
        return new HttpContextAccessor { HttpContext = null };
    }

    private static ClaimsPrincipal AuthenticatedUser(string? nameIdentifier = null, string? name = null)
    {
        var claims = new List<Claim>();
        if (nameIdentifier is not null)
            claims.Add(new Claim(ClaimTypes.NameIdentifier, nameIdentifier));
        if (name is not null)
            claims.Add(new Claim(ClaimTypes.Name, name));

        var identity = new ClaimsIdentity(claims, authenticationType: "TestScheme");
        return new ClaimsPrincipal(identity);
    }

    private static ClaimsPrincipal UnauthenticatedUser()
    {
        return new ClaimsPrincipal(new ClaimsIdentity());
    }

    [Fact]
    public void UserId_HttpContextNull_ReturnsNull()
    {
        var accessor = new HttpContextCurrentUserAccessor(WithNullHttpContext());

        Assert.Null(accessor.UserId);
    }

    [Fact]
    public void UserName_HttpContextNull_ReturnsNull()
    {
        var accessor = new HttpContextCurrentUserAccessor(WithNullHttpContext());

        Assert.Null(accessor.UserName);
    }

    [Fact]
    public void UserId_NameIdentifierClaimPresent_ReturnsClaimValue()
    {
        var user = AuthenticatedUser(nameIdentifier: "user-42");
        var accessor = new HttpContextCurrentUserAccessor(WithHttpContext(user));

        Assert.Equal("user-42", accessor.UserId);
    }

    [Fact]
    public void UserId_NameIdentifierClaimAbsent_ReturnsNull()
    {
        var user = AuthenticatedUser(name: "Rob");
        var accessor = new HttpContextCurrentUserAccessor(WithHttpContext(user));

        Assert.Null(accessor.UserId);
    }

    [Fact]
    public void UserName_NameClaimPresent_ReturnsIdentityName()
    {
        var user = AuthenticatedUser(name: "Rob");
        var accessor = new HttpContextCurrentUserAccessor(WithHttpContext(user));

        Assert.Equal("Rob", accessor.UserName);
    }

    [Fact]
    public void UserName_NameClaimAbsent_ReturnsNull()
    {
        var user = AuthenticatedUser(nameIdentifier: "user-42");
        var accessor = new HttpContextCurrentUserAccessor(WithHttpContext(user));

        Assert.Null(accessor.UserName);
    }

    [Fact]
    public void UserId_UnauthenticatedUser_ReturnsNull()
    {
        var accessor = new HttpContextCurrentUserAccessor(WithHttpContext(UnauthenticatedUser()));

        Assert.Null(accessor.UserId);
    }

    [Fact]
    public void UserName_UnauthenticatedUser_ReturnsNull()
    {
        var accessor = new HttpContextCurrentUserAccessor(WithHttpContext(UnauthenticatedUser()));

        Assert.Null(accessor.UserName);
    }
}
