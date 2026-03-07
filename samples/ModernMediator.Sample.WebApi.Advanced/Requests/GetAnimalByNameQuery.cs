namespace ModernMediator.Sample.WebApi.Advanced.Requests;

[Endpoint("/api/animals/{Name}", "GET")]
public record GetAnimalByNameQuery(string Name) : IRequest<Result<Animal>>;

public class GetAnimalByNameQueryHandler : IRequestHandler<GetAnimalByNameQuery, Result<Animal>>
{
    private readonly AnimalRepository _repository;

    public GetAnimalByNameQueryHandler(AnimalRepository repository) => _repository = repository;

    public Task<Result<Animal>> Handle(GetAnimalByNameQuery request, CancellationToken cancellationToken = default)
    {
        var animal = _repository.GetByName(request.Name);

        var result = animal is not null
            ? Result<Animal>.Success(animal)
            : Result<Animal>.Failure("NOT_FOUND", $"Animal '{request.Name}' not found.");

        return Task.FromResult(result);
    }
}
