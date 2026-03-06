namespace ModernMediator.Sample.Maui.Validation;

public class SubmitAdoptionCommandHandler : IRequestHandler<SubmitAdoptionCommand, AdoptionConfirmation>
{
    public Task<AdoptionConfirmation> Handle(SubmitAdoptionCommand request, CancellationToken cancellationToken = default)
    {
        var confirmation = new AdoptionConfirmation(
            $"{request.AnimalName} the {request.Species} has been adopted by {request.AdopterName}!");

        return Task.FromResult(confirmation);
    }
}
