using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using ModernMediator;

namespace ModernMediator.Tests
{
    /// <summary>
    /// Builds a configured IMediator with registered notification handlers and
    /// optional test infrastructure (sinks, error policy). Tests use this to
    /// avoid repeating DI wiring across test methods.
    /// </summary>
    public sealed class MediatorTestHarness
    {
        private readonly ServiceCollection _services = new();
        private readonly List<object> _handlerInstances = new();
        private RecordingSubscriberExceptionSink? _sink;
        private ErrorPolicy _errorPolicy = ErrorPolicy.ContinueAndAggregate;

        public MediatorTestHarness WithErrorPolicy(ErrorPolicy policy)
        {
            _errorPolicy = policy;
            return this;
        }

        public MediatorTestHarness WithRecordingSink(out RecordingSubscriberExceptionSink sink)
        {
            _sink = new RecordingSubscriberExceptionSink();
            _services.AddSingleton<ISubscriberExceptionSink>(_sink);
            sink = _sink;
            return this;
        }

        public MediatorTestHarness WithHandler<T>(INotificationHandler<T> handler)
            where T : INotification
        {
            _services.AddSingleton<INotificationHandler<T>>(handler);
            _handlerInstances.Add(handler);
            return this;
        }

        public IMediator Build()
        {
            _services.AddModernMediator(config =>
            {
                config.ErrorPolicy = _errorPolicy;
            });

            var provider = _services.BuildServiceProvider();
            return provider.GetRequiredService<IMediator>();
        }
    }
}
