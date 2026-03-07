using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ModernMediator.Sample.Avalonia.Basic.Requests;
using ModernMediator.Sample.Console.Domain;

namespace ModernMediator.Sample.Avalonia.Basic;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly IMediator _mediator;
    private string _searchName = "";
    private string _searchResult = "";
    private bool _isBusy;

    public MainViewModel(IMediator mediator)
    {
        _mediator = mediator;

        LoadCommand = new RelayCommand(async _ => await LoadAnimalsAsync());
        SearchCommand = new RelayCommand(
            async _ => await SearchAnimalAsync(),
            _ => !string.IsNullOrWhiteSpace(SearchName));

        _ = LoadAnimalsAsync();
    }

    public ObservableCollection<AnimalDisplayModel> Animals { get; } = new();

    public string SearchName
    {
        get => _searchName;
        set
        {
            _searchName = value;
            OnPropertyChanged();
            ((RelayCommand)SearchCommand).RaiseCanExecuteChanged();
        }
    }

    public string SearchResult
    {
        get => _searchResult;
        set { _searchResult = value; OnPropertyChanged(); }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set { _isBusy = value; OnPropertyChanged(); }
    }

    public ICommand LoadCommand { get; }
    public ICommand SearchCommand { get; }

    private async Task LoadAnimalsAsync()
    {
        IsBusy = true;
        var animals = await _mediator.Send(new GetAnimalsQuery());
        Animals.Clear();
        foreach (var animal in animals)
            Animals.Add(MapToDisplay(animal));
        IsBusy = false;
    }

    private async Task SearchAnimalAsync()
    {
        var result = await _mediator.Send(new FindAnimalByNameQuery(SearchName));
        SearchResult = result.IsSuccess
            ? $"Found: {FormatAnimal(result.Value!)}"
            : $"Not found: {result.Error!.Message}";
    }

    private static AnimalDisplayModel MapToDisplay(Animal animal) => animal switch
    {
        Dog d => new AnimalDisplayModel(d.Name, "Dog", d.AgeYears, $"Breed: {d.Breed}"),
        Cat c => new AnimalDisplayModel(c.Name, "Cat", c.AgeYears, $"Indoor: {c.IsIndoor}"),
        Eagle e => new AnimalDisplayModel(e.Name, "Eagle", e.AgeYears, $"Wingspan: {e.WingspanMeters}m"),
        _ => new AnimalDisplayModel(animal.Name, animal.GetType().Name, animal.AgeYears, "")
    };

    private static string FormatAnimal(Animal animal) => animal switch
    {
        Dog d => $"{d.Name} — Dog, {d.AgeYears}y, breed: {d.Breed}",
        Cat c => $"{c.Name} — Cat, {c.AgeYears}y, indoor: {c.IsIndoor}",
        Eagle e => $"{e.Name} — Eagle, {e.AgeYears}y, wingspan: {e.WingspanMeters}m",
        _ => $"{animal.Name} — {animal.GetType().Name}, {animal.AgeYears}y"
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
