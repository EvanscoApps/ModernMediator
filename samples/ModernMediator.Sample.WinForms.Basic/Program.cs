using System;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModernMediator.Dispatchers;

namespace ModernMediator.Sample.WinForms.Basic;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddModernMediator(config =>
                {
                    config.RegisterServicesFromAssemblies(
                        Assembly.GetExecutingAssembly(),
                        typeof(global::ModernMediator.Sample.Shared.Domain.Dog).Assembly);
                    config.AddLogging();
                });
                services.AddSingleton<MainForm>();
            })
            .Build();

        var mainForm = host.Services.GetRequiredService<MainForm>();
        var mediator = host.Services.GetRequiredService<IMediator>();
        mediator.SetDispatcher(new WinFormsDispatcher(mainForm));

        Application.Run(mainForm);
    }
}
