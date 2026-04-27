using System;
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

        /// <summary>
        /// The resolved handler type for this entry, with compiler-generated closure
        /// types unwrapped to the enclosing user type. May be null if the underlying
        /// method's declaring type cannot be determined.
        /// </summary>
        Type? HandlerType { get; }

        /// <summary>
        /// The handler instance for this entry. Returns the delegate target for instance
        /// subscriptions, or null for static subscriptions and dead weak references.
        /// </summary>
        object? HandlerInstance { get; }
    }
}