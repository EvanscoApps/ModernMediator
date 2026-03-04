using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

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
    }
}
