using System;
using Microsoft.Extensions.Logging;

namespace ModernMediator.Internal
{
    /// <summary>
    /// Default <see cref="ISubscriberExceptionSink"/> that routes contained subscriber
    /// exceptions to a Microsoft.Extensions.Logging <see cref="ILogger"/>. If the logger
    /// is null, the sink is a no-op — exceptions are still contained, just not logged.
    /// </summary>
    internal sealed class LoggerSubscriberExceptionSink : ISubscriberExceptionSink
    {
        private readonly ILogger? _logger;

        public LoggerSubscriberExceptionSink(ILogger? logger)
        {
            _logger = logger;
        }

        public void OnSubscriberException(Exception exception)
        {
            try
            {
                _logger?.LogError(exception, "HandlerError subscriber threw an exception. The exception was contained to prevent breaking dispatch.");
            }
            catch
            {
                // If logging itself fails, swallow — there is nowhere left to escalate.
            }
        }
    }
}
