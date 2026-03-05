using System.Reflection;
using ModernMediator;
using ModernMediator.AspNetCore.Generated;
using ModernMediator.FluentValidation;

var builder = WebApplication.CreateBuilder(args);

// ──────────────────────────────────────────────────────────────
// 1. Register ModernMediator with pipeline behaviors
// ──────────────────────────────────────────────────────────────
builder.Services.AddModernMediator(config =>
{
    // Scan this assembly for IRequestHandler<,> implementations
    config.RegisterServicesFromAssemblies(Assembly.GetExecutingAssembly());

    // Built-in logging: logs request start/end at configurable levels
    config.AddLogging(opts =>
    {
        opts.LogRequestPayload = true;   // serialize request to log output
        opts.LogResponsePayload = false; // keep response payloads out of logs
    });

    // Built-in timeout: enforced per-request via [Timeout(ms)] attribute
    config.AddTimeout();

    // Built-in telemetry: System.Diagnostics ActivitySource + Meter ("ModernMediator")
    config.AddTelemetry();
});

// ──────────────────────────────────────────────────────────────
// 2. Register FluentValidation validators and pipeline behavior
//    Scans this assembly for AbstractValidator<T> implementations
// ──────────────────────────────────────────────────────────────
builder.Services.AddModernMediatorValidation(Assembly.GetExecutingAssembly());

var app = builder.Build();

// ──────────────────────────────────────────────────────────────
// 3. Map all [Endpoint]-decorated requests as Minimal API routes
//    Generated at compile time by ModernMediator.AspNetCore.Generators
// ──────────────────────────────────────────────────────────────
app.MapMediatorEndpoints();

app.Run();
