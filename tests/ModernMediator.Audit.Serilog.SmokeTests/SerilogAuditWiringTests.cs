using Microsoft.Extensions.DependencyInjection;
using ModernMediator;
using ModernMediator.Audit.Serilog;
using Serilog;
using Xunit;

namespace ModernMediator.Audit.Serilog.SmokeTests;

/// <summary>
/// Smoke test that confirms <c>config.AddAudit&lt;SerilogAuditWriter&gt;</c> wires
/// correctly against the packed nupkg consumed via PackageReference.
/// </summary>
public class SerilogAuditWiringTests
{
    /// <summary>
    /// Wires <c>AddModernMediator</c> with <c>AddAudit&lt;SerilogAuditWriter&gt;</c>
    /// and asserts <see cref="IAuditWriter"/> resolves to <see cref="SerilogAuditWriter"/>.
    /// </summary>
    [Fact]
    public void IAuditWriter_resolves_to_SerilogAuditWriter_after_AddAudit()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILogger>(new LoggerConfiguration().CreateLogger());
        services.AddModernMediator(c =>
        {
            c.RegisterServicesFromAssemblyContaining<SerilogAuditWiringTests>();
            c.AddAudit<SerilogAuditWriter>();
        });

        var provider = services.BuildServiceProvider();

        var mediator = provider.GetRequiredService<IMediator>();
        Assert.NotNull(mediator);

        var writer = provider.GetRequiredService<IAuditWriter>();
        Assert.IsType<SerilogAuditWriter>(writer);
    }
}
