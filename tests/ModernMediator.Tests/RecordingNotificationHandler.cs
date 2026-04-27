using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ModernMediator;

namespace ModernMediator.Tests
{
    /// <summary>
    /// Test notification handler that records every Handle invocation and supports
    /// configurable throw behavior. Tests can assert against InvocationCount,
    /// ReceivedNotifications, and InvocationOrder.
    /// </summary>
    public sealed class RecordingNotificationHandler<T> : INotificationHandler<T>
        where T : INotification
    {
        private static int _globalSequence;

        private readonly List<T> _received = new();
        private readonly List<int> _invocationOrder = new();
        private readonly object _lock = new();

        public Exception? AlwaysThrow { get; set; }

        public (int InvocationNumber, Exception Exception)? ThrowOnInvocation { get; set; }

        public bool ObserveCancellation { get; set; } = true;

        public TimeSpan? Delay { get; set; }

        public int InvocationCount
        {
            get { lock (_lock) return _received.Count; }
        }

        public IReadOnlyList<T> ReceivedNotifications
        {
            get { lock (_lock) return _received.ToArray(); }
        }

        public IReadOnlyList<int> InvocationOrder
        {
            get { lock (_lock) return _invocationOrder.ToArray(); }
        }

        public async Task Handle(T notification, CancellationToken cancellationToken)
        {
            int invocationNumber;
            lock (_lock)
            {
                _received.Add(notification);
                invocationNumber = _received.Count;
                _invocationOrder.Add(Interlocked.Increment(ref _globalSequence));
            }

            if (ObserveCancellation && cancellationToken.IsCancellationRequested)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (Delay.HasValue)
            {
                await Task.Delay(Delay.Value, cancellationToken).ConfigureAwait(false);
            }

            if (ThrowOnInvocation.HasValue && ThrowOnInvocation.Value.InvocationNumber == invocationNumber)
            {
                throw ThrowOnInvocation.Value.Exception;
            }

            if (AlwaysThrow != null)
            {
                throw AlwaysThrow;
            }
        }

        public static void ResetGlobalSequence()
        {
            Interlocked.Exchange(ref _globalSequence, 0);
        }
    }
}
