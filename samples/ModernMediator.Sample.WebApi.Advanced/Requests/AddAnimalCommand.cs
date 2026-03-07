namespace ModernMediator.Sample.WebApi.Advanced.Requests;

[Endpoint("/api/animals", "POST")]
public record AddAnimalCommand(string Name, string Species, int AgeYears)
    : IRequest<Result<Animal>>;

public class AddAnimalCommandHandler : IRequestHandler<AddAnimalCommand, Result<Animal>>
{
    private readonly AnimalRepository _repository;

    public AddAnimalCommandHandler(AnimalRepository repository) => _repository = repository;

    public Task<Result<Animal>> Handle(AddAnimalCommand request, CancellationToken cancellationToken = default)
    {
        if (_repository.Exists(request.Name))
            return Task.FromResult(
                Result<Animal>.Failure("ALREADY_EXISTS", $"Animal '{request.Name}' already exists."));

        Animal? animal = request.Species switch
        {
            "Dog" => new Dog(request.Name, request.AgeYears, "Mixed"),
            "Cat" => new Cat(request.Name, request.AgeYears, true),
            "Eagle" => new Eagle(request.Name, request.AgeYears, 2.0),
            _ => null
        };

        if (animal is null)
            return Task.FromResult(
                Result<Animal>.Failure("INVALID_SPECIES", $"Species '{request.Species}' is not supported."));

        _repository.Add(animal);
        return Task.FromResult(Result<Animal>.Success(animal));
    }
}
