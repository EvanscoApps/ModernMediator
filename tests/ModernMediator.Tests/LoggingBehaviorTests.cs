using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace ModernMediator.Tests
{
    public record LogEntry(LogLevel Level, string Message, Exception? Exception);

    public class FakeLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
        }
    }

    public record LogTestRequest(string Name, int Age) : IRequest<LogTestResponse>;

    public record LogTestResponse(string Result);

    public class LogTestHandler : IRequestHandler<LogTestRequest, LogTestResponse>
    {
        public Task<LogTestResponse> Handle(LogTestRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new LogTestResponse($"Hello {request.Name}"));
        }
    }

    public class LoggingBehaviorTests
    {
        private readonly FakeLogger<LoggingBehavior<LogTestRequest, LogTestResponse>> _logger = new();
        private readonly LogTestRequest _request = new("Alice", 30);
        private readonly RequestHandlerDelegate<LogTestResponse> _next;
        private readonly LogTestResponse _expectedResponse = new("Hello Alice");

        public LoggingBehaviorTests()
        {
            _next = () => Task.FromResult(_expectedResponse);
        }

        [Fact]
        public async Task Handle_LogsRequestAndResponse_AtConfiguredLevels()
        {
            // Arrange
            var options = new LoggingOptions
            {
                RequestLogLevel = LogLevel.Debug,
                ResponseLogLevel = LogLevel.Warning
            };
            var behavior = new LoggingBehavior<LogTestRequest, LogTestResponse>(_logger, options);

            // Act
            var response = await behavior.Handle(_request, _next, CancellationToken.None);

            // Assert
            Assert.Equal(_expectedResponse, response);
            Assert.Equal(2, _logger.Entries.Count);
            Assert.Equal(LogLevel.Debug, _logger.Entries[0].Level);
            Assert.Contains("LogTestRequest", _logger.Entries[0].Message);
            Assert.Equal(LogLevel.Warning, _logger.Entries[1].Level);
            Assert.Contains("LogTestResponse", _logger.Entries[1].Message);
        }

        [Fact]
        public async Task Handle_DefaultLevels_UsesInformationAndError()
        {
            // Arrange
            var options = new LoggingOptions();
            var behavior = new LoggingBehavior<LogTestRequest, LogTestResponse>(_logger, options);

            // Act
            await behavior.Handle(_request, _next, CancellationToken.None);

            // Assert
            Assert.All(_logger.Entries, entry => Assert.Equal(LogLevel.Information, entry.Level));
        }

        [Fact]
        public async Task Handle_Exception_LogsAtErrorLevelAndRethrows()
        {
            // Arrange
            var options = new LoggingOptions { ErrorLogLevel = LogLevel.Critical };
            var behavior = new LoggingBehavior<LogTestRequest, LogTestResponse>(_logger, options);
            var expectedException = new InvalidOperationException("Handler failed");
            RequestHandlerDelegate<LogTestResponse> failingNext = () => throw expectedException;

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => behavior.Handle(_request, failingNext, CancellationToken.None));

            Assert.Same(expectedException, ex);
            // Should have request log + error log (no response log)
            Assert.Equal(2, _logger.Entries.Count);
            Assert.Equal(LogLevel.Information, _logger.Entries[0].Level);
            Assert.Equal(LogLevel.Critical, _logger.Entries[1].Level);
            Assert.Same(expectedException, _logger.Entries[1].Exception);
            Assert.Contains("LogTestRequest", _logger.Entries[1].Message);
        }

        [Fact]
        public async Task Handle_LogRequestPayloadTrue_IncludesSerializedRequest()
        {
            // Arrange
            var options = new LoggingOptions { LogRequestPayload = true };
            var behavior = new LoggingBehavior<LogTestRequest, LogTestResponse>(_logger, options);

            // Act
            await behavior.Handle(_request, _next, CancellationToken.None);

            // Assert
            var requestLog = _logger.Entries[0];
            Assert.Contains("Alice", requestLog.Message);
            Assert.Contains("30", requestLog.Message);
        }

        [Fact]
        public async Task Handle_LogResponsePayloadTrue_IncludesSerializedResponse()
        {
            // Arrange
            var options = new LoggingOptions { LogResponsePayload = true };
            var behavior = new LoggingBehavior<LogTestRequest, LogTestResponse>(_logger, options);

            // Act
            await behavior.Handle(_request, _next, CancellationToken.None);

            // Assert
            var responseLog = _logger.Entries[1];
            Assert.Contains("Hello Alice", responseLog.Message);
        }

        [Fact]
        public async Task Handle_LogRequestPayloadFalse_DoesNotIncludePayload()
        {
            // Arrange
            var options = new LoggingOptions { LogRequestPayload = false };
            var behavior = new LoggingBehavior<LogTestRequest, LogTestResponse>(_logger, options);

            // Act
            await behavior.Handle(_request, _next, CancellationToken.None);

            // Assert
            var requestLog = _logger.Entries[0];
            Assert.DoesNotContain("Alice", requestLog.Message);
            Assert.DoesNotContain("30", requestLog.Message);
        }

        [Fact]
        public async Task Handle_LogResponsePayloadFalse_DoesNotIncludePayload()
        {
            // Arrange
            var options = new LoggingOptions { LogResponsePayload = false };
            var behavior = new LoggingBehavior<LogTestRequest, LogTestResponse>(_logger, options);

            // Act
            await behavior.Handle(_request, _next, CancellationToken.None);

            // Assert
            var responseLog = _logger.Entries[1];
            Assert.DoesNotContain("Hello Alice", responseLog.Message);
        }

        [Fact]
        public void AddLogging_WithCustomOptions_AppliesConfiguration()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddSingleton(typeof(ILogger<>), typeof(FakeLogger<>));
            services.AddModernMediator(config =>
            {
                config.RegisterHandler<LogTestHandler>();
                config.AddLogging(opts =>
                {
                    opts.RequestLogLevel = LogLevel.Trace;
                    opts.ResponseLogLevel = LogLevel.Debug;
                    opts.ErrorLogLevel = LogLevel.Warning;
                    opts.LogRequestPayload = true;
                    opts.LogResponsePayload = true;
                });
            });

            var provider = services.BuildServiceProvider();

            // Assert - options are registered with custom values
            var options = provider.GetRequiredService<LoggingOptions>();
            Assert.Equal(LogLevel.Trace, options.RequestLogLevel);
            Assert.Equal(LogLevel.Debug, options.ResponseLogLevel);
            Assert.Equal(LogLevel.Warning, options.ErrorLogLevel);
            Assert.True(options.LogRequestPayload);
            Assert.True(options.LogResponsePayload);

            // Assert - behavior is registered
            var behaviors = provider.GetServices<IPipelineBehavior<LogTestRequest, LogTestResponse>>().ToList();
            Assert.Contains(behaviors, b => b is LoggingBehavior<LogTestRequest, LogTestResponse>);
        }

        [Fact]
        public void AddLogging_WithDefaultOptions_RegistersDefaults()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddSingleton(typeof(ILogger<>), typeof(FakeLogger<>));
            services.AddModernMediator(config =>
            {
                config.RegisterHandler<LogTestHandler>();
                config.AddLogging();
            });

            var provider = services.BuildServiceProvider();

            // Assert
            var options = provider.GetRequiredService<LoggingOptions>();
            Assert.Equal(LogLevel.Information, options.RequestLogLevel);
            Assert.Equal(LogLevel.Information, options.ResponseLogLevel);
            Assert.Equal(LogLevel.Error, options.ErrorLogLevel);
            Assert.False(options.LogRequestPayload);
            Assert.False(options.LogResponsePayload);
        }
    }
}
