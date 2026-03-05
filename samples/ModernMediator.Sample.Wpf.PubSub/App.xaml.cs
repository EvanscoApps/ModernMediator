using System.Reflection;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ModernMediator.Sample.Wpf.PubSub;

public partial class App : Application
{
    private readonly IHost _host;

    public App()
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddModernMediator(config =>
                {
                    config.RegisterServicesFromAssemblies(Assembly.GetExecutingAssembly(),
                        typeof(global::ModernMediator.Sample.Console.Domain.Dog).Assembly);
                    config.AddLogging();
                });

                // AdoptionFeedViewModel is both a ViewModel AND a notification handler.
                // Registered as singleton so the same instance that's bound to the UI
                // also receives AnimalAdoptedNotification callbacks from the mediator.
                services.AddSingleton<AdoptionFeedViewModel>();
                services.AddSingleton<INotificationHandler<AnimalAdoptedNotification>>(
                    sp => sp.GetRequiredService<AdoptionFeedViewModel>());

                // ShelterViewModel publishes notifications with an OnAdoptionConfirmed callback delegate.
                // The handler invokes this delegate after processing — calling back into the publisher
                // without any direct reference between the two ViewModels.
                services.AddTransient<ShelterViewModel>();

                services.AddTransient<MainViewModel>();
                services.AddTransient<MainWindow>();
            })
            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        await _host.StartAsync();

        // Bridge the DI-registered INotificationHandler to the mediator's in-memory
        // Subscribe/Publish system. The mediator dispatches to Action<T> subscriptions,
        // not to DI-resolved INotificationHandler<T> implementations, so we wire them here.
        var mediator = _host.Services.GetRequiredService<IMediator>();
        var feedVM = _host.Services.GetRequiredService<AdoptionFeedViewModel>();
        mediator.Subscribe<AnimalAdoptedNotification>(
            n => _ = feedVM.Handle(n, CancellationToken.None),
            weak: false);

        var window = _host.Services.GetRequiredService<MainWindow>();
        window.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await _host.StopAsync();
        _host.Dispose();
    }
}
