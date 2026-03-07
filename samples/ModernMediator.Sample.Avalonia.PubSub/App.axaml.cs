using System.Reflection;
using global::Avalonia;
using global::Avalonia.Controls.ApplicationLifetimes;
using global::Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModernMediator.Sample.Console.Domain;

namespace ModernMediator.Sample.Avalonia.PubSub;

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

                // AdoptionFeedViewModel is both a ViewModel AND a notification handler.
                // Registered as singleton so the same instance that's bound to the UI
                // also receives AnimalAdoptedNotification callbacks from the mediator.
                services.AddSingleton<AdoptionFeedViewModel>();
                services.AddSingleton<INotificationHandler<AnimalAdoptedNotification>>(
                    sp => sp.GetRequiredService<AdoptionFeedViewModel>());

                // ShelterViewModel publishes with an OnAdoptionConfirmed callback delegate.
                // The handler invokes it after processing — no direct reference between ViewModels.
                services.AddTransient<ShelterViewModel>();

                services.AddTransient<MainViewModel>();
                services.AddTransient<MainWindow>();
            })
            .Build();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Bridge the DI-registered INotificationHandler to the mediator's in-memory
            // Subscribe/Publish system.
            var mediator = host.Services.GetRequiredService<IMediator>();
            var feedVM = host.Services.GetRequiredService<AdoptionFeedViewModel>();
            mediator.Subscribe<AnimalAdoptedNotification>(
                n => _ = feedVM.Handle(n, CancellationToken.None),
                weak: false);

            desktop.MainWindow = host.Services.GetRequiredService<MainWindow>();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
