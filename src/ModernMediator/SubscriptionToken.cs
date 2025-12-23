using System;

namespace ModernMediator
{
    /// <summary>
    /// Token returned from Subscribe methods that allows unsubscribing via Dispose().
    /// </summary>
    internal sealed class SubscriptionToken : IDisposable
    {
        private Action? _unsubscribeAction;
        private bool _disposed;

        public SubscriptionToken(Action unsubscribeAction)
        {
            _unsubscribeAction = unsubscribeAction ?? throw new ArgumentNullException(nameof(unsubscribeAction));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _unsubscribeAction?.Invoke();
            _unsubscribeAction = null;
        }
    }
}
