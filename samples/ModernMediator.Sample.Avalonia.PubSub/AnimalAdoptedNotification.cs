using ModernMediator.Sample.Console.Domain;

namespace ModernMediator.Sample.Avalonia.PubSub;

/// <summary>
/// Notification published when an animal is adopted.
/// The <see cref="OnAdoptionConfirmed"/> delegate is the CALLBACK pattern:
/// the publisher supplies it, and the handler invokes it after processing,
/// returning an <see cref="AdoptionReceipt"/> without knowing who the publisher is.
/// </summary>
public record AnimalAdoptedNotification(
    Animal Animal,
    string AdoptedBy,
    Func<AdoptionReceipt, Task>? OnAdoptionConfirmed = null) : INotification;

public record AdoptionReceipt(string ConfirmationNumber, DateTime AdoptedAt);
