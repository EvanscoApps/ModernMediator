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

        public HandlerErrorEventArgs(Exception exception, object message, Type messageType)
        {
            Exception = exception;
            Message = message;
            MessageType = messageType;
        }
    }
}
