#if WINDOWS
using System;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ModernMediator.Dispatchers
{
    /// <summary>
    /// Dispatcher for Windows Forms applications.
    /// Uses Control.Invoke for UI thread marshalling.
    /// </summary>
    public sealed class WinFormsDispatcher : IDispatcher
    {
        private readonly Control _control;

        /// <summary>
        /// Creates a WinFormsDispatcher using a control for marshalling.
        /// Typically pass your main Form instance.
        /// </summary>
        public WinFormsDispatcher(Control control)
        {
            _control = control ?? throw new ArgumentNullException(nameof(control));
        }

        /// <inheritdoc />
        public bool CheckAccess() => !_control.InvokeRequired;

        /// <inheritdoc />
        public void Invoke(Action action)
        {
            if (_control.IsDisposed)
                return;

            if (CheckAccess())
            {
                action();
            }
            else
            {
                _control.Invoke(action);
            }
        }

        /// <inheritdoc />
        public Task InvokeAsync(Func<Task> func)
        {
            if (_control.IsDisposed)
                return Task.CompletedTask;

            if (CheckAccess())
            {
                return func();
            }
            else
            {
                var tcs = new TaskCompletionSource<bool>();

                _control.BeginInvoke(new Action(async () =>
                {
                    try
                    {
                        await func();
                        tcs.SetResult(true);
                    }
                    catch (Exception ex)
                    {
                        tcs.SetException(ex);
                    }
                }));

                return tcs.Task;
            }
        }
    }
}
#endif
