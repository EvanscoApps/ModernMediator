using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using ModernMediator;
using ModernMediator.FluentValidation;
using Xunit;

namespace ModernMediator.FluentValidation.Tests
{
    #region Test Requests, Responses, Handlers, and Validators

    public record ConfigOverloadRequest(string Name) : IRequest<ConfigOverloadResponse>;

    public record ConfigOverloadResponse(string Message);

    public class ConfigOverloadHandler : IRequestHandler<ConfigOverloadRequest, ConfigOverloadResponse>
    {
        public Task<ConfigOverloadResponse> Handle(ConfigOverloadRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ConfigOverloadResponse($"Hello, {request.Name}!"));
        }
    }

    public class ConfigOverloadValidator : AbstractValidator<ConfigOverloadRequest>
    {
        public ConfigOverloadValidator()
        {
            RuleFor(x => x.Name).NotEmpty();
        }
    }

    #endregion

    public class AddModernMediatorValidationConfigurationOverloadTests
    {
        [Fact]
        public void AddModernMediatorValidation_OnConfiguration_RegistersValidatorsFromAssembly()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddModernMediator(config =>
            {
                config.AddModernMediatorValidation(typeof(ConfigOverloadValidator).Assembly);
            });
            var serviceProvider = services.BuildServiceProvider();

            // Assert
            var validators = serviceProvider.GetServices<IValidator<ConfigOverloadRequest>>().ToArray();
            Assert.Single(validators);
            Assert.IsType<ConfigOverloadValidator>(validators[0]);
        }

        [Fact]
        public void AddModernMediatorValidation_OnConfiguration_RegistersValidationBehavior()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddModernMediator(config =>
            {
                config.AddModernMediatorValidation(typeof(ConfigOverloadValidator).Assembly);
            });
            var serviceProvider = services.BuildServiceProvider();

            // Assert
            var behaviors = serviceProvider.GetServices<IPipelineBehavior<ConfigOverloadRequest, ConfigOverloadResponse>>().ToArray();
            Assert.Single(behaviors);
            Assert.IsType<ValidationBehavior<ConfigOverloadRequest, ConfigOverloadResponse>>(behaviors[0]);
        }

        [Fact]
        public void AddModernMediatorValidation_OnConfiguration_HonorsRegistrationOrder()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddModernMediator(config =>
            {
                config.AddLogging();
                config.AddModernMediatorValidation(typeof(ConfigOverloadValidator).Assembly);
                config.AddTimeout();
            });

            // Assert — inspect ServiceDescriptors directly so registration order is verified
            // without requiring LoggingBehavior to be instantiable (it depends on ILogger<T>,
            // which would force a Microsoft.Extensions.Logging package reference into this test
            // project). The mediator dispatches behaviors in DI registration order, so the order
            // of these descriptors equals the pipeline order.
            var openBehaviorTypes = services
                .Where(d => d.ServiceType == typeof(IPipelineBehavior<,>))
                .Select(d => d.ImplementationType)
                .ToArray();
            Assert.Equal(3, openBehaviorTypes.Length);
            Assert.Equal(typeof(LoggingBehavior<,>), openBehaviorTypes[0]);
            Assert.Equal(typeof(ValidationBehavior<,>), openBehaviorTypes[1]);
            Assert.Equal(typeof(TimeoutBehavior<,>), openBehaviorTypes[2]);
        }

        [Fact]
        public void AddModernMediatorValidation_OnConfiguration_NoArgs_DefaultsToCallingAssembly()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddModernMediator(config =>
            {
                config.AddModernMediatorValidation();
            });
            var serviceProvider = services.BuildServiceProvider();

            // Assert
            var validators = serviceProvider.GetServices<IValidator<ConfigOverloadRequest>>().ToArray();
            Assert.Single(validators);
            Assert.IsType<ConfigOverloadValidator>(validators[0]);
        }

        [Fact]
        public void AddModernMediatorValidation_IServiceCollectionOverload_PlacesBehaviorAfterConfigBehaviors()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddModernMediator(config =>
            {
                config.AddLogging();
                config.AddTimeout();
            });
            services.AddModernMediatorValidation(typeof(ConfigOverloadValidator).Assembly);

            // Assert — inspect ServiceDescriptors directly (see HonorsRegistrationOrder for rationale)
            var openBehaviorTypes = services
                .Where(d => d.ServiceType == typeof(IPipelineBehavior<,>))
                .Select(d => d.ImplementationType)
                .ToArray();
            Assert.Equal(3, openBehaviorTypes.Length);
            Assert.Equal(typeof(LoggingBehavior<,>), openBehaviorTypes[0]);
            Assert.Equal(typeof(TimeoutBehavior<,>), openBehaviorTypes[1]);
            Assert.Equal(typeof(ValidationBehavior<,>), openBehaviorTypes[2]);
        }
    }
}
