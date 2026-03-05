using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModernMediator;
using ModernMediator.Sample.Console.Domain;
using ModernMediator.Sample.Console.PubSub;

// ──────────────────────────────────────────────────────────────
// ModernMediator Console Sample — Pub/Sub (Notifications)
//
// Demonstrates the fire-and-forget pub/sub messaging pattern:
//   - Subscribe: register handlers for a message type
//   - Publish:   broadcast a message to all subscribers
//   - Multiple handlers react to the same notification
//   - Handlers can filter by payload (e.g., animal type)
// ──────────────────────────────────────────────────────────────

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddModernMediator(config =>
        {
            // Logging behavior applies to Send (request/response pipeline).
            // Pub/sub notifications bypass the pipeline — handler output
            // is the primary observable effect.
            config.AddLogging();
        });
    })
    .Build();

await host.StartAsync();

var mediator = host.Services.GetRequiredService<IMediator>();

// ──────────────────────────────────────────────────────────────
// Subscribe handlers to the AnimalSightedNotification message.
//
// Subscribe<T>(Action<T>) registers a callback invoked whenever
// Publish<T>(message) is called. Each handler receives the full
// notification and decides internally whether to react.
//
// weak: false keeps handlers alive for the app's lifetime.
// In long-running apps, use the returned IDisposable to unsubscribe.
// ──────────────────────────────────────────────────────────────

var dogHandler = new DogResponseHandler();
var catHandler = new CatResponseHandler();
var eagleHandler = new EagleResponseHandler();
var logHandler = new AnimalSightingLogHandler();

mediator.Subscribe<AnimalSightedNotification>(dogHandler.Handle, weak: false);
mediator.Subscribe<AnimalSightedNotification>(catHandler.Handle, weak: false);
mediator.Subscribe<AnimalSightedNotification>(eagleHandler.Handle, weak: false);
mediator.Subscribe<AnimalSightedNotification>(logHandler.Handle, weak: false);

// ──────────────────────────────────────────────────────────────
// Publish notifications — each triggers all subscribers.
// Handlers that don't match the animal type silently return.
// ──────────────────────────────────────────────────────────────

// ── 1. Dog sighting ──────────────────────────────────────────
System.Console.WriteLine("=== Sighting 1: Dog at Central Park ===");
mediator.Publish(new AnimalSightedNotification(
    new Dog("Rex", 5, "German Shepherd"), "Central Park"));
System.Console.WriteLine();

// ── 2. Cat sighting ──────────────────────────────────────────
System.Console.WriteLine("=== Sighting 2: Cat at Rooftop Garden ===");
mediator.Publish(new AnimalSightedNotification(
    new Cat("Whiskers", 7, true), "Rooftop Garden"));
System.Console.WriteLine();

// ── 3. Eagle sighting ────────────────────────────────────────
System.Console.WriteLine("=== Sighting 3: Eagle at Mountain Ridge ===");
mediator.Publish(new AnimalSightedNotification(
    new Eagle("Aquila", 12, 2.1), "Mountain Ridge"));

System.Console.WriteLine();
System.Console.WriteLine("Press Enter to exit.");
System.Console.ReadLine();

await host.StopAsync();
