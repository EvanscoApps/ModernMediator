# ModernMediator

A modern, feature-rich mediator library for .NET 8 that combines the best of pub/sub and request/response patterns with advanced features for real-world applications.

## Features

### Core Patterns
- **Request/Response** - Send requests and receive typed responses
- **Streaming** - `IAsyncEnumerable` support for large datasets
- **Pub/Sub (Notifications)** - Fire-and-forget event broadcasting

### Pipeline
- **Pipeline Behaviors** - Wrap handler execution for cross-cutting concerns
- **Pre-Processors** - Run logic before handlers execute
- **Post-Processors** - Run logic after handlers complete

### Source Generators & AOT
- **Source Generators** - Compile-time code generation eliminates reflection
- **Native AOT Compatible** - Full support for ahead-of-time compilation
- **Compile-Time Diagnostics** - Catch missing handlers during build, not runtime
- **Zero Reflection** - Generated `AddModernMediatorGenerated()` for maximum performance

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
- **Assembly Scanning** - Auto-discover handlers, behaviors, and processors
- **Multi-target** - `net8.0` and `net8.0-windows`
- **Interface-first** - `IMediator` for testability and mocking

## Installation

```bash
dotnet add package ModernMediator
```

## Quick Start

### Setup with Dependency Injection (Recommended)

```csharp
// Program.cs - with assembly scanning (uses reflection)
services.AddModernMediator(config =>
{
    config.RegisterServicesFromAssemblyContaining<Program>();
});

// Or use source-generated registration (AOT-compatible, no reflection)
services.AddModernMediatorGenerated();

// Or with configuration
services.AddModernMediator(config =>
{
    config.RegisterServicesFromAssemblyContaining<Program>();
    config.ErrorPolicy = ErrorPolicy.LogAndContinue;
    config.Configure(m => m.SetDispatcher(new WpfDispatcher()));
});
```

### Setup without DI

```csharp
// Singleton (shared instance)
IMediator mediator = Mediator.Instance;

// Or create isolated instance
IMediator mediator = Mediator.Create();
```

## Source Generators

ModernMediator includes a source generator that discovers handlers at compile time and generates registration code. This provides:

- **Zero reflection at runtime** - All handler discovery happens during compilation
- **Native AOT support** - Works with ahead-of-time compilation
- **Faster startup** - No assembly scanning at runtime
- **Compile-time diagnostics** - Missing or duplicate handlers detected during build

### Generated Code

The source generator creates two files:

**`ModernMediator.Generated.g.cs`** - DI registration without reflection:
```csharp
// Auto-generated - use instead of assembly scanning
services.AddModernMediatorGenerated();
```

**`ModernMediator.SendExtensions.g.cs`** - Strongly-typed Send methods:
```csharp
// Generated extension methods bypass reflection entirely
var user = await mediator.Send(new GetUserQuery(42)); // No reflection!
```

### Diagnostics

| Code | Description |
|------|-------------|
| MM001 | Duplicate handler - multiple handlers for same request type |
| MM002 | No handler found - request type has no registered handler |
| MM003 | Abstract handler - handler class cannot be abstract |

## Usage

### Request/Response

```csharp
// Define request and response
public record GetUserQuery(int UserId) : IRequest<UserDto>;
public record UserDto(int Id, string Name, string Email);

// Define handler
public class GetUserHandler : IRequestHandler<GetUserQuery, UserDto>
{
    public async Task<UserDto> Handle(GetUserQuery request, CancellationToken ct = default)
    {
        var user = await _db.Users.FindAsync(request.UserId, ct);
        return new UserDto(user.Id, user.Name, user.Email);
    }
}

// Send request
var user = await mediator.Send(new GetUserQuery(42));
```

### Commands (No Return Value)

```csharp
// Define command
public record CreateUserCommand(string Name, string Email) : IRequest;

// Define handler
public class CreateUserHandler : IRequestHandler<CreateUserCommand, Unit>
{
    public async Task<Unit> Handle(CreateUserCommand request, CancellationToken ct = default)
    {
        await _db.Users.AddAsync(new User(request.Name, request.Email), ct);
        await _db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

// Send command
await mediator.Send(new CreateUserCommand("John", "john@example.com"));
```

### Streaming

```csharp
// Define stream request
public record GetAllUsersRequest(int PageSize) : IStreamRequest<UserDto>;

// Define stream handler
public class GetAllUsersHandler : IStreamRequestHandler<GetAllUsersRequest, UserDto>
{
    public async IAsyncEnumerable<UserDto> Handle(
        GetAllUsersRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var user in _db.Users.AsAsyncEnumerable().WithCancellation(ct))
        {
            yield return new UserDto(user.Id, user.Name, user.Email);
        }
    }
}

// Consume stream
await foreach (var user in mediator.CreateStream(new GetAllUsersRequest(100), ct))
{
    Console.WriteLine(user.Name);
}
```

### Pipeline Behaviors

