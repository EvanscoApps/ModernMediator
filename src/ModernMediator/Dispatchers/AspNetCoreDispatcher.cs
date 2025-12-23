using System;
using System.Threading.Tasks;

namespace ModernMediator.Dispatchers
{
    /// <summary>
    /// No-op dispatcher for ASP.NET Core (server-side, no UI thread).
    /// All operations execute on the current thread.
    /// </summary>
    public sealed class AspNetCoreDispatcher : IDispatcher
    {
        /// <inheritdoc />
        public bool CheckAccess() => true;

        /// <inheritdoc />
        public void Invoke(Action action) => action();

        /// <inheritdoc />
        public Task InvokeAsync(Func<Task> func) => func();
    }
}
