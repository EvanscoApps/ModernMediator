using System;

namespace ModernMediator.Internal
{
    /// <summary>
    /// Handler entry that holds a strong reference to the delegate.
    /// The handler will not be garbage collected while subscribed.
    /// </summary>
    internal sealed class StrongHandlerEntry<T> : IHandlerEntry
    {
        private readonly Action<T> _handler;
        private readonly Predicate<T>? _filter;

        public StrongHandlerEntry(Action<T> handler, Predicate<T>? filter)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
            _filter = filter;
        }

        public bool IsAlive => true;

        public bool TryInvoke(object message)
        {
            if (message is not T typedMessage)
                return false;

            if (_filter != null && !_filter(typedMessage))
                return false;

            _handler(typedMessage);
            return true;
        }
    }
}
