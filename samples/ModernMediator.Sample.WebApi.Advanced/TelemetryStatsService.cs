using System.Diagnostics.Metrics;

namespace ModernMediator.Sample.WebApi.Advanced;

public class TelemetryStatsService : IDisposable
{
    private long _totalRequests;
    private readonly MeterListener _meterListener;

    public TelemetryStatsService()
    {
        _meterListener = new MeterListener();
        _meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == MediatorTelemetry.SourceName)
                listener.EnableMeasurementEvents(instrument);
        };
        _meterListener.SetMeasurementEventCallback<long>(
            (instrument, measurement, tags, state) =>
            {
                if (instrument.Name == "modernmediator.requests")
                    Interlocked.Add(ref _totalRequests, measurement);
            });
        _meterListener.Start();
    }

    public long TotalRequests => Interlocked.Read(ref _totalRequests);

    public void Dispose() => _meterListener.Dispose();
}
