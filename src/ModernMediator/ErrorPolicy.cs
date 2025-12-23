namespace ModernMediator
{
    /// <summary>
    /// Policy for handling exceptions in message handlers.
    /// </summary>
    public enum ErrorPolicy
    {
        /// <summary>
        /// Continue processing handlers, aggregate all exceptions into AggregateException.
        /// </summary>
        ContinueAndAggregate,

        /// <summary>
        /// Stop processing on first exception and rethrow immediately.
        /// </summary>
        StopOnFirstError,

        /// <summary>
        /// Log via HandlerError event and continue processing remaining handlers.
        /// WARNING: If no one subscribes to HandlerError event, exceptions are swallowed!
        /// </summary>
        LogAndContinue
    }
}
