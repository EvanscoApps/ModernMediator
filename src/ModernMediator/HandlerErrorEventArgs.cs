using System;

namespace ModernMediator
{
    /// <summary>
    /// Event args for handler errors, raised via the HandlerError event.
    /// </summary>
    public class HandlerErrorEventArgs : EventArgs
    {
        /// <summary>
        /// The exception that was thrown by the handler.
        /// </summary>
        public Exception Exception { get; }

        /// <summary>
        /// The message that was being processed when the error occurred.
        /// </summary>
        public object Message { get; }

        /// <summary>
        /// The type of the message that was being processed.
        /// </summary>
        public Type MessageType { get; }

        /// <summary>
        /// The concrete handler type that threw the exception. On the DI-resolved
        /// INotificationHandler path, this is the resolved handler class. On the
        /// Subscribe-callback path, this is Method.DeclaringType with compiler-generated
        /// closure types unwrapped to the enclosing user type. May be null on legacy
        /// code paths that have not been updated to populate this property.
        /// </summary>
        public Type? HandlerType { get; }

        /// <summary>
        /// The handler instance that threw the exception. On the DI-resolved
        /// INotificationHandler path, this is the resolved DI handler instance. On the
        /// Subscribe-callback path, this is Delegate.Target. Null for static delegate
        /// subscriptions and null on legacy code paths that have not been updated to
        /// populate this property.
        /// </summary>
        public object? HandlerInstance { get; }

        /// <summary>
        /// Initializes a new instance of <see cref="HandlerErrorEventArgs"/> with the exception,
        /// message, and message type only.
        /// </summary>
        /// <param name="exception">The exception that was thrown by the handler.</param>
        /// <param name="message">The notification message that was being handled.</param>
        /// <param name="messageType">The runtime type of the message.</param>
        /// <remarks>
        /// This constructor predates the structured handler-identification properties added in v2.2.
        /// The 5-argument constructor is preferred when the dispatch context can supply the handler
        /// type and instance. This overload remains for callers that do not have that context available;
        /// when used, <see cref="HandlerType"/> and <see cref="HandlerInstance"/> are null.
        /// </remarks>
        public HandlerErrorEventArgs(Exception exception, object message, Type messageType)
        {
            Exception = exception;
            Message = message;
            MessageType = messageType;
        }

        /// <summary>
        /// Creates a new <see cref="HandlerErrorEventArgs"/> with handler identification.
        /// Use this overload from dispatch paths that can identify the source handler:
        /// the DI-resolved INotificationHandler path provides the resolved handler type
        /// and instance; the Subscribe-callback path provides Method.DeclaringType
        /// (with closure-type unwrapping) and Delegate.Target.
        /// </summary>
        /// <param name="exception">The exception that was thrown by the handler.</param>
        /// <param name="message">The message that was being processed when the error occurred.</param>
        /// <param name="messageType">The type of the message that was being processed.</param>
        /// <param name="handlerType">
        /// The concrete handler type that threw, or null if not available. On the
        /// DI-resolved path, the resolved handler class. On the Subscribe-callback path,
        /// Method.DeclaringType with compiler-generated closure types unwrapped to the
        /// enclosing user type.
        /// </param>
        /// <param name="handlerInstance">
        /// The handler instance that threw, or null. On the DI-resolved path, the resolved
        /// DI handler instance. On the Subscribe-callback path, Delegate.Target. Null for
        /// static delegate subscriptions.
        /// </param>
        public HandlerErrorEventArgs(Exception exception, object message, Type messageType, Type? handlerType, object? handlerInstance)
        {
            Exception = exception;
            Message = message;
            MessageType = messageType;
            HandlerType = handlerType;
            HandlerInstance = handlerInstance;
        }
    }
}
