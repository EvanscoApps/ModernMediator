#if WINDOWS
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace ModernMediator.Dispatchers
{
    /// <summary>
    /// Dispatcher for WPF applications.
    /// Uses Application.Current.Dispatcher for UI thread marshalling.
    /// </summary>
    public sealed class WpfDispatcher : IDispatcher
    {
        private readonly Dispatcher _dispatcher;

        /// <summary>
        /// Creates a WpfDispatcher using the current application's dispatcher.
        /// </summary>
        public WpfDispatcher()
        {
            _dispatcher = Application.Current?.Dispatcher
                ?? Dispatcher.CurrentDispatcher;
        }

        /// <summary>
        /// Creates a WpfDispatcher with a specific dispatcher instance.
        /// </summary>
        public WpfDispatcher(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        /// <inheritdoc />
        public bool CheckAccess() => _dispatcher.CheckAccess();

        /// <inheritdoc />
        public void Invoke(Action action)
        {
            if (CheckAccess())
            {
                action();
            }
            else
            {
                _dispatcher.Invoke(action);
            }
        }

        /// <inheritdoc />
        public Task InvokeAsync(Func<Task> func)
        {
            if (CheckAccess())
            {
                return func();
            }
            else
            {
                return _dispatcher.InvokeAsync(func).Task.Unwrap();
            }
        }
    }
}
#endif
