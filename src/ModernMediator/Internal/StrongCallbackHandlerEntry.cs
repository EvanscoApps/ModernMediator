using System;

namespace ModernMediator.Internal
{
    /// <summary>
    /// Callback handler entry that holds a strong reference to the delegate.
    /// The handler will not be garbage collected while subscribed.
    /// </summary>
    internal sealed class StrongCallbackHandlerEntry<TMessage, TResponse> : ICallbackHandlerEntry
    {
        private readonly Func<TMessage, TResponse> _handler;
        private readonly Predicate<TMessage>? _filter;

        public StrongCallbackHandlerEntry(Func<TMessage, TResponse> handler, Predicate<TMessage>? filter)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
            _filter = filter;
        }

        public bool IsAlive => true;

        public bool TryInvoke(object message, out object? response)
        {
            response = default;

            if (message is not TMessage typedMessage)
                return false;

            if (_filter != null && !_filter(typedMessage))
                return false;

            response = _handler(typedMessage);
            return true;
        }
    }
}