using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ModernMediator.Tests
{
    #region Test Types (at namespace level for assembly scanning)

    public record Phase5GetUserQuery(int UserId) : IRequest<Phase5UserDto>;
    public record Phase5UserDto(int Id, string Name);

    public class Phase5GetUserHandler : IRequestHandler<Phase5GetUserQuery, Phase5UserDto>
    {
        public Task<Phase5UserDto> Handle(Phase5GetUserQuery request, CancellationToken ct = default)
        {
            if (request.UserId < 0)
                throw new ArgumentException("UserId cannot be negative", nameof(request));

            if (request.UserId == 0)
                throw new Phase5NotFoundException($"User {request.UserId} not found");

            return Task.FromResult(new Phase5UserDto(request.UserId, $"User {request.UserId}"));
        }
    }

    public class Phase5NotFoundException : Exception
    {
        public Phase5NotFoundException(string message) : base(message) { }
    }

    public class Phase5NotFoundExceptionHandler : RequestExceptionHandler<Phase5GetUserQuery, Phase5UserDto, Phase5NotFoundException>
    {
        protected override Task<ExceptionHandlingResult<Phase5UserDto>> Handle(
            Phase5GetUserQuery request,
            Phase5NotFoundException exception,
            CancellationToken cancellationToken)
        {
            // Return a default user instead of throwing
            return Handled(new Phase5UserDto(0, "Unknown User"));
        }
    }

    public class Phase5ArgumentExceptionHandler : RequestExceptionHandler<Phase5GetUserQuery, Phase5UserDto, ArgumentException>
    {
        protected override Task<ExceptionHandlingResult<Phase5UserDto>> Handle(
            Phase5GetUserQuery request,
            ArgumentException exception,
            CancellationToken cancellationToken)
        {
            // Let it bubble up - don't handle it
            return NotHandled;
        }
    }

    public class Phase5BaseExceptionHandler : RequestExceptionHandler<Phase5GetUserQuery, Phase5UserDto, Exception>
    {
        protected override Task<ExceptionHandlingResult<Phase5UserDto>> Handle(
            Phase5GetUserQuery request,
            Exception exception,
            CancellationToken cancellationToken)
        {
            return Handled(new Phase5UserDto(-1, "Error Handled"));
        }
    }

    #endregion

    public class Phase5Tests
    {
        #region Exception Handler Tests

        [Fact]
        public async Task ExceptionHandler_HandlesException_ReturnsAlternateResponse()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddModernMediator(config =>
            {
                config.RegisterHandler<Phase5GetUserHandler>();
                config.AddExceptionHandler<Phase5NotFoundExceptionHandler>();
            });

            var sp = services.BuildServiceProvider();
            var mediator = sp.GetRequiredService<IMediator>();

            // Act - UserId 0 throws NotFoundException
            var result = await mediator.Send(new Phase5GetUserQuery(0));

            // Assert - Exception was handled, got alternate response
            Assert.NotNull(result);
            Assert.Equal(0, result.Id);
            Assert.Equal("Unknown User", result.Name);
        }

        [Fact]
        public async Task ExceptionHandler_NotHandled_ExceptionBubblesUp()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddModernMediator(config =>
            {
                config.RegisterHandler<Phase5GetUserHandler>();
                config.AddExceptionHandler<Phase5ArgumentExceptionHandler>();
            });

            var sp = services.BuildServiceProvider();
            var mediator = sp.GetRequiredService<IMediator>();

            // Act & Assert - ArgumentException bubbles up
            var ex = await Assert.ThrowsAsync<ArgumentException>(
                () => mediator.Send(new Phase5GetUserQuery(-1)));

            Assert.Contains("cannot be negative", ex.Message);
        }

        [Fact]
        public async Task ExceptionHandler_NoHandlerRegistered_ExceptionBubblesUp()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddModernMediator(config =>
            {
                config.RegisterHandler<Phase5GetUserHandler>();
                // No exception handler registered
            });

            var sp = services.BuildServiceProvider();
            var mediator = sp.GetRequiredService<IMediator>();

            // Act & Assert - NotFoundException bubbles up without handler
            await Assert.ThrowsAsync<Phase5NotFoundException>(
                () => mediator.Send(new Phase5GetUserQuery(0)));
        }

        [Fact]
        public async Task ExceptionHandler_HandlesBaseExceptionType()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddModernMediator(config =>
            {
                config.RegisterHandler<Phase5GetUserHandler>();
                config.AddExceptionHandler<Phase5BaseExceptionHandler>();
            });

            var sp = services.BuildServiceProvider();
            var mediator = sp.GetRequiredService<IMediator>();

            // Act - NotFoundException is caught by base Exception handler
            var result = await mediator.Send(new Phase5GetUserQuery(0));

            // Assert
            Assert.NotNull(result);
            Assert.Equal(-1, result.Id);
            Assert.Equal("Error Handled", result.Name);
        }

        [Fact]
        public async Task ExceptionHandler_DiscoveredByAssemblyScanning()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddModernMediator(config =>
            {
                config.RegisterServicesFromAssemblyContaining<Phase5Tests>();
            });

            var sp = services.BuildServiceProvider();
            var mediator = sp.GetRequiredService<IMediator>();

            // Act - NotFoundException should be caught by Phase5NotFoundExceptionHandler
            var result = await mediator.Send(new Phase5GetUserQuery(0));

            // Assert - Handler was discovered and invoked
            Assert.NotNull(result);
            Assert.Equal(0, result.Id);
            Assert.Equal("Unknown User", result.Name);
        }

        #endregion

        #region CachingMode Tests

        [Fact]
        public void CachingMode_DefaultIsEager()
        {
            // Arrange
            var services = new ServiceCollection();
            MediatorConfiguration? capturedConfig = null;

            services.AddModernMediator(config =>
            {
                capturedConfig = config;
            });

            // Assert
            Assert.NotNull(capturedConfig);
            Assert.Equal(CachingMode.Eager, capturedConfig.CachingMode);
        }

        [Fact]
        public void CachingMode_CanBeSetToLazy()
        {
            // Arrange
            var services = new ServiceCollection();
            MediatorConfiguration? capturedConfig = null;

            services.AddModernMediator(config =>
            {
                config.CachingMode = CachingMode.Lazy;
                capturedConfig = config;
            });

            // Assert
            Assert.NotNull(capturedConfig);
            Assert.Equal(CachingMode.Lazy, capturedConfig.CachingMode);
        }

        [Fact]
        public async Task CachingMode_Lazy_StillWorks()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddModernMediator(config =>
            {
                config.CachingMode = CachingMode.Lazy;
                config.RegisterHandler<Phase5GetUserHandler>();
            });

            var sp = services.BuildServiceProvider();
            var mediator = sp.GetRequiredService<IMediator>();

            // Act
            var result = await mediator.Send(new Phase5GetUserQuery(42));

            // Assert
            Assert.NotNull(result);
            Assert.Equal(42, result.Id);
            Assert.Equal("User 42", result.Name);
        }

        [Fact]
        public async Task CachingMode_Eager_StillWorks()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddModernMediator(config =>
            {
                config.CachingMode = CachingMode.Eager;
                config.RegisterHandler<Phase5GetUserHandler>();
            });

            var sp = services.BuildServiceProvider();
            var mediator = sp.GetRequiredService<IMediator>();

            // Act
            var result = await mediator.Send(new Phase5GetUserQuery(42));

            // Assert
            Assert.NotNull(result);
            Assert.Equal(42, result.Id);
            Assert.Equal("User 42", result.Name);
        }

        #endregion

        #region ExceptionHandlingResult Tests

        [Fact]
        public void ExceptionHandlingResult_Handle_SetsProperties()
        {
            // Arrange & Act
            var result = ExceptionHandlingResult<string>.Handle("test response");

            // Assert
            Assert.True(result.Handled);
            Assert.Equal("test response", result.Response);
        }

        [Fact]
        public void ExceptionHandlingResult_NotHandled_SetsProperties()
        {
            // Arrange & Act
            var result = ExceptionHandlingResult<string>.NotHandled();

            // Assert
            Assert.False(result.Handled);
            Assert.Null(result.Response);
        }

        #endregion
    }
}