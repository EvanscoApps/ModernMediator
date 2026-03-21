using Microsoft.Extensions.DependencyInjection;

namespace ModernMediator.AspNetCore
{
    /// <summary>
    /// Extension methods for registering ModernMediator ASP.NET Core integration services.
    /// </summary>
    public static class AspNetCoreServiceCollectionExtensions
    {
        /// <summary>
        /// Adds ModernMediator ASP.NET Core integration services to the dependency injection container.
        /// Registers <see cref="Microsoft.AspNetCore.Http.IHttpContextAccessor"/> and other
        /// ASP.NET Core integration services.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddModernMediatorAspNetCore(
            this IServiceCollection services)
        {
            services.AddHttpContextAccessor();
            return services;
        }
    }
}
