using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ModernMediator.Sample.Worker;

public class AnimalSightingWorker : BackgroundService
{
    private readonly IMediator _mediator;
    private readonly ILogger<AnimalSightingWorker> _logger;
    private readonly ActivityListener _activityListener;
    private int _iteration;

    private static readonly string[] Locations =
        ["Central Park", "River Trail", "Mountain Ridge", "Forest Edge", "Lakeside"];

    private static readonly Animal[] Animals =
    [
        new Dog("Rex", 5, "German Shepherd"),
        new Dog("Bella", 3, "Labrador"),
        new Cat("Whiskers", 7, true),
        new Cat("Shadow", 2, false),
        new Eagle("Aquila", 12, 2.1)
    ];

    public AnimalSightingWorker(IMediator mediator, ILogger<AnimalSightingWorker> logger)
    {
        _mediator = mediator;
        _logger = logger;

        // Subscribe to the ModernMediator ActivitySource to observe telemetry spans.
        // In production, you'd wire this to an OpenTelemetry exporter (Jaeger, Zipkin, OTLP)
        // via builder.Services.AddOpenTelemetry().WithTracing(b => b.AddSource("ModernMediator"))
        // instead of a manual ActivityListener.
        _activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == MediatorTelemetry.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity =>
            {
                var duration = activity.Duration.TotalMilliseconds;
                System.Console.WriteLine(
                    $"[Telemetry] Activity: {activity.OperationName} — {duration:F0}ms");
            }
        };
        ActivitySource.AddActivityListener(_activityListener);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        System.Console.WriteLine();
        System.Console.WriteLine("=== ModernMediator Worker Service Sample ===");
        System.Console.WriteLine("Dispatching commands every 3 seconds. Press Ctrl+C to stop.");
        System.Console.WriteLine();

        while (!stoppingToken.IsCancellationRequested)
        {
            _iteration++;

            var animal = Animals[Random.Shared.Next(Animals.Length)];
            var location = Locations[Random.Shared.Next(Locations.Length)];

            // 1. Record a sighting — wrapped in an Activity for telemetry
            var sighting = await DispatchWithActivity(
                nameof(RecordAnimalSightingCommand),
                () => _mediator.Send(new RecordAnimalSightingCommand(animal, location), stoppingToken));

            System.Console.WriteLine(
                $"[Sighting] {sighting.Animal.Name} spotted at {sighting.Location} — recorded as {sighting.Id:N}");

            if (stoppingToken.IsCancellationRequested) break;

            // 2. Every 3rd iteration: generate a report
            if (_iteration % 3 == 0)
            {
                var report = await DispatchWithActivity(
                    nameof(GenerateAnimalReportCommand),
                    () => _mediator.Send(
                        new GenerateAnimalReportCommand(DateOnly.FromDateTime(DateTime.Today)),
                        stoppingToken));

                System.Console.WriteLine(
                    $"[Report]   {report.Date} — {report.SightingsCount} sightings — {report.Summary}");
            }

            if (stoppingToken.IsCancellationRequested) break;

            // 3. Every 5th iteration: dispatch slow command — timeout is EXPECTED
            if (_iteration % 5 == 0)
            {
                try
                {
                    await DispatchWithActivity(
                        nameof(ProcessSlowSightingCommand),
                        () => _mediator.Send(new ProcessSlowSightingCommand(animal), stoppingToken));
                    System.Console.WriteLine("[Slow]     Completed (unexpected)");
                }
                catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
                {
                    System.Console.WriteLine(
                        "[EXPECTED TIMEOUT] ProcessSlowSightingCommand exceeded 100ms — OperationCanceledException caught");
                }
            }

            System.Console.WriteLine("────────────────────────────────────────────");

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        System.Console.WriteLine();
        System.Console.WriteLine("Worker shutting down. Goodbye!");
    }

    /// <summary>
    /// Wraps a mediator dispatch in a <see cref="MediatorTelemetry.ActivitySource"/> activity
    /// so the <see cref="ActivityListener"/> can observe operation name and duration.
    /// </summary>
    private static async Task<T> DispatchWithActivity<T>(string operationName, Func<Task<T>> dispatch)
    {
        using var activity = MediatorTelemetry.ActivitySource.StartActivity(operationName);
        return await dispatch();
    }

    public override void Dispose()
    {
        _activityListener.Dispose();
        base.Dispose();
    }
}
