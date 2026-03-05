using System.Reflection;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ModernMediator.Sample.Wpf.Basic;

public partial class App : Application
{
    private readonly IHost _host;

    public App()
    {
        var statusLog = new StatusLogService();

        _host = Host.CreateDefaultBuilder()
            .ConfigureLogging(logging =>
            {
                logging.AddProvider(new StatusLoggerProvider(statusLog));
            })
            .ConfigureServices(services =>
            {
                services.AddSingleton(statusLog);
                services.AddModernMediator(config =>
                {
                    config.RegisterServicesFromAssemblies(Assembly.GetExecutingAssembly(),
                        typeof(global::ModernMediator.Sample.Console.Domain.Dog).Assembly);
                    config.AddLogging();
                });
                services.AddTransient<MainViewModel>();
                services.AddTransient<MainWindow>();
            })
            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        await _host.StartAsync();
        var window = _host.Services.GetRequiredService<MainWindow>();
        window.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await _host.StopAsync();
        _host.Dispose();
    }
}
