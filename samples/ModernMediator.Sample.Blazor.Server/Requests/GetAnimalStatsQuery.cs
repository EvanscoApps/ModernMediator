using ModernMediator.Sample.Console.Domain;

namespace ModernMediator.Sample.Blazor.Server.Requests;

public record AnimalStats(int TotalCount, int MammalCount, int BirdCount);

public record GetAnimalStatsQuery : IRequest<AnimalStats>;

public class GetAnimalStatsQueryHandler : IRequestHandler<GetAnimalStatsQuery, AnimalStats>
{
    private static readonly Animal[] Animals =
    [
        new Dog("Rex", 5, "German Shepherd"),
        new Dog("Bella", 3, "Labrador"),
        new Cat("Whiskers", 7, true),
        new Cat("Shadow", 2, false),
        new Eagle("Aquila", 12, 2.1)
    ];

    public Task<AnimalStats> Handle(GetAnimalStatsQuery request, CancellationToken cancellationToken = default)
    {
        var stats = new AnimalStats(
            TotalCount: Animals.Length,
            MammalCount: Animals.Count(a => a is Dog or Cat),
            BirdCount: Animals.Count(a => a is Eagle));

        return Task.FromResult(stats);
    }
}
