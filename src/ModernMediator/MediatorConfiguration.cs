using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ModernMediator
{
    /// <summary>
    /// Configuration options for ModernMediator registration.
    /// </summary>
    public class MediatorConfiguration
    {
        private readonly IServiceCollection? _services;

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

        /// <summary>
        /// Gets or sets when handler wrappers and lookups are initialized.
        /// Defaults to Eager (initialize on first mediator access).
        /// Use Lazy for cold start scenarios (serverless, Native AOT).
        /// </summary>
        public CachingMode CachingMode { get; set; } = CachingMode.Eager;

        internal CachingMode CachingModeValue => CachingMode;

        /// <summary>
        /// Creates a new configuration for use with source-generated registration.
        /// </summary>
        public MediatorConfiguration()
        {
            _services = null;
        }

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
        /// Applies the configuration to a mediator instance.
        /// Used by source-generated registration.
        /// </summary>
        /// <param name="mediator">The mediator instance to configure.</param>
        public void ApplyConfiguration(IMediator mediator)
        {
            ConfigureAction?.Invoke(mediator);
        }

        /// <summary>
        /// Gets the underlying <see cref="IServiceCollection"/> for direct registration of
        /// related services. Intended for use by package-level extension methods (for example,
        /// FluentValidation, Audit, or Idempotency integrations) that need to register a
        /// pipeline behavior at the current position in the configuration sequence alongside
        /// the supporting services that the behavior depends on.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the configuration was constructed via the parameterless constructor
        /// (source-generated registration scenario) and no service collection is available.
        /// </exception>
        public IServiceCollection Services =>
            _services ?? throw new InvalidOperationException(
                "Assembly scanning and handler registration require IServiceCollection. " +
                "Use AddModernMediator() extension method, not the parameterless constructor.");

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
        /// Note: Open generic types (e.g., ValidationBehavior&lt;,&gt;) are skipped during scanning
        /// and must be registered explicitly via AddOpenBehavior or AddOpenExceptionHandler.
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
                    // Skip open generic types - they cannot be registered via assembly scanning
                    // because the DI container cannot instantiate unbound type parameters.
                    // These must be registered explicitly via AddOpenBehavior, AddOpenExceptionHandler, etc.
                    if (type.IsGenericTypeDefinition)
                    {
                        continue;
                    }

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

                    // Find IValueTaskRequestHandler<,> implementations
                    RegisterInterfaceImplementations(
                        type,
                        typeof(IValueTaskRequestHandler<,>),
                        HandlerLifetime);

                    // Find IValueTaskPipelineBehavior<,> implementations
                    RegisterInterfaceImplementations(
                        type,
                        typeof(IValueTaskPipelineBehavior<,>),
                        BehaviorLifetime);

                    // Find INotificationHandler<> implementations
                    RegisterInterfaceImplementations(
                        type,
                        typeof(INotificationHandler<>),
                        HandlerLifetime);

                    // Find IRequestExceptionHandler<,,> implementations
                    RegisterInterfaceImplementations(
                        type,
                        typeof(IRequestExceptionHandler<,,>),
                        BehaviorLifetime);
                }
            }

            return this;
        }

        private void RegisterInterfaceImplementations(Type implementationType, Type genericInterfaceType, ServiceLifetime lifetime)
        {
            // GetInterfaces() returns all interfaces including inherited ones from base classes
            var interfaces = implementationType.GetInterfaces()
                .Where(i => i.IsGenericType &&
                            i.GetGenericTypeDefinition() == genericInterfaceType);

            foreach (var @interface in interfaces)
            {
                var descriptor = new ServiceDescriptor(@interface, implementationType, lifetime);
                Services.TryAddEnumerable(descriptor);
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
                Services.TryAdd(descriptor);
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
            Services.AddSingleton(typeof(IRequestHandler<TRequest, TResponse>), handler);
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
                Services.TryAdd(descriptor);
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
            Services.AddSingleton(typeof(IStreamRequestHandler<TRequest, TResponse>), handler);
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
                Services.TryAddEnumerable(descriptor);
            }

            return this;
        }

        /// <summary>
        /// Register a ValueTask pipeline behavior type.
        /// </summary>
        /// <typeparam name="TBehavior">The behavior type to register.</typeparam>
        /// <returns>The configuration for chaining.</returns>
        public MediatorConfiguration AddValueTaskBehavior<TBehavior>() where TBehavior : class
        {
            var type = typeof(TBehavior);
            var interfaces = type.GetInterfaces()
                .Where(i => i.IsGenericType &&
                            i.GetGenericTypeDefinition() == typeof(IValueTaskPipelineBehavior<,>));

            foreach (var @interface in interfaces)
            {
                var descriptor = new ServiceDescriptor(@interface, type, BehaviorLifetime);
                Services.TryAddEnumerable(descriptor);
            }

            return this;
        }

        /// <summary>
        /// Register an open generic ValueTask pipeline behavior (applies to all requests).
        /// </summary>
        /// <param name="openBehaviorType">The open generic behavior type.</param>
        /// <returns>The configuration for chaining.</returns>
        public MediatorConfiguration AddOpenValueTaskBehavior(Type openBehaviorType)
        {
            if (!openBehaviorType.IsGenericTypeDefinition)
            {
                throw new ArgumentException("Type must be an open generic type definition", nameof(openBehaviorType));
            }

            var interfaces = openBehaviorType.GetInterfaces()
                .Where(i => i.IsGenericType &&
                            i.GetGenericTypeDefinition() == typeof(IValueTaskPipelineBehavior<,>));

            if (!interfaces.Any())
            {
                throw new ArgumentException(
                    $"Type {openBehaviorType.Name} does not implement IValueTaskPipelineBehavior<,>",
                    nameof(openBehaviorType));
            }

            Services.Add(new ServiceDescriptor(
                typeof(IValueTaskPipelineBehavior<,>),
                openBehaviorType,
                BehaviorLifetime));

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
            Services.Add(new ServiceDescriptor(
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
                Services.TryAddEnumerable(descriptor);
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
                Services.TryAddEnumerable(descriptor);
            }

            return this;
        }

        /// <summary>
        /// Register an exception handler type.
        /// </summary>
        /// <typeparam name="TExceptionHandler">The exception handler type to register.</typeparam>
        /// <returns>The configuration for chaining.</returns>
        public MediatorConfiguration AddExceptionHandler<TExceptionHandler>() where TExceptionHandler : class
        {
            var type = typeof(TExceptionHandler);
            var interfaces = type.GetInterfaces()
                .Where(i => i.IsGenericType &&
                            i.GetGenericTypeDefinition() == typeof(IRequestExceptionHandler<,,>));

            foreach (var @interface in interfaces)
            {
                var descriptor = new ServiceDescriptor(@interface, type, BehaviorLifetime);
                Services.TryAddEnumerable(descriptor);
            }

            return this;
        }

        /// <summary>
        /// Adds the built-in logging pipeline behavior with optional configuration.
        /// Registers <see cref="LoggingOptions"/> as a singleton and
        /// <see cref="LoggingBehavior{TRequest, TResponse}"/> as an open generic pipeline behavior.
        /// </summary>
        /// <param name="configure">Optional action to configure logging options. When <c>null</c>, default options are used.</param>
        /// <returns>The configuration for chaining.</returns>
        public MediatorConfiguration AddLogging(Action<LoggingOptions>? configure = null)
        {
            var options = new LoggingOptions();
            configure?.Invoke(options);

            Services.AddSingleton(options);
            Services.Add(new ServiceDescriptor(
                typeof(IPipelineBehavior<,>),
                typeof(LoggingBehavior<,>),
                BehaviorLifetime));

            return this;
        }

        /// <summary>
        /// Adds the timeout enforcement pipeline behavior.
        /// Registers <see cref="TimeoutBehavior{TRequest, TResponse}"/> as an open generic
        /// pipeline behavior. Handlers opt in to timeout enforcement by decorating their
        /// request type with <see cref="TimeoutAttribute"/>.
        /// </summary>
        /// <returns>The configuration for chaining.</returns>
        public MediatorConfiguration AddTimeout()
        {
            Services.Add(new ServiceDescriptor(
                typeof(IPipelineBehavior<,>),
                typeof(TimeoutBehavior<,>),
                BehaviorLifetime));

            return this;
        }

        /// <summary>
        /// Adds the telemetry registration hook with optional configuration.
        /// Registers <see cref="TelemetryOptions"/> as a singleton.
        /// Actual instrumentation is always active via source-generated dispatch code and incurs
        /// zero cost when no <see cref="System.Diagnostics.ActivityListener"/> or
        /// <see cref="System.Diagnostics.Metrics.MeterListener"/> is subscribed.
        /// </summary>
        /// <param name="configure">Optional action to configure telemetry options. When <c>null</c>, default options are used.</param>
        /// <returns>The configuration for chaining.</returns>
        public MediatorConfiguration AddTelemetry(Action<TelemetryOptions>? configure = null)
        {
            var options = new TelemetryOptions();
            configure?.Invoke(options);

            Services.AddSingleton(options);

            return this;
        }

        /// <summary>
        /// Adds the idempotency pipeline behavior.
        /// Registers <see cref="IdempotencyBehavior{TRequest, TResponse}"/> as an open
        /// generic pipeline behavior. Requests opt in by decorating with
        /// <see cref="IdempotentAttribute"/>.
        /// </summary>
        /// <param name="configure">Optional action to configure idempotency options.</param>
        /// <returns>The configuration for chaining.</returns>
        public MediatorConfiguration AddIdempotency(
            Action<IdempotencyOptions>? configure = null)
        {
            var options = new IdempotencyOptions();
            configure?.Invoke(options);

            switch (options.StoreMode)
            {
                case IdempotencyStoreMode.InMemory:
                    Services.AddMemoryCache();
                    Services.TryAddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();
                    break;
                case IdempotencyStoreMode.DistributedCache:
                    Services.TryAddSingleton<IIdempotencyStore, DistributedIdempotencyStore>();
                    break;
            }

            Services.Add(new ServiceDescriptor(
                typeof(IPipelineBehavior<,>),
                typeof(IdempotencyBehavior<,>),
                BehaviorLifetime));

            return this;
        }

        /// <summary>
        /// Adds the retry pipeline behavior.
        /// Registers <see cref="RetryBehavior{TRequest, TResponse}"/> as an open generic
        /// pipeline behavior. Requests opt in by decorating with <see cref="RetryAttribute"/>.
        /// </summary>
        /// <param name="configure">Optional action to configure retry options.</param>
        /// <returns>The configuration for chaining.</returns>
        public MediatorConfiguration AddRetry(Action<RetryOptions>? configure = null)
        {
            var options = new RetryOptions();
            configure?.Invoke(options);
            Services.AddSingleton(options);
            Services.Add(new ServiceDescriptor(
                typeof(IPipelineBehavior<,>),
                typeof(RetryBehavior<,>),
                BehaviorLifetime));
            return this;
        }

        /// <summary>
        /// Adds the audit pipeline behavior.
        /// Registers <see cref="AuditBehavior{TRequest, TResponse}"/> as an open generic
        /// pipeline behavior. All requests are audited unless decorated with
        /// <see cref="NoAuditAttribute"/>. Requires a registered <see cref="IAuditWriter"/>
        /// implementation.
        /// </summary>
        /// <typeparam name="TWriter">The <see cref="IAuditWriter"/> implementation to register.</typeparam>
        /// <param name="configure">Optional action to configure audit options.</param>
        /// <returns>The configuration for chaining.</returns>
        public MediatorConfiguration AddAudit<TWriter>(
            Action<AuditOptions>? configure = null)
            where TWriter : class, IAuditWriter
        {
            var options = new AuditOptions();
            configure?.Invoke(options);
            Services.AddSingleton(options);
            Services.AddSingleton<IAuditWriter, TWriter>();

            if (options.DispatchMode == AuditDispatchMode.Channel)
                AuditHostedServiceExtensions.AddAuditChannelInfrastructure(Services, options);

            Services.Add(new ServiceDescriptor(
                typeof(IPipelineBehavior<,>),
                typeof(AuditBehavior<,>),
                BehaviorLifetime));

            return this;
        }

        /// <summary>
        /// Adds the circuit breaker pipeline behavior.
        /// Registers <see cref="CircuitBreakerBehavior{TRequest, TResponse}"/> as an open
        /// generic pipeline behavior. Requests opt in by decorating with
        /// <see cref="CircuitBreakerAttribute"/>. Circuit state is scoped per request type.
        /// </summary>
        /// <returns>The configuration for chaining.</returns>
        public MediatorConfiguration AddCircuitBreaker()
        {
            Services.TryAddSingleton<ICircuitBreakerRegistry, CircuitBreakerRegistry>();
            Services.Add(new ServiceDescriptor(
                typeof(IPipelineBehavior<,>),
                typeof(CircuitBreakerBehavior<,>),
                BehaviorLifetime));
            return this;
        }

        /// <summary>
        /// Register an open generic exception handler (applies to all requests for a specific exception type).
        /// </summary>
        /// <param name="openExceptionHandlerType">The open generic exception handler type.</param>
        /// <returns>The configuration for chaining.</returns>
        /// <example>
        /// <code>
        /// config.AddOpenExceptionHandler(typeof(LoggingExceptionHandler&lt;,,&gt;));
        /// </code>
        /// </example>
        public MediatorConfiguration AddOpenExceptionHandler(Type openExceptionHandlerType)
        {
            if (!openExceptionHandlerType.IsGenericTypeDefinition)
            {
                throw new ArgumentException("Type must be an open generic type definition", nameof(openExceptionHandlerType));
            }

            var interfaces = openExceptionHandlerType.GetInterfaces()
                .Where(i => i.IsGenericType &&
                            i.GetGenericTypeDefinition() == typeof(IRequestExceptionHandler<,,>));

            if (!interfaces.Any())
            {
                throw new ArgumentException(
                    $"Type {openExceptionHandlerType.Name} does not implement IRequestExceptionHandler<,,>",
                    nameof(openExceptionHandlerType));
            }

            // Register as open generic
            Services.Add(new ServiceDescriptor(
                typeof(IRequestExceptionHandler<,,>),
                openExceptionHandlerType,
                BehaviorLifetime));

            return this;
        }
    }
}
