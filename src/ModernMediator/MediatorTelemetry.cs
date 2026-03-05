using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ModernMediator
{
    /// <summary>
    /// Provides shared OpenTelemetry instrumentation primitives for ModernMediator.
    /// The <see cref="ActivitySource"/> and <see cref="Meter"/> are zero-cost when no
    /// <see cref="ActivityListener"/> or <see cref="MeterListener"/> is subscribed.
    /// </summary>
    public static class MediatorTelemetry
    {
        /// <summary>
        /// The source name used for both the <see cref="ActivitySource"/> and the <see cref="Meter"/>.
        /// </summary>
        public const string SourceName = "ModernMediator";

        /// <summary>
        /// The <see cref="System.Diagnostics.ActivitySource"/> used to create tracing activities
        /// for mediator request dispatch.
        /// </summary>
        public static readonly ActivitySource ActivitySource = new(SourceName);

        /// <summary>
        /// The <see cref="System.Diagnostics.Metrics.Meter"/> used to create metrics instruments
        /// for mediator request dispatch.
        /// </summary>
        public static readonly Meter Meter = new(SourceName);

        /// <summary>
        /// Counter that tracks the total number of requests dispatched through the mediator.
        /// </summary>
        public static readonly Counter<long> RequestCounter =
            Meter.CreateCounter<long>("modernmediator.requests", "requests", "Total requests dispatched");

        /// <summary>
        /// Histogram that records request handler duration in milliseconds.
        /// </summary>
        public static readonly Histogram<double> RequestDuration =
            Meter.CreateHistogram<double>("modernmediator.request_duration", "ms", "Request handler duration in milliseconds");
    }
}
