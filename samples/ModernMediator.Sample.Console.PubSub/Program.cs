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

        // Register the DI-path throwing handler so the mediator resolves and
        // dispatches it when ProblematicSightingNotification is published.
        services.AddTransient<INotificationHandler<ProblematicSightingNotification>, ThrowingDiHandler>();

        // Override the default ISubscriberExceptionSink so contained subscriber
        // exceptions surface in the demo's console output.
        services.AddSingleton<ISubscriberExceptionSink, ConsoleSubscriberExceptionSink>();
    })
    .Build();

await host.StartAsync();

var mediator = host.Services.GetRequiredService<IMediator>();

// Switch to LogAndContinue for the error-handling demo below: the demo's job is
// to showcase the HandlerError event as the primary observation channel, so we
// want each handler exception to surface through HandlerError without being
// rethrown as AggregateException at the end of each Publish call. The default
// policy is ContinueAndAggregate; see ADR-006 for the cross-policy comparison.
mediator.ErrorPolicy = ErrorPolicy.LogAndContinue;

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

System.Console.WriteLine("=== Happy path ===");
System.Console.WriteLine();

// ── 1. Dog sighting ──────────────────────────────────────────
System.Console.WriteLine("--- Sighting 1: Dog at Central Park ---");
mediator.Publish(new AnimalSightedNotification(
    new Dog("Rex", 5, "German Shepherd"), "Central Park"));
System.Console.WriteLine();

// ── 2. Cat sighting ──────────────────────────────────────────
System.Console.WriteLine("--- Sighting 2: Cat at Rooftop Garden ---");
mediator.Publish(new AnimalSightedNotification(
    new Cat("Whiskers", 7, true), "Rooftop Garden"));
System.Console.WriteLine();

// ── 3. Eagle sighting ────────────────────────────────────────
System.Console.WriteLine("--- Sighting 3: Eagle at Mountain Ridge ---");
mediator.Publish(new AnimalSightedNotification(
    new Eagle("Aquila", 12, 2.1), "Mountain Ridge"));

// ──────────────────────────────────────────────────────────────
// v2.2 unified error handling demonstration.
//
// HandlerError is a single event surfaced on IMediator that fires
// whenever any handler throws — regardless of whether the handler
// reached the dispatcher via DI (INotificationHandler<T>) or via
// the Subscribe<T> callback path. HandlerErrorEventArgs carries
// the originating exception, the message, and (new in v2.2) the
// concrete HandlerType and HandlerInstance for both paths.
//
// ADR-005 also defines a containment contract: if a HandlerError
// subscriber itself throws, the mediator does not propagate that
// exception. It is routed to the configured ISubscriberExceptionSink
// so dispatch keeps running for the remaining subscribers.
// ──────────────────────────────────────────────────────────────

EventHandler<HandlerErrorEventArgs> diPathSubscriber = (_, args) =>
{
    System.Console.WriteLine("  HandlerError fired:");
    System.Console.WriteLine("    HandlerType:     " + (args.HandlerType?.Name ?? "(null)"));
    System.Console.WriteLine("    HandlerInstance: " + (args.HandlerInstance?.GetType().Name ?? "(null)"));
    System.Console.WriteLine("    Exception:       " + args.Exception.GetType().Name + ": " + args.Exception.Message);
};

EventHandler<HandlerErrorEventArgs> callbackPathSubscriber = (_, args) =>
{
    System.Console.WriteLine("  HandlerError fired:");
    System.Console.WriteLine("    HandlerType:     " + (args.HandlerType?.Name ?? "(null)"));
    System.Console.WriteLine("    HandlerInstance: " + (args.HandlerInstance?.GetType().Name ?? "(null)"));
    System.Console.WriteLine("    Exception:       " + args.Exception.GetType().Name + ": " + args.Exception.Message);
};

System.Console.WriteLine();
System.Console.WriteLine("=== Error handling: DI path ===");
mediator.HandlerError += diPathSubscriber;
// IPublisher.Publish (Task-returning) dispatches to DI-resolved INotificationHandler<T>
// instances; this is the path ThrowingDiHandler reaches.
await mediator.Publish(new ProblematicSightingNotification(
    "a sighting that throws via the DI handler"), CancellationToken.None);
mediator.HandlerError -= diPathSubscriber;

var throwingCallbackHandler = new ThrowingCallbackHandler();
var callbackSubscription = mediator.Subscribe<ProblematicSightingNotification>(
    throwingCallbackHandler.Handle, weak: false);

System.Console.WriteLine();
System.Console.WriteLine("=== Error handling: Callback path ===");
mediator.HandlerError += callbackPathSubscriber;
// IMediator.Publish<T>(T) (sync, returns bool) dispatches to Subscribe<T>(Action<T>)
// callbacks; this is the path ThrowingCallbackHandler reaches.
mediator.Publish(new ProblematicSightingNotification(
    "a sighting that throws via the callback handler"));
mediator.HandlerError -= callbackPathSubscriber;
callbackSubscription.Dispose();

EventHandler<HandlerErrorEventArgs> throwingErrorSubscriber = (_, _) =>
{
    throw new InvalidOperationException("This HandlerError subscriber deliberately throws.");
};

System.Console.WriteLine();
System.Console.WriteLine("=== Error handling: Subscriber containment ===");
mediator.HandlerError += throwingErrorSubscriber;
// Reuse the DI dispatch path so ThrowingDiHandler fires HandlerError, which then
// invokes throwingErrorSubscriber; its exception is contained and routed to the
// configured ConsoleSubscriberExceptionSink.
await mediator.Publish(new ProblematicSightingNotification(
    "a sighting that triggers a throwing error subscriber"), CancellationToken.None);
mediator.HandlerError -= throwingErrorSubscriber;

System.Console.WriteLine();
System.Console.WriteLine("(The mediator did not crash. The throwing subscriber's exception was routed");
System.Console.WriteLine(" to ConsoleSubscriberExceptionSink, which printed it above.)");

System.Console.WriteLine();
System.Console.WriteLine("Press Enter to exit.");
System.Console.ReadLine();

await host.StopAsync();