```csharp
// Logging behavior that wraps all requests
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request, 
        RequestHandlerDelegate<TResponse> next, 
        CancellationToken ct)
    {
        _logger.LogInformation("Handling {Request}", typeof(TRequest).Name);
        var response = await next();
        _logger.LogInformation("Handled {Request}", typeof(TRequest).Name);
        return response;
    }
}

// Validation behavior
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request, 
        RequestHandlerDelegate<TResponse> next, 
        CancellationToken ct)
    {
        var errors = await _validator.ValidateAsync(request, ct);
        if (errors.Any()) throw new ValidationException(errors);
        return await next();
    }
}

// Register behaviors
services.AddModernMediator(config =>
{
    config.RegisterServicesFromAssemblyContaining<Program>();
    config.AddBehavior<LoggingBehavior>();
    config.AddBehavior<ValidationBehavior>();
});
```

### Pre/Post Processors

```csharp
// Pre-processor runs before handler
public class AuthorizationPreProcessor<TRequest> : IRequestPreProcessor<TRequest>
{
    public Task Process(TRequest request, CancellationToken ct)
    {
        if (!_auth.IsAuthorized(request))
            throw new UnauthorizedException();
        return Task.CompletedTask;
    }
}

// Post-processor runs after handler
public class CachingPostProcessor<TRequest, TResponse> : IRequestPostProcessor<TRequest, TResponse>
{
    public Task Process(TRequest request, TResponse response, CancellationToken ct)
    {
        _cache.Set(request, response);
        return Task.CompletedTask;
    }
}

// Register processors
services.AddModernMediator(config =>
{
    config.RegisterServicesFromAssemblyContaining<Program>();
    config.AddRequestPreProcessor<AuthorizationPreProcessor>();
    config.AddRequestPostProcessor<CachingPostProcessor>();
});
```

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

## Comparisons

### ModernMediator vs MediatR

| Feature | ModernMediator | MediatR |
|---------|---------------|---------|
| Request/Response | ✅ Yes | ✅ Yes |
| Notifications (Pub/Sub) | ✅ Yes | ✅ Yes |
| Pipeline Behaviors | ✅ Yes | ✅ Yes |
| Streaming | ✅ Yes | ✅ Yes |
| Assembly Scanning | ✅ Yes | ✅ Yes |
| Source Generators | ✅ Yes | ❌ No |
| Native AOT | ✅ Yes | ❌ No |
| Weak References | ✅ Yes | ❌ No |
| Runtime Subscribe/Unsubscribe | ✅ Yes | ❌ No |
| UI Thread Dispatch | ✅ Built-in | ❌ Manual |
| Covariance | ✅ Yes | ❌ No |
| Predicate Filters | ✅ Yes | ❌ No |
| String Key Routing | ✅ Yes | ❌ No |
| Parallel Notifications | ✅ Default | ❌ Sequential |
| MIT License | ✅ Yes | ❌ Commercial* |

*MediatR moved to commercial licensing in July 2025

### ModernMediator vs Prism EventAggregator

For desktop developers using Prism, ModernMediator can replace EventAggregator while adding MediatR-style request/response:

| Feature | Prism EventAggregator | ModernMediator |
|---------|----------------------|----------------|
| Pub/Sub | ✅ `PubSubEvent<T>` | ✅ `Publish<T>` / `Subscribe<T>` |
| Weak References | ✅ `keepSubscriberReferenceAlive: false` | ✅ `weak: true` (default) |
| Strong References | ✅ `keepSubscriberReferenceAlive: true` | ✅ `weak: false` |
| Filter Subscriptions | ✅ `.Subscribe(handler, filter)` | ✅ `filter: predicate` |
| UI Thread | ✅ `ThreadOption.UIThread` | ✅ `SubscribeOnMainThread` |
| Background Thread | ✅ `ThreadOption.BackgroundThread` | ✅ `PublishAsync` |
| Unsubscribe | ✅ `SubscriptionToken` | ✅ `IDisposable` |
| Request/Response | ❌ No | ✅ `Send<TResponse>` |
| Pipeline Behaviors | ❌ No | ✅ Yes |
| Streaming | ❌ No | ✅ `CreateStream` |
| Source Generators | ❌ No | ✅ Yes |
| Native AOT | ❌ No | ✅ Yes |

**Bottom line:** If you're using Prism EventAggregator for pub/sub AND MediatR for CQRS, ModernMediator replaces both with one library.

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
- Replaces both EventAggregator and MediatR

### ASP.NET Core
- Full DI integration
- Request/response for CQRS patterns
- Pipeline behaviors for cross-cutting concerns

### Large Dataset Processing
- Streaming with `IAsyncEnumerable` for memory efficiency
- Cancellation support for long-running operations
- Backpressure-friendly enumeration

### Serverless & Native AOT
- Source generators eliminate reflection overhead
- Fast cold start times
- Full Native AOT compatibility
- Compile-time handler discovery

## License

MIT License - see [LICENSE](LICENSE) file.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.