namespace ModernMediator.Sample.WebApi.Advanced.Requests;

[Endpoint("/api/telemetry/stats", "GET")]
public record GetTelemetryStatsQuery : IRequest<TelemetryStats>;

public record TelemetryStats(
    string ActivitySourceName,
    string MeterName,
    long TotalRequestsRecorded);

public class GetTelemetryStatsQueryHandler : IRequestHandler<GetTelemetryStatsQuery, TelemetryStats>
{
    private readonly TelemetryStatsService _telemetryStats;

    public GetTelemetryStatsQueryHandler(TelemetryStatsService telemetryStats)
        => _telemetryStats = telemetryStats;

    public Task<TelemetryStats> Handle(GetTelemetryStatsQuery request, CancellationToken cancellationToken = default)
    {
        var stats = new TelemetryStats(
            MediatorTelemetry.ActivitySource.Name,
            MediatorTelemetry.Meter.Name,
            _telemetryStats.TotalRequests);

        return Task.FromResult(stats);
    }
}
