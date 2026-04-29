using ModernMediator.Sample.Shared.Domain;

namespace ModernMediator.Sample.WinForms.Basic.Requests;

public sealed record GetAnimalsQuery : IRequest<Animal[]>;

public sealed class GetAnimalsQueryHandler : IRequestHandler<GetAnimalsQuery, Animal[]>
{
    public Task<Animal[]> Handle(GetAnimalsQuery request, CancellationToken cancellationToken = default)
    {
        Animal[] animals =
        [
            new Dog("Rex", 5, "German Shepherd"),
            new Dog("Buddy", 3, "Labrador"),
            new Cat("Whiskers", 7, true),
            new Cat("Mittens", 2, false),
            new Eagle("Skyler", 4, 2.0),
        ];
        return Task.FromResult(animals);
    }
}
