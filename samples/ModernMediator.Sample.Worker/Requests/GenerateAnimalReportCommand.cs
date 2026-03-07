namespace ModernMediator.Sample.Worker.Requests;

// Timeout is generous — this handler completes well within 500ms
[Timeout(500)]
public record GenerateAnimalReportCommand(DateOnly Date) : IRequest<AnimalReport>;

public record AnimalReport(DateOnly Date, int SightingsCount, string Summary);

public class GenerateAnimalReportCommandHandler
    : IRequestHandler<GenerateAnimalReportCommand, AnimalReport>
{
    public Task<AnimalReport> Handle(
        GenerateAnimalReportCommand request, CancellationToken cancellationToken = default)
    {
        var report = new AnimalReport(
            request.Date,
            SightingsCount: Random.Shared.Next(1, 10),
            Summary: "All clear");

        return Task.FromResult(report);
    }
}
