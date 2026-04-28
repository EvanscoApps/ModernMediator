using ModernMediator.Sample.Shared.Domain;

namespace ModernMediator.Sample.Console.Basic.Requests;

// ── Timeout behavior ─────────────────────────────────────────
// AnimalProfile is a simple summary record.
// GetAnimalProfileQuery completes well within 2s.
// GetSlowAnimalProfileQuery intentionally exceeds its 100ms timeout.

public record AnimalProfile(string Summary);

[Timeout(2000)]
public record GetAnimalProfileQuery(string Name) : IRequest<AnimalProfile>;

[Timeout(100)]
public record GetSlowAnimalProfileQuery(string Name) : IRequest<AnimalProfile>;

public class GetAnimalProfileQueryHandler : IRequestHandler<GetAnimalProfileQuery, AnimalProfile>
{
    public Task<AnimalProfile> Handle(GetAnimalProfileQuery request, CancellationToken cancellationToken = default)
    {
        var profile = new AnimalProfile($"{request.Name} is a healthy animal in our registry.");
        return Task.FromResult(profile);
    }
}

public class GetSlowAnimalProfileQueryHandler : IRequestHandler<GetSlowAnimalProfileQuery, AnimalProfile>
{
    public async Task<AnimalProfile> Handle(GetSlowAnimalProfileQuery request, CancellationToken cancellationToken = default)
    {
        // Simulate a slow external call that exceeds the 100ms timeout
        await Task.Delay(500, cancellationToken);
        return new AnimalProfile($"{request.Name} profile loaded (slow path).");
    }
}
