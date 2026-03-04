using Microsoft.Extensions.Logging;

namespace ModernMediator
{
    /// <summary>
    /// Configuration options for the built-in logging pipeline behavior.
    /// </summary>
    public class LoggingOptions
    {
        /// <summary>
        /// Gets or sets the log level used when logging the incoming request.
        /// Defaults to <see cref="LogLevel.Information"/>.
        /// </summary>
        public LogLevel RequestLogLevel { get; set; } = LogLevel.Information;

        /// <summary>
        /// Gets or sets the log level used when logging a successful response.
        /// Defaults to <see cref="LogLevel.Information"/>.
        /// </summary>
        public LogLevel ResponseLogLevel { get; set; } = LogLevel.Information;

        /// <summary>
        /// Gets or sets the log level used when logging an exception.
        /// Defaults to <see cref="LogLevel.Error"/>.
        /// </summary>
        public LogLevel ErrorLogLevel { get; set; } = LogLevel.Error;

        /// <summary>
        /// Gets or sets a value indicating whether the serialized request payload
        /// is included in the request log entry. Defaults to <c>false</c>.
        /// </summary>
        public bool LogRequestPayload { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether the serialized response payload
        /// is included in the response log entry. Defaults to <c>false</c>.
        /// </summary>
        public bool LogResponsePayload { get; set; } = false;
    }
}
