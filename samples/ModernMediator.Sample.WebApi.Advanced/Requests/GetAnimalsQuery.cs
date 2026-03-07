namespace ModernMediator.Sample.WebApi.Advanced.Requests;

[Endpoint("/api/animals", "GET")]
public record GetAnimalsQuery : IRequest<Result<Animal[]>>;

public class GetAnimalsQueryHandler : IRequestHandler<GetAnimalsQuery, Result<Animal[]>>
{
    private readonly AnimalRepository _repository;

    public GetAnimalsQueryHandler(AnimalRepository repository) => _repository = repository;

    public Task<Result<Animal[]>> Handle(GetAnimalsQuery request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result<Animal[]>.Success(_repository.GetAll()));
    }
}
