using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ModernMediator
{
    /// <summary>
    /// Extension methods for registering ModernMediator with dependency injection.
    /// </summary>
    public static class MediatorServiceCollectionExtensions
    {
        /// <summary>
        /// Adds ModernMediator as a scoped service to the dependency injection container.
        /// This is the recommended way to use ModernMediator in modern .NET applications.
        /// Scoped registration ensures handlers can resolve scoped dependencies (e.g., DbContext).
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <remarks>
        /// For Pub/Sub with shared subscriptions across scopes, use <see cref="Mediator.Instance"/> instead.
        /// </remarks>
        public static IServiceCollection AddModernMediator(this IServiceCollection services)
        {
            services.TryAddScoped<IMediator>(sp => new Mediator(sp));
            services.TryAddScoped<ISender>(sp => sp.GetRequiredService<IMediator>());
            services.TryAddScoped<IPublisher>(sp => sp.GetRequiredService<IMediator>());
            services.TryAddScoped<IStreamer>(sp => sp.GetRequiredService<IMediator>());
            return services;
        }

        /// <summary>
        /// Adds ModernMediator with assembly scanning to automatically discover and register handlers.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="assemblies">The assemblies to scan for handlers.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddModernMediator(this IServiceCollection services, params Assembly[] assemblies)
        {
            return services.AddModernMediator(config =>
            {
                config.RegisterServicesFromAssemblies(assemblies);
            });
        }

        /// <summary>
        /// Adds ModernMediator with configuration options including assembly scanning.
        /// Scoped registration ensures handlers can resolve scoped dependencies (e.g., DbContext).
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configure">Action to configure the mediator options.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <remarks>
        /// For Pub/Sub with shared subscriptions across scopes, use <see cref="Mediator.Instance"/> instead.
        /// </remarks>
        public static IServiceCollection AddModernMediator(this IServiceCollection services, Action<MediatorConfiguration> configure)
        {
            var config = new MediatorConfiguration(services);
            configure(config);

            // Register the mediator as scoped to receive the correct scoped IServiceProvider
            services.TryAddScoped<IMediator>(sp =>
            {
                var mediator = new Mediator(sp);
                mediator.SetCachingMode(config.CachingModeValue);

                if (config.ErrorPolicy.HasValue)
                {
                    mediator.ErrorPolicy = config.ErrorPolicy.Value;
                }

                config.ConfigureAction?.Invoke(mediator);

                return mediator;
            });

            // Register segregated interfaces as forwarding aliases
            services.TryAddScoped<ISender>(sp => sp.GetRequiredService<IMediator>());
            services.TryAddScoped<IPublisher>(sp => sp.GetRequiredService<IMediator>());
            services.TryAddScoped<IStreamer>(sp => sp.GetRequiredService<IMediator>());

            return services;
        }
    }
}
