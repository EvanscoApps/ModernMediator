using System.Threading;
using System.Threading.Tasks;
using ModernMediator;

namespace ModernMediator.Tests
{
    /// <summary>
    /// Test handler that cancels a captured CancellationTokenSource when invoked.
    /// Used to exercise mid-dispatch cancellation behavior in cancellation contract tests.
    /// </summary>
    public sealed class CancellingNotificationHandler<T> : INotificationHandler<T>
        where T : INotification
    {
        private readonly CancellationTokenSource _source;

        public CancellingNotificationHandler(CancellationTokenSource source)
        {
            _source = source;
        }

        public int InvocationCount { get; private set; }

        public Task Handle(T notification, CancellationToken cancellationToken)
        {
            InvocationCount++;
            _source.Cancel();
            return Task.CompletedTask;
        }
    }
}
