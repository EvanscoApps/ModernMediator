namespace ModernMediator.Sample.Worker.Requests;

// Intentionally tight timeout — handler exceeds this to demonstrate timeout enforcement
[Timeout(100)]
public record ProcessSlowSightingCommand(Animal Animal) : IRequest<SightingRecord>;

public class ProcessSlowSightingCommandHandler
    : IRequestHandler<ProcessSlowSightingCommand, SightingRecord>
{
    public async Task<SightingRecord> Handle(
        ProcessSlowSightingCommand request, CancellationToken cancellationToken = default)
    {
        // Intentionally exceeds the 100ms timeout
        await Task.Delay(300, cancellationToken);

        return new SightingRecord(
            Guid.NewGuid(),
            request.Animal,
            "Slow Zone",
            DateTime.Now);
    }
}
