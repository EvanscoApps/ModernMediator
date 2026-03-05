using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using ModernMediator.Sample.Console.Domain;

namespace ModernMediator.Sample.Wpf.PubSub;

public class ShelterViewModel : INotifyPropertyChanged
{
    private readonly IMediator _mediator;
    private string _adopterName = "";
    private AnimalDisplayModel? _selectedAnimal;
    private string _confirmationMessage = "";

    public ShelterViewModel(IMediator mediator)
    {
        _mediator = mediator;

        AvailableAnimals = new ObservableCollection<AnimalDisplayModel>
        {
            new(new Dog("Rex", 5, "German Shepherd")),
            new(new Dog("Bella", 3, "Labrador")),
            new(new Cat("Whiskers", 7, true)),
            new(new Cat("Shadow", 2, false)),
            new(new Eagle("Aquila", 12, 2.1)),
        };

        AdoptCommand = new RelayCommand(
            Adopt,
            _ => SelectedAnimal is not null && !string.IsNullOrWhiteSpace(AdopterName));
    }

    public ObservableCollection<AnimalDisplayModel> AvailableAnimals { get; }

    public string AdopterName
    {
        get => _adopterName;
        set
        {
            _adopterName = value;
            OnPropertyChanged();
            ((RelayCommand)AdoptCommand).RaiseCanExecuteChanged();
        }
    }

    public AnimalDisplayModel? SelectedAnimal
    {
        get => _selectedAnimal;
        set
        {
            _selectedAnimal = value;
            OnPropertyChanged();
            ((RelayCommand)AdoptCommand).RaiseCanExecuteChanged();
        }
    }

    public string ConfirmationMessage
    {
        get => _confirmationMessage;
        set { _confirmationMessage = value; OnPropertyChanged(); }
    }

    public ICommand AdoptCommand { get; }

    private void Adopt(object? parameter)
    {
        var animal = SelectedAnimal!;
        AvailableAnimals.Remove(animal);

        // Publish the adoption notification with a CALLBACK delegate.
        // This delegate is created here (the publish site) and will be invoked
        // by the handler after it processes the adoption. ShelterViewModel doesn't
        // know who handles the notification or how — it only knows that if the
        // handler calls OnAdoptionConfirmed, a receipt arrives.
        var notification = new AnimalAdoptedNotification(
            animal.ToAnimal(),
            AdopterName,
            async receipt =>
            {
                // CALLBACK — invoked by the handler after processing.
                // The handler knows nothing about ShelterViewModel; it just calls the delegate.
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    ConfirmationMessage = $"Confirmed! #{receipt.ConfirmationNumber} at {receipt.AdoptedAt:t}";
                });
            });

        _mediator.Publish(notification);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
