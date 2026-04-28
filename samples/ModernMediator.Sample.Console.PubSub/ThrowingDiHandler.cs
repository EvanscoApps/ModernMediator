using System;
using System.Threading;
using System.Threading.Tasks;
using ModernMediator;

namespace ModernMediator.Sample.Console.PubSub;

/// <summary>
/// A notification handler registered via DI that deliberately throws. Demonstrates the
/// DI path of the v2.2 unified error-handling surface: when this handler throws, the
/// mediator's HandlerError event fires with HandlerType pointing to this class and
/// HandlerInstance pointing to the resolved instance.
/// </summary>
public sealed class ThrowingDiHandler : INotificationHandler<ProblematicSightingNotification>
{
    public Task Handle(ProblematicSightingNotification notification, CancellationToken cancellationToken)
    {
        throw new InvalidOperationException(
            "ThrowingDiHandler refuses to handle '" + notification.Description + "'.");
    }
}
