using ModernMediator.Sample.Shared.Domain;

namespace ModernMediator.Sample.Console.PubSub;

public class DogResponseHandler
{
    public void Handle(AnimalSightedNotification notification)
    {
        if (notification.Animal is not Dog) return;
        System.Console.WriteLine($"  \U0001F415 [Dog] {notification.Animal.Name} heard something at {notification.Location} and started barking!");
    }
}

public class CatResponseHandler
{
    public void Handle(AnimalSightedNotification notification)
    {
        if (notification.Animal is not Cat) return;
        System.Console.WriteLine($"  \U0001F408 [Cat] {notification.Animal.Name} ignored the commotion at {notification.Location} and went back to sleep.");
    }
}

public class EagleResponseHandler
{
    public void Handle(AnimalSightedNotification notification)
    {
        if (notification.Animal is not Eagle) return;
        System.Console.WriteLine($"  \U0001F985 [Eagle] {notification.Animal.Name} spotted movement at {notification.Location} and took flight!");
    }
}

public class AnimalSightingLogHandler
{
    public void Handle(AnimalSightedNotification notification)
    {
        System.Console.WriteLine($"  \U0001F4CB [Log] Sighting recorded: {notification.Animal.GetType().Name} named {notification.Animal.Name} at {notification.Location}");
    }
}
