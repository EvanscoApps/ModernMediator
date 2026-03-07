using global::ModernMediator.Sample.Blazor.Wasm;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using System.Reflection;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Register ModernMediator — all dispatch runs client-side in the browser
builder.Services.AddModernMediator(config =>
{
    // Scan this assembly for request handlers (GetAnimalsQueryHandler, etc.)
    // and the Domain assembly for any shared handlers
    config.RegisterServicesFromAssemblies(
        Assembly.GetExecutingAssembly(),
        typeof(Dog).Assembly);

    // Add LoggingBehavior — output goes to browser console (F12 developer tools)
    config.AddLogging();
});

await builder.Build().RunAsync();
