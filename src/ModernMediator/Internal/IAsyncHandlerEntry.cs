using System.Threading.Tasks;

namespace ModernMediator.Internal
{
    /// <summary>
    /// Internal interface for asynchronous handler entries.
    /// </summary>
    internal interface IAsyncHandlerEntry
    {
        /// <summary>
        /// Returns true if the handler is still alive (not garbage collected).
        /// </summary>
        bool IsAlive { get; }

        /// <summary>
        /// Attempts to invoke the async handler with the given message.
        /// Returns the task if invoked, null if handler is dead or filtered out.
        /// </summary>
        Task? TryInvokeAsync(object message);
    }
}
