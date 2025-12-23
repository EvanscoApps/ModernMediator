using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ModernMediator;
using Xunit;

namespace ModernMediator.Tests
{
    #region Test Requests and Handlers

    // Simple request with response
    public record GetUserQuery(int UserId) : IRequest<UserDto>;

    public record UserDto(int Id, string Name);

    public class GetUserQueryHandler : IRequestHandler<GetUserQuery, UserDto>
    {
        public Task<UserDto> Handle(GetUserQuery request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new UserDto(request.UserId, $"User {request.UserId}"));
        }
    }

    // Request with no return value
    public record CreateUserCommand(string Name) : IRequest;

    public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, Unit>
    {
        public static int CallCount { get; private set; }

        public Task<Unit> Handle(CreateUserCommand request, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Unit.Task;
        }

        public static void Reset() => CallCount = 0;
    }

    // Handler that throws
    public record FailingRequest : IRequest<string>;

    public class FailingRequestHandler : IRequestHandler<FailingRequest, string>
    {
        public Task<string> Handle(FailingRequest request, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Handler failed intentionally");
        }
    }

    // Async handler with delay
    public record SlowRequest(int DelayMs) : IRequest<string>;

    public class SlowRequestHandler : IRequestHandler<SlowRequest, string>
    {
        public async Task<string> Handle(SlowRequest request, CancellationToken cancellationToken = default)
        {
            await Task.Delay(request.DelayMs, cancellationToken);
            return "Completed";
        }
    }

    #endregion

    public class RequestResponseTests
    {
        [Fact]
        public async Task Send_WithRegisteredHandler_ReturnsResponse()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddModernMediator(config =>
            {
                config.RegisterServicesFromAssemblyContaining<GetUserQueryHandler>();
            });

            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            // Act
            var result = await mediator.Send(new GetUserQuery(42));

            // Assert
            Assert.NotNull(result);
            Assert.Equal(42, result.Id);
            Assert.Equal("User 42", result.Name);
        }

        [Fact]
        public async Task Send_WithNoReturnValue_ReturnsUnit()
        {
            // Arrange
            CreateUserCommandHandler.Reset();
            var services = new ServiceCollection();
            services.AddModernMediator(config =>
            {
                config.RegisterServicesFromAssemblyContaining<CreateUserCommandHandler>();
            });

            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            // Act
            var result = await mediator.Send(new CreateUserCommand("Test User"));

            // Assert
            Assert.Equal(Unit.Value, result);
            Assert.Equal(1, CreateUserCommandHandler.CallCount);
        }

        [Fact]
        public async Task Send_WithNoHandler_ThrowsInvalidOperationException()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddModernMediator(); // No handlers registered

            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await mediator.Send(new GetUserQuery(1)));
        }

        [Fact]
        public async Task Send_NullRequest_ThrowsArgumentNullException()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddModernMediator();

            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await mediator.Send<UserDto>(null!));
        }

        [Fact]
        public async Task Send_HandlerThrows_PropagatesException()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddModernMediator(config =>
            {
                config.RegisterServicesFromAssemblyContaining<FailingRequestHandler>();
            });

            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await mediator.Send(new FailingRequest()));

            Assert.Equal("Handler failed intentionally", ex.Message);
        }

        [Fact]
        public async Task Send_WithCancellationToken_RespectsCancellation()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddModernMediator(config =>
            {
                config.RegisterServicesFromAssemblyContaining<SlowRequestHandler>();
            });

            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();
            var cts = new CancellationTokenSource();
            cts.CancelAfter(50);

            // Act & Assert
            await Assert.ThrowsAsync<TaskCanceledException>(async () =>
                await mediator.Send(new SlowRequest(1000), cts.Token));
        }

        [Fact]
        public async Task Send_AfterDispose_ThrowsObjectDisposedException()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddModernMediator(config =>
            {
                config.RegisterServicesFromAssemblyContaining<GetUserQueryHandler>();
            });

            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();
            mediator.Dispose();

            // Act & Assert
            await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
                await mediator.Send(new GetUserQuery(1)));
        }
    }

    public class AssemblyScanningTests
    {
        [Fact]
        public void RegisterServicesFromAssembly_DiscoversHandlers()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddModernMediator(config =>
            {
                config.RegisterServicesFromAssemblyContaining<GetUserQueryHandler>();
            });

            var provider = services.BuildServiceProvider();

            // Act
            var handler = provider.GetService<IRequestHandler<GetUserQuery, UserDto>>();

            // Assert
            Assert.NotNull(handler);
            Assert.IsType<GetUserQueryHandler>(handler);
        }

        [Fact]
        public void RegisterHandler_Generic_RegistersHandler()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddModernMediator(config =>
            {
                config.RegisterHandler<GetUserQueryHandler>();
            });

            var provider = services.BuildServiceProvider();

            // Act
            var handler = provider.GetService<IRequestHandler<GetUserQuery, UserDto>>();

            // Assert
            Assert.NotNull(handler);
        }

        [Fact]
        public async Task RegisterHandler_Instance_UsesProvidedInstance()
        {
            // Arrange
            var handlerInstance = new GetUserQueryHandler();
            var services = new ServiceCollection();
            services.AddModernMediator(config =>
            {
                config.RegisterHandler<GetUserQuery, UserDto>(handlerInstance);
            });

            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            // Act
            var result = await mediator.Send(new GetUserQuery(99));

            // Assert
            Assert.Equal(99, result.Id);
        }

        [Fact]
        public void HandlerLifetime_Transient_CreatesNewInstances()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddModernMediator(config =>
            {
                config.HandlerLifetime = ServiceLifetime.Transient;
                config.RegisterHandler<GetUserQueryHandler>();
            });

            var provider = services.BuildServiceProvider();

            // Act
            var handler1 = provider.GetService<IRequestHandler<GetUserQuery, UserDto>>();
            var handler2 = provider.GetService<IRequestHandler<GetUserQuery, UserDto>>();

            // Assert
            Assert.NotSame(handler1, handler2);
        }

        [Fact]
        public void HandlerLifetime_Singleton_ReusesSameInstance()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddModernMediator(config =>
            {
                config.HandlerLifetime = ServiceLifetime.Singleton;
                config.RegisterHandler<GetUserQueryHandler>();
            });

            var provider = services.BuildServiceProvider();

            // Act
            var handler1 = provider.GetService<IRequestHandler<GetUserQuery, UserDto>>();
            var handler2 = provider.GetService<IRequestHandler<GetUserQuery, UserDto>>();

            // Assert
            Assert.Same(handler1, handler2);
        }
    }

    public class UnitTypeTests
    {
        [Fact]
        public void Unit_Value_IsSingleton()
        {
            var unit1 = Unit.Value;
            var unit2 = Unit.Value;

            Assert.Equal(unit1, unit2);
        }

        [Fact]
        public void Unit_Equals_AlwaysTrue()
        {
            var unit1 = new Unit();
            var unit2 = new Unit();

            Assert.True(unit1.Equals(unit2));
            Assert.True(unit1 == unit2);
            Assert.False(unit1 != unit2);
        }

        [Fact]
        public void Unit_GetHashCode_AlwaysZero()
        {
            Assert.Equal(0, Unit.Value.GetHashCode());
        }

        [Fact]
        public void Unit_ToString_ReturnsParens()
        {
            Assert.Equal("()", Unit.Value.ToString());
        }

        [Fact]
        public async Task Unit_Task_ReturnsCompletedTask()
        {
            var result = await Unit.Task;

            Assert.Equal(Unit.Value, result);
        }
    }

    public class MediatorConfigurationTests
    {
        [Fact]
        public async Task Configure_SetsErrorPolicy()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddModernMediator(config =>
            {
                config.ErrorPolicy = ErrorPolicy.StopOnFirstError;
                config.RegisterServicesFromAssemblyContaining<GetUserQueryHandler>();
            });

            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            // Assert
            Assert.Equal(ErrorPolicy.StopOnFirstError, mediator.ErrorPolicy);
        }

        [Fact]
        public async Task Configure_Action_ConfiguresMediator()
        {
            // Arrange
            var configureWasCalled = false;
            var services = new ServiceCollection();
            services.AddModernMediator(config =>
            {
                config.Configure(m =>
                {
                    configureWasCalled = true;
                    m.ErrorPolicy = ErrorPolicy.LogAndContinue;
                });
            });

            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            // Assert
            Assert.True(configureWasCalled);
            Assert.Equal(ErrorPolicy.LogAndContinue, mediator.ErrorPolicy);
        }
    }
}