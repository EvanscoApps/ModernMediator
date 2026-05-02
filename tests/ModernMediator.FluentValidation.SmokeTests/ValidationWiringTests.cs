using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using ModernMediator;
using Xunit;

namespace ModernMediator.FluentValidation.SmokeTests;

/// <summary>
/// Smoke test that confirms <c>AddModernMediatorValidation</c> wires correctly
/// against the packed nupkg consumed via PackageReference.
/// </summary>
public class ValidationWiringTests
{
    /// <summary>
    /// Wires <c>AddModernMediator</c> plus <c>AddModernMediatorValidation</c> and
    /// asserts that <see cref="IMediator"/> is resolvable from the resulting provider.
    /// </summary>
    [Fact]
    public void Mediator_resolves_after_AddModernMediatorValidation()
    {
        var services = new ServiceCollection();
        services.AddModernMediator(c => c.RegisterServicesFromAssemblyContaining<ValidationWiringTests>());
        services.AddModernMediatorValidation(typeof(ValidationWiringTests).Assembly);

        var provider = services.BuildServiceProvider();

        var mediator = provider.GetRequiredService<IMediator>();
        Assert.NotNull(mediator);
    }
}
