using ModernMediator.Sample.Shared.Domain;

namespace ModernMediator.Sample.Console.PubSub;

public record AnimalSightedNotification(Animal Animal, string Location) : INotification;
