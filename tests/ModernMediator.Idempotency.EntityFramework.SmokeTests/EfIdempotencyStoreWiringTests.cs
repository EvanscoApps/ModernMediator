using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ModernMediator;
using ModernMediator.Idempotency.EntityFramework;
using Xunit;

namespace ModernMediator.Idempotency.EntityFramework.SmokeTests;

/// <summary>
/// Smoke test that confirms the documented Idempotency.EntityFramework wiring pattern
/// (per-package README) results in <see cref="IIdempotencyStore"/> resolving to
/// <see cref="EfCoreIdempotencyStore"/> against the packed nupkg.
/// </summary>
public class EfIdempotencyStoreWiringTests
{
    /// <summary>
    /// Wires <see cref="IdempotencyDbContext"/> on EF Core InMemory, registers
    /// <see cref="EfCoreIdempotencyStore"/> as the <see cref="IIdempotencyStore"/>
    /// implementation before <c>AddIdempotency</c> runs (so the InMemory default
    /// is suppressed by <c>TryAddSingleton</c>), and asserts the EF store wins.
    /// </summary>
    [Fact]
    public void IIdempotencyStore_resolves_to_EfCoreIdempotencyStore_after_AddIdempotency()
    {
        var services = new ServiceCollection();
        services.AddDbContext<IdempotencyDbContext>(o => o.UseInMemoryDatabase("smoke-idempotency"));
        services.AddSingleton<IIdempotencyStore, EfCoreIdempotencyStore>();
        services.AddModernMediator(c =>
        {
            c.RegisterServicesFromAssemblyContaining<EfIdempotencyStoreWiringTests>();
            c.AddIdempotency();
        });

        var provider = services.BuildServiceProvider();

        var mediator = provider.GetRequiredService<IMediator>();
        Assert.NotNull(mediator);

        var store = provider.GetRequiredService<IIdempotencyStore>();
        Assert.IsType<EfCoreIdempotencyStore>(store);
    }
}
