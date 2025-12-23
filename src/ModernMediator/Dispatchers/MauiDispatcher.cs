#if MAUI || NET8_0_OR_GREATER
using System;
using System.Threading.Tasks;

namespace ModernMediator.Dispatchers
{
    /// <summary>
    /// Dispatcher for .NET MAUI applications.
    /// Uses MainThread.BeginInvokeOnMainThread for UI thread marshalling.
    /// </summary>
    public sealed class MauiDispatcher : IDispatcher
    {
        /// <inheritdoc />
        public bool CheckAccess()
        {
#if MAUI
            return Microsoft.Maui.ApplicationModel.MainThread.IsMainThread;
#else
            // Fallback for non-MAUI builds - assume we're on main thread
            return true;
#endif
        }

        /// <inheritdoc />
        public void Invoke(Action action)
        {
#if MAUI
            if (CheckAccess())
            {
                action();
            }
            else
            {
                Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(action);
            }
#else
            action();
#endif
        }

        /// <inheritdoc />
        public Task InvokeAsync(Func<Task> func)
        {
#if MAUI
            if (CheckAccess())
            {
                return func();
            }
            else
            {
                return Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(func);
            }
#else
            return func();
#endif
        }
    }
}
#endif
