using System;
using System.Reflection;

namespace ModernMediator.Internal
{
    /// <summary>
    /// Callback handler entry that holds a weak reference to the target object.
    /// Allows the target to be garbage collected when no other references exist.
    /// </summary>
    internal sealed class WeakCallbackHandlerEntry<TMessage, TResponse> : ICallbackHandlerEntry
    {
        private readonly WeakReference? _targetRef;
        private readonly MethodInfo _method;
        private readonly Predicate<TMessage>? _filter;
        private readonly bool _isStatic;

        public WeakCallbackHandlerEntry(Func<TMessage, TResponse> handler, Predicate<TMessage>? filter)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            _method = handler.Method;
            _filter = filter;
            _isStatic = handler.Target == null;

            if (!_isStatic)
            {
                _targetRef = new WeakReference(handler.Target);
            }
        }

        public bool IsAlive => _isStatic || (_targetRef?.IsAlive ?? false);

        public bool TryInvoke(object message, out object? response)
        {
            response = default;

            if (message is not TMessage typedMessage)
                return false;

            if (_filter != null && !_filter(typedMessage))
                return false;

            object? target = null;

            if (!_isStatic)
            {
                target = _targetRef?.Target;
                if (target == null)
                    return false;
            }

            response = _method.Invoke(target, new object[] { typedMessage! });
            return true;
        }
    }
}