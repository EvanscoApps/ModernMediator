# ModernMediator

A modern, feature-rich mediator library for .NET 8 that combines the best of pub/sub and request/response patterns with advanced features for real-world applications.

## Features

### Core Patterns
- **Pub/Sub (Notifications)** - Fire-and-forget event broadcasting
- **Request/Response** - Send requests and receive responses *(coming soon)*
- **Streaming** - IAsyncEnumerable support *(coming soon)*

### Advanced Capabilities
- **Weak References** - Handlers can be garbage collected, preventing memory leaks
- **Strong References** - Opt-in for handlers that must persist
- **Runtime Subscribe/Unsubscribe** - Dynamic handler registration (perfect for plugins)
- **Predicate Filters** - Filter messages at subscription time
- **Covariance** - Subscribe to base types, receive derived messages
- **String Key Routing** - Topic-based subscriptions alongside type-based

### Async-First Design
- **True Async Handlers** - `SubscribeAsync` with proper `Task.WhenAll` aggregation
- **Cancellation Support** - All async operations respect `CancellationToken`
- **Parallel Execution** - Notifications execute handlers concurrently

### Error Handling
- **Three Policies** - `ContinueAndAggregate`, `StopOnFirstError`, `LogAndContinue`
- **HandlerError Event** - Hook for logging and monitoring
- **Exception Unwrapping** - Clean stack traces without reflection noise

### UI Thread Support
- **Built-in Dispatchers** - WPF, WinForms, MAUI, ASP.NET Core
- **SubscribeOnMainThread** - Automatic UI thread marshalling

### Modern .NET Integration
- **Dependency Injection** - `services.AddModernMediator()`
- **Multi-target** - `net8.0` and `net8.0-windows`
- **Interface-first** - `IMediator` for testability and mocking
- **Pipeline Behaviors** - Cross-cutting concerns *(coming soon)*

## Installation

```bash
dotnet add package ModernMediator
```

## Quick Start

### Setup with Dependency Injection (Recommended)

```csharp
// Program.cs
services.AddModernMediator();

// Or with configuration
services.AddModernMediator(mediator =>
{
    mediator.ErrorPolicy = ErrorPolicy.LogAndContinue;
    mediator.SetDispatcher(new WpfDispatcher());
});
```

### Setup without DI

```csharp
// Singleton (shared instance)
IMediator mediator = Mediator.Instance;

// Or create isolated instance
IMediator mediator = Mediator.Create();
```

## Usage

### Pub/Sub (Notifications)

```csharp
// Define a message
public record OrderCreatedEvent(int OrderId, decimal Total);

// Subscribe
mediator.Subscribe<OrderCreatedEvent>(e => 
    Console.WriteLine($"Order {e.OrderId} created: ${e.Total}"));

// Subscribe with filter
mediator.Subscribe<OrderCreatedEvent>(
    e => NotifyVipTeam(e),
    filter: e => e.Total > 10000);

// Publish
mediator.Publish(new OrderCreatedEvent(123, 599.99m));
```

### Async Handlers

```csharp
// Subscribe async
mediator.SubscribeAsync<OrderCreatedEvent>(async e =>
{
    await SaveToDatabase(e);
    await SendEmailNotification(e);
});

// Publish and await all handlers
await mediator.PublishAsyncTrue(new OrderCreatedEvent(123, 599.99m));
```

### Covariance (Polymorphic Dispatch)

```csharp
public record AnimalEvent(string Name);
public record DogEvent(string Name, string Breed) : AnimalEvent(Name);
public record CatEvent(string Name, int LivesRemaining) : AnimalEvent(Name);

// This handler receives ALL animal events
mediator.Subscribe<AnimalEvent>(e => Console.WriteLine($"Animal: {e.Name}"));

// These are also delivered to the AnimalEvent handler
mediator.Publish(new DogEvent("Rex", "German Shepherd"));
mediator.Publish(new CatEvent("Whiskers", 9));
```

### String Key Routing

```csharp
// Subscribe to specific topics
mediator.Subscribe<OrderEvent>("orders.created", e => HandleNewOrder(e));
mediator.Subscribe<OrderEvent>("orders.shipped", e => HandleShippedOrder(e));
mediator.Subscribe<OrderEvent>("orders.cancelled", e => HandleCancelledOrder(e));

// Publish to specific topic
mediator.Publish("orders.created", new OrderEvent(...));
```

### Weak vs Strong References

```csharp
// Weak reference (default) - handler can be GC'd
mediator.Subscribe<Event>(handler.OnEvent, weak: true);

// Strong reference - handler persists until unsubscribed
mediator.Subscribe<Event>(handler.OnEvent, weak: false);
```

### Unsubscribing

```csharp
// Subscribe returns a disposable token
var subscription = mediator.Subscribe<Event>(OnEvent);

// Unsubscribe when done
subscription.Dispose();

// Or use with 'using' for scoped subscriptions
using (mediator.Subscribe<Event>(OnEvent))
{
    // Handler is active here
}
// Handler is automatically unsubscribed
```

### UI Thread Dispatching

```csharp
// Set dispatcher once at startup
mediator.SetDispatcher(new WpfDispatcher());

// Subscribe to receive on UI thread
mediator.SubscribeOnMainThread<DataChangedEvent>(e => 
    UpdateUI(e)); // Safe to update UI here

// Async version
mediator.SubscribeAsyncOnMainThread<DataChangedEvent>(async e =>
{
    await ProcessData(e);
    UpdateUI(e); // Safe to update UI
});
```

### Error Handling

```csharp
// Set error policy
mediator.ErrorPolicy = ErrorPolicy.LogAndContinue;

// Subscribe to errors
mediator.HandlerError += (sender, args) =>
{
    logger.LogError(args.Exception, 
        "Handler error for {MessageType}", 
        args.MessageType.Name);
};
```

## Comparison with MediatR

| Feature | ModernMediator | MediatR |
|---------|---------------|---------|
| Request/Response | ✅ Coming soon | ✅ Yes |
| Notifications (Pub/Sub) | ✅ Yes | ✅ Yes |
| Pipeline Behaviors | ✅ Coming soon | ✅ Yes |
| Weak References | ✅ Yes | ❌ No |
| Runtime Subscribe/Unsubscribe | ✅ Yes | ❌ No |
| UI Thread Dispatch | ✅ Built-in | ❌ Manual |
| Covariance | ✅ Yes | ❌ No |
| Predicate Filters | ✅ Yes | ❌ No |
| String Key Routing | ✅ Yes | ❌ No |
| Parallel Notifications | ✅ Default | ❌ Sequential |
| Assembly Scanning | ✅ Coming soon | ✅ Yes |
| Streaming | ✅ Coming soon | ✅ Yes |

## Use Cases

### Plugin Systems
ModernMediator excels at plugin architectures where plugins load/unload at runtime:
- Weak references prevent memory leaks when plugins unload
- Runtime subscribe/unsubscribe for dynamic registration
- String key routing for topic-based communication

### Desktop Applications (WPF, WinForms, MAUI)
- Built-in UI thread dispatchers
- Memory-efficient with weak references
- Easy decoupling of components

### ASP.NET Core
- Full DI integration
- Request/response for CQRS patterns
- Pipeline behaviors for cross-cutting concerns

## License

MIT License - see [LICENSE](LICENSE) file.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.