using System;
using System.Reflection;
using System.Threading.Tasks;

namespace ModernMediator.Internal
{
    /// <summary>
    /// Async callback handler entry that holds a weak reference to the target object.
    /// Allows the target to be garbage collected when no other references exist.
    /// </summary>
    internal sealed class WeakAsyncCallbackHandlerEntry<TMessage, TResponse> : IAsyncCallbackHandlerEntry
    {
        private readonly WeakReference? _targetRef;
        private readonly MethodInfo _method;
        private readonly Predicate<TMessage>? _filter;
        private readonly bool _isStatic;

        public WeakAsyncCallbackHandlerEntry(Func<TMessage, Task<TResponse>> handler, Predicate<TMessage>? filter)
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

        public async Task<object?>? TryInvokeAsync(object message)
        {
            if (message is not TMessage typedMessage)
                return null;

            if (_filter != null && !_filter(typedMessage))
                return null;

            object? target = null;

            if (!_isStatic)
            {
                target = _targetRef?.Target;
                if (target == null)
                    return null;
            }

            var task = (Task<TResponse>?)_method.Invoke(target, new object[] { typedMessage! });
            if (task == null)
                return null;

            return await task.ConfigureAwait(false);
        }
    }
}