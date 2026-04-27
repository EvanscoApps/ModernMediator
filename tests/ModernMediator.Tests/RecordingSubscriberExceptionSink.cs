using System;
using System.Collections.Generic;
using ModernMediator;

namespace ModernMediator.Tests
{
    /// <summary>
    /// Test ISubscriberExceptionSink that records every contained subscriber exception
    /// for assertion. Used to verify ADR-005's subscriber-thrown containment behavior.
    /// </summary>
    public sealed class RecordingSubscriberExceptionSink : ISubscriberExceptionSink
    {
        private readonly List<Exception> _received = new();
        private readonly object _lock = new();

        public IReadOnlyList<Exception> ReceivedExceptions
        {
            get { lock (_lock) return _received.ToArray(); }
        }

        public int ReceivedCount
        {
            get { lock (_lock) return _received.Count; }
        }

        public void OnSubscriberException(Exception exception)
        {
            lock (_lock)
            {
                _received.Add(exception);
            }
        }
    }
}
