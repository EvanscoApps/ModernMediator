namespace ModernMediator.Sample.Maui.Validation;

public record SubmitAdoptionCommand(
    string AnimalName,
    string Species,
    int AgeYears,
    string AdopterName,
    string AdopterEmail) : IRequest<AdoptionConfirmation>;

public record AdoptionConfirmation(string Message);
