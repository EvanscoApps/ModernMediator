using System.Reflection;
using global::Avalonia;
using global::Avalonia.Controls.ApplicationLifetimes;
using global::Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModernMediator.Sample.Avalonia.Basic.Requests;
using ModernMediator.Sample.Shared.Domain;

namespace ModernMediator.Sample.Avalonia.Basic;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddModernMediator(config =>
                {
                    config.RegisterServicesFromAssemblies(
                        Assembly.GetExecutingAssembly(),
                        typeof(Dog).Assembly);
                    config.AddLogging();
                });
                services.AddTransient<MainViewModel>();
                services.AddTransient<MainWindow>();
            })
            .Build();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = host.Services.GetRequiredService<MainWindow>();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
