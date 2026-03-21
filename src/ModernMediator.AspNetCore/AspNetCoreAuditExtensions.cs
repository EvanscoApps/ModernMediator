using Microsoft.Extensions.DependencyInjection;

namespace ModernMediator.AspNetCore;

/// <summary>
/// Extension methods for wiring ASP.NET Core identity into
/// <see cref="AuditOptions"/> during <c>AddAudit()</c> registration.
/// </summary>
public static class AspNetCoreAuditExtensions
{
    /// <summary>
    /// Configures <c>AuditBehavior</c> to resolve user identity from the current
    /// HTTP context via <see cref="HttpContextCurrentUserAccessor"/>.
    /// Call this inside <c>AddAudit()</c> when running in an ASP.NET Core host.
    /// </summary>
    /// <example>
    /// <code>
    /// services.AddModernMediator(cfg => cfg
    ///     .AddAudit&lt;MyAuditWriter&gt;(o => o.UseHttpContextIdentity(services)));
    /// </code>
    /// </example>
    public static AuditOptions UseHttpContextIdentity(
        this AuditOptions options,
        IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddSingleton<ICurrentUserAccessor, HttpContextCurrentUserAccessor>();
        return options;
    }
}
