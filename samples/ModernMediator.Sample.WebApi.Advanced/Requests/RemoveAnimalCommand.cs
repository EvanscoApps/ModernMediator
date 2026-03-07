namespace ModernMediator.Sample.WebApi.Advanced.Requests;

[Endpoint("/api/animals/{Name}", "DELETE")]
public record RemoveAnimalCommand(string Name) : IRequest<Result<bool>>;

public class RemoveAnimalCommandHandler : IRequestHandler<RemoveAnimalCommand, Result<bool>>
{
    private readonly AnimalRepository _repository;

    public RemoveAnimalCommandHandler(AnimalRepository repository) => _repository = repository;

    public Task<Result<bool>> Handle(RemoveAnimalCommand request, CancellationToken cancellationToken = default)
    {
        if (!_repository.Exists(request.Name))
            return Task.FromResult(
                Result<bool>.Failure("NOT_FOUND", $"Animal '{request.Name}' not found."));

        _repository.Remove(request.Name);
        return Task.FromResult(Result<bool>.Success(true));
    }
}
