using ModernMediator.Sample.Console.Domain;

namespace ModernMediator.Sample.Blazor.Server.Requests;

public record FindAnimalByNameQuery(string Name) : IRequest<Result<Animal>>;

public class FindAnimalByNameQueryHandler : IRequestHandler<FindAnimalByNameQuery, Result<Animal>>
{
    private static readonly Animal[] Animals =
    [
        new Dog("Rex", 5, "German Shepherd"),
        new Dog("Bella", 3, "Labrador"),
        new Cat("Whiskers", 7, true),
        new Cat("Shadow", 2, false),
        new Eagle("Aquila", 12, 2.1)
    ];

    public Task<Result<Animal>> Handle(FindAnimalByNameQuery request, CancellationToken cancellationToken = default)
    {
        var animal = Animals.FirstOrDefault(a =>
            string.Equals(a.Name, request.Name, StringComparison.OrdinalIgnoreCase));

        var result = animal is not null
            ? Result<Animal>.Success(animal)
            : Result<Animal>.Failure("NOT_FOUND", "No animal with that name exists.");

        return Task.FromResult(result);
    }
}
