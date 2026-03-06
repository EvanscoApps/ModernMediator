using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ModernMediator.FluentValidation;

namespace ModernMediator.Sample.Maui.Validation;

public class AdoptionFormViewModel : INotifyPropertyChanged
{
    private readonly IMediator _mediator;
    private string _animalName = "";
    private string _species = "";
    private int _ageYears;
    private string _adopterName = "";
    private string _adopterEmail = "";
    private string _animalNameError = "";
    private string _speciesError = "";
    private string _ageYearsError = "";
    private string _adopterNameError = "";
    private string _adopterEmailError = "";
    private string _successMessage = "";
    private bool _isBusy;

    public AdoptionFormViewModel(IMediator mediator)
    {
        _mediator = mediator;
        SubmitCommand = new RelayCommand(async _ => await SubmitAsync());
    }

    public string AnimalName
    {
        get => _animalName;
        set { _animalName = value; OnPropertyChanged(); }
    }

    public string Species
    {
        get => _species;
        set { _species = value; OnPropertyChanged(); }
    }

    public int AgeYears
    {
        get => _ageYears;
        set { _ageYears = value; OnPropertyChanged(); }
    }

    public string AdopterName
    {
        get => _adopterName;
        set { _adopterName = value; OnPropertyChanged(); }
    }

    public string AdopterEmail
    {
        get => _adopterEmail;
        set { _adopterEmail = value; OnPropertyChanged(); }
    }

    public string AnimalNameError
    {
        get => _animalNameError;
        private set { _animalNameError = value; OnPropertyChanged(); }
    }

    public string SpeciesError
    {
        get => _speciesError;
        private set { _speciesError = value; OnPropertyChanged(); }
    }

    public string AgeYearsError
    {
        get => _ageYearsError;
        private set { _ageYearsError = value; OnPropertyChanged(); }
    }

    public string AdopterNameError
    {
        get => _adopterNameError;
        private set { _adopterNameError = value; OnPropertyChanged(); }
    }

    public string AdopterEmailError
    {
        get => _adopterEmailError;
        private set { _adopterEmailError = value; OnPropertyChanged(); }
    }

    public string SuccessMessage
    {
        get => _successMessage;
        set { _successMessage = value; OnPropertyChanged(); }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set { _isBusy = value; OnPropertyChanged(); }
    }

    public ICommand SubmitCommand { get; }

    private async Task SubmitAsync()
    {
        IsBusy = true;
        ClearErrors();
        SuccessMessage = "";

        try
        {
            var result = await _mediator.Send(new SubmitAdoptionCommand(
                AnimalName, Species, AgeYears, AdopterName, AdopterEmail));

            SuccessMessage = result.Message;
        }
        catch (ModernValidationException ex)
        {
            ApplyValidationErrors(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ClearErrors()
    {
        AnimalNameError = "";
        SpeciesError = "";
        AgeYearsError = "";
        AdopterNameError = "";
        AdopterEmailError = "";
    }

    private void ApplyValidationErrors(ModernValidationException ex)
    {
        var grouped = ex.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => string.Join(", ", g.Select(e => e.ErrorMessage)));

        if (grouped.TryGetValue(nameof(AnimalName), out var animalNameErr))
            AnimalNameError = animalNameErr;
        if (grouped.TryGetValue(nameof(Species), out var speciesErr))
            SpeciesError = speciesErr;
        if (grouped.TryGetValue(nameof(AgeYears), out var ageErr))
            AgeYearsError = ageErr;
        if (grouped.TryGetValue(nameof(AdopterName), out var adopterNameErr))
            AdopterNameError = adopterNameErr;
        if (grouped.TryGetValue(nameof(AdopterEmail), out var adopterEmailErr))
            AdopterEmailError = adopterEmailErr;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
