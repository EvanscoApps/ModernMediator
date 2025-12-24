using System.Threading.Tasks;

namespace ModernMediator.Internal
{
    /// <summary>
    /// Internal interface for asynchronous callback handler entries that return a response.
    /// </summary>
    internal interface IAsyncCallbackHandlerEntry
    {
        /// <summary>
        /// Returns true if the handler is still alive (not garbage collected).
        /// </summary>
        bool IsAlive { get; }

        /// <summary>
        /// Attempts to invoke the async handler with the given message.
        /// Returns a task with the response if invoked, null if handler is dead or filtered out.
        /// </summary>
        Task<object?>? TryInvokeAsync(object message);
    }
}