using ModernMediator.Sample.Console.Domain;

namespace ModernMediator.Sample.Blazor.Wasm.Requests;

public record GetAnimalsQuery : IRequest<Animal[]>;

public class GetAnimalsQueryHandler : IRequestHandler<GetAnimalsQuery, Animal[]>
{
    public Task<Animal[]> Handle(GetAnimalsQuery request, CancellationToken cancellationToken = default)
    {
        Animal[] animals =
        [
            new Dog("Rex", 5, "German Shepherd"),
            new Dog("Bella", 3, "Labrador"),
            new Cat("Whiskers", 7, true),
            new Cat("Shadow", 2, false),
            new Eagle("Aquila", 12, 2.1)
        ];

        return Task.FromResult(animals);
    }
}
