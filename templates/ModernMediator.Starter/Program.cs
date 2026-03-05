using System.Reflection;
using ModernMediator;
using ModernMediator.AspNetCore.Generated;

var builder = WebApplication.CreateBuilder(args);

// Register ModernMediator with assembly scanning and built-in behaviors
builder.Services.AddModernMediator(config =>
{
    config.RegisterServicesFromAssemblies(Assembly.GetExecutingAssembly());
    config.AddLogging();
    config.AddTimeout();
});

var app = builder.Build();

// Map all [Endpoint]-decorated requests as Minimal API routes
app.MapMediatorEndpoints();

app.Run();
