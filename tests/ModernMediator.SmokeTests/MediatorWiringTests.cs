using Microsoft.Extensions.DependencyInjection;
using ModernMediator;
using Xunit;

namespace ModernMediator.SmokeTests;

/// <summary>
/// Smoke test that confirms <see cref="IMediator"/> resolves from a real DI container
/// after <c>AddModernMediator</c> runs against the packed nupkg consumed via PackageReference.
/// </summary>
public class MediatorWiringTests
{
    /// <summary>
    /// Wires <c>AddModernMediator</c> with assembly scanning, builds the provider,
    /// and asserts that <see cref="IMediator"/> is resolvable.
    /// </summary>
    [Fact]
    public void Mediator_resolves_from_DI_after_AddModernMediator()
    {
        var services = new ServiceCollection();
        services.AddModernMediator(c => c.RegisterServicesFromAssemblyContaining<MediatorWiringTests>());
        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();
        Assert.NotNull(mediator);
    }
}
