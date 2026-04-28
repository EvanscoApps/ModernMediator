using ModernMediator;

namespace ModernMediator.Sample.Console.PubSub;

/// <summary>
/// A notification used by the v2.2 HandlerError demonstration. Two handlers subscribe to
/// this notification and deliberately throw; one is registered via DI, one via the
/// Subscribe callback path. The mediator's HandlerError event fires for both, and the
/// custom ISubscriberExceptionSink observes any subscriber that itself throws.
/// </summary>
public sealed class ProblematicSightingNotification : INotification
{
    public ProblematicSightingNotification(string description)
    {
        Description = description;
    }

    public string Description { get; }
}
