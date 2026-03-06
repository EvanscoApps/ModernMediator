using FluentValidation;

namespace ModernMediator.Sample.Maui.Validation;

public class SubmitAdoptionCommandValidator : AbstractValidator<SubmitAdoptionCommand>
{
    private static readonly string[] AllowedSpecies = ["Dog", "Cat", "Eagle"];

    public SubmitAdoptionCommandValidator()
    {
        RuleFor(x => x.AnimalName)
            .NotEmpty().WithMessage("Animal name is required.")
            .MaximumLength(50).WithMessage("Animal name must be 50 characters or fewer.");

        RuleFor(x => x.Species)
            .NotEmpty().WithMessage("Species is required.")
            .Must(s => AllowedSpecies.Contains(s, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Species must be Dog, Cat, or Eagle.");

        RuleFor(x => x.AgeYears)
            .InclusiveBetween(0, 30).WithMessage("Age must be between 0 and 30.");

        RuleFor(x => x.AdopterName)
            .NotEmpty().WithMessage("Adopter name is required.")
            .MaximumLength(100).WithMessage("Adopter name must be 100 characters or fewer.");

        RuleFor(x => x.AdopterEmail)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.");
    }
}
