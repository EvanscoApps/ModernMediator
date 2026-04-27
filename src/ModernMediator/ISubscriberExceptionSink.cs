using System;

namespace ModernMediator
{
    /// <summary>
    /// Receives exceptions thrown by HandlerError event subscribers. The dispatcher
    /// contains subscriber-thrown exceptions to prevent breaking dispatch (per ADR-005),
    /// and routes them through this sink for diagnostic observation.
    /// </summary>
    /// <remarks>
    /// Consumers may register a custom implementation via dependency injection to route
    /// contained subscriber exceptions to a preferred observability system (structured
    /// logging, OpenTelemetry, Application Insights, etc.). If no custom implementation
    /// is registered, ModernMediator falls back to a default that emits a log entry via
    /// <see cref="Microsoft.Extensions.Logging.ILogger"/> when one is available, and
    /// otherwise silently swallows the exception.
    /// <para>
    /// Implementations must not throw. The dispatcher cannot meaningfully respond to a
    /// sink failure; any exception from a sink is suppressed.
    /// </para>
    /// </remarks>
    public interface ISubscriberExceptionSink
    {
        /// <summary>
        /// Receives a contained subscriber exception. Implementations should log,
        /// record, or otherwise observe the exception for diagnostic purposes.
        /// Must not throw.
        /// </summary>
        /// <param name="exception">The exception thrown by a HandlerError subscriber.</param>
        void OnSubscriberException(Exception exception);
    }
}
