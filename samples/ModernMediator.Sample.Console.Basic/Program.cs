using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModernMediator;
using ModernMediator.Sample.Console.Basic.Requests;
using ModernMediator.Sample.Shared.Domain;

// ──────────────────────────────────────────────────────────────
// ModernMediator Console Sample — Basic Features
//
// Demonstrates:
//   1. Basic request/response (GetAnimalsQuery)
//   2. Result<T> pattern    (FindAnimalByNameQuery)
//   3. Logging behavior     (AddLogging with LogRequestPayload)
//   4. Timeout behavior     (GetAnimalProfileQuery / GetSlowAnimalProfileQuery)
// ──────────────────────────────────────────────────────────────

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddModernMediator(config =>
        {
            // Scan this assembly for IRequestHandler<,> implementations
            config.RegisterServicesFromAssemblies(Assembly.GetExecutingAssembly());

            // Built-in logging: logs request name and serialized payload
            config.AddLogging(opts => opts.LogRequestPayload = true);

            // Built-in timeout: enforced per-request via [Timeout(ms)]
            config.AddTimeout();
        });
    })
    .Build();

await host.StartAsync();

var mediator = host.Services.GetRequiredService<IMediator>();

// ── 1. Basic request/response ────────────────────────────────
System.Console.WriteLine("=== 1. Basic Request/Response ===");
var animals = await mediator.Send(new GetAnimalsQuery());
foreach (var animal in animals)
{
    var detail = animal switch
    {
        Dog d => $"  Dog: {d.Name}, {d.AgeYears}y, breed={d.Breed}",
        Cat c => $"  Cat: {c.Name}, {c.AgeYears}y, indoor={c.IsIndoor}",
        Eagle e => $"  Eagle: {e.Name}, {e.AgeYears}y, wingspan={e.WingspanMeters}m",
        _ => $"  {animal.GetType().Name}: {animal.Name}, {animal.AgeYears}y"
    };
    System.Console.WriteLine(detail);
}
System.Console.WriteLine();

// ── 2. Result<T> pattern ─────────────────────────────────────
System.Console.WriteLine("=== 2. Result<T> Pattern ===");

// Successful lookup
var found = await mediator.Send(new FindAnimalByNameQuery("Rex"));
System.Console.WriteLine($"  Find 'Rex': {found}");

// Failed lookup
var notFound = await mediator.Send(new FindAnimalByNameQuery("Nessie"));
System.Console.WriteLine($"  Find 'Nessie': {notFound}");
System.Console.WriteLine();

// ── 3. Logging behavior ─────────────────────────────────────
// Logging is already active — the log entries printed above by
// Microsoft.Extensions.Logging demonstrate LoggingBehavior
// intercepting each Send() call with request payload serialization.
System.Console.WriteLine("=== 3. Logging Behavior ===");
System.Console.WriteLine("  (see log entries above — each dispatch is logged with payload)");
System.Console.WriteLine();

// ── 4. Timeout behavior ─────────────────────────────────────
System.Console.WriteLine("=== 4. Timeout Behavior ===");

// Completes within the 2000ms timeout
var profile = await mediator.Send(new GetAnimalProfileQuery("Rex"));
System.Console.WriteLine($"  Fast profile: {profile.Summary}");

// Exceeds the 100ms timeout — catch the cancellation
System.Console.WriteLine();
System.Console.WriteLine(">>> Dispatching slow query — timeout is EXPECTED (this demonstrates the feature)...");
try
{
    await mediator.Send(new GetSlowAnimalProfileQuery("Rex"));
    System.Console.WriteLine("  Slow profile: completed (unexpected)");
}
catch (OperationCanceledException)
{
    System.Console.WriteLine(">>> [EXPECTED] Timeout fired correctly. OperationCanceledException caught and handled.");
    System.Console.WriteLine("    Note: 'Exception thrown' lines in the VS debug window are normal first-chance notifications.");
}

System.Console.WriteLine();
System.Console.WriteLine("Done.");
System.Console.WriteLine();
System.Console.WriteLine("Press Enter to exit.");
System.Console.ReadLine();

await host.StopAsync();
