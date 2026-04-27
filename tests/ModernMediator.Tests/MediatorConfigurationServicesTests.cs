using System;
using Microsoft.Extensions.DependencyInjection;
using ModernMediator;
using Xunit;

namespace ModernMediator.Tests
{
    public class MediatorConfigurationServicesTests
    {
        [Fact]
        public void Services_WhenConstructedWithServiceCollection_ReturnsCapturedCollection()
        {
            // Arrange
            var services = new ServiceCollection();
            MediatorConfiguration? capturedConfig = null;

            services.AddModernMediator(config =>
            {
                capturedConfig = config;
            });

            // Act
            var resolved = capturedConfig!.Services;

            // Assert
            Assert.Same(services, resolved);
        }

        [Fact]
        public void Services_WhenConstructedParameterless_ThrowsInvalidOperationException()
        {
            // Arrange
            var config = new MediatorConfiguration();

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() => config.Services);
            Assert.Contains("AddModernMediator()", ex.Message);
        }
    }
}
