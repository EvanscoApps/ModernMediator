using System.Reflection;
using FluentValidation;
using ModernMediator.FluentValidation;
using global::ModernMediator.Sample.WebApi.Advanced.Validators;

var builder = WebApplication.CreateBuilder(args);

// Swagger/OpenAPI — makes it easy for developers evaluating the library
// to explore all endpoints interactively at /swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Singleton services
builder.Services.AddSingleton<AnimalRepository>();
builder.Services.AddSingleton<TelemetryStatsService>();

builder.Services.AddModernMediator(config =>
{
    config.RegisterServicesFromAssemblies(
        Assembly.GetExecutingAssembly(),
        typeof(Dog).Assembly);

    // Pipeline behaviors execute in registration order (outermost → innermost).
    // Each behavior wraps the next, forming a Russian-doll pipeline:
    //   Logging → Validation → Timeout → Handler
    //
    // On the way IN:  Logging runs first, then Validation, then Timeout, then the handler.
    // On the way OUT: Timeout completes, then Validation, then Logging.

    // 1. LoggingBehavior (outermost) — logs request name and serialized payload
    config.AddLogging(opts => opts.LogRequestPayload = true);

    // 2. ValidationBehavior (middle) — runs FluentValidation rules before the handler.
    config.AddModernMediatorValidation(typeof(Program).Assembly);

    // 3. TimeoutBehavior (innermost) — enforces [Timeout(ms)] closest to the handler
    config.AddTimeout();

    // Telemetry via System.Diagnostics ActivitySource + Meter
    config.AddTelemetry();
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

// ── Manual endpoint mapping for Result<T> → HTTP status code translation ──
//
// The source-generated MapMediatorEndpoints() returns raw Result<T> objects as 200 OK,
// which doesn't produce proper REST semantics (404 Not Found, 409 Conflict, etc.).
// For endpoints that return Result<T>, we map manually using ToHttpResult() which
// translates error codes to the appropriate HTTP status codes.

app.MapGet("/api/animals", async (IMediator mediator) =>
{
    var result = await mediator.Send(new GetAnimalsQuery());
    return result.ToHttpResult();
}).WithTags("Animals");

app.MapGet("/api/animals/{name}", async (string name, IMediator mediator) =>
{
    var result = await mediator.Send(new GetAnimalByNameQuery(name));
    return result.ToHttpResult();
}).WithTags("Animals");

app.MapPost("/api/animals", async (AddAnimalCommand command, IMediator mediator) =>
{
    try
    {
        var result = await mediator.Send(command);
        return result.ToHttpResult();
    }
    catch (global::ModernMediator.FluentValidation.ModernValidationException ex)
    {
        // ValidationBehavior throws when FluentValidation rules fail.
        // Map validation errors to a 400 Bad Request with structured error details.
        var errors = ex.Errors.Select(e => new { e.PropertyName, e.ErrorMessage });
        return Results.BadRequest(errors);
    }
}).WithTags("Animals");

app.MapDelete("/api/animals/{name}", async (string name, IMediator mediator) =>
{
    var result = await mediator.Send(new RemoveAnimalCommand(name));
    return result.ToHttpResult();
}).WithTags("Animals");

// Telemetry endpoint — returns raw response, no Result<T> mapping needed
app.MapGet("/api/telemetry/stats", async (IMediator mediator) =>
{
    return await mediator.Send(new GetTelemetryStatsQuery());
}).WithTags("Telemetry");

app.Run();
