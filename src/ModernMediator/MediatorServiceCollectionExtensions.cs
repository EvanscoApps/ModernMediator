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
        /// Adds ModernMediator as a singleton service to the dependency injection container.
        /// This is the recommended way to use ModernMediator in modern .NET applications.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddModernMediator(this IServiceCollection services)
        {
            services.TryAddSingleton<IMediator>(sp =>
            {
                var mediator = (Mediator)Mediator.Create();
                mediator.SetServiceProvider(sp);
                return mediator;
            });
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
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configure">Action to configure the mediator options.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddModernMediator(this IServiceCollection services, Action<MediatorConfiguration> configure)
        {
            var config = new MediatorConfiguration(services);
            configure(config);

            // Register the mediator
            services.TryAddSingleton<IMediator>(sp =>
            {
                var mediator = (Mediator)Mediator.Create();
                mediator.SetServiceProvider(sp);

                if (config.ErrorPolicy.HasValue)
                {
                    mediator.ErrorPolicy = config.ErrorPolicy.Value;
                }

                config.ConfigureAction?.Invoke(mediator);

                return mediator;
            });

            return services;
        }
    }

    /// <summary>
    /// Configuration options for ModernMediator registration.
    /// </summary>
    public class MediatorConfiguration
    {
        private readonly IServiceCollection _services;

        internal Action<IMediator>? ConfigureAction { get; private set; }

        /// <summary>
        /// Gets or sets the error policy.
        /// </summary>
        public ErrorPolicy? ErrorPolicy { get; set; }

        /// <summary>
        /// Gets or sets the default handler lifetime. Defaults to Transient.
        /// </summary>
        public ServiceLifetime HandlerLifetime { get; set; } = ServiceLifetime.Transient;

        /// <summary>
        /// Gets or sets the default behavior lifetime. Defaults to Transient.
        /// </summary>
        public ServiceLifetime BehaviorLifetime { get; set; } = ServiceLifetime.Transient;

        internal MediatorConfiguration(IServiceCollection services)
        {
            _services = services;
        }

        /// <summary>
        /// Configure the mediator instance directly (e.g., set dispatcher, subscribe to events).
        /// </summary>
        public MediatorConfiguration Configure(Action<IMediator> configure)
        {
            ConfigureAction = configure;
            return this;
        }

        /// <summary>
        /// Register handlers from the assembly containing the specified type.
        /// </summary>
        /// <typeparam name="T">A type in the assembly to scan.</typeparam>
        /// <returns>The configuration for chaining.</returns>
        public MediatorConfiguration RegisterServicesFromAssemblyContaining<T>()
        {
            return RegisterServicesFromAssemblies(typeof(T).Assembly);
        }

        /// <summary>
        /// Register handlers from the assembly containing the specified type.
        /// </summary>
        /// <param name="type">A type in the assembly to scan.</param>
        /// <returns>The configuration for chaining.</returns>
        public MediatorConfiguration RegisterServicesFromAssemblyContaining(Type type)
        {
            return RegisterServicesFromAssemblies(type.Assembly);
        }

        /// <summary>
        /// Register handlers, behaviors, and processors from the specified assemblies.
        /// </summary>
        /// <param name="assemblies">The assemblies to scan.</param>
        /// <returns>The configuration for chaining.</returns>
        public MediatorConfiguration RegisterServicesFromAssemblies(params Assembly[] assemblies)
        {
            foreach (var assembly in assemblies)
            {
                var types = assembly.GetTypes()
                    .Where(t => t is { IsClass: true, IsAbstract: false });

                foreach (var type in types)
                {
                    // Find IRequestHandler<,> implementations
                    RegisterInterfaceImplementations(
                        type,
                        typeof(IRequestHandler<,>),
                        HandlerLifetime);

                    // Find IStreamRequestHandler<,> implementations
                    RegisterInterfaceImplementations(
                        type,
                        typeof(IStreamRequestHandler<,>),
                        HandlerLifetime);

                    // Find IPipelineBehavior<,> implementations
                    RegisterInterfaceImplementations(
                        type,
                        typeof(IPipelineBehavior<,>),
                        BehaviorLifetime);

                    // Find IRequestPreProcessor<> implementations
                    RegisterInterfaceImplementations(
                        type,
                        typeof(IRequestPreProcessor<>),
                        BehaviorLifetime);

                    // Find IRequestPostProcessor<,> implementations
                    RegisterInterfaceImplementations(
                        type,
                        typeof(IRequestPostProcessor<,>),
                        BehaviorLifetime);
                }
            }

            return this;
        }

        private void RegisterInterfaceImplementations(Type implementationType, Type genericInterfaceType, ServiceLifetime lifetime)
        {
            var interfaces = implementationType.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == genericInterfaceType);

            foreach (var @interface in interfaces)
            {
                var descriptor = new ServiceDescriptor(@interface, implementationType, lifetime);
                _services.TryAddEnumerable(descriptor);
            }
        }

        /// <summary>
        /// Register a specific handler type.
        /// </summary>
        /// <typeparam name="THandler">The handler type to register.</typeparam>
        /// <returns>The configuration for chaining.</returns>
        public MediatorConfiguration RegisterHandler<THandler>() where THandler : class
        {
            var type = typeof(THandler);
            var interfaces = type.GetInterfaces()
                .Where(i => i.IsGenericType &&
                            i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>));

            foreach (var @interface in interfaces)
            {
                var descriptor = new ServiceDescriptor(@interface, type, HandlerLifetime);
                _services.TryAdd(descriptor);
            }

            return this;
        }

        /// <summary>
        /// Register a handler instance.
        /// </summary>
        /// <typeparam name="TRequest">The request type.</typeparam>
        /// <typeparam name="TResponse">The response type.</typeparam>
        /// <param name="handler">The handler instance.</param>
        /// <returns>The configuration for chaining.</returns>
        public MediatorConfiguration RegisterHandler<TRequest, TResponse>(IRequestHandler<TRequest, TResponse> handler)
            where TRequest : IRequest<TResponse>
        {
            _services.AddSingleton(typeof(IRequestHandler<TRequest, TResponse>), handler);
            return this;
        }

        /// <summary>
        /// Register a stream handler type.
        /// </summary>
        /// <typeparam name="THandler">The stream handler type to register.</typeparam>
        /// <returns>The configuration for chaining.</returns>
        public MediatorConfiguration RegisterStreamHandler<THandler>() where THandler : class
        {
            var type = typeof(THandler);
            var interfaces = type.GetInterfaces()
                .Where(i => i.IsGenericType &&
                            i.GetGenericTypeDefinition() == typeof(IStreamRequestHandler<,>));

            foreach (var @interface in interfaces)
            {
                var descriptor = new ServiceDescriptor(@interface, type, HandlerLifetime);
                _services.TryAdd(descriptor);
            }

            return this;
        }

        /// <summary>
        /// Register a stream handler instance.
        /// </summary>
        /// <typeparam name="TRequest">The stream request type.</typeparam>
        /// <typeparam name="TResponse">The response item type.</typeparam>
        /// <param name="handler">The stream handler instance.</param>
        /// <returns>The configuration for chaining.</returns>
        public MediatorConfiguration RegisterStreamHandler<TRequest, TResponse>(IStreamRequestHandler<TRequest, TResponse> handler)
            where TRequest : IStreamRequest<TResponse>
        {
            _services.AddSingleton(typeof(IStreamRequestHandler<TRequest, TResponse>), handler);
            return this;
        }

        /// <summary>
        /// Register a pipeline behavior type.
        /// </summary>
        /// <typeparam name="TBehavior">The behavior type to register.</typeparam>
        /// <returns>The configuration for chaining.</returns>
        public MediatorConfiguration AddBehavior<TBehavior>() where TBehavior : class
        {
            var type = typeof(TBehavior);
            var interfaces = type.GetInterfaces()
                .Where(i => i.IsGenericType &&
                            i.GetGenericTypeDefinition() == typeof(IPipelineBehavior<,>));

            foreach (var @interface in interfaces)
            {
                var descriptor = new ServiceDescriptor(@interface, type, BehaviorLifetime);
                _services.TryAddEnumerable(descriptor);
            }

            return this;
        }

        /// <summary>
        /// Register an open generic pipeline behavior (applies to all requests).
        /// </summary>
        /// <param name="openBehaviorType">The open generic behavior type, e.g., typeof(LoggingBehavior&lt;,&gt;).</param>
        /// <returns>The configuration for chaining.</returns>
        /// <example>
        /// <code>
        /// config.AddOpenBehavior(typeof(LoggingBehavior&lt;,&gt;));
        /// </code>
        /// </example>
        public MediatorConfiguration AddOpenBehavior(Type openBehaviorType)
        {
            if (!openBehaviorType.IsGenericTypeDefinition)
            {
                throw new ArgumentException("Type must be an open generic type definition", nameof(openBehaviorType));
            }

            var interfaces = openBehaviorType.GetInterfaces()
                .Where(i => i.IsGenericType &&
                            i.GetGenericTypeDefinition() == typeof(IPipelineBehavior<,>));

            if (!interfaces.Any())
            {
                throw new ArgumentException(
                    $"Type {openBehaviorType.Name} does not implement IPipelineBehavior<,>",
                    nameof(openBehaviorType));
            }

            // Register as open generic
            _services.Add(new ServiceDescriptor(
                typeof(IPipelineBehavior<,>),
                openBehaviorType,
                BehaviorLifetime));

            return this;
        }

        /// <summary>
        /// Register a pre-processor type.
        /// </summary>
        /// <typeparam name="TPreProcessor">The pre-processor type to register.</typeparam>
        /// <returns>The configuration for chaining.</returns>
        public MediatorConfiguration AddRequestPreProcessor<TPreProcessor>() where TPreProcessor : class
        {
            var type = typeof(TPreProcessor);
            var interfaces = type.GetInterfaces()
                .Where(i => i.IsGenericType &&
                            i.GetGenericTypeDefinition() == typeof(IRequestPreProcessor<>));

            foreach (var @interface in interfaces)
            {
                var descriptor = new ServiceDescriptor(@interface, type, BehaviorLifetime);
                _services.TryAddEnumerable(descriptor);
            }

            return this;
        }

        /// <summary>
        /// Register a post-processor type.
        /// </summary>
        /// <typeparam name="TPostProcessor">The post-processor type to register.</typeparam>
        /// <returns>The configuration for chaining.</returns>
        public MediatorConfiguration AddRequestPostProcessor<TPostProcessor>() where TPostProcessor : class
        {
            var type = typeof(TPostProcessor);
            var interfaces = type.GetInterfaces()
                .Where(i => i.IsGenericType &&
                            i.GetGenericTypeDefinition() == typeof(IRequestPostProcessor<,>));

            foreach (var @interface in interfaces)
            {
                var descriptor = new ServiceDescriptor(@interface, type, BehaviorLifetime);
                _services.TryAddEnumerable(descriptor);
            }

            return this;
        }
    }
}