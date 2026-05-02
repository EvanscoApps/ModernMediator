using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ModernMediator;
using ModernMediator.Audit.EntityFramework;
using Xunit;

namespace ModernMediator.Audit.EntityFramework.SmokeTests;

/// <summary>
/// Smoke test that confirms <c>config.AddAudit&lt;EfCoreAuditWriter&gt;</c> wires
/// correctly against the packed nupkg consumed via PackageReference.
/// </summary>
public class EfAuditWriterWiringTests
{
    /// <summary>
    /// Wires <c>AddModernMediator</c> with <c>AddAudit&lt;EfCoreAuditWriter&gt;</c>
    /// plus a registered <see cref="AuditDbContext"/> backed by EF Core InMemory,
    /// and asserts <see cref="IAuditWriter"/> resolves to <see cref="EfCoreAuditWriter"/>.
    /// </summary>
    [Fact]
    public void IAuditWriter_resolves_to_EfCoreAuditWriter_after_AddAudit()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AuditDbContext>(o => o.UseInMemoryDatabase("smoke-audit"));
        services.AddModernMediator(c =>
        {
            c.RegisterServicesFromAssemblyContaining<EfAuditWriterWiringTests>();
            c.AddAudit<EfCoreAuditWriter>();
        });

        var provider = services.BuildServiceProvider();

        var mediator = provider.GetRequiredService<IMediator>();
        Assert.NotNull(mediator);

        var writer = provider.GetRequiredService<IAuditWriter>();
        Assert.IsType<EfCoreAuditWriter>(writer);
    }
}
