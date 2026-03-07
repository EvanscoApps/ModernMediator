using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddModernMediator(config =>
        {
            // Scan this assembly for command handlers and the Domain assembly for shared types
            config.RegisterServicesFromAssemblies(
                Assembly.GetExecutingAssembly(),
                typeof(Dog).Assembly);

            // Built-in logging — request/response logged at each dispatch
            config.AddLogging(opts => opts.LogRequestPayload = true);

            // Timeout enforcement via [Timeout(ms)] attribute
            config.AddTimeout();

            // Telemetry via System.Diagnostics ActivitySource + Meter
            config.AddTelemetry();
        });

        // Register the background worker as a hosted service
        services.AddHostedService<AnimalSightingWorker>();
    })
    .Build();

await host.RunAsync();
