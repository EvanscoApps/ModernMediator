using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ModernMediator
{
    /// <summary>
    /// Extension methods for registering ModernMediator with dependency injection.
    /// </summary>
    public static class ModernMediatorServiceCollectionExtensions
    {
        /// <summary>
        /// Adds ModernMediator as a singleton service to the dependency injection container.
        /// This is the recommended way to use ModernMediator in modern .NET applications.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <example>
        /// <code>
        /// // In Program.cs or Startup.cs
        /// services.AddModernMediator();
        /// 
        /// // Then inject IModernMediator into your classes
        /// public class MyService
        /// {
        ///     private readonly IModernMediator _eventBus;
        ///     
        ///     public MyService(IModernMediator eventBus)
        ///     {
        ///         _eventBus = eventBus;
        ///     }
        /// }
        /// </code>
        /// </example>
        public static IServiceCollection AddModernMediator(this IServiceCollection services)
        {
            services.TryAddSingleton<IModernMediator>(_ => ModernMediator.Create());
            return services;
        }

        /// <summary>
        /// Adds ModernMediator as a singleton service with a custom configuration action.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configure">Action to configure the ModernMediator instance.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <example>
        /// <code>
        /// services.AddModernMediator(bus =>
        /// {
        ///     bus.ErrorPolicy = ErrorPolicy.LogAndContinue;
        ///     bus.SetDispatcher(new WpfDispatcher());
        /// });
        /// </code>
        /// </example>
        public static IServiceCollection AddModernMediator(this IServiceCollection services, Action<IModernMediator> configure)
        {
            services.TryAddSingleton<IModernMediator>(_ =>
            {
                var eventBus = ModernMediator.Create();
                configure(eventBus);
                return eventBus;
            });
            return services;
        }
    }
}
