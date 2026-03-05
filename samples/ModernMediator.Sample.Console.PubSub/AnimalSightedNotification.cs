using ModernMediator.Sample.Console.Domain;

namespace ModernMediator.Sample.Console.PubSub;

public record AnimalSightedNotification(Animal Animal, string Location) : INotification;
