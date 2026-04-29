using ModernMediator.Sample.Shared.Domain;

namespace ModernMediator.Sample.WinForms.Basic.Requests;

public sealed record FindAnimalByNameQuery(string Name) : IRequest<Result<Animal>>;

public sealed class FindAnimalByNameQueryHandler : IRequestHandler<FindAnimalByNameQuery, Result<Animal>>
{
    private static readonly Animal[] _animals =
    [
        new Dog("Rex", 5, "German Shepherd"),
        new Dog("Buddy", 3, "Labrador"),
        new Cat("Whiskers", 7, true),
        new Cat("Mittens", 2, false),
        new Eagle("Skyler", 4, 2.0),
    ];

    public Task<Result<Animal>> Handle(FindAnimalByNameQuery request, CancellationToken cancellationToken = default)
    {
        var found = _animals.FirstOrDefault(a =>
            string.Equals(a.Name, request.Name, StringComparison.OrdinalIgnoreCase));

        var result = found is not null
            ? Result<Animal>.Success(found)
            : Result<Animal>.Failure("NOT_FOUND", $"No animal named '{request.Name}'.");

        return Task.FromResult(result);
    }
}
