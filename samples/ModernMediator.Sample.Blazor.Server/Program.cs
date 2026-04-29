using System.Reflection;
using ModernMediator;
using ModernMediator.Sample.Blazor.Server.Components;
using ModernMediator.Sample.Shared.Domain;

var builder = WebApplication.CreateBuilder(args);

// Add Blazor interactive server-side rendering
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Register ModernMediator with request handlers and logging pipeline
builder.Services.AddModernMediator(config =>
{
    // Scan this assembly for request handlers (GetAnimalsQueryHandler, etc.)
    // and the Domain assembly for any shared handlers
    config.RegisterServicesFromAssemblies(
        Assembly.GetExecutingAssembly(),
        typeof(Dog).Assembly);

    // Add LoggingBehavior to the pipeline — output appears in server console
    config.AddLogging();
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
