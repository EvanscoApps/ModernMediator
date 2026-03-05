using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using ModernMediator.Sample.Console.Domain;

namespace ModernMediator.Sample.Wpf.PubSub;

public class AdoptionFeedViewModel : INotificationHandler<AnimalAdoptedNotification>, INotifyPropertyChanged
{
    private int _totalAdoptions;
    private string _lastAdoption = "";

    public ObservableCollection<string> AdoptionFeed { get; } = new();

    public int TotalAdoptions
    {
        get => _totalAdoptions;
        set { _totalAdoptions = value; OnPropertyChanged(); }
    }

    public string LastAdoption
    {
        get => _lastAdoption;
        set { _lastAdoption = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Handles the <see cref="AnimalAdoptedNotification"/>.
    /// This is the CALLBACK INVOCATION site — the handler generates an <see cref="AdoptionReceipt"/>
    /// and invokes <see cref="AnimalAdoptedNotification.OnAdoptionConfirmed"/> if the publisher
    /// supplied one. The handler has no knowledge of who is listening or what they do with the receipt.
    /// Neither ViewModel holds a reference to the other.
    /// </summary>
    public async Task Handle(AnimalAdoptedNotification notification, CancellationToken cancellationToken)
    {
        var receipt = new AdoptionReceipt(
            Guid.NewGuid().ToString("N")[..8].ToUpper(),
            DateTime.Now);

        // CALLBACK INVOCATION — the publisher supplied this delegate.
        // This handler has no knowledge of who is listening or what they do with the receipt.
        if (notification.OnAdoptionConfirmed is not null)
            await notification.OnAdoptionConfirmed(receipt);

        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var entry = $"{AnimalEmoji(notification.Animal)} {notification.Animal.Name} adopted by {notification.AdoptedBy}";
            AdoptionFeed.Insert(0, entry);
            TotalAdoptions++;
            LastAdoption = entry;
        });
    }

    private static string AnimalEmoji(Animal animal) => animal switch
    {
        Dog => "\U0001F415",
        Cat => "\U0001F408",
        Eagle => "\U0001F985",
        _ => "\U0001F43E"
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
