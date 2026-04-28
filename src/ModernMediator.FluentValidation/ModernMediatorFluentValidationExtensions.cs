using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using ModernMediator;

namespace ModernMediator.FluentValidation
{
    /// <summary>
    /// Extension methods for registering ModernMediator FluentValidation integration
    /// with the dependency injection container.
    /// </summary>
    public static class ModernMediatorFluentValidationExtensions
    {
        /// <summary>
        /// Adds FluentValidation integration to the ModernMediator pipeline.
        /// Registers all <see cref="IValidator{T}"/> implementations found in the specified assemblies
        /// and adds <see cref="ValidationBehavior{TRequest, TResponse}"/> as a pipeline behavior.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="assemblies">
        /// The assemblies to scan for validators. If none are provided, defaults to the calling assembly.
        /// </param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddModernMediatorValidation(this IServiceCollection services, params Assembly[] assemblies)
        {
            if (assemblies.Length == 0)
                assemblies = new[] { Assembly.GetCallingAssembly() };

            services.AddValidatorsFromAssemblies(assemblies);

            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

            return services;
        }

        /// <summary>
        /// Adds FluentValidation integration to the ModernMediator pipeline.
        /// Registers all <see cref="IValidator{T}"/> implementations found in the specified assembly
        /// and adds <see cref="ValidationBehavior{TRequest, TResponse}"/> as a pipeline behavior.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="assembly">The assembly to scan for validators.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddModernMediatorValidation(this IServiceCollection services, Assembly assembly)
        {
            return services.AddModernMediatorValidation(new[] { assembly });
        }

        /// <summary>
        /// Adds FluentValidation integration at the current position in the ModernMediator pipeline.
        /// Registers all <see cref="IValidator{T}"/> implementations found in the specified assemblies
        /// and adds <see cref="ValidationBehavior{TRequest, TResponse}"/> as a pipeline behavior at
        /// the configuration's current registration position, allowing the caller to control where
        /// validation runs relative to other behaviors registered via the same configuration.
        /// </summary>
        /// <param name="config">The mediator configuration.</param>
        /// <param name="assemblies">
        /// The assemblies to scan for validators. If none are provided, defaults to the calling assembly.
        /// </param>
        /// <returns>The configuration for chaining.</returns>
        /// <remarks>
        /// The behavior is registered using the configuration's current <see cref="MediatorConfiguration.BehaviorLifetime"/>,
        /// which defaults to <see cref="ServiceLifetime.Transient"/>. This differs from the
        /// <see cref="IServiceCollection"/>-based overloads, which always register the behavior as transient.
        /// </remarks>
        public static MediatorConfiguration AddModernMediatorValidation(this MediatorConfiguration config, params Assembly[] assemblies)
        {
            if (assemblies.Length == 0)
                assemblies = new[] { Assembly.GetCallingAssembly() };

            config.Services.AddValidatorsFromAssemblies(assemblies);

            config.Services.Add(new ServiceDescriptor(
                typeof(IPipelineBehavior<,>),
                typeof(ValidationBehavior<,>),
                config.BehaviorLifetime));

            return config;
        }

        /// <summary>
        /// Adds FluentValidation integration at the current position in the ModernMediator pipeline.
        /// Registers all <see cref="IValidator{T}"/> implementations found in the specified assembly
        /// and adds <see cref="ValidationBehavior{TRequest, TResponse}"/> as a pipeline behavior at
        /// the configuration's current registration position, allowing the caller to control where
        /// validation runs relative to other behaviors registered via the same configuration.
        /// </summary>
        /// <param name="config">The mediator configuration.</param>
        /// <param name="assembly">The assembly to scan for validators.</param>
        /// <returns>The configuration for chaining.</returns>
        /// <remarks>
        /// The behavior is registered using the configuration's current <see cref="MediatorConfiguration.BehaviorLifetime"/>,
        /// which defaults to <see cref="ServiceLifetime.Transient"/>. This differs from the
        /// <see cref="IServiceCollection"/>-based overloads, which always register the behavior as transient.
        /// </remarks>
        public static MediatorConfiguration AddModernMediatorValidation(this MediatorConfiguration config, Assembly assembly)
        {
            return config.AddModernMediatorValidation(new[] { assembly });
        }
    }
}
