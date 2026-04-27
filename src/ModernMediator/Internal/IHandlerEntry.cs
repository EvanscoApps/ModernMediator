using System;

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
