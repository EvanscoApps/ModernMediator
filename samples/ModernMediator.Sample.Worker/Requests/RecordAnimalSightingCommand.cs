namespace ModernMediator.Sample.Worker.Requests;

public record RecordAnimalSightingCommand(Animal Animal, string Location)
    : IRequest<SightingRecord>;

public record SightingRecord(Guid Id, Animal Animal, string Location, DateTime RecordedAt);

public class RecordAnimalSightingCommandHandler
    : IRequestHandler<RecordAnimalSightingCommand, SightingRecord>
{
    public async Task<SightingRecord> Handle(
        RecordAnimalSightingCommand request, CancellationToken cancellationToken = default)
    {
        // Simulate brief processing work
        await Task.Delay(Random.Shared.Next(50, 200), cancellationToken);

        return new SightingRecord(
            Guid.NewGuid(),
            request.Animal,
            request.Location,
            DateTime.Now);
    }
}
