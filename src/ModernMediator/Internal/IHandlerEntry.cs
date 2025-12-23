namespace ModernMediator.Internal
{
    /// <summary>
    /// Internal interface for synchronous handler entries.
    /// </summary>
    internal interface IHandlerEntry
    {
        /// <summary>
        /// Returns true if the handler is still alive (not garbage collected).
        /// </summary>
        bool IsAlive { get; }

        /// <summary>
        /// Attempts to invoke the handler with the given message.
        /// Returns true if the handler was invoked successfully.
        /// </summary>
        bool TryInvoke(object message);
    }
}
