using System;
using System.Threading.Tasks;

namespace ModernMediator.Internal
{
    /// <summary>
    /// Async callback handler entry that holds a strong reference to the delegate.
    /// The handler will not be garbage collected while subscribed.
    /// </summary>
    internal sealed class StrongAsyncCallbackHandlerEntry<TMessage, TResponse> : IAsyncCallbackHandlerEntry
    {
        private readonly Func<TMessage, Task<TResponse>> _handler;
        private readonly Predicate<TMessage>? _filter;

        public StrongAsyncCallbackHandlerEntry(Func<TMessage, Task<TResponse>> handler, Predicate<TMessage>? filter)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
            _filter = filter;
        }

        public bool IsAlive => true;

        public async Task<object?>? TryInvokeAsync(object message)
        {
            if (message is not TMessage typedMessage)
                return null;

            if (_filter != null && !_filter(typedMessage))
                return null;

            return await _handler(typedMessage).ConfigureAwait(false);
        }
    }
}