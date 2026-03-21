using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace ModernMediator.AspNetCore;

/// <summary>
/// An <see cref="ICurrentUserAccessor"/> implementation that resolves user identity
/// from the current HTTP request via <see cref="IHttpContextAccessor"/>.
/// Extracts <see cref="ClaimTypes.NameIdentifier"/> for <c>UserId</c> and
/// <see cref="System.Security.Principal.IIdentity.Name"/> for <c>UserName</c>.
/// Returns <c>null</c> for both properties when no HTTP context is available
/// or when the request is unauthenticated.
/// </summary>
public sealed class HttpContextCurrentUserAccessor : ICurrentUserAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of <see cref="HttpContextCurrentUserAccessor"/>.
    /// </summary>
    public HttpContextCurrentUserAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc/>
    public string? UserId =>
        _httpContextAccessor.HttpContext?.User
            .FindFirstValue(ClaimTypes.NameIdentifier);

    /// <inheritdoc/>
    public string? UserName =>
        _httpContextAccessor.HttpContext?.User.Identity?.Name;
}
