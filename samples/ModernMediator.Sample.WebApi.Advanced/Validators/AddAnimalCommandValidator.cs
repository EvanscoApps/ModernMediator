using FluentValidation;

namespace ModernMediator.Sample.WebApi.Advanced.Validators;

public class AddAnimalCommandValidator : AbstractValidator<AddAnimalCommand>
{
    private static readonly string[] ValidSpecies = ["Dog", "Cat", "Eagle"];

    public AddAnimalCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(50).WithMessage("Name must be 50 characters or fewer.");

        RuleFor(x => x.Species)
            .Must(s => ValidSpecies.Contains(s))
            .WithMessage("Species must be 'Dog', 'Cat', or 'Eagle'.");

        RuleFor(x => x.AgeYears)
            .InclusiveBetween(0, 30)
            .WithMessage("Age must be between 0 and 30.");
    }
}
