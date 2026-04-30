# ModernMediator

[![NuGet](https://img.shields.io/nuget/v/ModernMediator.svg)](https://www.nuget.org/packages/ModernMediator)

A modern, feature-rich mediator library for .NET 8 that combines the best of pub/sub and request/response patterns with advanced features for real-world applications. Zero reflection in hot paths, Native AOT compatible, compile-time diagnostics, and a ValueTask pipeline for zero-allocation dispatch.

## Status: Stable

Production-ready. Please report issues on GitHub.

## Documentation

📖 [Interactive Tutorial](https://evanscoapps.github.io/ModernMediator/ModernMediator-Tutorial.html)
📊 [Benchmarks](https://github.com/evanscoapps/ModernMediator/blob/main/BENCHMARKS.md)

## Do You Need a Mediator?

Some developers argue that the mediator pattern is unnecessary indirection. They're not wrong — if all you're doing is routing a request to a single handler with no cross-cutting concerns, a mediator adds complexity without value. You can inject a service directly, call a method on it, and it works fine.

The pattern earns its keep in two situations.

The first is pipeline behaviors. When every command needs validation, logging, telemetry, timeout enforcement, and eventually caching and retry — and you want those concerns applied consistently without every handler author remembering to wire them up manually — a mediator stops being ceremony and starts being infrastructure. The alternative is decorators or hand-rolled middleware, which either requires manual wiring per handler or introduces the same registration complexity the mediator solves more cleanly.

The second is plugin architectures. When you need to discover and dispatch to handlers that don't exist at compile time in the host application, the mediator pattern isn't a convenience — it's the natural solution. Runtime subscribe/unsubscribe, weak references for handler lifecycle management, and string key routing all support this use case.

If your project doesn't benefit from either of these, don't use a mediator. If it does, ModernMediator is designed to make that choice pay off.

## When Do You Need Pipeline Behaviors?

When you have logic that applies across many handlers and you don't want to repeat it inside each one.

**Validation** is the simplest example. Every command that accepts user input needs validation. Without a pipeline behavior, every handler starts with the same boilerplate — check the input, throw if it's bad, then do the actual work. Multiply that across fifty handlers and you have fifty places to forget, fifty places to get wrong, and fifty places to update when your validation strategy changes. A `ValidationBehavior` runs before every handler automatically. The handler author writes a FluentValidation validator, registers it, and never thinks about the plumbing.

**Logging** is the same story. You want to know that a request entered the pipeline, how long it took, and whether it succeeded or failed. Without a behavior, you're scattering logger calls across every handler. With a `LoggingBehavior`, it's applied uniformly and the handler code stays focused on business logic.

Then it cascades: **telemetry** — you want `ActivitySource` traces and duration metrics on every dispatch without instrumenting each handler individually. **Timeout enforcement** — you want a hard ceiling on handler execution time, applied via a `[Timeout]` attribute rather than each handler managing its own `CancellationTokenSource`. **Authorization** — you want policy checks before the handler even runs, not buried inside it.

The pattern is always the same: a concern that is orthogonal to the handler's purpose, that applies to many or all handlers, and that you want enforced consistently rather than relying on each developer to remember to include it. One behavior class, registered once, applied everywhere.

ModernMediator ships built-in behaviors for validation (via FluentValidation), logging, telemetry, and timeout enforcement. You don't need to write these yourself.

## Features

### Core Patterns
- **Request/Response** — Send requests and receive typed responses
- **Streaming** — `IAsyncEnumerable` support for large datasets
- **Pub/Sub (Notifications)** — DI-based notification dispatch via `IPublisher`
- **Pub/Sub with Callbacks** — Collect responses from multiple subscribers
- **Result\<T\> Pattern** — `readonly struct` with implicit conversions, `Map`, and `GetValueOrDefault` for railway-oriented error handling

### Pipeline
- **Pipeline Behaviors** — Wrap handler execution for cross-cutting concerns
- **Pre-Processors** — Run logic before handlers execute
- **Post-Processors** — Run logic after handlers complete
- **Exception Handlers** — Clean, typed exception handling separate from business logic
- **Built-in LoggingBehavior** — Request/response logging with configurable levels via `AddLogging()`
- **Built-in TimeoutBehavior** — Per-request timeout via `[Timeout(ms)]` attribute and `AddTimeout()`
- **Built-in ValidationBehavior** — FluentValidation integration via `ModernMediator.FluentValidation`
- **Built-in AuditBehavior** — per-request audit recording (type, user, trace ID, duration, outcome) dispatched to any `IAuditWriter`; opt out with `[NoAudit]`; registered via `AddAudit()`
- **Built-in IdempotencyBehavior** — deduplicates requests marked `[Idempotent]` by key and TTL using any `IIdempotencyStore`; registered via `AddIdempotency()`
- **Built-in CircuitBreakerBehavior** — per-request-type circuit breaker via `[CircuitBreaker]` attribute; open circuit throws `CircuitBreakerOpenException`; registered via `AddCircuitBreaker()`
- **Built-in RetryBehavior** — automatic retry with configurable count and delay strategy (None, Fixed, Linear, Exponential) via `[Retry]` attribute; registered via `AddRetry()`

### Source Generators & AOT
- **Source Generators** — Compile-time code generation eliminates reflection
- **Native AOT Compatible** — Full support for ahead-of-time compilation
- **Compile-Time Diagnostics** — 11 compile-time diagnostic rules (MM001–MM009, MM100, MM200) catch problems during build, plus the runtime MM201 prefix surfaced on dispatcher overload mismatch exceptions
- **Zero Reflection** — Generated `AddModernMediatorGenerated()` for maximum performance
- **CachingMode** — Eager (default) or Lazy initialization for cold start optimization
- **ASP.NET Core Endpoint Generation** — `[Endpoint]` attribute with `MapMediatorEndpoints()` for Minimal API integration

### Performance
- **ValueTask Pipeline** — `IValueTaskRequestHandler` and `ISender.SendAsync` for zero-allocation dispatch
- **Closure Elimination** — `RequestHandlerDelegate<TRequest, TResponse>` passes request and token explicitly
- **Lower allocations than MediatR** on every benchmark — see 📊 [Benchmarks](https://github.com/evanscoapps/ModernMediator/blob/main/BENCHMARKS.md)
- **4x faster cold start** than MediatR via source-generated registration

### Observability
- **OpenTelemetry Integration** — `ActivitySource` and `Meter` with `RequestCounter` and `RequestDuration` via `AddTelemetry()`

### Interface Segregation
- **ISender** — Request/response dispatch
- **IPublisher** — Notification publishing
- **IStreamer** — Streaming dispatch
- **IMediator** — Composes all three; all registered in DI as forwarding aliases

### Advanced Capabilities
- **Weak References** — Handlers can be garbage collected, preventing memory leaks
- **Strong References** — Opt-in for handlers that must persist
- **Runtime Subscribe/Unsubscribe** — Dynamic handler registration (perfect for plugins)
- **Predicate Filters** — Filter messages at subscription time
- **Covariance** — Subscribe to base types, receive derived messages
- **String Key Routing** — Topic-based subscriptions alongside type-based
- **ICurrentUserAccessor** — abstraction for resolving current user identity in pipeline behaviors; `HttpContextCurrentUserAccessor` (in `ModernMediator.AspNetCore`) resolves `UserId` and `UserName` from `IHttpContextAccessor`

### Async-First Design
- **True Async Handlers** — `SubscribeAsync` with proper `Task.WhenAll` aggregation
- **Cancellation Support** — All async operations respect `CancellationToken`
- **Parallel Execution** — Notifications execute handlers concurrently

### Error Policies
- **Three Policies** — `ContinueAndAggregate`, `StopOnFirstError`, `LogAndContinue`
- **HandlerError Event** — Hook for logging and monitoring
- **Exception Unwrapping** — Clean stack traces without reflection noise

### UI Thread Support
- **Built-in Dispatchers** — WPF, WinForms, MAUI, ASP.NET Core, Avalonia
- **SubscribeOnMainThread** — Automatic UI thread marshalling

### Modern .NET Integration
- **Dependency Injection** — `services.AddModernMediator()`
- **Assembly Scanning** — Auto-discover handlers, behaviors, and processors
- **Multi-target** — `net8.0` and `net8.0-windows`
- **Interface-first** — `IMediator` for testability and mocking

## Installation

```bash
dotnet add package ModernMediator
```

Optional packages:

```bash
dotnet add package ModernMediator.FluentValidation
dotnet add package ModernMediator.AspNetCore
dotnet add package ModernMediator.Audit.Serilog
dotnet add package ModernMediator.Audit.EntityFramework
dotnet add package ModernMediator.Idempotency.EntityFramework
```

## Quick Start

### Setup with Dependency Injection (Recommended)

`IMediator` is registered as **Scoped** by default, allowing handlers to resolve scoped dependencies like `DbContext`.

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
// Singleton (shared instance) - ideal for Pub/Sub across the application
IMediator mediator = Mediator.Instance;

// Or create isolated instance
IMediator mediator = Mediator.Create();
```

## Source Generators

ModernMediator includes a source generator that discovers handlers at compile time and generates registration code. This provides:

- **Zero reflection at runtime** — All handler discovery happens during compilation
- **Native AOT support** — Works with ahead-of-time compilation
- **Faster startup** — No assembly scanning at runtime
- **Compile-time diagnostics** — Missing or duplicate handlers detected during build

### Generated Code

The source generator creates two files:

**`ModernMediator.Generated.g.cs`** — DI registration without reflection:
```csharp
// Auto-generated - use instead of assembly scanning
services.AddModernMediatorGenerated();

// With configuration (ErrorPolicy, CachingMode, Dispatcher)
services.AddModernMediatorGenerated(config =>
{
    config.ErrorPolicy = ErrorPolicy.LogAndContinue;
    config.CachingMode = CachingMode.Lazy;
    config.Configure(m => m.SetDispatcher(new WpfDispatcher()));
});
```

**`ModernMediator.SendExtensions.g.cs`** — Strongly-typed Send methods:
```csharp
// Generated extension methods bypass reflection entirely
var user = await mediator.Send(new GetUserQuery(42)); // No reflection!
```

### Diagnostics

Compile-time codes are emitted by the source generators and surface in the IDE error list and build output. The single runtime code (MM201) is not a Roslyn diagnostic; it is a bracketed prefix on an `InvalidOperationException` message thrown by the dispatcher.

| Code  | Channel       | Description                                                           |
| :---- | :------------ | :-------------------------------------------------------------------- |
| MM001 | Compile-time  | Duplicate handler — multiple handlers for same request                |
| MM002 | Compile-time  | No handler found — request type has no registered handler             |
| MM003 | Compile-time  | Abstract handler — handler class cannot be abstract                   |
| MM004 | Compile-time  | Handler in wrong assembly — handler not in scanned assembly           |
| MM005 | Compile-time  | Missing cancellation token — handler should accept CancellationToken  |
| MM006 | Compile-time  | Non-public handler — handler class is not public                      |
| MM007 | Compile-time  | Handler implements multiple handler interfaces                        |
| MM008 | Compile-time  | Lambda with weak reference subscription                               |
| MM009 | Compile-time  | Dispatcher overload mismatch — `Send` called for a request whose handler is registered as `IValueTaskRequestHandler` (or vice versa). Detected by the analyzer in the same compilation; cross-assembly cases fall back to MM201 |
| MM100 | Compile-time  | Source generator internal error                                       |
| MM200 | Compile-time  | Invalid HTTP method on `[Endpoint]` attribute (ASP.NET Core endpoint generator) |
| MM201 | Runtime       | Dispatcher overload mismatch — `InvalidOperationException` thrown with `[MM201]` prefix when the dispatcher detects a Send/SendAsync mismatch at runtime (cross-assembly safety net for MM009) |

### CachingMode

Control when handler wrappers and lookups are initialized:

```csharp
services.AddModernMediator(config =>
{
    // Eager (default) - initialize everything on first mediator access
    // Best for long-running applications where startup cost is amortized
    config.CachingMode = CachingMode.Eager;
    
    // Lazy - initialize handlers on-demand as messages are processed
    // Best for serverless, Native AOT, or cold start scenarios
    config.CachingMode = CachingMode.Lazy;
});
```

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

### ValueTask Handlers (Zero-Allocation Path)

For performance-critical paths, use `IValueTaskRequestHandler` with `SendAsync` to avoid the `Task` allocation:

```csharp
// Define a ValueTask handler
public class GetCachedUserHandler : IValueTaskRequestHandler<GetUserQuery, UserDto>
{
    public ValueTask<UserDto> Handle(GetUserQuery request, CancellationToken ct = default)
    {
        if (_cache.TryGet(request.UserId, out var cached))
            return ValueTask.FromResult(cached);  // Zero allocation

        return new ValueTask<UserDto>(LoadFromDbAsync(request, ct));
    }
}

// Dispatch via the zero-allocation path
var user = await sender.SendAsync<UserDto>(new GetUserQuery(42));
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

Pipeline behaviors wrap handler execution for cross-cutting concerns like logging, validation, and transactions.

#### Built-in Behaviors

ModernMediator ships behaviors for common cross-cutting concerns:

```csharp
services.AddModernMediator(config =>
{
    config.RegisterServicesFromAssemblyContaining<Program>();

    // Built-in logging with configurable levels
    config.AddLogging();

    // Per-request timeout enforcement via [Timeout(ms)] attribute
    config.AddTimeout();

    // OpenTelemetry traces and metrics
    config.AddTelemetry();

    // Audit recording — pluggable IAuditWriter, opt out with [NoAudit]
    config.AddAudit();

    // Request deduplication — pluggable IIdempotencyStore, opt in with [Idempotent]
    config.AddIdempotency();

    // Per-request-type circuit breaker via [CircuitBreaker] attribute
    config.AddCircuitBreaker();

    // Automatic retry with configurable delay strategy via [Retry] attribute
    config.AddRetry();
});

// FluentValidation integration (separate package)
services.AddModernMediatorValidation();
```

#### Custom Open Generic Behaviors (Apply to All Requests)

Open generic behaviors must be registered explicitly with `AddOpenBehavior()`:

```csharp
// Transaction behavior that wraps all requests
public class TransactionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request, 
        RequestHandlerDelegate<TRequest, TResponse> next, 
        CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var response = await next(request, ct);
        await tx.CommitAsync(ct);
        return response;
    }
}

// Register open generic behaviors explicitly
services.AddModernMediator(config =>
{
    config.RegisterServicesFromAssemblyContaining<Program>();
    config.AddOpenBehavior(typeof(TransactionBehavior<,>));
});
```

#### Recommended Registration Order

Behaviors execute in registration order (first registered = outermost). For the built-in
behaviors, register them in this sequence so each layer wraps the one inside it correctly:

| Order | Behavior              | Registration              | Reason                                              |
| :---: | :-------------------- | :------------------------ | :-------------------------------------------------- |
| 1     | RetryBehavior         | `AddRetry()`              | Outermost — retries the entire inner pipeline       |
| 2     | CircuitBreakerBehavior| `AddCircuitBreaker()`     | Fails fast before attempting work                   |
| 3     | TimeoutBehavior       | `AddTimeout()`            | Enforces ceiling on each attempt                    |
| 4     | AuditBehavior         | `AddAudit()`              | Records outcome of each attempt                     |
| 5     | IdempotencyBehavior   | `AddIdempotency()`        | Short-circuits before validation if already handled |
| 6     | LoggingBehavior       | `AddLogging()`            | Logs the request entering the inner pipeline        |
| 7     | ValidationBehavior    | `AddOpenBehavior(typeof(ValidationBehavior<,>))` | Rejects invalid requests before handler |
| 8     | Handler               | —                         | Executes the business logic                         |

You are not required to register all behaviors — only the ones your application needs.

> **Note:** Assembly scanning skips open generic types. Always use `AddOpenBehavior()` for behaviors that apply to all request types.

#### Closed Generic Behaviors (Apply to Specific Requests)

Behaviors for specific request types are discovered by assembly scanning:

```csharp
// Behavior for a specific request type
public class GetUserCachingBehavior : IPipelineBehavior<GetUserQuery, UserDto>
{
    public async Task<UserDto> Handle(
        GetUserQuery request, 
        RequestHandlerDelegate<GetUserQuery, UserDto> next, 
        CancellationToken ct)
    {
        if (_cache.TryGet(request.UserId, out var cached))
            return cached;
        
        var result = await next(request, ct);
        _cache.Set(request.UserId, result);
        return result;
    }
}
```

### Result\<T\> Pattern

For operations that can fail without exceptions:

```csharp
public class CreateOrderHandler : IRequestHandler<CreateOrderCommand, Result<OrderDto>>
{
    public async Task<Result<OrderDto>> Handle(CreateOrderCommand request, CancellationToken ct)
    {
        if (request.Items.Count == 0)
            return new ResultError("Order must have at least one item");

        var order = await _db.CreateOrder(request, ct);
        return new OrderDto(order.Id, order.Total);  // Implicit conversion
    }
}

// Consuming results
var result = await mediator.Send(new CreateOrderCommand(items));
var dto = result.GetValueOrDefault(fallback);
var mapped = result.Map(order => order.Total);
```

### ASP.NET Core Endpoint Generation

Generate Minimal API endpoints directly from request handlers:

```csharp
[Endpoint(HttpMethod.Post, "/api/users")]
public record CreateUserCommand(string Name, string Email) : IRequest<UserDto>;

// In Program.cs
app.MapMediatorEndpoints();
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

### Exception Handlers

Exception handlers provide clean, typed exception handling separate from your business logic. They can return an alternate response or let the exception bubble up.

```csharp
// Define an exception handler for a specific exception type
public class NotFoundExceptionHandler : RequestExceptionHandler<GetUserQuery, UserDto, NotFoundException>
{
    protected override Task<ExceptionHandlingResult<UserDto>> Handle(
        GetUserQuery request,
        NotFoundException exception,
        CancellationToken ct)
    {
        // Return an alternate response
        return Handled(new UserDto(0, "Unknown User"));
        
        // Or let the exception bubble up
        // return NotHandled;
    }
}

// Register exception handlers
services.AddModernMediator(config =>
{
    config.RegisterServicesFromAssemblyContaining<Program>();
    config.AddExceptionHandler<NotFoundExceptionHandler>();
});
```

Exception handlers walk the exception type hierarchy, so a handler for `Exception` will catch all exceptions if no more specific handler is registered.

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

ModernMediator routes notifications along two delivery paths. The `IMediator.Publish<T>(T)` and `PublishAsync<T>(T)` overloads shown above invoke `Subscribe<T>` and `SubscribeAsync<T>` callbacks; the `IPublisher.Publish<TNotification>(notification, ct)` overload, shown in the next subsection, invokes DI-resolved `INotificationHandler<TNotification>` instances. The two paths are independent: a single publish call reaches one path, not both.

#### DI-Based Notifications

For CQRS-style notification handlers resolved from the DI container:

```csharp
public record OrderCreatedNotification(int OrderId) : INotification;

public class SendOrderEmailHandler : INotificationHandler<OrderCreatedNotification>
{
    public Task Handle(OrderCreatedNotification notification, CancellationToken ct)
    {
        // Send confirmation email
        return _emailService.SendOrderConfirmation(notification.OrderId, ct);
    }
}

// Publish through IPublisher (resolved from DI)
await publisher.Publish(new OrderCreatedNotification(123), ct);
```

Errors thrown by DI-resolved notification handlers participate in the unified `ErrorPolicy` and `HandlerError` story. See [Error Handling](#error-handling) below for the policy semantics, the event args properties, and the cancellation contract.

#### Pub/Sub and DI Scoping

When using dependency injection, `IMediator` is registered as Scoped. This means Pub/Sub subscriptions are per-scope:

```csharp
// Subscriptions on DI-injected IMediator are scoped to that request/scope
public class MyService
{
    public MyService(IMediator mediator)
    {
        // This subscription lives only as long as this scope
        mediator.Subscribe<SomeEvent>(HandleEvent);
    }
}

// For application-wide shared subscriptions, use the static singleton:
Mediator.Instance.Subscribe<OrderCreatedEvent>(e => GlobalHandler(e));
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

### Pub/Sub with Callbacks

Collect responses from multiple subscribers — perfect for confirmation dialogs, validation, or aggregating data from multiple sources:

```csharp
// Define message and response types
public record ConfirmationRequest(string Message);
public record ConfirmationResponse(bool Confirmed, string Source);

// Subscribe with a response
mediator.Subscribe<ConfirmationRequest, ConfirmationResponse>(
    request => new ConfirmationResponse(
        Confirmed: ShowDialog(request.Message),
        Source: "DialogService"),
    weak: false);

// Publish and collect all responses
var responses = mediator.Publish<ConfirmationRequest, ConfirmationResponse>(
    new ConfirmationRequest("Delete this item?"));

if (responses.Any(r => r.Confirmed))
{
    DeleteItem();
}
```

#### Async Callbacks

```csharp
// Multiple async validators
mediator.SubscribeAsync<ValidateRequest, ValidationResult>(
    async request => await ValidateLengthAsync(request));

mediator.SubscribeAsync<ValidateRequest, ValidationResult>(
    async request => await ValidateFormatAsync(request));

// Publish and await all responses
var results = await mediator.PublishAsync<ValidateRequest, ValidationResult>(
    new ValidateRequest("user input"));

var errors = results.Where(r => !r.IsValid).ToList();
```

#### Key Differences from Request/Response

| Pattern                | Handlers | Use Case                                    |
| :--------------------- | :------- | :------------------------------------------ |
| `Send<TResponse>`      | Exactly 1| CQRS commands/queries                       |
| `Publish<TMsg, TResp>` | 0 to N   | Collect responses from multiple subscribers |

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
mediator.SetDispatcher(new WpfDispatcher());      // WPF
mediator.SetDispatcher(new WinFormsDispatcher()); // WinForms
mediator.SetDispatcher(new MauiDispatcher());     // MAUI
mediator.SetDispatcher(new AvaloniaDispatcher()); // Avalonia (see below)

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

#### Avalonia Support

For Avalonia, copy the [`AvaloniaDispatcher.cs`](https://github.com/EvanscoApps/ModernMediator/blob/main/docs/AvaloniaDispatcher.cs) file into your project:

```csharp
// In your Avalonia App.axaml.cs or startup
mediator.SetDispatcher(new AvaloniaDispatcher());
```

> **Note:** The Avalonia dispatcher is community-tested. Please report any issues on GitHub.

### Error Handling

`ErrorPolicy` and the `HandlerError` event govern notification dispatch error handling on both paths uniformly: handlers registered as `INotificationHandler<T>` and resolved via DI, and runtime callbacks registered via `Subscribe<T>` or `SubscribeAsync<T>`. Configuring the policy or subscribing to the event affects both kinds of handler invocations identically.

`ErrorPolicy` is a property on `Mediator` (or set via the `MediatorConfiguration` callback in `AddModernMediator`) that takes one of three values:

- `ContinueAndAggregate` (default): all handlers run, exceptions are collected, and an `AggregateException` is thrown after dispatch completes.
- `LogAndContinue`: each handler exception is surfaced via `HandlerError` and dispatch continues to the remaining handlers without propagating.
- `StopOnFirstError`: the first handler exception fires `HandlerError` and then propagates from `Publish`. Remaining handlers are not invoked.

The `HandlerError` event is a universal observation channel. It fires for every handler exception under every policy, regardless of which dispatch path produced the exception. The policy governs propagation and aggregation; the event governs observation. Subscribers receive a `HandlerErrorEventArgs` with these properties:

- `Exception`: the unwrapped handler exception.
- `Message`: the published notification.
- `MessageType`: the runtime type of the notification.
- `HandlerType`: the concrete handler type. On the DI-resolved path, this is the resolved handler class (for example, `typeof(SendOrderEmailHandler)`). On the Subscribe-callback path, this is `Method.DeclaringType` with compiler-generated closure types unwrapped to the enclosing user type.
- `HandlerInstance`: the resolved DI handler instance on the DI-resolved path, or `Delegate.Target` on the Subscribe-callback path. Null for static delegate subscriptions.

```csharp
mediator.ErrorPolicy = ErrorPolicy.LogAndContinue;

mediator.HandlerError += (sender, args) =>
{
    logger.LogError(args.Exception,
        "Handler {HandlerType} failed for {MessageType}",
        args.HandlerType?.FullName,
        args.MessageType.Name);
};

// Both paths fire the same event with the same args shape:
mediator.Subscribe<OrderCreatedEvent>(e => throw new InvalidOperationException("from callback"));
await publisher.Publish(new OrderCreatedNotification(123)); // DI-resolved handlers also covered
```

Cooperative cancellation is treated as distinct from a handler fault. When the publish token's `IsCancellationRequested` is true and a handler throws `OperationCanceledException`, the event does not fire and the policy does not apply. The exception propagates from `Publish` unconditionally. This matches .NET conventions for cooperative cancellation across async APIs.

For consumers who want to route contained subscriber exceptions to a specific observability system rather than the default `ILogger` route, register an `ISubscriberExceptionSink` implementation in the service collection. ModernMediator uses the registered sink in preference to the built-in logging fallback. See ADR-005 in `docs/decisions/` for the full event semantics specification, and ADR-006 for the dispatch-path participation contract.

## Comparisons

### ModernMediator vs MediatR

MediatR v12.x is the last open-source release under the Apache 2.0 license. MediatR v13+ requires a commercial license from Lucky Penny Software. The comparison below reflects capabilities as of MediatR 12.x.

| Feature                        | ModernMediator        | MediatR 12.x (Apache 2.0) |
| :----------------------------- | :-------------------- | :------------------------- |
| Request/Response               | ✅ Yes                | ✅ Yes                     |
| Notifications (Pub/Sub)        | ✅ Yes                | ✅ Yes                     |
| Pub/Sub with Callbacks         | ✅ Yes                | ❌ No                      |
| Pipeline Behaviors             | ✅ Yes                | ✅ Yes                     |
| Streaming                      | ✅ Yes                | ✅ Yes                     |
| Assembly Scanning              | ✅ Yes                | ✅ Yes                     |
| Exception Handlers             | ✅ Yes                | ❌ No                      |
| Source Generators              | ✅ Yes                | ❌ No                      |
| Native AOT                     | ✅ Yes                | ❌ No                      |
| ValueTask Pipeline             | ✅ SendAsync          | ❌ No                      |
| Result\<T\> Pattern            | ✅ Built-in           | ❌ No                      |
| Built-in Logging Behavior      | ✅ AddLogging()       | ❌ No                      |
| Built-in Timeout Behavior      | ✅ AddTimeout()       | ❌ No                      |
| Built-in Validation Behavior   | ✅ FluentValidation   | ❌ No                      |
| OpenTelemetry Integration      | ✅ AddTelemetry()     | ❌ No                      |
| Built-in Audit Behavior        | ✅ AddAudit()         | ❌ No                      |
| Built-in Idempotency Behavior  | ✅ AddIdempotency()   | ❌ No                      |
| Built-in Circuit Breaker       | ✅ AddCircuitBreaker()| ❌ No                      |
| Built-in Retry Behavior        | ✅ AddRetry()         | ❌ No                      |
| Current User Accessor          | ✅ ICurrentUserAccessor| ❌ No                     |
| Endpoint Generation            | ✅ [Endpoint]         | ❌ No                      |
| ISender/IPublisher/IStreamer    | ✅ Segregated         | ✅ ISender only            |
| Weak References                | ✅ Yes                | ❌ No                      |
| Runtime Subscribe/Unsubscribe  | ✅ Yes                | ❌ No                      |
| UI Thread Dispatch             | ✅ Built-in           | ❌ Manual                  |
| Covariance                     | ✅ Yes                | ❌ No                      |
| Predicate Filters              | ✅ Yes                | ❌ No                      |
| String Key Routing             | ✅ Yes                | ❌ No                      |
| Parallel Notifications         | ✅ Default            | ❌ Sequential              |
| Compile-time Diagnostics       | ✅ 11 rules           | ❌ No                      |
| License                        | ✅ MIT                | Apache 2.0 (v13+ commercial) |

### Performance vs MediatR

ModernMediator allocates less memory than MediatR 12.4.1 on every benchmark. The `SendAsync` ValueTask path is over 2x faster with 80% fewer allocations. MediatR v13+ may have different performance characteristics. See 📊 [Benchmarks](https://github.com/evanscoapps/ModernMediator/blob/main/BENCHMARKS.md) for full three-way results including martinothamar/Mediator.

### ModernMediator vs Prism EventAggregator

For desktop developers using Prism, ModernMediator can replace EventAggregator while adding MediatR-style request/response:

| Feature              | Prism EventAggregator                    | ModernMediator                     |
| :------------------- | :--------------------------------------- | :--------------------------------- |
| Pub/Sub              | ✅ `PubSubEvent<T>`                      | ✅ `Publish<T>` / `Subscribe<T>`   |
| Pub/Sub w/ Callbacks | ❌ Manual (callback in payload)          | ✅ `Publish<TMsg, TResp>`          |
| Weak References      | ✅ `keepSubscriberReferenceAlive: false` | ✅ `weak: true` (default)          |
| Strong References    | ✅ `keepSubscriberReferenceAlive: true`  | ✅ `weak: false`                   |
| Filter Subscriptions | ✅ `.Subscribe(handler, filter)`         | ✅ `filter: predicate`             |
| UI Thread            | ✅ `ThreadOption.UIThread`               | ✅ `SubscribeOnMainThread`         |
| Background Thread    | ✅ `ThreadOption.BackgroundThread`       | ✅ `PublishAsync`                  |
| Unsubscribe          | ✅ `SubscriptionToken`                   | ✅ `IDisposable`                   |
| Request/Response     | ❌ No                                    | ✅ `Send<TResponse>`               |
| Pipeline Behaviors   | ❌ No                                    | ✅ Yes                             |
| Exception Handlers   | ❌ No                                    | ✅ Yes                             |
| Streaming            | ❌ No                                    | ✅ `CreateStream`                  |
| Source Generators     | ❌ No                                    | ✅ Yes                             |
| Native AOT           | ❌ No                                    | ✅ Yes                             |

**Bottom line:** If you're using Prism EventAggregator for pub/sub AND MediatR for CQRS, ModernMediator replaces both with one library.

## Samples

ModernMediator includes 14 cross-platform samples and a `dotnet new` template:

```bash
dotnet new modernmediator -n MyProject
```

Sample projects: Console (Basic, Domain, PubSub), WPF (Basic, PubSub), WinForms (Basic), MAUI (Basic, Validation), Avalonia (Basic, PubSub), Blazor (Server, WASM), Worker Service, WebApi, and WebApi.Advanced. The WinForms sample exercises `WinFormsDispatcher` for cross-thread notification dispatch from a background task to the UI thread.

## Use Cases

### Plugin Systems
ModernMediator excels at plugin architectures where plugins load/unload at runtime. Weak references prevent memory leaks when plugins unload. Runtime subscribe/unsubscribe enables dynamic registration. String key routing supports topic-based communication.

### Desktop Applications (WPF, WinForms, MAUI)
Built-in UI thread dispatchers, memory-efficient weak references, easy decoupling of components. Replaces both EventAggregator and MediatR.

### ASP.NET Core
Full DI integration with proper scoped service support. Request/response for CQRS patterns. Built-in pipeline behaviors for validation, logging, telemetry, and timeout. `[Endpoint]` attribute for Minimal API generation. Handlers can inject scoped services like `DbContext`. Call `services.AddModernMediatorAspNetCore()` to register `IHttpContextAccessor` and other ASP.NET Core integration services in one step.

### Large Dataset Processing
Streaming with `IAsyncEnumerable` for memory efficiency. Cancellation support for long-running operations. Backpressure-friendly enumeration.

### Serverless & Native AOT
Source generators eliminate reflection overhead. `CachingMode.Lazy` for fast cold start times. Full Native AOT compatibility. Compile-time handler discovery. ValueTask pipeline for minimal allocation overhead.

## Known Limitations

- **In-process only** — No distributed messaging. For microservices, combine with MassTransit or Wolverine for transport.
- **One handler per request** — Request/Response expects exactly one handler per request type.
- **No generic request handlers** — Each closed generic type needs its own handler.
- **Exception handlers for Request/Response only** — Pub/Sub notifications use `ErrorPolicy` instead.
- **Pipeline behaviors don't wrap streaming** — Behaviors wrap `Send()`, not `CreateStream()`.
- **Weak references + lambdas** — Closures capture `this`, which may prevent GC. Use method references or `weak: false`.
- **Behavior order = registration order** — First registered behavior executes first (outermost).
- **Native AOT requires source generator** — Use `AddModernMediatorGenerated()` instead of assembly scanning.
- **Open generics require explicit registration** — Assembly scanning skips open generic behaviors; use `AddOpenBehavior()`.
- **Scoped IMediator for DI** — Pub/Sub subscriptions via DI are per-scope; use `Mediator.Instance` for shared subscriptions.

## License

MIT License — see [LICENSE](LICENSE) file.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.