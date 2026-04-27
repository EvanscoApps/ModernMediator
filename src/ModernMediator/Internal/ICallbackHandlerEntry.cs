using System;

namespace ModernMediator.Internal
{
    /// <summary>
    /// Internal interface for synchronous callback handler entries that return a response.
    /// </summary>
    internal interface ICallbackHandlerEntry
    {
        /// <summary>
        /// Returns true if the handler is still alive (not garbage collected).
        /// </summary>
        bool IsAlive { get; }

        /// <summary>
        /// Attempts to invoke the handler with the given message and returns the response.
        /// </summary>
        /// <param name="message">The message to pass to the handler.</param>
        /// <param name="response">The response from the handler, if invoked.</param>
        /// <returns>True if the handler was invoked successfully.</returns>
        bool TryInvoke(object message, out object? response);

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