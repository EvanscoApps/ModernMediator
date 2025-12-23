using System;
using System.Threading.Tasks;

namespace ModernMediator.Internal
{
    /// <summary>
    /// Async handler entry that holds a strong reference to the delegate.
    /// The handler will not be garbage collected while subscribed.
    /// </summary>
    internal sealed class StrongAsyncHandlerEntry<T> : IAsyncHandlerEntry
    {
        private readonly Func<T, Task> _handler;
        private readonly Predicate<T>? _filter;

        public StrongAsyncHandlerEntry(Func<T, Task> handler, Predicate<T>? filter)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
            _filter = filter;
        }

        public bool IsAlive => true;

        public Task? TryInvokeAsync(object message)
        {
            if (message is not T typedMessage)
                return null;

            if (_filter != null && !_filter(typedMessage))
                return null;

            return _handler(typedMessage);
        }
    }
}
