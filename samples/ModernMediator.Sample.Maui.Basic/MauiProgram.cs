using System.Reflection;
using ModernMediator.Sample.Shared.Domain;

namespace ModernMediator.Sample.Maui.Basic;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddModernMediator(config =>
        {
            config.RegisterServicesFromAssemblies(
                Assembly.GetExecutingAssembly(),
                typeof(Dog).Assembly);
            config.AddLogging();
        });
        builder.Services.AddTransient<AnimalsViewModel>();
        builder.Services.AddTransient<MainPage>();

        return builder.Build();
    }
}
