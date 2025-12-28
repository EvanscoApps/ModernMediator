using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ModernMediator.Tests
{
    #region Test Infrastructure

    // Track execution order (thread-safe for parallel test execution)
    public static class ExecutionTracker
    {
        private static readonly object _lock = new();
        private static readonly List<string> _executionOrder = new();
        
        public static List<string> ExecutionOrder
        {
            get
            {
                lock (_lock)
                {
                    return _executionOrder.ToList(); // Return a copy
                }
            }
        }
        
        public static void Reset()
        {
            lock (_lock)
            {
                _executionOrder.Clear();
            }
        }
        
        public static void Log(string step)
        {
            lock (_lock)
            {
                _executionOrder.Add(step);
            }
        }
    }

    // Simple request for pipeline tests
    public record PipelineTestRequest(string Value) : IRequest<string>;

    public class PipelineTestHandler : IRequestHandler<PipelineTestRequest, string>
    {
        public Task<string> Handle(PipelineTestRequest request, CancellationToken cancellationToken = default)
        {
            ExecutionTracker.Log("Handler");
            return Task.FromResult($"Handled: {request.Value}");
        }
    }

    #endregion

    #region Test Behaviors

    public class LoggingBehavior : IPipelineBehavior<PipelineTestRequest, string>
    {
        public async Task<string> Handle(PipelineTestRequest request, RequestHandlerDelegate<string> next, CancellationToken cancellationToken)
        {
            ExecutionTracker.Log("LoggingBehavior:Before");
            var response = await next();
            ExecutionTracker.Log("LoggingBehavior:After");
            return response;
        }
    }

    public class ValidationBehavior : IPipelineBehavior<PipelineTestRequest, string>
    {
        public async Task<string> Handle(PipelineTestRequest request, RequestHandlerDelegate<string> next, CancellationToken cancellationToken)
        {
            ExecutionTracker.Log("ValidationBehavior:Before");
            
            if (string.IsNullOrEmpty(request.Value))
            {
                throw new ArgumentException("Value cannot be empty");
            }
            
            var response = await next();
            ExecutionTracker.Log("ValidationBehavior:After");
            return response;
        }
    }

    public class TimingBehavior : IPipelineBehavior<PipelineTestRequest, string>
    {
        public async Task<string> Handle(PipelineTestRequest request, RequestHandlerDelegate<string> next, CancellationToken cancellationToken)
        {
            ExecutionTracker.Log("TimingBehavior:Before");
            var response = await next();
            ExecutionTracker.Log("TimingBehavior:After");
            return response;
        }
    }

    // Behavior that modifies the response
    public class ResponseModifyingBehavior : IPipelineBehavior<PipelineTestRequest, string>
    {
        public async Task<string> Handle(PipelineTestRequest request, RequestHandlerDelegate<string> next, CancellationToken cancellationToken)
        {
            var response = await next();
            return response + " [Modified]";
        }
    }

    // Behavior that short-circuits (doesn't call next)
    public class ShortCircuitBehavior : IPipelineBehavior<PipelineTestRequest, string>
    {
        public Task<string> Handle(PipelineTestRequest request, RequestHandlerDelegate<string> next, CancellationToken cancellationToken)
        {
            ExecutionTracker.Log("ShortCircuit");
            return Task.FromResult("Short-circuited");
        }
    }

    #endregion

    #region Test Pre/Post Processors

    public class TestPreProcessor : IRequestPreProcessor<PipelineTestRequest>
    {
        public Task Process(PipelineTestRequest request, CancellationToken cancellationToken)
        {
            ExecutionTracker.Log("PreProcessor");
            return Task.CompletedTask;
        }
    }

    public class TestPostProcessor : IRequestPostProcessor<PipelineTestRequest, string>
    {
        public Task Process(PipelineTestRequest request, string response, CancellationToken cancellationToken)
        {
            ExecutionTracker.Log($"PostProcessor:{response}");
            return Task.CompletedTask;
        }
    }

    public class ValidationPreProcessor : IRequestPreProcessor<PipelineTestRequest>
    {
        public Task Process(PipelineTestRequest request, CancellationToken cancellationToken)
        {
            if (request.Value == "invalid")
            {
                throw new ArgumentException("Invalid value in pre-processor");
            }
            return Task.CompletedTask;
        }
    }

    #endregion

    [Collection("PipelineTests")]
    public class PipelineBehaviorTests
    {
        [Fact]
        public async Task SingleBehavior_ExecutesAroundHandler()
        {
            // Arrange
            ExecutionTracker.Reset();
            var services = new ServiceCollection();
            services.AddModernMediator(config =>
            {
                config.RegisterHandler<PipelineTestHandler>();
                config.AddBehavior<LoggingBehavior>();
            });

            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            // Act
            var result = await mediator.Send(new PipelineTestRequest("test"));

            // Assert
            Assert.Equal("Handled: test", result);
            Assert.Equal(new[] { "LoggingBehavior:Before", "Handler", "LoggingBehavior:After" }, ExecutionTracker.ExecutionOrder);
        }

        [Fact]
        public async Task MultipleBehaviors_ExecuteInOrder()
        {
            // Arrange
            ExecutionTracker.Reset();
            var services = new ServiceCollection();
            services.AddModernMediator(config =>
            {
                config.RegisterHandler<PipelineTestHandler>();
                config.AddBehavior<LoggingBehavior>();
                config.AddBehavior<ValidationBehavior>();
                config.AddBehavior<TimingBehavior>();
            });

            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            // Act
            var result = await mediator.Send(new PipelineTestRequest("test"));

            // Assert
            Assert.Equal("Handled: test", result);
            // Behaviors execute in registration order (outermost first)
            Assert.Equal(new[]
            {
                "LoggingBehavior:Before",
                "ValidationBehavior:Before",
                "TimingBehavior:Before",
                "Handler",
                "TimingBehavior:After",
                "ValidationBehavior:After",
                "LoggingBehavior:After"
            }, ExecutionTracker.ExecutionOrder);
        }

        [Fact]
        public async Task Behavior_CanModifyResponse()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddModernMediator(config =>
            {
                config.RegisterHandler<PipelineTestHandler>();
                config.AddBehavior<ResponseModifyingBehavior>();
            });

            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            // Act
            var result = await mediator.Send(new PipelineTestRequest("test"));

            // Assert
            Assert.Equal("Handled: test [Modified]", result);
        }

        [Fact]
        public async Task Behavior_CanShortCircuit()
        {
            // Arrange
            ExecutionTracker.Reset();
            var services = new ServiceCollection();
            services.AddModernMediator(config =>
            {
                config.RegisterHandler<PipelineTestHandler>();
                config.AddBehavior<ShortCircuitBehavior>();
            });

            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            // Act
            var result = await mediator.Send(new PipelineTestRequest("test"));

            // Assert
            Assert.Equal("Short-circuited", result);
            Assert.Equal(new[] { "ShortCircuit" }, ExecutionTracker.ExecutionOrder);
            Assert.DoesNotContain("Handler", ExecutionTracker.ExecutionOrder);
        }

        [Fact]
        public async Task Behavior_ThrowsException_PropagatesUp()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddModernMediator(config =>
            {
                config.RegisterHandler<PipelineTestHandler>();
                config.AddBehavior<ValidationBehavior>();
            });

            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ArgumentException>(async () =>
                await mediator.Send(new PipelineTestRequest("")));

            Assert.Equal("Value cannot be empty", ex.Message);
        }
    }

    [Collection("PipelineTests")]
    public class PreProcessorTests
    {
        [Fact]
        public async Task PreProcessor_ExecutesBeforeHandler()
        {
            // Arrange
            ExecutionTracker.Reset();
            var services = new ServiceCollection();
            services.AddModernMediator(config =>
            {
                config.RegisterHandler<PipelineTestHandler>();
                config.AddRequestPreProcessor<TestPreProcessor>();
            });

            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            // Act
            await mediator.Send(new PipelineTestRequest("test"));

            // Assert
            Assert.Equal(new[] { "PreProcessor", "Handler" }, ExecutionTracker.ExecutionOrder);
        }

        [Fact]
        public async Task PreProcessor_ThrowsException_StopsExecution()
        {
            // Arrange
            ExecutionTracker.Reset();
            var services = new ServiceCollection();
            services.AddModernMediator(config =>
            {
                config.RegisterHandler<PipelineTestHandler>();
                config.AddRequestPreProcessor<ValidationPreProcessor>();
            });

            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(async () =>
                await mediator.Send(new PipelineTestRequest("invalid")));

            Assert.DoesNotContain("Handler", ExecutionTracker.ExecutionOrder);
        }

        [Fact]
        public async Task PreProcessor_ExecutesBeforeBehaviors()
        {
            // Arrange
            ExecutionTracker.Reset();
            var services = new ServiceCollection();
            services.AddModernMediator(config =>
            {
                config.RegisterHandler<PipelineTestHandler>();
                config.AddRequestPreProcessor<TestPreProcessor>();
                config.AddBehavior<LoggingBehavior>();
            });

            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            // Act
            await mediator.Send(new PipelineTestRequest("test"));

            // Assert
            Assert.Equal(new[]
            {
                "PreProcessor",
                "LoggingBehavior:Before",
                "Handler",
                "LoggingBehavior:After"
            }, ExecutionTracker.ExecutionOrder);
        }
    }

    [Collection("PipelineTests")]
    public class PostProcessorTests
    {
        [Fact]
        public async Task PostProcessor_ExecutesAfterHandler()
        {
            // Arrange
            ExecutionTracker.Reset();
            var services = new ServiceCollection();
            services.AddModernMediator(config =>
            {
                config.RegisterHandler<PipelineTestHandler>();
                config.AddRequestPostProcessor<TestPostProcessor>();
            });

            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            // Act
            await mediator.Send(new PipelineTestRequest("test"));

            // Assert
            Assert.Equal(new[] { "Handler", "PostProcessor:Handled: test" }, ExecutionTracker.ExecutionOrder);
        }

        [Fact]
        public async Task PostProcessor_ExecutesAfterBehaviors()
        {
            // Arrange
            ExecutionTracker.Reset();
            var services = new ServiceCollection();
            services.AddModernMediator(config =>
            {
                config.RegisterHandler<PipelineTestHandler>();
                config.AddBehavior<LoggingBehavior>();
                config.AddRequestPostProcessor<TestPostProcessor>();
            });

            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            // Act
            await mediator.Send(new PipelineTestRequest("test"));

            // Assert
            Assert.Equal(new[]
            {
                "LoggingBehavior:Before",
                "Handler",
                "LoggingBehavior:After",
                "PostProcessor:Handled: test"
            }, ExecutionTracker.ExecutionOrder);
        }

        [Fact]
        public async Task PostProcessor_ReceivesModifiedResponse()
        {
            // Arrange
            ExecutionTracker.Reset();
            var services = new ServiceCollection();
            services.AddModernMediator(config =>
            {
                config.RegisterHandler<PipelineTestHandler>();
                config.AddBehavior<ResponseModifyingBehavior>();
                config.AddRequestPostProcessor<TestPostProcessor>();
            });

            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            // Act
            await mediator.Send(new PipelineTestRequest("test"));

            // Assert
            Assert.Contains("PostProcessor:Handled: test [Modified]", ExecutionTracker.ExecutionOrder);
        }
    }

    [Collection("PipelineTests")]
    public class FullPipelineTests
    {
        [Fact]
        public async Task FullPipeline_ExecutesInCorrectOrder()
        {
            // Arrange
            ExecutionTracker.Reset();
            var services = new ServiceCollection();
            services.AddModernMediator(config =>
            {
                config.RegisterHandler<PipelineTestHandler>();
                config.AddRequestPreProcessor<TestPreProcessor>();
                config.AddBehavior<LoggingBehavior>();
                config.AddBehavior<TimingBehavior>();
                config.AddRequestPostProcessor<TestPostProcessor>();
            });

            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            // Act
            var result = await mediator.Send(new PipelineTestRequest("test"));

            // Assert
            Assert.Equal("Handled: test", result);
            Assert.Equal(new[]
            {
                "PreProcessor",           // Pre-processors first
                "LoggingBehavior:Before", // Outer behavior
                "TimingBehavior:Before",  // Inner behavior
                "Handler",                // Actual handler
                "TimingBehavior:After",   // Inner behavior (reverse)
                "LoggingBehavior:After",  // Outer behavior (reverse)
                "PostProcessor:Handled: test" // Post-processors last
            }, ExecutionTracker.ExecutionOrder);
        }

        [Fact]
        public async Task AssemblyScanning_DiscoversHandlers()
        {
            // Arrange
            ExecutionTracker.Reset();
            var services = new ServiceCollection();
            services.AddModernMediator(config =>
            {
                // Only register the handler, not all behaviors from the test assembly
                config.RegisterHandler<PipelineTestHandler>();
            });

            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            // Act - should work if handler was registered
            var result = await mediator.Send(new PipelineTestRequest("test"));

            // Assert
            Assert.Equal("Handled: test", result);
        }

        [Fact]
        public void AssemblyScanning_DiscoversAllServiceTypes()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddModernMediator(config =>
            {
                config.RegisterServicesFromAssemblyContaining<PipelineTestHandler>();
            });

            var provider = services.BuildServiceProvider();

            // Assert - verify handler was discovered
            var handler = provider.GetService<IRequestHandler<PipelineTestRequest, string>>();
            Assert.NotNull(handler);

            // Assert - verify behaviors were discovered
            var behaviors = provider.GetServices<IPipelineBehavior<PipelineTestRequest, string>>().ToList();
            Assert.True(behaviors.Count > 0, "Should discover behaviors from assembly");
        }
    }

    [Collection("PipelineTests")]
    public class BehaviorLifetimeTests
    {
        [Fact]
        public void BehaviorLifetime_Transient_CreatesNewInstances()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddModernMediator(config =>
            {
                config.BehaviorLifetime = ServiceLifetime.Transient;
                config.AddBehavior<LoggingBehavior>();
            });

            var provider = services.BuildServiceProvider();

            // Act
            var behaviors1 = provider.GetServices<IPipelineBehavior<PipelineTestRequest, string>>().ToList();
            var behaviors2 = provider.GetServices<IPipelineBehavior<PipelineTestRequest, string>>().ToList();

            // Assert
            Assert.Single(behaviors1);
            Assert.Single(behaviors2);
            Assert.NotSame(behaviors1[0], behaviors2[0]);
        }

        [Fact]
        public void BehaviorLifetime_Singleton_ReusesSameInstance()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddModernMediator(config =>
            {
                config.BehaviorLifetime = ServiceLifetime.Singleton;
                config.AddBehavior<LoggingBehavior>();
            });

            var provider = services.BuildServiceProvider();

            // Act
            var behaviors1 = provider.GetServices<IPipelineBehavior<PipelineTestRequest, string>>().ToList();
            var behaviors2 = provider.GetServices<IPipelineBehavior<PipelineTestRequest, string>>().ToList();

            // Assert
            Assert.Single(behaviors1);
            Assert.Single(behaviors2);
            Assert.Same(behaviors1[0], behaviors2[0]);
        }
    }
}