using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ModernMediator
{
    /// <summary>
    /// A pipeline behavior that logs request entry, successful responses, and exceptions.
    /// Log levels and payload inclusion are controlled via <see cref="LoggingOptions"/>.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;
        private readonly LoggingOptions _options;

        /// <summary>
        /// Initializes a new instance of the <see cref="LoggingBehavior{TRequest, TResponse}"/> class.
        /// </summary>
        /// <param name="logger">The logger instance used to write log entries.</param>
        /// <param name="options">The logging configuration options.</param>
        public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger, LoggingOptions options)
        {
            _logger = logger;
            _options = options;
        }

        /// <inheritdoc />
        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            var requestName = typeof(TRequest).Name;

            if (_options.LogRequestPayload)
            {
                var payload = JsonSerializer.Serialize(request);
                _logger.Log(_options.RequestLogLevel, "Handling {RequestName} with payload {Payload}", requestName, payload);
            }
            else
            {
                _logger.Log(_options.RequestLogLevel, "Handling {RequestName}", requestName);
            }

            TResponse response;
            try
            {
                response = await next();
            }
            catch (Exception ex)
            {
                _logger.Log(_options.ErrorLogLevel, ex, "Error handling {RequestName}", requestName);
                throw;
            }

            var responseName = typeof(TResponse).Name;

            if (_options.LogResponsePayload)
            {
                var payload = JsonSerializer.Serialize(response);
                _logger.Log(_options.ResponseLogLevel, "Handled {RequestName} with response {ResponseName} payload {Payload}", requestName, responseName, payload);
            }
            else
            {
                _logger.Log(_options.ResponseLogLevel, "Handled {RequestName} with response {ResponseName}", requestName, responseName);
            }

            return response;
        }
    }
}
