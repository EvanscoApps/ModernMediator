using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;

namespace ModernMediator;

/// <summary>
/// Internal helpers for registering audit channel infrastructure.
/// </summary>
internal static class AuditHostedServiceExtensions
{
    internal static void AddAuditChannelInfrastructure(
        IServiceCollection services,
        AuditOptions options)
    {
        var channel = Channel.CreateBounded<AuditRecord>(
            new BoundedChannelOptions(options.ChannelCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true
            });

        services.AddSingleton(channel);
        services.AddHostedService<AuditChannelDrainer>();
    }
}
