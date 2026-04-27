using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ModernMediator;

namespace ModernMediator.Tests
{
    /// <summary>
    /// Builds a configured IMediator with optional test infrastructure (sink, error policy)
    /// for tests that exercise the runtime Subscribe-callback notification dispatch path.
    /// Subscribers are registered after Build() via Subscribe&lt;T&gt; and SubscribeAsync&lt;T&gt; calls
    /// on the returned mediator, since the Subscribe-callback path is not DI-registered.
    /// </summary>
    public sealed class CallbackTestHarness
    {
        private readonly ServiceCollection _services = new();
        private RecordingSubscriberExceptionSink? _sink;
        private ErrorPolicy _errorPolicy = ErrorPolicy.ContinueAndAggregate;

        public CallbackTestHarness WithErrorPolicy(ErrorPolicy policy)
        {
            _errorPolicy = policy;
            return this;
        }

        public CallbackTestHarness WithRecordingSink(out RecordingSubscriberExceptionSink sink)
        {
            _sink = new RecordingSubscriberExceptionSink();
            _services.AddSingleton<ISubscriberExceptionSink>(_sink);
            sink = _sink;
            return this;
        }

        /// <summary>
        /// Builds and returns the configured mediator. After calling Build, register
        /// subscribers via mediator.Subscribe&lt;T&gt; or mediator.SubscribeAsync&lt;T&gt;.
        /// </summary>
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
