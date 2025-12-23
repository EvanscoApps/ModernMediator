using System;
using System.Reflection;

namespace ModernMediator.Internal
{
    /// <summary>
    /// Handler entry that holds a weak reference to the target object.
    /// Allows the target to be garbage collected when no other references exist.
    /// </summary>
    internal sealed class WeakHandlerEntry<T> : IHandlerEntry
    {
        private readonly WeakReference? _targetRef;
        private readonly MethodInfo _method;
        private readonly Predicate<T>? _filter;
        private readonly bool _isStatic;

        public WeakHandlerEntry(Action<T> handler, Predicate<T>? filter)
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

        public bool TryInvoke(object message)
        {
            if (message is not T typedMessage)
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

            _method.Invoke(target, new object[] { typedMessage });
            return true;
        }
    }
}
