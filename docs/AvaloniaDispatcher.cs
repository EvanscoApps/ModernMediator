// ============================================================================
// AvaloniaDispatcher.cs
// 
// Drop this file into your Avalonia project to enable UI thread dispatching
// with ModernMediator. Requires the Avalonia NuGet package.
// 
// Usage:
//   mediator.SetDispatcher(new AvaloniaDispatcher());
// ============================================================================

using System;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace ModernMediator.Dispatchers
{
    /// <summary>
    /// Dispatcher for Avalonia applications.
    /// Uses Dispatcher.UIThread for UI thread marshalling.
    /// </summary>
    /// <remarks>
    /// This is a drop-in file. Copy it to your Avalonia project.
    /// Requires the Avalonia NuGet package to be referenced.
    /// </remarks>
    public sealed class AvaloniaDispatcher : IDispatcher
    {
        private readonly Dispatcher _dispatcher;

        /// <summary>
        /// Creates an AvaloniaDispatcher using the UI thread dispatcher.
        /// </summary>
        public AvaloniaDispatcher()
        {
            _dispatcher = Dispatcher.UIThread;
        }

        /// <summary>
        /// Creates an AvaloniaDispatcher with a specific dispatcher instance.
        /// </summary>
        /// <param name="dispatcher">The Avalonia dispatcher to use.</param>
        public AvaloniaDispatcher(Dispatcher dispatcher)
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
                return _dispatcher.InvokeAsync(func);
            }
        }
    }
}