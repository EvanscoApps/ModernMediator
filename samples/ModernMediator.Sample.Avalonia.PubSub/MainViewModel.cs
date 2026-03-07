namespace ModernMediator.Sample.Avalonia.PubSub;

public class MainViewModel
{
    public ShelterViewModel Shelter { get; }
    public AdoptionFeedViewModel AdoptionFeed { get; }

    public MainViewModel(ShelterViewModel shelter, AdoptionFeedViewModel adoptionFeed)
    {
        Shelter = shelter;
        AdoptionFeed = adoptionFeed;
    }
}
